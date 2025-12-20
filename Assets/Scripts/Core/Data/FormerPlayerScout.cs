using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Extended scout data for former NBA players who entered scouting.
    /// Includes their playing career history and derived abilities.
    /// </summary>
    [Serializable]
    public class FormerPlayerScout
    {
        // ==================== IDENTITY ====================
        public string FormerPlayerScoutId;
        public string ScoutId;                    // Reference to the Scout entity
        public string FormerPlayerId;             // Original player ID

        // ==================== PLAYING CAREER ====================
        public PlayerCareerReference PlayingCareer;

        // ==================== DERIVED SCOUTING ATTRIBUTES ====================
        /// <summary>
        /// Primary specialization derived from playing career
        /// </summary>
        public ScoutSpecialization DerivedPrimarySpec;

        /// <summary>
        /// Secondary specialization
        /// </summary>
        public ScoutSpecialization DerivedSecondarySpec;

        // ==================== PROGRESSION ====================
        /// <summary>
        /// Years in scouting career
        /// </summary>
        public int ScoutingCareerYears;

        /// <summary>
        /// Current team working for
        /// </summary>
        public string CurrentTeamId;

        /// <summary>
        /// Teams worked for as a scout
        /// </summary>
        public List<string> TeamsWorkedFor = new List<string>();

        // ==================== USER RELATIONSHIP ====================
        public bool WasCoachedByUser;
        public int SeasonsUnderUserCoaching;

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Create a FormerPlayerScout from retired player data
        /// </summary>
        public static FormerPlayerScout CreateFromRetiredPlayer(PlayerCareerData playerData)
        {
            var formerPlayerScout = new FormerPlayerScout
            {
                FormerPlayerScoutId = Guid.NewGuid().ToString(),
                FormerPlayerId = playerData.PlayerId,
                PlayingCareer = PlayerCareerReference.FromPlayerCareerData(playerData),
                ScoutingCareerYears = 0
            };

            // Derive specializations from playing career
            (formerPlayerScout.DerivedPrimarySpec, formerPlayerScout.DerivedSecondarySpec) =
                DeriveSpecializations(playerData, formerPlayerScout.PlayingCareer);

            return formerPlayerScout;
        }

        /// <summary>
        /// Create the actual UnifiedCareerProfile entity from FormerPlayerScout data
        /// </summary>
        public UnifiedCareerProfile CreateUnifiedProfile(string teamId)
        {
            var profile = new UnifiedCareerProfile
            {
                ProfileId = Guid.NewGuid().ToString(),
                PersonName = PlayingCareer?.FullName ?? "Unknown Scout",
                CurrentAge = CalculateCurrentAge(),
                BirthYear = DateTime.Now.Year - CalculateCurrentAge(),
                TeamId = teamId,
                CurrentRole = UnifiedRole.Scout,
                CurrentTrack = UnifiedCareerTrack.Scouting,

                // Core attributes derived from playing career
                EvaluationAccuracy = CalculateEvaluationAccuracy(),
                ProspectEvaluation = CalculateProspectEvaluation(),
                ProEvaluation = CalculateProEvaluation(),
                PotentialAssessment = CalculatePotentialAssessment(),

                CollegeConnections = CalculateCollegeConnections(),
                InternationalConnections = CalculateInternationalConnections(),
                AgentRelationships = CalculateAgentRelationships(),

                WorkRate = CalculateWorkRate(),
                AttentionToDetail = CalculateAttentionToDetail(),
                CharacterJudgment = CalculateCharacterJudgment(),

                // Specializations
                ScoutingSpecializations = new List<ScoutSpecialization> { DerivedPrimarySpec, DerivedSecondarySpec },

                // Career info
                TotalFrontOfficeYears = ScoutingCareerYears,
                
                // Former player link
                IsFormerPlayer = true,
                FormerPlayerId = FormerPlayerId,
                PlayingCareer = PlayingCareer
            };

            // Store reference
            ScoutId = profile.ProfileId;
            CurrentTeamId = teamId;

            // Set salary (MarketValue is computed on UnifiedCareerProfile)
            profile.AnnualSalary = (int)(profile.MarketValue * UnityEngine.Random.Range(0.9f, 1.1f));
            profile.ContractYearsRemaining = UnityEngine.Random.Range(2, 4);

            return profile;
        }

        // ==================== ATTRIBUTE CALCULATIONS ====================

        private int CalculateEvaluationAccuracy()
        {
            // Former players have good baseline from experience
            int baseValue = 55;
            int iqBonus = (PlayingCareer?.FinalBasketballIQ ?? 50) / 5;
            int expBonus = Math.Min(PlayingCareer?.SeasonsPlayed ?? 0, 15);
            int variance = UnityEngine.Random.Range(-10, 11);

            return Mathf.Clamp(baseValue + iqBonus + expBonus + variance, 40, 90);
        }

        private int CalculateProspectEvaluation()
        {
            int baseValue = 50;
            int iqBonus = (PlayingCareer?.FinalBasketballIQ ?? 50) / 5;
            int variance = UnityEngine.Random.Range(-10, 16);

            // Guards tend to be better at evaluating prospects (perimeter game translates)
            if (PlayingCareer?.PrimaryPosition == Position.PointGuard ||
                PlayingCareer?.PrimaryPosition == Position.ShootingGuard)
            {
                baseValue += 10;
            }

            return Mathf.Clamp(baseValue + iqBonus + variance, 35, 90);
        }

        private int CalculateProEvaluation()
        {
            // Former NBA players excel at evaluating current pros
            int baseValue = 65;
            int expBonus = Math.Min(PlayingCareer?.SeasonsPlayed ?? 0, 12);
            int variance = UnityEngine.Random.Range(-5, 11);

            // All-Stars played against everyone
            if (PlayingCareer?.AllStarSelections > 0)
                baseValue += 10;

            return Mathf.Clamp(baseValue + expBonus + variance, 50, 95);
        }

        private int CalculatePotentialAssessment()
        {
            int iqValue = PlayingCareer?.FinalBasketballIQ ?? 50;
            int variance = UnityEngine.Random.Range(-15, 11);

            // Leadership correlates with understanding player development
            int leadershipBonus = (PlayingCareer?.FinalLeadership ?? 50) / 10;

            return Mathf.Clamp(iqValue + leadershipBonus + variance, 35, 85);
        }

        private int CalculateCollegeConnections()
        {
            // Starts low but builds over time
            int baseValue = 30;
            int scoutingYearsBonus = ScoutingCareerYears * 5;
            int variance = UnityEngine.Random.Range(-5, 11);

            // College stars have existing connections
            if (PlayingCareer?.SeasonsPlayed < 15) // Likely remembered in college circles
                baseValue += 10;

            return Mathf.Clamp(baseValue + scoutingYearsBonus + variance, 20, 85);
        }

        private int CalculateInternationalConnections()
        {
            // Base is low for US-focused players
            int baseValue = 25;
            int scoutingYearsBonus = ScoutingCareerYears * 3;
            int variance = UnityEngine.Random.Range(-5, 11);

            return Mathf.Clamp(baseValue + scoutingYearsBonus + variance, 15, 75);
        }

        private int CalculateAgentRelationships()
        {
            // Former players know agents from their career
            int baseValue = 50;
            int variance = UnityEngine.Random.Range(-10, 16);

            // Stars dealt with top agents
            if (PlayingCareer?.LegacyTier == LegacyTier.HallOfFame ||
                PlayingCareer?.LegacyTier == LegacyTier.AllTimeTier)
            {
                baseValue += 20;
            }
            else if (PlayingCareer?.LegacyTier == LegacyTier.StarPlayer)
            {
                baseValue += 10;
            }

            return Mathf.Clamp(baseValue + variance, 35, 90);
        }

        private int CalculateWorkRate()
        {
            int workEthic = PlayingCareer?.FinalWorkEthic ?? 50;
            int variance = UnityEngine.Random.Range(-10, 11);

            return Mathf.Clamp(workEthic + variance, 40, 90);
        }

        private int CalculateAttentionToDetail()
        {
            int iqValue = PlayingCareer?.FinalBasketballIQ ?? 50;
            int variance = UnityEngine.Random.Range(-10, 11);

            return Mathf.Clamp(iqValue + variance, 35, 90);
        }

        private int CalculateCharacterJudgment()
        {
            // Experience in locker rooms helps judge character
            int baseValue = 55;
            int expBonus = Math.Min(PlayingCareer?.SeasonsPlayed ?? 0, 10);
            int leadershipBonus = (PlayingCareer?.FinalLeadership ?? 50) / 10;
            int variance = UnityEngine.Random.Range(-10, 11);

            return Mathf.Clamp(baseValue + expBonus + leadershipBonus + variance, 40, 90);
        }

        private int CalculateCurrentAge()
        {
            int retirementAge = 36;
            return retirementAge + ScoutingCareerYears;
        }

        // ==================== DERIVATION METHODS ====================

        private static (ScoutSpecialization primary, ScoutSpecialization secondary)
            DeriveSpecializations(PlayerCareerData playerData, PlayerCareerReference career)
        {
            ScoutSpecialization primary;
            ScoutSpecialization secondary;

            // Primary based on experience level
            if (career.SeasonsPlayed >= 10)
            {
                // Long career = good at Pro evaluation
                primary = ScoutSpecialization.Pro;
            }
            else
            {
                // Shorter career, may focus on prospects
                primary = ScoutSpecialization.College;
            }

            // Secondary based on position and skills
            if (career.WasKnownForDefense)
            {
                secondary = ScoutSpecialization.Advance; // Good at opponent prep
            }
            else if (career.AllStarSelections > 0)
            {
                secondary = ScoutSpecialization.Pro; // Knows star quality
            }
            else
            {
                secondary = ScoutSpecialization.Regional;
            }

            // Ensure no duplicates
            if (secondary == primary)
            {
                secondary = primary == ScoutSpecialization.Pro
                    ? ScoutSpecialization.College
                    : ScoutSpecialization.Pro;
            }

            return (primary, secondary);
        }

        // ==================== HELPER METHODS ====================

        private string GetFirstName()
        {
            if (string.IsNullOrEmpty(PlayingCareer?.FullName))
                return "Unknown";

            var parts = PlayingCareer.FullName.Split(' ');
            return parts.Length > 0 ? parts[0] : "Unknown";
        }

        private string GetLastName()
        {
            if (string.IsNullOrEmpty(PlayingCareer?.FullName))
                return "Scout";

            var parts = PlayingCareer.FullName.Split(' ');
            return parts.Length > 1 ? parts[^1] : parts[0];
        }

        /// <summary>
        /// Get a display description of this scout's playing career
        /// </summary>
        public string GetPlayingCareerSummary()
        {
            if (PlayingCareer == null)
                return "Unknown playing career";

            var summary = $"Former {PlayingCareer.SeasonsPlayed}-year NBA veteran";

            if (PlayingCareer.Championships > 0)
                summary += $", {PlayingCareer.Championships}x Champion";

            if (PlayingCareer.AllStarSelections > 0)
                summary += $", {PlayingCareer.AllStarSelections}x All-Star";

            return summary;
        }

        /// <summary>
        /// Update scouting career years (call each season)
        /// </summary>
        public void AdvanceYear()
        {
            ScoutingCareerYears++;
        }
    }
}
