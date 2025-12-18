using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using PlayerGameStats = NBAHeadCoach.Core.Simulation.PlayerGameStats;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Post-game panel showing box score, results, and headlines
    /// </summary>
    public class PostGamePanel : BasePanel
    {
        [Header("Result Header")]
        [SerializeField] private Text _resultText;
        [SerializeField] private Text _finalScoreText;
        [SerializeField] private Text _gameInfoText;
        [SerializeField] private Image _resultBackground;
        [SerializeField] private Color _winColor = new Color(0.2f, 0.5f, 0.2f);
        [SerializeField] private Color _lossColor = new Color(0.5f, 0.2f, 0.2f);

        [Header("Team Scores")]
        [SerializeField] private Text _homeTeamNameText;
        [SerializeField] private Text _homeScoreText;
        [SerializeField] private Text _awayTeamNameText;
        [SerializeField] private Text _awayScoreText;

        [Header("Quarter Scores")]
        [SerializeField] private Transform _quarterScoresContainer;
        [SerializeField] private Text[] _homeQuarterTexts;
        [SerializeField] private Text[] _awayQuarterTexts;

        [Header("Tabs")]
        [SerializeField] private Button _boxScoreTabButton;
        [SerializeField] private Button _highlightsTabButton;
        [SerializeField] private Button _standingsTabButton;
        [SerializeField] private GameObject _boxScorePanel;
        [SerializeField] private GameObject _highlightsPanel;
        [SerializeField] private GameObject _standingsPanel;

        [Header("Box Score - Player Team")]
        [SerializeField] private Transform _playerTeamBoxScoreContainer;
        [SerializeField] private Text _playerTeamNameHeader;

        [Header("Box Score - Opponent")]
        [SerializeField] private Transform _opponentBoxScoreContainer;
        [SerializeField] private Text _opponentTeamNameHeader;

        [Header("Player of the Game")]
        [SerializeField] private GameObject _potgPanel;
        [SerializeField] private Text _potgNameText;
        [SerializeField] private Text _potgStatsText;
        [SerializeField] private Image _potgImage;

        [Header("Headlines")]
        [SerializeField] private Transform _headlinesContainer;

        [Header("Updated Standings")]
        [SerializeField] private Text _newRecordText;
        [SerializeField] private Text _conferenceRankText;
        [SerializeField] private Text _playoffStatusText;
        [SerializeField] private Text _streakText;

        [Header("Buttons")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _viewFullBoxScoreButton;

        // State
        private BoxScore _boxScore;
        private PostGameSummary _summary;
        private PostGameTab _currentTab = PostGameTab.BoxScore;
        private string _playerTeamId;
        private bool _playerWon;

        // Events
        public event Action OnContinueRequested;

        protected override void Awake()
        {
            base.Awake();
            SetupTabs();
            SetupButtons();
        }

        private void SetupTabs()
        {
            if (_boxScoreTabButton != null)
                _boxScoreTabButton.onClick.AddListener(() => ShowTab(PostGameTab.BoxScore));

            if (_highlightsTabButton != null)
                _highlightsTabButton.onClick.AddListener(() => ShowTab(PostGameTab.Highlights));

            if (_standingsTabButton != null)
                _standingsTabButton.onClick.AddListener(() => ShowTab(PostGameTab.Standings));
        }

        private void SetupButtons()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(() => OnContinueRequested?.Invoke());
        }

        protected override void OnShown()
        {
            base.OnShown();
            ShowTab(PostGameTab.BoxScore);
        }

        /// <summary>
        /// Initialize post-game panel with match result
        /// </summary>
        public void Initialize(BoxScore boxScore, PostGameSummary summary)
        {
            _boxScore = boxScore;
            _summary = summary;
            _playerTeamId = GameManager.Instance?.PlayerTeamId ?? "";
            _playerWon = summary?.PlayerWon ?? false;

            RefreshUI();
        }

        private void RefreshUI()
        {
            RefreshResultHeader();
            RefreshTeamScores();
            RefreshQuarterScores();
            RefreshBoxScore();
            RefreshPlayerOfTheGame();
            RefreshHeadlines();
            RefreshStandingsUpdate();
        }

        #region Tab Management

        private void ShowTab(PostGameTab tab)
        {
            _currentTab = tab;

            if (_boxScorePanel != null)
                _boxScorePanel.SetActive(tab == PostGameTab.BoxScore);

            if (_highlightsPanel != null)
                _highlightsPanel.SetActive(tab == PostGameTab.Highlights);

            if (_standingsPanel != null)
                _standingsPanel.SetActive(tab == PostGameTab.Standings);

            UpdateTabButtonVisuals();
        }

        private void UpdateTabButtonVisuals()
        {
            SetTabButtonActive(_boxScoreTabButton, _currentTab == PostGameTab.BoxScore);
            SetTabButtonActive(_highlightsTabButton, _currentTab == PostGameTab.Highlights);
            SetTabButtonActive(_standingsTabButton, _currentTab == PostGameTab.Standings);
        }

        private void SetTabButtonActive(Button button, bool active)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = active ? new Color(0.3f, 0.3f, 0.5f) : new Color(0.2f, 0.2f, 0.3f);
            button.colors = colors;
        }

        #endregion

        #region Result Header

        private void RefreshResultHeader()
        {
            if (_resultText != null)
            {
                _resultText.text = _playerWon ? "VICTORY!" : "DEFEAT";
                _resultText.color = _playerWon ? Color.green : Color.red;
            }

            if (_resultBackground != null)
            {
                _resultBackground.color = _playerWon ? _winColor : _lossColor;
            }

            if (_finalScoreText != null && _boxScore != null)
            {
                _finalScoreText.text = _summary?.FinalScore ?? "";
            }

            if (_gameInfoText != null && _summary?.GameEvent != null)
            {
                string location = _summary.GameEvent.IsHomeGame ? "Home" : "Away";
                _gameInfoText.text = $"Game #{_summary.GameEvent.GameNumber} | {location}";
            }
        }

        #endregion

        #region Team Scores

        private void RefreshTeamScores()
        {
            if (_boxScore == null) return;

            if (_homeTeamNameText != null)
                _homeTeamNameText.text = _boxScore.HomeTeam?.Name ?? "Home";

            if (_homeScoreText != null)
                _homeScoreText.text = _boxScore.HomeScore.ToString();

            if (_awayTeamNameText != null)
                _awayTeamNameText.text = _boxScore.AwayTeam?.Name ?? "Away";

            if (_awayScoreText != null)
                _awayScoreText.text = _boxScore.AwayScore.ToString();
        }

        private void RefreshQuarterScores()
        {
            if (_boxScore == null) return;

            // Quarter scores for home team
            if (_homeQuarterTexts != null)
            {
                for (int i = 0; i < _homeQuarterTexts.Length; i++)
                {
                    if (_homeQuarterTexts[i] != null)
                    {
                        if (i < _boxScore.QuarterScoresHome.Count)
                            _homeQuarterTexts[i].text = _boxScore.QuarterScoresHome[i].ToString();
                        else
                            _homeQuarterTexts[i].text = "-";
                    }
                }
            }

            // Quarter scores for away team
            if (_awayQuarterTexts != null)
            {
                for (int i = 0; i < _awayQuarterTexts.Length; i++)
                {
                    if (_awayQuarterTexts[i] != null)
                    {
                        if (i < _boxScore.QuarterScoresAway.Count)
                            _awayQuarterTexts[i].text = _boxScore.QuarterScoresAway[i].ToString();
                        else
                            _awayQuarterTexts[i].text = "-";
                    }
                }
            }
        }

        #endregion

        #region Box Score

        private void RefreshBoxScore()
        {
            if (_boxScore == null) return;

            // Determine which team is player's
            bool playerIsHome = _boxScore.HomeTeam?.TeamId == _playerTeamId;
            var playerTeamStats = playerIsHome ? _boxScore.HomePlayerStats : _boxScore.AwayPlayerStats;
            var opponentStats = playerIsHome ? _boxScore.AwayPlayerStats : _boxScore.HomePlayerStats;
            var playerTeam = playerIsHome ? _boxScore.HomeTeam : _boxScore.AwayTeam;
            var opponentTeam = playerIsHome ? _boxScore.AwayTeam : _boxScore.HomeTeam;

            // Player team header
            if (_playerTeamNameHeader != null)
                _playerTeamNameHeader.text = $"{playerTeam?.City ?? ""} {playerTeam?.Name ?? "Your Team"}";

            // Opponent header
            if (_opponentTeamNameHeader != null)
                _opponentTeamNameHeader.text = $"{opponentTeam?.City ?? ""} {opponentTeam?.Name ?? "Opponent"}";

            // Create box score rows
            CreateBoxScoreTable(_playerTeamBoxScoreContainer, playerTeamStats);
            CreateBoxScoreTable(_opponentBoxScoreContainer, opponentStats);
        }

        private void CreateBoxScoreTable(Transform container, List<PlayerGameStats> playerStats)
        {
            if (container == null) return;

            // Clear existing
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }

            // Create header row
            CreateBoxScoreHeaderRow(container);

            if (playerStats == null) return;

            // Sort by minutes played
            var sortedStats = playerStats.OrderByDescending(s => s.Minutes).ToList();

            foreach (var stats in sortedStats)
            {
                CreateBoxScoreRow(container, stats);
            }
        }

        private void CreateBoxScoreHeaderRow(Transform container)
        {
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(container, false);

            var layout = headerGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;

            var layoutElement = headerGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 25;
            layoutElement.flexibleWidth = 1;

            // Header columns
            AddBoxScoreColumn(headerGO.transform, "Player", 150, true, TextAnchor.MiddleLeft);
            AddBoxScoreColumn(headerGO.transform, "MIN", 40, true);
            AddBoxScoreColumn(headerGO.transform, "PTS", 40, true);
            AddBoxScoreColumn(headerGO.transform, "REB", 40, true);
            AddBoxScoreColumn(headerGO.transform, "AST", 40, true);
            AddBoxScoreColumn(headerGO.transform, "STL", 35, true);
            AddBoxScoreColumn(headerGO.transform, "BLK", 35, true);
            AddBoxScoreColumn(headerGO.transform, "FG", 55, true);
            AddBoxScoreColumn(headerGO.transform, "3PT", 50, true);
            AddBoxScoreColumn(headerGO.transform, "FT", 50, true);
        }

        private void CreateBoxScoreRow(Transform container, PlayerGameStats stats)
        {
            var rowGO = new GameObject($"Row_{stats.PlayerId}");
            rowGO.transform.SetParent(container, false);

            var bg = rowGO.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f);

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(5, 5, 2, 2);

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 25;
            layoutElement.flexibleWidth = 1;

            // Data columns
            AddBoxScoreColumn(rowGO.transform, stats.PlayerName ?? "Unknown", 150, false, TextAnchor.MiddleLeft);
            AddBoxScoreColumn(rowGO.transform, stats.Minutes.ToString(), 40);
            AddBoxScoreColumn(rowGO.transform, stats.Points.ToString(), 40);
            AddBoxScoreColumn(rowGO.transform, stats.Rebounds.ToString(), 40);
            AddBoxScoreColumn(rowGO.transform, stats.Assists.ToString(), 40);
            AddBoxScoreColumn(rowGO.transform, stats.Steals.ToString(), 35);
            AddBoxScoreColumn(rowGO.transform, stats.Blocks.ToString(), 35);
            AddBoxScoreColumn(rowGO.transform, $"{stats.FieldGoalsMade}-{stats.FieldGoalsAttempted}", 55);
            AddBoxScoreColumn(rowGO.transform, $"{stats.ThreePointersMade}-{stats.ThreePointersAttempted}", 50);
            AddBoxScoreColumn(rowGO.transform, $"{stats.FreeThrowsMade}-{stats.FreeThrowsAttempted}", 50);
        }

        private void AddBoxScoreColumn(Transform parent, string text, float width, bool isHeader = false, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var colGO = new GameObject("Col");
            colGO.transform.SetParent(parent, false);

            var colText = colGO.AddComponent<Text>();
            colText.text = text;
            colText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            colText.fontSize = isHeader ? 11 : 12;
            colText.fontStyle = isHeader ? FontStyle.Bold : FontStyle.Normal;
            colText.color = isHeader ? new Color(0.7f, 0.7f, 0.7f) : Color.white;
            colText.alignment = alignment;

            var layout = colGO.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
        }

        #endregion

        #region Player of the Game

        private void RefreshPlayerOfTheGame()
        {
            var potg = _summary?.PlayerOfTheGame;

            if (_potgPanel != null)
                _potgPanel.SetActive(potg != null);

            if (potg == null) return;

            if (_potgNameText != null)
                _potgNameText.text = potg.PlayerName ?? "Unknown";

            if (_potgStatsText != null)
            {
                _potgStatsText.text = $"{potg.Points} PTS | {potg.Rebounds} REB | {potg.Assists} AST";
            }
        }

        #endregion

        #region Headlines

        private void RefreshHeadlines()
        {
            if (_headlinesContainer == null || _summary?.Headlines == null) return;

            // Clear existing
            foreach (Transform child in _headlinesContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var headline in _summary.Headlines)
            {
                CreateHeadlineEntry(headline);
            }

            // Add some generated headlines based on game
            AddGeneratedHeadlines();
        }

        private void CreateHeadlineEntry(string headline)
        {
            var headlineGO = new GameObject("Headline");
            headlineGO.transform.SetParent(_headlinesContainer, false);

            var layout = headlineGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 30;
            layout.flexibleWidth = 1;

            var text = headlineGO.AddComponent<Text>();
            text.text = $"- {headline}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
        }

        private void AddGeneratedHeadlines()
        {
            if (_boxScore == null) return;

            // Find notable performances
            var allStats = new List<PlayerGameStats>();
            if (_boxScore.HomePlayerStats != null) allStats.AddRange(_boxScore.HomePlayerStats);
            if (_boxScore.AwayPlayerStats != null) allStats.AddRange(_boxScore.AwayPlayerStats);

            // 30+ point scorer
            var thirtyPointScorer = allStats.FirstOrDefault(s => s.Points >= 30);
            if (thirtyPointScorer != null)
            {
                CreateHeadlineEntry($"{thirtyPointScorer.PlayerName} erupts for {thirtyPointScorer.Points} points");
            }

            // Triple-double
            var tripleDouble = allStats.FirstOrDefault(s =>
                (s.Points >= 10 ? 1 : 0) + (s.Rebounds >= 10 ? 1 : 0) + (s.Assists >= 10 ? 1 : 0) >= 3);
            if (tripleDouble != null)
            {
                CreateHeadlineEntry($"{tripleDouble.PlayerName} records a triple-double");
            }

            // Double-double
            if (tripleDouble == null)
            {
                var doubleDouble = allStats.FirstOrDefault(s =>
                    (s.Points >= 10 ? 1 : 0) + (s.Rebounds >= 10 ? 1 : 0) + (s.Assists >= 10 ? 1 : 0) >= 2);
                if (doubleDouble != null)
                {
                    CreateHeadlineEntry($"{doubleDouble.PlayerName} posts a double-double");
                }
            }

            // Close game
            int scoreDiff = Math.Abs(_boxScore.HomeScore - _boxScore.AwayScore);
            if (scoreDiff <= 5)
            {
                CreateHeadlineEntry("Nail-biter decided in final minutes");
            }

            // Overtime
            if (_boxScore.WentToOvertime)
            {
                string otPeriods = _boxScore.OvertimePeriods > 1 ? $"{_boxScore.OvertimePeriods}OT" : "OT";
                CreateHeadlineEntry($"Game goes to {otPeriods}");
            }
        }

        #endregion

        #region Standings Update

        private void RefreshStandingsUpdate()
        {
            var team = GameManager.Instance?.GetPlayerTeam();
            var seasonController = GameManager.Instance?.SeasonController;

            if (team == null || seasonController == null) return;

            if (_newRecordText != null)
                _newRecordText.text = $"New Record: {team.Wins}-{team.Losses}";

            if (_conferenceRankText != null)
            {
                int rank = seasonController.GetConferenceRank(team.TeamId);
                _conferenceRankText.text = $"Conference Rank: #{rank} in {team.Conference}";
            }

            if (_playoffStatusText != null)
            {
                bool inPlayoffs = seasonController.IsPlayoffTeam(team.TeamId);
                _playoffStatusText.text = inPlayoffs ? "Playoff Position: IN" : "Playoff Position: OUT";
                _playoffStatusText.color = inPlayoffs ? Color.green : Color.red;
            }

            if (_streakText != null)
            {
                var standings = seasonController.GetTeamStandings(team.TeamId);
                if (standings != null)
                {
                    // Update streak based on this game's result
                    if (_playerWon)
                    {
                        if (standings.Streak >= 0)
                            standings.Streak++;
                        else
                            standings.Streak = 1;
                    }
                    else
                    {
                        if (standings.Streak <= 0)
                            standings.Streak--;
                        else
                            standings.Streak = -1;
                    }

                    string streakText = standings.Streak > 0 ? $"W{standings.Streak}" : $"L{Math.Abs(standings.Streak)}";
                    _streakText.text = $"Current Streak: {streakText}";
                    _streakText.color = standings.Streak > 0 ? Color.green : Color.red;
                }
            }
        }

        #endregion

        private enum PostGameTab
        {
            BoxScore,
            Highlights,
            Standings
        }
    }
}
