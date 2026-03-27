using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.AI;
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
        [SerializeField] private UserRoleConfiguration _userRoleConfig;

        public UnifiedCareerProfile Career => _career;
        public string PlayerTeamId => _playerTeamId;
        public UserRoleConfiguration UserRoleConfig => _userRoleConfig;
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
        public Manager.LeagueStatsAggregator LeagueStats { get; private set; } = new Manager.LeagueStatsAggregator();
        public SeasonController SeasonController { get; private set; }
        public MatchFlowController MatchController { get; private set; }
        public SaveLoadManager SaveLoad { get; private set; }
        public GamePreferences Preferences { get; set; } = new GamePreferences();

        // These managers are MonoBehaviours that register themselves
        // We store references when they initialize
        private SalaryCapManager _salaryCapManager;
        public SalaryCapManager SalaryCapManager => _salaryCapManager;
        private RosterManager _rosterManager;
        private TradeSystem _tradeSystem;
        private FreeAgentManager _freeAgentManager;
        private DraftSystem _draftSystem;
        public DraftSystem DraftSystem => _draftSystem;
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
        private JobMarketManager _jobMarketManager;
        public JobMarketManager JobMarketManager => _jobMarketManager;

        private FinanceManager _financeManager;
        public FinanceManager FinanceManager => _financeManager;

        // Trade & AI System Managers (Plain C#)
        private AITradeEvaluator _tradeEvaluator;
        public AITradeEvaluator TradeEvaluator => _tradeEvaluator;
        private TradeFinder _tradeFinder;
        public TradeFinder TradeFinder => _tradeFinder;
        private TradeNegotiationManager _tradeNegotiationManager;
        public TradeNegotiationManager TradeNegotiationManager => _tradeNegotiationManager;

        // Trade AI Enhancement Managers
        private DraftPickRegistry _draftPickRegistry;
        public DraftPickRegistry DraftPickRegistry => _draftPickRegistry;
        private AITradeOfferGenerator _tradeOfferGenerator;
        public AITradeOfferGenerator TradeOfferGenerator => _tradeOfferGenerator;
        private TradeAnnouncementSystem _tradeAnnouncementSystem;
        public TradeAnnouncementSystem TradeAnnouncementSystem => _tradeAnnouncementSystem;

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

        /// <summary>Fired when captain selection is required (season start or captain traded/cut)</summary>
        public event Action<Team, bool> OnCaptainSelectionRequired; // Team, isReplacement

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

                // Auto-attach GameShell (FM layout) + ArtInjector (panel content)
                var gameShellType = System.Type.GetType("NBAHeadCoach.UI.Shell.GameShell");
                if (gameShellType != null && GetComponent(gameShellType) == null)
                    gameObject.AddComponent(gameShellType);

                var artInjectorType = System.Type.GetType("NBAHeadCoach.UI.ArtInjector");
                if (artInjectorType != null && GetComponent(artInjectorType) == null)
                    gameObject.AddComponent(artInjectorType);

                // Auto-attach MenuInjector for runtime main menu UI
                var menuInjectorType = System.Type.GetType("NBAHeadCoach.UI.MenuInjector");
                if (menuInjectorType != null && GetComponent(menuInjectorType) == null)
                    gameObject.AddComponent(menuInjectorType);

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
            _draftPickRegistry = new DraftPickRegistry();
            _tradeSystem = new TradeSystem(_salaryCapManager, PlayerDatabase, _draftPickRegistry);
            _tradeEvaluator = new AITradeEvaluator(_salaryCapManager, PlayerDatabase);
            _tradeFinder = new TradeFinder(_tradeEvaluator, _salaryCapManager, PlayerDatabase);
            _tradeNegotiationManager = new TradeNegotiationManager(_tradeEvaluator, _tradeSystem, _tradeFinder);

            // Initialize Trade AI Enhancement Systems
            _tradeAnnouncementSystem = new TradeAnnouncementSystem(PlayerDatabase, _tradeEvaluator?.ValueCalculator);
            _tradeOfferGenerator = new AITradeOfferGenerator(
                PlayerDatabase, _salaryCapManager, _tradeEvaluator?.ValueCalculator, _draftPickRegistry);

            // Load initial front office profiles (real NBA GMs as of Dec 2025)
            _tradeOfferGenerator?.LoadInitialFrontOfficeData();

            // Wire up trade execution -> announcements and captain detection
            if (_tradeSystem != null)
            {
                _tradeSystem.OnTradeExecuted += OnTradeExecutedHandler;
            }

            // Wire up roster manager -> captain removal detection
            if (_rosterManager != null)
            {
                _rosterManager.OnPlayerRemoved += OnPlayerRemovedHandler;
            }

            // Wire up season phase changes -> captain selection at season start
            if (SeasonController != null)
            {
                SeasonController.OnPhaseChanged += OnSeasonPhaseChangedHandler;
            }

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

            // Initialize draft pick registry with all teams
            if (_draftPickRegistry != null && _allTeams.Count > 0)
            {
                int currentYear = DateTime.Now.Year;
                _draftPickRegistry.InitializeForSeason(currentYear, _allTeams);
            }

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
                NewGameState.TeamSelection => NewGameState.RoleSelection,
                NewGameState.RoleSelection => NewGameState.DifficultySettings,
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
                NewGameState.RoleSelection => NewGameState.TeamSelection,
                NewGameState.DifficultySettings => NewGameState.RoleSelection,
                NewGameState.ContractNegotiation => NewGameState.DifficultySettings,
                NewGameState.Confirmation => NewGameState.ContractNegotiation,
                _ => NewGameState.CoachCreation
            };
        }

        #endregion

        #region New Game

        public void StartNewGame(string firstName, string lastName, int age, string teamId, DifficultySettings difficulty, int tactical, int development, int reputation, UserRole selectedRole = UserRole.Both, DateTime? coachBirthDate = null)
        {
            Debug.Log($"[GameManager] Starting new game: {firstName} {lastName}, Team: {teamId}, Role: {selectedRole}");

            string fullName = $"{firstName} {lastName}";
            var team = GetTeam(teamId);
            _playerTeamId = teamId;

            // Initialize season
            _currentSeason = DateTime.Now.Year;
            _currentDate = new DateTime(_currentSeason, 10, 1); // Season starts October

            // Create user role configuration
            _userRoleConfig = new UserRoleConfiguration
            {
                CurrentRole = selectedRole,
                TeamId = teamId
            };

            // Create career profile(s) based on selected role
            switch (selectedRole)
            {
                case UserRole.GMOnly:
                    // User is GM - create GM profile
                    _career = UnifiedCareerProfile.CreateForFrontOffice(fullName, _currentSeason, age, false, "Player_GM", null, coachBirthDate);
                    _career.IsUserControlled = true;
                    _career.Reputation = reputation;
                    _career.HandleHired(_currentSeason, teamId, team?.Name ?? teamId, UnifiedRole.GeneralManager);
                    _career.ContractYears = 4;
                    _career.CurrentSalary = 3_000_000;
                    _userRoleConfig.UserGMProfileId = _career.ProfileId;
                    PersonnelManager.Instance?.RegisterProfile(_career);

                    // Generate AI Head Coach
                    var aiCoach = GenerateAICoach(teamId, team?.Name ?? teamId);
                    _userRoleConfig.AICoachProfileId = aiCoach.ProfileId;
                    PersonnelManager.Instance?.RegisterProfile(aiCoach);
                    Debug.Log($"[GameManager] Generated AI Coach: {aiCoach.FullName}");
                    break;

                case UserRole.HeadCoachOnly:
                    // User is Head Coach - create coach profile
                    _career = UnifiedCareerProfile.CreateForCoaching(fullName, _currentSeason, age, false, "Player_Coach", null, coachBirthDate);
                    _career.IsUserControlled = true;
                    _career.TacticalRating = tactical;
                    _career.PlayerDevelopment = development;
                    _career.Reputation = reputation;
                    _career.HandleHired(_currentSeason, teamId, team?.Name ?? teamId, UnifiedRole.HeadCoach);
                    _career.ContractYears = 4;
                    _career.CurrentSalary = 5_000_000;
                    _userRoleConfig.UserCoachProfileId = _career.ProfileId;
                    PersonnelManager.Instance?.RegisterProfile(_career);

                    // Generate AI GM
                    var aiGM = GenerateAIGM(teamId, team?.Name ?? teamId);
                    _userRoleConfig.AIGMProfileId = aiGM.ProfileId;
                    PersonnelManager.Instance?.RegisterProfile(aiGM);
                    Debug.Log($"[GameManager] Generated AI GM: {aiGM.FullName}");
                    break;

                case UserRole.Both:
                default:
                    // User controls both - create combined coach profile (traditional mode)
                    _career = UnifiedCareerProfile.CreateForCoaching(fullName, _currentSeason, age, false, "Player_Coach", null, coachBirthDate);
                    _career.IsUserControlled = true;
                    _career.TacticalRating = tactical;
                    _career.PlayerDevelopment = development;
                    _career.Reputation = reputation;
                    _career.HandleHired(_currentSeason, teamId, team?.Name ?? teamId, UnifiedRole.HeadCoach);
                    _career.ContractYears = 4;
                    _career.CurrentSalary = 5_000_000;
                    _userRoleConfig.UserCoachProfileId = _career.ProfileId;
                    _userRoleConfig.UserGMProfileId = _career.ProfileId; // Same profile handles both
                    PersonnelManager.Instance?.RegisterProfile(_career);
                    break;
            }

            // Initialize season calendar for player's team
            SeasonController.InitializeSeason(_currentSeason);

            // Initialize all managers with new game data
            InitializeManagersForNewGame();

            // Initialize all 30 teams with AI coach personalities, lineups, and strategies
            InitializeAllTeamCoaches();

            // Initialize season stats for all players (must be after rosters are loaded)
            SeasonController.InitializePlayerSeasonStats(_currentSeason);

            // Fire event
            OnNewGameStarted?.Invoke();

            // Transition to playing state
            ChangeState(GameState.Playing);
            LoadScene(GAME_SCENE);

            // Auto-save
            SaveLoad.AutoSave(CreateSaveData());
        }

        private UnifiedCareerProfile GenerateAICoach(string teamId, string teamName)
        {
            // Generate a random AI coach with hidden personality traits
            var generatedName = Util.NameGenerator.GenerateStaffName();
            string firstName = generatedName?.FirstName ?? "John";
            string lastName = generatedName?.LastName ?? "Smith";
            int age = UnityEngine.Random.Range(40, 60);

            var aiCoach = UnifiedCareerProfile.CreateForCoaching($"{firstName} {lastName}", _currentSeason, age, false, $"AI_Coach_{teamId}", null);
            aiCoach.IsUserControlled = false;
            aiCoach.TacticalRating = UnityEngine.Random.Range(50, 85);
            aiCoach.PlayerDevelopment = UnityEngine.Random.Range(50, 85);
            aiCoach.Reputation = UnityEngine.Random.Range(40, 75);
            aiCoach.HandleHired(_currentSeason, teamId, teamName, UnifiedRole.HeadCoach);
            aiCoach.ContractYears = 3;
            aiCoach.CurrentSalary = 4_000_000;

            return aiCoach;
        }

        private UnifiedCareerProfile GenerateAIGM(string teamId, string teamName)
        {
            // Generate a random AI GM with hidden personality traits
            var generatedName = Util.NameGenerator.GenerateStaffName();
            string firstName = generatedName?.FirstName ?? "Mike";
            string lastName = generatedName?.LastName ?? "Johnson";
            int age = UnityEngine.Random.Range(38, 58);

            var aiGM = UnifiedCareerProfile.CreateForFrontOffice($"{firstName} {lastName}", _currentSeason, age, false, $"AI_GM_{teamId}", null);
            aiGM.IsUserControlled = false;
            aiGM.Reputation = UnityEngine.Random.Range(50, 80);
            aiGM.HandleHired(_currentSeason, teamId, teamName, UnifiedRole.GeneralManager);
            aiGM.ContractYears = 4;
            aiGM.CurrentSalary = 3_500_000;

            return aiGM;
        }

        /// <summary>
        /// Start a new career after accepting a job from the job market.
        /// Used when transitioning from unemployment to a new position.
        /// </summary>
        public void StartNewCareerFromJobMarket(string teamId, UserRole newRole, int salary, int contractYears)
        {
            Debug.Log($"[GameManager] Starting new career from job market: {teamId}, Role: {newRole}");

            var team = GetTeam(teamId);
            _playerTeamId = teamId;

            // Update role configuration
            _userRoleConfig = new UserRoleConfiguration
            {
                CurrentRole = newRole,
                TeamId = teamId
            };

            // Update existing career profile with new position
            if (_career != null)
            {
                var role = newRole == UserRole.GMOnly ? UnifiedRole.GeneralManager : UnifiedRole.HeadCoach;
                _career.HandleHired(_currentSeason, teamId, team?.Name ?? teamId, role);
                _career.ContractYears = contractYears;
                _career.CurrentSalary = salary;

                if (newRole == UserRole.GMOnly)
                {
                    _userRoleConfig.UserGMProfileId = _career.ProfileId;
                    // Generate AI coach
                    var aiCoach = GenerateAICoach(teamId, team?.Name ?? teamId);
                    _userRoleConfig.AICoachProfileId = aiCoach.ProfileId;
                    PersonnelManager.Instance?.RegisterProfile(aiCoach);
                }
                else
                {
                    _userRoleConfig.UserCoachProfileId = _career.ProfileId;
                    // Generate AI GM
                    var aiGM = GenerateAIGM(teamId, team?.Name ?? teamId);
                    _userRoleConfig.AIGMProfileId = aiGM.ProfileId;
                    PersonnelManager.Instance?.RegisterProfile(aiGM);
                }
            }

            // Return to playing state
            ChangeState(GameState.Playing);

            // Auto-save
            SaveLoad.AutoSave(CreateSaveData());
        }

        private void InitializeManagersForNewGame()
        {
            // Initialize managers that need setup for new game
            if (_career != null)
                _jobSecurityManager?.InitializeForNewSeason(_career, _playerTeamId);
            _developmentManager?.InitializeForNewSeason(_allTeams);

            // Generate initial draft class for upcoming draft
            var draftGen = DraftClassGenerator.Instance;
            draftGen?.GenerateDraftClass(_currentSeason + 1);

            // Initialize trade AI enhancement systems with player team
            SetupTradeSystemsForTeam(_playerTeamId);
        }

        /// <summary>
        /// Initialize all 30 teams with AI coach personalities, starting lineups, and strategies.
        /// </summary>
        private void InitializeAllTeamCoaches()
        {
            var rng = new System.Random();
            foreach (var team in _allTeams)
            {
                if (team == null) continue;
                team.CoachPersonality = AI.AICoachPersonality.GenerateRandom(rng);
                team.AutoSetStrategy(team.CoachPersonality);
                team.AutoSetStartingLineup(team.CoachPersonality);
                Debug.Log($"[GameManager] {team.Abbreviation}: Coach={team.CoachPersonality.OffensiveStyle}, Pace={team.CoachPersonality.PreferredPace:0}, 3PT={team.CoachPersonality.ThreePointEmphasis}, Starters={string.Join(",", team.StartingLineupIds?.Take(5) ?? new string[0])}");
            }
        }

        /// <summary>
        /// Set up trade systems with the player's team ID.
        /// Called when starting a new game or loading a save.
        /// </summary>
        private void SetupTradeSystemsForTeam(string teamId)
        {
            if (string.IsNullOrEmpty(teamId)) return;

            // Set player team on trade offer generator
            _tradeOfferGenerator?.SetPlayerTeamId(teamId);

            // Set player team on announcement system for priority highlighting
            _tradeAnnouncementSystem?.SetPlayerTeamId(teamId);

            // Initialize draft pick registry if not done yet
            if (_draftPickRegistry != null && _allTeams.Count > 0)
            {
                _draftPickRegistry.InitializeForSeason(_currentSeason, _allTeams);
            }

            Debug.Log($"[GameManager] Trade systems configured for team: {teamId}");
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
            _userRoleConfig = data.UserRoleConfig ?? new UserRoleConfiguration { CurrentRole = UserRole.Both };
            Preferences = data.Preferences ?? new GamePreferences();
            _currentSeason = data.CurrentSeason;
            // Restore date from string backup (JsonUtility can't roundtrip DateTime)
            if (!string.IsNullOrEmpty(data.CurrentDateStr) && DateTime.TryParse(data.CurrentDateStr, out var parsedDate))
                _currentDate = parsedDate;
            else if (data.CurrentDate.Year > 1)
                _currentDate = data.CurrentDate;
            else
                _currentDate = new DateTime(_currentSeason, 10, 1);

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
                if (player != null)
                {
                    // Preserve BirthDate from database (save doesn't store it correctly)
                    var savedBirthDate = player.BirthDate;
                    playerState?.ApplyTo(player);
                    if (player.BirthDate.Year < 1900 && savedBirthDate.Year >= 1900)
                        player.BirthDate = savedBirthDate;
                    // Ensure energy is initialized
                    if (player.Energy <= 0) player.Energy = 100f;
                    if (player.Morale <= 0) player.Morale = 75f;
                }
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

            // Restore draft pick registry
            if (data.DraftPickRegistryData != null)
            {
                _draftPickRegistry?.RestoreFromSave(data.DraftPickRegistryData);
            }
            else if (_draftPickRegistry != null && _allTeams.Count > 0)
            {
                // Initialize with defaults if no saved data
                _draftPickRegistry.InitializeForSeason(_currentSeason, _allTeams);
            }

            // Restore incoming trade offers
            if (data.IncomingOffersData != null)
            {
                _tradeOfferGenerator?.RestoreFromSave(data.IncomingOffersData);
            }

            // Set up trade systems with loaded player team
            SetupTradeSystemsForTeam(_playerTeamId);

            // Ensure all teams have valid lineups and strategies after restore
            InitializeAllTeamCoaches();

            // Ensure all players have season stats initialized
            SeasonController.InitializePlayerSeasonStats(_currentSeason);
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
                SaveTimestampStr = DateTime.Now.ToString("o"),
                Career = _career,
                PlayerTeamId = _playerTeamId,
                UserRoleConfig = _userRoleConfig,
                Preferences = Preferences ?? new GamePreferences(),
                CurrentSeason = _currentSeason,
                CurrentDate = _currentDate,
                CurrentDateStr = _currentDate.ToString("o"),
                TeamStates = CreateTeamStates(),
                PlayerStates = CreatePlayerStates(),
                CalendarData = SeasonController.CreateCalendarSaveData(),
                PlayoffData = _playoffManager?.CreateSaveData(),
                PersonalityData = _personalityManager?.CreateSaveData(),
                UnifiedCareers = PersonnelManager.Instance?.GetSaveData(),
                DraftPickRegistryData = _draftPickRegistry?.CreateSaveData(),
                IncomingOffersData = _tradeOfferGenerator?.CreateSaveData()
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

            // Simulate all other league games for today
            SimulateLeagueGamesForDate(_currentDate);

            // Process daily trade offers from AI teams
            _tradeOfferGenerator?.ProcessDailyOffers();

            OnDayAdvanced?.Invoke(_currentDate);
        }

        /// <summary>
        /// Simulates all non-player league games scheduled for a given date.
        /// Uses full game simulation for realistic stats and standings updates.
        /// </summary>
        private void SimulateLeagueGamesForDate(DateTime date)
        {
            if (SeasonController == null || _allTeams == null || _allTeams.Count == 0) return;

            // Get all games for today from the master schedule (league-wide)
            var todaysGames = SeasonController.Schedule?
                .Where(g => g.Date.Date == date.Date && !g.IsCompleted && g.Type == Data.CalendarEventType.Game)
                .ToList();

            if (todaysGames == null || todaysGames.Count == 0) return;

            string playerTeamId = PlayerTeamId;
            var simulator = new Simulation.GameSimulator(PlayerDatabase);

            // Only skip the player's NEXT game (the one they'll manually play/sim)
            var nextPlayerGame = SeasonController.GetNextGame();

            foreach (var game in todaysGames)
            {
                // Only skip the specific game the player is about to play
                if (nextPlayerGame != null && game.EventId == nextPlayerGame.EventId)
                    continue;

                var homeTeam = GetTeam(game.HomeTeamId);
                var awayTeam = GetTeam(game.AwayTeamId);
                if (homeTeam == null || awayTeam == null) continue;

                try
                {
                    var result = simulator.SimulateGame(homeTeam, awayTeam);
                    simulator.RecordGameToPlayerStats(result, game.EventId, game.Date);
                    SeasonController.RecordGameResult(game, result.HomeScore, result.AwayScore);
                    LeagueStats.AddGameResult(result.BoxScore);
                }
                catch (System.Exception ex)
                {
                    // Fallback: random result if simulation fails
                    Debug.LogWarning($"[GameManager] League sim failed for {game.AwayTeamId}@{game.HomeTeamId}: {ex.Message}");
                    int homeScore = UnityEngine.Random.Range(90, 120);
                    int awayScore = UnityEngine.Random.Range(90, 120);
                    if (homeScore == awayScore) homeScore += 2; // no ties
                    SeasonController.RecordGameResult(game, homeScore, awayScore);
                }
            }

            // Recalculate league averages and advanced stats for all players
            LeagueStats.Recalculate();
            RecalculateAllAdvancedStats();
        }

        /// <summary>
        /// Recalculate advanced stats for all players who have played games.
        /// </summary>
        private void RecalculateAllAdvancedStats()
        {
            var allPlayers = PlayerDatabase?.GetAllPlayers();
            if (allPlayers == null) return;
            foreach (var player in allPlayers)
            {
                if (player.CurrentSeasonStats != null && player.CurrentSeasonStats.GamesPlayed > 0)
                    LeagueStats.CalculatePlayerAdvancedStats(player.CurrentSeasonStats);
            }
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
                if (_career.CurrentContract != null)
                    _career.CurrentContract.CurrentYear++;
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

        /// <summary>
        /// Retire from career without saving — ends the current career permanently.
        /// </summary>
        public void RetireFromCareer()
        {
            Debug.Log("[GameManager] Retiring from career");
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

        /// <summary>
        /// Check if user controls roster decisions (GM or Both role)
        /// </summary>
        public bool UserControlsRoster => _userRoleConfig?.UserControlsRoster ?? true;

        /// <summary>
        /// Check if user controls in-game decisions (Coach or Both role)
        /// </summary>
        public bool UserControlsGames => _userRoleConfig?.UserControlsGames ?? true;

        /// <summary>
        /// Get the AI coach profile (if user is GM-only)
        /// </summary>
        public UnifiedCareerProfile GetAICoach()
        {
            if (_userRoleConfig?.HasAICoach != true) return null;
            return PersonnelManager.Instance?.GetProfile(_userRoleConfig.AICoachProfileId);
        }

        /// <summary>
        /// Get the AI GM profile (if user is Coach-only)
        /// </summary>
        public UnifiedCareerProfile GetAIGM()
        {
            if (_userRoleConfig?.HasAIGM != true) return null;
            return PersonnelManager.Instance?.GetProfile(_userRoleConfig.AIGMProfileId);
        }

        #endregion

        #region Captain System

        /// <summary>
        /// Handle trade execution - check if captain was traded away.
        /// </summary>
        private void OnTradeExecutedHandler(TradeProposal proposal)
        {
            // Generate trade announcement
            _tradeAnnouncementSystem?.GenerateAnnouncement(proposal);

            // Check if player team's captain was traded away
            if (string.IsNullOrEmpty(_playerTeamId)) return;

            foreach (var asset in proposal.AllAssets)
            {
                if (asset.Type == TradeAssetType.Player && asset.SendingTeamId == _playerTeamId)
                {
                    var player = PlayerDatabase?.GetPlayer(asset.PlayerId);
                    if (player != null && player.IsCaptain)
                    {
                        // Clear captain status since they're no longer on the team
                        player.IsCaptain = false;

                        Debug.Log($"[GameManager] Captain {player.FullName} was traded - requiring new captain selection");

                        // Fire event to show captain selection
                        var team = GetPlayerTeam();
                        if (team != null)
                        {
                            OnCaptainSelectionRequired?.Invoke(team, true);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Handle player removed from roster - check if captain was cut/waived.
        /// </summary>
        private void OnPlayerRemovedHandler(string teamId, string playerId)
        {
            // Only care about player team
            if (teamId != _playerTeamId) return;

            var player = PlayerDatabase?.GetPlayer(playerId);
            if (player != null && player.IsCaptain)
            {
                // Clear captain status
                player.IsCaptain = false;

                Debug.Log($"[GameManager] Captain {player.FullName} was removed from roster - requiring new captain selection");

                // Fire event to show captain selection
                var team = GetPlayerTeam();
                if (team != null)
                {
                    OnCaptainSelectionRequired?.Invoke(team, true);
                }
            }
        }

        /// <summary>
        /// Handle season phase changes - prompt for captain at start of regular season.
        /// </summary>
        private void OnSeasonPhaseChangedHandler(SeasonPhase newPhase)
        {
            // Only care about transition to RegularSeason
            if (newPhase != SeasonPhase.RegularSeason) return;

            // Check if player team has a captain
            var team = GetPlayerTeam();
            if (team == null) return;

            bool hasCaptain = team.Roster?.Any(p => p.IsCaptain) ?? false;

            if (!hasCaptain)
            {
                Debug.Log("[GameManager] Season starting - captain selection required");
                OnCaptainSelectionRequired?.Invoke(team, false);
            }
        }

        /// <summary>
        /// Assign a player as team captain.
        /// Called by UI after user selection.
        /// </summary>
        public void AssignCaptain(string playerId)
        {
            var team = GetPlayerTeam();
            if (team == null) return;

            // Remove captain status from any existing captain
            if (team.Roster != null)
            {
                foreach (var player in team.Roster)
                {
                    player.IsCaptain = false;
                }
            }

            // Assign new captain
            var newCaptain = PlayerDatabase?.GetPlayer(playerId);
            if (newCaptain != null)
            {
                newCaptain.IsCaptain = true;
                Debug.Log($"[GameManager] {newCaptain.FullName} assigned as team captain");

                // Also use MoraleChemistryManager if available
                _moraleChemistryManager?.AssignCaptain(team, playerId);
            }
        }

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
        public void RegisterJobMarketManager(JobMarketManager manager) => _jobMarketManager = manager;

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
