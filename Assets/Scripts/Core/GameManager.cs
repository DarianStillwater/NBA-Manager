using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.UI;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Central game orchestrator - manages state, coordinates all systems
    /// Singleton that persists across scenes
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        #region State Management

        [Header("Game State")]
        [SerializeField] private GameState _currentState = GameState.Booting;
        [SerializeField] private GameState _previousState;
        [SerializeField] private NewGameState _newGameState;
        [SerializeField] private OffseasonState _offseasonState;

        public GameState CurrentState => _currentState;
        public GameState PreviousState => _previousState;
        public NewGameState CurrentNewGameState => _newGameState;
        public OffseasonState CurrentOffseasonState => _offseasonState;

        #endregion

        #region Career & League Data

        [Header("Career Data")]
        [SerializeField] private CoachCareer _career;
        [SerializeField] private string _playerTeamId;
        [SerializeField] private int _currentSeason;
        [SerializeField] private DateTime _currentDate;

        public CoachCareer Career => _career;
        public CoachCareer CoachCareer => _career;  // Alias for compatibility
        public string PlayerTeamId => _playerTeamId;
        public int CurrentSeason => _currentSeason;
        public DateTime CurrentDate => _currentDate;

        [Header("League Data")]
        [SerializeField] private List<Team> _allTeams = new List<Team>();
        [SerializeField] private Dictionary<string, Team> _teamsById = new Dictionary<string, Team>();

        public List<Team> AllTeams => _allTeams;
        public Team PlayerTeam => GetTeam(_playerTeamId);

        #endregion

        #region Manager References

        [Header("Core Managers")]
        public PlayerDatabase PlayerDatabase { get; private set; }
        public SeasonController SeasonController { get; private set; }
        public MatchFlowController MatchController { get; private set; }
        public SaveLoadManager SaveLoad { get; private set; }

        // These managers are MonoBehaviours that register themselves
        // We store references when they initialize
        private SalaryCapManager _salaryCapManager;
        public SalaryCapManager SalaryCapManager => _salaryCapManager;
        private RosterManager _rosterManager;
        private TradeSystem _tradeSystem;
        private FreeAgentManager _freeAgentManager;
        private DraftSystem _draftSystem;
        private OffseasonManager _offseasonManager;
        private PlayerDevelopmentManager _developmentManager;
        private JobSecurityManager _jobSecurityManager;
        private AllStarManager _allStarManager;
        private CoachJobMarketManager _coachJobManager;
        private InjuryManager _injuryManager;
        public InjuryManager InjuryManager => _injuryManager;

        #endregion

        #region Events

        /// <summary>Fired when game state changes</summary>
        public event EventHandler<GameStateChangedEventArgs> OnStateChanged;

        /// <summary>Fired when a new game is started</summary>
        public event Action OnNewGameStarted;

        /// <summary>Fired when a game is loaded</summary>
        public event Action OnGameLoaded;

        /// <summary>Fired when day advances</summary>
        public event Action<DateTime> OnDayAdvanced;

        /// <summary>Fired when season changes</summary>
        public event Action<int> OnSeasonChanged;

        #endregion

        #region Scene Names

        public const string BOOT_SCENE = "Boot";
        public const string MAIN_MENU_SCENE = "MainMenu";
        public const string GAME_SCENE = "Game";
        public const string MATCH_SCENE = "Match";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            Debug.Log("[GameManager] Initializing...");

            // Create non-MonoBehaviour managers
            PlayerDatabase = new PlayerDatabase();
            SeasonController = new SeasonController(this);
            MatchController = new MatchFlowController(this);
            SaveLoad = new SaveLoadManager();

            // Load player/team data
            StartCoroutine(LoadGameData());
        }

        private IEnumerator LoadGameData()
        {
            Debug.Log("[GameManager] Loading game data...");

            // Load players and teams from JSON
            IEnumerator loadRoutine = null;
            try
            {
                loadRoutine = PlayerDatabase.LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Error starting data load: {ex.Message}");
            }

            if (loadRoutine != null)
            {
                yield return loadRoutine;
            }

            // Cache teams
            _allTeams = PlayerDatabase.GetAllTeams();
            _teamsById.Clear();
            foreach (var team in _allTeams)
            {
                if (team != null && !string.IsNullOrEmpty(team.TeamId))
                {
                    _teamsById[team.TeamId] = team;
                }
            }

            Debug.Log($"[GameManager] Loaded {_allTeams.Count} teams, {PlayerDatabase.GetAllPlayers().Count} players");
            Debug.Log($"[GameManager] GameManager.Instance is {(Instance != null ? "SET" : "NULL")}");

            // Transition to main menu
            ChangeState(GameState.MainMenu);
            LoadScene(MAIN_MENU_SCENE);
        }

        #endregion

        #region State Machine

        /// <summary>
        /// Change the game state
        /// </summary>
        public void ChangeState(GameState newState, object data = null)
        {
            if (_currentState == newState) return;

            _previousState = _currentState;
            _currentState = newState;

            Debug.Log($"[GameManager] State: {_previousState} → {_currentState}");

            // Invoke event
            OnStateChanged?.Invoke(this, new GameStateChangedEventArgs(_previousState, _currentState, data));

            // Handle state entry
            OnEnterState(newState, data);
        }

        private void OnEnterState(GameState state, object data)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    // Cleanup any active game data if coming from a game
                    break;

                case GameState.NewGame:
                    _newGameState = NewGameState.CoachCreation;
                    break;

                case GameState.Playing:
                    // Main gameplay loop
                    break;

                case GameState.PreGame:
                    // data should be the CalendarEvent for the game
                    MatchController?.PrepareMatch(data);
                    break;

                case GameState.Match:
                    break;

                case GameState.PostGame:
                    break;

                case GameState.Offseason:
                    _offseasonState = OffseasonState.SeasonEnd;
                    break;
            }
        }

        /// <summary>
        /// Advance to next new game wizard step
        /// </summary>
        public void AdvanceNewGameState()
        {
            _newGameState = _newGameState switch
            {
                NewGameState.CoachCreation => NewGameState.TeamSelection,
                NewGameState.TeamSelection => NewGameState.DifficultySettings,
                NewGameState.DifficultySettings => NewGameState.ContractNegotiation,
                NewGameState.ContractNegotiation => NewGameState.Confirmation,
                _ => NewGameState.Confirmation
            };
        }

        /// <summary>
        /// Go back in new game wizard
        /// </summary>
        public void PreviousNewGameState()
        {
            _newGameState = _newGameState switch
            {
                NewGameState.TeamSelection => NewGameState.CoachCreation,
                NewGameState.DifficultySettings => NewGameState.TeamSelection,
                NewGameState.ContractNegotiation => NewGameState.DifficultySettings,
                NewGameState.Confirmation => NewGameState.ContractNegotiation,
                _ => NewGameState.CoachCreation
            };
        }

        #endregion

        #region New Game

        /// <summary>
        /// Start a brand new career
        /// </summary>
        public void StartNewGame(string firstName, string lastName, int age, string teamId, DifficultySettings difficulty)
        {
            Debug.Log($"[GameManager] Starting new game: Coach {firstName} {lastName}, Team: {teamId}");

            // Create coach career
            _career = CoachCareer.CreateNewCareer(firstName, lastName, age);
            _playerTeamId = teamId;

            // Initialize season
            _currentSeason = DateTime.Now.Year;
            _currentDate = new DateTime(_currentSeason, 10, 1); // Season starts October

            // Accept the job
            var team = GetTeam(teamId);
            var contract = CoachContract.CreateStandardContract(teamId, 4, 5_000_000);
            _career.AcceptJob(teamId, team?.Name ?? teamId, contract);

            // Initialize season calendar for player's team
            SeasonController.InitializeSeason(_currentSeason);

            // Initialize all managers with new game data
            InitializeManagersForNewGame();

            // Fire event
            OnNewGameStarted?.Invoke();

            // Transition to playing state
            ChangeState(GameState.Playing);
            LoadScene(GAME_SCENE);

            // Auto-save
            SaveLoad.AutoSave(CreateSaveData());
        }

        private void InitializeManagersForNewGame()
        {
            // Initialize managers that need setup for new game
            // Most managers are singletons that initialize themselves
            // We just need to trigger any new-game-specific setup

            _jobSecurityManager?.InitializeForNewSeason(_career, _playerTeamId);
            _developmentManager?.InitializeForNewSeason(_allTeams);

            // Generate initial draft class for upcoming draft
            var draftGen = DraftClassGenerator.Instance;
            draftGen?.GenerateDraftClass(_currentSeason + 1);
        }

        #endregion

        #region Load Game

        /// <summary>
        /// Load a saved game
        /// </summary>
        public void LoadGame(string saveName)
        {
            ChangeState(GameState.Loading);

            var saveData = SaveLoad.LoadGame(saveName);
            if (saveData == null)
            {
                Debug.LogError($"[GameManager] Failed to load save: {saveName}");
                ChangeState(GameState.MainMenu);
                return;
            }

            RestoreFromSaveData(saveData);

            OnGameLoaded?.Invoke();

            ChangeState(GameState.Playing);
            LoadScene(GAME_SCENE);
        }

        private void RestoreFromSaveData(SaveData data)
        {
            _career = data.Career;
            _playerTeamId = data.PlayerTeamId;
            _currentSeason = data.CurrentSeason;
            _currentDate = data.CurrentDate;

            // Restore team states
            foreach (var teamState in data.TeamStates)
            {
                var team = GetTeam(teamState.TeamId);
                if (team != null)
                {
                    teamState.ApplyTo(team);
                }
            }

            // Restore player states
            foreach (var playerState in data.PlayerStates)
            {
                var player = PlayerDatabase.GetPlayer(playerState.PlayerId);
                playerState?.ApplyTo(player);
            }

            // Initialize season controller with saved calendar
            SeasonController.RestoreFromSave(data.CalendarData);
        }

        /// <summary>
        /// Create save data from current state
        /// </summary>
        public SaveData CreateSaveData()
        {
            return new SaveData
            {
                SaveVersion = SaveData.CURRENT_VERSION,
                SaveTimestamp = DateTime.Now,
                Career = _career,
                PlayerTeamId = _playerTeamId,
                CurrentSeason = _currentSeason,
                CurrentDate = _currentDate,
                TeamStates = CreateTeamStates(),
                PlayerStates = CreatePlayerStates(),
                CalendarData = SeasonController.CreateCalendarSaveData()
            };
        }

        private List<TeamSaveState> CreateTeamStates()
        {
            var states = new List<TeamSaveState>();
            foreach (var team in _allTeams)
            {
                states.Add(TeamSaveState.CreateFrom(team));
            }
            return states;
        }

        private List<PlayerSaveState> CreatePlayerStates()
        {
            var states = new List<PlayerSaveState>();
            foreach (var player in PlayerDatabase.GetAllPlayers())
            {
                states.Add(PlayerSaveState.CreateFrom(player));
            }
            return states;
        }

        #endregion

        #region Season Progression

        /// <summary>
        /// Advance to the next day
        /// </summary>
        public void AdvanceDay()
        {
            _currentDate = _currentDate.AddDays(1);
            SeasonController.AdvanceDay();
            OnDayAdvanced?.Invoke(_currentDate);
        }

        /// <summary>
        /// Advance to the next scheduled event (usually next game)
        /// </summary>
        public void AdvanceToNextEvent()
        {
            SeasonController.AdvanceToNextEvent();
        }

        /// <summary>
        /// Start a new season
        /// </summary>
        public void StartNewSeason()
        {
            _currentSeason++;
            _currentDate = new DateTime(_currentSeason, 10, 1);
            _career.Age++;
            _career.CurrentContract.CurrentYear++;

            SeasonController.InitializeSeason(_currentSeason);
            OnSeasonChanged?.Invoke(_currentSeason);

            ChangeState(GameState.Playing);
        }

        #endregion

        #region Scene Management

        /// <summary>
        /// Load a scene by name
        /// </summary>
        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneAsync(sceneName));
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            Debug.Log($"[GameManager] Loading scene: {sceneName}");

            var asyncOp = SceneManager.LoadSceneAsync(sceneName);
            while (!asyncOp.isDone)
            {
                yield return null;
            }

            Debug.Log($"[GameManager] Scene loaded: {sceneName}");
        }

        /// <summary>
        /// Return to main menu
        /// </summary>
        public void ReturnToMainMenu()
        {
            // Offer to save first
            SaveLoad.AutoSave(CreateSaveData());

            _career = null;
            _playerTeamId = null;

            ChangeState(GameState.MainMenu);
            LoadScene(MAIN_MENU_SCENE);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get team by ID
        /// </summary>
        public Team GetTeam(string teamId)
        {
            if (string.IsNullOrEmpty(teamId)) return null;
            _teamsById.TryGetValue(teamId, out var team);
            return team;
        }

        /// <summary>
        /// Get player's current team
        /// </summary>
        public Team GetPlayerTeam()
        {
            return GetTeam(_playerTeamId);
        }

        /// <summary>
        /// Check if we have an active game
        /// </summary>
        public bool HasActiveGame => _career != null && !string.IsNullOrEmpty(_playerTeamId);

        #endregion

        #region Manager Registration

        // Managers call these to register themselves when they initialize

        public void RegisterSalaryCapManager(SalaryCapManager manager) => _salaryCapManager = manager;
        public void RegisterRosterManager(RosterManager manager) => _rosterManager = manager;
        public void RegisterTradeSystem(TradeSystem manager) => _tradeSystem = manager;
        public void RegisterFreeAgentManager(FreeAgentManager manager) => _freeAgentManager = manager;
        public void RegisterDraftSystem(DraftSystem manager) => _draftSystem = manager;
        public void RegisterOffseasonManager(OffseasonManager manager) => _offseasonManager = manager;
        public void RegisterDevelopmentManager(PlayerDevelopmentManager manager) => _developmentManager = manager;
        public void RegisterJobSecurityManager(JobSecurityManager manager) => _jobSecurityManager = manager;
        public void RegisterAllStarManager(AllStarManager manager) => _allStarManager = manager;
        public void RegisterCoachJobManager(CoachJobMarketManager manager) => _coachJobManager = manager;
        public void RegisterInjuryManager(InjuryManager manager) => _injuryManager = manager;

        #endregion

        #region Debug

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"State: {_currentState}, Season: {_currentSeason}, Date: {_currentDate:yyyy-MM-dd}");
            Debug.Log($"Coach: {_career?.FullName ?? "None"}, Team: {_playerTeamId ?? "None"}");
            Debug.Log($"Teams: {_allTeams.Count}, Players: {PlayerDatabase?.GetAllPlayers().Count ?? 0}");
        }

        #endregion
    }
}
