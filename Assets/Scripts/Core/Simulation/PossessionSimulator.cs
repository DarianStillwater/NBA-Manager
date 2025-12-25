using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// The core simulation engine that processes possessions.
    /// Each possession takes 10-24 seconds and ends with a shot attempt or turnover.
    /// </summary>
    public class PossessionSimulator
    {
        private const float SHOT_CLOCK = 24f;
        private const float MIN_POSSESSION_TIME = 6f;   // Minimum seconds before shot
        private const float AVG_POSSESSION_TIME = 12f;  // Average possession length (~100 possessions per team)

        private System.Random _random;
        private SpatialTracker _tracker;

        // Rules systems
        private FoulSystem _foulSystem;
        private ViolationChecker _violationChecker;
        private FreeThrowHandler _freeThrowHandler;

        // Current game state
        private Player[] _offensePlayers = new Player[5];
        private Player[] _defensePlayers = new Player[5];
        private CourtPosition[] _offensePositions = new CourtPosition[5];
        private CourtPosition[] _defensePositions = new CourtPosition[5];
        private int _ballHandlerIndex;
        private TeamStrategy _offenseStrategy;
        private TeamStrategy _defenseStrategy;
        private bool _isOffenseHome;
        private string _offenseTeamId;
        private string _defenseTeamId;
        private GameContext _gameContext;
        private ViolationEventDetail _currentViolation;  // Stores violation from DeterminePossessionOutcome

        public PossessionSimulator(int? seed = null, FoulSystem foulSystem = null)
        {
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            _tracker = new SpatialTracker();
            _foulSystem = foulSystem ?? new FoulSystem();
            _violationChecker = new ViolationChecker();
            _freeThrowHandler = new FreeThrowHandler();
        }

        /// <summary>
        /// Access to foul system for external tracking.
        /// </summary>
        public FoulSystem FoulSystem => _foulSystem;

        /// <summary>
        /// Access to free throw handler.
        /// </summary>
        public FreeThrowHandler FreeThrowHandler => _freeThrowHandler;

        /// <summary>
        /// Simulates a single possession and returns the result with full spatial data.
        /// </summary>
        public PossessionResult SimulatePossession(
            Player[] offensePlayers,
            Player[] defensePlayers,
            TeamStrategy offenseStrategy,
            TeamStrategy defenseStrategy,
            float gameClock,
            int quarter,
            bool isOffenseHome,
            string offenseTeamId = null,
            string defenseTeamId = null,
            int scoreDifferential = 0)
        {
            _offensePlayers = offensePlayers;
            _defensePlayers = defensePlayers;
            _offenseStrategy = offenseStrategy;
            _defenseStrategy = defenseStrategy;
            _isOffenseHome = isOffenseHome;
            _offenseTeamId = offenseTeamId;
            _defenseTeamId = defenseTeamId;

            // Create game context for foul/FT calculations
            _gameContext = new GameContext
            {
                Quarter = quarter,
                GameClock = gameClock,
                ShotClock = SHOT_CLOCK,
                ScoreDifferential = scoreDifferential,
                IsClutchTime = quarter >= 4 && gameClock <= 300f && Math.Abs(scoreDifferential) <= 5,
                TimeoutJustCalled = false,
                OpponentRunPoints = 0,
                OpponentJustScored = false
            };

            var result = new PossessionResult
            {
                Events = new List<PossessionEvent>(),
                SpatialStates = new List<SpatialState>(),
                StartGameClock = gameClock,
                Quarter = quarter
            };

            // Initialize positions
            InitializePositions(isOffenseHome);
            
            // Determine possession duration (8-16 seconds, influenced by pace)
            float paceMultiplier = Mathf.Lerp(1.15f, 0.85f, _offenseStrategy.Pace / 100f);
            float possessionDuration = AVG_POSSESSION_TIME * paceMultiplier;
            possessionDuration += (float)(_random.NextDouble() * 4.0 - 2.0); // +/- 2 seconds variance
            possessionDuration = Mathf.Clamp(possessionDuration, MIN_POSSESSION_TIME, SHOT_CLOCK - 2f);
            
            // Don't exceed remaining game clock
            possessionDuration = Mathf.Min(possessionDuration, gameClock);

            // Select primary ball handler (weighted by position and skill)
            _ballHandlerIndex = SelectBallHandler();
            
            // Determine possession outcome FIRST (shot, turnover, or foul)
            PossessionOutcomeType outcomeType = DeterminePossessionOutcome();

            // Record spatial states during possession (every 0.5 seconds)
            float currentTime = gameClock;
            float possessionClock = SHOT_CLOCK;
            int spatialTicks = Mathf.FloorToInt(possessionDuration / 0.5f);
            
            for (int i = 0; i < spatialTicks && i < 48; i++) // Max 48 ticks (24 seconds)
            {
                var state = CreateSpatialState(currentTime, quarter, possessionClock);
                result.SpatialStates.Add(state);
                currentTime -= 0.5f;
                possessionClock -= 0.5f;
                SimulatePlayerMovement();
            }

            // Execute the possession outcome
            float endGameClock = gameClock - possessionDuration;

            // Clear stored violation for this possession
            _currentViolation = null;

            if (outcomeType == PossessionOutcomeType.Turnover)
            {
                result.Events.Add(CreateTurnoverEvent(endGameClock, quarter));
                result.Outcome = PossessionOutcome.Turnover;
                result.PointsScored = 0;
            }
            else if (outcomeType == PossessionOutcomeType.Violation)
            {
                // Violation detected - create turnover event with violation details
                var violationEvent = CreateViolationTurnoverEvent(endGameClock, quarter);
                result.Events.Add(violationEvent);
                result.Outcome = PossessionOutcome.Turnover;
                result.PointsScored = 0;
            }
            else // Shot attempt (may include foul check)
            {
                // Select shooter (could be different from ball handler after ball movement)
                int shooterIndex = SelectShooter();
                var shooter = _offensePlayers[shooterIndex];
                var defender = _defensePlayers[shooterIndex];

                // Check for defensive foul before shot
                var shotType = ShotCalculator.DetermineShotType(
                    shooter, _offensePositions[shooterIndex],
                    _offensePositions[shooterIndex].DistanceTo(_defensePositions[shooterIndex]), false);

                float foulChance = _foulSystem.CalculateFoulProbability(defender, shooter, shotType, _gameContext);
                bool defenderFouls = _random.NextDouble() < foulChance;

                if (defenderFouls)
                {
                    // Foul occurred - determine outcome
                    var shotResult = ExecuteShotWithFoul(shooter, shooterIndex, defender, shotType, endGameClock, quarter, possessionClock);
                    result.Events.AddRange(shotResult.Events);
                    result.Outcome = shotResult.Outcome;
                    result.PointsScored = shotResult.Points;
                }
                else
                {
                    // Normal shot attempt
                    var shotEvent = ExecuteShot(shooter, shooterIndex, endGameClock, quarter, possessionClock);
                    result.Events.Add(shotEvent);

                    if (shotEvent.Type == EventType.Block)
                    {
                        result.Outcome = PossessionOutcome.Block;
                        result.PointsScored = 0;
                    }
                    else if (shotEvent.Outcome == EventOutcome.Success)
                    {
                        result.Outcome = PossessionOutcome.Score;
                        result.PointsScored = shotEvent.PointsScored;
                    }
                    else
                    {
                        result.Outcome = PossessionOutcome.Miss;
                        result.PointsScored = 0;
                    }
                }
            }

            result.EndGameClock = endGameClock;
            return result;
        }

        /// <summary>
        /// Determines the outcome type of the possession (shot, turnover, violation, or foul).
        /// </summary>
        private PossessionOutcomeType DeterminePossessionOutcome()
        {
            Player handler = _offensePlayers[_ballHandlerIndex];

            // Check for violations first (traveling, backcourt, 3-second, etc.)
            var violation = _violationChecker.CheckForViolation(
                handler,
                _offensePlayers.ToList(),
                null,  // No shot type yet
                SHOT_CLOCK,
                true   // Assume crossed halfcourt for half-court possessions
            );

            if (violation != null)
            {
                // Store violation for later use in CreateTurnoverEvent
                _currentViolation = violation;
                return PossessionOutcomeType.Violation;
            }

            // Base turnover rate is ~13% in NBA
            float turnoverChance = 0.13f;

            // Adjust for ball handler skill
            turnoverChance -= (handler.BallHandling - 50) / 500f;
            turnoverChance -= (handler.BasketballIQ - 50) / 500f;

            // Adjust for defense
            turnoverChance += (_defenseStrategy.DefensiveAggression - 50) / 300f;

            // Adjust for team chemistry (good chemistry = fewer turnovers)
            float chemistryMod = GetLineupChemistryModifier();
            turnoverChance -= chemistryMod * 0.05f; // ±0.6% based on chemistry

            // Adjust for morale (low morale = more careless mistakes)
            float avgMorale = GetAverageMorale(_offensePlayers);
            float moraleMod = (avgMorale - 50f) / 50f; // -1 to +1
            turnoverChance -= moraleMod * 0.02f; // ±2% based on morale

            // TENDENCY INTEGRATION: Ball movement and risk tolerance affect turnovers
            if (handler.Tendencies != null)
            {
                // Ball stoppers hold the ball too long, higher turnover risk
                // Ball movement: -100 (ball stopper) to +100 (willing passer)
                float ballMovementMod = handler.Tendencies.BallMovement / 500f; // ±0.2
                turnoverChance -= ballMovementMod * 0.03f; // Ball stoppers +0.6%, willing passers -0.6%

                // Risk takers have more turnovers but also more big plays
                // Risk tolerance: -100 (risk averse) to +100 (risk taker)
                float riskMod = handler.Tendencies.RiskTolerance / 500f;
                turnoverChance += riskMod * 0.02f; // Risk takers +0.4%, risk averse -0.4%

                // Effort consistency affects focus and ball security
                float effortMod = handler.Tendencies.GetEffortModifier(handler.Energy, 1);
                turnoverChance -= (effortMod - 1f) * 0.1f; // High effort = fewer turnovers
            }

            turnoverChance = Mathf.Clamp(turnoverChance, 0.05f, 0.25f);

            return _random.NextDouble() < turnoverChance
                ? PossessionOutcomeType.Turnover
                : PossessionOutcomeType.Shot;
        }

        /// <summary>
        /// Gets lineup chemistry modifier from MoraleChemistryManager.
        /// </summary>
        private float GetLineupChemistryModifier()
        {
            var manager = Manager.MoraleChemistryManager.Instance;
            if (manager == null) return 0f;

            var playerIds = _offensePlayers.Select(p => p.PlayerId).ToList();
            return manager.GetLineupChemistryModifier(playerIds);
        }

        /// <summary>
        /// Gets average morale of players.
        /// </summary>
        private float GetAverageMorale(Player[] players)
        {
            float total = 0f;
            foreach (var player in players)
            {
                total += player.Morale;
            }
            return total / players.Length;
        }

        private enum PossessionOutcomeType { Shot, Turnover, Violation, Foul }

        /// <summary>
        /// Selects the ball handler based on position and playmaking skill.
        /// </summary>
        private int SelectBallHandler()
        {
            // Weight towards guards
            float[] weights = new float[5];
            weights[0] = 40f + _offensePlayers[0].BallHandling * 0.3f;  // PG
            weights[1] = 25f + _offensePlayers[1].BallHandling * 0.2f;  // SG
            weights[2] = 15f + _offensePlayers[2].BallHandling * 0.15f; // SF
            weights[3] = 10f + _offensePlayers[3].BallHandling * 0.1f;  // PF
            weights[4] = 5f + _offensePlayers[4].BallHandling * 0.05f;  // C

            float total = weights.Sum();
            float roll = (float)_random.NextDouble() * total;
            float cumulative = 0;
            
            for (int i = 0; i < 5; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative) return i;
            }
            return 0;
        }

        /// <summary>
        /// Selects the shooter based on shooting skill, strategy, and tendencies.
        /// </summary>
        private int SelectShooter()
        {
            float[] weights = new float[5];

            for (int i = 0; i < 5; i++)
            {
                Player p = _offensePlayers[i];

                // Base weight on shooting ability
                float avgShooting = (p.Shot_Three + p.Shot_MidRange + p.Finishing_Rim) / 3f;
                weights[i] = avgShooting;

                // Strategy influence
                if (_offenseStrategy.ThreePointFrequency > 60)
                    weights[i] += p.Shot_Three * 0.3f;
                if (_offenseStrategy.PostUpFrequency > 50 && (i == 3 || i == 4))
                    weights[i] += p.Finishing_PostMoves * 0.3f;

                // TENDENCY INTEGRATION: Shot selection tendencies affect usage
                if (p.Tendencies != null)
                {
                    // Quick trigger players (-100 ShotSelection) take more shots
                    // Patient shooters (+100 ShotSelection) take fewer but better shots
                    float shotSelectionMod = -p.Tendencies.ShotSelection / 200f; // +0.5 for quick trigger, -0.5 for patient
                    weights[i] *= (1f + shotSelectionMod * 0.3f);

                    // Ball stoppers take more shots (they don't pass)
                    // Ball movement: -100 to +100
                    float ballMovementMod = -p.Tendencies.BallMovement / 300f;
                    weights[i] *= (1f + ballMovementMod * 0.2f);

                    // Shot discipline affects whether they stay within their role
                    // Low shot discipline = takes shots outside their role
                    if (p.Tendencies.ShotDiscipline < 40)
                    {
                        weights[i] *= 1.15f; // 15% more likely to take shots
                    }
                }
            }

            float total = weights.Sum();
            float roll = (float)_random.NextDouble() * total;
            float cumulative = 0;

            for (int i = 0; i < 5; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative) return i;
            }
            return 0;
        }

        /// <summary>
        /// Sets initial positions for a half-court possession.
        /// </summary>
        private void InitializePositions(bool attackingRight)
        {
            float xMult = attackingRight ? 1f : -1f;

            // Offense - spread formation
            _offensePositions[0] = new CourtPosition(25f * xMult, 0);      // PG top
            _offensePositions[1] = new CourtPosition(28f * xMult, -16f);   // SG left wing
            _offensePositions[2] = new CourtPosition(28f * xMult, 16f);    // SF right wing
            _offensePositions[3] = new CourtPosition(36f * xMult, -8f);    // PF left block
            _offensePositions[4] = new CourtPosition(36f * xMult, 8f);     // C right block

            // Defense - man-to-man
            for (int i = 0; i < 5; i++)
            {
                float defX = Mathf.Lerp(_offensePositions[i].X, 42f * xMult, 0.25f);
                float defY = _offensePositions[i].Y * 0.9f;
                _defensePositions[i] = new CourtPosition(defX, defY);
            }
        }

        /// <summary>
        /// Simulates player movement during the possession.
        /// </summary>
        private void SimulatePlayerMovement()
        {
            // Simple movement - players shift positions slightly
            for (int i = 0; i < 5; i++)
            {
                float dx = (float)(_random.NextDouble() * 2.0 - 1.0);
                float dy = (float)(_random.NextDouble() * 2.0 - 1.0);
                _offensePositions[i] = new CourtPosition(
                    _offensePositions[i].X + dx,
                    _offensePositions[i].Y + dy
                );
                
                // Defenders track their man
                _defensePositions[i] = _defensePositions[i].MoveTowards(_offensePositions[i], 1.5f);
            }
        }

        /// <summary>
        /// Creates a spatial state snapshot.
        /// </summary>
        private SpatialState CreateSpatialState(float gameClock, int quarter, float possessionClock)
        {
            var state = new SpatialState(gameClock, quarter, possessionClock);

            for (int i = 0; i < 5; i++)
            {
                state.Players[i] = new PlayerSnapshot(
                    _offensePlayers[i].PlayerId,
                    _offensePositions[i].X,
                    _offensePositions[i].Y
                )
                {
                    HasBall = (i == _ballHandlerIndex),
                    CurrentAction = i == _ballHandlerIndex ? PlayerAction.Dribbling : PlayerAction.Idle
                };

                state.Players[i + 5] = new PlayerSnapshot(
                    _defensePlayers[i].PlayerId,
                    _defensePositions[i].X,
                    _defensePositions[i].Y
                )
                {
                    CurrentAction = PlayerAction.Defending
                };
            }

            state.Ball = new BallState(
                _offensePositions[_ballHandlerIndex].X,
                _offensePositions[_ballHandlerIndex].Y,
                4f
            )
            {
                Status = BallStatus.Held,
                HeldByPlayerId = _offensePlayers[_ballHandlerIndex].PlayerId
            };

            return state;
        }

        /// <summary>
        /// Executes a shot attempt.
        /// </summary>
        private PossessionEvent ExecuteShot(Player shooter, int shooterIndex, float gameClock, int quarter, float possessionClock)
        {
            var position = _offensePositions[shooterIndex];
            var defender = _defensePlayers[shooterIndex];
            float defenderDistance = position.DistanceTo(_defensePositions[shooterIndex]);

            // Determine shot type and zone
            var shotType = ShotCalculator.DetermineShotType(shooter, position, defenderDistance, false);

            // Calculate shot probability
            float shotProb = ShotCalculator.CalculateShotProbability(
                shooter, position, defender, defenderDistance, shotType,
                isFastBreak: false, secondsRemaining: possessionClock, isHomeTeam: _isOffenseHome);

            // TENDENCY INTEGRATION: Apply tendency-based modifiers
            if (shooter.Tendencies != null)
            {
                // Clutch behavior in late game situations
                bool isClutch = (quarter >= 4 && gameClock <= 120f) || // Last 2 min of 4th+ quarter
                                (quarter >= 4 && Mathf.Abs(0) <= 5); // Would need score margin, simplified
                float clutchMod = shooter.Tendencies.GetClutchModifier(isClutch);
                shotProb *= clutchMod;

                // Effort consistency affects shot quality
                float effortMod = shooter.Tendencies.GetEffortModifier(shooter.Energy, quarter);
                shotProb *= effortMod;

                // Quick trigger players take contested shots more often
                // This can hurt their percentages but also catches defenses off guard
                if (shooter.Tendencies.ShotSelection < -30 && defenderDistance < 4f)
                {
                    // Taking contested shot when patience would help - slight penalty
                    shotProb *= 0.95f;
                }
                else if (shooter.Tendencies.ShotSelection > 30 && defenderDistance > 5f)
                {
                    // Patient shooter with good look - slight bonus
                    shotProb *= 1.03f;
                }
            }

            bool made = _random.NextDouble() < shotProb;

            // Check for block (only on close shots with good defender)
            bool blocked = false;
            if (!made && defenderDistance < 4f)
            {
                float blockChance = (defender.Block / 100f) * (1f - defenderDistance / 6f) * 0.15f;

                // TENDENCY INTEGRATION: Defensive tendencies affect block attempts
                if (defender.Tendencies != null)
                {
                    // Aggressive defenders (+100 DefensiveGambling) go for more blocks
                    // Conservative defenders (-100) stay grounded
                    float gamblingMod = 1f + (defender.Tendencies.DefensiveGambling / 500f);
                    blockChance *= gamblingMod;

                    // Effort affects defensive contests
                    float effortMod = defender.Tendencies.GetEffortModifier(defender.Energy, quarter);
                    blockChance *= effortMod;

                    // Closeout control affects ability to contest without fouling
                    float closeoutBonus = (defender.Tendencies.CloseoutControl - 50) / 300f;
                    blockChance *= (1f + closeoutBonus);
                }

                blocked = _random.NextDouble() < blockChance;
            }

            // IMPORTANT: Use _isOffenseHome to determine which basket to calculate zones from
            // Home attacks basket at X=42, Away attacks basket at X=-42
            var zone = position.GetZone(_isOffenseHome);
            int points = made ? (zone == CourtZone.ThreePoint ? 3 : 2) : 0;

            // Record shot for shot charts
            _tracker.RecordShot(shooter.PlayerId, position, made, points);

            if (blocked)
            {
                return new PossessionEvent
                {
                    Type = EventType.Block,
                    GameClock = gameClock,
                    Quarter = quarter,
                    PossessionClock = possessionClock,
                    ActorPlayerId = shooter.PlayerId,
                    ActorPosition = position,
                    DefenderPlayerId = defender.PlayerId,
                    Outcome = EventOutcome.Fail,
                    ShotType = shotType,
                    ContestLevel = 1f - (defenderDistance / 6f)
                };
            }

            return new PossessionEvent
            {
                Type = EventType.Shot,
                GameClock = gameClock,
                Quarter = quarter,
                PossessionClock = possessionClock,
                ActorPlayerId = shooter.PlayerId,
                ActorPosition = position,
                DefenderPlayerId = defender.PlayerId,
                Outcome = made ? EventOutcome.Success : EventOutcome.Fail,
                PointsScored = points,
                ShotType = shotType,
                ContestLevel = 1f - Mathf.Clamp01(defenderDistance / 6f)
            };
        }

        /// <summary>
        /// Creates a turnover event.
        /// </summary>
        private PossessionEvent CreateTurnoverEvent(float gameClock, int quarter)
        {
            // Determine type of turnover
            string[] turnoverTypes = { "Bad pass", "Lost handle", "Offensive foul", "Traveled", "Out of bounds" };
            string description = turnoverTypes[_random.Next(turnoverTypes.Length)];

            return new PossessionEvent
            {
                Type = EventType.Turnover,
                GameClock = gameClock,
                Quarter = quarter,
                ActorPlayerId = _offensePlayers[_ballHandlerIndex].PlayerId,
                ActorPosition = _offensePositions[_ballHandlerIndex],
                Description = description,
                Outcome = EventOutcome.Fail
            };
        }

        /// <summary>
        /// Creates a turnover event for a violation.
        /// </summary>
        private PossessionEvent CreateViolationTurnoverEvent(float gameClock, int quarter)
        {
            var handler = _offensePlayers[_ballHandlerIndex];

            return new PossessionEvent
            {
                Type = EventType.Turnover,
                GameClock = gameClock,
                Quarter = quarter,
                ActorPlayerId = handler.PlayerId,
                ActorPosition = _offensePositions[_ballHandlerIndex],
                Description = _currentViolation?.Description ?? "Violation",
                ViolationDetail = _currentViolation,
                Outcome = EventOutcome.Fail
            };
        }

        /// <summary>
        /// Result of a shot attempt with a foul.
        /// </summary>
        private class ShotWithFoulResult
        {
            public List<PossessionEvent> Events = new List<PossessionEvent>();
            public PossessionOutcome Outcome;
            public int Points;
        }

        /// <summary>
        /// Executes a shot attempt where a foul occurred.
        /// Handles shooting fouls, and-ones, and free throw results.
        /// </summary>
        private ShotWithFoulResult ExecuteShotWithFoul(
            Player shooter,
            int shooterIndex,
            Player defender,
            ShotType shotType,
            float gameClock,
            int quarter,
            float possessionClock)
        {
            var result = new ShotWithFoulResult();
            var position = _offensePositions[shooterIndex];
            var zone = position.GetZone(_isOffenseHome);
            bool isThreePointer = zone == CourtZone.ThreePoint;

            // First, check if the shot was attempted and potentially made
            float defenderDistance = position.DistanceTo(_defensePositions[shooterIndex]);
            float shotProb = ShotCalculator.CalculateShotProbability(
                shooter, position, defender, defenderDistance, shotType,
                isFastBreak: false, secondsRemaining: possessionClock, isHomeTeam: _isOffenseHome);

            // Fouled shooters get a slightly lower percentage (disrupted)
            shotProb *= 0.85f;
            bool shotMade = _random.NextDouble() < shotProb;

            // Create foul event
            var foulEvent = _foulSystem.CreateFoulEvent(
                shooter,
                defender,
                _defenseTeamId,
                FoulType.Shooting,  // Shooting foul
                isShootingFoul: true,
                shotWasMade: shotMade,
                isBehindArc: isThreePointer,
                shotType: shotType,
                gameClock: gameClock,
                quarter: quarter,
                context: _gameContext);

            result.Events.Add(foulEvent);

            int fieldGoalPoints = 0;
            int freeThrowPoints = 0;

            // If shot was made, count the points
            if (shotMade)
            {
                fieldGoalPoints = isThreePointer ? 3 : 2;

                // Add shot event for and-one
                result.Events.Add(new PossessionEvent
                {
                    Type = EventType.Shot,
                    GameClock = gameClock,
                    Quarter = quarter,
                    ActorPlayerId = shooter.PlayerId,
                    ActorPosition = position,
                    DefenderPlayerId = defender.PlayerId,
                    Outcome = EventOutcome.Success,
                    PointsScored = fieldGoalPoints,
                    ShotType = shotType,
                    IsAndOne = true
                });
            }

            // Process free throws
            int freeThrows = foulEvent.FoulDetail?.FreeThrowsAwarded ?? 0;
            if (freeThrows > 0)
            {
                var ftResult = _freeThrowHandler.CalculateFreeThrows(shooter, freeThrows, _gameContext);
                freeThrowPoints = ftResult.Made;

                // Add free throw event
                result.Events.Add(_freeThrowHandler.CreateFreeThrowEvent(shooter, ftResult, gameClock, quarter));
            }

            result.Points = fieldGoalPoints + freeThrowPoints;
            result.Outcome = result.Points > 0 ? PossessionOutcome.Score : PossessionOutcome.Foul;

            return result;
        }
    }

    public enum PossessionOutcome
    {
        Score,
        Miss,
        Turnover,
        Block,
        Foul,
        EndOfQuarter
    }

    /// <summary>
    /// Complete result of a simulated possession.
    /// </summary>
    public class PossessionResult
    {
        public List<PossessionEvent> Events;
        public List<SpatialState> SpatialStates;
        public PossessionOutcome Outcome;
        public int PointsScored;
        public float StartGameClock;
        public float EndGameClock;
        public int Quarter;

        public float Duration => StartGameClock - EndGameClock;

        public string GetPlayByPlay(Func<string, string> getPlayerName)
        {
            return string.Join("\n", Events.Select(e => e.ToPlayByPlay(getPlayerName)));
        }
    }
}
