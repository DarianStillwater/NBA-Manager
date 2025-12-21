using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// Controls AI GM decision-making for Coach-Only mode.
    /// Processes roster requests, makes autonomous moves, and has hidden personality traits.
    /// </summary>
    public class AIGMController
    {
        private static AIGMController _instance;
        public static AIGMController Instance => _instance ??= new AIGMController();

        private System.Random _rng;

        // GM Personality (hidden from user, discovered through decisions)
        private AIGMPersonality _personality;
        private string _gmProfileId;
        private string _gmName;
        private string _teamId;

        // Request history
        private RosterRequestHistory _requestHistory;
        private List<string> _discoveredTraits;

        public RosterRequestHistory RequestHistory => _requestHistory;
        public List<string> DiscoveredTraits => _discoveredTraits;

        public AIGMController()
        {
            _rng = new System.Random();
            _requestHistory = new RosterRequestHistory();
            _discoveredTraits = new List<string>();
        }

        /// <summary>
        /// Initialize the AI GM for a specific team.
        /// </summary>
        public void Initialize(string gmProfileId, string gmName, string teamId)
        {
            _gmProfileId = gmProfileId;
            _gmName = gmName;
            _teamId = teamId;

            // Generate hidden personality
            _personality = AIGMPersonality.GenerateRandom(_rng);

            Debug.Log($"[AIGMController] Initialized for {gmName} ({teamId}). Personality generated.");
        }

        /// <summary>
        /// Process a roster request from the coach.
        /// </summary>
        public RosterRequestResult ProcessRequest(RosterRequest request)
        {
            if (request == null) return null;

            _requestHistory.AddRequest(request);

            // Evaluate based on request type and GM personality
            var result = EvaluateRequest(request);

            // Update history
            _requestHistory.UpdateRequestResult(request.RequestId, result);

            // Possibly reveal a personality trait
            if (result.RevealedTrait != null && !_discoveredTraits.Contains(result.RevealedTrait))
            {
                _discoveredTraits.Add(result.RevealedTrait);
            }

            return result;
        }

        private RosterRequestResult EvaluateRequest(RosterRequest request)
        {
            // Base approval chance starts at 50%
            float approvalChance = 0.5f;
            string revealedTrait = null;

            switch (request.Type)
            {
                case RosterRequestType.TradePlayer:
                    approvalChance = EvaluateTradeRequest(request, out revealedTrait);
                    break;

                case RosterRequestType.SignFreeAgent:
                    approvalChance = EvaluateSigningRequest(request, out revealedTrait);
                    break;

                case RosterRequestType.WaivePlayer:
                    approvalChance = EvaluateWaiveRequest(request, out revealedTrait);
                    break;

                case RosterRequestType.ExtendContract:
                    approvalChance = EvaluateExtensionRequest(request, out revealedTrait);
                    break;

                case RosterRequestType.AcquireBigMan:
                case RosterRequestType.AcquireGuard:
                case RosterRequestType.AcquireShooter:
                case RosterRequestType.AcquireDefender:
                case RosterRequestType.AcquireVeteran:
                    approvalChance = EvaluateNeedRequest(request, out revealedTrait);
                    break;

                case RosterRequestType.IncreaseBudget:
                    approvalChance = EvaluateBudgetRequest(request, out revealedTrait);
                    break;

                case RosterRequestType.TradeForPick:
                    approvalChance = EvaluatePickRequest(request, out revealedTrait);
                    break;
            }

            // Priority affects approval
            approvalChance += request.Priority switch
            {
                RequestPriority.Critical => 0.15f,
                RequestPriority.High => 0.10f,
                RequestPriority.Medium => 0f,
                RequestPriority.Low => -0.10f,
                _ => 0f
            };

            // Clamp to valid range
            approvalChance = Mathf.Clamp01(approvalChance);

            // Make the decision
            bool approved = _rng.NextDouble() < approvalChance;

            if (approved)
            {
                return GenerateApprovalResponse(request, revealedTrait);
            }
            else
            {
                return GenerateDenialResponse(request, revealedTrait);
            }
        }

        private float EvaluateTradeRequest(RosterRequest request, out string trait)
        {
            trait = null;
            float chance = 0.5f;

            // Personality effects
            if (_personality.TradeHappy)
            {
                chance += 0.2f;
                if (_rng.Next(100) < 30)
                    trait = "The GM seems willing to make moves";
            }
            else if (_personality.PatientBuilder)
            {
                chance -= 0.2f;
                if (_rng.Next(100) < 30)
                    trait = "The GM prefers stability over quick fixes";
            }

            // Star protection
            if (_personality.ProtectsStar && request.TradeAwayPlayerId != null)
            {
                // Would check if it's a star player
                chance -= 0.15f;
            }

            return chance;
        }

        private float EvaluateSigningRequest(RosterRequest request, out string trait)
        {
            trait = null;
            float chance = 0.55f;

            // Financial personality
            if (_personality.CostConscious)
            {
                chance -= 0.2f;
                if (_rng.Next(100) < 25)
                    trait = "The GM watches every dollar carefully";
            }

            if (_personality.SpendsToBuild)
            {
                chance += 0.15f;
                if (_rng.Next(100) < 25)
                    trait = "The GM isn't afraid to spend for talent";
            }

            // Veteran preference
            if (_personality.TrustsVeterans)
            {
                chance += 0.1f;
            }

            return chance;
        }

        private float EvaluateWaiveRequest(RosterRequest request, out string trait)
        {
            trait = null;
            float chance = 0.6f;  // Usually easier to cut someone

            if (_personality.LoyalToPlayers)
            {
                chance -= 0.2f;
                if (_rng.Next(100) < 30)
                    trait = "The GM seems hesitant to give up on players";
            }

            return chance;
        }

        private float EvaluateExtensionRequest(RosterRequest request, out string trait)
        {
            trait = null;
            float chance = 0.45f;

            if (_personality.LongTermThinker)
            {
                chance += 0.15f;
                if (_rng.Next(100) < 25)
                    trait = "The GM plans for the long term";
            }

            if (_personality.CostConscious)
            {
                chance -= 0.15f;
            }

            return chance;
        }

        private float EvaluateNeedRequest(RosterRequest request, out string trait)
        {
            trait = null;
            float chance = 0.5f;

            // GMs who trust coaches are more responsive
            if (_personality.TrustsCoach)
            {
                chance += 0.2f;
                if (_rng.Next(100) < 20)
                    trait = "The GM values your basketball input";
            }

            // Hands-on GMs might disagree with needs assessment
            if (_personality.HandsOn)
            {
                chance -= 0.1f;
                if (_rng.Next(100) < 20)
                    trait = "The GM has their own ideas about roster construction";
            }

            return chance;
        }

        private float EvaluateBudgetRequest(RosterRequest request, out string trait)
        {
            trait = null;
            float chance = 0.35f;  // Generally harder

            if (_personality.SpendsToBuild)
            {
                chance += 0.2f;
            }

            if (_personality.CostConscious)
            {
                chance -= 0.25f;
                if (_rng.Next(100) < 30)
                    trait = "The GM is focused on financial flexibility";
            }

            return chance;
        }

        private float EvaluatePickRequest(RosterRequest request, out string trait)
        {
            trait = null;
            float chance = 0.4f;

            if (_personality.ValuesDraftPicks)
            {
                chance += 0.25f;
                if (_rng.Next(100) < 25)
                    trait = "The GM highly values draft capital";
            }

            if (_personality.WinNowMentality)
            {
                chance -= 0.15f;
            }

            return chance;
        }

        private RosterRequestResult GenerateApprovalResponse(RosterRequest request, string trait)
        {
            string[] approvalResponses = GetApprovalResponses(request.Type);
            string response = approvalResponses[_rng.Next(approvalResponses.Length)];

            string action = request.Type switch
            {
                RosterRequestType.TradePlayer => "Working on trade scenarios",
                RosterRequestType.SignFreeAgent => "Reaching out to player's agent",
                RosterRequestType.WaivePlayer => "Processing the waiver",
                RosterRequestType.ExtendContract => "Beginning extension talks",
                _ => "Looking into options"
            };

            return new RosterRequestResult
            {
                IsApproved = true,
                GMResponse = response,
                ActionTaken = action,
                RevealedTrait = trait
            };
        }

        private RosterRequestResult GenerateDenialResponse(RosterRequest request, string trait)
        {
            string[] denialResponses = GetDenialResponses(request.Type);
            string response = denialResponses[_rng.Next(denialResponses.Length)];

            return new RosterRequestResult
            {
                IsApproved = false,
                GMResponse = response,
                RevealedTrait = trait
            };
        }

        private string[] GetApprovalResponses(RosterRequestType type)
        {
            return type switch
            {
                RosterRequestType.TradePlayer => new[]
                {
                    "I've been thinking the same thing. Let me work the phones.",
                    "Good call. I'll see what packages are out there.",
                    "Alright, I'll prioritize this. Give me a few days."
                },
                RosterRequestType.SignFreeAgent => new[]
                {
                    "I like it. Let me gauge their interest.",
                    "Solid suggestion. I'll make the call.",
                    "He'd fit well. I'll set up a meeting."
                },
                RosterRequestType.WaivePlayer => new[]
                {
                    "Agreed. It's time to move on.",
                    "I was coming to the same conclusion. Done.",
                    "Not easy, but necessary. I'll handle it."
                },
                RosterRequestType.ExtendContract => new[]
                {
                    "He's part of our core. I'll start discussions.",
                    "Smart to lock him up. On it.",
                    "Good thinking. Let's keep him happy."
                },
                _ => new[]
                {
                    "I'm on it.",
                    "Consider it done.",
                    "Good idea. I'll look into it."
                }
            };
        }

        private string[] GetDenialResponses(RosterRequestType type)
        {
            return type switch
            {
                RosterRequestType.TradePlayer => new[]
                {
                    "I don't think the value is there right now. Let's revisit later.",
                    "I'm not ready to give up on him yet.",
                    "The market's not right. Let's be patient."
                },
                RosterRequestType.SignFreeAgent => new[]
                {
                    "We can't afford him right now. Budget's tight.",
                    "I have concerns about the fit. Let's look elsewhere.",
                    "Not the right move financially."
                },
                RosterRequestType.WaivePlayer => new[]
                {
                    "Give him more time. He might turn it around.",
                    "The cap hit isn't worth it. Let's wait.",
                    "I think there's still value there."
                },
                RosterRequestType.ExtendContract => new[]
                {
                    "Too early. Let's see how the season plays out.",
                    "The numbers don't work right now.",
                    "I'm not ready to commit that much."
                },
                RosterRequestType.IncreaseBudget => new[]
                {
                    "Ownership won't go for it.",
                    "We need to maintain flexibility.",
                    "The budget is what it is. Work with it."
                },
                _ => new[]
                {
                    "Not right now. Focus on what we have.",
                    "I see it differently. Let's stay the course.",
                    "Trust me on this one."
                }
            };
        }

        /// <summary>
        /// Process daily autonomous GM decisions (trades, signings without coach input).
        /// </summary>
        public List<string> ProcessDailyDecisions()
        {
            var actions = new List<string>();

            // Small chance the GM makes moves on their own
            if (_rng.Next(100) < 5)  // 5% daily chance of autonomous move
            {
                if (_personality.TradeHappy && _rng.Next(100) < 30)
                {
                    actions.Add($"{_gmName} is exploring trade options");
                }
                else if (_personality.HandsOn && _rng.Next(100) < 20)
                {
                    actions.Add($"{_gmName} is looking at the waiver wire");
                }
            }

            return actions;
        }

        /// <summary>
        /// Get a summary of what we know about the GM's personality.
        /// </summary>
        public string GetKnownPersonalityDescription()
        {
            if (_discoveredTraits.Count == 0)
            {
                return "You haven't worked with this GM long enough to understand their style.";
            }

            string description = $"What you've learned about {_gmName}:\n\n";
            foreach (var trait in _discoveredTraits)
            {
                description += $"- {trait}\n";
            }

            var relationship = new CoachGMRelationship();
            relationship.UpdateFromHistory(_requestHistory);
            description += $"\nRelationship: {relationship.RelationshipStatus} (Approval rate: {relationship.ApprovalRate:P0})";

            return description;
        }
    }

    /// <summary>
    /// Hidden personality traits for AI GM.
    /// These are discovered through interactions.
    /// </summary>
    [Serializable]
    public class AIGMPersonality
    {
        // Trade tendencies
        public bool TradeHappy;         // Likes making moves
        public bool PatientBuilder;     // Prefers long-term building
        public bool WinNowMentality;    // Pushes for immediate success

        // Financial tendencies
        public bool CostConscious;      // Watches the budget closely
        public bool SpendsToBuild;      // Willing to pay for talent

        // Player handling
        public bool ProtectsStar;       // Won't trade top players
        public bool LoyalToPlayers;     // Hesitant to cut/trade vets
        public bool TrustsVeterans;     // Prefers experienced players

        // Draft tendencies
        public bool ValuesDraftPicks;   // Hoards picks
        public bool LongTermThinker;    // Plans 3+ years out

        // Coach relationship
        public bool TrustsCoach;        // Defers to coach on roster needs
        public bool HandsOn;            // Has own strong opinions

        public static AIGMPersonality GenerateRandom(System.Random rng)
        {
            return new AIGMPersonality
            {
                TradeHappy = rng.Next(100) < 40,
                PatientBuilder = rng.Next(100) < 50,
                WinNowMentality = rng.Next(100) < 35,
                CostConscious = rng.Next(100) < 55,
                SpendsToBuild = rng.Next(100) < 40,
                ProtectsStar = rng.Next(100) < 60,
                LoyalToPlayers = rng.Next(100) < 45,
                TrustsVeterans = rng.Next(100) < 50,
                ValuesDraftPicks = rng.Next(100) < 55,
                LongTermThinker = rng.Next(100) < 45,
                TrustsCoach = rng.Next(100) < 50,
                HandsOn = rng.Next(100) < 40
            };
        }
    }
}
