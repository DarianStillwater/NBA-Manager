using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.UI.Panels;
using CalendarEvent = NBAHeadCoach.Core.Data.CalendarEvent;
using GameState = NBAHeadCoach.Core.GameState;

namespace NBAHeadCoach.UI
{
    /// <summary>
    /// Controller for the main Game scene
    /// Manages dashboard, navigation, and game flow
    /// </summary>
    public class GameSceneController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _contentParent;
        [SerializeField] private Text _headerText;
        [SerializeField] private Text _dateText;
        [SerializeField] private Text _recordText;

        [Header("Navigation Buttons")]
        [SerializeField] private Button _dashboardButton;
        [SerializeField] private Button _rosterButton;
        [SerializeField] private Button _scheduleButton;
        [SerializeField] private Button _standingsButton;
        [SerializeField] private Button _inboxButton;
        [SerializeField] private Button _staffButton;

        [Header("Action Buttons")]
        [SerializeField] private Button _advanceDayButton;
        [SerializeField] private Button _simToNextGameButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _menuButton;

        [Header("Panels")]
        [SerializeField] private GameObject _dashboardPanel;
        [SerializeField] private GameObject _rosterPanel;
        [SerializeField] private GameObject _schedulePanel;
        [SerializeField] private GameObject _standingsPanel;
        [SerializeField] private GameObject _inboxPanel;
        [SerializeField] private GameObject _preGamePanel;
        [SerializeField] private GameObject _postGamePanel;
        [SerializeField] private GameObject _staffPanel;

        [Header("Panel Components")]
        [SerializeField] private DashboardPanel _dashboardPanelComponent;
        [SerializeField] private RosterPanel _rosterPanelComponent;
        [SerializeField] private CalendarPanel _calendarPanelComponent;
        [SerializeField] private StandingsPanel _standingsPanelComponent;
        [SerializeField] private InboxPanel _inboxPanelComponent;
        [SerializeField] private PreGamePanel _preGamePanelComponent;
        [SerializeField] private PostGamePanel _postGamePanelComponent;
        [SerializeField] private StaffPanel _staffPanelComponent;

        private GameManager _gameManager;

        // Panel registry for unified navigation
        private Dictionary<string, GameObject> _panelRegistry = new Dictionary<string, GameObject>();
        private Dictionary<string, BasePanel> _panelComponents = new Dictionary<string, BasePanel>();
        private string _currentPanelId;

        private void Start()
        {
            _gameManager = GameManager.Instance;

            if (_gameManager == null)
            {
                Debug.LogError("[GameSceneController] GameManager not found! Make sure to start from the Boot scene.");
                // Show error message to user
                if (_headerText != null)
                {
                    _headerText.text = "ERROR: Start from Boot scene";
                }
                return;
            }

            Debug.Log($"[GameSceneController] GameManager found. State: {_gameManager.CurrentState}, Team: {_gameManager.PlayerTeamId}");

            // Subscribe to events
            _gameManager.OnStateChanged += OnGameStateChanged;
            _gameManager.OnDayAdvanced += OnDayAdvanced;

            if (_gameManager.SeasonController != null)
            {
                _gameManager.SeasonController.OnGameDay += OnGameDay;
            }

            // Setup UI
            RegisterPanels();
            SetupButtonListeners();
            SetupPanelEvents();
            RefreshUI();

            // Show dashboard by default
            ShowPanel("Dashboard");
        }

        private void RegisterPanels()
        {
            RegisterPanel("Dashboard", _dashboardPanel, _dashboardPanelComponent);
            RegisterPanel("Roster", _rosterPanel, _rosterPanelComponent);
            RegisterPanel("Schedule", _schedulePanel, _calendarPanelComponent);
            RegisterPanel("Standings", _standingsPanel, _standingsPanelComponent);
            RegisterPanel("Inbox", _inboxPanel, _inboxPanelComponent);
            RegisterPanel("Staff", _staffPanel, _staffPanelComponent);
            RegisterPanel("PreGame", _preGamePanel, _preGamePanelComponent);
            RegisterPanel("PostGame", _postGamePanel, _postGamePanelComponent);
        }

