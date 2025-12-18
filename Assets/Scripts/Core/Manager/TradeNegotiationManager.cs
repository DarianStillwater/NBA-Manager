using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Status of a trade negotiation
    /// </summary>
    public enum TradeNegotiationStatus
    {
        Initiated,           // Just started
        AwaitingResponse,    // Waiting for other team
        CounterReceived,     // Other team countered
        InDiscussion,        // Back and forth
        Accepted,            // Deal agreed
        Rejected,            // Other team said no
        Withdrawn,           // You pulled out
        Expired,             // Took too long
        LeakedToMedia        // News got out
    }

    /// <summary>
    /// A trade negotiation session between teams
    /// </summary>
    [Serializable]
    public class TradeNegotiation
    {
        public string NegotiationId;
        public string InitiatingTeamId;
        public List<string> InvolvedTeamIds;

        public TradeNegotiationStatus Status;
        public List<TradeNegotiationRound> Rounds;

        public TradeProposal CurrentProposal;
        public TradeProposal LastCounterOffer;

        public DateTime StartedAt;
        public DateTime LastActivity;
        public DateTime ExpiresAt;

        public bool IsLeaked;
        public string LeakHeadline;

        public int TotalRounds => Rounds?.Count ?? 0;

        /// <summary>
        /// Check if negotiation is still active
        /// </summary>
        public bool IsActive => Status == TradeNegotiationStatus.Initiated ||
                               Status == TradeNegotiationStatus.AwaitingResponse ||
                               Status == TradeNegotiationStatus.CounterReceived ||
                               Status == TradeNegotiationStatus.InDiscussion;
    }

    /// <summary>
    /// A single round in trade negotiation
    /// </summary>
    [Serializable]
    public class TradeNegotiationRound
    {
        public int RoundNumber;
        public string ActingTeamId;
        public TradeNegotiationAction Action;
        public TradeProposal Proposal;
        public string Message;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Action taken in negotiation
    /// </summary>
    public enum TradeNegotiationAction
    {
        InitialOffer,
        Counter,
        Accept,
        Reject,
        Withdraw,
        ModifyTerms,
        AddTeam,             // Suggest expanding to multi-team
        RequestAsset,        // Ask for specific addition
        RemoveAsset          // Offer to remove something
    }

    /// <summary>
    /// Media leak about trade talks
    /// </summary>
    [Serializable]
    public class TradeLeak
    {
        public string LeakId;
        public string NegotiationId;
        public DateTime LeakedAt;
        public string Headline;
        public string Details;
        public string Source;                // "League sources", reporter name, etc.
        public LeakAccuracy Accuracy;        // How accurate is the leak
        public List<string> TeamsRevealed;
        public List<string> PlayersRevealed;
    }

    /// <summary>
    /// How accurate the leaked information is
    /// </summary>
    public enum LeakAccuracy
    {
        Accurate,            // Real trade talks
        PartiallyAccurate,   // Some details wrong
        Misleading,          // Mostly wrong
        Fabricated           // No actual talks (agent/team planting stories)
    }

    /// <summary>
    /// Manages the trade negotiation process including counter-offers
    /// </summary>
    public class TradeNegotiationManager
    {
        private Dictionary<string, TradeNegotiation> _activeNegotiations;
        private Dictionary<string, List<TradeNegotiation>> _negotiationHistory;
        private List<TradeLeak> _recentLeaks;

        private AITradeEvaluator _tradeEvaluator;
        private TradeSystem _tradeSystem;
        private TradeFinder _tradeFinder;

        public event Action<TradeNegotiation> OnNegotiationStarted;
        public event Action<TradeNegotiation> OnNegotiationUpdated;
        public event Action<TradeNegotiation> OnNegotiationCompleted;
        public event Action<TradeLeak> OnTradeLeak;
        public event Action<TradeNegotiation, TradeProposal> OnCounterOfferReceived;

        public TradeNegotiationManager(
            AITradeEvaluator evaluator,
            TradeSystem tradeSystem,
            TradeFinder tradeFinder)
        {
            _tradeEvaluator = evaluator;
            _tradeSystem = tradeSystem;
            _tradeFinder = tradeFinder;

            _activeNegotiations = new Dictionary<string, TradeNegotiation>();
            _negotiationHistory = new Dictionary<string, List<TradeNegotiation>>();
            _recentLeaks = new List<TradeLeak>();
        }

        // === Starting Negotiations ===

        /// <summary>
        /// Initiate a trade discussion with another team
        /// </summary>
        public TradeNegotiation InitiateNegotiation(
            string initiatingTeamId,
            TradeProposal proposal,
            string openingMessage = null)
        {
            var negotiation = new TradeNegotiation
            {
                NegotiationId = Guid.NewGuid().ToString(),
                InitiatingTeamId = initiatingTeamId,
                InvolvedTeamIds = proposal.GetInvolvedTeams().ToList(),
                Status = TradeNegotiationStatus.Initiated,
                Rounds = new List<TradeNegotiationRound>(),
                CurrentProposal = proposal,
                StartedAt = DateTime.Now,
                LastActivity = DateTime.Now,
                ExpiresAt = DateTime.Now.AddDays(7),
                IsLeaked = false
            };

            // Record initial offer
            negotiation.Rounds.Add(new TradeNegotiationRound
            {
                RoundNumber = 1,
                ActingTeamId = initiatingTeamId,
                Action = TradeNegotiationAction.InitialOffer,
                Proposal = proposal,
                Message = openingMessage ?? "We'd like to discuss a trade.",
                Timestamp = DateTime.Now
            });

            _activeNegotiations[negotiation.NegotiationId] = negotiation;

            OnNegotiationStarted?.Invoke(negotiation);

            // Process AI response
            ProcessAIResponse(negotiation);

            return negotiation;
        }

        /// <summary>
        /// Process AI team's response to a trade offer
        /// </summary>
        private void ProcessAIResponse(TradeNegotiation negotiation)
        {
            foreach (var teamId in negotiation.InvolvedTeamIds)
            {
                if (teamId == negotiation.InitiatingTeamId) continue;

                var frontOffice = _tradeEvaluator.GetFrontOffice(teamId);
                if (frontOffice == null)
                {
                    frontOffice = FrontOfficeProfile.CreateAverage(teamId, "AI GM");
                }

                // Evaluate the trade
                var evaluation = _tradeEvaluator.EvaluateTrade(negotiation.CurrentProposal, teamId);

                // Check for leaks
                CheckForLeak(negotiation, frontOffice);

                if (evaluation.IsAcceptable)
                {
                    // Accept the trade
                    AcceptNegotiation(negotiation.NegotiationId, teamId, "We have a deal.");
                }
                else if (evaluation.AdjustedBalance >= -20f)
                {
                    // Close enough to counter
                    var counterResult = _tradeEvaluator.GenerateCounterOffer(
                        negotiation.CurrentProposal, teamId);

                    if (counterResult?.CounterType == CounterOfferType.Modify ||
                        counterResult?.CounterType == CounterOfferType.DifferentDeal)
                    {
                        SubmitCounterOffer(negotiation.NegotiationId, teamId,
                            counterResult.CounterProposal, counterResult.Message);
                    }
                    else
                    {
                        RejectNegotiation(negotiation.NegotiationId, teamId, evaluation.Reasoning);
                    }
                }
                else
                {
                    // Too far apart - reject
                    RejectNegotiation(negotiation.NegotiationId, teamId, evaluation.Reasoning);
                }
            }
        }

        // === Counter-Offers ===

        /// <summary>
        /// Submit a counter-offer in an existing negotiation
        /// </summary>
        public void SubmitCounterOffer(
            string negotiationId,
            string teamId,
            TradeProposal counterProposal,
            string message = null)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return;

            if (!negotiation.IsActive)
                return;

            negotiation.LastCounterOffer = negotiation.CurrentProposal;
            negotiation.CurrentProposal = counterProposal;
            negotiation.Status = TradeNegotiationStatus.CounterReceived;
            negotiation.LastActivity = DateTime.Now;

            negotiation.Rounds.Add(new TradeNegotiationRound
            {
                RoundNumber = negotiation.TotalRounds + 1,
                ActingTeamId = teamId,
                Action = TradeNegotiationAction.Counter,
                Proposal = counterProposal,
                Message = message ?? GenerateCounterMessage(negotiation, teamId),
                Timestamp = DateTime.Now
            });

            OnNegotiationUpdated?.Invoke(negotiation);
            OnCounterOfferReceived?.Invoke(negotiation, counterProposal);

            Debug.Log($"[TradeNegotiation] {teamId} submitted counter-offer in round {negotiation.TotalRounds}");
        }

        /// <summary>
        /// Respond to a counter-offer (user action)
        /// </summary>
        public void RespondToCounter(
            string negotiationId,
            string userTeamId,
            TradeNegotiationAction action,
            TradeProposal modifiedProposal = null,
            string message = null)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return;

            switch (action)
            {
                case TradeNegotiationAction.Accept:
                    AcceptNegotiation(negotiationId, userTeamId, message ?? "Deal!");
                    break;

                case TradeNegotiationAction.Counter:
                    if (modifiedProposal != null)
                    {
                        SubmitCounterOffer(negotiationId, userTeamId, modifiedProposal, message);
                        // Process AI response to user's counter
                        ProcessAIResponse(negotiation);
                    }
                    break;

                case TradeNegotiationAction.Reject:
                    RejectNegotiation(negotiationId, userTeamId, message ?? "No deal.");
                    break;

                case TradeNegotiationAction.Withdraw:
                    WithdrawNegotiation(negotiationId, userTeamId);
                    break;

                case TradeNegotiationAction.AddTeam:
                    // Suggest multi-team expansion
                    negotiation.Rounds.Add(new TradeNegotiationRound
                    {
                        RoundNumber = negotiation.TotalRounds + 1,
                        ActingTeamId = userTeamId,
                        Action = TradeNegotiationAction.AddTeam,
                        Message = message ?? "What if we brought in a third team?",
                        Timestamp = DateTime.Now
                    });
                    negotiation.Status = TradeNegotiationStatus.InDiscussion;
                    OnNegotiationUpdated?.Invoke(negotiation);
                    break;
            }
        }

        /// <summary>
        /// Request a specific asset be added to the deal
        /// </summary>
        public void RequestAsset(
            string negotiationId,
            string requestingTeamId,
            string assetDescription,
            string message = null)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return;

            negotiation.Rounds.Add(new TradeNegotiationRound
            {
                RoundNumber = negotiation.TotalRounds + 1,
                ActingTeamId = requestingTeamId,
                Action = TradeNegotiationAction.RequestAsset,
                Message = message ?? $"We'd need you to include {assetDescription} to make this work.",
                Timestamp = DateTime.Now
            });

            negotiation.Status = TradeNegotiationStatus.InDiscussion;
            negotiation.LastActivity = DateTime.Now;

            OnNegotiationUpdated?.Invoke(negotiation);
        }

        // === Completing Negotiations ===

        /// <summary>
        /// Accept the current proposal
        /// </summary>
        public void AcceptNegotiation(string negotiationId, string acceptingTeamId, string message = null)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return;

            negotiation.Rounds.Add(new TradeNegotiationRound
            {
                RoundNumber = negotiation.TotalRounds + 1,
                ActingTeamId = acceptingTeamId,
                Action = TradeNegotiationAction.Accept,
                Proposal = negotiation.CurrentProposal,
                Message = message ?? "We have a deal!",
                Timestamp = DateTime.Now
            });

            negotiation.Status = TradeNegotiationStatus.Accepted;
            negotiation.LastActivity = DateTime.Now;

            // Execute the trade
            var tradeResult = _tradeSystem.ProposeTrade(negotiation.CurrentProposal, executeIfValid: true);

            FinalizeNegotiation(negotiation);

            Debug.Log($"[TradeNegotiation] Trade accepted after {negotiation.TotalRounds} rounds");
        }

        /// <summary>
        /// Reject the negotiation
        /// </summary>
        public void RejectNegotiation(string negotiationId, string rejectingTeamId, string reason = null)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return;

            negotiation.Rounds.Add(new TradeNegotiationRound
            {
                RoundNumber = negotiation.TotalRounds + 1,
                ActingTeamId = rejectingTeamId,
                Action = TradeNegotiationAction.Reject,
                Message = reason ?? "We're not interested in this deal.",
                Timestamp = DateTime.Now
            });

            negotiation.Status = TradeNegotiationStatus.Rejected;
            negotiation.LastActivity = DateTime.Now;

            FinalizeNegotiation(negotiation);

            Debug.Log($"[TradeNegotiation] Trade rejected by {rejectingTeamId}");
        }

        /// <summary>
        /// Withdraw from negotiation
        /// </summary>
        public void WithdrawNegotiation(string negotiationId, string withdrawingTeamId)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return;

            negotiation.Rounds.Add(new TradeNegotiationRound
            {
                RoundNumber = negotiation.TotalRounds + 1,
                ActingTeamId = withdrawingTeamId,
                Action = TradeNegotiationAction.Withdraw,
                Message = "We've decided to pull out of these discussions.",
                Timestamp = DateTime.Now
            });

            negotiation.Status = TradeNegotiationStatus.Withdrawn;
            negotiation.LastActivity = DateTime.Now;

            FinalizeNegotiation(negotiation);
        }

        /// <summary>
        /// Move negotiation to history
        /// </summary>
        private void FinalizeNegotiation(TradeNegotiation negotiation)
        {
            _activeNegotiations.Remove(negotiation.NegotiationId);

            foreach (var teamId in negotiation.InvolvedTeamIds)
            {
                if (!_negotiationHistory.ContainsKey(teamId))
                {
                    _negotiationHistory[teamId] = new List<TradeNegotiation>();
                }
                _negotiationHistory[teamId].Add(negotiation);
            }

            OnNegotiationCompleted?.Invoke(negotiation);
        }

        // === Leaks ===

        /// <summary>
        /// Check if trade talks get leaked to media
        /// </summary>
        private void CheckForLeak(TradeNegotiation negotiation, FrontOfficeProfile frontOffice)
        {
            if (negotiation.IsLeaked) return;

            // Determine if this involves star players
            bool involvesStarPlayer = negotiation.CurrentProposal.AllAssets
                .Any(a => a.Type == TradeAssetType.Player && a.Salary >= 25_000_000);

            // Check deadline proximity
            bool nearDeadline = IsNearDeadline();

            // Check for leak
            if (frontOffice.WouldLeakTrade(involvesStarPlayer, nearDeadline))
            {
                GenerateLeak(negotiation, frontOffice, involvesStarPlayer);
            }
        }

        /// <summary>
        /// Generate a media leak about trade talks
        /// </summary>
        private void GenerateLeak(
            TradeNegotiation negotiation,
            FrontOfficeProfile leakingFO,
            bool involvesStarPlayer)
        {
            negotiation.IsLeaked = true;
            negotiation.Status = TradeNegotiationStatus.LeakedToMedia;

            var leak = new TradeLeak
            {
                LeakId = Guid.NewGuid().ToString(),
                NegotiationId = negotiation.NegotiationId,
                LeakedAt = DateTime.Now,
                TeamsRevealed = new List<string>(),
                PlayersRevealed = new List<string>()
            };

            // Determine what gets revealed
            if (UnityEngine.Random.value < 0.7f)
            {
                leak.TeamsRevealed.AddRange(negotiation.InvolvedTeamIds);
            }
            else
            {
                // Only partial info
                leak.TeamsRevealed.Add(negotiation.InvolvedTeamIds[0]);
            }

            // Star players more likely to be named
            if (involvesStarPlayer && UnityEngine.Random.value < 0.8f)
            {
                var starPlayer = negotiation.CurrentProposal.AllAssets
                    .FirstOrDefault(a => a.Type == TradeAssetType.Player && a.Salary >= 25_000_000);
                if (starPlayer != null)
                {
                    leak.PlayersRevealed.Add(starPlayer.PlayerId);
                }
            }

            // Generate headline
            leak.Headline = GenerateLeakHeadline(leak, negotiation);
            leak.Details = GenerateLeakDetails(leak, negotiation);
            leak.Source = leakingFO.LeakBehavior.LeaksToFavoredReporters && leakingFO.LeakBehavior.FavoredReporters.Count > 0
                ? leakingFO.LeakBehavior.FavoredReporters[0]
                : "League sources";

            leak.Accuracy = UnityEngine.Random.value < 0.7f
                ? LeakAccuracy.Accurate
                : LeakAccuracy.PartiallyAccurate;

            negotiation.LeakHeadline = leak.Headline;
            _recentLeaks.Add(leak);

            OnTradeLeak?.Invoke(leak);

            Debug.Log($"[TradeNegotiation] LEAK: {leak.Headline}");
        }

        private string GenerateLeakHeadline(TradeLeak leak, TradeNegotiation negotiation)
        {
            if (leak.PlayersRevealed.Count > 0)
            {
                return $"REPORT: {leak.PlayersRevealed[0]} trade discussions heating up";
            }

            if (leak.TeamsRevealed.Count >= 2)
            {
                return $"REPORT: {leak.TeamsRevealed[0]} and {leak.TeamsRevealed[1]} in trade talks";
            }

            return $"REPORT: {leak.TeamsRevealed[0]} exploring trade options";
        }

        private string GenerateLeakDetails(TradeLeak leak, TradeNegotiation negotiation)
        {
            var details = new List<string>();

            details.Add($"According to {leak.Source}, ");

            if (leak.PlayersRevealed.Count > 0)
            {
                details.Add($"{leak.PlayersRevealed[0]} is a central piece in ongoing trade discussions.");
            }
            else
            {
                details.Add("multiple teams are engaged in trade discussions.");
            }

            if (leak.TeamsRevealed.Count >= 2)
            {
                details.Add($"The {leak.TeamsRevealed[0]} and {leak.TeamsRevealed[1]} have been in communication.");
            }

            return string.Join(" ", details);
        }

        // === Multi-Team Trades ===

        /// <summary>
        /// Suggest expanding to a multi-team trade
        /// </summary>
        public TradeProposal SuggestThirdTeam(
            string negotiationId,
            string suggestingTeamId,
            string thirdTeamId)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return null;

            // Get third team's interest
            var thirdTeamFO = _tradeEvaluator.GetFrontOffice(thirdTeamId);
            if (thirdTeamFO == null) return null;

            // Create expanded proposal
            var expandedProposal = CloneAndExpandProposal(
                negotiation.CurrentProposal, thirdTeamId);

            // Check if third team would be interested
            var evaluation = _tradeEvaluator.EvaluateTrade(expandedProposal, thirdTeamId);

            if (evaluation.IsAcceptable || evaluation.AdjustedBalance >= -10f)
            {
                negotiation.InvolvedTeamIds.Add(thirdTeamId);

                negotiation.Rounds.Add(new TradeNegotiationRound
                {
                    RoundNumber = negotiation.TotalRounds + 1,
                    ActingTeamId = suggestingTeamId,
                    Action = TradeNegotiationAction.AddTeam,
                    Proposal = expandedProposal,
                    Message = $"We've brought {thirdTeamId} into discussions to make this work.",
                    Timestamp = DateTime.Now
                });

                negotiation.CurrentProposal = expandedProposal;
                OnNegotiationUpdated?.Invoke(negotiation);

                return expandedProposal;
            }

            return null;
        }

        private TradeProposal CloneAndExpandProposal(TradeProposal original, string newTeamId)
        {
            // Simplified - in full implementation would properly structure 3-way trade
            var expanded = new TradeProposal
            {
                IsSignAndTrade = original.IsSignAndTrade,
                ProposedDate = DateTime.Now,
                AllAssets = new List<TradeAsset>(original.AllAssets)
            };

            return expanded;
        }

        // === Queries ===

        /// <summary>
        /// Get active negotiations for a team
        /// </summary>
        public List<TradeNegotiation> GetActiveNegotiations(string teamId)
        {
            return _activeNegotiations.Values
                .Where(n => n.InvolvedTeamIds.Contains(teamId) && n.IsActive)
                .OrderByDescending(n => n.LastActivity)
                .ToList();
        }

        /// <summary>
        /// Get negotiation by ID
        /// </summary>
        public TradeNegotiation GetNegotiation(string negotiationId)
        {
            _activeNegotiations.TryGetValue(negotiationId, out var negotiation);
            return negotiation;
        }

        /// <summary>
        /// Get negotiations awaiting user response
        /// </summary>
        public List<TradeNegotiation> GetPendingCounterOffers(string userTeamId)
        {
            return _activeNegotiations.Values
                .Where(n => n.InvolvedTeamIds.Contains(userTeamId) &&
                           n.Status == TradeNegotiationStatus.CounterReceived &&
                           n.Rounds.Last().ActingTeamId != userTeamId)
                .ToList();
        }

        /// <summary>
        /// Get recent leaks
        /// </summary>
        public List<TradeLeak> GetRecentLeaks(int maxDays = 7)
        {
            return _recentLeaks
                .Where(l => (DateTime.Now - l.LeakedAt).TotalDays <= maxDays)
                .OrderByDescending(l => l.LeakedAt)
                .ToList();
        }

        /// <summary>
        /// Get negotiation history for a team
        /// </summary>
        public List<TradeNegotiation> GetNegotiationHistory(string teamId)
        {
            if (_negotiationHistory.TryGetValue(teamId, out var history))
            {
                return history.OrderByDescending(n => n.StartedAt).ToList();
            }
            return new List<TradeNegotiation>();
        }

        // === Helpers ===

        private bool IsNearDeadline()
        {
            var now = DateTime.Now;
            return now.Month == 2 && now.Day <= 15;
        }

        private string GenerateCounterMessage(TradeNegotiation negotiation, string teamId)
        {
            var frontOffice = _tradeEvaluator.GetFrontOffice(teamId);
            if (frontOffice == null) return "We have a counter-proposal.";

            return frontOffice.Aggression switch
            {
                TradeAggression.Aggressive => "Here's what we need to make this happen.",
                TradeAggression.VeryPassive => "We appreciate the interest. This is closer to what we'd need.",
                _ => "We're interested, but would need some adjustments."
            };
        }

        /// <summary>
        /// Process expired negotiations
        /// </summary>
        public void ProcessExpirations()
        {
            var expired = _activeNegotiations.Values
                .Where(n => DateTime.Now > n.ExpiresAt)
                .ToList();

            foreach (var negotiation in expired)
            {
                negotiation.Status = TradeNegotiationStatus.Expired;
                FinalizeNegotiation(negotiation);

                Debug.Log($"[TradeNegotiation] Negotiation expired: {negotiation.NegotiationId}");
            }
        }
    }
}
