using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] private UnifiedCareerProfile _career;
        [SerializeField] private string _playerTeamId;
        [SerializeField] private int _currentSeason;
        [SerializeField] private DateTime _currentDate;

        public UnifiedCareerProfile Career => _career;
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
        private PersonnelManager _personnelManager;
        public PersonnelManager PersonnelManager => _personnelManager;
        private InjuryManager _injuryManager;
        public InjuryManager InjuryManager => _injuryManager;
        private PlayoffManager _playoffManager;
        public PlayoffManager PlayoffManager => _playoffManager;
        private HistoryManager _historyManager;
        public HistoryManager HistoryManager => _historyManager;
        private TrainingCampManager _trainingCampManager;
        public TrainingCampManager TrainingCampManager => _trainingCampManager;
        private MoraleChemistryManager _moraleChemistryManager;
        public MoraleChemistryManager MoraleChemistryManager => _moraleChemistryManager;
        private PersonalityManager _personalityManager;
        public PersonalityManager PersonalityManager => _personalityManager;
        private MediaManager _mediaManager;
        public MediaManager MediaManager => _mediaManager;
        private RevenueManager _revenueManager;
        public RevenueManager RevenueManager => _revenueManager;
        private AdvanceScoutingManager _advanceScoutingManager;
        public AdvanceScoutingManager AdvanceScoutingManager => _advanceScoutingManager;
        private FormerPlayerCareerManager _formerPlayerCareerManager;
        public FormerPlayerCareerManager FormerPlayerCareerManager => _formerPlayerCareerManager;

        private FinanceManager _financeManager;
        public FinanceManager FinanceManager => _financeManager;

        // Trade & AI System Managers (Plain C#)
        private AITradeEvaluator _tradeEvaluator;
        public AITradeEvaluator TradeEvaluator => _tradeEvaluator;
        private TradeFinder _tradeFinder;
        public TradeFinder TradeFinder => _tradeFinder;
        private TradeNegotiationManager _tradeNegotiationManager;
        public TradeNegotiationManager TradeNegotiationManager => _tradeNegotiationManager;

        // MatchSimulationController uses its own singleton pattern
        public MatchSimulationController MatchSimulation => MatchSimulationController.Instance;

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
            _salaryCapManager = new SalaryCapManager();
            _rosterManager = new RosterManager(_salaryCapManager);
            
            SeasonController = new SeasonController(this);
            MatchController = new MatchFlowController(this);
            SaveLoad = new SaveLoadManager();
            _personalityManager = new PersonalityManager();
            _financeManager = new FinanceManager();

            // Initialize Trade & AI Systems
            _tradeSystem = new TradeSystem(_salaryCapManager, PlayerDatabase);
            _tradeEvaluator = new AITradeEvaluator(_salaryCapManager, PlayerDatabase);
            _tradeFinder = new TradeFinder(_tradeEvaluator, _salaryCapManager, PlayerDatabase);
            _tradeNegotiationManager = new TradeNegotiationManager(_tradeEvaluator, _tradeSystem, _tradeFinder);

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

        public void StartNewGame(string firstName, string lastName, int age, string teamId, DifficultySettings difficulty, int tactical, int development, int reputation)
        {
            Debug.Log($"[GameManager] Starting new game: Coach {firstName} {lastName}, Team: {teamId}");

            // Create coach career using UnifiedCareerProfile
            _career = UnifiedCareerProfile.CreateForCoaching($"{firstName} {lastName}", DateTime.Now.Year, age, false, "Player_Coach", null);
            _career.IsUserControlled = true;
            _career.TacticalRating = tactical;
            _career.PlayerDevelopment = development;
            _career.Reputation = reputation;
            _playerTeamId = teamId;

            // Initialize season
            _currentSeason = DateTime.Now.Year;
            _currentDate = new DateTime(_currentSeason, 10, 1); // Season starts October

            // Accept the job
            var team = GetTeam(teamId);
            _career.HandleHired(_currentSeason, teamId, team?.Name ?? teamId, UnifiedRole.HeadCoach);
            
            // Set contract details
            _career.ContractYears = 4;
            _career.CurrentSalary = 5_000_000;

            PersonnelManager.Instance?.RegisterProfile(_career);

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
            _jobSecurityManager?.InitializeForNewSeason(); // Logic inside should use PersonnelManager
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

            // Restore playoff state
            if (data.PlayoffData != null)
            {
                _playoffManager?.RestoreFromSave(data.PlayoffData);
            }

            // Restore personality data
            if (data.PersonalityData != null)
            {
                _personalityManager?.LoadSaveData(data.PersonalityData);
            }

            // Restore Unified Careers
            if (data.UnifiedCareers != null)
            {
                PersonnelManager.Instance?.LoadSaveData(data.UnifiedCareers);
            }
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
                CalendarData = SeasonController.CreateCalendarSaveData(),
                PlayoffData = _playoffManager?.CreateSaveData(),
                PersonalityData = _personalityManager?.CreateSaveData(),
                UnifiedCareers = PersonnelManager.Instance?.GetSaveData()
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
            
            if (_career != null)
            {
                _career.CurrentAge++;
                _career.CurrentContractYear++;
            }

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
        public void RegisterPersonnelManager(PersonnelManager manager) => _personnelManager = manager;
        public void RegisterInjuryManager(InjuryManager manager) => _injuryManager = manager;
        public void RegisterPlayoffManager(PlayoffManager manager) => _playoffManager = manager;
        public void RegisterHistoryManager(HistoryManager manager) => _historyManager = manager;
        public void RegisterTrainingCampManager(TrainingCampManager manager) => _trainingCampManager = manager;
        public void RegisterMoraleChemistryManager(MoraleChemistryManager manager) => _moraleChemistryManager = manager;
        public void RegisterMediaManager(MediaManager manager) => _mediaManager = manager;
        public void RegisterRevenueManager(RevenueManager manager) => _revenueManager = manager;
        public void RegisterAdvanceScoutingManager(AdvanceScoutingManager manager) => _advanceScoutingManager = manager;
        public void RegisterFormerPlayerCareerManager(FormerPlayerCareerManager manager) => _formerPlayerCareerManager = manager;

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
