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
        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            var scroll = B.ScrollArea(parent);
            var title = B.Text(scroll, "Title", "STANDINGS", 18, FontStyle.Bold, UITheme.AccentPrimary);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            var sc = GameManager.Instance?.SeasonController;
            if (sc == null) return;

            BuildConferenceTable(scroll, "EASTERN CONFERENCE", "Eastern", team, teamColor, sc);
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(scroll, false);
            spacer.AddComponent<LayoutElement>().preferredHeight = 16;
            BuildConferenceTable(scroll, "WESTERN CONFERENCE", "Western", team, teamColor, sc);
        }

        private void BuildConferenceTable(RectTransform parent, string title, string conference,
            Team team, Color teamColor, SeasonController sc)
        {
            var confTitle = B.Text(parent, $"Title_{conference}", title, 14, FontStyle.Bold, UITheme.AccentSecondary);
            confTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            var headerRow = B.TableRow(parent, 26, UITheme.FMCardHeaderBg);
            B.TableCell(headerRow, "#", 30, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "", 24);
            B.TableCell(headerRow, "Team", 160, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "W", 35, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "L", 35, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "PCT", 50, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "GB", 40, FontStyle.Bold, UITheme.AccentPrimary);

            var standings = sc.GetConferenceStandings(conference);
            if (standings == null) return;

            for (int i = 0; i < standings.Count; i++)
            {
                var s = standings[i];
                int rank = i + 1;
                bool isPlayer = s.TeamId == team.TeamId;
                var bgColor = isPlayer ? UITheme.TeamTintedCard(teamColor, 0.25f) :
                    (i % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg);

                if (rank == 11)
                {
                    var line = new GameObject("PlayoffLine", typeof(RectTransform));
                    line.transform.SetParent(parent, false);
                    line.AddComponent<Image>().color = UITheme.Danger;
                    line.AddComponent<LayoutElement>().preferredHeight = 2;
                }

                var row = B.TableRow(parent, 26, bgColor);
                B.TableCell(row, rank.ToString(), 30, FontStyle.Normal,
                    rank <= 6 ? UITheme.Success : rank <= 10 ? UITheme.Warning : UITheme.TextSecondary);

                var logoGo = new GameObject("Logo", typeof(RectTransform));
                logoGo.transform.SetParent(row.transform, false);
                var logoImg = logoGo.AddComponent<Image>(); logoImg.preserveAspect = true;
                var logoSprite = ArtManager.GetTeamLogo(s.TeamId);
                if (logoSprite != null) logoImg.sprite = logoSprite;
                logoGo.AddComponent<LayoutElement>().preferredWidth = 24;

                var teamObj = GameManager.Instance?.GetTeam(s.TeamId);
                string teamName = teamObj != null ? $"{teamObj.City} {teamObj.Name}" : s.TeamName;
                B.TableCell(row, teamName, 160, isPlayer ? FontStyle.Bold : FontStyle.Normal,
                    isPlayer ? Color.white : UITheme.TextSecondary);
                B.TableCell(row, s.Wins.ToString(), 35, FontStyle.Normal, Color.white);
                B.TableCell(row, s.Losses.ToString(), 35, FontStyle.Normal, Color.white);
                B.TableCell(row, s.WinPercentage.ToString(".000"), 50, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, s.GamesBehind == 0 ? "—" : s.GamesBehind.ToString("0.0"), 40,
                    FontStyle.Normal, UITheme.TextSecondary);
            }
        }
    }
}
