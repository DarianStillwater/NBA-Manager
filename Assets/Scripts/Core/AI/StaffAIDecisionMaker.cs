using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// AI decision maker for delegated staff tasks.
    /// Staff quality SIGNIFICANTLY affects decision quality.
    /// </summary>
    public static class StaffAIDecisionMaker
    {
        // Quality thresholds and their decision accuracy
        private const int EliteThreshold = 85;
        private const int GoodThreshold = 70;
        private const int AverageThreshold = 50;

        private const float EliteAccuracy = 0.90f;
        private const float GoodAccuracy = 0.75f;
        private const float AverageAccuracy = 0.55f;
        private const float PoorAccuracy = 0.40f;

        // ==================== DECISION QUALITY ====================

        /// <summary>
        /// Calculate decision quality based on staff rating.
        /// Higher ratings = more accurate decisions.
        /// </summary>
        public static float CalculateDecisionQuality(int staffRating)
        {
            if (staffRating >= EliteThreshold) return EliteAccuracy;
            if (staffRating >= GoodThreshold) return GoodAccuracy;
            if (staffRating >= AverageThreshold) return AverageAccuracy;
            return PoorAccuracy;
        }

        /// <summary>
        /// Apply variance to a decision quality based on randomness.
        /// Even poor staff can get lucky, elite staff can miss.
        /// </summary>
        public static float ApplyQualityVariance(float baseQuality, int staffRating)
        {
            // Higher rated staff have less variance
            float varianceRange = staffRating >= GoodThreshold ? 0.10f : 0.20f;

            float variance = (UnityEngine.Random.value - 0.5f) * varianceRange;
            return Mathf.Clamp01(baseQuality + variance);
        }

        /// <summary>
        /// Get quality tier description.
        /// </summary>
        public static string GetQualityTierName(int staffRating)
        {
            if (staffRating >= EliteThreshold) return "Elite";
            if (staffRating >= GoodThreshold) return "Good";
            if (staffRating >= AverageThreshold) return "Average";
            return "Poor";
        }

        // ==================== SCOUT EVALUATIONS ====================

        /// <summary>
        /// Generate a scout evaluation of a player.
        /// Scout quality affects accuracy of the evaluation.
        /// </summary>
        public static ScoutEvaluationResult EvaluatePlayer(
            Scout scout,
            string playerId,
            string playerName,
            int actualOverall,
            int actualPotential,
            ScoutTaskType taskType)
        {
            int scoutRating = scout.OverallRating;
            float decisionQuality = CalculateDecisionQuality(scoutRating);
            decisionQuality = ApplyQualityVariance(decisionQuality, scoutRating);

            // Calculate estimated values with error based on quality
            float maxError = (1f - decisionQuality) * 20f;  // Up to 20 points off for poor scouts

            int overallError = Mathf.RoundToInt(UnityEngine.Random.Range(-maxError, maxError));
            int potentialError = Mathf.RoundToInt(UnityEngine.Random.Range(-maxError * 1.5f, maxError * 1.5f));

            int estimatedOverall = Mathf.Clamp(actualOverall + overallError, 40, 99);
            int estimatedPotential = Mathf.Clamp(actualPotential + potentialError, 40, 99);

            // Generate strengths/weaknesses based on accuracy
            string strengths = GenerateStrengthsReport(decisionQuality);
            string weaknesses = GenerateWeaknessesReport(decisionQuality);
            string recommendation = GenerateRecommendation(estimatedOverall, estimatedPotential, taskType);

            // Confidence level correlates with scout experience and rating
            int confidence = Mathf.Clamp(scout.ExperienceYears * 3 + scoutRating / 2, 30, 95);

            return new ScoutEvaluationResult
            {
                EvaluationId = $"EVAL_{Guid.NewGuid().ToString().Substring(0, 8)}",
                ScoutId = scout.ScoutId,
                TargetPlayerId = playerId,
                TaskType = taskType,
                ScoutName = scout.FullName,
                ScoutRating = scoutRating,
                EstimatedOverall = estimatedOverall,
                ActualOverall = actualOverall,
                EstimatedPotential = estimatedPotential,
                ActualPotential = actualPotential,
                OverallAccuracy = 1f - (Math.Abs(overallError) / 20f),
                PotentialAccuracy = 1f - (Math.Abs(potentialError) / 30f),
                Strengths = strengths,
                Weaknesses = weaknesses,
                RecommendedAction = recommendation,
                ConfidenceLevel = confidence,
                EvaluationDate = DateTime.Now
            };
        }

        /// <summary>
        /// Generate a draft prospect evaluation.
        /// </summary>
        public static ScoutEvaluationResult EvaluateDraftProspect(
            Scout scout,
            string prospectId,
            string prospectName,
            int actualOverall,
            int actualPotential)
        {
            // Prospect evaluation uses ProspectEvaluation skill
            var result = EvaluatePlayer(scout, prospectId, prospectName, actualOverall, actualPotential, ScoutTaskType.DraftProspectScouting);

            // Add college/international connections bonus
            float connectionBonus = (scout.CollegeConnections + scout.InternationalConnections) / 200f * 0.1f;
            result.OverallAccuracy = Mathf.Clamp01(result.OverallAccuracy + connectionBonus);

            return result;
        }

        /// <summary>
        /// Generate a trade target evaluation.
        /// </summary>
        public static ScoutEvaluationResult EvaluateTradeTarget(
            Scout scout,
            string playerId,
            string playerName,
            int actualOverall,
            int actualPotential)
        {
            // Trade target uses ProEvaluation skill
            return EvaluatePlayer(scout, playerId, playerName, actualOverall, actualPotential, ScoutTaskType.TradeTargetEvaluation);
        }

        /// <summary>
        /// Generate a free agent evaluation.
        /// </summary>
        public static ScoutEvaluationResult EvaluateFreeAgent(
            Scout scout,
            string playerId,
            string playerName,
            int actualOverall,
            int actualPotential)
        {
            var result = EvaluatePlayer(scout, playerId, playerName, actualOverall, actualPotential, ScoutTaskType.FreeAgentEvaluation);

            // Agent relationships help with FA intel
            float agentBonus = scout.AgentRelationships / 100f * 0.05f;
            result.OverallAccuracy = Mathf.Clamp01(result.OverallAccuracy + agentBonus);

            return result;
        }

        // ==================== COORDINATOR SUGGESTIONS ====================

        /// <summary>
        /// Generate an offensive coordinator suggestion during a game.
        /// </summary>
        public static CoordinatorSuggestion GetOffensiveSuggestion(
            Coach coordinator,
            int quarter,
            int timeRemaining,
            int scoreDifferential,
            string opponentTeamId)
        {
            int coachRating = coordinator.OverallRating;
            float quality = CalculateDecisionQuality(coachRating);

            var suggestions = new[]
            {
                ("Push the pace", "Opponent looks tired, attack in transition", 0.15f),
                ("Slow it down", "Control the clock and run half-court sets", -0.05f),
                ("Attack the paint", "Their rim protection is weak, get to the basket", 0.12f),
                ("Spread the floor", "Create spacing for driving lanes", 0.10f),
                ("Run pick and roll", "Their big can't handle it", 0.14f),
                ("Isolate your star", "Get mismatches and let him work", 0.08f),
                ("Move the ball", "Find the open shooter with extra passes", 0.11f),
                ("Post up", "We have a size advantage", 0.09f)
            };

            // Pick suggestion based on game situation and randomness
            int index = UnityEngine.Random.Range(0, suggestions.Length);
            var (suggestionType, reasoning, expectedImpact) = suggestions[index];

            // Adjust expected impact based on coach quality
            float adjustedImpact = expectedImpact * quality;

            // Confidence based on coach experience and rating
            int confidence = Mathf.Clamp(coordinator.ExperienceYears * 2 + coachRating / 2, 40, 95);

            return new CoordinatorSuggestion
            {
                SuggestionId = $"SUG_{Guid.NewGuid().ToString().Substring(0, 8)}",
                CoordinatorId = coordinator.CoachId,
                CoordinatorName = coordinator.FullName,
                IsOffensive = true,
                SuggestionType = suggestionType,
                Reasoning = reasoning,
                ConfidenceLevel = confidence,
                ExpectedImpact = adjustedImpact,
                Quarter = quarter,
                TimeRemainingSeconds = timeRemaining,
                ScoreDifferential = scoreDifferential,
                OpponentTeamId = opponentTeamId
            };
        }

        /// <summary>
        /// Generate a defensive coordinator suggestion during a game.
        /// </summary>
        public static CoordinatorSuggestion GetDefensiveSuggestion(
            Coach coordinator,
            int quarter,
            int timeRemaining,
            int scoreDifferential,
            string opponentTeamId)
        {
            int coachRating = coordinator.OverallRating;
            float quality = CalculateDecisionQuality(coachRating);

            var suggestions = new[]
            {
                ("Switch to zone", "Disrupt their rhythm with zone defense", 0.12f),
                ("Man-to-man pressure", "Get in their faces and force turnovers", 0.10f),
                ("Double the star", "Send help and make others beat us", 0.08f),
                ("Ice the pick and roll", "Force the ball handler baseline", 0.11f),
                ("Drop coverage", "Protect the rim and give up mid-range", 0.09f),
                ("Full court press", "Speed them up and force mistakes", 0.14f),
                ("Box and one", "Lock up their best player", 0.07f),
                ("Help defense", "Rotate and cover driving lanes", 0.10f)
            };

            int index = UnityEngine.Random.Range(0, suggestions.Length);
            var (suggestionType, reasoning, expectedImpact) = suggestions[index];

            float adjustedImpact = expectedImpact * quality;
            int confidence = Mathf.Clamp(coordinator.ExperienceYears * 2 + coachRating / 2, 40, 95);

            return new CoordinatorSuggestion
            {
                SuggestionId = $"SUG_{Guid.NewGuid().ToString().Substring(0, 8)}",
                CoordinatorId = coordinator.CoachId,
                CoordinatorName = coordinator.FullName,
                IsOffensive = false,
                SuggestionType = suggestionType,
                Reasoning = reasoning,
                ConfidenceLevel = confidence,
                ExpectedImpact = adjustedImpact,
                Quarter = quarter,
                TimeRemainingSeconds = timeRemaining,
                ScoreDifferential = scoreDifferential,
                OpponentTeamId = opponentTeamId
            };
        }

        // ==================== ASSISTANT COACH DEVELOPMENT ====================

        /// <summary>
        /// Calculate development bonus for a player from their assigned AC.
        /// </summary>
        public static float CalculateDevelopmentBonus(
            Coach assistantCoach,
            Position playerPosition,
            bool hasMatchingSpecialization)
        {
            int devRating = assistantCoach.PlayerDevelopment;
            float quality = CalculateDecisionQuality(devRating);

            // Base bonus from quality
            float bonus = quality * 0.25f;  // Up to 22.5% for elite coaches

            // Position-specific bonus
            if (playerPosition == Position.PointGuard || playerPosition == Position.ShootingGuard)
            {
                bonus += assistantCoach.GuardDevelopment / 100f * 0.10f;
            }
            else if (playerPosition == Position.PowerForward || playerPosition == Position.Center)
            {
                bonus += assistantCoach.BigManDevelopment / 100f * 0.10f;
            }

            // Specialization bonus
            if (hasMatchingSpecialization)
            {
                bonus += 0.05f;
            }

            return Mathf.Clamp(bonus, 0f, 0.35f);
        }

        /// <summary>
        /// Get capacity of how many players an AC can handle based on their rating.
        /// </summary>
        public static int GetAssistantCoachCapacity(int playerDevelopmentRating)
        {
            // Rating 0-29: 1 player
            // Rating 30-59: 2 players
            // Rating 60-89: 3 players
            // Rating 90-100: 4 players
            return 1 + (playerDevelopmentRating / 30);
        }

        // ==================== HEAD COACH AI (GM-ONLY MODE) ====================

        /// <summary>
        /// Make an in-game decision as the AI Head Coach.
        /// Used when user is in GM-only mode.
        /// </summary>
        public static HCGameDecision MakeInGameDecision(
            Coach headCoach,
            int quarter,
            int timeRemaining,
            int scoreDifferential,
            float teamFatigue)
        {
            int coachRating = headCoach.OverallRating;
            float quality = CalculateDecisionQuality(coachRating);

            var decision = new HCGameDecision
            {
                DecisionId = $"DEC_{Guid.NewGuid().ToString().Substring(0, 8)}",
                CoachId = headCoach.CoachId,
                CoachName = headCoach.FullName,
                CoachRating = coachRating,
                Quarter = quarter,
                TimeRemaining = timeRemaining,
                ScoreDifferential = scoreDifferential
            };

            // Timeout decision
            if (ShouldCallTimeout(headCoach, scoreDifferential, timeRemaining, quarter))
            {
                decision.CallTimeout = true;
                decision.TimeoutReason = "Stopping opponent run";
            }

            // Substitution decision
            if (teamFatigue > 0.7f)
            {
                decision.MakeSubstitutions = true;
                decision.SubstitutionReason = "Rest tired players";
            }

            // Strategy adjustment
            decision.StrategyAdjustment = DetermineStrategyAdjustment(headCoach, scoreDifferential, quarter);

            // Quality affects likelihood of good decisions
            decision.IsOptimalDecision = UnityEngine.Random.value < quality;

            return decision;
        }

        private static bool ShouldCallTimeout(Coach coach, int scoreDiff, int timeRemaining, int quarter)
        {
            // More experienced coaches use timeouts more strategically
            float timeoutThreshold = 0.3f - (coach.GameManagement / 100f * 0.2f);

            // Call timeout if opponent is on a run
            if (scoreDiff < -8 && UnityEngine.Random.value < timeoutThreshold)
                return true;

            // End of close game
            if (quarter == 4 && timeRemaining < 120 && Math.Abs(scoreDiff) < 5)
                return true;

            return false;
        }

        private static string DetermineStrategyAdjustment(Coach coach, int scoreDiff, int quarter)
        {
            // Late game adjustments
            if (quarter == 4)
            {
                if (scoreDiff < -10)
                    return "Press and foul to extend game";
                if (scoreDiff > 10)
                    return "Run clock and protect lead";
                if (Math.Abs(scoreDiff) < 5)
                    return "Play for last shot";
            }

            // General adjustments based on coach philosophy
            return coach.OffensivePhilosophy switch
            {
                CoachingPhilosophy.FastPace => "Push tempo in transition",
                CoachingPhilosophy.ThreePointHeavy => "Look for open threes",
                CoachingPhilosophy.InsideOut => "Establish post presence",
                _ => "Continue current approach"
            };
        }

        // ==================== HELPER METHODS ====================

        private static string GenerateStrengthsReport(float quality)
        {
            var strengths = new[]
            {
                "Excellent athleticism",
                "Strong basketball IQ",
                "Great shooter",
                "Solid defender",
                "Good court vision",
                "Natural scorer",
                "Versatile skillset",
                "Leadership qualities"
            };

            int count = quality >= 0.8f ? 3 : quality >= 0.6f ? 2 : 1;
            var selected = new List<string>();
            var rng = new System.Random();

            while (selected.Count < count)
            {
                var s = strengths[rng.Next(strengths.Length)];
                if (!selected.Contains(s)) selected.Add(s);
            }

            return string.Join(", ", selected);
        }

        private static string GenerateWeaknessesReport(float quality)
        {
            var weaknesses = new[]
            {
                "Needs to improve defense",
                "Shot selection issues",
                "Turnover prone",
                "Limited range",
                "Below average athleticism",
                "Inconsistent effort",
                "Poor free throw shooter",
                "Size concerns"
            };

            int count = quality >= 0.8f ? 2 : quality >= 0.6f ? 2 : 1;
            var selected = new List<string>();
            var rng = new System.Random();

            while (selected.Count < count)
            {
                var w = weaknesses[rng.Next(weaknesses.Length)];
                if (!selected.Contains(w)) selected.Add(w);
            }

            return string.Join(", ", selected);
        }

        private static string GenerateRecommendation(int estimatedOverall, int estimatedPotential, ScoutTaskType taskType)
        {
            if (taskType == ScoutTaskType.DraftProspectScouting)
            {
                if (estimatedPotential >= 85) return "Draft - Potential star";
                if (estimatedPotential >= 75) return "Draft - Solid contributor";
                if (estimatedPotential >= 65) return "Consider - Role player ceiling";
                return "Pass - Limited upside";
            }

            if (taskType == ScoutTaskType.TradeTargetEvaluation)
            {
                if (estimatedOverall >= 80) return "Pursue aggressively";
                if (estimatedOverall >= 70) return "Worth considering";
                return "Pass unless cheap";
            }

            if (taskType == ScoutTaskType.FreeAgentEvaluation)
            {
                if (estimatedOverall >= 75) return "Sign if affordable";
                if (estimatedOverall >= 65) return "Depth option";
                return "Training camp invite only";
            }

            return "Monitor situation";
        }
    }

    /// <summary>
    /// Decision made by AI Head Coach during a game.
    /// </summary>
    [Serializable]
    public class HCGameDecision
    {
        public string DecisionId;
        public string CoachId;
        public string CoachName;
        public int CoachRating;

        [Header("Game Context")]
        public int Quarter;
        public int TimeRemaining;
        public int ScoreDifferential;

        [Header("Decisions")]
        public bool CallTimeout;
        public string TimeoutReason;
        public bool MakeSubstitutions;
        public string SubstitutionReason;
        public string StrategyAdjustment;

        [Header("Quality")]
        public bool IsOptimalDecision;
    }
}
