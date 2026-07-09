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
    /// All-Star Weekend: the rosters (starters, reserves, snubs), the Saturday
    /// contests, and the game — box score and MVP once it's played. If the
    /// player's team leads its conference at the break, the player coaches the
    /// game and runs it from here.
    /// </summary>
    public class AllStarPanel : IGamePanel
    {
        private RectTransform _parent;
        private Team _team;
        private Color _teamColor;

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            _parent = parent; _team = team; _teamColor = teamColor;

            var scroll = B.ScrollArea(parent);
            var mgr = AllStarManager.Instance;
            var weekend = mgr?.CurrentWeekend;

            if (weekend == null)
            {
                var card = B.Card(scroll, "ALL-STAR WEEKEND", teamColor);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
                var t = B.Text(CardBody(card), "T",
                    "Rosters are announced when the season reaches the February break.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
                return;
            }

            BuildCoachBanner(scroll, mgr, weekend);
            BuildGameCard(scroll, weekend);
            BuildContestsCard(scroll, weekend);
            BuildRosterCard(scroll, weekend, "East");
            BuildRosterCard(scroll, weekend, "West");
            BuildSnubsCard(scroll, weekend);
        }

        private void Rebuild()
        {
            foreach (Transform child in _parent) Object.Destroy(child.gameObject);
            Build(_parent, _team, _teamColor);
        }

        private void BuildCoachBanner(RectTransform scroll, AllStarManager mgr, AllStarWeekendSummary weekend)
        {
            var gm = GameManager.Instance;
            if (weekend.AllStarGameResult != null || gm == null) return;
            if (!AllStarManager.PlayerCoachesASG(gm)) return;

            string conf = gm.GetPlayerTeam()?.Conference ?? "conference";
            var card = B.Card(scroll, $"YOU'RE COACHING THE {conf.ToUpper()} ALL-STARS", UITheme.AccentPrimary);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;
            var body = CardBody(card);

            var t = B.Text(body, "T",
                "Best record in the conference at the break — the sideline is yours. " +
                "Your game plan travels with the squad.",
                12, FontStyle.Normal, UITheme.TextSecondary);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

            var btnRow = B.Child(body, "BtnRow");
            btnRow.AddComponent<LayoutElement>().preferredHeight = 30;
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.spacing = 8;

            var btn = B.Child(btnRow.GetComponent<RectTransform>(), "CoachBtn");
            btn.AddComponent<LayoutElement>().preferredWidth = 180;
            btn.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.Success, 0.5f);
            btn.AddComponent<Button>().onClick.AddListener(() =>
            {
                mgr.RunAllStarWeekend(gm.PlayerDatabase);
                Rebuild();
            });
            var bt = B.Text(btn.GetComponent<RectTransform>(), "T", "COACH THE GAME", 13, FontStyle.Bold, Color.white);
            B.Stretch(bt.gameObject); bt.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildGameCard(RectTransform scroll, AllStarWeekendSummary weekend)
        {
            var g = weekend.AllStarGameResult;
            var card = B.Card(scroll, $"THE GAME — {weekend.HostCity}", _teamColor);
            var body = CardBody(card);

            if (g == null)
            {
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
                var t = B.Text(body, "T", "Tips off on All-Star Sunday.", 12, FontStyle.Italic, UITheme.TextSecondary);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
                return;
            }

            var top = g.PlayerStats.Values.OrderByDescending(s => s.Points).Take(10).ToList();
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 120 + top.Count * 22;

            var score = B.Text(body, "Score",
                $"EAST {g.EastScore} — WEST {g.WestScore}    ({g.WinningConference} wins)",
                16, FontStyle.Bold, Color.white);
            score.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;

            var mvp = B.Text(body, "MVP",
                $"MVP: {g.MvpName} — {g.MvpPoints} pts, {g.MvpRebounds} reb, {g.MvpAssists} ast",
                13, FontStyle.Bold, UITheme.AccentPrimary);
            mvp.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            var hdr = B.TableRow(body, 20, UITheme.PanelSurface);
            B.TableCell(hdr, "Player", 170, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "MIN", 40, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "PTS", 40, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "REB", 40, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "AST", 40, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "3PM", 40, FontStyle.Bold, UITheme.AccentPrimary);

            var names = weekend.GetAllSelected().ToDictionary(v => v.PlayerId, v => v.PlayerName);
            int i = 0;
            foreach (var line in top)
            {
                var row = B.TableRow(body, 22, i++ % 2 == 0 ? UITheme.CardBackground : UITheme.PanelSurface);
                B.TableCell(row, names.TryGetValue(line.PlayerId, out var n) ? n : line.PlayerId, 170);
                B.TableCell(row, line.Minutes.ToString(), 40);
                B.TableCell(row, line.Points.ToString(), 40, FontStyle.Bold, Color.white);
                B.TableCell(row, line.Rebounds.ToString(), 40);
                B.TableCell(row, line.Assists.ToString(), 40);
                B.TableCell(row, line.ThreePointersMade.ToString(), 40);
            }
        }

        private void BuildContestsCard(RectTransform scroll, AllStarWeekendSummary weekend)
        {
            var card = B.Card(scroll, "SATURDAY NIGHT", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
            var body = CardBody(card);

            string three = weekend.ThreePointContestResult?.WinnerName;
            string dunk = weekend.DunkContestResult?.WinnerName;
            var t = B.Text(body, "T",
                three == null && dunk == null
                    ? "The contests run on All-Star Saturday."
                    : $"Three-Point Contest: {three ?? "—"}\nDunk Contest: {dunk ?? "—"}",
                13, FontStyle.Normal, UITheme.TextPrimary);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
        }

        private void BuildRosterCard(RectTransform scroll, AllStarWeekendSummary weekend, string conference)
        {
            bool east = conference == "East";
            var starters = east ? weekend.EastStarters : weekend.WestStarters;
            var reserves = east ? weekend.EastReserves : weekend.WestReserves;

            var card = B.Card(scroll, $"{conference.ToUpper()} ALL-STARS", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                60 + (starters.Count + reserves.Count) * 20;
            var body = CardBody(card);

            foreach (var v in starters)
            {
                var row = B.TableRow(body, 20, UITheme.CardBackground);
                B.TableCell(row, $"★ {v.PlayerName}", 200, FontStyle.Bold, Color.white);
                B.TableCell(row, v.TeamId, 60, FontStyle.Normal, UITheme.TextSecondary);
            }
            foreach (var v in reserves)
            {
                var row = B.TableRow(body, 20, UITheme.PanelSurface);
                B.TableCell(row, $"   {v.PlayerName}", 200);
                B.TableCell(row, v.TeamId, 60, FontStyle.Normal, UITheme.TextSecondary);
            }
        }

        private void BuildSnubsCard(RectTransform scroll, AllStarWeekendSummary weekend)
        {
            if (weekend.NotableSnubs == null || weekend.NotableSnubs.Count == 0) return;
            var card = B.Card(scroll, "THE SNUBS", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 60 + weekend.NotableSnubs.Count * 18;
            var body = CardBody(card);
            foreach (var snub in weekend.NotableSnubs)
            {
                var t = B.Text(body, "S", snub, 12, FontStyle.Italic, UITheme.TextSecondary);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            }
        }

        private static RectTransform CardBody(RectTransform card)
        {
            var body = B.Child(card, "Body");
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 8);
            rt.offsetMax = new Vector2(-12, -(UITheme.FMCardHeaderHeight + 4));
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2; vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            return rt;
        }
    }
}
