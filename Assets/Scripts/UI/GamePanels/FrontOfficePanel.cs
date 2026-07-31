using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.UI.Shell;
using ContractOffer = NBAHeadCoach.Core.Manager.ContractOffer;
using DraftProspect = NBAHeadCoach.Core.Manager.DraftProspect;
using B = NBAHeadCoach.UI.Shell.UIBuilder;

namespace NBAHeadCoach.UI.GamePanels
{
    /// <summary>
    /// The GM's desk: free agency (offseason market + in-season buyout wire),
    /// the trade center (incoming offers, build-a-trade negotiations, league
    /// news), and the live draft board on draft night.
    /// </summary>
    public class FrontOfficePanel : IGamePanel, IDeepLinkPanel
    {
        private string _tab = "FREE AGENCY";
        private Team _team;
        private Color _teamColor;

        // Trade center state — survives rebuilds (panel instance is long-lived)
        private string _tradePartnerId;
        private readonly HashSet<string> _sendIds = new HashSet<string>();
        private readonly HashSet<string> _getIds = new HashSet<string>();
        // O4: draft picks in the same proposal, keyed "{OriginalTeamId}_{Year}_{Round}"
        private readonly HashSet<string> _sendPickKeys = new HashSet<string>();
        private readonly HashSet<string> _getPickKeys = new HashSet<string>();
        private string _activeNegotiationId;
        private string _status;

        // Contract-negotiation state (re-signing own free agents)
        private string _contractTalkId;
        private string _contractTalkPlayerId;
        private int _offerYears = 2;
        private long _offerSalary = 10_000_000L;
        private bool _offerPlayerOption;
        private bool _offerNoTrade;
        private bool _offerKicker;
        private string _contractTalkMsg;

        // Market browser state (O2): filters + the open bid editor
        private static readonly string[] PosFilters = { "ALL", "PG", "SG", "SF", "PF", "C" };
        private static readonly string[] PriceFilters = { "ALL", "<$10M", "<$20M", "$20M+" };
        private int _faPos;
        private int _faPrice;
        private string _bidPlayerId;
        private int _bidYears = 2;
        private long _bidSalary = 10_000_000L;

        public void SetDeepLinkPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            if (payload.StartsWith("offer:") || payload == "trades") _tab = "TRADES";
        }

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            _team = team;
            _teamColor = teamColor;

            // Draft night takes over the desk
            var off = OffseasonManager.Instance;
            if (off != null && off.DraftActive) _tab = "DRAFT";

            var root = B.Child(parent, "Root");
            B.Stretch(root);
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.padding = new RectOffset(12, 12, 8, 8);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            var rootRT = root.GetComponent<RectTransform>();

            var title = B.Text(rootRT, "Title", "FRONT OFFICE", 18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var tabs = B.Child(rootRT, "Tabs");
            var tabsLE = tabs.AddComponent<LayoutElement>(); tabsLE.preferredHeight = 28; tabsLE.flexibleHeight = 0;
            var hlg = tabs.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            foreach (var name in new[] { "FREE AGENCY", "TRADES", "DRAFT" })
                BuildTabButton(tabs.GetComponent<RectTransform>(), name);

            var bodyGo = B.Child(rootRT, "Body");
            var bodyLE = bodyGo.AddComponent<LayoutElement>(); bodyLE.flexibleHeight = 1;
            var scroll = B.FixedArea(bodyGo.GetComponent<RectTransform>());

            BuildGMDeskCard(scroll);
            if (_tab == "FREE AGENCY") BuildContractTalksCard(scroll, GameManager.Instance);
            if (_tab == "FREE AGENCY") BuildTrainingCampCard(scroll, GameManager.Instance);
            if (_tab == "FREE AGENCY") BuildSummerLeagueReview(scroll, GameManager.Instance);
            if (_tab == "DRAFT") BuildDraft(scroll);
            else if (_tab == "TRADES") BuildTrades(scroll);
            else BuildFreeAgency(scroll);
        }

        private void BuildTabButton(RectTransform parent, string name)
        {
            var go = B.Child(parent, $"Tab_{name}");
            bool active = _tab == name;
            go.AddComponent<Image>().color = active
                ? UITheme.DarkenColor(_teamColor, 0.5f) : UITheme.FMCardHeaderBg;
            go.AddComponent<Button>().onClick.AddListener(() =>
            {
                _tab = name;
                Refresh();
            });
            var t = B.Text(go.GetComponent<RectTransform>(), "T", name, 12,
                active ? FontStyle.Bold : FontStyle.Normal,
                active ? Color.white : UITheme.TextSecondary);
            B.Stretch(t.gameObject); t.alignment = TextAnchor.MiddleCenter;
        }

        private void Refresh() =>
            GameManager.Instance?.GetComponent<GameShell>()?.ShowPanel("FrontOffice");

        // ==================== FREE AGENCY ====================

