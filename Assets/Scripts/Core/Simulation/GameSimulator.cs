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
        private FoulSystem _foulSystem;
        private FreeThrowHandler _freeThrowHandler;

        private Team _homeTeam;
        private Team _awayTeam;
        private BoxScore _boxScore;
        private SpatialTracker _gameTracker;

        private int _currentQuarter;
        private float _gameClock;
        private bool _homeHasPossession;

        public BoxScore BoxScore => _boxScore;
        public SpatialTracker GameTracker => _gameTracker;
        public FoulSystem FoulSystem => _foulSystem;

        public GameSimulator(PlayerDatabase playerDatabase, int? seed = null)
        {
            _playerDatabase = playerDatabase;
            _foulSystem = new FoulSystem();
            _freeThrowHandler = new FreeThrowHandler();
            _possessionSimulator = new PossessionSimulator(seed, _foulSystem);
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

            // Reset foul system for new game
            _foulSystem.ResetGame();

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

            var result = new GameResult
            {
                HomeTeamId = homeTeam.TeamId,
                AwayTeamId = awayTeam.TeamId,
                HomeScore = _boxScore.HomeScore,
                AwayScore = _boxScore.AwayScore,
                BoxScore = _boxScore,
                Quarters = quartersPlayed
            };

            return result;
        }

        /// <summary>
        /// Creates GameLog entries for all players and adds them to their career stats.
        /// Call this after SimulateGame to record the game in player histories.
        /// </summary>
        public void RecordGameToPlayerStats(
            GameResult result,
            string gameId,
            DateTime gameDate,
            bool isPlayoff = false,
            int playoffRound = 0)
        {
            // Process home team players
            RecordTeamGameLogs(
                result.BoxScore.HomeTeamId,
                result.BoxScore.AwayTeamId,
                result,
                gameId,
                gameDate,
                isHome: true,
                isPlayoff,
                playoffRound
            );

            // Process away team players
            RecordTeamGameLogs(
                result.BoxScore.AwayTeamId,
                result.BoxScore.HomeTeamId,
                result,
                gameId,
                gameDate,
                isHome: false,
                isPlayoff,
                playoffRound
            );
        }

        private void RecordTeamGameLogs(
            string teamId,
            string opponentTeamId,
            GameResult result,
            string gameId,
            DateTime gameDate,
            bool isHome,
            bool isPlayoff,
            int playoffRound)
        {
            int teamScore = isHome ? result.HomeScore : result.AwayScore;
            int opponentScore = isHome ? result.AwayScore : result.HomeScore;
            bool wasOvertime = result.WentToOvertime;
            int overtimePeriods = result.Quarters - 4;

            // Get the player stats list for this team
            var playerStatsList = isHome
                ? result.BoxScore.HomePlayerStats
                : result.BoxScore.AwayPlayerStats;

            // Also check the dictionary for any players not in the lists
            var allPlayerIds = new HashSet<string>();
            foreach (var ps in playerStatsList)
            {
                allPlayerIds.Add(ps.PlayerId);
            }
            foreach (var kvp in result.BoxScore.PlayerStats)
            {
                allPlayerIds.Add(kvp.Key);
            }

            // Get active lineup for this team to determine starters
            var team = isHome ? _homeTeam : _awayTeam;
            var starterIds = team?.StartingLineupIds ?? new List<string>();

            foreach (var playerId in allPlayerIds)
            {
                var player = _playerDatabase?.GetPlayer(playerId);
                if (player == null || player.TeamId != teamId) continue;

                var stats = result.BoxScore.PlayerStats.TryGetValue(playerId, out var ps)
                    ? ps
                    : playerStatsList.FirstOrDefault(p => p.PlayerId == playerId);

                if (stats == null) continue;

                // Skip players who didn't play (0 minutes)
                if (stats.Minutes <= 0) continue;

                bool started = starterIds.Contains(playerId);

                var gameLog = GameLog.Create(
                    gameId: gameId,
                    date: gameDate,
                    opponentTeamId: opponentTeamId,
                    isHome: isHome,
                    isPlayoff: isPlayoff,
                    playoffRound: playoffRound,
                    minutes: stats.Minutes,
                    started: started,
                    points: stats.Points,
                    fgm: stats.FieldGoalsMade,
                    fga: stats.FieldGoalAttempts,
                    threePm: stats.ThreePointMade,
                    threePa: stats.ThreePointAttempts,
                    ftm: stats.FreeThrowsMade,
                    fta: stats.FreeThrowAttempts,
                    orb: stats.OffensiveRebounds,
                    drb: stats.DefensiveRebounds,
                    assists: stats.Assists,
                    steals: stats.Steals,
                    blocks: stats.Blocks,
                    turnovers: stats.Turnovers,
                    fouls: stats.PersonalFouls,
                    plusMinus: stats.PlusMinus,
                    teamScore: teamScore,
                    opponentScore: opponentScore,
                    wasOvertime: wasOvertime,
                    overtimePeriods: overtimePeriods > 0 ? overtimePeriods : 0
                );

                // Add to player's current season stats
                if (player.CurrentSeasonStats != null)
                {
                    player.CurrentSeasonStats.AddGameFromLog(gameLog);
                }
            }
        }

        /// <summary>
        /// Simulates a single quarter.
        /// </summary>
        private void SimulateQuarter(int quarterLengthSeconds)
        {
            _gameClock = quarterLengthSeconds;
            _homeHasPossession = _currentQuarter % 2 == 1; // Home starts Q1/Q3, Away starts Q2/Q4

            // Reset team fouls at the start of each quarter
            _foulSystem.ResetQuarterFouls();

            while (_gameClock > 0)
            {
                // Get active players
                var offensePlayers = GetActivePlayers(_homeHasPossession ? _homeTeam : _awayTeam);
                var defensePlayers = GetActivePlayers(_homeHasPossession ? _awayTeam : _homeTeam);
                var offenseStrategy = _homeHasPossession ? _homeTeam.OffensiveStrategy : _awayTeam.OffensiveStrategy;
                var defenseStrategy = _homeHasPossession ? _awayTeam.DefensiveStrategy : _homeTeam.DefensiveStrategy;

                // Determine team IDs for this possession
                string offenseTeamId = _homeHasPossession ? _homeTeam.TeamId : _awayTeam.TeamId;
                string defenseTeamId = _homeHasPossession ? _awayTeam.TeamId : _homeTeam.TeamId;
                int scoreDifferential = _homeHasPossession
                    ? _boxScore.HomeScore - _boxScore.AwayScore
                    : _boxScore.AwayScore - _boxScore.HomeScore;

                // Simulate possession
                var result = _possessionSimulator.SimulatePossession(
                    offensePlayers,
                    defensePlayers,
                    offenseStrategy,
                    defenseStrategy,
                    _gameClock,
                    _currentQuarter,
                    _homeHasPossession,
                    offenseTeamId,
                    defenseTeamId,
                    scoreDifferential
                );

                // Record stats
                ProcessPossessionResult(result, _homeHasPossession);

                // Process any free throws from foul events
                ProcessFreeThrows(result, _homeHasPossession);

                // Add spatial states to game tracker
                foreach (var state in result.SpatialStates)
                {
                    _gameTracker.RecordState(state);
                }

                // Update game clock
                _gameClock = result.EndGameClock;

                // Possession changes unless free throws retain possession (flagrant, technical)
                bool retainsPossession = CheckRetainsPossession(result);
                if (!retainsPossession)
                {
                    _homeHasPossession = !_homeHasPossession;
                }

                // Energy drain for active players
                DrainEnergy(offensePlayers, result.Duration * 0.2f);
                DrainEnergy(defensePlayers, result.Duration * 0.15f);
            }
        }

        /// <summary>
        /// Check if the offensive team retains possession (flagrant fouls, technicals).
        /// </summary>
        private bool CheckRetainsPossession(PossessionResult result)
        {
            foreach (var evt in result.Events)
            {
                if (evt.Type == EventType.Foul && evt.FoulDetail != null)
                {
                    // Flagrant fouls and technicals give ball back to fouled team
                    if (evt.FoulDetail.FoulType == FoulType.Flagrant1 ||
                        evt.FoulDetail.FoulType == FoulType.Flagrant2 ||
                        evt.FoulDetail.FoulType == FoulType.Technical)
                    {
                        return true;
                    }
                }
            }
            return false;
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
            string offenseTeamId = isHomePossession ? _homeTeam.TeamId : _awayTeam.TeamId;
            string defenseTeamId = isHomePossession ? _awayTeam.TeamId : _homeTeam.TeamId;

            foreach (var evt in result.Events)
            {
                switch (evt.Type)
                {
                    case EventType.Shot:
                        ProcessShot(evt, offenseTeamId);
                        break;
                    case EventType.Steal:
                        ProcessSteal(evt);
                        break;
                    case EventType.Block:
                        ProcessBlock(evt);
                        break;
                    case EventType.Rebound:
                        ProcessRebound(evt, offenseTeamId);
                        break;
                    case EventType.Pass when evt.Outcome == EventOutcome.Success:
                        // Potential assist - tracked when shot made
                        break;
                    case EventType.Turnover:
                        ProcessTurnover(evt);
                        break;
                    case EventType.Foul:
                        ProcessFoul(evt, defenseTeamId);
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

        /// <summary>
        /// Process foul events and update box score.
        /// </summary>
        private void ProcessFoul(PossessionEvent evt, string defenseTeamId)
        {
            if (evt.FoulDetail == null) return;

            // Record personal foul in box score
            if (evt.DefenderPlayerId != null &&
                evt.FoulDetail.FoulType != FoulType.Technical)
            {
                _boxScore.AddFoul(evt.DefenderPlayerId);
            }

            // Track team fouls in box score
            _boxScore.AddTeamFoul(defenseTeamId, _currentQuarter);
        }

        /// <summary>
        /// Process free throws from foul events.
        /// </summary>
        private void ProcessFreeThrows(PossessionResult result, bool isHomePossession)
        {
            foreach (var evt in result.Events)
            {
                if (evt.Type == EventType.Foul && evt.FoulDetail != null &&
                    evt.FoulDetail.FreeThrowsAwarded > 0)
                {
                    // Get the fouled player (shooter)
                    var shooter = _playerDatabase?.GetPlayer(evt.ActorPlayerId);
                    if (shooter == null) continue;

                    // Create game context for free throw calculation
                    var context = new GameContext
                    {
                        Quarter = _currentQuarter,
                        GameClock = _gameClock,
                        ScoreDifferential = isHomePossession
                            ? _boxScore.HomeScore - _boxScore.AwayScore
                            : _boxScore.AwayScore - _boxScore.HomeScore,
                        IsClutchTime = _currentQuarter >= 4 && _gameClock <= 300 &&
                            Mathf.Abs(_boxScore.HomeScore - _boxScore.AwayScore) <= 5,
                        TimeoutJustCalled = false
                    };

                    // Calculate free throw results
                    var ftResult = _freeThrowHandler.CalculateFreeThrows(
                        shooter,
                        evt.FoulDetail.FreeThrowsAwarded,
                        context
                    );

                    // Update box score with free throw stats
                    _boxScore.AddFreeThrows(shooter.PlayerId, ftResult.Made, ftResult.Attempts);

                    // Add points to team score
                    if (ftResult.Made > 0)
                    {
                        if (isHomePossession)
                            _boxScore.HomeScore += ftResult.Made;
                        else
                            _boxScore.AwayScore += ftResult.Made;
                    }

                    // Store result in event for play-by-play
                    evt.FreeThrowResult = ftResult;
                }
            }
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

        // Team references for UI compatibility
        public Team HomeTeam;
        public Team AwayTeam;

        // Player stats lists for UI compatibility
        public List<PlayerGameStats> HomePlayerStats = new List<PlayerGameStats>();
        public List<PlayerGameStats> AwayPlayerStats = new List<PlayerGameStats>();

        // Quarter scores for UI
        public List<int> QuarterScoresHome = new List<int>();
        public List<int> QuarterScoresAway = new List<int>();

        // Overtime tracking
        public bool WentToOvertime;
        public int OvertimePeriods;

        public BoxScore() { }

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

        public void AddFreeThrows(string playerId, int made, int attempts)
        {
            if (!PlayerStats.ContainsKey(playerId)) return;
            PlayerStats[playerId].FreeThrowsMade += made;
            PlayerStats[playerId].FreeThrowAttempts += attempts;
            PlayerStats[playerId].Points += made;
        }

        // Team foul tracking per quarter
        private Dictionary<string, Dictionary<int, int>> _teamFoulsPerQuarter = new Dictionary<string, Dictionary<int, int>>();

        public void AddTeamFoul(string teamId, int quarter)
        {
            if (!_teamFoulsPerQuarter.ContainsKey(teamId))
                _teamFoulsPerQuarter[teamId] = new Dictionary<int, int>();
            if (!_teamFoulsPerQuarter[teamId].ContainsKey(quarter))
                _teamFoulsPerQuarter[teamId][quarter] = 0;
            _teamFoulsPerQuarter[teamId][quarter]++;
        }

        public int GetTeamFouls(string teamId, int quarter)
        {
            if (!_teamFoulsPerQuarter.ContainsKey(teamId)) return 0;
            if (!_teamFoulsPerQuarter[teamId].ContainsKey(quarter)) return 0;
            return _teamFoulsPerQuarter[teamId][quarter];
        }

        public bool IsInBonus(string teamId, int quarter)
        {
            return GetTeamFouls(teamId, quarter) >= 5;
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
        public string PlayerName;  // For UI display
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

        // Aliases for UI compatibility
        public int Rebounds => TotalRebounds;
        public int FieldGoalsAttempted => TotalFGA;
        public int ThreePointersMade => ThreePointMade;
        public int ThreePointersAttempted => ThreePointAttempts;
        public int FreeThrowsAttempted => FreeThrowAttempts;

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
