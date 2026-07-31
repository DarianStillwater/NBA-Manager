using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Manager
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
        /// <summary>Final-season salary the QO amount was computed from.</summary>
        public long PriorSalary;
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
        // Dollars, not millions — the market compares these against cap figures.
        public long TotalValue;
        public long AnnualAverage;
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
        /// <summary>Cap mechanism the offer is made under (cap space, MLE, BAE, min).</summary>
        public SigningMethod Method;
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
    public class OffseasonManager : ISeasonPhaseListener, IDailyTickable, ISaveSection
    {
        public static OffseasonManager Instance { get; private set; }

        public string SystemId => "Offseason";
        public int TickOrder => Manager.TickOrder.Offseason;

        /// <summary>
        /// Activation happens when the champion is crowned (GameManager hands the
        /// season's closing data to BeginOffseason); nothing to do on phase entry.
        /// </summary>
        public void OnSeasonPhaseChanged(Data.SeasonPhase oldPhase, Data.SeasonPhase newPhase, System.DateTime date)
        {
        }

        // ==================== ORCHESTRATION (the real offseason) ====================
        // Date-driven, flag-idempotent stage runner. Each stage runs once when the
        // calendar reaches it; a mid-offseason load self-heals because unfinished
        // stages simply run on the next tick.

        private bool _engineActive;
        private int _seasonLabel;      // the season that just ended (e.g. 2025 = Oct 25–Jun 26)
        private int _calendarYear;     // the summer's calendar year (seasonLabel + 1)
        private bool _postSeasonDone;
        private bool _draftDone;
        private bool _freeAgencyOpen;
        private bool _summerDone;
        private bool _campStarted;
        private int _scrimmagesPlayed;
        private readonly List<string> _scrimmageLines = new List<string>();
        /// <summary>
        /// The player's three playable camp exhibitions, live on the season schedule so
        /// the normal game-day loop (PreGame → play/sim → PostGame) picks them up.
        /// </summary>
        private readonly List<Data.CalendarEvent> _preseasonEvents = new List<Data.CalendarEvent>();
        /// <summary>The player's chosen daily training focus during camp (UI-set).</summary>
        public TrainingFocus CampFocus = TrainingFocus.TeamBuilding;
        /// <summary>Most recent camp day report, for the Front Office camp card.</summary>
        public CampDayReport LastCampReport { get; private set; }
        public IReadOnlyList<string> ScrimmageLines => _scrimmageLines;
        public bool CampInProgress => _campStarted && !_campDone;
        private bool _campDone;
        private System.Random _rng = new System.Random();

        // Draft-night state (interactive: the night halts while you're on the clock)
        private DraftSystem _draft;
        private bool _draftStarted;
        private bool _onTheClock;
        private int _nextPick = 1;
        private DateTime _draftDay;
        private List<string> _draftOrder1 = new List<string>();
        private List<string> _draftOrder2 = new List<string>();
        // O4: which ORIGINAL team's pick sits at each slot. The slot order is fixed on
        // draft night; _draftOrderN is a derived view of who owns each slot right now.
        private List<string> _slotOrder1 = new List<string>();
        private List<string> _slotOrder2 = new List<string>();
        private readonly List<string> _playerPickResults = new List<string>();
        /// <summary>Draft-night trade-up offers carry this OfferId prefix so they can be found again.</summary>
        private const string OnClockOfferPrefix = "draftup-";

        // Pre-draft workouts (O3): your invites, pending until draft day auto-fills
        public const int MaxWorkoutInvites = 6;
        private readonly List<string> _workoutInvites = new List<string>();
        private bool _workoutsOpened;
        private bool _workoutsDone;

        // ==================== PUBLIC STATE (Front Office panel) ====================

        public bool EngineActive => _engineActive;
        public int OffseasonCalendarYear => _calendarYear;
        public bool DraftActive => _engineActive && _draftStarted && !_draftDone;
        public bool PlayerOnClock => DraftActive && _onTheClock;
        public int NextPickNumber => _nextPick;
        public DraftSystem DraftBoard => _draft;
        public bool FreeAgencySigningOpen => _engineActive && _freeAgencyOpen && !_campDone;

        /// <summary>Prospects you've brought in for a workout this summer.</summary>
        public IReadOnlyList<string> WorkoutInvites => _workoutInvites;
        public int WorkoutInvitesRemaining => Math.Max(0, MaxWorkoutInvites - _workoutInvites.Count);

        /// <summary>Jun 10 through draft day: the invite window.</summary>
        public bool WorkoutsOpen(DateTime date) =>
            _engineActive && !_workoutsDone && !_draftStarted && !_draftDone &&
            date.Date >= OffseasonDates.Workouts(_calendarYear).Date &&
            date.Date <= OffseasonDates.Draft(_calendarYear).Date;

        /// <summary>The season label whose draft class is on the board this summer.</summary>
        public int SeasonLabel => _seasonLabel;
        public bool ReSignWindowOpen => _engineActive && _postSeasonDone && !_campDone;

        // ==================== FREE AGENCY MARKET (O2) ====================

        private FreeAgencyMarket _market;
        private readonly List<QualifyingOffer> _pendingQOs = new List<QualifyingOffer>();

        /// <summary>The live July market: bids, offer sheets, agent intel.</summary>
        public FreeAgencyMarket Market => _market;

        /// <summary>Qualifying offers awaiting YOUR tender/withhold call.</summary>
        public IReadOnlyList<QualifyingOffer> PendingQualifyingOffers => _pendingQOs;

        /// <summary>
        /// Tender or withhold a qualifying offer on one of your expiring players.
        /// Withholding leaves him unrestricted. Ignoring it until the deadline lets
        /// the front office decide (tender anyone worth more than the QO).
        /// </summary>
        public bool SubmitQualifyingOfferDecision(GameManager gm, string playerId, bool tender,
            out string failReason)
        {
            failReason = "";
            var qo = _pendingQOs.FirstOrDefault(q => q.PlayerId == playerId);
            if (qo == null) { failReason = "No qualifying-offer decision pending."; return false; }

            if (tender && gm?.FreeAgents?.ExtendQualifyingOffer(qo.TeamId, playerId, qo.PriorSalary) != true)
            { failReason = "He's no longer eligible for a qualifying offer."; return false; }

            _pendingQOs.Remove(qo);
            return true;
        }

        /// <summary>Deadline day: auto-tender anyone worth more than his QO.</summary>
        private void AutoResolveQualifyingOffers(GameManager gm)
        {
            foreach (var qo in _pendingQOs.ToList())
            {
                var player = gm.PlayerDatabase?.GetPlayer(qo.PlayerId);
                if (player != null && MarketValue(player) >= (long)qo.Amount)
                    gm.FreeAgents?.ExtendQualifyingOffer(qo.TeamId, qo.PlayerId, qo.PriorSalary);
            }
            if (_pendingQOs.Count > 0)
                InboxService.Instance?.Publish(InboxMessageType.League, Data.RolePermissions.AIGMName,
                    "Qualifying offers filed",
                    "The deadline passed with decisions outstanding, so we tendered everyone worth more " +
                    "than his qualifying offer and let the rest walk.");
            _pendingQOs.Clear();
        }

        /// <summary>Builds the market on first use (and after a load).</summary>
        private FreeAgencyMarket EnsureMarket(GameManager gm)
        {
            if (_market != null || gm?.FreeAgents == null) return _market;
            _market = new FreeAgencyMarket(gm.FreeAgents, gm.SalaryCapManager, gm.PlayerDatabase,
                () => gm.AllTeams, () => gm.PlayerTeamId, seed: _seasonLabel * 131 + 11,
                today: gm.CurrentDate);
            return _market;
        }

        /// <summary>
        /// Start the real offseason. Called by GameManager right after the champion
        /// is crowned and the season's awards are voted.
        /// </summary>
        public void BeginOffseason(int seasonLabel, DateTime seasonEndDate)
        {
            _engineActive = true;
            _seasonLabel = seasonLabel;
            _calendarYear = seasonLabel + 1;
            _postSeasonDone = _draftDone = _freeAgencyOpen = _summerDone = _campDone = false;
            _draftStarted = _onTheClock = false;   // without this, summer #2+ never starts a draft
            _draft = null;
            _slotOrder1.Clear(); _slotOrder2.Clear();
            _rng = new System.Random(seasonLabel * 31 + 7);
            _market = null;              // fresh market each summer
            _pendingQOs.Clear();
            _preseasonEvents.Clear();
            _workoutInvites.Clear();
            _workoutsOpened = _workoutsDone = false;

            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                $"The {_seasonLabel} season is in the books",
                $"Draft night: Jun 22 · Free agency opens: Jul 6 · Camp: late Sep · New season: Oct 21.");

            Debug.Log($"[Offseason] Engine started for summer {_calendarYear}");
        }

        public void DailyTick(in DailyTickContext ctx)
        {
            if (!_engineActive) return;
            var gm = ctx.Game;
            if (gm == null) return;
            var date = ctx.Date;

            try
            {
                if (!_postSeasonDone) { RunPostSeason(gm); _postSeasonDone = true; }

                // Strictly after the stated deadline, so the deadline day is usable
                if (_pendingQOs.Count > 0 &&
                    date.Date > OffseasonDates.QualifyingOfferDeadline(_calendarYear).Date)
                    AutoResolveQualifyingOffers(gm);

                if (!_workoutsOpened && !_draftDone && date >= OffseasonDates.Workouts(_calendarYear))
                { OpenWorkouts(gm); _workoutsOpened = true; }

                if (!_draftDone && date >= OffseasonDates.Draft(_calendarYear))
                {
                    if (!_workoutsDone) RunRemainingWorkouts(gm);
                    if (!_draftStarted) StartDraftNight(gm, date);
                    // Advancing past draft night with a pick pending = the clock ran
                    // out; the war room picks best-available and the night resumes.
                    if (_onTheClock && date.Date > _draftDay.Date)
                        AutoPickPending(gm);
                    if (!_onTheClock) ContinueDraft(gm);
                }

                if (_draftDone && !_freeAgencyOpen && date >= OffseasonDates.FreeAgency(_calendarYear))
                { _freeAgencyOpen = true; OpenFreeAgency(gm); }

                if (_freeAgencyOpen && !_campDone)
                    RunDailyFreeAgency(gm, date);

                // Oct 1: leftover tendered RFAs take the qualifying offer rather than rot
                if (_freeAgencyOpen && !_campDone &&
                    date.Date >= OffseasonDates.RfaQualifyingOfferAccept(_calendarYear).Date)
                    AcceptLeftoverQualifyingOffers(gm);

                if (!_summerDone && date >= OffseasonDates.SummerLeague(_calendarYear))
                { RunSummerLeague(gm); _summerDone = true; }

                if (!_campDone && date >= OffseasonDates.CampStart(_calendarYear))
                {
                    if (!_campStarted) { StartCamp(gm); _campStarted = true; }
                    RunCampDay(gm, date);
                    if (date >= OffseasonDates.CampEnd(_calendarYear))
                    { FinishCamp(gm); _campDone = true; }
                }

                if (_campDone && date >= OffseasonDates.Rollover(_calendarYear))
                    Rollover(gm);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Offseason] Stage processing failed: {ex}");
            }
        }

        /// <summary>
        /// Immediately after the season: archive history, run development/aging,
        /// evaluate retirements, advance contracts, and build the free-agent pool.
        /// </summary>
        private void RunPostSeason(GameManager gm)
        {
            var players = gm.PlayerDatabase.GetAllPlayers();
            var inbox = InboxService.Instance;

            // Close the financial books while final records are still on the teams
            try { gm.FinanceSystem?.ProcessSeasonEnd(gm); }
            catch (Exception ex) { Debug.LogWarning($"[Offseason] Finance close failed: {ex.Message}"); }

            // Career stakes: the player's season lands in their record, staff ages a
            // year, and the owner delivers the verdict — which can be a firing
            try { gm.CareerStakes?.ProcessSeasonEnd(gm); }
            catch (Exception ex) { Debug.LogWarning($"[Offseason] Career evaluation failed: {ex.Message}"); }

            // Snapshot stats for next season's Most Improved comparison
            AwardManager.StorePreviousSeasonStats(players);

            // League history archive (champion, standings, leaders)
            var awards = gm.Awards?.GetForSeason(gm.CurrentSeason);
            try
            {
                gm.HistoryManager?.ArchiveSeason(_seasonLabel, gm.AllTeams, players,
                    _lastVotingResults, awards?.ChampionTeamId, awards?.FinalsMvpId,
                    PlayoffManager.Instance?.CurrentBracket);
            }
            catch (Exception ex) { Debug.LogWarning($"[Offseason] History archive failed: {ex.Message}"); }

            // Development: season growth from minutes, offseason programs, then aging
            var dev = gm.Development;
            if (dev != null)
            {
                dev.SetPlayerDatabase(gm.PlayerDatabase);
                dev.SetCurrentSeason(_seasonLabel);
                int developed = 0, declined = 0;
                var coachingByTeam = new Dictionary<string, float>();
                float CoachingFor(string teamId)
                {
                    if (string.IsNullOrEmpty(teamId)) return 0.5f;
                    if (!coachingByTeam.TryGetValue(teamId, out float q))
                    {
                        q = PersonnelManager.Instance?.GetDevelopmentQuality(teamId) ?? 0.5f;
                        coachingByTeam[teamId] = q;
                    }
                    return q;
                }
                foreach (var p in players)
                {
                    if (p == null || p.RetirementYear > 0) continue;
                    float coaching = CoachingFor(p.TeamId);
                    var grow = dev.ProcessSeasonDevelopment(p, p.MinutesPlayedThisSeason, coaching);
                    var off = dev.ProcessOffseasonDevelopment(p, coaching, 0.5f);
                    if (grow?.HasChanges == true || off?.HasChanges == true) developed++;
                    var age = dev.ApplyAgingEffects(p);
                    if (age?.HasChanges == true) declined++;
                }
                Debug.Log($"[Offseason] Development: {developed} improved, {declined} declined");
            }

            // Retirements
            var retiredIds = EvaluateAndApplyRetirements(gm);

            // Contract advancement -> expiring players hit free agency
            var expired = gm.SalaryCapManager.AdvanceContractYears();
            int toMarket = 0, tendered = 0;
            var fam0 = gm.FreeAgents;
            int seasonYear = gm.CurrentDate.Year;
            foreach (var contract in expired)
            {
                if (retiredIds.Contains(contract.PlayerId)) continue;
                var player = gm.PlayerDatabase.GetPlayer(contract.PlayerId);
                if (player == null) continue;

                fam0?.AddFreeAgent(contract.PlayerId, FreeAgentType.Unrestricted,
                    contract.TeamId, contract.ConsecutiveSeasonsWithTeam);

                // Restricted free agency: rookie-scale expirations and short-service
                // players get a qualifying offer if they're worth more than the QO.
                // AI teams decide now; YOUR calls become pending decisions until the
                // Jun 29 deadline auto-resolves them.
                if (fam0 != null && !string.IsNullOrEmpty(contract.TeamId) &&
                    IsRfaEligible(player, contract, seasonYear))
                {
                    long qo = fam0.ComputeQualifyingOfferAmount(player, contract.CurrentYearSalary);
                    bool mine = contract.TeamId == gm.PlayerTeamId &&
                                Data.RolePermissions.CanMakeRosterMoves;
                    if (mine)
                        _pendingQOs.Add(new QualifyingOffer
                        {
                            PlayerId = contract.PlayerId,
                            TeamId = contract.TeamId,
                            Amount = qo,
                            PriorSalary = contract.CurrentYearSalary,
                            Deadline = OffseasonDates.QualifyingOfferDeadline(_calendarYear)
                        });
                    else if (MarketValue(player) >= qo &&
                             fam0.ExtendQualifyingOffer(contract.TeamId, contract.PlayerId,
                                 contract.CurrentYearSalary))
                        tendered++;
                }

                RemoveFromRoster(gm, contract.TeamId, contract.PlayerId);
                player.TeamId = "";
                toMarket++;
            }

            inbox?.Publish(InboxMessageType.League, "League Office",
                $"Free-agent class takes shape: {toMarket} players hit the market",
                $"Contracts have expired across the league. {tendered} qualifying offers were tendered, " +
                "making those players restricted. Free agency opens July 6.");

            if (_pendingQOs.Count > 0)
                inbox?.Publish(InboxMessageType.League, "Front Office",
                    $"{_pendingQOs.Count} qualifying offer decision(s) on your desk",
                    "Tender to keep first refusal on a restricted free agent, or withhold and let him " +
                    $"walk unrestricted. Deadline {OffseasonDates.QualifyingOfferDeadline(_calendarYear):MMM d}.",
                    highPriority: true, deepLinkPanelId: "FrontOffice");
        }

        /// <summary>
        /// Whether an expiring player is subject to a restricted-free-agency
        /// qualifying offer rather than going straight to unrestricted.
        /// ponytail: undrafted players (DraftYear == 0) have no entry-year data
        /// to derive service from, so they're treated as UFA (service = int.MaxValue).
        /// </summary>
        internal static bool IsRfaEligible(Data.Player player, Data.Contract contract, int seasonYear)
        {
            if (player == null || contract == null) return false;
            int service = player.DraftYear > 0 ? seasonYear - player.DraftYear : int.MaxValue;
            return contract.Type == Data.ContractType.RookieScale || service <= 3;
        }

        private HashSet<string> EvaluateAndApplyRetirements(GameManager gm)
        {
            var retired = new HashSet<string>();
            var rm = gm.RetirementManager;
            if (rm == null) return retired;

            var candidates = gm.PlayerDatabase.GetAllPlayers()
                .Where(p => p != null && p.RetirementYear == 0 && p.Age >= 33 &&
                            !string.IsNullOrEmpty(p.TeamId))
                .ToList();
            if (candidates.Count == 0) return retired;

            Action<RetirementAnnouncement> collector = a => { if (a != null) retired.Add(a.PlayerId); };
            rm.OnRetirementAnnounced += collector;
            try
            {
                rm.EvaluateRetirements(candidates.Select(p => ToCareerData(gm, p)).ToList());
            }
            finally
            {
                rm.OnRetirementAnnounced -= collector;
            }

            foreach (var pid in retired)
            {
                var player = gm.PlayerDatabase.GetPlayer(pid);
                if (player == null) continue;

                player.RetirementYear = _seasonLabel;
                RemoveFromRoster(gm, player.TeamId, pid);
                gm.SalaryCapManager.RemoveContract(pid);
                string teamName = gm.GetTeam(player.TeamId)?.Name ?? "the league";
                player.TeamId = "";

                InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                    $"{player.FullName} announces retirement",
                    $"After {player.YearsPro} seasons and {player.CareerPoints:N0} career points, " +
                    $"{player.FullName} of the {teamName} is calling it a career.",
                    highPriority: player.OverallRating >= 85);
            }

            // Hall of Fame: fresh retirees join the eligibility list (5-year wait),
            // and this year's class gets voted on
            var history = gm.HistoryManager;
            if (history != null)
            {
                foreach (var pid in retired)
                    history.AddHallOfFameEligible(pid, _seasonLabel);

                try
                {
                    var everyone = gm.PlayerDatabase.GetAllPlayers();
                    var retirees = everyone.Where(p => p != null && p.RetirementYear > 0).ToList();
                    var inducted = history.VoteHallOfFame(_seasonLabel, everyone, retirees);
                    foreach (var hof in inducted)
                    {
                        InboxService.Instance?.Publish(InboxMessageType.League, "Hall of Fame",
                            $"{hof.PlayerName} elected to the Hall of Fame" +
                            (hof.IsFirstBallot ? " — first ballot" : ""),
                            $"{hof.PlayerName} enters the Hall with {hof.CareerPoints:N0} career points, " +
                            $"{hof.Championships} championship(s), {hof.MVPs} MVP(s), and {hof.AllStarSelections} All-Star nods.",
                            highPriority: true,
                            deepLinkPanelId: "History");
                    }
                }
                catch (Exception ex) { Debug.LogWarning($"[Offseason] HOF voting failed: {ex.Message}"); }
            }

            if (retired.Count > 0)
                Debug.Log($"[Offseason] {retired.Count} player(s) retired");
            return retired;
        }

        private PlayerCareerData ToCareerData(GameManager gm, Data.Player p)
        {
            return new PlayerCareerData
            {
                PlayerId = p.PlayerId,
                FullName = p.FullName,
                Age = p.Age,
                SeasonsPlayed = p.SeasonsPlayed,
                GamesPlayed = p.CareerStats?.Sum(s => s.GamesPlayed) ?? 0,
                CareerPoints = p.CareerPoints,
                CareerRebounds = p.CareerRebounds,
                CareerAssists = p.CareerAssists,
                CurrentTeamId = p.TeamId,
                CurrentRating = p.OverallRating,
                PeakRating = Math.Max(p.OverallRating, p.HiddenPotential),
                CareerInjuries = p.InjuryHistoryList?.Count ?? 0,
                BasketballIQ = p.BasketballIQ,
                Leadership = p.Leadership
            };
        }

        // ==================== PRE-DRAFT WORKOUTS (O3) ====================

        /// <summary>
        /// Jun 10: the gym opens. You get six invites; whoever you don't use by
        /// draft day the war room spends on prospects around your slot.
        /// </summary>
        private void OpenWorkouts(GameManager gm)
        {
            if (!Data.RolePermissions.CanMakeRosterMoves) return;   // the GM runs his own gym
            InboxService.Instance?.Publish(InboxMessageType.Scouting, "Scouting Department",
                "Pre-draft workouts are open",
                $"We can bring in {MaxWorkoutInvites} prospects before the {OffseasonDates.Draft(_calendarYear):MMM d} " +
                "draft. A workout is worth two scouting trips and can shake loose whatever a kid is hiding. " +
                "Pick them on the draft board.",
                deepLinkPanelId: "FrontOffice");
        }

        /// <summary>The draft class as the scouting department sees it right now.</summary>
        private IReadOnlyList<DraftProspect> WorkoutPool(GameManager gm) =>
            _draft?.GetProspects() as IReadOnlyList<DraftProspect>
            ?? gm?.Scouting?.GetProspectPreview(_seasonLabel)
            ?? new List<DraftProspect>();

        /// <summary>
        /// Bring a prospect in. He works out the same day and the report lands
        /// immediately — that's the whole point of spending an invite.
        /// </summary>
        public bool InviteToWorkout(GameManager gm, string prospectId, out string failReason)
        {
            failReason = "";
            if (gm == null || string.IsNullOrEmpty(prospectId)) { failReason = "Unavailable."; return false; }
            if (!WorkoutsOpen(gm.CurrentDate))
            { failReason = $"Workouts run {OffseasonDates.Workouts(_calendarYear):MMM d} to draft day."; return false; }
            if (_workoutInvites.Contains(prospectId)) { failReason = "He's already worked out for us."; return false; }
            if (WorkoutInvitesRemaining <= 0) { failReason = $"All {MaxWorkoutInvites} invites are spent."; return false; }

            var prospect = WorkoutPool(gm).FirstOrDefault(p => p.ProspectId == prospectId);
            if (prospect == null) { failReason = "Unknown prospect."; return false; }

            string summary = gm.Scouting?.FileWorkoutReport(prospect);
            if (summary == null) { failReason = "Scouting department unavailable."; return false; }
            _workoutInvites.Add(prospectId);

            InboxService.Instance?.Publish(InboxMessageType.Scouting, "Scouting Department",
                $"Workout: {prospect.FullName}", summary,
                deepLinkPanelId: "FrontOffice");
            return true;
        }

        /// <summary>
        /// Draft day: unspent invites go to prospects sitting around our slot, and
        /// the whole batch lands as one note instead of six.
        /// ponytail: AI teams don't run workouts — their picks read the hidden
        /// truth already, so per-team invites would buy nothing. Give AI drafting
        /// a scouting fog and this is where their invites go.
        /// </summary>
        private void RunRemainingWorkouts(GameManager gm)
        {
            _workoutsDone = true;
            int remaining = WorkoutInvitesRemaining;
            if (remaining <= 0) return;

            int slot = Math.Max(1, gm.AllTeams
                .Where(t => t != null).OrderBy(t => t.Wins).ThenBy(t => t.TeamId)
                .ToList().FindIndex(t => t.TeamId == gm.PlayerTeamId) + 1);

            var filled = new List<string>();
            foreach (var prospect in WorkoutPool(gm)
                         .Where(p => p != null && !_workoutInvites.Contains(p.ProspectId))
                         .OrderBy(p => Math.Abs(p.Intel.ConsensusRank - slot))
                         .Take(remaining))
            {
                string summary = gm.Scouting?.FileWorkoutReport(prospect);
                if (summary == null) return;
                _workoutInvites.Add(prospect.ProspectId);
                filled.Add($"{prospect.FullName} — {summary}");
            }

            if (filled.Count > 0 && Data.RolePermissions.CanMakeRosterMoves)
                InboxService.Instance?.Publish(InboxMessageType.Scouting, "Scouting Department",
                    $"{filled.Count} last-minute workout(s) around pick #{slot}",
                    string.Join("\n\n", filled), deepLinkPanelId: "FrontOffice");
        }

        /// <summary>
        /// Draft night: lottery for the 14 non-playoff teams, pick ownership from
        /// the registry, a fresh 120-prospect class. AI teams pick automatically;
        /// the night HALTS when your pick comes up — pick from the Front Office
        /// panel, or advance the day and the war room takes best-available.
        /// </summary>
        private void StartDraftNight(GameManager gm, DateTime date)
        {
            _draft = new DraftSystem(gm.SalaryCapManager, gm.PlayerDatabase,
                seed: _seasonLabel * 17 + 3);
            _draft.GenerateDraftClass(_calendarYear);

            // Draft order: worst record first; lottery shuffles the 14 non-playoff teams
            var bracket = PlayoffManager.Instance?.CurrentBracket;
            var playoffTeams = new HashSet<string>();
            if (bracket != null)
            {
                foreach (var seed in (bracket.Eastern?.Seeds ?? new string[0])) if (!string.IsNullOrEmpty(seed)) playoffTeams.Add(seed);
                foreach (var seed in (bracket.Western?.Seeds ?? new string[0])) if (!string.IsNullOrEmpty(seed)) playoffTeams.Add(seed);
            }

            var byRecord = gm.AllTeams.Where(t => t != null)
                .OrderBy(t => t.Wins).ThenBy(t => t.TeamId).ToList();
            var lotteryTeams = byRecord.Where(t => !playoffTeams.Contains(t.TeamId))
                .Select(t => t.TeamId).ToList();
            var playoffByRecord = byRecord.Where(t => playoffTeams.Contains(t.TeamId))
                .Select(t => t.TeamId).ToList();

            List<string> slotOrder;
            if (lotteryTeams.Count == 14)
            {
                var lottery = _draft.RunLottery(lotteryTeams, _rng);
                slotOrder = lottery.Select(r => r.TeamId).Concat(playoffByRecord).ToList();
            }
            else
            {
                slotOrder = byRecord.Select(t => t.TeamId).ToList(); // degenerate fallback
            }

            // Slot identity is fixed; ownership is looked up (and re-looked-up after
            // every trade tonight) from the registry.
            _slotOrder1 = new List<string>(slotOrder);
            _slotOrder2 = new List<string>(slotOrder);
            _draftOrder1 = new List<string>(slotOrder);
            _draftOrder2 = new List<string>(slotOrder);

            _draftStarted = true;
            _draftDay = date;
            _nextPick = 1;
            _onTheClock = false;
            _playerPickResults.Clear();
            RefreshDraftOrderOwnership(gm, resumeClock: false);

            int playerPickCount = _draftOrder1.Concat(_draftOrder2)
                .Count(id => id == gm.PlayerTeamId);
            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                $"{_calendarYear} Draft night is HERE",
                playerPickCount > 0
                    ? $"You hold {playerPickCount} pick(s) tonight. The board is live in the Front Office."
                    : "You hold no picks this year — watch the board in the Front Office.",
                highPriority: playerPickCount > 0,
                deepLinkPanelId: "FrontOffice");
        }

        // ==================== O4: DRAFT-NIGHT PICK TRADING ====================

        /// <summary>
        /// A trade just executed. During draft night the remaining slots may have
        /// changed hands, so re-derive ownership. Called from
        /// GameManager.OnTradeExecutedHandler — TradeSystem.ExecuteTrade fires
        /// OnTradeExecuted on every path (propose, agreed offer, AI-to-AI), so it's
        /// the one choke point that covers them all.
        /// </summary>
        public void NotifyTradeExecuted(GameManager gm)
        {
            if (!DraftActive) return;
            RefreshDraftOrderOwnership(gm);
        }

        /// <summary>Overall pick number a team's own pick sits at tonight (0 when unknown).</summary>
        public int PickSlotFor(string originalTeamId, int round)
        {
            var slots = round == 1 ? _slotOrder1 : _slotOrder2;
            int i = slots.IndexOf(originalTeamId);
            return i < 0 ? 0 : (round == 1 ? i + 1 : 31 + i);
        }

        /// <summary>
        /// Whether tonight's slot identity is known. False for a pre-O4 mid-draft save,
        /// where the board can't re-derive ownership, so this draft's picks must not trade.
        /// </summary>
        public bool SlotOrderTracked => _slotOrder1.Count > 0;

        /// <summary>Original team whose pick sits at an overall pick number (null when untracked).</summary>
        private string SlotOwnerAt(int pick, out int round)
        {
            round = pick <= 30 ? 1 : 2;
            var slots = round == 1 ? _slotOrder1 : _slotOrder2;
            int i = round == 1 ? pick - 1 : pick - 31;
            return i >= 0 && i < slots.Count ? slots[i] : null;
        }

        /// <summary>
        /// The slot on the clock was just exercised: burn the ORIGINAL team's pick in
        /// the registry so it can't be sold for the rest of the night, then advance.
        /// Every selection path (AI, your pick, clock expiry) advances through here.
        /// </summary>
        private void ConsumePick(GameManager gm)
        {
            string original = SlotOwnerAt(_nextPick, out int round);
            if (original != null) gm?.DraftPickRegistry?.MarkUsed(original, _calendarYear, round);
            _nextPick++;
        }

        /// <summary>
        /// Re-derive who owns every REMAINING slot from the registry. Picks already
        /// made keep the team that made them. When the pick on the clock changes hands
        /// the night hands off: an AI owner picks immediately, a player owner is clocked.
        /// Pre-O4 saves have no slot order, so this is a no-op for them.
        /// </summary>
        private void RefreshDraftOrderOwnership(GameManager gm, bool resumeClock = true)
        {
            if (_draft == null || _slotOrder1.Count == 0) return;
            var registry = gm?.DraftPickRegistry;

            List<string> Owners(List<string> slots, List<string> current, int round, int firstPick)
            {
                var owners = new List<string>(slots);
                for (int i = 0; i < slots.Count; i++)
                {
                    bool alreadyUsed = firstPick + i < _nextPick;
                    if (alreadyUsed && i < current.Count && !string.IsNullOrEmpty(current[i]))
                        owners[i] = current[i];      // never rewrite history
                    else
                        owners[i] = registry?.GetPick(slots[i], _calendarYear, round)?.CurrentOwnerId
                                    ?? slots[i];
                }
                return owners;
            }

            string before = _draft.GetTeamAtPick(_nextPick);
            _draftOrder1 = Owners(_slotOrder1, _draftOrder1, 1, 1);
            _draftOrder2 = Owners(_slotOrder2, _draftOrder2, 2, 31);
            _draft.SetDraftOrder(_draftOrder1, _draftOrder2);

            string after = _draft.GetTeamAtPick(_nextPick);
            if (!resumeClock || before == after || !DraftActive) return;

            ExpireOnClockOffers(gm);
            _onTheClock = false;
            ContinueDraft(gm);   // the new owner picks, or you get clocked
        }

        /// <summary>
        /// You're on the clock inside the top 20: rivals call about moving up. Their
        /// later first (plus a future second) for your slot, built as picks-for-picks
        /// and validated by the same CBA path as any trade, then dropped on the normal
        /// incoming-offers desk.
        /// ponytail: picks-only packages and no AI-to-AI draft trades — a player
        /// sweetener drags salary matching in, and AI-to-AI would need the whole
        /// evaluator on the clock. Both are upgrades, not blockers.
        /// </summary>
        private void GenerateOnClockOffers(GameManager gm, int pick)
        {
            var gen = gm?.TradeOfferGenerator;
            var registry = gm?.DraftPickRegistry;
            if (gen == null || registry == null || gm.Trades == null) return;
            if (pick > 20 || pick > _slotOrder1.Count) return;
            if (gen.GetPendingOffers().Any(o => o.OfferId?.StartsWith(OnClockOfferPrefix) == true))
                return;   // calls already on the desk for this pick

            // A top-5 pick ALWAYS draws calls; deeper in the round it's a roll, and by
            // #20 nobody's jumping the queue.
            if (pick > 5 && _rng.NextDouble() >= 0.85 - pick * 0.03) return;
            int wanted = _rng.NextDouble() < 0.35 ? 2 : 1;

            var yours = registry.GetPick(_slotOrder1[pick - 1], _calendarYear, 1);
            if (yours == null || yours.CurrentOwnerId != gm.PlayerTeamId) return;

            // Suitors: teams picking at least three slots behind you tonight, nearest
            // first — a jump from #28 to #3 isn't a call anyone makes.
            var suitors = new List<(Data.DraftPick pick, int slot)>();
            for (int i = pick + 2; i < _slotOrder1.Count; i++)
            {
                var theirs = registry.GetPick(_slotOrder1[i], _calendarYear, 1);
                if (theirs == null || theirs.CurrentOwnerId == gm.PlayerTeamId) continue;
                suitors.Add((theirs, i + 1));
                if (suitors.Count >= 8) break;
            }
            if (suitors.Count == 0) return;

            for (int n = 0; n < wanted && suitors.Count > 0; n++)
            {
                var (theirs, slot) = suitors[_rng.Next(suitors.Count)];
                suitors.RemoveAll(s => s.pick.CurrentOwnerId == theirs.CurrentOwnerId);
                string suitorId = theirs.CurrentOwnerId;

                var proposal = new TradeProposal { ProposedDate = gm.CurrentDate };
                proposal.AllAssets.Add(DraftPickRegistry.ToTradeAsset(yours, gm.PlayerTeamId, suitorId));
                proposal.AllAssets.Add(DraftPickRegistry.ToTradeAsset(theirs, suitorId, gm.PlayerTeamId));

                var sweetener = registry.GetPicksOwnedBy(suitorId)
                    .FirstOrDefault(p => p.Round == 2 && p.Year > _calendarYear);
                if (sweetener != null)
                    proposal.AllAssets.Add(DraftPickRegistry.ToTradeAsset(sweetener, suitorId, gm.PlayerTeamId));

                if (!(gm.Trades.ValidateProposal(proposal)?.IsValid ?? false)) continue;

                string sweetText = sweetener != null
                    ? $" and their {sweetener.Year} second-rounder" : "";
                gen.InjectOffer(new IncomingTradeOffer
                {
                    OfferId = OnClockOfferPrefix + Guid.NewGuid(),
                    OfferingTeamId = suitorId,
                    Proposal = proposal,
                    OfferMessage = $"Draft night: we want to move up. We'll give you #{slot} overall" +
                                   $"{sweetText} for the #{pick} pick — answer before you're off the clock.",
                    ReceivedAt = gm.CurrentDate,
                    ExpiresAt = _draftDay.AddDays(1),
                    Status = IncomingOfferStatus.Pending
                });
            }
        }

        /// <summary>
        /// Kill the trade-up calls: the pick they were about is gone. Found by OfferId
        /// prefix, so offers that rode through a mid-night save still get cleaned up.
        /// </summary>
        private static void ExpireOnClockOffers(GameManager gm)
        {
            var offers = gm?.TradeOfferGenerator?.GetPendingOffers();
            if (offers == null) return;
            foreach (var offer in offers)
                if (offer.OfferId?.StartsWith(OnClockOfferPrefix) == true)
                    offer.Status = IncomingOfferStatus.Expired;
        }

        /// <summary>Run AI picks until the player is on the clock or the draft ends.</summary>
        private void ContinueDraft(GameManager gm)
        {
            if (_draft == null || _draftDone) return;
            var inbox = InboxService.Instance;
            string pid = gm.PlayerTeamId;

            while (_nextPick <= 60)
            {
                string teamId = _draft.GetTeamAtPick(_nextPick);
                if (string.IsNullOrEmpty(teamId)) { _nextPick++; continue; }

                if (teamId == pid && !string.IsNullOrEmpty(pid) &&
                    Data.RolePermissions.CanMakeRosterMoves)
                {
                    _onTheClock = true;
                    GenerateOnClockOffers(gm, _nextPick);
                    inbox?.Publish(InboxMessageType.League, "League Office",
                        $"You're ON THE CLOCK at pick #{_nextPick}",
                        "Make your selection in the Front Office. Advancing the day lets the war room pick best-available.",
                        highPriority: true,
                        deepLinkPanelId: "FrontOffice");
                    return;
                }

                DoAIPick(gm, _nextPick, teamId);
                ConsumePick(gm);
            }

            FinishDraft(gm);
        }

        /// <summary>Your selection from the Front Office board while on the clock.</summary>
        public bool SubmitPlayerPick(GameManager gm, string prospectId)
        {
            if (!PlayerOnClock || gm == null || _draft == null) return false;

            var selection = _draft.MakePick(_nextPick, gm.PlayerTeamId, prospectId);
            var drafted = selection?.DraftedPlayer;
            if (drafted == null) return false;

            SyncDraftedPlayer(gm, gm.PlayerTeamId, drafted);
            _playerPickResults.Add($"#{_nextPick}: {drafted.FullName} ({drafted.Position})");
            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                $"With pick #{_nextPick}, you select {drafted.FullName}!",
                $"{drafted.FullName} ({drafted.Position}) joins the franchise on a rookie-scale deal.",
                highPriority: true,
                deepLinkPanelId: "Roster",
                deepLinkPayload: drafted.PlayerId);

            ConsumePick(gm);
            _onTheClock = false;
            ExpireOnClockOffers(gm);   // the pick they wanted is spent
            ContinueDraft(gm);
            return true;
        }

        private void AutoPickPending(GameManager gm)
        {
            if (!_onTheClock || _draft == null) return;
            var selection = _draft.AISelectPick(_nextPick, gm.PlayerTeamId);
            var drafted = selection?.DraftedPlayer;
            if (drafted != null)
            {
                SyncDraftedPlayer(gm, gm.PlayerTeamId, drafted);
                _playerPickResults.Add($"#{_nextPick}: {drafted.FullName} ({drafted.Position}) [auto]");
                InboxService.Instance?.Publish(InboxMessageType.League, "War Room",
                    $"Clock expired — {drafted.FullName} selected at #{_nextPick}",
                    "The war room went best-available when the clock ran out.",
                    highPriority: true);
            }
            ConsumePick(gm);
            _onTheClock = false;
            ExpireOnClockOffers(gm);
        }

        private void DoAIPick(GameManager gm, int pick, string teamId)
        {
            var selection = _draft.AISelectPick(pick, teamId);
            var drafted = selection?.DraftedPlayer;
            if (drafted != null && teamId == gm.PlayerTeamId)
            {
                // Coach-only: the GM ran the war room
                InboxService.Instance?.Publish(InboxMessageType.League,
                    Data.RolePermissions.AIGMName,
                    $"We took {drafted.FullName} at #{pick}",
                    $"{drafted.FullName} ({drafted.Position}) is our pick. Get him ready.",
                    highPriority: true, deepLinkPanelId: "Roster", deepLinkPayload: drafted.PlayerId);
            }
            if (drafted == null) return;

            SyncDraftedPlayer(gm, teamId, drafted);

            if (pick <= 5)
                InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                    $"Draft: {gm.GetTeam(teamId)?.Name ?? teamId} select {drafted.FullName} at #{pick}",
                    $"{drafted.FullName} goes #{pick} overall.");
        }

        private static void SyncDraftedPlayer(GameManager gm, string teamId, Data.Player drafted)
        {
            // DraftSystem adds to a throwaway roster list — the ID list is the authority
            var team = gm.GetTeam(teamId);
            if (team != null && !team.RosterPlayerIds.Contains(drafted.PlayerId))
                team.RosterPlayerIds.Add(drafted.PlayerId);
        }

        private void FinishDraft(GameManager gm)
        {
            _draftDone = true;
            _onTheClock = false;
            gm.DraftPickRegistry?.ProcessDraftCompletion(_calendarYear);

            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                $"{_calendarYear} NBA Draft complete",
                _playerPickResults.Count > 0
                    ? $"Your selections:\n{string.Join("\n", _playerPickResults)}"
                    : "Your team made no selections this year.",
                highPriority: _playerPickResults.Count > 0);

            Debug.Log($"[Offseason] Draft complete ({_calendarYear})");
        }

        // ==================== PLAYER-DRIVEN FREE AGENCY ====================

        /// <summary>Asking price for a free agent (shown on the Front Office market).</summary>
        public long EstimateMarketSalary(Data.Player p) => MarketValue(p);

        /// <summary>
        /// Sign a free agent to YOUR team from the Front Office panel. Own free
        /// agents can re-sign from June (Bird rights, before the market opens);
        /// everyone else once free agency opens July 6. Tries Bird rights, then cap
        /// space, then a minimum deal.
        /// </summary>
        /// <summary>
        /// Sign a free agent at NEGOTIATED terms (from a ContractNegotiationManager
        /// session the agent accepted) — same cap plumbing as the one-shot path, but
        /// the agreed salary and years are honored instead of the market lookup.
        /// A handshake on a CONTESTED free agent doesn't end it: the agreement becomes
        /// your leading bid and he decides on his decision day.
        /// </summary>
        public bool FinalizeNegotiatedSigning(GameManager gm, string playerId, int years,
            long annualSalary, out string failReason)
        {
            failReason = "";
            var fam = gm?.FreeAgents;
            var player = gm?.PlayerDatabase?.GetPlayer(playerId);
            var team = gm?.GetPlayerTeam();
            if (fam == null || player == null || team == null) { failReason = "Unavailable."; return false; }

            var fa = fam.GetFreeAgents().FirstOrDefault(f => f.PlayerId == playerId);
            if (fa == null) { failReason = "No longer a free agent."; return false; }
            if (team.RosterPlayerIds.Count >= 15) { failReason = "Roster is full (15)."; return false; }

            years = Mathf.Clamp(years, 1, 5);
            bool ownFreeAgent = fa.PreviousTeamId == team.TeamId;
            annualSalary = Math.Max(Data.LeagueCBA.GetMinimumSalary(ServiceYears(gm, player)), annualSalary);

            // Contested: the handshake is a bid, not a signature. Your OWN free agent
            // is different — Bird rights mean an accepted deal is a signature.
            var market = EnsureMarket(gm);
            if (!ownFreeAgent && market != null && market.IsContested(playerId, team.TeamId))
            {
                if (!market.PlaceBid(playerId, years, annualSalary, out failReason)) return false;

                // The market may have trimmed the salary or stepped the years down
                var placed = market.GetPlayerTeamBid(playerId);
                int bidYears = placed?.Years ?? years;
                long bidSalary = placed?.AnnualAverage ?? annualSalary;
                var day = market.GetDecisionDay(playerId);
                InboxService.Instance?.Publish(InboxMessageType.League, "Front Office",
                    $"You're the leader for {player.FullName}",
                    $"Your offer — {bidYears} years at ${bidSalary / 1_000_000f:0.0}M a year — is on the table, " +
                    "but he's still taking calls." +
                    (day.HasValue ? $" He decides {day.Value:dddd, MMM d}." : ""),
                    highPriority: true, deepLinkPanelId: "FrontOffice", deepLinkPayload: playerId);
                return true;
            }

            var methods = ownFreeAgent
                ? new[] { SigningMethod.BirdRights, SigningMethod.CapSpace, SigningMethod.MinimumSalary }
                : new[] { SigningMethod.CapSpace, SigningMethod.MidLevelException,
                          SigningMethod.BiAnnualException, SigningMethod.MinimumSalary };

            foreach (var method in methods)
            {
                int service = ServiceYears(gm, player);
                var offer = new SigningOffer
                {
                    AnnualSalary = method == SigningMethod.MinimumSalary
                        ? Data.LeagueCBA.GetMinimumSalary(service) : annualSalary,
                    Years = years,
                    Method = method,
                    PlayerYearsExperience = service
                };

                var check = fam.CanSign(team.TeamId, playerId, offer);
                if (!check.IsValid) { failReason = check.Reason; continue; }

                if (fam.ExecuteSigning(team.TeamId, playerId, offer))
                {
                    if (!team.RosterPlayerIds.Contains(playerId))
                        team.RosterPlayerIds.Add(playerId);
                    market?.DropPlayer(playerId);   // no ghost bids on a signed player

                    InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                        ownFreeAgent
                            ? $"{player.FullName} re-signs with {team.Name}"
                            : $"{player.FullName} signs with {team.Name}",
                        $"Agreed at the table: {years} years, ${offer.AnnualSalary * years / 1_000_000f:0.0}M total.",
                        highPriority: true);
                    return true;
                }
            }

            if (string.IsNullOrEmpty(failReason)) failReason = "Signing failed validation.";
            return false;
        }

        public bool SignFreeAgentToPlayerTeam(GameManager gm, string playerId, int years, out string failReason)
        {
            failReason = "";
            var fam = gm?.FreeAgents;
            var player = gm?.PlayerDatabase?.GetPlayer(playerId);
            var team = gm?.GetPlayerTeam();
            if (fam == null || player == null || team == null) { failReason = "Unavailable."; return false; }

            var fa = fam.GetFreeAgents().FirstOrDefault(f => f.PlayerId == playerId);
            if (fa == null) { failReason = "No longer a free agent."; return false; }

            bool ownFreeAgent = fa.PreviousTeamId == team.TeamId;
            if (!ownFreeAgent && !FreeAgencySigningOpen)
            { failReason = "The market opens July 6 — only your own free agents can re-sign now."; return false; }
            if (team.RosterPlayerIds.Count >= 15)
            { failReason = "Roster is full (15)."; return false; }

            long ask = MarketValue(player);
            years = Mathf.Clamp(years, 1, 4);

            // A contested free agent can't be signed on the spot — the one-shot offer
            // becomes a bid and he decides on his decision day (same as the table deal)
            if (!ownFreeAgent)
            {
                var market = EnsureMarket(gm);
                if (market != null && market.IsContested(playerId, team.TeamId))
                    return FinalizeNegotiatedSigning(gm, playerId, years, ask, out failReason);
            }

            var methods = ownFreeAgent
                ? new[] { SigningMethod.BirdRights, SigningMethod.CapSpace, SigningMethod.MinimumSalary }
                : new[] { SigningMethod.CapSpace, SigningMethod.MidLevelException,
                          SigningMethod.BiAnnualException, SigningMethod.MinimumSalary };

            foreach (var method in methods)
            {
                int service = ServiceYears(gm, player);
                var offer = new SigningOffer
                {
                    AnnualSalary = method == SigningMethod.MinimumSalary
                        ? Data.LeagueCBA.GetMinimumSalary(service) : ask,
                    Years = years,
                    Method = method,
                    PlayerYearsExperience = service
                };

                var check = fam.CanSign(team.TeamId, playerId, offer);
                if (!check.IsValid) { failReason = check.Reason; continue; }

                if (fam.ExecuteSigning(team.TeamId, playerId, offer))
                {
                    if (!team.RosterPlayerIds.Contains(playerId))
                        team.RosterPlayerIds.Add(playerId);
                    _market?.DropPlayer(playerId);   // no ghost bids on a signed player

                    InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                        ownFreeAgent
                            ? $"{player.FullName} re-signs with {team.Name}"
                            : $"{player.FullName} signs with {team.Name}",
                        $"{years} years, ${offer.AnnualSalary * years / 1_000_000f:0.0}M total.",
                        highPriority: true);
                    return true;
                }
            }

            if (string.IsNullOrEmpty(failReason)) failReason = "Signing failed validation.";
            return false;
        }

        private void OpenFreeAgency(GameManager gm)
        {
            int poolSize = gm.FreeAgents?.GetFreeAgents()?.Count ?? 0;
            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                "Free agency is OPEN",
                $"{poolSize} free agents are on the market.");
        }

        /// <summary>
        /// The market runs itself (bids, bidding wars, decision days, offer sheets),
        /// then the scrap heap drains: a couple of depth deals a day for free agents
        /// nobody is bidding on.
        /// </summary>
        private void RunDailyFreeAgency(GameManager gm, DateTime date)
        {
            var fam = gm.FreeAgents;
            if (fam == null) return;

            EnsureMarket(gm)?.RunDay(date);
            RunScrapHeap(gm);
        }

        /// <summary>
        /// Depth signings for free agents outside the marketed tier — what keeps the
        /// pool draining all summer. A team with room pays roughly what the player is
        /// worth; everyone else offers the minimum. A free agent nobody will take is
        /// skipped, never a reason to stop the day. Restricted free agents are left
        /// alone — their original team holds first refusal (see
        /// AcceptLeftoverQualifyingOffers).
        /// </summary>
        private void RunScrapHeap(GameManager gm)
        {
            var fam = gm.FreeAgents;
            var pool = fam?.GetFreeAgents();
            if (pool == null || pool.Count == 0) return;

            var marketed = new HashSet<string>(_market?.MarketedPlayerIds ?? new List<string>());
            var ranked = pool
                .Where(fa => fa.Type != FreeAgentType.Restricted)
                .Select(fa => gm.PlayerDatabase.GetPlayer(fa.PlayerId))
                .Where(p => p != null && p.RetirementYear == 0 && !marketed.Contains(p.PlayerId))
                .OrderByDescending(p => p.OverallRating)
                .ToList();

            int signingsToday = Math.Max(2, pool.Count / 12);
            int signed = 0;

            foreach (var player in ranked)
            {
                if (signed >= signingsToday) break;

                var suitors = gm.AllTeams.Where(t =>
                        t != null &&
                        gm.SalaryCapManager.GetStandardContractCount(t.TeamId) < RosterManager.STANDARD_ROSTER_MAX &&
                        (t.TeamId != gm.PlayerTeamId ||
                         !Data.RolePermissions.CanMakeRosterMoves ||
                         gm.SalaryCapManager.GetStandardContractCount(t.TeamId) < 13))
                    .OrderByDescending(t => gm.SalaryCapManager.GetCapSpace(t.TeamId))
                    .ToList();
                if (suitors.Count == 0) return;   // nobody in the league has a spot

                var team = suitors[Math.Min(_rng.Next(3), suitors.Count - 1)];
                int service = ServiceYears(gm, player);
                int years = 1 + _rng.Next(2);
                var offer = ScrapHeapOffer(gm.SalaryCapManager.GetCapSpace(team.TeamId),
                    MarketValue(player), service, years);

                if (!fam.ExecuteSigning(team.TeamId, player.PlayerId, offer))
                {
                    // Cap-space deal didn't validate — fall back to the minimum
                    if (offer.Method == SigningMethod.MinimumSalary) continue;
                    offer = ScrapHeapOffer(0L, MarketValue(player), service, years);
                    if (!fam.ExecuteSigning(team.TeamId, player.PlayerId, offer)) continue;
                }
                if (!team.RosterPlayerIds.Contains(player.PlayerId))
                    team.RosterPlayerIds.Add(player.PlayerId);
                signed++;

                if (team.TeamId == gm.PlayerTeamId)
                    InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                        $"{player.FullName} signs with {team.Name}",
                        offer.Method == SigningMethod.MinimumSalary
                            ? $"{offer.Years} year(s) at the minimum."
                            : $"{offer.Years} year(s) at ${offer.AnnualSalary / 1_000_000f:0.0}M a year.",
                        highPriority: true);
            }
        }

        /// <summary>
        /// What a depth free agent gets: real money (up to his market value) from a
        /// team with cap room, the league minimum from a team without it. Never below
        /// the minimum for his service, never above the room the team actually has.
        /// </summary>
        internal static SigningOffer ScrapHeapOffer(long capSpace, long marketValue, int service, int years)
        {
            long min = Data.LeagueCBA.GetMinimumSalary(service);
            bool hasRoom = capSpace >= min;
            return new SigningOffer
            {
                AnnualSalary = hasRoom ? Math.Max(min, Math.Min(marketValue, capSpace)) : min,
                Years = Math.Max(1, years),
                Method = hasRoom ? SigningMethod.CapSpace : SigningMethod.MinimumSalary,
                PlayerYearsExperience = service
            };
        }

        /// <summary>
        /// Oct 1: any tendered restricted free agent still unsigned takes his
        /// qualifying offer — one year with his original team (Bird rights, so an
        /// over-the-cap team can still do it). Without this, an RFA outside the
        /// marketed top tier would sit in the pool forever.
        /// </summary>
        private void AcceptLeftoverQualifyingOffers(GameManager gm)
        {
            var fam = gm.FreeAgents;
            if (fam == null) return;

            foreach (var fa in fam.GetFreeAgents()
                         .Where(f => f.Type == FreeAgentType.Restricted && f.HasQualifyingOffer &&
                                     f.QualifyingOfferAmount > 0 &&
                                     !string.IsNullOrEmpty(f.PreviousTeamId)).ToList())
            {
                var player = gm.PlayerDatabase?.GetPlayer(fa.PlayerId);
                var team = gm.GetTeam(fa.PreviousTeamId);
                if (player == null || player.RetirementYear > 0 || team == null) continue;
                // A live offer sheet gets to play out first
                if (_market?.HasOfferSheet(fa.PlayerId) == true) continue;

                int service = ServiceYears(gm, player);
                foreach (var method in new[] { SigningMethod.BirdRights, SigningMethod.CapSpace,
                                               SigningMethod.MinimumSalary })
                {
                    var offer = new SigningOffer
                    {
                        AnnualSalary = method == SigningMethod.MinimumSalary
                            ? Data.LeagueCBA.GetMinimumSalary(service)
                            : fa.QualifyingOfferAmount,
                        Years = 1,
                        Method = method,
                        PlayerYearsExperience = service
                    };
                    if (!fam.ExecuteSigning(team.TeamId, fa.PlayerId, offer)) continue;

                    if (!team.RosterPlayerIds.Contains(fa.PlayerId))
                        team.RosterPlayerIds.Add(fa.PlayerId);
                    _market?.DropPlayer(fa.PlayerId);

                    if (team.TeamId == gm.PlayerTeamId)
                        InboxService.Instance?.Publish(InboxMessageType.League, "Front Office",
                            $"{player.FullName} accepts his qualifying offer",
                            $"No offer sheet came in, so he takes the one-year deal at " +
                            $"${offer.AnnualSalary / 1_000_000f:0.0}M and is back in camp.",
                            highPriority: true, deepLinkPanelId: "Roster",
                            deepLinkPayload: fa.PlayerId);
                    break;
                }
            }
        }

        /// <summary>
        /// Years of service. YearsPro is 0 for every shipped player, so entry year is
        /// the real source.
        /// </summary>
        private static int ServiceYears(GameManager gm, Data.Player p)
        {
            if (p == null) return 0;
            int year = gm != null ? gm.CurrentDate.Year : 0;
            return p.DraftYear > 0 && year > 1 ? Math.Max(0, year - p.DraftYear) : p.YearsPro;
        }

        internal static long MarketValue(Data.Player p)
        {
            int r = p.OverallRating;
            if (r >= 90) return 45_000_000L;
            if (r >= 85) return 32_000_000L;
            if (r >= 80) return 22_000_000L;
            if (r >= 75) return 12_000_000L;
            if (r >= 70) return 6_000_000L;
            if (r >= 65) return 3_000_000L;
            return 1_200_000L;
        }

        private void RunSummerLeague(GameManager gm)
        {
            try
            {
                var sl = gm.SummerLeagueManager;
                if (sl == null) return;
                sl.PlayerSource = id => gm.PlayerDatabase?.GetPlayer(id);
                sl.StartSummerLeague(_calendarYear, gm.AllTeams);
                var summary = sl.SkipSummerLeague();
                if (summary != null)
                {
                    var mine = sl.GetStandings().FirstOrDefault(t => t.TeamId == gm.PlayerTeamId);
                    var star = mine?.RosterStats?.OrderByDescending(r => r.PointsPerGame).FirstOrDefault();
                    string body = mine == null
                        ? "Rookies and young players got their reps in Las Vegas."
                        : $"Your squad went {mine.Wins}-{mine.Losses}." +
                          (star != null
                              ? $" {star.PlayerName} led the way at {star.PointsPerGame:F1} a game."
                              : "") +
                          " Full breakdown at the Front Office desk.";
                    InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                        "Summer League wraps up", body,
                        deepLinkPanelId: "FrontOffice");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Offseason] Summer league skipped: {ex.Message}");
            }
        }

        /// <summary>Sep 27: camps open. The player's camp runs day-by-day with a chosen focus.</summary>
        private void StartCamp(GameManager gm)
        {
            var team = gm.GetPlayerTeam();
            var tc = gm.TrainingCampManager;
            if (team != null && tc != null)
                tc.StartTrainingCamp(team, (team.RosterPlayerIds ?? new List<string>())
                    .Select(id => gm.PlayerDatabase?.GetPlayer(id))
                    .Where(pl => pl != null).ToList());

            _scrimmageLines.Clear();
            _scrimmagesPlayed = 0;
            SchedulePreseasonGames(gm);
            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                "Training camps open",
                "Pick a daily focus at the Front Office desk. Three preseason games before opening night — " +
                "they show up on your schedule and you can play them.",
                deepLinkPanelId: "FrontOffice");
        }

        /// <summary>
        /// Three playable exhibitions on the camp scrimmage days, vs random opponents,
        /// alternating home/away. They go on the SEASON schedule, so GetTodaysGame /
        /// GetNextGame find them and the existing game-day flow does all the work.
        /// AI teams get no scheduled preseason games — their camps stay abstract.
        /// </summary>
        private void SchedulePreseasonGames(GameManager gm)
        {
            _preseasonEvents.Clear();
            var team = gm?.GetPlayerTeam();
            var season = gm?.SeasonController;
            var opponents = gm?.AllTeams?.Where(t => t != null && t.TeamId != team?.TeamId).ToList();
            if (team == null || season == null || opponents == null || opponents.Count == 0) return;

            for (int i = 0; i < OffseasonDates.ScrimmageDays.Length; i++)
            {
                var opponent = opponents[_rng.Next(opponents.Count)];
                bool home = i % 2 == 0;
                _preseasonEvents.Add(new Data.CalendarEvent
                {
                    EventId = $"PRE_{_calendarYear}_{i + 1}",
                    Type = Data.CalendarEventType.Game,
                    IsPreseason = true,
                    Date = new DateTime(_calendarYear, 10, OffseasonDates.ScrimmageDays[i]),
                    Title = $"Preseason vs {opponent.Abbreviation}",
                    HomeTeamId = home ? team.TeamId : opponent.TeamId,
                    AwayTeamId = home ? opponent.TeamId : team.TeamId,
                    IsHomeGame = home,
                    GameNumber = i + 1
                });
            }

            season.AddScheduledEvents(_preseasonEvents);
        }

        private void RunCampDay(GameManager gm, DateTime date)
        {
            var team = gm.GetPlayerTeam();
            var tc = gm.TrainingCampManager;
            if (team == null || tc == null) return;

            var roster = (team.RosterPlayerIds ?? new List<string>())
                .Select(id => gm.PlayerDatabase?.GetPlayer(id))
                .Where(pl => pl != null).ToList();

            LastCampReport = tc.AdvanceCampDay(team, roster, CampFocus);

            AutoSimUnplayedPreseason(gm, date);
        }

        /// <summary>
        /// A preseason game the player never played (simmed past it, coach-only mode,
        /// headless run) auto-sims the day AFTER its date — same completion path, so the
        /// save state matches a played game. The calendar event's IsCompleted flag is the
        /// no-double-sim guard: the pipeline sets it whichever path ran the game.
        /// </summary>
        private void AutoSimUnplayedPreseason(GameManager gm, DateTime date)
        {
            foreach (var game in _preseasonEvents)
            {
                if (game == null || game.IsCompleted || game.Date.Date >= date.Date) continue;

                var home = gm.GetTeam(game.HomeTeamId);
                var away = gm.GetTeam(game.AwayTeamId);
                if (home == null || away == null || gm.GameCompletion == null)
                {
                    // Unplayable — mark completed with a plausible score so it never
                    // retries, and still feed camp so progress can't stall on it.
                    int hs = _rng.Next(90, 111), as_ = _rng.Next(90, 111);
                    if (hs == as_) hs += 2;
                    game.IsCompleted = true;
                    game.HomeScore = hs;
                    game.AwayScore = as_;
                    Debug.LogError($"[Offseason] Preseason game {game.EventId} unplayable (missing team/completion pipeline) — scored {hs}-{as_} as a placeholder.");
                    _scrimmagesPlayed++;
                    continue;
                }

                try
                {
                    var result = new Simulation.GameSimulator(gm.PlayerDatabase).SimulateGame(home, away);
                    gm.GameCompletion.Complete(new Simulation.GameCompletionContext(
                        game, result, Simulation.GameSource.LeagueAutoSim, gm.PlayerTeamId));
                }
                catch (Exception ex)
                {
                    // Sim blew up: fall back like LeagueGameSimSystem does — score-only
                    // completion so the event can never retry and soft-lock the offseason.
                    int hs = _rng.Next(90, 111), as_ = _rng.Next(90, 111);
                    if (hs == as_) hs += 2;
                    Debug.LogError($"[Offseason] Preseason sim failed for {game.EventId}: {ex.Message} — falling back to score-only completion.");
                    gm.GameCompletion.CompleteScoreOnly(game, hs, as_);
                    _scrimmagesPlayed++;
                }
            }
        }

        /// <summary>
        /// A preseason game finished (played, quick-simmed, or auto-simmed) — feed the
        /// camp the same signal the old auto-scrimmage produced. Called by
        /// GameCompletionPipeline, the one choke point every sim path funnels through.
        /// </summary>
        public void NotifyPreseasonGamePlayed(Data.CalendarEvent game, Simulation.GameResult result)
        {
            var gm = GameManager.Instance;
            var team = gm?.GetPlayerTeam();
            var tc = gm?.TrainingCampManager;
            if (game == null || result == null || team == null || tc == null) return;

            if (game.HomeTeamId != team.TeamId && game.AwayTeamId != team.TeamId) return;

            bool isHome = game.HomeTeamId == team.TeamId;
            var opponent = gm.GetTeam(isHome ? game.AwayTeamId : game.HomeTeamId);
            if (opponent == null) return;

            int us = isHome ? result.HomeScore : result.AwayScore;
            int them = isHome ? result.AwayScore : result.HomeScore;

            _scrimmagesPlayed++;
            tc.SimulatePreseasonGame(
                new PreseasonGame
                {
                    GameId = game.EventId,
                    GameNumber = _scrimmagesPlayed,
                    IsHome = isHome,
                    OpponentTeamId = opponent.TeamId
                },
                team, opponent, RosterOf(gm, team), RosterOf(gm, opponent), us, them);

            string line = $"Preseason {_scrimmagesPlayed}: {(us > them ? "W" : "L")} {us}-{them} vs {opponent.Name}";
            _scrimmageLines.Add(line);
            InboxService.Instance?.Publish(InboxMessageType.League, "Coaching Staff",
                line, "Preseason reps — the result doesn't count, the tape does.",
                deepLinkPanelId: "FrontOffice");
        }

        private static List<Data.Player> RosterOf(GameManager gm, Data.Team team) =>
            (team?.RosterPlayerIds ?? new List<string>())
                .Select(id => gm.PlayerDatabase?.GetPlayer(id))
                .Where(pl => pl != null).ToList();

        /// <summary>Oct 20: camp closes — cut recommendations, then roster compliance.</summary>
        private void FinishCamp(GameManager gm)
        {
            var tc = gm.TrainingCampManager;
            var team = gm.GetPlayerTeam();
            if (tc != null && team != null)
            {
                var roster = (team.RosterPlayerIds ?? new List<string>())
                    .Select(id => gm.PlayerDatabase?.GetPlayer(id))
                    .Where(pl => pl != null).ToList();
                var recs = tc.GetCutRecommendations(team, roster);
                if (recs != null && recs.Count > 0)
                {
                    string names = string.Join(", ", recs.Take(3).Select(r => r.PlayerName));
                    InboxService.Instance?.Publish(InboxMessageType.League, "Coaching Staff",
                        "Camp report: cut candidates",
                        $"The staff would look hard at: {names}. Roster compliance runs before opening night.",
                        deepLinkPanelId: "FrontOffice");
                }
                tc.CompleteCamp();
            }

            // AI teams ran their own quiet camps
            foreach (var t in gm.AllTeams)
                if (t != null && t.TeamId != gm.PlayerTeamId)
                    t.TeamChemistry = Math.Min(100, t.TeamChemistry + 8f);

            RunRosterCompliance(gm);
        }

        /// <summary>
        /// Camp-time roster compliance: trim to 15, fill to 13 with minimum signings,
        /// and refresh every team's lineup for the new rosters.
        /// </summary>
        private void RunRosterCompliance(GameManager gm)
        {
            var fam = gm.FreeAgents;

            foreach (var team in gm.AllTeams)
            {
                if (team == null) continue;

                // Trim: waive lowest-rated until 15
                while (team.RosterPlayerIds.Count > 15)
                {
                    var cut = team.RosterPlayerIds
                        .Select(id => gm.PlayerDatabase.GetPlayer(id))
                        .Where(p => p != null)
                        .OrderBy(p => p.OverallRating)
                        .FirstOrDefault();
                    if (cut == null) break;

                    team.RosterPlayerIds.Remove(cut.PlayerId);
                    gm.SalaryCapManager.RemoveContract(cut.PlayerId);
                    fam?.AddFreeAgent(cut.PlayerId, FreeAgentType.Unrestricted, team.TeamId, 0);
                    cut.TeamId = "";
                }

                // Fill: sign best available to minimums until 13
                while (team.RosterPlayerIds.Count < 13)
                {
                    var best = fam?.GetFreeAgents()?
                        .Select(fa => gm.PlayerDatabase.GetPlayer(fa.PlayerId))
                        .Where(p => p != null && p.RetirementYear == 0)
                        .OrderByDescending(p => p.OverallRating)
                        .FirstOrDefault();
                    if (best == null) break;

                    int service = ServiceYears(gm, best);
                    var offer = new SigningOffer
                    {
                        AnnualSalary = Data.LeagueCBA.GetMinimumSalary(service),
                        Years = 1,
                        Method = SigningMethod.MinimumSalary,
                        PlayerYearsExperience = service
                    };
                    if (fam.ExecuteSigning(team.TeamId, best.PlayerId, offer))
                    {
                        if (!team.RosterPlayerIds.Contains(best.PlayerId))
                            team.RosterPlayerIds.Add(best.PlayerId);
                    }
                    else break;
                }

                // Rosters changed everywhere — refresh the five
                if (team.CoachPersonality != null)
                    team.AutoSetStartingLineup(team.CoachPersonality);
            }

            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                "Training camps open",
                "Rosters are set league-wide. The new season tips off October 22.");
        }

        /// <summary>
        /// Season rollover: archive stats (YearsPro++, logs cleared), fresh schedule,
        /// reset records, clear the old bracket — year N+1 begins.
        /// </summary>
        private void Rollover(GameManager gm)
        {
            _engineActive = false;
            _market = null;
            _pendingQOs.Clear();
            _preseasonEvents.Clear();   // exhibitions are done; don't ghost-inject PRE rows into next save

            PlayoffManager.Instance?.ResetForNewSeason();
            gm.Development?.SetCurrentSeason(_seasonLabel + 1);
            gm.StartNewSeason();
            gm.FreeAgents?.StartNewSeason();

            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                $"Welcome to the {_seasonLabel + 1} season!",
                "Training camp is done and opening night is here. Good luck, coach.",
                highPriority: true);

            Debug.Log($"[Offseason] Rollover complete — season {_seasonLabel + 1} begins");
        }

        private static void RemoveFromRoster(GameManager gm, string teamId, string playerId)
        {
            var team = gm.GetTeam(teamId);
            team?.RosterPlayerIds?.Remove(playerId);
            if (team?.StartingLineupIds != null)
            {
                for (int i = 0; i < team.StartingLineupIds.Length; i++)
                    if (team.StartingLineupIds[i] == playerId)
                        team.StartingLineupIds[i] = "";
            }
        }

        // The awards results from the season that just ended (for history archiving)
        private AwardVotingResults _lastVotingResults;
        public void SetSeasonClosingData(AwardVotingResults results) => _lastVotingResults = results;

        // ==================== SAVE SECTION ====================

        public void WriteSave(Data.SaveData data)
        {
            data.Offseason = new Data.OffseasonSaveData
            {
                EngineActive = _engineActive,
                SeasonLabel = _seasonLabel,
                CalendarYear = _calendarYear,
                PostSeasonDone = _postSeasonDone,
                DraftDone = _draftDone,
                FreeAgencyOpen = _freeAgencyOpen,
                SummerDone = _summerDone,
                CampDone = _campDone,
                CampStarted = _campStarted,
                ScrimmagesPlayed = _scrimmagesPlayed,
                CampFocusInt = (int)CampFocus,
                ScrimmageLines = new List<string>(_scrimmageLines),
                DraftStarted = _draftStarted,
                OnTheClock = _onTheClock,
                NextPick = _nextPick,
                DraftDayStr = _draftDay.Year > 1 ? _draftDay.ToString("o") : "",
                DraftOrder1 = new List<string>(_draftOrder1),
                DraftOrder2 = new List<string>(_draftOrder2),
                SlotOrder1 = new List<string>(_slotOrder1),
                SlotOrder2 = new List<string>(_slotOrder2),
                WorkoutInvites = new List<string>(_workoutInvites),
                WorkoutsOpened = _workoutsOpened,
                WorkoutsDone = _workoutsDone,

                PreseasonGames = _preseasonEvents
                    .Where(e => e != null)
                    .Select(e => new Data.PreseasonGameRecord
                    {
                        EventId = e.EventId,
                        DateStr = e.Date.ToString("o"),
                        HomeTeamId = e.HomeTeamId,
                        AwayTeamId = e.AwayTeamId,
                        GameNumber = e.GameNumber,
                        IsCompleted = e.IsCompleted,
                        HomeScore = e.HomeScore,
                        AwayScore = e.AwayScore
                    }).ToList(),
                FreeAgentPool = GameManager.Instance?.FreeAgents?.GetFreeAgents()?
                    .Select(fa => new Data.FreeAgentRecord
                    {
                        PlayerId = fa.PlayerId,
                        PreviousTeamId = fa.PreviousTeamId,
                        ConsecutiveSeasons = fa.ConsecutiveSeasons,
                        TypeInt = (int)fa.Type,
                        HasQualifyingOffer = fa.HasQualifyingOffer,
                        QualifyingOfferAmount = fa.QualifyingOfferAmount
                    }).ToList() ?? new List<Data.FreeAgentRecord>(),

                // O2 market: live bids, pending offer sheets, decision days, plus the
                // browser's marketed list and wire digest (rebuilt only on a market day)
                MarketBids = _market?.ToSave() ?? new List<Data.MarketBidRecord>(),
                MarketMarketedIds = _market?.MarketedPlayerIds?.ToList() ?? new List<string>(),
                MarketLastDigest = _market?.LastDigest ?? "",

                PendingQualifyingOffers = _pendingQOs
                    .Select(q => new Data.PendingQualifyingOfferRecord
                    {
                        PlayerId = q.PlayerId,
                        TeamId = q.TeamId,
                        Amount = (long)q.Amount,
                        PriorSalary = q.PriorSalary,
                        DeadlineStr = q.Deadline.Year > 1 ? q.Deadline.ToString("o") : ""
                    }).ToList(),

                // Cap-exception usage lives in FreeAgentManager and is only meaningful
                // alongside the market, so it rides in this section.
                // ponytail: ISaveSection.WriteSave has no gm parameter, so the pool and
                // usage come off the singleton — widen the interface if that ever hurts.
                ExceptionUsage = GameManager.Instance?.FreeAgents?.GetAllUsage()?
                    .Select(kv => new Data.ExceptionUsageRecord
                    {
                        TeamId = kv.Key,
                        MLEUsed = kv.Value.MLEUsed,
                        BiAnnualUsed = kv.Value.BiAnnualUsed,
                        TwoWayCount = kv.Value.TwoWayCount
                    }).ToList() ?? new List<Data.ExceptionUsageRecord>()
            };
        }

        public void ReadSave(Data.SaveData data, in SaveReadContext ctx)
        {
            var s = data.Offseason;

            // MLE/BAE debits are season state, not offseason state — an in-season load
            // needs them back even though the engine is idle.
            void RestoreExceptionUsage(FreeAgentManager target)
            {
                if (target == null || s?.ExceptionUsage == null) return;
                foreach (var u in s.ExceptionUsage)
                    target.RestoreUsage(u?.TeamId, u?.MLEUsed ?? 0L, u?.BiAnnualUsed ?? false,
                        u?.TwoWayCount ?? 0);
            }

            if (s == null || !s.EngineActive)
            {
                RestoreExceptionUsage(GameManager.Instance?.FreeAgents);
                _engineActive = false;
                return;
            }

            _engineActive = true;
            _seasonLabel = s.SeasonLabel;
            _calendarYear = s.CalendarYear;
            _postSeasonDone = s.PostSeasonDone;
            _draftDone = s.DraftDone;
            _freeAgencyOpen = s.FreeAgencyOpen;
            _summerDone = s.SummerDone;
            _campDone = s.CampDone;
            _campStarted = s.CampStarted;
            _scrimmagesPlayed = s.ScrimmagesPlayed;
            CampFocus = (TrainingFocus)s.CampFocusInt;
            _scrimmageLines.Clear();
            if (s.ScrimmageLines != null) _scrimmageLines.AddRange(s.ScrimmageLines);
            _rng = new System.Random(_seasonLabel * 31 + 7);

            var gm = GameManager.Instance;

            // Preseason games: the regenerated schedule holds regular-season games only,
            // so put ours back — played ones with their score, pending ones playable.
            _preseasonEvents.Clear();
            if (s.PreseasonGames != null && gm != null)
            {
                foreach (var rec in s.PreseasonGames)
                {
                    if (rec == null || string.IsNullOrEmpty(rec.EventId)) continue;
                    var game = new Data.CalendarEvent
                    {
                        EventId = rec.EventId,
                        Type = Data.CalendarEventType.Game,
                        IsPreseason = true,
                        HomeTeamId = rec.HomeTeamId,
                        AwayTeamId = rec.AwayTeamId,
                        IsHomeGame = rec.HomeTeamId == gm.PlayerTeamId,
                        GameNumber = rec.GameNumber,
                        IsCompleted = rec.IsCompleted,
                        HomeScore = rec.HomeScore,
                        AwayScore = rec.AwayScore,
                        Title = $"Preseason vs {gm.GetTeam(rec.HomeTeamId == gm.PlayerTeamId ? rec.AwayTeamId : rec.HomeTeamId)?.Abbreviation}"
                    };
                    if (DateTime.TryParse(rec.DateStr, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var when))
                        game.Date = when;
                    _preseasonEvents.Add(game);
                }
                gm.SeasonController?.AddScheduledEvents(_preseasonEvents);
            }

            var fam = gm?.FreeAgents;
            if (fam != null && s.FreeAgentPool != null)
            {
                fam.Clear();   // pool AND exception usage, so a load doubles neither
                // Pre-O1 saves have no TypeInt/QO fields — they default to UFA/0
                foreach (var record in s.FreeAgentPool)
                    fam.AddFreeAgent(record.PlayerId, (FreeAgentType)record.TypeInt,
                        record.PreviousTeamId, record.ConsecutiveSeasons,
                        record.HasQualifyingOffer, record.QualifyingOfferAmount);

                // Pre-O2 saves have no usage records — teams start the load with
                // untouched exceptions, which is the old behavior. Re-applied here
                // because fam.Clear() above wiped what the early restore put back.
                RestoreExceptionUsage(fam);
            }

            // Market state: cleared then restored (absent = empty market)
            _market = null;
            _pendingQOs.Clear();
            if (gm != null)
            {
                EnsureMarket(gm)?.Restore(s.MarketBids, gm.CurrentDate,
                    s.MarketMarketedIds, s.MarketLastDigest);

                if (s.PendingQualifyingOffers != null)
                    foreach (var q in s.PendingQualifyingOffers)
                    {
                        if (q == null || string.IsNullOrEmpty(q.PlayerId)) continue;
                        var qo = new QualifyingOffer
                        {
                            PlayerId = q.PlayerId,
                            TeamId = q.TeamId,
                            Amount = q.Amount,
                            PriorSalary = q.PriorSalary
                        };
                        if (DateTime.TryParse(q.DeadlineStr, null,
                                System.Globalization.DateTimeStyles.RoundtripKind, out var dl))
                            qo.Deadline = dl;
                        _pendingQOs.Add(qo);
                    }
            }

            // Mid-draft-night load: regenerate the class from the deterministic seed,
            // prune prospects already drafted before the save, restore the order.
            _draftStarted = s.DraftStarted && !s.DraftDone;
            _onTheClock = s.OnTheClock;
            _nextPick = Math.Max(1, s.NextPick);
            _draftOrder1 = s.DraftOrder1 ?? new List<string>();
            _draftOrder2 = s.DraftOrder2 ?? new List<string>();
            // Pre-O4 saves have no slot order — leaving it empty makes ownership
            // refresh a no-op, i.e. exactly the old behavior (owners frozen at tip-off).
            _slotOrder1 = s.SlotOrder1 ?? new List<string>();
            _slotOrder2 = s.SlotOrder2 ?? new List<string>();
            // Pre-O3 saves have no workout state — no invites spent, window unopened.
            // The reports themselves ride in ScoutingData next to the scouting book.
            _workoutInvites.Clear();
            if (s.WorkoutInvites != null) _workoutInvites.AddRange(s.WorkoutInvites);
            _workoutsOpened = s.WorkoutsOpened;
            _workoutsDone = s.WorkoutsDone;
            if (!string.IsNullOrEmpty(s.DraftDayStr) &&
                DateTime.TryParse(s.DraftDayStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dd))
                _draftDay = dd;

            if (_draftStarted && gm != null)
            {
                _draft = new DraftSystem(gm.SalaryCapManager, gm.PlayerDatabase,
                    seed: _seasonLabel * 17 + 3);
                _draft.GenerateDraftClass(_calendarYear);
                int pruned = _draft.RemoveProspects(p =>
                    gm.PlayerDatabase.GetPlayer($"draft_{_calendarYear}_{p.ProspectId}") != null);
                if (_draftOrder1.Count > 0)
                    _draft.SetDraftOrder(_draftOrder1, _draftOrder2);
                // Picks traded before the save are conveyed by the registry, so the
                // remaining slots come back owned by whoever holds them now.
                RefreshDraftOrderOwnership(gm, resumeClock: false);
                Debug.Log($"[Offseason] Mid-draft load: resumed at pick {_nextPick}, pruned {pruned} drafted prospects");
            }
            else
            {
                // Not mid-draft (pre-draft or post-draft save): drop any stale draft pool
                // so WorkoutPool falls back to the preview instead of pointing at a
                // post-draft prospect pool that no longer has the invited players.
                _draft = null;
            }
        }


        public OffseasonManager()
        {
            Instance = this;
        }
    }
}
