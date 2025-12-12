using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Agent personality types that affect negotiation behavior
    /// </summary>
    public enum AgentPersonality
    {
        Aggressive,      // Always pushes for max, walks away quickly
        Reasonable,      // Fair market value, moderate patience
        LoyaltyFocused,  // Prioritizes fit and player happiness, very patient
        DramaProne       // Leaks to media, unpredictable behavior
    }

    /// <summary>
    /// Agent specialization types
    /// </summary>
    public enum AgentSpecialization
    {
        Superstar,       // Represents max-level players
        Veteran,         // Experienced role players
        YoungTalent,     // Rookies and developing players
        International,   // International players
        General          // No specific focus
    }

    /// <summary>
    /// Represents an agent's relationship with a specific team
    /// </summary>
    [Serializable]
    public class AgentTeamRelationship
    {
        public string TeamId;

        [Range(0, 100)]
        public int RelationshipScore; // 0 = hostile, 50 = neutral, 100 = excellent

        public int SuccessfulDeals;   // Number of completed contracts
        public int FailedNegotiations; // Number of deals that fell through
        public bool HasActiveClient;   // Currently has a player on this team

        public DateTime LastInteraction;

        /// <summary>
        /// Get salary intel visibility based on relationship
        /// </summary>
        public SalaryIntelLevel GetIntelLevel()
        {
            if (RelationshipScore >= 80) return SalaryIntelLevel.Detailed;
            if (RelationshipScore >= 60) return SalaryIntelLevel.Approximate;
            if (RelationshipScore >= 40) return SalaryIntelLevel.Vague;
            return SalaryIntelLevel.None;
        }
    }

    /// <summary>
    /// Level of salary information available based on relationship
    /// </summary>
    public enum SalaryIntelLevel
    {
        None,        // "We're not sharing our expectations"
        Vague,       // "Looking for a significant contract"
        Approximate, // "Looking for something in the $15-20M range"
        Detailed     // "We want $18.5M per year, 4 years"
    }

    /// <summary>
    /// Negotiation style parameters based on personality
    /// </summary>
    [Serializable]
    public class NegotiationStyle
    {
        [Header("Round Limits")]
        [Range(1, 10)]
        public int MinRoundsBeforeWalkaway;
        [Range(1, 10)]
        public int MaxRoundsBeforeWalkaway;

        [Header("Offer Behavior")]
        [Range(0f, 1f)]
        public float InitialAskMultiplier;     // How much above market value they start
        [Range(0f, 1f)]
        public float CounterOfferAggression;   // How much they reduce per round
        [Range(0f, 1f)]
        public float WalkawayThreshold;        // Minimum % of ask they'll accept

        [Header("Flexibility")]
        [Range(0f, 1f)]
        public float YearsFlexibility;         // Willingness to adjust years
        [Range(0f, 1f)]
        public float OptionsFlexibility;       // Willingness to add team/player options
        [Range(0f, 1f)]
        public float IncentivesAcceptance;     // Willingness to accept incentive-laden deals

        /// <summary>
        /// Get actual rounds before walking away (randomized within range)
        /// </summary>
        public int GetRoundsBeforeWalkaway()
        {
            return UnityEngine.Random.Range(MinRoundsBeforeWalkaway, MaxRoundsBeforeWalkaway + 1);
        }

        /// <summary>
        /// Create negotiation style based on personality
        /// </summary>
        public static NegotiationStyle FromPersonality(AgentPersonality personality)
        {
            return personality switch
            {
                AgentPersonality.Aggressive => new NegotiationStyle
                {
                    MinRoundsBeforeWalkaway = 2,
                    MaxRoundsBeforeWalkaway = 3,
                    InitialAskMultiplier = 0.25f,      // Asks 25% above market
                    CounterOfferAggression = 0.85f,    // Only drops 15% per round
                    WalkawayThreshold = 0.90f,         // Won't go below 90% of ask
                    YearsFlexibility = 0.2f,
                    OptionsFlexibility = 0.1f,
                    IncentivesAcceptance = 0.2f
                },
                AgentPersonality.Reasonable => new NegotiationStyle
                {
                    MinRoundsBeforeWalkaway = 4,
                    MaxRoundsBeforeWalkaway = 6,
                    InitialAskMultiplier = 0.15f,      // Asks 15% above market
                    CounterOfferAggression = 0.70f,    // Drops 30% per round
                    WalkawayThreshold = 0.80f,         // Will go to 80% of ask
                    YearsFlexibility = 0.5f,
                    OptionsFlexibility = 0.5f,
                    IncentivesAcceptance = 0.6f
                },
                AgentPersonality.LoyaltyFocused => new NegotiationStyle
                {
                    MinRoundsBeforeWalkaway = 5,
                    MaxRoundsBeforeWalkaway = 8,
                    InitialAskMultiplier = 0.10f,      // Asks 10% above market
                    CounterOfferAggression = 0.60f,    // Drops 40% per round
                    WalkawayThreshold = 0.70f,         // Will go to 70% of ask for right fit
                    YearsFlexibility = 0.7f,
                    OptionsFlexibility = 0.7f,
                    IncentivesAcceptance = 0.8f
                },
                AgentPersonality.DramaProne => new NegotiationStyle
                {
                    MinRoundsBeforeWalkaway = 3,
                    MaxRoundsBeforeWalkaway = 4,
                    InitialAskMultiplier = 0.20f,      // Asks 20% above market
                    CounterOfferAggression = 0.75f,    // Somewhat aggressive
                    WalkawayThreshold = 0.85f,         // Won't go too low
                    YearsFlexibility = 0.3f,
                    OptionsFlexibility = 0.3f,
                    IncentivesAcceptance = 0.3f
                },
                _ => new NegotiationStyle()
            };
        }
    }

    /// <summary>
    /// Represents a player agent in the NBA ecosystem
    /// Agents are persistent and represent multiple players
    /// </summary>
    [Serializable]
    public class Agent
    {
        [Header("Identity")]
        public string AgentId;
        public string FirstName;
        public string LastName;
        public string AgencyName;

        [Header("Personality & Style")]
        public AgentPersonality Personality;
        public AgentSpecialization Specialization;

        [Header("Reputation")]
        [Range(0, 100)]
        public int Reputation;           // Overall standing in the league
        [Range(0, 100)]
        public int Influence;            // Ability to sway player decisions
        [Range(0, 100)]
        public int MediaConnections;     // Ability to generate press coverage

        [Header("Behavior Tendencies")]
        [Range(0, 100)]
        public int LeakTendency;         // Likelihood to leak negotiation details
        [Range(0, 100)]
        public int LoyaltyToClients;     // Prioritizes client wishes vs money
        [Range(0, 100)]
        public int RiskTolerance;        // Willingness to push for risky deals

        [Header("Client Management")]
        public List<string> ClientPlayerIds;  // Players this agent represents
        public int MaxClients;                 // Maximum players they can represent

        [Header("Relationships")]
        public List<AgentTeamRelationship> TeamRelationships;

        [Header("Track Record")]
        public int CareerDealsCompleted;
        public long TotalContractValueNegotiated;
        public int YearsExperience;

        public string FullName => $"{FirstName} {LastName}";

        /// <summary>
        /// Get the negotiation style for this agent
        /// </summary>
        public NegotiationStyle GetNegotiationStyle()
        {
            return NegotiationStyle.FromPersonality(Personality);
        }

        /// <summary>
        /// Get relationship with a specific team
        /// </summary>
        public AgentTeamRelationship GetTeamRelationship(string teamId)
        {
            var relationship = TeamRelationships?.Find(r => r.TeamId == teamId);
            if (relationship == null)
            {
                relationship = new AgentTeamRelationship
                {
                    TeamId = teamId,
                    RelationshipScore = 50, // Neutral starting point
                    SuccessfulDeals = 0,
                    FailedNegotiations = 0,
                    HasActiveClient = false,
                    LastInteraction = DateTime.MinValue
                };
                TeamRelationships ??= new List<AgentTeamRelationship>();
                TeamRelationships.Add(relationship);
            }
            return relationship;
        }

        /// <summary>
        /// Check if agent will leak negotiation info to media
        /// </summary>
        public bool WillLeakToMedia(bool isHighProfilePlayer, bool negotiationsStalled)
        {
            int leakChance = LeakTendency;

            // Drama-prone agents leak more
            if (Personality == AgentPersonality.DramaProne)
                leakChance += 20;

            // High profile players generate more leaks
            if (isHighProfilePlayer)
                leakChance += 15;

            // Stalled negotiations increase leak chance
            if (negotiationsStalled)
                leakChance += 25;

            // Loyalty-focused agents leak less
            if (Personality == AgentPersonality.LoyaltyFocused)
                leakChance -= 20;

            return UnityEngine.Random.Range(0, 100) < Mathf.Clamp(leakChance, 0, 95);
        }

        /// <summary>
        /// Calculate how much the agent prioritizes money vs other factors
        /// </summary>
        public float GetMoneyPriority()
        {
            return Personality switch
            {
                AgentPersonality.Aggressive => 0.9f,
                AgentPersonality.Reasonable => 0.6f,
                AgentPersonality.LoyaltyFocused => 0.4f,
                AgentPersonality.DramaProne => 0.7f,
                _ => 0.6f
            };
        }

        /// <summary>
        /// Get salary intel level for a team based on relationship
        /// </summary>
        public SalaryIntelLevel GetSalaryIntel(string teamId)
        {
            var relationship = GetTeamRelationship(teamId);
            return relationship.GetIntelLevel();
        }

        /// <summary>
        /// Generate vague salary description based on intel level
        /// </summary>
        public string GetSalaryExpectationDescription(long actualExpectation, string teamId)
        {
            var intelLevel = GetSalaryIntel(teamId);

            return intelLevel switch
            {
                SalaryIntelLevel.None => "The agent isn't sharing their expectations with your organization.",
                SalaryIntelLevel.Vague => GetVagueSalaryDescription(actualExpectation),
                SalaryIntelLevel.Approximate => GetApproximateSalaryDescription(actualExpectation),
                SalaryIntelLevel.Detailed => $"Looking for ${actualExpectation / 1_000_000f:F1}M per year.",
                _ => "Unknown salary expectations."
            };
        }

        private string GetVagueSalaryDescription(long salary)
        {
            if (salary >= 40_000_000) return "Seeking a max-level contract.";
            if (salary >= 25_000_000) return "Looking for a significant, starter-level deal.";
            if (salary >= 15_000_000) return "Expecting a solid rotation player contract.";
            if (salary >= 8_000_000) return "Looking for a reasonable role player deal.";
            if (salary >= 3_000_000) return "Seeking a roster spot with fair compensation.";
            return "Looking for any opportunity to prove themselves.";
        }

        private string GetApproximateSalaryDescription(long salary)
        {
            // Add some variance to the range shown
            float variance = 0.15f;
            long low = (long)(salary * (1 - variance));
            long high = (long)(salary * (1 + variance));

            // Round to nearest million
            low = (low / 1_000_000) * 1_000_000;
            high = (high / 1_000_000) * 1_000_000;

            if (high >= 1_000_000)
            {
                return $"Looking for something in the ${low / 1_000_000}-{high / 1_000_000}M range per year.";
            }
            return $"Looking for around ${salary / 1000}K per year.";
        }

        /// <summary>
        /// Update relationship after a negotiation outcome
        /// </summary>
        public void UpdateRelationshipAfterNegotiation(string teamId, bool successful, bool playerHappy)
        {
            var relationship = GetTeamRelationship(teamId);

            if (successful)
            {
                relationship.SuccessfulDeals++;
                relationship.RelationshipScore = Mathf.Min(100, relationship.RelationshipScore + 10);

                if (playerHappy)
                    relationship.RelationshipScore = Mathf.Min(100, relationship.RelationshipScore + 5);
            }
            else
            {
                relationship.FailedNegotiations++;
                relationship.RelationshipScore = Mathf.Max(0, relationship.RelationshipScore - 5);
            }

            relationship.LastInteraction = DateTime.Now;
        }

        /// <summary>
        /// Check if agent can take on a new client
        /// </summary>
        public bool CanAcceptNewClient()
        {
            return ClientPlayerIds == null || ClientPlayerIds.Count < MaxClients;
        }

        /// <summary>
        /// Add a player to client list
        /// </summary>
        public bool AddClient(string playerId)
        {
            if (!CanAcceptNewClient()) return false;

            ClientPlayerIds ??= new List<string>();
            if (!ClientPlayerIds.Contains(playerId))
            {
                ClientPlayerIds.Add(playerId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove a player from client list
        /// </summary>
        public bool RemoveClient(string playerId)
        {
            return ClientPlayerIds?.Remove(playerId) ?? false;
        }

        // === Factory Methods for Creating Agents ===

        public static Agent CreateAggressiveAgent(string firstName, string lastName, string agency)
        {
            return new Agent
            {
                AgentId = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                AgencyName = agency,
                Personality = AgentPersonality.Aggressive,
                Specialization = AgentSpecialization.Superstar,
                Reputation = UnityEngine.Random.Range(70, 95),
                Influence = UnityEngine.Random.Range(75, 95),
                MediaConnections = UnityEngine.Random.Range(60, 85),
                LeakTendency = UnityEngine.Random.Range(40, 70),
                LoyaltyToClients = UnityEngine.Random.Range(50, 70),
                RiskTolerance = UnityEngine.Random.Range(70, 90),
                MaxClients = UnityEngine.Random.Range(15, 25),
                ClientPlayerIds = new List<string>(),
                TeamRelationships = new List<AgentTeamRelationship>(),
                CareerDealsCompleted = UnityEngine.Random.Range(50, 200),
                YearsExperience = UnityEngine.Random.Range(10, 25)
            };
        }

        public static Agent CreateReasonableAgent(string firstName, string lastName, string agency)
        {
            return new Agent
            {
                AgentId = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                AgencyName = agency,
                Personality = AgentPersonality.Reasonable,
                Specialization = AgentSpecialization.General,
                Reputation = UnityEngine.Random.Range(50, 80),
                Influence = UnityEngine.Random.Range(40, 70),
                MediaConnections = UnityEngine.Random.Range(30, 60),
                LeakTendency = UnityEngine.Random.Range(20, 40),
                LoyaltyToClients = UnityEngine.Random.Range(60, 80),
                RiskTolerance = UnityEngine.Random.Range(40, 60),
                MaxClients = UnityEngine.Random.Range(20, 40),
                ClientPlayerIds = new List<string>(),
                TeamRelationships = new List<AgentTeamRelationship>(),
                CareerDealsCompleted = UnityEngine.Random.Range(30, 100),
                YearsExperience = UnityEngine.Random.Range(5, 20)
            };
        }

        public static Agent CreateLoyaltyFocusedAgent(string firstName, string lastName, string agency)
        {
            return new Agent
            {
                AgentId = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                AgencyName = agency,
                Personality = AgentPersonality.LoyaltyFocused,
                Specialization = AgentSpecialization.Veteran,
                Reputation = UnityEngine.Random.Range(55, 85),
                Influence = UnityEngine.Random.Range(50, 75),
                MediaConnections = UnityEngine.Random.Range(20, 50),
                LeakTendency = UnityEngine.Random.Range(5, 25),
                LoyaltyToClients = UnityEngine.Random.Range(80, 100),
                RiskTolerance = UnityEngine.Random.Range(30, 50),
                MaxClients = UnityEngine.Random.Range(10, 25),
                ClientPlayerIds = new List<string>(),
                TeamRelationships = new List<AgentTeamRelationship>(),
                CareerDealsCompleted = UnityEngine.Random.Range(40, 120),
                YearsExperience = UnityEngine.Random.Range(8, 22)
            };
        }

        public static Agent CreateDramaProneAgent(string firstName, string lastName, string agency)
        {
            return new Agent
            {
                AgentId = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                AgencyName = agency,
                Personality = AgentPersonality.DramaProne,
                Specialization = AgentSpecialization.YoungTalent,
                Reputation = UnityEngine.Random.Range(40, 75),
                Influence = UnityEngine.Random.Range(55, 80),
                MediaConnections = UnityEngine.Random.Range(70, 95),
                LeakTendency = UnityEngine.Random.Range(60, 90),
                LoyaltyToClients = UnityEngine.Random.Range(40, 65),
                RiskTolerance = UnityEngine.Random.Range(60, 85),
                MaxClients = UnityEngine.Random.Range(15, 30),
                ClientPlayerIds = new List<string>(),
                TeamRelationships = new List<AgentTeamRelationship>(),
                CareerDealsCompleted = UnityEngine.Random.Range(20, 80),
                YearsExperience = UnityEngine.Random.Range(3, 15)
            };
        }

        /// <summary>
        /// Create a random agent with weighted personality distribution
        /// </summary>
        public static Agent CreateRandomAgent(string firstName, string lastName, string agency)
        {
            int roll = UnityEngine.Random.Range(0, 100);

            // Distribution: 25% Aggressive, 40% Reasonable, 25% Loyalty, 10% Drama
            if (roll < 25)
                return CreateAggressiveAgent(firstName, lastName, agency);
            if (roll < 65)
                return CreateReasonableAgent(firstName, lastName, agency);
            if (roll < 90)
                return CreateLoyaltyFocusedAgent(firstName, lastName, agency);

            return CreateDramaProneAgent(firstName, lastName, agency);
        }
    }

    /// <summary>
    /// Manager for the agent ecosystem
    /// </summary>
    [Serializable]
    public class AgentManager
    {
        public List<Agent> AllAgents;

        // Famous agency names for generation
        private static readonly string[] AgencyNames = new string[]
        {
            "Klutch Sports Group",
            "Creative Artists Agency",
            "Wasserman",
            "Excel Sports Management",
            "Octagon",
            "BDA Sports Management",
            "Priority Sports",
            "Roc Nation Sports",
            "Independent Sports & Entertainment",
            "Landmark Sports Agency",
            "WME Sports",
            "Athletes First",
            "Tandem Sports",
            "Beyond Athlete Management",
            "ASM Sports"
        };

        private static readonly string[] FirstNames = new string[]
        {
            "Rich", "Leon", "Jeff", "Mark", "Aaron", "Bill", "Dan", "Sam",
            "Mike", "David", "Steve", "Chris", "James", "Robert", "Jason",
            "Kevin", "Brian", "Eric", "Greg", "Paul", "Tom", "Alex", "Anthony"
        };

        private static readonly string[] LastNames = new string[]
        {
            "Paul", "Rose", "Schwartz", "Bartelstein", "Mintz", "Duffy", "Fegan",
            "Goldin", "Miller", "Cohen", "Patterson", "Williams", "Johnson", "Anderson",
            "Martinez", "Thompson", "Garcia", "Robinson", "Clark", "Lewis", "Walker"
        };

        public AgentManager()
        {
            AllAgents = new List<Agent>();
        }

        /// <summary>
        /// Generate initial agent pool for the league
        /// </summary>
        public void GenerateAgentPool(int count = 50)
        {
            AllAgents.Clear();

            HashSet<string> usedNames = new HashSet<string>();

            for (int i = 0; i < count; i++)
            {
                string firstName, lastName, fullName;
                do
                {
                    firstName = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
                    lastName = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];
                    fullName = $"{firstName} {lastName}";
                } while (usedNames.Contains(fullName));

                usedNames.Add(fullName);

                string agency = AgencyNames[UnityEngine.Random.Range(0, AgencyNames.Length)];

                var agent = Agent.CreateRandomAgent(firstName, lastName, agency);
                AllAgents.Add(agent);
            }
        }

        /// <summary>
        /// Get agent by ID
        /// </summary>
        public Agent GetAgent(string agentId)
        {
            return AllAgents?.Find(a => a.AgentId == agentId);
        }

        /// <summary>
        /// Get agent for a specific player
        /// </summary>
        public Agent GetAgentForPlayer(string playerId)
        {
            return AllAgents?.Find(a => a.ClientPlayerIds?.Contains(playerId) ?? false);
        }

        /// <summary>
        /// Assign a player to an appropriate agent
        /// </summary>
        public Agent AssignAgentToPlayer(string playerId, bool isSuperstar = false, bool isInternational = false)
        {
            // Find available agents with matching specialization
            var availableAgents = AllAgents?.FindAll(a => a.CanAcceptNewClient());

            if (availableAgents == null || availableAgents.Count == 0)
            {
                // Create a new agent if none available
                var newAgent = Agent.CreateRandomAgent(
                    FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)],
                    LastNames[UnityEngine.Random.Range(0, LastNames.Length)],
                    AgencyNames[UnityEngine.Random.Range(0, AgencyNames.Length)]
                );
                AllAgents ??= new List<Agent>();
                AllAgents.Add(newAgent);
                newAgent.AddClient(playerId);
                return newAgent;
            }

            // Prioritize agents by specialization match
            Agent selectedAgent = null;

            if (isSuperstar)
            {
                selectedAgent = availableAgents.Find(a =>
                    a.Specialization == AgentSpecialization.Superstar &&
                    a.Reputation >= 70);
            }
            else if (isInternational)
            {
                selectedAgent = availableAgents.Find(a =>
                    a.Specialization == AgentSpecialization.International);
            }

            // Fall back to any available agent weighted by reputation
            if (selectedAgent == null)
            {
                // Sort by reputation and pick from top half
                availableAgents.Sort((a, b) => b.Reputation.CompareTo(a.Reputation));
                int maxIndex = Mathf.Min(availableAgents.Count, Mathf.Max(1, availableAgents.Count / 2));
                selectedAgent = availableAgents[UnityEngine.Random.Range(0, maxIndex)];
            }

            selectedAgent.AddClient(playerId);
            return selectedAgent;
        }

        /// <summary>
        /// Get agents with good relationship to a team (for intel purposes)
        /// </summary>
        public List<Agent> GetAgentsWithGoodRelationship(string teamId, int minRelationship = 70)
        {
            return AllAgents?.FindAll(a =>
                a.GetTeamRelationship(teamId).RelationshipScore >= minRelationship
            ) ?? new List<Agent>();
        }

        /// <summary>
        /// Get all agents representing players on a specific team
        /// </summary>
        public List<Agent> GetAgentsWithClientsOnTeam(string teamId, List<string> teamRosterIds)
        {
            return AllAgents?.FindAll(a =>
                a.ClientPlayerIds?.Exists(pid => teamRosterIds.Contains(pid)) ?? false
            ) ?? new List<Agent>();
        }
    }
}
