using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core; // For LegacyTier

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Career progression stages for former players entering front office
    /// </summary>
    [Serializable]
    public enum FrontOfficeProgressionStage
    {
        Scout,              // Entry level: 3-5 years
        AssistantGM,        // 3-5 years
        GeneralManager      // Final stage
    }

    /// <summary>
    /// Milestone types for front office career progression
    /// </summary>
    [Serializable]
    public enum FOMilestoneType
    {
        EnteredFrontOffice,
        PromotedToAGM,
        PromotedToGM,
        FiredAsGM,
        ResignedAsGM,
        WonChampionshipAsGM,
        MadePlayoffsAsGM,
        SuccessfulDraftPick,
        SuccessfulTrade,
        BadTrade
    }

    /// <summary>
    /// Records a milestone in a former player's front office career
    /// </summary>
    [Serializable]
    public class FOMilestone
    {
        public int Year;
        public string TeamId;
        public string TeamName;
        public FOMilestoneType Type;
        public FrontOfficeProgressionStage Stage;
        public string Description;

        public static FOMilestone Create(int year, string teamId, string teamName,
            FOMilestoneType type, FrontOfficeProgressionStage stage, string description = null)
        {
            return new FOMilestone
            {
                Year = year,
                TeamId = teamId,
                TeamName = teamName,
                Type = type,
                Stage = stage,
                Description = description ?? GetDefaultDescription(type, stage)
            };
        }

        private static string GetDefaultDescription(FOMilestoneType type, FrontOfficeProgressionStage stage)
        {
            return type switch
            {
                FOMilestoneType.EnteredFrontOffice => "Joined front office as Scout",
                FOMilestoneType.PromotedToAGM => "Promoted to Assistant General Manager",
                FOMilestoneType.PromotedToGM => "Named General Manager",
                FOMilestoneType.FiredAsGM => "Dismissed as General Manager",
                FOMilestoneType.ResignedAsGM => "Resigned as General Manager",
                FOMilestoneType.WonChampionshipAsGM => "Won NBA Championship as GM",
                FOMilestoneType.MadePlayoffsAsGM => "Led team to playoffs",
                FOMilestoneType.SuccessfulDraftPick => "Made successful draft selection",
                FOMilestoneType.SuccessfulTrade => "Executed successful trade",
                FOMilestoneType.BadTrade => "Made regrettable trade",
                _ => "Career milestone"
            };
        }
    }

    /// <summary>
    /// Tracks a former player's progression through front office ranks
    /// </summary>
    [Serializable]
    public class FrontOfficeProgressionData
    {
        public string ProgressionId;
        public string FormerPlayerId;
        public string FormerPlayerName;
        public string UnifiedProfileId;  // Link to UnifiedCareerProfile for cross-track transitions

        [Header("Progression Status")]
        public FrontOfficeProgressionStage CurrentStage;
        public int YearsAtCurrentStage;
        public int TotalFOCareerYears;
        public int YearEnteredFrontOffice;

        [Header("Current Position")]
        public string CurrentTeamId;
        public string CurrentTeamName;
        public bool IsActiveGM;

        [Header("History")]
        public List<string> TeamsWorkedFor = new List<string>();
        public List<FOMilestone> Milestones = new List<FOMilestone>();

        [Header("Performance Tracking")]
        public int TradesWon;
        public int TradesLost;
        public int DraftPicksHit;           // Successful draft picks
        public int DraftPicksBusted;        // Failed draft picks
        public int PlayoffAppearancesAsGM;
        public int ChampionshipsAsGM;
        public int TimesFired;

        [Header("Job Security (GM only)")]
        public int WarningsReceived;        // 2 warnings before firing
        public int ConsecutivePlayoffMisses;

        /// <summary>
        /// Calculate promotion eligibility based on time served and performance
        /// </summary>
        public bool IsEligibleForPromotion()
        {
            return CurrentStage switch
            {
                FrontOfficeProgressionStage.Scout => YearsAtCurrentStage >= 3,
                FrontOfficeProgressionStage.AssistantGM => YearsAtCurrentStage >= 3,
                FrontOfficeProgressionStage.GeneralManager => false, // Already at top
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
                FrontOfficeProgressionStage.Scout => 3,
                FrontOfficeProgressionStage.AssistantGM => 3,
                FrontOfficeProgressionStage.GeneralManager => 0,
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
                FrontOfficeProgressionStage.Scout => 5,
                FrontOfficeProgressionStage.AssistantGM => 5,
                FrontOfficeProgressionStage.GeneralManager => 99,
                _ => 99
            };
        }

        /// <summary>
        /// Calculate base promotion chance (before modifiers)
        /// </summary>
        public float GetBasePromotionChance(LegacyTier legacyTier)
        {
            if (!IsEligibleForPromotion())
                return 0f;

            float baseChance = 0.30f;

            // Time bonus: +5% per year over minimum
            int yearsOverMin = YearsAtCurrentStage - GetMinYearsAtStage();
            baseChance += yearsOverMin * 0.05f;

            // Legacy bonus - higher profile players get promoted faster
            baseChance += legacyTier switch
            {
                LegacyTier.HallOfFame => 0.15f,
                LegacyTier.AllTimeTier => 0.10f,
                LegacyTier.StarPlayer => 0.05f,
                _ => 0f
            };

            // Performance bonus for AGMs trying to become GM
            if (CurrentStage == FrontOfficeProgressionStage.AssistantGM)
            {
                baseChance += (TradesWon - TradesLost) * 0.02f;
                baseChance += (DraftPicksHit - DraftPicksBusted) * 0.03f;
            }

            return Mathf.Clamp01(baseChance);
        }

        /// <summary>
        /// Reset progression after being fired as GM
        /// </summary>
        public void HandleFiredAsGM(int currentYear, string teamId, string teamName)
        {
            // Record milestone
            Milestones.Add(FOMilestone.Create(
                currentYear, teamId, teamName,
                FOMilestoneType.FiredAsGM, FrontOfficeProgressionStage.GeneralManager
            ));

            // Reset to AGM level
            CurrentStage = FrontOfficeProgressionStage.AssistantGM;
            YearsAtCurrentStage = 0;
            CurrentTeamId = null;
            CurrentTeamName = null;
            IsActiveGM = false;
            TimesFired++;
            WarningsReceived = 0;
            ConsecutivePlayoffMisses = 0;
        }

        /// <summary>
        /// Get a summary of GM performance
        /// </summary>
        public string GetGMPerformanceSummary()
        {
            if (ChampionshipsAsGM > 0)
                return $"{ChampionshipsAsGM}x Champion GM";
            if (PlayoffAppearancesAsGM > 3)
                return $"{PlayoffAppearancesAsGM} playoff appearances as GM";
            if (TradesWon > TradesLost + 3)
                return "Trade wizard";
            if (DraftPicksHit > DraftPicksBusted + 2)
                return "Excellent drafter";
            return "Developing GM";
        }
    }

    /// <summary>
    /// Unique traits derived from a former player's playing career
    /// </summary>
    [Serializable]
    public class FormerPlayerGMTraits
    {
        [Header("Network")]
        public List<string> FormerTeammateIds = new List<string>();  // +15% signing bonus
        public List<string> FormerTeamIds = new List<string>();       // Teams they played for

        [Header("Background")]
        public string AlmaMater;                     // College - bonus for scouting that school
        public Position PlayedPosition;              // +10-20 scouting for this position

        [Header("Profile")]
        [Range(0, 100)]
        public int MediaPresenceLevel;               // Affects leaks + FA attraction
        public bool IsHighProfile;                   // HoF/Star = high profile

        [Header("Playing Style Influence")]
        public bool WasDefensivePlayer;              // Influences team building philosophy
        public bool WasShooter;                      // Values shooters more
        public bool WasPlaymaker;                    // Values playmakers more
        public bool WasPhysicalPlayer;               // Values physicality
        public bool WasHighIQPlayer;                 // Values basketball IQ

        /// <summary>
        /// Get signing bonus for former teammates
        /// </summary>
        public float GetFormerTeammateSigningBonus(string playerId)
        {
            if (FormerTeammateIds.Contains(playerId))
                return 0.15f; // 15% bonus
            return 0f;
        }

        /// <summary>
        /// Get position scouting bonus
        /// </summary>
        public int GetPositionScoutingBonus(Position position)
        {
            if (position == PlayedPosition)
                return 20;

            // Adjacent positions get partial bonus
            bool isAdjacent = (PlayedPosition, position) switch
            {
                (Position.PointGuard, Position.ShootingGuard) => true,
                (Position.ShootingGuard, Position.PointGuard) => true,
                (Position.ShootingGuard, Position.SmallForward) => true,
                (Position.SmallForward, Position.ShootingGuard) => true,
                (Position.SmallForward, Position.PowerForward) => true,
                (Position.PowerForward, Position.SmallForward) => true,
                (Position.PowerForward, Position.Center) => true,
                (Position.Center, Position.PowerForward) => true,
                _ => false
            };

            return isAdjacent ? 10 : 0;
        }

        /// <summary>
        /// Get leak modifier based on media presence
        /// </summary>
        public int GetMediaPresenceLeakModifier()
        {
            return MediaPresenceLevel / 5; // 0-20 modifier
        }

        /// <summary>
        /// Get free agent attraction bonus for high profile GMs
        /// </summary>
        public float GetFreeAgentAttractionBonus()
        {
            if (IsHighProfile)
                return 0.10f; // 10% bonus
            if (MediaPresenceLevel > 70)
                return 0.05f; // 5% bonus
            return 0f;
        }
    }

    /// <summary>
    /// Extended front office data for former NBA players who became GMs.
    /// Includes their playing career history and derived specializations.
    /// </summary>
    [Serializable]
    public class FormerPlayerGM
    {
        // ==================== IDENTITY ====================
        public string FormerPlayerGMId;
        public string FormerPlayerId;             // Original player ID

        // ==================== PLAYING CAREER ====================
        public PlayerCareerReference PlayingCareer;

        // ==================== DERIVED TRAITS ====================
        public FormerPlayerGMTraits DerivedTraits;

        // ==================== PROGRESSION ====================
        public FrontOfficeProgressionData FOProgression;

        // ==================== PERSONALITY ====================
        public List<PlayerPersonalityTrait> PersonalityTraits = new List<PlayerPersonalityTrait>();

        // ==================== USER RELATIONSHIP ====================
        [Header("User Relationship")]
        public bool WasCoachedByUser;
        public int SeasonsUnderUserCoaching;
        public string UserTeamIdWhenCoached;

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Create a FormerPlayerGM from retired player data
        /// </summary>
        public static FormerPlayerGM CreateFromRetiredPlayer(
            PlayerCareerData playerData,
            int currentYear,
            List<PlayerPersonalityTrait> traits = null,
            bool wasCoachedByUser = false,
            int seasonsUnderUser = 0,
            string userTeamId = null)
        {
            var formerPlayerGM = new FormerPlayerGM
            {
                FormerPlayerGMId = Guid.NewGuid().ToString(),
                FormerPlayerId = playerData.PlayerId,
                PlayingCareer = PlayerCareerReference.FromPlayerCareerData(playerData),
                PersonalityTraits = traits ?? GenerateDefaultTraits(playerData),
                WasCoachedByUser = wasCoachedByUser,
                SeasonsUnderUserCoaching = seasonsUnderUser,
                UserTeamIdWhenCoached = userTeamId
            };

            // Create progression data
            formerPlayerGM.FOProgression = CreateInitialProgression(
                playerData.PlayerId,
                playerData.FullName,
                currentYear
            );

            // Derive traits from playing career
            formerPlayerGM.DerivedTraits = DeriveTraits(playerData, formerPlayerGM.PlayingCareer);

            return formerPlayerGM;
        }

        /// <summary>
        /// Create a FrontOfficeProfile when this former player becomes a GM
        /// </summary>
        public FrontOfficeProfile CreateGMProfile(string teamId)
        {
            var profile = new FrontOfficeProfile
            {
                TeamId = teamId,
                GMName = PlayingCareer?.FullName ?? "Unknown GM",

                // Core competence based on legacy
                Competence = CalculateCompetence(),

                // Skills derived from playing career
                TradeEvaluationSkill = CalculateTradeEvaluationSkill(),
                NegotiationSkill = CalculateNegotiationSkill(),
                ScoutingAccuracy = CalculateScoutingAccuracy(),

                // Tendencies
                Aggression = CalculateAggression(),
                Patience = CalculatePatience(),
                RiskTolerance = CalculateRiskTolerance(),

                // Behavior
                LeakBehavior = CreateLeakBehavior(),

                WillingToTakeOnBadContracts = CalculateWillingToTakeOnBadContracts(),
                WillingToIncludePicks = CalculateWillingToIncludePicks(),

                // Relationships
                RelationshipsWithOtherGMs = new Dictionary<string, int>(),

                // Track record
                TradesCompleted = FOProgression?.TradesWon + FOProgression?.TradesLost ?? 0,
                TradesWon = FOProgression?.TradesWon ?? 0,
                TradesLost = FOProgression?.TradesLost ?? 0,
                YearsInPosition = 0 // New GM
            };

            // Mark progression as active GM
            if (FOProgression != null)
            {
                FOProgression.CurrentTeamId = teamId;
                FOProgression.CurrentStage = FrontOfficeProgressionStage.GeneralManager;
                FOProgression.IsActiveGM = true;
                FOProgression.YearsAtCurrentStage = 0;
            }

            return profile;
        }

        // ==================== SKILL CALCULATIONS ====================
        // Based on plan: IQ + PositionBonus ± 10, (Lead + IQ)/2 ± 10, etc.

        private FrontOfficeCompetence CalculateCompetence()
        {
            // Legacy determines starting competence tier
            return PlayingCareer?.LegacyTier switch
            {
                LegacyTier.HallOfFame => UnityEngine.Random.value > 0.3f
                    ? FrontOfficeCompetence.Good : FrontOfficeCompetence.Elite,
                LegacyTier.AllTimeTier => UnityEngine.Random.value > 0.5f
                    ? FrontOfficeCompetence.Good : FrontOfficeCompetence.Average,
                LegacyTier.StarPlayer => FrontOfficeCompetence.Average,
                LegacyTier.SolidCareer => UnityEngine.Random.value > 0.5f
                    ? FrontOfficeCompetence.Average : FrontOfficeCompetence.Poor,
                _ => FrontOfficeCompetence.Average
            };
        }

        private int CalculateTradeEvaluationSkill()
        {
            int iq = PlayingCareer?.FinalBasketballIQ ?? 50;
            int positionBonus = DerivedTraits?.GetPositionScoutingBonus(PlayingCareer?.PrimaryPosition ?? Position.PointGuard) ?? 0;
            int variance = UnityEngine.Random.Range(-10, 11);

            return Mathf.Clamp(iq + positionBonus / 2 + variance, 30, 95);
        }

        private int CalculateNegotiationSkill()
        {
            int iq = PlayingCareer?.FinalBasketballIQ ?? 50;
            int leadership = PlayingCareer?.FinalLeadership ?? 50;
            int baseValue = (iq + leadership) / 2;
            int variance = UnityEngine.Random.Range(-10, 11);

            // High profile players are better negotiators
            if (DerivedTraits?.IsHighProfile == true)
                baseValue += 10;

            return Mathf.Clamp(baseValue + variance, 30, 95);
        }

        private int CalculateScoutingAccuracy()
        {
            int iq = PlayingCareer?.FinalBasketballIQ ?? 50;
            int expBonus = Mathf.Min(PlayingCareer?.SeasonsPlayed ?? 0, 15); // Cap at 15
            int variance = UnityEngine.Random.Range(-10, 11);

            // Film junkies are better scouts
            if (PersonalityTraits.Contains(PlayerPersonalityTrait.FilmJunkie))
                iq += 10;

            return Mathf.Clamp(iq + expBonus + variance, 30, 95);
        }

        private int CalculatePatience()
        {
            // Experience breeds patience
            int seasons = PlayingCareer?.SeasonsPlayed ?? 6;
            int baseValue = seasons >= 12 ? 70 : 50;
            int variance = UnityEngine.Random.Range(-10, 11);

            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Professional))
                baseValue += 10;

            return Mathf.Clamp(baseValue + variance, 30, 90);
        }

        private int CalculateRiskTolerance()
        {
            // Championship winners are more conservative
            int championships = PlayingCareer?.Championships ?? 0;
            int baseValue = championships > 0 ? 40 : 60;
            int variance = UnityEngine.Random.Range(-10, 11);

            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Competitor))
                baseValue += 15;

            return Mathf.Clamp(baseValue + variance, 20, 80);
        }

        private TradeAggression CalculateAggression()
        {
            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Competitor))
                return TradeAggression.Aggressive;

            if (PersonalityTraits.Contains(PlayerPersonalityTrait.Professional))
                return TradeAggression.Moderate;

            if (PlayingCareer?.Championships > 0)
                return TradeAggression.Conservative;

            return TradeAggression.Moderate;
        }

        private bool CalculateWillingToTakeOnBadContracts()
        {
            // More experienced GMs willing to absorb contracts for assets
            return FOProgression?.TotalFOCareerYears > 5 || DerivedTraits?.IsHighProfile == true;
        }

        private bool CalculateWillingToIncludePicks()
        {
            // Championship winners protect picks more
            return PlayingCareer?.Championships == 0 || PersonalityTraits.Contains(PlayerPersonalityTrait.Competitor);
        }

        private LeakTendency CreateLeakBehavior()
        {
            int mediaPresence = DerivedTraits?.MediaPresenceLevel ?? 50;
            int leakModifier = DerivedTraits?.GetMediaPresenceLeakModifier() ?? 10;

            return new LeakTendency
            {
                BaseLeakChance = 20 + leakModifier,
                StarPlayerLeakBonus = mediaPresence > 70 ? 25 : 15,
                DeadlineLeakBonus = 15,
                LeaksToFavoredReporters = mediaPresence > 80,
                FavoredReporters = mediaPresence > 80
                    ? new List<string> { "Woj", "Shams" }
                    : new List<string>()
            };
        }

        // ==================== DERIVATION METHODS ====================

        private static FrontOfficeProgressionData CreateInitialProgression(
            string playerId, string playerName, int currentYear)
        {
            return new FrontOfficeProgressionData
            {
                ProgressionId = Guid.NewGuid().ToString(),
                FormerPlayerId = playerId,
                FormerPlayerName = playerName,
                CurrentStage = FrontOfficeProgressionStage.Scout,
                YearsAtCurrentStage = 0,
                TotalFOCareerYears = 0,
                YearEnteredFrontOffice = currentYear,
                CurrentTeamId = null,
                IsActiveGM = false,
                TeamsWorkedFor = new List<string>(),
                Milestones = new List<FOMilestone>
                {
                    FOMilestone.Create(currentYear, null, null,
                        FOMilestoneType.EnteredFrontOffice, FrontOfficeProgressionStage.Scout)
                },
                TradesWon = 0,
                TradesLost = 0,
                DraftPicksHit = 0,
                DraftPicksBusted = 0,
                PlayoffAppearancesAsGM = 0,
                ChampionshipsAsGM = 0,
                TimesFired = 0,
                WarningsReceived = 0,
                ConsecutivePlayoffMisses = 0
            };
        }

        private static FormerPlayerGMTraits DeriveTraits(
            PlayerCareerData playerData, PlayerCareerReference career)
        {
            var traits = new FormerPlayerGMTraits
            {
                PlayedPosition = career.PrimaryPosition,
                AlmaMater = null, // Would need to be set from player data if available
                FormerTeamIds = new List<string>(career.TeamsPlayedFor),
                FormerTeammateIds = new List<string>(), // Would be populated from roster history

                // Playing style influences
                WasDefensivePlayer = career.WasKnownForDefense || career.AllDefensiveSelections > 0,
                WasShooter = career.WasKnownForShooting,
                WasPlaymaker = career.WasKnownForPlaymaking,
                WasPhysicalPlayer = career.PrimaryPosition == Position.Center ||
                                   career.PrimaryPosition == Position.PowerForward,
                WasHighIQPlayer = career.FinalBasketballIQ >= 75,

                // Media presence based on legacy and accolades
                MediaPresenceLevel = CalculateMediaPresence(career),
                IsHighProfile = career.LegacyTier == LegacyTier.HallOfFame ||
                               career.LegacyTier == LegacyTier.AllTimeTier ||
                               career.AllStarSelections >= 5
            };

            return traits;
        }

        private static int CalculateMediaPresence(PlayerCareerReference career)
        {
            int presence = 30; // Base

            // Legacy tier
            presence += career.LegacyTier switch
            {
                LegacyTier.HallOfFame => 40,
                LegacyTier.AllTimeTier => 30,
                LegacyTier.StarPlayer => 20,
                LegacyTier.SolidCareer => 10,
                _ => 0
            };

            // Accolades
            presence += Mathf.Min(career.AllStarSelections * 3, 15);
            presence += Mathf.Min(career.Championships * 5, 15);
            if (career.MVPs > 0) presence += 10;

            return Mathf.Clamp(presence, 0, 100);
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

            // Long careers suggest Professional trait
            if (playerData.SeasonsPlayed >= 12)
            {
                traits.Add(PlayerPersonalityTrait.Professional);
            }

            // Add 1-2 random traits
            var allTraits = (PlayerPersonalityTrait[])Enum.GetValues(typeof(PlayerPersonalityTrait));
            int traitCount = rng.Next(1, 3);
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

        /// <summary>
        /// Get first name from full name
        /// </summary>
        public string GetFirstName()
        {
            if (string.IsNullOrEmpty(PlayingCareer?.FullName))
                return "Unknown";

            var parts = PlayingCareer.FullName.Split(' ');
            return parts.Length > 0 ? parts[0] : "Unknown";
        }

        /// <summary>
        /// Get last name from full name
        /// </summary>
        public string GetLastName()
        {
            if (string.IsNullOrEmpty(PlayingCareer?.FullName))
                return "GM";

            var parts = PlayingCareer.FullName.Split(' ');
            return parts.Length > 1 ? parts[^1] : parts[0];
        }

        /// <summary>
        /// Get a display description of this GM's playing career
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

        /// <summary>
        /// Get a short badge-style description for trade screens
        /// </summary>
        public string GetTradeBadge()
        {
            var parts = new List<string>();

            parts.Add($"{PlayingCareer?.SeasonsPlayed ?? 0}-yr vet");

            if (PlayingCareer?.Championships > 0)
                parts.Add($"{PlayingCareer.Championships}x Champ");

            if (PlayingCareer?.IsHallOfFamer == true)
                parts.Add("HOF");
            else if (PlayingCareer?.AllStarSelections >= 5)
                parts.Add($"{PlayingCareer.AllStarSelections}x All-Star");

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Check if this GM has a connection to a specific player
        /// </summary>
        public bool HasConnectionTo(string playerId)
        {
            return DerivedTraits?.FormerTeammateIds?.Contains(playerId) ?? false;
        }

        /// <summary>
        /// Check if this GM played for a specific team
        /// </summary>
        public bool PlayedForTeam(string teamId)
        {
            return DerivedTraits?.FormerTeamIds?.Contains(teamId) ?? false;
        }
    }
}
