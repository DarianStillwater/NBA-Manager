using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Front office competence level affecting trade behavior
    /// </summary>
    public enum FrontOfficeCompetence
    {
        Elite,      // Rarely makes bad trades, excellent at extracting value
        Good,       // Solid evaluations, occasionally wins trades
        Average,    // Fair trades, sometimes gets fleeced
        Poor,       // Frequently makes mistakes, can be exploited
        Terrible    // Historically bad decisions, easy target
    }

    /// <summary>
    /// Team's current competitive situation
    /// </summary>
    public enum TeamSituation
    {
        Championship,    // Title contender, win-now moves
        Contending,      // Playoff team looking to improve
        PlayoffBubble,   // On the edge, could go either way
        Rebuilding,      // Tanking or developing young core
        StuckInMiddle    // Not good enough to contend, not bad enough to tank
    }

    /// <summary>
    /// Trade aggressiveness level
    /// </summary>
    public enum TradeAggression
    {
        VeryPassive,     // Rarely makes trades
        Conservative,    // Only sure things
        Moderate,        // Normal trade activity
        Aggressive,      // Always looking to deal
        Desperate        // Will make bad trades to make moves
    }

    /// <summary>
    /// What the front office values in trades
    /// </summary>
    [Serializable]
    public class TradeValuePreferences
    {
        [Range(0f, 1f)]
        public float ValuesDraftPicks;       // Preference for accumulating picks

        [Range(0f, 1f)]
        public float ValuesYoungPlayers;     // Preference for young talent

        [Range(0f, 1f)]
        public float ValuesVeterans;         // Preference for win-now veterans

        [Range(0f, 1f)]
        public float ValuesSalaryRelief;     // Willingness to dump salary

        [Range(0f, 1f)]
        public float ValuesStarPower;        // Chases big names

        [Range(0f, 1f)]
        public float ValuesDepth;            // Values role players and depth

        public static TradeValuePreferences ForSituation(TeamSituation situation)
        {
            return situation switch
            {
                TeamSituation.Championship => new TradeValuePreferences
                {
                    ValuesDraftPicks = 0.2f,
                    ValuesYoungPlayers = 0.3f,
                    ValuesVeterans = 0.9f,
                    ValuesSalaryRelief = 0.1f,
                    ValuesStarPower = 0.8f,
                    ValuesDepth = 0.7f
                },
                TeamSituation.Contending => new TradeValuePreferences
                {
                    ValuesDraftPicks = 0.3f,
                    ValuesYoungPlayers = 0.4f,
                    ValuesVeterans = 0.7f,
                    ValuesSalaryRelief = 0.2f,
                    ValuesStarPower = 0.7f,
                    ValuesDepth = 0.6f
                },
                TeamSituation.PlayoffBubble => new TradeValuePreferences
                {
                    ValuesDraftPicks = 0.5f,
                    ValuesYoungPlayers = 0.5f,
                    ValuesVeterans = 0.5f,
                    ValuesSalaryRelief = 0.4f,
                    ValuesStarPower = 0.5f,
                    ValuesDepth = 0.5f
                },
                TeamSituation.Rebuilding => new TradeValuePreferences
                {
                    ValuesDraftPicks = 0.9f,
                    ValuesYoungPlayers = 0.8f,
                    ValuesVeterans = 0.2f,
                    ValuesSalaryRelief = 0.7f,
                    ValuesStarPower = 0.3f,
                    ValuesDepth = 0.2f
                },
                TeamSituation.StuckInMiddle => new TradeValuePreferences
                {
                    ValuesDraftPicks = 0.6f,
                    ValuesYoungPlayers = 0.6f,
                    ValuesVeterans = 0.4f,
                    ValuesSalaryRelief = 0.5f,
                    ValuesStarPower = 0.6f,
                    ValuesDepth = 0.4f
                },
                _ => new TradeValuePreferences
                {
                    ValuesDraftPicks = 0.5f,
                    ValuesYoungPlayers = 0.5f,
                    ValuesVeterans = 0.5f,
                    ValuesSalaryRelief = 0.5f,
                    ValuesStarPower = 0.5f,
                    ValuesDepth = 0.5f
                }
            };
        }
    }

    /// <summary>
    /// Information leak tendencies
    /// </summary>
    [Serializable]
    public class LeakTendency
    {
        [Range(0, 100)]
        public int BaseLeakChance;           // General tendency to leak info

        [Range(0, 100)]
        public int StarPlayerLeakBonus;      // Extra leak chance for star trades

        [Range(0, 100)]
        public int DeadlineLeakBonus;        // Extra leak chance near deadline

        public bool LeaksToFavoredReporters; // Has preferred media contacts
        public List<string> FavoredReporters;

        public int GetLeakChance(bool involvesStarPlayer, bool nearDeadline)
        {
            int chance = BaseLeakChance;
            if (involvesStarPlayer) chance += StarPlayerLeakBonus;
            if (nearDeadline) chance += DeadlineLeakBonus;
            return Mathf.Clamp(chance, 0, 95);
        }

        public static LeakTendency CreateTightLipped()
        {
            return new LeakTendency
            {
                BaseLeakChance = 10,
                StarPlayerLeakBonus = 10,
                DeadlineLeakBonus = 15,
                LeaksToFavoredReporters = false,
                FavoredReporters = new List<string>()
            };
        }

        public static LeakTendency CreateLeaky()
        {
            return new LeakTendency
            {
                BaseLeakChance = 50,
                StarPlayerLeakBonus = 25,
                DeadlineLeakBonus = 20,
                LeaksToFavoredReporters = true,
                FavoredReporters = new List<string> { "Woj", "Shams" }
            };
        }
    }

    /// <summary>
    /// Represents a team's front office profile for trade AI
    /// </summary>
    [Serializable]
    public class FrontOfficeProfile
    {
        [Header("Identity")]
        public string TeamId;
        public string GMName;
        public string PresidentName;

        [Header("Competence")]
        public FrontOfficeCompetence Competence;

        [Range(0, 100)]
        public int TradeEvaluationSkill;     // Ability to assess player value

        [Range(0, 100)]
        public int NegotiationSkill;         // Ability to extract value in deals

        [Range(0, 100)]
        public int ScoutingAccuracy;         // How well they project players

        [Header("Tendencies")]
        public TeamSituation CurrentSituation;
        public TradeAggression Aggression;
        public TradeValuePreferences ValuePreferences;

        [Header("Behavior")]
        public LeakTendency LeakBehavior;

        [Range(0, 100)]
        public int Patience;                  // Willingness to wait for right deal

        [Range(0, 100)]
        public int RiskTolerance;            // Willingness to gamble on upside

        public bool WillingToTakeOnBadContracts; // For picks/assets
        public bool WillingToIncludePicks;       // Gives up draft capital

        [Header("Relationships")]
        public Dictionary<string, int> RelationshipsWithOtherGMs; // TeamId -> Relationship score

        [Header("Track Record")]
        public int TradesCompleted;
        public int TradesWon;                 // Trades that worked out
        public int TradesLost;                // Trades that backfired
        public int YearsInPosition;

        [Header("Former Player")]
        public bool IsFormerPlayer;
        public string FormerPlayerId;
        public string FormerPlayerGMId;
        public FormerPlayerGMTraits FormerPlayerTraits;

        // === Computed Properties ===

        /// <summary>
        /// Get acceptance threshold modifier based on competence
        /// </summary>
        public float GetAcceptanceThresholdModifier()
        {
            return Competence switch
            {
                FrontOfficeCompetence.Elite => 1.15f,      // Needs better value to accept
                FrontOfficeCompetence.Good => 1.05f,
                FrontOfficeCompetence.Average => 1.0f,
                FrontOfficeCompetence.Poor => 0.90f,       // Accepts worse deals
                FrontOfficeCompetence.Terrible => 0.80f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get counter-offer quality based on negotiation skill
        /// </summary>
        public float GetCounterOfferQuality()
        {
            return NegotiationSkill / 100f;
        }

        /// <summary>
        /// Check if this FO would leak trade talks
        /// </summary>
        public bool WouldLeakTrade(bool involvesStarPlayer, bool nearDeadline)
        {
            int leakChance = LeakBehavior.GetLeakChance(involvesStarPlayer, nearDeadline);
            return UnityEngine.Random.Range(0, 100) < leakChance;
        }

        /// <summary>
        /// Get relationship modifier for trade with another team
        /// </summary>
        public float GetRelationshipModifier(string otherTeamId)
        {
            if (RelationshipsWithOtherGMs == null) return 1.0f;

            if (RelationshipsWithOtherGMs.TryGetValue(otherTeamId, out int relationship))
            {
                // -100 to 100 scale -> 0.85 to 1.15 modifier
                return 1.0f + (relationship / 666f);
            }
            return 1.0f;
        }

        /// <summary>
        /// Update relationship after trade
        /// </summary>
        public void UpdateRelationship(string otherTeamId, bool tradeFeltFair)
        {
            RelationshipsWithOtherGMs ??= new Dictionary<string, int>();

            if (!RelationshipsWithOtherGMs.ContainsKey(otherTeamId))
            {
                RelationshipsWithOtherGMs[otherTeamId] = 0;
            }

            int change = tradeFeltFair ? 5 : -10;
            RelationshipsWithOtherGMs[otherTeamId] = Mathf.Clamp(
                RelationshipsWithOtherGMs[otherTeamId] + change, -100, 100);
        }

        /// <summary>
        /// Check if willing to engage in trade talks for this type of deal
        /// </summary>
        public bool WouldConsiderTrade(TradeType tradeType)
        {
            return tradeType switch
            {
                TradeType.StarAcquisition => CurrentSituation <= TeamSituation.Contending,
                TradeType.SalaryDump => WillingToTakeOnBadContracts || CurrentSituation == TeamSituation.Rebuilding,
                TradeType.RolePlayerSwap => true,
                TradeType.DraftPickTrade => WillingToIncludePicks || CurrentSituation == TeamSituation.Championship,
                TradeType.TankMove => CurrentSituation == TeamSituation.Rebuilding,
                _ => true
            };
        }

        // === Former Player GM Methods ===

        /// <summary>
        /// Get signing bonus for former teammates (+15%)
        /// </summary>
        public float GetFormerTeammateSigningBonus(string playerId)
        {
            if (!IsFormerPlayer || FormerPlayerTraits == null)
                return 0f;

            return FormerPlayerTraits.GetFormerTeammateSigningBonus(playerId);
        }

        /// <summary>
        /// Get position-specific scouting bonus (+10-20 for GM's position)
        /// </summary>
        public int GetPositionScoutingBonus(Position position)
        {
            if (!IsFormerPlayer || FormerPlayerTraits == null)
                return 0;

            return FormerPlayerTraits.GetPositionScoutingBonus(position);
        }

        /// <summary>
        /// Get leak modifier based on former player's media presence
        /// </summary>
        public int GetFormerPlayerLeakModifier()
        {
            if (!IsFormerPlayer || FormerPlayerTraits == null)
                return 0;

            return FormerPlayerTraits.GetMediaPresenceLeakModifier();
        }

        /// <summary>
        /// Get free agent attraction bonus for high profile former player GMs
        /// </summary>
        public float GetFreeAgentAttractionBonus()
        {
            if (!IsFormerPlayer || FormerPlayerTraits == null)
                return 0f;

            return FormerPlayerTraits.GetFreeAgentAttractionBonus();
        }

        /// <summary>
        /// Check if this GM has a connection to a specific player (former teammate)
        /// </summary>
        public bool HasConnectionToPlayer(string playerId)
        {
            if (!IsFormerPlayer || FormerPlayerTraits == null)
                return false;

            return FormerPlayerTraits.FormerTeammateIds?.Contains(playerId) ?? false;
        }

        /// <summary>
        /// Check if this GM played for a specific team
        /// </summary>
        public bool PlayedForTeam(string teamId)
        {
            if (!IsFormerPlayer || FormerPlayerTraits == null)
                return false;

            return FormerPlayerTraits.FormerTeamIds?.Contains(teamId) ?? false;
        }

        // === Factory Methods ===

        public static FrontOfficeProfile CreateElite(string teamId, string gmName)
        {
            return new FrontOfficeProfile
            {
                TeamId = teamId,
                GMName = gmName,
                Competence = FrontOfficeCompetence.Elite,
                TradeEvaluationSkill = UnityEngine.Random.Range(85, 100),
                NegotiationSkill = UnityEngine.Random.Range(85, 100),
                ScoutingAccuracy = UnityEngine.Random.Range(80, 95),
                Aggression = TradeAggression.Moderate,
                Patience = UnityEngine.Random.Range(70, 90),
                RiskTolerance = UnityEngine.Random.Range(40, 60),
                WillingToTakeOnBadContracts = true,
                WillingToIncludePicks = false, // Elite GMs protect picks
                LeakBehavior = LeakTendency.CreateTightLipped(),
                RelationshipsWithOtherGMs = new Dictionary<string, int>(),
                YearsInPosition = UnityEngine.Random.Range(5, 15)
            };
        }

        public static FrontOfficeProfile CreatePoor(string teamId, string gmName)
        {
            return new FrontOfficeProfile
            {
                TeamId = teamId,
                GMName = gmName,
                Competence = FrontOfficeCompetence.Poor,
                TradeEvaluationSkill = UnityEngine.Random.Range(30, 50),
                NegotiationSkill = UnityEngine.Random.Range(25, 45),
                ScoutingAccuracy = UnityEngine.Random.Range(35, 55),
                Aggression = TradeAggression.Aggressive, // Bad GMs often over-trade
                Patience = UnityEngine.Random.Range(20, 40),
                RiskTolerance = UnityEngine.Random.Range(60, 85),
                WillingToTakeOnBadContracts = false,
                WillingToIncludePicks = true, // Often gives up too much
                LeakBehavior = LeakTendency.CreateLeaky(),
                RelationshipsWithOtherGMs = new Dictionary<string, int>(),
                YearsInPosition = UnityEngine.Random.Range(1, 4)
            };
        }

        public static FrontOfficeProfile CreateAverage(string teamId, string gmName)
        {
            return new FrontOfficeProfile
            {
                TeamId = teamId,
                GMName = gmName,
                Competence = FrontOfficeCompetence.Average,
                TradeEvaluationSkill = UnityEngine.Random.Range(50, 70),
                NegotiationSkill = UnityEngine.Random.Range(45, 65),
                ScoutingAccuracy = UnityEngine.Random.Range(50, 70),
                Aggression = TradeAggression.Moderate,
                Patience = UnityEngine.Random.Range(45, 65),
                RiskTolerance = UnityEngine.Random.Range(40, 60),
                WillingToTakeOnBadContracts = UnityEngine.Random.value > 0.5f,
                WillingToIncludePicks = UnityEngine.Random.value > 0.5f,
                LeakBehavior = new LeakTendency
                {
                    BaseLeakChance = 30,
                    StarPlayerLeakBonus = 20,
                    DeadlineLeakBonus = 15,
                    LeaksToFavoredReporters = false,
                    FavoredReporters = new List<string>()
                },
                RelationshipsWithOtherGMs = new Dictionary<string, int>(),
                YearsInPosition = UnityEngine.Random.Range(2, 8)
            };
        }

        /// <summary>
        /// Create random FO profile with weighted distribution
        /// </summary>
        public static FrontOfficeProfile CreateRandom(string teamId, string gmName)
        {
            int roll = UnityEngine.Random.Range(0, 100);

            // Distribution: 10% Elite, 25% Good, 35% Average, 20% Poor, 10% Terrible
            if (roll < 10)
                return CreateElite(teamId, gmName);
            if (roll < 35)
            {
                var profile = CreateAverage(teamId, gmName);
                profile.Competence = FrontOfficeCompetence.Good;
                profile.TradeEvaluationSkill += 15;
                profile.NegotiationSkill += 15;
                return profile;
            }
            if (roll < 70)
                return CreateAverage(teamId, gmName);
            if (roll < 90)
                return CreatePoor(teamId, gmName);

            var terrible = CreatePoor(teamId, gmName);
            terrible.Competence = FrontOfficeCompetence.Terrible;
            terrible.TradeEvaluationSkill -= 15;
            terrible.NegotiationSkill -= 15;
            return terrible;
        }

        /// <summary>
        /// Update situation based on team record
        /// </summary>
        public void UpdateSituation(int wins, int losses, bool hasStarPlayer, int draftPosition)
        {
            float winPct = wins / (float)(wins + losses);

            if (hasStarPlayer && winPct >= 0.65f)
            {
                CurrentSituation = TeamSituation.Championship;
            }
            else if (winPct >= 0.55f)
            {
                CurrentSituation = TeamSituation.Contending;
            }
            else if (winPct >= 0.45f)
            {
                CurrentSituation = TeamSituation.PlayoffBubble;
            }
            else if (winPct <= 0.30f || draftPosition <= 5)
            {
                CurrentSituation = TeamSituation.Rebuilding;
            }
            else
            {
                CurrentSituation = TeamSituation.StuckInMiddle;
            }

            // Update value preferences based on new situation
            ValuePreferences = TradeValuePreferences.ForSituation(CurrentSituation);
        }
    }

    /// <summary>
    /// Types of trades for categorization
    /// </summary>
    public enum TradeType
    {
        StarAcquisition,     // Getting a star player
        SalaryDump,          // Dumping bad contracts
        RolePlayerSwap,      // Trading role players
        DraftPickTrade,      // Primarily involving picks
        TankMove,            // Acquiring picks by trading away talent
        DepthMove,           // Adding bench depth
        ExpiringSalary       // Acquiring expiring contracts
    }

    /// <summary>
    /// Player trade availability status
    /// </summary>
    public enum TradeAvailability
    {
        Untouchable,         // Not available at any price
        HighPrice,           // Available but asking a lot
        Available,           // Listening to offers
        OnTheBlock,          // Actively shopping
        MustMove             // Desperate to trade (expiring, bad fit, etc.)
    }

    /// <summary>
    /// Player trade status and availability
    /// </summary>
    [Serializable]
    public class PlayerTradeStatus
    {
        public string PlayerId;
        public string PlayerName;
        public string TeamId;

        public TradeAvailability Availability;
        public string ReasonForAvailability;

        public bool HasRequestedTrade;
        public bool IsPubliclyKnown;          // Media knows about availability

        public List<string> PreferredDestinations;  // Teams player wants to go to
        public List<string> BlockedDestinations;    // Teams player won't go to

        public DateTime AvailableSince;
        public int DaysOnMarket => (DateTime.Now - AvailableSince).Days;

        /// <summary>
        /// Get asking price multiplier based on availability
        /// </summary>
        public float GetAskingPriceMultiplier()
        {
            return Availability switch
            {
                TradeAvailability.Untouchable => 999f,  // Won't trade
                TradeAvailability.HighPrice => 1.5f,
                TradeAvailability.Available => 1.0f,
                TradeAvailability.OnTheBlock => 0.85f,
                TradeAvailability.MustMove => 0.6f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Check if player would accept trade to destination
        /// </summary>
        public bool WouldAcceptDestination(string destinationTeamId)
        {
            if (BlockedDestinations?.Contains(destinationTeamId) == true)
                return false;

            // If no NTC, player can be traded anywhere
            // PreferredDestinations is just what they'd prefer
            return true;
        }
    }
}
