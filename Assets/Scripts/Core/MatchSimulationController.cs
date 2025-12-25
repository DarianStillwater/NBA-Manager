using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Controls match simulation with real-time play-by-play display.
    /// Supports variable speed, pausing, and coaching decision points.
    /// Designed for ESPN Gamecast-style visualization.
    /// </summary>
    public class MatchSimulationController : MonoBehaviour
    {
        public static MatchSimulationController Instance { get; private set; }

        #region Configuration

        [Header("Simulation Speed")]
        [SerializeField] private SimulationSpeed _currentSpeed = SimulationSpeed.Normal;
        [SerializeField] private float _speed1xDelay = 2.0f;   // Immersive
        [SerializeField] private float _speed2xDelay = 1.0f;   // Default
        [SerializeField] private float _speed4xDelay = 0.5f;   // Quick
        [SerializeField] private float _speed8xDelay = 0.25f;  // Very Fast
        [SerializeField] private float _instantDelay = 0f;     // Skip to decision points

        [Header("Auto-Pause Settings")]
        [SerializeField] private bool _autoPauseOnTimeout = true;
        [SerializeField] private bool _autoPauseOnQuarterEnd = true;
        [SerializeField] private bool _autoPauseOnFoulOut = true;
        [SerializeField] private bool _autoPauseOnInjury = true;
        [SerializeField] private bool _autoPauseOnClutchTime = true;

        [Header("Auto-Coach")]
        [SerializeField] private bool _autoCoachEnabled = false;
        [SerializeField] private int _autoSubFatigueThreshold = 60;

        #endregion

        #region State

        // Match state
        private Team _homeTeam;
        private Team _awayTeam;
        private Team _playerTeam;
        private bool _playerIsHome;
        private GameCoach _playerCoach;
        private GameCoach _aiCoach;

        // Simulation state
        private PossessionSimulator _possessionSimulator;
        private FoulSystem _foulSystem;
        private TimeoutIntelligence _timeoutIntelligence;
        private List<PossessionEvent> _allEvents = new List<PossessionEvent>();
        private List<PlayByPlayEntry> _playByPlay = new List<PlayByPlayEntry>();

        // Dead ball state
        private bool _isDeadBall = false;
        private int _homeUnansweredPoints = 0;
        private int _awayUnansweredPoints = 0;
        private bool _lastPossessionScored = false;

        // Game clock
        private int _currentQuarter = 1;
        private float _gameClock = 720f;
        private float _shotClock = 24f;
        private bool _homeHasPossession = true;

        // Score
        private int _homeScore = 0;
        private int _awayScore = 0;
        private int[] _quarterScores = new int[8]; // Home Q1-4+OT, Away Q1-4+OT

        // Running state
        private bool _isRunning = false;
        private bool _isPaused = false;
        private bool _isComplete = false;
        private Coroutine _simulationCoroutine;

        // Current lineup tracking
        private List<string> _homeLineup = new List<string>();
        private List<string> _awayLineup = new List<string>();
        private Dictionary<string, float> _playerMinutes = new Dictionary<string, float>();
        private Dictionary<string, int> _playerFouls = new Dictionary<string, int>();

        #endregion

        #region Properties

        public SimulationSpeed CurrentSpeed => _currentSpeed;
        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        public bool IsComplete => _isComplete;
        public int CurrentQuarter => _currentQuarter;
        public float GameClock => _gameClock;
        public float ShotClock => _shotClock;
        public int HomeScore => _homeScore;
        public int AwayScore => _awayScore;
        public bool HomeHasPossession => _homeHasPossession;
        public Team HomeTeam => _homeTeam;
        public Team AwayTeam => _awayTeam;
        public Team PlayerTeam => _playerTeam;
        public GameCoach PlayerCoach => _playerCoach;
        public List<PlayByPlayEntry> PlayByPlay => _playByPlay;
        public List<string> CurrentPlayerLineup => _playerIsHome ? _homeLineup : _awayLineup;

        public string FormattedClock => FormatGameClock(_gameClock);
        public string QuarterDisplay => _currentQuarter <= 4 ? $"Q{_currentQuarter}" : $"OT{_currentQuarter - 4}";
        public int ScoreDifferential => _playerIsHome ? _homeScore - _awayScore : _awayScore - _homeScore;
        public bool IsClutchTime => _currentQuarter >= 4 && _gameClock <= 300 && Mathf.Abs(_homeScore - _awayScore) <= 5;

        #endregion

        #region Events

        /// <summary>Fired when a new play-by-play entry is added</summary>
        public event Action<PlayByPlayEntry> OnPlayByPlay;

        /// <summary>Fired when scoreboard updates (score, clock, quarter)</summary>
        public event Action<ScoreboardUpdate> OnScoreboardUpdate;

        /// <summary>Fired when possession changes</summary>
        public event Action<bool> OnPossessionChange; // true = home has ball

        /// <summary>Fired when a timeout is called (auto-pauses)</summary>
        public event Action<string, TimeoutReason> OnTimeout; // teamId, reason

        /// <summary>Fired at quarter breaks</summary>
        public event Action<int> OnQuarterEnd;

        /// <summary>Fired when player fouls out</summary>
        public event Action<string> OnPlayerFoulOut;

        /// <summary>Fired when game is complete</summary>
        public event Action<BoxScore> OnGameComplete;

        /// <summary>Fired when user input is required (substitution, etc.)</summary>
        public event Action<CoachingDecisionRequired> OnCoachingDecisionRequired;

        /// <summary>Fired when simulation pauses</summary>
        public event Action OnSimulationPaused;

        /// <summary>Fired when simulation resumes</summary>
        public event Action OnSimulationResumed;

        /// <summary>Fired when coaching advisor has new suggestions</summary>
        public event Action<List<CoachingSuggestion>> OnCoachingSuggestions;

        /// <summary>Fired when analytics snapshot is updated</summary>
        public event Action<GameAnalyticsSnapshot> OnAnalyticsUpdate;

        /// <summary>Fired for each spatial state during possession (for court visualization)</summary>
        public event Action<SpatialState> OnSpatialStateUpdate;

        /// <summary>Fired when a shot is attempted (for court shot markers)</summary>
        public event Action<ShotMarkerData> OnShotAttempt;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize a new match
        /// </summary>
        public void InitializeMatch(Team homeTeam, Team awayTeam, Team playerTeam, GameCoach playerCoach)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _playerTeam = playerTeam;
            _playerIsHome = playerTeam.TeamId == homeTeam.TeamId;
            _playerCoach = playerCoach;

            // Create AI coach for opponent
            _aiCoach = new GameCoach(
                _playerIsHome ? awayTeam.Strategy : homeTeam.Strategy,
                null, null);

            // Initialize rules systems
            _foulSystem = new FoulSystem();
            _timeoutIntelligence = new TimeoutIntelligence();

            // Initialize simulator with foul system
            _possessionSimulator = new PossessionSimulator(null, _foulSystem);

            // Reset state
            _currentQuarter = 1;
            _gameClock = 720f;
            _shotClock = 24f;
            _homeScore = 0;
            _awayScore = 0;
            _homeHasPossession = true;
            _isComplete = false;
            _isPaused = false;
            _isRunning = false;
            _isDeadBall = false;
            _homeUnansweredPoints = 0;
            _awayUnansweredPoints = 0;
            _lastPossessionScored = false;

            // Reset foul system for new game
            _foulSystem.ResetGame();

            _allEvents.Clear();
            _playByPlay.Clear();
            _playerMinutes.Clear();
            _playerFouls.Clear();

            // Set starting lineups
            _homeLineup = homeTeam.StartingLineupIds?.ToList() ?? new List<string>();
            _awayLineup = awayTeam.StartingLineupIds?.ToList() ?? new List<string>();

            // Initialize player tracking
            foreach (var playerId in homeTeam.RosterPlayerIds.Concat(awayTeam.RosterPlayerIds))
            {
                _playerMinutes[playerId] = 0f;
                _playerFouls[playerId] = 0;
            }

            // Initialize analytics systems with player lookup functions
            _playerCoach.InitializeAnalytics(GetPlayer, GetPlayerName);

            Debug.Log($"[MatchSimController] Match initialized: {awayTeam.Name} @ {homeTeam.Name}");

            // Fire initial scoreboard update
            FireScoreboardUpdate();
        }

        /// <summary>
        /// Start or resume the simulation
        /// </summary>
        public void StartSimulation()
        {
            if (_isComplete) return;

            _isRunning = true;
            _isPaused = false;

            if (_simulationCoroutine == null)
            {
                _simulationCoroutine = StartCoroutine(SimulationLoop());
            }

            OnSimulationResumed?.Invoke();
            Debug.Log("[MatchSimController] Simulation started/resumed");
        }

        /// <summary>
        /// Pause the simulation
        /// </summary>
        public void PauseSimulation()
        {
            _isPaused = true;
            OnSimulationPaused?.Invoke();
            Debug.Log("[MatchSimController] Simulation paused");
        }

        /// <summary>
        /// Stop and reset the simulation
        /// </summary>
        public void StopSimulation()
        {
            _isRunning = false;
            _isPaused = false;

            if (_simulationCoroutine != null)
            {
                StopCoroutine(_simulationCoroutine);
                _simulationCoroutine = null;
            }
        }

        /// <summary>
        /// Set simulation speed
        /// </summary>
        public void SetSpeed(SimulationSpeed speed)
        {
            _currentSpeed = speed;
            Debug.Log($"[MatchSimController] Speed set to: {speed}");
        }

        /// <summary>
        /// Toggle auto-coach mode
        /// </summary>
        public void SetAutoCoach(bool enabled)
        {
            _autoCoachEnabled = enabled;
        }

        /// <summary>
        /// Make a substitution for the player's team
        /// </summary>
        public bool MakeSubstitution(string playerOut, string playerIn)
        {
            var lineup = _playerIsHome ? _homeLineup : _awayLineup;

            if (!lineup.Contains(playerOut))
            {
                Debug.LogWarning($"[MatchSimController] {playerOut} not in lineup");
                return false;
            }

            lineup.Remove(playerOut);
            lineup.Add(playerIn);

            AddPlayByPlayEntry(new PlayByPlayEntry
            {
                Clock = FormattedClock,
                Quarter = _currentQuarter,
                Team = _playerTeam.Abbreviation,
                Description = $"SUB: {GetPlayerName(playerIn)} checks in for {GetPlayerName(playerOut)}",
                IsHighlight = false,
                Type = PlayByPlayType.Substitution
            });

            return true;
        }

        /// <summary>
        /// Call a timeout for the player's team
        /// </summary>
        public bool CallTimeout()
        {
            var result = _playerCoach.CallTimeout(TimeoutReason.Coach);
            if (result.Success)
            {
                _isPaused = true;
                OnTimeout?.Invoke(_playerTeam.TeamId, TimeoutReason.Coach);

                AddPlayByPlayEntry(new PlayByPlayEntry
                {
                    Clock = FormattedClock,
                    Quarter = _currentQuarter,
                    Team = _playerTeam.Abbreviation,
                    Description = "TIMEOUT",
                    IsHighlight = true,
                    Type = PlayByPlayType.Timeout
                });
            }
            return result.Success;
        }

        #endregion

        #region Simulation Loop

        private IEnumerator SimulationLoop()
        {
            // Generate tip-off announcement
            var context = CreateGameContext();
            var tipOffEntry = PlayByPlayGenerator.GenerateGameEvent(GameEventType.TipOff, context);
            AddPlayByPlayEntry(tipOffEntry);

            while (!_isComplete)
            {
                // Wait if paused
                while (_isPaused)
                {
                    yield return null;
                }

                // Simulate one possession
                yield return SimulatePossession();

                // Check for quarter end
                if (_gameClock <= 0)
                {
                    yield return HandleQuarterEnd();
                }

                // Wait based on speed
                float delay = GetDelayForSpeed();
                if (delay > 0)
                {
                    yield return new WaitForSeconds(delay);
                }
            }

            _simulationCoroutine = null;
        }

        private IEnumerator SimulatePossession()
        {
            // Get current lineups
            var offenseTeam = _homeHasPossession ? _homeTeam : _awayTeam;
            var defenseTeam = _homeHasPossession ? _awayTeam : _homeTeam;
            var offenseLineup = _homeHasPossession ? _homeLineup : _awayLineup;
            var defenseLineup = _homeHasPossession ? _awayLineup : _homeLineup;

            // Get players
            var offensePlayers = GetPlayersFromLineup(offenseLineup);
            var defensePlayers = GetPlayersFromLineup(defenseLineup);

            if (offensePlayers.Length < 5 || defensePlayers.Length < 5)
            {
                Debug.LogError("[MatchSimController] Not enough players!");
                yield break;
            }

            // Get strategies
            var offenseStrategy = offenseTeam.Strategy ?? new TeamStrategy();
            var defenseStrategy = defenseTeam.Strategy ?? new TeamStrategy();

            // Calculate score differential from offense perspective
            int scoreDifferential = _homeHasPossession
                ? _homeScore - _awayScore
                : _awayScore - _homeScore;

            // Simulate with team IDs for foul tracking
            var result = _possessionSimulator.SimulatePossession(
                offensePlayers,
                defensePlayers,
                offenseStrategy,
                defenseStrategy,
                _gameClock,
                _currentQuarter,
                _homeHasPossession,
                offenseTeam.TeamId,
                defenseTeam.TeamId,
                scoreDifferential
            );

            // Process result
            ProcessPossessionResult(result, _homeHasPossession);

            // Process free throws from foul events
            yield return ProcessFreeThrowsFromResult(result, _homeHasPossession);

            // Update clock
            _gameClock = result.EndGameClock;
            _shotClock = 24f;

            // Track minutes
            float minutesElapsed = result.Duration / 60f;
            foreach (var playerId in offenseLineup.Concat(defenseLineup))
            {
                if (_playerMinutes.ContainsKey(playerId))
                    _playerMinutes[playerId] += minutesElapsed;
            }

            // Detect dead ball situations
            _isDeadBall = IsDeadBall(result);

            // Check for AI timeout before possession changes
            if (_isDeadBall)
            {
                yield return CheckAITimeout(_homeHasPossession);
            }

            // Change possession (unless flagrant/technical retains possession)
            bool retainsPossession = CheckRetainsPossession(result);
            if (!retainsPossession)
            {
                _homeHasPossession = !_homeHasPossession;
            }
            OnPossessionChange?.Invoke(_homeHasPossession);

            // Check auto-pause conditions
            if (ShouldAutoPause(result))
            {
                PauseSimulation();
                yield return null;
            }

            // Offer substitution opportunity at dead balls for player team
            if (_isDeadBall && !_autoCoachEnabled)
            {
                var playerLineup = _playerIsHome ? _homeLineup : _awayLineup;
                var fatigueCount = playerLineup.Count(id => GetPlayerEnergy(id) < _autoSubFatigueThreshold);
                var foulTroubleCount = playerLineup.Count(id => _playerFouls.GetValueOrDefault(id, 0) >= 4);

                if (fatigueCount > 0 || foulTroubleCount > 0)
                {
                    OnCoachingDecisionRequired?.Invoke(new CoachingDecisionRequired
                    {
                        Type = CoachingDecisionType.Substitution,
                        Message = $"Dead ball opportunity. {fatigueCount} player(s) fatigued, {foulTroubleCount} in foul trouble."
                    });
                }
            }
        }

        /// <summary>
        /// Check if a possession result creates a dead ball situation.
        /// </summary>
        private bool IsDeadBall(PossessionResult result)
        {
            foreach (var evt in result.Events)
            {
                // Fouls create dead balls
                if (evt.Type == EventType.Foul)
                    return true;

                // Timeouts are dead balls
                if (evt.Type == EventType.Timeout)
                    return true;

                // Out of bounds turnovers
                if (evt.Type == EventType.Turnover &&
                    evt.ViolationDetail?.ViolationType == ViolationType.OutOfBounds)
                    return true;

                // Made baskets (technically a dead ball before inbound)
                if (evt.Type == EventType.Shot && evt.Outcome == EventOutcome.Success)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if the offensive team retains possession (flagrant fouls, technicals).
        /// </summary>
        private bool CheckRetainsPossession(PossessionResult result)
        {
            foreach (var evt in result.Events)
            {
                if (evt.Type == EventType.Foul && evt.FoulDetail != null)
                {
                    // Flagrant fouls and technicals give ball back to fouled team
                    if (evt.FoulDetail.FoulType == FoulType.Flagrant1 ||
                        evt.FoulDetail.FoulType == FoulType.Flagrant2 ||
                        evt.FoulDetail.FoulType == FoulType.Technical)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check if AI coach should call timeout.
        /// </summary>
        private IEnumerator CheckAITimeout(bool offenseIsHome)
        {
            // Determine which AI coach to check (the one not on offense)
            bool checkHome = !offenseIsHome;
            var aiTeam = checkHome ? _homeTeam : _awayTeam;
            var coach = (checkHome == _playerIsHome) ? _playerCoach : _aiCoach;

            // Skip if player team (unless auto-coach is enabled)
            if ((checkHome == _playerIsHome) && !_autoCoachEnabled)
                yield break;

            // Skip if no timeouts remaining
            if (coach.TimeoutsRemaining <= 0)
                yield break;

            // Get players on court for energy check
            var lineup = checkHome ? _homeLineup : _awayLineup;
            var players = GetPlayersFromLineup(lineup);

            // Calculate unanswered points against this team
            int unansweredAgainst = checkHome ? _awayUnansweredPoints : _homeUnansweredPoints;

            // Create game context for AI decision
            int teamScoreDiff = checkHome
                ? _homeScore - _awayScore
                : _awayScore - _homeScore;

            var gameContext = new GameContext
            {
                Quarter = _currentQuarter,
                GameClock = _gameClock,
                ScoreDifferential = teamScoreDiff,
                IsClutchTime = IsClutchTime,
                TimeoutJustCalled = false,
                OpponentRunPoints = unansweredAgainst,
                OpponentJustScored = _lastPossessionScored && (offenseIsHome != checkHome)
            };

            // Get AI timeout decision
            var decision = _timeoutIntelligence.ShouldCallTimeout(
                players,
                gameContext,
                coach.TimeoutsRemaining,
                isPlayerTeam: false);

            if (decision.ShouldCall)
            {
                // AI calls timeout
                coach.UseTimeout();

                AddPlayByPlayEntry(new PlayByPlayEntry
                {
                    Clock = FormattedClock,
                    Quarter = _currentQuarter,
                    Team = aiTeam.Abbreviation,
                    Description = GetTimeoutDescription(decision.Reason),
                    IsHighlight = true,
                    Type = PlayByPlayType.Timeout
                });

                OnTimeout?.Invoke(aiTeam.TeamId, decision.Reason);

                // Reset unanswered points after timeout
                if (checkHome)
                    _awayUnansweredPoints = 0;
                else
                    _homeUnansweredPoints = 0;

                // Brief pause to show timeout
                yield return new WaitForSeconds(0.5f);
            }
        }

        private string GetTimeoutDescription(TimeoutReason reason)
        {
            return reason switch
            {
                TimeoutReason.StopRun => "TIMEOUT called to stop the run!",
                TimeoutReason.IcingShooter => "TIMEOUT called - icing the shooter!",
                TimeoutReason.AdvanceBall => "TIMEOUT called to advance the ball.",
                TimeoutReason.DrawUpPlay => "TIMEOUT called to draw up a play.",
                TimeoutReason.RestPlayers => "TIMEOUT called to rest players.",
                TimeoutReason.Mandatory => "Full timeout.",
                _ => "TIMEOUT"
            };
        }

        /// <summary>
        /// Process free throws from foul events.
        /// </summary>
        private IEnumerator ProcessFreeThrowsFromResult(PossessionResult result, bool offenseIsHome)
        {
            foreach (var evt in result.Events)
            {
                if (evt.Type == EventType.Foul && evt.FoulDetail != null &&
                    evt.FoulDetail.FreeThrowsAwarded > 0)
                {
                    // Get the fouled player (shooter)
                    var shooter = GetPlayer(evt.ActorPlayerId);
                    if (shooter == null) continue;

                    // Calculate FT results using the handler
                    var ftHandler = _possessionSimulator.FreeThrowHandler;
                    var ftContext = new GameContext
                    {
                        Quarter = _currentQuarter,
                        GameClock = _gameClock,
                        ScoreDifferential = offenseIsHome
                            ? _homeScore - _awayScore
                            : _awayScore - _homeScore,
                        IsClutchTime = IsClutchTime,
                        TimeoutJustCalled = false
                    };

                    var ftResult = ftHandler.CalculateFreeThrows(
                        shooter,
                        evt.FoulDetail.FreeThrowsAwarded,
                        ftContext);

                    // Update score
                    if (ftResult.Made > 0)
                    {
                        if (offenseIsHome)
                            _homeScore += ftResult.Made;
                        else
                            _awayScore += ftResult.Made;

                        // Track for unanswered points
                        if (offenseIsHome)
                        {
                            _homeUnansweredPoints += ftResult.Made;
                            _awayUnansweredPoints = 0;
                        }
                        else
                        {
                            _awayUnansweredPoints += ftResult.Made;
                            _homeUnansweredPoints = 0;
                        }
                    }

                    // Add play-by-play entry for free throws
                    AddPlayByPlayEntry(new PlayByPlayEntry
                    {
                        Clock = FormattedClock,
                        Quarter = _currentQuarter,
                        Team = offenseIsHome ? _homeTeam.Abbreviation : _awayTeam.Abbreviation,
                        Description = ftResult.PlayByPlayText,
                        IsHighlight = ftResult.Made == ftResult.Attempts && ftResult.Attempts > 1,
                        Type = ftResult.Made > 0 ? PlayByPlayType.Score : PlayByPlayType.Regular,
                        ActorPlayerId = shooter.PlayerId,
                        Points = ftResult.Made,
                        HomeScore = _homeScore,
                        AwayScore = _awayScore
                    });

                    FireScoreboardUpdate();

                    // Brief delay for free throw display
                    float delay = GetDelayForSpeed() * 0.5f;
                    if (delay > 0)
                        yield return new WaitForSeconds(delay);
                }
            }
        }

        private float GetPlayerEnergy(string playerId)
        {
            var player = GetPlayer(playerId);
            return player?.Energy ?? 100f;
        }

        private void ProcessPossessionResult(PossessionResult result, bool offenseIsHome)
        {
            // Determine if this is the player's team possession
            bool isPlayerTeamPossession = (_playerIsHome == offenseIsHome);
            var offenseLineup = offenseIsHome ? _homeLineup : _awayLineup;
            var defenseLineup = offenseIsHome ? _awayLineup : _homeLineup;

            // Fire spatial state events for court visualization
            if (result.SpatialStates != null)
            {
                foreach (var state in result.SpatialStates)
                {
                    OnSpatialStateUpdate?.Invoke(state);
                }
            }

            // Feed data to analytics tracker
            var analyticsTracker = _playerCoach.GetAnalyticsTracker();
            if (analyticsTracker != null)
            {
                // Record from player team's perspective
                if (isPlayerTeamPossession)
                {
                    analyticsTracker.RecordPossession(result, true, offenseLineup, defenseLineup);
                }
                else
                {
                    analyticsTracker.RecordPossession(result, false, defenseLineup, offenseLineup);
                }

                // Set current lineup for +/- tracking
                analyticsTracker.SetCurrentLineup(_playerIsHome ? _homeLineup : _awayLineup);
            }

            // Feed matchup data
            var matchupEvaluator = _playerCoach.GetMatchupEvaluator();
            if (matchupEvaluator != null && !isPlayerTeamPossession)
            {
                // Track opponent possessions for defensive matchup analysis
                foreach (var evt in result.Events.Where(e => e.Type == EventType.Shot))
                {
                    if (!string.IsNullOrEmpty(evt.DefenderPlayerId) && !string.IsNullOrEmpty(evt.ActorPlayerId))
                    {
                        matchupEvaluator.RecordMatchupPossession(
                            evt.DefenderPlayerId,
                            evt.ActorPlayerId,
                            evt.PointsScored,
                            evt.Outcome == EventOutcome.Success
                        );
                    }
                }
            }

            foreach (var evt in result.Events)
            {
                _allEvents.Add(evt);

                // Generate play-by-play
                var entry = CreatePlayByPlayEntry(evt, offenseIsHome);
                if (entry != null)
                {
                    AddPlayByPlayEntry(entry);
                }

                // Update score
                if (evt.PointsScored > 0)
                {
                    if (offenseIsHome)
                    {
                        _homeScore += evt.PointsScored;
                        _homeUnansweredPoints += evt.PointsScored;
                        _awayUnansweredPoints = 0;
                    }
                    else
                    {
                        _awayScore += evt.PointsScored;
                        _awayUnansweredPoints += evt.PointsScored;
                        _homeUnansweredPoints = 0;
                    }

                    _lastPossessionScored = true;
                    FireScoreboardUpdate();
                }
                else
                {
                    _lastPossessionScored = false;
                }

                // Fire shot marker event for court visualization
                if (evt.Type == EventType.Shot)
                {
                    var shotMarker = new ShotMarkerData(
                        evt.ActorPlayerId,
                        GetPlayerName(evt.ActorPlayerId),
                        evt.ActorPosition,
                        evt.Outcome == EventOutcome.Success,
                        evt.PointsScored,
                        _gameClock,
                        _currentQuarter,
                        ConvertToShotType(evt.ShotType)
                    );
                    OnShotAttempt?.Invoke(shotMarker);
                }

                // Track fouls
                if (evt.Type == EventType.Foul && !string.IsNullOrEmpty(evt.DefenderPlayerId))
                {
                    if (_playerFouls.ContainsKey(evt.DefenderPlayerId))
                    {
                        _playerFouls[evt.DefenderPlayerId]++;

                        if (_playerFouls[evt.DefenderPlayerId] >= 6)
                        {
                            OnPlayerFoulOut?.Invoke(evt.DefenderPlayerId);
                            HandleFoulOut(evt.DefenderPlayerId);
                        }
                    }
                }
            }

            // Analyze and fire coaching suggestions every few possessions
            if (TotalPossessions % 3 == 0)
            {
                TriggerCoachingAnalysis();
            }
        }

        private int TotalPossessions => _allEvents.Count(e => e.Type == EventType.Shot || e.Type == EventType.Turnover);

        private void TriggerCoachingAnalysis()
        {
            var advisor = _playerCoach.GetCoachingAdvisor();
            if (advisor == null) return;

            var suggestions = advisor.AnalyzeAndSuggest(_playerCoach, CurrentPlayerLineup);

            if (suggestions.Any())
            {
                OnCoachingSuggestions?.Invoke(suggestions);

                // Auto-pause for high priority suggestions if configured
                var urgentSuggestions = suggestions.Where(s => s.Priority == SuggestionPriority.High).ToList();
                if (urgentSuggestions.Any() && !_autoCoachEnabled)
                {
                    // Could add auto-pause here if desired
                    // PauseSimulation();
                }
            }

            // Fire analytics update
            var snapshot = _playerCoach.GetCurrentAnalytics();
            OnAnalyticsUpdate?.Invoke(snapshot);
        }

        private PlayByPlayEntry CreatePlayByPlayEntry(PossessionEvent evt, bool offenseIsHome)
        {
            // Create game context for enhanced play-by-play generation
            var context = new GameContext
            {
                HomeTeam = _homeTeam,
                AwayTeam = _awayTeam,
                OffenseTeam = offenseIsHome ? _homeTeam : _awayTeam,
                HomeScore = _homeScore,
                AwayScore = _awayScore,
                Quarter = _currentQuarter,
                GameClock = _gameClock,
                HomeOnOffense = offenseIsHome
            };

            // Use enhanced PlayByPlayGenerator for broadcast-style descriptions
            return PlayByPlayGenerator.Generate(evt, context, GetPlayer);
        }

        private Player GetPlayer(string playerId)
        {
            return GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);
        }

        private PlayByPlayType GetPlayByPlayType(PossessionEvent evt)
        {
            return evt.Type switch
            {
                EventType.Shot when evt.Outcome == EventOutcome.Success => PlayByPlayType.Score,
                EventType.FreeThrow when evt.Outcome == EventOutcome.Success => PlayByPlayType.Score,
                EventType.Block => PlayByPlayType.DefensivePlay,
                EventType.Steal => PlayByPlayType.DefensivePlay,
                EventType.Rebound => PlayByPlayType.DefensivePlay,
                EventType.Turnover => PlayByPlayType.Turnover,
                EventType.Foul => PlayByPlayType.Foul,
                EventType.Timeout => PlayByPlayType.Timeout,
                _ => PlayByPlayType.Regular
            };
        }

        private IEnumerator HandleQuarterEnd()
        {
            OnQuarterEnd?.Invoke(_currentQuarter);

            var context = CreateGameContext();
            var entry = PlayByPlayGenerator.GenerateGameEvent(GameEventType.QuarterEnd, context);
            AddPlayByPlayEntry(entry);

            if (_autoPauseOnQuarterEnd)
            {
                PauseSimulation();
            }

            // Check for game end
            if (_currentQuarter >= 4 && _homeScore != _awayScore)
            {
                HandleGameEnd();
                yield break;
            }

            // Start next quarter/overtime
            _currentQuarter++;
            _gameClock = _currentQuarter <= 4 ? 720f : 300f;
            _homeHasPossession = _currentQuarter % 2 == 1; // Alternate

            // Reset team fouls for new quarter
            _foulSystem.ResetQuarterFouls();

            context = CreateGameContext();
            var eventType = _currentQuarter > 4 ? GameEventType.OvertimeStart : GameEventType.QuarterStart;
            entry = PlayByPlayGenerator.GenerateGameEvent(eventType, context);
            AddPlayByPlayEntry(entry);

            FireScoreboardUpdate();
        }

        private void HandleGameEnd()
        {
            _isComplete = true;
            _isRunning = false;

            var context = CreateGameContext();
            var entry = PlayByPlayGenerator.GenerateGameEvent(GameEventType.GameEnd, context);
            AddPlayByPlayEntry(entry);

            var boxScore = CreateBoxScore();
            OnGameComplete?.Invoke(boxScore);

            Debug.Log($"[MatchSimController] Game complete: {_awayTeam.Abbreviation} {_awayScore} @ {_homeTeam.Abbreviation} {_homeScore}");
        }

        private GameContext CreateGameContext()
        {
            return new GameContext
            {
                HomeTeam = _homeTeam,
                AwayTeam = _awayTeam,
                OffenseTeam = _homeHasPossession ? _homeTeam : _awayTeam,
                HomeScore = _homeScore,
                AwayScore = _awayScore,
                Quarter = _currentQuarter,
                GameClock = _gameClock,
                HomeOnOffense = _homeHasPossession
            };
        }

        private void HandleFoulOut(string playerId)
        {
            // Find which team the player is on
            bool isHome = _homeLineup.Contains(playerId);
            var lineup = isHome ? _homeLineup : _awayLineup;
            var team = isHome ? _homeTeam : _awayTeam;

            // Auto-sub if available
            var bench = team.RosterPlayerIds
                .Where(id => !lineup.Contains(id) && _playerFouls.GetValueOrDefault(id, 0) < 6)
                .FirstOrDefault();

            if (bench != null)
            {
                lineup.Remove(playerId);
                lineup.Add(bench);

                AddPlayByPlayEntry(new PlayByPlayEntry
                {
                    Clock = FormattedClock,
                    Quarter = _currentQuarter,
                    Team = team.Abbreviation,
                    Description = $"{GetPlayerName(playerId)} fouls out! {GetPlayerName(bench)} checks in.",
                    IsHighlight = true,
                    Type = PlayByPlayType.Substitution
                });
            }

            if (_autoPauseOnFoulOut && team == _playerTeam)
            {
                PauseSimulation();
                OnCoachingDecisionRequired?.Invoke(new CoachingDecisionRequired
                {
                    Type = CoachingDecisionType.FoulOutSubstitution,
                    PlayerId = playerId
                });
            }
        }

        #endregion

        #region Helpers

        private float GetDelayForSpeed()
        {
            return _currentSpeed switch
            {
                SimulationSpeed.Slow => _speed1xDelay,
                SimulationSpeed.Normal => _speed2xDelay,
                SimulationSpeed.Fast => _speed4xDelay,
                SimulationSpeed.VeryFast => _speed8xDelay,
                SimulationSpeed.Instant => _instantDelay,
                _ => _speed2xDelay
            };
        }

        private bool ShouldAutoPause(PossessionResult result)
        {
            if (_autoPauseOnClutchTime && IsClutchTime && !_isPaused)
            {
                return true;
            }

            foreach (var evt in result.Events)
            {
                if (_autoPauseOnTimeout && evt.Type == EventType.Timeout)
                    return true;
            }

            return false;
        }

        private Player[] GetPlayersFromLineup(List<string> lineup)
        {
            var players = new Player[5];
            var db = GameManager.Instance?.PlayerDatabase;
            if (db == null) return players;

            for (int i = 0; i < Math.Min(5, lineup.Count); i++)
            {
                players[i] = db.GetPlayer(lineup[i]);
            }
            return players;
        }

        private string GetPlayerName(string playerId)
        {
            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);
            return player?.DisplayName ?? playerId ?? "Unknown";
        }

        private string FormatGameClock(float seconds)
        {
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{mins}:{secs:D2}";
        }

        private ShotType ConvertToShotType(Simulation.ShotType? simShotType)
        {
            if (simShotType == null) return Data.ShotType.Jumper;

            return simShotType.Value switch
            {
                Simulation.ShotType.Layup => Data.ShotType.Layup,
                Simulation.ShotType.Dunk => Data.ShotType.Dunk,
                Simulation.ShotType.MidRange => Data.ShotType.Jumper,
                Simulation.ShotType.ThreePointer => Data.ShotType.ThreePointer,
                Simulation.ShotType.FreeThrow => Data.ShotType.FreeThrow,
                _ => Data.ShotType.Jumper
            };
        }

        private void FireScoreboardUpdate()
        {
            OnScoreboardUpdate?.Invoke(new ScoreboardUpdate
            {
                HomeScore = _homeScore,
                AwayScore = _awayScore,
                Quarter = _currentQuarter,
                Clock = _gameClock,
                ShotClock = _shotClock,
                HomeHasPossession = _homeHasPossession
            });
        }

        private void AddPlayByPlayEntry(PlayByPlayEntry entry)
        {
            _playByPlay.Add(entry);
            OnPlayByPlay?.Invoke(entry);
        }

        private BoxScore CreateBoxScore()
        {
            var boxScore = new BoxScore(_homeTeam.TeamId, _awayTeam.TeamId)
            {
                HomeScore = _homeScore,
                AwayScore = _awayScore
            };

            // Add player stats from events
            foreach (var playerId in _playerMinutes.Keys)
            {
                boxScore.AddPlayerStats(playerId, new PlayerGameStats
                {
                    PlayerId = playerId,
                    Minutes = _playerMinutes[playerId],
                    PersonalFouls = _playerFouls.GetValueOrDefault(playerId, 0)
                });
            }

            return boxScore;
        }

        #endregion
    }

    #region Data Types

    public enum SimulationSpeed
    {
        Slow,      // 2.0s per possession - Immersive
        Normal,    // 1.0s per possession - Default
        Fast,      // 0.5s per possession - Quick
        VeryFast,  // 0.25s per possession
        Instant    // 0s - Skip to decision points
    }

    [Serializable]
    public class PlayByPlayEntry
    {
        public string Clock;
        public int Quarter;
        public string Team;
        public string Description;
        public bool IsHighlight;
        public PlayByPlayType Type;
        public string ActorPlayerId;
        public int Points;
        public int HomeScore;
        public int AwayScore;
        public DateTime Timestamp = DateTime.Now;
    }

    public enum PlayByPlayType
    {
        Regular,
        Score,
        DefensivePlay,
        Turnover,
        Foul,
        Timeout,
        Substitution,
        GameEvent
    }

    [Serializable]
    public class ScoreboardUpdate
    {
        public int HomeScore;
        public int AwayScore;
        public int Quarter;
        public float Clock;
        public float ShotClock;
        public bool HomeHasPossession;
    }

    [Serializable]
    public class CoachingDecisionRequired
    {
        public CoachingDecisionType Type;
        public string PlayerId;
        public string Message;
    }

    public enum CoachingDecisionType
    {
        Timeout,
        Substitution,
        FoulOutSubstitution,
        EndOfQuarter,
        ClutchTime
    }

    #endregion
}
