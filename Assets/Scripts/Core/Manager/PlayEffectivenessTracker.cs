using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Tracks play call effectiveness during games.
    /// Identifies hot/cold plays and suggests adjustments based on what's working.
    /// </summary>
    public class PlayEffectivenessTracker
    {
        // ==================== PLAY TRACKING ====================
        private Dictionary<string, PlayRecord> _playRecords = new Dictionary<string, PlayRecord>();
        private Dictionary<PlayType, QuickPlayRecord> _quickPlayRecords = new Dictionary<PlayType, QuickPlayRecord>();
        private List<PlayAttempt> _recentAttempts = new List<PlayAttempt>();

        // ==================== CONFIGURATION ====================
        private const int HOT_PLAY_WINDOW = 5;       // Last 5 attempts to determine hot/cold
        private const float HOT_THRESHOLD = 0.60f;    // 60%+ success rate = hot
        private const float COLD_THRESHOLD = 0.30f;   // 30% or less success rate = cold
        private const int MIN_ATTEMPTS_FOR_STATUS = 3; // Minimum attempts to be labeled hot/cold

        // ==================== OPPONENT ADJUSTMENT TRACKING ====================
        private Dictionary<string, int> _consecutiveFailures = new Dictionary<string, int>();
        private const int ADJUSTMENT_THRESHOLD = 3;   // 3 consecutive failures suggests opponent adjusted

        // ==================== CONSTRUCTOR ====================

        public PlayEffectivenessTracker()
        {
            // Initialize quick play tracking
            foreach (PlayType type in Enum.GetValues(typeof(PlayType)))
            {
                _quickPlayRecords[type] = new QuickPlayRecord { PlayType = type };
            }
        }

        // ==================== RECORDING ====================

        /// <summary>
        /// Records a set play execution result.
        /// </summary>
        public void RecordSetPlayResult(SetPlay play, PossessionResult result)
        {
            if (play == null) return;

            string playId = play.PlayId ?? play.PlayName;

            if (!_playRecords.ContainsKey(playId))
            {
                _playRecords[playId] = new PlayRecord
                {
                    PlayId = playId,
                    PlayName = play.PlayName,
                    Category = play.Category.ToString()
                };
            }

            var record = _playRecords[playId];
            bool success = result.Outcome == PossessionOutcome.Score;
            int points = result.PointsScored;

            record.TotalAttempts++;
            if (success)
            {
                record.SuccessfulAttempts++;
                record.TotalPoints += points;
                _consecutiveFailures[playId] = 0;
            }
            else
            {
                if (!_consecutiveFailures.ContainsKey(playId))
                    _consecutiveFailures[playId] = 0;
                _consecutiveFailures[playId]++;
            }

            // Track recent attempts
            record.RecentResults.Add(success);
            if (record.RecentResults.Count > HOT_PLAY_WINDOW)
                record.RecentResults.RemoveAt(0);

            // Global recent attempts
            _recentAttempts.Add(new PlayAttempt
            {
                PlayId = playId,
                PlayName = play.PlayName,
                Success = success,
                Points = points,
                Timestamp = DateTime.Now
            });

            // Keep only last 50 attempts
            if (_recentAttempts.Count > 50)
                _recentAttempts.RemoveAt(0);
        }

        /// <summary>
        /// Records a quick play (PlayType) execution result.
        /// </summary>
        public void RecordQuickPlayResult(PlayType playType, PossessionResult result)
        {
            if (!_quickPlayRecords.ContainsKey(playType))
                _quickPlayRecords[playType] = new QuickPlayRecord { PlayType = playType };

            var record = _quickPlayRecords[playType];
            bool success = result.Outcome == PossessionOutcome.Score;
            int points = result.PointsScored;

            record.TotalAttempts++;
            if (success)
            {
                record.SuccessfulAttempts++;
                record.TotalPoints += points;
            }

            record.RecentResults.Add(success);
            if (record.RecentResults.Count > HOT_PLAY_WINDOW)
                record.RecentResults.RemoveAt(0);

            // Also track in global recent attempts
            _recentAttempts.Add(new PlayAttempt
            {
                PlayId = playType.ToString(),
                PlayName = playType.ToString(),
                Success = success,
                Points = points,
                Timestamp = DateTime.Now,
                IsQuickPlay = true
            });

            if (_recentAttempts.Count > 50)
                _recentAttempts.RemoveAt(0);
        }

        // ==================== HOT/COLD DETECTION ====================

        /// <summary>
        /// Gets all plays currently performing well.
        /// </summary>
        public List<PlayEffectiveness> GetHotPlays()
        {
            var hot = new List<PlayEffectiveness>();

            // Set plays
            foreach (var record in _playRecords.Values)
            {
                if (record.RecentResults.Count >= MIN_ATTEMPTS_FOR_STATUS)
                {
                    float recentRate = record.RecentResults.Count(r => r) / (float)record.RecentResults.Count;
                    if (recentRate >= HOT_THRESHOLD)
                    {
                        hot.Add(new PlayEffectiveness
                        {
                            PlayId = record.PlayId,
                            PlayName = record.PlayName,
                            SuccessRate = record.SuccessRate,
                            RecentSuccessRate = recentRate,
                            PointsPerPlay = record.PointsPerPlay,
                            TotalAttempts = record.TotalAttempts,
                            IsHot = true
                        });
                    }
                }
            }

            // Quick plays
            foreach (var record in _quickPlayRecords.Values)
            {
                if (record.RecentResults.Count >= MIN_ATTEMPTS_FOR_STATUS)
                {
                    float recentRate = record.RecentResults.Count(r => r) / (float)record.RecentResults.Count;
                    if (recentRate >= HOT_THRESHOLD)
                    {
                        hot.Add(new PlayEffectiveness
                        {
                            PlayId = record.PlayType.ToString(),
                            PlayName = record.PlayType.ToString(),
                            SuccessRate = record.SuccessRate,
                            RecentSuccessRate = recentRate,
                            PointsPerPlay = record.PointsPerPlay,
                            TotalAttempts = record.TotalAttempts,
                            IsHot = true,
                            IsQuickPlay = true
                        });
                    }
                }
            }

            return hot.OrderByDescending(p => p.RecentSuccessRate).ToList();
        }

        /// <summary>
        /// Gets all plays currently performing poorly.
        /// </summary>
        public List<PlayEffectiveness> GetColdPlays()
        {
            var cold = new List<PlayEffectiveness>();

            // Set plays
            foreach (var record in _playRecords.Values)
            {
                if (record.RecentResults.Count >= MIN_ATTEMPTS_FOR_STATUS)
                {
                    float recentRate = record.RecentResults.Count(r => r) / (float)record.RecentResults.Count;
                    if (recentRate <= COLD_THRESHOLD)
                    {
                        cold.Add(new PlayEffectiveness
                        {
                            PlayId = record.PlayId,
                            PlayName = record.PlayName,
                            SuccessRate = record.SuccessRate,
                            RecentSuccessRate = recentRate,
                            PointsPerPlay = record.PointsPerPlay,
                            TotalAttempts = record.TotalAttempts,
                            IsCold = true,
                            OpponentAdjusted = _consecutiveFailures.TryGetValue(record.PlayId, out var fails) && fails >= ADJUSTMENT_THRESHOLD
                        });
                    }
                }
            }

            // Quick plays
            foreach (var record in _quickPlayRecords.Values)
            {
                if (record.RecentResults.Count >= MIN_ATTEMPTS_FOR_STATUS)
                {
                    float recentRate = record.RecentResults.Count(r => r) / (float)record.RecentResults.Count;
                    if (recentRate <= COLD_THRESHOLD)
                    {
                        cold.Add(new PlayEffectiveness
                        {
                            PlayId = record.PlayType.ToString(),
                            PlayName = record.PlayType.ToString(),
                            SuccessRate = record.SuccessRate,
                            RecentSuccessRate = recentRate,
                            PointsPerPlay = record.PointsPerPlay,
                            TotalAttempts = record.TotalAttempts,
                            IsCold = true,
                            IsQuickPlay = true
                        });
                    }
                }
            }

            return cold.OrderBy(p => p.RecentSuccessRate).ToList();
        }

        /// <summary>
        /// Checks if a specific play is currently hot.
        /// </summary>
        public bool IsPlayHot(string playId)
        {
            if (_playRecords.TryGetValue(playId, out var record))
            {
                if (record.RecentResults.Count >= MIN_ATTEMPTS_FOR_STATUS)
                {
                    float rate = record.RecentResults.Count(r => r) / (float)record.RecentResults.Count;
                    return rate >= HOT_THRESHOLD;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a specific play is currently cold.
        /// </summary>
        public bool IsPlayCold(string playId)
        {
            if (_playRecords.TryGetValue(playId, out var record))
            {
                if (record.RecentResults.Count >= MIN_ATTEMPTS_FOR_STATUS)
                {
                    float rate = record.RecentResults.Count(r => r) / (float)record.RecentResults.Count;
                    return rate <= COLD_THRESHOLD;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if opponent appears to have adjusted to a play.
        /// </summary>
        public bool HasOpponentAdjusted(string playId)
        {
            return _consecutiveFailures.TryGetValue(playId, out var fails) && fails >= ADJUSTMENT_THRESHOLD;
        }

        // ==================== PLAY RECOMMENDATIONS ====================

        /// <summary>
        /// Gets play recommendations based on current effectiveness.
        /// </summary>
        public PlayRecommendations GetPlayRecommendations()
        {
            var recommendations = new PlayRecommendations
            {
                HotPlays = GetHotPlays(),
                ColdPlays = GetColdPlays()
            };

            // Best performing overall
            var allPlays = _playRecords.Values
                .Where(r => r.TotalAttempts >= 2)
                .OrderByDescending(r => r.PointsPerPlay)
                .Take(5)
                .Select(r => new PlayEffectiveness
                {
                    PlayId = r.PlayId,
                    PlayName = r.PlayName,
                    SuccessRate = r.SuccessRate,
                    PointsPerPlay = r.PointsPerPlay,
                    TotalAttempts = r.TotalAttempts
                })
                .ToList();

            recommendations.BestOverall = allPlays;

            return recommendations;
        }

        /// <summary>
        /// Gets the best play type for current situation.
        /// </summary>
        public PlayType? GetBestQuickPlayType()
        {
            var effective = _quickPlayRecords.Values
                .Where(r => r.TotalAttempts >= 2 && r.SuccessRate >= 0.4f)
                .OrderByDescending(r => r.PointsPerPlay)
                .FirstOrDefault();

            return effective?.PlayType;
        }

        // ==================== STATISTICS ====================

        /// <summary>
        /// Gets statistics for a specific play.
        /// </summary>
        public PlayStats GetPlayStats(string playId)
        {
            if (!_playRecords.TryGetValue(playId, out var record))
                return null;

            return new PlayStats
            {
                PlayId = record.PlayId,
                PlayName = record.PlayName,
                TotalAttempts = record.TotalAttempts,
                SuccessfulAttempts = record.SuccessfulAttempts,
                SuccessRate = record.SuccessRate,
                TotalPoints = record.TotalPoints,
                PointsPerPlay = record.PointsPerPlay,
                IsCurrentlyHot = IsPlayHot(playId),
                IsCurrentlyCold = IsPlayCold(playId),
                OpponentAdjusted = HasOpponentAdjusted(playId)
            };
        }

        /// <summary>
        /// Gets statistics for a quick play type.
        /// </summary>
        public PlayStats GetQuickPlayStats(PlayType playType)
        {
            if (!_quickPlayRecords.TryGetValue(playType, out var record))
                return null;

            return new PlayStats
            {
                PlayId = playType.ToString(),
                PlayName = playType.ToString(),
                TotalAttempts = record.TotalAttempts,
                SuccessfulAttempts = record.SuccessfulAttempts,
                SuccessRate = record.SuccessRate,
                TotalPoints = record.TotalPoints,
                PointsPerPlay = record.PointsPerPlay
            };
        }

        /// <summary>
        /// Gets all play statistics for the game.
        /// </summary>
        public List<PlayStats> GetAllPlayStats()
        {
            var stats = new List<PlayStats>();

            foreach (var record in _playRecords.Values)
            {
                stats.Add(new PlayStats
                {
                    PlayId = record.PlayId,
                    PlayName = record.PlayName,
                    TotalAttempts = record.TotalAttempts,
                    SuccessfulAttempts = record.SuccessfulAttempts,
                    SuccessRate = record.SuccessRate,
                    TotalPoints = record.TotalPoints,
                    PointsPerPlay = record.PointsPerPlay,
                    IsCurrentlyHot = IsPlayHot(record.PlayId),
                    IsCurrentlyCold = IsPlayCold(record.PlayId)
                });
            }

            return stats.OrderByDescending(s => s.TotalAttempts).ToList();
        }

        /// <summary>
        /// Gets summary of play effectiveness.
        /// </summary>
        public PlayEffectivenessSummary GetSummary()
        {
            var hotPlays = GetHotPlays();
            var coldPlays = GetColdPlays();
            int totalAttempts = _playRecords.Values.Sum(r => r.TotalAttempts) +
                               _quickPlayRecords.Values.Sum(r => r.TotalAttempts);
            int totalSuccess = _playRecords.Values.Sum(r => r.SuccessfulAttempts) +
                              _quickPlayRecords.Values.Sum(r => r.SuccessfulAttempts);
            int totalPoints = _playRecords.Values.Sum(r => r.TotalPoints) +
                             _quickPlayRecords.Values.Sum(r => r.TotalPoints);

            return new PlayEffectivenessSummary
            {
                TotalPlaysRun = totalAttempts,
                OverallSuccessRate = totalAttempts > 0 ? totalSuccess / (float)totalAttempts : 0f,
                OverallPointsPerPlay = totalAttempts > 0 ? totalPoints / (float)totalAttempts : 0f,
                HotPlayCount = hotPlays.Count,
                ColdPlayCount = coldPlays.Count,
                BestPlay = hotPlays.FirstOrDefault()?.PlayName,
                WorstPlay = coldPlays.FirstOrDefault()?.PlayName
            };
        }

        // ==================== RECENT ACTIVITY ====================

        /// <summary>
        /// Gets the most recent play attempts.
        /// </summary>
        public List<PlayAttempt> GetRecentAttempts(int count = 10)
        {
            return _recentAttempts.TakeLast(count).Reverse().ToList();
        }

        // ==================== RESET ====================

        /// <summary>
        /// Resets all tracking for a new game.
        /// </summary>
        public void ResetForNewGame()
        {
            _playRecords.Clear();
            _recentAttempts.Clear();
            _consecutiveFailures.Clear();

            foreach (var type in _quickPlayRecords.Keys.ToList())
            {
                _quickPlayRecords[type] = new QuickPlayRecord { PlayType = type };
            }
        }
    }

    // ==================== DATA STRUCTURES ====================

    [Serializable]
    public class PlayRecord
    {
        public string PlayId;
        public string PlayName;
        public string Category;
        public int TotalAttempts;
        public int SuccessfulAttempts;
        public int TotalPoints;
        public List<bool> RecentResults = new List<bool>();

        public float SuccessRate => TotalAttempts > 0 ? SuccessfulAttempts / (float)TotalAttempts : 0f;
        public float PointsPerPlay => TotalAttempts > 0 ? TotalPoints / (float)TotalAttempts : 0f;
    }

    [Serializable]
    public class QuickPlayRecord
    {
        public PlayType PlayType;
        public int TotalAttempts;
        public int SuccessfulAttempts;
        public int TotalPoints;
        public List<bool> RecentResults = new List<bool>();

        public float SuccessRate => TotalAttempts > 0 ? SuccessfulAttempts / (float)TotalAttempts : 0f;
        public float PointsPerPlay => TotalAttempts > 0 ? TotalPoints / (float)TotalAttempts : 0f;
    }

    [Serializable]
    public class PlayAttempt
    {
        public string PlayId;
        public string PlayName;
        public bool Success;
        public int Points;
        public DateTime Timestamp;
        public bool IsQuickPlay;
    }

    [Serializable]
    public class PlayEffectiveness
    {
        public string PlayId;
        public string PlayName;
        public float SuccessRate;
        public float RecentSuccessRate;
        public float PointsPerPlay;
        public int TotalAttempts;
        public bool IsHot;
        public bool IsCold;
        public bool OpponentAdjusted;
        public bool IsQuickPlay;
    }

    [Serializable]
    public class PlayStats
    {
        public string PlayId;
        public string PlayName;
        public int TotalAttempts;
        public int SuccessfulAttempts;
        public float SuccessRate;
        public int TotalPoints;
        public float PointsPerPlay;
        public bool IsCurrentlyHot;
        public bool IsCurrentlyCold;
        public bool OpponentAdjusted;
    }

    [Serializable]
    public class PlayRecommendations
    {
        public List<PlayEffectiveness> HotPlays;
        public List<PlayEffectiveness> ColdPlays;
        public List<PlayEffectiveness> BestOverall;
    }

    [Serializable]
    public class PlayEffectivenessSummary
    {
        public int TotalPlaysRun;
        public float OverallSuccessRate;
        public float OverallPointsPerPlay;
        public int HotPlayCount;
        public int ColdPlayCount;
        public string BestPlay;
        public string WorstPlay;
    }
}
