using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Offseason phase progression
    /// </summary>
    [Serializable]
    public enum OffseasonPhase
    {
        PostSeason,             // Immediately after playoffs/finals
        DraftLottery,           // Lottery for non-playoff teams
        DraftCombine,           // Pre-draft workouts
        Draft,                  // NBA Draft
        FreeAgencyMoratorium,   // Dead period before FA opens
        FreeAgencyEarly,        // First few days - max deals
        FreeAgencyMain,         // Mid free agency
        FreeAgencyLate,         // Bargain hunting
        SummerLeague,           // Las Vegas Summer League
        TrainingCampPrep,       // Before training camp
        Complete                // Ready for new season
    }

    /// <summary>
    /// Types of contract extensions
    /// </summary>
    [Serializable]
    public enum ExtensionType
    {
        RookieScale,            // After 3rd year
        Veteran,                // Standard extension
        Supermax,               // Designated veteran extension
        EarlyBird,              // Early Bird rights
        NonBird,                // Non-Bird rights
        TwoWay                  // Two-way contract conversion
    }

    /// <summary>
    /// Result of extension negotiation
    /// </summary>
    [Serializable]
    public class ExtensionResult
    {
        public string PlayerId;
        public string PlayerName;
        public ExtensionType Type;
        public bool Accepted;
        public int Years;
        public float TotalValue;
        public float AnnualValue;
        public List<string> Incentives = new List<string>();
        public string RejectionReason;
        public bool PlayerWantsToTestMarket;
    }

    /// <summary>
    /// Contract extension offer
    /// </summary>
    [Serializable]
    public class ExtensionOffer
    {
        public string PlayerId;
        public string TeamId;
        public ExtensionType Type;
        public int Years;
        public float TotalValue;
        public float AnnualValue;
        public bool IncludesPlayerOption;
        public bool IncludesTeamOption;
        public bool IncludesNoTradeClause;
        public bool IncludesTradeKicker;
        public float TradeKickerPercent;
        public List<string> Incentives = new List<string>();
        public DateTime OfferExpires;
    }

    /// <summary>
    /// Qualifying offer for restricted free agents
    /// </summary>
    [Serializable]
    public class QualifyingOffer
    {
        public string PlayerId;
        public string TeamId;
        public float Amount;
        public bool Extended;
        public DateTime Deadline;
        public bool Accepted;
        public bool Declined;
    }

    /// <summary>
    /// Tracking restricted free agent situation
    /// </summary>
    [Serializable]
    public class RestrictedFreeAgentStatus
    {
        public string PlayerId;
        public string OriginalTeamId;
        public QualifyingOffer QualifyingOffer;
        public List<FreeAgentOffer> OfferSheets = new List<FreeAgentOffer>();
        public FreeAgentOffer MatchedOffer;
        public bool TeamDeclinedToMatch;
        public DateTime MatchDeadline;
    }

    /// <summary>
    /// Contract offer during free agency
    /// </summary>
    [Serializable]
    public class FreeAgentOffer
    {
        public string OfferId;
        public string PlayerId;
        public string TeamId;
        public int Years;
        public float TotalValue;
        public float AnnualAverage;
        public List<float> YearlySalaries = new List<float>();
        public bool PlayerOption;
        public bool TeamOption;
        public int OptionYear;
        public bool NoTradeClause;
        public bool TradeKicker;
        public float TradeKickerPercent;
        public List<string> Incentives = new List<string>();
        public DateTime OfferDate;
        public DateTime ExpiresAt;
        public FreeAgentOfferStatus Status;
    }

    [Serializable]
    public enum FreeAgentOfferStatus
    {
        Pending,
        Accepted,
        Declined,
        Withdrawn,
        Expired,
        Matched       // For RFA offer sheets
    }

    /// <summary>
    /// Free agency meeting request
    /// </summary>
    [Serializable]
    public class FreeAgentMeeting
    {
        public string MeetingId;
        public string PlayerId;
        public string TeamId;
        public DateTime ScheduledTime;
        public int DurationMinutes;
        public bool Completed;
        public float ImpressionScore;       // How well the pitch went
        public List<string> PitchPoints = new List<string>();
        public List<string> PlayerConcerns = new List<string>();
    }

    /// <summary>
    /// Player's free agency priorities
    /// </summary>
    [Serializable]
    public class FreeAgentPriorities
    {
        public string PlayerId;

        [Range(0f, 1f)]
        public float MoneyImportance;
        [Range(0f, 1f)]
        public float WinningImportance;
        [Range(0f, 1f)]
        public float RoleImportance;
        [Range(0f, 1f)]
        public float LocationImportance;
        [Range(0f, 1f)]
        public float LoyaltyImportance;
        [Range(0f, 1f)]
        public float FamilyImportance;

        public List<string> PreferredCities = new List<string>();
        public List<string> PreferredTeammates = new List<string>();
        public string PreferredRole;
        public bool WantsMaxContract;
        public bool WillingToTakeDiscount;
        public float MaxDiscountPercent;
    }

    /// <summary>
    /// Offseason event for tracking
    /// </summary>
    [Serializable]
    public class OffseasonEvent
    {
        public string EventId;
        public DateTime Date;
        public OffseasonEventType Type;
        public string Headline;
        public string Details;
        public List<string> InvolvedTeamIds = new List<string>();
        public List<string> InvolvedPlayerIds = new List<string>();
    }

    [Serializable]
    public enum OffseasonEventType
    {
        DraftPick,
        FreeAgentSigning,
        ExtensionSigned,
        Trade,
        QualifyingOfferExtended,
        RFAOfferSheet,
        RFAMatched,
        PlayerRetirement,
        CoachHired,
        CoachFired,
        Waived,
        TwoWaySigned,
        InternationalSigning
    }

    /// <summary>
    /// Complete offseason summary
    /// </summary>
    [Serializable]
    public class OffseasonSummary
    {
        public int Season;
        public List<OffseasonEvent> Events = new List<OffseasonEvent>();
        public List<string> TopFreeAgentSignings = new List<string>();
        public List<string> BiggestSurprises = new List<string>();
        public string MostImprovedTeam;
        public string BiggestLoser;
        public Dictionary<string, float> TeamGradeChanges = new Dictionary<string, float>();
    }

    /// <summary>
    /// Manages entire offseason progression: Draft, Free Agency, Extensions
    /// </summary>
    public class OffseasonManager : MonoBehaviour
    {
        public static OffseasonManager Instance { get; private set; }

        [Header("Current Offseason State")]
        [SerializeField] private int currentSeason;
        [SerializeField] private OffseasonPhase currentPhase;
        [SerializeField] private DateTime currentDate;
        [SerializeField] private bool offseasonActive;

        [Header("Key Dates")]
        [SerializeField] private DateTime draftLotteryDate;
        [SerializeField] private DateTime draftCombineStart;
        [SerializeField] private DateTime draftDate;
        [SerializeField] private DateTime moratoriumStart;
        [SerializeField] private DateTime freeAgencyStart;
        [SerializeField] private DateTime summerLeagueStart;
        [SerializeField] private DateTime trainingCampStart;

        [Header("Extensions")]
        [SerializeField] private List<ExtensionOffer> pendingExtensions = new List<ExtensionOffer>();
        [SerializeField] private List<ExtensionResult> completedExtensions = new List<ExtensionResult>();
        [SerializeField] private DateTime extensionDeadline;

        [Header("Free Agency")]
        [SerializeField] private List<string> unrestricted​FreeAgentIds = new List<string>();
        [SerializeField] private List<RestrictedFreeAgentStatus> restrictedFreeAgents = new List<RestrictedFreeAgentStatus>();
        [SerializeField] private List<FreeAgentMeeting> scheduledMeetings = new List<FreeAgentMeeting>();
        [SerializeField] private List<FreeAgentOffer> activeOffers = new List<FreeAgentOffer>();

        [Header("Events")]
        [SerializeField] private List<OffseasonEvent> offseasonEvents = new List<OffseasonEvent>();

        // Events
        public event Action<OffseasonPhase> OnPhaseChanged;
        public event Action<ExtensionResult> OnExtensionCompleted;
        public event Action<FreeAgentOffer> OnFreeAgentSigned;
        public event Action<RestrictedFreeAgentStatus> OnRFAMatched;
        public event Action<OffseasonEvent> OnOffseasonEvent;
        public event Action<OffseasonSummary> OnOffseasonComplete;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initialize offseason with key dates
        /// </summary>
        public void StartOffseason(int season, DateTime seasonEndDate)
        {
            currentSeason = season;
            offseasonActive = true;
            currentPhase = OffseasonPhase.PostSeason;
            currentDate = seasonEndDate;

            // Set key dates based on typical NBA offseason calendar
            draftLotteryDate = new DateTime(season, 5, 14);
            draftCombineStart = new DateTime(season, 5, 16);
            draftDate = new DateTime(season, 6, 22);
            moratoriumStart = new DateTime(season, 6, 30);
            freeAgencyStart = new DateTime(season, 7, 6);
            summerLeagueStart = new DateTime(season, 7, 7);
            extensionDeadline = new DateTime(season, 10, 21);
            trainingCampStart = new DateTime(season, 9, 27);

            offseasonEvents.Clear();
            pendingExtensions.Clear();
            completedExtensions.Clear();
            activeOffers.Clear();

            // Identify free agents
            IdentifyFreeAgents();

            Debug.Log($"Offseason {season} started. Draft: {draftDate:MMM dd}, FA opens: {freeAgencyStart:MMM dd}");
        }

        /// <summary>
        /// Advance to next offseason phase
        /// </summary>
        public void AdvancePhase()
        {
            var previousPhase = currentPhase;

            currentPhase = currentPhase switch
            {
                OffseasonPhase.PostSeason => OffseasonPhase.DraftLottery,
                OffseasonPhase.DraftLottery => OffseasonPhase.DraftCombine,
                OffseasonPhase.DraftCombine => OffseasonPhase.Draft,
                OffseasonPhase.Draft => OffseasonPhase.FreeAgencyMoratorium,
                OffseasonPhase.FreeAgencyMoratorium => OffseasonPhase.FreeAgencyEarly,
                OffseasonPhase.FreeAgencyEarly => OffseasonPhase.FreeAgencyMain,
                OffseasonPhase.FreeAgencyMain => OffseasonPhase.FreeAgencyLate,
                OffseasonPhase.FreeAgencyLate => OffseasonPhase.SummerLeague,
                OffseasonPhase.SummerLeague => OffseasonPhase.TrainingCampPrep,
                OffseasonPhase.TrainingCampPrep => OffseasonPhase.Complete,
                _ => OffseasonPhase.Complete
            };

            // Update current date based on phase
            UpdateDateForPhase();

            OnPhaseChanged?.Invoke(currentPhase);

            if (currentPhase == OffseasonPhase.Complete)
            {
                CompleteOffseason();
            }

            Debug.Log($"Offseason advanced: {previousPhase} → {currentPhase}");
        }

        private void UpdateDateForPhase()
        {
            currentDate = currentPhase switch
            {
                OffseasonPhase.DraftLottery => draftLotteryDate,
                OffseasonPhase.DraftCombine => draftCombineStart,
                OffseasonPhase.Draft => draftDate,
                OffseasonPhase.FreeAgencyMoratorium => moratoriumStart,
                OffseasonPhase.FreeAgencyEarly => freeAgencyStart,
                OffseasonPhase.FreeAgencyMain => freeAgencyStart.AddDays(7),
                OffseasonPhase.FreeAgencyLate => freeAgencyStart.AddDays(21),
                OffseasonPhase.SummerLeague => summerLeagueStart,
                OffseasonPhase.TrainingCampPrep => trainingCampStart,
                _ => currentDate
            };
        }

        #region Extension Management

        /// <summary>
        /// Create extension offer for a player
        /// </summary>
        public ExtensionOffer CreateExtensionOffer(string playerId, string teamId, ExtensionType type,
            int years, float totalValue, bool playerOption = false, bool noTradeClause = false)
        {
            var offer = new ExtensionOffer
            {
                PlayerId = playerId,
                TeamId = teamId,
                Type = type,
                Years = years,
                TotalValue = totalValue,
                AnnualValue = totalValue / years,
                IncludesPlayerOption = playerOption,
                IncludesNoTradeClause = noTradeClause,
                OfferExpires = extensionDeadline
            };

            pendingExtensions.Add(offer);
            return offer;
        }

        /// <summary>
        /// Process extension offer response
        /// </summary>
        public ExtensionResult ProcessExtensionResponse(ExtensionOffer offer, FreeAgentPriorities priorities)
        {
            var result = new ExtensionResult
            {
                PlayerId = offer.PlayerId,
                Type = offer.Type,
                Years = offer.Years,
                TotalValue = offer.TotalValue,
                AnnualValue = offer.AnnualValue,
                Incentives = offer.Incentives
            };

            // Calculate acceptance probability
            float acceptChance = CalculateExtensionAcceptance(offer, priorities);

            if (UnityEngine.Random.value < acceptChance)
            {
                result.Accepted = true;
                RecordOffseasonEvent(OffseasonEventType.ExtensionSigned,
                    $"{result.PlayerName} signs extension",
                    $"{result.Years} years, ${result.TotalValue:F1}M",
                    new List<string> { offer.TeamId },
                    new List<string> { offer.PlayerId });
            }
            else
            {
                result.Accepted = false;
                result.PlayerWantsToTestMarket = priorities.MoneyImportance > 0.7f ||
                                                  priorities.WinningImportance > 0.8f;
                result.RejectionReason = DetermineRejectionReason(offer, priorities);
            }

            completedExtensions.Add(result);
            pendingExtensions.Remove(offer);

            OnExtensionCompleted?.Invoke(result);
            return result;
        }

        private float CalculateExtensionAcceptance(ExtensionOffer offer, FreeAgentPriorities priorities)
        {
            float acceptance = 0.5f;

            // Money factor
            float marketValue = EstimateMarketValue(offer.PlayerId);
            float valueRatio = offer.AnnualValue / marketValue;

            if (valueRatio >= 1.0f)
                acceptance += 0.25f * priorities.MoneyImportance;
            else if (valueRatio >= 0.9f)
                acceptance += 0.1f * priorities.MoneyImportance;
            else
                acceptance -= 0.2f * priorities.MoneyImportance;

            // Loyalty factor
            acceptance += 0.15f * priorities.LoyaltyImportance;

            // Contract length preference
            if (offer.Years >= 4)
                acceptance += 0.1f;

            // Player option bonus
            if (offer.IncludesPlayerOption)
                acceptance += 0.1f;

            // No trade clause value
            if (offer.IncludesNoTradeClause)
                acceptance += 0.05f;

            // Young players may want to test market
            if (offer.Type == ExtensionType.RookieScale && priorities.MoneyImportance > 0.6f)
                acceptance -= 0.15f;

            return Mathf.Clamp01(acceptance);
        }

        private string DetermineRejectionReason(ExtensionOffer offer, FreeAgentPriorities priorities)
        {
            float marketValue = EstimateMarketValue(offer.PlayerId);

            if (offer.AnnualValue < marketValue * 0.85f)
                return "Seeking higher annual value";
            if (priorities.WinningImportance > 0.8f)
                return "Wants to explore championship contenders";
            if (offer.Years < 3)
                return "Seeking longer term security";
            if (!offer.IncludesPlayerOption && priorities.MoneyImportance > 0.6f)
                return "Wants player option for flexibility";

            return "Wants to test free agency market";
        }

        private float EstimateMarketValue(string playerId)
        {
            // Would integrate with player evaluation system
            return UnityEngine.Random.Range(10f, 45f);
        }

        #endregion

        #region Free Agency Management

        /// <summary>
        /// Identify all free agents at start of offseason
        /// </summary>
        private void IdentifyFreeAgents()
        {
            // Would integrate with contract system
            // Placeholder - creates sample free agents
            unrestricted​FreeAgentIds.Clear();
            restrictedFreeAgents.Clear();
        }

        /// <summary>
        /// Extend qualifying offer to make player RFA
        /// </summary>
        public QualifyingOffer ExtendQualifyingOffer(string playerId, string teamId, float amount)
        {
            var qo = new QualifyingOffer
            {
                PlayerId = playerId,
                TeamId = teamId,
                Amount = amount,
                Extended = true,
                Deadline = freeAgencyStart.AddDays(-1)
            };

            var rfaStatus = new RestrictedFreeAgentStatus
            {
                PlayerId = playerId,
                OriginalTeamId = teamId,
                QualifyingOffer = qo
            };

            restrictedFreeAgents.Add(rfaStatus);

            RecordOffseasonEvent(OffseasonEventType.QualifyingOfferExtended,
                "Qualifying offer extended",
                $"${amount:F1}M qualifying offer",
                new List<string> { teamId },
                new List<string> { playerId });

            return qo;
        }

        /// <summary>
        /// Schedule a free agent meeting
        /// </summary>
        public FreeAgentMeeting ScheduleMeeting(string playerId, string teamId, DateTime time, List<string> pitchPoints)
        {
            var meeting = new FreeAgentMeeting
            {
                MeetingId = Guid.NewGuid().ToString(),
                PlayerId = playerId,
                TeamId = teamId,
                ScheduledTime = time,
                DurationMinutes = 60,
                PitchPoints = pitchPoints
            };

            scheduledMeetings.Add(meeting);
            return meeting;
        }

        /// <summary>
        /// Conduct free agent meeting
        /// </summary>
        public void ConductMeeting(FreeAgentMeeting meeting, FreeAgentPriorities playerPriorities)
        {
            meeting.Completed = true;

            float impression = 0.5f;

            // Evaluate pitch points against player priorities
            foreach (var pitch in meeting.PitchPoints)
            {
                if (pitch.Contains("money") && playerPriorities.MoneyImportance > 0.6f)
                    impression += 0.1f;
                if (pitch.Contains("championship") && playerPriorities.WinningImportance > 0.6f)
                    impression += 0.15f;
                if (pitch.Contains("role") && playerPriorities.RoleImportance > 0.6f)
                    impression += 0.1f;
                if (pitch.Contains("family") && playerPriorities.FamilyImportance > 0.5f)
                    impression += 0.1f;
            }

            // Generate player concerns
            if (playerPriorities.WinningImportance > 0.7f)
                meeting.PlayerConcerns.Add("Wants to know championship timeline");
            if (playerPriorities.RoleImportance > 0.6f)
                meeting.PlayerConcerns.Add("Curious about expected role and minutes");
            if (playerPriorities.LocationImportance > 0.5f)
                meeting.PlayerConcerns.Add("Has questions about living in the city");

            meeting.ImpressionScore = Mathf.Clamp01(impression + UnityEngine.Random.Range(-0.1f, 0.1f));
        }

        /// <summary>
        /// Make contract offer to free agent
        /// </summary>
        public FreeAgentOffer MakeOffer(string playerId, string teamId, int years, float totalValue,
            bool playerOption = false, int optionYear = 0, bool noTradeClause = false)
        {
            var offer = new FreeAgentOffer
            {
                OfferId = Guid.NewGuid().ToString(),
                PlayerId = playerId,
                TeamId = teamId,
                Years = years,
                TotalValue = totalValue,
                AnnualAverage = totalValue / years,
                PlayerOption = playerOption,
                OptionYear = optionYear,
                NoTradeClause = noTradeClause,
                OfferDate = currentDate,
                ExpiresAt = currentDate.AddDays(3),
                Status = FreeAgentOfferStatus.Pending
            };

            // Generate yearly salaries with 8% annual raises
            float baseSalary = offer.AnnualAverage * 0.92f;
            for (int i = 0; i < years; i++)
            {
                offer.YearlySalaries.Add(baseSalary * Mathf.Pow(1.08f, i));
            }

            activeOffers.Add(offer);

            // Check if this is offer sheet for RFA
            var rfaStatus = restrictedFreeAgents.FirstOrDefault(r => r.PlayerId == playerId);
            if (rfaStatus != null && teamId != rfaStatus.OriginalTeamId)
            {
                rfaStatus.OfferSheets.Add(offer);
                rfaStatus.MatchDeadline = currentDate.AddDays(2);

                RecordOffseasonEvent(OffseasonEventType.RFAOfferSheet,
                    "Offer sheet signed",
                    $"{years} years, ${totalValue:F1}M offer sheet",
                    new List<string> { teamId, rfaStatus.OriginalTeamId },
                    new List<string> { playerId });
            }

            return offer;
        }

        /// <summary>
        /// Process free agent's decision on offers
        /// </summary>
        public FreeAgentOffer ProcessFreeAgentDecision(string playerId, FreeAgentPriorities priorities)
        {
            var offers = activeOffers.Where(o => o.PlayerId == playerId && o.Status == FreeAgentOfferStatus.Pending).ToList();

            if (offers.Count == 0) return null;

            // Score each offer
            var scoredOffers = offers.Select(o => new
            {
                Offer = o,
                Score = ScoreOffer(o, priorities)
            }).OrderByDescending(x => x.Score).ToList();

            // Best offer wins (with some randomness)
            var bestOffer = scoredOffers.First().Offer;

            // Small chance player takes surprise offer if scores are close
            if (scoredOffers.Count > 1 &&
                scoredOffers[1].Score > scoredOffers[0].Score * 0.95f &&
                UnityEngine.Random.value < 0.2f)
            {
                bestOffer = scoredOffers[1].Offer;
            }

            // Accept best offer, decline others
            foreach (var offer in offers)
            {
                offer.Status = offer == bestOffer ? FreeAgentOfferStatus.Accepted : FreeAgentOfferStatus.Declined;
            }

            RecordOffseasonEvent(OffseasonEventType.FreeAgentSigning,
                "Free agent signing",
                $"{bestOffer.Years} years, ${bestOffer.TotalValue:F1}M",
                new List<string> { bestOffer.TeamId },
                new List<string> { playerId });

            OnFreeAgentSigned?.Invoke(bestOffer);
            return bestOffer;
        }

        private float ScoreOffer(FreeAgentOffer offer, FreeAgentPriorities priorities)
        {
            float score = 0f;

            // Money
            float marketValue = EstimateMarketValue(offer.PlayerId);
            float valueRatio = offer.AnnualAverage / marketValue;
            score += valueRatio * priorities.MoneyImportance * 40f;

            // Team winning potential (would integrate with team evaluation)
            float teamWinPotential = GetTeamWinningPotential(offer.TeamId);
            score += teamWinPotential * priorities.WinningImportance * 30f;

            // Role importance (would integrate with depth chart)
            float roleScore = GetRoleScore(offer.PlayerId, offer.TeamId);
            score += roleScore * priorities.RoleImportance * 15f;

            // Location
            if (priorities.PreferredCities.Contains(GetTeamCity(offer.TeamId)))
                score += 10f * priorities.LocationImportance;

            // Contract flexibility
            if (offer.PlayerOption)
                score += 5f;
            if (offer.NoTradeClause)
                score += 3f;

            // Years security
            score += offer.Years * 2f;

            return score;
        }

        /// <summary>
        /// Match RFA offer sheet
        /// </summary>
        public void MatchOfferSheet(RestrictedFreeAgentStatus rfaStatus, FreeAgentOffer offerSheet)
        {
            offerSheet.Status = FreeAgentOfferStatus.Matched;
            rfaStatus.MatchedOffer = offerSheet;

            RecordOffseasonEvent(OffseasonEventType.RFAMatched,
                "Offer sheet matched",
                $"{rfaStatus.OriginalTeamId} matches ${offerSheet.TotalValue:F1}M offer",
                new List<string> { rfaStatus.OriginalTeamId, offerSheet.TeamId },
                new List<string> { rfaStatus.PlayerId });

            OnRFAMatched?.Invoke(rfaStatus);
        }

        /// <summary>
        /// Decline to match RFA offer sheet
        /// </summary>
        public void DeclineToMatch(RestrictedFreeAgentStatus rfaStatus, FreeAgentOffer offerSheet)
        {
            rfaStatus.TeamDeclinedToMatch = true;
            offerSheet.Status = FreeAgentOfferStatus.Accepted;

            RecordOffseasonEvent(OffseasonEventType.FreeAgentSigning,
                "RFA signs offer sheet",
                $"{offerSheet.TeamId} signs RFA after match declined",
                new List<string> { offerSheet.TeamId, rfaStatus.OriginalTeamId },
                new List<string> { rfaStatus.PlayerId });

            OnFreeAgentSigned?.Invoke(offerSheet);
        }

        #endregion

        #region Helper Methods

        private float GetTeamWinningPotential(string teamId)
        {
            // Would integrate with team evaluation
            return UnityEngine.Random.Range(0.3f, 1.0f);
        }

        private float GetRoleScore(string playerId, string teamId)
        {
            // Would integrate with depth chart analysis
            return UnityEngine.Random.Range(0.4f, 1.0f);
        }

        private string GetTeamCity(string teamId)
        {
            // Would integrate with team data
            return "City";
        }

        private void RecordOffseasonEvent(OffseasonEventType type, string headline, string details,
            List<string> teamIds, List<string> playerIds)
        {
            var ev = new OffseasonEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Date = currentDate,
                Type = type,
                Headline = headline,
                Details = details,
                InvolvedTeamIds = teamIds,
                InvolvedPlayerIds = playerIds
            };

            offseasonEvents.Add(ev);
            OnOffseasonEvent?.Invoke(ev);
        }

        #endregion

        /// <summary>
        /// Complete offseason and generate summary
        /// </summary>
        private void CompleteOffseason()
        {
            offseasonActive = false;

            var summary = new OffseasonSummary
            {
                Season = currentSeason,
                Events = new List<OffseasonEvent>(offseasonEvents)
            };

            // Identify top signings
            var topSignings = offseasonEvents
                .Where(e => e.Type == OffseasonEventType.FreeAgentSigning)
                .Take(10)
                .Select(e => e.Headline)
                .ToList();
            summary.TopFreeAgentSignings = topSignings;

            OnOffseasonComplete?.Invoke(summary);
        }

        /// <summary>
        /// Get all events for a specific team
        /// </summary>
        public List<OffseasonEvent> GetTeamEvents(string teamId)
        {
            return offseasonEvents.Where(e => e.InvolvedTeamIds.Contains(teamId)).ToList();
        }

        /// <summary>
        /// Get all pending offers for a player
        /// </summary>
        public List<FreeAgentOffer> GetPendingOffers(string playerId)
        {
            return activeOffers.Where(o => o.PlayerId == playerId && o.Status == FreeAgentOfferStatus.Pending).ToList();
        }

        /// <summary>
        /// Get current offseason phase
        /// </summary>
        public OffseasonPhase CurrentPhase => currentPhase;

        /// <summary>
        /// Check if offseason is active
        /// </summary>
        public bool IsActive => offseasonActive;

        /// <summary>
        /// Get days until free agency
        /// </summary>
        public int DaysUntilFreeAgency => (freeAgencyStart - currentDate).Days;

        /// <summary>
        /// Get all unrestricted free agents
        /// </summary>
        public List<string> UnrestrictedFreeAgents => unrestricted​FreeAgentIds;

        /// <summary>
        /// Get all restricted free agents
        /// </summary>
        public List<RestrictedFreeAgentStatus> RestrictedFreeAgents => restrictedFreeAgents;
    }
}
