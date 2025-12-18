using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;
using NBAHeadCoach.UI.Panels;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Tabbed coaching menu for in-game decisions.
    /// Provides access to offense, defense, substitutions, matchups, and overall strategy.
    /// Wires directly to GameCoach for all actions.
    /// </summary>
    public class CoachingMenuView : MonoBehaviour
    {
        #region References

        [Header("Panel")]
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Button _closeButton;

        [Header("Tabs")]
        [SerializeField] private Button _offenseTab;
        [SerializeField] private Button _defenseTab;
        [SerializeField] private Button _substitutionsTab;
        [SerializeField] private Button _matchupsTab;
        [SerializeField] private Button _strategyTab;

        [Header("Tab Content Panels")]
        [SerializeField] private GameObject _offensePanel;
        [SerializeField] private GameObject _defensePanel;
        [SerializeField] private GameObject _substitutionsPanel;
        [SerializeField] private GameObject _matchupsPanel;
        [SerializeField] private GameObject _strategyPanel;

        [Header("Offense Controls")]
        [SerializeField] private Dropdown _offensiveSchemeDropdown;
        [SerializeField] private Dropdown _paceDropdown;
        [SerializeField] private Transform _quickActionsContainer;
        [SerializeField] private Transform _setPlaysContainer;
        [SerializeField] private Button _callPlayButton;
        [SerializeField] private Text _selectedPlayText;

        [Header("Defense Controls")]
        [SerializeField] private Dropdown _defensiveSchemeDropdown;
        [SerializeField] private Dropdown _pnrCoverageDropdown;
        [SerializeField] private Dropdown _transitionDefenseDropdown;
        [SerializeField] private Toggle _fullCourtPressToggle;
        [SerializeField] private Toggle _hackAShaqToggle;
        [SerializeField] private Dropdown _hackAShaqTargetDropdown;

        [Header("Substitutions Controls")]
        [SerializeField] private Transform _currentLineupContainer;
        [SerializeField] private Transform _benchContainer;
        [SerializeField] private Button _confirmSubsButton;
        [SerializeField] private Text _substitutionSuggestionsText;
        [SerializeField] private Slider _fatigueThresholdSlider;
        [SerializeField] private Text _fatigueThresholdText;

        [Header("Matchups Controls")]
        [SerializeField] private Transform _matchupsListContainer;
        [SerializeField] private Dropdown _doubleTeamTargetDropdown;
        [SerializeField] private Dropdown _doubleTeamTriggerDropdown;
        [SerializeField] private Button _clearDoubleTeamButton;

        [Header("Strategy Controls")]
        [SerializeField] private Dropdown _intensityDropdown;
        [SerializeField] private Text _timeoutsRemainingText;
        [SerializeField] private Button _callTimeoutButton;
        [SerializeField] private Button _challengeCallButton;
        [SerializeField] private Text _challengeStatusText;
        [SerializeField] private Text _endGameRecommendationText;

        [Header("Player Card Prefab")]
        [SerializeField] private GameObject _playerCardPrefab;
        [SerializeField] private GameObject _matchupRowPrefab;
        [SerializeField] private GameObject _playButtonPrefab;

        #endregion

        #region State

        private GameCoach _gameCoach;
        private Team _playerTeam;
        private Team _opponentTeam;
        private List<string> _currentLineup = new List<string>();
        private List<string> _pendingSubsOut = new List<string>();
        private List<string> _pendingSubsIn = new List<string>();
        private CoachingMenuTab _currentTab = CoachingMenuTab.Offense;
        private string _highlightedPlayerId;

        // UI Element tracking
        private List<PlayerCardUI> _lineupCards = new List<PlayerCardUI>();
        private List<PlayerCardUI> _benchCards = new List<PlayerCardUI>();
        private List<MatchupRowUI> _matchupRows = new List<MatchupRowUI>();

        #endregion

        #region Events

        public event Action<SubstitutionResult> OnSubstitutionMade;
        public event Action OnMenuClosed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetupButtons();
            SetupDropdowns();
            HideAllPanels();
        }

        private void Start()
        {
            if (_menuPanel != null)
                _menuPanel.SetActive(false);
        }

        #endregion

        #region Setup

        private void SetupButtons()
        {
            _closeButton?.onClick.AddListener(Hide);

            // Tab buttons
            _offenseTab?.onClick.AddListener(() => ShowTab(CoachingMenuTab.Offense));
            _defenseTab?.onClick.AddListener(() => ShowTab(CoachingMenuTab.Defense));
            _substitutionsTab?.onClick.AddListener(() => ShowTab(CoachingMenuTab.Substitutions));
            _matchupsTab?.onClick.AddListener(() => ShowTab(CoachingMenuTab.Matchups));
            _strategyTab?.onClick.AddListener(() => ShowTab(CoachingMenuTab.Strategy));

            // Action buttons
            _confirmSubsButton?.onClick.AddListener(ConfirmSubstitutions);
            _callTimeoutButton?.onClick.AddListener(CallTimeout);
            _challengeCallButton?.onClick.AddListener(ChallengeCall);
            _clearDoubleTeamButton?.onClick.AddListener(ClearDoubleTeam);
            _callPlayButton?.onClick.AddListener(CallSelectedPlay);

            // Toggle handlers
            _fullCourtPressToggle?.onValueChanged.AddListener(OnFullCourtPressToggled);
            _hackAShaqToggle?.onValueChanged.AddListener(OnHackAShaqToggled);

            // Slider handlers
            if (_fatigueThresholdSlider != null)
            {
                _fatigueThresholdSlider.onValueChanged.AddListener(OnFatigueThresholdChanged);
            }
        }

        private void SetupDropdowns()
        {
            // Offensive schemes
            if (_offensiveSchemeDropdown != null)
            {
                _offensiveSchemeDropdown.ClearOptions();
                _offensiveSchemeDropdown.AddOptions(Enum.GetNames(typeof(OffensiveScheme)).ToList());
                _offensiveSchemeDropdown.onValueChanged.AddListener(OnOffensiveSchemeChanged);
            }

            // Pace
            if (_paceDropdown != null)
            {
                _paceDropdown.ClearOptions();
                _paceDropdown.AddOptions(Enum.GetNames(typeof(GamePace)).ToList());
                _paceDropdown.onValueChanged.AddListener(OnPaceChanged);
            }

            // Defensive schemes
            if (_defensiveSchemeDropdown != null)
            {
                _defensiveSchemeDropdown.ClearOptions();
                _defensiveSchemeDropdown.AddOptions(Enum.GetNames(typeof(DefensiveScheme)).ToList());
                _defensiveSchemeDropdown.onValueChanged.AddListener(OnDefensiveSchemeChanged);
            }

            // PnR Coverage
            if (_pnrCoverageDropdown != null)
            {
                _pnrCoverageDropdown.ClearOptions();
                _pnrCoverageDropdown.AddOptions(Enum.GetNames(typeof(PnRCoverageScheme)).ToList());
                _pnrCoverageDropdown.onValueChanged.AddListener(OnPnRCoverageChanged);
            }

            // Transition defense
            if (_transitionDefenseDropdown != null)
            {
                _transitionDefenseDropdown.ClearOptions();
                _transitionDefenseDropdown.AddOptions(Enum.GetNames(typeof(TransitionDefenseLevel)).ToList());
                _transitionDefenseDropdown.onValueChanged.AddListener(OnTransitionDefenseChanged);
            }

            // Intensity
            if (_intensityDropdown != null)
            {
                _intensityDropdown.ClearOptions();
                _intensityDropdown.AddOptions(Enum.GetNames(typeof(IntensityLevel)).ToList());
                _intensityDropdown.onValueChanged.AddListener(OnIntensityChanged);
            }

            // Double team trigger
            if (_doubleTeamTriggerDropdown != null)
            {
                _doubleTeamTriggerDropdown.ClearOptions();
                _doubleTeamTriggerDropdown.AddOptions(Enum.GetNames(typeof(DoubleTeamTrigger)).ToList());
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the coaching menu with game context
        /// </summary>
        public void Initialize(GameCoach gameCoach, Team playerTeam, Team opponentTeam, List<string> currentLineup)
        {
            _gameCoach = gameCoach;
            _playerTeam = playerTeam;
            _opponentTeam = opponentTeam;
            _currentLineup = new List<string>(currentLineup);

            RefreshAllControls();
        }

        /// <summary>
        /// Show the menu at a specific tab
        /// </summary>
        public void Show(CoachingMenuTab tab = CoachingMenuTab.Offense)
        {
            if (_menuPanel != null)
                _menuPanel.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            ShowTab(tab);
            RefreshAllControls();
        }

        /// <summary>
        /// Hide the menu
        /// </summary>
        public void Hide()
        {
            if (_menuPanel != null)
                _menuPanel.SetActive(false);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            OnMenuClosed?.Invoke();
        }

        /// <summary>
        /// Highlight a player that needs to be substituted (e.g., foul out)
        /// </summary>
        public void HighlightPlayerForSubstitution(string playerId)
        {
            _highlightedPlayerId = playerId;
            Show(CoachingMenuTab.Substitutions);

            // Find and highlight the player card
            foreach (var card in _lineupCards)
            {
                if (card.PlayerId == playerId)
                {
                    card.SetHighlight(true, Color.red);
                }
            }
        }

        /// <summary>
        /// Update the current lineup display
        /// </summary>
        public void UpdateLineup(List<string> newLineup)
        {
            _currentLineup = new List<string>(newLineup);
            RefreshSubstitutionsPanel();
        }

        #endregion

        #region Tab Management

        private void ShowTab(CoachingMenuTab tab)
        {
            _currentTab = tab;
            HideAllPanels();

            // Show selected panel
            switch (tab)
            {
                case CoachingMenuTab.Offense:
                    if (_offensePanel != null) _offensePanel.SetActive(true);
                    break;
                case CoachingMenuTab.Defense:
                    if (_defensePanel != null) _defensePanel.SetActive(true);
                    break;
                case CoachingMenuTab.Substitutions:
                    if (_substitutionsPanel != null) _substitutionsPanel.SetActive(true);
                    RefreshSubstitutionsPanel();
                    break;
                case CoachingMenuTab.Matchups:
                    if (_matchupsPanel != null) _matchupsPanel.SetActive(true);
                    RefreshMatchupsPanel();
                    break;
                case CoachingMenuTab.Strategy:
                    if (_strategyPanel != null) _strategyPanel.SetActive(true);
                    RefreshStrategyPanel();
                    break;
            }

            UpdateTabHighlights(tab);
        }

        private void HideAllPanels()
        {
            if (_offensePanel != null) _offensePanel.SetActive(false);
            if (_defensePanel != null) _defensePanel.SetActive(false);
            if (_substitutionsPanel != null) _substitutionsPanel.SetActive(false);
            if (_matchupsPanel != null) _matchupsPanel.SetActive(false);
            if (_strategyPanel != null) _strategyPanel.SetActive(false);
        }

        private void UpdateTabHighlights(CoachingMenuTab activeTab)
        {
            SetTabHighlight(_offenseTab, activeTab == CoachingMenuTab.Offense);
            SetTabHighlight(_defenseTab, activeTab == CoachingMenuTab.Defense);
            SetTabHighlight(_substitutionsTab, activeTab == CoachingMenuTab.Substitutions);
            SetTabHighlight(_matchupsTab, activeTab == CoachingMenuTab.Matchups);
            SetTabHighlight(_strategyTab, activeTab == CoachingMenuTab.Strategy);
        }

        private void SetTabHighlight(Button tab, bool active)
        {
            if (tab == null) return;

            var colors = tab.colors;
            colors.normalColor = active ? new Color(0.3f, 0.5f, 0.8f) : Color.white;
            tab.colors = colors;
        }

        #endregion

        #region Refresh Methods

        private void RefreshAllControls()
        {
            if (_gameCoach == null) return;

            var tactics = _gameCoach.GetTactics();

            // Offense
            if (_offensiveSchemeDropdown != null)
                _offensiveSchemeDropdown.value = (int)tactics.Offense;
            if (_paceDropdown != null)
                _paceDropdown.value = (int)tactics.Pace;

            // Defense
            if (_defensiveSchemeDropdown != null)
                _defensiveSchemeDropdown.value = (int)tactics.Defense;
            if (_pnrCoverageDropdown != null)
                _pnrCoverageDropdown.value = (int)tactics.PnRCoverage;
            if (_transitionDefenseDropdown != null)
                _transitionDefenseDropdown.value = (int)tactics.TransitionDefense;
            if (_fullCourtPressToggle != null)
                _fullCourtPressToggle.isOn = tactics.PressEnabled;
            if (_hackAShaqToggle != null)
                _hackAShaqToggle.isOn = tactics.HackAShaqEnabled;

            // Strategy
            if (_intensityDropdown != null)
                _intensityDropdown.value = (int)tactics.Intensity;

            RefreshTimeoutsDisplay();
            RefreshChallengeDisplay();
            RefreshEndGameRecommendation();
            RefreshSetPlaysList();
        }

        private void RefreshSubstitutionsPanel()
        {
            ClearContainer(_currentLineupContainer);
            ClearContainer(_benchContainer);
            _lineupCards.Clear();
            _benchCards.Clear();
            _pendingSubsOut.Clear();
            _pendingSubsIn.Clear();

            if (_playerTeam?.Roster == null) return;

            var db = GameManager.Instance?.PlayerDatabase;
            if (db == null) return;

            // Create lineup cards
            foreach (var playerId in _currentLineup)
            {
                var player = db.GetPlayer(playerId);
                if (player != null)
                {
                    var card = CreatePlayerCard(player, _currentLineupContainer, true);
                    _lineupCards.Add(card);
                }
            }

            // Create bench cards
            foreach (var playerId in _playerTeam.RosterPlayerIds)
            {
                if (!_currentLineup.Contains(playerId))
                {
                    var player = db.GetPlayer(playerId);
                    if (player != null && !player.IsInjured)
                    {
                        var card = CreatePlayerCard(player, _benchContainer, false);
                        _benchCards.Add(card);
                    }
                }
            }

            // Update substitution suggestions
            RefreshSubstitutionSuggestions();
        }

        private void RefreshSubstitutionSuggestions()
        {
            if (_substitutionSuggestionsText == null || _gameCoach == null) return;

            var playerEnergy = new Dictionary<string, float>();
            var db = GameManager.Instance?.PlayerDatabase;

            if (db != null)
            {
                foreach (var playerId in _currentLineup)
                {
                    var player = db.GetPlayer(playerId);
                    if (player != null)
                    {
                        playerEnergy[playerId] = player.Energy;
                    }
                }
            }

            var suggestions = _gameCoach.GetSubstitutionSuggestions(_currentLineup, playerEnergy);

            if (suggestions.Count == 0)
            {
                _substitutionSuggestionsText.text = "No substitutions recommended";
            }
            else
            {
                var text = "Recommended:\n";
                foreach (var s in suggestions.Take(3))
                {
                    var player = db?.GetPlayer(s.PlayerOut);
                    text += $"• Sub out {player?.DisplayName ?? s.PlayerOut}: {s.Reason}\n";
                }
                _substitutionSuggestionsText.text = text;
            }
        }

        private void RefreshMatchupsPanel()
        {
            ClearContainer(_matchupsListContainer);
            _matchupRows.Clear();

            if (_gameCoach == null || _opponentTeam == null) return;

            var matchups = _gameCoach.GetAllMatchups();
            var db = GameManager.Instance?.PlayerDatabase;

            // Get opponent starters (first 5 in roster for simplicity)
            var opponentPlayers = _opponentTeam.RosterPlayerIds?.Take(5).ToList() ?? new List<string>();

            foreach (var opponentId in opponentPlayers)
            {
                var opponent = db?.GetPlayer(opponentId);
                if (opponent == null) continue;

                var existingMatchup = matchups.FirstOrDefault(m => m.OpponentId == opponentId);

                var row = CreateMatchupRow(opponent, existingMatchup?.DefenderId);
                _matchupRows.Add(row);
            }

            // Populate double team target dropdown
            if (_doubleTeamTargetDropdown != null)
            {
                _doubleTeamTargetDropdown.ClearOptions();
                var options = new List<string> { "None" };
                foreach (var opponentId in opponentPlayers)
                {
                    var player = db?.GetPlayer(opponentId);
                    options.Add(player?.DisplayName ?? opponentId);
                }
                _doubleTeamTargetDropdown.AddOptions(options);
            }
        }

        private void RefreshStrategyPanel()
        {
            RefreshTimeoutsDisplay();
            RefreshChallengeDisplay();
            RefreshEndGameRecommendation();
        }

        private void RefreshTimeoutsDisplay()
        {
            if (_timeoutsRemainingText != null && _gameCoach != null)
            {
                _timeoutsRemainingText.text = $"Timeouts: {_gameCoach.TimeoutsRemaining}";
            }

            if (_callTimeoutButton != null && _gameCoach != null)
            {
                _callTimeoutButton.interactable = _gameCoach.TimeoutsRemaining > 0;
            }
        }

        private void RefreshChallengeDisplay()
        {
            if (_challengeStatusText != null && _gameCoach != null)
            {
                _challengeStatusText.text = _gameCoach.ChallengeUsed ? "Challenge Used" : "Challenge Available";
            }

            if (_challengeCallButton != null && _gameCoach != null)
            {
                _challengeCallButton.interactable = !_gameCoach.ChallengeUsed && _gameCoach.TimeoutsRemaining > 0;
            }
        }

        private void RefreshEndGameRecommendation()
        {
            if (_endGameRecommendationText == null || _gameCoach == null) return;

            var recommendation = _gameCoach.GetEndGameRecommendation();
            _endGameRecommendationText.text = recommendation?.Explanation ?? "";
        }

        private void RefreshSetPlaysList()
        {
            ClearContainer(_setPlaysContainer);

            if (_gameCoach == null) return;

            var plays = _gameCoach.GetRecommendedPlays();
            if (plays == null || plays.Count == 0)
            {
                // Show quick actions instead
                var quickActions = new[] {
                    (QuickActionType.Isolation, "ISO"),
                    (QuickActionType.PickAndRoll, "PnR"),
                    (QuickActionType.PostUp, "Post"),
                    (QuickActionType.ShooterAction, "Shooter"),
                    (QuickActionType.Transition, "Push"),
                    (QuickActionType.SlowDown, "Slow")
                };

                foreach (var (action, label) in quickActions)
                {
                    CreateQuickActionButton(action, label);
                }
            }
            else
            {
                foreach (var play in plays.Take(6))
                {
                    CreateSetPlayButton(play);
                }
            }
        }

        #endregion

        #region UI Factory Methods

        private PlayerCardUI CreatePlayerCard(Player player, Transform container, bool isOnCourt)
        {
            if (container == null) return null;

            GameObject cardObj;
            if (_playerCardPrefab != null)
            {
                cardObj = Instantiate(_playerCardPrefab, container);
            }
            else
            {
                cardObj = new GameObject($"PlayerCard_{player.PlayerId}");
                cardObj.transform.SetParent(container, false);

                var layout = cardObj.AddComponent<HorizontalLayoutGroup>();
                layout.childForceExpandWidth = false;
                layout.spacing = 5;

                // Add basic text
                var textObj = new GameObject("Name");
                textObj.transform.SetParent(cardObj.transform, false);
                var text = textObj.AddComponent<Text>();
                text.text = $"{player.JerseyNumber} {player.LastName}";
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                var button = cardObj.AddComponent<Button>();
            }

            var cardUI = cardObj.GetComponent<PlayerCardUI>();
            if (cardUI == null)
            {
                cardUI = cardObj.AddComponent<PlayerCardUI>();
            }

            cardUI.Initialize(player, isOnCourt);
            cardUI.OnClicked += () => OnPlayerCardClicked(player.PlayerId, isOnCourt);

            return cardUI;
        }

        private MatchupRowUI CreateMatchupRow(Player opponent, string currentDefenderId)
        {
            if (_matchupsListContainer == null) return null;

            GameObject rowObj;
            if (_matchupRowPrefab != null)
            {
                rowObj = Instantiate(_matchupRowPrefab, _matchupsListContainer);
            }
            else
            {
                rowObj = new GameObject($"Matchup_{opponent.PlayerId}");
                rowObj.transform.SetParent(_matchupsListContainer, false);

                var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10;
            }

            var rowUI = rowObj.GetComponent<MatchupRowUI>();
            if (rowUI == null)
            {
                rowUI = rowObj.AddComponent<MatchupRowUI>();
            }

            rowUI.Initialize(opponent, _currentLineup, currentDefenderId);
            rowUI.OnMatchupChanged += (defenderId) => OnMatchupChanged(opponent.PlayerId, defenderId);

            return rowUI;
        }

        private void CreateQuickActionButton(QuickActionType action, string label)
        {
            if (_quickActionsContainer == null) return;

            var buttonObj = new GameObject($"QuickAction_{action}");
            buttonObj.transform.SetParent(_quickActionsContainer, false);

            var button = buttonObj.AddComponent<Button>();
            var image = buttonObj.AddComponent<Image>();
            image.color = Color.white;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.black;

            var rt = buttonObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60, 40);

            button.onClick.AddListener(() => CallQuickAction(action));
        }

        private void CreateSetPlayButton(SetPlay play)
        {
            if (_setPlaysContainer == null || play == null) return;

            var buttonObj = new GameObject($"Play_{play.PlayId}");
            buttonObj.transform.SetParent(_setPlaysContainer, false);

            var button = buttonObj.AddComponent<Button>();
            var image = buttonObj.AddComponent<Image>();
            image.color = Color.white;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            var text = textObj.AddComponent<Text>();
            text.text = play.PlayName;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.black;

            var rt = buttonObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 40);

            button.onClick.AddListener(() =>
            {
                _gameCoach?.CallSetPlay(play.PlayId);
                if (_selectedPlayText != null) _selectedPlayText.text = $"Called: {play.PlayName}";
            });
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

        #region Action Handlers

        private void OnPlayerCardClicked(string playerId, bool isOnCourt)
        {
            if (isOnCourt)
            {
                // Select for substitution out
                if (_pendingSubsOut.Contains(playerId))
                {
                    _pendingSubsOut.Remove(playerId);
                }
                else
                {
                    _pendingSubsOut.Add(playerId);
                }
            }
            else
            {
                // Select for substitution in
                if (_pendingSubsIn.Contains(playerId))
                {
                    _pendingSubsIn.Remove(playerId);
                }
                else if (_pendingSubsIn.Count < _pendingSubsOut.Count)
                {
                    _pendingSubsIn.Add(playerId);
                }
            }

            UpdateSubstitutionHighlights();
        }

        private void UpdateSubstitutionHighlights()
        {
            foreach (var card in _lineupCards)
            {
                card.SetHighlight(_pendingSubsOut.Contains(card.PlayerId), Color.red);
            }

            foreach (var card in _benchCards)
            {
                card.SetHighlight(_pendingSubsIn.Contains(card.PlayerId), Color.green);
            }

            if (_confirmSubsButton != null)
            {
                _confirmSubsButton.interactable = _pendingSubsOut.Count > 0 &&
                                                   _pendingSubsOut.Count == _pendingSubsIn.Count;
            }
        }

        private void ConfirmSubstitutions()
        {
            if (_gameCoach == null || _pendingSubsOut.Count == 0) return;

            var result = _gameCoach.SubstituteMultiple(_pendingSubsOut, _pendingSubsIn, _currentLineup);

            if (result.Success)
            {
                _currentLineup = result.NewLineup;
                OnSubstitutionMade?.Invoke(result);
                RefreshSubstitutionsPanel();
            }
            else
            {
                Debug.LogWarning($"[CoachingMenu] Substitution failed: {result.Reason}");
            }
        }

        private void OnMatchupChanged(string opponentId, string defenderId)
        {
            _gameCoach?.SetDefensiveMatchup(defenderId, opponentId);
        }

        private void CallTimeout()
        {
            var result = _gameCoach?.CallTimeout(TimeoutReason.Coach);
            if (result?.Success == true)
            {
                RefreshTimeoutsDisplay();
                Debug.Log("[CoachingMenu] Timeout called");
            }
        }

        private void ChallengeCall()
        {
            var result = _gameCoach?.UseChallenge(ChallengeType.FoulCall);
            if (result != null)
            {
                RefreshChallengeDisplay();
                Debug.Log($"[CoachingMenu] Challenge: {result.Message}");
            }
        }

        private void ClearDoubleTeam()
        {
            var tactics = _gameCoach?.GetTactics();
            if (tactics?.DoubleTeamTarget != null)
            {
                _gameCoach.ClearDoubleTeam(tactics.DoubleTeamTarget);
            }
        }

        private void CallSelectedPlay()
        {
            // Would call currently selected play
        }

        private void CallQuickAction(QuickActionType action)
        {
            _gameCoach?.CallQuickAction(action);
            if (_selectedPlayText != null)
            {
                _selectedPlayText.text = $"Called: {action}";
            }
        }

        #endregion

        #region Dropdown Handlers

        private void OnOffensiveSchemeChanged(int index)
        {
            _gameCoach?.SetOffense((OffensiveScheme)index);
        }

        private void OnPaceChanged(int index)
        {
            _gameCoach?.SetPace((GamePace)index);
        }

        private void OnDefensiveSchemeChanged(int index)
        {
            _gameCoach?.SetDefense((DefensiveScheme)index);
        }

        private void OnPnRCoverageChanged(int index)
        {
            _gameCoach?.SetPickAndRollCoverage((PnRCoverageScheme)index);
        }

        private void OnTransitionDefenseChanged(int index)
        {
            _gameCoach?.SetTransitionDefense((TransitionDefenseLevel)index);
        }

        private void OnIntensityChanged(int index)
        {
            _gameCoach?.SetIntensity((IntensityLevel)index);
        }

        private void OnFullCourtPressToggled(bool enabled)
        {
            var tactics = _gameCoach?.GetTactics();
            if (tactics != null)
            {
                tactics.PressEnabled = enabled;
            }
        }

        private void OnHackAShaqToggled(bool enabled)
        {
            var tactics = _gameCoach?.GetTactics();
            if (tactics != null)
            {
                tactics.HackAShaqEnabled = enabled;
            }
        }

        private void OnFatigueThresholdChanged(float value)
        {
            int threshold = (int)value;
            _gameCoach?.SetFatigueThreshold(threshold);

            if (_fatigueThresholdText != null)
            {
                _fatigueThresholdText.text = $"Fatigue Threshold: {threshold}%";
            }
        }

        #endregion
    }

    /// <summary>
    /// UI component for player cards in substitution view
    /// </summary>
    public class PlayerCardUI : MonoBehaviour
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _positionText;
        [SerializeField] private Text _energyText;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Button _button;

        public string PlayerId { get; private set; }
        public event Action OnClicked;

        private Color _normalColor = Color.white;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            _button?.onClick.AddListener(() => OnClicked?.Invoke());
        }

        public void Initialize(Player player, bool isOnCourt)
        {
            PlayerId = player.PlayerId;

            if (_nameText != null)
                _nameText.text = $"#{player.JerseyNumber} {player.LastName}";

            if (_positionText != null)
                _positionText.text = player.Position;

            if (_energyText != null)
                _energyText.text = $"{player.Energy:F0}%";

            _normalColor = isOnCourt ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.8f, 0.8f, 0.8f);

            if (_backgroundImage != null)
                _backgroundImage.color = _normalColor;
        }

        public void SetHighlight(bool highlighted, Color highlightColor)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = highlighted ? highlightColor : _normalColor;
            }
        }
    }

    /// <summary>
    /// UI component for matchup rows
    /// </summary>
    public class MatchupRowUI : MonoBehaviour
    {
        [SerializeField] private Text _opponentNameText;
        [SerializeField] private Dropdown _defenderDropdown;

        private string _opponentId;
        private List<string> _defenderOptions = new List<string>();

        public event Action<string> OnMatchupChanged;

        public void Initialize(Player opponent, List<string> availableDefenders, string currentDefenderId)
        {
            _opponentId = opponent.PlayerId;

            if (_opponentNameText != null)
                _opponentNameText.text = opponent.DisplayName;

            if (_defenderDropdown != null)
            {
                _defenderDropdown.ClearOptions();
                _defenderOptions.Clear();

                var db = GameManager.Instance?.PlayerDatabase;
                foreach (var defenderId in availableDefenders)
                {
                    var defender = db?.GetPlayer(defenderId);
                    _defenderOptions.Add(defenderId);
                    _defenderDropdown.options.Add(new Dropdown.OptionData(defender?.DisplayName ?? defenderId));
                }

                // Set current selection
                int index = _defenderOptions.IndexOf(currentDefenderId);
                if (index >= 0) _defenderDropdown.value = index;

                _defenderDropdown.onValueChanged.AddListener(OnDefenderSelected);
            }
        }

        private void OnDefenderSelected(int index)
        {
            if (index >= 0 && index < _defenderOptions.Count)
            {
                OnMatchupChanged?.Invoke(_defenderOptions[index]);
            }
        }
    }

    /// <summary>
    /// Quick action types for simplified play calls
    /// </summary>
    public enum QuickActionType
    {
        Isolation,
        PickAndRoll,
        PostUp,
        ShooterAction,
        Transition,
        SlowDown
    }
}
