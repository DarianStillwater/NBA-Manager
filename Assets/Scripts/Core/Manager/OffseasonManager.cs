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

            StartOffseason(_calendarYear, seasonEndDate); // legacy calendar/date bookkeeping

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

                if (!_draftDone && date >= new DateTime(_calendarYear, 6, 22))
                {
                    if (!_draftStarted) StartDraftNight(gm, date);
                    // Advancing past draft night with a pick pending = the clock ran
                    // out; the war room picks best-available and the night resumes.
                    if (_onTheClock && date.Date > _draftDay.Date)
                        AutoPickPending(gm);
                    if (!_onTheClock) ContinueDraft(gm);
                }

                if (_draftDone && !_freeAgencyOpen && date >= new DateTime(_calendarYear, 7, 6))
                { _freeAgencyOpen = true; OpenFreeAgency(gm); }

                if (_freeAgencyOpen && !_campDone)
                    RunDailyFreeAgency(gm, date);

                if (!_summerDone && date >= new DateTime(_calendarYear, 7, 12))
                { RunSummerLeague(gm); _summerDone = true; }

                if (!_campDone && date >= new DateTime(_calendarYear, 9, 27))
                { RunRosterCompliance(gm); _campDone = true; }

                if (_campDone && date >= new DateTime(_calendarYear, 10, 21))
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
                foreach (var p in players)
                {
                    if (p == null || p.RetirementYear > 0) continue;
                    var grow = dev.ProcessSeasonDevelopment(p, p.MinutesPlayedThisSeason, 0.55f);
                    var off = dev.ProcessOffseasonDevelopment(p, 0.55f, 0.5f);
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
            int toMarket = 0;
            foreach (var contract in expired)
            {
                if (retiredIds.Contains(contract.PlayerId)) continue;
                var player = gm.PlayerDatabase.GetPlayer(contract.PlayerId);
                if (player == null) continue;

                gm.FreeAgents?.AddFreeAgent(contract.PlayerId, FreeAgentType.Unrestricted,
                    contract.TeamId, contract.ConsecutiveSeasonsWithTeam);
                RemoveFromRoster(gm, contract.TeamId, contract.PlayerId);
                player.TeamId = "";
                toMarket++;
            }

            inbox?.Publish(InboxMessageType.League, "League Office",
                $"Free-agent class takes shape: {toMarket} players hit the market",
                "Contracts have expired across the league. Free agency opens July 6.");
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

                if (teamId == pid && !string.IsNullOrEmpty(pid))
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
                        (t.TeamId != gm.PlayerTeamId || t.RosterPlayerIds.Count < 13))
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
                sl.StartSummerLeague(_calendarYear);
                var summary = sl.SkipSummerLeague();
                if (summary != null)
                    InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                        "Summer League wraps up",
                        "Rookies and young players got their reps in Las Vegas.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Offseason] Summer league skipped: {ex.Message}");
            }
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
            offseasonActive = false;
            currentPhase = OffseasonPhase.Complete;

            PlayoffManager.Instance?.ResetForNewSeason();
            gm.Development?.SetCurrentSeason(_seasonLabel + 1);
            gm.StartNewSeason();

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
                        ConsecutiveSeasons = 0
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
            _rng = new System.Random(_seasonLabel * 31 + 7);

            var gm = GameManager.Instance;

            var fam = gm?.FreeAgents;
            if (fam != null && s.FreeAgentPool != null)
            {
                foreach (var record in s.FreeAgentPool)
                    fam.AddFreeAgent(record.PlayerId, FreeAgentType.Unrestricted,
                        record.PreviousTeamId, record.ConsecutiveSeasons);
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

        public OffseasonManager()
        {
            Instance = this;
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
