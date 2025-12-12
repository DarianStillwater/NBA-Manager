using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Text-based scouting report generated from hidden player attributes.
    /// Contains no numerical ratings - only descriptive assessments.
    /// </summary>
    [Serializable]
    public class ScoutingReport
    {
        // ==================== IDENTITY ====================
        public string ReportId;
        public string PlayerId;
        public string PlayerName;
        public ScoutingTargetType TargetType;

        // ==================== SCOUT INFO ====================
        public string ScoutId;
        public string ScoutName;
        public int ScoutSkillAtGeneration;  // For tracking report quality

        // ==================== TIMING ====================
        public DateTime GeneratedDate;
        public int TimesScoutedTotal;       // How many times this player has been scouted
        public int GamesObservedThisReport; // Games watched for this specific report

        // ==================== ACCURACY ====================
        /// <summary>
        /// Internal accuracy rating (0-1). NOT shown to player.
        /// Higher = more accurate assessments.
        /// </summary>
        [HideInInspector]
        public float InternalAccuracy;

        // ==================== PLAYER INFO (Basic - Always Visible) ====================
        public string Position;
        public string HeightDisplay;        // "6'6\""
        public string WeightDisplay;        // "215 lbs"
        public int Age;
        public int YearsProOrCollege;       // Years pro for NBA, years in college for prospects
        public string CurrentTeamOrSchool;

        // ==================== PHYSICAL PROFILE ====================
        public PhysicalAssessment Physical;

        // ==================== SKILL ASSESSMENTS (Text-Based) ====================
        public SkillAssessment OffensiveSkills;
        public SkillAssessment DefensiveSkills;
        public SkillAssessment PlaymakingSkills;
        public SkillAssessment AthleticProfile;

        // ==================== MENTAL/INTANGIBLES ====================
        public MentalAssessment MentalProfile;
        public PersonalityAssessment Personality;

        // ==================== POTENTIAL (For Prospects) ====================
        public PotentialAssessment Potential;

        // ==================== OVERALL SUMMARY ====================
        public string OverallSummary;           // 2-3 sentence overall assessment
        public string ProjectedRole;            // "Starter", "Rotation Player", "End of Bench"
        public string ComparisonPlayer;         // "Reminds me of a young..."
        public string BiggestStrength;
        public string BiggestConcern;

        // ==================== STRENGTHS & WEAKNESSES ====================
        public List<string> Strengths = new List<string>();
        public List<string> Weaknesses = new List<string>();
        public List<string> AreasToWatch = new List<string>();  // Neither strength nor weakness

        // ==================== RECOMMENDATIONS ====================
        public DraftRecommendation DraftRec;    // For prospects only
        public TradeRecommendation TradeRec;    // For NBA players only

        // ==================== REPORT FRESHNESS ====================
        public const int OUTDATED_THRESHOLD_DAYS = 60;  // 2 months

        public bool IsOutdated => (DateTime.Now - GeneratedDate).TotalDays > OUTDATED_THRESHOLD_DAYS;

        public string FreshnessStatus
        {
            get
            {
                int daysOld = (int)(DateTime.Now - GeneratedDate).TotalDays;
                if (daysOld <= 14) return "RECENT";
                if (daysOld <= 30) return "CURRENT";
                if (daysOld <= OUTDATED_THRESHOLD_DAYS) return "AGING";
                return "OUTDATED";
            }
        }

        public string DateDisplay => GeneratedDate.ToString("MMM dd, yyyy");

        // ==================== CONFIDENCE LEVEL ====================
        /// <summary>
        /// How confident the scout is in this report.
        /// Based on times scouted and scout skill.
        /// </summary>
        public ReportConfidence Confidence
        {
            get
            {
                if (TimesScoutedTotal >= 5 && InternalAccuracy >= 0.8f) return ReportConfidence.VeryHigh;
                if (TimesScoutedTotal >= 3 && InternalAccuracy >= 0.65f) return ReportConfidence.High;
                if (TimesScoutedTotal >= 2 && InternalAccuracy >= 0.5f) return ReportConfidence.Moderate;
                if (TimesScoutedTotal >= 1) return ReportConfidence.Low;
                return ReportConfidence.VeryLow;
            }
        }

        public string ConfidenceDisplay => Confidence switch
        {
            ReportConfidence.VeryHigh => "Very High Confidence",
            ReportConfidence.High => "High Confidence",
            ReportConfidence.Moderate => "Moderate Confidence",
            ReportConfidence.Low => "Low Confidence - More Scouting Recommended",
            ReportConfidence.VeryLow => "Very Low Confidence - Preliminary Assessment Only",
            _ => "Unknown"
        };
    }

    // ==================== ASSESSMENT CLASSES ====================

    [Serializable]
    public class PhysicalAssessment
    {
        public string Wingspan;             // "Plus wingspan for position" / "Average" / "Below average"
        public string BodyType;             // "Muscular build", "Slight frame", "Long and lean"
        public string AthleticismGrade;     // "Elite athlete", "Above average", "Average", etc.
        public string Notes;                // Free-form physical observations
    }

    [Serializable]
    public class SkillAssessment
    {
        public string Category;             // "Offensive", "Defensive", etc.
        public List<SkillGrade> Skills = new List<SkillGrade>();
        public string Summary;              // Overall category summary
    }

    [Serializable]
    public class SkillGrade
    {
        public string SkillName;            // "Three-Point Shooting", "Ball Handling"
        public string Grade;                // Text grade: "Elite", "Very Good", "Average", etc.
        public string Notes;                // Specific observations
    }

    [Serializable]
    public class MentalAssessment
    {
        public string BasketballIQ;         // "High IQ", "Average", "Needs coaching"
        public string Coachability;         // "Very coachable", "Stubborn", etc.
        public string WorkEthic;            // "Gym rat", "Good worker", "Questions about motor"
        public string Composure;            // "Ice in veins", "Gets rattled", etc.
        public string Consistency;          // "Steady performer", "Streaky", "Inconsistent"
        public string Summary;
    }

    [Serializable]
    public class PersonalityAssessment
    {
        public string LeadershipProfile;    // "Vocal leader", "Leads by example", "Follower"
        public string CharacterConcerns;    // Any red flags or "No concerns"
        public string TeammateDynamics;     // "Great teammate", "Can be difficult", etc.
        public string MediaPresence;        // "Handles media well", "Avoids spotlight"
        public string MotivationAssessment; // "Highly motivated", "Money-driven", "Ring chaser"
        public List<string> PersonalityTraitsIdentified = new List<string>();
    }

    [Serializable]
    public class PotentialAssessment
    {
        public string CeilingDescription;   // "All-Star potential", "Solid starter", etc.
        public string FloorDescription;     // "At worst, a rotation player"
        public string DevelopmentOutlook;   // "High upside with development"
        public string TimelineEstimate;     // "NBA-ready now", "2-3 years away", "Project"
        public string KeyDevelopmentAreas;  // What they need to work on
        public string RiskLevel;            // "Low risk", "Moderate risk", "High risk/high reward"
    }

    [Serializable]
    public class DraftRecommendation
    {
        public string ProjectedRange;       // "Lottery pick", "Mid first-round", "Second round"
        public string ValueAssessment;      // "Worth reaching for", "Good value at position"
        public string FitWithTeam;          // How they'd fit with scouting team
        public string FinalVerdict;         // "Strongly recommend", "Proceed with caution", etc.
        public List<string> ProTeamFits = new List<string>();    // Teams where they'd fit well
        public List<string> PoorFitTeams = new List<string>();   // Teams to avoid
    }

    [Serializable]
    public class TradeRecommendation
    {
        public string CurrentValue;         // "High trade value", "Negative asset"
        public string ContractAssessment;   // "Team-friendly deal", "Overpaid"
        public string AcquisitionPriority;  // "Top target", "Nice to have", "Avoid"
        public string TradeNotes;           // General trade-related observations
    }

    // ==================== ENUMS ====================

    public enum ReportConfidence
    {
        VeryLow,
        Low,
        Moderate,
        High,
        VeryHigh
    }

    /// <summary>
    /// Text grade descriptors for converting numerical attributes.
    /// These are the ONLY ways players see skill levels.
    /// </summary>
    public static class SkillGradeDescriptors
    {
        public static string GetGrade(int attributeValue)
        {
            return attributeValue switch
            {
                >= 95 => "Generational",
                >= 90 => "Elite (All-NBA caliber)",
                >= 85 => "Excellent (All-Star level)",
                >= 80 => "Very Good (High-end starter)",
                >= 75 => "Above Average (Quality starter)",
                >= 70 => "Solid (Starter quality)",
                >= 65 => "Adequate (Low-end starter)",
                >= 60 => "Average (Rotation player)",
                >= 55 => "Below Average (Situational)",
                >= 50 => "Limited (Bench depth)",
                >= 45 => "Poor (End of bench)",
                >= 40 => "Weak (Below NBA level)",
                >= 30 => "Major Weakness",
                _ => "Liability"
            };
        }

        public static string GetGradeShort(int attributeValue)
        {
            return attributeValue switch
            {
                >= 95 => "A++",
                >= 90 => "A+",
                >= 85 => "A",
                >= 80 => "A-",
                >= 75 => "B+",
                >= 70 => "B",
                >= 65 => "B-",
                >= 60 => "C+",
                >= 55 => "C",
                >= 50 => "C-",
                >= 45 => "D+",
                >= 40 => "D",
                >= 30 => "D-",
                _ => "F"
            };
        }

        /// <summary>
        /// Gets a grade with potential inaccuracy based on scout skill and times scouted.
        /// </summary>
        public static string GetGradeWithAccuracy(int trueValue, float accuracy, System.Random rng)
        {
            // Calculate potential error range (lower accuracy = more variance)
            float maxError = (1f - accuracy) * 25f;  // Up to 25 points off with 0% accuracy
            int error = (int)((rng.NextDouble() * 2 - 1) * maxError);
            int perceivedValue = Mathf.Clamp(trueValue + error, 0, 100);
            return GetGrade(perceivedValue);
        }

        public static List<string> GetPotentialDescriptors()
        {
            return new List<string>
            {
                "Generational prospect",
                "Franchise cornerstone potential",
                "All-Star ceiling",
                "High-end starter upside",
                "Solid starter potential",
                "Quality rotation player",
                "Role player ceiling",
                "Limited upside",
                "Minimal NBA potential"
            };
        }
    }
}
