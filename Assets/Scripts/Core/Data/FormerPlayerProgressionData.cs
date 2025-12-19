using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core; // For LegacyTier

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Career progression stages for former players entering coaching
    /// </summary>
    [Serializable]
    public enum CoachingProgressionStage
    {
        AssistantCoach,         // Entry level: 3-5 years
        PositionCoach,          // Guard/Big/Development coach: 2-3 years
        Coordinator,            // Offensive/Defensive coordinator: 2-3 years
        HeadCoach               // Final stage
    }

    /// <summary>
    /// Career path types for retired players
    /// </summary>
    [Serializable]
    public enum PostPlayerCareerPath
    {
        Coaching,               // Entered coaching pipeline
        Scouting,              // Became a scout
        MediaAnalyst,          // TV/Media career
        FrontOffice,           // Executive/front office role
        PrivateLife            // Left basketball
    }

    /// <summary>
    /// Summary of a player's NBA playing career for retention in coach/scout records
    /// </summary>
    [Serializable]
    public class PlayerCareerReference
    {
        public string PlayerId;
        public string FullName;

        [Header("Career Stats")]
        public int SeasonsPlayed;
        public int GamesPlayed;
        public int TotalPoints;
        public int TotalRebounds;
        public int TotalAssists;
        public float CareerPPG;
        public float CareerRPG;
        public float CareerAPG;

        [Header("Accolades")]
        public int Championships;
        public int AllStarSelections;
        public int AllNbaSelections;
        public int AllDefensiveSelections;
        public int MVPs;
        public int FinalsMVPs;
        public bool IsHallOfFamer;

        [Header("Position & Style")]
        public Position PrimaryPosition;
        public Position SecondaryPosition;
        public bool WasKnownForDefense;
        public bool WasKnownForShooting;
        public bool WasKnownForPlaymaking;
        public bool WasKnownForLeadership;

        [Header("Teams")]
        public List<string> TeamsPlayedFor = new List<string>();
        public string PrimaryTeam;
        public int YearsWithPrimaryTeam;

        [Header("Attributes at Retirement")]
        public int FinalBasketballIQ;
        public int FinalLeadership;
        public int FinalWorkEthic;

        [Header("Legacy")]
        public LegacyTier LegacyTier;
        public int RetirementYear;

        /// <summary>
        /// Create career reference from player career data
        /// </summary>
        public static PlayerCareerReference FromPlayerCareerData(PlayerCareerData data)
        {
            return new PlayerCareerReference
            {
                PlayerId = data.PlayerId,
                FullName = data.FullName,
                SeasonsPlayed = data.SeasonsPlayed,
                GamesPlayed = data.GamesPlayed,
                TotalPoints = data.CareerPoints,
                TotalRebounds = data.CareerRebounds,
                TotalAssists = data.CareerAssists,
                CareerPPG = data.GamesPlayed > 0 ? (float)data.CareerPoints / data.GamesPlayed : 0,
                CareerRPG = data.GamesPlayed > 0 ? (float)data.CareerRebounds / data.GamesPlayed : 0,
                CareerAPG = data.GamesPlayed > 0 ? (float)data.CareerAssists / data.GamesPlayed : 0,
                Championships = data.Championships,
                AllStarSelections = data.AllStarSelections,
                AllNbaSelections = data.AllNbaSelections,
                AllDefensiveSelections = data.AllDefensiveSelections,
                MVPs = data.Mvps,
                FinalsMVPs = data.FinalsMvps,
                TeamsPlayedFor = new List<string>(data.TeamsPlayedFor),
                PrimaryTeam = data.PrimaryTeam,
                YearsWithPrimaryTeam = data.YearsWithPrimaryTeam,
                RetirementYear = DateTime.Now.Year
            };
        }
    }

    /// <summary>
    /// Tracks a former player's progression through the coaching/scouting ranks
    /// </summary>
    [Serializable]
    public class FormerPlayerProgressionData
    {
        public string ProgressionId;
        public string FormerPlayerId;
        public string FormerPlayerName;
        public string UnifiedProfileId;  // Link to UnifiedCareerProfile for cross-track transitions

        [Header("Career Path")]
        public PostPlayerCareerPath CareerPath;
        public CoachingProgressionStage CurrentStage;

        [Header("Progression Timeline")]
        public int YearsAtCurrentStage;
        public int TotalCareerYears;         // Years since retirement
        public int YearEnteredPipeline;

        [Header("Current Position")]
        public string CurrentTeamId;
        public string CurrentTeamName;
        public string CurrentCoachId;        // If they became a coach
        public string CurrentScoutId;        // If they became a scout

        [Header("History")]
        public List<string> TeamsWorkedFor = new List<string>();
        public List<ProgressionMilestone> Milestones = new List<ProgressionMilestone>();

        [Header("User Relationship")]
        public bool WasCoachedByUser;
        public int SeasonsUnderUserCoaching;
        public string UserTeamIdWhenCoached;

        [Header("Performance")]
        public float ProgressionScore;        // How well they're doing (affects promotion speed)
        public int PromotionChanceBonus;      // Accumulated bonus from good performance
        [Range(0, 100)]
        public int Reputation;                // Overall reputation (for unified career system)
        public int YearEnteredCoaching;       // Year they started coaching

        [Header("Playing Career")]
        public PlayerCareerReference PlayingCareer;

        /// <summary>
        /// Calculate promotion eligibility based on time served and performance
        /// </summary>
        public bool IsEligibleForPromotion()
        {
            if (CareerPath != PostPlayerCareerPath.Coaching)
                return false;

            return CurrentStage switch
            {
                CoachingProgressionStage.AssistantCoach => YearsAtCurrentStage >= 3,
                CoachingProgressionStage.PositionCoach => YearsAtCurrentStage >= 2,
                CoachingProgressionStage.Coordinator => YearsAtCurrentStage >= 2,
                CoachingProgressionStage.HeadCoach => false, // Already at top
                _ => false
            };
        }

        /// <summary>
        /// Get minimum years required at current stage
        /// </summary>
        public int GetMinYearsAtStage()
        {
            return CurrentStage switch
            {
                CoachingProgressionStage.AssistantCoach => 3,
                CoachingProgressionStage.PositionCoach => 2,
                CoachingProgressionStage.Coordinator => 2,
                CoachingProgressionStage.HeadCoach => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Get maximum years typically spent at current stage
        /// </summary>
        public int GetMaxYearsAtStage()
        {
            return CurrentStage switch
            {
                CoachingProgressionStage.AssistantCoach => 5,
                CoachingProgressionStage.PositionCoach => 3,
                CoachingProgressionStage.Coordinator => 3,
                CoachingProgressionStage.HeadCoach => 99,
                _ => 99
            };
        }

        /// <summary>
        /// Calculate base promotion chance (before modifiers)
        /// </summary>
        public float GetBasePromotionChance()
        {
            if (!IsEligibleForPromotion())
                return 0f;

            float baseChance = 0.30f;

            // Time bonus: +5% per year over minimum
            int yearsOverMin = YearsAtCurrentStage - GetMinYearsAtStage();
            baseChance += yearsOverMin * 0.05f;

            // Legacy bonus
            if (PlayingCareer != null)
            {
                baseChance += PlayingCareer.LegacyTier switch
                {
                    LegacyTier.HallOfFame => 0.15f,
                    LegacyTier.AllTimeTier => 0.10f,
                    LegacyTier.StarPlayer => 0.05f,
                    _ => 0f
                };
            }

            // Performance bonus
            baseChance += PromotionChanceBonus / 100f;

            return Mathf.Clamp01(baseChance);
        }
    }

    /// <summary>
    /// Records a milestone in a former player's coaching career
    /// </summary>
    [Serializable]
    public class ProgressionMilestone
    {
        public int Year;
        public string TeamId;
        public string TeamName;
        public CoachingProgressionStage Stage;
        public string Description;

        public static ProgressionMilestone CreatePromotion(int year, string teamId, string teamName,
            CoachingProgressionStage newStage)
        {
            return new ProgressionMilestone
            {
                Year = year,
                TeamId = teamId,
                TeamName = teamName,
                Stage = newStage,
                Description = newStage switch
                {
                    CoachingProgressionStage.PositionCoach => "Promoted to Position Coach",
                    CoachingProgressionStage.Coordinator => "Promoted to Coordinator",
                    CoachingProgressionStage.HeadCoach => "Named Head Coach",
                    _ => "Joined coaching staff"
                }
            };
        }
    }

    /// <summary>
    /// Personality traits that influence career path and coaching style
    /// </summary>
    [Serializable]
    public enum PlayerPersonalityTrait
    {
        Leader,             // Natural leader, good at motivation
        Mentor,             // Enjoys teaching younger players
        Student,            // Always learning, high basketball IQ
        Competitor,         // Fierce competitor, demands excellence
        Professional,       // Business-like approach
        Fiery,              // Emotional, passionate
        Reserved,           // Quiet, leads by example
        MediaSavvy,         // Good with press and public
        FilmJunkie,         // Watches tons of film
        StrategicMind       // Understands X's and O's deeply
    }

    // Note: LegacyTier enum is defined in RetirementManager.cs (NBAHeadCoach.Core namespace)
}
