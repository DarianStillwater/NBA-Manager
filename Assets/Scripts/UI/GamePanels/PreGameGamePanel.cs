using System;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;

namespace NBAHeadCoach.UI.GamePanels
{
    public class PreGameGamePanel : IGamePanel
    {
        private readonly GameShell _shell;
        private CalendarEvent _gameEvent;

        public PreGameGamePanel(GameShell shell) { _shell = shell; }

        public void SetGameEvent(CalendarEvent gameEvent) { _gameEvent = gameEvent; }

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            if (_gameEvent == null) return;
            var gm = GameManager.Instance;
            var playerTeamId = gm?.PlayerTeamId;
            bool isHome = _gameEvent.HomeTeamId == playerTeamId;
            string oppId = isHome ? _gameEvent.AwayTeamId : _gameEvent.HomeTeamId;
            var playerTeam = gm?.GetTeam(playerTeamId);
            var oppTeam = gm?.GetTeam(oppId);

            var scroll = UIBuilder.FixedArea(parent);

            var title = UIBuilder.Text(scroll, "Title", "GAME DAY", 22, FontStyle.Bold, UITheme.AccentPrimary);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

            var date = UIBuilder.Text(scroll, "Date", _gameEvent.Date.ToString("dddd, MMMM d, yyyy"), 14, FontStyle.Normal, UITheme.TextSecondary);
            date.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            var matchCard = UIBuilder.Card(scroll, "MATCHUP", UITheme.AccentPrimary);
            var matchContent = UIBuilder.Text(matchCard, "Match", "", 16, FontStyle.Normal, Color.white);
            UIBuilder.FillCard(matchContent);
            matchContent.horizontalOverflow = HorizontalWrapMode.Wrap;
            matchContent.alignment = TextAnchor.MiddleCenter;

            string homeLabel = isHome ? $"{playerTeam?.City} {playerTeam?.Name} (YOU)" : $"{oppTeam?.City} {oppTeam?.Name}";
            string awayLabel = isHome ? $"{oppTeam?.City} {oppTeam?.Name}" : $"{playerTeam?.City} {playerTeam?.Name} (YOU)";
            matchContent.text = $"{awayLabel}\n\n  @  \n\n{homeLabel}\n\n{(isHome ? "HOME GAME" : "AWAY GAME")}";

            if (_gameEvent.IsNationalTV)
            {
                var tv = UIBuilder.Text(scroll, "TV", $"National TV: {_gameEvent.Broadcaster}", 12, FontStyle.Italic, UITheme.AccentSecondary);
                tv.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            var btnRow = UIBuilder.Child(scroll, "Buttons");
            btnRow.AddComponent<LayoutElement>().preferredHeight = 50;
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.padding = new RectOffset(40, 40, 4, 4);
            var br = btnRow.GetComponent<RectTransform>();

            var simGo = UIBuilder.Child(br, "SimBtn");
            simGo.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.Success, 0.5f);
            UIBuilder.ApplyOutline(simGo, UITheme.DarkenColor(UITheme.Success, 0.3f), 1f);
            var simBtn = simGo.AddComponent<Button>();
            var simText = UIBuilder.Text(simGo.GetComponent<RectTransform>(), "ST", "SIMULATE GAME", 14, FontStyle.Bold, Color.white);
            simText.alignment = TextAnchor.MiddleCenter; UIBuilder.Stretch(simText.gameObject);
            var capturedEvent = _gameEvent;
            simBtn.onClick.AddListener(() => SimulateAndShowResult(gm, playerTeam, oppTeam, isHome, capturedEvent));

            var playGo = UIBuilder.Child(br, "PlayBtn");
            playGo.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.AccentSecondary, 0.5f);
            UIBuilder.ApplyOutline(playGo, UITheme.DarkenColor(UITheme.AccentSecondary, 0.3f), 1f);
            var playBtn = playGo.AddComponent<Button>();
            var playText = UIBuilder.Text(playGo.GetComponent<RectTransform>(), "PT", "PLAY GAME", 14, FontStyle.Bold, Color.white);
            playText.alignment = TextAnchor.MiddleCenter; UIBuilder.Stretch(playText.gameObject);
            playBtn.onClick.AddListener(() => SimulateAndShowResult(gm, playerTeam, oppTeam, isHome, capturedEvent));
        }

        private void SimulateAndShowResult(GameManager gm, Team playerTeam, Team oppTeam, bool isHome, CalendarEvent gameEvent)
        {
            try
            {
                if (playerTeam == null || oppTeam == null) { Debug.LogError("[PreGame] Cannot simulate: null team"); return; }
                var homeTeam = isHome ? playerTeam : oppTeam;
                var awayTeam = isHome ? oppTeam : playerTeam;
                var simulator = new NBAHeadCoach.Core.Simulation.GameSimulator(gm.PlayerDatabase);
                var result = simulator.SimulateGame(homeTeam, awayTeam);
                simulator.RecordGameToPlayerStats(result, gameEvent.EventId, gameEvent.Date);
                gm?.SeasonController?.RecordGameResult(gameEvent, result.HomeScore, result.AwayScore);
                gm?.LeagueStats?.AddGameResult(result.BoxScore);
                gm?.LeagueStats?.Recalculate();
                Debug.Log($"[PreGame] Game simulated: {result.AwayScore}-{result.HomeScore}. Record: {playerTeam?.Wins}-{playerTeam?.Losses}");

                // Show post-game via shell
                var postGame = new PostGameGamePanel(_shell);
                postGame.SetResult(result, isHome);
                _shell?.RegisterPanel("PostGame", postGame);
                _shell?.ShowPanel("PostGame");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PreGame] Simulation failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
