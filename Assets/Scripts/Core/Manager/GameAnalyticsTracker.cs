using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Tracks real-time game statistics during simulation.
    /// Provides analytics for coaching decisions including efficiency, shot charts,
    /// matchup data, lineup tracking, and momentum analysis.
    /// </summary>
    public class GameAnalyticsTracker
    {
        // ==================== POSSESSION TRACKING ====================
        private List<PossessionRecord> _teamPossessions = new List<PossessionRecord>();
        private List<PossessionRecord> _opponentPossessions = new List<PossessionRecord>();

        // ==================== SHOT CHARTS ====================
        private Dictionary<string, List<ShotRecord>> _playerShots = new Dictionary<string, List<ShotRecord>>();
        private Dictionary<ShotZone, ZoneStats> _teamShotsByZone = new Dictionary<ShotZone, ZoneStats>();
        private Dictionary<ShotZone, ZoneStats> _opponentShotsByZone = new Dictionary<ShotZone, ZoneStats>();

        // ==================== MATCHUP TRACKING ====================
        private Dictionary<string, MatchupRecord> _matchupRecords = new Dictionary<string, MatchupRecord>();

        // ==================== LINEUP TRACKING ====================
        private Dictionary<string, LineupRecord> _lineupRecords = new Dictionary<string, LineupRecord>();
        private string _currentLineupKey;
        private List<string> _currentLineup = new List<string>();

        // ==================== PLAYER PERFORMANCE ====================
        private Dictionary<string, AnalyticsPlayerStats> _playerStats = new Dictionary<string, AnalyticsPlayerStats>();

        // ==================== MOMENTUM & RUNS ====================
        private int _teamRunPoints = 0;
        private int _opponentRunPoints = 0;
        private int _consecutiveTeamScores = 0;
        private int _consecutiveOpponentScores = 0;
        private float _momentum = 50f;  // 0-100, 50 is neutral
        private List<RunRecord> _runs = new List<RunRecord>();

        // ==================== QUARTER TRACKING ====================
        private Dictionary<int, QuarterStats> _quarterStats = new Dictionary<int, QuarterStats>();
        private int _currentQuarter = 1;

        // ==================== HOT/COLD TRACKING ====================
        private const int HOT_THRESHOLD_MAKES = 3;  // 3 makes in last 5 shots = hot
        private const int COLD_THRESHOLD_MISSES = 4; // 4 misses in last 5 shots = cold

        // ==================== GAME STATE ====================
        public int TeamScore { get; private set; }
        public int OpponentScore { get; private set; }
        public int TotalPossessions => _teamPossessions.Count + _opponentPossessions.Count;

        // ==================== CONSTRUCTOR ====================

        public GameAnalyticsTracker()
        {
            InitializeZoneStats();
            for (int q = 1; q <= 4; q++)
            {
                _quarterStats[q] = new QuarterStats { Quarter = q };
            }
        }

        private void InitializeZoneStats()
        {
            foreach (ShotZone zone in Enum.GetValues(typeof(ShotZone)))
            {
                _teamShotsByZone[zone] = new ZoneStats { Zone = zone };
                _opponentShotsByZone[zone] = new ZoneStats { Zone = zone };
            }
        }

        // ==================== POSSESSION RECORDING ====================

        /// <summary>
        /// Records a completed possession result.
        /// </summary>
        public void RecordPossession(PossessionResult result, bool isTeamPossession, List<string> lineup, List<string> opponentLineup)
        {
            var record = new PossessionRecord
            {
                Quarter = result.Quarter,
                StartClock = result.StartGameClock,
                EndClock = result.EndGameClock,
                Outcome = result.Outcome,
                PointsScored = result.PointsScored,
                Lineup = lineup.ToList(),
                OpponentLineup = opponentLineup.ToList()
            };

            if (isTeamPossession)
            {
                _teamPossessions.Add(record);
                ProcessTeamPossession(result, lineup);
            }
            else
            {
                _opponentPossessions.Add(record);
                ProcessOpponentPossession(result, opponentLineup, lineup);
            }

            // Update lineup tracking
            UpdateLineupStats(lineup, result.PointsScored, isTeamPossession);

            // Update quarter stats
            UpdateQuarterStats(result.Quarter, result.PointsScored, isTeamPossession);

            // Update momentum and runs
            UpdateMomentum(result.PointsScored, isTeamPossession);
        }

        private void ProcessTeamPossession(PossessionResult result, List<string> lineup)
        {
            foreach (var evt in result.Events)
            {
                if (evt.Type == EventType.Shot)
                {
                    RecordShot(evt, true);
                }
            }

            if (result.PointsScored > 0)
            {
                TeamScore += result.PointsScored;
            }
        }

        private void ProcessOpponentPossession(PossessionResult result, List<string> scorers, List<string> defenders)
        {
            foreach (var evt in result.Events)
            {
                if (evt.Type == EventType.Shot)
                {
                    RecordShot(evt, false);

                    // Track matchup if we have defender info
                    if (!string.IsNullOrEmpty(evt.DefenderPlayerId))
                    {
                        RecordMatchupPossession(evt.DefenderPlayerId, evt.ActorPlayerId,
                            evt.PointsScored, evt.Outcome == EventOutcome.Success);
                    }
                }
            }

            if (result.PointsScored > 0)
            {
                OpponentScore += result.PointsScored;
            }
        }

        // ==================== SHOT TRACKING ====================

        private void RecordShot(PossessionEvent evt, bool isTeam)
        {
            var zone = GetShotZone(evt.ActorPosition);
            bool made = evt.Outcome == EventOutcome.Success;
            int points = evt.PointsScored;

            // Player shots
            if (!_playerShots.ContainsKey(evt.ActorPlayerId))
                _playerShots[evt.ActorPlayerId] = new List<ShotRecord>();

            _playerShots[evt.ActorPlayerId].Add(new ShotRecord
            {
                Zone = zone,
                Made = made,
                Points = points,
                Contested = evt.ContestLevel > 0.5f,
                Position = evt.ActorPosition,
                Quarter = evt.Quarter,
                GameClock = evt.GameClock
            });

            // Zone stats
            var zoneStats = isTeam ? _teamShotsByZone : _opponentShotsByZone;
            zoneStats[zone].Attempts++;
            if (made) zoneStats[zone].Makes++;
            zoneStats[zone].Points += points;

            // Player game stats
            UpdatePlayerStats(evt.ActorPlayerId, points, made, zone);
        }

        private ShotZone GetShotZone(CourtPosition position)
        {
            float distFromBasket = Mathf.Abs(position.X - 42f);  // Assume attacking right

            if (distFromBasket <= 4f)
                return ShotZone.RestrictedArea;
            if (distFromBasket <= 8f)
                return ShotZone.Paint;
            if (distFromBasket <= 16f)
                return ShotZone.MidRange;

            // Three point zones
            if (position.Y < -14f)
                return ShotZone.LeftCornerThree;
            if (position.Y > 14f)
                return ShotZone.RightCornerThree;
            if (position.Y < -7f)
                return ShotZone.LeftWingThree;
            if (position.Y > 7f)
                return ShotZone.RightWingThree;

            return ShotZone.TopKeyThree;
        }

        // ==================== MATCHUP TRACKING ====================

        private void RecordMatchupPossession(string defenderId, string offenderId, int pointsAllowed, bool scored)
        {
            string key = $"{defenderId}_vs_{offenderId}";

            if (!_matchupRecords.ContainsKey(key))
            {
                _matchupRecords[key] = new MatchupRecord
                {
                    DefenderId = defenderId,
                    OffenderId = offenderId
                };
            }

            var record = _matchupRecords[key];
            record.Possessions++;
            record.PointsAllowed += pointsAllowed;
            if (scored) record.FieldGoalsAllowed++;
        }

        /// <summary>
        /// Gets matchup performance for a specific defender.
        /// </summary>
        public MatchupSummary GetDefenderMatchupSummary(string defenderId)
        {
            var matchups = _matchupRecords.Values
                .Where(m => m.DefenderId == defenderId)
                .ToList();

            if (!matchups.Any())
                return new MatchupSummary { DefenderId = defenderId };

            return new MatchupSummary
            {
                DefenderId = defenderId,
                TotalPossessions = matchups.Sum(m => m.Possessions),
                TotalPointsAllowed = matchups.Sum(m => m.PointsAllowed),
                PointsPerPossession = matchups.Sum(m => m.PointsAllowed) / (float)matchups.Sum(m => m.Possessions),
                WorstMatchup = matchups.OrderByDescending(m => m.PointsPerPossession).FirstOrDefault()?.OffenderId
            };
        }

        /// <summary>
        /// Gets all matchup records for analysis.
        /// </summary>
        public List<MatchupRecord> GetAllMatchups() => _matchupRecords.Values.ToList();

        /// <summary>
        /// Identifies defenders who are struggling (high PPP allowed).
        /// </summary>
        public List<MatchupAlert> GetMatchupAlerts(float pppThreshold = 1.2f)
        {
            var alerts = new List<MatchupAlert>();

            foreach (var matchup in _matchupRecords.Values)
            {
                if (matchup.Possessions >= 3 && matchup.PointsPerPossession > pppThreshold)
                {
                    alerts.Add(new MatchupAlert
                    {
                        DefenderId = matchup.DefenderId,
                        OffenderId = matchup.OffenderId,
                        PointsPerPossession = matchup.PointsPerPossession,
                        Possessions = matchup.Possessions,
                        Severity = matchup.PointsPerPossession > 1.5f ? AlertSeverity.High : AlertSeverity.Medium
                    });
                }
            }

            return alerts.OrderByDescending(a => a.PointsPerPossession).ToList();
        }

        // ==================== LINEUP TRACKING ====================

        /// <summary>
        /// Sets the current lineup for tracking.
        /// </summary>
        public void SetCurrentLineup(List<string> lineup)
        {
            _currentLineup = lineup.OrderBy(p => p).ToList();
            _currentLineupKey = string.Join(",", _currentLineup);

            if (!_lineupRecords.ContainsKey(_currentLineupKey))
            {
                _lineupRecords[_currentLineupKey] = new LineupRecord
                {
                    PlayerIds = _currentLineup.ToList()
                };
            }
        }

        private void UpdateLineupStats(List<string> lineup, int points, bool isTeam)
        {
            var key = string.Join(",", lineup.OrderBy(p => p));

            if (!_lineupRecords.ContainsKey(key))
            {
                _lineupRecords[key] = new LineupRecord { PlayerIds = lineup.ToList() };
            }

            var record = _lineupRecords[key];
            record.Possessions++;

            if (isTeam)
            {
                record.PointsScored += points;
            }
            else
            {
                record.PointsAllowed += points;
            }
        }

        /// <summary>
        /// Gets lineup +/- data for all lineups used.
        /// </summary>
        public List<LineupPlusMinus> GetLineupPlusMinusData()
        {
            return _lineupRecords.Values
                .Where(r => r.Possessions >= 4)
                .Select(r => new LineupPlusMinus
                {
                    PlayerIds = r.PlayerIds,
                    PlusMinus = r.PlusMinus,
                    OffensiveRating = r.OffensiveRating,
                    DefensiveRating = r.DefensiveRating,
                    Possessions = r.Possessions
                })
                .OrderByDescending(l => l.PlusMinus)
                .ToList();
        }

        /// <summary>
        /// Gets the current lineup's +/- this game.
        /// </summary>
        public int GetCurrentLineupPlusMinus()
        {
            if (string.IsNullOrEmpty(_currentLineupKey) || !_lineupRecords.ContainsKey(_currentLineupKey))
                return 0;

            return _lineupRecords[_currentLineupKey].PlusMinus;
        }

        // ==================== PLAYER STATS ====================

        private void UpdatePlayerStats(string playerId, int points, bool made, ShotZone zone)
        {
            if (!_playerStats.ContainsKey(playerId))
                _playerStats[playerId] = new AnalyticsPlayerStats { PlayerId = playerId };

            var stats = _playerStats[playerId];
            stats.Points += points;
            stats.FieldGoalAttempts++;
            if (made) stats.FieldGoalsMade++;

            if (zone >= ShotZone.LeftCornerThree)
            {
                stats.ThreePointAttempts++;
                if (made) stats.ThreePointsMade++;
            }
        }

        /// <summary>
        /// Gets player game stats.
        /// </summary>
        public AnalyticsPlayerStats GetPlayerStats(string playerId)
        {
            return _playerStats.TryGetValue(playerId, out var stats) ? stats : new AnalyticsPlayerStats { PlayerId = playerId };
        }

        /// <summary>
        /// Gets all player stats for the game.
        /// </summary>
        public List<AnalyticsPlayerStats> GetAllPlayerStats() => _playerStats.Values.ToList();

        // ==================== HOT/COLD TRACKING ====================

        /// <summary>
        /// Determines if a player is "hot" based on recent makes.
        /// </summary>
        public bool IsPlayerHot(string playerId)
        {
            if (!_playerShots.ContainsKey(playerId))
                return false;

            var recentShots = _playerShots[playerId].TakeLast(5).ToList();
            if (recentShots.Count < 3)
                return false;

            int makes = recentShots.Count(s => s.Made);
            return makes >= HOT_THRESHOLD_MAKES;
        }

        /// <summary>
        /// Determines if a player is "cold" based on recent misses.
        /// </summary>
        public bool IsPlayerCold(string playerId)
        {
            if (!_playerShots.ContainsKey(playerId))
                return false;

            var recentShots = _playerShots[playerId].TakeLast(5).ToList();
            if (recentShots.Count < 4)
                return false;

            int misses = recentShots.Count(s => !s.Made);
            return misses >= COLD_THRESHOLD_MISSES;
        }

        /// <summary>
        /// Gets list of hot players.
        /// </summary>
        public List<string> GetHotPlayers()
        {
            return _playerShots.Keys.Where(IsPlayerHot).ToList();
        }

        /// <summary>
        /// Gets list of cold players.
        /// </summary>
        public List<string> GetColdPlayers()
        {
            return _playerShots.Keys.Where(IsPlayerCold).ToList();
        }

        // ==================== EFFICIENCY METRICS ====================

        /// <summary>
        /// Gets team offensive rating (points per 100 possessions).
        /// </summary>
        public float GetTeamOffensiveRating()
        {
            if (_teamPossessions.Count == 0) return 0f;
            return (TeamScore / (float)_teamPossessions.Count) * 100f;
        }

        /// <summary>
        /// Gets team defensive rating (opponent points per 100 possessions).
        /// </summary>
        public float GetTeamDefensiveRating()
        {
            if (_opponentPossessions.Count == 0) return 0f;
            return (OpponentScore / (float)_opponentPossessions.Count) * 100f;
        }

        /// <summary>
        /// Gets net rating (offensive - defensive rating).
        /// </summary>
        public float GetNetRating() => GetTeamOffensiveRating() - GetTeamDefensiveRating();

        /// <summary>
        /// Gets shooting percentages by zone.
        /// </summary>
        public Dictionary<ShotZone, float> GetShootingByZone(bool isTeam = true)
        {
            var zoneStats = isTeam ? _teamShotsByZone : _opponentShotsByZone;
            return zoneStats.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Percentage
            );
        }

        /// <summary>
        /// Gets effective field goal percentage.
        /// </summary>
        public float GetEffectiveFieldGoalPercentage(bool isTeam = true)
        {
            var zoneStats = isTeam ? _teamShotsByZone : _opponentShotsByZone;
            int totalMakes = zoneStats.Values.Sum(z => z.Makes);
            int totalAttempts = zoneStats.Values.Sum(z => z.Attempts);
            int threesMade = zoneStats.Values
                .Where(z => z.Zone >= ShotZone.LeftCornerThree)
                .Sum(z => z.Makes);

            if (totalAttempts == 0) return 0f;
            return (totalMakes + 0.5f * threesMade) / totalAttempts;
        }

        // ==================== MOMENTUM & RUNS ====================

        private void UpdateMomentum(int points, bool isTeam)
        {
            if (isTeam && points > 0)
            {
                _teamRunPoints += points;
                _opponentRunPoints = 0;
                _consecutiveTeamScores++;
                _consecutiveOpponentScores = 0;
                _momentum = Mathf.Min(100f, _momentum + points * 3f);

                // Record significant run
                if (_teamRunPoints >= 8)
                {
                    _runs.Add(new RunRecord
                    {
                        Points = _teamRunPoints,
                        IsTeamRun = true,
                        Quarter = _currentQuarter
                    });
                }
            }
            else if (!isTeam && points > 0)
            {
                _opponentRunPoints += points;
                _teamRunPoints = 0;
                _consecutiveOpponentScores++;
                _consecutiveTeamScores = 0;
                _momentum = Mathf.Max(0f, _momentum - points * 3f);

                // Record significant run
                if (_opponentRunPoints >= 8)
                {
                    _runs.Add(new RunRecord
                    {
                        Points = _opponentRunPoints,
                        IsTeamRun = false,
                        Quarter = _currentQuarter
                    });
                }
            }
        }

        /// <summary>
        /// Gets current momentum value (0-100, 50 neutral).
        /// </summary>
        public float GetMomentum() => _momentum;

        /// <summary>
        /// Gets current opponent run points.
        /// </summary>
        public int GetOpponentRunPoints() => _opponentRunPoints;

        /// <summary>
        /// Gets current team run points.
        /// </summary>
        public int GetTeamRunPoints() => _teamRunPoints;

        /// <summary>
        /// Determines if opponent is on a dangerous run.
        /// </summary>
        public bool IsOpponentOnRun(int threshold = 6) => _opponentRunPoints >= threshold;

        /// <summary>
        /// Determines if team is on a run.
        /// </summary>
        public bool IsTeamOnRun(int threshold = 6) => _teamRunPoints >= threshold;

        // ==================== QUARTER STATS ====================

        private void UpdateQuarterStats(int quarter, int points, bool isTeam)
        {
            _currentQuarter = quarter;

            if (!_quarterStats.ContainsKey(quarter))
                _quarterStats[quarter] = new QuarterStats { Quarter = quarter };

            if (isTeam)
                _quarterStats[quarter].TeamPoints += points;
            else
                _quarterStats[quarter].OpponentPoints += points;
        }

        /// <summary>
        /// Gets stats for a specific quarter.
        /// </summary>
        public QuarterStats GetQuarterStats(int quarter)
        {
            return _quarterStats.TryGetValue(quarter, out var stats) ? stats : new QuarterStats { Quarter = quarter };
        }

        // ==================== COMPREHENSIVE ANALYTICS SNAPSHOT ====================

        /// <summary>
        /// Gets a complete analytics snapshot for display/coaching decisions.
        /// </summary>
        public GameAnalyticsSnapshot GetSnapshot()
        {
            return new GameAnalyticsSnapshot
            {
                TeamScore = TeamScore,
                OpponentScore = OpponentScore,
                TeamPossessions = _teamPossessions.Count,
                OpponentPossessions = _opponentPossessions.Count,
                OffensiveRating = GetTeamOffensiveRating(),
                DefensiveRating = GetTeamDefensiveRating(),
                NetRating = GetNetRating(),
                Momentum = _momentum,
                TeamRunPoints = _teamRunPoints,
                OpponentRunPoints = _opponentRunPoints,
                HotPlayers = GetHotPlayers(),
                ColdPlayers = GetColdPlayers(),
                MatchupAlerts = GetMatchupAlerts(),
                TeamEFG = GetEffectiveFieldGoalPercentage(true),
                OpponentEFG = GetEffectiveFieldGoalPercentage(false),
                CurrentLineupPlusMinus = GetCurrentLineupPlusMinus()
            };
        }

        // ==================== RESET ====================

        /// <summary>
        /// Resets all tracking for a new game.
        /// </summary>
        public void ResetForNewGame()
        {
            _teamPossessions.Clear();
            _opponentPossessions.Clear();
            _playerShots.Clear();
            _matchupRecords.Clear();
            _lineupRecords.Clear();
            _playerStats.Clear();
            _runs.Clear();

            InitializeZoneStats();

            TeamScore = 0;
            OpponentScore = 0;
            _teamRunPoints = 0;
            _opponentRunPoints = 0;
            _consecutiveTeamScores = 0;
            _consecutiveOpponentScores = 0;
            _momentum = 50f;
            _currentQuarter = 1;
            _currentLineupKey = null;
            _currentLineup.Clear();

            for (int q = 1; q <= 4; q++)
            {
                _quarterStats[q] = new QuarterStats { Quarter = q };
            }
        }
    }

    // ==================== DATA STRUCTURES ====================

    public enum ShotZone
    {
        RestrictedArea,
        Paint,
        MidRange,
        LeftCornerThree,
        RightCornerThree,
        LeftWingThree,
        RightWingThree,
        TopKeyThree
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High
    }

    [Serializable]
    public class PossessionRecord
    {
        public int Quarter;
        public float StartClock;
        public float EndClock;
        public PossessionOutcome Outcome;
        public int PointsScored;
        public List<string> Lineup;
        public List<string> OpponentLineup;
    }

    [Serializable]
    public class ShotRecord
    {
        public ShotZone Zone;
        public bool Made;
        public int Points;
        public bool Contested;
        public CourtPosition Position;
        public int Quarter;
        public float GameClock;
    }

    [Serializable]
    public class ZoneStats
    {
        public ShotZone Zone;
        public int Attempts;
        public int Makes;
        public int Points;

        public float Percentage => Attempts > 0 ? Makes / (float)Attempts : 0f;
        public float PointsPerAttempt => Attempts > 0 ? Points / (float)Attempts : 0f;
    }

    [Serializable]
    public class MatchupRecord
    {
        public string DefenderId;
        public string OffenderId;
        public int Possessions;
        public int PointsAllowed;
        public int FieldGoalsAllowed;

        public float PointsPerPossession => Possessions > 0 ? PointsAllowed / (float)Possessions : 0f;
        public float FieldGoalPercentageAllowed => Possessions > 0 ? FieldGoalsAllowed / (float)Possessions : 0f;
    }

    [Serializable]
    public class MatchupSummary
    {
        public string DefenderId;
        public int TotalPossessions;
        public int TotalPointsAllowed;
        public float PointsPerPossession;
        public string WorstMatchup;
    }

    [Serializable]
    public class MatchupAlert
    {
        public string DefenderId;
        public string OffenderId;
        public float PointsPerPossession;
        public int Possessions;
        public AlertSeverity Severity;
    }

    [Serializable]
    public class LineupRecord
    {
        public List<string> PlayerIds;
        public int Possessions;
        public int PointsScored;
        public int PointsAllowed;

        public int PlusMinus => PointsScored - PointsAllowed;
        public float OffensiveRating => Possessions > 0 ? (PointsScored / (float)Possessions) * 100f : 0f;
        public float DefensiveRating => Possessions > 0 ? (PointsAllowed / (float)Possessions) * 100f : 0f;
    }

    [Serializable]
    public class LineupPlusMinus
    {
        public List<string> PlayerIds;
        public int PlusMinus;
        public float OffensiveRating;
        public float DefensiveRating;
        public int Possessions;
    }

    [Serializable]
    public class AnalyticsPlayerStats
    {
        public string PlayerId;
        public int Points;
        public int FieldGoalsMade;
        public int FieldGoalAttempts;
        public int ThreePointsMade;
        public int ThreePointAttempts;
        public int Rebounds;
        public int Assists;
        public int Steals;
        public int Blocks;
        public int Turnovers;
        public int Fouls;

        public float FieldGoalPercentage => FieldGoalAttempts > 0 ? FieldGoalsMade / (float)FieldGoalAttempts : 0f;
        public float ThreePointPercentage => ThreePointAttempts > 0 ? ThreePointsMade / (float)ThreePointAttempts : 0f;
    }

    [Serializable]
    public class RunRecord
    {
        public int Points;
        public bool IsTeamRun;
        public int Quarter;
    }

    [Serializable]
    public class QuarterStats
    {
        public int Quarter;
        public int TeamPoints;
        public int OpponentPoints;

        public int PlusMinus => TeamPoints - OpponentPoints;
    }

    [Serializable]
    public class GameAnalyticsSnapshot
    {
        public int TeamScore;
        public int OpponentScore;
        public int TeamPossessions;
        public int OpponentPossessions;
        public float OffensiveRating;
        public float DefensiveRating;
        public float NetRating;
        public float Momentum;
        public int TeamRunPoints;
        public int OpponentRunPoints;
        public List<string> HotPlayers;
        public List<string> ColdPlayers;
        public List<MatchupAlert> MatchupAlerts;
        public float TeamEFG;
        public float OpponentEFG;
        public int CurrentLineupPlusMinus;

        public int ScoreDifferential => TeamScore - OpponentScore;
    }
}
