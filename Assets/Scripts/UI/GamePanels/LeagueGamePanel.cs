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
    /// League hub: stat leaders, around-the-league scores, and the award history.
    /// </summary>
    public class LeagueGamePanel : IGamePanel
    {
        private string _tab = "LEADERS";
        private RectTransform _body;
        private Team _team;
        private Color _teamColor;

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            _team = team;
            _teamColor = teamColor;

            var root = B.Child(parent, "Root");
            B.Stretch(root);
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.padding = new RectOffset(12, 12, 8, 8);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            var rootRT = root.GetComponent<RectTransform>();

            var title = B.Text(rootRT, "Title", "LEAGUE", 18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            // Tab bar
            var tabs = B.Child(rootRT, "Tabs");
            var tabsLE = tabs.AddComponent<LayoutElement>(); tabsLE.preferredHeight = 28; tabsLE.flexibleHeight = 0;
            var hlg = tabs.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            foreach (var name in new[] { "LEADERS", "SCORES", "AWARDS" })
                BuildTabButton(tabs.GetComponent<RectTransform>(), name);

            // Body fills the rest
            var bodyGo = B.Child(rootRT, "Body");
            var bodyLE = bodyGo.AddComponent<LayoutElement>(); bodyLE.flexibleHeight = 1;
            _body = bodyGo.GetComponent<RectTransform>();

            RebuildBody();
        }

        private void BuildTabButton(RectTransform parent, string name)
        {
            var go = B.Child(parent, $"Tab_{name}");
            bool active = _tab == name;
            go.AddComponent<Image>().color = active
                ? UITheme.DarkenColor(_teamColor, 0.5f)
                : UITheme.FMCardHeaderBg;
            go.AddComponent<Button>().onClick.AddListener(() =>
            {
                _tab = name;
                GameManager.Instance?.GetComponent<GameShell>()?.ShowPanel("League");
            });
            var t = B.Text(go.GetComponent<RectTransform>(), "T", name, 12,
                active ? FontStyle.Bold : FontStyle.Normal,
                active ? Color.white : UITheme.TextSecondary);
            B.Stretch(t.gameObject); t.alignment = TextAnchor.MiddleCenter;
        }

        private void RebuildBody()
        {
            var scroll = B.FixedArea(_body);
            switch (_tab)
            {
                case "SCORES": BuildScores(scroll); break;
                case "AWARDS": BuildAwards(scroll); break;
                default: BuildLeaders(scroll); break;
            }
        }

        // ==================== LEADERS ====================

        private void BuildLeaders(RectTransform scroll)
        {
            var players = GameManager.Instance?.PlayerDatabase?.GetAllPlayers()?
                .Where(p => p?.CurrentSeasonStats != null && p.CurrentSeasonStats.GamesPlayed >= 10)
                .ToList();

            if (players == null || players.Count == 0)
            {
                Empty(scroll, "Stat leaders appear once the season is underway.");
                return;
            }

            BuildLeaderCard(scroll, "POINTS PER GAME", players, p => p.CurrentSeasonStats.PPG);
            BuildLeaderCard(scroll, "REBOUNDS PER GAME", players, p => p.CurrentSeasonStats.RPG);
            BuildLeaderCard(scroll, "ASSISTS PER GAME", players, p => p.CurrentSeasonStats.APG);
            BuildLeaderCard(scroll, "STEALS PER GAME", players, p => p.CurrentSeasonStats.SPG);
            BuildLeaderCard(scroll, "BLOCKS PER GAME", players, p => p.CurrentSeasonStats.BPG);
        }

        private void BuildLeaderCard(RectTransform scroll, string header, List<Player> players, Func<Player, float> stat)
        {
            var card = B.Card(scroll, header, _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 130;

            string pid = GameManager.Instance?.PlayerTeamId;
            var top = players.OrderByDescending(stat).Take(5).ToList();
            var lines = top.Select((p, i) =>
            {
                string row = $"{i + 1}.  {p.FullName}  ({TeamAbbr(p.TeamId)})   <b>{stat(p):0.0}</b>";
                return p.TeamId == pid ? $"<color=white><b>{row}</b></color>" : row;
            });

            var text = B.Text(card, "Leaders", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text); text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.3f;
        }

        // ==================== SCORES ====================

        private void BuildScores(RectTransform scroll)
        {
            var sc = GameManager.Instance?.SeasonController;
            if (sc == null) { Empty(scroll, "No schedule yet."); return; }

            var today = sc.CurrentDate.Date;

            // Today's slate (scheduled or already final)
            var slate = sc.Schedule?
                .Where(e => e.Type == CalendarEventType.Game && e.Date.Date == today)
                .ToList();
            if (slate != null && slate.Count > 0)
            {
                var card = B.Card(scroll, $"TODAY — {today:MMM dd}", _teamColor);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 40 + slate.Count * 18;
                var lines = slate.Select(e => e.IsCompleted
                    ? $"{TeamAbbr(e.AwayTeamId)} {e.AwayScore}  @  {TeamAbbr(e.HomeTeamId)} {e.HomeScore}   <size=10>FINAL</size>"
                    : $"{TeamAbbr(e.AwayTeamId)}  @  {TeamAbbr(e.HomeTeamId)}" + (e.IsPlayoffGame ? $"   <size=10>{e.Title}</size>" : ""));
                var text = B.Text(card, "Slate", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
                B.FillCard(text); text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.3f;
            }

            // Recent results, grouped by date (most recent 3 game days)
            var recentDays = sc.CompletedGames?
                .Where(g => g != null)
                .GroupBy(g => g.Date.Date)
                .OrderByDescending(g => g.Key)
                .Take(3)
                .ToList();

            if (recentDays == null || recentDays.Count == 0)
            {
                if (slate == null || slate.Count == 0)
                    Empty(scroll, "Around-the-league scores appear once games are played.");
                return;
            }

            foreach (var day in recentDays)
            {
                var games = day.OrderBy(g => g.HomeTeamId).ToList();
                var card = B.Card(scroll, $"RESULTS — {day.Key:MMM dd}", _teamColor);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 40 + games.Count * 18;

                string pid = GameManager.Instance?.PlayerTeamId;
                var lines = games.Select(g =>
                {
                    string row = $"{TeamAbbr(g.AwayTeamId)} {g.AwayScore}  @  {TeamAbbr(g.HomeTeamId)} {g.HomeScore}";
                    if (g.IsPlayoff) row += "   <size=10>PLAYOFFS</size>";
                    bool mine = g.HomeTeamId == pid || g.AwayTeamId == pid;
                    return mine ? $"<color=white><b>{row}</b></color>" : row;
                });

                var text = B.Text(card, "Games", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
                B.FillCard(text); text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.3f;
            }
        }

        // ==================== AWARDS ====================

        private void BuildAwards(RectTransform scroll)
        {
            var store = GameManager.Instance?.Awards;
            var history = store?.History;

            if (history == null || history.Count == 0)
            {
                Empty(scroll, "Season awards are announced when the champion is crowned;\nAll-Star selections at the February break.");
                return;
            }

            foreach (var awards in history.OrderByDescending(a => a.Season))
            {
                var card = B.Card(scroll, $"{awards.Season} SEASON", _teamColor);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 220;

                var lines = new List<string>();
                if (!string.IsNullOrEmpty(awards.ChampionTeamId))
                    lines.Add($"<b>NBA Champions:</b>  {TeamName(awards.ChampionTeamId)}");
                Add(lines, "Finals MVP", awards.FinalsMvpId);
                Add(lines, "MVP", awards.MvpId);
                Add(lines, "Defensive Player", awards.DpoyId);
                Add(lines, "Rookie of the Year", awards.RoyId);
                Add(lines, "Sixth Man", awards.SixthManId);
                Add(lines, "Most Improved", awards.MipId);
                if (!string.IsNullOrEmpty(awards.CotyId))
                    lines.Add($"Coach of the Year:  {TeamName(awards.CotyId)}");
                if (awards.AllNbaFirst != null && awards.AllNbaFirst.Count > 0)
                    lines.Add($"All-NBA 1st:  {string.Join(", ", awards.AllNbaFirst.Select(PlayerName))}");
                if (awards.AllStars != null && awards.AllStars.Count > 0)
                    lines.Add($"<size=11>All-Stars:  {string.Join(", ", awards.AllStars.Select(PlayerName))}</size>");

                var text = B.Text(card, "Awards", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
                B.FillCard(text); text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.3f;
            }
        }

        private static void Add(List<string> lines, string label, string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            lines.Add($"{label}:  {PlayerName(playerId)}");
        }

        // ==================== HELPERS ====================

        private void Empty(RectTransform scroll, string message)
        {
            var text = B.Text(scroll, "Empty", message, 13, FontStyle.Italic, UITheme.TextSecondary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
        }

        private static string TeamAbbr(string teamId) =>
            GameManager.Instance?.GetTeam(teamId)?.Abbreviation ?? teamId ?? "?";

        private static string TeamName(string teamId)
        {
            var t = GameManager.Instance?.GetTeam(teamId);
            return t != null ? $"{t.City} {t.Name}" : teamId;
        }

        private static string PlayerName(string playerId) =>
            GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId)?.FullName ?? playerId;
    }
}
