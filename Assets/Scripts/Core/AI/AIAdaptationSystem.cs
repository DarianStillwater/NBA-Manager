using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// AI system that learns opponent patterns during games and makes strategic adjustments.
    /// Tracks tendencies, identifies patterns, and recommends/applies counter-strategies.
    /// </summary>
    public class AIAdaptationSystem
    {
        #region Configuration

        private const int MIN_POSSESSIONS_TO_ADAPT = 8;      // Minimum data before making adjustments
        private const int PATTERN_DETECTION_WINDOW = 12;    // Possessions to look back for patterns
        private const float ADAPTATION_THRESHOLD = 0.65f;   // Frequency threshold to trigger adjustment

        #endregion

        #region State

        // Possession tracking per team
        private Dictionary<string, TeamPossessionData> _teamData = new Dictionary<string, TeamPossessionData>();

        // Current game adaptations applied
        private List<GameAdaptation> _activeAdaptations = new List<GameAdaptation>();

        // Coach AI personality affects adaptation speed
        private float _adaptationSpeed = 1.0f;
        private float _riskTolerance = 0.5f;

        #endregion

        #region Events

        public event Action<GameAdaptation> OnAdaptationApplied;
        public event Action<PatternDetected> OnPatternDetected;
        public event Action<string> OnCoachingInsight;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize for a new game.
        /// </summary>
        public void InitializeForGame(Team homeTeam, Team awayTeam, AICoachPersonality aiPersonality = null)
        {
            _teamData.Clear();
            _activeAdaptations.Clear();

            _teamData[homeTeam.TeamId] = new TeamPossessionData(homeTeam.TeamId, homeTeam.Name);
            _teamData[awayTeam.TeamId] = new TeamPossessionData(awayTeam.TeamId, awayTeam.Name);

            // Set adaptation parameters from AI personality
            if (aiPersonality != null)
            {
                _adaptationSpeed = aiPersonality.AdaptationSpeed;
                _riskTolerance = aiPersonality.RiskTolerance;
            }
        }

        #endregion

        #region Possession Recording

        /// <summary>
        /// Record a possession result for pattern analysis.
        /// </summary>
        public void RecordPossession(PossessionRecord record)
        {
            if (!_teamData.TryGetValue(record.OffenseTeamId, out var data))
                return;

            data.AllPossessions.Add(record);

            // Update aggregated stats
            UpdateAggregatedStats(data, record);

            // Check for patterns after minimum possessions
            if (data.AllPossessions.Count >= MIN_POSSESSIONS_TO_ADAPT)
            {
                DetectPatterns(data);
            }
        }

        private void UpdateAggregatedStats(TeamPossessionData data, PossessionRecord record)
        {
            // Track shot locations
            if (record.ShotType != ShotType.None)
            {
                data.ShotsByZone[record.ShotZone]++;
                if (record.ShotMade)
                    data.MadeByZone[record.ShotZone]++;
            }

            // Track play types
            data.PlayTypeCounts[record.PlayType]++;
            if (record.PointsScored > 0)
                data.PlayTypeSuccess[record.PlayType]++;

            // Track player usage
            if (!string.IsNullOrEmpty(record.PrimaryPlayerId))
            {
                data.PlayerUsage[record.PrimaryPlayerId] =
                    data.PlayerUsage.GetValueOrDefault(record.PrimaryPlayerId, 0) + 1;

                if (record.PointsScored > 0)
                {
                    data.PlayerSuccesses[record.PrimaryPlayerId] =
                        data.PlayerSuccesses.GetValueOrDefault(record.PrimaryPlayerId, 0) + 1;
                }
            }

            // Track quarter tendencies
            data.QuarterPossessions[record.Quarter]++;

            // Track clutch situations
            if (record.IsClutch)
            {
                data.ClutchPossessions.Add(record);
            }
        }

        #endregion

        #region Pattern Detection

        private void DetectPatterns(TeamPossessionData data)
        {
            var patterns = new List<PatternDetected>();

            // Check for over-reliance on specific zones
            patterns.AddRange(DetectShotZonePatterns(data));

            // Check for predictable play types
            patterns.AddRange(DetectPlayTypePatterns(data));

            // Check for player isolation tendencies
            patterns.AddRange(DetectPlayerPatterns(data));

            // Check for situational patterns (late clock, clutch)
            patterns.AddRange(DetectSituationalPatterns(data));

            // Fire events and consider adaptations
            foreach (var pattern in patterns)
            {
                if (!data.DetectedPatterns.Contains(pattern.PatternId))
                {
                    data.DetectedPatterns.Add(pattern.PatternId);
                    OnPatternDetected?.Invoke(pattern);

                    // Suggest or apply counter-strategy
                    SuggestAdaptation(pattern, data.TeamId);
                }
            }
        }

        private List<PatternDetected> DetectShotZonePatterns(TeamPossessionData data)
        {
            var patterns = new List<PatternDetected>();
            int totalShots = data.ShotsByZone.Values.Sum();

            if (totalShots < MIN_POSSESSIONS_TO_ADAPT) return patterns;

            foreach (var zone in data.ShotsByZone.Keys)
            {
                float frequency = (float)data.ShotsByZone[zone] / totalShots;

                if (frequency >= ADAPTATION_THRESHOLD)
                {
                    int made = data.MadeByZone.GetValueOrDefault(zone, 0);
                    float pct = totalShots > 0 ? (float)made / data.ShotsByZone[zone] : 0;

                    patterns.Add(new PatternDetected
                    {
                        PatternId = $"zone_{zone}",
                        PatternType = PatternType.ShotZonePreference,
                        Description = $"Heavy reliance on {zone} zone ({frequency:P0} of shots)",
                        Frequency = frequency,
                        SuccessRate = pct,
                        TeamId = data.TeamId,
                        Metadata = new Dictionary<string, object> { { "zone", zone } }
                    });
                }
            }

            return patterns;
        }

        private List<PatternDetected> DetectPlayTypePatterns(TeamPossessionData data)
        {
            var patterns = new List<PatternDetected>();
            int totalPlays = data.PlayTypeCounts.Values.Sum();

            if (totalPlays < MIN_POSSESSIONS_TO_ADAPT) return patterns;

            foreach (var playType in data.PlayTypeCounts.Keys)
            {
                float frequency = (float)data.PlayTypeCounts[playType] / totalPlays;

                if (frequency >= ADAPTATION_THRESHOLD * 0.8f) // Slightly lower threshold for play types
                {
                    int successes = data.PlayTypeSuccess.GetValueOrDefault(playType, 0);
                    float successRate = (float)successes / data.PlayTypeCounts[playType];

                    patterns.Add(new PatternDetected
                    {
                        PatternId = $"play_{playType}",
                        PatternType = PatternType.PlayTypePreference,
                        Description = $"Predictable {playType} offense ({frequency:P0} of possessions)",
                        Frequency = frequency,
                        SuccessRate = successRate,
                        TeamId = data.TeamId,
                        Metadata = new Dictionary<string, object> { { "playType", playType } }
                    });
                }
            }

            return patterns;
        }

        private List<PatternDetected> DetectPlayerPatterns(TeamPossessionData data)
        {
            var patterns = new List<PatternDetected>();
            int totalUsage = data.PlayerUsage.Values.Sum();

            if (totalUsage < MIN_POSSESSIONS_TO_ADAPT) return patterns;

            foreach (var playerId in data.PlayerUsage.Keys)
            {
                float usage = (float)data.PlayerUsage[playerId] / totalUsage;

                if (usage >= 0.40f) // Player involved in 40%+ of possessions
                {
                    int successes = data.PlayerSuccesses.GetValueOrDefault(playerId, 0);
                    float successRate = (float)successes / data.PlayerUsage[playerId];

                    patterns.Add(new PatternDetected
                    {
                        PatternId = $"player_{playerId}",
                        PatternType = PatternType.PlayerIsolation,
                        Description = $"Heavy reliance on single player ({usage:P0} usage)",
                        Frequency = usage,
                        SuccessRate = successRate,
                        TeamId = data.TeamId,
                        Metadata = new Dictionary<string, object> { { "playerId", playerId } }
                    });
                }
            }

            return patterns;
        }

        private List<PatternDetected> DetectSituationalPatterns(TeamPossessionData data)
        {
            var patterns = new List<PatternDetected>();

            // Check clutch tendencies
            if (data.ClutchPossessions.Count >= 3)
            {
                // See if they go to same player in clutch
                var clutchUsage = data.ClutchPossessions
                    .Where(p => !string.IsNullOrEmpty(p.PrimaryPlayerId))
                    .GroupBy(p => p.PrimaryPlayerId)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (clutchUsage != null)
                {
                    float clutchFreq = (float)clutchUsage.Count() / data.ClutchPossessions.Count;
                    if (clutchFreq >= 0.6f)
                    {
                        patterns.Add(new PatternDetected
                        {
                            PatternId = $"clutch_{clutchUsage.Key}",
                            PatternType = PatternType.ClutchTendency,
                            Description = $"Predictable clutch option ({clutchFreq:P0} of clutch possessions)",
                            Frequency = clutchFreq,
                            TeamId = data.TeamId,
                            Metadata = new Dictionary<string, object> { { "clutchPlayerId", clutchUsage.Key } }
                        });
                    }
                }

                // Check clutch play type
                var clutchPlayType = data.ClutchPossessions
                    .GroupBy(p => p.PlayType)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (clutchPlayType != null)
                {
                    float playFreq = (float)clutchPlayType.Count() / data.ClutchPossessions.Count;
                    if (playFreq >= 0.7f)
                    {
                        patterns.Add(new PatternDetected
                        {
                            PatternId = $"clutchplay_{clutchPlayType.Key}",
                            PatternType = PatternType.ClutchPlayType,
                            Description = $"Predictable clutch play type: {clutchPlayType.Key}",
                            Frequency = playFreq,
                            TeamId = data.TeamId,
                            Metadata = new Dictionary<string, object> { { "playType", clutchPlayType.Key } }
                        });
                    }
                }
            }

            return patterns;
        }

        #endregion

        #region Adaptation Suggestions

        private void SuggestAdaptation(PatternDetected pattern, string opponentTeamId)
        {
            GameAdaptation adaptation = null;

            switch (pattern.PatternType)
            {
                case PatternType.ShotZonePreference:
                    adaptation = CreateZoneDefenseAdaptation(pattern);
                    break;

                case PatternType.PlayTypePreference:
                    adaptation = CreatePlayTypeCounterAdaptation(pattern);
                    break;

                case PatternType.PlayerIsolation:
                    adaptation = CreatePlayerFocusAdaptation(pattern);
                    break;

                case PatternType.ClutchTendency:
                case PatternType.ClutchPlayType:
                    adaptation = CreateClutchDefenseAdaptation(pattern);
                    break;
            }

            if (adaptation != null)
            {
                adaptation.SourcePattern = pattern;
                adaptation.OpponentTeamId = opponentTeamId;

                // Fire insight for coach
                OnCoachingInsight?.Invoke(adaptation.CoachingTip);
            }
        }

        private GameAdaptation CreateZoneDefenseAdaptation(PatternDetected pattern)
        {
            var zone = (ShotZone)pattern.Metadata["zone"];
            string tip, adjustment;

            switch (zone)
            {
                case ShotZone.Paint:
                    tip = "Opponent attacking the paint heavily. Consider packing the paint or switching to zone.";
                    adjustment = "Help defense at rim, wall off driving lanes";
                    break;

                case ShotZone.ThreePointCorner:
                    tip = "Corner three is their weapon. Close out harder on corner shooters.";
                    adjustment = "Deny corner three, force baseline drive";
                    break;

                case ShotZone.ThreePointWing:
                    tip = "Wing threes are their go-to. Contest with hands up.";
                    adjustment = "Run shooters off the line, force mid-range";
                    break;

                case ShotZone.ThreePointTop:
                    tip = "Top of key threes being hunted. High hedge on picks.";
                    adjustment = "Aggressive pick and roll coverage, trap ball handler";
                    break;

                case ShotZone.MidRange:
                    tip = "Mid-range heavy offense. Let them take those shots.";
                    adjustment = "Sag off, protect three and rim";
                    break;

                default:
                    tip = $"High shot frequency from {zone}. Adjust defensive focus.";
                    adjustment = "Tighten coverage in that area";
                    break;
            }

            return new GameAdaptation
            {
                AdaptationType = AdaptationType.DefensiveZoneFocus,
                Description = adjustment,
                CoachingTip = tip,
                ImpactZone = zone,
                ExpectedImpact = pattern.SuccessRate > 0.45f ? 0.08f : 0.04f // Bigger impact if they're shooting well
            };
        }

        private GameAdaptation CreatePlayTypeCounterAdaptation(PatternDetected pattern)
        {
            var playType = (PlayType)pattern.Metadata["playType"];
            string tip, adjustment;

            switch (playType)
            {
                case PlayType.PickAndRoll:
                    tip = "Pick and roll is their bread and butter. Consider switching or trapping.";
                    adjustment = "Switch all screens, deny roll man";
                    break;

                case PlayType.Isolation:
                    tip = "Heavy isolation offense. Send help and make them pass.";
                    adjustment = "Early double teams on ISO, rotate quickly";
                    break;

                case PlayType.PostUp:
                    tip = "Post-heavy offense. Front the post or dig on catch.";
                    adjustment = "Deny post entry, double on catch";
                    break;

                case PlayType.Transition:
                    tip = "They're running! Get back in transition, no easy buckets.";
                    adjustment = "Sprint back, protect the paint first";
                    break;

                case PlayType.SpotUp:
                    tip = "Spot-up shooters feasting. Close out under control.";
                    adjustment = "No fly-bys, contest with hands up";
                    break;

                case PlayType.Handoff:
                    tip = "DHO action working well. Consider switching or going under.";
                    adjustment = "Switch handoffs, stay attached to shooter";
                    break;

                case PlayType.Cut:
                    tip = "Cutting and off-ball movement hurting us. Stay in passing lanes.";
                    adjustment = "Active hands, jump to the ball";
                    break;

                default:
                    tip = $"Predictable {playType} offense. Prepare defensive counter.";
                    adjustment = "Anticipate the action";
                    break;
            }

            return new GameAdaptation
            {
                AdaptationType = AdaptationType.PlayTypeCounter,
                Description = adjustment,
                CoachingTip = tip,
                CounteredPlayType = playType,
                ExpectedImpact = pattern.Frequency * 0.1f
            };
        }

        private GameAdaptation CreatePlayerFocusAdaptation(PatternDetected pattern)
        {
            var playerId = pattern.Metadata["playerId"].ToString();

            return new GameAdaptation
            {
                AdaptationType = AdaptationType.PlayerFocus,
                Description = $"Double team on catch, force ball out of hands",
                CoachingTip = "Their offense runs through one player. Take them out of the game.",
                FocusPlayerId = playerId,
                ExpectedImpact = pattern.Frequency * 0.12f
            };
        }

        private GameAdaptation CreateClutchDefenseAdaptation(PatternDetected pattern)
        {
            string playerId = null;
            PlayType? playType = null;

            if (pattern.Metadata.ContainsKey("clutchPlayerId"))
                playerId = pattern.Metadata["clutchPlayerId"].ToString();
            if (pattern.Metadata.ContainsKey("playType"))
                playType = (PlayType)pattern.Metadata["playType"];

            var tip = "Clutch situations: they're predictable. ";
            if (!string.IsNullOrEmpty(playerId))
                tip += "The ball's going to their go-to guy. ";
            if (playType.HasValue)
                tip += $"Expect {playType.Value}. ";

            return new GameAdaptation
            {
                AdaptationType = AdaptationType.ClutchDefense,
                Description = "Anticipate clutch actions, pre-position defense",
                CoachingTip = tip,
                FocusPlayerId = playerId,
                CounteredPlayType = playType,
                ExpectedImpact = 0.15f,
                IsClutchOnly = true
            };
        }

        #endregion

        #region Apply Adaptations

        /// <summary>
        /// Apply an adaptation to the defensive strategy.
        /// </summary>
        public void ApplyAdaptation(GameAdaptation adaptation, TeamStrategy defenseStrategy)
        {
            if (_activeAdaptations.Contains(adaptation))
                return;

            // Modify strategy based on adaptation
            switch (adaptation.AdaptationType)
            {
                case AdaptationType.DefensiveZoneFocus:
                    ApplyZoneFocusAdaptation(adaptation, defenseStrategy);
                    break;

                case AdaptationType.PlayTypeCounter:
                    ApplyPlayTypeCounterAdaptation(adaptation, defenseStrategy);
                    break;

                case AdaptationType.PlayerFocus:
                    ApplyPlayerFocusAdaptation(adaptation, defenseStrategy);
                    break;
            }

            _activeAdaptations.Add(adaptation);
            OnAdaptationApplied?.Invoke(adaptation);
        }

        private void ApplyZoneFocusAdaptation(GameAdaptation adaptation, TeamStrategy strategy)
        {
            switch (adaptation.ImpactZone)
            {
                case ShotZone.Paint:
                    strategy.DefensiveAggression = Math.Min(100, strategy.DefensiveAggression + 10);
                    break;

                case ShotZone.ThreePointCorner:
                case ShotZone.ThreePointWing:
                case ShotZone.ThreePointTop:
                    strategy.DefensiveAggression = Math.Min(100, strategy.DefensiveAggression + 5);
                    break;
            }
        }

        private void ApplyPlayTypeCounterAdaptation(GameAdaptation adaptation, TeamStrategy strategy)
        {
            switch (adaptation.CounteredPlayType)
            {
                case PlayType.PickAndRoll:
                    strategy.DefensiveAggression = Math.Min(100, strategy.DefensiveAggression + 8);
                    break;

                case PlayType.Transition:
                    strategy.Pace = Math.Max(0, strategy.Pace - 10); // Slow down to prevent fast breaks
                    break;
            }
        }

        private void ApplyPlayerFocusAdaptation(GameAdaptation adaptation, TeamStrategy strategy)
        {
            strategy.DefensiveAggression = Math.Min(100, strategy.DefensiveAggression + 12);
        }

        #endregion

        #region Queries

        /// <summary>
        /// Gets all detected patterns for a team.
        /// </summary>
        public List<PatternDetected> GetDetectedPatterns(string teamId)
        {
            if (!_teamData.TryGetValue(teamId, out var data))
                return new List<PatternDetected>();

            return data.DetectedPatterns
                .Select(id => new PatternDetected { PatternId = id })
                .ToList();
        }

        /// <summary>
        /// Gets all active adaptations.
        /// </summary>
        public List<GameAdaptation> GetActiveAdaptations()
        {
            return new List<GameAdaptation>(_activeAdaptations);
        }

        /// <summary>
        /// Gets shooting tendencies by zone.
        /// </summary>
        public Dictionary<ShotZone, (int attempts, int makes)> GetShotTendencies(string teamId)
        {
            var result = new Dictionary<ShotZone, (int, int)>();

            if (_teamData.TryGetValue(teamId, out var data))
            {
                foreach (var zone in data.ShotsByZone.Keys)
                {
                    result[zone] = (data.ShotsByZone[zone], data.MadeByZone.GetValueOrDefault(zone, 0));
                }
            }

            return result;
        }

        /// <summary>
        /// Gets play type distribution.
        /// </summary>
        public Dictionary<PlayType, float> GetPlayTypeDistribution(string teamId)
        {
            var result = new Dictionary<PlayType, float>();

            if (_teamData.TryGetValue(teamId, out var data))
            {
                int total = data.PlayTypeCounts.Values.Sum();
                if (total > 0)
                {
                    foreach (var pt in data.PlayTypeCounts.Keys)
                    {
                        result[pt] = (float)data.PlayTypeCounts[pt] / total;
                    }
                }
            }

            return result;
        }

        #endregion
    }

    #region Supporting Types

    [Serializable]
    public class TeamPossessionData
    {
        public string TeamId;
        public string TeamName;
        public List<PossessionRecord> AllPossessions = new List<PossessionRecord>();
        public Dictionary<ShotZone, int> ShotsByZone = new Dictionary<ShotZone, int>();
        public Dictionary<ShotZone, int> MadeByZone = new Dictionary<ShotZone, int>();
        public Dictionary<PlayType, int> PlayTypeCounts = new Dictionary<PlayType, int>();
        public Dictionary<PlayType, int> PlayTypeSuccess = new Dictionary<PlayType, int>();
        public Dictionary<string, int> PlayerUsage = new Dictionary<string, int>();
        public Dictionary<string, int> PlayerSuccesses = new Dictionary<string, int>();
        public Dictionary<int, int> QuarterPossessions = new Dictionary<int, int>();
        public List<PossessionRecord> ClutchPossessions = new List<PossessionRecord>();
        public HashSet<string> DetectedPatterns = new HashSet<string>();

        public TeamPossessionData(string teamId, string teamName)
        {
            TeamId = teamId;
            TeamName = teamName;

            // Initialize zone and play type dictionaries
            foreach (ShotZone zone in Enum.GetValues(typeof(ShotZone)))
            {
                ShotsByZone[zone] = 0;
                MadeByZone[zone] = 0;
            }
            foreach (PlayType pt in Enum.GetValues(typeof(PlayType)))
            {
                PlayTypeCounts[pt] = 0;
                PlayTypeSuccess[pt] = 0;
            }
            for (int q = 1; q <= 4; q++)
            {
                QuarterPossessions[q] = 0;
            }
        }
    }

    [Serializable]
    public class PossessionRecord
    {
        public string OffenseTeamId;
        public int Quarter;
        public float GameClock;
        public bool IsClutch;
        public ShotType ShotType;
        public ShotZone ShotZone;
        public bool ShotMade;
        public int PointsScored;
        public PlayType PlayType;
        public string PrimaryPlayerId;
        public string AssistPlayerId;
        public bool WasTurnover;
    }

    [Serializable]
    public class PatternDetected
    {
        public string PatternId;
        public PatternType PatternType;
        public string Description;
        public float Frequency;
        public float SuccessRate;
        public string TeamId;
        public Dictionary<string, object> Metadata = new Dictionary<string, object>();
    }

    [Serializable]
    public class GameAdaptation
    {
        public AdaptationType AdaptationType;
        public string Description;
        public string CoachingTip;
        public PatternDetected SourcePattern;
        public string OpponentTeamId;
        public float ExpectedImpact;
        public bool IsClutchOnly;

        // Specific adaptation data
        public ShotZone? ImpactZone;
        public PlayType? CounteredPlayType;
        public string FocusPlayerId;
    }

    public enum PatternType
    {
        ShotZonePreference,
        PlayTypePreference,
        PlayerIsolation,
        ClutchTendency,
        ClutchPlayType,
        QuarterTendency,
        TempoChange
    }

    public enum AdaptationType
    {
        DefensiveZoneFocus,
        PlayTypeCounter,
        PlayerFocus,
        ClutchDefense,
        TempoAdjustment,
        RotationChange
    }

    public enum ShotZone
    {
        Paint,
        MidRange,
        ThreePointCorner,
        ThreePointWing,
        ThreePointTop
    }

    public enum ShotType
    {
        None,
        Layup,
        Dunk,
        Floater,
        MidRange,
        ThreePointer,
        PostMove,
        FreeThrow
    }

    public enum PlayType
    {
        PickAndRoll,
        Isolation,
        PostUp,
        Transition,
        SpotUp,
        Handoff,
        Cut,
        OffScreens,
        Putback
    }

    #endregion
}
