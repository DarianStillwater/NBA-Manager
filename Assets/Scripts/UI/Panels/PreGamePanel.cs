using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using CalendarEvent = NBAHeadCoach.Core.Data.CalendarEvent;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Pre-game panel for lineup selection, tactics, and opponent scouting
    /// </summary>
    public class PreGamePanel : BasePanel
    {
        [Header("Game Info")]
        [SerializeField] private Text _gameInfoText;
        [SerializeField] private Text _opponentNameText;
        [SerializeField] private Text _opponentRecordText;
        [SerializeField] private Image _opponentLogo;
        [SerializeField] private Text _locationText;
        [SerializeField] private Text _broadcastText;

        [Header("Tabs")]
        [SerializeField] private Button _lineupTabButton;
        [SerializeField] private Button _tacticsTabButton;
        [SerializeField] private Button _scoutingTabButton;
        [SerializeField] private GameObject _lineupPanel;
        [SerializeField] private GameObject _tacticsPanel;
        [SerializeField] private GameObject _scoutingPanel;

        [Header("Lineup - Starters")]
        [SerializeField] private Transform _startersContainer;
        [SerializeField] private LineupSlotUI[] _starterSlots;

        [Header("Lineup - Bench")]
        [SerializeField] private Transform _benchContainer;
        [SerializeField] private GameObject _benchPlayerPrefab;

        [Header("Tactics - Offense")]
        [SerializeField] private Dropdown _offensiveSystemDropdown;
        [SerializeField] private Slider _paceSlider;
        [SerializeField] private Text _paceValueText;
        [SerializeField] private Slider _threePointRateSlider;
        [SerializeField] private Text _threePointRateText;

        [Header("Tactics - Defense")]
        [SerializeField] private Dropdown _defensiveSystemDropdown;
        [SerializeField] private Toggle _pressToggle;
        [SerializeField] private Toggle _doubleTeamToggle;

        [Header("Scouting Report")]
        [SerializeField] private Text _opponentStyleText;
        [SerializeField] private Text _opponentStrengthsText;
        [SerializeField] private Text _opponentWeaknessesText;
        [SerializeField] private Text _keyPlayersText;
        [SerializeField] private Text _recentFormText;
        [SerializeField] private Transform _opponentStartersContainer;

        [Header("Buttons")]
        [SerializeField] private Button _playGameButton;
        [SerializeField] private Button _simGameButton;
        [SerializeField] private Button _backButton;

        // State
        private CalendarEvent _currentGame;
        private Team _playerTeam;
        private Team _opponentTeam;
        private bool _isHomeGame;
        private int[] _selectedLineup = new int[5];
        private TeamStrategy _selectedStrategy;
        private List<Player> _availablePlayers = new List<Player>();
        private List<BenchPlayerUI> _benchPlayerUIs = new List<BenchPlayerUI>();
        private PreGameTab _currentTab = PreGameTab.Lineup;

        // Events
        public event Action<CalendarEvent, int[], TeamStrategy> OnPlayGameRequested;
        public event Action<CalendarEvent, int[], TeamStrategy> OnSimGameRequested;
        public event Action OnBackRequested;

        protected override void Awake()
        {
            base.Awake();
            SetupTabs();
            SetupTacticsControls();
            SetupButtons();
        }

        private void SetupTabs()
        {
            if (_lineupTabButton != null)
                _lineupTabButton.onClick.AddListener(() => ShowTab(PreGameTab.Lineup));

            if (_tacticsTabButton != null)
                _tacticsTabButton.onClick.AddListener(() => ShowTab(PreGameTab.Tactics));

            if (_scoutingTabButton != null)
                _scoutingTabButton.onClick.AddListener(() => ShowTab(PreGameTab.Scouting));
        }

        private void SetupTacticsControls()
        {
            // Offensive systems
            if (_offensiveSystemDropdown != null)
            {
                _offensiveSystemDropdown.ClearOptions();
                _offensiveSystemDropdown.AddOptions(new List<string>
                {
                    "Balanced",
                    "Motion Offense",
                    "Pick and Roll Heavy",
                    "Isolation",
                    "Post Up",
                    "Fast Break",
                    "Three Point Focus",
                    "Inside-Out"
                });
                _offensiveSystemDropdown.onValueChanged.AddListener(OnOffensiveSystemChanged);
            }

            // Defensive systems
            if (_defensiveSystemDropdown != null)
            {
                _defensiveSystemDropdown.ClearOptions();
                _defensiveSystemDropdown.AddOptions(new List<string>
                {
                    "Man-to-Man",
                    "2-3 Zone",
                    "3-2 Zone",
                    "1-3-1 Zone",
                    "Match-up Zone",
                    "Full Court Press",
                    "Switching Everything"
                });
                _defensiveSystemDropdown.onValueChanged.AddListener(OnDefensiveSystemChanged);
            }

            // Pace slider
            if (_paceSlider != null)
            {
                _paceSlider.minValue = 90;
                _paceSlider.maxValue = 110;
                _paceSlider.value = 100;
                _paceSlider.onValueChanged.AddListener(OnPaceChanged);
            }

            // Three point rate slider
            if (_threePointRateSlider != null)
            {
                _threePointRateSlider.minValue = 0.2f;
                _threePointRateSlider.maxValue = 0.5f;
                _threePointRateSlider.value = 0.35f;
                _threePointRateSlider.onValueChanged.AddListener(OnThreePointRateChanged);
            }
        }

        private void SetupButtons()
        {
            if (_playGameButton != null)
                _playGameButton.onClick.AddListener(OnPlayClicked);

            if (_simGameButton != null)
                _simGameButton.onClick.AddListener(OnSimClicked);

            if (_backButton != null)
                _backButton.onClick.AddListener(() => OnBackRequested?.Invoke());
        }

        protected override void OnShown()
        {
            base.OnShown();
            ShowTab(PreGameTab.Lineup);
        }

        /// <summary>
        /// Initialize the pre-game panel with match data
        /// </summary>
        public void Initialize(CalendarEvent game, Team playerTeam, Team opponentTeam, bool isHomeGame)
        {
            _currentGame = game;
            _playerTeam = playerTeam;
            _opponentTeam = opponentTeam;
            _isHomeGame = isHomeGame;

            // Copy current lineup
            if (_playerTeam?.StartingLineup != null)
            {
                Array.Copy(_playerTeam.StartingLineup, _selectedLineup, 5);
            }

            // Copy current strategy
            _selectedStrategy = _playerTeam?.Strategy ?? new TeamStrategy();

            // Get available players
            _availablePlayers = _playerTeam?.Roster?
                .Where(p => !p.IsInjured)
                .OrderByDescending(p => p.Overall)
                .ToList() ?? new List<Player>();

            RefreshUI();
        }

        private void RefreshUI()
        {
            RefreshGameInfo();
            RefreshLineup();
            RefreshTactics();
            RefreshScouting();
        }

        #region Tab Management

        private void ShowTab(PreGameTab tab)
        {
            _currentTab = tab;

            if (_lineupPanel != null)
                _lineupPanel.SetActive(tab == PreGameTab.Lineup);

            if (_tacticsPanel != null)
                _tacticsPanel.SetActive(tab == PreGameTab.Tactics);

            if (_scoutingPanel != null)
                _scoutingPanel.SetActive(tab == PreGameTab.Scouting);

            // Update tab button visuals
            UpdateTabButtonVisuals();
        }

        private void UpdateTabButtonVisuals()
        {
            SetTabButtonActive(_lineupTabButton, _currentTab == PreGameTab.Lineup);
            SetTabButtonActive(_tacticsTabButton, _currentTab == PreGameTab.Tactics);
            SetTabButtonActive(_scoutingTabButton, _currentTab == PreGameTab.Scouting);
        }

        private void SetTabButtonActive(Button button, bool active)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = active ? new Color(0.3f, 0.3f, 0.5f) : new Color(0.2f, 0.2f, 0.3f);
            button.colors = colors;
        }

        #endregion

        #region Game Info

        private void RefreshGameInfo()
        {
            if (_gameInfoText != null)
            {
                string gameNum = _currentGame?.GameNumber.ToString() ?? "";
                _gameInfoText.text = $"Game #{gameNum} - {_currentGame?.Date:MMM d, yyyy}";
            }

            if (_opponentNameText != null)
                _opponentNameText.text = $"{_opponentTeam?.City ?? ""} {_opponentTeam?.Name ?? "Unknown"}";

            if (_opponentRecordText != null)
                _opponentRecordText.text = $"({_opponentTeam?.Wins ?? 0}-{_opponentTeam?.Losses ?? 0})";

            if (_locationText != null)
                _locationText.text = _isHomeGame ? "HOME GAME" : "AWAY GAME";

            if (_broadcastText != null)
            {
                if (_currentGame?.IsNationalTV == true)
                    _broadcastText.text = $"National TV: {_currentGame.BroadcastNetwork}";
                else
                    _broadcastText.text = "Local Broadcast";
            }
        }

        #endregion

        #region Lineup

        private void RefreshLineup()
        {
            // Refresh starter slots
            if (_starterSlots != null)
            {
                string[] positions = { "PG", "SG", "SF", "PF", "C" };
                for (int i = 0; i < _starterSlots.Length && i < 5; i++)
                {
                    int playerIndex = _selectedLineup[i];
                    Player player = null;

                    if (playerIndex >= 0 && playerIndex < _availablePlayers.Count)
                    {
                        player = _availablePlayers[playerIndex];
                    }

                    _starterSlots[i]?.Setup(i, positions[i], player, OnStarterSlotClicked);
                }
            }

            // Refresh bench
            RefreshBench();
        }

        private void RefreshBench()
        {
            if (_benchContainer == null) return;

            // Clear existing
            foreach (var ui in _benchPlayerUIs)
            {
                if (ui != null) Destroy(ui.gameObject);
            }
            _benchPlayerUIs.Clear();

            // Get bench players (not in starting lineup)
            var starterIds = new HashSet<string>();
            for (int i = 0; i < 5; i++)
            {
                int idx = _selectedLineup[i];
                if (idx >= 0 && idx < _availablePlayers.Count)
                {
                    starterIds.Add(_availablePlayers[idx].PlayerId);
                }
            }

            var benchPlayers = _availablePlayers.Where(p => !starterIds.Contains(p.PlayerId)).ToList();

            foreach (var player in benchPlayers)
            {
                CreateBenchPlayerUI(player);
            }
        }

        private void CreateBenchPlayerUI(Player player)
        {
            GameObject playerGO;
            if (_benchPlayerPrefab != null)
            {
                playerGO = Instantiate(_benchPlayerPrefab, _benchContainer);
            }
            else
            {
                playerGO = CreateDefaultBenchPlayerUI(player);
            }

            var benchUI = playerGO.GetComponent<BenchPlayerUI>();
            if (benchUI == null)
            {
                benchUI = playerGO.AddComponent<BenchPlayerUI>();
            }

            benchUI.Setup(player, OnBenchPlayerClicked);
            _benchPlayerUIs.Add(benchUI);
        }

        private GameObject CreateDefaultBenchPlayerUI(Player player)
        {
            var playerGO = new GameObject($"BenchPlayer_{player.PlayerId}");
            playerGO.transform.SetParent(_benchContainer, false);

            var image = playerGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.25f);

            var button = playerGO.AddComponent<Button>();

            var layout = playerGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 40;
            layout.flexibleWidth = 1;

            // Player info text
            var textGO = new GameObject("PlayerInfo");
            textGO.transform.SetParent(playerGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.text = $"{player.PositionString} - {player.FirstName} {player.LastName} ({player.Overall})";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            return playerGO;
        }

        private int _selectedStarterSlot = -1;

        private void OnStarterSlotClicked(int slotIndex)
        {
            _selectedStarterSlot = slotIndex;
            // Highlight the slot for swap
        }

        private void OnBenchPlayerClicked(Player player)
        {
            if (_selectedStarterSlot >= 0 && _selectedStarterSlot < 5)
            {
                // Find player index
                int playerIndex = _availablePlayers.IndexOf(player);
                if (playerIndex >= 0)
                {
                    _selectedLineup[_selectedStarterSlot] = playerIndex;
                    _selectedStarterSlot = -1;
                    RefreshLineup();
                }
            }
        }

        #endregion

        #region Tactics

        private void RefreshTactics()
        {
            if (_selectedStrategy == null) return;

            if (_paceSlider != null)
            {
                _paceSlider.value = _selectedStrategy.Pace;
                OnPaceChanged(_selectedStrategy.Pace);
            }

            if (_threePointRateSlider != null)
            {
                _threePointRateSlider.value = _selectedStrategy.ThreePointRate;
                OnThreePointRateChanged(_selectedStrategy.ThreePointRate);
            }
        }

        private void OnOffensiveSystemChanged(int index)
        {
            // Map to offensive system enum
            // For now, just store the index
        }

        private void OnDefensiveSystemChanged(int index)
        {
            // Map to defensive system enum
        }

        private void OnPaceChanged(float value)
        {
            _selectedStrategy.Pace = Mathf.RoundToInt(value);

            if (_paceValueText != null)
            {
                string paceDesc = value > 105 ? "Fast" : value < 95 ? "Slow" : "Average";
                _paceValueText.text = $"{value:F0} ({paceDesc})";
            }
        }

        private void OnThreePointRateChanged(float value)
        {
            _selectedStrategy.ThreePointRate = value;

            if (_threePointRateText != null)
            {
                _threePointRateText.text = $"{value * 100:F0}%";
            }
        }

        #endregion

        #region Scouting

        private void RefreshScouting()
        {
            if (_opponentTeam == null) return;

            // Get scouting report from MatchController
            var scoutingReport = GameManager.Instance?.MatchController?.GetOpponentScouting();

            if (scoutingReport != null)
            {
                if (_opponentStyleText != null)
                {
                    string style = scoutingReport.Pace > 102 ? "Up-tempo" :
                                   scoutingReport.Pace < 98 ? "Slow-paced" : "Balanced";
                    _opponentStyleText.text = $"Style: {style}";
                }

                if (_opponentStrengthsText != null)
                    _opponentStrengthsText.text = $"Strengths:\n{string.Join("\n", scoutingReport.Strengths)}";

                if (_opponentWeaknessesText != null)
                    _opponentWeaknessesText.text = $"Weaknesses:\n{string.Join("\n", scoutingReport.Weaknesses)}";

                if (_recentFormText != null)
                    _recentFormText.text = $"Recent Form: {scoutingReport.RecentForm}";
            }

            // Show opponent starters
            RefreshOpponentStarters();
        }

        private void RefreshOpponentStarters()
        {
            if (_opponentStartersContainer == null || _opponentTeam?.Roster == null) return;

            // Clear existing
            foreach (Transform child in _opponentStartersContainer)
            {
                Destroy(child.gameObject);
            }

            // Get top 5 players by overall
            var starters = _opponentTeam.Roster
                .OrderByDescending(p => p.Overall)
                .Take(5)
                .ToList();

            foreach (var player in starters)
            {
                CreateOpponentPlayerEntry(player);
            }
        }

        private void CreateOpponentPlayerEntry(Player player)
        {
            var entryGO = new GameObject(player.LastName);
            entryGO.transform.SetParent(_opponentStartersContainer, false);

            var layout = entryGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 25;

            var text = entryGO.AddComponent<Text>();
            text.text = $"{player.PositionString} - {player.FirstName} {player.LastName} ({player.Overall})";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.color = Color.white;
        }

        #endregion

        #region Actions

        private void OnPlayClicked()
        {
            ApplySettings();
            OnPlayGameRequested?.Invoke(_currentGame, _selectedLineup, _selectedStrategy);
        }

        private void OnSimClicked()
        {
            ApplySettings();
            OnSimGameRequested?.Invoke(_currentGame, _selectedLineup, _selectedStrategy);
        }

        private void ApplySettings()
        {
            // Apply lineup and strategy to player team
            if (_playerTeam != null)
            {
                _playerTeam.StartingLineup = _selectedLineup;
                _playerTeam.Strategy = _selectedStrategy;
            }
        }

        #endregion

        private enum PreGameTab
        {
            Lineup,
            Tactics,
            Scouting
        }
    }

    /// <summary>
    /// UI component for lineup starter slots
    /// </summary>
    [Serializable]
    public class LineupSlotUI : MonoBehaviour
    {
        public int SlotIndex { get; private set; }
        public Player CurrentPlayer { get; private set; }

        [SerializeField] private Text _positionText;
        [SerializeField] private Text _playerNameText;
        [SerializeField] private Text _ratingText;
        [SerializeField] private Image _background;

        private Button _button;
        private Action<int> _onClick;

        public void Setup(int slotIndex, string position, Player player, Action<int> onClick)
        {
            SlotIndex = slotIndex;
            CurrentPlayer = player;
            _onClick = onClick;

            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _onClick?.Invoke(SlotIndex));
            }

            if (_positionText != null)
                _positionText.text = position;

            if (player != null)
            {
                if (_playerNameText != null)
                    _playerNameText.text = $"{player.FirstName} {player.LastName}";

                if (_ratingText != null)
                    _ratingText.text = player.Overall.ToString();
            }
            else
            {
                if (_playerNameText != null)
                    _playerNameText.text = "Empty";

                if (_ratingText != null)
                    _ratingText.text = "-";
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_background != null)
            {
                _background.color = highlighted
                    ? new Color(0.3f, 0.5f, 0.3f)
                    : new Color(0.2f, 0.2f, 0.3f);
            }
        }
    }

    /// <summary>
    /// UI component for bench players
    /// </summary>
    public class BenchPlayerUI : MonoBehaviour
    {
        public Player Player { get; private set; }

        private Button _button;
        private Action<Player> _onClick;

        public void Setup(Player player, Action<Player> onClick)
        {
            Player = player;
            _onClick = onClick;

            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _onClick?.Invoke(Player));
            }
        }
    }
}
