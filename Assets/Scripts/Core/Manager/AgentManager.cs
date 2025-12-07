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
                Status = NegotiationStatus.Initial,
                StartDate = DateTime.Now,
                Patience = 100 - agent.Aggression + 20, // Aggressive = less patience
                MeetingsHeld = 0
            };
            
            // Set initial demands based on agent personality
            negotiation.AgentDemand = CalculateInitialDemand(agent, type);
            
            _activeNegotiations[negotiation.NegotiationId] = negotiation;
            return negotiation;
        }

        private ContractOffer CalculateInitialDemand(Agent agent, NegotiationType type)
        {
            // Aggressive agents start higher
            float aggressionMultiplier = 1f + (agent.Aggression / 200f); // 1.0 - 1.5x
            
            // Base values (would come from player evaluation in real implementation)
            long baseSalary = 10_000_000;
            int baseYears = 4;
            
            return new ContractOffer
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
        public NegotiationResult MakeOffer(string negotiationId, ContractOffer teamOffer)
        {
            if (!_activeNegotiations.TryGetValue(negotiationId, out var negotiation))
                return new NegotiationResult { Success = false, Message = "Negotiation not found" };
            
            var agent = GetAgent(negotiation.AgentId);
            negotiation.TeamOffer = teamOffer;
            negotiation.MeetingsHeld++;
            
            // Calculate acceptance probability
            float acceptChance = CalculateAcceptanceChance(negotiation, agent, teamOffer);
            
            var rng = new System.Random();
            bool accepted = rng.NextDouble() < acceptChance;
            
            if (accepted)
            {
                negotiation.Status = NegotiationStatus.Accepted;
                return new NegotiationResult
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
                negotiation.Status = NegotiationStatus.WalkedAway;
                return new NegotiationResult
                {
                    Success = false,
                    Message = $"{agent.Name} has walked away from negotiations!"
                };
            }
            
            // Agent makes counter
            negotiation.AgentDemand = CalculateCounterOffer(negotiation, agent, teamOffer);
            negotiation.Status = NegotiationStatus.CounterOffer;
            
            return new NegotiationResult
            {
                Success = false,
                Message = $"{agent.Name} counters with ${negotiation.AgentDemand.AnnualSalary:N0}/year",
                CounterOffer = negotiation.AgentDemand,
                RemainingPatience = negotiation.Patience
            };
        }

        private float CalculateAcceptanceChance(Negotiation negotiation, Agent agent, ContractOffer offer)
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

        private ContractOffer CalculateCounterOffer(Negotiation negotiation, Agent agent, ContractOffer offer)
        {
            var demand = negotiation.AgentDemand;
            
            // Move slightly toward offer (but not all the way)
            float movementRate = (100 - agent.Aggression) / 200f + 0.1f; // 10-60% movement
            
            long salaryDiff = demand.AnnualSalary - offer.AnnualSalary;
            long newSalary = demand.AnnualSalary - (long)(salaryDiff * movementRate);
            
            return new ContractOffer
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
    }

    [Serializable]
    public class Negotiation
    {
        public string NegotiationId;
        public string PlayerId;
        public string TeamId;
        public string AgentId;
        public NegotiationType Type;
        public NegotiationStatus Status;
        public DateTime StartDate;
        
        public int Patience;        // 0-100, walks away at 0
        public int MeetingsHeld;
        
        public ContractOffer AgentDemand;
        public ContractOffer TeamOffer;
    }

    public enum NegotiationType
    {
        Extension,
        FreeAgent,
        RFA,
        TradeAndExtend
    }

    public enum NegotiationStatus
    {
        Initial,
        CounterOffer,
        Accepted,
        Rejected,
        WalkedAway
    }

    [Serializable]
    public class ContractOffer
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

    public class NegotiationResult
    {
        public bool Success;
        public string Message;
        public ContractOffer AcceptedTerms;
        public ContractOffer CounterOffer;
        public int RemainingPatience;
    }
}
