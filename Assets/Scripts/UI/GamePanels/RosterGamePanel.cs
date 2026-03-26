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
            var scroll = B.FixedArea(parent, spacing: 1, padding: 4);
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
                B.TableCell(row, stats != null && stats.GamesPlayed > 0 ? stats.PPG.ToString("0.0") : "—", 45, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, stats != null && stats.GamesPlayed > 0 ? stats.RPG.ToString("0.0") : "—", 45, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, stats != null && stats.GamesPlayed > 0 ? stats.APG.ToString("0.0") : "—", 45, FontStyle.Normal, UITheme.TextSecondary);

                string status = p.IsInjured ? "INJURED" : p.Energy < 50 ? "TIRED" : "OK";
                Color statusColor = p.IsInjured ? UITheme.Danger : p.Energy < 50 ? UITheme.Warning : UITheme.Success;
                B.TableCell(row, status, 70, FontStyle.Bold, statusColor);

                var player = p;
                row.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowPlayerDetail(player));
            }
        }

        private void ShowPlayerDetail(Player player) { _detailPlayer = player; _playerTab = "Overview"; RebuildDetail(); }
        private void SwitchTab(string tab) { _playerTab = tab; RebuildDetail(); }

        private void RebuildDetail()
        {
            _shell?.RegisterPanel("PlayerDetail", new LegacyPanelAdapter((p, t, c) => BuildDetailContent(p, _detailPlayer)));
            _shell?.ShowPanel("PlayerDetail");
        }

        // ═══════════════════════════════════════════════════════
        //  PLAYER DETAIL
        // ═══════════════════════════════════════════════════════

        private void BuildDetailContent(RectTransform parent, Player p)
        {
            // Top bar
            var topBar = B.Child(parent, "TopBar");
            topBar.AddComponent<Image>().color = UITheme.CardHeaderFrosted;
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

            // Accent line
            var accent = B.Child(parent, "Accent");
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0, 1); ar.anchorMax = Vector2.one;
            ar.pivot = new Vector2(0.5f, 1); ar.anchoredPosition = new Vector2(0, -40); ar.sizeDelta = new Vector2(0, 3);
            accent.AddComponent<Image>().color = UITheme.AccentPrimary;

            // Tab bar — 2 tabs only
            var tabBar = B.Child(parent, "TabBar");
            tabBar.AddComponent<Image>().color = UITheme.PanelSurface;
            var tbr = tabBar.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = Vector2.one;
            tbr.pivot = new Vector2(0.5f, 1); tbr.anchoredPosition = new Vector2(0, -43); tbr.sizeDelta = new Vector2(0, 32);
            var th = tabBar.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 0; th.padding = new RectOffset(12, 12, 0, 0);
            th.childControlWidth = true; th.childControlHeight = true; th.childForceExpandWidth = false;

            foreach (var tab in new[] { "Overview", "Stats" })
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

            // Content area
            var cGo = B.Child(parent, "TabContent");
            var cr = cGo.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.offsetMin = Vector2.zero; cr.offsetMax = new Vector2(0, -75);

            switch (_playerTab)
            {
                case "Overview": BuildOverview(cr, p); break;
                case "Stats": BuildStats(cr, p); break;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  OVERVIEW — FM-style: left bio/contract, right scouting badges
        // ═══════════════════════════════════════════════════════

        private void BuildOverview(RectTransform parent, Player p)
        {
            // Two-column split
            var leftGo = B.Child(parent, "Left");
            var lr = leftGo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0.55f, 1); lr.sizeDelta = Vector2.zero;

            var rightGo = B.Child(parent, "Right");
            var rr = rightGo.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.56f, 0); rr.anchorMax = Vector2.one; rr.sizeDelta = Vector2.zero;

            // LEFT: scrollable bio + contract + status
            var leftScroll = B.FixedArea(lr, spacing: 0, padding: 4);

            // Bio
            string draftInfo = p.DraftRound > 0 ? $"{p.DraftYear} R{p.DraftRound} Pick #{p.DraftPick}" : "Undrafted";
            string nationality = string.IsNullOrEmpty(p.Nationality) ? "USA" : p.Nationality;
            string college = string.IsNullOrEmpty(p.College) ? "—" : p.College;
            string expStr = p.YearsPro == 0 ? "Rookie" : $"{p.YearsPro} yrs";

            var bioTitle = B.Text(leftScroll, "BioTitle", "BIOGRAPHY", 11, FontStyle.Bold, UITheme.AccentPrimary);
            bioTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            AddInfoRow(leftScroll, "Height", p.HeightFormatted);
            AddInfoRow(leftScroll, "Weight", $"{p.WeightLbs:0} lbs");
            AddInfoRow(leftScroll, "Age", p.Age.ToString());
            AddInfoRow(leftScroll, "Born", p.BirthDateFormatted);
            AddInfoRow(leftScroll, "Nationality", nationality);
            AddInfoRow(leftScroll, "College", college);
            AddInfoRow(leftScroll, "Draft", draftInfo);
            AddInfoRow(leftScroll, "Experience", expStr);

            AddSpacer(leftScroll, 8);

            // Contract
            var contractTitle = B.Text(leftScroll, "ContractTitle", "CONTRACT", 11, FontStyle.Bold, UITheme.AccentPrimary);
            contractTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            var c = p.CurrentContract;
            if (c != null)
            {
                AddInfoRow(leftScroll, "Type", c.Type.ToString());
                AddInfoRow(leftScroll, "Salary", $"${c.CurrentYearSalary / 1000000.0:F1}M");
                AddInfoRow(leftScroll, "Years Left", c.YearsRemaining.ToString());
                AddInfoRow(leftScroll, "Total Value", $"${c.GetTotalRemainingValue() / 1000000.0:F1}M");
                if (c.HasPlayerOption) AddInfoRow(leftScroll, "Option", $"Player Opt Yr {c.OptionYear}");
                if (c.HasTeamOption) AddInfoRow(leftScroll, "Option", $"Team Opt Yr {c.OptionYear}");
                if (c.HasNoTradeClause) AddInfoRow(leftScroll, "NTC", "Yes");
                if (c.IsExpiring) AddInfoRow(leftScroll, "Status", "EXPIRING", UITheme.Warning);
            }
            else
            {
                AddInfoRow(leftScroll, "Status", "No contract");
            }

            AddSpacer(leftScroll, 8);

            // Status
            var statusTitle = B.Text(leftScroll, "StatusTitle", "STATUS", 11, FontStyle.Bold, UITheme.AccentPrimary);
            statusTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            AddInfoRow(leftScroll, "Energy", $"{p.Energy:0}%", p.Energy > 70 ? UITheme.Success : p.Energy > 40 ? UITheme.Warning : UITheme.Danger);
            AddInfoRow(leftScroll, "Morale", $"{p.Morale:0}%", p.Morale > 60 ? UITheme.Success : p.Morale > 35 ? UITheme.Warning : UITheme.Danger);
            AddInfoRow(leftScroll, "Form", $"{p.Form:0}%", p.Form > 60 ? UITheme.Success : p.Form > 35 ? UITheme.Warning : UITheme.Danger);
            string health = p.IsInjured ? $"INJURED ({p.InjuryDaysRemaining}d)" : "Healthy";
            AddInfoRow(leftScroll, "Health", health, p.IsInjured ? UITheme.Danger : UITheme.Success);
            if (p.IsCaptain) AddInfoRow(leftScroll, "Role", "\u2605 CAPTAIN", UITheme.AccentPrimary);

            // RIGHT: scrollable scouting badges
            var rightScroll = B.FixedArea(rr, spacing: 0, padding: 4);

            var scoutTitle = B.Text(rightScroll, "ScoutTitle", "SCOUTING REPORT", 11, FontStyle.Bold, UITheme.AccentSecondary);
            scoutTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            // Generate badges from attributes (hidden numbers → text descriptions)
            var badges = GenerateScoutingBadges(p);
            foreach (var badge in badges)
            {
                var badgeRow = B.TableRow(rightScroll, 22, Color.clear);
                var badgeGo = B.Child(badgeRow, "Badge");
                badgeGo.AddComponent<LayoutElement>().preferredWidth = 16;
                badgeGo.AddComponent<Image>().color = badge.color;
                B.TableCell(badgeRow, badge.text, 200, FontStyle.Normal, badge.color);
            }

            if (badges.Count == 0)
            {
                var noBadges = B.Text(rightScroll, "NoBadges", "No scouting data available yet.", 12, FontStyle.Italic, UITheme.TextSecondary);
                noBadges.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            }

            // Tendencies section
            AddSpacer(rightScroll, 8);
            var tendTitle = B.Text(rightScroll, "TendTitle", "TENDENCIES", 11, FontStyle.Bold, UITheme.AccentSecondary);
            tendTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            if (p.Tendencies != null)
            {
                AddTendencyRow(rightScroll, "Shot Selection", p.Tendencies.ShotSelection, "Patient", "Quick Trigger");
                AddTendencyRow(rightScroll, "Ball Movement", p.Tendencies.BallMovement, "Ball Stopper", "Willing Passer");
                AddTendencyRow(rightScroll, "Defense", p.Tendencies.DefensiveGambling, "Conservative", "Aggressive");
                AddTendencyRow(rightScroll, "Clutch", p.Tendencies.ClutchBehavior, "Shrinks", "Rises Up");
                AddTendencyRow(rightScroll, "Effort", p.Tendencies.EffortConsistency, "Coasts", "Always Hustles");
            }
            else
            {
                var noTend = B.Text(rightScroll, "NoTend", "No tendency data.", 12, FontStyle.Italic, UITheme.TextSecondary);
                noTend.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  STATS — Season + Career + Advanced
        // ═══════════════════════════════════════════════════════

        private void BuildStats(RectTransform parent, Player p)
        {
            var scroll = B.FixedArea(parent, spacing: 1, padding: 4);
            var s = p.CurrentSeasonStats;

            // Current Season
            var seasonTitle = B.Text(scroll, "SeasonTitle", "CURRENT SEASON", 11, FontStyle.Bold, UITheme.AccentPrimary);
            seasonTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            if (s != null && s.GamesPlayed > 0)
            {
                var hdr1 = B.TableRow(scroll, 20, UITheme.CardHeaderFrosted);
                B.TableCell(hdr1, "GP", 30, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "GS", 30, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "MPG", 35, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "PPG", 35, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "RPG", 35, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "APG", 35, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "SPG", 35, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "BPG", 35, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "FG%", 40, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "3P%", 40, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(hdr1, "FT%", 40, FontStyle.Bold, UITheme.AccentPrimary);

                var row1 = B.TableRow(scroll, 22, UITheme.CardFrosted);
                B.TableCell(row1, s.GamesPlayed.ToString(), 30, FontStyle.Bold, Color.white);
                B.TableCell(row1, s.GamesStarted.ToString(), 30);
                B.TableCell(row1, s.MPG.ToString("0.0"), 35);
                B.TableCell(row1, s.PPG.ToString("0.0"), 35, FontStyle.Bold, Color.white);
                B.TableCell(row1, s.RPG.ToString("0.0"), 35);
                B.TableCell(row1, s.APG.ToString("0.0"), 35);
                B.TableCell(row1, s.SPG.ToString("0.0"), 35);
                B.TableCell(row1, s.BPG.ToString("0.0"), 35);
                B.TableCell(row1, $"{s.FG_Pct * 100:0.0}", 40);
                B.TableCell(row1, $"{s.ThreeP_Pct * 100:0.0}", 40);
                B.TableCell(row1, $"{s.FT_Pct * 100:0.0}", 40);
            }
            else
            {
                var noStats = B.Text(scroll, "No", "No games played this season.", 12, FontStyle.Italic, UITheme.TextSecondary);
                noStats.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            }

            AddSpacer(scroll, 10);

            // Career Averages
            var careerTitle = B.Text(scroll, "CareerTitle",
                p.CareerStats != null && p.CareerStats.Count > 1 ? $"CAREER ({p.CareerStats.Count} seasons)" : "CAREER",
                11, FontStyle.Bold, UITheme.AccentPrimary);
            careerTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            if (p.CareerStats != null && p.CareerStats.Count > 0)
            {
                int totalGP = p.CareerStats.Sum(cs => cs.GamesPlayed);
                if (totalGP > 0)
                {
                    float cPPG = (float)p.CareerStats.Sum(cs => cs.Points) / totalGP;
                    float cRPG = (float)p.CareerStats.Sum(cs => cs.TotalRebounds) / totalGP;
                    float cAPG = (float)p.CareerStats.Sum(cs => cs.Assists) / totalGP;
                    float cSPG = (float)p.CareerStats.Sum(cs => cs.Steals) / totalGP;
                    float cBPG = (float)p.CareerStats.Sum(cs => cs.Blocks) / totalGP;
                    int cFGM = p.CareerStats.Sum(cs => cs.FG_Made);
                    int cFGA = p.CareerStats.Sum(cs => cs.FG_Attempts);
                    int c3M = p.CareerStats.Sum(cs => cs.ThreeP_Made);
                    int c3A = p.CareerStats.Sum(cs => cs.ThreeP_Attempts);
                    int cFTM = p.CareerStats.Sum(cs => cs.FT_Made);
                    int cFTA = p.CareerStats.Sum(cs => cs.FT_Attempts);

                    var crow = B.TableRow(scroll, 22, UITheme.CardFrosted);
                    B.TableCell(crow, totalGP.ToString(), 30, FontStyle.Bold, Color.white);
                    B.TableCell(crow, "", 30);
                    B.TableCell(crow, "", 35);
                    B.TableCell(crow, cPPG.ToString("0.0"), 35, FontStyle.Bold, Color.white);
                    B.TableCell(crow, cRPG.ToString("0.0"), 35);
                    B.TableCell(crow, cAPG.ToString("0.0"), 35);
                    B.TableCell(crow, cSPG.ToString("0.0"), 35);
                    B.TableCell(crow, cBPG.ToString("0.0"), 35);
                    B.TableCell(crow, cFGA > 0 ? $"{(float)cFGM / cFGA * 100:0.0}" : "—", 40);
                    B.TableCell(crow, c3A > 0 ? $"{(float)c3M / c3A * 100:0.0}" : "—", 40);
                    B.TableCell(crow, cFTA > 0 ? $"{(float)cFTM / cFTA * 100:0.0}" : "—", 40);
                }
            }

            AddSpacer(scroll, 10);

            // Advanced
            var advTitle = B.Text(scroll, "AdvTitle", "ADVANCED", 11, FontStyle.Bold, UITheme.AccentPrimary);
            advTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            if (s != null && s.GamesPlayed > 0)
            {
                var ahdr = B.TableRow(scroll, 20, UITheme.CardHeaderFrosted);
                B.TableCell(ahdr, "PER", 40, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(ahdr, "TS%", 40, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(ahdr, "eFG%", 40, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(ahdr, "USG%", 40, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(ahdr, "WS", 40, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(ahdr, "BPM", 40, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(ahdr, "VORP", 40, FontStyle.Bold, UITheme.AccentPrimary);

                var arow = B.TableRow(scroll, 22, UITheme.CardFrosted);
                B.TableCell(arow, s.PER.ToString("0.0"), 40);
                B.TableCell(arow, $"{s.TrueShootingPct * 100:0.0}", 40);
                B.TableCell(arow, $"{s.EffectiveFGPct * 100:0.0}", 40);
                B.TableCell(arow, $"{s.UsgPct * 100:0.0}", 40);
                B.TableCell(arow, s.WinShares.ToString("0.0"), 40);
                B.TableCell(arow, s.BoxPlusMinus.ToString("+0.0;-0.0"), 40);
                B.TableCell(arow, s.VORP.ToString("0.0"), 40);
            }
            else
            {
                var noAdv = B.Text(scroll, "NoAdv", "Play games to generate advanced metrics.", 12, FontStyle.Italic, UITheme.TextSecondary);
                noAdv.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════

        private void AddInfoRow(RectTransform parent, string label, string value, Color? valueColor = null)
        {
            var row = B.TableRow(parent, 20, Color.clear);
            B.TableCell(row, label, 90, FontStyle.Normal, UITheme.TextSecondary);
            B.TableCell(row, value, 160, FontStyle.Bold, valueColor ?? Color.white);
        }

        private void AddSpacer(RectTransform parent, float height)
        {
            var sp = B.Child(parent, "Spacer");
            sp.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private void AddTendencyRow(RectTransform parent, string label, float value, string lowLabel, string highLabel)
        {
            // value is -100 to +100. Show as text description
            var row = B.TableRow(parent, 20, Color.clear);
            string desc;
            Color col;
            if (value > 50) { desc = highLabel; col = UITheme.Success; }
            else if (value > 15) { desc = $"Leans {highLabel}"; col = UITheme.TextSecondary; }
            else if (value < -50) { desc = lowLabel; col = UITheme.Danger; }
            else if (value < -15) { desc = $"Leans {lowLabel}"; col = UITheme.TextSecondary; }
            else { desc = "Balanced"; col = UITheme.TextSecondary; }
            B.TableCell(row, label, 100, FontStyle.Normal, UITheme.TextSecondary);
            B.TableCell(row, desc, 150, FontStyle.Normal, col);
        }

        private struct Badge { public string text; public Color color; }

        private List<Badge> GenerateScoutingBadges(Player p)
        {
            var badges = new List<Badge>();

            // Stats-based badges (visible to player — based on performance, not hidden attributes)
            var s = p.CurrentSeasonStats;
            if (s != null && s.GamesPlayed > 0)
            {
                if (s.PPG >= 25) badges.Add(new Badge { text = "Franchise Player", color = UITheme.AccentPrimary });
                else if (s.PPG >= 20) badges.Add(new Badge { text = "All-Star Caliber", color = UITheme.Success });
                else if (s.PPG >= 15) badges.Add(new Badge { text = "Reliable Scorer", color = UITheme.Success });
                if (s.RPG >= 10) badges.Add(new Badge { text = "Board Crasher", color = UITheme.Success });
                if (s.APG >= 8) badges.Add(new Badge { text = "Floor General", color = UITheme.Success });
                else if (s.APG >= 5) badges.Add(new Badge { text = "Good Playmaker", color = UITheme.Success });
                if (s.SPG >= 1.5f) badges.Add(new Badge { text = "Pickpocket", color = UITheme.Success });
                if (s.BPG >= 2) badges.Add(new Badge { text = "Rim Protector", color = UITheme.Success });
                if (s.ThreeP_Pct > 0.38f && s.ThreeP_Attempts > 20) badges.Add(new Badge { text = "Sharpshooter", color = UITheme.Success });
                if (s.ThreeP_Pct < 0.28f && s.ThreeP_Attempts > 20) badges.Add(new Badge { text = "Poor Shooter", color = UITheme.Danger });
                if (s.FT_Pct > 0.85f) badges.Add(new Badge { text = "Reliable Free Throw", color = UITheme.Success });
                if (s.FT_Pct < 0.60f && s.FT_Attempts > 10) badges.Add(new Badge { text = "Poor Free Throw", color = UITheme.Danger });
                if (s.TPG > 3.5f) badges.Add(new Badge { text = "Turnover Prone", color = UITheme.Danger });
            }

            // Experience-based
            if (p.YearsPro == 0) badges.Add(new Badge { text = "Rookie", color = UITheme.AccentSecondary });
            else if (p.YearsPro >= 12) badges.Add(new Badge { text = "Seasoned Veteran", color = UITheme.AccentSecondary });

            // Attribute-based (scouting report — text only, never numbers)
            if (p.Speed > 80) badges.Add(new Badge { text = "Speedster", color = UITheme.Success });
            if (p.Speed < 40) badges.Add(new Badge { text = "Slow Feet", color = UITheme.Danger });
            if (p.BallHandling > 80) badges.Add(new Badge { text = "Elite Ball Handler", color = UITheme.Success });
            if (p.Passing > 80) badges.Add(new Badge { text = "Exceptional Passer", color = UITheme.Success });
            if (p.Defense_Perimeter > 75) badges.Add(new Badge { text = "Lockdown Defender", color = UITheme.Success });
            if (p.Defense_Perimeter < 35) badges.Add(new Badge { text = "Defensive Liability", color = UITheme.Danger });
            if (p.Block > 70) badges.Add(new Badge { text = "Shot Blocker", color = UITheme.Success });
            if (p.Strength > 80) badges.Add(new Badge { text = "Physical", color = UITheme.Success });

            return badges;
        }
    }
}
