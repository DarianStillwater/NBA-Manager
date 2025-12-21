using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// New Game wizard panel - handles coach creation flow
    /// Steps: Coach Creation → Team Selection → Difficulty → Confirmation
    /// </summary>
    public class NewGamePanel : BasePanel
    {
        [Header("Step Containers")]
        [SerializeField] private GameObject _coachCreationStep;
        [SerializeField] private GameObject _teamSelectionStep;
        [SerializeField] private GameObject _roleSelectionStep;
        [SerializeField] private GameObject _difficultyStep;
        [SerializeField] private GameObject _confirmationStep;

        [Header("Coach Creation")]
        [SerializeField] private InputField _firstNameInput;
        [SerializeField] private InputField _lastNameInput;
        [SerializeField] private Slider _ageSlider;
        [SerializeField] private Text _ageValueText;
        [SerializeField] private Dropdown _backgroundDropdown;
        [SerializeField] private Text _backgroundDescriptionText;

        [Header("Team Selection")]
        [SerializeField] private Transform _teamListContainer;
        [SerializeField] private GameObject _teamCardPrefab;
        [SerializeField] private Text _selectedTeamNameText;
        [SerializeField] private Text _selectedTeamInfoText;
        [SerializeField] private Dropdown _conferenceFilter;
        [SerializeField] private Dropdown _situationFilter;

        [Header("Role Selection")]
        [SerializeField] private Toggle _roleGMOnlyToggle;
        [SerializeField] private Toggle _roleCoachOnlyToggle;
        [SerializeField] private Toggle _roleBothToggle;
        [SerializeField] private Text _roleDescriptionText;
        [SerializeField] private Text _aiCounterpartText;

        [Header("Difficulty")]
        [SerializeField] private Dropdown _difficultyPresetDropdown;
        [SerializeField] private Slider _aiAggressivenessSlider;
        [SerializeField] private Slider _injuryFrequencySlider;
        [SerializeField] private Slider _ownerPatienceSlider;
        [SerializeField] private Toggle _customDifficultyToggle;
        [SerializeField] private GameObject _customDifficultyPanel;

        [Header("Confirmation")]
        [SerializeField] private Text _summaryCoachText;
        [SerializeField] private Text _summaryTeamText;
        [SerializeField] private Text _summaryDifficultyText;
        [SerializeField] private Text _contractOfferText;

        [Header("Navigation")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Text _stepIndicatorText;

        // Current state
        private NewGameState _currentStep = NewGameState.CoachCreation;
        private NewGameData _gameData = new NewGameData();
        private List<Team> _allTeams = new List<Team>();
        private List<TeamCard> _teamCards = new List<TeamCard>();

        // Coach backgrounds
        private readonly CoachBackground[] _backgrounds = new[]
        {
            new CoachBackground("Former Player", "High reputation from playing career, still learning the coaching craft.", 70, 45, 60),
            new CoachBackground("College Coach", "Balanced experience with proven track record at the collegiate level.", 55, 60, 55),
            new CoachBackground("Assistant Coach", "Years of NBA experience as an assistant, strong tactical knowledge.", 50, 75, 50),
            new CoachBackground("International Coach", "Unique perspective from European or international leagues.", 45, 65, 70),
            new CoachBackground("Analytics Pioneer", "Data-driven approach, innovative but unproven at NBA level.", 40, 55, 80)
        };

        public override void Initialize()
        {
            base.Initialize();

            SetupCoachCreation();
            SetupTeamSelection();
            SetupRoleSelection();
            SetupDifficulty();
            SetupNavigation();

            ShowStep(NewGameState.CoachCreation);
        }

        #region Setup

        private void SetupCoachCreation()
        {
            // Age slider
            if (_ageSlider != null)
            {
                _ageSlider.minValue = 35;
                _ageSlider.maxValue = 65;
                _ageSlider.value = 45;
                _ageSlider.onValueChanged.AddListener(OnAgeChanged);
                OnAgeChanged(45);
            }

            // Background dropdown
            if (_backgroundDropdown != null)
            {
                _backgroundDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var bg in _backgrounds)
                {
                    options.Add(bg.Name);
                }
                _backgroundDropdown.AddOptions(options);
                _backgroundDropdown.onValueChanged.AddListener(OnBackgroundChanged);
                OnBackgroundChanged(0);
            }

            // Default names
            if (_firstNameInput != null) _firstNameInput.text = "";
            if (_lastNameInput != null) _lastNameInput.text = "";
        }

        private void SetupTeamSelection()
        {
            // Get teams from GameManager
            if (GameManager.Instance != null)
            {
                _allTeams = GameManager.Instance.AllTeams;
            }

            // Conference filter
            if (_conferenceFilter != null)
            {
                _conferenceFilter.ClearOptions();
                _conferenceFilter.AddOptions(new List<string> { "All Teams", "Eastern Conference", "Western Conference" });
                _conferenceFilter.onValueChanged.AddListener(_ => RefreshTeamList());
            }

            // Situation filter
            if (_situationFilter != null)
            {
                _situationFilter.ClearOptions();
                _situationFilter.AddOptions(new List<string> { "All Situations", "Contenders", "Rebuilding", "Middle Tier" });
                _situationFilter.onValueChanged.AddListener(_ => RefreshTeamList());
            }

            RefreshTeamList();
        }

        private void SetupRoleSelection()
        {
            // Default to "Both" role
            _gameData.SelectedRole = UserRole.Both;

            // Setup toggle group behavior (radio buttons)
            if (_roleBothToggle != null)
            {
                _roleBothToggle.isOn = true;
                _roleBothToggle.onValueChanged.AddListener(isOn => { if (isOn) OnRoleSelected(UserRole.Both); });
            }

            if (_roleGMOnlyToggle != null)
            {
                _roleGMOnlyToggle.isOn = false;
                _roleGMOnlyToggle.onValueChanged.AddListener(isOn => { if (isOn) OnRoleSelected(UserRole.GMOnly); });
            }

            if (_roleCoachOnlyToggle != null)
            {
                _roleCoachOnlyToggle.isOn = false;
                _roleCoachOnlyToggle.onValueChanged.AddListener(isOn => { if (isOn) OnRoleSelected(UserRole.HeadCoachOnly); });
            }

            // Update initial description
            UpdateRoleDescription(UserRole.Both);
        }

        private void SetupDifficulty()
        {
            // Preset dropdown
            if (_difficultyPresetDropdown != null)
            {
                _difficultyPresetDropdown.ClearOptions();
                _difficultyPresetDropdown.AddOptions(new List<string> { "Easy", "Normal", "Hard", "Legendary", "Custom" });
                _difficultyPresetDropdown.value = 1; // Normal
                _difficultyPresetDropdown.onValueChanged.AddListener(OnDifficultyPresetChanged);
            }

            // Custom toggle
            if (_customDifficultyToggle != null)
            {
                _customDifficultyToggle.onValueChanged.AddListener(OnCustomToggleChanged);
            }

            // Initialize sliders
            SetDifficultySliders(DifficultySettings.CreateFromPreset(DifficultyPreset.Normal));
        }

        private void SetupNavigation()
        {
            if (_nextButton != null)
                _nextButton.onClick.AddListener(OnNextClicked);
            if (_backButton != null)
                _backButton.onClick.AddListener(OnBackClicked);
            if (_startGameButton != null)
                _startGameButton.onClick.AddListener(OnStartGameClicked);
        }

        #endregion

        #region Step Navigation

        private void ShowStep(NewGameState step)
        {
            _currentStep = step;

            // Hide all steps
            SetActive(_coachCreationStep, false);
            SetActive(_teamSelectionStep, false);
            SetActive(_roleSelectionStep, false);
            SetActive(_difficultyStep, false);
            SetActive(_confirmationStep, false);

            // Show current step
            switch (step)
            {
                case NewGameState.CoachCreation:
                    SetActive(_coachCreationStep, true);
                    UpdateStepIndicator("Step 1 of 5: Create Your Coach");
                    break;

                case NewGameState.TeamSelection:
                    SetActive(_teamSelectionStep, true);
                    UpdateStepIndicator("Step 2 of 5: Select Your Team");
                    RefreshTeamList();
                    break;

                case NewGameState.RoleSelection:
                    SetActive(_roleSelectionStep, true);
                    UpdateStepIndicator("Step 3 of 5: Choose Your Role");
                    UpdateRoleDescription(_gameData.SelectedRole);
                    break;

                case NewGameState.DifficultySettings:
                    SetActive(_difficultyStep, true);
                    UpdateStepIndicator("Step 4 of 5: Difficulty Settings");
                    break;

                case NewGameState.Confirmation:
                    SetActive(_confirmationStep, true);
                    UpdateStepIndicator("Step 5 of 5: Confirm & Start");
                    UpdateConfirmationSummary();
                    break;
            }

            // Update button visibility
            UpdateNavigationButtons();
        }

        private void UpdateNavigationButtons()
        {
            bool isFirstStep = _currentStep == NewGameState.CoachCreation;
            bool isLastStep = _currentStep == NewGameState.Confirmation;

            if (_backButton != null)
                _backButton.gameObject.SetActive(!isFirstStep);
            if (_nextButton != null)
                _nextButton.gameObject.SetActive(!isLastStep);
            if (_startGameButton != null)
                _startGameButton.gameObject.SetActive(isLastStep);
        }

        private void UpdateStepIndicator(string text)
        {
            if (_stepIndicatorText != null)
                _stepIndicatorText.text = text;
        }

        private void OnNextClicked()
        {
            // Validate current step
            if (!ValidateCurrentStep()) return;

            // Save current step data
            SaveCurrentStepData();

            // Move to next step
            _currentStep = _currentStep switch
            {
                NewGameState.CoachCreation => NewGameState.TeamSelection,
                NewGameState.TeamSelection => NewGameState.RoleSelection,
                NewGameState.RoleSelection => NewGameState.DifficultySettings,
                NewGameState.DifficultySettings => NewGameState.Confirmation,
                _ => _currentStep
            };

            ShowStep(_currentStep);
        }

        private void OnBackClicked()
        {
            _currentStep = _currentStep switch
            {
                NewGameState.TeamSelection => NewGameState.CoachCreation,
                NewGameState.RoleSelection => NewGameState.TeamSelection,
                NewGameState.DifficultySettings => NewGameState.RoleSelection,
                NewGameState.Confirmation => NewGameState.DifficultySettings,
                _ => _currentStep
            };

            ShowStep(_currentStep);
        }

        private bool ValidateCurrentStep()
        {
            switch (_currentStep)
            {
                case NewGameState.CoachCreation:
                    string firstName = _firstNameInput?.text?.Trim() ?? "";
                    string lastName = _lastNameInput?.text?.Trim() ?? "";

                    if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
                    {
                        Debug.Log("Please enter first and last name");
                        return false;
                    }
                    return true;

                case NewGameState.TeamSelection:
                    if (string.IsNullOrEmpty(_gameData.SelectedTeamId))
                    {
                        Debug.Log("Please select a team");
                        return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        private void SaveCurrentStepData()
        {
            switch (_currentStep)
            {
                case NewGameState.CoachCreation:
                    _gameData.FirstName = _firstNameInput?.text?.Trim() ?? "Coach";
                    _gameData.LastName = _lastNameInput?.text?.Trim() ?? "Player";
                    _gameData.Age = Mathf.RoundToInt(_ageSlider?.value ?? 45);
                    _gameData.BackgroundIndex = _backgroundDropdown?.value ?? 0;
                    _gameData.Background = _backgrounds[_gameData.BackgroundIndex];
                    break;

                case NewGameState.DifficultySettings:
                    SaveDifficultySettings();
                    break;
            }
        }

        #endregion

        #region Coach Creation

        private void OnAgeChanged(float value)
        {
            int age = Mathf.RoundToInt(value);
            if (_ageValueText != null)
                _ageValueText.text = $"{age} years old";
        }

        private void OnBackgroundChanged(int index)
        {
            if (index >= 0 && index < _backgrounds.Length)
            {
                var bg = _backgrounds[index];
                if (_backgroundDescriptionText != null)
                {
                    _backgroundDescriptionText.text = $"{bg.Description}\n\n" +
                        $"Starting Reputation: {bg.StartingReputation}\n" +
                        $"Tactical Knowledge: {bg.TacticalKnowledge}\n" +
                        $"Player Development: {bg.DevelopmentSkill}";
                }
            }
        }

        #endregion

        #region Role Selection

        private void OnRoleSelected(UserRole role)
        {
            _gameData.SelectedRole = role;
            UpdateRoleDescription(role);
        }

        private void UpdateRoleDescription(UserRole role)
        {
            string description = "";
            string aiCounterpart = "";

            switch (role)
            {
                case UserRole.GMOnly:
                    description = "GENERAL MANAGER\n\n" +
                        "• Control all roster decisions: trades, free agency, draft\n" +
                        "• Hire and fire coaching staff at any time\n" +
                        "• Games are simulated with AI head coach making all decisions\n" +
                        "• Receive game summaries and box scores after each game\n" +
                        "• Evaluate your coach's performance and make changes as needed";
                    aiCounterpart = "An AI Head Coach will be generated with hidden personality traits that you'll discover through their coaching decisions.";
                    break;

                case UserRole.HeadCoachOnly:
                    description = "HEAD COACH\n\n" +
                        "• Control all in-game decisions: plays, subs, timeouts\n" +
                        "• Manage player development and training focus\n" +
                        "• Request roster moves from the GM (they may approve or deny)\n" +
                        "• Focus on X's and O's without front office distractions\n" +
                        "• Job security depends on your team's performance";
                    aiCounterpart = "An AI General Manager will control roster decisions. You can request players, but the GM decides. Their personality is hidden and revealed through their decisions.";
                    break;

                case UserRole.Both:
                default:
                    description = "GM & HEAD COACH\n\n" +
                        "• Full control over all team operations\n" +
                        "• Make all roster decisions and in-game coaching calls\n" +
                        "• The complete NBA management experience\n" +
                        "• Build your roster AND coach your team to victory\n" +
                        "• Traditional game mode with maximum control";
                    aiCounterpart = "No AI counterpart - you handle everything yourself!";
                    break;
            }

            if (_roleDescriptionText != null)
                _roleDescriptionText.text = description;

            if (_aiCounterpartText != null)
                _aiCounterpartText.text = aiCounterpart;
        }

        #endregion

        #region Team Selection

        private void RefreshTeamList()
        {
            // Clear existing cards
            foreach (var card in _teamCards)
            {
                if (card != null && card.gameObject != null)
                    Destroy(card.gameObject);
            }
            _teamCards.Clear();

            // Filter teams
            var filteredTeams = FilterTeams();

            // Create team cards
            foreach (var team in filteredTeams)
            {
                CreateTeamCard(team);
            }
        }

        private List<Team> FilterTeams()
        {
            var filtered = new List<Team>(_allTeams);

            // Conference filter
            if (_conferenceFilter != null && _conferenceFilter.value > 0)
            {
                string conference = _conferenceFilter.value == 1 ? "Eastern" : "Western";
                filtered = filtered.FindAll(t => t.Conference == conference);
            }

            // Situation filter (simplified - would use actual team analysis)
            if (_situationFilter != null && _situationFilter.value > 0)
            {
                // This would filter by team situation
            }

            // Sort by city name
            filtered.Sort((a, b) => string.Compare(a.City, b.City, StringComparison.Ordinal));

            return filtered;
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
                // Create simple card if no prefab
                cardGO = CreateSimpleTeamCard(team);
            }

            var card = cardGO.GetComponent<TeamCard>();
            if (card == null)
                card = cardGO.AddComponent<TeamCard>();

            card.Setup(team, OnTeamSelected);
            _teamCards.Add(card);

            // Highlight if selected
            if (_gameData.SelectedTeamId == team.TeamId)
            {
                card.SetSelected(true);
            }
        }

        private GameObject CreateSimpleTeamCard(Team team)
        {
            var cardGO = new GameObject($"TeamCard_{team.TeamId}");
            cardGO.transform.SetParent(_teamListContainer, false);

            // Add layout element
            var layoutElement = cardGO.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 280;
            layoutElement.preferredHeight = 80;

            // Background
            var image = cardGO.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.2f);

            // Button
            var button = cardGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.25f, 0.25f, 0.35f);
            colors.selectedColor = new Color(0.2f, 0.3f, 0.5f);
            button.colors = colors;

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(cardGO.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = $"{team.City} {team.Name}\n{team.Wins}-{team.Losses}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(15, 5);
            textRect.offsetMax = new Vector2(-15, -5);

            return cardGO;
        }

        private void OnTeamSelected(Team team)
        {
            _gameData.SelectedTeamId = team.TeamId;
            _gameData.SelectedTeam = team;

            // Update selection UI
            foreach (var card in _teamCards)
            {
                card.SetSelected(card.Team.TeamId == team.TeamId);
            }

            // Update info text
            if (_selectedTeamNameText != null)
                _selectedTeamNameText.text = $"{team.City} {team.Name}";

            if (_selectedTeamInfoText != null)
            {
                string info = $"Record: {team.Wins}-{team.Losses}\n" +
                             $"Conference: {team.Conference}\n" +
                             $"Division: {team.Division}\n" +
                             $"Arena: {team.ArenaName}";
                _selectedTeamInfoText.text = info;
            }
        }

        #endregion

        #region Difficulty Settings

        private void OnDifficultyPresetChanged(int index)
        {
            if (index < 4) // Not custom
            {
                var preset = (DifficultyPreset)index;
                var settings = DifficultySettings.CreateFromPreset(preset);
                SetDifficultySliders(settings);

                if (_customDifficultyToggle != null)
                    _customDifficultyToggle.isOn = false;
            }
        }

        private void OnCustomToggleChanged(bool isCustom)
        {
            SetActive(_customDifficultyPanel, isCustom);

            if (isCustom && _difficultyPresetDropdown != null)
            {
                _difficultyPresetDropdown.value = 4; // Custom
            }
        }

        private void SetDifficultySliders(DifficultySettings settings)
        {
            if (_aiAggressivenessSlider != null)
                _aiAggressivenessSlider.value = settings.AIAggressiveness;
            if (_injuryFrequencySlider != null)
                _injuryFrequencySlider.value = settings.InjuryFrequency;
            if (_ownerPatienceSlider != null)
                _ownerPatienceSlider.value = settings.OwnerPatience;
        }

        private void SaveDifficultySettings()
        {
            int presetIndex = _difficultyPresetDropdown?.value ?? 1;

            if (presetIndex < 4)
            {
                _gameData.Difficulty = DifficultySettings.CreateFromPreset((DifficultyPreset)presetIndex);
            }
            else
            {
                // Custom settings
                _gameData.Difficulty = new DifficultySettings
                {
                    Preset = DifficultyPreset.Custom,
                    AIAggressiveness = _aiAggressivenessSlider?.value ?? 0.5f,
                    InjuryFrequency = _injuryFrequencySlider?.value ?? 0.5f,
                    OwnerPatience = _ownerPatienceSlider?.value ?? 0.5f
                };
            }
        }

        #endregion

        #region Confirmation

        private void UpdateConfirmationSummary()
        {
            // Coach summary with role
            if (_summaryCoachText != null)
            {
                string bgName = _gameData.Background?.Name ?? "Unknown";
                string roleName = GetRoleDisplayName(_gameData.SelectedRole);
                _summaryCoachText.text = $"{_gameData.FirstName} {_gameData.LastName}\n" +
                                        $"Age: {_gameData.Age}\n" +
                                        $"Background: {bgName}\n" +
                                        $"Role: {roleName}";
            }

            // Team summary
            if (_summaryTeamText != null && _gameData.SelectedTeam != null)
            {
                var team = _gameData.SelectedTeam;
                _summaryTeamText.text = $"Team: {team.City} {team.Name}\n" +
                                       $"Record: {team.Wins}-{team.Losses}\n" +
                                       $"Conference: {team.Conference}";
            }

            // Difficulty summary
            if (_summaryDifficultyText != null)
            {
                _summaryDifficultyText.text = $"Difficulty: {_gameData.Difficulty?.Preset ?? DifficultyPreset.Normal}";
            }

            // Contract offer
            if (_contractOfferText != null)
            {
                int years = 4;
                long salary = CalculateContractOffer();
                _contractOfferText.text = $"Contract Offer:\n" +
                                         $"{years} Years, ${salary / 1000000.0:F1}M per year\n" +
                                         $"Total Value: ${(salary * years) / 1000000.0:F1}M";
            }
        }

        private long CalculateContractOffer()
        {
            // Base salary based on background reputation
            long baseSalary = 3_000_000;

            if (_gameData.Background != null)
            {
                baseSalary += (_gameData.Background.StartingReputation - 50) * 50_000;
            }

            return baseSalary;
        }

        #endregion

        #region Start Game

        private void OnStartGameClicked()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("GameManager not found!");
                return;
            }

            // Start the game
            var background = _gameData.Background;
            int tactical = background?.TacticalKnowledge ?? 50;
            int development = background?.DevelopmentSkill ?? 50;
            int reputation = background?.StartingReputation ?? 50;

            GameManager.Instance.StartNewGame(
                _gameData.FirstName,
                _gameData.LastName,
                _gameData.Age,
                _gameData.SelectedTeamId,
                _gameData.Difficulty,
                tactical,
                development,
                reputation,
                _gameData.SelectedRole
            );
        }

        #endregion

        #region Helpers

        private void SetActive(GameObject go, bool active)
        {
            if (go != null)
                go.SetActive(active);
        }

        private string GetRoleDisplayName(UserRole role)
        {
            return role switch
            {
                UserRole.GMOnly => "General Manager",
                UserRole.HeadCoachOnly => "Head Coach",
                UserRole.Both => "GM & Head Coach",
                _ => "Unknown"
            };
        }

        #endregion
    }

    /// <summary>
    /// Data collected during new game wizard
    /// </summary>
    [Serializable]
    public class NewGameData
    {
        public string FirstName;
        public string LastName;
        public int Age = 45;
        public int BackgroundIndex;
        public CoachBackground Background;
        public string SelectedTeamId;
        public Team SelectedTeam;
        public UserRole SelectedRole = UserRole.Both;
        public DifficultySettings Difficulty;
    }

    /// <summary>
    /// Coach background option
    /// </summary>
    [Serializable]
    public class CoachBackground
    {
        public string Name;
        public string Description;
        public int StartingReputation;
        public int TacticalKnowledge;
        public int DevelopmentSkill;

        public CoachBackground(string name, string desc, int rep, int tactical, int dev)
        {
            Name = name;
            Description = desc;
            StartingReputation = rep;
            TacticalKnowledge = tactical;
            DevelopmentSkill = dev;
        }
    }

    /// <summary>
    /// Team card component for team selection
    /// </summary>
    public class TeamCard : MonoBehaviour
    {
        public Team Team { get; private set; }

        private Button _button;
        private Image _background;
        private Action<Team> _onSelected;
        private bool _isSelected;

        public void Setup(Team team, Action<Team> onSelected)
        {
            Team = team;
            _onSelected = onSelected;

            _button = GetComponent<Button>();
            _background = GetComponent<Image>();

            if (_button != null)
            {
                _button.onClick.AddListener(() => _onSelected?.Invoke(Team));
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (_background != null)
            {
                _background.color = selected
                    ? new Color(0.2f, 0.35f, 0.5f)
                    : new Color(0.15f, 0.15f, 0.2f);
            }
        }
    }
}
