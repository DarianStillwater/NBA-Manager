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
        private DifficultySettings _difficulty = new DifficultySettings();
        public DifficultySettings Difficulty => _difficulty;

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

        // Plain C# manager systems, constructed in Initialize()
        private SalaryCapManager _salaryCapManager;
        public SalaryCapManager SalaryCapManager => _salaryCapManager;
        private RosterManager _rosterManager;
        private TradeSystem _tradeSystem;
        public TradeSystem Trades => _tradeSystem;
        private TradeDeskSystem _tradeDesk;
        public TradeDeskSystem TradeDesk => _tradeDesk;
        private InSeasonFreeAgencySystem _inSeasonSigning;
        public InSeasonFreeAgencySystem InSeasonSigning => _inSeasonSigning;
        private FinanceSystem _financeSystem;
        public FinanceSystem FinanceSystem => _financeSystem;
        private DevelopmentSystem _developmentDesk;
        public DevelopmentSystem DevelopmentDesk => _developmentDesk;
        private ScoutingSystem _scoutingSystem;
        public ScoutingSystem Scouting => _scoutingSystem;
        private FreeAgentManager _freeAgentManager;
        public FreeAgentManager FreeAgents => _freeAgentManager;
        private DraftSystem _draftSystem;
        public DraftSystem DraftSystem => _draftSystem;
        private OffseasonManager _offseasonManager;
        public OffseasonManager Offseason => _offseasonManager;
        private PlayerDevelopmentManager _developmentManager;
        public PlayerDevelopmentManager Development => _developmentManager;
        private JobSecurityManager _jobSecurityManager;
        public JobSecurityManager JobSecurity => _jobSecurityManager;
        private CareerStakesSystem _careerStakes;
        public CareerStakesSystem CareerStakes => _careerStakes;
        private TransactionLog _transactionLog;
        public TransactionLog Transactions => _transactionLog;
        private InboxService _inboxService;
        public InboxService Inbox => _inboxService;
        private AwardsStore _awardsStore;
        public AwardsStore Awards => _awardsStore;
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
        private MentorshipManager _mentorshipManager;
        public MentorshipManager MentorshipManager => _mentorshipManager;
        private RetirementManager _retirementManager;
        public RetirementManager RetirementManager => _retirementManager;
        private GMJobSecurityManager _gmJobSecurityManager;
        public GMJobSecurityManager GMJobSecurityManager => _gmJobSecurityManager;
        private DraftClassGenerator _draftClassGenerator;
        public DraftClassGenerator DraftClassGenerator => _draftClassGenerator;
        private LeagueEventsManager _leagueEventsManager;
        public LeagueEventsManager LeagueEventsManager => _leagueEventsManager;
        private SummerLeagueManager _summerLeagueManager;
        public SummerLeagueManager SummerLeagueManager => _summerLeagueManager;
        private GamePlanBuilder _gamePlanBuilder;
        public GamePlanBuilder GamePlanBuilder => _gamePlanBuilder;
        private ScoutingReportGenerator _scoutingReportGenerator;
        public ScoutingReportGenerator ScoutingReportGenerator => _scoutingReportGenerator;
        private Manager.AgentManager _agentManager;
        public Manager.AgentManager AgentManager => _agentManager;
        private ContractNegotiationManager _contractNegotiationManager;
        public ContractNegotiationManager ContractNegotiationManager => _contractNegotiationManager;

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

        /// <summary>
        /// Manifest of live game systems. The daily loop (and, as migration proceeds,
        /// the save pipeline and phase fan-out) iterates this registry instead of
        /// hand-maintained call lists.
        /// </summary>
        public GameSystemRegistry Systems { get; private set; }

        /// <summary>
        /// THE post-game processor — every sim path (league auto-sim, quick-sim,
        /// interactive match) records completed games through this pipeline.
        /// </summary>
        public Simulation.GameCompletionPipeline GameCompletion { get; private set; }

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

            // Construct the (formerly MonoBehaviour, formerly dormant) manager systems.
            // Order matters: FormerPlayerCareerManager and GMJobSecurityManager subscribe to
            // RetirementManager events in their constructors; MoraleChemistryManager shares
            // GameManager's PersonalityManager so restored personalities drive chemistry.
            _personnelManager = new PersonnelManager();
            _injuryManager = new InjuryManager();
            _mentorshipManager = new MentorshipManager();
            _retirementManager = new RetirementManager();
            _formerPlayerCareerManager = new FormerPlayerCareerManager();
            _gmJobSecurityManager = new GMJobSecurityManager();
            _moraleChemistryManager = new MoraleChemistryManager(_personalityManager);
            _mediaManager = new MediaManager();
            _revenueManager = new RevenueManager();
            _trainingCampManager = new TrainingCampManager();
            _allStarManager = new AllStarManager();
            _advanceScoutingManager = new AdvanceScoutingManager();
            _historyManager = new HistoryManager();
            _jobMarketManager = new JobMarketManager();
            _offseasonManager = new OffseasonManager();
            _draftClassGenerator = new DraftClassGenerator();
            _playoffManager = new PlayoffManager();
            _leagueEventsManager = new LeagueEventsManager();
            _summerLeagueManager = new SummerLeagueManager();
            _gamePlanBuilder = new GamePlanBuilder();
            _scoutingReportGenerator = new ScoutingReportGenerator();
            _agentManager = new Manager.AgentManager();
            _contractNegotiationManager = new ContractNegotiationManager(_agentManager);

            // Plain C# managers that previously relied on never-called Register hooks
            _draftSystem = new DraftSystem(_salaryCapManager, PlayerDatabase);
            _freeAgentManager = new FreeAgentManager(_salaryCapManager, PlayerDatabase);
            _jobSecurityManager = new JobSecurityManager();
            _developmentManager = new PlayerDevelopmentManager();
            _transactionLog = new TransactionLog();
            _inboxService = new InboxService();
            _awardsStore = new AwardsStore();

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

            // AI trade evaluations share the same front-office personalities the
            // offer generator loaded — without this, every evaluation falls back
            // to a bland average GM.
            if (_tradeEvaluator != null && _tradeOfferGenerator != null)
            {
                foreach (var fo in _tradeOfferGenerator.GetAllFrontOffices().Values)
                    _tradeEvaluator.RegisterFrontOffice(fo);
            }

            // League-wide trade market (AI-AI deals, deadline frenzy, offer desk)
            _tradeDesk = TradeDeskSystem.CreateDefault(this);

            // In-season free agency: AI roster patching + the player's buyout market
            _inSeasonSigning = InSeasonFreeAgencySystem.CreateDefault(this);

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

            // Champion crowned -> vote and announce the season's awards
            if (_playoffManager != null)
            {
                _playoffManager.OnNBAChampion += OnChampionCrownedHandler;
            }

            RegisterSystems();

            // Bridge existing producer events into the inbox
            _injuryManager.OnPlayerInjured += OnPlayerInjuredLineupHook;
            InboxWiring.Wire(this, _inboxService, _jobSecurityManager, _financeManager,
                _tradeAnnouncementSystem, _mediaManager, _injuryManager, _playoffManager,
                _tradeOfferGenerator, _jobMarketManager, _historyManager);

            // Load player/team data
            StartCoroutine(LoadGameData());
        }

        /// <summary>
        /// Register every live system with the registry. Runs after ALL constructions —
        /// constructor order above carries event subscriptions and must not change.
        /// </summary>
        private void RegisterSystems()
        {
            GameCompletion = Simulation.GameCompletionPipeline.CreateDefault(this);

            Systems = new GameSystemRegistry();

            Systems.Register(SeasonController);
            Systems.Register(new LeagueGameSimSystem(this));
            Systems.Register(_tradeOfferGenerator);   // also ISaveSection (incoming offers)
            Systems.Register(_tradeDesk);             // AI-AI trades + deadline frenzy
            Systems.Register(_inSeasonSigning);       // in-season FA wire

            // Economy: owners, revenue, payroll, luxury-tax conversations.
            // Constructed here because it hooks the completion pipeline above.
            _financeSystem = FinanceSystem.CreateDefault(this);
            _financeSystem.HookGameRevenue(GameCompletion);
            Systems.Register(_financeSystem);

            // Development desk: weekly practice, mentorship sessions, tendency
            // training for the player's team (also boots the lazy engines).
            _developmentDesk = DevelopmentSystem.CreateDefault(this);
            Systems.Register(_developmentDesk);

            // Scouting fog of war: assignments become reports; the draft-class
            // preview shares the June draft's deterministic seed.
            _scoutingSystem = ScoutingSystem.CreateDefault(this);
            Systems.Register(_scoutingSystem);
            Systems.Register(_personnelManager);      // also ISaveSection (unified careers)

            // Career stakes: owner expectations, weekly hot-seat evaluation,
            // firings, and the season-end verdict. Runs just before the job
            // market ticks so a firing lands on a live market.
            _careerStakes = new CareerStakesSystem(
                _jobSecurityManager,
                _gmJobSecurityManager,
                id => _financeManager?.GetTeamFinances(id),
                () => _career,
                GetTeam,
                FirePlayerFromCurrentJob);
            Systems.Register(_careerStakes);
            Systems.Register(new Manager.AIGMSystem());
            Systems.Register(_summerLeagueManager);
            Systems.Register(_trainingCampManager);
            Systems.Register(_agentManager);
            Systems.Register(_contractNegotiationManager);

            Systems.Register(_jobMarketManager);      // also ISaveSection (openings/applications)
            Systems.Register(_mentorshipManager);
            Systems.Register(_mediaManager);          // also ISaveSection (media reputation)

            // Save-section-only systems (no daily tick yet). Registration order is the
            // save read/write order.
            Systems.Register(_salaryCapManager);
            Systems.Register(_injuryManager);
            Systems.Register(_playoffManager);
            Systems.Register(_personalityManager);
            Systems.Register(_draftPickRegistry);
            Systems.Register(_transactionLog);
            Systems.Register(_inboxService);
            Systems.Register(_awardsStore);
            Systems.Register(_historyManager);        // season archives, records, Hall of Fame

            // Every completed game feeds the record books and career milestones
            GameCompletion.OnGameCompleted += OnGameCompletedHistoryHook;

            // Phase rails: OffseasonManager stays a stub until Phase 2; AllStarManager
            // runs the All-Star selections when the break begins.
            Systems.Register(_offseasonManager);
            Systems.Register(_allStarManager);
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
                    // MatchSceneSetup auto-attaches when scene loads
                    StartCoroutine(AttachMatchSceneSetup());
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
            _difficulty = difficulty ?? new DifficultySettings();

            // Initialize season
            _currentSeason = DateTime.Now.Year;
            _currentDate = new DateTime(_currentSeason, 10, 21); // Eve of regular season (first games Oct 22)

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

            // GM-only: the generated AI head coach (not a random one) runs the bench
            ApplyAICoachToPlayerTeam();

            // Generate hidden personalities for every roster so morale/chemistry has data to work with
            if (_moraleChemistryManager != null)
            {
                foreach (var rosterTeam in _allTeams)
                {
                    if (rosterTeam != null) _moraleChemistryManager.InitializeTeamPersonalities(rosterTeam);
                }
            }

            // Initialize season stats for all players (must be after rosters are loaded)
            SeasonController.InitializePlayerSeasonStats(_currentSeason);

            // New-game-only system initialization (owners/finances, future systems).
            // NEVER runs on load — that's the whole point of the interface.
            var newGameCtx = new NewGameContext(teamId, _currentSeason, difficulty);
            foreach (var initializable in Systems.NewGameInitializables)
            {
                try { initializable.InitializeForNewGame(newGameCtx); }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameManager] New-game init failed for {initializable.SystemId}: {ex.Message}");
                }
            }

            // Owner expectations need the finances the initializables just created
            _careerStakes?.SetPlayerSeasonExpectations();

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
        /// True when the player's career is between jobs — the league keeps
        /// running (all games auto-sim) while they work the job market.
        /// </summary>
        public bool IsCareerUnemployed => _career?.CurrentRole == UnifiedRole.Unemployed;

        /// <summary>
        /// Feeds every completed game into the record books: franchise/league
        /// single-game records and career milestones (both existed with zero
        /// callers before Phase 6).
        /// </summary>
        private void OnGameCompletedHistoryHook(Simulation.GameCompletionContext ctx)
        {
            try
            {
                var box = ctx.Result?.BoxScore;
                if (box == null || _historyManager == null) return;

                _historyManager.CheckGameRecordsForGame(_currentSeason, box,
                    id => PlayerDatabase?.GetPlayer(id)?.TeamId);

                foreach (var stats in box.PlayerStats.Values)
                {
                    if (stats.Points == 0 && stats.Rebounds == 0 && stats.Assists == 0) continue;
                    var player = PlayerDatabase?.GetPlayer(stats.PlayerId);
                    if (player != null) _historyManager.CheckCareerMilestones(player, stats);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameManager] History hook failed: {ex.Message}");
            }
        }

        /// <summary>
        /// The franchise-side fallout of the player losing their job: the market
        /// flow (reputation hit, severance, openings, unemployed state), the career
        /// record, an AI successor so the old team keeps functioning, and the inbox
        /// notice. The owner-side message/status is JobSecurityManager's job and has
        /// already happened by the time this runs.
        /// </summary>
        public void FirePlayerFromCurrentJob(FiringReason reason)
        {
            if (_career == null || _career.CurrentRole == UnifiedRole.Unemployed) return;

            string oldTeamId = _playerTeamId;
            var oldTeam = GetTeam(oldTeamId);
            var oldRole = _userRoleConfig?.CurrentRole ?? UserRole.Both;

            // Market flow first — it reads the current config and salary for severance
            _jobMarketManager?.HandlePlayerFired(reason);

            // Career record: history entry, unemployed track, fired counter
            _career.HandleFired(_currentSeason, reason.ToString());

            // The old franchise hires AI replacements so the league keeps running
            if (oldTeam != null)
            {
                if (oldRole != UserRole.GMOnly)
                    PersonnelManager.Instance?.RegisterProfile(GenerateAICoach(oldTeamId, oldTeam.Name));
                if (oldRole != UserRole.HeadCoachOnly)
                    PersonnelManager.Instance?.RegisterProfile(GenerateAIGM(oldTeamId, oldTeam.Name));
            }

            SaveLoad?.AutoSave(CreateSaveData());
            Debug.Log($"[GameManager] Player fired from {oldTeamId} ({reason}). The job hunt begins.");
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

            // GM-only hires get their named AI coach installed on the new bench
            ApplyAICoachToPlayerTeam();

            // The new owner sets expectations on day one
            _jobSecurityManager?.InitializeForNewSeason(_career, teamId);
            _careerStakes?.SetPlayerSeasonExpectations();

            _inboxService?.Publish(InboxMessageType.JobMarket, "League News",
                $"You're the new {(newRole == UserRole.GMOnly ? "GM" : "head coach")} of the {team?.Name ?? teamId}",
                "A fresh start. The owner has shared expectations for the season — they're waiting at your Career desk.",
                highPriority: true,
                deepLinkPanelId: "Career");

            // Return to playing state
            ChangeState(GameState.Playing);

            // Auto-save
            SaveLoad.AutoSave(CreateSaveData());
        }

        private void InitializeManagersForNewGame()
        {
            // Create contracts from JSON data for all players
            InitializePlayerContracts();

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
        /// <summary>
        /// GM-only mode: replace the player team's random coach personality with one
        /// derived from the generated AI head coach profile, and let that coach set
        /// the strategy and starting five. No-op in Coach/Both modes.
        /// </summary>
        private void ApplyAICoachToPlayerTeam()
        {
            if (_userRoleConfig?.HasAICoach != true) return;
            var team = GetPlayerTeam();
            var profile = GetAICoach();
            if (team == null || profile == null) return;

            team.CoachPersonality = AI.AICoachPersonality.FromProfile(profile);
            team.AutoSetStrategy(team.CoachPersonality);
            team.AutoSetStartingLineup(team.CoachPersonality);
        }

        /// <summary>
        /// When the user doesn't control coaching and a starter goes down, the AI
        /// coach reshuffles the five and reports it.
        /// </summary>
        private void OnPlayerInjuredLineupHook(Player player, InjuryEvent evt)
        {
            if (player == null || player.TeamId != _playerTeamId) return;
            if (Data.RolePermissions.CanEditLineupAndStrategy) return;

            var team = GetPlayerTeam();
            if (team?.StartingLineupIds == null) return;
            if (System.Array.IndexOf(team.StartingLineupIds, player.PlayerId) < 0) return;

            team.AutoSetStartingLineup(team.CoachPersonality);
            _inboxService?.Publish(InboxMessageType.Injury, Data.RolePermissions.AICoachName,
                $"Lineup change after {player.FullName}'s injury",
                $"With {player.FullName} out, I'm adjusting the starting five. We'll keep competing.",
                deepLinkPanelId: "Roster");
        }

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

        /// <summary>
        /// Creates Contract objects from JSON ContractSalary/ContractYears fields for all players.
        /// </summary>
        private void InitializePlayerContracts()
        {
            if (_salaryCapManager == null) return;

            int created = 0;
            foreach (var player in PlayerDatabase.GetAllPlayers())
            {
                // Skip if already has a contract (e.g., from save data)
                if (_salaryCapManager.GetContract(player.PlayerId) != null) continue;

                if (player.ContractSalary > 0 && player.ContractYears > 0)
                {
                    var contract = Data.Contract.Create(
                        player.PlayerId,
                        player.TeamId,
                        player.ContractSalary,
                        player.ContractYears
                    );
                    _salaryCapManager.RegisterContract(contract);
                    created++;
                }
                else if (!string.IsNullOrEmpty(player.TeamId))
                {
                    // Player has a team but no contract data — create a minimum contract
                    int yrsExp = Math.Max(0, player.Age - 22); // approximate years of experience
                    var contract = Data.Contract.CreateMinimum(player.PlayerId, player.TeamId, yrsExp);
                    _salaryCapManager.RegisterContract(contract);
                    created++;
                }
            }
            Debug.Log($"[GameManager] Initialized {created} player contracts");
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
                _currentDate = new DateTime(_currentSeason, 10, 21);

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

            // Restore difficulty (legacy saves carry defaults)
            _difficulty = data.Difficulty ?? new DifficultySettings();

            // Initialize season controller with saved calendar
            SeasonController.RestoreFromSave(data.CalendarData);

            // Each registered save section reads its own slice (registration order).
            var readCtx = new SaveReadContext(
                data.SaveVersion, SaveData.IsLegacyVersion(data.SaveVersion), _currentSeason);
            if (Systems != null)
            {
                foreach (var section in Systems.SaveSections)
                {
                    try { section.ReadSave(data, readCtx); }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GameManager] Save section {section.SystemId} failed to read: {ex}");
                    }
                }
            }

            // Legacy fallback: no saved pick registry -> derive league defaults
            if (data.DraftPickRegistryData == null && _draftPickRegistry != null && _allTeams.Count > 0)
            {
                _draftPickRegistry.InitializeForSeason(_currentSeason, _allTeams);
            }

            // Legacy fallback: derive contracts from base JSON for players without
            // saved contracts (v1.1 saves restore them via the SalaryCap section,
            // so this creates nothing on modern saves)
            InitializePlayerContracts();

            // Set up trade systems with loaded player team
            SetupTradeSystemsForTeam(_playerTeamId);

            // Legacy fallback ONLY: teams from v1.0 saves carry no coach/lineup state.
            // v1.1 saves round-trip strategy/lineup/personality via TeamSaveState —
            // re-randomizing them on every load was the old "my tactics reset" bug.
            var coachRng = new System.Random();
            foreach (var team in _allTeams)
            {
                if (team == null) continue;
                bool hasLineup = team.StartingLineupIds != null &&
                                 System.Array.Exists(team.StartingLineupIds, id => !string.IsNullOrEmpty(id));
                if (team.CoachPersonality == null)
                {
                    team.CoachPersonality = AI.AICoachPersonality.GenerateRandom(coachRng);
                    team.AutoSetStrategy(team.CoachPersonality);
                    team.AutoSetStartingLineup(team.CoachPersonality);
                }
                else if (!hasLineup)
                {
                    // Restored coach/strategy but no usable lineup — fill the five
                    // without touching the restored strategy.
                    team.AutoSetStartingLineup(team.CoachPersonality);
                }
            }

            // Ensure all players have season stats initialized
            SeasonController.InitializePlayerSeasonStats(_currentSeason);
        }

        /// <summary>
        /// Create save data from current state
        /// </summary>
        public SaveData CreateSaveData()
        {
            var data = new SaveData
            {
                SaveVersion = SaveData.CURRENT_VERSION,
                SaveTimestamp = DateTime.Now,
                SaveTimestampStr = DateTime.Now.ToString("o"),
                Career = _career,
                PlayerTeamId = _playerTeamId,
                Difficulty = _difficulty,
                UserRoleConfig = _userRoleConfig,
                Preferences = Preferences ?? new GamePreferences(),
                CurrentSeason = _currentSeason,
                CurrentDate = _currentDate,
                CurrentDateStr = _currentDate.ToString("o"),
                TeamStates = CreateTeamStates(),
                PlayerStates = CreatePlayerStates(),
                CalendarData = SeasonController.CreateCalendarSaveData()
            };

            // Each registered save section writes its own slice.
            if (Systems != null)
            {
                foreach (var section in Systems.SaveSections)
                {
                    try { section.WriteSave(data); }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GameManager] Save section {section.SystemId} failed to write: {ex}");
                    }
                }
            }

            return data;
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
            Systems?.TickDay(new DailyTickContext(_currentDate, _playerTeamId, this));
            OnDayAdvanced?.Invoke(_currentDate);
        }

        /// <summary>
        /// Apply post-game morale processing for both teams of a completed simulated game.
        /// Safe to call from any sim path (league auto-sim, quick sim, interactive match).
        /// </summary>
        public void ProcessPostGameMorale(Simulation.GameResult result, bool isPlayoff = false)
        {
            if (result == null || _moraleChemistryManager == null) return;

            var homeTeam = GetTeam(result.HomeTeamId);
            var awayTeam = GetTeam(result.AwayTeamId);
            if (homeTeam != null) _moraleChemistryManager.ProcessGameResult(result, homeTeam, isPlayoff);
            if (awayTeam != null) _moraleChemistryManager.ProcessGameResult(result, awayTeam, isPlayoff);
        }

        /// <summary>
        /// Start a new season
        /// </summary>
        public void StartNewSeason()
        {
            _currentSeason++;
            _currentDate = new DateTime(_currentSeason, 10, 21);

            if (_career != null)
            {
                _career.CurrentAge++;
                if (_career.CurrentContract != null)
                    _career.CurrentContract.CurrentYear++;
            }

            // TransitionToNewSeason (not InitializeSeason directly) so the completed
            // season is ARCHIVED first: game logs cleared, YearsPro++, per-season
            // counters reset — then the new season initializes on top.
            SeasonController.TransitionToNewSeason(_currentSeason);
            _financeSystem?.OnNewSeason(_currentSeason);
            _careerStakes?.OnNewSeason();
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

        private IEnumerator AttachMatchSceneSetup()
        {
            // Wait for Match scene to load
            yield return new WaitForSeconds(0.3f);
            if (FindAnyObjectByType<UI.Match.MatchSceneSetup>() == null)
            {
                var go = new GameObject("MatchSceneSetup");
                go.AddComponent<UI.Match.MatchSceneSetup>();
            }
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
        /// <summary>
        /// Season finale: the champion is crowned, so the season's awards are voted
        /// on regular-season stats, recorded, and announced.
        /// </summary>
        private void OnChampionCrownedHandler(string championId, string runnerUpId)
        {
            try
            {
                var players = PlayerDatabase?.GetAllPlayers();
                if (players == null || _allTeams == null || _allTeams.Count == 0) return;

                var teams = _allTeams.Where(t => t != null).ToDictionary(t => t.TeamId, t => t);
                var results = AwardManager.VoteSeasonAwards(_currentSeason, players, teams);
                string cotyTeamId = AwardManager.VoteCoachOfYear(_currentSeason, teams);
                var champTeam = GetTeam(championId);
                var finalsMvp = AwardManager.VoteFinalsMVP(_currentSeason, champTeam?.Roster, championId);

                _awardsStore?.RecordSeasonAwards(_currentSeason, results, cotyTeamId,
                    finalsMvp?.PlayerId, championId, runnerUpId);

                // The season is over — hand off to the offseason engine
                _offseasonManager?.SetSeasonClosingData(results);
                _offseasonManager?.BeginOffseason(_currentSeason, _currentDate);

                if (_inboxService != null)
                {
                    if (finalsMvp != null)
                        _inboxService.Publish(InboxMessageType.League, "League Office",
                            $"{finalsMvp.FullName} named Finals MVP",
                            $"{finalsMvp.FullName} of the {champTeam?.Name} takes home the Finals MVP trophy.");

                    if (results?.MVP != null)
                        _inboxService.Publish(InboxMessageType.League, "League Office",
                            $"{results.MVP.FullName} wins MVP",
                            $"{results.MVP.FullName} is the {_currentSeason} Most Valuable Player " +
                            $"({results.MVP.CurrentSeasonStats?.PPG:0.0} PPG).",
                            highPriority: results.MVP.TeamId == _playerTeamId);

                    string Line(string label, Player p) =>
                        p != null ? $"{label}: {p.FullName}\n" : "";
                    _inboxService.Publish(InboxMessageType.League, "League Office",
                        $"{_currentSeason} season awards announced",
                        Line("DPOY", results?.DPOY) + Line("Rookie of the Year", results?.ROTY) +
                        Line("Sixth Man", results?.SixthMan) + Line("Most Improved", results?.MIP) +
                        $"Coach of the Year: {GetTeam(cotyTeamId)?.Name ?? cotyTeamId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Awards ceremony failed: {ex}");
            }
        }

        private void OnTradeExecutedHandler(TradeProposal proposal)
        {
            // Move traded players between Team rosters — the trade engine only
            // rewrites contracts and PlayerDatabase TeamIds. Without this the
            // rosters (and everything reading them) go stale.
            TradeDeskSystem.ApplyRosterSync(proposal, GetTeam);

            // Generate trade announcement
            _tradeAnnouncementSystem?.GenerateAnnouncement(proposal);

            // Record in transaction history
            if (_transactionLog != null && proposal != null)
            {
                var teamIds = new List<string>();
                var playerIds = new List<string>();
                foreach (var asset in proposal.AllAssets)
                {
                    if (!string.IsNullOrEmpty(asset.SendingTeamId) && !teamIds.Contains(asset.SendingTeamId))
                        teamIds.Add(asset.SendingTeamId);
                    if (asset.Type == TradeAssetType.Player && !string.IsNullOrEmpty(asset.PlayerId))
                        playerIds.Add(asset.PlayerId);
                }
                var names = playerIds
                    .Select(id => PlayerDatabase?.GetPlayer(id)?.FullName)
                    .Where(n => !string.IsNullOrEmpty(n));
                _transactionLog.Add(TransactionType.Trade, _currentDate, teamIds, playerIds,
                    $"Trade between {string.Join("/", teamIds)}: {string.Join(", ", names)}");
            }

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

        // (Manager registration hooks removed — all managers are constructed directly in Initialize())

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
