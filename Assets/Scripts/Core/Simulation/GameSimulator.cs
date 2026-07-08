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

        // Rotation tracking
        private const float SUB_ENERGY_THRESHOLD = 55f;  // Sub out when energy drops below this
        private const float BENCH_ENERGY_RECOVERY = 0.8f; // Energy recovered per second on bench
        private string[] _homeOnCourt = new string[5];
        private string[] _awayOnCourt = new string[5];

        public BoxScore BoxScore => _boxScore;
        public SpatialTracker GameTracker => _gameTracker;
        public FoulSystem FoulSystem => _foulSystem;

        public GameSimulator(PlayerDatabase playerDatabase, int? seed = null)
        {
            _playerDatabase = playerDatabase;
            _foulSystem = new FoulSystem();
            _freeThrowHandler = new FreeThrowHandler();
            _possessionSimulator = new PossessionSimulator(seed, _foulSystem);
            // Headless full-game sims need no spatial choreography (league auto-sim runs 15 games/day)
            _possessionSimulator.SpatialDetail = Choreography.SpatialDetailLevel.None;
            _gameTracker = new SpatialTracker();
        }

        /// <summary>
        /// Simulates a complete game between two teams.
        /// </summary>
        /// <summary>
        /// Simulate an exhibition (All-Star game, summer league, camp scrimmage):
        /// the full live sim with a box score, but nothing leaks into the season —
        /// no energy drain on the real players, no DNP-rest (playoff availability
        /// rules), and the caller must NOT route the result through the
        /// GameCompletionPipeline.
        /// </summary>
        public GameResult SimulateExhibition(Team homeTeam, Team awayTeam)
        {
            // Snapshot energy so the exhibition costs the real season nothing
            var energySnapshot = new Dictionary<string, float>();
            foreach (var pid in homeTeam.RosterPlayerIds.Concat(awayTeam.RosterPlayerIds))
            {
                if (string.IsNullOrEmpty(pid)) continue;
                var p = _playerDatabase.GetPlayer(pid);
                if (p != null && !energySnapshot.ContainsKey(pid)) energySnapshot[pid] = p.Energy;
            }

            var result = SimulateGame(homeTeam, awayTeam, isPlayoff: true);

            foreach (var kv in energySnapshot)
            {
                var p = _playerDatabase.GetPlayer(kv.Key);
                if (p != null) p.Energy = kv.Value;
            }

            return result;
        }

        public GameResult SimulateGame(Team homeTeam, Team awayTeam, bool isPlayoff = false)
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

            // Energy carries over between games — players tip off with whatever the
            // season (rest days, back-to-backs) left them. Guard only against raw
            // never-initialized Players from tools/tests.
            foreach (var pid in homeTeam.RosterPlayerIds.Concat(awayTeam.RosterPlayerIds))
            {
                if (string.IsNullOrEmpty(pid)) continue;
                var p = _playerDatabase.GetPlayer(pid);
                if (p != null && p.Energy <= 0f) p.Energy = 100f;
            }

            // Game-day availability: injuries always sit; gassed players get a
            // rest night in the regular season (never in the playoffs).
            BuildAvailability(homeTeam, _homeUnavailable, isPlayoff);
            BuildAvailability(awayTeam, _awayUnavailable, isPlayoff);

            // Initialize on-court lineups (starters) — fill nulls from roster
            SetupLineup(_homeOnCourt, homeTeam);
            SetupLineup(_awayOnCourt, awayTeam);

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
        /// Delegating wrapper over GameStatRecorder (the shared implementation all sim
        /// paths use) — kept for existing call sites and tests.
        /// </summary>
        public void RecordGameToPlayerStats(
            GameResult result,
            string gameId,
            DateTime gameDate,
            bool isPlayoff = false,
            int playoffRound = 0)
        {
            GameStatRecorder.Record(result, gameId, gameDate, _playerDatabase,
                _homeTeam, _awayTeam, isPlayoff, playoffRound);
        }

        /// <summary>
        /// Simulates a single quarter.
        /// </summary>
        private void SimulateQuarter(int quarterLengthSeconds)
        {
            _gameClock = quarterLengthSeconds;
            _homeHasPossession = _currentQuarter % 2 == 1; // Home starts Q1/Q3, Away starts Q2/Q4
            bool liveBallStart = false; // quarters open with a dead-ball inbound

            // Reset team fouls at the start of each quarter
            _foulSystem.ResetQuarterFouls();

            while (_gameClock > 0)
            {
                // Get active players
                var offensePlayers = GetActivePlayers(_homeHasPossession ? _homeTeam : _awayTeam);
                var defensePlayers = GetActivePlayers(_homeHasPossession ? _awayTeam : _homeTeam);
                // One strategy object per team is the authority for BOTH ends
                // (Team.Strategy alias) — the defending team's DefensiveSystem lives
                // on it. Matches the interactive path; DefensiveStrategy is legacy.
                var offenseStrategy = _homeHasPossession ? _homeTeam.OffensiveStrategy : _awayTeam.OffensiveStrategy;
                var defenseStrategy = _homeHasPossession ? _awayTeam.OffensiveStrategy : _homeTeam.OffensiveStrategy;

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
                    scoreDifferential,
                    liveBallStart
                );

                // Record stats
                ProcessPossessionResult(result, _homeHasPossession);

                // Process any free throws from foul events
                ProcessFreeThrows(result, _homeHasPossession);

                // Add spatial states to game tracker (empty at SpatialDetail.None)
                if (result.SpatialStates != null)
                {
                    foreach (var state in result.SpatialStates)
                    {
                        _gameTracker.RecordState(state);
                    }
                }

                // Update game clock
                _gameClock = result.EndGameClock;

                // Possession changes unless free throws retain possession (flagrant, technical)
                bool retainsPossession = CheckRetainsPossession(result);
                if (!retainsPossession)
                {
                    _homeHasPossession = !_homeHasPossession;
                }

                // Transition logic: the NEXT possession starts live off a defensive
                // board or a steal — that's when fast breaks happen
                liveBallStart = result.Outcome == PossessionOutcome.Miss ||
                                result.Outcome == PossessionOutcome.Block ||
                                (result.Outcome == PossessionOutcome.Turnover &&
                                 result.Events.Any(e => e.Type == EventType.Steal));

                // Energy drain for active players
                DrainEnergy(offensePlayers, result.Duration * 0.35f);
                DrainEnergy(defensePlayers, result.Duration * 0.3f);

                // Track seconds for on-court players (converted to minutes at end)
                foreach (var p in offensePlayers.Concat(defensePlayers))
                {
                    if (_boxScore.PlayerStats.ContainsKey(p.PlayerId))
                        _boxScore.PlayerStats[p.PlayerId].SecondsPlayed += result.Duration;
                }

                // Bench recovery
                RecoverBenchEnergy(_homeTeam, result.Duration);
                RecoverBenchEnergy(_awayTeam, result.Duration);

                // Check foul-outs first (mandatory subs)
                CheckFoulOuts(_homeTeam);
                CheckFoulOuts(_awayTeam);

                // Check substitutions after each possession
                CheckSubstitutions(_homeTeam);
                CheckSubstitutions(_awayTeam);
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

        // ==================== GAME-DAY AVAILABILITY ====================

        private const float REST_ENERGY_HARD = 35f;   // this gassed, anyone sits
        private const float REST_ENERGY_SOFT = 50f;   // cautious coaches rest below this
        private const int MIN_AVAILABLE = 8;

        private readonly HashSet<string> _homeUnavailable = new HashSet<string>();
        private readonly HashSet<string> _awayUnavailable = new HashSet<string>();

        private HashSet<string> UnavailableFor(Team team) =>
            team.TeamId == _homeTeam.TeamId ? _homeUnavailable : _awayUnavailable;

        /// <summary>
        /// Who dresses tonight. Injured players never play; exhausted players get
        /// a DNP-rest in the regular season (coaches with a high load-management
        /// tendency rest merely-tired players too). Never below 8 available —
        /// rested players get un-rested freshest-first; injuries stay out.
        /// </summary>
        private void BuildAvailability(Team team, HashSet<string> unavailable, bool isPlayoff)
        {
            unavailable.Clear();
            var rosterIds = team.RosterPlayerIds?.Where(id => !string.IsNullOrEmpty(id)).ToList()
                            ?? new List<string>();

            int loadTendency = team.CoachPersonality?.LoadManagementTendency ?? 40;

            foreach (var id in rosterIds)
            {
                var p = _playerDatabase.GetPlayer(id);
                if (p == null) continue;

                if (p.IsInjured) { unavailable.Add(id); continue; }
                if (isPlayoff) continue; // nobody rests in the playoffs

                if (p.Energy < REST_ENERGY_HARD) unavailable.Add(id);
                else if (p.Energy < REST_ENERGY_SOFT && loadTendency >= 60) unavailable.Add(id);
            }

            int available = rosterIds.Count(id => !unavailable.Contains(id));
            if (available >= MIN_AVAILABLE) return;

            var unrest = rosterIds
                .Where(id => unavailable.Contains(id))
                .Select(id => _playerDatabase.GetPlayer(id))
                .Where(p => p != null && !p.IsInjured)
                .OrderByDescending(p => p.Energy)
                .ToList();

            foreach (var p in unrest)
            {
                if (available >= MIN_AVAILABLE) break;
                unavailable.Remove(p.PlayerId);
                available++;
            }
        }

        /// <summary>
        /// Sets up the 5-player on-court lineup, falling back to roster if StartingLineupIds has nulls.
        /// </summary>
        private void SetupLineup(string[] onCourt, Team team)
        {
            var unavailable = UnavailableFor(team);
            var starters = team.StartingLineupIds;
            var rosterIds = team.RosterPlayerIds?.Where(id => !string.IsNullOrEmpty(id) && !unavailable.Contains(id)).ToList()
                            ?? new List<string>();
            var used = new HashSet<string>();

            for (int i = 0; i < 5; i++)
            {
                if (starters != null && i < starters.Length && !string.IsNullOrEmpty(starters[i]) &&
                    !unavailable.Contains(starters[i]) && !used.Contains(starters[i]))
                {
                    onCourt[i] = starters[i];
                    used.Add(starters[i]);
                }
                else
                {
                    // Pick next available roster player
                    var fallback = rosterIds.FirstOrDefault(id => !used.Contains(id));
                    onCourt[i] = fallback ?? "unknown";
                    if (fallback != null) used.Add(fallback);
                }
            }
        }

        /// <summary>
        /// Gets the 5 active players for a team from on-court tracking.
        /// </summary>
        private Player[] GetActivePlayers(Team team)
        {
            var onCourt = team.TeamId == _homeTeam.TeamId ? _homeOnCourt : _awayOnCourt;
            var players = new Player[5];
            var usedIds = new HashSet<string>();

            for (int i = 0; i < 5; i++)
            {
                if (!string.IsNullOrEmpty(onCourt[i]))
                    players[i] = _playerDatabase.GetPlayer(onCourt[i]);

                if (players[i] != null)
                {
                    usedIds.Add(onCourt[i]);
                }
                else
                {
                    // Fallback: grab any available roster player not already on court
                    var unavailable = UnavailableFor(team);
                    foreach (var p in team.Roster)
                    {
                        if (p != null && !string.IsNullOrEmpty(p.PlayerId) &&
                            !usedIds.Contains(p.PlayerId) && !unavailable.Contains(p.PlayerId))
                        {
                            players[i] = p;
                            onCourt[i] = p.PlayerId;
                            usedIds.Add(p.PlayerId);
                            break;
                        }
                    }
                }
            }
            return players;
        }

        /// <summary>
        /// Checks if any on-court player has fouled out (6 personal fouls) and forces a substitution.
        /// </summary>
        private void CheckFoulOuts(Team team)
        {
            var onCourt = team.TeamId == _homeTeam.TeamId ? _homeOnCourt : _awayOnCourt;
            var onCourtSet = new HashSet<string>(onCourt);

            for (int i = 0; i < 5; i++)
            {
                if (!_boxScore.PlayerStats.ContainsKey(onCourt[i])) continue;
                if (_boxScore.PlayerStats[onCourt[i]].PersonalFouls < 6) continue;

                // Player fouled out — must sub
                string bestSub = null;
                int bestFouls = 6;

                var unavailable = UnavailableFor(team);
                foreach (var pid in team.RosterPlayerIds)
                {
                    if (onCourtSet.Contains(pid) || unavailable.Contains(pid)) continue;
                    var stats = _boxScore.PlayerStats.ContainsKey(pid) ? _boxScore.PlayerStats[pid] : null;
                    int fouls = stats?.PersonalFouls ?? 0;
                    if (fouls < bestFouls) // only sub in someone who hasn't also fouled out
                    {
                        bestSub = pid;
                        bestFouls = fouls;
                    }
                }

                if (bestSub != null)
                {
                    onCourtSet.Remove(onCourt[i]);
                    onCourt[i] = bestSub;
                    onCourtSet.Add(bestSub);
                }
                // If no valid sub, player must stay (all bench fouled out — extremely rare)
            }
        }

        /// <summary>
        /// Checks if any on-court player is fatigued and subs in a rested bench player.
        /// </summary>
        private void CheckSubstitutions(Team team)
        {
            var onCourt = team.TeamId == _homeTeam.TeamId ? _homeOnCourt : _awayOnCourt;
            var onCourtSet = new HashSet<string>(onCourt);

            for (int i = 0; i < 5; i++)
            {
                var player = _playerDatabase.GetPlayer(onCourt[i]);
                if (player == null || player.Energy > SUB_ENERGY_THRESHOLD) continue;

                // Find best available bench player (highest energy among bench)
                string bestSub = null;
                float bestEnergy = 0f;

                var unavailable = UnavailableFor(team);
                foreach (var pid in team.RosterPlayerIds)
                {
                    if (onCourtSet.Contains(pid) || unavailable.Contains(pid)) continue;
                    var bench = _playerDatabase.GetPlayer(pid);
                    if (bench == null) continue;
                    // Never rotate a fouled-out player back onto the court
                    if (_boxScore.PlayerStats.ContainsKey(pid) &&
                        _boxScore.PlayerStats[pid].PersonalFouls >= 6) continue;
                    if (bench.Energy > bestEnergy)
                    {
                        bestEnergy = bench.Energy;
                        bestSub = pid;
                    }
                }

                // Only sub if bench player has meaningfully more energy
                if (bestSub != null && bestEnergy > player.Energy + 15f)
                {
                    onCourtSet.Remove(onCourt[i]);
                    onCourt[i] = bestSub;
                    onCourtSet.Add(bestSub);
                }
            }
        }

        /// <summary>
        /// Recovers energy for bench players (not on court).
        /// </summary>
        private void RecoverBenchEnergy(Team team, float seconds)
        {
            var onCourt = team.TeamId == _homeTeam.TeamId ? _homeOnCourt : _awayOnCourt;
            var onCourtSet = new HashSet<string>(onCourt);

            foreach (var pid in team.RosterPlayerIds)
            {
                if (onCourtSet.Contains(pid)) continue;
                var player = _playerDatabase.GetPlayer(pid);
                if (player != null)
                    player.RestoreEnergy(seconds * BENCH_ENERGY_RECOVERY);
            }
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
        /// Processes a possession result to update box score. Per-player stat application
        /// lives in BoxScoreEventApplier (shared with the interactive match controller).
        /// </summary>
        private void ProcessPossessionResult(PossessionResult result, bool isHomePossession)
        {
            string defenseTeamId = isHomePossession ? _awayTeam.TeamId : _homeTeam.TeamId;
            BoxScoreEventApplier.Apply(_boxScore, result, isHomePossession, defenseTeamId, _currentQuarter);

            // Add points to team score
            if (result.PointsScored > 0)
            {
                if (isHomePossession)
                    _boxScore.HomeScore += result.PointsScored;
                else
                    _boxScore.AwayScore += result.PointsScored;
            }
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
                    var shooter = _playerDatabase?.GetPlayer(evt.ActorPlayerId);
                    if (shooter == null) continue;

                    // Use already-calculated FT result if available (from ExecuteShotWithFoul)
                    if (evt.FreeThrowResult != null)
                    {
                        _boxScore.AddFreeThrows(shooter.PlayerId, evt.FreeThrowResult.Made, evt.FreeThrowResult.Attempts);
                    }
                    else
                    {
                        // Calculate fresh for non-shooting fouls
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

                        var ftResult = _freeThrowHandler.CalculateFreeThrows(
                            shooter, evt.FoulDetail.FreeThrowsAwarded, context);

                        _boxScore.AddFreeThrows(shooter.PlayerId, ftResult.Made, ftResult.Attempts);

                        // Add points for non-shooting fouls (shooting fouls already in result.PointsScored)
                        if (ftResult.Made > 0)
                        {
                            if (isHomePossession)
                                _boxScore.HomeScore += ftResult.Made;
                            else
                                _boxScore.AwayScore += ftResult.Made;
                        }

                        evt.FreeThrowResult = ftResult;
                    }
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

        /// <summary>
        /// Gets a player's game stats by their player ID. Returns null if not found.
        /// </summary>
        public PlayerGameStats GetPlayerStats(string playerId)
        {
            return PlayerStats.TryGetValue(playerId, out var stats) ? stats : null;
        }

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
        public float SecondsPlayed;
        public int Minutes => Mathf.RoundToInt(SecondsPlayed / 60f);
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
