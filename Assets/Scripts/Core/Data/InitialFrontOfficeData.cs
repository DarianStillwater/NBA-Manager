using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Data structure for loading initial front office profiles from JSON.
    /// Contains real-world GM data as of a specific date.
    /// </summary>
    [Serializable]
    public class InitialFrontOfficeData
    {
        /// <summary>Date this data is accurate as of (e.g., "2025-12-01")</summary>
        public string dataAsOf;

        /// <summary>All 30 team front office profiles</summary>
        public List<FrontOfficeEntry> frontOffices;
    }

    /// <summary>
    /// Represents a single team's front office data for JSON loading.
    /// </summary>
    [Serializable]
    public class FrontOfficeEntry
    {
        // Identity
        public string teamId;
        public string gmName;
        public string presidentName;

        // Competence (string for JSON: "Elite", "Good", "Average", "Poor", "Terrible")
        public string competence;

        // Skills (0-100)
        public int tradeEvaluationSkill;
        public int negotiationSkill;
        public int scoutingAccuracy;

        // Tendencies (strings for JSON)
        public string tradeAggression;  // "VeryPassive", "Conservative", "Moderate", "Aggressive", "Desperate"
        public string teamSituation;    // "Championship", "Contending", "PlayoffBubble", "Rebuilding", "StuckInMiddle"

        // Behavior (0-100)
        public int patience;
        public int riskTolerance;
        public bool willingToTakeOnBadContracts;
        public bool willingToIncludePicks;
        public string leakTendency;     // "TightLipped", "Average", "Leaky"

        // Track Record
        public int yearsInPosition;

        // Former Player (optional)
        public bool isFormerPlayer;
        public string formerPlayerId;   // Optional - for linking to player data

        /// <summary>
        /// Convert to the game's FrontOfficeProfile type.
        /// </summary>
        public FrontOfficeProfile ToFrontOfficeProfile()
        {
            var profile = new FrontOfficeProfile
            {
                TeamId = teamId,
                GMName = gmName,
                PresidentName = presidentName ?? gmName,
                Competence = ParseCompetence(competence),
                TradeEvaluationSkill = tradeEvaluationSkill,
                NegotiationSkill = negotiationSkill,
                ScoutingAccuracy = scoutingAccuracy,
                Aggression = ParseTradeAggression(tradeAggression),
                CurrentSituation = ParseTeamSituation(teamSituation),
                Patience = patience,
                RiskTolerance = riskTolerance,
                WillingToTakeOnBadContracts = willingToTakeOnBadContracts,
                WillingToIncludePicks = willingToIncludePicks,
                LeakBehavior = CreateLeakTendency(leakTendency),
                YearsInPosition = yearsInPosition,
                IsFormerPlayer = isFormerPlayer,
                FormerPlayerId = formerPlayerId,
                RelationshipsWithOtherGMs = new Dictionary<string, int>()
            };

            // Set value preferences based on team situation
            profile.ValuePreferences = TradeValuePreferences.ForSituation(profile.CurrentSituation);

            return profile;
        }

        private static FrontOfficeCompetence ParseCompetence(string value)
        {
            if (string.IsNullOrEmpty(value))
                return FrontOfficeCompetence.Average;

            return value.ToLower() switch
            {
                "elite" => FrontOfficeCompetence.Elite,
                "good" => FrontOfficeCompetence.Good,
                "average" => FrontOfficeCompetence.Average,
                "poor" => FrontOfficeCompetence.Poor,
                "terrible" => FrontOfficeCompetence.Terrible,
                _ => FrontOfficeCompetence.Average
            };
        }

        private static TradeAggression ParseTradeAggression(string value)
        {
            if (string.IsNullOrEmpty(value))
                return TradeAggression.Moderate;

            return value.ToLower() switch
            {
                "verypassive" => TradeAggression.VeryPassive,
                "conservative" => TradeAggression.Conservative,
                "moderate" => TradeAggression.Moderate,
                "aggressive" => TradeAggression.Aggressive,
                "desperate" => TradeAggression.Desperate,
                _ => TradeAggression.Moderate
            };
        }

        private static TeamSituation ParseTeamSituation(string value)
        {
            if (string.IsNullOrEmpty(value))
                return TeamSituation.PlayoffBubble;

            return value.ToLower() switch
            {
                "championship" => TeamSituation.Championship,
                "contending" => TeamSituation.Contending,
                "playoffbubble" => TeamSituation.PlayoffBubble,
                "rebuilding" => TeamSituation.Rebuilding,
                "stuckinthemiddle" => TeamSituation.StuckInMiddle,
                _ => TeamSituation.PlayoffBubble
            };
        }

        private static LeakTendency CreateLeakTendency(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new LeakTendency
                {
                    BaseLeakChance = 30,
                    StarPlayerLeakBonus = 20,
                    DeadlineLeakBonus = 15,
                    LeaksToFavoredReporters = false,
                    FavoredReporters = new List<string>()
                };

            return value.ToLower() switch
            {
                "tightlipped" => LeakTendency.CreateTightLipped(),
                "leaky" => LeakTendency.CreateLeaky(),
                _ => new LeakTendency
                {
                    BaseLeakChance = 30,
                    StarPlayerLeakBonus = 20,
                    DeadlineLeakBonus = 15,
                    LeaksToFavoredReporters = false,
                    FavoredReporters = new List<string>()
                }
            };
        }
    }
}
