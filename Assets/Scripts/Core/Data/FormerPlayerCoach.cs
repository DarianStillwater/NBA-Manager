using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Extended coach data for former NBA players who entered coaching.
    /// Includes their playing career history and derived specializations.
    /// </summary>
    [Serializable]
    public class FormerPlayerCoach
    {
        // ==================== IDENTITY ====================
        public string FormerPlayerCoachId;
        public string CoachId;                    // Reference to the Coach entity
        public string FormerPlayerId;             // Original player ID

        // ==================== PLAYING CAREER ====================
        public PlayerCareerReference PlayingCareer;

        // ==================== DERIVED COACHING ATTRIBUTES ====================
        /// <summary>
        /// Specializations derived from playing career position and style
        /// </summary>
        public List<CoachSpecialization> DerivedSpecializations = new List<CoachSpecialization>();

        /// <summary>
        /// Coaching style derived from personality traits
        /// </summary>
        public CoachingStyle DerivedStyle;

        /// <summary>
        /// Offensive philosophy derived from playing style
        /// </summary>
        public CoachingPhilosophy DerivedOffensivePhilosophy;

        /// <summary>
        /// Defensive philosophy derived from playing style
        /// </summary>
        public CoachingPhilosophy DerivedDefensivePhilosophy;

        // ==================== PROGRESSION ====================
        public FormerPlayerProgressionData Progression;

        // ==================== PERSONALITY ====================
        public List<PlayerPersonalityTrait> PersonalityTraits = new List<PlayerPersonalityTrait>();

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Create a FormerPlayerCoach from retired player data
        /// </summary>
        public static FormerPlayerCoach CreateFromRetiredPlayer(
            PlayerCareerData playerData,
            FormerPlayerProgressionData progression,
            List<PlayerPersonalityTrait> traits = null)
        {
            var formerPlayerCoach = new FormerPlayerCoach
            {
                FormerPlayerCoachId = Guid.NewGuid().ToString(),
                FormerPlayerId = playerData.PlayerId,
                PlayingCareer = PlayerCareerReference.FromPlayerCareerData(playerData),
                Progression = progression,
                PersonalityTraits = traits ?? GenerateDefaultTraits(playerData)
            };

            // Derive specializations from playing position
            formerPlayerCoach.DerivedSpecializations = DeriveSpecializations(playerData, formerPlayerCoach.PlayingCareer);

            // Derive coaching style from personality
            formerPlayerCoach.DerivedStyle = DeriveCoachingStyle(formerPlayerCoach.PersonalityTraits);

            // Derive philosophies from playing style
            (formerPlayerCoach.DerivedOffensivePhilosophy, formerPlayerCoach.DerivedDefensivePhilosophy) =
                DerivePhilosophies(playerData, formerPlayerCoach.PlayingCareer);

            return formerPlayerCoach;
        }

        /// <summary>
        /// Create the actual UnifiedCareerProfile entity from FormerPlayerCoach data
        /// </summary>
        public UnifiedCareerProfile CreateUnifiedProfile(string teamId, UnifiedRole role)
        {
            var profile = new UnifiedCareerProfile
            {
                ProfileId = Guid.NewGuid().ToString(),
                PersonName = PlayingCareer?.FullName ?? "Unknown Coach",
                CurrentAge = CalculateCurrentAge(),
                BirthYear = DateTime.Now.Year - CalculateCurrentAge(),
                TeamId = teamId,
                CurrentRole = role,
                CurrentTrack = UnifiedCareerTrack.Coaching,

                // Core attributes derived from playing career
                OffensiveScheme = CalculateOffensiveScheme(),
                DefensiveScheme = CalculateDefensiveScheme(),
                InGameAdjustments = CalculateInGameAdjustments(),
                PlayDesign = CalculatePlayDesign(),

                GameManagement = CalculateGameManagement(),
                ClockManagement = CalculateClockManagement(),
                RotationManagement = CalculateRotationManagement(),

                PlayerDevelopment = CalculatePlayerDevelopment(),
                GuardDevelopment = CalculateGuardDevelopment(),
                BigManDevelopment = CalculateBigManDevelopment(),
                ShootingCoaching = CalculateShootingCoaching(),
                DefenseCoaching = CalculateDefenseCoaching(),

                Motivation = CalculateMotivation(),
                Communication = CalculateCommunication(),
                ManManagement = CalculateManManagement(),
                MediaHandling = CalculateMediaHandling(),

                ScoutingAbility = CalculateScoutingAbility(),
                TalentEvaluation = CalculateTalentEvaluation(),

                // Career info
                TotalCoachingYears = Progression?.TotalCareerYears ?? 0,
                TotalWins = 0,
                TotalLosses = 0,
                TotalPlayoffAppearances = 0,
                TotalChampionships = 0,
                CoachingReputation = CalculateStartingReputation(),

                // Style
                PrimaryStyle = DerivedStyle,
                OffensivePhilosophy = DerivedOffensivePhilosophy,
                DefensivePhilosophy = DerivedDefensivePhilosophy,

                // Specializations
                Specializations = new List<CoachSpecialization>(DerivedSpecializations),

                // Former player link
                IsFormerPlayer = true,
                FormerPlayerId = FormerPlayerId,
                PlayingCareer = PlayingCareer
            };

            // Store reference
            CoachId = profile.ProfileId;

            return profile;
        }

        // ==================== ATTRIBUTE CALCULATIONS ====================
        // All use Player attributes + variance as specified in plan

        private int CalculateOffensiveScheme()
        {
            int baseValue = PlayingCareer?.FinalBasketballIQ ?? 50;
            int variance = UnityEngine.Random.Range(-10, 11);
            return Mathf.Clamp(baseValue + variance, 30, 95);
        }

        private int CalculateDefensiveScheme()
        {
            int baseValue = PlayingCareer?.FinalBasketballIQ ?? 50;
            if (PlayingCareer?.WasKnownForDefense == true)
                baseValue += 10;
            int variance = UnityEngine.Random.Range(-10, 11);
            return Mathf.Clamp(baseValue + variance, 30, 95);
        }

        private int CalculateInGameAdjustments()
        {
            int baseValue = PlayingCareer?.FinalBasketballIQ ?? 50;
            int variance = UnityEngine.Random.Range(-15, 11);
            return Mathf.Clamp(baseValue + variance, 25, 90);
        }

        private int CalculatePlayDesign()
        {
            int baseValue = PlayingCareer?.FinalBasketballIQ ?? 50;
            if (PlayingCareer?.WasKnownForPlaymaking == true)
                baseValue += 15;
            int variance = UnityEngine.Random.Range(-10, 16);
            return Mathf.Clamp(baseValue + variance, 25, 95);
        }

        private int CalculateGameManagement()
        {
            int baseValue = (PlayingCareer?.FinalBasketballIQ ?? 50 + PlayingCareer?.FinalLeadership ?? 50) / 2;
            int variance = UnityEngine.Random.Range(-10, 16);

            // Experience bonus
            if (Progression?.TotalCareerYears > 5)
                baseValue += 10;

            return Mathf.Clamp(baseValue + variance, 30, 90);
        }

        private int CalculateClockManagement()
        {
            int baseValue = PlayingCareer?.FinalBasketballIQ ?? 50;
            int variance = UnityEngine.Random.Range(-15, 11);
            return Mathf.Clamp(baseValue + variance, 25, 85);
        }

        private int CalculateRotationManagement()
        {
            int baseValue = PlayingCareer?.FinalBasketballIQ ?? 50;
            int variance = UnityEngine.Random.Range(-10, 11);
            return Mathf.Clamp(baseValue + variance, 30, 85);
        }

        private int CalculatePlayerDevelopment()
        {
            int iq = PlayingCareer?.FinalBasketballIQ ?? 50;
            int leadership = PlayingCareer?.FinalLeadership ?? 50;
            int baseValue = (iq + leadership) / 2;
            int variance = UnityEngine.Random.Range(0, 21);

            // Mentor trait bonus
            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Mentor))
                baseValue += 15;

            return Mathf.Clamp(baseValue + variance, 35, 95);
        }

        private int CalculateGuardDevelopment()
        {
            int baseValue = CalculatePlayerDevelopment();
            if (PlayingCareer?.PrimaryPosition == Position.PointGuard ||
                PlayingCareer?.PrimaryPosition == Position.ShootingGuard)
            {
                baseValue += 15;
            }
            return Mathf.Clamp(baseValue, 30, 95);
        }

        private int CalculateBigManDevelopment()
        {
            int baseValue = CalculatePlayerDevelopment();
            if (PlayingCareer?.PrimaryPosition == Position.PowerForward ||
                PlayingCareer?.PrimaryPosition == Position.Center)
            {
                baseValue += 15;
            }
            return Mathf.Clamp(baseValue, 30, 95);
        }

        private int CalculateShootingCoaching()
        {
            int baseValue = 50;
            if (PlayingCareer?.WasKnownForShooting == true)
                baseValue += 25;
            int variance = UnityEngine.Random.Range(-10, 16);
            return Mathf.Clamp(baseValue + variance, 30, 95);
        }

        private int CalculateDefenseCoaching()
        {
            int baseValue = 50;
            if (PlayingCareer?.WasKnownForDefense == true)
                baseValue += 25;
            if (PlayingCareer?.AllDefensiveSelections > 0)
                baseValue += 10;
            int variance = UnityEngine.Random.Range(-10, 11);
            return Mathf.Clamp(baseValue + variance, 30, 95);
        }

        private int CalculateMotivation()
        {
            int leadership = PlayingCareer?.FinalLeadership ?? 50;
            int variance = UnityEngine.Random.Range(-10, 16);

            // Trait bonuses
            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Leader))
                leadership += 15;
            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Competitor))
                leadership += 10;
            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Fiery))
                leadership += 5;

            return Mathf.Clamp(leadership + variance, 35, 95);
        }

        private int CalculateCommunication()
        {
            int baseValue = PlayingCareer?.FinalLeadership ?? 50;
            int variance = UnityEngine.Random.Range(-10, 11);

            if (PersonalityTraits.Contains(PlayerPersonalityTrait.MediaSavvy))
                baseValue += 15;
            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Reserved))
                baseValue -= 10;

            return Mathf.Clamp(baseValue + variance, 30, 90);
        }

        private int CalculateManManagement()
        {
            int leadership = PlayingCareer?.FinalLeadership ?? 50;
            int variance = UnityEngine.Random.Range(-15, 11);

            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Professional))
                leadership += 10;
            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Fiery))
                leadership -= 5; // Can cause friction

            return Mathf.Clamp(leadership + variance, 25, 90);
        }

        private int CalculateMediaHandling()
        {
            int baseValue = 50;
            int variance = UnityEngine.Random.Range(-10, 11);

            if (PersonalityTraits.Contains(PlayerPersonalityTrait.MediaSavvy))
                baseValue += 20;
            if (PlayingCareer?.LegacyTier == LegacyTier.HallOfFame)
                baseValue += 10; // Media experience from stardom

            return Mathf.Clamp(baseValue + variance, 30, 90);
        }

        private int CalculateScoutingAbility()
        {
            int iq = PlayingCareer?.FinalBasketballIQ ?? 50;
            int variance = UnityEngine.Random.Range(-10, 11);

            if (PersonalityTraits.Contains(PlayerPersonalityTrait.FilmJunkie))
                iq += 15;

            return Mathf.Clamp(iq + variance, 30, 85);
        }

        private int CalculateTalentEvaluation()
        {
            int iq = PlayingCareer?.FinalBasketballIQ ?? 50;
            int variance = UnityEngine.Random.Range(-10, 11);

            // Years of NBA experience help evaluation
            if (PlayingCareer?.SeasonsPlayed > 10)
                iq += 10;

            return Mathf.Clamp(iq + variance, 30, 85);
        }

        private int CalculateStartingReputation()
        {
            // Base reputation from playing legacy
            int rep = PlayingCareer?.LegacyTier switch
            {
                LegacyTier.HallOfFame => 70,
                LegacyTier.AllTimeTier => 60,
                LegacyTier.StarPlayer => 50,
                LegacyTier.SolidCareer => 40,
                LegacyTier.RolePlayers => 35,
                LegacyTier.BriefCareer => 30,
                _ => 35
            };

            // Adjust for coaching experience
            if (Progression != null)
            {
                rep += Progression.TotalCareerYears * 2;
            }

            return Mathf.Clamp(rep, 30, 85);
        }

        private int CalculateCurrentAge()
        {
            // Assume average retirement age of 35-38, then add coaching years
            int retirementAge = 36;
            int coachingYears = Progression?.TotalCareerYears ?? 0;
            return retirementAge + coachingYears;
        }

        // ==================== DERIVATION METHODS ====================

        private static List<CoachSpecialization> DeriveSpecializations(
            PlayerCareerData playerData,
            PlayerCareerReference career)
        {
            var specs = new List<CoachSpecialization>();

            // Position-based specializations
            switch (career.PrimaryPosition)
            {
                case Position.PointGuard:
                case Position.ShootingGuard:
                    specs.Add(CoachSpecialization.GuardDevelopment);
                    specs.Add(CoachSpecialization.PlaymakingDevelopment);
                    break;
                case Position.PowerForward:
                case Position.Center:
                    specs.Add(CoachSpecialization.BigManDevelopment);
                    break;
            }

            // Skill-based specializations
            if (career.WasKnownForShooting)
                specs.Add(CoachSpecialization.ShootingDevelopment);

            if (career.WasKnownForDefense || career.AllDefensiveSelections > 0)
            {
                specs.Add(CoachSpecialization.DefensiveDevelopment);
                specs.Add(CoachSpecialization.DefensiveSchemes);
            }

            // Limit to 3 specializations max
            if (specs.Count > 3)
            {
                specs = specs.GetRange(0, 3);
            }

            return specs;
        }

        private static CoachingStyle DeriveCoachingStyle(List<PlayerPersonalityTrait> traits)
        {
            if (traits.Contains(PlayerPersonalityTrait.Competitor))
                return CoachingStyle.Demanding;

            if (traits.Contains(PlayerPersonalityTrait.Mentor))
                return CoachingStyle.Supportive;

            if (traits.Contains(PlayerPersonalityTrait.StrategicMind) ||
                traits.Contains(PlayerPersonalityTrait.FilmJunkie))
                return CoachingStyle.Analytical;

            if (traits.Contains(PlayerPersonalityTrait.Student))
                return CoachingStyle.PlayerDevelopment;

            if (traits.Contains(PlayerPersonalityTrait.Fiery))
                return CoachingStyle.WinNow;

            // Default based on random
            var styles = new[] { CoachingStyle.Supportive, CoachingStyle.Analytical,
                                CoachingStyle.PlayerDevelopment, CoachingStyle.Demanding };
            return styles[UnityEngine.Random.Range(0, styles.Length)];
        }

        private static (CoachingPhilosophy offensive, CoachingPhilosophy defensive)
            DerivePhilosophies(PlayerCareerData playerData, PlayerCareerReference career)
        {
            CoachingPhilosophy offensive;
            CoachingPhilosophy defensive;

            // Offensive philosophy based on playing style
            if (career.WasKnownForShooting)
            {
                offensive = CoachingPhilosophy.ThreePointHeavy;
            }
            else if (career.WasKnownForPlaymaking)
            {
                offensive = CoachingPhilosophy.BallMovement;
            }
            else if (career.PrimaryPosition == Position.Center ||
                     career.PrimaryPosition == Position.PowerForward)
            {
                offensive = CoachingPhilosophy.InsideOut;
            }
            else
            {
                offensive = CoachingPhilosophy.FastPace;
            }

            // Defensive philosophy
            if (career.WasKnownForDefense)
            {
                defensive = career.AllDefensiveSelections > 3
                    ? CoachingPhilosophy.Aggressive
                    : CoachingPhilosophy.ManToMan;
            }
            else
            {
                defensive = CoachingPhilosophy.Conservative;
            }

            return (offensive, defensive);
        }

        private static List<PlayerPersonalityTrait> GenerateDefaultTraits(PlayerCareerData playerData)
        {
            var traits = new List<PlayerPersonalityTrait>();
            var rng = new System.Random(playerData.PlayerId.GetHashCode());

            // Leadership suggests Leader trait
            if (playerData.AllStarSelections > 3)
            {
                traits.Add(PlayerPersonalityTrait.Leader);
            }

            // Add 1-3 random traits
            var allTraits = (PlayerPersonalityTrait[])Enum.GetValues(typeof(PlayerPersonalityTrait));
            int traitCount = rng.Next(1, 4);
            for (int i = 0; i < traitCount; i++)
            {
                var trait = allTraits[rng.Next(allTraits.Length)];
                if (!traits.Contains(trait))
                {
                    traits.Add(trait);
                }
            }

            return traits;
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
                return "Coach";

            var parts = PlayingCareer.FullName.Split(' ');
            return parts.Length > 1 ? parts[^1] : parts[0];
        }

        /// <summary>
        /// Get a display description of this coach's playing career
        /// </summary>
        public string GetPlayingCareerSummary()
        {
            if (PlayingCareer == null)
                return "Unknown playing career";

            var summary = $"{PlayingCareer.SeasonsPlayed}-year NBA veteran";

            if (PlayingCareer.Championships > 0)
                summary += $", {PlayingCareer.Championships}x NBA Champion";

            if (PlayingCareer.AllStarSelections > 0)
                summary += $", {PlayingCareer.AllStarSelections}x All-Star";

            if (PlayingCareer.IsHallOfFamer)
                summary += ", Hall of Famer";

            return summary;
        }
    }
}
