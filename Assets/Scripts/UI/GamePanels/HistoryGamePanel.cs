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
    /// The league's memory: past champions season by season, all-time career
    /// leaders (retirees included), single-game and season records, and the
    /// Hall of Fame.
    /// </summary>
    public class HistoryGamePanel : IGamePanel
    {
        private string _tab = "CHAMPIONS";
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

            var title = B.Text(rootRT, "Title", "HISTORY & RECORDS", 18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var tabs = B.Child(rootRT, "Tabs");
            var tabsLE = tabs.AddComponent<LayoutElement>(); tabsLE.preferredHeight = 28; tabsLE.flexibleHeight = 0;
            var hlg = tabs.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            foreach (var name in new[] { "CHAMPIONS", "ALL-TIME", "RECORDS", "HALL OF FAME" })
                BuildTabButton(tabs.GetComponent<RectTransform>(), name);

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
                GameManager.Instance?.GetComponent<GameShell>()?.ShowPanel("History");
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
                case "ALL-TIME": BuildAllTimeLeaders(scroll); break;
                case "RECORDS": BuildRecords(scroll); break;
                case "HALL OF FAME": BuildHallOfFame(scroll); break;
                default: BuildChampions(scroll); break;
            }
        }

        // ==================== CHAMPIONS ====================

        private void BuildChampions(RectTransform scroll)
        {
            var history = GameManager.Instance?.HistoryManager;
            var archives = history?.GetAllArchives();

            if (archives == null || archives.Count == 0)
            {
                Empty(scroll, "No completed seasons yet. The first banner goes up in June.");
                return;
            }

            var card = B.Card(scroll, "CHAMPIONS BY SEASON", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 60 + archives.Count * 28;
            var list = ListArea(card);

            foreach (var archive in archives)
            {
                var champStanding = archive.TeamStandings?.FirstOrDefault(t => t.TeamId == archive.ChampionTeamId);
                string record = champStanding != null ? $"{champStanding.Wins}-{champStanding.Losses}" : "";

                var row = B.TableRow(list, 24, UITheme.PanelSurface);
                B.TableCell(row, $"{archive.Year}-{(archive.Year + 1) % 100:D2}", 80, FontStyle.Bold, UITheme.TextPrimary);
                B.TableCell(row, $"🏆 {TeamName(archive.ChampionTeamId)}", 190, FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(row, record, 70, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, $"Finals MVP: {PlayerName(archive.FinalsMVPId)}", 200, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, $"MVP: {PlayerName(archive.MVPId)}", 190, FontStyle.Normal, UITheme.TextSecondary);
            }
        }

        // ==================== ALL-TIME LEADERS ====================

        private void BuildAllTimeLeaders(RectTransform scroll)
        {
            var gm = GameManager.Instance;
            var history = gm?.HistoryManager;
            var players = gm?.PlayerDatabase?.GetAllPlayers();
            if (history == null || players == null || players.Count == 0)
            {
                Empty(scroll, "Career leaders unavailable.");
                return;
            }

            BuildLeaderCard(scroll, "ALL-TIME POINTS", history, players, StatCategory.Points);
            BuildLeaderCard(scroll, "ALL-TIME REBOUNDS", history, players, StatCategory.Rebounds);
            BuildLeaderCard(scroll, "ALL-TIME ASSISTS", history, players, StatCategory.Assists);
            BuildLeaderCard(scroll, "ALL-TIME STEALS", history, players, StatCategory.Steals);
            BuildLeaderCard(scroll, "ALL-TIME BLOCKS", history, players, StatCategory.Blocks);
        }

        private void BuildLeaderCard(RectTransform scroll, string header, HistoryManager history,
            List<Player> players, StatCategory category)
        {
            var leaders = history.GetAllTimeLeaders(players, category, 10)
                .Where(l => l.value > 0).ToList();
            if (leaders.Count == 0) return;

            var card = B.Card(scroll, header, _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 46 + leaders.Count * 24;
            var list = ListArea(card);

            for (int i = 0; i < leaders.Count; i++)
            {
                var (player, value) = leaders[i];
                bool retired = player.RetirementYear > 0;

                var row = B.TableRow(list, 20, i % 2 == 0 ? UITheme.PanelSurface : UITheme.CardBackground);
                B.TableCell(row, $"{i + 1}.", 30, FontStyle.Bold, UITheme.TextSecondary);
                B.TableCell(row, player.FullName + (retired ? "  (retired)" : ""), 230,
                    FontStyle.Normal, retired ? UITheme.TextSecondary : UITheme.TextPrimary);
                B.TableCell(row, $"{value:N0}", 100, FontStyle.Bold, UITheme.AccentPrimary);
            }
        }

        // ==================== RECORDS ====================

        private void BuildRecords(RectTransform scroll)
        {
            var gm = GameManager.Instance;
            var history = gm?.HistoryManager;
            if (history == null)
            {
                Empty(scroll, "Records unavailable.");
                return;
            }

            var league = history.GetLeagueRecords();
            var leagueCard = B.Card(scroll, "LEAGUE RECORDS", _teamColor);
            leagueCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;
            var leagueList = ListArea(leagueCard);

            RecordRow(leagueList, "Points in a game", league?.SingleGamePoints);
            RecordRow(leagueList, "Rebounds in a game", league?.SingleGameRebounds);
            RecordRow(leagueList, "Assists in a game", league?.SingleGameAssists);
            SeasonRecordRow(leagueList, "Best scoring season (PPG)", league?.SeasonPPG);
            SeasonRecordRow(leagueList, "Best rebounding season (RPG)", league?.SeasonRPG);
            SeasonRecordRow(leagueList, "Best assist season (APG)", league?.SeasonAPG);
            if (league?.BestTeamRecord != null)
            {
                var row = B.TableRow(leagueList, 22, UITheme.PanelSurface);
                B.TableCell(row, "Best team record", 220, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, $"{league.BestTeamRecord.Wins}-{league.BestTeamRecord.Losses}", 90,
                    FontStyle.Bold, UITheme.AccentPrimary);
                B.TableCell(row, $"{TeamName(league.BestTeamRecord.TeamId)}, {league.BestTeamRecord.Year}", 240,
                    FontStyle.Normal, UITheme.TextSecondary);
            }

            // The player's franchise
            var franchise = history.GetFranchiseRecords(_team?.TeamId);
            var title = $"FRANCHISE RECORDS — {(_team?.Name ?? "YOUR TEAM").ToUpper()}";
            var card = B.Card(scroll, title, _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 210;
            var list = ListArea(card);

            if (franchise == null)
            {
                var none = B.Text(list, "None", "Franchise records build as games are played.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
                return;
            }

            RecordRow(list, "Points in a game", franchise.SingleGamePoints);
            RecordRow(list, "Rebounds in a game", franchise.SingleGameRebounds);
            RecordRow(list, "Assists in a game", franchise.SingleGameAssists);
            RecordRow(list, "Steals in a game", franchise.SingleGameSteals);
            RecordRow(list, "Blocks in a game", franchise.SingleGameBlocks);
            RecordRow(list, "Threes in a game", franchise.SingleGameThrees);
            RecordRow(list, "Team points in a game", franchise.TeamHighestScore);
        }

        private void RecordRow(RectTransform list, string label, SingleGameRecord record)
        {
            if (record == null || record.Value <= 0) return;
            var row = B.TableRow(list, 22, UITheme.PanelSurface);
            B.TableCell(row, label, 220, FontStyle.Normal, UITheme.TextSecondary);
            B.TableCell(row, record.Value.ToString(), 60, FontStyle.Bold, UITheme.AccentPrimary);
            string holder = string.IsNullOrEmpty(record.PlayerId) ? TeamName(record.TeamId) : PlayerName(record.PlayerId);
            B.TableCell(row, $"{holder}, {record.Year}", 260, FontStyle.Normal, UITheme.TextSecondary);
        }

        private void SeasonRecordRow(RectTransform list, string label, SeasonStatRecord record)
        {
            if (record == null || record.Value <= 0) return;
            var row = B.TableRow(list, 22, UITheme.PanelSurface);
            B.TableCell(row, label, 220, FontStyle.Normal, UITheme.TextSecondary);
            B.TableCell(row, $"{record.Value:F1}", 60, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(row, $"{PlayerName(record.PlayerId)}, {record.Year}", 260, FontStyle.Normal, UITheme.TextSecondary);
        }

        // ==================== HALL OF FAME ====================

        private void BuildHallOfFame(RectTransform scroll)
        {
            var history = GameManager.Instance?.HistoryManager;
            var inductees = history?.GetHallOfFame();

            if (inductees == null || inductees.Count == 0)
            {
                Empty(scroll, "The Hall is empty — legends become eligible five years after retirement.");
                return;
            }

            var card = B.Card(scroll, "HALL OF FAME", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 60 + inductees.Count * 40;
            var list = ListArea(card);

            foreach (var hof in inductees)
            {
                var entry = B.Text(list, "HOF",
                    $"<b>{hof.PlayerName}</b>  ·  class of {hof.InductionYear}{(hof.IsFirstBallot ? "  ·  first ballot" : "")}\n" +
                    $"{hof.CareerPoints:N0} pts   ·   {hof.Championships} title(s)   ·   {hof.MVPs} MVP(s)   ·   " +
                    $"{hof.AllStarSelections} All-Star   ·   {hof.AllNBASelections} All-NBA",
                    11, FontStyle.Normal, UITheme.TextSecondary);
                entry.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
                entry.lineSpacing = 1.2f;
            }
        }

        // ==================== HELPERS ====================

        private RectTransform ListArea(RectTransform card)
        {
            var list = B.Child(card, "List");
            B.Stretch(list);
            var lv = list.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 2; lv.padding = new RectOffset(10, 10, 34, 6);
            lv.childControlWidth = true; lv.childControlHeight = false; lv.childForceExpandWidth = true;
            return list.GetComponent<RectTransform>();
        }

        private void Empty(RectTransform scroll, string message)
        {
            var none = B.Text(scroll, "None", message, 13, FontStyle.Italic, UITheme.TextSecondary);
            none.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        }

        private static string PlayerName(string id) =>
            string.IsNullOrEmpty(id) ? "—"
            : GameManager.Instance?.PlayerDatabase?.GetPlayer(id)?.FullName ?? id;

        private static string TeamName(string id) =>
            string.IsNullOrEmpty(id) ? "—"
            : GameManager.Instance?.GetTeam(id)?.Name ?? id;
    }
}
