using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// Stats-based player value calculator for trade evaluation.
    /// Replaces salary-tier approach with multi-factor assessment.
    /// </summary>
    public class PlayerValueCalculator
    {
        private readonly PlayerDatabase _playerDatabase;
        private readonly SalaryCapManager _capManager;

        // Cache for computed values (cleared on stats change)
        private Dictionary<string, PlayerValueAssessment> _valueCache = new Dictionary<string, PlayerValueAssessment>();

        // Value scale constants (0-150 scale for total value)
        private const float MAX_VALUE = 150f;
        private const float STAR_THRESHOLD = 100f;
        private const float ALL_STAR_THRESHOLD = 75f;
        private const float STARTER_THRESHOLD = 50f;
        private const float ROTATION_THRESHOLD = 30f;
        private const float BENCH_THRESHOLD = 15f;

        // Position scarcity modifiers
        private static readonly Dictionary<Position, float> PositionScarcityModifiers = new Dictionary<Position, float>
        {
            { Position.Center, 1.10f },       // +10% - rare position
            { Position.PointGuard, 1.05f },   // +5% - playmaking premium
            { Position.ShootingGuard, 1.00f },
            { Position.SmallForward, 1.00f },
            { Position.PowerForward, 1.00f }
        };

        // Age curve peak values by position category
        private static readonly Dictionary<string, (int peakStart, int peakEnd, int declineStart)> AgeCurves = new Dictionary<string, (int, int, int)>
        {
            { "Guard", (27, 30, 31) },    // Guards: Peak 27-30, decline at 31
            { "Wing", (26, 30, 32) },     // Wings: Peak 26-30, decline at 32
            { "Big", (26, 29, 30) }       // Bigs: Peak 26-29, decline at 30
        };

        public PlayerValueCalculator(PlayerDatabase playerDatabase, SalaryCapManager capManager)
        {
            _playerDatabase = playerDatabase;
            _capManager = capManager;
        }

        /// <summary>
        /// Calculate comprehensive player value for trade purposes.
        /// </summary>
        public PlayerValueAssessment CalculateValue(Player player, Contract contract, FrontOfficeProfile evaluatingFO)
        {
            if (player == null) return CreateEmptyAssessment();

            string cacheKey = $"{player.PlayerId}_{evaluatingFO?.TeamId ?? "default"}";

            // Production value based on OverallRating (0-100 scale mapped to 0-80)
            float productionValue = CalculateProductionValue(player);

            // Potential value based on HiddenPotential and age (0-40)
            float potentialValue = CalculatePotentialValue(player);

            // Contract value - how good is the deal? (-20 to +30)
            float contractValue = CalculateContractValue(player, contract);

            // Position scarcity modifier (multiplicative)
            float positionMultiplier = CalculatePositionScarcityMultiplier(player);

            // Age curve modifier (multiplicative)
            float ageCurveMultiplier = CalculateAgeCurveMultiplier(player);

            // Base value before FO modifiers
            float baseValue = (productionValue + potentialValue + contractValue) * positionMultiplier * ageCurveMultiplier;

            // Apply FO personality modifiers
            float adjustedValue = ApplyFOPersonalityModifiers(baseValue, player, evaluatingFO);

            // Clamp to valid range
            float totalValue = Mathf.Clamp(adjustedValue, 0f, MAX_VALUE);

            var assessment = new PlayerValueAssessment
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                TotalValue = totalValue,
                ProductionValue = productionValue,
                PotentialValue = potentialValue,
                ContractValue = contractValue,
                PositionScarcityModifier = positionMultiplier,
                AgeCurveModifier = ageCurveMultiplier,
                ValueTier = DetermineValueTier(totalValue),
                CalculatedAt = DateTime.Now
            };

            return assessment;
        }

        /// <summary>
        /// Calculate production value based on overall rating and role.
        /// </summary>
        private float CalculateProductionValue(Player player)
        {
            int overall = player.OverallRating;

            // Map 0-100 overall to 0-80 production value
            // Elite players (90+) get bonus value
            float baseProduction = overall * 0.7f;

            // Star bonus for elite players
            if (overall >= 90)
            {
                baseProduction += (overall - 90) * 2f; // Extra 0-20 points for stars
            }
            else if (overall >= 80)
            {
                baseProduction += (overall - 80) * 0.8f; // Extra 0-8 points for all-stars
            }

            return Mathf.Clamp(baseProduction, 0f, 80f);
        }

        /// <summary>
        /// Calculate potential value based on hidden potential, current rating, and age.
        /// </summary>
        private float CalculatePotentialValue(Player player)
        {
            int potential = player.HiddenPotential;
            int current = player.OverallRating;
            int age = player.Age;

            // Growth room = potential - current rating
            int growthRoom = Mathf.Max(0, potential - current);

            // Young players' potential is worth more
            float ageMultiplier = age switch
            {
                <= 21 => 1.5f,  // Very young - maximum potential value
                <= 23 => 1.3f,  // Young
                <= 25 => 1.0f,  // Still developing
                <= 27 => 0.5f,  // Peak approaching, less growth expected
                _ => 0.2f       // Past peak - minimal potential value
            };

            // Base potential value from growth room
            float potentialValue = growthRoom * 0.4f * ageMultiplier;

            // Bonus for very high potential players
            if (potential >= 90 && age <= 24)
            {
                potentialValue += 10f; // Premium for potential star
            }

            return Mathf.Clamp(potentialValue, 0f, 40f);
        }

        /// <summary>
        /// Calculate contract value (production vs salary efficiency).
        /// Positive = underpaid, negative = overpaid.
        /// </summary>
        private float CalculateContractValue(Player player, Contract contract)
        {
            if (contract == null) return 0f;

            long salary = contract.CurrentYearSalary;
            int overall = player.OverallRating;
            int yearsRemaining = contract.YearsRemaining;

            // Expected salary based on overall rating
            long expectedSalary = GetExpectedSalary(overall);

            // Efficiency ratio
            float efficiency = expectedSalary > 0 ? (float)expectedSalary / salary : 1f;

            // Convert to value points
            float contractValue = 0f;

            if (efficiency >= 2.0f)
            {
                // Massively underpaid - huge value
                contractValue = 25f + (efficiency - 2f) * 5f;
            }
            else if (efficiency >= 1.5f)
            {
                // Significantly underpaid
                contractValue = 15f + (efficiency - 1.5f) * 20f;
            }
            else if (efficiency >= 1.0f)
            {
                // Fairly priced to underpaid
                contractValue = (efficiency - 1f) * 30f;
            }
            else if (efficiency >= 0.7f)
            {
                // Slightly overpaid
                contractValue = (efficiency - 1f) * 20f;
            }
            else
            {
                // Significantly overpaid
                contractValue = (efficiency - 1f) * 30f;
            }

            // Contract length modifier
            if (yearsRemaining == 1)
            {
                // Expiring - can be valuable for flexibility
                contractValue += 5f;
            }
            else if (yearsRemaining >= 4)
            {
                // Long contract - risky
                contractValue -= 5f;
            }

            return Mathf.Clamp(contractValue, -20f, 30f);
        }

        /// <summary>
        /// Get expected salary based on overall rating.
        /// </summary>
        private long GetExpectedSalary(int overall)
        {
            return overall switch
            {
                >= 90 => 40_000_000,  // Superstar max
                >= 85 => 30_000_000,  // All-NBA
                >= 80 => 22_000_000,  // All-Star level
                >= 75 => 15_000_000,  // Quality starter
                >= 70 => 10_000_000,  // Solid starter
                >= 65 => 6_000_000,   // Rotation player
                >= 60 => 3_000_000,   // Bench player
                _ => 1_500_000        // End of bench / minimum
            };
        }

        /// <summary>
        /// Calculate position scarcity multiplier.
        /// </summary>
        private float CalculatePositionScarcityMultiplier(Player player)
        {
            if (PositionScarcityModifiers.TryGetValue(player.Position, out float modifier))
            {
                return modifier;
            }
            return 1.0f;
        }

        /// <summary>
        /// Calculate age curve multiplier based on position.
        /// </summary>
        private float CalculateAgeCurveMultiplier(Player player)
        {
            int age = player.Age;
            string positionCategory = GetPositionCategory(player.Position);

            if (!AgeCurves.TryGetValue(positionCategory, out var curve))
            {
                curve = (27, 30, 31); // Default curve
            }

            // Before peak
            if (age < curve.peakStart)
            {
                // Rising value toward peak
                return 0.85f + (age - 19) * 0.025f;
            }
            // At peak
            else if (age >= curve.peakStart && age <= curve.peakEnd)
            {
                return 1.0f;
            }
            // Decline phase
            else if (age > curve.peakEnd)
            {
                int yearsDeclined = age - curve.peakEnd;
                float declineRate = positionCategory == "Big" ? 0.08f : 0.06f; // Bigs decline faster
                return Mathf.Max(0.4f, 1.0f - (yearsDeclined * declineRate));
            }

            return 1.0f;
        }

        /// <summary>
        /// Get position category for age curve lookup.
        /// </summary>
        private string GetPositionCategory(Position position)
        {
            return position switch
            {
                Position.PointGuard or Position.ShootingGuard => "Guard",
                Position.SmallForward => "Wing",
                Position.PowerForward or Position.Center => "Big",
                _ => "Wing"
            };
        }

        /// <summary>
        /// Apply front office personality modifiers to base value.
        /// </summary>
        private float ApplyFOPersonalityModifiers(float baseValue, Player player, FrontOfficeProfile fo)
        {
            if (fo == null) return baseValue;

            float adjustedValue = baseValue;
            int age = player.Age;

            // Young player preference
            if (age <= 24 && fo.ValuePreferences != null)
            {
                float youngBonus = fo.ValuePreferences.ValuesYoungPlayers * 0.15f;
                adjustedValue *= (1f + youngBonus);
            }

            // Veteran preference
            if (age >= 30 && fo.ValuePreferences != null)
            {
                float vetBonus = fo.ValuePreferences.ValuesVeterans * 0.10f;
                adjustedValue *= (1f + vetBonus);
            }

            // Star power preference
            if (player.OverallRating >= 85 && fo.ValuePreferences != null)
            {
                float starBonus = fo.ValuePreferences.ValuesStarPower * 0.10f;
                adjustedValue *= (1f + starBonus);
            }

            // Aggression affects valuation
            adjustedValue *= fo.Aggression switch
            {
                TradeAggression.Aggressive => 1.05f,  // Willing to pay more
                TradeAggression.Desperate => 1.10f,   // Will overpay
                TradeAggression.Conservative => 0.95f, // More cautious
                TradeAggression.VeryPassive => 0.90f,  // Very conservative
                _ => 1.0f
            };

            // Team situation affects valuation
            adjustedValue *= fo.CurrentSituation switch
            {
                TeamSituation.Championship when player.OverallRating >= 75 => 1.10f, // Win-now premium
                TeamSituation.Rebuilding when age <= 24 => 1.10f,                     // Youth premium
                TeamSituation.Rebuilding when age >= 28 => 0.85f,                     // Vets less valued
                _ => 1.0f
            };

            // Competence affects evaluation accuracy (add noise for less competent FOs)
            float noiseRange = (100 - fo.TradeEvaluationSkill) / 200f;
            float noise = UnityEngine.Random.Range(-noiseRange, noiseRange);
            adjustedValue *= (1f + noise);

            return adjustedValue;
        }

        /// <summary>
        /// Determine value tier label from total value.
        /// </summary>
        private string DetermineValueTier(float totalValue)
        {
            return totalValue switch
            {
                >= STAR_THRESHOLD => "Star",
                >= ALL_STAR_THRESHOLD => "All-Star",
                >= STARTER_THRESHOLD => "Quality Starter",
                >= ROTATION_THRESHOLD => "Rotation",
                >= BENCH_THRESHOLD => "Bench",
                _ => "Fringe"
            };
        }

        /// <summary>
        /// Get qualitative value description (for UI - hides exact numbers).
        /// </summary>
        public string GetQualitativeDescription(PlayerValueAssessment assessment)
        {
            if (assessment == null) return "Unknown";

            string tier = assessment.ValueTier;
            string contractDesc = assessment.ContractValue switch
            {
                >= 15f => "on a bargain contract",
                >= 5f => "with good contract value",
                >= -5f => "at fair market value",
                >= -15f => "slightly overpaid",
                _ => "on an expensive contract"
            };

            return $"{tier} player {contractDesc}";
        }

        /// <summary>
        /// Clear value cache (call when player stats change).
        /// </summary>
        public void InvalidateCache(string playerId = null)
        {
            if (playerId == null)
            {
                _valueCache.Clear();
            }
            else
            {
                var keysToRemove = new List<string>();
                foreach (var key in _valueCache.Keys)
                {
                    if (key.StartsWith(playerId))
                        keysToRemove.Add(key);
                }
                foreach (var key in keysToRemove)
                {
                    _valueCache.Remove(key);
                }
            }
        }

        private PlayerValueAssessment CreateEmptyAssessment()
        {
            return new PlayerValueAssessment
            {
                TotalValue = 0,
                ValueTier = "Unknown"
            };
        }

        /// <summary>
        /// Batch calculate values for a roster.
        /// </summary>
        public List<PlayerValueAssessment> CalculateRosterValues(List<Player> players, FrontOfficeProfile evaluatingFO)
        {
            var assessments = new List<PlayerValueAssessment>();
            foreach (var player in players)
            {
                var contract = _capManager?.GetContract(player.PlayerId);
                assessments.Add(CalculateValue(player, contract, evaluatingFO));
            }
            return assessments;
        }

        /// <summary>
        /// Compare two players' trade values.
        /// Returns positive if player1 is more valuable.
        /// </summary>
        public float CompareValues(Player player1, Player player2, FrontOfficeProfile evaluatingFO)
        {
            var contract1 = _capManager?.GetContract(player1.PlayerId);
            var contract2 = _capManager?.GetContract(player2.PlayerId);

            var assessment1 = CalculateValue(player1, contract1, evaluatingFO);
            var assessment2 = CalculateValue(player2, contract2, evaluatingFO);

            return assessment1.TotalValue - assessment2.TotalValue;
        }
    }

    /// <summary>
    /// Complete assessment of a player's trade value.
    /// </summary>
    [Serializable]
    public class PlayerValueAssessment
    {
        public string PlayerId;
        public string PlayerName;

        // Total value on 0-150 scale
        public float TotalValue;

        // Component values
        public float ProductionValue;      // Current production (0-80)
        public float PotentialValue;       // Future upside (0-40)
        public float ContractValue;        // Contract efficiency (-20 to +30)

        // Modifiers applied
        public float PositionScarcityModifier;
        public float AgeCurveModifier;

        // Qualitative tier
        public string ValueTier;           // "Star", "All-Star", "Quality Starter", etc.

        public DateTime CalculatedAt;

        // Helper for UI display
        public string GetValueDescription()
        {
            return $"{ValueTier} ({TotalValue:F0} value)";
        }
    }
}
