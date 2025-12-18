using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Types of injuries that can occur.
    /// </summary>
    public enum InjuryType
    {
        None,
        Ankle,
        Knee_Minor,      // Sprain, soreness
        Knee_ACL,        // Season-ending
        Knee_Meniscus,   // Moderate to major
        Back,
        Shoulder,
        Concussion,
        Hamstring,
        Wrist,
        Foot,
        Hip,
        Groin,
        Calf,
        Quad,
        Illness          // Non-physical (flu, COVID, etc.)
    }

    /// <summary>
    /// Severity levels affecting recovery time.
    /// </summary>
    public enum InjurySeverity
    {
        Minor,           // 1-7 days
        Moderate,        // 1-3 weeks
        Major,           // 3-8 weeks
        Severe           // Season-ending
    }

    /// <summary>
    /// Game day availability status.
    /// </summary>
    public enum InjuryStatus
    {
        Healthy,         // 100% to play
        Probable,        // 90% to play, 0 days remaining, minor lingering
        Questionable,    // 50-75% to play, 1-2 days remaining
        Doubtful,        // 25% to play, 3-4 days remaining
        Out              // 0% to play, 5+ days remaining
    }

    /// <summary>
    /// Records a specific injury occurrence.
    /// </summary>
    [Serializable]
    public class InjuryEvent
    {
        public string PlayerId;
        public InjuryType Type;
        public InjurySeverity Severity;
        public int DaysOut;
        public int OriginalDaysOut;
        public DateTime DateOccurred;
        public string Description;
        public bool IsReInjury;        // Same body part as previous injury
        public bool OccurredInGame;    // vs training/practice

        /// <summary>
        /// Generate a human-readable description of the injury.
        /// </summary>
        public string GetDescription()
        {
            if (!string.IsNullOrEmpty(Description))
                return Description;

            string severityText = Severity switch
            {
                InjurySeverity.Minor => "minor",
                InjurySeverity.Moderate => "moderate",
                InjurySeverity.Major => "significant",
                InjurySeverity.Severe => "severe",
                _ => ""
            };

            string bodyPart = Type switch
            {
                InjuryType.Ankle => "ankle sprain",
                InjuryType.Knee_Minor => "knee soreness",
                InjuryType.Knee_ACL => "torn ACL",
                InjuryType.Knee_Meniscus => "meniscus tear",
                InjuryType.Back => "back spasms",
                InjuryType.Shoulder => "shoulder injury",
                InjuryType.Concussion => "concussion",
                InjuryType.Hamstring => "hamstring strain",
                InjuryType.Wrist => "wrist injury",
                InjuryType.Foot => "foot injury",
                InjuryType.Hip => "hip injury",
                InjuryType.Groin => "groin strain",
                InjuryType.Calf => "calf strain",
                InjuryType.Quad => "quad strain",
                InjuryType.Illness => "illness",
                _ => "injury"
            };

            string reinjuryText = IsReInjury ? "Re-aggravated " : "";
            return $"{reinjuryText}{severityText} {bodyPart}".Trim();
        }

        /// <summary>
        /// Get expected recovery time text.
        /// </summary>
        public string GetRecoveryText()
        {
            if (DaysOut <= 0) return "Day-to-day";
            if (DaysOut <= 7) return $"{DaysOut} days";
            if (DaysOut <= 14) return "1-2 weeks";
            if (DaysOut <= 28) return "2-4 weeks";
            if (DaysOut <= 56) return "4-8 weeks";
            return "Indefinite";
        }
    }

    /// <summary>
    /// Tracks injury history for a specific body part.
    /// Used to calculate re-injury risk.
    /// </summary>
    [Serializable]
    public class InjuryHistory
    {
        public InjuryType BodyPart;
        public int TimesInjured;
        public DateTime LastInjuryDate;

        /// <summary>
        /// Re-injury risk modifier (+25% per previous injury to same body part).
        /// </summary>
        public float ReInjuryRiskModifier => TimesInjured * 0.25f;
    }

    /// <summary>
    /// Context for injury risk calculation.
    /// </summary>
    [Serializable]
    public class InjuryContext
    {
        public bool IsGameAction;          // vs practice
        public bool IsContactPlay;         // Drives, rebounds, screens, post-ups
        public float PlayerEnergy;         // Current energy level (0-100)
        public int MinutesThisGame;        // Fatigue factor within game
        public int MinutesLast7Days;       // Workload factor
        public bool IsBackToBack;          // Playing consecutive days
        public bool IsPlayoffs;            // Higher intensity
        public int GamesInLast5Days;       // Schedule density
    }

    /// <summary>
    /// Load management advice for a player.
    /// </summary>
    [Serializable]
    public class LoadManagementAdvice
    {
        public string PlayerId;
        public int RecommendedMaxMinutes;
        public bool ShouldRest;
        public string Reason;
        public float InjuryRiskLevel;      // 0-1 (0=low, 1=high)
        public LoadManagementUrgency Urgency;

        public string GetReasonText()
        {
            if (!string.IsNullOrEmpty(Reason))
                return Reason;

            if (ShouldRest)
                return "Player needs rest to reduce injury risk";
            if (RecommendedMaxMinutes < 25)
                return $"Limit to {RecommendedMaxMinutes} minutes due to fatigue";
            return "Normal workload acceptable";
        }
    }

    public enum LoadManagementUrgency
    {
        Low,         // Can play full minutes
        Medium,      // Should limit minutes
        High,        // Should consider resting
        Critical     // Must rest
    }

    /// <summary>
    /// Recovery time ranges for each injury type and severity.
    /// </summary>
    public static class InjuryRecoveryTables
    {
        /// <summary>
        /// Get recovery time in days for an injury.
        /// </summary>
        public static (int min, int max) GetRecoveryRange(InjuryType type, InjurySeverity severity)
        {
            return (type, severity) switch
            {
                // Ankle injuries
                (InjuryType.Ankle, InjurySeverity.Minor) => (3, 5),
                (InjuryType.Ankle, InjurySeverity.Moderate) => (7, 14),
                (InjuryType.Ankle, InjurySeverity.Major) => (21, 28),
                (InjuryType.Ankle, InjurySeverity.Severe) => (42, 56),

                // Knee injuries (minor - soreness, sprain)
                (InjuryType.Knee_Minor, InjurySeverity.Minor) => (5, 7),
                (InjuryType.Knee_Minor, InjurySeverity.Moderate) => (14, 21),
                (InjuryType.Knee_Minor, InjurySeverity.Major) => (28, 42),
                (InjuryType.Knee_Minor, InjurySeverity.Severe) => (56, 84),

                // ACL - always severe (season-ending)
                (InjuryType.Knee_ACL, _) => (270, 365),

                // Meniscus
                (InjuryType.Knee_Meniscus, InjurySeverity.Minor) => (14, 21),
                (InjuryType.Knee_Meniscus, InjurySeverity.Moderate) => (28, 42),
                (InjuryType.Knee_Meniscus, InjurySeverity.Major) => (56, 84),
                (InjuryType.Knee_Meniscus, InjurySeverity.Severe) => (120, 180),

                // Back injuries
                (InjuryType.Back, InjurySeverity.Minor) => (3, 7),
                (InjuryType.Back, InjurySeverity.Moderate) => (7, 21),
                (InjuryType.Back, InjurySeverity.Major) => (28, 42),
                (InjuryType.Back, InjurySeverity.Severe) => (60, 120),

                // Shoulder injuries
                (InjuryType.Shoulder, InjurySeverity.Minor) => (5, 10),
                (InjuryType.Shoulder, InjurySeverity.Moderate) => (14, 28),
                (InjuryType.Shoulder, InjurySeverity.Major) => (42, 56),
                (InjuryType.Shoulder, InjurySeverity.Severe) => (90, 180),

                // Concussion (protocol-based)
                (InjuryType.Concussion, InjurySeverity.Minor) => (7, 10),
                (InjuryType.Concussion, InjurySeverity.Moderate) => (14, 28),
                (InjuryType.Concussion, InjurySeverity.Major) => (28, 56),
                (InjuryType.Concussion, InjurySeverity.Severe) => (60, 120),

                // Hamstring
                (InjuryType.Hamstring, InjurySeverity.Minor) => (5, 7),
                (InjuryType.Hamstring, InjurySeverity.Moderate) => (14, 21),
                (InjuryType.Hamstring, InjurySeverity.Major) => (28, 42),
                (InjuryType.Hamstring, InjurySeverity.Severe) => (56, 84),

                // Groin
                (InjuryType.Groin, InjurySeverity.Minor) => (5, 7),
                (InjuryType.Groin, InjurySeverity.Moderate) => (14, 21),
                (InjuryType.Groin, InjurySeverity.Major) => (28, 42),
                (InjuryType.Groin, InjurySeverity.Severe) => (56, 84),

                // Calf
                (InjuryType.Calf, InjurySeverity.Minor) => (5, 7),
                (InjuryType.Calf, InjurySeverity.Moderate) => (14, 21),
                (InjuryType.Calf, InjurySeverity.Major) => (28, 42),
                (InjuryType.Calf, InjurySeverity.Severe) => (42, 70),

                // Quad
                (InjuryType.Quad, InjurySeverity.Minor) => (5, 7),
                (InjuryType.Quad, InjurySeverity.Moderate) => (14, 21),
                (InjuryType.Quad, InjurySeverity.Major) => (28, 42),
                (InjuryType.Quad, InjurySeverity.Severe) => (56, 84),

                // Wrist
                (InjuryType.Wrist, InjurySeverity.Minor) => (3, 5),
                (InjuryType.Wrist, InjurySeverity.Moderate) => (7, 14),
                (InjuryType.Wrist, InjurySeverity.Major) => (21, 35),
                (InjuryType.Wrist, InjurySeverity.Severe) => (42, 84),

                // Foot
                (InjuryType.Foot, InjurySeverity.Minor) => (5, 7),
                (InjuryType.Foot, InjurySeverity.Moderate) => (14, 21),
                (InjuryType.Foot, InjurySeverity.Major) => (35, 56),
                (InjuryType.Foot, InjurySeverity.Severe) => (84, 180),

                // Hip
                (InjuryType.Hip, InjurySeverity.Minor) => (3, 7),
                (InjuryType.Hip, InjurySeverity.Moderate) => (14, 21),
                (InjuryType.Hip, InjurySeverity.Major) => (28, 42),
                (InjuryType.Hip, InjurySeverity.Severe) => (56, 120),

                // Illness
                (InjuryType.Illness, InjurySeverity.Minor) => (1, 3),
                (InjuryType.Illness, InjurySeverity.Moderate) => (3, 7),
                (InjuryType.Illness, InjurySeverity.Major) => (7, 14),
                (InjuryType.Illness, InjurySeverity.Severe) => (14, 28),

                // Default
                _ => (7, 14)
            };
        }

        /// <summary>
        /// Get weighted random recovery time within range.
        /// </summary>
        public static int GetRecoveryDays(InjuryType type, InjurySeverity severity, System.Random rng = null)
        {
            rng ??= new System.Random();
            var (min, max) = GetRecoveryRange(type, severity);
            return rng.Next(min, max + 1);
        }

        /// <summary>
        /// Check if this injury type affects this body part (for re-injury tracking).
        /// </summary>
        public static bool AffectsSameArea(InjuryType type1, InjuryType type2)
        {
            // Group knee injuries together
            if (IsKneeInjury(type1) && IsKneeInjury(type2)) return true;

            // Group leg muscle injuries (hamstring, quad, calf, groin)
            if (IsLegMuscleInjury(type1) && IsLegMuscleInjury(type2)) return true;

            // Direct match
            return type1 == type2;
        }

        public static bool IsKneeInjury(InjuryType type)
        {
            return type == InjuryType.Knee_Minor ||
                   type == InjuryType.Knee_ACL ||
                   type == InjuryType.Knee_Meniscus;
        }

        public static bool IsLegMuscleInjury(InjuryType type)
        {
            return type == InjuryType.Hamstring ||
                   type == InjuryType.Quad ||
                   type == InjuryType.Calf ||
                   type == InjuryType.Groin;
        }

        public static bool IsSeasonEnding(InjuryType type, InjurySeverity severity)
        {
            // ACL is always season-ending
            if (type == InjuryType.Knee_ACL) return true;

            // Severe injuries with 90+ day recovery
            var (_, max) = GetRecoveryRange(type, severity);
            return max >= 90;
        }
    }

    /// <summary>
    /// Weights for injury type probability based on play action.
    /// </summary>
    public static class InjuryProbabilityWeights
    {
        /// <summary>
        /// Get injury type weights for contact plays (drives, post-ups, rebounds).
        /// </summary>
        public static Dictionary<InjuryType, float> GetContactPlayWeights()
        {
            return new Dictionary<InjuryType, float>
            {
                { InjuryType.Ankle, 0.25f },
                { InjuryType.Knee_Minor, 0.15f },
                { InjuryType.Knee_ACL, 0.02f },
                { InjuryType.Knee_Meniscus, 0.05f },
                { InjuryType.Back, 0.08f },
                { InjuryType.Shoulder, 0.10f },
                { InjuryType.Concussion, 0.05f },
                { InjuryType.Hamstring, 0.08f },
                { InjuryType.Groin, 0.05f },
                { InjuryType.Calf, 0.05f },
                { InjuryType.Quad, 0.05f },
                { InjuryType.Wrist, 0.04f },
                { InjuryType.Foot, 0.03f }
            };
        }

        /// <summary>
        /// Get injury type weights for non-contact plays (jumpers, running).
        /// </summary>
        public static Dictionary<InjuryType, float> GetNonContactPlayWeights()
        {
            return new Dictionary<InjuryType, float>
            {
                { InjuryType.Ankle, 0.20f },
                { InjuryType.Knee_Minor, 0.10f },
                { InjuryType.Knee_ACL, 0.03f },
                { InjuryType.Hamstring, 0.20f },
                { InjuryType.Groin, 0.12f },
                { InjuryType.Calf, 0.15f },
                { InjuryType.Quad, 0.10f },
                { InjuryType.Back, 0.05f },
                { InjuryType.Foot, 0.05f }
            };
        }

        /// <summary>
        /// Get severity distribution based on injury type.
        /// </summary>
        public static Dictionary<InjurySeverity, float> GetSeverityWeights(InjuryType type)
        {
            // ACL is always severe
            if (type == InjuryType.Knee_ACL)
            {
                return new Dictionary<InjurySeverity, float>
                {
                    { InjurySeverity.Severe, 1.0f }
                };
            }

            // Standard distribution
            return new Dictionary<InjurySeverity, float>
            {
                { InjurySeverity.Minor, 0.50f },
                { InjurySeverity.Moderate, 0.30f },
                { InjurySeverity.Major, 0.15f },
                { InjurySeverity.Severe, 0.05f }
            };
        }
    }
}
