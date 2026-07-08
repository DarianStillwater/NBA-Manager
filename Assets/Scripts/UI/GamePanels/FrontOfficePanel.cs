using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.UI.Shell;
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
        private string _activeNegotiationId;
        private string _status;

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

            BuildFaSection(scroll, "OPEN MARKET", market
                .OrderByDescending(fa => gm.PlayerDatabase.GetPlayer(fa.PlayerId)?.OverallRating ?? 0)
                .Take(40).ToList(), gm, off, off.FreeAgencySigningOpen);
        }

        /// <summary>In-season buyout wire: leftover pool, minimum deals only.</summary>
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
                RowButton(row, "Sign", "SIGN", UITheme.Success, () =>
                {
                    string why = "desk unavailable.";
                    bool ok = gm.InSeasonSigning != null &&
                              gm.InSeasonSigning.SignFreeAgentToPlayerTeam(gm, pid, out why);
                    _status = ok ? $"{player.FullName} signed for the rest of the season."
                                 : $"Signing blocked: {why}";
                    Refresh();
                });
            }
        }

        private void BuildFaSection(RectTransform scroll, string headerText, List<FreeAgent> list,
            GameManager gm, OffseasonManager off, bool canSign)
        {
            var card = B.Card(scroll, headerText, _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + list.Count * 26);

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
                var row = PlayerRow(rt, $"FA_{fa.PlayerId}",
                    $"{PlayerLine(gm, player, null)}  ·  asks {Money(ask)}/yr");

                if (canSign)
                {
                    string pid = fa.PlayerId;
                    RowButton(row, "Sign", fa.PreviousTeamId == _team.TeamId ? "RE-SIGN" : "SIGN",
                        UITheme.Success, () =>
                    {
                        bool ok = off.SignFreeAgentToPlayerTeam(GameManager.Instance, pid, 2, out string why);
                        if (!ok) Debug.LogWarning($"[FrontOffice] Signing failed: {why}");
                        Refresh();
                    });
                }
            }
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
                : TradablePlayers(gm, _team).Count + TradablePlayers(gm, partner).Count + 4;
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
            BuildPickList(rt, gm, partner, _getIds, $"{partner.Abbreviation} SEND");

            // Totals + legality + propose
            long salaryOut = _sendIds.Sum(id => gm.SalaryCapManager.GetContract(id)?.CurrentYearSalary ?? 0);
            long salaryIn = _getIds.Sum(id => gm.SalaryCapManager.GetContract(id)?.CurrentYearSalary ?? 0);
            var totals = B.Text(rt, "Totals",
                $"Salary out: <b>{Money(salaryOut)}</b>   Salary in: <b>{Money(salaryIn)}</b>",
                12, FontStyle.Normal, UITheme.TextSecondary);
            totals.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            if (_sendIds.Count > 0 && _getIds.Count > 0)
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

                    RowButton(proposeRow, "Propose", "PROPOSE TRADE", UITheme.AccentPrimary, () =>
                    {
                        SubmitProposal(gm);
                        Refresh();
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
            return proposal;
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
                .OrderBy(p => p.MockDraftPosition)
                .Take(25)
                .ToList();

            var pool = B.Card(scroll, "BEST AVAILABLE", _teamColor);
            pool.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + available.Count * 26);

            var rt = CardBody(pool);

            foreach (var prospect in available)
            {
                string intel = gm?.Scouting != null
                    ? (gm.Scouting.IsScouted(prospect.ProspectId)
                        ? $"<color=white>{gm.Scouting.DescribeProspect(prospect.ProspectId)}</color>"
                        : "<color=#EAB308>unscouted — drafting blind</color>")
                    : prospect.Tier.ToString();
                var row = PlayerRow(rt, $"P_{prospect.ProspectId}",
                    $"Mock #{prospect.MockDraftPosition}  {prospect.FirstName} {prospect.LastName}  ·  {prospect.Position}  ·  {prospect.Age}y  ·  {prospect.College}  ·  {intel}");

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
