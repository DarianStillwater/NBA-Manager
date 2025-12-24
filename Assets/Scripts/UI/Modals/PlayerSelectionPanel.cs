using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// Modal panel for selecting players when assigning staff.
    /// Used for coach development assignments and scout player evaluations.
    /// </summary>
    public class PlayerSelectionPanel : SlidePanel
    {
        [Header("Header")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _instructionsText;
        [SerializeField] private Text _capacityText;

        [Header("Player List")]
        [SerializeField] private Transform _playerListContainer;
        [SerializeField] private GameObject _playerRowPrefab;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Filters")]
        [SerializeField] private Dropdown _positionFilter;
        [SerializeField] private Toggle _showAllToggle;

        [Header("Buttons")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        // State
        private List<string> _selectedPlayerIds = new List<string>();
        private int _maxSelections = 5;
        private List<PlayerSelectionRow> _activeRows = new List<PlayerSelectionRow>();
        private Action<List<string>> _onConfirm;
        private Action _onCancel;

        protected override void Awake()
        {
            base.Awake();
            SetupControls();
        }

        private void SetupControls()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);

            if (_positionFilter != null)
            {
                _positionFilter.ClearOptions();
                _positionFilter.AddOptions(new List<string>
                {
                    "All Positions",
                    "Guards",
                    "Forwards",
                    "Centers"
                });
                _positionFilter.onValueChanged.AddListener(OnFilterChanged);
            }

            if (_showAllToggle != null)
                _showAllToggle.onValueChanged.AddListener(OnShowAllChanged);
        }

        /// <summary>
        /// Show the panel for coach player development assignment.
        /// </summary>
        /// <param name="coach">The coach being assigned</param>
        /// <param name="maxPlayers">Maximum players the coach can develop</param>
        /// <param name="onConfirm">Callback with selected player IDs</param>
        /// <param name="onCancel">Called if cancelled</param>
        public void ShowForCoachAssignment(UnifiedCareerProfile coach, int maxPlayers, Action<List<string>> onConfirm, Action onCancel = null)
        {
            _maxSelections = maxPlayers;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _selectedPlayerIds.Clear();

            if (_titleText != null)
                _titleText.text = $"Assign Players to {coach.PersonName}";

            if (_instructionsText != null)
                _instructionsText.text = $"Select up to {maxPlayers} players for development focus.";

            PopulatePlayerList(GetTeamPlayers());
            UpdateCapacityDisplay();
            ShowSlide();
        }

        /// <summary>
        /// Show the panel for scout player evaluation assignment.
        /// </summary>
        /// <param name="scout">The scout being assigned</param>
        /// <param name="maxPlayers">Maximum players to evaluate</param>
        /// <param name="onConfirm">Callback with selected player IDs</param>
        /// <param name="onCancel">Called if cancelled</param>
        public void ShowForScoutEvaluation(UnifiedCareerProfile scout, int maxPlayers, Action<List<string>> onConfirm, Action onCancel = null)
        {
            _maxSelections = maxPlayers;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _selectedPlayerIds.Clear();

            if (_titleText != null)
                _titleText.text = $"Assign Players to {scout.PersonName}";

            if (_instructionsText != null)
                _instructionsText.text = $"Select up to {maxPlayers} players to scout.";

            PopulatePlayerList(GetTeamPlayers());
            UpdateCapacityDisplay();
            ShowSlide();
        }

        private List<Player> GetTeamPlayers()
        {
            var players = new List<Player>();
            var teamId = GameManager.Instance?.PlayerTeamId;

            if (!string.IsNullOrEmpty(teamId))
            {
                var team = GameManager.Instance?.GetTeam(teamId);
                if (team?.Roster != null)
                {
                    players.AddRange(team.Roster);
                }
            }

            return players;
        }

        private void PopulatePlayerList(List<Player> players)
        {
            // Clear existing rows
            ClearPlayerList();

            int filterValue = _positionFilter?.value ?? 0;

            foreach (var player in players)
            {
                // Apply position filter
                if (filterValue > 0 && !MatchesPositionFilter(player, filterValue))
                    continue;

                if (_playerRowPrefab != null)
                {
                    var rowObj = Instantiate(_playerRowPrefab, _playerListContainer);
                    var row = rowObj.GetComponent<PlayerSelectionRow>();

                    if (row == null)
                        row = rowObj.AddComponent<PlayerSelectionRow>();

                    row.Setup(player, _selectedPlayerIds.Contains(player.Id));
                    row.OnToggled += OnPlayerToggled;
                    _activeRows.Add(row);
                }
            }
        }

        private void ClearPlayerList()
        {
            foreach (var row in _activeRows)
            {
                row.OnToggled -= OnPlayerToggled;
                Destroy(row.gameObject);
            }
            _activeRows.Clear();
        }

        private bool MatchesPositionFilter(Player player, int filterValue)
        {
            var pos = player.PrimaryPosition;
            return filterValue switch
            {
                1 => pos == "PG" || pos == "SG" || pos == "G",  // Guards
                2 => pos == "SF" || pos == "PF" || pos == "F",  // Forwards
                3 => pos == "C",                                  // Centers
                _ => true
            };
        }

        private void OnPlayerToggled(string playerId, bool isSelected)
        {
            if (isSelected)
            {
                if (_selectedPlayerIds.Count < _maxSelections && !_selectedPlayerIds.Contains(playerId))
                {
                    _selectedPlayerIds.Add(playerId);
                }
                else if (_selectedPlayerIds.Count >= _maxSelections)
                {
                    // At capacity - find the row and unselect it
                    var row = _activeRows.Find(r => r.PlayerId == playerId);
                    row?.SetSelected(false);
                }
            }
            else
            {
                _selectedPlayerIds.Remove(playerId);
            }

            UpdateCapacityDisplay();
            UpdateConfirmButton();
        }

        private void UpdateCapacityDisplay()
        {
            if (_capacityText != null)
            {
                _capacityText.text = $"{_selectedPlayerIds.Count} / {_maxSelections} Selected";

                // Color based on capacity
                if (_selectedPlayerIds.Count >= _maxSelections)
                    _capacityText.color = new Color(0.9f, 0.7f, 0.2f); // Yellow - at capacity
                else if (_selectedPlayerIds.Count > 0)
                    _capacityText.color = new Color(0.3f, 0.8f, 0.3f); // Green - has selections
                else
                    _capacityText.color = Color.white;
            }
        }

        private void UpdateConfirmButton()
        {
            if (_confirmButton != null)
            {
                _confirmButton.interactable = _selectedPlayerIds.Count > 0;
            }
        }

        private void OnFilterChanged(int value)
        {
            PopulatePlayerList(GetTeamPlayers());
        }

        private void OnShowAllChanged(bool showAll)
        {
            PopulatePlayerList(GetTeamPlayers());
        }

        private void OnConfirmClicked()
        {
            var selectedIds = new List<string>(_selectedPlayerIds);
            HideSlide(() => {
                _onConfirm?.Invoke(selectedIds);
            });
        }

        private void OnCancelClicked()
        {
            HideSlide(() => {
                _onCancel?.Invoke();
            });
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            ClearPlayerList();
            _selectedPlayerIds.Clear();
            _onConfirm = null;
            _onCancel = null;
        }
    }

    /// <summary>
    /// Individual row for player selection.
    /// </summary>
    public class PlayerSelectionRow : MonoBehaviour
    {
        private Text _nameText;
        private Text _positionText;
        private Text _ageText;
        private Toggle _selectionToggle;
        private Image _backgroundImage;

        private string _playerId;
        private bool _isSetup;

        public string PlayerId => _playerId;
        public event Action<string, bool> OnToggled;

        public void Setup(Player player, bool isSelected)
        {
            if (!_isSetup)
            {
                // Find components if not assigned
                _selectionToggle = GetComponentInChildren<Toggle>();
                var texts = GetComponentsInChildren<Text>();
                if (texts.Length >= 1) _nameText = texts[0];
                if (texts.Length >= 2) _positionText = texts[1];
                if (texts.Length >= 3) _ageText = texts[2];
                _backgroundImage = GetComponent<Image>();

                if (_selectionToggle != null)
                    _selectionToggle.onValueChanged.AddListener(OnToggleChanged);

                _isSetup = true;
            }

            _playerId = player.Id;

            if (_nameText != null)
                _nameText.text = player.FullName;

            if (_positionText != null)
                _positionText.text = player.PrimaryPosition;

            if (_ageText != null)
                _ageText.text = $"Age {player.Age}";

            if (_selectionToggle != null)
            {
                _selectionToggle.SetIsOnWithoutNotify(isSelected);
            }

            UpdateBackground(isSelected);
        }

        public void SetSelected(bool selected)
        {
            if (_selectionToggle != null)
                _selectionToggle.SetIsOnWithoutNotify(selected);

            UpdateBackground(selected);
        }

        private void OnToggleChanged(bool isOn)
        {
            UpdateBackground(isOn);
            OnToggled?.Invoke(_playerId, isOn);
        }

        private void UpdateBackground(bool isSelected)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = isSelected
                    ? new Color(0.2f, 0.4f, 0.6f, 0.5f)  // Selected highlight
                    : new Color(0.1f, 0.1f, 0.1f, 0.5f); // Default
            }
        }

        private void OnDestroy()
        {
            if (_selectionToggle != null)
                _selectionToggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }
}
