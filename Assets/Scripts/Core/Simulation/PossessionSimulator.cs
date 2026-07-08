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
        private HalfCourtAction _currentAction;          // Action family decided per possession
        private bool _isFastBreak;                       // Real transition possession (decided, not cosmetic)

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
            int scoreDifferential = 0,
            bool liveBallStart = false,
            HalfCourtAction? calledAction = null)
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

            // Select primary ball handler (weighted by position and skill)
            _ballHandlerIndex = SelectBallHandler();

            // End-game: the defense may simply give a foul — up 3 protecting the arc,
            // or trailing and stopping the clock. Short possession, free throws.
            if (DefenseWantsIntentionalFoul(gameClock, quarter, scoreDifferential))
                return BuildIntentionalFoulPossession(result, gameClock, quarter);

            // Transition first: a live-ball start plus a coach who pushes can turn this
            // into a real fast break — shorter clock, rim pressure, easier looks.
            // A called set play implies half-court: the call overrides the break.
            _isFastBreak = !calledAction.HasValue && RollFastBreak(liveBallStart);

            // Pick the half-court action family: the coach's play call wins, else
            // roll from the offense's system frequencies. Shapes possession duration,
            // turnover risk, shooter selection, and shot zones below — all on the
            // decision RNG, identical at every SpatialDetail.
            _currentAction = calledAction ?? (_isFastBreak ? HalfCourtAction.Motion : SelectAction());

            float possessionDuration;
            if (_isFastBreak)
            {
                // Early offense: the shot comes up in 4-7 seconds
                possessionDuration = 4f + (float)_random.NextDouble() * 3f;
            }
            else
            {
                // Half-court duration (influenced by pace, shot-clock philosophy, action)
                float paceMultiplier = Mathf.Lerp(1.15f, 0.85f, _offenseStrategy.Pace / 100f);
                possessionDuration = AVG_POSSESSION_TIME * paceMultiplier;
                possessionDuration += DurationShiftForApproach();
                possessionDuration += _currentAction switch
                {
                    HalfCourtAction.Isolation => 1.0f,   // clear-outs burn clock
                    HalfCourtAction.PostUp => 0.8f,      // post entries take time
                    HalfCourtAction.Cut => -0.5f,        // quick hitters
                    HalfCourtAction.Handoff => -0.5f,
                    _ => 0f
                };
                possessionDuration += (float)(_random.NextDouble() * 4.0 - 2.0); // +/- 2 seconds variance
                possessionDuration = Mathf.Clamp(possessionDuration, MIN_POSSESSION_TIME, SHOT_CLOCK - 2f);
            }

            // End-game clock management: milk a lead, hurry a deficit, hold for the last shot
            possessionDuration = ApplyEndGameTiming(possessionDuration, gameClock, quarter, scoreDifferential);

            // Don't exceed remaining game clock
            possessionDuration = Mathf.Min(possessionDuration, gameClock);

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
                IsFastBreak = _isFastBreak,
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

                // Check for steal before shot (~5% chance); gambling defenses reach more
                float stealChance = 0.05f * (defender.Steal / 80f);
                var stealSys = _defenseStrategy?.DefensiveSystem;
                if (stealSys != null)
                {
                    stealChance *= 1f + (stealSys.GamblingFrequency - 30) / 100f;
                    if (stealSys.PrimaryScheme == DefensiveSchemeType.FullCourtPress ||
                        stealSys.PrimaryScheme == DefensiveSchemeType.HalfCourtTrap)
                        stealChance *= 1.2f;
                }
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
                    // Check for defensive foul before shot. Decided ONCE — the shot event
                    // reuses this exact type so script (choreography/narration) and event
                    // (ticker/marker/stats) can never disagree.
                    var shotType = ShotCalculator.DetermineShotType(
                        shooter, _offensePositions[shooterIndex],
                        _offensePositions[shooterIndex].DistanceTo(_defensePositions[shooterIndex]),
                        false, _isOffenseHome, _random);

                    _script.ShotType = shotType;

                    float foulChance = _foulSystem.CalculateFoulProbability(defender, shooter, shotType,
                        _gameContext, _defenseStrategy?.DefensiveSystem);
                    // Zone defenses commit fewer fouls (no man to chase)
                    float zoneFrac = (_defenseStrategy?.DefensiveSystem?.ZoneUsage ?? 0) / 100f;
                    if (zoneFrac > 0f) foulChance *= 1f - 0.15f * zoneFrac;
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
                        var shotEvent = ExecuteShot(shooter, shooterIndex, shotType, endGameClock, quarter, possessionClock);
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

                        // Generate rebound on missed shots. Crashing the glass earns
                        // second chances; sprinting back and zone defense concede them.
                        if (shotEvent.Outcome == EventOutcome.Fail || shotEvent.Type == EventType.Block)
                        {
                            float orebChance = 0.25f;
                            orebChance += (_offenseStrategy.OffensiveReboundingFocus - 2) * 0.02f;
                            orebChance += (_offenseStrategy.SendersOnOffensiveGlass - 2) * 0.01f;
                            if (_offenseStrategy.TransitionDefense == TransitionDefensePreference.SprintBack)
                                orebChance -= 0.03f;
                            orebChance += (_defenseStrategy?.DefensiveSystem?.ZoneUsage ?? 0) / 100f * 0.04f;
                            if (_isFastBreak) orebChance -= 0.05f; // nobody trails the break
                            orebChance = Mathf.Clamp(orebChance, 0.10f, 0.40f);

                            bool offensiveRebound = _random.NextDouble() < orebChance;
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
                result.PresentationSeconds = _choreographer.TotalSeconds;
                result.LiveSeconds = _choreographer.LiveSeconds;
                result.NarrationBeats = _choreographer.Beats;
            }

            result.EndGameClock = endGameClock;
            result.WasFastBreak = _isFastBreak;
            result.Action = _currentAction;
            return result;
        }

        /// <summary>
        /// The defense elects to give a foul: leading by exactly 3 in the final
        /// possession window (deny the game-tying three), or trailing late and
        /// stopping the clock. Only when the foul actually sends the offense to
        /// the line (defense at 4+ team fouls — the foul itself triggers the bonus);
        /// before that, fouling would hand over the ball for nothing here.
        /// </summary>
        private bool DefenseWantsIntentionalFoul(float gameClock, int quarter, int scoreDiff)
        {
            var close = _defenseStrategy?.CloseGameStrategy;
            if (close == null || quarter < 4) return false;
            if (gameClock <= 2f) return false; // too late to matter
            if (_foulSystem.GetTeamFouls(_defenseTeamId) < 4) return false;

            int defenseMargin = -scoreDiff; // positive = defense leading

            // Up 3, shot-clock-off window: foul before the tying three goes up
            if (close.FoulWhenDown3 && defenseMargin == 3 && gameClock <= 24f)
                return true;

            // Trailing with the clock dying: foul to extend — but only while the
            // deficit is still chaseable in the time left (nobody fouls down 9
            // with under a minute; each made pair ends the tactic naturally)
            if (close.IntentionalFoulWhenDown && !close.PlayStraightUpLate &&
                defenseMargin < 0 && gameClock <= 45f &&
                -defenseMargin <= 3f + gameClock / 12f)
                return true;

            return false;
        }

        /// <summary>
        /// A give-the-foul possession: 1.5-3.5 seconds, a personal foul on the
        /// handler (or the worst free-throw shooter if the coach hunts hack-a),
        /// and bonus free throws. Choreographed like any decided shooting foul.
        /// </summary>
        private PossessionResult BuildIntentionalFoulPossession(PossessionResult result, float gameClock, int quarter)
        {
            float duration = 1.5f + (float)_random.NextDouble() * 2f;
            duration = Mathf.Min(duration, gameClock);
            float endClock = gameClock - duration;

            // Whom to grab: the handler, unless hunting a poor free-throw shooter
            int targetIdx = _ballHandlerIndex;
            var d = _defenseStrategy?.DefensiveSystem;
            if (d != null && d.IntentionalFoulPoorShooters)
            {
                for (int i = 0; i < 5; i++)
                    if (_offensePlayers[i].FreeThrow < d.FreeThrowThresholdToFoul + 20 &&
                        _offensePlayers[i].FreeThrow < _offensePlayers[targetIdx].FreeThrow)
                        targetIdx = i;
            }
            var fouled = _offensePlayers[targetIdx];
            var fouler = _defensePlayers[targetIdx];

            _script = new PossessionScript
            {
                Offense = _offensePlayers,
                Defense = _defensePlayers,
                OffenseAttacksRight = _isOffenseHome,
                StartGameClock = gameClock,
                Duration = duration,
                Quarter = quarter,
                OffenseStrategy = _offenseStrategy,
                DefenseStrategy = _defenseStrategy,
                InitialBallHandlerIndex = _ballHandlerIndex,
                IsFastBreak = false,
                Events = result.Events,
                ShooterIndex = targetIdx,
                ShotType = ShotType.Layup,
                ShotPosition = _offensePositions[targetIdx],
                Ending = ScriptEnding.ShootingFoulMissed
            };

            var foulEvent = _foulSystem.CreateFoulEvent(fouled, fouler, _defenseTeamId,
                FoulType.Personal, isShootingFoul: false, shotWasMade: false, isBehindArc: false,
                shotType: null, gameClock: endClock, quarter: quarter, context: _gameContext);
            result.Events.Add(foulEvent);

            int made = 0;
            int freeThrows = foulEvent.FoulDetail?.FreeThrowsAwarded ?? 0;
            if (freeThrows > 0)
            {
                var ft = _freeThrowHandler.CalculateFreeThrows(fouled, freeThrows, _gameContext);
                made = ft.Made;
                foulEvent.FreeThrowResult = ft;
                result.Events.Add(_freeThrowHandler.CreateFreeThrowEvent(fouled, ft, endClock, quarter));
                _script.FreeThrows = ft;
            }

            result.Outcome = made > 0 ? PossessionOutcome.Score : PossessionOutcome.Foul;
            result.PointsScored = made;
            _script.PointsScored = made;

            for (int i = 0; i < 5; i++)
            {
                _script.OffenseSpots[i] = _offensePositions[i];
                _script.DefenseSpots[i] = _defensePositions[i];
            }

            if (SpatialDetail == SpatialDetailLevel.Full)
            {
                _choreographer ??= new PossessionChoreographer(_choreoRandom);
                result.SpatialStates = _choreographer.Choreograph(_script);
                result.PresentationSeconds = _choreographer.TotalSeconds;
                result.LiveSeconds = _choreographer.LiveSeconds;
                result.NarrationBeats = _choreographer.Beats;
            }

            result.EndGameClock = endClock;
            result.WasFastBreak = false;
            return result;
        }

        /// <summary>
        /// End-game clock management, all from the offense's CloseGameStrategy:
        /// milk a late lead, hurry when trailing, hold for the quarter's last shot.
        /// Neutral outside the final minutes.
        /// </summary>
        private float ApplyEndGameTiming(float duration, float gameClock, int quarter, int scoreDiff)
        {
            var close = _offenseStrategy.CloseGameStrategy;

            // Milk the clock protecting a late lead
            if (quarter >= 4 && gameClock <= 180f && close != null && close.RunClockWhenAhead &&
                scoreDiff >= Mathf.Max(1, close.SecondsAheadToSlowDown))
            {
                duration = Mathf.Max(duration, SHOT_CLOCK - 3f + (float)_random.NextDouble() * 2f);
            }

            // Trailing late: get a shot up fast
            if (quarter >= 4 && gameClock <= 60f && scoreDiff < 0 && scoreDiff >= -12)
            {
                duration = Mathf.Min(duration, 5f + (float)_random.NextDouble() * 3f);
            }

            // Hold for the last shot of the quarter (only when not chasing points)
            if (gameClock <= SHOT_CLOCK + 6f && gameClock > 6f && scoreDiff >= 0)
            {
                float hold = gameClock - (close?.TargetLastShotClock ?? 4);
                if (hold > 4f) duration = Mathf.Max(duration, Mathf.Min(hold, SHOT_CLOCK - 0.5f));
            }

            return duration;
        }

        /// <summary>
        /// Decides whether this possession is a real fast break: the offense's push
        /// philosophy, a live-ball start (steal / defensive board), and how hard the
        /// defense commits to getting back.
        /// </summary>
        private bool RollFastBreak(bool liveBallStart)
        {
            // A team milking a late lead does not run
            var close = _offenseStrategy.CloseGameStrategy;
            if (_gameContext.Quarter >= 4 && _gameContext.GameClock <= 180f &&
                close != null && close.RunClockWhenAhead &&
                _gameContext.ScoreDifferential >= Mathf.Max(1, close.SecondsAheadToSlowDown))
                return false;

            float chance = _offenseStrategy.TransitionOffense switch
            {
                TransitionPreference.NoPush => 0.02f,
                TransitionPreference.OpportunisticPush => 0.06f,
                TransitionPreference.PushWhenPossible => 0.11f,
                TransitionPreference.AggressivePush => 0.16f,
                TransitionPreference.AlwaysPush => 0.22f,
                _ => 0.06f
            };
            chance *= liveBallStart ? 1.8f : 0.25f;
            chance *= _defenseStrategy?.TransitionDefense switch
            {
                TransitionDefensePreference.SprintBack => 0.6f,
                TransitionDefensePreference.GambleForSteals => 1.25f,
                TransitionDefensePreference.MatchupTransition => 0.85f,
                _ => 1f
            };
            return _random.NextDouble() < chance;
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

            // Action risk: screen-and-exchange actions add traffic; clear-outs are safe
            turnoverChance += _currentAction switch
            {
                HalfCourtAction.Handoff => 0.010f,
                HalfCourtAction.PickAndRoll => 0.005f,
                HalfCourtAction.Cut => 0.005f,
                HalfCourtAction.Isolation => -0.010f,
                _ => 0f
            };

            // Defensive scheme pressure: traps and blitzes force mistakes
            var dSys = _defenseStrategy?.DefensiveSystem;
            if (dSys != null)
            {
                if (_currentAction == HalfCourtAction.PickAndRoll &&
                    dSys.PickAndRollCoverage == PnRCoverage.Blitz)
                    turnoverChance += 0.02f;
                if (dSys.PrimaryScheme == DefensiveSchemeType.FullCourtPress ||
                    dSys.PrimaryScheme == DefensiveSchemeType.HalfCourtTrap)
                    turnoverChance += 0.015f;
            }

            // Reckless pushes get loose with the ball
            if (_isFastBreak && _offenseStrategy.TransitionOffense >= TransitionPreference.AggressivePush)
                turnoverChance += 0.01f;

            // Ball-movement philosophy: more passes carry a little more risk
            var osys = _offenseStrategy.OffensiveSystem;
            if (osys != null)
            {
                turnoverChance += (osys.BallMovementPriority - 70) / 100f * 0.01f;
                if (osys.ExtraPassPhilosophy) turnoverChance += 0.005f;
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
        /// Picks the half-court action family for this possession from the offense's
        /// OffensiveSystem frequencies. Neutral at defaults: a motion-led mix with
        /// PnR as the most common set action (matching today's implicit behavior).
        /// </summary>
        private HalfCourtAction SelectAction()
        {
            var sys = _offenseStrategy.OffensiveSystem;
            float pnr = sys?.PickAndRollFrequency ?? 30;
            float iso = sys?.IsolationFrequency ?? 15;
            float post = Mathf.Max(_offenseStrategy.PostUpFrequency, sys?.PostUpFrequency ?? 10);
            float handoff = (sys?.HandoffFrequency ?? 30) * 0.5f;
            float cut = (sys?.CuttingFrequency ?? 50) * 0.4f;
            float motion = 25f + (sys?.BallMovementPriority ?? 70) * 0.25f;

            // Clutch: the coach's late-game play call tilts the mix hard
            if (_gameContext.IsClutchTime)
            {
                switch (_offenseStrategy.CloseGameStrategy?.LateGamePlay)
                {
                    case LateGamePlayType.StarIsolation: iso *= 3f; break;
                    case LateGamePlayType.PickAndRoll: pnr *= 3f; break;
                    case LateGamePlayType.PostUp: post *= 3f; break;
                }
            }

            // The declared system identity tilts the mix — this is what makes the
            // pre-game scheme picker and the strategy templates play differently
            switch (sys?.PrimarySystem)
            {
                case OffensiveSystemType.PickAndRollHeavy: pnr *= 1.5f; break;
                case OffensiveSystemType.IsoHeavy: iso *= 1.6f; break;
                case OffensiveSystemType.PostUpFocused: post *= 1.6f; break;
                case OffensiveSystemType.TriangleOffense: post *= 1.3f; cut *= 1.3f; break;
                case OffensiveSystemType.PrincetonOffense: cut *= 1.6f; break;
                case OffensiveSystemType.FlexOffense: cut *= 1.3f; handoff *= 1.2f; break;
                case OffensiveSystemType.HornsSet: pnr *= 1.3f; break;
                case OffensiveSystemType.MotionOffense: motion *= 1.3f; cut *= 1.2f; break;
                case OffensiveSystemType.FiveOut: motion *= 1.2f; break;
            }

            float roll = (float)_random.NextDouble() * (pnr + iso + post + handoff + cut + motion);
            if ((roll -= pnr) < 0) return HalfCourtAction.PickAndRoll;
            if ((roll -= iso) < 0) return HalfCourtAction.Isolation;
            if ((roll -= post) < 0) return HalfCourtAction.PostUp;
            if ((roll -= handoff) < 0) return HalfCourtAction.Handoff;
            if ((roll -= cut) < 0) return HalfCourtAction.Cut;
            return HalfCourtAction.Motion;
        }

        /// <summary>
        /// Possession-length shift from shot-clock philosophy. Zero at defaults
        /// (Balanced approach, entry at 14).
        /// </summary>
        private float DurationShiftForApproach()
        {
            var sys = _offenseStrategy.OffensiveSystem;
            if (sys == null) return 0f;

            float shift = sys.ShotClockApproach switch
            {
                ShotClockApproach.EarlyGoodShot => -1.5f,
                ShotClockApproach.WorkForBestShot => 1.5f,
                ShotClockApproach.RunClockLate => 3f,
                _ => 0f
            };
            // Entry at 18 = looking to score early (shorter); entry at 8 = patient (longer)
            shift += (sys.TargetShotClockEntry - 14) * -0.15f;
            return shift;
        }

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
        /// Selects the shooter based on shooting skill, strategy, action, and tendencies.
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

                // Action shaping: who the chosen set actually creates shots for
                switch (_currentAction)
                {
                    case HalfCourtAction.PickAndRoll:
                        if (i == _ballHandlerIndex) weights[i] *= 1.6f;          // handler pull-up/drive
                        if (i == 3 || i == 4) weights[i] += p.Finishing_Rim * 0.35f; // roll man
                        break;
                    case HalfCourtAction.PostUp:
                        if (i == 3 || i == 4) weights[i] += p.Finishing_PostMoves * 0.5f;
                        else weights[i] *= 0.75f;
                        break;
                    case HalfCourtAction.Cut:
                        weights[i] += p.Finishing_Rim * 0.15f;
                        break;
                    case HalfCourtAction.Handoff:
                        if (i <= 2) weights[i] *= 1.15f;                          // perimeter chain
                        break;
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

            // Isolation: clear out for the best creator on the floor — and the man
            // with the ball tends to BE that creator (self-created, unassisted looks)
            if (_currentAction == HalfCourtAction.Isolation)
            {
                int star = 0;
                float best = float.MinValue;
                for (int i = 0; i < 5; i++)
                {
                    var p = _offensePlayers[i];
                    float isoSkill = (p.Shot_Three + p.Shot_MidRange + p.Finishing_Rim) / 3f * 0.6f
                                     + p.BallHandling * 0.4f;
                    if (isoSkill > best) { best = isoSkill; star = i; }
                }
                weights[star] *= 3.5f;
                weights[_ballHandlerIndex] *= 1.5f;
            }

            // Clutch: feed the designated closer if he's on the floor
            if (_gameContext.IsClutchTime)
            {
                string goTo = _offenseStrategy.CloseGameStrategy?.GoToPlayerId;
                if (!string.IsNullOrEmpty(goTo))
                {
                    for (int i = 0; i < 5; i++)
                        if (_offensePlayers[i].PlayerId == goTo) { weights[i] *= 3f; break; }
                }
            }

            // Ball-movement philosophy shapes who shoots: low-movement offenses run
            // through the handler (unassisted); high-movement teams spread usage
            // toward the open man. Neutral in the 60-70 default band.
            float bmp = _offenseStrategy.OffensiveSystem?.BallMovementPriority ?? 70;
            if (bmp < 60)
            {
                weights[_ballHandlerIndex] *= 1f + (60f - bmp) / 60f; // up to x2 at BMP 0
            }
            else if (bmp > 70)
            {
                float avg = weights.Average();
                float flatten = (bmp - 70f) / 30f * 0.5f; // up to 50% toward even usage
                for (int i = 0; i < 5; i++) weights[i] = Mathf.Lerp(weights[i], avg, flatten);
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

            // The action tilts where the shot comes from
            switch (_currentAction)
            {
                case HalfCourtAction.PickAndRoll:
                    rimWeight *= 1.3f; threeWeight *= 1.1f; midWeight *= 0.85f; break;
                case HalfCourtAction.PostUp:
                    midWeight *= 1.5f; rimWeight *= 1.3f; threeWeight *= 0.55f; break;
                case HalfCourtAction.Isolation:
                    midWeight *= 1.3f; threeWeight *= 1.1f; break;
                case HalfCourtAction.Cut:
                    rimWeight *= 1.7f; midWeight *= 0.7f; threeWeight *= 0.75f; break;
                case HalfCourtAction.Handoff:
                    threeWeight *= 1.25f; break;
            }

            // Transition shots are rim runs and kick-ahead threes, almost never pull-up twos
            if (_isFastBreak)
            {
                rimWeight *= 2f; threeWeight *= 1.2f; midWeight *= 0.5f;
            }

            // Late-game geometry: down three or more in the final 30 seconds, the
            // three is the only shot; a coach calling for it hunts it all clutch
            if (_gameContext.Quarter >= 4 && _gameContext.GameClock <= 30f &&
                _gameContext.ScoreDifferential <= -3)
            {
                threeWeight *= 3f;
            }
            else if (_gameContext.IsClutchTime &&
                     _offenseStrategy.CloseGameStrategy?.LateGamePlay == LateGamePlayType.ThreePointer)
            {
                threeWeight *= 2f;
            }

            float total = threeWeight + midWeight + rimWeight;

            float roll = (float)_random.NextDouble() * total;

            CourtPosition newPos;
            if (roll < threeWeight)
            {
                // Three-point zone — pick a spot ACTUALLY behind the arc, with margin over
                // the GetZone thresholds (23.75 ft / 22 ft corner) so points always award 3.
                float[] yOptions = { -22f, -16f, 0f, 16f, 22f };
                float y = yOptions[_random.Next(yOptions.Length)];
                float r = (float)_random.NextDouble();
                float x;
                if (Mathf.Abs(y) > 20f)
                {
                    // Corner three: on the baseline lane, 22.1-22.8 ft from the rim
                    x = 42f;
                    y = Mathf.Sign(y) * (22.1f + r * 0.7f);
                }
                else
                {
                    // Top/wing three on the arc: 24.2-26.2 ft out
                    float dist = 24.2f + r * 2.0f;
                    x = 42f - Mathf.Sqrt(dist * dist - y * y);
                }
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

            // Defender closes out to a bounded contest gap (0.5-5 ft) no matter how far the
            // shooter relocated — the old proportional lag left deep threes wide open by
            // construction (bigger relocation ⇒ bigger leftover gap ⇒ +open bonus every time).
            float trail = 0.5f + (float)_random.NextDouble() * 4.5f;

            // Floor spacing stretches closeouts; packed spacing shrinks them
            trail += _offenseStrategy.OffensiveSystem?.SpacingWidth switch
            {
                SpacingLevel.Tight => -0.4f,
                SpacingLevel.Wide => 0.3f,
                SpacingLevel.ExtraWide => 0.6f,
                _ => 0f
            };
            trail = Mathf.Max(0.2f, trail);

            float gap = _defensePositions[shooterIndex].DistanceTo(newPos);
            _defensePositions[shooterIndex] = _defensePositions[shooterIndex].MoveTowards(newPos,
                Mathf.Max(0f, gap - trail));
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
        private PossessionEvent ExecuteShot(Player shooter, int shooterIndex, ShotType shotType, float gameClock, int quarter, float possessionClock)
        {
            var position = _offensePositions[shooterIndex];
            var defender = _defensePlayers[shooterIndex];
            float defenderDistance = position.DistanceTo(_defensePositions[shooterIndex]);
            var shotZone = position.GetZone(_isOffenseHome);

            // Calculate shot probability
            float shotProb = ShotCalculator.CalculateShotProbability(
                shooter, position, defender, defenderDistance, shotType,
                isFastBreak: _isFastBreak, secondsRemaining: possessionClock, isHomeTeam: _isOffenseHome);

            // Defensive scheme shapes the look by zone (neutral at scheme defaults)
            shotProb = ApplyDefenseSchemeModifiers(shotProb, shotZone);

            // Ball-movement teams find the open man: small bonus on assisted looks
            var osys = _offenseStrategy.OffensiveSystem;
            if (osys != null && _script != null && _script.BallHandlerPassedToShooter)
            {
                float moveBonus = (osys.BallMovementPriority - 70) / 100f * 0.05f;
                if (osys.ExtraPassPhilosophy) moveBonus += 0.015f;
                shotProb *= 1f + Mathf.Clamp(moveBonus, -0.03f, 0.05f);
            }

            // TENDENCY INTEGRATION: Apply tendency-based modifiers
            if (shooter.Tendencies != null)
            {
                // Clutch behavior in late game situations: last 2 minutes of the 4th+,
                // or any close-and-late situation the game context already flags
                bool isClutch = _gameContext.IsClutchTime || (quarter >= 4 && gameClock <= 120f);
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
                    ContestLevel = 1f - Mathf.Clamp01(defenderDistance / 6f)
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
        /// Defensive scheme shot-quality modifiers, by zone and against the chosen
        /// action. Every term is neutral at the scheme defaults (ZoneUsage 0,
        /// TeamHelp, Contesting 70, Contest closeouts, Gambling 30, SwitchSome).
        /// </summary>
        private float ApplyDefenseSchemeModifiers(float prob, CourtZone zone)
        {
            var d = _defenseStrategy?.DefensiveSystem;
            if (d == null) return prob;

            bool interior = zone == CourtZone.RestrictedArea || zone == CourtZone.Paint;
            float mod = 1f;

            // Zone: walls off the interior, concedes perimeter looks
            float zoneFrac = d.ZoneUsage / 100f;
            if (zoneFrac > 0f)
                mod *= interior ? 1f - 0.06f * zoneFrac : 1f + 0.05f * zoneFrac;

            // Help philosophy: bodies in the paint vs staying home on shooters
            switch (d.HelpDefense)
            {
                case HelpDefenseLevel.NoHelp: mod *= interior ? 1.04f : 0.98f; break;
                case HelpDefenseLevel.LimitedHelp: mod *= interior ? 1.02f : 1f; break;
                case HelpDefenseLevel.ActiveHelp: mod *= interior ? 0.97f : 1.02f; break;
                case HelpDefenseLevel.PackedPaint: mod *= interior ? 0.95f : 1.04f; break;
            }

            // Contest commitment on jumpers
            if (!interior)
                mod *= 1f - (d.ContestingLevel - 70) / 100f * 0.08f;

            switch (d.CloseoutStyle)
            {
                case CloseoutStyle.StayGrounded: if (!interior) mod *= 1.015f; break;
                case CloseoutStyle.RunOff: mod *= zone == CourtZone.ThreePoint ? 0.98f : 1.01f; break;
            }

            // Overplaying defenses give up cleaner looks when the gamble misses
            mod *= 1f + (d.GamblingFrequency - 30) / 1000f;

            // Scheme counters to the chosen action
            if (_currentAction == HalfCourtAction.PickAndRoll && !_isFastBreak)
            {
                switch (d.PickAndRollCoverage)
                {
                    case PnRCoverage.DropCoverage:
                        mod *= interior ? 0.98f : (zone != CourtZone.ThreePoint ? 1.03f : 1f); break;
                    case PnRCoverage.Blitz:
                        mod *= 1.03f; break; // beat the trap and it's 4-on-3
                    case PnRCoverage.SwitchAll:
                        mod *= 0.985f; break;
                }
                if (d.SwitchingLevel >= SwitchingLevel.SwitchMost) mod *= 0.985f;
            }
            else if (_currentAction == HalfCourtAction.Isolation &&
                     d.SwitchingLevel == SwitchingLevel.SwitchAll)
            {
                mod *= 1.025f; // hunt the switch, attack the mismatch
            }

            return Mathf.Clamp(prob * mod, 0.02f, 0.95f);
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
            // Determine type of turnover — ONE decided draw (same order/count as before).
            // The kind drives both the choreographed visual and the ticker wording,
            // so what's described is always exactly what's shown.
            string[] turnoverTypes = { "Bad pass", "Lost handle", "Offensive foul", "Traveled", "Out of bounds" };
            TurnoverKind[] kinds = { TurnoverKind.BadPass, TurnoverKind.LostHandle,
                TurnoverKind.OffensiveFoul, TurnoverKind.Traveled, TurnoverKind.OutOfBounds };
            int pick = _random.Next(turnoverTypes.Length);

            _script.Turnover = kinds[pick];

            return new PossessionEvent
            {
                Type = EventType.Turnover,
                GameClock = gameClock,
                Quarter = quarter,
                ActorPlayerId = _offensePlayers[_ballHandlerIndex].PlayerId,
                ActorPosition = _offensePositions[_ballHandlerIndex],
                Description = turnoverTypes[pick],
                Turnover = kinds[pick],
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
                isFastBreak: _isFastBreak, secondsRemaining: possessionClock, isHomeTeam: _isOffenseHome);

            // Defensive scheme shapes the look by zone (neutral at scheme defaults)
            shotProb = ApplyDefenseSchemeModifiers(shotProb, zone);

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
    /// Half-court action families the offense can run. Decided per possession from
    /// OffensiveSystem frequencies (decision-side only — never touches choreography RNG).
    /// </summary>
    public enum HalfCourtAction
    {
        Motion,
        PickAndRoll,
        Isolation,
        PostUp,
        Handoff,
        Cut
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

        /// <summary>Decision metadata: this possession was a real transition push.</summary>
        public bool WasFastBreak;

        /// <summary>Decision metadata: the half-court action family this possession ran.</summary>
        public HalfCourtAction Action;

        /// <summary>True length of the choreographed timeline in seconds from possession start
        /// (includes the shot-resolution tail past the live end). 0 when no choreography ran.</summary>
        public float PresentationSeconds;

        /// <summary>End of the live action window before the shot-resolution tail.
        /// 0 when no choreography ran (headless).</summary>
        public float LiveSeconds;

        /// <summary>Timed narration moments from the choreographer (radio bar + ticker re-timing).
        /// Null when no choreography ran (headless). Purely presentational.</summary>
        public List<Choreography.NarrationBeat> NarrationBeats;

        public float Duration => StartGameClock - EndGameClock;

        public string GetPlayByPlay(Func<string, string> getPlayerName)
        {
            return string.Join("\n", Events.Select(e => e.ToPlayByPlay(getPlayerName)));
        }
    }
}
