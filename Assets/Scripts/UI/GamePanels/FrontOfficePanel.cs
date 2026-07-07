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
    /// The GM's desk: free agency (re-sign your own from June, sign the market from
    /// July 6) and the live draft board on draft night. Trades join in Phase 3.
    /// </summary>
    public class FrontOfficePanel : IGamePanel
    {
        private string _tab = "FREE AGENCY";
        private Team _team;
        private Color _teamColor;

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
            foreach (var name in new[] { "FREE AGENCY", "DRAFT" })
                BuildTabButton(tabs.GetComponent<RectTransform>(), name);

            var bodyGo = B.Child(rootRT, "Body");
            var bodyLE = bodyGo.AddComponent<LayoutElement>(); bodyLE.flexibleHeight = 1;
            var scroll = B.FixedArea(bodyGo.GetComponent<RectTransform>());

            if (_tab == "DRAFT") BuildDraft(scroll);
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

            if (off == null || !off.EngineActive || fam == null)
            {
                Empty(scroll, "Free agency runs in the offseason.\nYour own free agents can re-sign from late June; the market opens July 6.");
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

        private void BuildFaSection(RectTransform scroll, string headerText, List<FreeAgent> list,
            GameManager gm, OffseasonManager off, bool canSign)
        {
            var card = B.Card(scroll, headerText, _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + list.Count * 26);

            var body = B.Child(card, "Body");
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 8); rt.offsetMax = new Vector2(-12, -36);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.spacing = 2;

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

                var row = B.Child(rt, $"FA_{fa.PlayerId}");
                row.AddComponent<LayoutElement>().preferredHeight = 24;
                var rowHlg = row.AddComponent<HorizontalLayoutGroup>();
                rowHlg.childControlWidth = true; rowHlg.childControlHeight = true;
                rowHlg.childForceExpandWidth = false; rowHlg.spacing = 8;

                long ask = off.EstimateMarketSalary(player);
                var last = player.CareerStats?.LastOrDefault(s => s.GamesPlayed > 0);
                string statLine = last != null ? $"{last.PPG:0.0}p {last.RPG:0.0}r {last.APG:0.0}a" : "—";

                var label = B.Text(row.GetComponent<RectTransform>(), "L",
                    $"{player.FullName}  ·  {PositionShort(player.Position)}  ·  {player.Age}y  ·  {statLine}  ·  asks {Money(ask)}/yr",
                    12, FontStyle.Normal, UITheme.TextSecondary);
                label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                if (canSign)
                {
                    string pid = fa.PlayerId;
                    var btn = B.Child(row.GetComponent<RectTransform>(), "Sign");
                    btn.AddComponent<LayoutElement>().preferredWidth = 78;
                    btn.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.Success, 0.55f);
                    btn.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        bool ok = off.SignFreeAgentToPlayerTeam(GameManager.Instance, pid, 2, out string why);
                        if (!ok) Debug.LogWarning($"[FrontOffice] Signing failed: {why}");
                        Refresh();
                    });
                    var bt = B.Text(btn.GetComponent<RectTransform>(), "T",
                        fa.PreviousTeamId == _team.TeamId ? "RE-SIGN" : "SIGN",
                        11, FontStyle.Bold, Color.white);
                    B.Stretch(bt.gameObject); bt.alignment = TextAnchor.MiddleCenter;
                }
            }
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

            var body = B.Child(pool, "Body");
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 8); rt.offsetMax = new Vector2(-12, -36);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.spacing = 2;

            foreach (var prospect in available)
            {
                var row = B.Child(rt, $"P_{prospect.ProspectId}");
                row.AddComponent<LayoutElement>().preferredHeight = 24;
                var rowHlg = row.AddComponent<HorizontalLayoutGroup>();
                rowHlg.childControlWidth = true; rowHlg.childControlHeight = true;
                rowHlg.childForceExpandWidth = false; rowHlg.spacing = 8;

                var label = B.Text(row.GetComponent<RectTransform>(), "L",
                    $"Mock #{prospect.MockDraftPosition}  {prospect.FirstName} {prospect.LastName}  ·  {prospect.Position}  ·  {prospect.Age}y  ·  {prospect.College}  ·  {prospect.Tier}",
                    12, FontStyle.Normal, UITheme.TextSecondary);
                label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                if (off.PlayerOnClock)
                {
                    string prospectId = prospect.ProspectId;
                    var btn = B.Child(row.GetComponent<RectTransform>(), "Draft");
                    btn.AddComponent<LayoutElement>().preferredWidth = 70;
                    btn.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.AccentPrimary, 0.55f);
                    btn.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        OffseasonManager.Instance?.SubmitPlayerPick(GameManager.Instance, prospectId);
                        Refresh();
                    });
                    var bt = B.Text(btn.GetComponent<RectTransform>(), "T", "DRAFT", 11, FontStyle.Bold, Color.white);
                    B.Stretch(bt.gameObject); bt.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        // ==================== HELPERS ====================

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