        private void RegisterPanel(string id, GameObject panelObject, BasePanel component)
        {
            if (panelObject != null)
            {
                _panelRegistry[id] = panelObject;
            }
            if (component != null)
            {
                _panelComponents[id] = component;
            }
        }

        private void SetupPanelEvents()
        {
            // Dashboard events
            if (_dashboardPanelComponent != null)
            {
                _dashboardPanelComponent.OnAdvanceDayClicked += OnAdvanceDayClicked;
                _dashboardPanelComponent.OnSimToNextGameClicked += OnSimToNextGameClicked;
                _dashboardPanelComponent.OnPlayGameClicked += OnPlayGameFromDashboard;
                _dashboardPanelComponent.OnSimGameClicked += OnSimGameFromDashboard;
                _dashboardPanelComponent.OnViewCalendarClicked += ShowSchedule;
                _dashboardPanelComponent.OnViewRosterClicked += ShowRoster;
                _dashboardPanelComponent.OnSaveGameClicked += OnSaveClicked;
            }

            // Calendar events
            if (_calendarPanelComponent != null)
            {
                _calendarPanelComponent.OnPlayGameRequested += OnPlayGameFromCalendar;
                _calendarPanelComponent.OnSimGameRequested += OnSimGameFromCalendar;
            }

            // Pre-game events
            if (_preGamePanelComponent != null)
            {
                _preGamePanelComponent.OnPlayGameRequested += OnPlayGameFromPreGame;
                _preGamePanelComponent.OnSimGameRequested += OnSimGameFromPreGame;
                _preGamePanelComponent.OnBackRequested += ShowDashboard;
            }

            // Post-game events
            if (_postGamePanelComponent != null)
            {
                _postGamePanelComponent.OnContinueRequested += OnContinueFromPostGame;
            }

            // Roster events
            if (_rosterPanelComponent != null)
            {
                _rosterPanelComponent.OnTradePlayer += OnTradePlayerRequested;
            }
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged -= OnGameStateChanged;
                _gameManager.OnDayAdvanced -= OnDayAdvanced;

                if (_gameManager.SeasonController != null)
                {
                    _gameManager.SeasonController.OnGameDay -= OnGameDay;
                }
            }
        }

        private void SetupButtonListeners()
        {
            // Navigation
            AddButtonListener(_dashboardButton, ShowDashboard);
            AddButtonListener(_rosterButton, ShowRoster);
            AddButtonListener(_scheduleButton, ShowSchedule);
            AddButtonListener(_standingsButton, ShowStandings);
            AddButtonListener(_inboxButton, ShowInbox);
            AddButtonListener(_staffButton, ShowStaff);

            // Actions
            AddButtonListener(_advanceDayButton, OnAdvanceDayClicked);
            AddButtonListener(_simToNextGameButton, OnSimToNextGameClicked);
            AddButtonListener(_saveButton, OnSaveClicked);
            AddButtonListener(_menuButton, OnMenuClicked);
        }

        private void AddButtonListener(Button button, Action action)
        {
            if (button != null)
                button.onClick.AddListener(() => action());
        }

        #region UI Refresh

        private void RefreshUI()
        {
            if (_gameManager == null) return;

            // Update header
            if (_headerText != null && _gameManager.Career != null)
            {
                var team = _gameManager.GetPlayerTeam();
                _headerText.text = $"{_gameManager.Career.FullName} - {team?.Name ?? "Free Agent"}";
            }

            // Update date
            if (_dateText != null)
            {
                _dateText.text = _gameManager.CurrentDate.ToString("MMM dd, yyyy");
            }

            // Update record
            if (_recordText != null)
            {
                var team = _gameManager.GetPlayerTeam();
                if (team != null)
                {
                    _recordText.text = $"Record: {team.Wins}-{team.Losses}";
                }
            }
        }

        #endregion

        #region Panel Navigation

        /// <summary>
        /// Shows a panel by ID using the unified panel registry.
        /// </summary>
        public void ShowPanel(string panelId)
        {
            HideAllPanels();

            if (_panelRegistry.TryGetValue(panelId, out var panel))
            {
                panel.SetActive(true);
                _currentPanelId = panelId;

                // Call Show() on component if available (handles CanvasGroup)
                if (_panelComponents.TryGetValue(panelId, out var component))
                {
                    component.Show();
                }

                // Panel-specific refresh logic
                RefreshPanel(panelId);
            }
            else
            {
                Debug.LogWarning($"[GameSceneController] Panel '{panelId}' not found in registry");
            }
        }

        /// <summary>
        /// Gets the currently active panel ID.
        /// </summary>
        public string CurrentPanelId => _currentPanelId;

        private void HideAllPanels()
        {
            foreach (var kvp in _panelRegistry)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.SetActive(false);
                }

                // Call Hide() on component if available
                if (_panelComponents.TryGetValue(kvp.Key, out var component))
                {
                    component.Hide();
                }
            }
        }

        private void RefreshPanel(string panelId)
        {
            switch (panelId)
            {
                case "Dashboard":
                    _dashboardPanelComponent?.RefreshDashboard();
                    break;
                case "Roster":
                    _rosterPanelComponent?.RefreshList();
                    break;
                case "Staff":
                    _staffPanelComponent?.RefreshStaffList();
                    break;
                // Schedule, Standings, Inbox refresh on show via BasePanel.OnShown()
            }
        }

        // Legacy methods for backward compatibility
        private void ShowDashboard() => ShowPanel("Dashboard");
        private void ShowRoster() => ShowPanel("Roster");
        private void ShowSchedule() => ShowPanel("Schedule");
        private void ShowStandings() => ShowPanel("Standings");
        private void ShowInbox() => ShowPanel("Inbox");
        private void ShowStaff() => ShowPanel("Staff");
        private void ShowPreGame() => ShowPanel("PreGame");
        private void ShowPostGame() => ShowPanel("PostGame");

        #endregion

        #region Action Handlers

        private void OnAdvanceDayClicked()
        {
            if (_gameManager != null)
            {
                _gameManager.AdvanceDay();
                RefreshUI();
            }
        }

        private void OnSimToNextGameClicked()
        {
            if (_gameManager != null)
            {
                _gameManager.AdvanceToNextEvent();
                RefreshUI();
            }
        }

        private void OnSaveClicked()
        {
            if (_gameManager != null)
            {
                var saveData = _gameManager.CreateSaveData();
                saveData.SaveName = $"Manual Save - {DateTime.Now:MMM dd HH:mm}";
                _gameManager.SaveLoad.SaveGame(saveData, _gameManager.SaveLoad.GetNextSaveSlotName());
                Debug.Log("[GameSceneController] Game saved!");
            }
        }

        private void OnMenuClicked()
        {
            if (_gameManager != null)
            {
                _gameManager.ReturnToMainMenu();
            }
        }

        #endregion

        #region Event Handlers

        private void OnGameStateChanged(object sender, GameStateChangedEventArgs e)
        {
            Debug.Log($"[GameSceneController] State changed: {e.PreviousState} → {e.NewState}");

            switch (e.NewState)
            {
                case GameState.PreGame:
                    ShowPreGame();
                    break;

                case GameState.PostGame:
                    ShowPostGame();
                    break;

                case GameState.Playing:
                    ShowDashboard();
                    break;
            }

            RefreshUI();
        }

        private void OnDayAdvanced(DateTime newDate)
        {
            RefreshUI();
        }

        private void OnGameDay(CalendarEvent gameEvent)
        {
            Debug.Log($"[GameSceneController] Game day: {gameEvent.AwayTeamId} @ {gameEvent.HomeTeamId}");

            // Show pre-game panel or prompt
            ShowPreGame();
        }

        #endregion

        #region Pre-Game Actions

        /// <summary>
        /// Called when player clicks "Simulate" in pre-game
        /// </summary>
        public void OnSimulateGameClicked()
        {
            if (_gameManager?.MatchController != null)
            {
                var result = _gameManager.MatchController.SimulateMatch();
                Debug.Log($"[GameSceneController] Game simulated: {result.HomeScore}-{result.AwayScore}");
            }
        }

        /// <summary>
        /// Called when player clicks "Play" in pre-game
        /// </summary>
        public void OnPlayGameClicked()
        {
            if (_gameManager?.MatchController != null)
            {
                _gameManager.MatchController.StartInteractiveMatch();
            }
        }

        private void OnPlayGameFromDashboard(CalendarEvent game)
        {
            PrepareAndShowPreGame(game);
        }

        private void OnSimGameFromDashboard(CalendarEvent game)
        {
            SimulateGame(game);
        }

        private void OnPlayGameFromCalendar(CalendarEvent game)
        {
            PrepareAndShowPreGame(game);
        }

        private void OnSimGameFromCalendar(CalendarEvent game)
        {
            SimulateGame(game);
        }

        private void OnPlayGameFromPreGame(CalendarEvent game, int[] lineup, TeamStrategy strategy)
        {
            if (_gameManager?.MatchController != null)
            {
                _gameManager.MatchController.SetStartingLineup(lineup);
                _gameManager.MatchController.SetStrategy(strategy);
                _gameManager.MatchController.StartInteractiveMatch();
            }
        }

        private void OnSimGameFromPreGame(CalendarEvent game, int[] lineup, TeamStrategy strategy)
        {
            if (_gameManager?.MatchController != null)
            {
                _gameManager.MatchController.SetStartingLineup(lineup);
                _gameManager.MatchController.SetStrategy(strategy);
                var result = _gameManager.MatchController.SimulateMatch();
                ShowPostGameResults(result);
            }
        }

        private void PrepareAndShowPreGame(CalendarEvent game)
        {
            if (_gameManager?.MatchController != null)
            {
                _gameManager.MatchController.PrepareMatch(game);

                if (_preGamePanelComponent != null)
                {
                    var playerTeam = _gameManager.GetPlayerTeam();
                    var opponentTeam = _gameManager.MatchController.OpponentTeam;
                    bool isHome = _gameManager.MatchController.IsHomeGame;

                    _preGamePanelComponent.Initialize(game, playerTeam, opponentTeam, isHome);
                }

                ShowPreGame();
            }
        }

        private void SimulateGame(CalendarEvent game)
        {
            if (_gameManager?.MatchController != null)
            {
                _gameManager.MatchController.PrepareMatch(game);
                var result = _gameManager.MatchController.SimulateMatch();
                ShowPostGameResults(result);
            }
        }

        private void ShowPostGameResults(BoxScore result)
        {
            if (_postGamePanelComponent != null)
            {
                var summary = _gameManager.MatchController.GetPostGameSummary();
                _postGamePanelComponent.Initialize(result, summary);
            }
            ShowPostGame();
        }

        #endregion

        #region Post-Game Actions

        /// <summary>
        /// Called when player clicks "Continue" in post-game
        /// </summary>
        public void OnContinueFromPostGame()
        {
            if (_gameManager?.MatchController != null)
            {
                _gameManager.MatchController.ContinueFromPostGame();
                ShowDashboard();
                RefreshUI();
            }
        }

        #endregion

        #region Trade Actions

        private void OnTradePlayerRequested(Player player)
        {
            Debug.Log($"[GameSceneController] Trade requested for {player.FirstName} {player.LastName}");
            // Would open trade panel with player pre-selected
        }

        #endregion
    }
}
