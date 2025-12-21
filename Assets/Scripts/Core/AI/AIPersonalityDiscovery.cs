using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// Tracks observations about AI personality traits and reveals them over time.
    /// Works with both AI GM (for coaches) and AI Coach (for GMs).
    /// </summary>
    [Serializable]
    public class AIPersonalityDiscovery
    {
        // Observation tracking
        public Dictionary<string, TraitObservation> Observations = new Dictionary<string, TraitObservation>();
        public List<string> RevealedTraits = new List<string>();
        public List<PersonalityInsight> Insights = new List<PersonalityInsight>();

        // Relationship tracking
        public float RelationshipScore = 50f;  // 0-100
        public int TotalInteractions;
        public DateTime? FirstInteraction;
        public DateTime? LastInteraction;

        // AI identity
        public string AIProfileId;
        public string AIName;
        public bool IsGM;  // True = AI GM, False = AI Coach

        /// <summary>
        /// Record an observation about the AI's behavior.
        /// </summary>
        public void RecordObservation(string traitKey, bool positiveEvidence, string context)
        {
            if (!Observations.ContainsKey(traitKey))
            {
                Observations[traitKey] = new TraitObservation
                {
                    TraitKey = traitKey,
                    TraitName = GetTraitDisplayName(traitKey)
                };
            }

            var obs = Observations[traitKey];
            obs.TotalObservations++;

            if (positiveEvidence)
                obs.PositiveEvidence++;
            else
                obs.NegativeEvidence++;

            obs.RecentContexts.Add(context);
            if (obs.RecentContexts.Count > 5)
                obs.RecentContexts.RemoveAt(0);

            obs.LastObserved = DateTime.Now;
            LastInteraction = DateTime.Now;
            TotalInteractions++;

            if (FirstInteraction == null)
                FirstInteraction = DateTime.Now;

            // Check if ready to reveal
            CheckForReveal(obs);
        }

        /// <summary>
        /// Check if a trait has enough evidence to be revealed.
        /// </summary>
        private void CheckForReveal(TraitObservation obs)
        {
            if (obs.IsRevealed) return;

            // Need at least 3 observations
            if (obs.TotalObservations < 3) return;

            // Need strong evidence one way or the other
            float confidence = obs.GetConfidence();
            if (confidence < 0.6f) return;

            // Reveal the trait
            obs.IsRevealed = true;

            string insight = GenerateInsight(obs);
            if (!RevealedTraits.Contains(insight))
            {
                RevealedTraits.Add(insight);
                Insights.Add(new PersonalityInsight
                {
                    TraitKey = obs.TraitKey,
                    InsightText = insight,
                    DiscoveryDate = DateTime.Now,
                    Confidence = confidence,
                    IsPositive = obs.PositiveEvidence > obs.NegativeEvidence
                });
            }
        }

        private string GenerateInsight(TraitObservation obs)
        {
            bool hasIt = obs.PositiveEvidence > obs.NegativeEvidence;

            return obs.TraitKey switch
            {
                // GM traits
                "TradeHappy" => hasIt ? "Willing to make moves when opportunities arise" : "Prefers roster stability over frequent trades",
                "PatientBuilder" => hasIt ? "Takes a long-term approach to roster building" : "Focused on immediate results",
                "WinNow" => hasIt ? "Aggressive about winning now" : "Comfortable with gradual improvement",
                "CostConscious" => hasIt ? "Very budget-conscious in negotiations" : "Willing to spend for talent",
                "ValuesDraftPicks" => hasIt ? "Highly values draft capital" : "Views picks as trade chips",
                "TrustsCoach" => hasIt ? "Values your basketball input" : "Has own strong opinions on roster",
                "ProtectsStar" => hasIt ? "Reluctant to trade star players" : "No player is untouchable",
                "LoyalToPlayers" => hasIt ? "Hesitant to give up on players" : "Quick to move on from underperformers",

                // Coach traits
                "OffensiveMinded" => hasIt ? "Prioritizes offensive systems" : "Defensive-focused approach",
                "PlaysYouth" => hasIt ? "Willing to develop young players in games" : "Prefers veteran presence",
                "QuickToAdjust" => hasIt ? "Makes frequent in-game adjustments" : "Trusts the original game plan",
                "ClutchCoach" => hasIt ? "Excellent in crunch time situations" : "Tends to struggle in close games",
                "PlayersFavorite" => hasIt ? "Players respond well to coaching style" : "Has a more demanding approach",

                _ => $"Observed pattern: {obs.TraitName}"
            };
        }

        private string GetTraitDisplayName(string key)
        {
            return key switch
            {
                "TradeHappy" => "Trade Activity",
                "PatientBuilder" => "Building Philosophy",
                "WinNow" => "Urgency Level",
                "CostConscious" => "Budget Approach",
                "ValuesDraftPicks" => "Draft Pick Value",
                "TrustsCoach" => "Coach Relationship",
                "ProtectsStar" => "Star Protection",
                "LoyalToPlayers" => "Player Loyalty",
                "OffensiveMinded" => "Offensive Focus",
                "PlaysYouth" => "Youth Development",
                "QuickToAdjust" => "Adjustment Speed",
                "ClutchCoach" => "Clutch Performance",
                "PlayersFavorite" => "Player Relations",
                _ => key
            };
        }

        /// <summary>
        /// Get a summary of what we know about this AI's personality.
        /// </summary>
        public string GetPersonalitySummary()
        {
            if (RevealedTraits.Count == 0)
            {
                if (TotalInteractions == 0)
                    return $"You haven't worked with {AIName} yet.";
                else
                    return $"Still learning about {AIName}'s approach. Keep observing.";
            }

            string summary = $"What you've learned about {AIName}:\n\n";

            foreach (var insight in Insights.OrderByDescending(i => i.DiscoveryDate).Take(5))
            {
                summary += $"• {insight.InsightText}\n";
            }

            summary += $"\nRelationship: {GetRelationshipDescription()}";

            return summary;
        }

        /// <summary>
        /// Get short list of discovered traits for display.
        /// </summary>
        public List<string> GetTraitList()
        {
            return Insights
                .OrderByDescending(i => i.Confidence)
                .Take(5)
                .Select(i => i.InsightText)
                .ToList();
        }

        /// <summary>
        /// Get relationship quality description.
        /// </summary>
        public string GetRelationshipDescription()
        {
            if (TotalInteractions == 0) return "New";

            return RelationshipScore switch
            {
                >= 80 => "Excellent - Strong mutual respect",
                >= 60 => "Good - Professional and productive",
                >= 40 => "Neutral - Still building rapport",
                >= 20 => "Strained - Some tension exists",
                _ => "Difficult - Consider your approach"
            };
        }

        /// <summary>
        /// Adjust relationship based on interaction outcome.
        /// </summary>
        public void AdjustRelationship(float change)
        {
            RelationshipScore = Mathf.Clamp(RelationshipScore + change, 0, 100);
        }

        /// <summary>
        /// Get confidence level for a specific trait.
        /// </summary>
        public float GetTraitConfidence(string traitKey)
        {
            if (!Observations.ContainsKey(traitKey))
                return 0f;

            return Observations[traitKey].GetConfidence();
        }

        /// <summary>
        /// Check if a trait has been revealed.
        /// </summary>
        public bool IsTraitRevealed(string traitKey)
        {
            return Observations.ContainsKey(traitKey) && Observations[traitKey].IsRevealed;
        }
    }

    /// <summary>
    /// Tracks observations about a specific trait.
    /// </summary>
    [Serializable]
    public class TraitObservation
    {
        public string TraitKey;
        public string TraitName;
        public int TotalObservations;
        public int PositiveEvidence;
        public int NegativeEvidence;
        public List<string> RecentContexts = new List<string>();
        public DateTime? LastObserved;
        public bool IsRevealed;

        /// <summary>
        /// Calculate confidence that we understand this trait.
        /// </summary>
        public float GetConfidence()
        {
            if (TotalObservations == 0) return 0f;

            // How consistent is the evidence?
            int dominant = Math.Max(PositiveEvidence, NegativeEvidence);
            float consistency = (float)dominant / TotalObservations;

            // More observations = higher confidence
            float observationBonus = Mathf.Min(TotalObservations / 10f, 0.3f);

            return Mathf.Clamp01(consistency + observationBonus);
        }

        /// <summary>
        /// Get likely trait value (true = has trait, false = doesn't).
        /// </summary>
        public bool GetLikelyValue()
        {
            return PositiveEvidence > NegativeEvidence;
        }
    }

    /// <summary>
    /// A revealed personality insight.
    /// </summary>
    [Serializable]
    public class PersonalityInsight
    {
        public string TraitKey;
        public string InsightText;
        public DateTime DiscoveryDate;
        public float Confidence;
        public bool IsPositive;
    }

    /// <summary>
    /// Manager for all personality discovery across AI staff.
    /// </summary>
    public class PersonalityDiscoveryManager : MonoBehaviour
    {
        private static PersonalityDiscoveryManager _instance;
        public static PersonalityDiscoveryManager Instance => _instance;

        private Dictionary<string, AIPersonalityDiscovery> _discoveries = new Dictionary<string, AIPersonalityDiscovery>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// Get or create discovery tracker for an AI.
        /// </summary>
        public AIPersonalityDiscovery GetDiscovery(string aiProfileId, string aiName, bool isGM)
        {
            if (!_discoveries.ContainsKey(aiProfileId))
            {
                _discoveries[aiProfileId] = new AIPersonalityDiscovery
                {
                    AIProfileId = aiProfileId,
                    AIName = aiName,
                    IsGM = isGM
                };
            }

            return _discoveries[aiProfileId];
        }

        /// <summary>
        /// Record an observation about an AI.
        /// </summary>
        public void RecordObservation(string aiProfileId, string traitKey, bool positiveEvidence, string context)
        {
            if (_discoveries.ContainsKey(aiProfileId))
            {
                _discoveries[aiProfileId].RecordObservation(traitKey, positiveEvidence, context);
            }
        }

        /// <summary>
        /// Get all discovered insights for an AI.
        /// </summary>
        public List<string> GetInsights(string aiProfileId)
        {
            if (!_discoveries.ContainsKey(aiProfileId))
                return new List<string>();

            return _discoveries[aiProfileId].GetTraitList();
        }

        /// <summary>
        /// Save discovery state.
        /// </summary>
        public Dictionary<string, AIPersonalityDiscovery> GetSaveData()
        {
            return new Dictionary<string, AIPersonalityDiscovery>(_discoveries);
        }

        /// <summary>
        /// Load discovery state.
        /// </summary>
        public void LoadSaveData(Dictionary<string, AIPersonalityDiscovery> data)
        {
            _discoveries = data ?? new Dictionary<string, AIPersonalityDiscovery>();
        }
    }
}
