using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Components;
using CalendarEvent = NBAHeadCoach.Core.Data.CalendarEvent;
using SeasonPhase = NBAHeadCoach.Core.Data.SeasonPhase;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Main dashboard panel showing team overview and quick actions
    /// </summary>
    public class DashboardPanel : BasePanel
    {
        [Header("Layout Containers")]
        public Transform HeroContainer;
        public Transform GridContainer;

        [Header("Team Info")]
        [SerializeField] private Text _teamNameText;
        [SerializeField] private Text _recordText;
        [SerializeField] private Text _standingsText;
        [SerializeField] private Text _coachNameText;
        [SerializeField] private Image _teamLogo;

        [Header("Date & Season")]
        [SerializeField] private Text _currentDateText;
        [SerializeField] private Text _seasonText;
        [SerializeField] private Text _phaseText;

        [Header("Next Game")]
        [SerializeField] private GameObject _nextGamePanel;
        [SerializeField] private Text _nextGameDateText;
        [SerializeField] private Text _nextGameOpponentText;
        [SerializeField] private Text _nextGameLocationText;
        [SerializeField] private Button _playNextGameButton;
        [SerializeField] private Button _simNextGameButton;

        [Header("Last Game")]
        [SerializeField] private GameObject _lastGamePanel;
        [SerializeField] private Text _lastGameResultText;
        [SerializeField] private Text _lastGameScoreText;
        [SerializeField] private Text _lastGameHighlightText;

        [Header("Quick Stats")]
        [SerializeField] private Text _winsText;
        [SerializeField] private Text _lossesText;
        [SerializeField] private Text _winStreakText;
        [SerializeField] private Text _homeRecordText;
        [SerializeField] private Text _awayRecordText;

        [Header("Roster Summary")]
        [SerializeField] private Text _rosterCountText;
        [SerializeField] private Text _injuredPlayersText;
        [SerializeField] private Text _starPlayerText;

        [Header("Financial Summary")]
        [SerializeField] private Text _salaryCapText;
        [SerializeField] private Text _capSpaceText;

        [Header("Inbox Preview")]
        [SerializeField] private Transform _inboxPreviewContainer;
        [SerializeField] private Text _unreadMessagesText;

        [Header("Action Buttons")]
        [SerializeField] private Button _advanceDayButton;
        [SerializeField] private Button _simToNextGameButton;
        [SerializeField] private Button _viewCalendarButton;
        [SerializeField] private Button _viewRosterButton;
        [SerializeField] private Button _saveGameButton;

        private List<DashboardWidget> _widgets = new List<DashboardWidget>();

        // Events
        public event Action OnAdvanceDayClicked;
        public event Action OnSimToNextGameClicked;
        public event Action<CalendarEvent> OnPlayGameClicked;
        public event Action<CalendarEvent> OnSimGameClicked;
        public event Action OnViewCalendarClicked;
        public event Action OnViewRosterClicked;
        public event Action OnSaveGameClicked;

        protected override void Awake()
        {
            base.Awake();
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (_advanceDayButton != null)
                _advanceDayButton.onClick.AddListener(() => OnAdvanceDayClicked?.Invoke());

            if (_simToNextGameButton != null)
                _simToNextGameButton.onClick.AddListener(() => OnSimToNextGameClicked?.Invoke());

            if (_playNextGameButton != null)
                _playNextGameButton.onClick.AddListener(OnPlayNextGame);

            if (_simNextGameButton != null)
                _simNextGameButton.onClick.AddListener(OnSimNextGame);

            if (_viewCalendarButton != null)
                _viewCalendarButton.onClick.AddListener(() => OnViewCalendarClicked?.Invoke());

            if (_viewRosterButton != null)
                _viewRosterButton.onClick.AddListener(() => OnViewRosterClicked?.Invoke());

            if (_saveGameButton != null)
                _saveGameButton.onClick.AddListener(() => OnSaveGameClicked?.Invoke());
        }

        protected override void OnShown()
        {
            base.OnShown();
            RefreshDashboard();
            RefreshAllWidgets();
        }

        /// <summary>
        /// Refresh all dashboard data from GameManager
        /// </summary>
        public void RefreshDashboard()
        {
            if (GameManager.Instance == null) return;

            RefreshTeamInfo();
            RefreshDateInfo();
            RefreshNextGame();
            RefreshLastGame();
            RefreshQuickStats();
            RefreshRosterSummary();
            RefreshFinancialSummary();
        }

        #region Data Refresh Methods

        private void RefreshTeamInfo()
        {
            var team = GameManager.Instance.GetPlayerTeam();
            if (team == null) return;

            if (_teamNameText != null)
                _teamNameText.text = $"{team.City} {team.Name}";

            if (_recordText != null)
                _recordText.text = $"{team.Wins}-{team.Losses}";

            if (_standingsText != null)
            {
                var seasonController = GameManager.Instance.SeasonController;
                if (seasonController != null)
                {
                    int rank = seasonController.GetConferenceRank(team.TeamId);
                    bool inPlayoffs = seasonController.IsPlayoffTeam(team.TeamId);
                    string playoffStatus = inPlayoffs ? "(Playoff Position)" : "";
                    _standingsText.text = $"#{rank} in {team.Conference} {playoffStatus}";
                }
            }

            if (_coachNameText != null)
            {
                var career = GameManager.Instance.Career;
                if (career != null)
                    _coachNameText.text = $"Coach {career.PersonName}";
            }
        }

        private void RefreshDateInfo()
        {
            var seasonController = GameManager.Instance.SeasonController;
            if (seasonController == null) return;

            if (_currentDateText != null)
                _currentDateText.text = seasonController.CurrentDate.ToString("dddd, MMMM d, yyyy");

            if (_seasonText != null)
                _seasonText.text = $"{seasonController.CurrentSeason}-{seasonController.CurrentSeason + 1} Season";

            if (_phaseText != null)
                _phaseText.text = GetPhaseDisplayName(seasonController.CurrentPhase);
        }

        private string GetPhaseDisplayName(SeasonPhase phase)
        {
            return phase switch
            {
                SeasonPhase.Preseason => "Preseason",
                SeasonPhase.RegularSeason => "Regular Season",
                SeasonPhase.AllStarBreak => "All-Star Break",
                SeasonPhase.Playoffs => "Playoffs",
                SeasonPhase.Draft => "Draft",
                SeasonPhase.FreeAgency => "Free Agency",
                SeasonPhase.SummerLeague => "Summer League",
                SeasonPhase.TrainingCamp => "Training Camp",
                SeasonPhase.Offseason => "Offseason",
                _ => phase.ToString()
            };
        }

        private void RefreshNextGame()
        {
            var seasonController = GameManager.Instance.SeasonController;
            var nextGame = seasonController?.GetNextGame();

            if (_nextGamePanel != null)
                _nextGamePanel.SetActive(nextGame != null);

            if (nextGame == null) return;

            // Get opponent
            string opponentId = nextGame.IsHomeGame ? nextGame.AwayTeamId : nextGame.HomeTeamId;
            var opponent = GameManager.Instance.GetTeam(opponentId);

            if (_nextGameDateText != null)
                _nextGameDateText.text = nextGame.Date.ToString("MMM d");

            if (_nextGameOpponentText != null)
                _nextGameOpponentText.text = $"{opponent?.City ?? ""} {opponent?.Name ?? opponentId}";

            if (_nextGameLocationText != null)
                _nextGameLocationText.text = nextGame.IsHomeGame ? "HOME" : "AWAY";

            // Show play buttons only if it's today's game
            bool isToday = nextGame.Date.Date == seasonController.CurrentDate.Date;
            if (_playNextGameButton != null)
                _playNextGameButton.gameObject.SetActive(isToday);
            if (_simNextGameButton != null)
                _simNextGameButton.gameObject.SetActive(isToday);
        }

        private void RefreshLastGame()
        {
            var seasonController = GameManager.Instance.SeasonController;
            var completedGames = seasonController?.CompletedGames;

            if (completedGames == null || completedGames.Count == 0)
            {
                if (_lastGamePanel != null)
                    _lastGamePanel.SetActive(false);
                return;
            }

            var lastGame = completedGames.OrderByDescending(g => g.Date).First();
            string playerTeamId = GameManager.Instance.PlayerTeamId;

            bool isHome = lastGame.HomeTeamId == playerTeamId;
            int playerScore = isHome ? lastGame.HomeScore : lastGame.AwayScore;
            int opponentScore = isHome ? lastGame.AwayScore : lastGame.HomeScore;
            bool won = playerScore > opponentScore;

            string opponentId = isHome ? lastGame.AwayTeamId : lastGame.HomeTeamId;
            var opponent = GameManager.Instance.GetTeam(opponentId);

            if (_lastGamePanel != null)
                _lastGamePanel.SetActive(true);

            if (_lastGameResultText != null)
            {
                _lastGameResultText.text = won ? "WIN" : "LOSS";
                _lastGameResultText.color = won ? Color.green : Color.red;
            }

            if (_lastGameScoreText != null)
                _lastGameScoreText.text = $"{playerScore}-{opponentScore} vs {opponent?.Name ?? opponentId}";

            // TODO: Add highlight player stats
            if (_lastGameHighlightText != null)
                _lastGameHighlightText.text = "";
        }

        private void RefreshQuickStats()
        {
            var team = GameManager.Instance.GetPlayerTeam();
            if (team == null) return;

            if (_winsText != null)
                _winsText.text = team.Wins.ToString();

            if (_lossesText != null)
                _lossesText.text = team.Losses.ToString();

            var standings = GameManager.Instance.SeasonController?.GetTeamStandings(team.TeamId);
            if (standings != null)
            {
                if (_winStreakText != null)
                {
                    string streakText = standings.Streak > 0 ? $"W{standings.Streak}" :
                                        standings.Streak < 0 ? $"L{Math.Abs(standings.Streak)}" : "-";
                    _winStreakText.text = streakText;
                }

                if (_homeRecordText != null)
                    _homeRecordText.text = $"{standings.HomeWins}-{standings.HomeLosses}";

                if (_awayRecordText != null)
                    _awayRecordText.text = $"{standings.AwayWins}-{standings.AwayLosses}";
            }
        }

        private void RefreshRosterSummary()
        {
            var team = GameManager.Instance.GetPlayerTeam();
            if (team?.Roster == null) return;

            if (_rosterCountText != null)
                _rosterCountText.text = $"{team.Roster.Count} Players";

            if (_injuredPlayersText != null)
            {
                int injured = team.Roster.Count(p => p.IsInjured);
                _injuredPlayersText.text = injured > 0 ? $"{injured} Injured" : "All Healthy";
                _injuredPlayersText.color = injured > 0 ? Color.red : Color.green;
            }

            if (_starPlayerText != null)
            {
                var starPlayer = team.Roster.OrderByDescending(p => p.Overall).FirstOrDefault();
                if (starPlayer != null)
                    _starPlayerText.text = $"Star: {starPlayer.FirstName} {starPlayer.LastName} ({starPlayer.Overall})";
            }
        }

        private void RefreshFinancialSummary()
        {
            var team = GameManager.Instance.GetPlayerTeam();
            if (team?.Roster == null) return;

            long totalSalary = team.Roster.Sum(p => p.AnnualSalary);
            long salaryCap = 136000000; // 2024-25 cap

            if (_salaryCapText != null)
                _salaryCapText.text = $"Payroll: ${totalSalary / 1000000f:F1}M";

            if (_capSpaceText != null)
            {
                long capSpace = salaryCap - totalSalary;
                _capSpaceText.text = $"Cap Space: ${capSpace / 1000000f:F1}M";
                _capSpaceText.color = capSpace >= 0 ? Color.green : Color.red;
            }
        }

        #endregion

        #region Widget Management

        public void RegisterWidget(DashboardWidget widget, bool isHero = false)
        {
            _widgets.Add(widget);
            widget.transform.SetParent(isHero ? HeroContainer : GridContainer, false);

            // Allow layout groups to control size for grid, but force stretch for Hero
            if (isHero)
            {
                RectTransform rt = widget.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        public void RefreshAllWidgets()
        {
            foreach (var widget in _widgets)
            {
                widget.Refresh();
            }
        }

        #endregion

        #region Button Handlers

        private void OnPlayNextGame()
        {
            var nextGame = GameManager.Instance?.SeasonController?.GetTodaysGame();
            if (nextGame != null)
            {
                OnPlayGameClicked?.Invoke(nextGame);
            }
        }

        private void OnSimNextGame()
        {
            var nextGame = GameManager.Instance?.SeasonController?.GetTodaysGame();
            if (nextGame != null)
            {
                OnSimGameClicked?.Invoke(nextGame);
            }
        }

        #endregion
    }
}
