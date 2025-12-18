using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// ESPN Gamecast-style match visualization panel.
    /// Shows live play-by-play, scoreboard, stats, and coaching controls.
    /// </summary>
    public class MatchPanel : BasePanel
    {
        #region Layout References

        [Header("Scoreboard Section")]
        [SerializeField] private RectTransform _scoreboardSection;
        [SerializeField] private Text _awayTeamName;
        [SerializeField] private Text _awayScore;
        [SerializeField] private Image _awayLogo;
        [SerializeField] private Text _homeTeamName;
        [SerializeField] private Text _homeScore;
        [SerializeField] private Image _homeLogo;
        [SerializeField] private Text _quarterText;
        [SerializeField] private Text _clockText;
        [SerializeField] private Text _shotClockText;
        [SerializeField] private GameObject _homePossessionIndicator;
        [SerializeField] private GameObject _awayPossessionIndicator;

        [Header("Play-by-Play Section")]
        [SerializeField] private RectTransform _playByPlaySection;
        [SerializeField] private ScrollRect _playByPlayScrollRect;
        [SerializeField] private Transform _playByPlayContent;
        [SerializeField] private GameObject _playByPlayEntryPrefab;
        [SerializeField] private int _maxPlayByPlayEntries = 100;

        [Header("Stats Section")]
        [SerializeField] private RectTransform _statsSection;
        [SerializeField] private Transform _homeLineupContainer;
        [SerializeField] private Transform _awayLineupContainer;
        [SerializeField] private Text _homeTeamStatsText;
        [SerializeField] private Text _awayTeamStatsText;
        [SerializeField] private Button _boxScoreButton;

        [Header("Court Diagram Section")]
        [SerializeField] private RectTransform _courtDiagramSection;
        [SerializeField] private CourtDiagramView _courtDiagram;

        [Header("Speed Controls")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _speed1xButton;
        [SerializeField] private Button _speed2xButton;
        [SerializeField] private Button _speed4xButton;
        [SerializeField] private Button _speed8xButton;
        [SerializeField] private Button _instantButton;
        [SerializeField] private Text _currentSpeedText;

        [Header("Coaching Controls")]
        [SerializeField] private Button _timeoutButton;
        [SerializeField] private Button _substitutionButton;
        [SerializeField] private Button _strategyButton;
        [SerializeField] private Toggle _autoCoachToggle;
        [SerializeField] private Text _timeoutsRemainingText;
        [SerializeField] private CoachingMenuView _coachingMenu;

        [Header("Game End")]
        [SerializeField] private GameObject _gameEndOverlay;
        [SerializeField] private Text _finalScoreText;
        [SerializeField] private Text _gameResultText;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _viewBoxScoreButton;

        #endregion

        #region State

        private MatchSimulationController _simController;
        private Team _homeTeam;
        private Team _awayTeam;
        private Team _playerTeam;
        private bool _playerIsHome;
        private List<GameObject> _playByPlayEntries = new List<GameObject>();
        private SimulationSpeed _currentSpeed = SimulationSpeed.Normal;

        #endregion

        #region Events

        public event Action OnContinueAfterGame;
        public event Action<BoxScore> OnViewBoxScore;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            SetupButtons();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void SetupButtons()
        {
            // Speed controls
            _pauseButton?.onClick.AddListener(OnPauseClicked);
            _playButton?.onClick.AddListener(OnPlayClicked);
            _speed1xButton?.onClick.AddListener(() => SetSpeed(SimulationSpeed.Slow));
            _speed2xButton?.onClick.AddListener(() => SetSpeed(SimulationSpeed.Normal));
            _speed4xButton?.onClick.AddListener(() => SetSpeed(SimulationSpeed.Fast));
            _speed8xButton?.onClick.AddListener(() => SetSpeed(SimulationSpeed.VeryFast));
            _instantButton?.onClick.AddListener(() => SetSpeed(SimulationSpeed.Instant));

            // Coaching controls
            _timeoutButton?.onClick.AddListener(OnTimeoutClicked);
            _substitutionButton?.onClick.AddListener(OnSubstitutionClicked);
            _strategyButton?.onClick.AddListener(OnStrategyClicked);
            _autoCoachToggle?.onValueChanged.AddListener(OnAutoCoachToggled);

            // Box score
            _boxScoreButton?.onClick.AddListener(OnBoxScoreClicked);

            // Game end
            _continueButton?.onClick.AddListener(OnContinueClicked);
            _viewBoxScoreButton?.onClick.AddListener(OnViewBoxScoreClicked);
        }

        private void SubscribeToEvents()
        {
            _simController = MatchSimulationController.Instance;
            if (_simController == null) return;

            _simController.OnPlayByPlay += HandlePlayByPlay;
            _simController.OnScoreboardUpdate += HandleScoreboardUpdate;
            _simController.OnPossessionChange += HandlePossessionChange;
            _simController.OnTimeout += HandleTimeout;
            _simController.OnQuarterEnd += HandleQuarterEnd;
            _simController.OnPlayerFoulOut += HandlePlayerFoulOut;
            _simController.OnGameComplete += HandleGameComplete;
            _simController.OnCoachingDecisionRequired += HandleCoachingDecision;
            _simController.OnSimulationPaused += HandleSimulationPaused;
            _simController.OnSimulationResumed += HandleSimulationResumed;
        }

        private void UnsubscribeFromEvents()
        {
            if (_simController == null) return;

            _simController.OnPlayByPlay -= HandlePlayByPlay;
            _simController.OnScoreboardUpdate -= HandleScoreboardUpdate;
            _simController.OnPossessionChange -= HandlePossessionChange;
            _simController.OnTimeout -= HandleTimeout;
            _simController.OnQuarterEnd -= HandleQuarterEnd;
            _simController.OnPlayerFoulOut -= HandlePlayerFoulOut;
            _simController.OnGameComplete -= HandleGameComplete;
            _simController.OnCoachingDecisionRequired -= HandleCoachingDecision;
            _simController.OnSimulationPaused -= HandleSimulationPaused;
            _simController.OnSimulationResumed -= HandleSimulationResumed;
        }

        /// <summary>
        /// Initialize the panel for a new match
        /// </summary>
        public void InitializeMatch(Team homeTeam, Team awayTeam, Team playerTeam)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _playerTeam = playerTeam;
            _playerIsHome = playerTeam.TeamId == homeTeam.TeamId;

            // Clear previous entries
            ClearPlayByPlay();

            // Set team info
            if (_awayTeamName != null) _awayTeamName.text = awayTeam.Abbreviation;
            if (_homeTeamName != null) _homeTeamName.text = homeTeam.Abbreviation;
            if (_awayScore != null) _awayScore.text = "0";
            if (_homeScore != null) _homeScore.text = "0";
            if (_quarterText != null) _quarterText.text = "Q1";
            if (_clockText != null) _clockText.text = "12:00";
            if (_shotClockText != null) _shotClockText.text = "24";

            // Initialize lineups display
            RefreshLineups();

            // Hide game end overlay
            if (_gameEndOverlay != null) _gameEndOverlay.SetActive(false);

            // Update button states
            UpdatePlayPauseButtons(false);
            UpdateSpeedButtonStates();

            Debug.Log($"[MatchPanel] Initialized: {awayTeam.Name} @ {homeTeam.Name}");
        }

        #endregion

        #region Event Handlers

        private void HandlePlayByPlay(PlayByPlayEntry entry)
        {
            AddPlayByPlayEntry(entry);
        }

        private void HandleScoreboardUpdate(ScoreboardUpdate update)
        {
            if (_homeScore != null) _homeScore.text = update.HomeScore.ToString();
            if (_awayScore != null) _awayScore.text = update.AwayScore.ToString();
            if (_quarterText != null)
            {
                _quarterText.text = update.Quarter <= 4 ? $"Q{update.Quarter}" : $"OT{update.Quarter - 4}";
            }
            if (_clockText != null)
            {
                int mins = (int)(update.Clock / 60);
                int secs = (int)(update.Clock % 60);
                _clockText.text = $"{mins}:{secs:D2}";
            }
            if (_shotClockText != null) _shotClockText.text = ((int)update.ShotClock).ToString();

            UpdatePossessionIndicators(update.HomeHasPossession);
        }

        private void HandlePossessionChange(bool homeHasPossession)
        {
            UpdatePossessionIndicators(homeHasPossession);

            // Update court diagram
            if (_courtDiagram != null)
            {
                _courtDiagram.SetPossession(homeHasPossession);
            }
        }

        private void HandleTimeout(string teamId, TimeoutReason reason)
        {
            // Could show timeout popup or animation
            Debug.Log($"[MatchPanel] Timeout called by {teamId}: {reason}");
            RefreshTimeoutsRemaining();
        }

        private void HandleQuarterEnd(int quarter)
        {
            // Could show quarter break UI
            Debug.Log($"[MatchPanel] End of Q{quarter}");
        }

        private void HandlePlayerFoulOut(string playerId)
        {
            // Could show foul out notification
            Debug.Log($"[MatchPanel] Player fouled out: {playerId}");
            RefreshLineups();
        }

        private void HandleGameComplete(BoxScore boxScore)
        {
            ShowGameEndOverlay(boxScore);
        }

        private void HandleCoachingDecision(CoachingDecisionRequired decision)
        {
            // Show appropriate coaching interface
            switch (decision.Type)
            {
                case CoachingDecisionType.FoulOutSubstitution:
                    ShowSubstitutionMenu(decision.PlayerId);
                    break;
                case CoachingDecisionType.Timeout:
                    // Timeout menu already triggered
                    break;
                case CoachingDecisionType.ClutchTime:
                    ShowClutchTimeNotification();
                    break;
            }
        }

        private void HandleSimulationPaused()
        {
            UpdatePlayPauseButtons(true);
        }

        private void HandleSimulationResumed()
        {
            UpdatePlayPauseButtons(false);
        }

        #endregion

        #region Play-by-Play

        private void AddPlayByPlayEntry(PlayByPlayEntry entry)
        {
            if (_playByPlayContent == null || _playByPlayEntryPrefab == null) return;

            // Create entry UI
            var entryGO = Instantiate(_playByPlayEntryPrefab, _playByPlayContent);
            ConfigurePlayByPlayEntry(entryGO, entry);
            _playByPlayEntries.Add(entryGO);

            // Enforce max entries
            while (_playByPlayEntries.Count > _maxPlayByPlayEntries)
            {
                Destroy(_playByPlayEntries[0]);
                _playByPlayEntries.RemoveAt(0);
            }

            // Scroll to bottom
            if (_playByPlayScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _playByPlayScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void ConfigurePlayByPlayEntry(GameObject entryGO, PlayByPlayEntry entry)
        {
            // Try to find child components
            var clockText = entryGO.transform.Find("Clock")?.GetComponent<Text>();
            var teamText = entryGO.transform.Find("Team")?.GetComponent<Text>();
            var descText = entryGO.transform.Find("Description")?.GetComponent<Text>();
            var scoreText = entryGO.transform.Find("Score")?.GetComponent<Text>();
            var background = entryGO.GetComponent<Image>();

            if (clockText != null) clockText.text = entry.Clock;
            if (teamText != null) teamText.text = entry.Team;
            if (descText != null) descText.text = entry.Description;
            if (scoreText != null && entry.Points > 0)
            {
                scoreText.text = $"{entry.AwayScore}-{entry.HomeScore}";
            }

            // Highlight styling
            if (background != null && entry.IsHighlight)
            {
                background.color = GetHighlightColor(entry.Type);
            }
        }

        private Color GetHighlightColor(PlayByPlayType type)
        {
            return type switch
            {
                PlayByPlayType.Score => new Color(0.2f, 0.6f, 0.2f, 0.3f),       // Green tint
                PlayByPlayType.DefensivePlay => new Color(0.2f, 0.4f, 0.8f, 0.3f), // Blue tint
                PlayByPlayType.Turnover => new Color(0.8f, 0.4f, 0.2f, 0.3f),     // Orange tint
                PlayByPlayType.Foul => new Color(0.8f, 0.2f, 0.2f, 0.3f),         // Red tint
                PlayByPlayType.GameEvent => new Color(0.6f, 0.6f, 0.2f, 0.3f),    // Yellow tint
                _ => new Color(0.3f, 0.3f, 0.3f, 0.2f)
            };
        }

        private void ClearPlayByPlay()
        {
            foreach (var entry in _playByPlayEntries)
            {
                if (entry != null) Destroy(entry);
            }
            _playByPlayEntries.Clear();
        }

        #endregion

        #region UI Updates

        private void UpdatePossessionIndicators(bool homeHasPossession)
        {
            if (_homePossessionIndicator != null)
                _homePossessionIndicator.SetActive(homeHasPossession);
            if (_awayPossessionIndicator != null)
                _awayPossessionIndicator.SetActive(!homeHasPossession);
        }

        private void UpdatePlayPauseButtons(bool isPaused)
        {
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(!isPaused);
            if (_playButton != null) _playButton.gameObject.SetActive(isPaused);
        }

        private void UpdateSpeedButtonStates()
        {
            // Highlight current speed button
            SetButtonHighlight(_speed1xButton, _currentSpeed == SimulationSpeed.Slow);
            SetButtonHighlight(_speed2xButton, _currentSpeed == SimulationSpeed.Normal);
            SetButtonHighlight(_speed4xButton, _currentSpeed == SimulationSpeed.Fast);
            SetButtonHighlight(_speed8xButton, _currentSpeed == SimulationSpeed.VeryFast);
            SetButtonHighlight(_instantButton, _currentSpeed == SimulationSpeed.Instant);

            if (_currentSpeedText != null)
            {
                _currentSpeedText.text = _currentSpeed switch
                {
                    SimulationSpeed.Slow => "1x",
                    SimulationSpeed.Normal => "2x",
                    SimulationSpeed.Fast => "4x",
                    SimulationSpeed.VeryFast => "8x",
                    SimulationSpeed.Instant => "MAX",
                    _ => "2x"
                };
            }
        }

        private void SetButtonHighlight(Button button, bool highlighted)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = highlighted ? new Color(0.3f, 0.6f, 0.9f) : Color.white;
            button.colors = colors;
        }

        private void RefreshLineups()
        {
            if (_simController == null) return;

            // Clear existing lineup displays
            ClearContainer(_homeLineupContainer);
            ClearContainer(_awayLineupContainer);

            // Would populate with current lineups
            // For now just placeholder
        }

        private void RefreshTimeoutsRemaining()
        {
            if (_timeoutsRemainingText != null && _simController?.PlayerCoach != null)
            {
                _timeoutsRemainingText.text = $"TO: {_simController.PlayerCoach.TimeoutsRemaining}";
            }
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        #endregion

        #region Game End

        private void ShowGameEndOverlay(BoxScore boxScore)
        {
            if (_gameEndOverlay == null) return;

            _gameEndOverlay.SetActive(true);

            bool playerWon = (_playerIsHome && boxScore.HomeScore > boxScore.AwayScore) ||
                            (!_playerIsHome && boxScore.AwayScore > boxScore.HomeScore);

            if (_finalScoreText != null)
            {
                _finalScoreText.text = $"{_awayTeam.Abbreviation} {boxScore.AwayScore} - {_homeTeam.Abbreviation} {boxScore.HomeScore}";
            }

            if (_gameResultText != null)
            {
                _gameResultText.text = playerWon ? "VICTORY!" : "DEFEAT";
                _gameResultText.color = playerWon ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            }
        }

        #endregion

        #region Button Handlers

        private void OnPauseClicked()
        {
            _simController?.PauseSimulation();
        }

        private void OnPlayClicked()
        {
            _simController?.StartSimulation();
        }

        private void SetSpeed(SimulationSpeed speed)
        {
            _currentSpeed = speed;
            _simController?.SetSpeed(speed);
            UpdateSpeedButtonStates();
        }

        private void OnTimeoutClicked()
        {
            if (_simController?.CallTimeout() == true)
            {
                RefreshTimeoutsRemaining();
            }
        }

        private void OnSubstitutionClicked()
        {
            ShowSubstitutionMenu(null);
        }

        private void OnStrategyClicked()
        {
            if (_coachingMenu != null)
            {
                _coachingMenu.Show(CoachingMenuTab.Strategy);
            }
        }

        private void OnAutoCoachToggled(bool enabled)
        {
            _simController?.SetAutoCoach(enabled);
        }

        private void OnBoxScoreClicked()
        {
            // Would show box score popup
            Debug.Log("[MatchPanel] Box score clicked");
        }

        private void OnContinueClicked()
        {
            OnContinueAfterGame?.Invoke();
        }

        private void OnViewBoxScoreClicked()
        {
            // Would pass the box score to handler
            Debug.Log("[MatchPanel] View box score clicked");
        }

        #endregion

        #region Coaching Menus

        private void ShowSubstitutionMenu(string foulOutPlayerId)
        {
            if (_coachingMenu != null)
            {
                _coachingMenu.Show(CoachingMenuTab.Substitutions);
                if (!string.IsNullOrEmpty(foulOutPlayerId))
                {
                    _coachingMenu.HighlightPlayerForSubstitution(foulOutPlayerId);
                }
            }
        }

        private void ShowClutchTimeNotification()
        {
            // Could show a "CLUTCH TIME" notification
            Debug.Log("[MatchPanel] Clutch time!");
        }

        #endregion

        #region Panel Overrides

        protected override void OnShown()
        {
            base.OnShown();
            SubscribeToEvents();
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            UnsubscribeFromEvents();
        }

        #endregion
    }

    /// <summary>
    /// Tabs for the coaching menu
    /// </summary>
    public enum CoachingMenuTab
    {
        Offense,
        Defense,
        Substitutions,
        Matchups,
        Strategy
    }
}
