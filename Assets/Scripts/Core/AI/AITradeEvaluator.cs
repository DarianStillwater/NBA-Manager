using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// AI system for evaluating trades from a team's perspective
    /// </summary>
    public class AITradeEvaluator
    {
        private Dictionary<string, FrontOfficeProfile> _frontOffices;
        private Dictionary<string, PlayerTradeStatus> _playerTradeStatus;
        private SalaryCapManager _capManager;
        private PlayerDatabase _playerDatabase;

        // Value constants for trade evaluation
        private const float STAR_PLAYER_BASE = 100f;
        private const float ALL_STAR_BASE = 70f;
        private const float STARTER_BASE = 40f;
        private const float ROTATION_BASE = 20f;
        private const float BENCH_BASE = 10f;

        private const float FIRST_ROUND_PICK_BASE = 25f;
        private const float SECOND_ROUND_PICK_BASE = 5f;
        private const float TOP_5_PICK_BONUS = 30f;
        private const float LOTTERY_PICK_BONUS = 15f;

        public AITradeEvaluator(SalaryCapManager capManager, PlayerDatabase playerDatabase)
        {
            _capManager = capManager;
            _playerDatabase = playerDatabase;
            _frontOffices = new Dictionary<string, FrontOfficeProfile>();
            _playerTradeStatus = new Dictionary<string, PlayerTradeStatus>();
        }

        /// <summary>
        /// Register a front office profile
        /// </summary>
        public void RegisterFrontOffice(FrontOfficeProfile profile)
        {
            _frontOffices[profile.TeamId] = profile;
        }

        /// <summary>
        /// Get front office profile
        /// </summary>
        public FrontOfficeProfile GetFrontOffice(string teamId)
        {
            _frontOffices.TryGetValue(teamId, out var profile);
            return profile;
        }

        /// <summary>
        /// Set player trade availability
        /// </summary>
        public void SetPlayerTradeStatus(PlayerTradeStatus status)
        {
            _playerTradeStatus[status.PlayerId] = status;
        }

        /// <summary>
        /// Get player trade status
        /// </summary>
        public PlayerTradeStatus GetPlayerTradeStatus(string playerId)
        {
            _playerTradeStatus.TryGetValue(playerId, out var status);
            return status;
        }

        // === Main Trade Evaluation ===

        /// <summary>
        /// Evaluate a trade from a specific team's perspective
        /// Returns a score where positive = good trade, negative = bad trade
        /// </summary>
        public TradeEvaluationResult EvaluateTrade(TradeProposal proposal, string evaluatingTeamId)
        {
            var result = new TradeEvaluationResult
            {
                TeamId = evaluatingTeamId,
                Proposal = proposal
            };

            var frontOffice = GetFrontOffice(evaluatingTeamId);
            if (frontOffice == null)
            {
                // Create a default average FO
                frontOffice = FrontOfficeProfile.CreateAverage(evaluatingTeamId, "Unknown GM");
            }

            // Calculate value of assets being received
            float receivingValue = 0f;
            foreach (var asset in proposal.AllAssets.Where(a => a.ReceivingTeamId == evaluatingTeamId))
            {
                var assetEval = EvaluateAsset(asset, frontOffice);
                receivingValue += assetEval.Value;
                result.AssetsReceiving.Add(assetEval);
            }

            // Calculate value of assets being given up
            float givingValue = 0f;
            foreach (var asset in proposal.AllAssets.Where(a => a.SendingTeamId == evaluatingTeamId))
            {
                var assetEval = EvaluateAsset(asset, frontOffice);
                givingValue += assetEval.Value;
                result.AssetsGiving.Add(assetEval);
            }

            // Base trade balance
            result.RawTradeBalance = receivingValue - givingValue;

            // Apply front office modifiers
            result.AdjustedBalance = ApplyFrontOfficeModifiers(result.RawTradeBalance, frontOffice, proposal);

            // Apply situational modifiers
            result.AdjustedBalance = ApplySituationalModifiers(result.AdjustedBalance, frontOffice, proposal);

            // Determine if trade is acceptable
            float threshold = GetAcceptanceThreshold(frontOffice);
            result.IsAcceptable = result.AdjustedBalance >= threshold;
            result.AcceptanceThreshold = threshold;

            // Generate reasoning
            result.Reasoning = GenerateTradeReasoning(result, frontOffice);

            return result;
        }

        /// <summary>
        /// Evaluate a single trade asset
        /// </summary>
        private AssetEvaluation EvaluateAsset(TradeAsset asset, FrontOfficeProfile frontOffice)
        {
            var eval = new AssetEvaluation
            {
                Asset = asset,
                AssetDescription = asset.GetDescription()
            };

            switch (asset.Type)
            {
                case TradeAssetType.Player:
                    eval.Value = EvaluatePlayerValue(asset, frontOffice);
                    eval.ValueBreakdown = GetPlayerValueBreakdown(asset);
                    break;

                case TradeAssetType.DraftPick:
                    eval.Value = EvaluateDraftPickValue(asset, frontOffice);
                    eval.ValueBreakdown = GetPickValueBreakdown(asset);
                    break;

                case TradeAssetType.Cash:
                    eval.Value = asset.CashAmount / 1_000_000f; // $1M = 1 point
                    eval.ValueBreakdown = $"Cash: ${asset.CashAmount / 1_000_000f:F1}M";
                    break;
            }

            return eval;
        }

        /// <summary>
        /// Evaluate player value for trade purposes
        /// </summary>
        private float EvaluatePlayerValue(TradeAsset asset, FrontOfficeProfile frontOffice)
        {
            var player = _playerDatabase?.GetPlayer(asset.PlayerId);
            var contract = _capManager?.GetContract(asset.PlayerId);

            if (player == null) return asset.Salary / 2_000_000f; // Fallback based on salary

            float value = 0f;

            // Base value from player tier (estimated from salary and hidden attributes)
            value = EstimatePlayerTierValue(player, contract);

            // Age modifier
            value *= GetAgeMultiplier(player.Age);

            // Contract value modifier
            if (contract != null)
            {
                value *= GetContractMultiplier(contract, frontOffice);
            }

            // Apply FO preference modifiers
            if (player.Age <= 24)
            {
                value *= (1f + frontOffice.ValuePreferences.ValuesYoungPlayers * 0.3f);
            }
            else if (player.Age >= 30)
            {
                value *= (1f + frontOffice.ValuePreferences.ValuesVeterans * 0.2f);
            }

            // Trade availability discount
            var tradeStatus = GetPlayerTradeStatus(asset.PlayerId);
            if (tradeStatus != null)
            {
                value *= tradeStatus.GetAskingPriceMultiplier();
            }

            // === Former Player GM Bonuses ===
            if (frontOffice.IsFormerPlayer && frontOffice.FormerPlayerTraits != null)
            {
                // Position scouting bonus: Former player GMs evaluate their position better
                // +10-20% value bonus for players at the GM's position
                int positionBonus = frontOffice.GetPositionScoutingBonus(player.PrimaryPosition);
                if (positionBonus > 0)
                {
                    // Position bonus increases perceived value (better evaluation)
                    value *= (1f + positionBonus / 100f);
                }

                // Former teammate preference bonus: GMs value their former teammates more
                // +15% value for acquiring former teammates
                float teammateBonus = frontOffice.GetFormerTeammateSigningBonus(asset.PlayerId);
                if (teammateBonus > 0)
                {
                    value *= (1f + teammateBonus);
                }
            }

            return value;
        }

        /// <summary>
        /// Estimate player tier value based on salary and attributes
        /// </summary>
        private float EstimatePlayerTierValue(Player player, Contract contract)
        {
            long salary = contract?.CurrentYearSalary ?? 0;

            // Use salary as proxy for player quality (since attributes are hidden)
            if (salary >= 35_000_000) return STAR_PLAYER_BASE;
            if (salary >= 20_000_000) return ALL_STAR_BASE;
            if (salary >= 12_000_000) return STARTER_BASE;
            if (salary >= 5_000_000) return ROTATION_BASE;
            return BENCH_BASE;
        }

        /// <summary>
        /// Get age multiplier for player value
        /// </summary>
        private float GetAgeMultiplier(int age)
        {
            if (age <= 22) return 1.3f;  // Young with upside
            if (age <= 26) return 1.15f; // Prime developing
            if (age <= 30) return 1.0f;  // Peak
            if (age <= 33) return 0.8f;  // Beginning decline
            if (age <= 35) return 0.6f;  // Declining
            return 0.4f;                  // Old
        }

        /// <summary>
        /// Get contract value multiplier
        /// </summary>
        private float GetContractMultiplier(Contract contract, FrontOfficeProfile frontOffice)
        {
            float multiplier = 1.0f;

            // Years remaining
            switch (contract.YearsRemaining)
            {
                case 1:
                    // Expiring - valuable for teams wanting flexibility
                    if (frontOffice.ValuePreferences.ValuesSalaryRelief > 0.5f)
                        multiplier = 1.1f;
                    else
                        multiplier = 0.8f; // Risk of losing player
                    break;
                case 2:
                    multiplier = 1.0f;
                    break;
                case 3:
                    multiplier = 0.95f;
                    break;
                case 4:
                    multiplier = 0.9f;
                    break;
                default:
                    multiplier = 0.85f; // Very long contract = risk
                    break;
            }

            // Bad contract penalty
            if (contract.CurrentYearSalary > 20_000_000 && contract.YearsRemaining >= 3)
            {
                // Potentially overpaid
                multiplier *= 0.85f;
            }

            return multiplier;
        }

        /// <summary>
        /// Evaluate draft pick value
        /// </summary>
        private float EvaluateDraftPickValue(TradeAsset asset, FrontOfficeProfile frontOffice)
        {
            float value = asset.IsFirstRound ? FIRST_ROUND_PICK_BASE : SECOND_ROUND_PICK_BASE;

            // Year discount (future picks worth less)
            int yearsAway = asset.Year - DateTime.Now.Year;
            float yearDiscount = Mathf.Pow(0.9f, yearsAway);
            value *= yearDiscount;

            // Protection impact
            if (asset.DraftPickDetails != null && asset.DraftPickDetails.IsProtected)
            {
                value *= 0.7f; // Protected picks worth less to receiver
            }

            // Projected pick position bonus (if we can estimate)
            // For now, assume picks from bad teams are more valuable
            var originalTeamFO = GetFrontOffice(asset.OriginalTeamId);
            if (originalTeamFO?.CurrentSituation == TeamSituation.Rebuilding)
            {
                value += LOTTERY_PICK_BONUS * yearDiscount;
            }

            // FO preference modifier
            value *= (1f + frontOffice.ValuePreferences.ValuesDraftPicks * 0.4f);

            return value;
        }

        /// <summary>
        /// Apply front office competence modifiers
        /// </summary>
        private float ApplyFrontOfficeModifiers(float baseBalance, FrontOfficeProfile frontOffice, TradeProposal proposal)
        {
            float adjusted = baseBalance;

            // Competence affects perception of value
            float evaluationError = (100 - frontOffice.TradeEvaluationSkill) / 200f;
            float randomError = UnityEngine.Random.Range(-evaluationError, evaluationError);
            adjusted *= (1 + randomError);

            // Poor negotiators accept worse deals
            adjusted *= frontOffice.GetAcceptanceThresholdModifier();

            return adjusted;
        }

        /// <summary>
        /// Apply situational modifiers based on team needs
        /// </summary>
        private float ApplySituationalModifiers(float baseBalance, FrontOfficeProfile frontOffice, TradeProposal proposal)
        {
            float adjusted = baseBalance;

            // Deadline desperation
            if (IsNearDeadline() && frontOffice.CurrentSituation == TeamSituation.Championship)
            {
                adjusted *= 0.9f; // More willing to overpay
            }

            // Cap situation
            var capStatus = _capManager?.GetCapStatus(frontOffice.TeamId);
            if (capStatus >= TeamCapStatus.AboveSecondApron)
            {
                // Desperate for salary relief
                long incomingSalary = proposal.GetIncomingSalary(frontOffice.TeamId);
                long outgoingSalary = proposal.GetOutgoingSalary(frontOffice.TeamId);

                if (outgoingSalary > incomingSalary)
                {
                    adjusted += 5f; // Bonus for getting under tax
                }
            }

            // Relationship modifier with trading partner
            var tradingWith = proposal.GetInvolvedTeams()
                .FirstOrDefault(t => t != frontOffice.TeamId);
            if (tradingWith != null)
            {
                adjusted *= frontOffice.GetRelationshipModifier(tradingWith);
            }

            return adjusted;
        }

        /// <summary>
        /// Get acceptance threshold based on front office tendencies
        /// </summary>
        private float GetAcceptanceThreshold(FrontOfficeProfile frontOffice)
        {
            // Base threshold
            float threshold = 0f;

            // Aggression affects threshold
            threshold += frontOffice.Aggression switch
            {
                TradeAggression.VeryPassive => 5f,
                TradeAggression.Conservative => 2f,
                TradeAggression.Moderate => 0f,
                TradeAggression.Aggressive => -2f,
                TradeAggression.Desperate => -5f,
                _ => 0f
            };

            // Situation affects threshold
            threshold += frontOffice.CurrentSituation switch
            {
                TeamSituation.Championship => -3f,  // More willing to deal
                TeamSituation.Rebuilding => 2f,     // Wants assets, patient
                TeamSituation.StuckInMiddle => -1f, // Wants to shake things up
                _ => 0f
            };

            return threshold;
        }

        /// <summary>
        /// Generate human-readable reasoning for trade evaluation
        /// </summary>
        private string GenerateTradeReasoning(TradeEvaluationResult result, FrontOfficeProfile frontOffice)
        {
            var reasons = new List<string>();

            if (result.IsAcceptable)
            {
                if (result.AdjustedBalance >= 10f)
                    reasons.Add("This trade significantly improves our team.");
                else if (result.AdjustedBalance >= 5f)
                    reasons.Add("This trade is favorable for us.");
                else
                    reasons.Add("This trade works for us.");

                // Situation-specific
                if (frontOffice.CurrentSituation == TeamSituation.Championship)
                    reasons.Add("We're in win-now mode and this helps us compete.");
                else if (frontOffice.CurrentSituation == TeamSituation.Rebuilding)
                    reasons.Add("This fits our rebuild timeline.");
            }
            else
            {
                if (result.AdjustedBalance < -10f)
                    reasons.Add("We'd be giving up far too much in this deal.");
                else if (result.AdjustedBalance < -5f)
                    reasons.Add("The value isn't there for us.");
                else
                    reasons.Add("We're not convinced this makes us better.");

                // What would make it work
                float deficit = result.AcceptanceThreshold - result.AdjustedBalance;
                if (deficit < 10f)
                    reasons.Add("We'd need a little more to make this work.");
                else if (deficit < 20f)
                    reasons.Add("We'd need a significantly sweetened offer.");
                else
                    reasons.Add("We're far apart on value.");
            }

            return string.Join(" ", reasons);
        }

        private bool IsNearDeadline()
        {
            // Simplified deadline check
            var now = DateTime.Now;
            return now.Month == 2 && now.Day <= 15;
        }

        private string GetPlayerValueBreakdown(TradeAsset asset)
        {
            var contract = _capManager?.GetContract(asset.PlayerId);
            string salary = contract != null ? $"${contract.CurrentYearSalary / 1_000_000f:F1}M" : "Unknown";
            string years = contract != null ? $"{contract.YearsRemaining}yr" : "";
            return $"{asset.PlayerId}: {salary}, {years}";
        }

        private string GetPickValueBreakdown(TradeAsset asset)
        {
            string pickType = asset.IsFirstRound ? "1st" : "2nd";
            string protection = asset.DraftPickDetails?.IsProtected == true ? " (protected)" : "";
            return $"{asset.Year} {pickType} ({asset.OriginalTeamId}){protection}";
        }

        // === Counter-Offer Generation ===

        /// <summary>
        /// Generate a counter-offer from the AI
        /// </summary>
        public CounterOfferResult GenerateCounterOffer(TradeProposal originalProposal, string counteringTeamId)
        {
            var result = new CounterOfferResult
            {
                OriginalProposal = originalProposal,
                CounterType = CounterOfferType.Modify
            };

            var frontOffice = GetFrontOffice(counteringTeamId);
            if (frontOffice == null) return null;

            var evaluation = EvaluateTrade(originalProposal, counteringTeamId);

            // If trade is close to acceptable, suggest modifications
            if (evaluation.AdjustedBalance >= -15f)
            {
                result.CounterProposal = GenerateModifiedProposal(originalProposal, counteringTeamId, evaluation);
                result.CounterType = CounterOfferType.Modify;
                result.Message = "We're interested, but would need the following adjustments:";
            }
            // If trade is far off, suggest different deal
            else if (frontOffice.Aggression >= TradeAggression.Moderate)
            {
                result.CounterProposal = GenerateAlternativeProposal(originalProposal, counteringTeamId);
                result.CounterType = CounterOfferType.DifferentDeal;
                result.Message = "That doesn't work for us, but we'd consider this instead:";
            }
            else
            {
                result.CounterType = CounterOfferType.Decline;
                result.Message = evaluation.Reasoning;
            }

            return result;
        }

        private TradeProposal GenerateModifiedProposal(TradeProposal original, string teamId, TradeEvaluationResult eval)
        {
            var modified = CloneProposal(original);

            // Calculate what additional value is needed
            float valueNeeded = eval.AcceptanceThreshold - eval.AdjustedBalance + 2f; // Add buffer

            // Try to add assets to make up the difference
            // This is simplified - in full implementation would check roster/picks
            var otherTeam = original.GetInvolvedTeams().First(t => t != teamId);

            // Request additional draft pick if value gap is significant
            if (valueNeeded >= 10f)
            {
                modified.AllAssets.Add(new TradeAsset
                {
                    Type = TradeAssetType.DraftPick,
                    Year = DateTime.Now.Year + 1,
                    IsFirstRound = valueNeeded >= 20f,
                    OriginalTeamId = otherTeam,
                    SendingTeamId = otherTeam,
                    ReceivingTeamId = teamId
                });
            }

            return modified;
        }

        private TradeProposal GenerateAlternativeProposal(TradeProposal original, string teamId)
        {
            // Simplified - return modified version
            return GenerateModifiedProposal(original, teamId,
                EvaluateTrade(original, teamId));
        }

        private TradeProposal CloneProposal(TradeProposal original)
        {
            var clone = new TradeProposal
            {
                IsSignAndTrade = original.IsSignAndTrade,
                ProposedDate = original.ProposedDate,
                AllAssets = new List<TradeAsset>(original.AllAssets)
            };
            return clone;
        }
    }

    // === Result Classes ===

    /// <summary>
    /// Complete evaluation of a trade from one team's perspective
    /// </summary>
    public class TradeEvaluationResult
    {
        public string TeamId;
        public TradeProposal Proposal;

        public List<AssetEvaluation> AssetsReceiving = new List<AssetEvaluation>();
        public List<AssetEvaluation> AssetsGiving = new List<AssetEvaluation>();

        public float RawTradeBalance;        // Pure value comparison
        public float AdjustedBalance;        // After all modifiers
        public float AcceptanceThreshold;    // What balance needed to accept

        public bool IsAcceptable;
        public string Reasoning;

        public float TotalValueReceiving => AssetsReceiving.Sum(a => a.Value);
        public float TotalValueGiving => AssetsGiving.Sum(a => a.Value);
    }

    /// <summary>
    /// Evaluation of a single asset
    /// </summary>
    public class AssetEvaluation
    {
        public TradeAsset Asset;
        public string AssetDescription;
        public float Value;
        public string ValueBreakdown;
    }

    /// <summary>
    /// Result of counter-offer generation
    /// </summary>
    public class CounterOfferResult
    {
        public TradeProposal OriginalProposal;
        public TradeProposal CounterProposal;
        public CounterOfferType CounterType;
        public string Message;
    }

    /// <summary>
    /// Type of counter-offer
    /// </summary>
    public enum CounterOfferType
    {
        Decline,            // Not interested
        Modify,             // Same players, different terms
        DifferentDeal,      // Different players entirely
        MultiTeam           // Suggest expanding to more teams
    }
}
