using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents a team's training facility and its impact on player development.
    /// Facility quality affects development rates and injury recovery.
    /// </summary>
    [Serializable]
    public class TrainingFacility
    {
        // ==================== IDENTITY ====================
        public string TeamId;
        public string FacilityName;

        // ==================== OVERALL RATING ====================
        [Range(1, 5)] public int OverallRating;  // 1-5 stars

        // ==================== SPECIFIC FACILITIES ====================
        [Header("Training Areas")]
        [Range(1, 5)] public int WeightRoom;           // Strength training
        [Range(1, 5)] public int CourtFacility;        // Practice courts
        [Range(1, 5)] public int ShootingLab;          // Shooting machines, analysis
        [Range(1, 5)] public int FilmRoom;             // Video analysis
        [Range(1, 5)] public int RecoveryCenter;       // Injury rehab, recovery
        [Range(1, 5)] public int SportsScience;        // Analytics, biometrics
        [Range(1, 5)] public int NutritionCenter;      // Diet, meal prep

        // ==================== STAFF ====================
        public List<Trainer> Trainers = new List<Trainer>();

        // ==================== COSTS ====================
        public int AnnualMaintenanceCost;
        public int UpgradeCost;                        // Cost to upgrade one star level

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Overall development bonus from facility (0-0.5).
        /// 5-star facility provides up to 50% development bonus.
        /// </summary>
        public float DevelopmentBonus
        {
            get
            {
                float avgRating = (WeightRoom + CourtFacility + ShootingLab + FilmRoom +
                                  RecoveryCenter + SportsScience + NutritionCenter) / 7f;
                return (avgRating - 1) / 4f * 0.5f;  // Scale 1-5 to 0-0.5
            }
        }

        /// <summary>
        /// Shooting development bonus specifically.
        /// </summary>
        public float ShootingDevelopmentBonus
        {
            get
            {
                float rating = (ShootingLab * 2 + CourtFacility + SportsScience) / 4f;
                return (rating - 1) / 4f * 0.4f;
            }
        }

        /// <summary>
        /// Physical development bonus.
        /// </summary>
        public float PhysicalDevelopmentBonus
        {
            get
            {
                float rating = (WeightRoom * 2 + NutritionCenter + RecoveryCenter) / 4f;
                return (rating - 1) / 4f * 0.4f;
            }
        }

        /// <summary>
        /// Injury recovery speed multiplier.
        /// Better facilities = faster recovery.
        /// </summary>
        public float InjuryRecoveryMultiplier
        {
            get
            {
                float rating = (RecoveryCenter * 2 + SportsScience + NutritionCenter) / 4f;
                return 0.8f + (rating / 5f * 0.4f);  // 0.8 to 1.2
            }
        }

        /// <summary>
        /// Injury prevention bonus (reduces injury chance).
        /// </summary>
        public float InjuryPreventionBonus
        {
            get
            {
                float rating = (RecoveryCenter + SportsScience + NutritionCenter + WeightRoom) / 4f;
                return (rating - 1) / 4f * 0.3f;  // Up to 30% injury reduction
            }
        }

        /// <summary>
        /// Film study quality affects game preparation and IQ development.
        /// </summary>
        public float FilmStudyBonus
        {
            get
            {
                float rating = (FilmRoom * 2 + SportsScience) / 3f;
                return (rating - 1) / 4f * 0.3f;
            }
        }

        // ==================== TRAINER BONUSES ====================

        /// <summary>
        /// Gets total trainer bonus for a specific development area.
        /// </summary>
        public float GetTrainerBonus(AttributeCategory category)
        {
            float bonus = 0f;
            foreach (var trainer in Trainers)
            {
                if (trainer.Specialization == category)
                {
                    bonus += trainer.EffectivenessBonus;
                }
            }
            return Mathf.Min(bonus, 0.5f);  // Cap at 50%
        }

        /// <summary>
        /// Gets combined facility + trainer bonus for development.
        /// </summary>
        public float GetTotalDevelopmentBonus(AttributeCategory category)
        {
            float facilityBonus = category switch
            {
                AttributeCategory.Shooting => ShootingDevelopmentBonus,
                AttributeCategory.Physical => PhysicalDevelopmentBonus,
                AttributeCategory.Mental => FilmStudyBonus,
                _ => DevelopmentBonus
            };

            float trainerBonus = GetTrainerBonus(category);

            return Mathf.Min(facilityBonus + trainerBonus, 1f);  // Cap at 100% bonus
        }

        // ==================== UPGRADES ====================

        /// <summary>
        /// Upgrades a specific facility area.
        /// </summary>
        public (bool success, string message, int cost) UpgradeArea(FacilityArea area)
        {
            int currentRating = GetAreaRating(area);
            if (currentRating >= 5)
                return (false, $"{area} is already at maximum level.", 0);

            int cost = CalculateUpgradeCost(area, currentRating);

            SetAreaRating(area, currentRating + 1);
            RecalculateOverallRating();

            return (true, $"Upgraded {area} to {currentRating + 1} stars.", cost);
        }

        private int GetAreaRating(FacilityArea area)
        {
            return area switch
            {
                FacilityArea.WeightRoom => WeightRoom,
                FacilityArea.CourtFacility => CourtFacility,
                FacilityArea.ShootingLab => ShootingLab,
                FacilityArea.FilmRoom => FilmRoom,
                FacilityArea.RecoveryCenter => RecoveryCenter,
                FacilityArea.SportsScience => SportsScience,
                FacilityArea.NutritionCenter => NutritionCenter,
                _ => 1
            };
        }

        private void SetAreaRating(FacilityArea area, int rating)
        {
            rating = Mathf.Clamp(rating, 1, 5);
            switch (area)
            {
                case FacilityArea.WeightRoom: WeightRoom = rating; break;
                case FacilityArea.CourtFacility: CourtFacility = rating; break;
                case FacilityArea.ShootingLab: ShootingLab = rating; break;
                case FacilityArea.FilmRoom: FilmRoom = rating; break;
                case FacilityArea.RecoveryCenter: RecoveryCenter = rating; break;
                case FacilityArea.SportsScience: SportsScience = rating; break;
                case FacilityArea.NutritionCenter: NutritionCenter = rating; break;
            }
        }

        private int CalculateUpgradeCost(FacilityArea area, int currentRating)
        {
            // Exponential cost increase
            int baseCost = 500_000;
            return baseCost * (int)Math.Pow(2, currentRating);
        }

        private void RecalculateOverallRating()
        {
            float avg = (WeightRoom + CourtFacility + ShootingLab + FilmRoom +
                        RecoveryCenter + SportsScience + NutritionCenter) / 7f;
            OverallRating = Mathf.RoundToInt(avg);
        }

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates a facility with specified star rating.
        /// </summary>
        public static TrainingFacility Create(string teamId, int starRating)
        {
            starRating = Mathf.Clamp(starRating, 1, 5);

            var facility = new TrainingFacility
            {
                TeamId = teamId,
                FacilityName = $"{teamId} Training Center",
                OverallRating = starRating,
                WeightRoom = starRating,
                CourtFacility = starRating,
                ShootingLab = starRating,
                FilmRoom = starRating,
                RecoveryCenter = starRating,
                SportsScience = starRating,
                NutritionCenter = starRating,
                AnnualMaintenanceCost = starRating * 500_000,
                UpgradeCost = 1_000_000 * starRating
            };

            return facility;
        }

        /// <summary>
        /// Creates a facility with varied area ratings.
        /// </summary>
        public static TrainingFacility CreateVaried(string teamId, int baseRating, System.Random rng = null)
        {
            rng ??= new System.Random();
            baseRating = Mathf.Clamp(baseRating, 1, 5);

            var facility = new TrainingFacility
            {
                TeamId = teamId,
                FacilityName = $"{teamId} Training Center",
                WeightRoom = Mathf.Clamp(baseRating + rng.Next(-1, 2), 1, 5),
                CourtFacility = Mathf.Clamp(baseRating + rng.Next(-1, 2), 1, 5),
                ShootingLab = Mathf.Clamp(baseRating + rng.Next(-1, 2), 1, 5),
                FilmRoom = Mathf.Clamp(baseRating + rng.Next(-1, 2), 1, 5),
                RecoveryCenter = Mathf.Clamp(baseRating + rng.Next(-1, 2), 1, 5),
                SportsScience = Mathf.Clamp(baseRating + rng.Next(-1, 2), 1, 5),
                NutritionCenter = Mathf.Clamp(baseRating + rng.Next(-1, 2), 1, 5)
            };

            facility.RecalculateOverallRating();
            facility.AnnualMaintenanceCost = facility.OverallRating * 500_000;
            facility.UpgradeCost = 1_000_000 * facility.OverallRating;

            return facility;
        }
    }

    /// <summary>
    /// Specific areas of a training facility that can be upgraded.
    /// </summary>
    public enum FacilityArea
    {
        WeightRoom,
        CourtFacility,
        ShootingLab,
        FilmRoom,
        RecoveryCenter,
        SportsScience,
        NutritionCenter
    }

    /// <summary>
    /// Skill-specific trainer that provides development bonuses.
    /// </summary>
    [Serializable]
    public class Trainer
    {
        public string TrainerId;
        public string Name;
        public AttributeCategory Specialization;

        [Range(0, 100)] public int SkillRating;        // Expertise level
        [Range(0, 100)] public int Communication;      // How well they teach
        [Range(0, 100)] public int Reputation;         // Attracts players

        public int AnnualSalary;
        public int ExperienceYears;

        /// <summary>
        /// Development bonus this trainer provides (0-0.25).
        /// </summary>
        public float EffectivenessBonus
        {
            get
            {
                float skillBonus = SkillRating / 100f * 0.15f;
                float commBonus = Communication / 100f * 0.1f;
                return skillBonus + commBonus;
            }
        }

        /// <summary>
        /// Creates a random trainer.
        /// </summary>
        public static Trainer CreateRandom(AttributeCategory specialization, System.Random rng = null)
        {
            rng ??= new System.Random();

            var firstNames = new[] { "Mike", "John", "Dave", "Steve", "Tom", "Chris", "Mark", "Paul" };
            var lastNames = new[] { "Thompson", "Williams", "Brown", "Davis", "Johnson", "Miller", "Wilson" };

            int experience = rng.Next(1, 25);

            return new Trainer
            {
                TrainerId = $"TRAINER_{Guid.NewGuid().ToString().Substring(0, 8)}",
                Name = $"{firstNames[rng.Next(firstNames.Length)]} {lastNames[rng.Next(lastNames.Length)]}",
                Specialization = specialization,
                SkillRating = 40 + rng.Next(50) + Math.Min(experience, 15),
                Communication = 40 + rng.Next(50),
                Reputation = 30 + rng.Next(60),
                ExperienceYears = experience,
                AnnualSalary = 100_000 + experience * 10_000 + rng.Next(50_000)
            };
        }
    }
}
