using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI
{
    /// <summary>
    /// Controller for the Main Menu scene
    /// Handles New Game, Load Game, Settings, and Quit
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Button References")]
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _loadGameButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _newGamePanel;
        [SerializeField] private GameObject _loadGamePanel;
        [SerializeField] private GameObject _settingsPanel;

        [Header("New Game UI")]
        [SerializeField] private InputField _coachFirstNameInput;
        [SerializeField] private InputField _coachLastNameInput;
        [SerializeField] private Dropdown _teamDropdown;
        [SerializeField] private Dropdown _difficultyDropdown;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _backFromNewGameButton;

        [Header("Load Game UI")]
        [SerializeField] private Transform _saveSlotContainer;
        [SerializeField] private Button _loadSelectedButton;
        [SerializeField] private Button _deleteSelectedButton;
        [SerializeField] private Button _backFromLoadButton;

        private string _selectedSaveSlot;
        private List<Team> _availableTeams = new List<Team>();

        private void Start()
        {
            // Find buttons if not assigned
            FindUIReferences();

            // Setup button listeners
            SetupButtonListeners();

            // Initialize dropdowns
            InitializeDropdowns();

            // Show main menu
            ShowMainMenu();
        }

        private void FindUIReferences()
        {
            // Try to find buttons by name if not assigned
            if (_newGameButton == null)
                _newGameButton = FindButtonByName("NewGameButton");
            if (_loadGameButton == null)
                _loadGameButton = FindButtonByName("LoadGameButton");
            if (_settingsButton == null)
                _settingsButton = FindButtonByName("SettingsButton");
            if (_quitButton == null)
                _quitButton = FindButtonByName("QuitButton");
        }

        private Button FindButtonByName(string name)
        {
            var go = GameObject.Find(name);
            return go?.GetComponent<Button>();
        }

        private void SetupButtonListeners()
        {
            // Main menu buttons
            if (_newGameButton != null)
                _newGameButton.onClick.AddListener(OnNewGameClicked);
            if (_loadGameButton != null)
                _loadGameButton.onClick.AddListener(OnLoadGameClicked);
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuitClicked);

            // New game buttons
            if (_startGameButton != null)
                _startGameButton.onClick.AddListener(OnStartGameClicked);
            if (_backFromNewGameButton != null)
                _backFromNewGameButton.onClick.AddListener(ShowMainMenu);

            // Load game buttons
            if (_loadSelectedButton != null)
                _loadSelectedButton.onClick.AddListener(OnLoadSelectedClicked);
            if (_deleteSelectedButton != null)
                _deleteSelectedButton.onClick.AddListener(OnDeleteSelectedClicked);
            if (_backFromLoadButton != null)
                _backFromLoadButton.onClick.AddListener(ShowMainMenu);
        }

        private void InitializeDropdowns()
        {
            // Team dropdown
            if (_teamDropdown != null && GameManager.Instance != null)
            {
                _teamDropdown.ClearOptions();
                _availableTeams = GameManager.Instance.AllTeams;

                var options = new List<string>();
                foreach (var team in _availableTeams)
                {
                    options.Add($"{team.City} {team.Name}");
                }
                _teamDropdown.AddOptions(options);
            }

            // Difficulty dropdown
            if (_difficultyDropdown != null)
            {
                _difficultyDropdown.ClearOptions();
                _difficultyDropdown.AddOptions(new List<string>
                {
                    "Easy",
                    "Normal",
                    "Hard",
                    "Legendary"
                });
                _difficultyDropdown.value = 1; // Default to Normal
            }
        }

        #region Panel Management

        private void ShowMainMenu()
        {
            SetPanelActive(_mainMenuPanel, true);
            SetPanelActive(_newGamePanel, false);
            SetPanelActive(_loadGamePanel, false);
            SetPanelActive(_settingsPanel, false);
        }

        private void ShowNewGamePanel()
        {
            SetPanelActive(_mainMenuPanel, false);
            SetPanelActive(_newGamePanel, true);
            SetPanelActive(_loadGamePanel, false);
            SetPanelActive(_settingsPanel, false);

            // Clear inputs
            if (_coachFirstNameInput != null) _coachFirstNameInput.text = "";
            if (_coachLastNameInput != null) _coachLastNameInput.text = "";
        }

        private void ShowLoadGamePanel()
        {
            SetPanelActive(_mainMenuPanel, false);
            SetPanelActive(_newGamePanel, false);
            SetPanelActive(_loadGamePanel, true);
            SetPanelActive(_settingsPanel, false);

            // Populate save slots
            PopulateSaveSlots();
        }

        private void ShowSettingsPanel()
        {
            SetPanelActive(_mainMenuPanel, false);
            SetPanelActive(_newGamePanel, false);
            SetPanelActive(_loadGamePanel, false);
            SetPanelActive(_settingsPanel, true);
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        #endregion

        #region Button Handlers

        private void OnNewGameClicked()
        {
            Debug.Log("[MainMenuController] New Game clicked");

            // If we have a new game panel, show it
            if (_newGamePanel != null)
            {
                ShowNewGamePanel();
            }
            else
            {
                // Quick start with defaults
                QuickStartNewGame();
            }
        }

        private void OnLoadGameClicked()
        {
            Debug.Log("[MainMenuController] Load Game clicked");

            if (_loadGamePanel != null)
            {
                ShowLoadGamePanel();
            }
            else
            {
                // Try to load most recent auto-save
                LoadMostRecent();
            }
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[MainMenuController] Settings clicked");

            if (_settingsPanel != null)
            {
                ShowSettingsPanel();
            }
        }

        private void OnQuitClicked()
        {
            Debug.Log("[MainMenuController] Quit clicked");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnStartGameClicked()
        {
            string firstName = _coachFirstNameInput?.text ?? "John";
            string lastName = _coachLastNameInput?.text ?? "Smith";

            if (string.IsNullOrWhiteSpace(firstName)) firstName = "John";
            if (string.IsNullOrWhiteSpace(lastName)) lastName = "Smith";

            // Get selected team
            string teamId = "LAL"; // Default
            if (_teamDropdown != null && _availableTeams.Count > 0)
            {
                int index = _teamDropdown.value;
                if (index >= 0 && index < _availableTeams.Count)
                {
                    teamId = _availableTeams[index].TeamId;
                }
            }

            // Get difficulty
            DifficultyPreset difficulty = DifficultyPreset.Normal;
            if (_difficultyDropdown != null)
            {
                difficulty = (DifficultyPreset)_difficultyDropdown.value;
            }

            StartNewGame(firstName, lastName, teamId, difficulty);
        }

        private void OnLoadSelectedClicked()
        {
            if (!string.IsNullOrEmpty(_selectedSaveSlot) && GameManager.Instance != null)
            {
                GameManager.Instance.LoadGame(_selectedSaveSlot);
            }
        }

        private void OnDeleteSelectedClicked()
        {
            if (!string.IsNullOrEmpty(_selectedSaveSlot) && GameManager.Instance != null)
            {
                GameManager.Instance.SaveLoad.DeleteSave(_selectedSaveSlot);
                PopulateSaveSlots();
            }
        }

        #endregion

        #region Game Start

        private void QuickStartNewGame()
        {
            // Start with defaults
            StartNewGame("Coach", "Player", "LAL", DifficultyPreset.Normal);
        }

        private void StartNewGame(string firstName, string lastName, string teamId, DifficultyPreset difficulty)
        {
            Debug.Log($"[MainMenuController] Starting new game: {firstName} {lastName}, Team: {teamId}, Difficulty: {difficulty}");

            if (GameManager.Instance != null)
            {
                var diffSettings = DifficultySettings.CreateFromPreset(difficulty);
                GameManager.Instance.StartNewGame(firstName, lastName, 45, teamId, diffSettings);
            }
            else
            {
                Debug.LogError("[MainMenuController] GameManager not found!");
            }
        }

        private void LoadMostRecent()
        {
            if (GameManager.Instance != null)
            {
                var saveData = GameManager.Instance.SaveLoad.LoadLatestAutoSave();
                if (saveData != null)
                {
                    GameManager.Instance.LoadGame(saveData.SaveSlot);
                }
                else
                {
                    Debug.Log("[MainMenuController] No saves found");
                }
            }
        }

        #endregion

        #region Save Slots

        private void PopulateSaveSlots()
        {
            if (_saveSlotContainer == null || GameManager.Instance == null) return;

            // Clear existing slots
            foreach (Transform child in _saveSlotContainer)
            {
                Destroy(child.gameObject);
            }

            // Get all saves
            var saves = GameManager.Instance.SaveLoad.GetAllSaves();

            foreach (var save in saves)
            {
                CreateSaveSlotUI(save);
            }

            _selectedSaveSlot = null;
            UpdateLoadButtons();
        }

        private void CreateSaveSlotUI(SaveSlotInfo save)
        {
            // Create save slot button
            var slotGO = new GameObject(save.SlotName);
            slotGO.transform.SetParent(_saveSlotContainer, false);

            var button = slotGO.AddComponent<Button>();
            var image = slotGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.3f);

            var rect = slotGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 80);

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(slotGO.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = $"{save.SaveName}\n{save.CoachName} - {save.TeamId}\nSeason {save.Season} | {save.Record}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            // Click handler
            string slotName = save.SlotName;
            button.onClick.AddListener(() => SelectSaveSlot(slotName));
        }

        private void SelectSaveSlot(string slotName)
        {
            _selectedSaveSlot = slotName;
            UpdateLoadButtons();
        }

        private void UpdateLoadButtons()
        {
            bool hasSelection = !string.IsNullOrEmpty(_selectedSaveSlot);

            if (_loadSelectedButton != null)
                _loadSelectedButton.interactable = hasSelection;
            if (_deleteSelectedButton != null)
                _deleteSelectedButton.interactable = hasSelection;
        }

        #endregion
    }
}
