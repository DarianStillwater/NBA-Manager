using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Simulates a complete basketball game using the possession simulator.
    /// Manages quarters, substitutions, fouls, and generates box scores.
    /// </summary>
    public class GameSimulator
    {
        private const int QUARTER_LENGTH_SECONDS = 720; // 12 minutes
        private const int OVERTIME_LENGTH_SECONDS = 300; // 5 minutes
        private const int REGULAR_QUARTERS = 4;

        private PossessionSimulator _possessionSimulator;
        private PlayerDatabase _playerDatabase;
        
        private Team _homeTeam;
        private Team _awayTeam;
        private BoxScore _boxScore;
        private SpatialTracker _gameTracker;
        
        private int _currentQuarter;
        private float _gameClock;
        private bool _homeHasPossession;

        public BoxScore BoxScore => _boxScore;
        public SpatialTracker GameTracker => _gameTracker;

        public GameSimulator(PlayerDatabase playerDatabase, int? seed = null)
        {
            _playerDatabase = playerDatabase;
            _possessionSimulator = new PossessionSimulator(seed);
            _gameTracker = new SpatialTracker();
        }

        /// <summary>
        /// Simulates a complete game between two teams.
        /// </summary>
        public GameResult SimulateGame(Team homeTeam, Team awayTeam)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _boxScore = new BoxScore(homeTeam.TeamId, awayTeam.TeamId);
            _gameTracker.Clear();

            // Initialize player stats
            InitializePlayerStats(homeTeam);
            InitializePlayerStats(awayTeam);

            // Simulate 4 regular quarters
            int quartersPlayed = 0;
            for (int q = 1; q <= REGULAR_QUARTERS; q++)
            {
                _currentQuarter = q;
                SimulateQuarter(QUARTER_LENGTH_SECONDS);
                quartersPlayed = q;
            }

            // Handle overtime if tied
            while (_boxScore.HomeScore == _boxScore.AwayScore)
            {
                quartersPlayed++;
                _currentQuarter = quartersPlayed;
                SimulateQuarter(OVERTIME_LENGTH_SECONDS);
            }

            return new GameResult
            {
                HomeTeamId = homeTeam.TeamId,
                AwayTeamId = awayTeam.TeamId,
                HomeScore = _boxScore.HomeScore,
                AwayScore = _boxScore.AwayScore,
                BoxScore = _boxScore,
                Quarters = quartersPlayed
            };
        }

        /// <summary>
        /// Simulates a single quarter.
        /// </summary>
        private void SimulateQuarter(int quarterLengthSeconds)
        {
            _gameClock = quarterLengthSeconds;
            _homeHasPossession = _currentQuarter % 2 == 1; // Home starts Q1/Q3, Away starts Q2/Q4

            while (_gameClock > 0)
            {
                // Get active players
                var offensePlayers = GetActivePlayers(_homeHasPossession ? _homeTeam : _awayTeam);
                var defensePlayers = GetActivePlayers(_homeHasPossession ? _awayTeam : _homeTeam);
                var offenseStrategy = _homeHasPossession ? _homeTeam.OffensiveStrategy : _awayTeam.OffensiveStrategy;
                var defenseStrategy = _homeHasPossession ? _awayTeam.DefensiveStrategy : _homeTeam.DefensiveStrategy;

                // Simulate possession
                var result = _possessionSimulator.SimulatePossession(
                    offensePlayers,
                    defensePlayers,
                    offenseStrategy,
                    defenseStrategy,
                    _gameClock,
                    _currentQuarter,
                    _homeHasPossession
                );

                // Record stats
                ProcessPossessionResult(result, _homeHasPossession);

                // Add spatial states to game tracker
                foreach (var state in result.SpatialStates)
                {
                    _gameTracker.RecordState(state);
                }

                // Update game clock
                _gameClock = result.EndGameClock;

                // Possession ALWAYS changes after any possession ends
                _homeHasPossession = !_homeHasPossession;

                // Energy drain for active players
                DrainEnergy(offensePlayers, result.Duration * 0.2f);
                DrainEnergy(defensePlayers, result.Duration * 0.15f);
            }
        }

        /// <summary>
        /// Gets the 5 active players for a team.
        /// </summary>
        private Player[] GetActivePlayers(Team team)
        {
            var players = new Player[5];
            for (int i = 0; i < 5; i++)
            {
                players[i] = _playerDatabase.GetPlayer(team.StartingLineupIds[i]);
            }
            return players;
        }

        /// <summary>
        /// Initializes stat tracking for all players.
        /// </summary>
        private void InitializePlayerStats(Team team)
        {
            foreach (var playerId in team.RosterPlayerIds)
            {
                _boxScore.InitializePlayer(playerId);
            }
        }

        /// <summary>
        /// Processes a possession result to update box score.
        /// </summary>
        private void ProcessPossessionResult(PossessionResult result, bool isHomePossession)
        {
            string teamId = isHomePossession ? _homeTeam.TeamId : _awayTeam.TeamId;

            foreach (var evt in result.Events)
            {
                switch (evt.Type)
                {
                    case EventType.Shot:
                        ProcessShot(evt, teamId);
                        break;
                    case EventType.Steal:
                        ProcessSteal(evt);
                        break;
                    case EventType.Block:
                        ProcessBlock(evt);
                        break;
                    case EventType.Rebound:
                        ProcessRebound(evt, teamId);
                        break;
                    case EventType.Pass when evt.Outcome == EventOutcome.Success:
                        // Potential assist - tracked when shot made
                        break;
                    case EventType.Turnover:
                        ProcessTurnover(evt);
                        break;
                }
            }

            // Add points to team score
            if (result.PointsScored > 0)
            {
                if (isHomePossession)
                    _boxScore.HomeScore += result.PointsScored;
                else
                    _boxScore.AwayScore += result.PointsScored;

                // Find the assist (pass before made shot)
                var shotEvent = result.Events.LastOrDefault(e => e.Type == EventType.Shot);
                if (shotEvent != null)
                {
                    var passEvent = result.Events.LastOrDefault(e => 
                        e.Type == EventType.Pass && 
                        e.TargetPlayerId == shotEvent.ActorPlayerId &&
                        e.Outcome == EventOutcome.Success);
                    
                    if (passEvent != null)
                    {
                        _boxScore.AddAssist(passEvent.ActorPlayerId);
                    }
                }
            }
        }

        private void ProcessShot(PossessionEvent evt, string teamId)
        {
            var zone = evt.ActorPosition.GetZone(true);
            bool isThree = zone == CourtZone.ThreePoint;
            bool made = evt.Outcome == EventOutcome.Success;

            _boxScore.AddShotAttempt(evt.ActorPlayerId, isThree);
            if (made)
            {
                _boxScore.AddShotMade(evt.ActorPlayerId, isThree, evt.PointsScored);
            }
        }

        private void ProcessSteal(PossessionEvent evt)
        {
            _boxScore.AddSteal(evt.ActorPlayerId);
            if (evt.TargetPlayerId != null)
            {
                _boxScore.AddTurnover(evt.TargetPlayerId);
            }
        }

        private void ProcessBlock(PossessionEvent evt)
        {
            _boxScore.AddBlock(evt.DefenderPlayerId);
        }

        private void ProcessRebound(PossessionEvent evt, string teamId)
        {
            // Determine if offensive or defensive
            // For simplicity, we'll mark based on team context
            _boxScore.AddRebound(evt.ActorPlayerId, isOffensive: false);
        }

        private void ProcessTurnover(PossessionEvent evt)
        {
            _boxScore.AddTurnover(evt.ActorPlayerId);
        }

        private bool CheckForFreeThrows(PossessionResult result)
        {
            // Simplified: no free throws in this version
            return false;
        }

        private void DrainEnergy(Player[] players, float amount)
        {
            foreach (var player in players)
            {
                player.ConsumeEnergy(amount);
            }
        }
    }

    /// <summary>
    /// Complete box score for a game.
    /// </summary>
    [Serializable]
    public class BoxScore
    {
        public string HomeTeamId;
        public string AwayTeamId;
        public int HomeScore;
        public int AwayScore;
        public Dictionary<string, PlayerGameStats> PlayerStats = new Dictionary<string, PlayerGameStats>();

        public BoxScore(string homeId, string awayId)
        {
            HomeTeamId = homeId;
            AwayTeamId = awayId;
        }

        public void InitializePlayer(string playerId)
        {
            PlayerStats[playerId] = new PlayerGameStats { PlayerId = playerId };
        }

        public void AddShotAttempt(string playerId, bool isThree)
        {
            if (!PlayerStats.ContainsKey(playerId)) return;
            if (isThree)
                PlayerStats[playerId].ThreePointAttempts++;
            else
                PlayerStats[playerId].FieldGoalAttempts++;
        }

        public void AddShotMade(string playerId, bool isThree, int points)
        {
            if (!PlayerStats.ContainsKey(playerId)) return;
            if (isThree)
                PlayerStats[playerId].ThreePointMade++;
            else
                PlayerStats[playerId].FieldGoalsMade++;
            PlayerStats[playerId].Points += points;
        }

        public void AddAssist(string playerId)
        {
            if (PlayerStats.ContainsKey(playerId))
                PlayerStats[playerId].Assists++;
        }

        public void AddRebound(string playerId, bool isOffensive)
        {
            if (!PlayerStats.ContainsKey(playerId)) return;
            if (isOffensive)
                PlayerStats[playerId].OffensiveRebounds++;
            else
                PlayerStats[playerId].DefensiveRebounds++;
        }

        public void AddSteal(string playerId)
        {
            if (PlayerStats.ContainsKey(playerId))
                PlayerStats[playerId].Steals++;
        }

        public void AddBlock(string playerId)
        {
            if (PlayerStats.ContainsKey(playerId))
                PlayerStats[playerId].Blocks++;
        }

        public void AddTurnover(string playerId)
        {
            if (PlayerStats.ContainsKey(playerId))
                PlayerStats[playerId].Turnovers++;
        }

        public void AddFoul(string playerId)
        {
            if (PlayerStats.ContainsKey(playerId))
                PlayerStats[playerId].PersonalFouls++;
        }

        /// <summary>
        /// Generates a formatted box score string.
        /// </summary>
        public string ToFormattedString(Func<string, string> getPlayerName)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"FINAL SCORE: {HomeTeamId} {HomeScore} - {AwayScore} {AwayTeamId}");
            sb.AppendLine();
            sb.AppendLine("PLAYER          PTS  FG     3PT    REB  AST  STL  BLK  TO");
            sb.AppendLine("--------------------------------------------------------------");

            foreach (var stats in PlayerStats.Values.OrderByDescending(s => s.Points))
            {
                string name = getPlayerName(stats.PlayerId).PadRight(15).Substring(0, 15);
                string fg = $"{stats.FieldGoalsMade}/{stats.TotalFGA}".PadRight(6);
                string threes = $"{stats.ThreePointMade}/{stats.ThreePointAttempts}".PadRight(6);
                
                sb.AppendLine($"{name} {stats.Points,3}  {fg} {threes} {stats.TotalRebounds,3}  {stats.Assists,3}  {stats.Steals,3}  {stats.Blocks,3}  {stats.Turnovers,2}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Individual player stats for a game.
    /// </summary>
    [Serializable]
    public class PlayerGameStats
    {
        public string PlayerId;
        public int Minutes;
        public int Points;
        public int FieldGoalsMade;
        public int FieldGoalAttempts;
        public int ThreePointMade;
        public int ThreePointAttempts;
        public int FreeThrowsMade;
        public int FreeThrowAttempts;
        public int OffensiveRebounds;
        public int DefensiveRebounds;
        public int Assists;
        public int Steals;
        public int Blocks;
        public int Turnovers;
        public int PersonalFouls;
        public int PlusMinus;

        public int TotalRebounds => OffensiveRebounds + DefensiveRebounds;
        public int TotalFGA => FieldGoalAttempts + ThreePointAttempts;
        public int TotalFGM => FieldGoalsMade + ThreePointMade;
        public float FieldGoalPercentage => TotalFGA > 0 ? (float)TotalFGM / TotalFGA * 100f : 0f;
        public float ThreePointPercentage => ThreePointAttempts > 0 ? (float)ThreePointMade / ThreePointAttempts * 100f : 0f;
    }

    /// <summary>
    /// Complete result of a simulated game.
    /// </summary>
    public class GameResult
    {
        public string HomeTeamId;
        public string AwayTeamId;
        public int HomeScore;
        public int AwayScore;
        public BoxScore BoxScore;
        public int Quarters;
        public bool WentToOvertime => Quarters > 4;
        public string WinnerTeamId => HomeScore > AwayScore ? HomeTeamId : AwayTeamId;
    }
}
