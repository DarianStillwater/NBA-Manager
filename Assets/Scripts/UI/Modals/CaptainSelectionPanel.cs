using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// Modal panel for selecting a team captain.
    /// Shown at the start of the season or when the current captain is traded/cut.
    /// This is a MANDATORY selection - cannot be dismissed without choosing.
    /// </summary>
    public class CaptainSelectionPanel : SlidePanel
    {
        [Header("Header")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _instructionsText;

        [Header("Player List")]
        [SerializeField] private Transform _playerListContainer;
        [SerializeField] private GameObject _playerRowPrefab;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Selection Info")]
        [SerializeField] private Text _selectedPlayerText;
        [SerializeField] private Text _leadershipWarningText;

        [Header("Buttons")]
        [SerializeField] private Button _confirmButton;
        // No cancel button - selection is mandatory

        [Header("Staff Recommendation Thresholds (Hidden)")]
        [SerializeField] private int _topChoiceThreshold = 75;
        [SerializeField] private int _recommendedThreshold = 65;
        [SerializeField] private int _potentialThreshold = 55;

        [Header("Colors")]
        [SerializeField] private Color _topChoiceColor = new Color(1f, 0.84f, 0f);           // Gold
        [SerializeField] private Color _recommendedColor = new Color(0.3f, 0.8f, 0.3f);      // Green
        [SerializeField] private Color _cautionColor = new Color(0.9f, 0.7f, 0.2f);          // Yellow
        [SerializeField] private Color _concernColor = new Color(0.8f, 0.3f, 0.3f);          // Red

        // State
        private string _selectedPlayerId;
        private List<CaptainSelectionRow> _activeRows = new List<CaptainSelectionRow>();
        private Action<string> _onCaptainSelected;
        private Team _team;

        protected override void Awake()
        {
            base.Awake();
            SetupControls();
        }

        private void SetupControls()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            // Disable confirm initially
            UpdateConfirmButton();
        }

        /// <summary>
        /// Show the panel for captain selection.
        /// </summary>
        /// <param name="team">The team to select captain for</param>
        /// <param name="onCaptainSelected">Callback with selected player ID</param>
        /// <param name="isReplacement">True if replacing a traded/cut captain</param>
        public void ShowForCaptainSelection(Team team, Action<string> onCaptainSelected, bool isReplacement = false)
        {
            _team = team;
            _onCaptainSelected = onCaptainSelected;
            _selectedPlayerId = null;

            if (_titleText != null)
            {
                _titleText.text = isReplacement
                    ? "Select New Team Captain"
                    : "Select Team Captain";
            }

            if (_instructionsText != null)
            {
                _instructionsText.text = isReplacement
                    ? "Your captain has left the team. Your coaching staff has reviewed potential replacements below."
                    : "Your coaching staff has evaluated the roster and identified players with leadership qualities. Select a captain for the upcoming season.";
            }

            if (_selectedPlayerText != null)
                _selectedPlayerText.text = "No player selected";

            if (_leadershipWarningText != null)
                _leadershipWarningText.gameObject.SetActive(false);

            PopulatePlayerList();
            UpdateConfirmButton();
            ShowSlide();
        }

        private void PopulatePlayerList()
        {
            ClearPlayerList();

            if (_team?.Roster == null) return;

            // Sort players by Leadership (highest first) - but don't expose the numbers
            var sortedPlayers = _team.Roster
                .OrderByDescending(p => p.Leadership)
                .ToList();

            foreach (var player in sortedPlayers)
            {
                CreatePlayerRow(player);
            }
        }

        private void CreatePlayerRow(Player player)
        {
            if (_playerRowPrefab != null && _playerListContainer != null)
            {
                var rowObj = Instantiate(_playerRowPrefab, _playerListContainer);
                var row = rowObj.GetComponent<CaptainSelectionRow>();

                if (row == null)
                    row = rowObj.AddComponent<CaptainSelectionRow>();

                // Get staff recommendation based on hidden leadership stat
                var (recommendationText, recommendationColor) = GetStaffRecommendation(player.Leadership);

                row.Setup(
                    player,
                    recommendationText,
                    recommendationColor,
                    player.PlayerId == _selectedPlayerId
                );
                row.OnSelected += OnPlayerSelected;
                _activeRows.Add(row);
            }
        }

        /// <summary>
        /// Get coaching staff recommendation text based on hidden leadership attribute.
        /// Returns qualitative assessment, not numerical values.
        /// </summary>
        private (string text, Color color) GetStaffRecommendation(int leadership)
        {
            if (leadership >= _topChoiceThreshold)
                return ("Staff's Top Choice", _topChoiceColor);
            if (leadership >= _recommendedThreshold)
                return ("Recommended by Staff", _recommendedColor);
            if (leadership >= _potentialThreshold)
                return ("Shows Leadership Potential", Color.white);
            return ("", Color.clear); // No recommendation for low leadership players
        }

        private void ClearPlayerList()
        {
            foreach (var row in _activeRows)
            {
                row.OnSelected -= OnPlayerSelected;
                Destroy(row.gameObject);
            }
            _activeRows.Clear();
        }

        private void OnPlayerSelected(string playerId)
        {
            _selectedPlayerId = playerId;

            // Update all row selection states
            foreach (var row in _activeRows)
            {
                row.SetSelected(row.PlayerId == playerId);
            }

            // Update selection info
            var player = _team?.Roster?.Find(p => p.PlayerId == playerId);
            if (player != null)
            {
                if (_selectedPlayerText != null)
                    _selectedPlayerText.text = $"Selected: {player.FullName}";

                // Show qualitative staff feedback based on hidden leadership
                if (_leadershipWarningText != null)
                {
                    if (player.Leadership < _potentialThreshold)
                    {
                        // Staff has serious concerns
                        _leadershipWarningText.text = $"Your coaching staff has concerns about {player.FirstName}'s ability to command the locker room. Consider a more experienced leader.";
                        _leadershipWarningText.color = _concernColor;
                        _leadershipWarningText.gameObject.SetActive(true);
                    }
                    else if (player.Leadership < _recommendedThreshold)
                    {
                        // Staff sees potential but has reservations
                        _leadershipWarningText.text = $"Your coaching staff notes that {player.FirstName} is still developing as a vocal leader, but has the respect of teammates.";
                        _leadershipWarningText.color = _cautionColor;
                        _leadershipWarningText.gameObject.SetActive(true);
                    }
                    else
                    {
                        // Good choice - no warning needed
                        _leadershipWarningText.gameObject.SetActive(false);
                    }
                }
            }

            UpdateConfirmButton();
        }

        private void UpdateConfirmButton()
        {
            if (_confirmButton != null)
            {
                _confirmButton.interactable = !string.IsNullOrEmpty(_selectedPlayerId);
            }
        }

        private void OnConfirmClicked()
        {
            if (string.IsNullOrEmpty(_selectedPlayerId)) return;

            string selectedId = _selectedPlayerId;
            HideSlide(() => {
                _onCaptainSelected?.Invoke(selectedId);
            });
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            ClearPlayerList();
            _selectedPlayerId = null;
            _onCaptainSelected = null;
            _team = null;
        }
    }

    /// <summary>
    /// Individual row for captain selection.
    /// Shows player info with coaching staff recommendation (no numerical attributes).
    /// </summary>
    public class CaptainSelectionRow : MonoBehaviour
    {
        private Text _nameText;
        private Text _experienceText;
        private Text _staffRecommendationText;
        private Button _selectButton;
        private Image _backgroundImage;

        private string _playerId;
        private bool _isSetup;

        public string PlayerId => _playerId;
        public event Action<string> OnSelected;

        /// <summary>
        /// Setup the row with player info and staff recommendation.
        /// No numerical attributes are displayed - only qualitative staff assessment.
        /// </summary>
        public void Setup(Player player, string staffRecommendation, Color recommendationColor, bool isSelected)
        {
            if (!_isSetup)
            {
                _selectButton = GetComponentInChildren<Button>();
                var texts = GetComponentsInChildren<Text>();
                if (texts.Length >= 1) _nameText = texts[0];
                if (texts.Length >= 2) _experienceText = texts[1];
                if (texts.Length >= 3) _staffRecommendationText = texts[2];
                _backgroundImage = GetComponent<Image>();

                if (_selectButton != null)
                    _selectButton.onClick.AddListener(OnButtonClicked);

                _isSetup = true;
            }

            _playerId = player.PlayerId;

            if (_nameText != null)
                _nameText.text = $"{player.FullName} ({player.PrimaryPosition})";

            // Show experience instead of numerical leadership
            if (_experienceText != null)
            {
                int yearsExp = player.YearsInLeague;
                _experienceText.text = yearsExp switch
                {
                    0 => "Rookie",
                    1 => "1 year experience",
                    _ => $"{yearsExp}-year veteran"
                };
            }

            // Staff recommendation (qualitative, not numerical)
            if (_staffRecommendationText != null)
            {
                if (!string.IsNullOrEmpty(staffRecommendation))
                {
                    _staffRecommendationText.text = staffRecommendation;
                    _staffRecommendationText.color = recommendationColor;
                    _staffRecommendationText.gameObject.SetActive(true);
                }
                else
                {
                    _staffRecommendationText.gameObject.SetActive(false);
                }
            }

            SetSelected(isSelected);
        }

        public void SetSelected(bool selected)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = selected
                    ? new Color(0.2f, 0.5f, 0.3f, 0.6f)  // Selected - green tint
                    : new Color(0.1f, 0.1f, 0.1f, 0.5f); // Default
            }
        }

        private void OnButtonClicked()
        {
            OnSelected?.Invoke(_playerId);
        }

        private void OnDestroy()
        {
            if (_selectButton != null)
                _selectButton.onClick.RemoveListener(OnButtonClicked);
        }
    }
}
