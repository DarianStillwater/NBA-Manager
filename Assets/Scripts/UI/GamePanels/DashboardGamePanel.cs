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
    public class DashboardGamePanel : IGamePanel
    {
        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            var content = B.Child(parent, "Content");
            var cr = B.Stretch(content);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.padding = new RectOffset(12, 12, 8, 8);
            vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true;

            var title = B.Text(cr, "Title", "DASHBOARD", 18, FontStyle.Bold, UITheme.AccentPrimary);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            var row1 = CreateRow(cr, "Row1", 140); BuildNextGameCard(row1, team, teamColor); BuildSeasonOverviewCard(row1, team, teamColor);
            var row2 = CreateRow(cr, "Row2", 140); BuildRecentResultsCard(row2, teamColor); BuildRosterSummaryCard(row2, team, teamColor);
            var row3 = CreateRow(cr, "Row3", 140); BuildFinancialCard(row3, team, teamColor); BuildCoachingStaffCard(row3, team, teamColor);
            var row4 = CreateRow(cr, "Row4", 160); BuildScheduleCard(row4, team, teamColor);
        }

        private RectTransform CreateRow(RectTransform parent, string name, float height)
        {
            var row = B.Child(parent, name);
            row.AddComponent<LayoutElement>().preferredHeight = height;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            return row.GetComponent<RectTransform>();
        }

        private void BuildNextGameCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = B.Card(parent, "NEXT GAME", teamColor);
            var nextGame = GameManager.Instance?.SeasonController?.GetNextGame();
            if (nextGame != null)
            {
                string opponentId = nextGame.HomeTeamId == team.TeamId ? nextGame.AwayTeamId : nextGame.HomeTeamId;
                bool isHome = nextGame.HomeTeamId == team.TeamId;
                var opponent = GameManager.Instance?.GetTeam(opponentId);

                var logoGo = B.Child(card, "OpponentLogo");
                var lr = logoGo.GetComponent<RectTransform>();
                lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(0, 1);
                lr.pivot = new Vector2(0, 0.5f); lr.sizeDelta = new Vector2(48, 0);
                lr.anchoredPosition = new Vector2(12, -(UITheme.FMCardHeaderHeight / 2));
                var logoImg = logoGo.AddComponent<Image>(); logoImg.preserveAspect = true;
                var oppLogo = ArtManager.GetTeamLogo(opponentId);
                if (oppLogo != null) logoImg.sprite = oppLogo;

                string oppName = opponent != null ? $"{opponent.City} {opponent.Name}" : opponentId;
                string oppRecord = opponent != null ? $"({opponent.Wins}-{opponent.Losses})" : "";
                var info = B.Text(card, "Info", $"vs {oppName} {oppRecord}\n{(isHome ? "HOME" : "AWAY")}  •  {nextGame.Date:MMM dd}",
                    13, FontStyle.Normal, UITheme.TextSecondary);
                var ir = info.GetComponent<RectTransform>();
                ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
                ir.offsetMin = new Vector2(68, 8); ir.offsetMax = new Vector2(-8, -(UITheme.FMCardHeaderHeight + 4));
                info.alignment = TextAnchor.MiddleLeft;
            }
            else
            {
                var noGame = B.Text(card, "NoGame", "No games scheduled", 13, FontStyle.Italic, UITheme.TextSecondary);
                B.FillCard(noGame); noGame.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void BuildSeasonOverviewCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = B.Card(parent, "SEASON OVERVIEW", teamColor);
            var sc = GameManager.Instance?.SeasonController;
            int rank = sc?.GetConferenceRank(team.TeamId) ?? 0;
            string conf = team.Conference ?? "—";
            var info = B.Text(card, "Info",
                $"Conference:  {conf}\nRank:  #{rank}\nRecord:  {team.Wins}-{team.Losses}\n" +
                $"Win%:  {(team.Wins + team.Losses > 0 ? ((float)team.Wins / (team.Wins + team.Losses)).ToString(".000") : "—")}",
                13, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(info); info.lineSpacing = 1.5f;
        }

        private void BuildRecentResultsCard(RectTransform parent, Color teamColor)
        {
            var card = B.Card(parent, "RECENT RESULTS", teamColor);
            var text = B.Text(card, "Content", "No games played yet", 13, FontStyle.Italic, UITheme.TextSecondary);
            B.FillCard(text); text.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildRosterSummaryCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = B.Card(parent, "ROSTER", teamColor);
            int rosterCount = team.RosterPlayerIds?.Count ?? 0;
            int injured = team.Roster?.Count(p => p.IsInjured) ?? 0;
            var topPlayer = team.Roster?.OrderByDescending(p => p.OverallRating).FirstOrDefault();
            string content = $"Players:  {rosterCount}\nInjured:  {injured}";
            if (topPlayer != null) content += $"\nStar:  {topPlayer.FirstName} {topPlayer.LastName}";
            var info = B.Text(card, "Info", content, 13, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(info); info.lineSpacing = 1.5f;
        }

        private void BuildFinancialCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = B.Card(parent, "FINANCES", teamColor);
            var info = B.Text(card, "Info", "Salary Cap:  $140.6M\nTeam Payroll:  —\nCap Space:  —\nLuxury Tax:  No",
                13, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(info); info.lineSpacing = 1.5f;
        }

        private void BuildCoachingStaffCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = B.Card(parent, "COACHING STAFF", teamColor);
            string coachName = GameManager.Instance?.Career?.PersonName ?? "Player";
            var info = B.Text(card, "Info", $"Head Coach:  {coachName}\nAssistants:  —\nScouts:  —",
                13, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(info); info.lineSpacing = 1.5f;
        }

        private void BuildScheduleCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = B.Card(parent, "UPCOMING SCHEDULE", teamColor);
            string content = "";
            var sc = GameManager.Instance?.SeasonController;
            if (sc != null)
            {
                var upcoming = sc.GetUpcomingGames(5);
                if (upcoming != null && upcoming.Count > 0)
                {
                    foreach (var game in upcoming)
                    {
                        bool isHome = game.HomeTeamId == team.TeamId;
                        string oppId = isHome ? game.AwayTeamId : game.HomeTeamId;
                        var opp = GameManager.Instance?.GetTeam(oppId);
                        content += $"{game.Date:MMM dd}  {(isHome ? "vs" : "@")} {opp?.Abbreviation ?? oppId}\n";
                    }
                }
                else content = "No upcoming games";
            }
            else content = "Schedule not available";
            var info = B.Text(card, "Info", content.TrimEnd(), 13, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(info); info.lineSpacing = 1.4f;
        }
    }
}
