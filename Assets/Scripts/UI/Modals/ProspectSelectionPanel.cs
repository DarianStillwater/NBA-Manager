using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// Modal panel for selecting draft prospects for scouting assignments.
    /// Allows scouts to be assigned to evaluate specific draft prospects.
    /// </summary>
    public class ProspectSelectionPanel : SlidePanel
    {
        [Header("Header")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _instructionsText;
        [SerializeField] private Text _capacityText;

        [Header("Prospect List")]
        [SerializeField] private Transform _prospectListContainer;
        [SerializeField] private GameObject _prospectRowPrefab;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Filters")]
        [SerializeField] private Dropdown _positionFilter;
        [SerializeField] private Dropdown _regionFilter;
        [SerializeField] private Dropdown _projectionFilter;

        [Header("Buttons")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        // State
        private List<string> _selectedProspectIds = new List<string>();
        private int _maxSelections = 5;
        private List<ProspectSelectionRow> _activeRows = new List<ProspectSelectionRow>();
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
                    "Point Guards",
                    "Shooting Guards",
                    "Small Forwards",
                    "Power Forwards",
                    "Centers"
                });
                _positionFilter.onValueChanged.AddListener(OnFilterChanged);
            }

            if (_regionFilter != null)
            {
                _regionFilter.ClearOptions();
                _regionFilter.AddOptions(new List<string>
                {
                    "All Regions",
                    "College",
                    "International"
                });
                _regionFilter.onValueChanged.AddListener(OnFilterChanged);
            }

            if (_projectionFilter != null)
            {
                _projectionFilter.ClearOptions();
                _projectionFilter.AddOptions(new List<string>
                {
                    "All Prospects",
                    "Lottery (1-14)",
                    "First Round (15-30)",
                    "Second Round (31-60)",
                    "Undrafted"
                });
                _projectionFilter.onValueChanged.AddListener(OnFilterChanged);
            }
        }

        /// <summary>
        /// Show the panel for prospect scouting assignment.
        /// </summary>
        /// <param name="scout">The scout being assigned</param>
        /// <param name="maxProspects">Maximum prospects to scout</param>
        /// <param name="onConfirm">Callback with selected prospect IDs</param>
        /// <param name="onCancel">Called if cancelled</param>
        public void ShowForProspectScouting(UnifiedCareerProfile scout, int maxProspects, Action<List<string>> onConfirm, Action onCancel = null)
        {
            _maxSelections = maxProspects;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _selectedProspectIds.Clear();

            if (_titleText != null)
                _titleText.text = $"Assign Prospects to {scout.PersonName}";

            if (_instructionsText != null)
                _instructionsText.text = $"Select up to {maxProspects} draft prospects to scout.";

            PopulateProspectList();
            UpdateCapacityDisplay();
            ShowSlide();
        }

        private void PopulateProspectList()
        {
            // Clear existing rows
            ClearProspectList();

            // Get prospects from DraftManager
            var prospects = GetAvailableProspects();

            // Apply filters
            int posFilter = _positionFilter?.value ?? 0;
            int regionFilter = _regionFilter?.value ?? 0;
            int projFilter = _projectionFilter?.value ?? 0;

            foreach (var prospect in prospects)
            {
                // Apply position filter
                if (posFilter > 0 && !MatchesPositionFilter(prospect, posFilter))
                    continue;

                // Apply region filter
                if (regionFilter > 0 && !MatchesRegionFilter(prospect, regionFilter))
                    continue;

                // Apply projection filter
                if (projFilter > 0 && !MatchesProjectionFilter(prospect, projFilter))
                    continue;

                if (_prospectRowPrefab != null)
                {
                    var rowObj = Instantiate(_prospectRowPrefab, _prospectListContainer);
                    var row = rowObj.GetComponent<ProspectSelectionRow>();

                    if (row == null)
                        row = rowObj.AddComponent<ProspectSelectionRow>();

                    row.Setup(prospect, _selectedProspectIds.Contains(prospect.ProspectId));
                    row.OnToggled += OnProspectToggled;
                    _activeRows.Add(row);
                }
            }
        }

        private List<ProspectInfo> GetAvailableProspects()
        {
            var prospects = new List<ProspectInfo>();

            // Try to get prospects from DraftSystem
            var draftManager = GameManager.Instance?.DraftSystem;
            if (draftManager != null)
            {
                var draftProspects = draftManager.GetCurrentDraftClass();
                if (draftProspects != null)
                {
                    foreach (var dp in draftProspects)
                    {
                        prospects.Add(new ProspectInfo
                        {
                            ProspectId = dp.ProspectId,
                            FullName = dp.FullName,
                            Position = dp.Position.ToString(),
                            Age = dp.Age,
                            College = dp.College,
                            Country = dp.Country,
                            IsInternational = dp.IsInternational,
                            MockDraftPosition = dp.MockDraftPosition
                        });
                    }
                }
            }

            // Sort by mock draft position
            prospects.Sort((a, b) => a.MockDraftPosition.CompareTo(b.MockDraftPosition));

            return prospects;
        }

        private void ClearProspectList()
        {
            foreach (var row in _activeRows)
            {
                row.OnToggled -= OnProspectToggled;
                Destroy(row.gameObject);
            }
            _activeRows.Clear();
        }

        private bool MatchesPositionFilter(ProspectInfo prospect, int filterValue)
        {
            var pos = prospect.Position?.ToUpper() ?? "";
            return filterValue switch
            {
                1 => pos == "PG" || pos.Contains("POINT"),
                2 => pos == "SG" || pos.Contains("SHOOTING"),
                3 => pos == "SF" || pos.Contains("SMALL"),
                4 => pos == "PF" || pos.Contains("POWER"),
                5 => pos == "C" || pos.Contains("CENTER"),
                _ => true
            };
        }

        private bool MatchesRegionFilter(ProspectInfo prospect, int filterValue)
        {
            return filterValue switch
            {
                1 => !prospect.IsInternational,  // College
                2 => prospect.IsInternational,    // International
                _ => true
            };
        }

        private bool MatchesProjectionFilter(ProspectInfo prospect, int filterValue)
        {
            int pick = prospect.MockDraftPosition;
            return filterValue switch
            {
                1 => pick >= 1 && pick <= 14,     // Lottery
                2 => pick >= 15 && pick <= 30,    // First Round
                3 => pick >= 31 && pick <= 60,    // Second Round
                4 => pick > 60,                    // Undrafted
                _ => true
            };
        }

        private void OnProspectToggled(string prospectId, bool isSelected)
        {
            if (isSelected)
            {
                if (_selectedProspectIds.Count < _maxSelections && !_selectedProspectIds.Contains(prospectId))
                {
                    _selectedProspectIds.Add(prospectId);
                }
                else if (_selectedProspectIds.Count >= _maxSelections)
                {
                    // At capacity - find the row and unselect it
                    var row = _activeRows.Find(r => r.ProspectId == prospectId);
                    row?.SetSelected(false);
                }
            }
            else
            {
                _selectedProspectIds.Remove(prospectId);
            }

            UpdateCapacityDisplay();
            UpdateConfirmButton();
        }

        private void UpdateCapacityDisplay()
        {
            if (_capacityText != null)
            {
                _capacityText.text = $"{_selectedProspectIds.Count} / {_maxSelections} Selected";

                // Color based on capacity
                if (_selectedProspectIds.Count >= _maxSelections)
                    _capacityText.color = new Color(0.9f, 0.7f, 0.2f); // Yellow - at capacity
                else if (_selectedProspectIds.Count > 0)
                    _capacityText.color = new Color(0.3f, 0.8f, 0.3f); // Green - has selections
                else
                    _capacityText.color = Color.white;
            }
        }

        private void UpdateConfirmButton()
        {
            if (_confirmButton != null)
            {
                _confirmButton.interactable = _selectedProspectIds.Count > 0;
            }
        }

        private void OnFilterChanged(int value)
        {
            PopulateProspectList();
        }

        private void OnConfirmClicked()
        {
            var selectedIds = new List<string>(_selectedProspectIds);
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
            ClearProspectList();
            _selectedProspectIds.Clear();
            _onConfirm = null;
            _onCancel = null;
        }
    }

    /// <summary>
    /// Simplified prospect info for selection display.
    /// </summary>
    public class ProspectInfo
    {
        public string ProspectId;
        public string FullName;
        public string Position;
        public int Age;
        public string College;
        public string Country;
        public bool IsInternational;
        public int MockDraftPosition;
    }

    /// <summary>
    /// Individual row for prospect selection.
    /// </summary>
    public class ProspectSelectionRow : MonoBehaviour
    {
        private Text _nameText;
        private Text _positionText;
        private Text _schoolText;
        private Text _projectionText;
        private Toggle _selectionToggle;
        private Image _backgroundImage;

        private string _prospectId;
        private bool _isSetup;

        public string ProspectId => _prospectId;
        public event Action<string, bool> OnToggled;

        public void Setup(ProspectInfo prospect, bool isSelected)
        {
            if (!_isSetup)
            {
                // Find components if not assigned
                _selectionToggle = GetComponentInChildren<Toggle>();
                var texts = GetComponentsInChildren<Text>();
                if (texts.Length >= 1) _nameText = texts[0];
                if (texts.Length >= 2) _positionText = texts[1];
                if (texts.Length >= 3) _schoolText = texts[2];
                if (texts.Length >= 4) _projectionText = texts[3];
                _backgroundImage = GetComponent<Image>();

                if (_selectionToggle != null)
                    _selectionToggle.onValueChanged.AddListener(OnToggleChanged);

                _isSetup = true;
            }

            _prospectId = prospect.ProspectId;

            if (_nameText != null)
                _nameText.text = prospect.FullName;

            if (_positionText != null)
                _positionText.text = prospect.Position;

            if (_schoolText != null)
                _schoolText.text = prospect.IsInternational ? prospect.Country : prospect.College;

            if (_projectionText != null)
            {
                int pick = prospect.MockDraftPosition;
                _projectionText.text = pick <= 14 ? $"#{pick} (Lottery)" :
                                        pick <= 30 ? $"#{pick} (1st Rd)" :
                                        pick <= 60 ? $"#{pick} (2nd Rd)" :
                                        "Undrafted";
            }

            if (_selectionToggle != null)
            {
                _selectionToggle.SetIsOnWithoutNotify(isSelected);
            }

            UpdateBackground(isSelected, prospect.MockDraftPosition);
        }

        public void SetSelected(bool selected)
        {
            if (_selectionToggle != null)
                _selectionToggle.SetIsOnWithoutNotify(selected);

            // Just update with default mock position for color
            UpdateBackground(selected, 30);
        }

        private void OnToggleChanged(bool isOn)
        {
            UpdateBackground(isOn, 30);
            OnToggled?.Invoke(_prospectId, isOn);
        }

        private void UpdateBackground(bool isSelected, int mockPosition)
        {
            if (_backgroundImage != null)
            {
                if (isSelected)
                {
                    _backgroundImage.color = new Color(0.2f, 0.4f, 0.6f, 0.5f);  // Selected highlight
                }
                else if (mockPosition <= 5)
                {
                    _backgroundImage.color = new Color(0.3f, 0.5f, 0.3f, 0.3f);  // Top 5 highlight
                }
                else if (mockPosition <= 14)
                {
                    _backgroundImage.color = new Color(0.3f, 0.4f, 0.5f, 0.3f);  // Lottery highlight
                }
                else
                {
                    _backgroundImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);  // Default
                }
            }
        }

        private void OnDestroy()
        {
            if (_selectionToggle != null)
                _selectionToggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }
}
