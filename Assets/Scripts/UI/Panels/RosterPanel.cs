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
    /// Roster management panel showing all team players with details
    /// </summary>
    public class RosterPanel : BasePanel
    {
        [Header("List configuration")]
        public Transform ListContent;
        public GameObject RosterRowPrefab;

        [Header("Team Info")]
        [SerializeField] private Text _teamNameText;
        [SerializeField] private Text _rosterCountText;
        [SerializeField] private Text _payrollText;
        [SerializeField] private Text _capSpaceText;

        [Header("Sort & Filter")]
        [SerializeField] private Dropdown _sortDropdown;
        [SerializeField] private Dropdown _positionFilter;
        [SerializeField] private InputField _searchInput;

        [Header("Player Details")]
        [SerializeField] private GameObject _playerDetailPanel;
        [SerializeField] private Text _playerNameText;
        [SerializeField] private Text _playerPositionText;
        [SerializeField] private Text _playerAgeText;
        [SerializeField] private Text _playerOverallText;
        [SerializeField] private Text _playerContractText;
        [SerializeField] private Transform _attributesContainer;

        [Header("Actions")]
        [SerializeField] private Button _releaseButton;
        [SerializeField] private Button _tradeButton;
        [SerializeField] private Button _extendButton;
        [SerializeField] private Button _setLineupButton;

        [Header("Lineup Editor")]
        [SerializeField] private GameObject _lineupEditorPanel;
        [SerializeField] private Transform[] _starterSlots;

        private List<GameObject> _spawnedRows = new List<GameObject>();
        private List<Player> _roster = new List<Player>();
        private Player _selectedPlayer;
        private SortMode _currentSort = SortMode.Overall;
        private string _currentPositionFilter = "All";
        private string _searchQuery = "";

        // Events
        public event Action<Player> OnPlayerSelected;
        public event Action<Player> OnReleasePlayer;
        public event Action<Player> OnTradePlayer;
        public event Action<Player> OnExtendPlayer;

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
                _sortDropdown.AddOptions(new List<string>
                {
                    "Overall",
                    "Position",
                    "Age",
                    "Salary",
                    "Name"
                });
                _sortDropdown.onValueChanged.AddListener(OnSortChanged);
            }

            if (_positionFilter != null)
            {
                _positionFilter.ClearOptions();
                _positionFilter.AddOptions(new List<string>
                {
                    "All",
                    "PG",
                    "SG",
                    "SF",
                    "PF",
                    "C"
                });
                _positionFilter.onValueChanged.AddListener(OnPositionFilterChanged);
            }

            if (_searchInput != null)
            {
                _searchInput.onValueChanged.AddListener(OnSearchChanged);
            }

            if (_releaseButton != null)
                _releaseButton.onClick.AddListener(OnReleaseClicked);

            if (_tradeButton != null)
                _tradeButton.onClick.AddListener(OnTradeClicked);

            if (_extendButton != null)
                _extendButton.onClick.AddListener(OnExtendClicked);

            if (_setLineupButton != null)
                _setLineupButton.onClick.AddListener(ToggleLineupEditor);
        }

        protected override void OnShown()
        {
            base.OnShown();
            RefreshList();
            ClearPlayerDetail();
        }

        public void RefreshList()
        {
            // Clear old rows
            foreach (var go in _spawnedRows) Destroy(go);
            _spawnedRows.Clear();

            // Get roster from GameManager
            var team = GameManager.Instance?.GetPlayerTeam();
            if (team?.Roster == null)
            {
                Debug.Log("[RosterPanel] No team roster available");
                return;
            }

            _roster = team.Roster;

            // Update team info
            RefreshTeamInfo(team);

            // Apply filters and sort
            var filteredRoster = GetFilteredAndSortedRoster();

            // Create rows
            foreach (var player in filteredRoster)
            {
                CreatePlayerRow(player);
            }
        }

        private void RefreshTeamInfo(Team team)
        {
            if (_teamNameText != null)
                _teamNameText.text = $"{team.City} {team.Name}";

            if (_rosterCountText != null)
                _rosterCountText.text = $"{team.Roster.Count} Players";

            long totalSalary = team.Roster.Sum(p => p.AnnualSalary);
            long salaryCap = 136000000;

            if (_payrollText != null)
                _payrollText.text = $"Payroll: ${totalSalary / 1000000f:F1}M";

            if (_capSpaceText != null)
            {
                long capSpace = salaryCap - totalSalary;
                _capSpaceText.text = $"Cap Space: ${capSpace / 1000000f:F1}M";
                _capSpaceText.color = capSpace >= 0 ? Color.green : Color.red;
            }
        }

        #region Filtering & Sorting

        private void OnSortChanged(int index)
        {
            _currentSort = (SortMode)index;
            RefreshList();
        }

        private void OnPositionFilterChanged(int index)
        {
            _currentPositionFilter = _positionFilter.options[index].text;
            RefreshList();
        }

        private void OnSearchChanged(string query)
        {
            _searchQuery = query.ToLower();
            RefreshList();
        }

        private List<Player> GetFilteredAndSortedRoster()
        {
            var filtered = _roster.AsEnumerable();

            // Position filter
            if (_currentPositionFilter != "All")
            {
                filtered = filtered.Where(p => p.PositionString == _currentPositionFilter);
            }

            // Search filter
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                filtered = filtered.Where(p =>
                    p.FirstName.ToLower().Contains(_searchQuery) ||
                    p.LastName.ToLower().Contains(_searchQuery));
            }

            // Apply sort
            filtered = _currentSort switch
            {
                SortMode.Overall => filtered.OrderByDescending(p => p.Overall),
                SortMode.Position => filtered.OrderBy(p => GetPositionOrder(p.PositionString)),
                SortMode.Age => filtered.OrderBy(p => p.Age),
                SortMode.Salary => filtered.OrderByDescending(p => p.CurrentContract?.AnnualSalary ?? 0),
                SortMode.Name => filtered.OrderBy(p => p.LastName).ThenBy(p => p.FirstName),
                _ => filtered.OrderByDescending(p => p.Overall)
            };

            return filtered.ToList();
        }

        private int GetPositionOrder(string position)
        {
            return position switch
            {
                "PG" => 0,
                "SG" => 1,
                "SF" => 2,
                "PF" => 3,
                "C" => 4,
                _ => 5
            };
        }

        #endregion

        #region Player Rows

        private void CreatePlayerRow(Player player)
        {
            GameObject rowGO;
            if (RosterRowPrefab != null && ListContent != null)
            {
                rowGO = Instantiate(RosterRowPrefab, ListContent);
                var row = rowGO.GetComponent<Components.RosterRow>();
                if (row != null) row.Setup(player);
            }
            else
            {
                rowGO = CreateDefaultPlayerRow(player);
            }

            // Add click handler
            var button = rowGO.GetComponent<Button>();
            if (button == null) button = rowGO.AddComponent<Button>();

            var capturedPlayer = player;
            button.onClick.AddListener(() => SelectPlayer(capturedPlayer));

            _spawnedRows.Add(rowGO);
        }

        private GameObject CreateDefaultPlayerRow(Player player)
        {
            var rowGO = new GameObject($"PlayerRow_{player.PlayerId}");
            if (ListContent != null)
                rowGO.transform.SetParent(ListContent, false);

            var bg = rowGO.AddComponent<Image>();
            bg.color = player.IsInjured
                ? new Color(0.3f, 0.15f, 0.15f)
                : new Color(0.15f, 0.15f, 0.2f);

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(10, 10, 5, 5);

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 40;
            layoutElement.flexibleWidth = 1;

            // Position
            AddColumnText(rowGO.transform, player.PositionString, 40);

            // Name
            string injuryIcon = player.IsInjured ? " [INJ]" : "";
            AddColumnText(rowGO.transform, $"{player.FirstName} {player.LastName}{injuryIcon}", 180, TextAnchor.MiddleLeft);

            // Age
            AddColumnText(rowGO.transform, player.Age.ToString(), 40);

            // Overall
            var ovrText = AddColumnText(rowGO.transform, player.Overall.ToString(), 50);
            ovrText.color = GetOverallColor(player.Overall);

            // Contract
            long salary = player.AnnualSalary;
            int years = player.CurrentContract?.YearsRemaining ?? 0;
            AddColumnText(rowGO.transform, $"${salary / 1000000f:F1}M ({years}yr)", 100, TextAnchor.MiddleRight);

            return rowGO;
        }

        private Text AddColumnText(Transform parent, string text, float width, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var colGO = new GameObject("Col");
            colGO.transform.SetParent(parent, false);

            var colText = colGO.AddComponent<Text>();
            colText.text = text;
            colText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            colText.fontSize = 14;
            colText.color = Color.white;
            colText.alignment = alignment;

            var colLayout = colGO.AddComponent<LayoutElement>();
            colLayout.preferredWidth = width;

            return colText;
        }

        private Color GetOverallColor(int overall)
        {
            if (overall >= 85) return new Color(0.3f, 0.8f, 0.3f); // Green - Elite
            if (overall >= 75) return new Color(0.5f, 0.7f, 0.9f); // Blue - Starter
            if (overall >= 65) return new Color(0.9f, 0.9f, 0.5f); // Yellow - Role player
            return new Color(0.7f, 0.7f, 0.7f); // Gray - Bench
        }

        #endregion

        #region Player Details

        private void SelectPlayer(Player player)
        {
            _selectedPlayer = player;
            ShowPlayerDetail(player);
            OnPlayerSelected?.Invoke(player);
        }

        private void ClearPlayerDetail()
        {
            _selectedPlayer = null;
            if (_playerDetailPanel != null)
                _playerDetailPanel.SetActive(false);
        }

        private void ShowPlayerDetail(Player player)
        {
            if (_playerDetailPanel != null)
                _playerDetailPanel.SetActive(true);

            if (_playerNameText != null)
                _playerNameText.text = $"{player.FirstName} {player.LastName}";

            if (_playerPositionText != null)
                _playerPositionText.text = $"Position: {player.PositionString}";

            if (_playerAgeText != null)
                _playerAgeText.text = $"Age: {player.Age}";

            if (_playerOverallText != null)
            {
                _playerOverallText.text = $"Overall: {player.Overall}";
                _playerOverallText.color = GetOverallColor(player.Overall);
            }

            if (_playerContractText != null && player.CurrentContract != null)
            {
                var contract = player.CurrentContract;
                _playerContractText.text = $"Contract: ${contract.CurrentYearSalary / 1000000f:F1}M/yr, {contract.YearsRemaining} years remaining";
            }

            // Show attributes
            RefreshAttributesDisplay(player);

            // Update button states
            UpdateActionButtons(player);
        }

        private void RefreshAttributesDisplay(Player player)
        {
            if (_attributesContainer == null) return;

            // Clear existing
            foreach (Transform child in _attributesContainer)
            {
                Destroy(child.gameObject);
            }

            // Core attributes
            AddAttributeRow("Inside Scoring", player.InsideScoring);
            AddAttributeRow("Mid-Range", player.MidRangeScoring);
            AddAttributeRow("3-Point", player.ThreePointScoring);
            AddAttributeRow("Free Throw", player.FreeThrow);
            AddAttributeRow("Passing", player.Passing);
            AddAttributeRow("Ball Handling", player.BallHandling);
            AddAttributeRow("Rebounding", player.Rebounding);
            AddAttributeRow("Perimeter D", player.PerimeterDefense);
            AddAttributeRow("Interior D", player.InteriorDefense);
            AddAttributeRow("Athleticism", player.Athleticism);
            AddAttributeRow("Basketball IQ", player.BasketballIQ);
        }

        private void AddAttributeRow(string name, int value)
        {
            var rowGO = new GameObject(name);
            rowGO.transform.SetParent(_attributesContainer, false);

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 20;

            // Name
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(rowGO.transform, false);
            var nameText = nameGO.AddComponent<Text>();
            nameText.text = name;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 12;
            nameText.color = new Color(0.7f, 0.7f, 0.7f);
            var nameLayout = nameGO.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = 100;

            // Value
            var valueGO = new GameObject("Value");
            valueGO.transform.SetParent(rowGO.transform, false);
            var valueText = valueGO.AddComponent<Text>();
            valueText.text = value.ToString();
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 12;
            valueText.color = GetOverallColor(value);
            var valueLayout = valueGO.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 40;
        }

        private void UpdateActionButtons(Player player)
        {
            // Enable/disable based on player status and contract
            if (_releaseButton != null)
            {
                // Can't release player if under guaranteed contract (simplified)
                _releaseButton.interactable = player.CurrentContract?.AnnualSalary < 5000000;
            }

            if (_tradeButton != null)
            {
                _tradeButton.interactable = true;
            }

            if (_extendButton != null)
            {
                // Can only extend if in final year
                _extendButton.interactable = player.CurrentContract?.YearsRemaining <= 1;
            }
        }

        #endregion

        #region Actions

        private void OnReleaseClicked()
        {
            if (_selectedPlayer != null)
            {
                OnReleasePlayer?.Invoke(_selectedPlayer);
            }
        }

        private void OnTradeClicked()
        {
            if (_selectedPlayer != null)
            {
                OnTradePlayer?.Invoke(_selectedPlayer);
            }
        }

        private void OnExtendClicked()
        {
            if (_selectedPlayer != null)
            {
                OnExtendPlayer?.Invoke(_selectedPlayer);
            }
        }

        private void ToggleLineupEditor()
        {
            if (_lineupEditorPanel != null)
            {
                _lineupEditorPanel.SetActive(!_lineupEditorPanel.activeSelf);
            }
        }

        #endregion

        /// <summary>
        /// Populate roster from external source (legacy support)
        /// </summary>
        public void Populate(List<Player> players)
        {
            _roster = players ?? new List<Player>();
            RefreshList();
        }

        private enum SortMode
        {
            Overall,
            Position,
            Age,
            Salary,
            Name
        }
    }
}
