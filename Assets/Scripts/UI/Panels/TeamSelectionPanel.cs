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
    /// Standalone team selection panel with detailed team preview
    /// Can be used from NewGamePanel or as standalone screen
    /// </summary>
    public class TeamSelectionPanel : BasePanel
    {
        [Header("Team List")]
        [SerializeField] private Transform _teamListContainer;
        [SerializeField] private GameObject _teamCardPrefab;

        [Header("Filter Controls")]
        [SerializeField] private Dropdown _conferenceFilter;
        [SerializeField] private Dropdown _situationFilter;
        [SerializeField] private InputField _searchInput;

        [Header("Team Preview")]
        [SerializeField] private GameObject _previewPanel;
        [SerializeField] private Image _teamLogoPreview;
        [SerializeField] private Text _teamNameText;
        [SerializeField] private Text _teamRecordText;
        [SerializeField] private Text _teamLocationText;
        [SerializeField] private Text _teamArenaText;

        [Header("Roster Preview")]
        [SerializeField] private Transform _startersContainer;
        [SerializeField] private Text _rosterOverviewText;
        [SerializeField] private Text _salaryCapText;

        [Header("Team Stats")]
        [SerializeField] private Text _offenseRatingText;
        [SerializeField] private Text _defenseRatingText;
        [SerializeField] private Text _paceText;
        [SerializeField] private Text _strengthsText;
        [SerializeField] private Text _weaknessesText;

        [Header("Situation Info")]
        [SerializeField] private Text _ownerExpectationsText;
        [SerializeField] private Text _jobDifficultyText;
        [SerializeField] private Text _contractOfferText;

        [Header("Buttons")]
        [SerializeField] private Button _selectButton;
        [SerializeField] private Button _backButton;

        // State
        private List<Team> _allTeams = new List<Team>();
        private List<TeamCardUI> _teamCards = new List<TeamCardUI>();
        private Team _selectedTeam;
        private string _currentConferenceFilter = "All";
        private string _currentSituationFilter = "All";
        private string _searchQuery = "";

        // Events
        public event Action<Team> OnTeamSelected;
        public event Action OnBackPressed;

        protected override void Awake()
        {
            base.Awake();
            SetupFilters();
            SetupButtons();
        }

        private void SetupFilters()
        {
            if (_conferenceFilter != null)
            {
                _conferenceFilter.ClearOptions();
                _conferenceFilter.AddOptions(new List<string> { "All", "Eastern", "Western" });
                _conferenceFilter.onValueChanged.AddListener(OnConferenceFilterChanged);
            }

            if (_situationFilter != null)
            {
                _situationFilter.ClearOptions();
                _situationFilter.AddOptions(new List<string>
                {
                    "All",
                    "Contender",
                    "Playoff Team",
                    "Rebuilding",
                    "Lottery Team"
                });
                _situationFilter.onValueChanged.AddListener(OnSituationFilterChanged);
            }

            if (_searchInput != null)
            {
                _searchInput.onValueChanged.AddListener(OnSearchChanged);
            }
        }

        private void SetupButtons()
        {
            if (_selectButton != null)
            {
                _selectButton.onClick.AddListener(OnSelectClicked);
                _selectButton.interactable = false;
            }

            if (_backButton != null)
            {
                _backButton.onClick.AddListener(() => OnBackPressed?.Invoke());
            }
        }

        protected override void OnShown()
        {
            base.OnShown();
            LoadTeams();
            RefreshTeamList();
            ClearPreview();
        }

        private void LoadTeams()
        {
            _allTeams.Clear();

            if (GameManager.Instance != null)
            {
                _allTeams = GameManager.Instance.AllTeams ?? new List<Team>();
            }

            // If no teams loaded, create placeholder data
            if (_allTeams.Count == 0)
            {
                Debug.LogWarning("[TeamSelectionPanel] No teams available - using placeholders");
            }
        }

        #region Filtering

        private void OnConferenceFilterChanged(int index)
        {
            _currentConferenceFilter = _conferenceFilter.options[index].text;
            RefreshTeamList();
        }

        private void OnSituationFilterChanged(int index)
        {
            _currentSituationFilter = _situationFilter.options[index].text;
            RefreshTeamList();
        }

        private void OnSearchChanged(string query)
        {
            _searchQuery = query.ToLower();
            RefreshTeamList();
        }

        private List<Team> GetFilteredTeams()
        {
            var filtered = _allTeams.AsEnumerable();

            // Conference filter
            if (_currentConferenceFilter != "All")
            {
                filtered = filtered.Where(t => t.Conference == _currentConferenceFilter);
            }

            // Situation filter
            if (_currentSituationFilter != "All")
            {
                filtered = filtered.Where(t => GetTeamSituation(t) == _currentSituationFilter);
            }

            // Search filter
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                filtered = filtered.Where(t =>
                    t.Name.ToLower().Contains(_searchQuery) ||
                    t.City.ToLower().Contains(_searchQuery) ||
                    t.TeamId.ToLower().Contains(_searchQuery));
            }

            return filtered.OrderBy(t => t.City).ToList();
        }

        private string GetTeamSituation(Team team)
        {
            if (team == null) return "Unknown";

            float winPct = team.Wins + team.Losses > 0
                ? team.Wins / (float)(team.Wins + team.Losses)
                : 0.5f;

            // Calculate team overall
            float avgOverall = team.Roster?.Count > 0
                ? (float)team.Roster.Average(p => p.Overall)
                : 70f;

            if (avgOverall >= 80 && winPct >= 0.6f) return "Contender";
            if (avgOverall >= 75 || winPct >= 0.5f) return "Playoff Team";
            if (avgOverall >= 70) return "Rebuilding";
            return "Lottery Team";
        }

        #endregion

        #region Team List

        private void RefreshTeamList()
        {
            // Clear existing cards
            foreach (var card in _teamCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _teamCards.Clear();

            var filteredTeams = GetFilteredTeams();

            foreach (var team in filteredTeams)
            {
                CreateTeamCard(team);
            }
        }

        private void CreateTeamCard(Team team)
        {
            if (_teamListContainer == null) return;

            GameObject cardGO;
            if (_teamCardPrefab != null)
            {
                cardGO = Instantiate(_teamCardPrefab, _teamListContainer);
            }
            else
            {
                cardGO = CreateDefaultTeamCard(team);
            }

            var cardUI = cardGO.GetComponent<TeamCardUI>();
            if (cardUI == null)
            {
                cardUI = cardGO.AddComponent<TeamCardUI>();
            }

            cardUI.Setup(team, OnTeamCardClicked);
            _teamCards.Add(cardUI);
        }

        private GameObject CreateDefaultTeamCard(Team team)
        {
            var cardGO = new GameObject($"TeamCard_{team.TeamId}");
            cardGO.transform.SetParent(_teamListContainer, false);

            var rect = cardGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 100);

            var image = cardGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.3f);

            var button = cardGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.5f);
            colors.selectedColor = new Color(0.2f, 0.4f, 0.6f);
            button.colors = colors;

            // Team name text
            var textGO = new GameObject("TeamName");
            textGO.transform.SetParent(cardGO.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = $"{team.City}\n{team.Name}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // Record text
            var recordGO = new GameObject("Record");
            recordGO.transform.SetParent(cardGO.transform, false);
            var recordText = recordGO.AddComponent<Text>();
            recordText.text = $"{team.Wins}-{team.Losses}";
            recordText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            recordText.fontSize = 12;
            recordText.color = new Color(0.7f, 0.7f, 0.7f);
            recordText.alignment = TextAnchor.LowerCenter;

            var recordRect = recordText.GetComponent<RectTransform>();
            recordRect.anchorMin = new Vector2(0, 0);
            recordRect.anchorMax = new Vector2(1, 0.3f);
            recordRect.sizeDelta = Vector2.zero;

            return cardGO;
        }

        private void OnTeamCardClicked(Team team)
        {
            _selectedTeam = team;

            // Update card visuals
            foreach (var card in _teamCards)
            {
                card.SetSelected(card.Team == team);
            }

            // Show preview
            ShowTeamPreview(team);

            // Enable select button
            if (_selectButton != null)
            {
                _selectButton.interactable = true;
            }
        }

        #endregion

        #region Team Preview

        private void ClearPreview()
        {
            if (_previewPanel != null)
            {
                _previewPanel.SetActive(false);
            }
            _selectedTeam = null;

            if (_selectButton != null)
            {
                _selectButton.interactable = false;
            }
        }

        private void ShowTeamPreview(Team team)
        {
            if (_previewPanel != null)
            {
                _previewPanel.SetActive(true);
            }

            // Basic info
            if (_teamNameText != null)
                _teamNameText.text = $"{team.City} {team.Name}";

            if (_teamRecordText != null)
                _teamRecordText.text = $"Record: {team.Wins}-{team.Losses}";

            if (_teamLocationText != null)
                _teamLocationText.text = $"Location: {team.City}";

            if (_teamArenaText != null)
                _teamArenaText.text = $"Arena: {team.Arena ?? "Arena"}";

            // Roster overview
            UpdateRosterPreview(team);

            // Team stats
            UpdateTeamStats(team);

            // Situation info
            UpdateSituationInfo(team);
        }

        private void UpdateRosterPreview(Team team)
        {
            if (_rosterOverviewText != null && team.Roster != null)
            {
                int rosterCount = team.Roster.Count;
                float avgAge = team.Roster.Count > 0
                    ? (float)team.Roster.Average(p => p.Age)
                    : 0;
                float avgOvr = team.Roster.Count > 0
                    ? (float)team.Roster.Average(p => p.Overall)
                    : 0;

                _rosterOverviewText.text = $"Roster: {rosterCount} players\n" +
                                           $"Avg Age: {avgAge:F1}\n" +
                                           $"Avg OVR: {avgOvr:F0}";
            }

            // Show starters
            if (_startersContainer != null && team.Roster != null)
            {
                // Clear existing
                foreach (Transform child in _startersContainer)
                {
                    Destroy(child.gameObject);
                }

                // Get top 5 by overall
                var starters = team.Roster
                    .OrderByDescending(p => p.Overall)
                    .Take(5)
                    .ToList();

                foreach (var player in starters)
                {
                    CreateStarterEntry(player);
                }
            }

            // Salary cap
            if (_salaryCapText != null)
            {
                long totalSalary = team.Roster?.Sum(p => p.AnnualSalary) ?? 0;
                long salaryCap = 136000000; // 2024-25 cap
                _salaryCapText.text = $"Payroll: ${totalSalary / 1000000f:F1}M / ${salaryCap / 1000000f:F0}M";
            }
        }

        private void CreateStarterEntry(Player player)
        {
            var entryGO = new GameObject(player.LastName);
            entryGO.transform.SetParent(_startersContainer, false);

            var text = entryGO.AddComponent<Text>();
            text.text = $"{player.PositionString} - {player.FirstName} {player.LastName} ({player.Overall})";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;

            var layout = entryGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 20;
        }

        private void UpdateTeamStats(Team team)
        {
            // Calculate team ratings
            float offRating = CalculateOffensiveRating(team);
            float defRating = CalculateDefensiveRating(team);
            float pace = team.Strategy?.Pace ?? 100f;

            if (_offenseRatingText != null)
                _offenseRatingText.text = $"Offense: {GetRatingGrade(offRating)}";

            if (_defenseRatingText != null)
                _defenseRatingText.text = $"Defense: {GetRatingGrade(defRating)}";

            if (_paceText != null)
                _paceText.text = $"Pace: {(pace > 102 ? "Fast" : pace < 98 ? "Slow" : "Average")}";

            // Analyze strengths/weaknesses
            if (_strengthsText != null)
                _strengthsText.text = $"Strengths:\n{GetTeamStrengths(team)}";

            if (_weaknessesText != null)
                _weaknessesText.text = $"Weaknesses:\n{GetTeamWeaknesses(team)}";
        }

        private void UpdateSituationInfo(Team team)
        {
            string situation = GetTeamSituation(team);

            if (_ownerExpectationsText != null)
            {
                string expectations = situation switch
                {
                    "Contender" => "Championship or bust. Owner expects a title.",
                    "Playoff Team" => "Make playoffs, compete in first round.",
                    "Rebuilding" => "Develop young talent, build for future.",
                    "Lottery Team" => "Tank for picks, minimal expectations.",
                    _ => "Unknown expectations."
                };
                _ownerExpectationsText.text = $"Owner Expects: {expectations}";
            }

            if (_jobDifficultyText != null)
            {
                string difficulty = situation switch
                {
                    "Contender" => "HIGH - Pressure to win now",
                    "Playoff Team" => "MEDIUM - Solid foundation",
                    "Rebuilding" => "MEDIUM - Need to show progress",
                    "Lottery Team" => "LOW - Long runway",
                    _ => "UNKNOWN"
                };
                _jobDifficultyText.text = $"Job Difficulty: {difficulty}";
            }

            if (_contractOfferText != null)
            {
                int years = situation == "Contender" ? 2 : situation == "Lottery Team" ? 5 : 3;
                float salary = situation == "Contender" ? 8f : situation == "Lottery Team" ? 3f : 5f;
                _contractOfferText.text = $"Contract Offer: {years} years, ${salary}M/year";
            }
        }

        private float CalculateOffensiveRating(Team team)
        {
            if (team?.Roster == null || team.Roster.Count == 0) return 70f;

            return (float)team.Roster.Average(p =>
                (p.InsideScoring + p.MidRangeScoring + p.ThreePointScoring + p.Passing) / 4f);
        }

        private float CalculateDefensiveRating(Team team)
        {
            if (team?.Roster == null || team.Roster.Count == 0) return 70f;

            return (float)team.Roster.Average(p =>
                (p.PerimeterDefense + p.InteriorDefense + p.Rebounding) / 3f);
        }

        private string GetRatingGrade(float rating)
        {
            if (rating >= 85) return "A+";
            if (rating >= 80) return "A";
            if (rating >= 75) return "B+";
            if (rating >= 70) return "B";
            if (rating >= 65) return "C+";
            if (rating >= 60) return "C";
            return "D";
        }

        private string GetTeamStrengths(Team team)
        {
            var strengths = new List<string>();

            if (team?.Roster == null || team.Roster.Count == 0)
                return "- Unknown";

            float avgThree = (float)team.Roster.Average(p => p.ThreePointScoring);
            float avgInside = (float)team.Roster.Average(p => p.InsideScoring);
            float avgDef = (float)team.Roster.Average(p => (p.PerimeterDefense + p.InteriorDefense) / 2f);
            float avgReb = (float)team.Roster.Average(p => p.Rebounding);

            if (avgThree >= 75) strengths.Add("- Elite shooting");
            if (avgInside >= 75) strengths.Add("- Strong inside scoring");
            if (avgDef >= 75) strengths.Add("- Lockdown defense");
            if (avgReb >= 75) strengths.Add("- Dominant rebounding");

            if (strengths.Count == 0)
                strengths.Add("- Balanced roster");

            return string.Join("\n", strengths.Take(3));
        }

        private string GetTeamWeaknesses(Team team)
        {
            var weaknesses = new List<string>();

            if (team?.Roster == null || team.Roster.Count == 0)
                return "- Unknown";

            float avgThree = (float)team.Roster.Average(p => p.ThreePointScoring);
            float avgInside = (float)team.Roster.Average(p => p.InsideScoring);
            float avgDef = (float)team.Roster.Average(p => (p.PerimeterDefense + p.InteriorDefense) / 2f);
            float avgAthl = (float)team.Roster.Average(p => p.Athleticism);

            if (avgThree < 65) weaknesses.Add("- Poor shooting");
            if (avgInside < 65) weaknesses.Add("- Weak inside game");
            if (avgDef < 65) weaknesses.Add("- Porous defense");
            if (avgAthl < 65) weaknesses.Add("- Lack of athleticism");

            // Check age
            float avgAge = (float)team.Roster.Average(p => p.Age);
            if (avgAge > 30) weaknesses.Add("- Aging roster");

            if (weaknesses.Count == 0)
                weaknesses.Add("- No major weaknesses");

            return string.Join("\n", weaknesses.Take(3));
        }

        #endregion

        #region Actions

        private void OnSelectClicked()
        {
            if (_selectedTeam != null)
            {
                OnTeamSelected?.Invoke(_selectedTeam);
            }
        }

        /// <summary>
        /// External method to set a team (for wizard integration)
        /// </summary>
        public void SelectTeam(Team team)
        {
            _selectedTeam = team;
            ShowTeamPreview(team);
        }

        /// <summary>
        /// Get currently selected team
        /// </summary>
        public Team GetSelectedTeam() => _selectedTeam;

        #endregion
    }

    /// <summary>
    /// UI component for team cards in the selection list
    /// </summary>
    public class TeamCardUI : MonoBehaviour
    {
        public Team Team { get; private set; }

        private Button _button;
        private Image _background;
        private Image _selectionBorder;
        private Action<Team> _onClicked;

        public void Setup(Team team, Action<Team> onClicked)
        {
            Team = team;
            _onClicked = onClicked;

            _button = GetComponent<Button>();
            _background = GetComponent<Image>();

            if (_button != null)
            {
                _button.onClick.AddListener(() => _onClicked?.Invoke(Team));
            }
        }

        public void SetSelected(bool selected)
        {
            if (_background != null)
            {
                _background.color = selected
                    ? new Color(0.2f, 0.4f, 0.6f)
                    : new Color(0.2f, 0.2f, 0.3f);
            }
        }
    }
}
