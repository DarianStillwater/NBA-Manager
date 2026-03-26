using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;
using NBAHeadCoach.UI.Shell;
using B = NBAHeadCoach.UI.Shell.UIBuilder;

namespace NBAHeadCoach.UI.GamePanels
{
    public class StandingsGamePanel : IGamePanel
    {
        private string _activeConference = "Eastern";
        private GameShell _shell;

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            // Tab bar at top
            var tabBar = B.Child(parent, "TabBar");
            var tbr = tabBar.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = Vector2.one;
            tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 32);
            var tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 0; tabHlg.childControlWidth = true; tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;

            foreach (var conf in new[] { "Eastern", "Western" })
            {
                var c = conf;
                var tGo = B.Child(tbr, $"Tab_{conf}");
                tGo.AddComponent<Image>().color = conf == _activeConference ? UITheme.FMNavActive : Color.clear;
                var tText = B.Text(tGo.GetComponent<RectTransform>(), "T", $"{conf.ToUpper()} CONFERENCE",
                    12, FontStyle.Bold, conf == _activeConference ? UITheme.AccentPrimary : UITheme.TextSecondary);
                tText.alignment = TextAnchor.MiddleCenter; B.Stretch(tText.gameObject);

                // Accent underline
                var acc = B.Child(tGo.GetComponent<RectTransform>(), "Acc");
                var accr = acc.GetComponent<RectTransform>();
                accr.anchorMin = Vector2.zero; accr.anchorMax = new Vector2(1, 0); accr.sizeDelta = new Vector2(0, 2);
                acc.AddComponent<Image>().color = conf == _activeConference ? UITheme.AccentPrimary : Color.clear;

                tGo.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _activeConference = c;
                    // Find shell and rebuild
                    var shell = GameManager.Instance?.GetComponent<GameShell>();
                    shell?.RegisterPanel("Standings", this);
                    shell?.ShowPanel("Standings");
                });
            }

            // Content area below tabs
            var content = B.Child(parent, "Content");
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.offsetMin = Vector2.zero; cr.offsetMax = new Vector2(0, -32);

            var area = B.FixedArea(cr, spacing: 0, padding: 2);

            var sc = GameManager.Instance?.SeasonController;
            if (sc == null) return;

            BuildConferenceTable(area, _activeConference, team, teamColor, sc);
        }

        private void BuildConferenceTable(RectTransform parent, string conference, Team team, Color teamColor, SeasonController sc)
        {
            // Header row
            var headerRow = B.TableRow(parent, 22, UITheme.FMCardHeaderBg);
            B.TableCell(headerRow, "#", 28, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "", 22);
            B.TableCell(headerRow, "Team", 150, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "W", 32, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "L", 32, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "PCT", 45, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "GB", 38, FontStyle.Bold, UITheme.AccentPrimary);

            var standings = sc.GetConferenceStandings(conference);
            if (standings == null) return;

            for (int i = 0; i < standings.Count; i++)
            {
                var s = standings[i];
                int rank = i + 1;
                bool isPlayer = s.TeamId == team.TeamId;
                var bgColor = isPlayer ? UITheme.TeamTintedCard(teamColor, 0.25f) :
                    (i % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg);

                // Playoff cutoff line after 10th team
                if (rank == 11)
                {
                    var line = new GameObject("PlayoffLine", typeof(RectTransform));
                    line.transform.SetParent(parent, false);
                    line.AddComponent<Image>().color = UITheme.Danger;
                    line.AddComponent<LayoutElement>().preferredHeight = 2;
                }

                var row = B.TableRow(parent, 22, bgColor);
                B.TableCell(row, rank.ToString(), 28, FontStyle.Normal,
                    rank <= 6 ? UITheme.Success : rank <= 10 ? UITheme.Warning : UITheme.TextSecondary);

                var logoGo = new GameObject("Logo", typeof(RectTransform));
                logoGo.transform.SetParent(row.transform, false);
                var logoImg = logoGo.AddComponent<Image>(); logoImg.preserveAspect = true;
                var logoSprite = ArtManager.GetTeamLogo(s.TeamId);
                if (logoSprite != null) logoImg.sprite = logoSprite;
                logoGo.AddComponent<LayoutElement>().preferredWidth = 22;

                var teamObj = GameManager.Instance?.GetTeam(s.TeamId);
                string teamName = teamObj != null ? $"{teamObj.City} {teamObj.Name}" : s.TeamName;
                B.TableCell(row, teamName, 150, isPlayer ? FontStyle.Bold : FontStyle.Normal,
                    isPlayer ? Color.white : UITheme.TextSecondary);
                B.TableCell(row, s.Wins.ToString(), 32, FontStyle.Normal, Color.white);
                B.TableCell(row, s.Losses.ToString(), 32, FontStyle.Normal, Color.white);
                B.TableCell(row, s.WinPercentage.ToString(".000"), 45, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, s.GamesBehind == 0 ? "—" : s.GamesBehind.ToString("0.0"), 38,
                    FontStyle.Normal, UITheme.TextSecondary);
            }
        }
    }
}
