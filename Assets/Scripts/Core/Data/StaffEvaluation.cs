using System;
using System.Collections.Generic;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Text-based evaluation of a staff member.
    /// No numeric attributes are exposed - only descriptive assessments.
    /// Follows the "no visible attributes" design philosophy.
    /// </summary>
    [Serializable]
    public class StaffEvaluation
    {
        public string ProfileId;
        public string PersonName;
        public UnifiedRole Role;

        /// <summary>
        /// Overall tier for color coding (hidden from user display).
        /// </summary>
        public EvaluationTier Tier;

        /// <summary>
        /// One-line overall assessment (e.g., "Elite coach with championship experience").
        /// </summary>
        public string OverallAssessment;

        /// <summary>
        /// Summary of key strengths (2-3 sentences).
        /// </summary>
        public string StrengthsSummary;

        /// <summary>
        /// Summary of weaknesses or areas for improvement (2-3 sentences).
        /// </summary>
        public string WeaknessesSummary;

        /// <summary>
        /// Individual skill assessments with grades and notes.
        /// </summary>
        public List<StaffSkillAssessment> SkillAssessments = new List<StaffSkillAssessment>();

        /// <summary>
        /// Career trajectory notes (e.g., "Rising star", "Experienced veteran").
        /// </summary>
        public string CareerTrajectory;

        /// <summary>
        /// Potential/ceiling assessment.
        /// </summary>
        public string PotentialAssessment;
    }

    /// <summary>
    /// Individual skill assessment for staff (text-based).
    /// </summary>
    [Serializable]
    public class StaffSkillAssessment
    {
        public string SkillName;
        public string Grade;        // "Elite", "Very Good", "Solid", etc.
        public string Description;  // Detailed notes about this skill
    }

    /// <summary>
    /// Evaluation tier for internal use (color coding, sorting).
    /// Users see text descriptions, not these values.
    /// </summary>
    public enum EvaluationTier
    {
        Elite,          // >= 85
        VeryGood,       // >= 75
        Solid,          // >= 65
        Average,        // >= 55
        BelowAverage,   // >= 45
        Poor            // < 45
    }

    /// <summary>
    /// Helper methods for evaluation tiers.
    /// </summary>
    public static class EvaluationTierExtensions
    {
        public static EvaluationTier FromRating(int rating)
        {
            return rating switch
            {
                >= 85 => EvaluationTier.Elite,
                >= 75 => EvaluationTier.VeryGood,
                >= 65 => EvaluationTier.Solid,
                >= 55 => EvaluationTier.Average,
                >= 45 => EvaluationTier.BelowAverage,
                _ => EvaluationTier.Poor
            };
        }

        public static string GetLabel(this EvaluationTier tier)
        {
            return tier switch
            {
                EvaluationTier.Elite => "Elite",
                EvaluationTier.VeryGood => "Very Good",
                EvaluationTier.Solid => "Solid",
                EvaluationTier.Average => "Average",
                EvaluationTier.BelowAverage => "Below Average",
                EvaluationTier.Poor => "Poor",
                _ => "Unknown"
            };
        }

        public static UnityEngine.Color GetColor(this EvaluationTier tier)
        {
            return tier switch
            {
                EvaluationTier.Elite => new UnityEngine.Color(0.2f, 0.8f, 0.2f),      // Green
                EvaluationTier.VeryGood => new UnityEngine.Color(0.4f, 0.7f, 0.9f),   // Blue
                EvaluationTier.Solid => new UnityEngine.Color(0.6f, 0.8f, 0.4f),      // Light green
                EvaluationTier.Average => new UnityEngine.Color(0.9f, 0.7f, 0.2f),    // Yellow
                EvaluationTier.BelowAverage => new UnityEngine.Color(0.9f, 0.5f, 0.3f), // Orange
                EvaluationTier.Poor => new UnityEngine.Color(0.9f, 0.3f, 0.3f),       // Red
                _ => UnityEngine.Color.white
            };
        }
    }
}
