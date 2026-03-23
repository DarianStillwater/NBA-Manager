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
        private PlayType _currentPlayType;

        private enum PlayType { Motion, PickAndRoll, Isolation, PostUp }
        private bool _isFastBreak;

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
            int scoreDifferential = 0,
            PossessionOutcome? previousOutcome = null)
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
            
            // Check for fast break opportunity based on transition settings
            bool isFastBreak = false;
            bool transitionOpportunity = previousOutcome == PossessionOutcome.Turnover ||
                                          previousOutcome == PossessionOutcome.Miss ||
                                          previousOutcome == PossessionOutcome.Block;
            if (transitionOpportunity)
            {
                float fastBreakChance = _offenseStrategy.TransitionOffense switch
                {
                    TransitionPreference.NoPush => 0.02f,
                    TransitionPreference.OpportunisticPush => 0.10f,
                    TransitionPreference.PushWhenPossible => 0.18f,
                    TransitionPreference.AggressivePush => 0.25f,
                    TransitionPreference.AlwaysPush => 0.35f,
                    _ => 0.10f
                };

                // Opponent transition defense reduces fast break chance
                fastBreakChance *= _defenseStrategy.TransitionDefense switch
                {
                    TransitionDefensePreference.SprintBack => 0.6f,
                    TransitionDefensePreference.BalancedTransition => 0.8f,
                    TransitionDefensePreference.GambleForSteals => 1.1f,
                    TransitionDefensePreference.MatchupTransition => 0.9f,
                    _ => 0.8f
                };

                isFastBreak = _random.NextDouble() < fastBreakChance;
            }
            _isFastBreak = isFastBreak;

            // Determine possession duration (8-16 seconds, influenced by pace)
            float paceMultiplier = Mathf.Lerp(1.15f, 0.85f, _offenseStrategy.Pace / 100f);
            float possessionDuration;
            if (isFastBreak)
            {
                possessionDuration = 4f + (float)(_random.NextDouble() * 4.0); // 4-8 seconds
            }
            else
            {
                possessionDuration = AVG_POSSESSION_TIME * paceMultiplier;
                possessionDuration += (float)(_random.NextDouble() * 4.0 - 2.0); // +/- 2 seconds variance
            }
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
            else if (outcomeType == PossessionOutcomeType.Steal)
            {
                result.Events.Add(CreateStealEvent(endGameClock, quarter));
                result.Outcome = PossessionOutcome.Turnover;
                result.PointsScored = 0;
            }
            else // Shot attempt (may include foul check)
            {
                // Determine play type based on offensive strategy
                _currentPlayType = DeterminePlayType();

                // End-of-game strategy overrides
                if (_gameContext.IsClutchTime && _offenseStrategy.CloseGameStrategy != null)
                {
                    var clutch = _offenseStrategy.CloseGameStrategy;

                    // Override play type in clutch
                    if (clutch.LateGamePlay == LateGamePlayType.StarIsolation)
                        _currentPlayType = PlayType.Isolation;
                    else if (clutch.LateGamePlay == LateGamePlayType.PickAndRoll)
                        _currentPlayType = PlayType.PickAndRoll;
                    else if (clutch.LateGamePlay == LateGamePlayType.PostUp)
                        _currentPlayType = PlayType.PostUp;

                    // Run clock when ahead — burn more of the shot clock
                    if (clutch.RunClockWhenAhead && _gameContext.ScoreDifferential >= clutch.SecondsAheadToSlowDown)
                    {
                        float clutchDuration = Mathf.Max(possessionDuration, SHOT_CLOCK - clutch.TargetLastShotClock);
                        clutchDuration = Mathf.Min(clutchDuration, gameClock);
                        endGameClock = gameClock - clutchDuration;
                    }
                }

                // Select shooter (influenced by play type)
                int shooterIndex = SelectShooter();

                // In clutch with StarIsolation, force the go-to player if specified
                if (_gameContext.IsClutchTime && _offenseStrategy.CloseGameStrategy != null &&
                    _offenseStrategy.CloseGameStrategy.LateGamePlay == LateGamePlayType.StarIsolation &&
                    !string.IsNullOrEmpty(_offenseStrategy.CloseGameStrategy.GoToPlayerId))
                {
                    string starId = _offenseStrategy.CloseGameStrategy.GoToPlayerId;
                    for (int i = 0; i < 5; i++)
                    {
                        if (_offensePlayers[i].PlayerId == starId)
                        {
                            shooterIndex = i;
                            break;
                        }
                    }
                }

                var shooter = _offensePlayers[shooterIndex];
                var defender = _defensePlayers[shooterIndex];

                // Generate assist pass event if shooter differs from ball handler
                if (shooterIndex != _ballHandlerIndex)
                {
                    float assistChance = 0.60f; // NBA average ~55-60% of made FGs are assisted

                    // Play type strongly affects assist probability
                    assistChance += _currentPlayType switch
                    {
                        PlayType.Motion => 0.15f,       // Motion = lots of passing
                        PlayType.PickAndRoll => 0.05f,  // PnR = moderate assists
                        PlayType.Isolation => -0.25f,   // Iso = rarely assisted
                        PlayType.PostUp => -0.10f,      // Post-up = sometimes assisted
                        _ => 0f
                    };

                    // Ball movement priority increases assists (0-100, default 70)
                    var offSystem = _offenseStrategy.OffensiveSystem;
                    if (offSystem != null)
                    {
                        assistChance += (offSystem.BallMovementPriority - 50) / 200f; // ±25%

                        // Extra pass philosophy bonus
                        if (offSystem.ExtraPassPhilosophy)
                            assistChance += 0.10f;
                    }

                    // Passer skill modifier
                    var passer = _offensePlayers[_ballHandlerIndex];
                    assistChance += (passer.Passing - 50) / 400f; // ±12.5%

                    assistChance = Mathf.Clamp(assistChance, 0.15f, 0.90f);

                    if (_random.NextDouble() < assistChance)
                    {
                        result.Events.Add(new PossessionEvent
                        {
                            Type = EventType.Pass,
                            GameClock = endGameClock,
                            Quarter = quarter,
                            ActorPlayerId = passer.PlayerId,
                            TargetPlayerId = shooter.PlayerId,
                            Outcome = EventOutcome.Success
                        });
                    }
                }

                // Reposition shooter based on strategy shot zone frequencies
                RepositionShooterForZone(shooterIndex);

                // Check for defensive foul before shot
                var shotType = ShotCalculator.DetermineShotType(
                    shooter, _offensePositions[shooterIndex],
                    _offensePositions[shooterIndex].DistanceTo(_defensePositions[shooterIndex]), false);

                float foulChance = _foulSystem.CalculateFoulProbability(defender, shooter, shotType, _gameContext, _defenseStrategy.DefensiveSystem);
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

                    // Generate rebound on miss or block
                    bool isMissOrBlock = shotEvent.Type == EventType.Block ||
                        (shotEvent.Type == EventType.Shot && shotEvent.Outcome == EventOutcome.Fail);
                    if (isMissOrBlock)
                    {
                        var reboundEvent = GenerateRebound(endGameClock, quarter);
                        result.Events.Add(reboundEvent);

                        // Offensive rebound → second chance shot (max 1 per possession)
                        if (reboundEvent.IsOffensiveRebound)
                        {
                            int secondShooterIndex = SelectShooter();
                            var secondShooter = _offensePlayers[secondShooterIndex];
                            var secondDefender = _defensePlayers[secondShooterIndex];
                            RepositionShooterForZone(secondShooterIndex);
                            var secondShotEvent = ExecuteShot(secondShooter, secondShooterIndex, endGameClock, quarter, possessionClock);
                            result.Events.Add(secondShotEvent);

                            if (secondShotEvent.Type == EventType.Block)
                            {
                                result.Outcome = PossessionOutcome.Block;
                            }
                            else if (secondShotEvent.Outcome == EventOutcome.Success)
                            {
                                result.Outcome = PossessionOutcome.Score;
                                result.PointsScored = secondShotEvent.PointsScored;
                            }
                            // else stays as Miss
                        }
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

            // === STEAL CHECK (separate from turnovers) ===
            // Base steal chance ~3.5% per possession (NBA avg ~7-8 steals/team over ~100 possessions)
            float stealChance = 0.035f;

            // Defensive strategy influence
            var defSystem = _defenseStrategy.DefensiveSystem;
            if (defSystem != null)
            {
                stealChance += (defSystem.OnBallPressure - 50) / 500f;      // ±10%
                stealChance += (defSystem.GamblingFrequency - 30) / 600f;   // ±12%
            }

            // Ball handler's handling reduces steal chance
            stealChance -= (handler.BallHandling - 50) / 500f;             // ±10%

            stealChance = Mathf.Clamp(stealChance, 0.01f, 0.10f);

            if (_random.NextDouble() < stealChance)
            {
                return PossessionOutcomeType.Steal;
            }

            // Base turnover rate ~10% (reduced from 13% since steals are now separate)
            float turnoverChance = 0.10f;

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

        private enum PossessionOutcomeType { Shot, Turnover, Violation, Foul, Steal }

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

                // Strategy influence (continuous weights instead of thresholds)
                weights[i] += p.Shot_Three * (_offenseStrategy.ThreePointFrequency / 100f) * 0.5f;
                if (i == 3 || i == 4) // Bigs
                    weights[i] += p.Finishing_PostMoves * (_offenseStrategy.PostUpFrequency / 100f) * 0.5f;
                // Rim attack favors athletic finishers
                weights[i] += p.Finishing_Rim * (_offenseStrategy.RimAttackFrequency / 100f) * 0.3f;

                // Play type influence on shooter selection
                switch (_currentPlayType)
                {
                    case PlayType.Isolation:
                        // Iso: heavily favor the best scorer
                        weights[i] *= (avgShooting / 70f); // best shooters get amplified
                        break;
                    case PlayType.PostUp:
                        // Post-up: favor bigs (PF/C)
                        if (i >= 3)
                            weights[i] *= 2.0f;
                        else
                            weights[i] *= 0.5f;
                        break;
                    case PlayType.PickAndRoll:
                        // PnR: favor ball handler and the big (screener)
                        if (i == _ballHandlerIndex)
                            weights[i] *= 1.5f;
                        else if (i >= 3) // big as roller/popper
                            weights[i] *= 1.3f;
                        break;
                    // Motion: no additional weighting (default spread)
                }

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
        /// Determines the play type for this possession based on offensive strategy frequencies.
        /// </summary>
        private PlayType DeterminePlayType()
        {
            var offSystem = _offenseStrategy.OffensiveSystem;
            if (offSystem == null) return PlayType.Motion;

            float pnr = offSystem.PickAndRollFrequency;
            float iso = offSystem.IsolationFrequency;
            float post = offSystem.PostUpFrequency;
            float motion = Mathf.Max(100f - pnr - iso - post, 10f); // remainder is motion

            float total = pnr + iso + post + motion;
            float roll = (float)_random.NextDouble() * total;

            if (roll < pnr) return PlayType.PickAndRoll;
            if (roll < pnr + iso) return PlayType.Isolation;
            if (roll < pnr + iso + post) return PlayType.PostUp;
            return PlayType.Motion;
        }

        /// <summary>
        /// Repositions the shooter based on strategy shot zone frequencies.
        /// Uses ThreePointFrequency, MidRangeFrequency, RimAttackFrequency as weights.
        /// </summary>
        private void RepositionShooterForZone(int shooterIndex)
        {
            float xMult = _isOffenseHome ? 1f : -1f;

            // Start with strategy frequencies, then modify by play type
            float threeWeight = Mathf.Max(_offenseStrategy.ThreePointFrequency, 1f);
            float midWeight = Mathf.Max(_offenseStrategy.MidRangeFrequency, 1f);
            float rimWeight = Mathf.Max(_offenseStrategy.RimAttackFrequency, 1f);

            // Play type shifts zone distribution
            switch (_currentPlayType)
            {
                case PlayType.PostUp:
                    rimWeight *= 2.0f;    // Post-ups heavily favor paint
                    midWeight *= 1.2f;    // Some face-up mid-range
                    threeWeight *= 0.3f;  // Rarely kick out for 3
                    break;
                case PlayType.Isolation:
                    midWeight *= 1.5f;    // Iso creates mid-range pullups
                    rimWeight *= 1.3f;    // Drives to rim
                    break;
                case PlayType.PickAndRoll:
                    rimWeight *= 1.4f;    // Roll to rim
                    midWeight *= 1.3f;    // Pull-up mid-range
                    break;
                // Motion: uses base frequencies unmodified
            }
            float total = threeWeight + midWeight + rimWeight;

            float roll = (float)_random.NextDouble() * total;

            CourtPosition newPos;
            if (roll < threeWeight)
            {
                // Three-point zone — pick a spot on the arc
                float[] yOptions = { -22f, -16f, 0f, 16f, 22f }; // corners to top
                float y = yOptions[_random.Next(yOptions.Length)];
                float x = Mathf.Abs(y) > 20f ? 42f : (28f + (float)_random.NextDouble() * 4f); // corners vs wings
                newPos = new CourtPosition(x * xMult, y);
            }
            else if (roll < threeWeight + midWeight)
            {
                // Mid-range zone
                float angle = (float)(_random.NextDouble() * Mathf.PI) - Mathf.PI / 2f;
                float dist = 12f + (float)_random.NextDouble() * 8f; // 12-20 feet from basket
                float basketX = 42f * xMult;
                newPos = new CourtPosition(basketX - Mathf.Cos(angle) * dist * xMult, Mathf.Sin(angle) * dist);
            }
            else
            {
                // Rim zone — restricted area / paint
                float x = 38f + (float)_random.NextDouble() * 4f; // near basket
                float y = (float)(_random.NextDouble() * 10f - 5f);
                newPos = new CourtPosition(x * xMult, y);
            }

            _offensePositions[shooterIndex] = newPos;
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

            // Spacing modifier: wider spacing = defenders further away = less contested shots
            var offSystem = _offenseStrategy.OffensiveSystem;
            if (offSystem != null)
            {
                float spacingMult = offSystem.SpacingWidth switch
                {
                    SpacingLevel.Tight => 0.85f,
                    SpacingLevel.Normal => 1.0f,
                    SpacingLevel.Wide => 1.1f,
                    SpacingLevel.ExtraWide => 1.2f,
                    _ => 1.0f
                };
                defenderDistance *= spacingMult;
            }

            // Determine shot type and zone
            var shotType = ShotCalculator.DetermineShotType(shooter, position, defenderDistance, false);

            // Calculate shot probability
            float shotProb = ShotCalculator.CalculateShotProbability(
                shooter, position, defender, defenderDistance, shotType,
                isFastBreak: _isFastBreak, secondsRemaining: possessionClock, isHomeTeam: _isOffenseHome);

            // === DEFENSIVE SCHEME MODIFIERS ===
            var defSystem = _defenseStrategy.DefensiveSystem;
            if (defSystem != null)
            {
                var zone = position.GetZone(_isOffenseHome);

                // ContestingLevel affects how well shots are contested (default 70)
                float contestScale = defSystem.ContestingLevel / 70f;
                // Higher contesting = defender effectively closer
                defenderDistance /= Mathf.Max(contestScale, 0.5f);

                // OnBallPressure affects defender distance
                float pressureScale = 1f - (defSystem.OnBallPressure - 50) / 200f; // 50 = neutral
                defenderDistance *= Mathf.Clamp(pressureScale, 0.6f, 1.4f);

                // PackedPaint / ActiveHelp: harder to score inside
                if (defSystem.HelpDefense == HelpDefenseLevel.PackedPaint ||
                    defSystem.HelpDefense == HelpDefenseLevel.ActiveHelp)
                {
                    if (zone == CourtZone.RestrictedArea || zone == CourtZone.Paint)
                        shotProb *= 0.92f; // -8% for inside shots
                }

                // Zone defense: mid-range harder, corner 3s slightly easier
                if (defSystem.ZoneUsage > 0)
                {
                    float zoneInfluence = defSystem.ZoneUsage / 100f;
                    if (zone == CourtZone.ShortMidRange || zone == CourtZone.LongMidRange)
                        shotProb *= (1f - 0.06f * zoneInfluence); // up to -6%
                    else if (zone == CourtZone.ThreePoint)
                        shotProb *= (1f + 0.03f * zoneInfluence); // up to +3% (open corner 3s vs zone)
                }

                // SwitchAll: potential mismatches — slight bonus for offense
                if (defSystem.SwitchingLevel == SwitchingLevel.SwitchAll)
                    shotProb *= 1.02f; // +2% mismatch advantage
            }

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
        /// Creates a steal event. Selects the best defender by Steal attribute.
        /// </summary>
        private PossessionEvent CreateStealEvent(float gameClock, int quarter)
        {
            // Select stealing defender weighted by Steal attribute
            float[] weights = new float[5];
            for (int i = 0; i < 5; i++)
            {
                weights[i] = Mathf.Max(_defensePlayers[i].Steal, 5f);
            }

            float total = weights.Sum();
            float roll = (float)_random.NextDouble() * total;
            float cumulative = 0;
            int stealerIndex = 0;
            for (int i = 0; i < 5; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative) { stealerIndex = i; break; }
            }

            return new PossessionEvent
            {
                Type = EventType.Steal,
                GameClock = gameClock,
                Quarter = quarter,
                ActorPlayerId = _defensePlayers[stealerIndex].PlayerId,
                TargetPlayerId = _offensePlayers[_ballHandlerIndex].PlayerId,
                Outcome = EventOutcome.Success,
                Description = $"Steal by defender"
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
        /// Generates a rebound event after a missed shot or block.
        /// Determines offensive vs defensive rebound based on strategy and player skill.
        /// </summary>
        private PossessionEvent GenerateRebound(float gameClock, int quarter)
        {
            // Base offensive rebound rate ~22% (NBA average)
            float orebChance = 0.22f;

            // OffensiveReboundingFocus (1-5): +4% per level above 2
            orebChance += (_offenseStrategy.OffensiveReboundingFocus - 2) * 0.04f;

            // SendersOnOffensiveGlass: +3% per sender above 2
            orebChance += (_offenseStrategy.SendersOnOffensiveGlass - 2) * 0.03f;

            orebChance = Mathf.Clamp(orebChance, 0.08f, 0.40f);

            bool isOffensive = _random.NextDouble() < orebChance;

            // Select rebounder weighted by rebounding skill
            Player[] reboundTeam = isOffensive ? _offensePlayers : _defensePlayers;
            float[] weights = new float[5];
            for (int i = 0; i < 5; i++)
            {
                // Bigs (PF/C at index 3,4) get positional bonus
                float posBonus = (i >= 3) ? 1.5f : 1.0f;
                // Use DefensiveRebound as general rebounding skill
                weights[i] = reboundTeam[i].DefensiveRebound * posBonus;
                if (weights[i] < 1f) weights[i] = 1f;
            }

            float total = weights.Sum();
            float roll = (float)_random.NextDouble() * total;
            float cumulative = 0;
            int rebounderIndex = 0;
            for (int i = 0; i < 5; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative) { rebounderIndex = i; break; }
            }

            return new PossessionEvent
            {
                Type = EventType.Rebound,
                GameClock = gameClock,
                Quarter = quarter,
                ActorPlayerId = reboundTeam[rebounderIndex].PlayerId,
                Outcome = EventOutcome.Success,
                IsOffensiveRebound = isOffensive
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
                isFastBreak: _isFastBreak, secondsRemaining: possessionClock, isHomeTeam: _isOffenseHome);

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
