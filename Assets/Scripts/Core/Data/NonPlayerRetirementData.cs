using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Reason for retirement
    /// </summary>
    [Serializable]
    public enum NonPlayerRetirementReason
    {
        Age,                    // Voluntary retirement due to age
        Unemployment,           // Forced - 3 years without job
        Health,                 // Random health issues
        Voluntary,              // Chose to retire (success-related)
        FamilyReasons,          // Personal reasons
        Burnout                 // Extended stress/losses
    }

    /// <summary>
    /// Factors that influence retirement probability for non-players
    /// (coaches, GMs, scouts, etc.)
    /// </summary>
    [Serializable]
    public class NonPlayerRetirementFactors
    {
        [Header("Factor Weights (should sum to 1.0)")]
        [Range(0f, 1f)] public float AgeWeight = 0.35f;
        [Range(0f, 1f)] public float UnemploymentWeight = 0.30f;
        [Range(0f, 1f)] public float RecentSuccessWeight = 0.20f;
        [Range(0f, 1f)] public float HealthWeight = 0.10f;
        [Range(0f, 1f)] public float WealthWeight = 0.05f;

        [Header("Calculated Values (0-100)")]
        [Range(0, 100)] public float AgeFactor;              // Higher = more likely to retire
        [Range(0, 100)] public float UnemploymentFactor;     // Higher = more likely to retire
        [Range(0, 100)] public float RecentSuccessFactor;    // Higher = LESS likely to retire
        [Range(0, 100)] public float HealthFactor;           // Random element
        [Range(0, 100)] public float WealthFactor;           // Long career = financially secure

        [Header("Result")]
        public float FinalRetirementChance;                  // 0.0 to 1.0
        public bool WouldForceRetirement;
        public NonPlayerRetirementReason PrimaryReason;

        /// <summary>
        /// Calculate total weighted retirement chance
        /// </summary>
        public void CalculateFinalChance()
        {
            // Invert success factor (high success = low retirement chance contribution)
            float invertedSuccess = 100f - RecentSuccessFactor;

            FinalRetirementChance =
                (AgeFactor * AgeWeight) +
                (UnemploymentFactor * UnemploymentWeight) +
                (invertedSuccess * RecentSuccessWeight) +
                (HealthFactor * HealthWeight) +
                (WealthFactor * WealthWeight);

            // Normalize to 0-1 range
            FinalRetirementChance /= 100f;

            // Determine primary reason
            DeterminePrimaryReason();
        }

        private void DeterminePrimaryReason()
        {
            // Forced retirement check
            if (WouldForceRetirement)
            {
                PrimaryReason = NonPlayerRetirementReason.Unemployment;
                return;
            }

            // Find highest factor
            float maxFactor = 0f;
            PrimaryReason = NonPlayerRetirementReason.Age;

            if (AgeFactor > maxFactor)
            {
                maxFactor = AgeFactor;
                PrimaryReason = NonPlayerRetirementReason.Age;
            }

            if (UnemploymentFactor > maxFactor)
            {
                maxFactor = UnemploymentFactor;
                PrimaryReason = NonPlayerRetirementReason.Unemployment;
            }

            if (HealthFactor > maxFactor)
            {
                maxFactor = HealthFactor;
                PrimaryReason = NonPlayerRetirementReason.Health;
            }

            // Success factor can lead to voluntary retirement (going out on top)
            if (RecentSuccessFactor > 80f && AgeFactor > 50f)
            {
                PrimaryReason = NonPlayerRetirementReason.Voluntary;
            }
        }

        /// <summary>
        /// Create factors for a given profile
        /// </summary>
        public static NonPlayerRetirementFactors CalculateFor(UnifiedCareerProfile profile)
        {
            var factors = new NonPlayerRetirementFactors();

            // Age factor (starts at 55, increases to 65+)
            factors.AgeFactor = CalculateAgeFactor(profile.CurrentAge);

            // Unemployment factor
            factors.UnemploymentFactor = CalculateUnemploymentFactor(profile.YearsUnemployed);
            factors.WouldForceRetirement = profile.YearsUnemployed >= 3;

            // Recent success factor
            factors.RecentSuccessFactor = CalculateSuccessFactor(profile);

            // Health factor (random with age influence)
            factors.HealthFactor = CalculateHealthFactor(profile.CurrentAge);

            // Wealth/career factor
            factors.WealthFactor = CalculateWealthFactor(profile);

            factors.CalculateFinalChance();
            return factors;
        }

        private static float CalculateAgeFactor(int age)
        {
            // Age retirement curve:
            // < 55: 0%
            // 55-58: 0-15%
            // 58-62: 15-40%
            // 62-65: 40-70%
            // 65-68: 70-95%
            // 68+: 95%+

            if (age < 55) return 0f;
            if (age >= 68) return 95f;

            if (age < 58)
                return Mathf.Lerp(0f, 15f, (age - 55) / 3f);
            if (age < 62)
                return Mathf.Lerp(15f, 40f, (age - 58) / 4f);
            if (age < 65)
                return Mathf.Lerp(40f, 70f, (age - 62) / 3f);

            // 65-68
            return Mathf.Lerp(70f, 95f, (age - 65) / 3f);
        }

        private static float CalculateUnemploymentFactor(int yearsUnemployed)
        {
            // Unemployment factor increases dramatically
            // 0 years: 0%
            // 1 year: 30%
            // 2 years: 60%
            // 3+ years: 100% (forced retirement)

            if (yearsUnemployed <= 0) return 0f;
            if (yearsUnemployed >= 3) return 100f;

            return yearsUnemployed * 30f;
        }

        private static float CalculateSuccessFactor(UnifiedCareerProfile profile)
        {
            float factor = 0f;

            // Recent championship massively reduces retirement chance
            // Check last entry for championship
            if (profile.CareerHistory.Count > 0)
            {
                var lastEntry = profile.CareerHistory[profile.CareerHistory.Count - 1];
                if (lastEntry.WonChampionship)
                    factor += 40f;
            }

            // Playoff appearances in last 5 years
            int recentPlayoffs = profile.GetRecentPlayoffAppearances(5);
            factor += recentPlayoffs * 10f;

            // Currently employed helps
            if (profile.IsEmployed())
                factor += 20f;

            return Mathf.Clamp(factor, 0f, 100f);
        }

        private static float CalculateHealthFactor(int age)
        {
            // Random health issues, slightly influenced by age
            float baseChance = UnityEngine.Random.Range(0f, 30f);
            float ageBonus = age > 60 ? (age - 60) * 2f : 0f;
            return Mathf.Clamp(baseChance + ageBonus, 0f, 50f);
        }

        private static float CalculateWealthFactor(UnifiedCareerProfile profile)
        {
            // Long career = more financially secure = can afford to retire
            int totalYears = profile.TotalCoachingYears + profile.TotalFrontOfficeYears;

            if (totalYears < 10) return 0f;
            if (totalYears >= 25) return 60f;

            return Mathf.Lerp(0f, 60f, (totalYears - 10) / 15f);
        }
    }

    /// <summary>
    /// Data for a non-player retirement announcement
    /// </summary>
    [Serializable]
    public class NonPlayerRetirementAnnouncement
    {
        public string ProfileId;
        public string PersonName;
        public UnifiedRole FinalRole;
        public string FinalTeamName;
        public int TotalYearsInBasketball;
        public int TotalYearsCoaching;
        public int TotalYearsFrontOffice;

        [Header("Career Highlights")]
        public int TotalChampionships;
        public int TotalPlayoffAppearances;
        public int TotalWins;
        public int TotalLosses;
        public List<string> TeamsWorkedFor;

        [Header("Playing Career (if former player)")]
        public bool WasFormerPlayer;
        public string PlayingCareerSummary;

        [Header("Announcement Details")]
        public string Headline;
        public string Statement;
        public bool WasForced;
        public NonPlayerRetirementReason RetirementReason;
        public int RetirementYear;

        /// <summary>
        /// Generate a retirement announcement from a profile
        /// </summary>
        public static NonPlayerRetirementAnnouncement Generate(
            UnifiedCareerProfile profile,
            int currentYear,
            NonPlayerRetirementReason reason,
            bool wasForced)
        {
            var announcement = new NonPlayerRetirementAnnouncement
            {
                ProfileId = profile.ProfileId,
                PersonName = profile.PersonName,
                FinalRole = profile.CurrentRole,
                FinalTeamName = profile.CurrentTeamName,
                TotalYearsInBasketball = profile.GetTotalYearsInBasketball(),
                TotalYearsCoaching = profile.TotalCoachingYears,
                TotalYearsFrontOffice = profile.TotalFrontOfficeYears,
                TotalChampionships = profile.TotalChampionships,
                TotalPlayoffAppearances = profile.TotalPlayoffAppearances,
                TotalWins = profile.TotalWins,
                TotalLosses = profile.TotalLosses,
                TeamsWorkedFor = new List<string>(profile.TeamsWorkedFor),
                WasFormerPlayer = profile.IsFormerPlayer,
                WasForced = wasForced,
                RetirementReason = reason,
                RetirementYear = currentYear
            };

            // Playing career summary
            if (profile.IsFormerPlayer && profile.PlayingCareer != null)
            {
                announcement.PlayingCareerSummary = GetPlayingCareerSummary(profile.PlayingCareer);
            }

            // Generate headline and statement
            announcement.Headline = GenerateHeadline(announcement);
            announcement.Statement = GenerateStatement(announcement);

            return announcement;
        }

        private static string GetPlayingCareerSummary(PlayerCareerReference career)
        {
            var parts = new List<string>();

            parts.Add($"{career.SeasonsPlayed}-year NBA veteran");

            if (career.Championships > 0)
                parts.Add($"{career.Championships}x NBA Champion");

            if (career.AllStarSelections > 0)
                parts.Add($"{career.AllStarSelections}x All-Star");

            if (career.IsHallOfFamer)
                parts.Add("Hall of Famer");

            return string.Join(", ", parts);
        }

        private static string GenerateHeadline(NonPlayerRetirementAnnouncement ann)
        {
            string roleTitle = ann.FinalRole switch
            {
                UnifiedRole.HeadCoach => "Head Coach",
                UnifiedRole.GeneralManager => "General Manager",
                UnifiedRole.Coordinator => "Coordinator",
                UnifiedRole.AssistantGM => "Assistant GM",
                UnifiedRole.PositionCoach => "Coach",
                UnifiedRole.Scout => "Scout",
                UnifiedRole.AssistantCoach => "Assistant Coach",
                UnifiedRole.Unemployed => "Basketball Executive",
                _ => "Basketball Executive"
            };

            if (ann.WasForced)
            {
                return $"{ann.PersonName} Steps Away from Basketball After {ann.TotalYearsInBasketball} Years";
            }

            if (ann.TotalChampionships > 0)
            {
                return $"{ann.TotalChampionships}x Champion {roleTitle} {ann.PersonName} Announces Retirement";
            }

            if (ann.WasFormerPlayer)
            {
                return $"Former NBA Player Turned {roleTitle} {ann.PersonName} Retires";
            }

            return $"Veteran {roleTitle} {ann.PersonName} Announces Retirement After {ann.TotalYearsInBasketball} Years";
        }

        private static string GenerateStatement(NonPlayerRetirementAnnouncement ann)
        {
            var statement = new System.Text.StringBuilder();

            // Opening
            if (ann.WasForced)
            {
                statement.Append($"After {ann.TotalYearsInBasketball} years in basketball, {ann.PersonName} ");
                statement.Append("has decided to step away from the game. ");

                if (ann.RetirementReason == NonPlayerRetirementReason.Unemployment)
                {
                    statement.Append($"Following {3} years without a position, ");
                    statement.Append($"{ann.PersonName} has chosen to pursue other interests. ");
                }
            }
            else
            {
                statement.Append($"{ann.PersonName} has announced their retirement ");
                statement.Append($"after {ann.TotalYearsInBasketball} years in professional basketball. ");
            }

            // Career summary
            if (ann.TotalYearsCoaching > 0 && ann.TotalYearsFrontOffice > 0)
            {
                statement.Append($"Their unique career spanned both the coaching ranks ({ann.TotalYearsCoaching} years) ");
                statement.Append($"and the front office ({ann.TotalYearsFrontOffice} years). ");
            }
            else if (ann.TotalYearsCoaching > 0)
            {
                statement.Append($"They spent {ann.TotalYearsCoaching} years on the sidelines as a coach. ");
            }
            else if (ann.TotalYearsFrontOffice > 0)
            {
                statement.Append($"They spent {ann.TotalYearsFrontOffice} years building rosters in the front office. ");
            }

            // Achievements
            if (ann.TotalChampionships > 0)
            {
                statement.Append($"They captured {ann.TotalChampionships} championship");
                statement.Append(ann.TotalChampionships > 1 ? "s " : " ");
                statement.Append($"and made {ann.TotalPlayoffAppearances} playoff appearances. ");
            }
            else if (ann.TotalPlayoffAppearances > 0)
            {
                statement.Append($"They guided teams to {ann.TotalPlayoffAppearances} playoff appearances. ");
            }

            // Win-loss record
            if (ann.TotalWins > 0)
            {
                float winPct = (float)ann.TotalWins / (ann.TotalWins + ann.TotalLosses);
                statement.Append($"Their career record stands at {ann.TotalWins}-{ann.TotalLosses} ({winPct:P1}). ");
            }

            // Former player note
            if (ann.WasFormerPlayer && !string.IsNullOrEmpty(ann.PlayingCareerSummary))
            {
                statement.Append($"Before their post-playing career, they were a {ann.PlayingCareerSummary}. ");
            }

            return statement.ToString();
        }

        /// <summary>
        /// Get a short summary for news feed
        /// </summary>
        public string GetShortSummary()
        {
            if (WasForced)
            {
                return $"{PersonName} leaves basketball after {TotalYearsInBasketball} years";
            }

            if (TotalChampionships > 0)
            {
                return $"{TotalChampionships}x champion {PersonName} retires";
            }

            return $"{PersonName} retires after {TotalYearsInBasketball} years in basketball";
        }
    }

    /// <summary>
    /// Configuration settings for non-player retirement
    /// </summary>
    [Serializable]
    public class NonPlayerRetirementSettings
    {
        [Header("Age Thresholds")]
        public int RetirementAgeStart = 55;
        public int RetirementAgeEnd = 68;
        public int ForcedRetirementAge = 70;

        [Header("Unemployment")]
        public int MaxUnemployedYearsBeforeForced = 3;

        [Header("Random Events")]
        [Range(0f, 0.1f)]
        public float BaseHealthIssueChance = 0.02f;  // 2% per year
        [Range(0f, 0.1f)]
        public float BaseFamilyReasonsChance = 0.01f;  // 1% per year

        [Header("Success Modifiers")]
        public float ChampionshipRetirementReduction = 0.30f;  // -30%
        public float PlayoffAppearanceReduction = 0.05f;  // -5% per appearance (last 5 years)
        public int PlayoffAppearanceWindowYears = 5;

        /// <summary>
        /// Get default settings
        /// </summary>
        public static NonPlayerRetirementSettings Default => new NonPlayerRetirementSettings();
    }
}
