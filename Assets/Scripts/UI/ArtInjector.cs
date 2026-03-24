using System;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;
using NBAHeadCoach.UI.Shell;
using NBAHeadCoach.UI.GamePanels;

namespace NBAHeadCoach.UI
{
    /// <summary>
    /// Registers game panels with GameShell and handles PreGame/PostGame flows.
    /// Panel content is now in individual files under GamePanels/.
    /// </summary>
    public class ArtInjector : MonoBehaviour
    {
        private GameShell _shell;

        private void Start()
        {
            _shell = GetComponent<GameShell>();
            if (_shell == null) return;

            _shell.RegisterPanel("Dashboard", new DashboardGamePanel());
            _shell.RegisterPanel("Roster", new RosterGamePanel(_shell));
            _shell.RegisterPanel("Schedule", new ScheduleGamePanel());
            _shell.RegisterPanel("Standings", new StandingsGamePanel());
            _shell.RegisterPanel("Inbox", new InboxGamePanel());
            _shell.RegisterPanel("Staff", new StaffGamePanel());
        }

        // ═══════════════════════════════════════════════════════
        //  PRE-GAME & POST-GAME (to be extracted later)
        // ═══════════════════════════════════════════════════════

        public void ShowPreGame(CalendarEvent gameEvent)
        {
            _shell?.RegisterPanel("PreGame", new LegacyPanelAdapter((p, t, c) => BuildPreGameContent(p, gameEvent)));
            _shell?.ShowPanel("PreGame");
        }

