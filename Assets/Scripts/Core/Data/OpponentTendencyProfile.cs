using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Comprehensive analysis of an opponent's tendencies, patterns, and weaknesses.
    /// Generated from scouting data and used for game planning.
    /// </summary>
    [Serializable]
    public class OpponentTendencyProfile
    {
        // ==================== IDENTITY ====================
        public string TeamId;
        public string TeamName;
        public string HeadCoachId;
        public string HeadCoachName;

        /// <summary>
        /// When this profile was last updated.
        /// </summary>
        public DateTime LastUpdated;

        /// <summary>
        /// Data quality (0-100) - how much scouting went into this profile.
        /// </summary>
        [Range(0, 100)] public int DataQuality = 50;

        // ==================== COACH PERSONALITY ====================

        /// <summary>
        /// Reference to the coach's personality profile.
        /// </summary>
        public string CoachPersonalityId;

        /// <summary>
        /// How predictable the coach is (0-100).
        /// Low = adapts frequently, High = sticks to patterns.
        /// </summary>
        [Range(0, 100)] public int CoachPredictability = 50;

        /// <summary>
        /// Coach's aggression level (0-100).
        /// </summary>
        [Range(0, 100)] public int CoachAggression = 50;

        // ==================== OFFENSIVE TENDENCIES ====================
        [Header("Offensive Tendencies")]

        /// <summary>Frequency of pick-and-roll actions (0-100)</summary>
        [Range(0, 100)] public int PnRFrequency = 50;

        /// <summary>Frequency of isolation plays (0-100)</summary>
        [Range(0, 100)] public int IsolationFrequency = 30;

        /// <summary>Frequency of post-up plays (0-100)</summary>
        [Range(0, 100)] public int PostUpFrequency = 25;

        /// <summary>Three-point attempt rate (0-100)</summary>
        [Range(0, 100)] public int ThreePointRate = 40;

        /// <summary>Transition offense frequency (0-100)</summary>
        [Range(0, 100)] public int TransitionRate = 35;

        /// <summary>Offensive rebounding aggressiveness (0-100)</summary>
        [Range(0, 100)] public int OffensiveReboundingRate = 30;

        /// <summary>Preferred pace (1=slow, 100=fast)</summary>
        [Range(1, 100)] public int PreferredPace = 50;

        /// <summary>Shot selection patience (0=quick triggers, 100=patient)</summary>
        [Range(0, 100)] public int ShotPatience = 50;

        /// <summary>Ball movement tendency (0=iso heavy, 100=ball movement)</summary>
        [Range(0, 100)] public int BallMovement = 50;

        // ==================== DEFENSIVE TENDENCIES ====================
        [Header("Defensive Tendencies")]

        /// <summary>How often they use zone defense (0-100)</summary>
        [Range(0, 100)] public int ZoneUsage = 20;

        /// <summary>How often they trap/double (0-100)</summary>
        [Range(0, 100)] public int TrapFrequency = 25;

        /// <summary>How often they switch on screens (0-100)</summary>
        [Range(0, 100)] public int SwitchTendency = 40;

        /// <summary>Help defense aggressiveness (0-100)</summary>
        [Range(0, 100)] public int HelpDefenseAggression = 50;

        /// <summary>Full-court press tendency (0-100)</summary>
        [Range(0, 100)] public int PressFrequency = 15;

        /// <summary>Close-out aggressiveness (0-100)</summary>
        [Range(0, 100)] public int CloseoutAggression = 50;

        /// <summary>Primary defensive scheme</summary>
        public DefensiveScheme PrimaryDefense = DefensiveScheme.ManToMan;

        /// <summary>Secondary defensive scheme (situational)</summary>
        public DefensiveScheme SecondaryDefense = DefensiveScheme.ManToMan;

        // ==================== KEY PLAYER TENDENCIES ====================
        [Header("Key Players")]

        /// <summary>
        /// Individual tendencies for key players.
        /// </summary>
        public List<OpponentPlayerTendency> KeyPlayerTendencies = new List<OpponentPlayerTendency>();

        /// <summary>
        /// Players to prioritize stopping.
        /// </summary>
        public List<string> PriorityDefenders = new List<string>();

        /// <summary>
        /// Players who can be exploited defensively.
        /// </summary>
        public List<string> DefensiveWeakLinks = new List<string>();

        // ==================== ADJUSTMENT PATTERNS ====================
        [Header("Adjustment Patterns")]

        /// <summary>
        /// What they typically do when losing by 10+.
        /// </summary>
        public List<AdjustmentPattern> WhenLosingBig = new List<AdjustmentPattern>();

        /// <summary>
        /// What they typically do when winning by 10+.
        /// </summary>
        public List<AdjustmentPattern> WhenWinningBig = new List<AdjustmentPattern>();

        /// <summary>
        /// Typical halftime adjustments.
        /// </summary>
        public List<AdjustmentPattern> HalftimeAdjustments = new List<AdjustmentPattern>();

        /// <summary>
        /// Responses when their offense is struggling.
        /// </summary>
        public List<AdjustmentPattern> WhenOffenseStruggles = new List<AdjustmentPattern>();

        /// <summary>
        /// Responses when their defense is getting torched.
        /// </summary>
        public List<AdjustmentPattern> WhenDefenseStruggles = new List<AdjustmentPattern>();

        // ==================== CLUTCH TENDENCIES ====================
        [Header("Clutch Situations")]

        /// <summary>
        /// Primary go-to player in clutch situations.
        /// </summary>
        public string ClutchGoToPlayer;

        /// <summary>
        /// Secondary clutch option.
        /// </summary>
        public string ClutchSecondaryOption;

        /// <summary>
        /// Preferred clutch play type.
        /// </summary>
        public ClutchPlayType PreferredClutchPlay = ClutchPlayType.Isolation;

        /// <summary>
        /// How often they call timeout in clutch (0-100).
        /// </summary>
        [Range(0, 100)] public int ClutchTimeoutTendency = 70;

        /// <summary>
        /// Late-game defensive strategy.
        /// </summary>
        public LateGameDefense LateGameDefenseStrategy = LateGameDefense.Standard;

        /// <summary>
        /// Intentional fouling tendency when trailing (0-100).
        /// </summary>
        [Range(0, 100)] public int IntentionalFoulTendency = 50;

        // ==================== EXPLOITABLE WEAKNESSES ====================
        [Header("Weaknesses")]

        /// <summary>
        /// Known weaknesses that can be exploited.
        /// </summary>
        public List<ExploitableWeakness> Weaknesses = new List<ExploitableWeakness>();

        /// <summary>
        /// Areas where they are strong (avoid).
        /// </summary>
        public List<TeamStrength> Strengths = new List<TeamStrength>();

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Overall offensive style classification.
        /// </summary>
        public OffensiveStyle OffensiveStyleClassification
        {
            get
            {
                if (ThreePointRate >= 60 && BallMovement >= 60)
                    return OffensiveStyle.ModernSpacing;
                if (PnRFrequency >= 60)
                    return OffensiveStyle.PnRHeavy;
                if (IsolationFrequency >= 50)
                    return OffensiveStyle.StarDriven;
                if (PostUpFrequency >= 40)
                    return OffensiveStyle.InsideOut;
                if (TransitionRate >= 50)
                    return OffensiveStyle.RunAndGun;
                if (BallMovement >= 70)
                    return OffensiveStyle.BallMovement;
                return OffensiveStyle.Balanced;
            }
        }

        /// <summary>
        /// Overall defensive style classification.
        /// </summary>
        public DefensiveStyle DefensiveStyleClassification
        {
            get
            {
                if (ZoneUsage >= 40)
                    return DefensiveStyle.ZoneBased;
                if (TrapFrequency >= 50)
                    return DefensiveStyle.Trapping;
                if (SwitchTendency >= 60)
                    return DefensiveStyle.SwitchEverything;
                if (PressFrequency >= 40)
                    return DefensiveStyle.FullCourtPressure;
                if (HelpDefenseAggression >= 70)
                    return DefensiveStyle.HelpAndRecover;
                return DefensiveStyle.Traditional;
            }
        }

        /// <summary>
        /// How reliable this profile is for game planning.
        /// </summary>
        public ProfileReliability Reliability
        {
            get
            {
                if (DataQuality >= 80) return ProfileReliability.Excellent;
                if (DataQuality >= 60) return ProfileReliability.Good;
                if (DataQuality >= 40) return ProfileReliability.Fair;
                return ProfileReliability.Limited;
            }
        }

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates a basic profile with default tendencies.
        /// </summary>
        public static OpponentTendencyProfile CreateBasic(string teamId, string teamName)
        {
            return new OpponentTendencyProfile
            {
                TeamId = teamId,
                TeamName = teamName,
                LastUpdated = DateTime.Now,
                DataQuality = 30 // Basic = limited data
            };
        }

        /// <summary>
        /// Creates a profile from scouting data.
        /// </summary>
        public static OpponentTendencyProfile CreateFromScouting(
            string teamId,
            string teamName,
            int scoutingDepth,
            TeamSeasonStats stats)
        {
            var profile = new OpponentTendencyProfile
            {
                TeamId = teamId,
                TeamName = teamName,
                LastUpdated = DateTime.Now,
                DataQuality = scoutingDepth
            };

            // Populate from stats if available
            if (stats != null)
            {
                profile.ThreePointRate = Mathf.RoundToInt(stats.ThreePointAttemptRate * 100);
                profile.PreferredPace = Mathf.RoundToInt(stats.Pace);
                profile.TransitionRate = Mathf.RoundToInt(stats.FastBreakPointsPerGame / 0.15f); // Normalize
            }

            return profile;
        }

        // ==================== METHODS ====================

        /// <summary>
        /// Gets a game planning summary.
        /// </summary>
        public string GetGamePlanSummary()
        {
            var summary = new System.Text.StringBuilder();

            summary.AppendLine($"=== {TeamName} Scouting Report ===");
            summary.AppendLine($"Data Quality: {Reliability} ({DataQuality}%)");
            summary.AppendLine();

            summary.AppendLine($"Offensive Style: {OffensiveStyleClassification}");
            summary.AppendLine($"- PnR Frequency: {PnRFrequency}%");
            summary.AppendLine($"- 3PT Rate: {ThreePointRate}%");
            summary.AppendLine($"- Pace: {(PreferredPace > 60 ? "Fast" : PreferredPace < 40 ? "Slow" : "Medium")}");
            summary.AppendLine();

            summary.AppendLine($"Defensive Style: {DefensiveStyleClassification}");
            summary.AppendLine($"- Zone Usage: {ZoneUsage}%");
            summary.AppendLine($"- Switch Tendency: {SwitchTendency}%");
            summary.AppendLine();

            if (Weaknesses.Count > 0)
            {
                summary.AppendLine("KEY WEAKNESSES:");
                foreach (var weakness in Weaknesses.Take(3))
                {
                    summary.AppendLine($"- {weakness.Description}");
                }
            }

            return summary.ToString();
        }

        /// <summary>
        /// Gets the most likely adjustment for a given situation.
        /// </summary>
        public AdjustmentPattern GetLikelyAdjustment(GameSituation situation)
        {
            List<AdjustmentPattern> patterns = situation switch
            {
                GameSituation.LosingBig => WhenLosingBig,
                GameSituation.WinningBig => WhenWinningBig,
                GameSituation.Halftime => HalftimeAdjustments,
                GameSituation.OffenseStruggling => WhenOffenseStruggles,
                GameSituation.DefenseStruggling => WhenDefenseStruggles,
                _ => new List<AdjustmentPattern>()
            };

            return patterns.OrderByDescending(p => p.Likelihood).FirstOrDefault();
        }

        /// <summary>
        /// Updates the profile with new game data.
        /// </summary>
        public void UpdateFromGameData(GameData data)
        {
            if (data == null) return;

            // Recalculate tendencies based on observed data
            // This would use statistical analysis of recent games

            LastUpdated = DateTime.Now;
            DataQuality = Mathf.Min(100, DataQuality + 5); // Each game improves quality
        }
    }

    // ==================== SUPPORTING TYPES ====================

    /// <summary>
    /// Individual player tendency information.
    /// </summary>
    [Serializable]
    public class OpponentPlayerTendency
    {
        public string PlayerId;
        public string PlayerName;
        public string Position;

        /// <summary>Preferred shooting zones</summary>
        public List<string> PreferredZones = new List<string>();

        /// <summary>Go-to moves</summary>
        public List<string> SignatureMoves = new List<string>();

        /// <summary>Defensive weaknesses</summary>
        public List<string> DefensiveWeaknesses = new List<string>();

        /// <summary>How to defend this player</summary>
        public string DefensiveStrategy;

        /// <summary>Usage rate when on floor (0-100)</summary>
        [Range(0, 100)] public int UsageRate = 20;

        /// <summary>Clutch performance rating (0-100)</summary>
        [Range(0, 100)] public int ClutchRating = 50;
    }

    /// <summary>
    /// A predictable adjustment pattern.
    /// </summary>
    [Serializable]
    public class AdjustmentPattern
    {
        public AdjustmentType Type;
        public string Description;

        /// <summary>How likely this adjustment is (0-100)</summary>
        [Range(0, 100)] public int Likelihood = 50;

        /// <summary>Counter-strategy recommendation</summary>
        public string CounterStrategy;

        /// <summary>Trigger conditions</summary>
        public List<string> Triggers = new List<string>();
    }

    /// <summary>
    /// An exploitable weakness.
    /// </summary>
    [Serializable]
    public class ExploitableWeakness
    {
        public WeaknessCategory Category;
        public string Description;

        /// <summary>How severe this weakness is (0-100)</summary>
        [Range(0, 100)] public int Severity = 50;

        /// <summary>How to exploit it</summary>
        public string ExploitStrategy;

        /// <summary>Players involved</summary>
        public List<string> InvolvedPlayerIds = new List<string>();
    }

    /// <summary>
    /// A team strength to avoid.
    /// </summary>
    [Serializable]
    public class TeamStrength
    {
        public StrengthCategory Category;
        public string Description;

        /// <summary>How dominant this strength is (0-100)</summary>
        [Range(0, 100)] public int Dominance = 50;

        /// <summary>How to neutralize it</summary>
        public string NeutralizationStrategy;
    }

    /// <summary>
    /// Team season statistics for profile generation.
    /// </summary>
    public class TeamSeasonStats
    {
        public float PointsPerGame;
        public float PointsAllowedPerGame;
        public float Pace;
        public float ThreePointAttemptRate;
        public float ThreePointPercentage;
        public float TurnoverRate;
        public float OffensiveRating;
        public float DefensiveRating;
        public float FastBreakPointsPerGame;
        public float PointsInPaint;
    }

    /// <summary>
    /// Game data for profile updates.
    /// </summary>
    public class GameData
    {
        public string GameId;
        public DateTime Date;
        public int PointsScored;
        public int PointsAllowed;
        public Dictionary<string, int> PlayTypeFrequencies;
    }

    // ==================== ENUMS ====================

    public enum OffensiveStyle
    {
        Balanced,
        ModernSpacing,
        PnRHeavy,
        StarDriven,
        InsideOut,
        RunAndGun,
        BallMovement
    }

    public enum DefensiveStyle
    {
        Traditional,
        ZoneBased,
        Trapping,
        SwitchEverything,
        FullCourtPressure,
        HelpAndRecover
    }

    public enum DefensiveScheme
    {
        ManToMan,
        Zone23,
        Zone32,
        Zone131,
        MatchupZone,
        BoxAndOne,
        Triangle
    }

    public enum ClutchPlayType
    {
        Isolation,
        PickAndRoll,
        PostUp,
        OffBallScreen,
        DesignedPlay
    }

    public enum LateGameDefense
    {
        Standard,
        Switch,
        Trap,
        Zone,
        FoulToStop
    }

    public enum GameSituation
    {
        Normal,
        LosingBig,
        WinningBig,
        Halftime,
        OffenseStruggling,
        DefenseStruggling,
        ClutchTime,
        EndOfQuarter
    }

    public enum AdjustmentType
    {
        ChangeDefense,
        ChangeLineup,
        ChangePace,
        ChangePlayCalling,
        TargetPlayer,
        ChangeMatchups,
        GoSmall,
        GoBig,
        IncreaseTrapping,
        SlowDown,
        SpeedUp
    }

    public enum WeaknessCategory
    {
        PerimeterDefense,
        InteriorDefense,
        Rebounding,
        Turnovers,
        FreeThrowShooting,
        ThreePointDefense,
        TransitionDefense,
        Conditioning,
        SpecificPlayer,
        SchemeVulnerability
    }

    public enum StrengthCategory
    {
        ThreePointShooting,
        InteriorScoring,
        FastBreak,
        HalfCourtDefense,
        Rebounding,
        DepthAdvantage,
        SuperstarDominance,
        Coaching
    }

    public enum ProfileReliability
    {
        Limited,
        Fair,
        Good,
        Excellent
    }
}
