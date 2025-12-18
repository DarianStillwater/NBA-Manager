using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Draft panel for the NBA Draft experience
    /// </summary>
    public class DraftPanel : BasePanel
    {
        [Header("Draft Info")]
        [SerializeField] private Text _draftTitleText;
        [SerializeField] private Text _currentPickText;
        [SerializeField] private Text _onTheClockText;
        [SerializeField] private Text _timerText;

        [Header("Prospect List")]
        [SerializeField] private Transform _prospectListContainer;
        [SerializeField] private Dropdown _sortDropdown;
        [SerializeField] private Dropdown _positionFilter;
        [SerializeField] private InputField _searchInput;

        [Header("Prospect Detail")]
        [SerializeField] private GameObject _prospectDetailPanel;
        [SerializeField] private Text _prospectNameText;
        [SerializeField] private Text _prospectPositionText;
        [SerializeField] private Text _prospectAgeText;
        [SerializeField] private Text _prospectCollegeText;
        [SerializeField] private Text _prospectOverallText;
        [SerializeField] private Text _prospectProjectionText;
        [SerializeField] private Transform _prospectAttributesContainer;
        [SerializeField] private Text _scoutingReportText;

        [Header("Team Needs")]
        [SerializeField] private Transform _teamNeedsContainer;
        [SerializeField] private Text _teamNeedsSummaryText;

        [Header("Draft Board")]
        [SerializeField] private Transform _draftBoardContainer;
        [SerializeField] private Text _yourNextPickText;

        [Header("Draft Actions")]
        [SerializeField] private Button _draftPlayerButton;
        [SerializeField] private Button _autoDraftButton;
        [SerializeField] private Button _tradePickButton;
        [SerializeField] private Button _simToPickButton;

        [Header("Mock Draft")]
        [SerializeField] private Toggle _showMockDraftToggle;
        [SerializeField] private Transform _mockDraftContainer;

        // State
        private List<DraftProspect> _allProspects = new List<DraftProspect>();
        private List<DraftProspect> _availableProspects = new List<DraftProspect>();
        private List<DraftSelection> _draftResults = new List<DraftSelection>();
        private DraftProspect _selectedProspect;
        private int _currentPick = 1;
        private int _totalPicks = 60;
        private bool _isDrafting = false;
        private string _playerTeamId;
        private List<int> _playerPicks = new List<int>();

        // Events
        public event Action<DraftProspect> OnPlayerDrafted;
        public event Action OnDraftComplete;

        protected override void Awake()
        {
            base.Awake();
            SetupControls();
        }

        private void SetupControls()
        {
            if (_sortDropdown != null)
            {
                _sortDropdown.ClearOptions();
                _sortDropdown.AddOptions(new List<string> { "Rank", "Overall", "Position", "Age" });
                _sortDropdown.onValueChanged.AddListener(OnSortChanged);
            }

            if (_positionFilter != null)
            {
                _positionFilter.ClearOptions();
                _positionFilter.AddOptions(new List<string> { "All", "PG", "SG", "SF", "PF", "C" });
                _positionFilter.onValueChanged.AddListener(OnPositionFilterChanged);
            }

            if (_searchInput != null)
            {
                _searchInput.onValueChanged.AddListener(OnSearchChanged);
            }

            if (_draftPlayerButton != null)
                _draftPlayerButton.onClick.AddListener(DraftSelectedPlayer);

            if (_autoDraftButton != null)
                _autoDraftButton.onClick.AddListener(AutoDraft);

            if (_tradePickButton != null)
                _tradePickButton.onClick.AddListener(OpenTradePickDialog);

            if (_simToPickButton != null)
                _simToPickButton.onClick.AddListener(SimToNextPick);
        }

        protected override void OnShown()
        {
            base.OnShown();
            InitializeDraft();
        }

        /// <summary>
        /// Initialize the draft with prospects
        /// </summary>
        public void InitializeDraft()
        {
            _playerTeamId = GameManager.Instance?.PlayerTeamId;
            int season = GameManager.Instance?.SeasonController?.CurrentSeason ?? 2024;

            if (_draftTitleText != null)
                _draftTitleText.text = $"{season + 1} NBA Draft";

            // Generate draft class (would come from DraftClassGenerator)
            GenerateDraftClass();

            // Determine player's picks
            DeterminePlayerPicks();

            // Start draft
            _currentPick = 1;
            _isDrafting = true;
            _draftResults.Clear();

            RefreshUI();
            UpdateDraftStatus();
        }

        private void GenerateDraftClass()
        {
            _allProspects.Clear();
            _availableProspects.Clear();

            // Generate 60 prospects
            var random = new System.Random();
            string[] firstNames = { "Marcus", "James", "Tyler", "Chris", "Anthony", "Kevin", "Michael", "David", "Brandon", "Jordan" };
            string[] lastNames = { "Williams", "Johnson", "Smith", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor", "Anderson" };
            string[] colleges = { "Duke", "Kentucky", "North Carolina", "Kansas", "UCLA", "Michigan State", "Gonzaga", "Villanova", "Arizona", "Auburn" };
            string[] positions = { "PG", "SG", "SF", "PF", "C" };

            for (int i = 0; i < 60; i++)
            {
                int overallBase = i < 5 ? 80 : i < 14 ? 72 : i < 30 ? 65 : 58;
                int overall = overallBase + random.Next(-3, 4);

                var prospect = new DraftProspect
                {
                    ProspectId = $"PROSPECT_{i + 1:D3}",
                    FirstName = firstNames[random.Next(firstNames.Length)],
                    LastName = lastNames[random.Next(lastNames.Length)],
                    Position = positions[random.Next(positions.Length)],
                    Age = random.Next(19, 23),
                    College = colleges[random.Next(colleges.Length)],
                    Overall = overall,
                    Potential = overall + random.Next(5, 15),
                    Rank = i + 1,
                    ProjectedPick = i + 1 + random.Next(-3, 4),

                    // Attributes
                    InsideScoring = overall + random.Next(-10, 11),
                    OutsideScoring = overall + random.Next(-10, 11),
                    Athleticism = overall + random.Next(-10, 11),
                    Defense = overall + random.Next(-10, 11),
                    Playmaking = overall + random.Next(-10, 11),
                    Rebounding = overall + random.Next(-10, 11),

                    // Scouting
                    Strengths = GetRandomStrengths(random),
                    Weaknesses = GetRandomWeaknesses(random),
                    Comparison = GetRandomComparison(random)
                };

                _allProspects.Add(prospect);
                _availableProspects.Add(prospect);
            }
        }

        private string GetRandomStrengths(System.Random random)
        {
            string[] strengths = {
                "Elite athleticism",
                "High basketball IQ",
                "Smooth shooting stroke",
                "Excellent court vision",
                "Defensive intensity",
                "Rebounding instincts",
                "Leadership qualities"
            };
            return strengths[random.Next(strengths.Length)];
        }

        private string GetRandomWeaknesses(System.Random random)
        {
            string[] weaknesses = {
                "Needs to add strength",
                "Inconsistent jumper",
                "Turnover prone",
                "Defensive awareness",
                "Free throw struggles",
                "Limited range"
            };
            return weaknesses[random.Next(weaknesses.Length)];
        }

        private string GetRandomComparison(System.Random random)
        {
            string[] comparisons = {
                "Jayson Tatum",
                "Ja Morant",
                "Anthony Edwards",
                "Evan Mobley",
                "Cade Cunningham",
                "Paolo Banchero"
            };
            return comparisons[random.Next(comparisons.Length)];
        }

        private void DeterminePlayerPicks()
        {
            _playerPicks.Clear();

            // Simplified - give player pick at their lottery position
            var team = GameManager.Instance?.GetPlayerTeam();
            if (team != null)
            {
                // Calculate lottery position based on record (simplified)
                int losses = team.Losses > 0 ? team.Losses : 40;
                int lotteryPosition = Math.Max(1, Math.Min(14, losses / 3));

                _playerPicks.Add(lotteryPosition);
                _playerPicks.Add(lotteryPosition + 30); // Second round
            }
            else
            {
                _playerPicks.Add(15);
                _playerPicks.Add(45);
            }

            UpdateYourNextPick();
        }

        private void UpdateYourNextPick()
        {
            if (_yourNextPickText == null) return;

            var nextPick = _playerPicks.FirstOrDefault(p => p >= _currentPick);
            if (nextPick > 0)
            {
                _yourNextPickText.text = $"Your next pick: #{nextPick}";
            }
            else
            {
                _yourNextPickText.text = "No remaining picks";
            }
        }

        #region UI Refresh

        private void RefreshUI()
        {
            RefreshProspectList();
            RefreshDraftBoard();
            RefreshTeamNeeds();
            ClearProspectDetail();
        }

        private void RefreshProspectList()
        {
            if (_prospectListContainer == null) return;

            // Clear existing
            foreach (Transform child in _prospectListContainer)
            {
                Destroy(child.gameObject);
            }

            // Apply filters
            var filtered = _availableProspects.AsEnumerable();

            // Position filter
            string posFilter = _positionFilter?.options[_positionFilter.value].text ?? "All";
            if (posFilter != "All")
            {
                filtered = filtered.Where(p => p.Position == posFilter);
            }

            // Search filter
            if (_searchInput != null && !string.IsNullOrEmpty(_searchInput.text))
            {
                string query = _searchInput.text.ToLower();
                filtered = filtered.Where(p =>
                    p.FirstName.ToLower().Contains(query) ||
                    p.LastName.ToLower().Contains(query) ||
                    p.College.ToLower().Contains(query));
            }

            // Sort
            int sortIndex = _sortDropdown?.value ?? 0;
            filtered = sortIndex switch
            {
                0 => filtered.OrderBy(p => p.Rank),
                1 => filtered.OrderByDescending(p => p.Overall),
                2 => filtered.OrderBy(p => p.Position),
                3 => filtered.OrderBy(p => p.Age),
                _ => filtered.OrderBy(p => p.Rank)
            };

            // Create rows
            foreach (var prospect in filtered)
            {
                CreateProspectRow(prospect);
            }
        }

        private void CreateProspectRow(DraftProspect prospect)
        {
            var rowGO = new GameObject($"Prospect_{prospect.ProspectId}");
            rowGO.transform.SetParent(_prospectListContainer, false);

            var bg = rowGO.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.18f, 0.23f);

            var button = rowGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.25f, 0.25f, 0.35f);
            button.colors = colors;

            var layout = rowGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 40;
            layout.flexibleWidth = 1;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(rowGO.transform, false);
            var contentLayout = contentGO.AddComponent<HorizontalLayoutGroup>();
            contentLayout.spacing = 10;
            contentLayout.padding = new RectOffset(10, 10, 5, 5);
            contentLayout.childAlignment = TextAnchor.MiddleLeft;

            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = Vector2.zero;

            // Rank
            AddColumnText(contentGO.transform, $"#{prospect.Rank}", 40);

            // Name
            AddColumnText(contentGO.transform, $"{prospect.FirstName} {prospect.LastName}", 150, TextAnchor.MiddleLeft);

            // Position
            AddColumnText(contentGO.transform, prospect.Position, 40);

            // Overall
            var ovrText = AddColumnText(contentGO.transform, prospect.Overall.ToString(), 40);
            ovrText.color = GetOverallColor(prospect.Overall);

            // College
            AddColumnText(contentGO.transform, prospect.College, 100, TextAnchor.MiddleLeft);

            // Click handler
            var capturedProspect = prospect;
            button.onClick.AddListener(() => SelectProspect(capturedProspect));
        }

        private Text AddColumnText(Transform parent, string text, float width, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var textGO = new GameObject("Col");
            textGO.transform.SetParent(parent, false);
            var textComp = textGO.AddComponent<Text>();
            textComp.text = text;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = 13;
            textComp.color = Color.white;
            textComp.alignment = align;

            var layout = textGO.AddComponent<LayoutElement>();
            layout.preferredWidth = width;

            return textComp;
        }

        private void RefreshDraftBoard()
        {
            if (_draftBoardContainer == null) return;

            foreach (Transform child in _draftBoardContainer)
            {
                Destroy(child.gameObject);
            }

            // Show recent picks
            foreach (var selection in _draftResults.TakeLast(10))
            {
                CreateDraftBoardEntry(selection);
            }
        }

        private void CreateDraftBoardEntry(DraftSelection selection)
        {
            var entryGO = new GameObject($"Pick_{selection.PickNumber}");
            entryGO.transform.SetParent(_draftBoardContainer, false);

            var layout = entryGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 25;

            var text = entryGO.AddComponent<Text>();
            text.text = $"#{selection.PickNumber}: {selection.Prospect?.FirstName} {selection.Prospect?.LastName} ({selection.TeamId})";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = selection.TeamId == _playerTeamId ? Color.green : Color.white;
        }

        private void RefreshTeamNeeds()
        {
            if (_teamNeedsSummaryText == null) return;

            var team = GameManager.Instance?.GetPlayerTeam();
            if (team?.Roster == null)
            {
                _teamNeedsSummaryText.text = "Team needs: Unknown";
                return;
            }

            // Analyze roster needs
            var positionCounts = team.Roster.GroupBy(p => p.Position)
                .ToDictionary(g => g.Key, g => g.Count());

            var needs = new List<string>();
            if (!positionCounts.ContainsKey(Position.PointGuard) || positionCounts[Position.PointGuard] < 2) needs.Add("PG");
            if (!positionCounts.ContainsKey(Position.Center) || positionCounts[Position.Center] < 2) needs.Add("C");
            if (!positionCounts.ContainsKey(Position.SmallForward) || positionCounts[Position.SmallForward] < 2) needs.Add("SF");

            _teamNeedsSummaryText.text = needs.Count > 0
                ? $"Team needs: {string.Join(", ", needs)}"
                : "Team needs: Best player available";
        }

        #endregion

        #region Prospect Detail

        private void SelectProspect(DraftProspect prospect)
        {
            _selectedProspect = prospect;
            ShowProspectDetail(prospect);
        }

        private void ClearProspectDetail()
        {
            _selectedProspect = null;
            if (_prospectDetailPanel != null)
                _prospectDetailPanel.SetActive(false);
        }

        private void ShowProspectDetail(DraftProspect prospect)
        {
            if (_prospectDetailPanel != null)
                _prospectDetailPanel.SetActive(true);

            if (_prospectNameText != null)
                _prospectNameText.text = $"{prospect.FirstName} {prospect.LastName}";

            if (_prospectPositionText != null)
                _prospectPositionText.text = prospect.Position;

            if (_prospectAgeText != null)
                _prospectAgeText.text = $"Age: {prospect.Age}";

            if (_prospectCollegeText != null)
                _prospectCollegeText.text = prospect.College;

            if (_prospectOverallText != null)
            {
                _prospectOverallText.text = $"Overall: {prospect.Overall}";
                _prospectOverallText.color = GetOverallColor(prospect.Overall);
            }

            if (_prospectProjectionText != null)
                _prospectProjectionText.text = $"Projected: #{prospect.ProjectedPick}";

            if (_scoutingReportText != null)
            {
                _scoutingReportText.text = $"Strengths: {prospect.Strengths}\n" +
                                           $"Weaknesses: {prospect.Weaknesses}\n" +
                                           $"Comparison: {prospect.Comparison}";
            }

            // Refresh attributes
            RefreshProspectAttributes(prospect);

            // Update draft button state
            UpdateDraftButton();
        }

        private void RefreshProspectAttributes(DraftProspect prospect)
        {
            if (_prospectAttributesContainer == null) return;

            foreach (Transform child in _prospectAttributesContainer)
            {
                Destroy(child.gameObject);
            }

            AddAttributeBar("Scoring", (prospect.InsideScoring + prospect.OutsideScoring) / 2);
            AddAttributeBar("Athleticism", prospect.Athleticism);
            AddAttributeBar("Defense", prospect.Defense);
            AddAttributeBar("Playmaking", prospect.Playmaking);
            AddAttributeBar("Rebounding", prospect.Rebounding);
            AddAttributeBar("Potential", prospect.Potential);
        }

        private void AddAttributeBar(string name, int value)
        {
            var rowGO = new GameObject(name);
            rowGO.transform.SetParent(_prospectAttributesContainer, false);

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 20;

            // Name
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(rowGO.transform, false);
            var nameText = nameGO.AddComponent<Text>();
            nameText.text = name;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 11;
            nameText.color = new Color(0.7f, 0.7f, 0.7f);
            var nameLayout = nameGO.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = 80;

            // Value
            var valueGO = new GameObject("Value");
            valueGO.transform.SetParent(rowGO.transform, false);
            var valueText = valueGO.AddComponent<Text>();
            valueText.text = value.ToString();
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 11;
            valueText.color = GetOverallColor(value);
            var valueLayout = valueGO.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 30;
        }

        private Color GetOverallColor(int value)
        {
            if (value >= 80) return new Color(0.3f, 0.8f, 0.3f);
            if (value >= 70) return new Color(0.5f, 0.7f, 0.9f);
            if (value >= 60) return new Color(0.9f, 0.9f, 0.5f);
            return new Color(0.7f, 0.7f, 0.7f);
        }

        #endregion

        #region Draft Status

        private void UpdateDraftStatus()
        {
            if (_currentPickText != null)
                _currentPickText.text = $"Pick #{_currentPick} of {_totalPicks}";

            bool isPlayerPick = _playerPicks.Contains(_currentPick);

            if (_onTheClockText != null)
            {
                if (isPlayerPick)
                {
                    _onTheClockText.text = "YOU ARE ON THE CLOCK!";
                    _onTheClockText.color = Color.green;
                }
                else
                {
                    // Get team on the clock (simplified)
                    _onTheClockText.text = "Waiting...";
                    _onTheClockText.color = Color.white;
                }
            }

            UpdateDraftButton();
            UpdateYourNextPick();
        }

        private void UpdateDraftButton()
        {
            if (_draftPlayerButton == null) return;

            bool isPlayerPick = _playerPicks.Contains(_currentPick);
            bool hasSelection = _selectedProspect != null;

            _draftPlayerButton.interactable = isPlayerPick && hasSelection;
        }

        #endregion

        #region Draft Actions

        private void DraftSelectedPlayer()
        {
            if (_selectedProspect == null) return;
            if (!_playerPicks.Contains(_currentPick)) return;

            DraftPlayer(_selectedProspect, _playerTeamId);
        }

        private void AutoDraft()
        {
            if (!_playerPicks.Contains(_currentPick)) return;

            // Pick best available
            var bestAvailable = _availableProspects.OrderBy(p => p.Rank).FirstOrDefault();
            if (bestAvailable != null)
            {
                DraftPlayer(bestAvailable, _playerTeamId);
            }
        }

        private void DraftPlayer(DraftProspect prospect, string teamId)
        {
            var selection = new DraftSelection
            {
                PickNumber = _currentPick,
                TeamId = teamId,
                Prospect = prospect
            };

            _draftResults.Add(selection);
            _availableProspects.Remove(prospect);

            Debug.Log($"[DraftPanel] Pick #{_currentPick}: {prospect.FirstName} {prospect.LastName} to {teamId}");

            if (teamId == _playerTeamId)
            {
                OnPlayerDrafted?.Invoke(prospect);
            }

            AdvanceDraft();
        }

        private void AdvanceDraft()
        {
            _currentPick++;

            if (_currentPick > _totalPicks)
            {
                EndDraft();
                return;
            }

            // If not player's pick, AI drafts
            if (!_playerPicks.Contains(_currentPick))
            {
                // AI picks (would use more sophisticated logic)
                var aiPick = _availableProspects.OrderBy(p => p.Rank).FirstOrDefault();
                if (aiPick != null)
                {
                    string aiTeamId = $"TEAM_{(_currentPick % 30) + 1}";
                    DraftPlayer(aiPick, aiTeamId);
                }
            }
            else
            {
                // Player's turn
                UpdateDraftStatus();
                RefreshUI();
            }
        }

        private void SimToNextPick()
        {
            while (_currentPick <= _totalPicks && !_playerPicks.Contains(_currentPick))
            {
                var aiPick = _availableProspects.OrderBy(p => p.Rank).FirstOrDefault();
                if (aiPick != null)
                {
                    string aiTeamId = $"TEAM_{(_currentPick % 30) + 1}";
                    DraftPlayer(aiPick, aiTeamId);
                }
            }

            UpdateDraftStatus();
            RefreshUI();
        }

        private void OpenTradePickDialog()
        {
            Debug.Log("[DraftPanel] Opening trade pick dialog...");
            // Would open trade panel focused on draft picks
        }

        private void EndDraft()
        {
            _isDrafting = false;
            Debug.Log("[DraftPanel] Draft complete!");
            OnDraftComplete?.Invoke();
        }

        #endregion

        private void OnSortChanged(int index) => RefreshProspectList();
        private void OnPositionFilterChanged(int index) => RefreshProspectList();
        private void OnSearchChanged(string query) => RefreshProspectList();
    }

    /// <summary>
    /// Draft prospect data
    /// </summary>
    [Serializable]
    public class DraftProspect
    {
        public string ProspectId;
        public string FirstName;
        public string LastName;
        public string Position;
        public int Age;
        public string College;
        public int Overall;
        public int Potential;
        public int Rank;
        public int ProjectedPick;

        // Attributes
        public int InsideScoring;
        public int OutsideScoring;
        public int Athleticism;
        public int Defense;
        public int Playmaking;
        public int Rebounding;

        // Scouting
        public string Strengths;
        public string Weaknesses;
        public string Comparison;
    }

    /// <summary>
    /// Draft selection record
    /// </summary>
    [Serializable]
    public class DraftSelection
    {
        public int PickNumber;
        public string TeamId;
        public DraftProspect Prospect;
    }
}
