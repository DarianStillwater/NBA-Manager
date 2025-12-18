using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Interest level from AI team in a trade/player
    /// </summary>
    public enum AIInterestLevel
    {
        None,           // Not interested at all
        Minimal,        // Would consider if price is right
        Moderate,       // Actively interested
        High,           // Very interested, might reach out
        Desperate       // Will overpay
    }

    /// <summary>
    /// A potential trade suggestion
    /// </summary>
    [Serializable]
    public class TradeSuggestion
    {
        public string SuggestionId;
        public TradeProposal Proposal;
        public string Description;

        public string YourTeamId;
        public string PartnerTeamId;

        public AIInterestLevel PartnerInterest;
        public float EstimatedFairness;      // 0 = bad for you, 1 = fair, 2 = great for you
        public string WhyItWorks;

        public List<string> PlayersYouGive;
        public List<string> PlayersYouGet;
        public List<string> PicksYouGive;
        public List<string> PicksYouGet;

        public DateTime GeneratedAt;
        public bool HasBeenViewed;
    }

    /// <summary>
    /// Interest from AI team in one of your players
    /// </summary>
    [Serializable]
    public class PlayerInterest
    {
        public string InterestedTeamId;
        public string InterestedTeamName;
        public string PlayerId;
        public string PlayerName;

        public AIInterestLevel InterestLevel;
        public string WhyInterested;

        public List<string> PotentialReturns;  // What they might offer
        public bool HasReachedOut;              // Have they contacted you?

        public DateTime FirstShownInterest;
    }

    /// <summary>
    /// Manages trade finding, suggestions, and AI interest tracking
    /// </summary>
    public class TradeFinder
    {
        private AITradeEvaluator _tradeEvaluator;
        private SalaryCapManager _capManager;
        private PlayerDatabase _playerDatabase;

        private Dictionary<string, PlayerTradeStatus> _tradeBlock;           // Players on trade block
        private Dictionary<string, List<PlayerInterest>> _playerInterest;    // Interest in your players
        private List<TradeSuggestion> _activeSuggestions;

        public event Action<PlayerInterest> OnNewInterest;
        public event Action<TradeSuggestion> OnNewSuggestion;

        public TradeFinder(AITradeEvaluator evaluator, SalaryCapManager capManager, PlayerDatabase playerDatabase)
        {
            _tradeEvaluator = evaluator;
            _capManager = capManager;
            _playerDatabase = playerDatabase;

            _tradeBlock = new Dictionary<string, PlayerTradeStatus>();
            _playerInterest = new Dictionary<string, List<PlayerInterest>>();
            _activeSuggestions = new List<TradeSuggestion>();
        }

        // === Trade Block Management ===

        /// <summary>
        /// Put a player on the trade block
        /// </summary>
        public void PutOnTradeBlock(string playerId, string playerName, string teamId, string reason = null)
        {
            var status = new PlayerTradeStatus
            {
                PlayerId = playerId,
                PlayerName = playerName,
                TeamId = teamId,
                Availability = TradeAvailability.OnTheBlock,
                ReasonForAvailability = reason ?? "Team is shopping player",
                IsPubliclyKnown = false,
                AvailableSince = DateTime.Now,
                PreferredDestinations = new List<string>(),
                BlockedDestinations = new List<string>()
            };

            _tradeBlock[playerId] = status;
            _tradeEvaluator.SetPlayerTradeStatus(status);

            Debug.Log($"[TradeFinder] {playerName} placed on trade block");

            // Generate interest from AI teams
            GenerateAIInterest(status);
        }

        /// <summary>
        /// Remove player from trade block
        /// </summary>
        public void RemoveFromTradeBlock(string playerId)
        {
            if (_tradeBlock.Remove(playerId))
            {
                _playerInterest.Remove(playerId);
                Debug.Log($"[TradeFinder] Player {playerId} removed from trade block");
            }
        }

        /// <summary>
        /// Mark a player as available but not actively shopped
        /// </summary>
        public void MarkAsAvailable(string playerId, string playerName, string teamId)
        {
            var status = new PlayerTradeStatus
            {
                PlayerId = playerId,
                PlayerName = playerName,
                TeamId = teamId,
                Availability = TradeAvailability.Available,
                ReasonForAvailability = "Listening to offers",
                IsPubliclyKnown = false,
                AvailableSince = DateTime.Now,
                PreferredDestinations = new List<string>(),
                BlockedDestinations = new List<string>()
            };

            _tradeBlock[playerId] = status;
            _tradeEvaluator.SetPlayerTradeStatus(status);
        }

        /// <summary>
        /// Get all players on trade block
        /// </summary>
        public List<PlayerTradeStatus> GetTradeBlock()
        {
            return _tradeBlock.Values
                .Where(p => p.Availability >= TradeAvailability.Available)
                .OrderByDescending(p => p.Availability)
                .ToList();
        }

        /// <summary>
        /// Get players that AI teams are shopping
        /// </summary>
        public List<PlayerTradeStatus> GetLeagueWideAvailable()
        {
            // In full implementation, this would query all AI team trade blocks
            // For now, return players we know about
            return _tradeBlock.Values
                .Where(p => p.IsPubliclyKnown)
                .ToList();
        }

        // === AI Interest Generation ===

        /// <summary>
        /// Generate interest from AI teams in a player
        /// </summary>
        private void GenerateAIInterest(PlayerTradeStatus playerStatus)
        {
            var player = _playerDatabase?.GetPlayer(playerStatus.PlayerId);
            var contract = _capManager?.GetContract(playerStatus.PlayerId);

            if (player == null) return;

            var interests = new List<PlayerInterest>();

            // Check each AI team for potential interest
            foreach (var teamId in GetAITeamIds())
            {
                if (teamId == playerStatus.TeamId) continue; // Skip own team

                var frontOffice = _tradeEvaluator.GetFrontOffice(teamId);
                if (frontOffice == null) continue;

                var interest = EvaluateTeamInterest(player, contract, frontOffice, playerStatus);

                if (interest.InterestLevel > AIInterestLevel.None)
                {
                    interests.Add(interest);
                    OnNewInterest?.Invoke(interest);
                }
            }

            _playerInterest[playerStatus.PlayerId] = interests;

            Debug.Log($"[TradeFinder] Generated {interests.Count} interested teams for {playerStatus.PlayerName}");
        }

        /// <summary>
        /// Evaluate how interested a specific team would be in a player
        /// </summary>
        private PlayerInterest EvaluateTeamInterest(
            Player player,
            Contract contract,
            FrontOfficeProfile frontOffice,
            PlayerTradeStatus status)
        {
            var interest = new PlayerInterest
            {
                InterestedTeamId = frontOffice.TeamId,
                InterestedTeamName = frontOffice.TeamId, // Would be actual team name
                PlayerId = player.PlayerId,
                PlayerName = status.PlayerName,
                InterestLevel = AIInterestLevel.None,
                FirstShownInterest = DateTime.Now,
                PotentialReturns = new List<string>()
            };

            float interestScore = 0f;

            // Base interest from player quality (salary proxy)
            if (contract != null)
            {
                if (contract.CurrentYearSalary >= 25_000_000)
                    interestScore += 30f;
                else if (contract.CurrentYearSalary >= 15_000_000)
                    interestScore += 20f;
                else if (contract.CurrentYearSalary >= 8_000_000)
                    interestScore += 10f;
            }

            // Team situation fit
            switch (frontOffice.CurrentSituation)
            {
                case TeamSituation.Championship:
                case TeamSituation.Contending:
                    // Want veterans and proven players
                    if (player.Age >= 26 && player.Age <= 32)
                        interestScore += 20f;
                    interest.WhyInterested = "Looking to add a proven player for playoff push";
                    break;

                case TeamSituation.Rebuilding:
                    // Want young players and picks
                    if (player.Age <= 24)
                        interestScore += 25f;
                    else if (player.Age <= 26)
                        interestScore += 10f;
                    else
                        interestScore -= 10f; // Less interest in veterans
                    interest.WhyInterested = "Interested in young talent for rebuild";
                    break;

                case TeamSituation.PlayoffBubble:
                case TeamSituation.StuckInMiddle:
                    // Moderate interest in most players
                    interestScore += 10f;
                    interest.WhyInterested = "Looking to shake up roster";
                    break;
            }

            // Contract value consideration
            if (contract != null)
            {
                if (contract.YearsRemaining == 1)
                {
                    // Expiring contracts valuable for cap flexibility
                    if (frontOffice.ValuePreferences.ValuesSalaryRelief > 0.5f)
                    {
                        interestScore += 15f;
                        interest.WhyInterested = "Expiring contract provides flexibility";
                    }
                }

                // Check if team can afford salary
                var capSpace = _capManager?.GetCapSpace(frontOffice.TeamId) ?? 0;
                if (contract.CurrentYearSalary > capSpace + 5_000_000)
                {
                    // Would need to send salary back
                    interestScore -= 10f;
                }
            }

            // FO aggression affects willingness to pursue
            interestScore += frontOffice.Aggression switch
            {
                TradeAggression.Aggressive => 10f,
                TradeAggression.Desperate => 20f,
                TradeAggression.VeryPassive => -15f,
                TradeAggression.Conservative => -5f,
                _ => 0f
            };

            // Random variance
            interestScore += UnityEngine.Random.Range(-10f, 10f);

            // Convert score to interest level
            interest.InterestLevel = interestScore switch
            {
                >= 50f => AIInterestLevel.Desperate,
                >= 40f => AIInterestLevel.High,
                >= 25f => AIInterestLevel.Moderate,
                >= 15f => AIInterestLevel.Minimal,
                _ => AIInterestLevel.None
            };

            // Generate potential return assets
            if (interest.InterestLevel >= AIInterestLevel.Minimal)
            {
                interest.PotentialReturns = GeneratePotentialReturns(frontOffice, contract?.CurrentYearSalary ?? 0);
            }

            return interest;
        }

        /// <summary>
        /// Generate what a team might offer in return
        /// </summary>
        private List<string> GeneratePotentialReturns(FrontOfficeProfile frontOffice, long targetSalary)
        {
            var returns = new List<string>();

            // Based on what team typically offers
            if (frontOffice.WillingToIncludePicks)
            {
                returns.Add("Draft pick(s)");
            }

            if (targetSalary >= 15_000_000)
            {
                returns.Add("Salary matching player(s)");
            }

            if (frontOffice.CurrentSituation == TeamSituation.Rebuilding)
            {
                returns.Add("Young player(s)");
            }
            else
            {
                returns.Add("Role player(s)");
            }

            return returns;
        }

        // === Trade Suggestions ===

        /// <summary>
        /// Generate trade suggestions for a team
        /// </summary>
        public List<TradeSuggestion> GenerateTradeSuggestions(string userTeamId, int maxSuggestions = 5)
        {
            var suggestions = new List<TradeSuggestion>();

            // Get players with interest
            var playersWithInterest = _playerInterest
                .Where(kvp => _tradeBlock.ContainsKey(kvp.Key) &&
                             _tradeBlock[kvp.Key].TeamId == userTeamId &&
                             kvp.Value.Any(i => i.InterestLevel >= AIInterestLevel.Moderate))
                .ToList();

            foreach (var (playerId, interests) in playersWithInterest)
            {
                var status = _tradeBlock[playerId];

                // Generate suggestion for each highly interested team
                foreach (var interest in interests.Where(i => i.InterestLevel >= AIInterestLevel.Moderate).Take(2))
                {
                    var suggestion = GenerateSuggestion(status, interest, userTeamId);
                    if (suggestion != null)
                    {
                        suggestions.Add(suggestion);
                    }
                }
            }

            // Also generate some unsolicited suggestions based on team needs
            suggestions.AddRange(GenerateNeedBasedSuggestions(userTeamId, maxSuggestions - suggestions.Count));

            _activeSuggestions = suggestions.Take(maxSuggestions).ToList();

            return _activeSuggestions;
        }

        /// <summary>
        /// Generate a specific trade suggestion
        /// </summary>
        private TradeSuggestion GenerateSuggestion(
            PlayerTradeStatus playerStatus,
            PlayerInterest interest,
            string userTeamId)
        {
            var partnerFO = _tradeEvaluator.GetFrontOffice(interest.InterestedTeamId);
            if (partnerFO == null) return null;

            var playerContract = _capManager?.GetContract(playerStatus.PlayerId);
            if (playerContract == null) return null;

            var suggestion = new TradeSuggestion
            {
                SuggestionId = Guid.NewGuid().ToString(),
                YourTeamId = userTeamId,
                PartnerTeamId = interest.InterestedTeamId,
                PartnerInterest = interest.InterestLevel,
                PlayersYouGive = new List<string> { playerStatus.PlayerName },
                PlayersYouGet = new List<string>(),
                PicksYouGive = new List<string>(),
                PicksYouGet = new List<string>(),
                GeneratedAt = DateTime.Now,
                HasBeenViewed = false
            };

            // Build proposal
            var builder = new TradeProposalBuilder(_capManager);
            builder.AddPlayer(playerStatus.PlayerId, userTeamId, interest.InterestedTeamId);

            // Add return based on partner's tendencies
            if (partnerFO.WillingToIncludePicks)
            {
                // Add a pick
                int pickYear = DateTime.Now.Year + 1;
                bool isFirstRound = interest.InterestLevel >= AIInterestLevel.High;

                builder.AddDraftPick(pickYear, isFirstRound, interest.InterestedTeamId, userTeamId);
                suggestion.PicksYouGet.Add($"{pickYear} {(isFirstRound ? "1st" : "2nd")} Round");
            }

            // May need salary matching
            if (playerContract.CurrentYearSalary >= 10_000_000)
            {
                suggestion.PlayersYouGet.Add("Salary filler (TBD)");
            }

            suggestion.Proposal = builder.Build();

            // Evaluate fairness
            var userEval = _tradeEvaluator.EvaluateTrade(suggestion.Proposal, userTeamId);
            var partnerEval = _tradeEvaluator.EvaluateTrade(suggestion.Proposal, interest.InterestedTeamId);

            suggestion.EstimatedFairness = CalculateFairness(userEval, partnerEval);
            suggestion.WhyItWorks = GenerateWhyItWorks(interest, partnerFO, playerStatus);
            suggestion.Description = $"Trade {playerStatus.PlayerName} to {interest.InterestedTeamId}";

            return suggestion;
        }

        /// <summary>
        /// Generate suggestions based on team needs rather than trade block
        /// </summary>
        private List<TradeSuggestion> GenerateNeedBasedSuggestions(string userTeamId, int count)
        {
            var suggestions = new List<TradeSuggestion>();

            if (count <= 0) return suggestions;

            // Get user's front office situation
            var userFO = _tradeEvaluator.GetFrontOffice(userTeamId);
            if (userFO == null) return suggestions;

            // Find teams that might trade with us based on opposite needs
            foreach (var teamId in GetAITeamIds())
            {
                if (teamId == userTeamId) continue;

                var partnerFO = _tradeEvaluator.GetFrontOffice(teamId);
                if (partnerFO == null) continue;

                // Check for complementary needs
                if (HasComplementaryNeeds(userFO, partnerFO))
                {
                    var suggestion = GenerateNeedBasedSuggestion(userTeamId, teamId, userFO, partnerFO);
                    if (suggestion != null)
                    {
                        suggestions.Add(suggestion);
                        if (suggestions.Count >= count) break;
                    }
                }
            }

            return suggestions;
        }

        private bool HasComplementaryNeeds(FrontOfficeProfile userFO, FrontOfficeProfile partnerFO)
        {
            // Championship team + Rebuilding team = good trade partners
            if (userFO.CurrentSituation == TeamSituation.Championship &&
                partnerFO.CurrentSituation == TeamSituation.Rebuilding)
                return true;

            if (userFO.CurrentSituation == TeamSituation.Rebuilding &&
                partnerFO.CurrentSituation == TeamSituation.Championship)
                return true;

            // One wants salary relief, other can absorb
            if (userFO.ValuePreferences.ValuesSalaryRelief > 0.6f &&
                partnerFO.WillingToTakeOnBadContracts)
                return true;

            return false;
        }

        private TradeSuggestion GenerateNeedBasedSuggestion(
            string userTeamId,
            string partnerTeamId,
            FrontOfficeProfile userFO,
            FrontOfficeProfile partnerFO)
        {
            // Simplified - in full implementation would analyze rosters
            return new TradeSuggestion
            {
                SuggestionId = Guid.NewGuid().ToString(),
                YourTeamId = userTeamId,
                PartnerTeamId = partnerTeamId,
                PartnerInterest = AIInterestLevel.Moderate,
                Description = $"Potential deal with {partnerTeamId}",
                WhyItWorks = "Complementary team needs could make a deal work",
                EstimatedFairness = 1.0f,
                PlayersYouGive = new List<string>(),
                PlayersYouGet = new List<string>(),
                PicksYouGive = new List<string>(),
                PicksYouGet = new List<string>(),
                GeneratedAt = DateTime.Now
            };
        }

        private float CalculateFairness(TradeEvaluationResult userEval, TradeEvaluationResult partnerEval)
        {
            // 0 = bad for user, 1 = fair, 2 = great for user
            float userValue = userEval.AdjustedBalance;
            float partnerValue = partnerEval.AdjustedBalance;

            // If both are positive, it's fair
            if (userValue >= 0 && partnerValue >= 0)
                return 1.0f + (userValue / 50f);

            // If user is negative, bad for them
            if (userValue < 0)
                return 0.5f + (userValue / 20f);

            return 1.0f;
        }

        private string GenerateWhyItWorks(PlayerInterest interest, FrontOfficeProfile partnerFO, PlayerTradeStatus status)
        {
            var reasons = new List<string>();

            reasons.Add($"{interest.InterestedTeamId} has shown {interest.InterestLevel.ToString().ToLower()} interest");

            if (interest.WhyInterested != null)
                reasons.Add(interest.WhyInterested);

            if (status.DaysOnMarket > 14)
                reasons.Add("Player has been available for a while - might accept less");

            return string.Join(". ", reasons);
        }

        // === Interest Queries ===

        /// <summary>
        /// Get teams interested in a specific player
        /// </summary>
        public List<PlayerInterest> GetInterestedTeams(string playerId)
        {
            if (_playerInterest.TryGetValue(playerId, out var interests))
            {
                return interests.OrderByDescending(i => i.InterestLevel).ToList();
            }
            return new List<PlayerInterest>();
        }

        /// <summary>
        /// Get all players that have interested teams
        /// </summary>
        public Dictionary<string, List<PlayerInterest>> GetAllInterest()
        {
            return new Dictionary<string, List<PlayerInterest>>(_playerInterest);
        }

        /// <summary>
        /// Get active trade suggestions
        /// </summary>
        public List<TradeSuggestion> GetActiveSuggestions()
        {
            return _activeSuggestions.Where(s => (DateTime.Now - s.GeneratedAt).TotalDays < 7).ToList();
        }

        /// <summary>
        /// Refresh interest for all players on trade block
        /// </summary>
        public void RefreshAllInterest()
        {
            foreach (var status in _tradeBlock.Values)
            {
                GenerateAIInterest(status);
            }
        }

        // === Helper Methods ===

        private IEnumerable<string> GetAITeamIds()
        {
            // In full implementation, get all team IDs except user team
            // For now, return placeholder team IDs
            return new[] { "LAL", "BOS", "MIA", "GSW", "PHX", "DEN", "MIL", "PHI",
                         "NYK", "BKN", "CHI", "DAL", "ATL", "CLE", "SAC" };
        }

        /// <summary>
        /// Simulate AI teams making trade block moves
        /// </summary>
        public void SimulateAITradeBlockActivity()
        {
            foreach (var teamId in GetAITeamIds())
            {
                var frontOffice = _tradeEvaluator.GetFrontOffice(teamId);
                if (frontOffice == null) continue;

                // Check if team should put players on block
                if (frontOffice.CurrentSituation == TeamSituation.Rebuilding)
                {
                    // Rebuilding teams more likely to shop veterans
                    if (UnityEngine.Random.value < 0.3f)
                    {
                        // Would add a veteran to trade block
                        // Simplified - in full implementation would pick actual player
                    }
                }
            }
        }
    }
}
