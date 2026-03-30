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
            var sc = GameManager.Instance?.SeasonController;
            if (sc == null) return;

            // Two columns side by side — both stretch to fill
            var hlg = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(2, 2, 0, 0);

            BuildConferenceColumn(parent, "Eastern", team, teamColor, sc);
            BuildConferenceColumn(parent, "Western", team, teamColor, sc);
        }

        private void BuildConferenceColumn(RectTransform parent, string conference, Team team, Color teamColor, SeasonController sc)
        {
            var col = B.Child(parent, conference);
            col.AddComponent<Image>().color = UITheme.CardBackground;
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0; vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true; // rows expand to fill
            vlg.padding = new RectOffset(0, 0, 0, 0);

            var colRT = col.GetComponent<RectTransform>();

            // Conference title — fixed height, doesn't expand
            var titleRow = B.Child(colRT, "Title");
            var titleLE = titleRow.AddComponent<LayoutElement>(); titleLE.flexibleHeight = 0; titleLE.preferredHeight = 24;
            titleRow.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var tt = B.Text(titleRow.GetComponent<RectTransform>(), "T", $"{conference.ToUpper()} CONFERENCE", 12, FontStyle.Bold, UITheme.AccentPrimary);
            B.Stretch(tt.gameObject); tt.alignment = TextAnchor.MiddleCenter;

            // Header row — fixed height
            var hdr = B.TableRow(colRT, 0, UITheme.PanelSurface);
            var hdrLE = hdr.gameObject.GetComponent<LayoutElement>() ?? hdr.gameObject.AddComponent<LayoutElement>();
            hdrLE.flexibleHeight = 0; hdrLE.preferredHeight = 20;
            B.TableCell(hdr, "#", 24, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "", 18);
            B.TableCell(hdr, "Team", 130, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "W", 28, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "L", 28, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "PCT", 42, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(hdr, "GB", 34, FontStyle.Bold, UITheme.AccentPrimary);

            var standings = sc.GetConferenceStandings(conference);
            if (standings == null) return;

            for (int i = 0; i < standings.Count; i++)
            {
                var s = standings[i];
                int rank = i + 1;
                bool isPlayer = s.TeamId == team.TeamId;
                var bgColor = isPlayer ? UITheme.TeamTintedCard(teamColor, 0.25f) :
                    (i % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg);

                // Divider lines — thin, don't expand
                if (rank == 7 || rank == 11)
                {
                    var line = new GameObject(rank == 7 ? "PlayInLine" : "PlayoffLine", typeof(RectTransform));
                    line.transform.SetParent(colRT, false);
                    line.AddComponent<Image>().color = rank == 7 ? UITheme.Warning : UITheme.Danger;
                    var lineLE = line.AddComponent<LayoutElement>(); lineLE.preferredHeight = 2; lineLE.flexibleHeight = 0;
                }

                // Data row — flexibleHeight = 1 so it expands equally with other rows
                var row = B.TableRow(colRT, 0, bgColor);
                var rowLE = row.gameObject.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
                rowLE.flexibleHeight = 1;

                B.TableCell(row, rank.ToString(), 24, FontStyle.Normal,
                    rank <= 6 ? UITheme.Success : rank <= 10 ? UITheme.Warning : UITheme.TextSecondary);

                var logoGo = new GameObject("Logo", typeof(RectTransform));
                logoGo.transform.SetParent(row.transform, false);
                var logoImg = logoGo.AddComponent<Image>(); logoImg.preserveAspect = true;
                var logoSprite = ArtManager.GetTeamLogo(s.TeamId);
                if (logoSprite != null) logoImg.sprite = logoSprite;
                logoGo.AddComponent<LayoutElement>().preferredWidth = 18;

                var teamObj = GameManager.Instance?.GetTeam(s.TeamId);
                string teamName = teamObj != null ? $"{teamObj.City} {teamObj.Name}" : s.TeamName;
                B.TableCell(row, teamName, 130, isPlayer ? FontStyle.Bold : FontStyle.Normal,
                    isPlayer ? Color.white : UITheme.TextSecondary);
                B.TableCell(row, s.Wins.ToString(), 28, FontStyle.Normal, Color.white);
                B.TableCell(row, s.Losses.ToString(), 28, FontStyle.Normal, Color.white);
                B.TableCell(row, s.WinPercentage.ToString(".000"), 42, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, s.GamesBehind == 0 ? "—" : s.GamesBehind.ToString("0.0"), 34,
                    FontStyle.Normal, UITheme.TextSecondary);
            }
        }
    }
}
