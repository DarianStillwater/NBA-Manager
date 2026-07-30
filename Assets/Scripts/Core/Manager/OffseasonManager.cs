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
        private readonly List<string> _playerPickResults = new List<string>();

        // ==================== PUBLIC STATE (Front Office panel) ====================

        public bool EngineActive => _engineActive;
        public int OffseasonCalendarYear => _calendarYear;
        public bool DraftActive => _engineActive && _draftStarted && !_draftDone;
        public bool PlayerOnClock => DraftActive && _onTheClock;
        public int NextPickNumber => _nextPick;
        public DraftSystem DraftBoard => _draft;
        public bool FreeAgencySigningOpen => _engineActive && _freeAgencyOpen && !_campDone;
        public bool ReSignWindowOpen => _engineActive && _postSeasonDone && !_campDone;

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
            _rng = new System.Random(seasonLabel * 31 + 7);

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

                if (!_draftDone && date >= OffseasonDates.Draft(_calendarYear))
                {
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
                // Every team auto-tenders this phase, the player's included —
                // interactive tendering lands in O2.
                if (fam0 != null && !string.IsNullOrEmpty(contract.TeamId) &&
                    IsRfaEligible(player, contract, seasonYear))
                {
                    long qo = fam0.ComputeQualifyingOfferAmount(player, contract.CurrentYearSalary);
                    if (MarketSalary(player) >= qo &&
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

            // Pick ownership: a traded pick belongs to its current owner
            List<string> OwnersFor(int round) => slotOrder
                .Select(original =>
                    gm.DraftPickRegistry?.GetPick(original, _calendarYear, round)?.CurrentOwnerId ?? original)
                .ToList();

            _draftOrder1 = OwnersFor(1);
            _draftOrder2 = OwnersFor(2);
            _draft.SetDraftOrder(_draftOrder1, _draftOrder2);

            _draftStarted = true;
            _draftDay = date;
            _nextPick = 1;
            _onTheClock = false;
            _playerPickResults.Clear();

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
                    inbox?.Publish(InboxMessageType.League, "League Office",
                        $"You're ON THE CLOCK at pick #{_nextPick}",
                        "Make your selection in the Front Office. Advancing the day lets the war room pick best-available.",
                        highPriority: true,
                        deepLinkPanelId: "FrontOffice");
                    return;
                }

                DoAIPick(gm, _nextPick, teamId);
                _nextPick++;
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

            _nextPick++;
            _onTheClock = false;
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
            _nextPick++;
            _onTheClock = false;
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
        public long EstimateMarketSalary(Data.Player p) => MarketSalary(p);

        /// <summary>
        /// Sign a free agent to YOUR team from the Front Office panel. Own free
        /// agents can re-sign from June (Bird rights, before the market opens);
        /// everyone else once free agency opens July 6. Tries Bird rights, then cap
        /// space, then a minimum deal.
        /// </summary>
        /// <summary>
        /// Sign an own free agent at NEGOTIATED terms (from a ContractNegotiationManager
        /// session the agent accepted) — same cap plumbing as the one-shot path, but
        /// the agreed salary and years are honored instead of the market lookup.
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
            annualSalary = Math.Max(1_200_000L, annualSalary);
            bool ownFreeAgent = fa.PreviousTeamId == team.TeamId;

            var methods = ownFreeAgent
                ? new[] { SigningMethod.BirdRights, SigningMethod.CapSpace, SigningMethod.MinimumSalary }
                : new[] { SigningMethod.CapSpace, SigningMethod.MinimumSalary };

            foreach (var method in methods)
            {
                var offer = new SigningOffer
                {
                    AnnualSalary = method == SigningMethod.MinimumSalary ? 1_200_000L : annualSalary,
                    Years = years,
                    Method = method,
                    PlayerYearsExperience = player.YearsPro
                };

                var check = fam.CanSign(team.TeamId, playerId, offer);
                if (!check.IsValid) { failReason = check.Reason; continue; }

                if (fam.ExecuteSigning(team.TeamId, playerId, offer))
                {
                    if (!team.RosterPlayerIds.Contains(playerId))
                        team.RosterPlayerIds.Add(playerId);

                    InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                        $"{player.FullName} re-signs with {team.Name}",
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

            long ask = MarketSalary(player);
            years = Mathf.Clamp(years, 1, 4);

            var methods = ownFreeAgent
                ? new[] { SigningMethod.BirdRights, SigningMethod.CapSpace, SigningMethod.MinimumSalary }
                : new[] { SigningMethod.CapSpace, SigningMethod.MinimumSalary };

            foreach (var method in methods)
            {
                var offer = new SigningOffer
                {
                    AnnualSalary = method == SigningMethod.MinimumSalary ? 1_200_000L : ask,
                    Years = years,
                    Method = method,
                    PlayerYearsExperience = player.YearsPro
                };

                var check = fam.CanSign(team.TeamId, playerId, offer);
                if (!check.IsValid) { failReason = check.Reason; continue; }

                if (fam.ExecuteSigning(team.TeamId, playerId, offer))
                {
                    if (!team.RosterPlayerIds.Contains(playerId))
                        team.RosterPlayerIds.Add(playerId);

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
        /// AI signings, a few per day: best available free agents pick the team with
        /// the most cap room that has a roster spot. Your team only auto-signs cheap
        /// depth if the roster is short (full FA control arrives with the FA screen).
        /// </summary>
        private void RunDailyFreeAgency(GameManager gm, DateTime date)
        {
            var fam = gm.FreeAgents;
            if (fam == null) return;

            var pool = fam.GetFreeAgents();
            if (pool == null || pool.Count == 0) return;

            int signingsToday = Math.Max(2, pool.Count / 12);

            var ranked = pool
                .Select(fa => new { fa, player = gm.PlayerDatabase.GetPlayer(fa.PlayerId) })
                .Where(x => x.player != null && x.player.RetirementYear == 0)
                .OrderByDescending(x => x.player.OverallRating)
                .ToList();

            foreach (var entry in ranked.Take(signingsToday))
            {
                var player = entry.player;

                var suitors = gm.AllTeams.Where(t =>
                        t != null &&
                        t.RosterPlayerIds.Count < 15 &&
                        (t.TeamId != gm.PlayerTeamId ||
                         !Data.RolePermissions.CanMakeRosterMoves ||
                         t.RosterPlayerIds.Count < 13))
                    .OrderByDescending(t => gm.SalaryCapManager.GetCapSpace(t.TeamId))
                    .ToList();
                if (suitors.Count == 0) return;

                // Modest market randomness: one of the top three cap-space teams
                var team = suitors[Math.Min(_rng.Next(3), suitors.Count - 1)];

                long ask = MarketSalary(player);
                long capSpace = gm.SalaryCapManager.GetCapSpace(team.TeamId);
                var offer = new SigningOffer
                {
                    AnnualSalary = Math.Min(ask, Math.Max(capSpace, 1_200_000L)),
                    Years = player.OverallRating >= 80 ? 3 + _rng.Next(2) : 1 + _rng.Next(3),
                    Method = capSpace >= ask ? SigningMethod.CapSpace : SigningMethod.MinimumSalary,
                    PlayerYearsExperience = player.YearsPro
                };
                if (offer.Method == SigningMethod.MinimumSalary) offer.AnnualSalary = 1_200_000L;

                // Restricted free agency: the original team gets first refusal.
                // ponytail: naive match heuristic (keep good players you can pay) —
                // real offer sheets and a player-team match prompt arrive in O2.
                var rfa = entry.fa;
                if (rfa.Type == FreeAgentType.Restricted && rfa.HasQualifyingOffer &&
                    !string.IsNullOrEmpty(rfa.PreviousTeamId) && rfa.PreviousTeamId != team.TeamId)
                {
                    var original = gm.AllTeams.FirstOrDefault(t => t?.TeamId == rfa.PreviousTeamId);
                    bool worthMatching = original != null && original.RosterPlayerIds.Count < 15 &&
                        (player.OverallRating >= 78 ||
                         (player.OverallRating >= 70 &&
                          gm.SalaryCapManager.GetCapSpace(original.TeamId) >= offer.AnnualSalary));

                    if (worthMatching)
                    {
                        var matched = new SigningOffer
                        {
                            AnnualSalary = offer.AnnualSalary,
                            Years = offer.Years,
                            PlayerYearsExperience = offer.PlayerYearsExperience,
                            Method = rfa.BirdRights != Data.BirdRightsType.None
                                ? SigningMethod.BirdRights
                                : SigningMethod.CapSpace
                        };
                        // A match the original team can't legally make is no match at
                        // all — the offer sheet then goes through as signed.
                        if (fam.CanSign(original.TeamId, player.PlayerId, matched).IsValid)
                        {
                            team = original;
                            offer = matched;
                        }
                    }
                }

                if (fam.ExecuteSigning(team.TeamId, player.PlayerId, offer))
                {
                    if (!team.RosterPlayerIds.Contains(player.PlayerId))
                        team.RosterPlayerIds.Add(player.PlayerId);

                    bool notable = player.OverallRating >= 80 || team.TeamId == gm.PlayerTeamId;
                    if (notable)
                        InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                            $"{player.FullName} signs with {team.Name}",
                            $"{offer.Years} years, ${offer.AnnualSalary * offer.Years / 1_000_000f:0.0}M total.",
                            highPriority: team.TeamId == gm.PlayerTeamId);
                }
            }
        }

        private long MarketSalary(Data.Player p)
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
            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                "Training camps open",
                "Pick a daily focus at the Front Office desk. Three preseason scrimmages before opening night.",
                deepLinkPanelId: "FrontOffice");
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

            if (OffseasonDates.IsScrimmageDay(date) && _scrimmagesPlayed < OffseasonDates.ScrimmageDays.Length)
                RunScrimmage(gm, team, roster, date);
        }

        /// <summary>A camp scrimmage: the REAL sim as an exhibition — no W/L, no stats.</summary>
        private void RunScrimmage(GameManager gm, Data.Team team, List<Data.Player> roster, DateTime date)
        {
            var opponents = gm.AllTeams?.Where(t => t != null && t.TeamId != team.TeamId).ToList();
            if (opponents == null || opponents.Count == 0) return;
            var opponent = opponents[_rng.Next(opponents.Count)];

            var sim = new Simulation.GameSimulator(gm.PlayerDatabase);
            var result = sim.SimulateExhibition(team, opponent);
            int us = result.HomeScore, them = result.AwayScore;

            _scrimmagesPlayed++;
            var game = new PreseasonGame
            {
                GameId = $"PRE_{_calendarYear}_{_scrimmagesPlayed}",
                OpponentTeamId = opponent.TeamId
            };
            gm.TrainingCampManager?.SimulatePreseasonGame(game, team, opponent, roster,
                (opponent.RosterPlayerIds ?? new List<string>())
                    .Select(id => gm.PlayerDatabase?.GetPlayer(id)).Where(pl => pl != null).ToList(),
                us, them);

            string line = $"Scrimmage {_scrimmagesPlayed}: {(us > them ? "W" : "L")} {us}-{them} vs {opponent.Name}";
            _scrimmageLines.Add(line);
            InboxService.Instance?.Publish(InboxMessageType.League, "Coaching Staff",
                line, "Preseason reps — the result doesn't count, the tape does.",
                deepLinkPanelId: "FrontOffice");
        }

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

                    var offer = new SigningOffer
                    {
                        AnnualSalary = 1_200_000L,
                        Years = 1,
                        Method = SigningMethod.MinimumSalary,
                        PlayerYearsExperience = best.YearsPro
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
                FreeAgentPool = GameManager.Instance?.FreeAgents?.GetFreeAgents()?
                    .Select(fa => new Data.FreeAgentRecord
                    {
                        PlayerId = fa.PlayerId,
                        PreviousTeamId = fa.PreviousTeamId,
                        ConsecutiveSeasons = fa.ConsecutiveSeasons,
                        TypeInt = (int)fa.Type,
                        HasQualifyingOffer = fa.HasQualifyingOffer,
                        QualifyingOfferAmount = fa.QualifyingOfferAmount
                    }).ToList() ?? new List<Data.FreeAgentRecord>()
            };
        }

        public void ReadSave(Data.SaveData data, in SaveReadContext ctx)
        {
            var s = data.Offseason;
            if (s == null || !s.EngineActive)
            {
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

            var fam = gm?.FreeAgents;
            if (fam != null && s.FreeAgentPool != null)
            {
                fam.Clear();
                // Pre-O1 saves have no TypeInt/QO fields — they default to UFA/0
                foreach (var record in s.FreeAgentPool)
                    fam.AddFreeAgent(record.PlayerId, (FreeAgentType)record.TypeInt,
                        record.PreviousTeamId, record.ConsecutiveSeasons,
                        record.HasQualifyingOffer, record.QualifyingOfferAmount);
            }

            // Mid-draft-night load: regenerate the class from the deterministic seed,
            // prune prospects already drafted before the save, restore the order.
            _draftStarted = s.DraftStarted && !s.DraftDone;
            _onTheClock = s.OnTheClock;
            _nextPick = Math.Max(1, s.NextPick);
            _draftOrder1 = s.DraftOrder1 ?? new List<string>();
            _draftOrder2 = s.DraftOrder2 ?? new List<string>();
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
                Debug.Log($"[Offseason] Mid-draft load: resumed at pick {_nextPick}, pruned {pruned} drafted prospects");
            }
        }


        public OffseasonManager()
        {
            Instance = this;
        }
    }
}
