using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Status of a negotiation
    /// </summary>
    public enum NegotiationStatus
    {
        NotStarted,
        InProgress,
        PlayerConsidering,    // Waiting for player response
        CounterOfferReceived, // Agent sent counter
        Accepted,
        Rejected,
        WalkedAway,           // Agent/player ended talks
        Expired               // Deadline passed
    }

    /// <summary>
    /// Player priorities that affect contract decisions
    /// </summary>
    [Serializable]
    public class PlayerPriorities
    {
        [Range(0f, 1f)]
        public float Money;        // Highest AAV
        [Range(0f, 1f)]
        public float Winning;      // Contender preference
        [Range(0f, 1f)]
        public float Role;         // Starting, minutes guarantee
        [Range(0f, 1f)]
        public float Location;     // Market size, weather, home state
        [Range(0f, 1f)]
        public float Loyalty;      // Stay with current team

        /// <summary>
        /// Normalize priorities to sum to 1.0
        /// </summary>
        public void Normalize()
        {
            float total = Money + Winning + Role + Location + Loyalty;
            if (total > 0)
            {
                Money /= total;
                Winning /= total;
                Role /= total;
                Location /= total;
                Loyalty /= total;
            }
        }

        /// <summary>
        /// Generate random priorities based on player archetype
        /// </summary>
        public static PlayerPriorities Generate(bool isVeteran, bool isStar, bool isYoung)
        {
            var priorities = new PlayerPriorities();

            if (isStar)
            {
                // Stars care about money and winning
                priorities.Money = UnityEngine.Random.Range(0.25f, 0.40f);
                priorities.Winning = UnityEngine.Random.Range(0.25f, 0.40f);
                priorities.Role = UnityEngine.Random.Range(0.10f, 0.20f);
                priorities.Location = UnityEngine.Random.Range(0.05f, 0.15f);
                priorities.Loyalty = UnityEngine.Random.Range(0.05f, 0.15f);
            }
            else if (isVeteran)
            {
                // Veterans often chase rings
                priorities.Money = UnityEngine.Random.Range(0.10f, 0.25f);
                priorities.Winning = UnityEngine.Random.Range(0.30f, 0.50f);
                priorities.Role = UnityEngine.Random.Range(0.15f, 0.25f);
                priorities.Location = UnityEngine.Random.Range(0.05f, 0.15f);
                priorities.Loyalty = UnityEngine.Random.Range(0.10f, 0.20f);
            }
            else if (isYoung)
            {
                // Young players want opportunity
                priorities.Money = UnityEngine.Random.Range(0.20f, 0.35f);
                priorities.Winning = UnityEngine.Random.Range(0.10f, 0.20f);
                priorities.Role = UnityEngine.Random.Range(0.25f, 0.40f);
                priorities.Location = UnityEngine.Random.Range(0.05f, 0.15f);
                priorities.Loyalty = UnityEngine.Random.Range(0.05f, 0.15f);
            }
            else
            {
                // Average/role players
                priorities.Money = UnityEngine.Random.Range(0.25f, 0.40f);
                priorities.Winning = UnityEngine.Random.Range(0.15f, 0.25f);
                priorities.Role = UnityEngine.Random.Range(0.15f, 0.25f);
                priorities.Location = UnityEngine.Random.Range(0.10f, 0.20f);
                priorities.Loyalty = UnityEngine.Random.Range(0.05f, 0.15f);
            }

            priorities.Normalize();
            return priorities;
        }
    }

    /// <summary>
    /// Represents a contract offer in negotiations
    /// </summary>
    [Serializable]
    public class ContractOffer
    {
        public string OfferId;
        public string OfferingTeamId;
        public string PlayerId;

        [Header("Core Terms")]
        public int Years;
        public long AnnualSalary;       // Per year
        public long TotalValue;         // Years * Salary (before adjustments)

        [Header("Options")]
        public bool HasPlayerOption;     // Player can opt out
        public int PlayerOptionYear;     // Which year (0 = none)
        public bool HasTeamOption;       // Team can opt out
        public int TeamOptionYear;

        [Header("Additional Terms")]
        public bool HasNoTradeClause;
        public bool HasTradeKicker;
        public float TradeKickerPercent; // Usually 15%

        [Header("Incentives")]
        public long LikelyIncentives;    // Count against cap
        public long UnlikelyIncentives;  // Don't count against cap
        public List<string> IncentiveDescriptions;

        [Header("Signing Bonus")]
        public long SigningBonus;

        [Header("Bird Rights Used")]
        public BirdRightsType BirdRightsUsed;

        public DateTime OfferDate;
        public DateTime ExpirationDate;

        public long GetEffectiveAnnualValue()
        {
            return AnnualSalary + LikelyIncentives;
        }

        public long GetMaxPossibleValue()
        {
            return (AnnualSalary * Years) + LikelyIncentives + UnlikelyIncentives + SigningBonus;
        }

        /// <summary>
        /// Create a descriptive summary of the offer
        /// </summary>
        public string GetOfferSummary()
        {
            string summary = $"{Years} years, ${TotalValue / 1_000_000f:F1}M";

            if (HasPlayerOption)
                summary += $" (PO in year {PlayerOptionYear})";
            if (HasTeamOption)
                summary += $" (TO in year {TeamOptionYear})";
            if (HasNoTradeClause)
                summary += " + NTC";
            if (HasTradeKicker)
                summary += $" + {TradeKickerPercent:F0}% trade kicker";
            if (LikelyIncentives > 0 || UnlikelyIncentives > 0)
                summary += $" + ${(LikelyIncentives + UnlikelyIncentives) / 1_000_000f:F1}M incentives";

            return summary;
        }
    }

    /// <summary>
    /// Bird rights types for contract signing
    /// </summary>
    public enum BirdRightsType
    {
        None,
        NonBird,      // 1 year with team: 120% prev salary, 4 years max
        EarlyBird,    // 2 years with team: avg salary or 175% prev, 4 years max
        FullBird      // 3+ years with team: any amount to max, 5 years, 8% raises
    }

    /// <summary>
    /// A single round in the negotiation process
    /// </summary>
    [Serializable]
    public class NegotiationRound
    {
        public int RoundNumber;
        public DateTime Timestamp;
        public string InitiatingParty;   // "Team" or "Agent"
        public ContractOffer Offer;
        public string ResponseMessage;
        public bool WasAccepted;
        public bool WasRejected;
        public bool WasCountered;
    }

    /// <summary>
    /// Complete negotiation session between team and player
    /// </summary>
    [Serializable]
    public class NegotiationSession
    {
        public string NegotiationId;
        public string PlayerId;
        public string PlayerName;
        public string TeamId;
        public string AgentId;

        public NegotiationStatus Status;
        public List<NegotiationRound> Rounds;

        public int CurrentRound;
        public int MaxRoundsBeforeWalkaway;  // Set by agent personality

        public PlayerPriorities PlayerPriorities;
        public long PlayerMarketValue;       // Estimated fair value
        public long PlayerAskingPrice;       // What agent initially wants

        public ContractOffer CurrentTeamOffer;
        public ContractOffer CurrentAgentCounter;

        public DateTime StartedAt;
        public DateTime LastActivity;
        public DateTime Deadline;

        public bool IsLeakedToMedia;
        public List<string> MediaLeaks;      // News that got out

        /// <summary>
        /// Check if negotiation can continue
        /// </summary>
        public bool CanContinue()
        {
            return Status == NegotiationStatus.InProgress ||
                   Status == NegotiationStatus.CounterOfferReceived;
        }

        /// <summary>
        /// Check if we've hit the walkaway limit
        /// </summary>
        public bool HasReachedWalkawayLimit()
        {
            return CurrentRound >= MaxRoundsBeforeWalkaway;
        }
    }

    /// <summary>
    /// Response from the agent during negotiation
    /// </summary>
    [Serializable]
    public class AgentResponse
    {
        public NegotiationStatus ResultingStatus;
        public ContractOffer CounterOffer;      // If countering
        public string Message;                   // Agent's response text
        public bool LeakedToMedia;
        public string LeakContent;
    }

    /// <summary>
    /// Manages contract negotiations between teams and players/agents
    /// </summary>
    public class ContractNegotiationManager
    {
        private Dictionary<string, NegotiationSession> _activeNegotiations;
        private Dictionary<string, List<NegotiationSession>> _negotiationHistory;
        private AgentManager _agentManager;

        public event Action<NegotiationSession> OnNegotiationStarted;
        public event Action<NegotiationSession> OnNegotiationUpdated;
        public event Action<NegotiationSession> OnNegotiationCompleted;
        public event Action<string, string> OnMediaLeak; // (headline, details)

        public ContractNegotiationManager(AgentManager agentManager)
        {
            _agentManager = agentManager;
            _activeNegotiations = new Dictionary<string, NegotiationSession>();
            _negotiationHistory = new Dictionary<string, List<NegotiationSession>>();
        }

        /// <summary>
        /// Start a new negotiation with a player
        /// </summary>
        public NegotiationSession StartNegotiation(
            string playerId,
            string playerName,
            string teamId,
            long estimatedMarketValue,
            PlayerPriorities priorities = null)
        {
            // Get the player's agent
            var agent = _agentManager.GetAgentForPlayer(playerId);
            if (agent == null)
            {
                // Assign an agent if they don't have one
                agent = _agentManager.AssignAgentToPlayer(playerId);
            }

            var negotiationStyle = agent.GetNegotiationStyle();

            var session = new NegotiationSession
            {
                NegotiationId = Guid.NewGuid().ToString(),
                PlayerId = playerId,
                PlayerName = playerName,
                TeamId = teamId,
                AgentId = agent.AgentId,
                Status = NegotiationStatus.InProgress,
                Rounds = new List<NegotiationRound>(),
                CurrentRound = 0,
                MaxRoundsBeforeWalkaway = negotiationStyle.GetRoundsBeforeWalkaway(),
                PlayerPriorities = priorities ?? PlayerPriorities.Generate(false, false, false),
                PlayerMarketValue = estimatedMarketValue,
                PlayerAskingPrice = (long)(estimatedMarketValue * (1 + negotiationStyle.InitialAskMultiplier)),
                StartedAt = DateTime.Now,
                LastActivity = DateTime.Now,
                Deadline = DateTime.Now.AddDays(14), // 2 weeks default
                IsLeakedToMedia = false,
                MediaLeaks = new List<string>()
            };

            _activeNegotiations[session.NegotiationId] = session;

            OnNegotiationStarted?.Invoke(session);

            Debug.Log($"[Negotiation] Started with {playerName} (Agent: {agent.FullName}, {agent.Personality}). Max rounds: {session.MaxRoundsBeforeWalkaway}");

            return session;
        }

        /// <summary>
        /// Submit an offer to the player/agent
        /// </summary>
        public AgentResponse SubmitOffer(string negotiationId, ContractOffer offer)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var session))
            {
                return new AgentResponse
                {
                    ResultingStatus = NegotiationStatus.Expired,
                    Message = "Negotiation not found or has ended."
                };
            }

            if (!session.CanContinue())
            {
                return new AgentResponse
                {
                    ResultingStatus = session.Status,
                    Message = "This negotiation has already concluded."
                };
            }

            var agent = _agentManager.GetAgent(session.AgentId);
            var style = agent.GetNegotiationStyle();

            session.CurrentRound++;
            session.LastActivity = DateTime.Now;
            session.CurrentTeamOffer = offer;

            // Record this round
            var round = new NegotiationRound
            {
                RoundNumber = session.CurrentRound,
                Timestamp = DateTime.Now,
                InitiatingParty = "Team",
                Offer = offer,
            };

            // Evaluate the offer
            var response = EvaluateOffer(session, offer, agent, style);

            round.WasAccepted = response.ResultingStatus == NegotiationStatus.Accepted;
            round.WasRejected = response.ResultingStatus == NegotiationStatus.Rejected ||
                               response.ResultingStatus == NegotiationStatus.WalkedAway;
            round.WasCountered = response.ResultingStatus == NegotiationStatus.CounterOfferReceived;
            round.ResponseMessage = response.Message;

            session.Rounds.Add(round);
            session.Status = response.ResultingStatus;

            if (response.CounterOffer != null)
            {
                session.CurrentAgentCounter = response.CounterOffer;
            }

            // Check for media leak
            if (CheckForMediaLeak(session, agent, offer))
            {
                response.LeakedToMedia = true;
                response.LeakContent = GenerateLeakContent(session, offer);
                session.IsLeakedToMedia = true;
                session.MediaLeaks.Add(response.LeakContent);
                OnMediaLeak?.Invoke($"{session.PlayerName} Contract Talks", response.LeakContent);
            }

            // Move to history if completed
            if (response.ResultingStatus == NegotiationStatus.Accepted ||
                response.ResultingStatus == NegotiationStatus.Rejected ||
                response.ResultingStatus == NegotiationStatus.WalkedAway)
            {
                FinalizeNegotiation(session, response.ResultingStatus == NegotiationStatus.Accepted);
            }

            OnNegotiationUpdated?.Invoke(session);

            return response;
        }

        /// <summary>
        /// Evaluate an offer and generate agent response
        /// </summary>
        private AgentResponse EvaluateOffer(
            NegotiationSession session,
            ContractOffer offer,
            Agent agent,
            NegotiationStyle style)
        {
            var response = new AgentResponse();

            float offerValue = CalculateOfferValue(session, offer, agent);
            float askingValue = session.PlayerAskingPrice;
            float offerPercentage = offer.AnnualSalary / (float)askingValue;

            Debug.Log($"[Negotiation] Round {session.CurrentRound}: Offer ${offer.AnnualSalary / 1_000_000f:F1}M ({offerPercentage:P0} of ask). Walkaway threshold: {style.WalkawayThreshold:P0}");

            // Check if they'll accept
            if (offerPercentage >= 0.95f)
            {
                // Accept offers at 95%+ of asking price
                response.ResultingStatus = NegotiationStatus.Accepted;
                response.Message = GenerateAcceptMessage(agent, offer);
            }
            else if (offerPercentage >= style.WalkawayThreshold)
            {
                // Within negotiable range - counter or accept based on rounds
                if (session.CurrentRound >= session.MaxRoundsBeforeWalkaway - 1 && offerPercentage >= style.WalkawayThreshold + 0.05f)
                {
                    // Running out of patience, might accept
                    if (UnityEngine.Random.value < 0.4f)
                    {
                        response.ResultingStatus = NegotiationStatus.Accepted;
                        response.Message = GenerateReluctantAcceptMessage(agent, offer);
                    }
                    else
                    {
                        response = GenerateCounterOffer(session, offer, agent, style);
                    }
                }
                else
                {
                    response = GenerateCounterOffer(session, offer, agent, style);
                }
            }
            else if (session.HasReachedWalkawayLimit())
            {
                // Too low and out of patience - walk away
                response.ResultingStatus = NegotiationStatus.WalkedAway;
                response.Message = GenerateWalkawayMessage(agent, offer, session);
            }
            else if (offerPercentage < style.WalkawayThreshold * 0.7f)
            {
                // Insultingly low offer
                response.ResultingStatus = NegotiationStatus.Rejected;
                response.Message = GenerateInsultedMessage(agent, offer);

                // Drama-prone agents might walk immediately
                if (agent.Personality == AgentPersonality.DramaProne && UnityEngine.Random.value < 0.3f)
                {
                    response.ResultingStatus = NegotiationStatus.WalkedAway;
                    response.Message = GenerateDramaticExitMessage(agent);
                }
            }
            else
            {
                // Counter offer
                response = GenerateCounterOffer(session, offer, agent, style);
            }

            return response;
        }

        /// <summary>
        /// Generate a counter offer from the agent
        /// </summary>
        private AgentResponse GenerateCounterOffer(
            NegotiationSession session,
            ContractOffer teamOffer,
            Agent agent,
            NegotiationStyle style)
        {
            var response = new AgentResponse
            {
                ResultingStatus = NegotiationStatus.CounterOfferReceived
            };

            // Calculate the counter based on asking price and aggression
            float roundProgress = session.CurrentRound / (float)session.MaxRoundsBeforeWalkaway;
            float dropPerRound = 1f - style.CounterOfferAggression;

            // Agent drops their ask each round
            long currentAsk = (long)(session.PlayerAskingPrice * (1 - (dropPerRound * roundProgress)));
            currentAsk = Math.Max(currentAsk, (long)(session.PlayerMarketValue * style.WalkawayThreshold));

            // Counter offer
            var counter = new ContractOffer
            {
                OfferId = Guid.NewGuid().ToString(),
                OfferingTeamId = session.TeamId,
                PlayerId = session.PlayerId,
                Years = AdjustYears(teamOffer.Years, style, session.CurrentRound),
                AnnualSalary = currentAsk,
                TotalValue = currentAsk * AdjustYears(teamOffer.Years, style, session.CurrentRound),
                HasPlayerOption = ShouldRequestPlayerOption(teamOffer, style, agent),
                PlayerOptionYear = teamOffer.Years,
                HasTeamOption = false, // Agents don't want team options
                HasNoTradeClause = ShouldRequestNoTrade(teamOffer, style, agent),
                HasTradeKicker = ShouldRequestTradeKicker(teamOffer, style),
                TradeKickerPercent = 15f,
                OfferDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddDays(3)
            };

            // Might accept team's structure with higher salary
            if (UnityEngine.Random.value < style.YearsFlexibility)
            {
                counter.Years = teamOffer.Years;
                counter.TotalValue = currentAsk * teamOffer.Years;
            }

            response.CounterOffer = counter;
            response.Message = GenerateCounterMessage(agent, counter, teamOffer, session);

            return response;
        }

        private int AdjustYears(int offeredYears, NegotiationStyle style, int round)
        {
            // Generally agents want longer deals for more security
            if (UnityEngine.Random.value > style.YearsFlexibility)
            {
                return Math.Min(5, offeredYears + 1);
            }
            return offeredYears;
        }

        private bool ShouldRequestPlayerOption(ContractOffer offer, NegotiationStyle style, Agent agent)
        {
            if (offer.HasPlayerOption) return true;
            if (agent.Personality == AgentPersonality.Aggressive) return true;
            return UnityEngine.Random.value > style.OptionsFlexibility;
        }

        private bool ShouldRequestNoTrade(ContractOffer offer, NegotiationStyle style, Agent agent)
        {
            if (offer.HasNoTradeClause) return true;
            if (agent.Personality == AgentPersonality.LoyaltyFocused) return UnityEngine.Random.value < 0.3f;
            return UnityEngine.Random.value < 0.15f;
        }

        private bool ShouldRequestTradeKicker(ContractOffer offer, NegotiationStyle style)
        {
            if (offer.HasTradeKicker) return true;
            return UnityEngine.Random.value < 0.2f;
        }

        /// <summary>
        /// Calculate the overall value of an offer considering all factors
        /// </summary>
        private float CalculateOfferValue(NegotiationSession session, ContractOffer offer, Agent agent)
        {
            float baseValue = offer.AnnualSalary;

            // Add value for player options
            if (offer.HasPlayerOption)
                baseValue *= 1.05f;

            // Subtract value for team options
            if (offer.HasTeamOption)
                baseValue *= 0.95f;

            // Add value for no-trade clause
            if (offer.HasNoTradeClause)
                baseValue *= 1.03f;

            // Consider incentives
            baseValue += offer.LikelyIncentives * 0.8f;
            baseValue += offer.UnlikelyIncentives * 0.3f;

            return baseValue;
        }

        /// <summary>
        /// Check if this negotiation gets leaked to media
        /// </summary>
        private bool CheckForMediaLeak(NegotiationSession session, Agent agent, ContractOffer offer)
        {
            // Already leaked
            if (session.IsLeakedToMedia) return false;

            bool isHighProfile = offer.AnnualSalary >= 20_000_000;
            bool isStalled = session.CurrentRound >= session.MaxRoundsBeforeWalkaway / 2;

            return agent.WillLeakToMedia(isHighProfile, isStalled);
        }

        private string GenerateLeakContent(NegotiationSession session, ContractOffer offer)
        {
            var templates = new[]
            {
                $"Sources say {session.PlayerName} is seeking a deal in the ${offer.AnnualSalary / 1_000_000f:F0}M per year range.",
                $"Negotiations between {session.PlayerName}'s camp and the team have hit a snag over contract length.",
                $"Multiple teams are monitoring the {session.PlayerName} situation as talks continue.",
                $"{session.PlayerName}'s agent is reportedly 'frustrated' with the pace of negotiations.",
                $"The {session.PlayerName} contract talks are expected to go down to the wire."
            };

            return templates[UnityEngine.Random.Range(0, templates.Length)];
        }

        /// <summary>
        /// Finalize a completed negotiation
        /// </summary>
        private void FinalizeNegotiation(NegotiationSession session, bool wasSuccessful)
        {
            _activeNegotiations.Remove(session.NegotiationId);

            if (!_negotiationHistory.ContainsKey(session.PlayerId))
            {
                _negotiationHistory[session.PlayerId] = new List<NegotiationSession>();
            }
            _negotiationHistory[session.PlayerId].Add(session);

            // Update agent relationship
            var agent = _agentManager.GetAgent(session.AgentId);
            agent?.UpdateRelationshipAfterNegotiation(session.TeamId, wasSuccessful, wasSuccessful);

            OnNegotiationCompleted?.Invoke(session);

            Debug.Log($"[Negotiation] Completed with {session.PlayerName}: {session.Status} after {session.CurrentRound} rounds");
        }

        // === Message Generation Methods ===

        private string GenerateAcceptMessage(Agent agent, ContractOffer offer)
        {
            var messages = new[]
            {
                $"We have a deal. {offer.GetOfferSummary()} works for us.",
                "After reviewing the terms, my client is ready to sign.",
                "We're pleased to accept this offer. Let's finalize the paperwork.",
                "This is fair value. We'll take it."
            };
            return messages[UnityEngine.Random.Range(0, messages.Length)];
        }

        private string GenerateReluctantAcceptMessage(Agent agent, ContractOffer offer)
        {
            var messages = new[]
            {
                "Look, it's not exactly what we wanted, but my client wants to move forward. We'll accept.",
                "We've been going back and forth long enough. Let's get this done at these terms.",
                "My client is eager to get to work. We'll accept, but we expect this to be remembered at extension time.",
                "Fine. We'll make this work. But we could have gotten there faster."
            };
            return messages[UnityEngine.Random.Range(0, messages.Length)];
        }

        private string GenerateWalkawayMessage(Agent agent, NegotiationSession session)
        {
            return agent.Personality switch
            {
                AgentPersonality.Aggressive =>
                    "We're done here. Your organization clearly doesn't value my client appropriately. We'll find a team that does.",
                AgentPersonality.Reasonable =>
                    "I appreciate your time, but we're too far apart. We need to explore other options.",
                AgentPersonality.LoyaltyFocused =>
                    "I really hoped we could make this work, but I have to do what's best for my client. We'll have to part ways.",
                AgentPersonality.DramaProne =>
                    "This has been a complete waste of time. Don't be surprised when you see where my client ends up.",
                _ => "We're ending negotiations at this time."
            };
        }

        private string GenerateInsultedMessage(Agent agent, ContractOffer offer)
        {
            return agent.Personality switch
            {
                AgentPersonality.Aggressive =>
                    $"${offer.AnnualSalary / 1_000_000f:F1}M? Are you serious? That's insulting. Come back with a real offer.",
                AgentPersonality.Reasonable =>
                    "I appreciate the offer, but it's significantly below market value. We need to see a substantial increase.",
                AgentPersonality.LoyaltyFocused =>
                    "My client loves this organization, but this offer doesn't reflect his value. Please reconsider.",
                AgentPersonality.DramaProne =>
                    "Wow. Just... wow. I'm going to pretend I didn't see this. Try again.",
                _ => "This offer is well below what we're looking for."
            };
        }

        private string GenerateDramaticExitMessage(Agent agent)
        {
            var messages = new[]
            {
                "That's it. I'm done. Expect a call from every reporter in the league in about 10 minutes.",
                "You've made a powerful enemy today. Good luck in free agency going forward.",
                "I've never been so disrespected in my career. We're DONE.",
                "My client deserves better than this circus. We're out."
            };
            return messages[UnityEngine.Random.Range(0, messages.Length)];
        }

        private string GenerateCounterMessage(Agent agent, ContractOffer counter, ContractOffer teamOffer, NegotiationSession session)
        {
            float difference = (counter.AnnualSalary - teamOffer.AnnualSalary) / (float)teamOffer.AnnualSalary;

            string baseMessage = agent.Personality switch
            {
                AgentPersonality.Aggressive =>
                    $"We need to see ${counter.AnnualSalary / 1_000_000f:F1}M per year. That's the number.",
                AgentPersonality.Reasonable =>
                    $"We're getting closer. How about ${counter.AnnualSalary / 1_000_000f:F1}M? That would work for both sides.",
                AgentPersonality.LoyaltyFocused =>
                    $"My client wants to be here. At ${counter.AnnualSalary / 1_000_000f:F1}M, we can make that happen.",
                AgentPersonality.DramaProne =>
                    $"${counter.AnnualSalary / 1_000_000f:F1}M. Other teams are offering more, you know.",
                _ => $"Our counter is ${counter.AnnualSalary / 1_000_000f:F1}M per year."
            };

            // Add comments about specific terms
            if (counter.HasPlayerOption && !teamOffer.HasPlayerOption)
            {
                baseMessage += " And we need that player option.";
            }
            if (counter.Years > teamOffer.Years)
            {
                baseMessage += $" We also need {counter.Years} years for security.";
            }

            return baseMessage;
        }

        // === Query Methods ===

        /// <summary>
        /// Get all active negotiations for a team
        /// </summary>
        public List<NegotiationSession> GetActiveNegotiationsForTeam(string teamId)
        {
            var results = new List<NegotiationSession>();
            foreach (var session in _activeNegotiations.Values)
            {
                if (session.TeamId == teamId && session.CanContinue())
                {
                    results.Add(session);
                }
            }
            return results;
        }

        /// <summary>
        /// Get negotiation by ID
        /// </summary>
        public NegotiationSession GetNegotiation(string negotiationId)
        {
            _activeNegotiations.TryGetValue(negotiationId, out var session);
            return session;
        }

        /// <summary>
        /// Get salary intel for a player from their agent
        /// </summary>
        public string GetSalaryIntel(string playerId, string teamId, long actualExpectation)
        {
            var agent = _agentManager.GetAgentForPlayer(playerId);
            if (agent == null)
            {
                return "Unable to get salary information - player has no representation.";
            }

            return agent.GetSalaryExpectationDescription(actualExpectation, teamId);
        }

        /// <summary>
        /// Get negotiation history for a player
        /// </summary>
        public List<NegotiationSession> GetNegotiationHistory(string playerId)
        {
            if (_negotiationHistory.TryGetValue(playerId, out var history))
            {
                return new List<NegotiationSession>(history);
            }
            return new List<NegotiationSession>();
        }

        /// <summary>
        /// Cancel/abandon an active negotiation
        /// </summary>
        public void AbandonNegotiation(string negotiationId)
        {
            if (_activeNegotiations.TryGetValue(negotiationId, out var session))
            {
                session.Status = NegotiationStatus.Rejected;
                FinalizeNegotiation(session, false);

                // This hurts relationship
                var agent = _agentManager.GetAgent(session.AgentId);
                if (agent != null)
                {
                    var relationship = agent.GetTeamRelationship(session.TeamId);
                    relationship.RelationshipScore = Mathf.Max(0, relationship.RelationshipScore - 10);
                }
            }
        }
    }
}
