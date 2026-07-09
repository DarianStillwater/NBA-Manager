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
using TimeoutReason = NBAHeadCoach.Core.Gameplay.TimeoutReason;
using ShotType = NBAHeadCoach.Core.Data.ShotType;
using EventType = NBAHeadCoach.Core.Simulation.EventType;
using GameContext = NBAHeadCoach.Core.Simulation.GameContext;

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
        private bool _liveBallForNext = false; // next possession starts off a live board/steal
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

        // Full per-player box score, updated live as possessions resolve (shared applier
        // with the headless GameSimulator, so both paths count stats identically).
        private BoxScore _liveBox;

        /// <summary>Live box score for in-game UI (overlay, tooltips). Stats lead the
        /// on-screen presentation by at most one possession. Scores synced on read.</summary>
        public BoxScore LiveBoxScore
        {
            get
            {
                if (_liveBox != null)
                {
                    _liveBox.HomeScore = _homeScore;
                    _liveBox.AwayScore = _awayScore;
                }
                return _liveBox;
            }
        }

        public List<string> CurrentHomeLineup => new List<string>(_homeLineup);
        public List<string> CurrentAwayLineup => new List<string>(_awayLineup);

        public bool HasFouledOut(string playerId) => _playerFouls.GetValueOrDefault(playerId, 0) >= 6;

        /// <summary>Live on-court five for a team — the single source of truth for sub UIs.</summary>
        public IReadOnlyList<string> GetLineup(string teamId)
        {
            if (_homeTeam != null && _homeTeam.TeamId == teamId) return _homeLineup;
            if (_awayTeam != null && _awayTeam.TeamId == teamId) return _awayLineup;
            return new List<string>();
        }

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

        /// <summary>Fires after any lineup change (manual sub or foul-out auto-sub): teamId, outgoing id, incoming id.</summary>
        public event Action<string, string, string> OnLineupChanged;

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

        /// <summary>
        /// Fired with a complete possession packaged for presentation (FM-style playback).
        /// When subscribed, per-possession presentation events (PBP, scoreboard, shots,
        /// spatial states) are captured into the packet instead of firing live, and the
        /// sim loop blocks until <see cref="CompletePresentation"/> is called.
        /// With no subscriber, behavior is exactly the legacy live-event flow.
        /// </summary>
        public event Action<PossessionPlaybackPacket> OnPossessionReady;

        #endregion

        #region Presentation Capture (FM-style playback handshake)

        private PossessionPlaybackPacket _captureSink;       // non-null while capturing a possession
        private float _captureCurrentOffset;                  // playback offset for events being emitted
        private bool _presentationDone;

        /// <summary>Called by the playback director when it has finished presenting a possession.</summary>
        public void CompletePresentation() => _presentationDone = true;

        private bool HasPresentationConsumer => OnPossessionReady != null;

        private void BeginCapture(PossessionResult result, bool offenseIsHome)
        {
            _captureSink = new PossessionPlaybackPacket
            {
                Timeline = result.SpatialStates ?? new List<SpatialState>(),
                StartGameClock = result.StartGameClock,
                EndGameClock = result.EndGameClock,
                PresentationSeconds = result.PresentationSeconds,
                LiveSeconds = result.LiveSeconds,
                Quarter = result.Quarter,
                OffenseIsHome = offenseIsHome
            };
            _captureCurrentOffset = 0f;
        }

        private void CaptureOffsetForEvent(float eventGameClock)
        {
            if (_captureSink == null) return;
            float offset = _captureSink.StartGameClock - eventGameClock;
            float max = _captureSink.DurationGameSeconds;
            _captureCurrentOffset = Mathf.Clamp(offset, 0f, max);
        }

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

            // Create AI coach for opponent — with a real playbook and default
            // player instructions, same as the player's coach
            var aiTeam = _playerIsHome ? awayTeam : homeTeam;
            _aiCoach = new GameCoach(
                aiTeam.Strategy,
                PlayBook.CreateDefault(aiTeam.TeamId),
                TeamGameInstructions.CreateDefault(aiTeam.TeamId, aiTeam.Roster ?? new List<Player>()));

            // Initialize rules systems
            _foulSystem = new FoulSystem();
            _timeoutIntelligence = new TimeoutIntelligence();

            // Initialize simulator with foul system; interactive matches get full
            // choreographed spatial timelines for the court view
            _possessionSimulator = new PossessionSimulator(null, _foulSystem);
            _possessionSimulator.SpatialDetail = Simulation.Choreography.SpatialDetailLevel.Full;

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
            _liveBallForNext = false;
            _homeUnansweredPoints = 0;
            _awayUnansweredPoints = 0;
            _lastPossessionScored = false;

            // Reset foul system for new game
            _foulSystem.ResetGame();

            _allEvents.Clear();
            _playByPlay.Clear();
            _playerMinutes.Clear();
            _playerFouls.Clear();

            // Set starting lineups (de-duped defensively — a duplicated id would sim short-handed)
            _homeLineup = homeTeam.StartingLineupIds?.Distinct().ToList() ?? new List<string>();
            _awayLineup = awayTeam.StartingLineupIds?.Distinct().ToList() ?? new List<string>();

            // Initialize player tracking + the live box score (HomeTeam/AwayTeam set so the
            // game-end result carries real team references, not nulls).
            _liveBox = new BoxScore(homeTeam.TeamId, awayTeam.TeamId)
            {
                HomeTeam = homeTeam,
                AwayTeam = awayTeam
            };
            foreach (var playerId in homeTeam.RosterPlayerIds.Concat(awayTeam.RosterPlayerIds))
            {
                _playerMinutes[playerId] = 0f;
                _playerFouls[playerId] = 0;
                _liveBox.InitializePlayer(playerId);
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

            if (string.IsNullOrEmpty(playerOut) || string.IsNullOrEmpty(playerIn))
            {
                Debug.LogWarning("[MatchSimController] Substitution rejected: missing player id");
                return false;
            }
            if (!lineup.Contains(playerOut))
            {
                Debug.LogWarning($"[MatchSimController] {playerOut} not in lineup");
                return false;
            }
            if (lineup.Contains(playerIn))
            {
                Debug.LogWarning($"[MatchSimController] {playerIn} is already on the court");
                return false;
            }
            if (_playerTeam == null || !_playerTeam.RosterPlayerIds.Contains(playerIn))
            {
                Debug.LogWarning($"[MatchSimController] {playerIn} is not on the roster");
                return false;
            }
            if (_playerFouls.GetValueOrDefault(playerIn, 0) >= 6)
            {
                Debug.LogWarning($"[MatchSimController] {playerIn} has fouled out");
                return false;
            }

            ReplaceInLineup(lineup, _playerTeam.TeamId, playerOut, playerIn);

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

                // Wait based on speed — always yield at least once per possession to prevent freezing.
                // In packet mode the playback director owns all pacing, so no extra delay here.
                float delay = HasPresentationConsumer ? 0f : GetDelayForSpeed();
                if (delay > 0)
                {
                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    yield return null; // Prevent Unity freeze at Instant speed
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

            // A coach's play call (set play or quick action) forces the next
            // possession's action family — consumed once
            var offenseCoach = _homeHasPossession == _playerIsHome ? _playerCoach : _aiCoach;
            var calledAction = offenseCoach?.ConsumePendingActionCall();

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
                scoreDifferential,
                _liveBallForNext,
                calledAction
            );

            // Transition logic: the next possession starts live off a defensive
            // board or a steal (same rule as the headless sim)
            _liveBallForNext = result.Outcome == PossessionOutcome.Miss ||
                               result.Outcome == PossessionOutcome.Block ||
                               (result.Outcome == PossessionOutcome.Turnover &&
                                result.Events.Any(e => e.Type == EventType.Steal));

            // In packet mode, presentation side-effects are captured instead of fired live
            if (HasPresentationConsumer)
                BeginCapture(result, _homeHasPossession);

            // Snapshot scores for per-possession +/- (applied after FT processing)
            int preHomeScore = _homeScore;
            int preAwayScore = _awayScore;

            // Process result
            ProcessPossessionResult(result, _homeHasPossession);

            // Process free throws from foul events
            yield return ProcessFreeThrowsFromResult(result, _homeHasPossession);

            // Update clock
            _gameClock = result.EndGameClock;
            _shotClock = 24f;

            // Track minutes + live box score minutes/plus-minus
            float minutesElapsed = result.Duration / 60f;
            int deltaHome = _homeScore - preHomeScore;
            int deltaAway = _awayScore - preAwayScore;
            foreach (var playerId in offenseLineup.Concat(defenseLineup))
            {
                if (_playerMinutes.ContainsKey(playerId))
                    _playerMinutes[playerId] += minutesElapsed;

                if (_liveBox != null && _liveBox.PlayerStats.TryGetValue(playerId, out var line))
                {
                    line.SecondsPlayed += result.Duration;
                    bool onHome = _homeLineup.Contains(playerId);
                    line.PlusMinus += onHome ? deltaHome - deltaAway : deltaAway - deltaHome;
                }
            }

            // Hand the captured possession to the playback director and wait until
            // it has been presented (played or skipped). Sim state is already final.
            if (_captureSink != null)
            {
                var packet = _captureSink;
                _captureSink = null;

                // Narration beats and re-timed events interleave out of append order; the
                // director's forward-only cursor needs ascending offsets. OrderBy is stable,
                // so same-offset entries keep their append order (entry → scoreboard → marker).
                packet.Events = packet.Events.OrderBy(e => e.Offset).ToList();

                packet.AnyHighlight = packet.Events.Any(e => e.Entry != null && e.Entry.IsHighlight);
                packet.AnyScore = packet.Events.Any(e => (e.Entry != null && e.Entry.Points > 0) || e.ShotPoints > 0);
                packet.AnyDefensiveHighlight = result.Events.Any(e =>
                    e.Type == EventType.Block || e.Type == EventType.Steal);

                _presentationDone = false;
                OnPossessionReady?.Invoke(packet);

                // If the subscriber vanished or completed synchronously, don't deadlock
                yield return new WaitUntil(() => _presentationDone || OnPossessionReady == null);
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
            var timeoutCtx = new TimeoutIntelligence.TimeoutContext
            {
                Quarter = gameContext.Quarter,
                GameClock = gameContext.GameClock,
                ScoreDifferential = gameContext.ScoreDifferential,
                TimeoutsRemaining = coach.TimeoutsRemaining,
                OpponentRunPoints = gameContext.OpponentRunPoints,
                OpponentJustScored = gameContext.OpponentJustScored
            };
            var decision = _timeoutIntelligence.ShouldCallTimeout(timeoutCtx);

            if (decision.ShouldCall)
            {
                // AI calls timeout
                coach.CallTimeout(TimeoutReason.Coach);

                AddPlayByPlayEntry(new PlayByPlayEntry
                {
                    Clock = FormattedClock,
                    Quarter = _currentQuarter,
                    Team = aiTeam.Abbreviation,
                    Description = GetTimeoutDescription((TimeoutReason)(int)decision.Reason),
                    IsHighlight = true,
                    Type = PlayByPlayType.Timeout
                });

                OnTimeout?.Invoke(aiTeam.TeamId, (TimeoutReason)(int)decision.Reason);

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
            int ftSequence = 0;
            foreach (var evt in result.Events)
            {
                if (evt.Type == EventType.Foul && evt.FoulDetail != null &&
                    evt.FoulDetail.FreeThrowsAwarded > 0)
                {
                    // Get the fouled player (shooter)
                    var shooter = GetPlayer(evt.ActorPlayerId);
                    if (shooter == null) continue;

                    // Use the simulator's pre-computed FT result when present so the
                    // displayed score always matches the simulated possession (and the
                    // choreographed FT visuals). Re-roll only as a legacy fallback.
                    var ftResult = evt.FreeThrowResult;
                    if (ftResult == null)
                    {
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

                        ftResult = ftHandler.CalculateFreeThrows(
                            shooter,
                            evt.FoulDetail.FreeThrowsAwarded,
                            ftContext);
                    }

                    // In packet mode, free throws present in the tail past the live timeline.
                    // When the choreographer produced FT beats, anchor the summary entry to the
                    // LAST attempt's actual visual moment instead of the synthetic 1.5s spacing.
                    if (_captureSink != null)
                    {
                        ftSequence++;
                        float? lastFtBeat = null;
                        if (result.NarrationBeats != null)
                        {
                            for (int i = result.NarrationBeats.Count - 1; i >= 0; i--)
                            {
                                if (result.NarrationBeats[i].Kind ==
                                    Simulation.Choreography.NarrationBeatKind.FreeThrowAttempt)
                                {
                                    lastFtBeat = result.NarrationBeats[i].T;
                                    break;
                                }
                            }
                        }

                        if (lastFtBeat.HasValue)
                        {
                            _captureCurrentOffset = lastFtBeat.Value + 0.2f;
                            _captureSink.TailSeconds = Mathf.Max(_captureSink.TailSeconds,
                                lastFtBeat.Value - _captureSink.DurationGameSeconds + 1f);
                        }
                        else
                        {
                            _captureCurrentOffset = _captureSink.DurationGameSeconds + ftSequence * 1.5f;
                            _captureSink.TailSeconds = ftSequence * 1.5f + 0.5f;
                        }
                    }

                    // Live box score: free-throw stats (FG points already applied via events)
                    _liveBox?.AddFreeThrows(shooter.PlayerId, ftResult.Made, ftResult.Attempts);

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

                    // Brief delay for free throw display (director paces this in packet mode)
                    if (_captureSink == null)
                    {
                        float delay = GetDelayForSpeed() * 0.5f;
                        if (delay > 0)
                            yield return new WaitForSeconds(delay);
                    }
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

            // Fire spatial state events for court visualization.
            // In packet mode the timeline travels inside the packet instead.
            if (result.SpatialStates != null && _captureSink == null)
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

            // Live box score: per-player stats via the same applier as the headless sim
            if (_liveBox != null)
            {
                string defTeamId = offenseIsHome ? _awayTeam.TeamId : _homeTeam.TeamId;
                BoxScoreEventApplier.Apply(_liveBox, result, offenseIsHome, defTeamId, _currentQuarter);
            }

            // Radio narration for the court bar, timed to the choreographed beats
            EmitNarrationEvents(result, offenseIsHome);

            foreach (var evt in result.Events)
            {
                _allEvents.Add(evt);

                // Free throws are scored + narrated exclusively by ProcessFreeThrowsFromResult.
                // Counting them here as well double-counted FT points and duplicated FT play-by-play.
                if (evt.Type == EventType.FreeThrow)
                    continue;

                // Anchor captured presentation events to this event's true on-screen moment
                // (choreographed beat time when available; possession-boundary clock otherwise).
                float? beatT = BeatTimeFor(evt, result.NarrationBeats);
                if (beatT.HasValue && _captureSink != null)
                    _captureCurrentOffset = beatT.Value;
                else
                    CaptureOffsetForEvent(evt.GameClock);

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
                        ConvertToShotType(evt.ShotType, evt.ActorPosition, offenseIsHome)
                    );

                    if (_captureSink != null)
                    {
                        _captureSink.Events.Add(new TimedPlaybackEvent
                        {
                            Offset = _captureCurrentOffset,
                            ShotMarker = shotMarker,
                            ShotMade = evt.Outcome == EventOutcome.Success,
                            ShotPoints = evt.PointsScored
                        });
                    }
                    else
                    {
                        OnShotAttempt?.Invoke(shotMarker);
                    }
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
            var context = new PlayByPlayContext
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

        // ── Radio narration (court bar) ─────────────────────────────

        /// <summary>Action-beat priority for the per-possession line budget (lower = kept first).</summary>
        private static int NarrationPriority(Simulation.Choreography.NarrationBeatKind kind)
        {
            switch (kind)
            {
                case Simulation.Choreography.NarrationBeatKind.Drive:
                case Simulation.Choreography.NarrationBeatKind.Curl:
                case Simulation.Choreography.NarrationBeatKind.BackdoorCut:
                case Simulation.Choreography.NarrationBeatKind.Handoff:
                    return 1;
                case Simulation.Choreography.NarrationBeatKind.ScreenSet:
                case Simulation.Choreography.NarrationBeatKind.Roll:
                case Simulation.Choreography.NarrationBeatKind.PostMove:
                case Simulation.Choreography.NarrationBeatKind.PostFeed:
                case Simulation.Choreography.NarrationBeatKind.KickOut:
                case Simulation.Choreography.NarrationBeatKind.AssistPass:
                    return 2;
                case Simulation.Choreography.NarrationBeatKind.BringUp:
                    return 3;
                default:
                    return 4;   // SwingPass / IsoJab
            }
        }

        private static bool IsResultBeat(Simulation.Choreography.NarrationBeatKind kind)
        {
            switch (kind)
            {
                case Simulation.Choreography.NarrationBeatKind.ShotWindup:
                case Simulation.Choreography.NarrationBeatKind.RimResult:
                case Simulation.Choreography.NarrationBeatKind.ReboundScramble:
                case Simulation.Choreography.NarrationBeatKind.StealJump:
                case Simulation.Choreography.NarrationBeatKind.OobTurnover:
                case Simulation.Choreography.NarrationBeatKind.LooseBallTurnover:
                case Simulation.Choreography.NarrationBeatKind.Violation:
                case Simulation.Choreography.NarrationBeatKind.FreeThrowSetup:
                case Simulation.Choreography.NarrationBeatKind.FreeThrowAttempt:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Convert the choreographer's narration beats into timed radio-call lines for the court
        /// bar. Result-class beats always narrate; action beats are budgeted (≤4 by priority) so
        /// a possession reads as ~4-7 calls. Never touches the play-by-play ticker.
        /// </summary>
        private void EmitNarrationEvents(PossessionResult result, bool offenseIsHome)
        {
            if (_captureSink == null || result.NarrationBeats == null) return;

            var offLineup = offenseIsHome ? _homeLineup : _awayLineup;
            var defLineup = offenseIsHome ? _awayLineup : _homeLineup;

            var ctx = new PlayByPlayContext
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

            // Budget the action beats; keep every result-class beat.
            var actionBeats = result.NarrationBeats.Where(b => !IsResultBeat(b.Kind))
                .OrderBy(b => NarrationPriority(b.Kind)).ThenBy(b => b.T)
                .Take(4).ToList();
            var chosen = result.NarrationBeats.Where(b => IsResultBeat(b.Kind))
                .Concat(actionBeats).OrderBy(b => b.T).ToList();

            var shotEvt = result.Events.FirstOrDefault(e => e.Type == EventType.Shot);
            bool wasBlocked = result.Events.Any(e => e.Type == EventType.Block);

            foreach (var beat in chosen)
            {
                Player actor = ResolveBeatPlayer(beat.ActorIndex, beat.ActorIsDefense, offLineup, defLineup);
                Player target = beat.Kind == Simulation.Choreography.NarrationBeatKind.FreeThrowSetup
                    ? null   // TargetIndex carries the attempt count, not a player
                    : ResolveBeatPlayer(beat.TargetIndex, false, offLineup, defLineup);

                var line = RadioNarrator.Narrate(beat, ctx, actor, target);
                if (line == null || string.IsNullOrEmpty(line.Text)) continue;

                ApplyNarrationSpecials(beat, line, result, ctx, shotEvt, wasBlocked);

                _captureSink.Events.Add(new TimedPlaybackEvent { Offset = beat.T, Narration = line });
            }
        }

        private Player ResolveBeatPlayer(int lineupIndex, bool isDefense, List<string> off, List<string> def)
        {
            if (lineupIndex < 0) return null;
            var lineup = isDefense ? def : off;
            if (lineup == null || lineupIndex >= lineup.Count) return null;
            return GetPlayer(lineup[lineupIndex]);
        }

        /// <summary>Big-moment banners: breakaway slams, rejections, buzzer beaters, daggers, and-ones.</summary>
        private void ApplyNarrationSpecials(Simulation.Choreography.NarrationBeat beat, NarrationLine line,
            PossessionResult result, PlayByPlayContext ctx, PossessionEvent shotEvt, bool wasBlocked)
        {
            if (beat.Kind != Simulation.Choreography.NarrationBeatKind.RimResult) return;

            // Blocked shots: the rim-result call becomes the rejection.
            if (wasBlocked)
            {
                line.Text = ctx.IsClutchTime ? "SWATTED AWAY! Huge stop!" : "Rejected at the rim!";
                if (ctx.IsClutchTime || (shotEvt?.ContestLevel ?? 0f) > 0.8f)
                {
                    line.Style = NarrationStyle.Special;
                    line.Badge = "REJECTED!";
                }
                else line.Style = NarrationStyle.Excited;
                return;
            }

            if (!beat.Made) return;

            int pts = beat.PointsScored > 0 ? beat.PointsScored : (beat.IsThree ? 3 : 2);
            int homeAfter = ctx.HomeScore + (ctx.HomeOnOffense ? pts : 0);
            int awayAfter = ctx.AwayScore + (ctx.HomeOnOffense ? 0 : pts);
            int offenseMargin = ctx.HomeOnOffense ? homeAfter - awayAfter : awayAfter - homeAfter;

            if (beat.ShotType == Simulation.ShotType.Dunk && (shotEvt?.IsFastBreak ?? false))
            {
                line.Style = NarrationStyle.Special; line.Badge = "SLAM!";
            }
            else if (result.EndGameClock <= 0.1f && _gameClock <= 24f)
            {
                line.Style = NarrationStyle.Special; line.Badge = "BUZZER BEATER!";
            }
            else if (beat.IsThree && ctx.IsClutchTime && offenseMargin >= 6)
            {
                line.Style = NarrationStyle.Special; line.Badge = "DAGGER!";
            }
            else if (shotEvt?.IsAndOne ?? false)
            {
                line.Style = NarrationStyle.Special; line.Badge = "AND ONE!";
            }
        }

        /// <summary>The true on-screen moment for a decided event, from the choreographed beats.</summary>
        private static float? BeatTimeFor(PossessionEvent evt,
            List<Simulation.Choreography.NarrationBeat> beats)
        {
            if (beats == null) return null;

            Simulation.Choreography.NarrationBeatKind kind;
            switch (evt.Type)
            {
                case EventType.Shot:
                case EventType.Block:
                    kind = Simulation.Choreography.NarrationBeatKind.RimResult; break;
                case EventType.Rebound:
                    kind = Simulation.Choreography.NarrationBeatKind.ReboundScramble; break;
                case EventType.Steal:
                    kind = Simulation.Choreography.NarrationBeatKind.StealJump; break;
                case EventType.Turnover:
                {
                    return FindBeat(beats, Simulation.Choreography.NarrationBeatKind.OobTurnover)
                        ?? FindBeat(beats, Simulation.Choreography.NarrationBeatKind.LooseBallTurnover)
                        ?? FindBeat(beats, Simulation.Choreography.NarrationBeatKind.Violation);
                }
                case EventType.Foul:
                    kind = Simulation.Choreography.NarrationBeatKind.ShotWindup; break;
                default:
                    return null;
            }
            return FindBeat(beats, kind);
        }

        private static float? FindBeat(List<Simulation.Choreography.NarrationBeat> beats,
            Simulation.Choreography.NarrationBeatKind kind)
        {
            for (int i = 0; i < beats.Count; i++)
                if (beats[i].Kind == kind) return beats[i].T;
            return null;
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
            _liveBallForNext = false; // quarters open with a dead-ball inbound

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

        private PlayByPlayContext CreateGameContext()
        {
            return new PlayByPlayContext
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
                ReplaceInLineup(lineup, team.TeamId, playerId, bench);

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

        /// <summary>In-place slot replacement (preserves PG..C ordering) + change notification.</summary>
        private void ReplaceInLineup(List<string> lineup, string teamId, string playerOut, string playerIn)
        {
            int idx = lineup.IndexOf(playerOut);
            if (idx < 0) return;
            lineup[idx] = playerIn;
            OnLineupChanged?.Invoke(teamId, playerOut, playerIn);
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

        private ShotType ConvertToShotType(Simulation.ShotType? simShotType, CourtPosition shotPos, bool offenseIsHome)
        {
            if (simShotType == null) return Data.ShotType.Jumper;

            // ThreePointer is a DISTANCE class, not a shot style — use THE scoring
            // definition so three-point FX fire exactly when the scoreboard adds 3.
            bool isThree = shotPos.IsThreePointShot(offenseIsHome) ||
                           simShotType == Simulation.ShotType.Heave;

            return simShotType.Value switch
            {
                Simulation.ShotType.Dunk => Data.ShotType.Dunk,
                Simulation.ShotType.Layup => Data.ShotType.Layup,
                Simulation.ShotType.Floater => Data.ShotType.Layup,
                Simulation.ShotType.Hookshot => Data.ShotType.Layup,
                Simulation.ShotType.TipIn => Data.ShotType.Layup,
                _ => isThree ? Data.ShotType.ThreePointer : Data.ShotType.Jumper
            };
        }

        private void FireScoreboardUpdate()
        {
            var update = new ScoreboardUpdate
            {
                HomeScore = _homeScore,
                AwayScore = _awayScore,
                Quarter = _currentQuarter,
                Clock = _gameClock,
                ShotClock = _shotClock,
                HomeHasPossession = _homeHasPossession
            };

            if (_captureSink != null)
            {
                _captureSink.Events.Add(new TimedPlaybackEvent
                {
                    Offset = _captureCurrentOffset,
                    Scoreboard = update
                });
                return;
            }

            OnScoreboardUpdate?.Invoke(update);
        }

        private void AddPlayByPlayEntry(PlayByPlayEntry entry)
        {
            _playByPlay.Add(entry);

            if (_captureSink != null)
            {
                _captureSink.Events.Add(new TimedPlaybackEvent
                {
                    Offset = _captureCurrentOffset,
                    Entry = entry
                });
                return;
            }

            OnPlayByPlay?.Invoke(entry);
        }

        private BoxScore CreateBoxScore()
        {
            // The live box has been accumulating real per-player stats all game —
            // interactive matches now record a full box score (season stats, POTG).
            return LiveBoxScore ?? new BoxScore(_homeTeam.TeamId, _awayTeam.TeamId)
            {
                HomeScore = _homeScore,
                AwayScore = _awayScore
            };
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
