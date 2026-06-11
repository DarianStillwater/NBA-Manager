using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// The core simulation engine that processes possessions.
    /// Each possession takes 10-24 seconds and ends with a shot attempt or turnover.
    /// </summary>
    public class PossessionSimulator
    {
        public static string VERSION = "ROOT_v2";
        private const float SHOT_CLOCK = 24f;
        private const float MIN_POSSESSION_TIME = 6f;   // Minimum seconds before shot
        private const float AVG_POSSESSION_TIME = 15f;  // Average possession length (~95 possessions per team per game)

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
            // Separate RNG for presentation/choreography so visual variety never
            // perturbs the decision RNG stream (outcomes identical with/without choreography).
            _choreoRandom = seed.HasValue ? new System.Random(seed.Value ^ 0x5DEECE6) : new System.Random();
            _tracker = new SpatialTracker();
            _foulSystem = foulSystem ?? new FoulSystem();
            _violationChecker = new ViolationChecker();
            _freeThrowHandler = new FreeThrowHandler();
        }

        /// <summary>
        /// Spatial output level. Headless sims (GameSimulator) should use None;
        /// the interactive match sets Full. NOTE: the legacy spatial path still consumes
        /// decision RNG, so this flag takes effect once the choreographer replaces it.
        /// </summary>
        public SpatialDetailLevel SpatialDetail { get; set; } = SpatialDetailLevel.Full;

        private System.Random _choreoRandom;
        private PossessionScript _script;

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

            // Begin the choreography script — decisions are recorded as they're made,
            // and presentation is generated from the script after all decisions.
            _script = new PossessionScript
            {
                Offense = _offensePlayers,
                Defense = _defensePlayers,
                OffenseAttacksRight = _isOffenseHome,
                StartGameClock = gameClock,
                Duration = possessionDuration,
                Quarter = quarter,
                OffenseStrategy = _offenseStrategy,
                DefenseStrategy = _defenseStrategy,
                InitialBallHandlerIndex = _ballHandlerIndex,
                IsFastBreak = possessionDuration < 8f,
                Events = result.Events
            };

            // Shot clock remaining at the end of the possession (the legacy tick loop
            // decremented this in 0.5s steps; preserved exactly for decision parity —
            // it feeds the shot-clock-pressure modifier in ExecuteShot).
            float possessionClock = SHOT_CLOCK -
                Mathf.Min(Mathf.FloorToInt(possessionDuration / 0.5f), 48) * 0.5f;

            // Execute the possession outcome
            float endGameClock = gameClock - possessionDuration;

            // Clear stored violation for this possession
            _currentViolation = null;

            if (outcomeType == PossessionOutcomeType.Turnover)
            {
                result.Events.Add(CreateTurnoverEvent(endGameClock, quarter));
                result.Outcome = PossessionOutcome.Turnover;
                result.PointsScored = 0;
                _script.Ending = ScriptEnding.Turnover;
            }
            else if (outcomeType == PossessionOutcomeType.Violation)
            {
                // Violation detected - create turnover event with violation details
                var violationEvent = CreateViolationTurnoverEvent(endGameClock, quarter);
                result.Events.Add(violationEvent);
                result.Outcome = PossessionOutcome.Turnover;
                result.PointsScored = 0;
                _script.Ending = ScriptEnding.Violation;
            }
            else // Shot attempt (may include foul check)
            {
                // Select shooter (could be different from ball handler after ball movement)
                int shooterIndex = SelectShooter();

                // Reposition shooter to appropriate zone based on strategy
                RepositionShooterForZone(shooterIndex);

                var shooter = _offensePlayers[shooterIndex];
                var defender = _defensePlayers[shooterIndex];

                _script.ShooterIndex = shooterIndex;
                _script.BallHandlerPassedToShooter = shooterIndex != _ballHandlerIndex;

                // Add pass/assist event if ball handler passed to shooter
                if (shooterIndex != _ballHandlerIndex)
                {
                    result.Events.Add(new PossessionEvent
                    {
                        Type = EventType.Pass,
                        GameClock = endGameClock + 1f,
                        Quarter = quarter,
                        ActorPlayerId = _offensePlayers[_ballHandlerIndex].PlayerId,
                        TargetPlayerId = shooter.PlayerId,
                        Outcome = EventOutcome.Success
                    });

                    _ballHandlerIndex = shooterIndex;
                }

                // Check for steal before shot (~5% chance)
                float stealChance = 0.05f * (defender.Steal / 80f);
                if (_random.NextDouble() < stealChance)
                {
                    result.Events.Add(new PossessionEvent
                    {
                        Type = EventType.Steal,
                        GameClock = endGameClock,
                        Quarter = quarter,
                        ActorPlayerId = defender.PlayerId,
                        TargetPlayerId = shooter.PlayerId,
                        Outcome = EventOutcome.Success
                    });
                    result.Outcome = PossessionOutcome.Turnover;
                    result.PointsScored = 0;
                    _script.Ending = ScriptEnding.Steal;
                }
                else
                {
                    // Check for defensive foul before shot
                    var shotType = ShotCalculator.DetermineShotType(
                        shooter, _offensePositions[shooterIndex],
                        _offensePositions[shooterIndex].DistanceTo(_defensePositions[shooterIndex]), false);

                    _script.ShotType = shotType;

                    float foulChance = _foulSystem.CalculateFoulProbability(defender, shooter, shotType, _gameContext);
                    bool defenderFouls = _random.NextDouble() < foulChance;

                    if (defenderFouls)
                    {
                        var shotResult = ExecuteShotWithFoul(shooter, shooterIndex, defender, shotType, endGameClock, quarter, possessionClock);
                        result.Events.AddRange(shotResult.Events);
                        result.Outcome = shotResult.Outcome;
                        result.PointsScored = shotResult.Points;

                        bool andOneMade = shotResult.Events.Any(e => e.Type == EventType.Shot && e.IsAndOne);
                        _script.Ending = andOneMade ? ScriptEnding.AndOneShot : ScriptEnding.ShootingFoulMissed;
                        _script.FreeThrows = shotResult.Events
                            .FirstOrDefault(e => e.Type == EventType.Foul)?.FreeThrowResult;
                    }
                    else
                    {
                        var shotEvent = ExecuteShot(shooter, shooterIndex, endGameClock, quarter, possessionClock);
                        result.Events.Add(shotEvent);
                        _script.ContestLevel = shotEvent.ContestLevel;

                        if (shotEvent.Type == EventType.Block)
                        {
                            result.Outcome = PossessionOutcome.Block;
                            result.PointsScored = 0;
                            _script.Ending = ScriptEnding.BlockedShot;
                        }
                        else if (shotEvent.Outcome == EventOutcome.Success)
                        {
                            result.Outcome = PossessionOutcome.Score;
                            result.PointsScored = shotEvent.PointsScored;
                            _script.Ending = ScriptEnding.MadeShot;
                        }
                        else
                        {
                            result.Outcome = PossessionOutcome.Miss;
                            result.PointsScored = 0;
                            _script.Ending = ScriptEnding.MissedShot;
                        }

                        // Generate rebound on missed shots
                        if (shotEvent.Outcome == EventOutcome.Fail || shotEvent.Type == EventType.Block)
                        {
                            bool offensiveRebound = _random.NextDouble() < 0.25f; // ~25% OREB rate
                            int rebounderIndex;
                            Player rebounder;

                            if (offensiveRebound)
                            {
                                // Offensive rebound - weight by rebounding skill
                                rebounderIndex = SelectRebounder(_offensePlayers, true);
                                rebounder = _offensePlayers[rebounderIndex];
                            }
                            else
                            {
                                // Defensive rebound
                                rebounderIndex = SelectRebounder(_defensePlayers, false);
                                rebounder = _defensePlayers[rebounderIndex];
                            }

                            _script.RebounderIndex = rebounderIndex;
                            _script.RebounderIsDefense = !offensiveRebound;

                            result.Events.Add(new PossessionEvent
                            {
                                Type = EventType.Rebound,
                                GameClock = endGameClock,
                                Quarter = quarter,
                                ActorPlayerId = rebounder.PlayerId,
                                Outcome = EventOutcome.Success,
                                IsOffensiveRebound = offensiveRebound
                            });
                        }
                    }
                }

                // Final decided geometry for the choreographer
                _script.ShotPosition = _offensePositions[shooterIndex];
            }

            // Snapshot the decided end-of-possession spots (formation targets for choreography)
            for (int i = 0; i < 5; i++)
            {
                _script.OffenseSpots[i] = _offensePositions[i];
                _script.DefenseSpots[i] = _defensePositions[i];
            }
            _script.PointsScored = result.PointsScored;

            // Choreograph the decided possession into a believable spatial timeline.
            // Headless sims (SpatialDetail.None) skip this entirely — zero cost.
            if (SpatialDetail == SpatialDetailLevel.Full)
            {
                _choreographer ??= new PossessionChoreographer(_choreoRandom);
                result.SpatialStates = _choreographer.Choreograph(_script);
            }

            result.EndGameClock = endGameClock;
            return result;
        }

        private PossessionChoreographer _choreographer;

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
        /// Repositions the shooter to an appropriate zone based on offensive strategy.
        /// Uses ThreePointFrequency, MidRangeFrequency, RimAttackFrequency as weights.
        /// </summary>
        private void RepositionShooterForZone(int shooterIndex)
        {
            float xMult = _isOffenseHome ? 1f : -1f;

            float threeWeight = Mathf.Max(_offenseStrategy.ThreePointFrequency, 1f);
            float midWeight = Mathf.Max(_offenseStrategy.MidRangeFrequency, 1f);
            float rimWeight = Mathf.Max(_offenseStrategy.RimAttackFrequency, 1f);
            float total = threeWeight + midWeight + rimWeight;

            float roll = (float)_random.NextDouble() * total;

            CourtPosition newPos;
            if (roll < threeWeight)
            {
                // Three-point zone — pick a spot on the arc
                float[] yOptions = { -22f, -16f, 0f, 16f, 22f };
                float y = yOptions[_random.Next(yOptions.Length)];
                float x = Mathf.Abs(y) > 20f ? 42f : (28f + (float)_random.NextDouble() * 4f);
                newPos = new CourtPosition(x * xMult, y);
            }
            else if (roll < threeWeight + midWeight)
            {
                // Mid-range zone
                float angle = (float)(_random.NextDouble() * Mathf.PI) - Mathf.PI / 2f;
                float dist = 12f + (float)_random.NextDouble() * 8f;
                float basketX = 42f * xMult;
                newPos = new CourtPosition(basketX - Mathf.Cos(angle) * dist * xMult, Mathf.Sin(angle) * dist);
            }
            else
            {
                // Rim zone
                float x = 38f + (float)_random.NextDouble() * 4f;
                float y = (float)(_random.NextDouble() * 10f - 5f);
                newPos = new CourtPosition(x * xMult, y);
            }

            _offensePositions[shooterIndex] = newPos;

            // Defender tracks the shooter (with some lag — not perfectly on them)
            float defTrack = 0.6f + (float)_random.NextDouble() * 0.3f; // 60-90% tracking
            _defensePositions[shooterIndex] = _defensePositions[shooterIndex].MoveTowards(newPos,
                _defensePositions[shooterIndex].DistanceTo(newPos) * defTrack);
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

            // Defense - man-to-man (offset toward basket to stay between offense and rim)
            for (int i = 0; i < 5; i++)
            {
                float defX = Mathf.Lerp(_offensePositions[i].X, 42f * xMult, 0.4f);
                float defY = _offensePositions[i].Y * 0.7f;
                _defensePositions[i] = new CourtPosition(defX, defY);
            }
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

            // Check for block attempt (close shots with good defender)
            bool blocked = false;
            if (defenderDistance < 3f)
            {
                float blockChance = (defender.Block / 100f) * (1f - defenderDistance / 4f) * 0.07f;

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

            // A blocked shot is always a miss
            if (blocked) made = false;

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
        /// Selects a rebounder weighted by rebounding skill.
        /// </summary>
        private int SelectRebounder(Player[] players, bool offensive)
        {
            float totalWeight = 0f;
            float[] weights = new float[5];
            for (int i = 0; i < 5; i++)
            {
                // Use DefensiveRebound as base; offensive rebounds also factor strength
                weights[i] = offensive
                    ? Mathf.Max(players[i].DefensiveRebound * 0.7f + players[i].Strength * 0.3f, 20f)
                    : Mathf.Max(players[i].DefensiveRebound, 20f);
                totalWeight += weights[i];
            }

            float roll = (float)_random.NextDouble() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < 5; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative) return i;
            }
            return 4;
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

                // Store FT result on the foul event so GameSimulator doesn't recalculate
                foulEvent.FreeThrowResult = ftResult;

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