        private void BuildFreeAgency(RectTransform scroll)
        {
            var gm = GameManager.Instance;
            var off = OffseasonManager.Instance;
            var fam = gm?.FreeAgents;

            // Header: cap situation
            long capSpace = gm?.SalaryCapManager?.GetCapSpace(_team.TeamId) ?? 0;
            int rosterCount = _team?.RosterPlayerIds?.Count ?? 0;
            var header = B.Text(scroll, "CapLine",
                $"Cap space: <b>{Money(capSpace)}</b>   Roster: <b>{rosterCount}/15</b>",
                13, FontStyle.Normal, UITheme.TextSecondary);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            bool offseasonDesk = off != null && off.EngineActive;

            if (fam == null)
            {
                Empty(scroll, "Free agency data unavailable.");
                return;
            }

            if (!offseasonDesk)
            {
                BuildInSeasonMarket(scroll, gm, fam);
                return;
            }

            if (!string.IsNullOrEmpty(_status))
            {
                var note = B.Text(scroll, "Status", _status, 12, FontStyle.Italic, UITheme.Warning);
                note.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            BuildMarketWire(scroll, off);
            BuildRestrictedCard(scroll, gm, off);

            var pool = fam.GetFreeAgents();
            var ours = pool.Where(fa => fa.PreviousTeamId == _team.TeamId).ToList();
            var market = pool.Where(fa => fa.PreviousTeamId != _team.TeamId).ToList();

            BuildFaSection(scroll, "YOUR FREE AGENTS", ours, gm, off, true);

            if (!off.FreeAgencySigningOpen)
            {
                var note = B.Text(scroll, "MarketNote",
                    "The open market unlocks July 6 — until then only your own free agents can re-sign.",
                    12, FontStyle.Italic, UITheme.Warning);
                note.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            var marketed = new HashSet<string>();
            if (off.FreeAgencySigningOpen && off.Market != null)
            {
                marketed = new HashSet<string>(off.Market.MarketedPlayerIds);
                BuildMarketBrowser(scroll, gm, off);
            }

            BuildFaSection(scroll, marketed.Count > 0 ? "OTHER FREE AGENTS" : "OPEN MARKET", market
                .Where(fa => !marketed.Contains(fa.PlayerId))
                .OrderByDescending(fa => gm.PlayerDatabase.GetPlayer(fa.PlayerId)?.OverallRating ?? 0)
                .Take(40).ToList(), gm, off, off.FreeAgencySigningOpen);
        }

        // ==================== MARKET BROWSER (O2) ====================

        /// <summary>Yesterday's league-wide signings, straight from the market digest.</summary>
        private void BuildMarketWire(RectTransform scroll, OffseasonManager off)
        {
            string digest = off.Market?.LastDigest;
            if (string.IsNullOrEmpty(digest)) return;

            int lines = digest.Split('\n').Length;
            var card = B.Card(scroll, "MARKET WIRE", UITheme.AccentSecondary);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 + lines * 16;
            var rt = CardBody(card);
            var text = B.Text(rt, "Digest", digest, 11, FontStyle.Normal, UITheme.TextSecondary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = lines * 16;
            text.alignment = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// The bidding floor: who's drawing interest, what his agent is saying, when he
        /// decides, and your own bid. Filters are panel-local cycle buttons.
        /// </summary>
        private void BuildMarketBrowser(RectTransform scroll, GameManager gm, OffseasonManager off)
        {
            var mkt = off.Market;
            // Your own free agents belong to the YOUR FREE AGENTS card, not the market
            var ownIds = new HashSet<string>((gm.FreeAgents?.GetFreeAgents() ?? new List<FreeAgent>())
                .Where(fa => fa.PreviousTeamId == _team.TeamId).Select(fa => fa.PlayerId));
            var rows = mkt.MarketedPlayerIds
                .Where(id => !ownIds.Contains(id))
                .Select(id => gm.PlayerDatabase.GetPlayer(id))
                .Where(p => p != null && p.RetirementYear == 0 && PassesFilters(off, p))
                .ToList();

            var card = B.Card(scroll, "OPEN MARKET — THE BIDDING", _teamColor);
            bool editing = rows.Any(p => p.PlayerId == _bidPlayerId);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                44 + 28 + Mathf.Max(1, rows.Count) * 40 + (editing ? 30 : 0);
            var rt = CardBody(card);

            // Filter row
            var filters = B.Child(rt, "Filters");
            filters.AddComponent<LayoutElement>().preferredHeight = 26;
            var fh = filters.AddComponent<HorizontalLayoutGroup>();
            fh.childControlWidth = true; fh.childControlHeight = true;
            fh.childForceExpandWidth = false; fh.spacing = 6;
            var frt = filters.GetComponent<RectTransform>();
            Label(frt, "Filter:", 46);
            SmallBtn(frt, PosFilters[_faPos], 52,
                () => { _faPos = (_faPos + 1) % PosFilters.Length; Refresh(); });
            SmallBtn(frt, PriceFilters[_faPrice], 66,
                () => { _faPrice = (_faPrice + 1) % PriceFilters.Length; Refresh(); });

            if (rows.Count == 0)
            {
                var none = B.Text(rt, "None", "Nobody on the market fits that filter.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            bool viaGM = !NBAHeadCoach.Core.Data.RolePermissions.CanMakeRosterMoves;
            foreach (var player in rows)
            {
                string pid = player.PlayerId;
                var mine = mkt.GetPlayerTeamBid(pid);
                var day = mkt.GetDecisionDay(pid);
                long ask = off.EstimateMarketSalary(player);

                string flags = mkt.IsContested(pid, _team.TeamId) ? "  ·  <color=#EAB308>CONTESTED</color>" : "";
                if (day.HasValue)
                {
                    int days = (day.Value.Date - gm.CurrentDate.Date).Days;
                    flags += days <= 0 ? "  ·  decides today" : $"  ·  decides in {days}d";
                }
                string yours = mine != null
                    ? $"  ·  <color=white>your bid: {mine.Years}yr at {Money(mine.AnnualAverage)}/yr</color>" : "";

                var row = PlayerRow(rt, $"MK_{pid}",
                    $"{PlayerLine(gm, player, null)}  ·  asks {Money(ask)}/yr{flags}{yours}\n" +
                    $"<i>{mkt.GetAgentIntel(pid)}</i>");
                row.GetComponent<LayoutElement>().preferredHeight = 38;

                var bidFor = mine;
                RowButton(row, "Bid", viaGM ? "ASK GM" : (mine != null ? "RAISE" : "BID"),
                    UITheme.AccentPrimary, () =>
                {
                    _bidPlayerId = pid;
                    _bidYears = bidFor?.Years ?? 2;
                    _bidSalary = bidFor?.AnnualAverage ?? ask;
                    Refresh();
                }, width: 70);

                RowButton(row, "Negotiate", "NEGOTIATE", UITheme.AccentSecondary, () =>
                {
                    Action doTalk = () => OpenContractTalks(gm, off, player);
                    if (viaGM) AskGM(NBAHeadCoach.Core.Data.RosterRequest.CreateSigningRequest(
                        pid, player.FullName, "He's worth a real offer."), doTalk);
                    else { doTalk(); Refresh(); }
                }, width: 84);

                if (mine != null)
                    RowButton(row, "Withdraw", "WITHDRAW", UITheme.Danger, () =>
                    {
                        mkt.WithdrawBid(pid);
                        _status = $"You pulled your offer to {player.FullName}.";
                        if (_bidPlayerId == pid) _bidPlayerId = null;
                        Refresh();
                    }, width: 84);

                if (_bidPlayerId == pid) BuildBidEditor(rt, mkt, player, viaGM);
            }
        }

        /// <summary>Inline offer editor: same steppers as the contract table.</summary>
        private void BuildBidEditor(RectTransform rt, FreeAgencyMarket mkt, Player player, bool viaGM)
        {
            var terms = B.Child(rt, "BidTerms");
            terms.AddComponent<LayoutElement>().preferredHeight = 26;
            var th = terms.AddComponent<HorizontalLayoutGroup>();
            th.childControlWidth = true; th.childControlHeight = true;
            th.childForceExpandWidth = false; th.spacing = 6;
            var trt = terms.GetComponent<RectTransform>();

            SmallBtn(trt, "-", 22, () => { _bidYears = Mathf.Max(1, _bidYears - 1); Refresh(); });
            Label(trt, $"{_bidYears} yrs", 46);
            SmallBtn(trt, "+", 22, () => { _bidYears = Mathf.Min(5, _bidYears + 1); Refresh(); });
            long floor = mkt.MinSalaryFor(player.PlayerId);
            SmallBtn(trt, "-", 22, () => { _bidSalary = Math.Max(floor, _bidSalary - 1_000_000L); Refresh(); });
            Label(trt, $"${_bidSalary / 1_000_000f:F1}M/yr", 76);
            SmallBtn(trt, "+", 22, () => { _bidSalary += 1_000_000L; Refresh(); });

            string pid = player.PlayerId;
            int years = _bidYears;
            long salary = _bidSalary;
            RowButton(terms, "Place", viaGM ? "ASK GM" : "PLACE BID", UITheme.Success, () =>
            {
                Action doBid = () =>
                {
                    if (mkt.PlaceBid(pid, years, salary, out string why))
                    {
                        // The market may have trimmed the terms to fit the cap
                        var placed = mkt.GetPlayerTeamBid(pid);
                        _status = $"Your offer to {player.FullName}: " +
                                  $"{placed?.Years ?? years} yrs at " +
                                  $"{Money(placed?.AnnualAverage ?? salary)}/yr.";
                    }
                    else _status = $"Bid rejected: {why}";
                    _bidPlayerId = null;
                };
                if (viaGM) AskGM(NBAHeadCoach.Core.Data.RosterRequest.CreateSigningRequest(
                    pid, player.FullName, "Put this offer on the table."), doBid);
                else { doBid(); Refresh(); }
            }, width: 96);
            RowButton(terms, "Cancel", "CANCEL", UITheme.Warning,
                () => { _bidPlayerId = null; Refresh(); }, width: 76);
        }

        private bool PassesFilters(OffseasonManager off, Player player)
        {
            if (_faPos > 0 && PositionShort(player.Position) != PosFilters[_faPos]) return false;
            long ask = off.EstimateMarketSalary(player);
            return _faPrice switch
            {
                1 => ask < 10_000_000L,
                2 => ask < 20_000_000L,
                3 => ask >= 20_000_000L,
                _ => true
            };
        }

        /// <summary>Qualifying offers awaiting a tender call, plus offer sheets to match.</summary>
        private void BuildRestrictedCard(RectTransform scroll, GameManager gm, OffseasonManager off)
        {
            var qos = off.PendingQualifyingOffers ?? (IReadOnlyList<QualifyingOffer>)new List<QualifyingOffer>();
            var sheets = off.Market?.PendingMatchDecisions
                ?? (IReadOnlyList<RestrictedFreeAgentStatus>)new List<RestrictedFreeAgentStatus>();
            if (qos.Count == 0 && sheets.Count == 0) return;

            var card = B.Card(scroll, "RESTRICTED / QUALIFYING OFFERS", UITheme.AccentPrimary);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                44 + (qos.Count + sheets.Count) * 26;
            var rt = CardBody(card);
            bool viaGM = !NBAHeadCoach.Core.Data.RolePermissions.CanMakeRosterMoves;

            foreach (var qo in qos.ToList())
            {
                var player = gm.PlayerDatabase.GetPlayer(qo.PlayerId);
                if (player == null) continue;
                string pid = qo.PlayerId;
                var row = PlayerRow(rt, $"QO_{pid}",
                    $"{player.FullName}  ·  {PositionShort(player.Position)}  ·  QO {Money((long)qo.Amount)}/yr  " +
                    $"·  made {Money(qo.PriorSalary)} last year  ·  deadline {qo.Deadline:MMM d}");

                RowButton(row, "Tender", viaGM ? "ASK GM" : "TENDER", UITheme.Success,
                    () => ResolveQO(gm, off, player, pid, true, viaGM), width: 84);
                RowButton(row, "Withhold", viaGM ? "ASK GM" : "WITHHOLD", UITheme.Danger,
                    () => ResolveQO(gm, off, player, pid, false, viaGM), width: 84);
            }

            foreach (var sheet in sheets.ToList())
            {
                var player = gm.PlayerDatabase.GetPlayer(sheet.PlayerId);
                var offer = sheet.OfferSheets?.FirstOrDefault();
                if (player == null || offer == null) continue;
                string pid = sheet.PlayerId;

                var row = PlayerRow(rt, $"RFA_{pid}",
                    $"{player.FullName}  ·  offer sheet from {TeamAbbr(offer.TeamId)}: " +
                    $"{offer.Years}yr at {Money(offer.AnnualAverage)}/yr  ·  match by {sheet.MatchDeadline:MMM d}");

                RowButton(row, "Match", viaGM ? "ASK GM" : "MATCH", UITheme.Success,
                    () => ResolveMatch(gm, off, player, pid, true, viaGM), width: 84);
                RowButton(row, "Walk", viaGM ? "ASK GM" : "LET WALK", UITheme.Danger,
                    () => ResolveMatch(gm, off, player, pid, false, viaGM), width: 84);
            }
        }

        private void ResolveQO(GameManager gm, OffseasonManager off, Player player, string pid,
            bool tender, bool viaGM)
        {
            Action act = () =>
            {
                bool ok = off.SubmitQualifyingOfferDecision(gm, pid, tender, out string why);
                _status = !ok ? $"Couldn't do that: {why}"
                    : tender ? $"Qualifying offer tendered to {player.FullName} — he's restricted."
                             : $"No qualifying offer for {player.FullName} — he walks unrestricted.";
            };
            if (viaGM) AskGM(NBAHeadCoach.Core.Data.RosterRequest.CreateSigningRequest(
                pid, player.FullName, tender ? "Tender him the qualifying offer." : "Let him walk."), act);
            else { act(); Refresh(); }
        }

        private void ResolveMatch(GameManager gm, OffseasonManager off, Player player, string pid,
            bool match, bool viaGM)
        {
            Action act = () =>
            {
                bool ok = off.Market.ResolveMatchDecision(pid, match, out string why);
                _status = !ok ? $"Couldn't do that: {why}"
                    : match ? $"You matched the sheet — {player.FullName} stays."
                            : $"{player.FullName} leaves on the offer sheet.";
            };
            if (viaGM) AskGM(NBAHeadCoach.Core.Data.RosterRequest.CreateSigningRequest(
                pid, player.FullName, match ? "Match the sheet and keep him." : "Let him go."), act);
            else { act(); Refresh(); }
        }

        /// <summary>In-season buyout wire: leftover pool, minimum deals only.</summary>
        // ==================== COACH-ONLY: THE GM DESK ====================

        /// <summary>
        /// Coach-only mode: roster moves go through the AI GM as requests. Approval
        /// executes the move; denial comes back with the GM's reasoning. Either way
        /// the verdict lands in the inbox and the status line.
        /// </summary>
        private void AskGM(NBAHeadCoach.Core.Data.RosterRequest request, Action onApproved)
        {
            NBAHeadCoach.Core.Manager.AIGMSystem.EnsureInitialized(GameManager.Instance);
            var gmCtl = NBAHeadCoach.Core.AI.AIGMController.Instance;
            var result = gmCtl.ProcessRequest(request);

            if (result?.IsApproved == true)
            {
                // Status first: if the approved action itself fails, its message wins
                _status = $"GM approved: \"{result.GMResponse}\"";
                onApproved?.Invoke();
            }
            else
            {
                _status = $"GM declined: \"{result?.GMResponse ?? "No answer."}\"";
            }

            NBAHeadCoach.Core.Manager.InboxService.Instance?.Publish(
                NBAHeadCoach.Core.Manager.InboxMessageType.League,
                gmCtl.GMName ?? "Front Office",
                result?.IsApproved == true ? "Request approved" : "Request denied",
                result?.GMResponse ?? "", deepLinkPanelId: "FrontOffice");
            Refresh();
        }

        private void BuildGMDeskCard(RectTransform scroll)
        {
            if (NBAHeadCoach.Core.Data.RolePermissions.CanMakeRosterMoves) return;

            NBAHeadCoach.Core.Manager.AIGMSystem.EnsureInitialized(GameManager.Instance);
            var card = B.Card(scroll, "YOUR GENERAL MANAGER", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;
            var body = CardBody(card);
            var t = B.Text(body, "Desc",
                NBAHeadCoach.Core.AI.AIGMController.Instance.GetKnownPersonalityDescription(),
                11, FontStyle.Normal, UITheme.TextSecondary);
            t.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        }

        /// <summary>Camp desk: daily focus, latest report, scrimmage results, cut watch.</summary>
        private void BuildTrainingCampCard(RectTransform scroll, GameManager gm)
        {
            var off = gm.Offseason;
            if (off == null || !off.CampInProgress) return;
            var tc = gm.TrainingCampManager;
            var status = tc?.GetStatus();
            if (status == null) return;

            var card = B.Card(scroll, $"TRAINING CAMP — DAY {status.Day}, {CampPhaseWord(status.Phase)}", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;
            var rt = CardBody(card);

            // Focus selector
            var focusRow = B.Child(rt, "FocusRow");
            focusRow.AddComponent<LayoutElement>().preferredHeight = 26;
            var fh = focusRow.AddComponent<HorizontalLayoutGroup>();
            fh.childControlWidth = true; fh.childControlHeight = true;
            fh.childForceExpandWidth = false; fh.spacing = 8;
            var fl = B.Text(focusRow.GetComponent<RectTransform>(), "L",
                $"Today's focus: {FocusWord(off.CampFocus)}", 12, FontStyle.Bold, UITheme.TextPrimary);
            fl.gameObject.AddComponent<LayoutElement>().preferredWidth = 280;
            var fbtn = B.Child(focusRow.GetComponent<RectTransform>(), "Cycle");
            fbtn.AddComponent<LayoutElement>().preferredWidth = 110;
            fbtn.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.AccentSecondary, 0.5f);
            fbtn.AddComponent<Button>().onClick.AddListener(() =>
            {
                var values = (TrainingFocus[])Enum.GetValues(typeof(TrainingFocus));
                off.CampFocus = values[((int)off.CampFocus + 1) % values.Length];
                Refresh();
            });
            var fbt = B.Text(fbtn.GetComponent<RectTransform>(), "T", "CHANGE", 11, FontStyle.Bold, Color.white);
            B.Stretch(fbt.gameObject); fbt.alignment = TextAnchor.MiddleCenter;

            // Latest report, in words
            var report = off.LastCampReport;
            if (report != null)
            {
                string standouts = report.StandoutPerformers.Count > 0
                    ? string.Join(", ", report.StandoutPerformers.Select(sp => sp.PlayerName)) : "nobody yet";
                string strugglers = report.StrugglingPlayers.Count > 0
                    ? string.Join(", ", report.StrugglingPlayers.Select(sp => sp.PlayerName)) : "nobody";
                string chem = report.ChemistryGain > 1.2f ? "Chemistry is building fast."
                    : report.ChemistryGain > 0.4f ? "Chemistry is coming along." : "Chemistry work has been light.";
                var rep = B.Text(rt, "Report",
                    $"Standing out: {standouts}\nStruggling: {strugglers}\n{chem}",
                    11, FontStyle.Normal, UITheme.TextSecondary);
                rep.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;

                foreach (var inj in report.Injuries)
                {
                    var it = B.Text(rt, "Inj", $"⚠ {inj.PlayerName} — {inj.Severity}, out ~{inj.DaysOut} days",
                        11, FontStyle.Bold, UITheme.Warning);
                    it.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
                }
            }

            foreach (var line in off.ScrimmageLines)
            {
                var lt = B.Text(rt, "Scrim", line, 11, FontStyle.Italic, UITheme.TextSecondary);
                lt.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
            }
        }

        private static string CampPhaseWord(TrainingCampPhase phase) => phase switch
        {
            TrainingCampPhase.EarlyCamp => "CONDITIONING",
            TrainingCampPhase.MidCamp => "SCRIMMAGES",
            TrainingCampPhase.Preseason => "PRESEASON",
            TrainingCampPhase.FinalCuts => "FINAL CUTS",
            _ => phase.ToString().ToUpper()
        };

        private static string FocusWord(TrainingFocus focus) => focus switch
        {
            TrainingFocus.Offense => "Offensive sets",
            TrainingFocus.Defense => "Defensive principles",
            TrainingFocus.Conditioning => "Conditioning",
            TrainingFocus.Shooting => "Shooting drills",
            TrainingFocus.PlaybookInstallation => "Playbook installation",
            TrainingFocus.TeamBuilding => "Team building",
            TrainingFocus.PlayerEvaluation => "Player evaluation",
            _ => focus.ToString()
        };

        /// <summary>Post-Vegas review: your summer squad's lines and scout reads.</summary>
        private void BuildSummerLeagueReview(RectTransform scroll, GameManager gm)
        {
            var sl = gm.SummerLeagueManager;
            var mine = sl?.GetStandings()?.FirstOrDefault(t => t.TeamId == gm.PlayerTeamId);
            if (mine == null || mine.RosterStats == null || mine.RosterStats.Count == 0) return;
            if (mine.GamesPlayed == 0) return;

            var card = B.Card(scroll, $"SUMMER LEAGUE REVIEW — {mine.Wins}-{mine.Losses}", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                70 + mine.RosterStats.Count * 40;
            var rt = CardBody(card);

            foreach (var line in mine.RosterStats.OrderByDescending(r => r.PointsPerGame))
            {
                string marker = line.ExceededExpectations ? " \u2b50" : line.Disappointed ? " \u26a0" : "";
                var head = B.Text(rt, $"SLH_{line.PlayerId}",
                    $"{line.PlayerName}{marker}  \u00b7  {line.PointsPerGame:F1} pts, {line.ReboundsPerGame:F1} reb, {line.AssistsPerGame:F1} ast",
                    12, FontStyle.Bold, UITheme.TextPrimary);
                head.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
                if (!string.IsNullOrEmpty(line.ScoutingNotes))
                {
                    var note = B.Text(rt, $"SLN_{line.PlayerId}", line.ScoutingNotes,
                        11, FontStyle.Italic, UITheme.TextSecondary);
                    note.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
                }
            }
        }

        private void BuildInSeasonMarket(RectTransform scroll, GameManager gm, FreeAgentManager fam)
        {
            if (!string.IsNullOrEmpty(_status))
            {
                var note = B.Text(scroll, "Status", _status, 12, FontStyle.Italic, UITheme.Warning);
                note.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            var pool = fam.GetFreeAgents()
                .OrderByDescending(fa => gm.PlayerDatabase.GetPlayer(fa.PlayerId)?.OverallRating ?? 0)
                .Take(30).ToList();

            var card = B.Card(scroll, "AVAILABLE FREE AGENTS — REST-OF-SEASON MINIMUM DEALS", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + Mathf.Max(1, pool.Count) * 26);

            var rt = CardBody(card);
            if (pool.Count == 0)
            {
                var none = B.Text(rt, "None", "The wire is quiet — nobody worth a call right now.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            bool hasRoom = (_team?.RosterPlayerIds?.Count ?? 15) < 15;
            foreach (var fa in pool)
            {
                var player = gm.PlayerDatabase.GetPlayer(fa.PlayerId);
                if (player == null || player.RetirementYear > 0) continue;

                var row = PlayerRow(rt, $"FA_{fa.PlayerId}", PlayerLine(gm, player, null));
                if (!hasRoom) continue;

                string pid = fa.PlayerId;
                bool viaGM = !NBAHeadCoach.Core.Data.RolePermissions.CanMakeRosterMoves;
                RowButton(row, "Sign", viaGM ? "ASK GM" : "SIGN", UITheme.Success, () =>
                {
                    Action doSign = () =>
                    {
                        string why = "desk unavailable.";
                        bool ok = gm.InSeasonSigning != null &&
                                  gm.InSeasonSigning.SignFreeAgentToPlayerTeam(gm, pid, out why);
                        _status = ok ? $"{player.FullName} signed for the rest of the season."
                                     : $"Signing blocked: {why}";
                    };
                    if (viaGM)
                        AskGM(NBAHeadCoach.Core.Data.RosterRequest.CreateSigningRequest(
                            pid, player.FullName, "We need this player for the stretch run."), doSign);
                    else { doSign(); Refresh(); }
                });
            }
        }

        private void BuildFaSection(RectTransform scroll, string headerText, List<FreeAgent> list,
            GameManager gm, OffseasonManager off, bool canSign)
        {
            // Your own free agents can be under rival pressure too — those rows carry
            // the agent-intel line, so they need the extra height
            var mkt = off.Market;
            bool Contested(FreeAgent fa) => mkt != null && fa.PreviousTeamId == _team.TeamId &&
                                            mkt.IsContested(fa.PlayerId, _team.TeamId);

            var card = B.Card(scroll, headerText, _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + list.Count * 26 + list.Count(Contested) * 14);

            var rt = CardBody(card);

            if (list.Count == 0)
            {
                var none = B.Text(rt, "None", "Nobody here right now.", 12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            foreach (var fa in list)
            {
                var player = gm.PlayerDatabase.GetPlayer(fa.PlayerId);
                if (player == null || player.RetirementYear > 0) continue;

                long ask = off.EstimateMarketSalary(player);
                bool contested = Contested(fa);
                var row = PlayerRow(rt, $"FA_{fa.PlayerId}",
                    $"{PlayerLine(gm, player, null)}  ·  asks {Money(ask)}/yr" +
                    (contested
                        ? $"  ·  <color=#EAB308>CONTESTED</color>\n<i>{mkt.GetAgentIntel(fa.PlayerId)}</i>"
                        : ""));
                if (contested) row.GetComponent<LayoutElement>().preferredHeight = 40;

                if (canSign)
                {
                    string pid = fa.PlayerId;
                    bool ownFa = fa.PreviousTeamId == _team.TeamId;
                    bool viaGM = !NBAHeadCoach.Core.Data.RolePermissions.CanMakeRosterMoves;
                    string label = viaGM ? "ASK GM" : (ownFa ? "NEGOTIATE" : "SIGN");
                    RowButton(row, "Sign", label, UITheme.Success, () =>
                    {
                        Action doSign = () =>
                        {
                            bool ok = off.SignFreeAgentToPlayerTeam(GameManager.Instance, pid, 2, out string why);
                            if (!ok) Debug.LogWarning($"[FrontOffice] Signing failed: {why}");
                        };
                        if (viaGM)
                        {
                            AskGM(NBAHeadCoach.Core.Data.RosterRequest.CreateSigningRequest(
                                pid, player.FullName, "I want this player in camp."), doSign);
                        }
                        else if (ownFa)
                        {
                            OpenContractTalks(GameManager.Instance, off, player);
                            Refresh();
                        }
                        else { doSign(); Refresh(); }
                    });
                }
            }
        }

        // ==================== CONTRACT TALKS (RE-SIGNING) ====================

        private void OpenContractTalks(GameManager gm, OffseasonManager off, Player player)
        {
            var neg = gm.ContractNegotiationManager;
            if (neg == null) return;

            long market = off.EstimateMarketSalary(player);
            bool star = player.OverallRating >= 82;
            bool vet = player.YearsPro >= 8;
            var session = neg.StartNegotiation(player.PlayerId, player.FullName, _team.TeamId,
                market, PlayerPriorities.Generate(vet, star, player.YearsPro <= 3));

            _contractTalkId = session.NegotiationId;
            _contractTalkPlayerId = player.PlayerId;
            _offerYears = 2;
            _offerSalary = market;
            _offerPlayerOption = false;
            _offerNoTrade = false;
            _offerKicker = false;
            _contractTalkMsg = gm.ContractNegotiationManager
                .GetSalaryIntel(player.PlayerId, _team.TeamId, market);
        }

        private void BuildContractTalksCard(RectTransform scroll, GameManager gm)
        {
            if (string.IsNullOrEmpty(_contractTalkId)) return;
            var neg = gm.ContractNegotiationManager;
            var session = neg?.GetNegotiation(_contractTalkId);
            if (session == null) { _contractTalkId = null; return; }

            var agent = gm.AgentManager?.GetAgentForPlayer(session.PlayerId);
            var card = B.Card(scroll, $"CONTRACT TALKS — {session.PlayerName}", UITheme.AccentPrimary);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 210;
            var rt = CardBody(card);

            var head = B.Text(rt, "Agent",
                $"Across the table: {agent?.Name ?? "the agent"}   ·   Round {session.CurrentRound + 1} of at most {session.MaxRoundsBeforeWalkaway}",
                11, FontStyle.Italic, UITheme.TextSecondary);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            if (!string.IsNullOrEmpty(_contractTalkMsg))
            {
                var msg = B.Text(rt, "Msg", _contractTalkMsg, 11, FontStyle.Normal, UITheme.TextPrimary);
                msg.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            }
            if (session.CurrentAgentCounter != null)
            {
                var c = session.CurrentAgentCounter;
                var ct = B.Text(rt, "Counter",
                    $"Their counter: {c.Years} yrs at ${c.AnnualSalary / 1_000_000f:F1}M per",
                    11, FontStyle.Bold, UITheme.Warning);
                ct.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            }

            // Terms row: years / salary steppers + option toggles
            var terms = B.Child(rt, "Terms");
            terms.AddComponent<LayoutElement>().preferredHeight = 26;
            var th = terms.AddComponent<HorizontalLayoutGroup>();
            th.childControlWidth = true; th.childControlHeight = true;
            th.childForceExpandWidth = false; th.spacing = 6;
            var trt = terms.GetComponent<RectTransform>();

            SmallBtn(trt, "-", 22, () => { _offerYears = Mathf.Max(1, _offerYears - 1); Refresh(); });
            Label(trt, $"{_offerYears} yrs", 46);
            SmallBtn(trt, "+", 22, () => { _offerYears = Mathf.Min(5, _offerYears + 1); Refresh(); });
            SmallBtn(trt, "-", 22, () => { _offerSalary = Math.Max(1_200_000L, _offerSalary - 500_000L); Refresh(); });
            Label(trt, $"${_offerSalary / 1_000_000f:F1}M/yr", 76);
            SmallBtn(trt, "+", 22, () => { _offerSalary += 500_000L; Refresh(); });
            SmallBtn(trt, _offerPlayerOption ? "PO ✓" : "PO", 44,
                () => { _offerPlayerOption = !_offerPlayerOption; Refresh(); });
            SmallBtn(trt, _offerNoTrade ? "NTC ✓" : "NTC", 48,
                () => { _offerNoTrade = !_offerNoTrade; Refresh(); });
            SmallBtn(trt, _offerKicker ? "KICKER ✓" : "KICKER", 66,
                () => { _offerKicker = !_offerKicker; Refresh(); });

            // Actions
            var actions = B.Child(rt, "Actions");
            actions.AddComponent<LayoutElement>().preferredHeight = 28;
            var ah = actions.AddComponent<HorizontalLayoutGroup>();
            ah.childControlWidth = true; ah.childControlHeight = true;
            ah.childForceExpandWidth = false; ah.spacing = 8;
            var art = actions.GetComponent<RectTransform>();

            RowButton(art.gameObject, "Submit", "SUBMIT OFFER", UITheme.Success,
                () => SubmitContractOffer(gm), width: 130);
            RowButton(art.gameObject, "Walk", "WALK AWAY", UITheme.Warning, () =>
            {
                gm.ContractNegotiationManager?.AbandonNegotiation(_contractTalkId);
                _contractTalkId = null;
                _status = "You walked away from the table.";
                Refresh();
            }, width: 110);
        }

        private void SubmitContractOffer(GameManager gm)
        {
            var neg = gm.ContractNegotiationManager;
            var session = neg?.GetNegotiation(_contractTalkId);
            if (session == null) { _contractTalkId = null; Refresh(); return; }

            var offer = new ContractOffer
            {
                OfferId = Guid.NewGuid().ToString(),
                OfferingTeamId = _team.TeamId,
                PlayerId = session.PlayerId,
                Years = _offerYears,
                AnnualSalary = _offerSalary,
                TotalValue = _offerYears * _offerSalary,
                HasPlayerOption = _offerPlayerOption,
                PlayerOptionYear = _offerPlayerOption ? _offerYears : 0,
                HasNoTradeClause = _offerNoTrade,
                HasTradeKicker = _offerKicker,
                TradeKickerPercent = _offerKicker ? 15f : 0f,
                IncentiveDescriptions = new System.Collections.Generic.List<string>(),
                OfferDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddDays(7)
            };

            var response = neg.SubmitOffer(_contractTalkId, offer);
            _contractTalkMsg = response?.Message ?? "";

            switch (response?.ResultingStatus)
            {
                case NegotiationStatus.Accepted:
                    bool ok = gm.Offseason.FinalizeNegotiatedSigning(gm, session.PlayerId,
                        _offerYears, _offerSalary, out string why);
                    _status = ok
                        ? $"{session.PlayerName} agreed: {_offerYears} yrs, ${_offerSalary * _offerYears / 1_000_000f:F1}M total."
                        : $"Terms agreed but the signing failed: {why}";
                    _contractTalkId = null;
                    break;

                case NegotiationStatus.CounterOfferReceived:
                    var counter = neg.GetNegotiation(_contractTalkId)?.CurrentAgentCounter;
                    if (counter != null)
                    {
                        _offerYears = Mathf.Clamp(counter.Years, 1, 5);
                        _offerSalary = counter.AnnualSalary;
                    }
                    break;

                case NegotiationStatus.Rejected:
                case NegotiationStatus.WalkedAway:
                    _status = $"Talks with {session.PlayerName} are over: {response.Message}";
                    _contractTalkId = null;
                    break;
            }
            Refresh();
        }

        private void SmallBtn(RectTransform parent, string label, float width, Action onClick)
        {
            var go = B.Child(parent, $"Btn_{label}");
            go.AddComponent<LayoutElement>().preferredWidth = width;
            go.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.AccentSecondary, 0.55f);
            go.AddComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var t = B.Text(go.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Bold, Color.white);
            B.Stretch(t.gameObject); t.alignment = TextAnchor.MiddleCenter;
        }

        private void Label(RectTransform parent, string text, float width)
        {
            var t = B.Text(parent, $"L_{text}", text, 12, FontStyle.Bold, UITheme.TextPrimary);
            t.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            t.alignment = TextAnchor.MiddleCenter;
        }

        // ==================== TRADES ====================

        private void BuildTrades(RectTransform scroll)
        {
            var gm = GameManager.Instance;
            if (gm == null) { Empty(scroll, "Trade desk unavailable."); return; }

            if (!string.IsNullOrEmpty(_status))
            {
                var note = B.Text(scroll, "Status", _status, 12, FontStyle.Italic, UITheme.Warning);
                note.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            }

            bool pastDeadline = LeagueCBA.IsPastTradeDeadline(gm.CurrentDate);
            if (pastDeadline)
            {
                var note = B.Text(scroll, "Deadline",
                    "The trade deadline has passed — the market reopens after the season.",
                    13, FontStyle.Italic, UITheme.Warning);
                note.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            }
            else
            {
                BuildIncomingOffers(scroll, gm);
                BuildActiveNegotiation(scroll, gm);
                BuildProposalBuilder(scroll, gm);
            }

            BuildRecentTrades(scroll, gm);
        }

        private void BuildIncomingOffers(RectTransform scroll, GameManager gm)
        {
            var offers = gm.TradeOfferGenerator?.GetPendingOffers() ?? new List<IncomingTradeOffer>();
            var card = B.Card(scroll, "INCOMING OFFERS", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + Mathf.Max(1, offers.Count) * 46);

            var rt = CardBody(card);
            if (offers.Count == 0)
            {
                var none = B.Text(rt, "None", "No offers on the table. Rival GMs call when they want someone.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            foreach (var offer in offers)
            {
                if (offer.Proposal == null) continue;
                string give = DescribeAssets(gm, offer.Proposal, sentBy: gm.PlayerTeamId);
                string get = DescribeAssets(gm, offer.Proposal, sentBy: offer.OfferingTeamId);

                var row = B.Child(rt, $"Offer_{offer.OfferId}");
                row.AddComponent<LayoutElement>().preferredHeight = 44;
                var rowHlg = row.AddComponent<HorizontalLayoutGroup>();
                rowHlg.childControlWidth = true; rowHlg.childControlHeight = true;
                rowHlg.childForceExpandWidth = false; rowHlg.spacing = 8;

                var label = B.Text(row.GetComponent<RectTransform>(), "L",
                    $"<b>{TeamAbbr(offer.OfferingTeamId)}</b>: {offer.OfferMessage}\n" +
                    $"You give: {give}   You get: {get}   ({offer.DaysUntilExpiry}d left)",
                    11, FontStyle.Normal, UITheme.TextSecondary);
                label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                string offerId = offer.OfferId;
                RowButton(row, "Accept", "ACCEPT", UITheme.Success, () =>
                {
                    var result = gm.TradeDesk.AcceptIncomingOffer(offerId, gm.CurrentDate);
                    _status = result.Status == TradeStatus.Completed
                        ? "Trade completed — welcome your new players."
                        : $"League office blocked it: {FirstIssue(result)}";
                    Refresh();
                });
                RowButton(row, "Decline", "DECLINE", UITheme.Danger, () =>
                {
                    gm.TradeDesk.DeclineIncomingOffer(offerId);
                    _status = "Offer declined.";
                    Refresh();
                });
            }
        }

        private void BuildActiveNegotiation(RectTransform scroll, GameManager gm)
        {
            if (string.IsNullOrEmpty(_activeNegotiationId)) return;
            var n = gm.TradeNegotiationManager?.GetNegotiation(_activeNegotiationId);
            if (n == null || !n.IsActive) { _activeNegotiationId = null; return; }

            var card = B.Card(scroll, $"COUNTER-OFFER FROM {TeamAbbr(OtherTeam(n, gm.PlayerTeamId))}", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 118;
            var rt = CardBody(card);

            string give = DescribeAssets(gm, n.CurrentProposal, sentBy: gm.PlayerTeamId);
            string get = DescribeAssets(gm, n.CurrentProposal, sentBy: OtherTeam(n, gm.PlayerTeamId));
            string message = n.Rounds?.LastOrDefault()?.Message ?? "";

            var text = B.Text(rt, "Counter",
                $"\"{message}\"\nTheir terms — you give: {give}\nYou get: {get}",
                12, FontStyle.Normal, UITheme.TextSecondary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;

            var buttons = B.Child(rt, "Buttons");
            buttons.AddComponent<LayoutElement>().preferredHeight = 26;
            var hlg = buttons.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.spacing = 8;

            RowButton(buttons, "AcceptCounter", "ACCEPT COUNTER", UITheme.Success, () =>
            {
                var neg = gm.TradeNegotiationManager.GetNegotiation(_activeNegotiationId);
                gm.TradeNegotiationManager.RespondToCounter(_activeNegotiationId, gm.PlayerTeamId,
                    TradeNegotiationAction.Accept);
                _status = neg?.Status == TradeNegotiationStatus.Accepted
                    ? "Deal! The counter-offer is done."
                    : $"Fell through: {neg?.Rounds?.LastOrDefault()?.Message}";
                _activeNegotiationId = null;
                ClearProposalState();
                Refresh();
            });
            RowButton(buttons, "Walk", "WALK AWAY", UITheme.Danger, () =>
            {
                gm.TradeNegotiationManager.WithdrawNegotiation(_activeNegotiationId, gm.PlayerTeamId);
                _activeNegotiationId = null;
                _status = "You walked away from the table.";
                Refresh();
            });
        }

        private void BuildProposalBuilder(RectTransform scroll, GameManager gm)
        {
            var card = B.Card(scroll, "PROPOSE A TRADE", _teamColor);
            var partners = (gm.AllTeams ?? new List<Team>())
                .Where(t => t != null && t.TeamId != gm.PlayerTeamId)
                .OrderBy(t => t.Abbreviation).ToList();

            var partner = string.IsNullOrEmpty(_tradePartnerId) ? null : gm.GetTeam(_tradePartnerId);
            int gridRows = Mathf.CeilToInt(partners.Count / 10f);
            int listRows = partner == null ? 0
                : TradablePlayers(gm, _team).Count + TradablePlayers(gm, partner).Count
                  + TradablePicks(gm, _team).Count + TradablePicks(gm, partner).Count + 4;
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                44 + gridRows * 26 + listRows * 24 + (partner == null ? 10 : 70);

            var rt = CardBody(card);

            // Partner grid
            for (int r = 0; r < gridRows; r++)
            {
                var gridRow = B.Child(rt, $"PartnerRow{r}");
                gridRow.AddComponent<LayoutElement>().preferredHeight = 24;
                var gh = gridRow.AddComponent<HorizontalLayoutGroup>();
                gh.childControlWidth = true; gh.childControlHeight = true;
                gh.childForceExpandWidth = true; gh.spacing = 3;

                foreach (var t in partners.Skip(r * 10).Take(10))
                {
                    var btn = B.Child(gridRow.GetComponent<RectTransform>(), $"P_{t.TeamId}");
                    bool selected = t.TeamId == _tradePartnerId;
                    btn.AddComponent<Image>().color = selected
                        ? UITheme.DarkenColor(_teamColor, 0.45f) : UITheme.FMCardHeaderBg;
                    string tid = t.TeamId;
                    btn.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        _tradePartnerId = tid;
                        ClearProposalState(keepPartner: true);
                        Refresh();
                    });
                    var bt = B.Text(btn.GetComponent<RectTransform>(), "T", t.Abbreviation, 10,
                        selected ? FontStyle.Bold : FontStyle.Normal,
                        selected ? Color.white : UITheme.TextSecondary);
                    B.Stretch(bt.gameObject); bt.alignment = TextAnchor.MiddleCenter;
                }
            }

            if (partner == null)
            {
                var hint = B.Text(rt, "Hint", "Pick a trade partner to open talks.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            BuildPickList(rt, gm, _team, _sendIds, "YOU SEND");
            BuildDraftPickRows(rt, gm, _team, _sendPickKeys);
            BuildPickList(rt, gm, partner, _getIds, $"{partner.Abbreviation} SEND");
            BuildDraftPickRows(rt, gm, partner, _getPickKeys);

            // Totals + legality + propose
            long salaryOut = _sendIds.Sum(id => gm.SalaryCapManager.GetContract(id)?.CurrentYearSalary ?? 0);
            long salaryIn = _getIds.Sum(id => gm.SalaryCapManager.GetContract(id)?.CurrentYearSalary ?? 0);
            var totals = B.Text(rt, "Totals",
                $"Salary out: <b>{Money(salaryOut)}</b>   Salary in: <b>{Money(salaryIn)}</b>",
                12, FontStyle.Normal, UITheme.TextSecondary);
            totals.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            if (_sendIds.Count + _sendPickKeys.Count > 0 && _getIds.Count + _getPickKeys.Count > 0)
            {
                var proposal = BuildDraftProposal(gm);
                var validation = gm.Trades.ValidateProposal(proposal);
                var legality = B.Text(rt, "Legality",
                    validation.IsValid ? "Deal is CBA-legal." : $"Not legal yet: {validation.Issues.FirstOrDefault()}",
                    12, FontStyle.Italic, validation.IsValid ? UITheme.Success : UITheme.Warning);
                legality.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

                if (validation.IsValid)
                {
                    var proposeRow = B.Child(rt, "ProposeRow");
                    proposeRow.AddComponent<LayoutElement>().preferredHeight = 28;
                    var ph = proposeRow.AddComponent<HorizontalLayoutGroup>();
                    ph.childControlWidth = true; ph.childControlHeight = true;
                    ph.childForceExpandWidth = false; ph.spacing = 8;

                    bool tradeViaGM = !NBAHeadCoach.Core.Data.RolePermissions.CanMakeRosterMoves;
                    RowButton(proposeRow, "Propose", tradeViaGM ? "SUGGEST TO GM" : "PROPOSE TRADE",
                        UITheme.AccentPrimary, () =>
                    {
                        if (tradeViaGM)
                        {
                            string targetId = _getIds.FirstOrDefault() ?? "";
                            string awayId = _sendIds.FirstOrDefault() ?? "";
                            // Picks-only deal: name the picks instead, so the GM's inbox
                            // message reads as a deal rather than "target for outgoing".
                            string targetName = gm.PlayerDatabase.GetPlayer(targetId)?.FullName
                                ?? PickName(gm, _getPickKeys) ?? "target";
                            string awayName = gm.PlayerDatabase.GetPlayer(awayId)?.FullName
                                ?? PickName(gm, _sendPickKeys) ?? "outgoing";
                            var req = NBAHeadCoach.Core.Data.RosterRequest.CreateTradeRequest(
                                targetId, targetName, awayId, awayName,
                                "This deal makes us better. I want it done.");
                            AskGM(req, () => SubmitProposal(gm));
                        }
                        else { SubmitProposal(gm); Refresh(); }
                    }, width: 140);
                }
            }
        }

        private void BuildPickList(RectTransform rt, GameManager gm, Team team, HashSet<string> selection, string header)
        {
            var head = B.Text(rt, $"Head_{team.TeamId}", header, 12, FontStyle.Bold, UITheme.AccentSecondary);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            foreach (var (player, contract) in TradablePlayers(gm, team))
            {
                bool selected = selection.Contains(player.PlayerId);
                var row = PlayerRow(rt, $"T_{player.PlayerId}",
                    (selected ? "<color=white><b>✓ </b></color>" : "") + PlayerLine(gm, player, contract));

                string pid = player.PlayerId;
                RowButton(row, "Toggle", selected ? "REMOVE" : "ADD",
                    selected ? UITheme.Danger : UITheme.AccentSecondary, () =>
                {
                    if (!selection.Remove(pid)) selection.Add(pid);
                    Refresh();
                });
            }
        }

        /// <summary>
        /// Draft picks a team can put in the deal: this draft's year plus the next two.
        /// On draft night current-year picks show the slot they're on ("#7 overall").
        /// </summary>
        private void BuildDraftPickRows(RectTransform rt, GameManager gm, Team team,
            HashSet<string> selection)
        {
            var off = OffseasonManager.Instance;
            foreach (var pick in TradablePicks(gm, team))
            {
                string key = PickKey(pick);
                bool selected = selection.Contains(key);

                int slot = off != null && off.DraftActive && pick.Year == off.OffseasonCalendarYear
                    ? off.PickSlotFor(pick.OriginalTeamId, pick.Round) : 0;
                string label = PickLabel(pick) + (slot > 0 ? $" — #{slot} overall" : "");

                var row = PlayerRow(rt, $"PK_{team.TeamId}_{key}",
                    (selected ? "<color=white><b>✓ </b></color>" : "") + label);

                RowButton(row, "TogglePick", selected ? "REMOVE" : "ADD",
                    selected ? UITheme.Danger : UITheme.AccentSecondary, () =>
                {
                    if (!selection.Remove(key)) selection.Add(key);
                    Refresh();
                });
            }
        }

        private static List<DraftPick> TradablePicks(GameManager gm, Team team)
        {
            var off = OffseasonManager.Instance;
            bool nightLive = off != null && off.DraftActive;
            int firstYear = nightLive ? off.OffseasonCalendarYear : gm.CurrentDate.Year;

            // A pre-O4 mid-draft save has no slot order, so the board can't re-derive
            // ownership tonight — a sold pick would still pick for the seller. Keep this
            // draft's picks off the table on those saves; future years trade fine.
            int minYear = nightLive && !off.SlotOrderTracked ? firstYear + 1 : firstYear;

            return (gm.DraftPickRegistry?.GetPicksOwnedBy(team.TeamId) ?? new List<DraftPick>())
                .Where(p => !p.IsUsed && p.Year >= minYear && p.Year <= firstYear + 2)
                .OrderBy(p => p.Year).ThenBy(p => p.Round).ToList();
        }

        /// <summary>"2027 1st (via BOS)" — the deal-desk name for a pick.</summary>
        private static string PickLabel(DraftPick pick)
        {
            string via = pick.CurrentOwnerId != pick.OriginalTeamId
                ? $" (via {TeamAbbr(pick.OriginalTeamId)})" : "";
            return $"{pick.Year} {(pick.Round == 1 ? "1st" : "2nd")}{via}";
        }

        /// <summary>Label of the first selected pick, for describing a picks-only deal.</summary>
        private static string PickName(GameManager gm, HashSet<string> keys)
        {
            var pick = keys.Select(k => PickFromKey(gm, k)).FirstOrDefault(p => p != null);
            return pick == null ? null : PickLabel(pick);
        }

        private static string PickKey(DraftPick pick) =>
            $"{pick.OriginalTeamId}_{pick.Year}_{pick.Round}";

        private static DraftPick PickFromKey(GameManager gm, string key)
        {
            var parts = key?.Split('_');
            if (parts == null || parts.Length < 3) return null;
            if (!int.TryParse(parts[parts.Length - 2], out int year) ||
                !int.TryParse(parts[parts.Length - 1], out int round)) return null;
            string original = string.Join("_", parts.Take(parts.Length - 2));
            return gm.DraftPickRegistry?.GetPick(original, year, round);
        }

        private List<(Player player, Contract contract)> TradablePlayers(GameManager gm, Team team)
        {
            var list = new List<(Player, Contract)>();
            foreach (var id in team.RosterPlayerIds)
            {
                var p = gm.PlayerDatabase.GetPlayer(id);
                var c = gm.SalaryCapManager.GetContract(id);
                if (p != null && c != null) list.Add((p, c));
            }
            return list.OrderByDescending(pc => pc.Item2.CurrentYearSalary).ToList();
        }

        private TradeProposal BuildDraftProposal(GameManager gm)
        {
            var proposal = new TradeProposal { ProposedDate = gm.CurrentDate };
            foreach (var id in _sendIds)
            {
                proposal.AllAssets.Add(new TradeAsset
                {
                    Type = TradeAssetType.Player, PlayerId = id,
                    Salary = gm.SalaryCapManager.GetContract(id)?.CurrentYearSalary ?? 0,
                    SendingTeamId = gm.PlayerTeamId, ReceivingTeamId = _tradePartnerId
                });
            }
            foreach (var id in _getIds)
            {
                proposal.AllAssets.Add(new TradeAsset
                {
                    Type = TradeAssetType.Player, PlayerId = id,
                    Salary = gm.SalaryCapManager.GetContract(id)?.CurrentYearSalary ?? 0,
                    SendingTeamId = _tradePartnerId, ReceivingTeamId = gm.PlayerTeamId
                });
            }
            AddPickAssets(proposal, gm, _sendPickKeys, gm.PlayerTeamId, _tradePartnerId);
            AddPickAssets(proposal, gm, _getPickKeys, _tradePartnerId, gm.PlayerTeamId);
            return proposal;
        }

        private static void AddPickAssets(TradeProposal proposal, GameManager gm,
            HashSet<string> keys, string from, string to)
        {
            foreach (var key in keys)
            {
                var asset = DraftPickRegistry.ToTradeAsset(PickFromKey(gm, key), from, to);
                if (asset != null) proposal.AllAssets.Add(asset);
            }
        }

        private void SubmitProposal(GameManager gm)
        {
            var proposal = BuildDraftProposal(gm);
            var negotiation = gm.TradeNegotiationManager.InitiateNegotiation(gm.PlayerTeamId, proposal);
            var lastRound = negotiation?.Rounds?.LastOrDefault();

            switch (negotiation?.Status)
            {
                case TradeNegotiationStatus.Accepted:
                    _status = "Deal! They accepted your offer — the trade is done.";
                    ClearProposalState();
                    break;
                case TradeNegotiationStatus.CounterReceived:
                    _activeNegotiationId = negotiation.NegotiationId;
                    _status = "They countered — terms are on the table above.";
                    break;
                case TradeNegotiationStatus.Rejected:
                    _status = $"Rejected: {lastRound?.Message}";
                    break;
                default:
                    _status = lastRound?.Message ?? "No response.";
                    break;
            }
        }

        private void ClearProposalState(bool keepPartner = false)
        {
            _sendIds.Clear();
            _getIds.Clear();
            _sendPickKeys.Clear();
            _getPickKeys.Clear();
            if (!keepPartner) _tradePartnerId = null;
        }

        private void BuildRecentTrades(RectTransform scroll, GameManager gm)
        {
            var news = gm.TradeAnnouncementSystem?.GetRecentAnnouncements(8) ?? new List<TradeAnnouncement>();
            var card = B.Card(scroll, "AROUND THE LEAGUE", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + Mathf.Max(1, news.Count) * 18);

            var rt = CardBody(card);
            if (news.Count == 0)
            {
                var none = B.Text(rt, "None", "No trades around the league yet.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            var lines = news.Select(a => a.InvolvesPlayerTeam
                ? $"<color=white><b>{a.Headline}</b></color>"
                : a.Headline);
            var text = B.Text(rt, "News", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = news.Count * 18;
            text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.2f;
        }

        // ==================== DRAFT ====================

        private void BuildDraft(RectTransform scroll)
        {
            var gm = GameManager.Instance;
            var off = OffseasonManager.Instance;
            var draft = off?.DraftBoard;

            if (gm != null && off != null && off.WorkoutsOpen(gm.CurrentDate))
                BuildWorkoutsCard(scroll, gm, off);

            if (off == null || !off.DraftActive || draft == null)
            {
                Empty(scroll, "The draft is held on June 22.\nWhen your pick is on the clock, the board goes live here.");
                return;
            }

            if (off.PlayerOnClock)
            {
                var clock = B.Text(scroll, "Clock",
                    $"YOU ARE ON THE CLOCK — PICK #{off.NextPickNumber}",
                    16, FontStyle.Bold, UITheme.Warning);
                clock.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            }
            else
            {
                var waiting = B.Text(scroll, "Waiting",
                    $"Draft in progress — pick #{off.NextPickNumber} up next.",
                    13, FontStyle.Italic, UITheme.TextSecondary);
                waiting.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            }

            // Board: last 10 selections
            var results = draft.GetDraftResults();
            if (results.Count > 0)
            {
                var board = B.Card(scroll, "DRAFT BOARD", _teamColor);
                var shown = results.OrderByDescending(r => r.PickNumber).Take(10).ToList();
                board.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 + shown.Count * 17;
                var lines = shown.Select(r =>
                {
                    string row = $"#{r.PickNumber}  {TeamAbbr(r.TeamId)}  —  {r.Prospect.FirstName} {r.Prospect.LastName} ({r.Prospect.Position})";
                    return r.TeamId == _team.TeamId ? $"<color=white><b>{row}</b></color>" : row;
                });
                var text = B.Text(board, "Picks", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
                B.FillCard(text); text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.25f;
            }

            // Available prospects
            var available = draft.GetProspects()
                .OrderBy(p => p.Intel.ConsensusRank)
                .Take(25)
                .ToList();

            var pool = B.Card(scroll, "BEST AVAILABLE", _teamColor);
            pool.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + available.Count * 26);

            var rt = CardBody(pool);

            foreach (var prospect in available)
            {
                var row = PlayerRow(rt, $"P_{prospect.ProspectId}", ProspectLine(gm, prospect));

                if (off.PlayerOnClock)
                {
                    string prospectId = prospect.ProspectId;
                    RowButton(row, "Draft", "DRAFT", UITheme.AccentPrimary, () =>
                    {
                        OffseasonManager.Instance?.SubmitPlayerPick(GameManager.Instance, prospectId);
                        Refresh();
                    });
                }
            }
        }

        /// <summary>
        /// Pre-draft workouts (Jun 10 → draft day): six invites, each one worth two
        /// scouting trips and a chance to shake loose whatever a prospect is hiding.
        /// </summary>
        private void BuildWorkoutsCard(RectTransform scroll, GameManager gm, OffseasonManager off)
        {
            var pool = gm.Scouting?.GetProspectPreview(off.SeasonLabel);
            if (pool == null || pool.Count == 0) return;

            var shown = pool.OrderBy(p => p.Intel.ConsensusRank).Take(15).ToList();
            var card = B.Card(scroll, $"PRE-DRAFT WORKOUTS — {off.WorkoutInvitesRemaining} INVITE(S) LEFT",
                UITheme.AccentPrimary);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 + shown.Count * 26;
            var rt = CardBody(card);
            bool viaGM = !NBAHeadCoach.Core.Data.RolePermissions.CanMakeRosterMoves;

            foreach (var prospect in shown)
            {
                var row = PlayerRow(rt, $"W_{prospect.ProspectId}", ProspectLine(gm, prospect));
                if (off.WorkoutInvites.Contains(prospect.ProspectId)) continue;
                if (off.WorkoutInvitesRemaining <= 0) continue;

                string pid = prospect.ProspectId, name = prospect.FullName;
                RowButton(row, "Invite", viaGM ? "ASK GM" : "INVITE", UITheme.Success, () =>
                {
                    Action act = () =>
                    {
                        bool ok = OffseasonManager.Instance.InviteToWorkout(GameManager.Instance, pid,
                            out string why);
                        _status = ok ? $"{name} worked out for us — report filed."
                                     : $"Couldn't do that: {why}";
                    };
                    if (viaGM) AskGM(NBAHeadCoach.Core.Data.RosterRequest.CreateSigningRequest(
                        pid, name, "Bring him in for a pre-draft workout."), act);
                    else { act(); Refresh(); }
                }, width: 84);
            }
        }

        /// <summary>
        /// One prospect row: public college production and mock range always, the
        /// department's projection range once he's been scouted, the red flag only
        /// once something surfaced it, and the workout note if we brought him in.
        /// </summary>
        private string ProspectLine(GameManager gm, DraftProspect prospect)
        {
            var intel = prospect.Intel;
            var parts = new List<string>
            {
                $"{prospect.FirstName} {prospect.LastName}",
                PositionShort(prospect.Position),
                $"{prospect.Age}y",
                prospect.College ?? prospect.Country ?? "—",
                intel.StatLine,
                intel.MockRange
            };

            var sc = gm?.Scouting;
            if (sc == null) return string.Join("  ·  ", parts);

            parts.Add(sc.IsScouted(prospect.ProspectId)
                ? $"<color=white>{sc.ProjectionRange(prospect)}</color>"
                : "<color=#EAB308>unscouted — drafting blind</color>");

            if (intel.RedFlag != ProspectRedFlag.None && sc.IsRedFlagRevealed(prospect.ProspectId))
                parts.Add($"<color=#EF4444>FLAG: {intel.RedFlagText}</color>");

            string workout = sc.GetWorkoutSummary(prospect.ProspectId);
            if (!string.IsNullOrEmpty(workout)) parts.Add($"<color=white>workout — {workout}</color>");

            return string.Join("  ·  ", parts);
        }

        // ==================== HELPERS ====================

        private RectTransform CardBody(RectTransform card)
        {
            var body = B.Child(card, "Body");
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 8); rt.offsetMax = new Vector2(-12, -36);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.spacing = 2;
            return rt;
        }

        private GameObject PlayerRow(RectTransform parent, string name, string label)
        {
            var row = B.Child(parent, name);
            row.AddComponent<LayoutElement>().preferredHeight = 24;
            var rowHlg = row.AddComponent<HorizontalLayoutGroup>();
            rowHlg.childControlWidth = true; rowHlg.childControlHeight = true;
            rowHlg.childForceExpandWidth = false; rowHlg.spacing = 8;

            var text = B.Text(row.GetComponent<RectTransform>(), "L", label, 12, FontStyle.Normal, UITheme.TextSecondary);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            return row;
        }

        private void RowButton(GameObject row, string name, string label, Color color, Action onClick, int width = 90)
        {
            var btn = B.Child(row.GetComponent<RectTransform>(), name);
            btn.AddComponent<LayoutElement>().preferredWidth = width;
            btn.AddComponent<Image>().color = UITheme.DarkenColor(color, 0.55f);
            btn.AddComponent<Button>().onClick.AddListener(() => onClick());
            var bt = B.Text(btn.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Bold, Color.white);
            B.Stretch(bt.gameObject); bt.alignment = TextAnchor.MiddleCenter;
        }

        private string PlayerLine(GameManager gm, Player player, Contract contract)
        {
            contract = contract ?? gm.SalaryCapManager?.GetContract(player.PlayerId);
            var last = player.CurrentSeasonStats?.GamesPlayed > 0
                ? player.CurrentSeasonStats
                : player.CareerStats?.LastOrDefault(s => s.GamesPlayed > 0);
            string statLine = last != null ? $"{last.PPG:0.0}p {last.RPG:0.0}r {last.APG:0.0}a" : "—";
            string money = contract != null ? $"{Money(contract.CurrentYearSalary)}/yr" : "no deal";
            return $"{player.FullName}  ·  {PositionShort(player.Position)}  ·  {player.Age}y  ·  {statLine}  ·  {money}";
        }

        private string DescribeAssets(GameManager gm, TradeProposal proposal, string sentBy)
        {
            if (proposal?.AllAssets == null) return "—";
            var parts = proposal.AllAssets
                .Where(a => a.SendingTeamId == sentBy)
                .Select(a => a.Type == TradeAssetType.Player
                    ? gm.PlayerDatabase?.GetPlayer(a.PlayerId)?.FullName ?? a.PlayerId
                    : a.GetDescription())
                .ToList();
            return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        }

        private static string OtherTeam(TradeNegotiation n, string playerTeamId) =>
            n?.InvolvedTeamIds?.FirstOrDefault(t => t != playerTeamId) ?? "";

        private static string FirstIssue(TradeResult result) =>
            result?.ValidationResult?.Issues?.FirstOrDefault() ?? "failed validation.";

        private void Empty(RectTransform scroll, string message)
        {
            var text = B.Text(scroll, "Empty", message, 13, FontStyle.Italic, UITheme.TextSecondary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
        }

        private static string Money(long amount) => $"${amount / 1_000_000f:0.0}M";

        private static string TeamAbbr(string teamId) =>
            GameManager.Instance?.GetTeam(teamId)?.Abbreviation ?? teamId;

        private static string PositionShort(Position pos) => pos switch
        {
            Position.PointGuard => "PG",
            Position.ShootingGuard => "SG",
            Position.SmallForward => "SF",
            Position.PowerForward => "PF",
            Position.Center => "C",
            _ => "?"
        };
    }
}
