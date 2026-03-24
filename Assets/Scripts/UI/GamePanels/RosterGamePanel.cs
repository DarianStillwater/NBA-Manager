using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;
using B = NBAHeadCoach.UI.Shell.UIBuilder;

namespace NBAHeadCoach.UI.GamePanels
{
    public class RosterGamePanel : IGamePanel
    {
        private readonly GameShell _shell;
        private Player _detailPlayer;
        private string _playerTab = "Overview";

        public RosterGamePanel(GameShell shell) { _shell = shell; }

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            var scroll = B.ScrollArea(parent);
            var title = B.Text(scroll, "Title", "SQUAD", 18, FontStyle.Bold, UITheme.AccentPrimary);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            var headerRow = B.TableRow(scroll, 28, UITheme.FMCardHeaderBg);
            B.TableCell(headerRow, "Name", 180, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "Ht", 55, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "Wt", 55, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "Age", 35, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "#", 30, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "PPG", 45, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "RPG", 45, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "APG", 45, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "Status", 70, FontStyle.Bold, UITheme.AccentPrimary);

            var roster = team.Roster?.OrderBy(p => p.Position).ThenByDescending(p => p.OverallRating).ToList();
            if (roster == null) return;

            for (int i = 0; i < roster.Count; i++)
            {
                var p = roster[i];
                var bgColor = i % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg;
                var row = B.TableRow(scroll, 30, bgColor);

                B.TableCell(row, $"{p.FirstName[0]}. {p.LastName}", 180, FontStyle.Normal, Color.white);
                B.TableCell(row, p.HeightFormatted, 55, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, $"{p.WeightLbs:0}", 55, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, p.Age.ToString(), 35, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, p.JerseyNumber?.ToString() ?? "—", 30, FontStyle.Normal, UITheme.TextSecondary);

                var stats = p.CurrentSeasonStats;
                B.TableCell(row, stats?.PPG.ToString("0.0") ?? "—", 45, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, stats?.RPG.ToString("0.0") ?? "—", 45, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, stats?.APG.ToString("0.0") ?? "—", 45, FontStyle.Normal, UITheme.TextSecondary);

                string status = p.IsInjured ? "INJURED" : p.Energy < 50 ? "TIRED" : "OK";
                Color statusColor = p.IsInjured ? UITheme.Danger : p.Energy < 50 ? UITheme.Warning : UITheme.Success;
                B.TableCell(row, status, 70, FontStyle.Bold, statusColor);

                var player = p;
                row.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowPlayerDetail(player));
            }
        }

        private void ShowPlayerDetail(Player player)
        {
            _detailPlayer = player;
            _playerTab = "Overview";
            RebuildDetail();
        }

        private void SwitchTab(string tab) { _playerTab = tab; RebuildDetail(); }

        private void RebuildDetail()
        {
            _shell?.RegisterPanel("PlayerDetail", new LegacyPanelAdapter((p, t, c) => BuildDetailContent(p, _detailPlayer)));
            _shell?.ShowPanel("PlayerDetail");
        }

        private void BuildDetailContent(RectTransform parent, Player p)
        {
            var topBar = B.Child(parent, "TopBar");
            topBar.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var tr = topBar.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 1); tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0.5f, 1); tr.sizeDelta = new Vector2(0, 40);
            var tlg = topBar.AddComponent<HorizontalLayoutGroup>();
            tlg.spacing = 12; tlg.padding = new RectOffset(12, 12, 4, 4);
            tlg.childControlWidth = false; tlg.childControlHeight = true; tlg.childAlignment = TextAnchor.MiddleLeft;

            var backGo = B.Child(tr, "Back");
            backGo.AddComponent<LayoutElement>().preferredWidth = 80;
            backGo.AddComponent<Image>().color = UITheme.PanelSurface;
            backGo.AddComponent<Button>().onClick.AddListener(() => _shell?.ShowPanel("Roster"));
            var bt = B.Text(backGo.GetComponent<RectTransform>(), "BT", "\u25C0 SQUAD", 11, FontStyle.Bold, UITheme.AccentPrimary);
            bt.alignment = TextAnchor.MiddleCenter; B.Stretch(bt.gameObject);

            string posStr = p.PositionString != "??" ? $"  |  {p.PositionString}" : "";
            var nt = B.Text(tr, "Name", $"#{p.JerseyNumber}  {p.FirstName} {p.LastName}{posStr}", 16, FontStyle.Bold, Color.white);
            nt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var accent = B.Child(parent, "Accent");
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0, 1); ar.anchorMax = Vector2.one;
            ar.pivot = new Vector2(0.5f, 1); ar.anchoredPosition = new Vector2(0, -40); ar.sizeDelta = new Vector2(0, 3);
            accent.AddComponent<Image>().color = UITheme.AccentPrimary;

            var tabBar = B.Child(parent, "TabBar");
            tabBar.AddComponent<Image>().color = UITheme.PanelSurface;
            var tbr = tabBar.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = Vector2.one;
            tbr.pivot = new Vector2(0.5f, 1); tbr.anchoredPosition = new Vector2(0, -43); tbr.sizeDelta = new Vector2(0, 32);
            var th = tabBar.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 0; th.padding = new RectOffset(12, 12, 0, 0);
            th.childControlWidth = true; th.childControlHeight = true; th.childForceExpandWidth = false;

            string[] tabs = { "Overview", "Stats", "Scouting", "Contract" };
            foreach (var tab in tabs)
            {
                var t = tab;
                var tGo = B.Child(tbr, $"Tab_{tab}");
                tGo.AddComponent<LayoutElement>().preferredWidth = 100;
                tGo.AddComponent<Image>().color = tab == _playerTab ? UITheme.FMNavActive : Color.clear;
                var tAcc = B.Child(tGo.GetComponent<RectTransform>(), "Acc");
                var tacr = tAcc.GetComponent<RectTransform>();
                tacr.anchorMin = Vector2.zero; tacr.anchorMax = new Vector2(1, 0); tacr.sizeDelta = new Vector2(0, 2);
                tAcc.AddComponent<Image>().color = tab == _playerTab ? UITheme.AccentPrimary : Color.clear;
                var tt = B.Text(tGo.GetComponent<RectTransform>(), "Text", tab, 11, FontStyle.Bold,
                    tab == _playerTab ? UITheme.AccentPrimary : UITheme.TextSecondary);
                tt.alignment = TextAnchor.MiddleCenter; B.Stretch(tt.gameObject);
                tGo.AddComponent<Button>().onClick.AddListener(() => SwitchTab(t));
            }

            var cGo = B.Child(parent, "TabContent");
            var cr = cGo.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.offsetMin = Vector2.zero; cr.offsetMax = new Vector2(0, -75);

            switch (_playerTab)
            {
                case "Overview": BuildOverview(cr, p); break;
                case "Stats": BuildStats(cr, p); break;
                case "Scouting": BuildScouting(cr, p); break;
                case "Contract": BuildContract(cr, p); break;
            }
        }

        private void BuildOverview(RectTransform parent, Player p)
        {
            var scroll = B.ScrollArea(parent);
            var bioCard = B.Card(scroll, "Biography", UITheme.AccentPrimary);
            var bio = B.Text(bioCard, "Bio", "", 12, FontStyle.Normal, Color.white);
            B.FillCard(bio); bio.horizontalOverflow = HorizontalWrapMode.Wrap;
            string draftInfo = p.DraftRound > 0 ? $"{p.DraftYear} Round {p.DraftRound}, Pick #{p.DraftPick}" : "Undrafted";
            bio.text = $"Height: {p.HeightFormatted}    Weight: {p.WeightLbs:0} lbs\n" +
                $"Born: {p.BirthDateFormatted} (Age {p.Age})\nNationality: {(string.IsNullOrEmpty(p.Nationality) ? "USA" : p.Nationality)}\n" +
                $"College: {(string.IsNullOrEmpty(p.College) ? "—" : p.College)}\nDraft: {draftInfo}\n" +
                $"Experience: {(p.YearsPro == 0 ? "Rookie" : $"{p.YearsPro} years")}";

            var statsCard = B.Card(scroll, "Current Season", UITheme.AccentPrimary);
            var st = B.Text(statsCard, "Stats", "", 12, FontStyle.Normal, Color.white);
            B.FillCard(st); st.horizontalOverflow = HorizontalWrapMode.Wrap;
            var s = p.CurrentSeasonStats;
            st.text = s != null && s.GamesPlayed > 0
                ? $"Games: {s.GamesPlayed}  |  Started: {s.GamesStarted}\n\nPPG: {s.PPG:0.0}    RPG: {s.RPG:0.0}    APG: {s.APG:0.0}\n" +
                  $"SPG: {s.SPG:0.0}    BPG: {s.BPG:0.0}    MPG: {s.MPG:0.0}\n\nFG%: {s.FG_Pct * 100:0.0}    3P%: {s.ThreeP_Pct * 100:0.0}    FT%: {s.FT_Pct * 100:0.0}"
                : "No games played this season.";

            var statusCard = B.Card(scroll, "Status", UITheme.AccentPrimary);
            var sc = B.Text(statusCard, "Status", "", 12, FontStyle.Normal, Color.white);
            B.FillCard(sc); sc.horizontalOverflow = HorizontalWrapMode.Wrap;
            string inj = p.IsInjured ? $"INJURED — {p.InjuryDaysRemaining} days remaining" : "Healthy";
            string cap = p.IsCaptain ? "  \u2605 CAPTAIN" : "";
            sc.text = $"Energy: {p.Energy:0}%    Morale: {p.Morale:0}%    Form: {p.Form:0}%\nHealth: {inj}{cap}";
        }

        private void BuildStats(RectTransform parent, Player p)
        {
            var scroll = B.ScrollArea(parent);
            var s = p.CurrentSeasonStats;
            if (s == null || s.GamesPlayed == 0)
            { B.Text(scroll, "No", "No stats available — season hasn't started.", 14, FontStyle.Normal, UITheme.TextSecondary).gameObject.AddComponent<LayoutElement>().preferredHeight = 40; return; }

            var avg = B.Card(scroll, "Per-Game Averages", UITheme.AccentPrimary);
            var at = B.Text(avg, "A", "", 12, FontStyle.Normal, Color.white); B.FillCard(at); at.horizontalOverflow = HorizontalWrapMode.Wrap;
            at.text = $"PPG: {s.PPG:0.0}    RPG: {s.RPG:0.0}    APG: {s.APG:0.0}\nSPG: {s.SPG:0.0}    BPG: {s.BPG:0.0}    TPG: {s.TPG:0.0}\nMPG: {s.MPG:0.0}    +/-: {s.AvgPlusMinus:+0.0;-0.0}";

            var sh = B.Card(scroll, "Shooting Splits", UITheme.AccentPrimary);
            var sht = B.Text(sh, "S", "", 12, FontStyle.Normal, Color.white); B.FillCard(sht); sht.horizontalOverflow = HorizontalWrapMode.Wrap;
            sht.text = $"FG: {s.FG_Made}/{s.FG_Attempts} ({s.FG_Pct * 100:0.0}%)\n3P: {s.ThreeP_Made}/{s.ThreeP_Attempts} ({s.ThreeP_Pct * 100:0.0}%)\n" +
                $"FT: {s.FT_Made}/{s.FT_Attempts} ({s.FT_Pct * 100:0.0}%)\neFG%: {s.EffectiveFGPct * 100:0.0}    TS%: {s.TrueShootingPct * 100:0.0}";

            var adv = B.Card(scroll, "Advanced", UITheme.AccentPrimary);
            var advt = B.Text(adv, "Adv", "", 12, FontStyle.Normal, Color.white); B.FillCard(advt); advt.horizontalOverflow = HorizontalWrapMode.Wrap;
            advt.text = $"PER: {s.PER:0.0}    USG%: {s.UsgPct * 100:0.0}\nWS: {s.WinShares:0.0}    BPM: {s.BoxPlusMinus:+0.0;-0.0}    VORP: {s.VORP:0.0}";
        }

        private void BuildScouting(RectTransform parent, Player p)
        {
            var scroll = B.ScrollArea(parent);
            var card = B.Card(scroll, "Scouting Report", UITheme.AccentPrimary);
            var text = B.Text(card, "Scout", "", 12, FontStyle.Normal, Color.white); B.FillCard(text); text.horizontalOverflow = HorizontalWrapMode.Wrap;
            string r = $"{p.FirstName} {p.LastName} — {p.PositionString}\n{p.HeightFormatted}, {p.WeightLbs:0} lbs\n\n";
            if (p.YearsPro == 0) r += "A rookie still adapting to the NBA game.\n\n";
            else if (p.YearsPro >= 10) r += $"A seasoned {p.YearsPro}-year veteran with deep experience.\n\n";
            else r += $"A {p.YearsPro}-year pro continuing to develop.\n\n";
            var stats = p.CurrentSeasonStats;
            if (stats != null && stats.GamesPlayed > 0)
            {
                if (stats.PPG >= 20) r += "Elite scorer who can take over games.\n";
                else if (stats.PPG >= 15) r += "Reliable scoring option in the rotation.\n";
                if (stats.RPG >= 10) r += "Dominant presence on the boards.\n";
                if (stats.APG >= 7) r += "Outstanding playmaker and floor general.\n";
                if (stats.SPG >= 1.5f) r += "Disruptive defender who creates turnovers.\n";
                if (stats.BPG >= 2) r += "Imposing rim protector.\n";
            }
            else r += "No game data available yet this season.\n";
            text.text = r;
        }

        private void BuildContract(RectTransform parent, Player p)
        {
            var scroll = B.ScrollArea(parent);
            var card = B.Card(scroll, "Contract Details", UITheme.AccentPrimary);
            var text = B.Text(card, "C", "", 12, FontStyle.Normal, Color.white); B.FillCard(text); text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var c = p.CurrentContract;
            if (c == null) { text.text = "No active contract."; return; }
            string opts = "";
            if (c.HasPlayerOption) opts += $"Player Option in Year {c.OptionYear}\n";
            if (c.HasTeamOption) opts += $"Team Option in Year {c.OptionYear}\n";
            if (c.HasNoTradeClause) opts += "No-Trade Clause\n";
            if (c.TradeKickerPercent > 0) opts += $"Trade Kicker: {c.TradeKickerPercent:0}%\n";
            if (string.IsNullOrEmpty(opts)) opts = "None\n";
            text.text = $"Type: {c.Type}\nAnnual Salary: ${c.CurrentYearSalary / 1000000.0:F2}M\n" +
                $"Years Remaining: {c.YearsRemaining}\nTotal Remaining: ${c.GetTotalRemainingValue() / 1000000.0:F2}M\n" +
                $"Guaranteed: ${c.GuaranteedMoney / 1000000.0:F2}M\n\nOptions & Clauses:\n{opts}\n" +
                $"Bird Rights: {c.BirdRights}\n" + (c.IsExpiring ? "CONTRACT EXPIRING" : "");
        }
    }
}
