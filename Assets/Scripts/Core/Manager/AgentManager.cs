using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages player agents and contract negotiations.
    /// Uses real NBA agent names and personalities.
    /// </summary>
    public class AgentManager
    {
        private List<Agent> _agents = new List<Agent>();
        private Dictionary<string, string> _playerAgents = new Dictionary<string, string>(); // playerId -> agentId
        private Dictionary<string, Negotiation> _activeNegotiations = new Dictionary<string, Negotiation>();
        
        public AgentManager()
        {
            InitializeRealAgents();
        }

        // ==================== REAL NBA AGENTS ====================

        private void InitializeRealAgents()
        {
            _agents = new List<Agent>
            {
                // Super Agents (CAA, Klutch, Excel, WME)
                new Agent
                {
                    AgentId = "AGENT_PAUL",
                    Name = "Rich Paul",
                    Agency = "Klutch Sports Group",
                    IsSuperAgent = true,
                    Aggression = 95,
                    Loyalty = 30,
                    Reputation = 98,
                    ClientCount = 25,
                    PreferredMarkets = new List<string> { "LAL", "MIA", "BKN", "NYK" }
                },
                new Agent
                {
                    AgentId = "AGENT_MINTZ",
                    Name = "Jeff Schwartz",
                    Agency = "Excel Sports Management",
                    IsSuperAgent = true,
                    Aggression = 85,
                    Loyalty = 50,
                    Reputation = 95,
                    ClientCount = 30
                },
                new Agent
                {
                    AgentId = "AGENT_FALK",
                    Name = "Leon Rose/CAA",
                    Agency = "Creative Artists Agency",
                    IsSuperAgent = true,
                    Aggression = 80,
                    Loyalty = 45,
                    Reputation = 92,
                    ClientCount = 40
                },
                new Agent
                {
                    AgentId = "AGENT_TELLEM",
                    Name = "Arn Tellem/WME",
                    Agency = "WME Sports",
                    IsSuperAgent = true,
                    Aggression = 75,
                    Loyalty = 55,
                    Reputation = 90,
                    ClientCount = 35
                },
                
                // Top Tier Agents
                new Agent
                {
                    AgentId = "AGENT_WECHSLER",
                    Name = "Mark Bartelstein",
                    Agency = "Priority Sports",
                    IsSuperAgent = false,
                    Aggression = 70,
                    Loyalty = 60,
                    Reputation = 85,
                    ClientCount = 20
                },
                new Agent
                {
                    AgentId = "AGENT_LOVE",
                    Name = "Bill Duffy",
                    Agency = "BDA Sports",
                    IsSuperAgent = false,
                    Aggression = 65,
                    Loyalty = 70,
                    Reputation = 82,
                    ClientCount = 15
                },
                new Agent
                {
                    AgentId = "AGENT_FLEISHER",
                    Name = "Sam Goldfeder",
                    Agency = "Goldfeder Sports",
                    IsSuperAgent = false,
                    Aggression = 60,
                    Loyalty = 65,
                    Reputation = 78,
                    ClientCount = 12
                },
                new Agent
                {
                    AgentId = "AGENT_PELINKA",
                    Name = "Aaron Mintz",
                    Agency = "CAA Sports",
                    IsSuperAgent = false,
                    Aggression = 75,
                    Loyalty = 50,
                    Reputation = 80,
                    ClientCount = 18
                },
                
                // Mid-Tier Agents
                new Agent
                {
                    AgentId = "AGENT_GENERIC_1",
                    Name = "Mike Harrison",
                    Agency = "Tandem Sports",
                    IsSuperAgent = false,
                    Aggression = 55,
                    Loyalty = 70,
                    Reputation = 65,
                    ClientCount = 8
                },
                new Agent
                {
                    AgentId = "AGENT_GENERIC_2",
                    Name = "Steven Heumann",
                    Agency = "Wasserman",
                    IsSuperAgent = false,
                    Aggression = 50,
                    Loyalty = 75,
                    Reputation = 60,
                    ClientCount = 10
                }
            };
        }

        // ==================== AGENT ACCESS ====================

        public List<Agent> GetAllAgents() => _agents.ToList();
        
        public Agent GetAgent(string agentId) => _agents.FirstOrDefault(a => a.AgentId == agentId);
        
        public Agent GetPlayerAgent(string playerId)
        {
            if (_playerAgents.TryGetValue(playerId, out var agentId))
                return GetAgent(agentId);
            return null;
        }

        /// <summary>
        /// Alias for GetPlayerAgent for compatibility
        /// </summary>
        public Agent GetAgentForPlayer(string playerId) => GetPlayerAgent(playerId);

        /// <summary>
        /// Assigns a random agent to player and returns the agent
        /// </summary>
        public Agent AssignAgentToPlayer(string playerId, int playerOverall = 70)
        {
            AssignRandomAgent(playerId, playerOverall);
            return GetPlayerAgent(playerId);
        }

        public void AssignAgent(string playerId, string agentId)
        {
            _playerAgents[playerId] = agentId;
        }

        /// <summary>
        /// Assigns a random agent to a player based on player tier.
        /// </summary>
        public void AssignRandomAgent(string playerId, int playerOverall)
        {
            var rng = new System.Random();
            Agent agent;
            
            // Stars get super agents
            if (playerOverall >= 85)
            {
                var superAgents = _agents.Where(a => a.IsSuperAgent).ToList();
                agent = superAgents[rng.Next(superAgents.Count)];
            }
            // Good players get top agents
            else if (playerOverall >= 70)
            {
                var topAgents = _agents.Where(a => a.Reputation >= 75).ToList();
                agent = topAgents[rng.Next(topAgents.Count)];
            }
            // Everyone else
            else
            {
                agent = _agents[rng.Next(_agents.Count)];
            }
            
            AssignAgent(playerId, agent.AgentId);
        }

        // ==================== NEGOTIATIONS ====================

        /// <summary>
        /// Starts a contract negotiation.
        /// </summary>
        public Negotiation StartNegotiation(string playerId, string teamId, NegotiationType type)
        {
            var agent = GetPlayerAgent(playerId);
            if (agent == null)
            {
                AssignRandomAgent(playerId, 70); // Default
                agent = GetPlayerAgent(playerId);
            }
            
            var negotiation = new Negotiation
            {
                NegotiationId = $"NEG_{DateTime.Now.Ticks}",
                PlayerId = playerId,
                TeamId = teamId,
                AgentId = agent.AgentId,
                Type = type,
                Status = AgentNegotiationStatus.Initial,
                StartDate = DateTime.Now,
                Patience = 100 - agent.Aggression + 20, // Aggressive = less patience
                MeetingsHeld = 0
            };
            
            // Set initial demands based on agent personality
            negotiation.AgentDemand = CalculateInitialDemand(agent, type);
            
            _activeNegotiations[negotiation.NegotiationId] = negotiation;
            return negotiation;
        }

        private AgentContractOffer CalculateInitialDemand(Agent agent, NegotiationType type)
        {
            // Aggressive agents start higher
            float aggressionMultiplier = 1f + (agent.Aggression / 200f); // 1.0 - 1.5x

            // Base values (would come from player evaluation in real implementation)
            long baseSalary = 10_000_000;
            int baseYears = 4;

            return new AgentContractOffer
            {
                AnnualSalary = (long)(baseSalary * aggressionMultiplier),
                Years = baseYears,
                WantsNoTradeClause = agent.Aggression > 80,
                WantsPlayerOption = agent.Aggression > 70,
                WantsTradeKicker = agent.Aggression > 60
            };
        }

        /// <summary>
        /// Makes a counter offer in a negotiation.
        /// </summary>
        public AgentNegotiationResult MakeOffer(string negotiationId, AgentContractOffer teamOffer)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return new AgentNegotiationResult { Success = false, Message = "Negotiation not found" };
            
            var agent = GetAgent(negotiation.AgentId);
            negotiation.TeamOffer = teamOffer;
            negotiation.MeetingsHeld++;
            
            // Calculate acceptance probability
            float acceptChance = CalculateAcceptanceChance(negotiation, agent, teamOffer);
            
            var rng = new System.Random();
            bool accepted = rng.NextDouble() < acceptChance;
            
            if (accepted)
            {
                negotiation.Status = AgentNegotiationStatus.Accepted;
                return new AgentNegotiationResult
                {
                    Success = true,
                    Message = $"{agent.Name} accepts the offer!",
                    AcceptedTerms = teamOffer
                };
            }
            
            // Reduce patience
            int patienceLoss = 10 + (int)(agent.Aggression / 10);
            negotiation.Patience -= patienceLoss;
            
            if (negotiation.Patience <= 0)
            {
                negotiation.Status = AgentNegotiationStatus.WalkedAway;
                return new AgentNegotiationResult
                {
                    Success = false,
                    Message = $"{agent.Name} has walked away from negotiations!"
                };
            }
            
            // Agent makes counter
            negotiation.AgentDemand = CalculateCounterOffer(negotiation, agent, teamOffer);
            negotiation.Status = AgentNegotiationStatus.CounterOffer;

            return new AgentNegotiationResult
            {
                Success = false,
                Message = $"{agent.Name} counters with ${negotiation.AgentDemand.AnnualSalary:N0}/year",
                CounterOffer = negotiation.AgentDemand,
                RemainingPatience = negotiation.Patience
            };
        }

        private float CalculateAcceptanceChance(Negotiation negotiation, Agent agent, AgentContractOffer offer)
        {
            var demand = negotiation.AgentDemand;
            
            // How close is offer to demand?
            float salaryRatio = (float)offer.AnnualSalary / demand.AnnualSalary;
            float yearsRatio = (float)offer.Years / demand.Years;
            
            float baseChance = (salaryRatio * 0.7f + yearsRatio * 0.3f);
            
            // Adjust for agent personality
            if (agent.Loyalty > 70)
                baseChance += 0.1f; // More likely to accept from current team
            
            // More meetings = slightly more willing
            baseChance += negotiation.MeetingsHeld * 0.02f;
            
            // Options affect acceptance
            if (demand.WantsPlayerOption && !offer.HasPlayerOption)
                baseChance -= 0.15f;
            if (demand.WantsNoTradeClause && !offer.HasNoTradeClause)
                baseChance -= 0.1f;
            
            return Mathf.Clamp01(baseChance - 0.3f); // Needs ~85%+ match to accept
        }

        private AgentContractOffer CalculateCounterOffer(Negotiation negotiation, Agent agent, AgentContractOffer offer)
        {
            var demand = negotiation.AgentDemand;

            // Move slightly toward offer (but not all the way)
            float movementRate = (100 - agent.Aggression) / 200f + 0.1f; // 10-60% movement

            long salaryDiff = demand.AnnualSalary - offer.AnnualSalary;
            long newSalary = demand.AnnualSalary - (long)(salaryDiff * movementRate);

            return new AgentContractOffer
            {
                AnnualSalary = Math.Max(offer.AnnualSalary, newSalary),
                Years = demand.Years,
                WantsNoTradeClause = demand.WantsNoTradeClause,
                WantsPlayerOption = demand.WantsPlayerOption,
                WantsTradeKicker = demand.WantsTradeKicker,
                HasPlayerOption = demand.WantsPlayerOption,
                HasNoTradeClause = demand.WantsNoTradeClause
            };
        }

        // ==================== AGENT INFLUENCE ====================

        /// <summary>
        /// Checks if agent is demanding a trade for player.
        /// </summary>
        public bool IsAgentDemandingTrade(string playerId, int playerMorale)
        {
            var agent = GetPlayerAgent(playerId);
            if (agent == null) return false;
            
            // Unhappy player + aggressive agent = trade demand
            float tradeChance = 0f;
            
            if (playerMorale < 30)
                tradeChance += 0.4f;
            if (playerMorale < 20)
                tradeChance += 0.3f;
            
            tradeChance += agent.Aggression / 200f; // 0-0.5 from aggression
            
            return new System.Random().NextDouble() < tradeChance;
        }

        /// <summary>
        /// Gets FA destination preferences influenced by agent.
        /// </summary>
        public List<string> GetAgentPreferredDestinations(string playerId)
        {
            var agent = GetPlayerAgent(playerId);
            return agent?.PreferredMarkets ?? new List<string>();
        }

        // ==================== CLEANUP ====================

        public void EndNegotiation(string negotiationId)
        {
            _activeNegotiations.Remove(negotiationId);
        }

        public Negotiation GetNegotiation(string negotiationId)
        {
            return _activeNegotiations.TryGetValue(negotiationId, out var n) ? n : null;
        }
    }

    // ==================== DATA CLASSES ====================

    [Serializable]
    public class Agent
    {
        public string AgentId;
        public string Name;
        public string Agency;
        public bool IsSuperAgent;

        public int Aggression;      // 0-100, how hard they push
        public int Loyalty;         // 0-100, preference for keeping player
        public int Reputation;      // 0-100, player trust
        public int ClientCount;

        public List<string> PreferredMarkets = new List<string>(); // Team IDs

        // Alias for compatibility
        public string FullName => Name;

        // Personality mapped from Aggression for compatibility with Data.Agent interface
        public NBAHeadCoach.Core.Data.AgentPersonality Personality
        {
            get
            {
                if (Aggression >= 80) return NBAHeadCoach.Core.Data.AgentPersonality.Aggressive;
                if (Loyalty >= 70) return NBAHeadCoach.Core.Data.AgentPersonality.LoyaltyFocused;
                if (Aggression >= 50) return NBAHeadCoach.Core.Data.AgentPersonality.DramaProne;
                return NBAHeadCoach.Core.Data.AgentPersonality.Reasonable;
            }
        }

        /// <summary>
        /// Get negotiation style based on agent personality
        /// </summary>
        public NBAHeadCoach.Core.Data.NegotiationStyle GetNegotiationStyle()
        {
            return NBAHeadCoach.Core.Data.NegotiationStyle.FromPersonality(Personality);
        }

        /// <summary>
        /// Get relationship with a team (returns default for this simple agent)
        /// </summary>
        public NBAHeadCoach.Core.Data.AgentTeamRelationship GetTeamRelationship(string teamId)
        {
            return new NBAHeadCoach.Core.Data.AgentTeamRelationship
            {
                TeamId = teamId,
                RelationshipScore = PreferredMarkets.Contains(teamId) ? 70 : 50,
                SuccessfulDeals = 0,
                FailedNegotiations = 0,
                HasActiveClient = false,
                LastInteraction = DateTime.MinValue
            };
        }

        /// <summary>
        /// Check if agent will leak to media
        /// </summary>
        public bool WillLeakToMedia(bool isHighProfile, bool stalled)
        {
            int chance = Aggression / 2;
            if (isHighProfile) chance += 15;
            if (stalled) chance += 20;
            return UnityEngine.Random.Range(0, 100) < chance;
        }

        /// <summary>
        /// Update relationship after negotiation
        /// </summary>
        public void UpdateRelationshipAfterNegotiation(string teamId, bool successful, bool playerSigned = false)
        {
            // Simple implementation - could track in a dictionary if needed
        }

        /// <summary>
        /// Get salary expectation description for UI
        /// </summary>
        public string GetSalaryExpectationDescription(long baseSalary, string teamId = null)
        {
            float multiplier = 1f + (Aggression / 100f * 0.3f); // 1.0 to 1.3x based on aggression

            // Preferred markets get a slight discount
            if (teamId != null && PreferredMarkets.Contains(teamId))
                multiplier -= 0.05f;

            long expected = (long)(baseSalary * multiplier);
            return $"${expected / 1_000_000:F1}M per year";
        }
    }

    [Serializable]
    public class Negotiation
    {
        public string NegotiationId;
        public string PlayerId;
        public string TeamId;
        public string AgentId;
        public NegotiationType Type;
        public AgentNegotiationStatus Status;
        public DateTime StartDate;

        public int Patience;        // 0-100, walks away at 0
        public int MeetingsHeld;

        public AgentContractOffer AgentDemand;
        public AgentContractOffer TeamOffer;
    }

    public enum NegotiationType
    {
        Extension,
        FreeAgent,
        RFA,
        TradeAndExtend
    }

    public enum AgentNegotiationStatus
    {
        Initial,
        CounterOffer,
        Accepted,
        Rejected,
        WalkedAway
    }

    [Serializable]
    public class AgentContractOffer
    {
        public long AnnualSalary;
        public int Years;
        public bool WantsNoTradeClause;
        public bool WantsPlayerOption;
        public bool WantsTradeKicker;
        public bool HasNoTradeClause;
        public bool HasPlayerOption;
        public float TradeKickerPercent;
    }

    public class AgentNegotiationResult
    {
        public bool Success;
        public string Message;
        public AgentContractOffer AcceptedTerms;
        public AgentContractOffer CounterOffer;
        public int RemainingPatience;
    }
}