        private void BuildPreGameContent(RectTransform parent, CalendarEvent gameEvent)
        {
            var gm = GameManager.Instance;
            var playerTeamId = gm?.PlayerTeamId;
            bool isHome = gameEvent.HomeTeamId == playerTeamId;
            string oppId = isHome ? gameEvent.AwayTeamId : gameEvent.HomeTeamId;
            var playerTeam = gm?.GetTeam(playerTeamId);
            var oppTeam = gm?.GetTeam(oppId);

            var scroll = UIBuilder.ScrollArea(parent);

            var title = UIBuilder.Text(scroll, "Title", "GAME DAY", 22, FontStyle.Bold, UITheme.AccentPrimary);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

            var date = UIBuilder.Text(scroll, "Date", gameEvent.Date.ToString("dddd, MMMM d, yyyy"), 14, FontStyle.Normal, UITheme.TextSecondary);
            date.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            var matchCard = UIBuilder.Card(scroll, "MATCHUP", UITheme.AccentPrimary);
            var matchContent = UIBuilder.Text(matchCard, "Match", "", 16, FontStyle.Normal, Color.white);
            UIBuilder.FillCard(matchContent);
            matchContent.horizontalOverflow = HorizontalWrapMode.Wrap;
            matchContent.alignment = TextAnchor.MiddleCenter;

            string homeLabel = isHome ? $"{playerTeam?.City} {playerTeam?.Name} (YOU)" : $"{oppTeam?.City} {oppTeam?.Name}";
            string awayLabel = isHome ? $"{oppTeam?.City} {oppTeam?.Name}" : $"{playerTeam?.City} {playerTeam?.Name} (YOU)";
            matchContent.text = $"{awayLabel}\n\n  @  \n\n{homeLabel}\n\n{(isHome ? "HOME GAME" : "AWAY GAME")}";

            if (gameEvent.IsNationalTV)
            {
                var tv = UIBuilder.Text(scroll, "TV", $"National TV: {gameEvent.Broadcaster}", 12, FontStyle.Italic, UITheme.AccentSecondary);
                tv.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            var btnRow = UIBuilder.Child(scroll, "Buttons");
            btnRow.AddComponent<LayoutElement>().preferredHeight = 50;
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.padding = new RectOffset(40, 40, 4, 4);
            var br = btnRow.GetComponent<RectTransform>();

            // Simulate
            var simGo = UIBuilder.Child(br, "SimBtn");
            simGo.AddComponent<Image>().color = new Color32(30, 60, 40, 255);
            var simBtn = simGo.AddComponent<Button>();
            var simText = UIBuilder.Text(simGo.GetComponent<RectTransform>(), "ST", "SIMULATE GAME", 14, FontStyle.Bold, UITheme.Success);
            simText.alignment = TextAnchor.MiddleCenter; UIBuilder.Stretch(simText.gameObject);
            simBtn.onClick.AddListener(() =>
            {
                if (playerTeam == null || oppTeam == null) return;
                var homeTeam = isHome ? playerTeam : oppTeam;
                var awayTeam = isHome ? oppTeam : playerTeam;
                var simulator = new NBAHeadCoach.Core.Simulation.GameSimulator(gm.PlayerDatabase);
                var result = simulator.SimulateGame(homeTeam, awayTeam);
                gm?.SeasonController?.RecordGameResult(gameEvent, result.HomeScore, result.AwayScore);
                if (result.HomeScore > result.AwayScore) { homeTeam.Wins++; awayTeam.Losses++; }
                else { awayTeam.Wins++; homeTeam.Losses++; }
                ShowPostGame(result, isHome);
            });

            // Play
            var playGo = UIBuilder.Child(br, "PlayBtn");
            playGo.AddComponent<Image>().color = new Color32(40, 40, 70, 255);
            var playBtn = playGo.AddComponent<Button>();
            var playText = UIBuilder.Text(playGo.GetComponent<RectTransform>(), "PT", "PLAY GAME", 14, FontStyle.Bold, UITheme.AccentSecondary);
            playText.alignment = TextAnchor.MiddleCenter; UIBuilder.Stretch(playText.gameObject);
            playBtn.onClick.AddListener(() =>
            {
                gm?.MatchController?.PrepareMatch(gameEvent);
                gm?.MatchController?.StartInteractiveMatch();
            });
        }

        private void ShowPostGame(NBAHeadCoach.Core.Simulation.GameResult result, bool wasHome)
        {
            _shell?.RegisterPanel("PostGame", new LegacyPanelAdapter((p, t, c) => BuildPostGameContent(p, result, wasHome)));
            _shell?.ShowPanel("PostGame");
        }

        private void BuildPostGameContent(RectTransform parent, NBAHeadCoach.Core.Simulation.GameResult result, bool wasHome)
        {
            var scroll = UIBuilder.ScrollArea(parent);
            var bs = result.BoxScore;

            bool playerWon = (wasHome && result.HomeScore > result.AwayScore) || (!wasHome && result.AwayScore > result.HomeScore);
            string resultText = playerWon ? "VICTORY!" : "DEFEAT";
            Color resultColor = playerWon ? UITheme.Success : UITheme.Danger;

            var header = UIBuilder.Text(scroll, "Result", resultText, 28, FontStyle.Bold, resultColor);
            header.alignment = TextAnchor.MiddleCenter;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

            string homeName = bs?.HomeTeam?.City ?? result.HomeTeamId;
            string awayName = bs?.AwayTeam?.City ?? result.AwayTeamId;
            var score = UIBuilder.Text(scroll, "Score",
                $"{awayName}  {result.AwayScore}  —  {result.HomeScore}  {homeName}",
                20, FontStyle.Bold, Color.white);
            score.alignment = TextAnchor.MiddleCenter;
            score.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

            if (result.WentToOvertime)
            {
                var ot = UIBuilder.Text(scroll, "OT", $"({result.Quarters - 4}OT)", 14, FontStyle.Italic, UITheme.TextSecondary);
                ot.alignment = TextAnchor.MiddleCenter;
                ot.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            string playerTeamId = GameManager.Instance?.PlayerTeamId;
            var team = GameManager.Instance?.GetTeam(playerTeamId);
            if (team?.Roster != null && bs != null)
            {
                var boxCard = UIBuilder.Card(scroll, $"{team.City} {team.Name} BOX SCORE", UITheme.AccentPrimary);
                var headerRow = UIBuilder.TableRow(boxCard, 24, UITheme.FMCardHeaderBg);
                UIBuilder.TableCell(headerRow, "Player", 150, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.TableCell(headerRow, "MIN", 40, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.TableCell(headerRow, "PTS", 40, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.TableCell(headerRow, "REB", 40, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.TableCell(headerRow, "AST", 40, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.TableCell(headerRow, "FG", 55, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.TableCell(headerRow, "3PT", 50, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.TableCell(headerRow, "+/-", 40, FontStyle.Bold, UITheme.AccentPrimary);

                int rowIdx = 0;
                foreach (var player in team.Roster)
                {
                    var pStats = bs.GetPlayerStats(player.PlayerId);
                    if (pStats == null || pStats.Minutes == 0) continue;
                    var bgColor = rowIdx % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg;
                    var row = UIBuilder.TableRow(boxCard, 26, bgColor);
                    UIBuilder.TableCell(row, $"{player.FirstName[0]}. {player.LastName}", 150, FontStyle.Normal, Color.white);
                    UIBuilder.TableCell(row, pStats.Minutes.ToString(), 40, FontStyle.Normal, UITheme.TextSecondary);
                    UIBuilder.TableCell(row, pStats.Points.ToString(), 40, FontStyle.Bold, Color.white);
                    UIBuilder.TableCell(row, pStats.Rebounds.ToString(), 40, FontStyle.Normal, UITheme.TextSecondary);
                    UIBuilder.TableCell(row, pStats.Assists.ToString(), 40, FontStyle.Normal, UITheme.TextSecondary);
                    UIBuilder.TableCell(row, $"{pStats.FieldGoalsMade}-{pStats.FieldGoalAttempts}", 55, FontStyle.Normal, UITheme.TextSecondary);
                    UIBuilder.TableCell(row, $"{pStats.ThreePointMade}-{pStats.ThreePointAttempts}", 50, FontStyle.Normal, UITheme.TextSecondary);
                    string pmStr = pStats.PlusMinus >= 0 ? $"+{pStats.PlusMinus}" : pStats.PlusMinus.ToString();
                    UIBuilder.TableCell(row, pmStr, 40, FontStyle.Normal, pStats.PlusMinus >= 0 ? UITheme.Success : UITheme.Danger);
                    rowIdx++;
                }
            }

            var btnGo = UIBuilder.Child(scroll, "ContinueBtn");
            btnGo.AddComponent<LayoutElement>().preferredHeight = 44;
            btnGo.AddComponent<Image>().color = new Color32(30, 50, 70, 255);
            btnGo.AddComponent<Button>().onClick.AddListener(() => _shell?.ShowPanel("Dashboard"));
            var btnText = UIBuilder.Text(btnGo.GetComponent<RectTransform>(), "BT", "CONTINUE", 14, FontStyle.Bold, UITheme.AccentPrimary);
            btnText.alignment = TextAnchor.MiddleCenter; UIBuilder.Stretch(btnText.gameObject);
        }
    }
}
