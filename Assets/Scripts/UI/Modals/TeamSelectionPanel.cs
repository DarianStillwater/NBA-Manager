using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// Modal panel for selecting an opponent team for advance scouting.
    /// Highlights upcoming opponents in the schedule.
    /// </summary>
    public class TeamSelectionPanel : SlidePanel
    {
        [Header("Header")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _instructionsText;

        [Header("Team List")]
        [SerializeField] private Transform _teamListContainer;
        [SerializeField] private GameObject _teamRowPrefab;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Filters")]
        [SerializeField] private Dropdown _conferenceFilter;
        [SerializeField] private Toggle _upcomingOnlyToggle;

        [Header("Buttons")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        [Header("Colors")]
        [SerializeField] private Color _upcomingOpponentColor = new Color(0.3f, 0.6f, 0.3f, 0.5f);
        [SerializeField] private Color _normalTeamColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

        // State
        private string _selectedTeamId;
        private List<TeamSelectionRow> _activeRows = new List<TeamSelectionRow>();
        private List<string> _upcomingOpponentIds = new List<string>();
        private Action<string> _onConfirm;
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

            if (_conferenceFilter != null)
            {
                _conferenceFilter.ClearOptions();
                _conferenceFilter.AddOptions(new List<string>
                {
                    "All Teams",
                    "Eastern Conference",
                    "Western Conference"
                });
                _conferenceFilter.onValueChanged.AddListener(OnFilterChanged);
            }

            if (_upcomingOnlyToggle != null)
                _upcomingOnlyToggle.onValueChanged.AddListener(OnUpcomingOnlyChanged);
        }

        /// <summary>
        /// Show the panel for opponent advance scouting.
        /// </summary>
        /// <param name="scout">The scout being assigned</param>
        /// <param name="onConfirm">Callback with selected team ID</param>
        /// <param name="onCancel">Called if cancelled</param>
        public void ShowForOpponentScouting(UnifiedCareerProfile scout, Action<string> onConfirm, Action onCancel = null)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _selectedTeamId = null;

            if (_titleText != null)
                _titleText.text = $"Assign Opponent Scouting to {scout.PersonName}";

            if (_instructionsText != null)
                _instructionsText.text = "Select an opponent team to scout. Upcoming opponents are highlighted.";

            // Get upcoming opponents from schedule
            LoadUpcomingOpponents();
            PopulateTeamList(GetAllTeams());
            UpdateConfirmButton();
            ShowSlide();
        }

        private void LoadUpcomingOpponents()
        {
            _upcomingOpponentIds.Clear();

            var schedule = GameManager.Instance?.ScheduleManager;
            var playerTeamId = GameManager.Instance?.PlayerTeamId;

            if (schedule != null && !string.IsNullOrEmpty(playerTeamId))
            {
                // Get next 5 games
                var upcomingGames = schedule.GetUpcomingGames(playerTeamId, 5);
                foreach (var game in upcomingGames)
                {
                    var opponentId = game.HomeTeamId == playerTeamId ? game.AwayTeamId : game.HomeTeamId;
                    if (!_upcomingOpponentIds.Contains(opponentId))
                        _upcomingOpponentIds.Add(opponentId);
                }
            }
        }

        private List<Team> GetAllTeams()
        {
            var teams = new List<Team>();
            var playerTeamId = GameManager.Instance?.PlayerTeamId;

            // Get all teams except player's team
            var allTeams = GameManager.Instance?.GetAllTeams();
            if (allTeams != null)
            {
                foreach (var team in allTeams)
                {
                    if (team.TeamId != playerTeamId)
                        teams.Add(team);
                }
            }

            return teams;
        }

        private void PopulateTeamList(List<Team> teams)
        {
            // Clear existing rows
            ClearTeamList();

            int filterValue = _conferenceFilter?.value ?? 0;
            bool upcomingOnly = _upcomingOnlyToggle?.isOn ?? false;

            // Sort: upcoming opponents first
            teams.Sort((a, b) => {
                bool aUpcoming = _upcomingOpponentIds.Contains(a.TeamId);
                bool bUpcoming = _upcomingOpponentIds.Contains(b.TeamId);
                if (aUpcoming && !bUpcoming) return -1;
                if (!aUpcoming && bUpcoming) return 1;
                return string.Compare(a.Name, b.Name);
            });

            foreach (var team in teams)
            {
                // Apply conference filter
                if (filterValue > 0 && !MatchesConferenceFilter(team, filterValue))
                    continue;

                // Apply upcoming only filter
                if (upcomingOnly && !_upcomingOpponentIds.Contains(team.TeamId))
                    continue;

                if (_teamRowPrefab != null)
                {
                    var rowObj = Instantiate(_teamRowPrefab, _teamListContainer);
                    var row = rowObj.GetComponent<TeamSelectionRow>();

                    if (row == null)
                        row = rowObj.AddComponent<TeamSelectionRow>();

                    bool isUpcoming = _upcomingOpponentIds.Contains(team.TeamId);
                    bool isSelected = team.TeamId == _selectedTeamId;
                    row.Setup(team, isUpcoming, isSelected, _upcomingOpponentColor, _normalTeamColor);
                    row.OnSelected += OnTeamSelected;
                    _activeRows.Add(row);
                }
            }
        }

        private void ClearTeamList()
        {
            foreach (var row in _activeRows)
            {
                row.OnSelected -= OnTeamSelected;
                Destroy(row.gameObject);
            }
            _activeRows.Clear();
        }

        private bool MatchesConferenceFilter(Team team, int filterValue)
        {
            return filterValue switch
            {
                1 => team.Conference == "Eastern",
                2 => team.Conference == "Western",
                _ => true
            };
        }

        private void OnTeamSelected(string teamId)
        {
            _selectedTeamId = teamId;

            // Update all rows selection state
            foreach (var row in _activeRows)
            {
                row.SetSelected(row.TeamId == teamId);
            }

            UpdateConfirmButton();
        }

        private void UpdateConfirmButton()
        {
            if (_confirmButton != null)
            {
                _confirmButton.interactable = !string.IsNullOrEmpty(_selectedTeamId);
            }
        }

        private void OnFilterChanged(int value)
        {
            PopulateTeamList(GetAllTeams());
        }

        private void OnUpcomingOnlyChanged(bool upcomingOnly)
        {
            PopulateTeamList(GetAllTeams());
        }

        private void OnConfirmClicked()
        {
            if (string.IsNullOrEmpty(_selectedTeamId)) return;

            var selectedId = _selectedTeamId;
            HideSlide(() => {
                _onConfirm?.Invoke(selectedId);
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
            ClearTeamList();
            _selectedTeamId = null;
            _onConfirm = null;
            _onCancel = null;
        }
    }

    /// <summary>
    /// Individual row for team selection.
    /// </summary>
    public class TeamSelectionRow : MonoBehaviour
    {
        private Text _teamNameText;
        private Text _recordText;
        private Text _conferenceText;
        private Image _upcomingIndicator;
        private Button _selectButton;
        private Image _backgroundImage;

        private string _teamId;
        private bool _isSetup;
        private Color _upcomingColor;
        private Color _normalColor;

        public string TeamId => _teamId;
        public event Action<string> OnSelected;

        public void Setup(Team team, bool isUpcoming, bool isSelected, Color upcomingColor, Color normalColor)
        {
            if (!_isSetup)
            {
                // Find components if not assigned
                _selectButton = GetComponentInChildren<Button>();
                var texts = GetComponentsInChildren<Text>();
                if (texts.Length >= 1) _teamNameText = texts[0];
                if (texts.Length >= 2) _recordText = texts[1];
                if (texts.Length >= 3) _conferenceText = texts[2];
                _backgroundImage = GetComponent<Image>();

                var images = GetComponentsInChildren<Image>();
                if (images.Length > 1)
                    _upcomingIndicator = images[1];

                if (_selectButton != null)
                    _selectButton.onClick.AddListener(OnButtonClicked);

                _isSetup = true;
            }

            _teamId = team.TeamId;
            _upcomingColor = upcomingColor;
            _normalColor = normalColor;

            if (_teamNameText != null)
                _teamNameText.text = team.Name;

            if (_recordText != null)
                _recordText.text = $"{team.Wins}-{team.Losses}";

            if (_conferenceText != null)
                _conferenceText.text = team.Conference ?? "";

            if (_upcomingIndicator != null)
            {
                _upcomingIndicator.enabled = isUpcoming;
                _upcomingIndicator.color = new Color(0.2f, 0.8f, 0.2f); // Green indicator
            }

            UpdateBackground(isUpcoming, isSelected);
        }

        public void SetSelected(bool selected)
        {
            bool isUpcoming = _upcomingIndicator?.enabled ?? false;
            UpdateBackground(isUpcoming, selected);
        }

        private void OnButtonClicked()
        {
            OnSelected?.Invoke(_teamId);
        }

        private void UpdateBackground(bool isUpcoming, bool isSelected)
        {
            if (_backgroundImage != null)
            {
                if (isSelected)
                    _backgroundImage.color = new Color(0.3f, 0.5f, 0.7f, 0.7f); // Selected
                else if (isUpcoming)
                    _backgroundImage.color = _upcomingColor;
                else
                    _backgroundImage.color = _normalColor;
            }
        }

        private void OnDestroy()
        {
            if (_selectButton != null)
                _selectButton.onClick.RemoveListener(OnButtonClicked);
        }
    }
}
