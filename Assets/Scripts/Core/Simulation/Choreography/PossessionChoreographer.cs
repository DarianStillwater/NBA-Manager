using System;
using System.Collections.Generic;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>
    /// Turns an already-DECIDED possession (PossessionScript) into a dense, believable
    /// spatial timeline: bring the ball up, set the offense, swing passes, the decided
    /// pass to the shooter, a real shot arc into the rim (through the net on makes,
    /// carom to the decided rebound spot on misses), and free-throw sequences.
    ///
    /// Purely presentational: consumes its own RNG, never touches outcomes or stats.
    /// Output is a list of SpatialStates at fixed ticks (denser during ball flight),
    /// GameClock strictly decreasing, ball motion continuous (no teleports).
    /// </summary>
    public class PossessionChoreographer
    {
        public const float Tick = 0.2f;
        public const float FlightTick = 0.1f;

        // Movement speeds (ft/s), tuned to real NBA motion. Authored legs are smoothstep-eased,
        // which peaks at 1.5× the average leg speed — so averages are set at targetPeak/1.5
        // (AdvanceAvg 18.5 ⇒ ~28 ft/s peak sprint; CutAvg 10 ⇒ 15 ft/s cuts). Worst case at the
        // 0.2s tick is 5.6 ft/tick, under the ChoreographyTest 8 ft playerJumps bound.
        // Defenders move via MoveTowards (no easing spike), so their values are true caps.
        private const float AdvanceAvg = 18.5f;   // transition bring-up (peak ≈ 28)
        private const float CutAvg = 10f;         // halfcourt cuts/screens/rolls (peak 15)
        private const float RelocateAvg = 8f;     // off-ball spacing relocations (peak 12)
        private const float JabAvg = 5.3f;        // iso jab steps (peak ≈ 8)
        private const float SprintAvg = 16.5f;    // rim runs / hard cuts, Glide ceiling (peak ≈ 24.8)
        private const float DefenseSpeed = 15f;   // base man-tracking, just under a cutting man
        private const float DefenseSprint = 24f;  // contest/hedge/rebound bursts (4.8 ft/tick)

        // Choreographed body-contact distances (P2). Authored just above the render-side
        // SoftSeparation contact minimum (1.05 ft) so the separation pass preserves the pair
        // rather than fighting it. Hip-ride sits above the 1.6 ft crowd minimum but well inside
        // the "DefensiveStance defender within 3 ft of the handler" contact-pair trigger.
        private const float ScreenContactDist = 1.1f;   // screener ↔ screened defender
        private const float PostContactDist = 1.1f;      // post ↔ his defender (rim side)
        private const float HipRideDist = 1.35f;         // on-ball defender arm's-length ride
        private const float CloseoutDist = 2.25f;        // catch closeout: arm's length, not full contact
        private const int TransitionPaintCap = 3;        // max defenders sinking into the lane in transition

        private readonly System.Random _rng;

        /// <summary>Defending team's scheme for the CURRENT possession — set by the
        /// simulator before Choreograph so zone defenses render as zones.</summary>
        public DefensiveSchemeType DefensiveScheme = DefensiveSchemeType.ManToManStandard;

        // Per-possession working state
        private PossessionScript _s;
        private float _rimX;
        private Track[] _offense;                // waypoint tracks, index 0-4
        private CourtPosition[] _defPos;         // integrated defender positions
        private List<BallSegment> _ball;
        private float _shotStartT;               // when the shot leaves the hand (-1 = no shot)
        private float _liveEndT;                 // end of live action (= Duration)
        private float _totalT;                   // includes resolution/FT tail
        private float _clockStartT;              // presentation time before the clock starts (inbound lead-in; 0 = live/legacy)
        private float _transitionEndT;           // live rebound/steal push window end (0 when not a live push)
        private CourtPosition _reboundSpot;
        private List<(float t, PossessionPhase phase)> _phaseMarks;
        private ActionPlan _action;              // the synthesized offensive action for this possession
        private DefenderAssignment[] _defPlan;   // per-defender assignment (sag/nav/help), decided once

        // Authored-contact pairs (P2), resolved once after _defPlan is built.
        private int _screenDefIdx = -1;          // defender caught on the screen (guards the beneficiary)
        private int _screenBeneIdx = -1;         // offensive player the screen frees (handler or cutter)
        private int _postDefIdx = -1;            // defender holding ground against the backing-down post
        private List<NarrationBeat> _beats;      // timed narration moments for the radio bar

        // Free-throw window state: while t >= _ftStartT the defense holds authored lane/arc
        // spots instead of man-tracking; lane bodies crash from _ftCrashT on a final-attempt
        // miss. All targets precomputed at build time — never per-tick RNG.
        private float _ftStartT = -1f;
        private float _ftCrashT = -1f;
        private CourtPosition[] _ftDefSpots;
        private CourtPosition[] _ftCrashSpots;
        private bool[] _ftLaneDefender;

        // Loose-ball turnover: from _looseBallT the fumbling handler's defender breaks off
        // man-tracking and sprints to scoop the ball at _reboundSpot.
        private float _looseBallT = -1f;
        private int _looseBallDefIdx = -1;

        // ── Rebound battle (P3). From _reboundCrashT crashers ring _reboundSpot: paired
        //    offense/defenders box out (mutual TargetPlayerId → P2 contact rendering), the decided
        //    winner takes the board, and 1-2 nearest losers get an early/shorter leap so the winner
        //    visibly out-jumps them. Winner identity + spot + timing are already decided — untouched. ──
        private CourtPosition[] _reboundCrashSpots;   // per-defender box-out target (rim side of his crasher)
        private bool[] _reboundCrashDef;              // defender has a box-out target this battle
        private float _reboundCrashT = -1f;
        private int[] _reboundOffPartner;             // offense idx → paired defender idx (-1 none)
        private int[] _reboundDefPartner;             // defender idx → paired offense idx (-1 none)

        // Extra presentational leaps (rebound-battle losers): (isDefense, idx, apexT, peak).
        // Consumed by ActionDynamics after the shooter/decided-rebounder cases.
        private List<(bool isDefense, int idx, float apexT, float peak)> _extraLeaps;

        // ── Presentational jump/densification timing (P2). All purely for rendering —
        //    they never touch outcomes. ──
        private float _shotWindup;               // gather time before the shot leaves the hand
        private float _shotRimT = -1f;           // when the shot reaches the rim (= release + flight)
        private float _reboundArriveT = -1f;     // when the ball arrives at the rebound spot (the leap)
        private List<float> _sharpCutTimes;      // key times where an offensive player cuts >90°

        public PossessionChoreographer(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        /// <summary>True length of the last choreographed timeline in seconds from possession start
        /// (includes the shot-resolution tail). Valid immediately after <see cref="Choreograph"/>.</summary>
        public float TotalSeconds => _totalT;

        /// <summary>End of the LIVE action window (before the shot-resolution/FT tail) — the span
        /// over which the game clock should tick during playback. Valid after <see cref="Choreograph"/>.</summary>
        public float LiveSeconds => _liveEndT;

        /// <summary>Presentation seconds at the FRONT of the timeline during which the clock HOLDS —
        /// the dead-ball inbound lead-in before the game/shot clock starts ticking. 0 for live starts
        /// and legacy backcourt possessions. Valid after <see cref="Choreograph"/>.</summary>
        public float ClockStartOffset => _clockStartT;

        /// <summary>Timed narration moments for the last choreographed possession (radio bar +
        /// ticker re-timing). Sorted ascending by T. Valid immediately after <see cref="Choreograph"/>.</summary>
        public List<NarrationBeat> Beats => _beats;

        public List<SpatialState> Choreograph(PossessionScript script)
        {
            _s = script;
            _rimX = CourtGeometry.RimXFor(script.OffenseAttacksRight);
            _offense = new Track[5];
            _ball = new List<BallSegment>();
            _phaseMarks = new List<(float, PossessionPhase)>();
            _beats = new List<NarrationBeat>();
            _shotStartT = -1f;
            _clockStartT = 0f;
            _transitionEndT = 0f;
            _heldBy = -1;
            _ftStartT = -1f;
            _ftCrashT = -1f;
            _ftDefSpots = null;
            _ftCrashSpots = null;
            _ftLaneDefender = null;
            _looseBallT = -1f;
            _looseBallDefIdx = -1;
            _reboundCrashSpots = null;
            _reboundCrashDef = null;
            _reboundCrashT = -1f;
            _reboundOffPartner = null;
            _reboundDefPartner = null;
            _extraLeaps = new List<(bool, int, float, float)>();
            _shotWindup = 0f;
            _shotRimT = -1f;
            _reboundArriveT = -1f;
            _sharpCutTimes = new List<float>();
            _screenDefIdx = -1;
            _screenBeneIdx = -1;
            _postDefIdx = -1;
            _action = ActionLibrary.Choose(script, _rng);

            BuildOffenseAndBall();
            _defPlan = DefenseChoreographer.BuildPlan(_action, script, _rng, DefensiveScheme);
            ResolveContactPairs();
            EmitExecutionBeats();

            // Builders don't run in strict time order — sort and clamp for consumers.
            for (int i = 0; i < _beats.Count; i++)
                _beats[i].T = Math.Min(Math.Max(_beats[i].T, 0f), _totalT);
            _beats.Sort((a, b) => a.T.CompareTo(b.T));

            return Emit();
        }

        /// <summary>
        /// Narrate the possession's execution facts — only when they touched a real
        /// shot, so the radio calls out the lapse the viewer can actually see.
        /// </summary>
        private void EmitExecutionBeats()
        {
            if (_shotStartT <= 0f || _s.ShooterIndex < 0) return;

            if (_s.Lapse != LapseType.None && _s.LapseDefenderIndex >= 0)
            {
                var kind = _s.Lapse switch
                {
                    LapseType.LateCloseout => NarrationBeatKind.LateCloseout,
                    LapseType.BlownRotation => NarrationBeatKind.BlownRotation,
                    _ => NarrationBeatKind.MissedHelp
                };
                var beat = Beat(Math.Max(0.1f, _shotStartT - 0.35f), kind,
                    _s.LapseDefenderIndex, _s.ShooterIndex);
                beat.ActorIsDefense = true;
            }

            if (_s.Deviation == OffensiveDeviation.HeroBall && _s.OffDeviatorIndex >= 0)
                Beat(Math.Max(0.1f, _shotStartT - 1.2f), NarrationBeatKind.HeroBall,
                    _s.OffDeviatorIndex);
        }

        /// <summary>Record a narration beat (presentational only — never a PossessionEvent).</summary>
        private NarrationBeat Beat(float t, NarrationBeatKind kind, int actor = -1, int target = -1)
        {
            var beat = new NarrationBeat
            {
                T = t,
                Kind = kind,
                ActorIndex = actor,
                TargetIndex = target,
                Action = _action?.Action ?? OffensiveAction.SpotUp
            };
            _beats.Add(beat);
            return beat;
        }

        // ════════════════════════════════════════════════════════════════
        //  Timeline construction
        // ════════════════════════════════════════════════════════════════

        private void BuildOffenseAndBall()
        {
            // Time reserved at the end of the live window for the shot sequence
            bool hasShot = _s.ShooterIndex >= 0 &&
                           (_s.Ending == ScriptEnding.MadeShot || _s.Ending == ScriptEnding.MissedShot ||
                            _s.Ending == ScriptEnding.BlockedShot || _s.Ending == ScriptEnding.AndOneShot ||
                            _s.Ending == ScriptEnding.ShootingFoulMissed);
            float shotFlight = hasShot ? BallFlight.ShotDuration(_s.ShotPosition, _rimX, _s.ShotType) : 0f;
            float windup = hasShot ? BallFlight.WindupFor(_s.ShotType) : 0f;

            // Buzzer possession: an end-of-quarter heave, or any shot that runs the clock to 0. The
            // ball must LEAVE THE HAND just before the buzzer with flight still in the air at 0 —
            // the normal 3s floor + shot-fit expansion would push the release seconds past it, so a
            // 1.5s catch-and-heave has to be authored honestly (release near _s.Duration, rim after).
            bool buzzer = hasShot && _s.Duration < 3f &&
                          (_s.ShotType == ShotType.Heave || _s.Duration >= _s.StartGameClock - 0.05f);

            // ── Phase A/B spots (needed early: the advance window is distance-derived) ──
            // Continuous flow: begin where the previous possession left every player (by PlayerId),
            // so possessions chain instead of teleporting to backcourt. Backcourt/null is the legacy
            // reset (safety net; MatchSimulationController also resets on subs/quarters/timeouts).
            var startSpots = StartSpots();
            var formation = FormationLibrary.GetHalfCourtSpots(_s.OffenseStrategy, _s.OffenseAttacksRight, _rng);

            float duration, advanceEnd;
            if (buzzer)
            {
                // Release ~0.1s before the buzzer; the rim (and any carom) lands after it. No bring-up
                // stretch — the shooter catches and launches. The Emit per-tick cap absorbs a long
                // opening leg if the start spot is far (presentational; decided timing is untouched).
                float releaseT = Math.Max(0.3f, _s.Duration - 0.1f);
                advanceEnd = Math.Min(_s.IsFastBreak ? 2f : 4f, Math.Max(0.2f, releaseT - windup - 0.1f));
                duration = releaseT + shotFlight;
            }
            else
            {
                duration = Math.Max(_s.Duration, 3f);
                advanceEnd = _s.IsFastBreak ? Math.Min(2f, duration * 0.35f) : Math.Min(4f, duration * 0.3f);

                // Cap the bring-up at a real sprint: arrival is derived from the actual distance, and
                // the PRESENTATION window stretches to fit rather than speeding anyone up. The display
                // clock maps Start→End proportionally over LiveSeconds, so game time stays honest.
                float maxAdvanceDist = 0f;
                for (int i = 0; i < 5; i++)
                {
                    float d = startSpots[i].DistanceTo(formation[i]);
                    if (d > maxAdvanceDist) maxAdvanceDist = d;
                }
                advanceEnd = Math.Max(advanceEnd, maxAdvanceDist / AdvanceAvg);
                duration = Math.Max(duration, advanceEnd + (hasShot ? 1.6f + windup + shotFlight : 1.0f));
            }

            _liveEndT = duration;
            float shotReleaseT = duration - shotFlight;          // ball reaches rim at ~duration
            _shotStartT = hasShot ? shotReleaseT : -1f;
            _shotWindup = hasShot ? windup : 0f;
            _shotRimT = hasShot ? shotReleaseT + shotFlight : -1f;
            float actionEnd = hasShot ? Math.Max(advanceEnd + 0.5f, shotReleaseT - windup) : duration;

            // Opening phase: a live rebound/steal is a Transition push; a dead ball is an Inbound
            // lead-in (BuildStartBall hands back to Advance once the ball is caught). Otherwise the
            // legacy Advance. Marks are sorted by time at the end of this method for PhaseAt.
            var startKind = _s.StartContext?.Kind ?? PossessionStartKind.Backcourt;
            var openPhase = startKind == PossessionStartKind.LiveRebound ? PossessionPhase.Transition
                          : IsInboundKind(startKind) ? PossessionPhase.Inbound
                          : PossessionPhase.Advance;
            _phaseMarks.Add((0f, openPhase));
            _phaseMarks.Add((advanceEnd, PossessionPhase.SetOffense));
            _phaseMarks.Add((Math.Min(advanceEnd + 1f, actionEnd), PossessionPhase.Action));
            if (hasShot) _phaseMarks.Add((shotReleaseT - windup, PossessionPhase.Shot));

            // ── Phase A/B: backcourt → formation ──
            for (int i = 0; i < 5; i++)
            {
                _offense[i] = new Track(startSpots[i]);
                float arrive = advanceEnd + 0.4f * (float)_rng.NextDouble();
                var action = i == _s.InitialBallHandlerIndex ? PlayerAction.Dribbling
                           : _s.IsFastBreak ? PlayerAction.Sprinting : PlayerAction.Running;
                _offense[i].Add(arrive, formation[i], action);
            }

            // ── Phase C: run a recognizable action ──
            float actionStart = advanceEnd + 0.6f;
            float cursor = actionStart;

            // Ball lead-in: how the ball gets into the initial handler's hands to start the set.
            // Legacy backcourt dribbles it up; a live rebound/steal outlets and pushes; a dead ball
            // walks an inbounder behind the line and passes in (clock held until the catch). Every
            // branch ends with the initial handler dribbling into the action at actionStart.
            int holder = _s.InitialBallHandlerIndex;
            BuildStartBall(holder, advanceEnd, actionStart, startSpots);

            // Off-ball movement of the action (screens, rolls/pops, cuts, post-ups). Records
            // beat times on _action for the defense to react to.
            BuildAction(holder, actionStart, actionEnd, formation);

            // Keep every off-ball player in constant motion (relocations/cuts) so no one stands.
            // Runs BEFORE ball movement so passes sample players' final positions.
            BuildContinuousSpacing(advanceEnd, actionEnd);

            // Ball movement: swings + optional interior touch, always returning the ball to the
            // initial handler before the decided assist so assist credit matches the visual.
            holder = BuildBallProgression(holder, ref cursor, actionEnd, formation);

            if (hasShot)
            {
                BuildShotSequence(holder, actionEnd, shotReleaseT, windup, shotFlight);
            }
            else
            {
                BuildNonShotEnding(holder);
            }

            // Tail: micro-sway through the resolution so dots never look frozen.
            for (int i = 0; i < 5; i++)
                _offense[i].SwayUntil(_totalT, _rng);

            // Densification hints: collect sharp offensive direction changes (>90° between two
            // real legs) so Emit can sample those cuts at the fine tick. Long fast legs only —
            // this keeps the extra frame budget small (presentational; no outcome effect).
            for (int i = 0; i < 5; i++)
                _offense[i].CollectSharpTurns(_sharpCutTimes, minLegDist: 6f, minAngleDeg: 95f);
            _sharpCutTimes.Sort();

            // BuildStartBall may append a lead-in phase mark (inbound → Advance hand-off) out of
            // time order; PhaseAt takes the last mark in list order, so keep them time-sorted.
            _phaseMarks.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        }

        // ════════════════════════════════════════════════════════════════
        //  Continuous-flow openings (Phase 1): where players/ball start each possession
        // ════════════════════════════════════════════════════════════════

        private static bool IsInboundKind(PossessionStartKind k) =>
            k == PossessionStartKind.MadeBasketInbound || k == PossessionStartKind.FreeThrowInbound ||
            k == PossessionStartKind.BaselineOob || k == PossessionStartKind.SidelineOob;

        /// <summary>Index of an offensive player by id, or -1.</summary>
        private int IndexOfOffense(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return -1;
            for (int i = 0; i < 5; i++)
                if (_s.Offense[i].PlayerId == playerId) return i;
            return -1;
        }

        /// <summary>Phase-A start spots. Backcourt/null → legacy backcourt spread. Every other kind
        /// seeds from the prior possession's final frame by PlayerId (continuity); missing ids fall
        /// back to the backcourt spot.</summary>
        private CourtPosition[] StartSpots()
        {
            var backcourt = FormationLibrary.GetBackcourtSpots(_s.OffenseAttacksRight, false, _rng);
            var ctx = _s.StartContext;
            if (ctx == null || ctx.Kind == PossessionStartKind.Backcourt) return backcourt;

            var spots = new CourtPosition[5];
            for (int i = 0; i < 5; i++)
                spots[i] = ctx.PositionOf(_s.Offense[i].PlayerId) ?? backcourt[i];
            return spots;
        }

        /// <summary>Author the ball's opening lead-in (t=0 → actionStart) per StartContext.Kind and
        /// set _clockStartT. Ends with the initial handler dribbling into the set at actionStart.
        /// Presentational only — never touches decided geometry.</summary>
        private void BuildStartBall(int holder, float advanceEnd, float actionStart, CourtPosition[] startSpots)
        {
            var ctx = _s.StartContext;

            if (ctx == null || ctx.Kind == PossessionStartKind.Backcourt)
            {
                float ballCursor = 0f;
                AddHeld(ref ballCursor, actionStart, holder, dribbled: true);
                Beat(advanceEnd * 0.5f, NarrationBeatKind.BringUp, holder);
                return;
            }

            int carrier = IndexOfOffense(ctx.BallCarrierId);

            if (!IsInboundKind(ctx.Kind))
            {
                // Live start (rebound / steal / loose ball): clock runs immediately.
                _clockStartT = 0f;
                _transitionEndT = ctx.Kind == PossessionStartKind.LiveRebound ? advanceEnd : 0f;

                if (carrier < 0 || carrier == holder)
                {
                    // Recovering player pushes it up himself: quick secure, then dribble.
                    float secure = Math.Min(0.5f, actionStart * 0.3f);
                    AddHeldFromTo(0f, secure, holder, dribbled: false);
                    AddHeldFromTo(secure, actionStart, holder, dribbled: true);
                    Beat(advanceEnd * 0.5f, NarrationBeatKind.BringUp, holder);
                }
                else
                {
                    // Rebounder/stealer secures, pivots, and outlets up-court to the handler.
                    float pivot = Math.Min(0.4f + 0.4f * (float)_rng.NextDouble(), actionStart * 0.4f);
                    AddHeldFromTo(0f, pivot, carrier, dribbled: false);
                    float catchT = LeadPass(carrier, holder, pivot, PassStyle.Chest, NarrationBeatKind.BringUp);
                    AddHeldFromTo(catchT, actionStart, holder, dribbled: true);
                }
                return;
            }

            // ── Dead-ball inbound (made basket / final FT / OOB turnover) ──
            int inbounder = InbounderIndex(holder, ctx.BallSpot, startSpots);
            var oob = ctx.Kind == PossessionStartKind.SidelineOob
                ? SidelineInbound(ctx.BallSpot)
                : NearestOutOfBounds(ctx.BallSpot);

            // Inbounder steps behind the line and holds the ball there until the pass. The lead-in
            // fits inside the advance window so the walk→hold→inbound keys stay ahead of the
            // formation-arrival key (ponytail: cosmetic jitter only if advanceEnd < ~0.8s, which an
            // inbound — always some bring-up — never hits).
            float lead = Math.Max(0.5f, Math.Min(advanceEnd - 0.4f, 1.6f));
            float walk = lead * 0.4f;
            float release = lead * 0.75f;
            _offense[inbounder].Add(walk, oob, PlayerAction.Inbounding);
            _offense[inbounder].Add(release, oob, PlayerAction.Inbounding);
            AddHeldFromTo(0f, release, inbounder, dribbled: false);

            // Inbound pass, led to where the handler is at the catch; clock starts on the catch.
            float catchIn = LeadPass(inbounder, holder, release, PassStyle.Chest, NarrationBeatKind.BringUp);
            _clockStartT = catchIn;
            _phaseMarks.Add((catchIn, PossessionPhase.Advance));
            AddHeldFromTo(catchIn, actionStart, holder, dribbled: true);
        }

        /// <summary>Author a led pass fromIdx→toIdx starting at startT (two-round lead-the-receiver
        /// fixed point, matching PassTo). Stamps Passing/Catching, pins the catch point on the
        /// receiver's track, records a beat. Returns the catch time.</summary>
        private float LeadPass(int fromIdx, int toIdx, float startT, PassStyle style, NarrationBeatKind beatKind)
        {
            var fromPos = _offense[fromIdx].PositionAt(startT);
            var to = _offense[toIdx].PositionAt(startT);
            float flight = BallFlight.PassDuration(fromPos, to);
            to = _offense[toIdx].PositionAt(startT + flight);
            flight = BallFlight.PassDuration(fromPos, to);
            to = _offense[toIdx].PositionAt(startT + flight);

            Beat(startT, beatKind, toIdx);
            _offense[fromIdx].Stamp(startT, PlayerAction.Passing, _s.Offense[toIdx].PlayerId);
            _ball.Add(new PassSegment(startT, startT + flight, fromPos, to, style));
            _offense[toIdx].Add(startT + flight, to, PlayerAction.Catching);
            return startT + flight;
        }

        /// <summary>Pick the inbounder: the offensive player (not the handler) whose start spot is
        /// nearest the ball's dead spot — i.e. the man already closest to the baseline.</summary>
        private int InbounderIndex(int holder, CourtPosition ballSpot, CourtPosition[] startSpots)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                if (i == holder) continue;
                float d = startSpots[i].DistanceTo(ballSpot);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best < 0 ? (holder == 0 ? 1 : 0) : best;
        }

        /// <summary>Nearest sideline inbound point to a dead-ball spot (just past the sideline).</summary>
        private static CourtPosition SidelineInbound(CourtPosition from)
        {
            float y = from.Y >= 0f ? CourtGeometry.HalfWidth + 1.5f : -(CourtGeometry.HalfWidth + 1.5f);
            float x = Math.Min(Math.Max(from.X, -CourtGeometry.HalfLength + 3f), CourtGeometry.HalfLength - 3f);
            return new CourtPosition(x, y);
        }

        /// <summary>
        /// Move the ball for the possession. On-ball actions (PnR/PnP/iso/DHO/backdoor) keep the
        /// ball with the initial handler — the single decided pass is fired later by
        /// BuildShotSequence. Off-ball actions reverse the ball and, on a post-up, feed the post
        /// and kick back out. Always ends with the ball back at the initial handler so the decided
        /// assist credit matches the visual. Returns the final holder.
        /// </summary>
        private int BuildBallProgression(int holder, ref float cursor, float actionEnd, CourtPosition[] formation)
        {
            int initial = holder;

            // On-ball actions: the handler keeps it (BuildShotSequence bridges the held ball).
            if (IsOnBallAction(_action.Action))
                return holder;

            const float reserve = 1.4f;                 // time kept back for the return pass
            float budgetEnd = actionEnd - reserve;
            var pool = PerimeterPool(initial);

            // Cosmetic perimeter reversals
            int swings = _rng.Next(0, 3);               // 0-2
            for (int p = 0; p < swings && cursor + 1.5f < budgetEnd; p++)
            {
                int target = PickDifferent(pool, holder);
                if (target < 0) break;
                holder = PassTo(holder, target, ref cursor);
            }

            // Interior touch on a post-up: feed the post (bounce pass, mostly), hold a beat,
            // kick back out.
            if (_action.InteriorTouch && _action.PostIndex >= 0 && _action.PostIndex != holder &&
                cursor + 2.2f < budgetEnd)
            {
                int post = _action.PostIndex;
                var feedStyle = _rng.NextDouble() < 0.6 ? PassStyle.Bounce : PassStyle.Chest;
                holder = PassTo(holder, post, ref cursor, feedStyle, NarrationBeatKind.PostFeed);
                cursor += 0.6f + (float)_rng.NextDouble() * 0.4f;   // post beat
                AddHeldSegmentEndingAt(cursor, holder);
                int kick = PickDifferent(pool, post);
                if (kick >= 0 && cursor + 1.2f < budgetEnd)
                    holder = PassTo(holder, kick, ref cursor, PassStyle.Chest, NarrationBeatKind.KickOut);
            }

            // Always return the ball to the initial handler before the decided assist.
            if (holder != initial)
                holder = PassTo(holder, initial, ref cursor);

            return holder;
        }

        /// <summary>Fire a pass holder→target at the current cursor, advance the cursor past the
        /// catch + a short hold, and leave the ball held by the target. Returns the new holder.
        /// The pass is LED to where the catcher will be at arrival — spacers keep relocating, and
        /// a pass to their old spot would teleport the ball onto the moved player at the catch.</summary>
        private int PassTo(int holder, int target, ref float cursor, PassStyle style = PassStyle.Chest,
            NarrationBeatKind beatKind = NarrationBeatKind.SwingPass)
        {
            var from = _offense[holder].PositionAt(cursor);
            // Two-round fixed point: estimate flight from the current spot, then re-aim at where
            // the catcher's track puts them at that arrival time (tracks are already fully laid).
            var to = _offense[target].PositionAt(cursor);
            float flight = BallFlight.PassDuration(from, to);
            to = _offense[target].PositionAt(cursor + flight);
            flight = BallFlight.PassDuration(from, to);
            to = _offense[target].PositionAt(cursor + flight);

            Beat(cursor, beatKind, holder, target);
            _offense[holder].Stamp(cursor, PlayerAction.Passing, _s.Offense[target].PlayerId);
            _ball.Add(new PassSegment(cursor, cursor + flight, from, to, style));
            // Pin the catcher at the catch point so the track and the ball agree at the hand-off.
            _offense[target].Add(cursor + flight, to, PlayerAction.Catching);

            cursor += flight + 0.5f + (float)_rng.NextDouble() * 0.4f;
            AddHeldSegmentEndingAt(cursor, target);
            return target;
        }

        /// <summary>Perimeter passing pool: PG/SG/SF/PF minus the deep post (bigs get the ball via
        /// the roll/post feed, not random reversals).</summary>
        private System.Collections.Generic.List<int> PerimeterPool(int initial)
        {
            var pool = new System.Collections.Generic.List<int> { 0, 1, 2, 3 };
            // The shooter and the deep post get the ball only via the decided assist / post feed —
            // never via a random reversal — so their positions can't be shifted after a pass is laid.
            if (_action.PostIndex >= 0) pool.Remove(_action.PostIndex);
            if (_s.ShooterIndex >= 0) pool.Remove(_s.ShooterIndex);
            if (!pool.Contains(initial)) pool.Add(initial);
            return pool;
        }

        private int PickDifferent(System.Collections.Generic.List<int> pool, int exclude)
        {
            if (pool.Count == 0) return -1;
            int start = _rng.Next(pool.Count);
            for (int k = 0; k < pool.Count; k++)
            {
                int cand = pool[(start + k) % pool.Count];
                if (cand != exclude) return cand;
            }
            return -1;
        }

        private static bool IsOnBallAction(OffensiveAction a) =>
            a == OffensiveAction.PickAndRoll || a == OffensiveAction.PickAndPop ||
            a == OffensiveAction.Isolation || a == OffensiveAction.DribbleHandoff ||
            a == OffensiveAction.Backdoor;

        private float M => _s.OffenseAttacksRight ? 1f : -1f;

        // ════════════════════════════════════════════════════════════════
        //  Off-ball action choreography (screens, rolls/pops, cuts, post-ups)
        // ════════════════════════════════════════════════════════════════

        private void BuildAction(int handler, float actionStart, float actionEnd, CourtPosition[] formation)
        {
            switch (_action.Action)
            {
                case OffensiveAction.PickAndRoll:       BuildScreenAction(handler, actionStart, roll: true); break;
                case OffensiveAction.PickAndPop:        BuildScreenAction(handler, actionStart, roll: false); break;
                case OffensiveAction.OffBallScreenCurl: BuildOffBallScreen(actionStart); break;
                case OffensiveAction.Backdoor:          BuildBackdoor(actionStart); break;
                case OffensiveAction.PostUp:            BuildPostUp(actionStart); break;
                case OffensiveAction.DribbleHandoff:    BuildHandoff(handler, actionStart); break;
                case OffensiveAction.Isolation:         BuildIsolation(handler, actionStart, actionEnd); break;
                case OffensiveAction.Transition:        BuildTransitionAction(actionStart); break;
                case OffensiveAction.SpotUp:            break; // movement from spacing + shot sequence
            }
        }

        private void BuildScreenAction(int handler, float actionStart, bool roll)
        {
            int scr = _action.Screener;
            if (scr < 0 || scr == handler) return;

            // Screener comes up to set the ball screen; handler dribbles to use it.
            _action.ScreenStartT = Glide(scr, actionStart, _action.ScreenSpot, PlayerAction.Screening);
            Glide(handler, actionStart + 0.2f,
                _action.ScreenSpot.MoveTowards(new CourtPosition(_rimX, 0f), 3f), PlayerAction.Dribbling);

            float holdEnd = _action.ScreenStartT + 0.7f;   // 0.7s of genuine body contact
            _action.ScreenEndT = holdEnd;
            _offense[scr].Stamp(holdEnd, PlayerAction.Screening);

            // Roll to the rim or pop to the elbow/arc.
            _action.RollStartT = holdEnd + 0.1f;
            var dest = roll ? _action.RollTarget : _action.PopTarget;
            Glide(scr, _action.RollStartT, dest, roll ? PlayerAction.Cutting : PlayerAction.Catching);

            Beat(_action.ScreenStartT, NarrationBeatKind.ScreenSet, scr, handler);
            Beat(_action.ScreenEndT, NarrationBeatKind.Drive, handler);
            Beat(_action.RollStartT, NarrationBeatKind.Roll, scr);
        }

        private void BuildOffBallScreen(float actionStart)
        {
            int scr = _action.Screener, cut = _action.Cutter;
            if (cut < 0) return;

            if (scr >= 0)
            {
                var screenPos = FormationLibrary.Clamp(new CourtPosition((CourtGeometry.RimX - 8f) * M, _action.SideY * 7f));
                _action.ScreenStartT = Glide(scr, actionStart, screenPos, PlayerAction.Screening);
                _action.ScreenEndT = _action.ScreenStartT + 0.7f;   // 0.7s of genuine body contact
                _offense[scr].Stamp(_action.ScreenEndT, PlayerAction.Screening);   // hold through contact
                Beat(_action.ScreenStartT, NarrationBeatKind.ScreenSet, scr, cut);
            }

            // Cutter starts low on the weak side, then curls up off the screen to the catch.
            var low = FormationLibrary.Clamp(new CourtPosition((CourtGeometry.RimX - 4f) * M, -_action.SideY * 6f));
            float t1 = Glide(cut, actionStart, low, PlayerAction.Cutting);
            Glide(cut, t1 + 0.1f, _s.ShotPosition, PlayerAction.Cutting);
            Beat(t1 + 0.1f, NarrationBeatKind.Curl, cut);
        }

        private void BuildBackdoor(float actionStart)
        {
            int cut = _action.Cutter;
            if (cut < 0) return;
            var high = FormationLibrary.Clamp(new CourtPosition(30f * M, _action.SideY * 16f));
            float t1 = Glide(cut, actionStart, high, PlayerAction.Cutting);
            Glide(cut, t1 + 0.3f, _s.ShotPosition, PlayerAction.Cutting);   // backdoor to the rim
            Beat(t1 + 0.3f, NarrationBeatKind.BackdoorCut, cut);
        }

        private void BuildPostUp(float actionStart)
        {
            int post = _action.PostIndex;
            if (post < 0) return;
            var block = FormationLibrary.Clamp(new CourtPosition((CourtGeometry.RimX - 6f) * M, _action.SideY * 6f));
            float t1 = Glide(post, actionStart, block, PlayerAction.PostingUp);
            Beat(t1 + 0.4f, NarrationBeatKind.PostMove, post);

            // Back down: 3 small rhythmic push-in steps toward the rim (~0.4 ft each over ~1.5s).
            // The defender holds ground at body-contact distance (IntegrateDefense post branch).
            _action.PostStartT = t1;
            var p = block;
            float pt = t1;
            var rimLine = new CourtPosition(_rimX, block.Y);
            for (int k = 0; k < 3; k++)
            {
                p = p.MoveTowards(rimLine, 0.4f);
                pt += 0.5f;
                _offense[post].Add(pt, FormationLibrary.Clamp(p), PlayerAction.PostingUp);
            }
            _action.PostEndT = pt;
        }

        private void BuildHandoff(int handler, float actionStart)
        {
            // Dribble to the hand-off spot just outside where the shooter will catch; the
            // shooter cuts in to meet him there. The Handoff beat is emitted by
            // BuildShotSequence AT the exchange — narrating it here (on arrival) called the
            // hand-off seconds before the ball actually moved.
            var spot = FormationLibrary.Clamp(new CourtPosition(_s.ShotPosition.X - 2f * M, _s.ShotPosition.Y));
            float t1 = Glide(handler, actionStart + 0.2f, spot, PlayerAction.Dribbling);

            float ts = t1;
            if (_s.ShooterIndex >= 0 && _s.ShooterIndex != handler)
            {
                var meet = FormationLibrary.Clamp(new CourtPosition(spot.X + 1.5f * M, spot.Y + 1f));
                ts = Glide(_s.ShooterIndex, actionStart + 0.2f, meet, PlayerAction.Cutting);
            }

            _action.HandoffT = Math.Max(t1, ts) + 0.1f;
            _action.HandoffSpot = spot;
        }

        private void BuildIsolation(int handler, float actionStart, float actionEnd)
        {
            var iso = _s.ShooterIndex >= 0 ? _s.ShotPosition : _offense[handler].PositionAt(actionStart);
            float t = actionStart + 0.3f;
            Beat(t, NarrationBeatKind.IsoJab, handler);
            for (int k = 0; k < 3 && t + 0.7f < actionEnd; k++)
            {
                var jab = FormationLibrary.Jitter(iso, 2.5f, _rng);
                t = Glide(handler, t, jab, PlayerAction.Dribbling, speed: JabAvg);
                t += 0.25f;
            }
        }

        private void BuildTransitionAction(float actionStart)
        {
            int runner = PickBigNot(_action.Handler);
            if (runner >= 0)
                Glide(runner, actionStart,
                    FormationLibrary.Clamp(new CourtPosition((CourtGeometry.RimX - 5f) * M, _action.SideY * 3f)),
                    PlayerAction.Sprinting, SprintAvg);
        }

        // ── Continuous off-ball spacing so no one stands still ──

        private void BuildContinuousSpacing(float advanceEnd, float actionEnd)
        {
            var involved = new System.Collections.Generic.HashSet<int>();
            if (_s.ShooterIndex >= 0) involved.Add(_s.ShooterIndex);
            if (_action.Screener >= 0) involved.Add(_action.Screener);
            if (_action.Cutter >= 0) involved.Add(_action.Cutter);
            if (_action.PostIndex >= 0) involved.Add(_action.PostIndex);
            if (IsOnBallAction(_action.Action)) involved.Add(_action.Handler);

            var slots = SpacingSlots();

            for (int i = 0; i < 5; i++)
            {
                if (involved.Contains(i)) continue;
                float t = advanceEnd + 0.8f + (float)_rng.NextDouble() * 0.5f;
                while (t + 1.0f < actionEnd)
                {
                    var cur = _offense[i].PositionAt(t);
                    var next = FormationLibrary.Jitter(NearbySlot(cur, slots), 1.5f, _rng);
                    // The ARRIVAL must stay inside the action window: an overshooting
                    // relocation key would interleave with later resolution/FT keys
                    // (box-outs, rebound spots, lane setup) and read as a teleport.
                    float span = Math.Max(0.25f, cur.DistanceTo(next) / RelocateAvg);
                    if (t + span > actionEnd - 0.1f) break;
                    float arrive = Glide(i, t, next, PlayerAction.Cutting, RelocateAvg);
                    t = arrive + 0.4f + (float)_rng.NextDouble() * 0.7f;
                }
            }
        }

        private System.Collections.Generic.List<CourtPosition> SpacingSlots()
        {
            float m = M;
            return new System.Collections.Generic.List<CourtPosition>
            {
                new CourtPosition(24f * m, 0f),
                new CourtPosition(29f * m, -16f),
                new CourtPosition(29f * m, 16f),
                new CourtPosition(42f * m, -22f),
                new CourtPosition(42f * m, 22f),
                new CourtPosition(30f * m, -7f),
                new CourtPosition(30f * m, 7f)
            };
        }

        private CourtPosition NearbySlot(CourtPosition cur, System.Collections.Generic.List<CourtPosition> slots)
        {
            var pick = slots[_rng.Next(slots.Count)];
            if (cur.DistanceTo(pick) < 4f) pick = slots[_rng.Next(slots.Count)];
            return pick;
        }

        private int PickBigNot(int exclude)
        {
            if (4 != exclude) return 4;
            if (3 != exclude) return 3;
            return -1;
        }

        /// <summary>Add a waypoint the player eases to, at a speed-capped arrival time so smoothstep's
        /// 1.5× peak stays under the ChoreographyTest per-tick bound. Returns the arrival time.</summary>
        private float Glide(int i, float fromT, CourtPosition to, PlayerAction action, float speed = CutAvg)
        {
            if (speed > SprintAvg) speed = SprintAvg;
            var from = _offense[i].PositionAt(fromT);
            to = FormationLibrary.Clamp(to);
            float dist = from.DistanceTo(to);
            float span = Math.Max(0.25f, dist / speed);
            float arriveT = fromT + span;
            _offense[i].Add(arriveT, to, action);
            return arriveT;
        }

        private void BuildShotSequence(int holder, float actionEnd, float shotReleaseT, float windup, float shotFlight)
        {
            int shooter = _s.ShooterIndex;

            // Decided pass to the shooter (the assist) — shooter catches at/near the shot spot
            if (_s.BallHandlerPassedToShooter && holder != shooter &&
                _action.Action == OffensiveAction.DribbleHandoff && _action.HandoffT > 0f)
            {
                // Dribble hand-off: the decided assist IS the exchange — one short pass at the
                // meeting spot, called once, exactly when the ball changes hands.
                float exch = Math.Max(Math.Min(_action.HandoffT, shotReleaseT - windup - 0.5f), _phaseMarks[2].t);
                var from = _offense[holder].PositionAt(exch);
                var to = _offense[shooter].PositionAt(exch + 0.2f);

                _offense[holder].Stamp(exch, PlayerAction.Passing, _s.Offense[shooter].PlayerId);
                EndHeldAt(exch);
                _ball.Add(new PassSegment(exch, exch + 0.2f, from, to, PassStyle.Chest));
                _offense[shooter].Stamp(exch + 0.2f, PlayerAction.Catching);
                Beat(exch, NarrationBeatKind.Handoff, holder, shooter);   // the ONLY call for this pass

                // Shooter works to the shot spot with the ball; handler clears out.
                _offense[shooter].Add(shotReleaseT - 0.15f, _s.ShotPosition, PlayerAction.Dribbling);
                AddHeldFromTo(exch + 0.2f, shotReleaseT, shooter, dribbled: true);
                Glide(holder, exch + 0.3f, FormationLibrary.Clamp(
                    new CourtPosition(_s.ShotPosition.X - 8f * M, -_s.ShotPosition.Y * 0.5f)), PlayerAction.Running);
            }
            else if (_s.BallHandlerPassedToShooter && holder != shooter)
            {
                float passT;
                if (_s.ShotType == ShotType.CatchAndShoot)
                {
                    // Catch-and-shoot: no dribble/gather. Land the catch ~0.35s before the
                    // (fixed) release so the shot goes up in rhythm right off the pass.
                    var est = _offense[holder].PositionAt(Math.Max(shotReleaseT - 0.5f, _phaseMarks[2].t));
                    float estFlight = BallFlight.PassDuration(est, _s.ShotPosition);
                    passT = Math.Max(shotReleaseT - estFlight - 0.35f, _phaseMarks[2].t);
                }
                else
                {
                    passT = Math.Max(actionEnd - 0.6f, _phaseMarks[2].t);
                }
                // Shooter relocates to the catch spot just before the pass
                _offense[shooter].Add(passT, _s.ShotPosition, PlayerAction.Cutting);

                var from = _offense[holder].PositionAt(passT);
                float flight = BallFlight.PassDuration(from, _s.ShotPosition);

                // Assist style: backdoor cuts get a lob; pocket passes to a rolling/posting big
                // are mostly bounce passes; everything else is a chest pass.
                var assistStyle = PassStyle.Chest;
                if (_action.Action == OffensiveAction.Backdoor)
                    assistStyle = PassStyle.Lob;
                else if (shooter >= 3 && IsDriveShot() && _rng.NextDouble() < 0.6)
                    assistStyle = PassStyle.Bounce;

                _offense[holder].Stamp(passT, PlayerAction.Passing, _s.Offense[shooter].PlayerId);
                EndHeldAt(passT);
                _ball.Add(new PassSegment(passT, passT + flight, from, _s.ShotPosition, assistStyle));
                _offense[shooter].Stamp(passT + flight, PlayerAction.Catching);
                Beat(passT, NarrationBeatKind.AssistPass, holder, shooter);

                AddHeldFromTo(passT + flight, shotReleaseT, shooter, dribbled: false);
            }
            else
            {
                // Shooter has the ball. Drives go rim-ward; jumpers step to the spot.
                var driveAction = IsDriveShot() ? PlayerAction.Sprinting : PlayerAction.Dribbling;
                float arriveT = shotReleaseT - 0.15f;
                _offense[shooter].Add(arriveT, _s.ShotPosition, driveAction);
                AddHeldFromCursorTo(shotReleaseT, shooter, dribbled: true);
                if (IsDriveShot())
                    Beat(Math.Max(actionEnd - 0.2f, shotReleaseT - 1f), NarrationBeatKind.Drive, shooter);
            }

            // The shot itself. ~25% of driving layups are flagged acrobatic (reverse/off-hand
            // finish) deterministically from the shooter+clock so the renderer picks a fancy clip.
            bool acrobatic = _s.ShotType == ShotType.Layup && IsDriveShot() && IsAcrobaticLayup();
            var shotAction = _s.ShotType == ShotType.Dunk ? PlayerAction.Dunking
                            : acrobatic ? PlayerAction.LayupAcrobatic
                            : _s.ShotType == ShotType.Layup || _s.ShotType == ShotType.TipIn ? PlayerAction.Layup
                            : PlayerAction.Shooting;
            _offense[shooter].Add(shotReleaseT, _s.ShotPosition, shotAction);

            // Fadeaway / step-back: author a small backward-drift leg during the windup so the
            // silhouette reads even without clips — the shooter gathers slightly toward the rim,
            // then drifts back OUT to the (fixed) shot spot at release. Ball stays glued (held).
            // Presentational only: release instant, flight, and outcome are untouched.
            if (_s.ShotType == ShotType.Fadeaway || _s.ShotType == ShotType.StepBack)
            {
                float back = _s.ShotType == ShotType.StepBack ? 3.0f : 1.75f;
                var gather = _s.ShotPosition.MoveTowards(new CourtPosition(_rimX, 0f), back);
                _offense[shooter].Add(shotReleaseT - windup, gather, PlayerAction.Dribbling);
            }

            // Dunk carry: the dot travels WITH the ball to the rim over the short thrust,
            // so the slam reads as carried, not launched.
            if (_s.ShotType == ShotType.Dunk)
            {
                var rimApproach = _s.ShotPosition.MoveTowards(new CourtPosition(_rimX, 0f),
                    Math.Max(0f, _s.ShotPosition.DistanceTo(new CourtPosition(_rimX, 0f)) - 2f));
                _offense[shooter].Add(shotReleaseT + shotFlight, rimApproach, PlayerAction.Dunking);
            }

            bool made = _s.Ending == ScriptEnding.MadeShot || _s.Ending == ScriptEnding.AndOneShot;
            bool blocked = _s.Ending == ScriptEnding.BlockedShot;

            // The windup call ("Pull-up from the elbow...") — held until the result lands.
            // IsThree uses THE scoring definition, so "from deep" is said only when it pays 3.
            var windupBeat = Beat(shotReleaseT - windup, NarrationBeatKind.ShotWindup, shooter);
            windupBeat.ShotType = _s.ShotType;
            windupBeat.ContestLevel = _s.ContestLevel;
            windupBeat.IsThree = _s.ShotPosition.IsThreePointShot(_s.OffenseAttacksRight);

            if (blocked)
            {
                // Ball leaves the hand, gets swatted a few feet out, recovered by the rebounder
                var recovery = FormationLibrary.Jitter(
                    NearPoint(_s.ShotPosition, 9f), 2f, _rng);
                _reboundSpot = recovery;
                float deflectEnd = shotReleaseT + 0.6f;
                _ball.Add(new BlockSegment(shotReleaseT, deflectEnd, _s.ShotPosition, recovery));
                BuildReboundScramble(deflectEnd, recovery);
                _totalT = deflectEnd + 1.0f;
                _phaseMarks.Add((shotReleaseT, PossessionPhase.Resolution));

                Beat(shotReleaseT + 0.15f, NarrationBeatKind.RimResult, shooter);
                EmitReboundBeat(deflectEnd);
            }
            else
            {
                float rimT = shotReleaseT + shotFlight;
                float shotDist = _s.ShotPosition.DistanceTo(new CourtPosition(_rimX, 0f));

                // Full glass usage (presentational): layups live on the glass, close-range
                // misses come off glass-then-rim, the occasional midrange jumper banks in.
                // The bank occupies the SAME flight window, so rimT and everything keyed to
                // it (RimResult beat, MakeSegment, CaromSegment) is untouched.
                bool jumperType = _s.ShotType != ShotType.Dunk && _s.ShotType != ShotType.TipIn &&
                                  _s.ShotType != ShotType.Layup && _s.ShotType != ShotType.Floater &&
                                  _s.ShotType != ShotType.Hookshot && _s.ShotType != ShotType.Heave;
                bool bank = made
                    ? _s.ShotType == ShotType.Layup
                        || (_s.ShotType == ShotType.Floater && _rng.NextDouble() < 0.4)
                        || (jumperType && shotDist > 8f && shotDist < 18f && _rng.NextDouble() < 0.10)
                    : shotDist < 10f && _s.ShotType != ShotType.Dunk && _s.ShotType != ShotType.TipIn;

                if (bank)
                {
                    float side = _s.ShotPosition.Y >= 0.5f ? 1f
                               : _s.ShotPosition.Y <= -0.5f ? -1f
                               : (_rng.NextDouble() < 0.5 ? 1f : -1f);
                    float glassY = side * Math.Min(Math.Max(Math.Abs(_s.ShotPosition.Y) * 0.25f, 0.5f), 2f);
                    float hop = Math.Min(0.18f, shotFlight * 0.45f);
                    float uGlass = (shotFlight - hop) / shotFlight;
                    _ball.Add(new BankSegment(shotReleaseT, rimT, _s.ShotPosition, _rimX, glassY, uGlass, _s.ShotType));
                }
                else
                {
                    _ball.Add(new ShotSegment(shotReleaseT, rimT, _s.ShotPosition, _rimX, _s.ShotType));
                }

                var resultBeat = Beat(rimT, NarrationBeatKind.RimResult, shooter);
                resultBeat.ShotType = _s.ShotType;
                resultBeat.Made = made;
                resultBeat.IsThree = windupBeat.IsThree;
                if (made) resultBeat.PointsScored = FgPointsFromEvents();

                if (made)
                {
                    float netT = rimT + BallFlight.MakeFollowThroughFor(_s.ShotType);
                    _ball.Add(new MakeSegment(rimT, netT, _rimX, _s.ShotType));
                    _offense[shooter].Stamp(rimT + 0.1f, PlayerAction.Celebrating);
                    _ball.Add(new DeadSegment(netT, netT + 0.8f, new CourtPosition(_rimX, 0f)));
                    _totalT = netT + 0.8f;
                }
                else
                {
                    // Long misses carom along the actual shot line (front/back iron);
                    // close misses keep the generic scatter (they already banked in via glass).
                    _reboundSpot = shotDist >= 10f
                        ? BallFlight.ComputeReboundSpot(_s.ShotPosition, _rimX, _rng, frontIron: _rng.NextDouble() < 0.6)
                        : BallFlight.ComputeReboundSpot(_s.ShotPosition, _rimX, _rng);
                    float caromEnd = rimT + BallFlight.CaromDuration;
                    _ball.Add(new CaromSegment(rimT, caromEnd, _rimX, _reboundSpot));
                    BuildReboundScramble(caromEnd, _reboundSpot);
                    _totalT = caromEnd + 1.0f;
                    EmitReboundBeat(caromEnd);
                }
                _phaseMarks.Add((rimT, PossessionPhase.Resolution));
            }

            // Free throws appended after the live resolution
            if (_s.FreeThrows != null && _s.FreeThrows.Attempts > 0)
                BuildFreeThrows();
        }

        /// <summary>Actual FG points for the made shot — read from the decided event so score
        /// tags never guess. Falls back to the unified is-three rule (identical by construction).</summary>
        private int FgPointsFromEvents()
        {
            if (_s.Events != null)
            {
                for (int i = 0; i < _s.Events.Count; i++)
                {
                    if (_s.Events[i].Type == EventType.Shot && _s.Events[i].Outcome == EventOutcome.Success)
                        return _s.Events[i].PointsScored;
                }
            }
            return _s.ShotPosition.IsThreePointShot(_s.OffenseAttacksRight) ? 3 : 2;
        }

        /// <summary>Rebound narration beat (defense flag flips the roster lookup downstream).</summary>
        private void EmitReboundBeat(float t)
        {
            if (_s.RebounderIndex < 0) return;
            var beat = Beat(t, NarrationBeatKind.ReboundScramble, _s.RebounderIndex);
            beat.ActorIsDefense = _s.RebounderIsDefense;
        }

        /// <summary>Author a real rebound battle around the decided rebound spot: 2 offensive
        /// crashers ring the spot, each paired with his man (identity — the defense plan isn't built
        /// yet) who boxes him out one body-length toward the ball (mutual TargetPlayerId + BoxingOut
        /// drives the P2 contact rendering/lean). The sim-decided winner takes the board with the full
        /// Rebounding leap; the 1-2 nearest losers get an EARLY, SHORTER leap so the winner visibly
        /// out-jumps them. Winner identity, rebound spot, and outcome timing are all pre-decided.</summary>
        private void BuildReboundScramble(float ballArriveT, CourtPosition spot)
        {
            if (_s.RebounderIndex < 0) return;
            _reboundArriveT = ballArriveT;   // the winner's leap (presentational jump timing)

            int winner = _s.RebounderIndex;
            bool winnerDef = _s.RebounderIsDefense;

            _reboundCrashT = Math.Max(0f, ballArriveT - 0.8f);
            _reboundCrashSpots = new CourtPosition[5];
            _reboundCrashDef = new bool[5];
            _reboundOffPartner = new int[5];
            _reboundDefPartner = new int[5];
            for (int i = 0; i < 5; i++) { _reboundOffPartner[i] = -1; _reboundDefPartner[i] = -1; }

            // Decided winner secures the ball at the spot with the full leap (offense via a track key;
            // a defensive winner is driven to _reboundSpot by IntegrateDefense's defenseRebounds branch).
            if (!winnerDef)
                _offense[winner].Add(ballArriveT, spot, PlayerAction.Rebounding);

            var offCrashers = NearestOffense(spot, _reboundCrashT, 2);
            var losers = new List<(bool isDefense, int idx, float distToSpot)>();

            int ringK = 0;
            foreach (int oi in offCrashers)
            {
                var ring = RingPoint(spot, ringK++, offCrashers.Count);

                // Offensive crasher (unless he's the winner, already Rebounding at the spot).
                if (!(!winnerDef && oi == winner))
                {
                    _offense[oi].Add(ballArriveT, ring, PlayerAction.BoxingOut, _s.Defense[oi].PlayerId);
                    _reboundOffPartner[oi] = oi;   // identity man
                    losers.Add((false, oi, ring.DistanceTo(spot)));
                }

                // His man boxes him out on the rim side (skip if that defender IS the winner).
                int di = oi;
                if (!(winnerDef && di == winner))
                {
                    _reboundCrashSpots[di] = ring.MoveTowards(spot, ScreenContactDist);
                    _reboundCrashDef[di] = true;
                    _reboundDefPartner[di] = oi;
                    losers.Add((true, di, _reboundCrashSpots[di].DistanceTo(spot)));
                }
            }

            // 1-2 nearest losers leap EARLY and SHORTER so the decided winner out-jumps them.
            losers.Sort((a, b) => a.distToSpot.CompareTo(b.distToSpot));
            int leapers = Math.Min(2, losers.Count);
            for (int k = 0; k < leapers; k++)
                _extraLeaps.Add((losers[k].isDefense, losers[k].idx, ballArriveT - 0.30f, 1.3f));

            _ball.Add(new DeadSegment(ballArriveT, ballArriveT + 1.0f, spot));
        }

        /// <summary>Indices of the <paramref name="count"/> offensive players nearest a spot at time t.</summary>
        private List<int> NearestOffense(CourtPosition spot, float t, int count)
        {
            var idx = new List<int> { 0, 1, 2, 3, 4 };
            idx.Sort((a, b) => _offense[a].PositionAt(t).DistanceTo(spot)
                .CompareTo(_offense[b].PositionAt(t).DistanceTo(spot)));
            return idx.GetRange(0, Math.Min(count, idx.Count));
        }

        /// <summary>A deterministically-jittered point on a 1.5–3 ft ring around the spot (seeded rng),
        /// evenly spaced by slot so crashers don't stack.</summary>
        private CourtPosition RingPoint(CourtPosition spot, int k, int total)
        {
            double ang = (total > 0 ? 2.0 * Math.PI * k / total : 0.0) + _rng.NextDouble() * 0.8;
            float r = 1.5f + (float)_rng.NextDouble() * 1.5f;
            return FormationLibrary.Clamp(new CourtPosition(
                spot.X + (float)Math.Cos(ang) * r,
                spot.Y + (float)Math.Sin(ang) * r));
        }

        private void BuildNonShotEnding(int holder)
        {
            float endT = _liveEndT;

            switch (_s.Ending)
            {
                case ScriptEnding.Steal:
                {
                    // The shooter's defender jumps a pass: throw a doomed pass, defender takes it
                    int shooter = Math.Max(_s.ShooterIndex, 0);
                    float passT = Math.Max(endT - 0.8f, 0.5f);
                    var from = _offense[holder].PositionAt(passT);
                    var to = _offense[shooter].PositionAt(passT);
                    var midpoint = new CourtPosition((from.X + to.X) / 2f, (from.Y + to.Y) / 2f);

                    _offense[holder].Stamp(passT, PlayerAction.Passing);
                    EndHeldAt(passT);
                    float interceptT = passT + BallFlight.PassDuration(from, midpoint);
                    _ball.Add(new PassSegment(passT, interceptT, from, midpoint));
                    _ball.Add(new DeadSegment(interceptT, interceptT + 0.8f, midpoint));
                    _reboundSpot = midpoint; // defense converges here
                    _totalT = interceptT + 0.8f;
                    // The decided stealer is the SHOOTER'S DEFENDER (event actor) — not the passer.
                    var stealBeat = Beat(interceptT, NarrationBeatKind.StealJump, shooter);
                    stealBeat.ActorIsDefense = true;
                    break;
                }
                case ScriptEnding.Turnover when _s.Turnover == TurnoverKind.LostHandle:
                {
                    // Lost handle: the ball squirts loose along the floor and the handler's
                    // defender breaks off to scoop it up.
                    float t0 = Math.Max(endT - 0.8f, 0.5f);
                    EndHeldAt(t0);
                    var from = _offense[holder].PositionAt(t0);
                    var spot = NearPoint(from, 7f);
                    float dur = BallFlight.PassDuration(from, spot);
                    _offense[holder].Stamp(t0, PlayerAction.Idle);
                    _ball.Add(new PassSegment(t0, t0 + dur, from, spot, PassStyle.Bounce));
                    _ball.Add(new DeadSegment(t0 + dur, t0 + dur + 1.0f, spot));
                    _reboundSpot = spot;
                    _looseBallT = t0;
                    _looseBallDefIdx = holder;   // his man reacts first
                    _totalT = t0 + dur + 1.0f;
                    var lbBeat = Beat(t0 + dur + 0.4f, NarrationBeatKind.LooseBallTurnover, holder);
                    lbBeat.Turnover = _s.Turnover;
                    break;
                }
                case ScriptEnding.Turnover when _s.Turnover == TurnoverKind.Traveled ||
                                                _s.Turnover == TurnoverKind.OffensiveFoul:
                {
                    // Whistle: play freezes, ball goes dead in the handler's hands.
                    EndHeldAt(endT - 0.3f);
                    var pos = _offense[holder].PositionAt(endT - 0.3f);
                    _ball.Add(new DeadSegment(endT - 0.3f, endT + 0.7f, pos));
                    _offense[holder].Stamp(endT - 0.3f, PlayerAction.Idle);
                    _totalT = endT + 0.7f;
                    var whistleBeat = Beat(endT - 0.3f, NarrationBeatKind.Violation, holder);
                    whistleBeat.Turnover = _s.Turnover;
                    break;
                }
                case ScriptEnding.Turnover:
                {
                    // Errant pass: the ball sails out of bounds off the nearest line.
                    float throwT = Math.Max(endT - 0.5f, 0.5f);
                    EndHeldAt(throwT);
                    var from = _offense[holder].PositionAt(throwT);
                    var oob = NearestOutOfBounds(from);
                    _offense[holder].Stamp(throwT, PlayerAction.Passing);
                    float arriveT = throwT + BallFlight.PassDuration(from, oob);
                    _ball.Add(new PassSegment(throwT, arriveT, from, oob));
                    _ball.Add(new DeadSegment(arriveT, arriveT + 0.8f, oob));
                    _totalT = arriveT + 0.8f;
                    var oobBeat = Beat(arriveT, NarrationBeatKind.OobTurnover, holder);
                    oobBeat.Turnover = _s.Turnover;
                    break;
                }
                default: // Violation: whistle, dead ball in place
                {
                    EndHeldAt(endT - 0.3f);
                    var pos = _offense[holder].PositionAt(endT - 0.3f);
                    _ball.Add(new DeadSegment(endT - 0.3f, endT + 0.7f, pos));
                    _offense[holder].Stamp(endT - 0.3f, PlayerAction.Idle);
                    _totalT = endT + 0.7f;
                    Beat(endT - 0.3f, NarrationBeatKind.Violation, holder);
                    break;
                }
            }
            // Whistle turnovers / violations freeze into a dead ball (the next possession inbounds
            // it — Phase 1). A live steal / lost handle stays a live Resolution transition.
            bool deadWhistle = _s.Ending == ScriptEnding.Turnover
                ? _s.Turnover != TurnoverKind.LostHandle          // Traveled / OffensiveFoul / errant-OOB
                : _s.Ending != ScriptEnding.Steal;                // Violation (default) whistle
            _phaseMarks.Add((endT - 0.3f, deadWhistle ? PossessionPhase.DeadBall : PossessionPhase.Resolution));
        }

        private void BuildFreeThrows()
        {
            int shooter = _s.ShooterIndex >= 0 ? _s.ShooterIndex : _s.InitialBallHandlerIndex;
            float setupStart = _totalT;

            _phaseMarks.Add((setupStart, PossessionPhase.FreeThrow));

            // NBA alignment roles: the bigs battle on the lane. Offense takes the two second
            // slots, defense the two blocks (nearest the rim, theirs by rule) + a third one
            // slot above the low-side offensive rebounder. Everyone else spaces past the arc.
            var laneOff = RankByBigness(_s.Offense, shooter);
            var defRank = RankByBigness(_s.Defense, -1);
            int oLane1 = laneOff[0];
            int oLane2 = laneOff.Length > 1 ? laneOff[1] : laneOff[0];

            var offSpots = FormationLibrary.GetFreeThrowOffenseSpots(
                _s.OffenseAttacksRight, shooter, oLane1, oLane2, _rng);
            _ftDefSpots = FormationLibrary.GetFreeThrowDefenseSpots(
                _s.OffenseAttacksRight, defRank[0], defRank[1], defRank[2], _rng);
            _ftLaneDefender = new bool[5];
            _ftLaneDefender[defRank[0]] = _ftLaneDefender[defRank[1]] = _ftLaneDefender[defRank[2]] = true;
            _ftStartT = setupStart;

            // Setup time derived from the farthest walk so nobody warps to their spot.
            float maxOffenseDist = 0f;
            for (int i = 0; i < 5; i++)
            {
                float d = _offense[i].PositionAt(setupStart).DistanceTo(offSpots[i]);
                if (d > maxOffenseDist) maxOffenseDist = d;
            }
            float setupEnd = setupStart + Math.Max(1.2f, maxOffenseDist / 16f);
            for (int i = 0; i < 5; i++)
                _offense[i].Add(setupEnd, offSpots[i], PlayerAction.Running);

            var ftLine = offSpots[shooter];
            float cursor = setupEnd + 0.3f;
            int made = _s.FreeThrows.Made;

            var setupBeat = Beat(setupStart, NarrationBeatKind.FreeThrowSetup, shooter);
            setupBeat.TargetIndex = _s.FreeThrows.Attempts;   // "to the line for {n}"

            float tailEnd = 0f;   // latest crash/retreat arrival to keep inside the timeline

            for (int a = 0; a < _s.FreeThrows.Attempts; a++)
            {
                bool thisMade = a < made; // presentational ordering: makes first
                bool isFinal = a == _s.FreeThrows.Attempts - 1;
                var ftBeat = Beat(cursor + 0.9f, NarrationBeatKind.FreeThrowAttempt, shooter);
                ftBeat.Made = thisMade;

                // Ball returns to the line (referee toss) from wherever it ended up
                var ballPos = SampleBall(cursor - 0.45f);
                _ball.Add(new PassSegment(cursor - 0.45f, cursor - 0.05f,
                    new CourtPosition(ballPos.X, ballPos.Y), ftLine));
                _ball.Add(new DeadSegment(cursor - 0.05f, cursor, ftLine));
                _offense[shooter].Stamp(cursor, PlayerAction.Shooting);

                float flight = 0.9f;
                _ball.Add(new ShotSegment(cursor, cursor + flight, ftLine, _rimX, null));

                if (thisMade)
                {
                    _ball.Add(new MakeSegment(cursor + flight, cursor + flight + 0.3f, _rimX));
                    _ball.Add(new DeadSegment(cursor + flight + 0.3f, cursor + flight + 0.9f, new CourtPosition(_rimX, 0f)));

                    if (isFinal)
                    {
                        // Ball game dead after the last make: the shooting team's shooter + arc
                        // players turn and retreat toward their own half for defensive balance.
                        float netT = cursor + flight + 0.3f;
                        for (int i = 0; i < 5; i++)
                        {
                            if (i == oLane1 || i == oLane2) continue;   // lane bodies walk off later
                            var cur = _offense[i].PositionAt(netT);
                            var back = FormationLibrary.Clamp(new CourtPosition(
                                8f * -M + 3f * ((float)_rng.NextDouble() - 0.5f), cur.Y * 0.6f));
                            float arrive = Glide(i, netT + 0.2f, back, PlayerAction.Running, SprintAvg);
                            if (arrive > tailEnd) tailEnd = arrive;
                        }
                    }
                }
                else
                {
                    var spot = BallFlight.ComputeReboundSpot(ftLine, _rimX, _rng);
                    _ball.Add(new CaromSegment(cursor + flight, cursor + flight + 0.5f, _rimX, spot));
                    _ball.Add(new DeadSegment(cursor + flight + 0.5f, cursor + flight + 0.9f, spot));

                    if (isFinal)
                    {
                        // Live miss: lane bodies crash the glass; the decided rebounder wins it.
                        _reboundSpot = spot;
                        _ftCrashT = cursor + flight;
                        _reboundArriveT = _ftCrashT + 0.5f;   // the rebound leap on the FT miss
                        _ftCrashSpots = new CourtPosition[5];
                        for (int i = 0; i < 5; i++)
                            _ftCrashSpots[i] = _ftLaneDefender[i] ? NearPoint(spot, 2.5f) : _ftDefSpots[i];

                        bool offenseRebounds = !_s.RebounderIsDefense && _s.RebounderIndex >= 0;
                        foreach (int idx in new[] { oLane1, oLane2 })
                        {
                            if (offenseRebounds && idx == _s.RebounderIndex) continue;
                            var laneSpot = _offense[idx].PositionAt(_ftCrashT);
                            var target = NearPoint(spot, 3f);
                            float arrive = _ftCrashT + Math.Max(0.6f, laneSpot.DistanceTo(target) / 12f);
                            _offense[idx].Add(arrive, target, PlayerAction.BoxingOut);
                            if (arrive > tailEnd) tailEnd = arrive;
                        }
                        if (offenseRebounds)
                        {
                            var from = _offense[_s.RebounderIndex].PositionAt(_ftCrashT);
                            float arrive = _ftCrashT + Math.Max(0.6f, from.DistanceTo(spot) / 12f);
                            _offense[_s.RebounderIndex].Add(arrive, spot, PlayerAction.Rebounding);
                            if (arrive > tailEnd) tailEnd = arrive;
                        }
                        else
                        {
                            // Defensive board (the opponent gets it): the shooting team's arc players
                            // turn and retreat toward their own half — mirrors the made-FT retreat so
                            // the next possession's LiveRebound start context has sensible prior spots.
                            float back0 = _ftCrashT + 0.6f;
                            for (int i = 0; i < 5; i++)
                            {
                                if (i == oLane1 || i == oLane2) continue;   // lane bodies box out above
                                var cur = _offense[i].PositionAt(back0);
                                var back = FormationLibrary.Clamp(new CourtPosition(
                                    8f * -M + 3f * ((float)_rng.NextDouble() - 0.5f), cur.Y * 0.6f));
                                float arrive = Glide(i, back0 + 0.2f, back, PlayerAction.Running, SprintAvg);
                                if (arrive > tailEnd) tailEnd = arrive;
                            }
                        }
                    }
                }

                cursor += flight + 1.0f;
            }

            _totalT = Math.Max(cursor + 0.3f, tailEnd + 0.2f);
        }

        /// <summary>Lineup indices ordered biggest-first — Position (C→PG), then height, then
        /// index — skipping <paramref name="exclude"/>. Deterministic: no RNG, so the same
        /// possession always picks the same lane bodies.</summary>
        private static int[] RankByBigness(Player[] five, int exclude)
        {
            var order = new List<int>(5);
            for (int i = 0; i < 5; i++)
                if (i != exclude) order.Add(i);
            order.Sort((a, b) =>
            {
                int byPos = ((int)five[b].Position).CompareTo((int)five[a].Position);
                if (byPos != 0) return byPos;
                int byHeight = five[b].HeightInches.CompareTo(five[a].HeightInches);
                if (byHeight != 0) return byHeight;
                return b.CompareTo(a);
            });
            return order.ToArray();
        }

        // ════════════════════════════════════════════════════════════════
        //  Ball segment helpers (keep the ball continuous across the timeline)
        // ════════════════════════════════════════════════════════════════

        private float _heldUntil;
        private int _heldBy = -1;

        private void AddHeld(ref float cursor, float until, int holderIdx, bool dribbled)
        {
            _ball.Add(new HeldSegment(cursor, until, _offense[holderIdx],
                _s.Offense[holderIdx].PlayerId, dribbled, DribblePhase(dribbled)));
            _heldUntil = until;
            _heldBy = holderIdx;
            cursor = until;
        }

        private void AddHeldSegmentEndingAt(float until, int holderIdx)
        {
            float start = LastBallEnd();
            _ball.Add(new HeldSegment(start, until, _offense[holderIdx],
                _s.Offense[holderIdx].PlayerId, dribbled: false));
            _heldBy = holderIdx;
            _heldUntil = until;
        }

        private void AddHeldFromTo(float from, float until, int holderIdx, bool dribbled)
        {
            if (until <= from) return;
            _ball.Add(new HeldSegment(from, until, _offense[holderIdx],
                _s.Offense[holderIdx].PlayerId, dribbled, DribblePhase(dribbled)));
            _heldBy = holderIdx;
            _heldUntil = until;
        }

        /// <summary>Random dribble phase so consecutive handlers never bounce in sync.</summary>
        private float DribblePhase(bool dribbled) =>
            dribbled ? (float)(_rng.NextDouble() * Math.PI * 2) : 0f;

        private void AddHeldFromCursorTo(float until, int holderIdx, bool dribbled)
        {
            AddHeldFromTo(LastBallEnd(), until, holderIdx, dribbled);
        }

        /// <summary>Trim/extend the current trailing held segment to end at t.</summary>
        private void EndHeldAt(float t)
        {
            if (_ball.Count == 0) return;
            var last = _ball[_ball.Count - 1];
            if (last is HeldSegment held && t > held.T0)
                held.T1 = t;
        }

        private float LastBallEnd() => _ball.Count > 0 ? _ball[_ball.Count - 1].T1 : 0f;

        private bool IsDriveShot() =>
            _s.ShotType == ShotType.Dunk || _s.ShotType == ShotType.Layup || _s.ShotType == ShotType.Floater;

        /// <summary>Deterministic ~25% acrobatic-layup flag from the shooter id + start clock —
        /// a stable FNV-1a hash (no Date, no extra rng) so the same possession always decides the
        /// same way across sessions. Presentational only.</summary>
        private bool IsAcrobaticLayup()
        {
            unchecked
            {
                uint h = 2166136261u;
                string id = _s.Offense[_s.ShooterIndex].PlayerId ?? string.Empty;
                for (int i = 0; i < id.Length; i++) h = (h ^ id[i]) * 16777619u;
                h = (h ^ (uint)(int)(_s.StartGameClock * 100f)) * 16777619u;
                return (h & 3u) == 0u;   // ~1 in 4
            }
        }

        /// <summary>A point just past the nearest boundary line from `from` — where a turnover ball sails OOB.</summary>
        private static CourtPosition NearestOutOfBounds(CourtPosition from)
        {
            const float past = 1.5f;
            float distTop = CourtGeometry.HalfWidth - from.Y;     // +Y sideline
            float distBot = from.Y + CourtGeometry.HalfWidth;     // -Y sideline
            float distRight = CourtGeometry.HalfLength - from.X;  // +X baseline
            float distLeft = from.X + CourtGeometry.HalfLength;   // -X baseline

            float min = Math.Min(Math.Min(distTop, distBot), Math.Min(distRight, distLeft));
            if (min == distTop) return new CourtPosition(from.X, CourtGeometry.HalfWidth + past);
            if (min == distBot) return new CourtPosition(from.X, -CourtGeometry.HalfWidth - past);
            if (min == distRight) return new CourtPosition(CourtGeometry.HalfLength + past, from.Y);
            return new CourtPosition(-CourtGeometry.HalfLength - past, from.Y);
        }

        private CourtPosition NearPoint(CourtPosition p, float maxDist)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float d = (float)_rng.NextDouble() * maxDist;
            return FormationLibrary.Clamp(new CourtPosition(
                p.X + (float)Math.Cos(angle) * d,
                p.Y + (float)Math.Sin(angle) * d));
        }

        // ════════════════════════════════════════════════════════════════
        //  Emission: sample tracks + ball + integrate defense at fixed ticks
        // ════════════════════════════════════════════════════════════════

        private List<SpatialState> Emit()
        {
            var states = new List<SpatialState>(160);

            // Defense starts where it ended the prior possession (this defense = the previous
            // possession's offense), so it's already back at this end — seeded by PlayerId from the
            // StartContext. Backcourt retreat is the legacy fallback for missing ids / null context.
            var defBackcourt = FormationLibrary.GetBackcourtSpots(_s.OffenseAttacksRight, true, _rng);
            _defPos = new CourtPosition[5];
            var startCtx = _s.StartContext;
            for (int i = 0; i < 5; i++)
                _defPos[i] = startCtx != null && startCtx.Kind != PossessionStartKind.Backcourt
                    ? (startCtx.PositionOf(_s.Defense[i].PlayerId) ?? defBackcourt[i])
                    : defBackcourt[i];

            var times = new List<float>(states.Capacity);
            float t = 0f;
            float prevT = 0f;
            var prevOff = new (float x, float y)[5];
            bool hasPrev = false;
            while (t <= _totalT + 0.001f)
            {
                float dt = t - prevT;
                IntegrateDefense(t, dt);
                var state = Sample(t);

                // Hard per-tick displacement cap for the offense. Authored legs are
                // speed-budgeted, but a deadline-bound leg (catch-and-relocate to a
                // far shot spot) can exceed sprint speed; the emitted stream is the
                // contract (≤8 ft per 0.2s tick), so late arrivals beat teleports.
                if (hasPrev)
                {
                    float cap = 7.5f * (dt <= 0f ? 1f : dt / Tick);
                    for (int i = 0; i < 5; i++)
                    {
                        var snap = state.Players[i];
                        float dx = snap.X - prevOff[i].x;
                        float dy = snap.Y - prevOff[i].y;
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d > cap)
                        {
                            float k = cap / d;
                            snap.X = prevOff[i].x + dx * k;
                            snap.Y = prevOff[i].y + dy * k;
                            state.Players[i] = snap;
                        }
                    }
                }
                for (int i = 0; i < 5; i++)
                    prevOff[i] = (state.Players[i].X, state.Players[i].Y);
                hasPrev = true;

                states.Add(state);
                times.Add(t);
                prevT = t;

                var seg = BallSegmentAt(t);
                bool inFlight = seg is PassSegment || seg is ShotSegment || seg is BankSegment ||
                                seg is CaromSegment || seg is BlockSegment;
                // Densify (0.1s) during the shot windup, the rebound leap, and sharp cuts too,
                // so P3 characters have enough frames to animate those beats convincingly. Also
                // densify while dribbled so the authored ~2.2Hz floor-to-hand bounce (|sin|) is
                // sampled cleanly instead of aliasing at the coarse tick.
                bool dribbling = state.Ball.Status == BallStatus.Dribbled;
                t += (inFlight || dribbling || InDenseActionWindow(t)) ? FlightTick : Tick;
            }

            // Speed post-pass: difference each player's FINAL (post-cap) position against the
            // adjacent frame. Done here, after the per-tick displacement cap, so the emitted
            // speed matches the motion the renderer actually plays back.
            ComputeSpeeds(states, times);

            return states;
        }

        /// <summary>Presentational densification window: the shot gather/release, the rebound
        /// leap, and sharp offensive cuts. Ball-flight windows are already dense via the segment
        /// check in Emit, so this only adds the non-flight beats. Never affects outcomes.</summary>
        private bool InDenseActionWindow(float t)
        {
            if (_shotStartT > 0f && t >= _shotStartT - _shotWindup - 0.05f && t <= _shotStartT + 0.05f)
                return true;
            if (_reboundArriveT > 0f && t >= _reboundArriveT - 0.35f && t <= _reboundArriveT + 0.35f)
                return true;
            if (_sharpCutTimes != null)
            {
                for (int i = 0; i < _sharpCutTimes.Count; i++)
                {
                    float ct = _sharpCutTimes[i];
                    if (t >= ct - 0.2f && t <= ct + 0.2f) return true;
                    if (ct - 0.2f > t) break;   // sorted ascending: no later cut can contain t
                }
            }
            return false;
        }

        /// <summary>Fills PlayerSnapshot.SpeedFeetPerSec by differencing adjacent frames.</summary>
        private static void ComputeSpeeds(List<SpatialState> states, List<float> times)
        {
            if (states.Count < 2) return;
            for (int k = 0; k < states.Count; k++)
            {
                // Backward difference (forward for the very first frame).
                int a = k > 0 ? k - 1 : k;
                int b = k > 0 ? k : k + 1;
                float dt = times[b] - times[a];
                if (dt <= 0f) continue;
                var from = states[a];
                var to = states[b];
                for (int p = 0; p < 10; p++)
                {
                    float dx = to.Players[p].X - from.Players[p].X;
                    float dy = to.Players[p].Y - from.Players[p].Y;
                    float speed = (float)Math.Sqrt(dx * dx + dy * dy) / dt;
                    var snap = states[k].Players[p];
                    snap.SpeedFeetPerSec = speed;
                    states[k].Players[p] = snap;
                }
            }
        }

        private SpatialState Sample(float t)
        {
            // Clock holds through the dead-ball inbound lead-in (t < _clockStartT), then ticks.
            float ticking = Math.Max(0f, t - _clockStartT);
            float gameClock = Math.Max(_s.StartGameClock - ticking, 0f);
            float possClock = Math.Max(24f - ticking, 0f);
            var state = new SpatialState(gameClock, _s.Quarter, possClock)
            {
                Phase = PhaseAt(t),
                PresentationTime = t
            };

            var ball = SampleBall(t);
            state.Ball = ball;
            var ballPos = new CourtPosition(ball.X, ball.Y);

            for (int i = 0; i < 5; i++)
            {
                var (pos, action, target) = _offense[i].Sample(t);
                bool hasBall = ball.HeldByPlayerId == _s.Offense[i].PlayerId;
                var (offVert, offPhase) = ActionDynamics(isDefense: false, idx: i, t: t, action: action);
                state.Players[i] = new PlayerSnapshot(_s.Offense[i].PlayerId, pos.X, pos.Y)
                {
                    HasBall = hasBall,
                    CurrentAction = action,
                    TargetPlayerId = ContactPartnerId(false, i, t) ?? target,
                    FacingAngle = OffenseFacing(i, t, pos, hasBall, action, ballPos),
                    VerticalOffset = offVert,
                    ActionPhase = offPhase,
                    ShotStyle = ShotStyleFor(i, t)
                };

                var defAction = DefenseActionAt(i, t);
                var (defVert, defPhase) = ActionDynamics(isDefense: true, idx: i, t: t, action: defAction);
                state.Players[i + 5] = new PlayerSnapshot(_s.Defense[i].PlayerId, _defPos[i].X, _defPos[i].Y)
                {
                    CurrentAction = defAction,
                    TargetPlayerId = ContactPartnerId(true, i, t),
                    FacingAngle = DefenseFacing(i, t, _defPos[i], defAction, ball, ballPos),
                    DefensiveStance = defAction == PlayerAction.Defending || defAction == PlayerAction.Contesting,
                    VerticalOffset = defVert,
                    ActionPhase = defPhase
                };
            }

            return state;
        }

        /// <summary>The shot type to stamp on a player's snapshot: only the shooter, and only from
        /// windup start through the ball reaching the rim. Null otherwise. Lets the renderer pick a
        /// distinct shot animation per ShotType; never affects outcomes.</summary>
        private ShotType? ShotStyleFor(int offenseIdx, float t)
        {
            if (_shotStartT <= 0f || offenseIdx != _s.ShooterIndex || _s.ShotType == null)
                return null;
            float windupStart = _shotStartT - _shotWindup;
            float end = _shotRimT > 0f ? _shotRimT : _shotStartT + 0.05f;
            return (t >= windupStart - 0.05f && t <= end + 0.05f) ? _s.ShotType : null;
        }

        /// <summary>Angle (radians) from <paramref name="from"/> toward <paramref name="to"/> in
        /// court space — the same convention the existing FacingFor used (atan2 of the delta).</summary>
        private static float AngleTo(CourtPosition from, CourtPosition to)
            => (float)Math.Atan2(to.Y - from.Y, to.X - from.X);

        /// <summary>Meaningful facing for an offensive player: the ball-handler/shooter squares to
        /// the rim, a moving player faces where they're cutting, everyone else watches the ball.</summary>
        private float OffenseFacing(int i, float t, CourtPosition pos, bool hasBall,
            PlayerAction action, CourtPosition ballPos)
        {
            var rim = new CourtPosition(_rimX, 0f);
            // Post backing down: back to the basket (face away from the rim).
            if (action == PlayerAction.PostingUp)
                return AngleTo(rim, pos);
            // Screener: back/side to the defender he's screening.
            if (action == PlayerAction.Screening && _screenDefIdx >= 0 && InScreenContact(t))
                return AngleTo(_defPos[_screenDefIdx], pos);
            bool shooting = action == PlayerAction.Shooting || action == PlayerAction.Dunking ||
                            action == PlayerAction.Layup || action == PlayerAction.LayupAcrobatic;
            if (hasBall || shooting)
                return AngleTo(pos, rim);

            // Facing the direction of travel when genuinely cutting/relocating.
            var ahead = _offense[i].PositionAt(t + 0.15f);
            float mdx = ahead.X - pos.X, mdy = ahead.Y - pos.Y;
            if (mdx * mdx + mdy * mdy > 0.09f)   // >~2 ft/s of motion
                return (float)Math.Atan2(mdy, mdx);

            // Boxing out / rebounding: watch the rim (the shot). Otherwise track the ball.
            if (action == PlayerAction.Rebounding || action == PlayerAction.BoxingOut)
                return AngleTo(pos, rim);
            return AngleTo(pos, ballPos);
        }

        /// <summary>Meaningful facing for a defender: guarding the ball → face the ball; crashing the
        /// glass → face the rim; otherwise face the assigned man (ball-you-man orientation).</summary>
        private float DefenseFacing(int i, float t, CourtPosition pos, PlayerAction action,
            BallState ball, CourtPosition ballPos)
        {
            if (action == PlayerAction.Rebounding || action == PlayerAction.BoxingOut)
                return AngleTo(pos, new CourtPosition(_rimX, 0f));

            int man = _defPlan != null ? _defPlan[i].ManIndex : i;
            bool guardsBall = ball.HeldByPlayerId != null &&
                              _s.Offense[man].PlayerId == ball.HeldByPlayerId;
            if (guardsBall || action == PlayerAction.Contesting)
                return AngleTo(pos, ballPos);
            return AngleTo(pos, _offense[man].PositionAt(t));   // ball-you-man: face the assigned man
        }

        private BallState SampleBall(float t)
        {
            var seg = BallSegmentAt(t);
            if (seg != null) return seg.Sample(t);

            // Gap: hold the ball exactly where the most recent segment left it
            // (jumping back to a holder would teleport the ball).
            BallSegment latest = null;
            for (int i = 0; i < _ball.Count; i++)
            {
                if (_ball[i].T1 <= t && (latest == null || _ball[i].T1 > latest.T1))
                    latest = _ball[i];
            }
            if (latest != null) return latest.Sample(latest.T1);

            // Before any segment exists: ball with the initial handler
            int holder = _heldBy >= 0 ? _heldBy : _s.InitialBallHandlerIndex;
            var pos = _offense[holder].PositionAt(t);
            return new BallState(pos.X, pos.Y, CourtGeometry.HeldBallHeight)
            {
                Status = BallStatus.Held,
                HeldByPlayerId = _s.Offense[holder].PlayerId
            };
        }

        private BallSegment BallSegmentAt(float t)
        {
            for (int i = _ball.Count - 1; i >= 0; i--)
            {
                if (t >= _ball[i].T0 && t <= _ball[i].T1)
                    return _ball[i];
            }
            return null;
        }

        private PossessionPhase PhaseAt(float t)
        {
            var phase = PossessionPhase.Advance;
            foreach (var (markT, p) in _phaseMarks)
                if (t >= markT) phase = p;
            return phase;
        }

        // ── Jump height + action phase (presentational; drives P3 airborne animation) ──

        /// <summary>Vertical jump offset (feet) and normalized action progress (0..1) for a player
        /// at time t. Covers the shooter's shot, the rebounder's leap, and a contesting/blocking
        /// defender. Everyone else stays grounded (0,0). Timing reuses the same shot/rebound marks
        /// the ball-flight choreography already uses.</summary>
        private (float vertical, float phase) ActionDynamics(bool isDefense, int idx, float t, PlayerAction action)
        {
            if (!isDefense)
            {
                if (_s.ShooterIndex == idx && _shotStartT > 0f)
                {
                    var jump = ShooterJump(t);
                    if (jump.vertical > 0f || jump.phase > 0f) return jump;
                }
                if (_reboundArriveT > 0f && !_s.RebounderIsDefense && _s.RebounderIndex == idx)
                    return ReboundLeap(t);
                var oExtra = ExtraLeapFor(false, idx, t);
                if (oExtra.HasValue) return oExtra.Value;
                return (0f, 0f);
            }

            // Defense: the contesting man leaps at the shot (a real block goes higher); the
            // defensive rebounder leaps for the board.
            if (action == PlayerAction.Contesting && _shotStartT > 0f)
            {
                float peak = _s.Ending == ScriptEnding.BlockedShot ? 3.2f : 1.4f;
                return Leap(t, _shotStartT - 0.10f, _shotStartT + 0.05f, _shotStartT + 0.45f, peak);
            }
            if (_reboundArriveT > 0f && _s.RebounderIsDefense && _s.RebounderIndex == idx &&
                action == PlayerAction.Rebounding)
                return ReboundLeap(t);
            var dExtra = ExtraLeapFor(true, idx, t);
            if (dExtra.HasValue) return dExtra.Value;
            return (0f, 0f);
        }

        /// <summary>A rebound-battle loser's early/short leap (registered by BuildReboundScramble), or
        /// null when this player has no extra leap or is outside its window.</summary>
        private (float vertical, float phase)? ExtraLeapFor(bool isDefense, int idx, float t)
        {
            if (_extraLeaps == null) return null;
            for (int i = 0; i < _extraLeaps.Count; i++)
            {
                var l = _extraLeaps[i];
                if (l.isDefense != isDefense || l.idx != idx) continue;
                var leap = Leap(t, l.apexT - 0.28f, l.apexT, l.apexT + 0.28f, l.peak);
                if (leap.vertical > 0f || leap.phase > 0f) return leap;
            }
            return null;
        }

        /// <summary>The shooter's jump: dunks/layups rise into the rim and carry; jump shots leave the
        /// floor near release and land. ActionPhase spans windup→release→landing so a clip can sync.</summary>
        private (float vertical, float phase) ShooterJump(float t)
        {
            float peak = PeakJumpFor(_s.ShotType ?? ShotType.Jumper);
            float windupStart = _shotStartT - _shotWindup;
            float riseStart, apexT, landEnd;

            if (_s.ShotType == ShotType.Dunk)
            {
                riseStart = _shotStartT - 0.12f;
                apexT = _shotRimT > 0f ? _shotRimT : _shotStartT + 0.25f;   // highest at the rim
                landEnd = apexT + 0.35f;
            }
            else if (_s.ShotType == ShotType.Layup || _s.ShotType == ShotType.TipIn)
            {
                riseStart = _shotStartT - 0.12f;
                apexT = (_shotRimT > 0f ? _shotRimT : _shotStartT + 0.2f) - 0.05f;
                landEnd = apexT + 0.35f;
            }
            else   // jump shots / floaters / hooks / heaves
            {
                riseStart = _shotStartT - 0.20f;
                apexT = _shotStartT + 0.05f;
                landEnd = _shotStartT + 0.55f;
            }

            float vertical = Leap(t, riseStart, apexT, landEnd, peak).vertical;
            float phaseEnd = Math.Max(landEnd, _shotRimT > 0f ? _shotRimT : landEnd);
            float phase = 0f;
            if (t >= windupStart && t <= phaseEnd && phaseEnd > windupStart)
                phase = (t - windupStart) / (phaseEnd - windupStart);
            return (vertical, Math.Min(Math.Max(phase, 0f), 1f));
        }

        private (float vertical, float phase) ReboundLeap(float t)
            => Leap(t, _reboundArriveT - 0.30f, _reboundArriveT, _reboundArriveT + 0.30f, 2.0f);

        /// <summary>A smooth jump arc: 0 at riseStart, peak at apexT, back to 0 at landEnd, using
        /// quarter-sine halves so it's continuous at the apex. Returns (height, phase 0..1).</summary>
        private static (float vertical, float phase) Leap(float t, float riseStart, float apexT, float landEnd, float peak)
        {
            if (t <= riseStart || t >= landEnd) return (0f, 0f);
            float vertical, phase;
            if (t <= apexT)
            {
                float u = apexT > riseStart ? (t - riseStart) / (apexT - riseStart) : 1f;
                vertical = peak * (float)Math.Sin(u * Math.PI * 0.5);   // 0 → peak
                phase = 0.5f * u;
            }
            else
            {
                float u = landEnd > apexT ? (t - apexT) / (landEnd - apexT) : 1f;
                vertical = peak * (float)Math.Cos(u * Math.PI * 0.5);   // peak → 0
                phase = 0.5f + 0.5f * u;
            }
            return (vertical, Math.Min(Math.Max(phase, 0f), 1f));
        }

        /// <summary>Peak jump height (feet) by shot type — dunks highest, set jumpers a moderate hop.</summary>
        private static float PeakJumpFor(ShotType type)
        {
            switch (type)
            {
                case ShotType.Dunk: return 3.4f;
                case ShotType.TipIn: return 2.6f;
                case ShotType.Layup: return 2.4f;
                case ShotType.Floater: return 1.7f;   // high, quick release over the big
                case ShotType.Hookshot: return 1.1f;
                case ShotType.Heave: return 0.8f;
                default: return 1.6f;   // jump shots (mid / three)
            }
        }

        // ── Defense: integrate toward shadow targets each tick ──

        // ════════════════════════════════════════════════════════════════
        //  Authored-contact pairing (P2): who meets whom, at what distance
        // ════════════════════════════════════════════════════════════════

        /// <summary>Resolve the screen and post-up defender indices once _defPlan exists, so the
        /// integrator and the snapshot pass know which defender each contact pairs with.</summary>
        private void ResolveContactPairs()
        {
            _screenDefIdx = -1;
            _screenBeneIdx = -1;
            _postDefIdx = -1;
            if (_defPlan == null) return;

            if (_action.Screener >= 0 && _action.ScreenEndT > 0f)
            {
                _screenBeneIdx = IsOnBallAction(_action.Action) ? _action.Handler : _action.Cutter;
                if (_screenBeneIdx >= 0) _screenDefIdx = DefenderGuarding(_screenBeneIdx);
            }
            if (_action.Action == OffensiveAction.PostUp && _action.PostIndex >= 0)
                _postDefIdx = DefenderGuarding(_action.PostIndex);
        }

        /// <summary>Defender index currently assigned to an offensive player (follows Switches), or
        /// the identity fallback.</summary>
        private int DefenderGuarding(int offIdx)
        {
            for (int d = 0; d < 5; d++)
                if (_defPlan[d].ManIndex == offIdx) return d;
            return (offIdx >= 0 && offIdx < 5) ? offIdx : -1;
        }

        /// <summary>True during the frames the screen is being held (body contact window).</summary>
        private bool InScreenContact(float t) =>
            _action.ScreenEndT > 0f && t >= _action.ScreenStartT && t <= _action.ScreenEndT;

        /// <summary>The contact partner's PlayerId for a screen or post-up pair (both directions), or
        /// null. Consumed by the render-side SoftSeparation pass to hold the pair at contact distance
        /// rather than the crowd minimum.</summary>
        private string ContactPartnerId(bool isDefense, int idx, float t)
        {
            if (_screenDefIdx >= 0 && InScreenContact(t))
            {
                if (isDefense && idx == _screenDefIdx) return _s.Offense[_action.Screener].PlayerId;
                if (!isDefense && idx == _action.Screener) return _s.Defense[_screenDefIdx].PlayerId;
            }
            if (_postDefIdx >= 0 && _action.PostStartT >= 0f &&
                t >= _action.PostStartT && t <= _action.PostEndT)
            {
                if (isDefense && idx == _postDefIdx) return _s.Offense[_action.PostIndex].PlayerId;
                if (!isDefense && idx == _action.PostIndex) return _s.Defense[_postDefIdx].PlayerId;
            }
            // Rebound battle: box-out pairs name each other so P2 renders the contact/lean.
            if (_reboundCrashT >= 0f && t >= _reboundCrashT)
            {
                if (isDefense && _reboundDefPartner != null && _reboundDefPartner[idx] >= 0)
                    return _s.Offense[_reboundDefPartner[idx]].PlayerId;
                if (!isDefense && _reboundOffPartner != null && _reboundOffPartner[idx] >= 0)
                    return _s.Defense[_reboundOffPartner[idx]].PlayerId;
            }
            return null;
        }

        /// <summary>Whether a court point sits inside the attacking paint (lane box).</summary>
        private bool InLaneBox(CourtPosition p)
        {
            float d = (_rimX - p.X) * M;   // ft from the rim toward midcourt
            return Math.Abs(p.Y) <= 8f && d >= -3f && d <= 14f;
        }

        /// <summary>During a transition retreat, flag the defenders (beyond the paint cap) whose men
        /// sit in the lane so they hold at the elbow instead of collapsing the paint. Keeps the
        /// defenders whose men are nearest the rim.</summary>
        private bool[] ComputeTransitionPaintHolds(float t)
        {
            var holds = new bool[5];
            var laneDefs = new List<int>();
            for (int i = 0; i < 5; i++)
                if (InLaneBox(_offense[_defPlan[i].ManIndex].PositionAt(t))) laneDefs.Add(i);

            if (laneDefs.Count <= TransitionPaintCap) return holds;
            laneDefs.Sort((a, b) =>
            {
                float da = (_rimX - _offense[_defPlan[a].ManIndex].PositionAt(t).X) * M;
                float db = (_rimX - _offense[_defPlan[b].ManIndex].PositionAt(t).X) * M;
                return da.CompareTo(db);
            });
            for (int k = TransitionPaintCap; k < laneDefs.Count; k++) holds[laneDefs[k]] = true;
            return holds;
        }

        private void IntegrateDefense(float t, float dt)
        {
            if (dt <= 0f) return;

            var ball = SampleBall(t);
            bool resolution = t > _liveEndT;
            bool defenseRebounds = resolution && _s.RebounderIsDefense && _s.RebounderIndex >= 0;

            var rim = new CourtPosition(_rimX, 0f);
            // Only on-ball screens (PnR/PnP) drive the ball-defender nav / hedge / roller-help logic.
            bool screenAction = _action.ScreenEndT > 0f &&
                (_action.Action == OffensiveAction.PickAndRoll || _action.Action == OffensiveAction.PickAndPop);

            // Transition paint relief: cap how many defenders sink into the lane at once — extras
            // hold at the elbow so the paint doesn't collapse into one crashing scrum.
            bool[] transitionHolds = t < _transitionEndT ? ComputeTransitionPaintHolds(t) : null;

            for (int i = 0; i < 5; i++)
            {
                // Free-throw window: hold the authored lane/arc spot instead of man-tracking
                // (defenders drifting after their man mid-FT looked broken). On a final-attempt
                // miss the lane bodies crash toward the precomputed rebound battle.
                if (_ftStartT >= 0f && t >= _ftStartT)
                {
                    CourtPosition ftTarget = _ftDefSpots[i];
                    float ftSpeed = DefenseSpeed;
                    if (_ftCrashT >= 0f && t >= _ftCrashT)
                    {
                        if (_s.RebounderIsDefense && i == _s.RebounderIndex)
                        {
                            ftTarget = _reboundSpot;
                            ftSpeed = DefenseSprint;
                        }
                        else if (_ftLaneDefender[i])
                        {
                            ftTarget = _ftCrashSpots[i];
                            ftSpeed = DefenseSprint;
                        }
                    }
                    _defPos[i] = _defPos[i].MoveTowards(ftTarget, ftSpeed * dt);
                    continue;
                }

                int man = _defPlan[i].ManIndex;   // follows a Switch
                CourtPosition target;
                float speed = DefenseSpeed;

                // Transition retreat: on a live rebound the defense is scrambling back. While still
                // beaten downcourt (behind the ball) a defender sprints goalside; once he's goalside
                // of the ball — or the ball has crossed half-court — he blends back to normal
                // man-marking instead of continuing to collapse toward the rim.
                if (t < _transitionEndT)
                {
                    var manPos = _offense[man].PositionAt(t);
                    var ballPos = new CourtPosition(ball.X, ball.Y);
                    bool recovered = (_defPos[i].X - ballPos.X) * M >= 0f || ballPos.X * M > 0f;
                    CourtPosition backTarget;
                    if (!recovered)
                    {
                        backTarget = Blend(manPos, rim, 0.5f);   // get back first, pick up late
                    }
                    else
                    {
                        backTarget = Blend(manPos, rim, SagFor(i, ball));   // normal man-marking spot
                        if (transitionHolds != null && transitionHolds[i])
                            backTarget = new CourtPosition(CourtGeometry.FreeThrowLineX * M,
                                manPos.Y >= 0f ? 8f : -8f);   // paint capped: hold at the elbow
                    }
                    _defPos[i] = _defPos[i].MoveTowards(backTarget, DefenseSprint * dt);
                    continue;
                }

                if (_looseBallT >= 0f && t >= _looseBallT && i == _looseBallDefIdx)
                {
                    // Loose-ball turnover: this defender breaks off and scoops it up.
                    target = _reboundSpot;
                    speed = DefenseSprint;
                }
                else if (defenseRebounds && i == _s.RebounderIndex)
                {
                    target = _reboundSpot;
                    speed = DefenseSprint;
                }
                else if (_reboundCrashDef != null && t >= _reboundCrashT && _reboundCrashDef[i])
                {
                    // Rebound battle: this defender crashes to box out his paired offensive crasher,
                    // one body-length toward the ball (P2 renders the contact/lean off the pairing).
                    target = _reboundCrashSpots[i];
                    speed = DefenseSprint;
                }
                else if (resolution && _s.Ending == ScriptEnding.MadeShot && i == 0)
                {
                    // Point-of-attack defender collects the made ball under the rim
                    target = rim;
                }
                else if (_s.Lapse == LapseType.BlownRotation && i == _s.LapseDefenderIndex &&
                         _shotStartT > 0f && t > _shotStartT - 2f && t <= _shotStartT + 0.3f)
                {
                    // Blown rotation: he's lost the assignment — caught flat-footed
                    // while the play happens around him.
                    target = _defPos[i];
                    speed = 0f;
                }
                else if (IsContestMoment(t, i) && man == _s.ShooterIndex)
                {
                    // At the shot, the contest distance is the exact inverse of the
                    // decided ContestLevel so the visual matches the probability math.
                    float contestDist = (1f - _s.ContestLevel) * 6f;
                    var shooterPos = _offense[_s.ShooterIndex].PositionAt(t);
                    target = shooterPos.MoveTowards(rim, contestDist);
                    speed = DefenseSprint;
                }
                else if (i == _screenDefIdx && _screenBeneIdx >= 0 && InScreenContact(t))
                {
                    // Caught on the screen: ride the screener's hip at body-contact distance,
                    // on the side the beneficiary is using it. Authored just above the render
                    // separation contact minimum so the pair meets instead of overlapping.
                    var screenerPos = _offense[_action.Screener].PositionAt(t);
                    var benePos = _offense[_screenBeneIdx].PositionAt(t);
                    target = screenerPos.MoveTowards(benePos, ScreenContactDist);
                    speed = DefenseSprint;
                }
                else if (i == _postDefIdx && _action.PostStartT >= 0f &&
                         t >= _action.PostStartT && t <= _action.PostEndT)
                {
                    // Hold ground against the backing-down post at body-contact distance (rim side),
                    // giving ground slowly as he bumps in.
                    var postPos = _offense[_action.PostIndex].PositionAt(t);
                    target = postPos.MoveTowards(rim, PostContactDist);
                    speed = DefenseSpeed * 0.6f;
                }
                else if (screenAction && _defPlan[i].Nav == ScreenNavigation.Hedge &&
                         man == _action.Screener &&
                         t >= _action.ScreenStartT && t <= _action.ScreenEndT + 0.4f)
                {
                    // Screener's defender shows/hedges on the ball, then recovers after the beat.
                    target = Blend(_action.ScreenSpot, _offense[_action.Handler].PositionAt(t), 0.3f);
                    speed = DefenseSprint;
                }
                else if (_defPlan[i].IsHelper && _defPlan[i].TagIndex >= 0 && _action.RollStartT > 0f &&
                         t >= _action.RollStartT && t <= _action.RollStartT + 0.9f &&
                         !(_s.Lapse == LapseType.MissedHelp && i == _s.LapseDefenderIndex))
                {
                    // Low man tags the roller/popper, then recovers to his own man.
                    var roller = _offense[_defPlan[i].TagIndex].PositionAt(t);
                    target = Blend(roller, rim, 0.4f);
                }
                else if (_defPlan[i].IsZone)
                {
                    // Organic zone: each defender plays a ROLE for wherever the ball
                    // is — the area owner closes out, neighbors pinch, weak side
                    // sinks, 1-3-1 traps corners. High-IQ defenders rotate while the
                    // pass is still in the air; low-IQ defenders track a stale ball.
                    var a = _defPlan[i];
                    CourtPosition effBall;
                    var seg = BallSegmentAt(t);
                    if (seg is PassSegment ps && t >= ps.T0 + a.ReactDelay)
                    {
                        effBall = ps.Destination;            // jump the rotation to the catch point
                    }
                    else
                    {
                        var lagged = SampleBall(Math.Max(0f, t - Math.Max(0f, a.ReactDelay)));
                        effBall = new CourtPosition(lagged.X, lagged.Y);
                    }

                    var (zoneTarget, role) = ZoneBehavior.ComputeTarget(
                        DefensiveScheme, _defPlan, i, effBall, _rimX);

                    // Sloppy closeouts stop short of the ball; disciplined ones arrive.
                    if (role == ZoneRole.BallArea)
                        zoneTarget = zoneTarget.MoveTowards(a.ZoneSpot, (1f - a.CloseoutTightness) * 3f);

                    target = zoneTarget;
                    speed = DefenseSpeed * a.SpeedMult;
                    // Long rotations (skip passes, corner traps) are dead sprints.
                    if ((role == ZoneRole.BallArea || role == ZoneRole.Trap) &&
                        _defPos[i].DistanceTo(target) > 8f)
                        speed = DefenseSprint * a.SpeedMult;
                }
                else if (!resolution && JustCaught(man, t) &&
                         _defPos[i].DistanceTo(_offense[man].PositionAt(t)) > CloseoutDist)
                {
                    // Closeout on the catch: sprint from the sag to arm's length with hands up
                    // (DefensiveStance via DefenseActionAt). Once inside CloseoutDist the branch drops
                    // out and the guardsBall hip-ride below settles him onto the new handler.
                    target = _offense[man].PositionAt(t).MoveTowards(rim, CloseoutDist);
                    speed = DefenseSprint;
                }
                else
                {
                    var manPos = _offense[man].PositionAt(t);
                    bool guardsBall = ball.HeldByPlayerId != null &&
                                      _s.Offense[man].PlayerId == ball.HeldByPlayerId;
                    if (guardsBall && !resolution)
                    {
                        // On-ball hip-ride: arm's-length, goalside on the handler's driving line.
                        target = manPos.MoveTowards(rim, HipRideDist);
                        speed = DefenseSprint;
                    }
                    else
                    {
                        float sag = SagFor(i, ball);
                        target = Blend(manPos, rim, sag);
                    }

                    // Ball defender navigating the screen (over/under) — a subtle, stable offset.
                    if (screenAction && man == _action.Handler &&
                        t >= _action.ScreenStartT && t <= _action.ScreenEndT + 0.3f)
                    {
                        float side = _defPlan[i].Nav == ScreenNavigation.GoUnder ? -1f : 1f;
                        target = new CourtPosition(target.X, target.Y + side * 1.5f);
                    }
                }

                _defPos[i] = _defPos[i].MoveTowards(target, speed * dt);
            }
        }

        private bool IsContestMoment(float t) => IsContestMoment(t, -1);

        /// <summary>A late-closeout culprit leaves for his contest half a beat late.</summary>
        private bool IsContestMoment(float t, int defenderIdx)
        {
            float lead = 0.8f;
            if (defenderIdx >= 0 && _s.Lapse == LapseType.LateCloseout &&
                defenderIdx == _s.LapseDefenderIndex)
                lead = 0.35f;
            return _shotStartT > 0f && _s.ShooterIndex >= 0 &&
                   t > _shotStartT - lead && t < _shotStartT + 0.3f;
        }

        // Sag depth is decided ONCE per defender in the plan — no per-tick re-roll, so no twitch.
        private float SagFor(int defenderIdx, BallState ball)
        {
            var a = _defPlan[defenderIdx];
            bool guardsBall = ball.HeldByPlayerId != null &&
                              _s.Offense[a.ManIndex].PlayerId == ball.HeldByPlayerId;
            return guardsBall ? a.OnBallPressure : a.SagDepth;
        }

        private PlayerAction DefenseActionAt(int i, float t)
        {
            if (_ftStartT >= 0f && t >= _ftStartT)
            {
                if (_ftCrashT >= 0f && t >= _ftCrashT && _s.RebounderIsDefense && i == _s.RebounderIndex)
                    return PlayerAction.Rebounding;
                if (_ftLaneDefender != null && _ftLaneDefender[i]) return PlayerAction.BoxingOut;
                return PlayerAction.Defending;
            }

            if (_looseBallT >= 0f && t >= _looseBallT && i == _looseBallDefIdx) return PlayerAction.Rebounding;

            int man = _defPlan[i].ManIndex;
            if (IsContestMoment(t) && man == _s.ShooterIndex) return PlayerAction.Contesting;
            if (t > _liveEndT && _s.RebounderIsDefense && i == _s.RebounderIndex) return PlayerAction.Rebounding;
            if (t > _liveEndT) return PlayerAction.BoxingOut;   // box out during the shot/resolution
            if (_action.ScreenEndT > 0f && _defPlan[i].Nav == ScreenNavigation.Hedge &&
                man == _action.Screener && t >= _action.ScreenStartT && t <= _action.ScreenEndT + 0.4f)
                return PlayerAction.Screening;   // hedging posture
            return PlayerAction.Defending;
        }

        private static CourtPosition Blend(CourtPosition a, CourtPosition b, float u)
        {
            return new CourtPosition(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u);
        }

        /// <summary>True within ~0.5s after an off-ball pass landed on offensive player
        /// <paramref name="man"/> (his defender should be closing out on the catch).</summary>
        private bool JustCaught(int man, float t)
        {
            for (int i = 0; i < _ball.Count; i++)
            {
                if (_ball[i] is PassSegment ps && t >= ps.T1 && t <= ps.T1 + 0.5f &&
                    _offense[man].PositionAt(ps.T1).DistanceTo(ps.Destination) < 2f)
                    return true;
            }
            return false;
        }

        // ════════════════════════════════════════════════════════════════
        //  Internal track/segment types
        // ════════════════════════════════════════════════════════════════

        /// <summary>Per-player waypoint track with smoothstep easing between keys.</summary>
        private class Track
        {
            private struct Key
            {
                public float T;
                public CourtPosition Pos;
                public PlayerAction Action;
                public string Target;
            }

            private readonly List<Key> _keys = new List<Key>();
            private float _swayPhase;

            public Track(CourtPosition start)
            {
                _keys.Add(new Key { T = 0f, Pos = start, Action = PlayerAction.Idle });
            }

            public void Add(float t, CourtPosition pos, PlayerAction action, string target = null)
            {
                // keep keys time-ordered
                int insert = _keys.Count;
                while (insert > 0 && _keys[insert - 1].T > t) insert--;
                _keys.Insert(insert, new Key { T = t, Pos = pos, Action = action, Target = target });
            }

            /// <summary>Annotate an action at time t without moving (position sampled from the track).</summary>
            public void Stamp(float t, PlayerAction action, string target = null)
            {
                Add(t, PositionAt(t), action, target);
            }

            public void SwayUntil(float endT, System.Random rng)
            {
                _swayPhase = (float)rng.NextDouble() * 6.28f;
                var last = _keys[_keys.Count - 1];
                if (last.T < endT)
                    Add(endT, last.Pos, last.Action == PlayerAction.Idle ? PlayerAction.Idle : last.Action);
            }

            /// <summary>Append the times of keys where the travel direction reverses by more than
            /// <paramref name="minAngleDeg"/>, with both adjacent legs longer than
            /// <paramref name="minLegDist"/> ft — sharp, fast cuts worth densifying for animation.</summary>
            public void CollectSharpTurns(List<float> outTimes, float minLegDist, float minAngleDeg)
            {
                for (int i = 1; i < _keys.Count - 1; i++)
                {
                    var a = _keys[i - 1].Pos; var b = _keys[i].Pos; var c = _keys[i + 1].Pos;
                    float inX = b.X - a.X, inY = b.Y - a.Y;
                    float outX = c.X - b.X, outY = c.Y - b.Y;
                    float inLen = (float)Math.Sqrt(inX * inX + inY * inY);
                    float outLen = (float)Math.Sqrt(outX * outX + outY * outY);
                    if (inLen < minLegDist || outLen < minLegDist) continue;
                    float dot = (inX * outX + inY * outY) / (inLen * outLen);
                    dot = Math.Min(1f, Math.Max(-1f, dot));
                    float ang = (float)(Math.Acos(dot) * 180.0 / Math.PI);
                    if (ang >= minAngleDeg) outTimes.Add(_keys[i].T);
                }
            }

            public CourtPosition PositionAt(float t) => SampleCore(t).pos;

            public (CourtPosition pos, PlayerAction action, string target) Sample(float t)
            {
                var core = SampleCore(t);
                // Gentle stationary sway so dots never look frozen
                float sway = 0.6f * (float)Math.Sin(t * 1.1f + _swayPhase);
                var pos = new CourtPosition(core.pos.X, core.pos.Y + sway * 0.4f);
                return (pos, core.action, core.target);
            }

            private (CourtPosition pos, PlayerAction action, string target) SampleCore(float t)
            {
                if (t <= _keys[0].T) return (_keys[0].Pos, _keys[0].Action, _keys[0].Target);

                for (int i = 0; i < _keys.Count - 1; i++)
                {
                    if (t >= _keys[i].T && t <= _keys[i + 1].T)
                    {
                        float span = _keys[i + 1].T - _keys[i].T;
                        float u = span <= 0.0001f ? 1f : (t - _keys[i].T) / span;
                        // Smoothstep peaks at 1.5x the segment's average speed; a
                        // deadline-bound leg authored near the sprint limit would spike
                        // past the per-tick movement cap. Fast legs run linear instead
                        // (peak == average), keeping every authored move under the cap.
                        float dist = _keys[i].Pos.DistanceTo(_keys[i + 1].Pos);
                        bool nearCap = span > 0.0001f && dist / span * 1.5f > 38f;
                        if (!nearCap)
                            u = u * u * (3f - 2f * u); // smoothstep
                        var pos = new CourtPosition(
                            _keys[i].Pos.X + (_keys[i + 1].Pos.X - _keys[i].Pos.X) * u,
                            _keys[i].Pos.Y + (_keys[i + 1].Pos.Y - _keys[i].Pos.Y) * u);
                        // action of the segment we're moving through
                        var key = _keys[i + 1];
                        return (pos, key.Action, key.Target);
                    }
                }

                var last = _keys[_keys.Count - 1];
                return (last.Pos, last.Action, last.Target);
            }
        }

        private abstract class BallSegment
        {
            public float T0, T1;
            protected float U(float t) => T1 <= T0 ? 1f : Math.Min(Math.Max((t - T0) / (T1 - T0), 0f), 1f);
            public abstract BallState Sample(float t);
        }

        private class HeldSegment : BallSegment
        {
            private readonly Track _holder;
            private readonly string _playerId;
            private readonly bool _dribbled;

            private readonly float _dribblePhase;

            public HeldSegment(float t0, float t1, Track holder, string playerId, bool dribbled,
                float dribblePhase = 0f)
            {
                T0 = t0; T1 = t1; _holder = holder; _playerId = playerId; _dribbled = dribbled;
                _dribblePhase = dribblePhase;
            }

            public override BallState Sample(float t)
            {
                var pos = _holder.PositionAt(t);
                // Motion-derived dribble: a carrier in motion must pound the ball — a flat-held
                // ball gliding with a moving player reads as traveling. Spacing relocations can
                // move a catcher during an authored "static" hold, so check actual track motion.
                bool moving = _dribbled;
                if (!moving)
                {
                    float lookback = Math.Max(t - 0.12f, T0);
                    float window = t - lookback;
                    if (window > 0.01f)
                        moving = pos.DistanceTo(_holder.PositionAt(lookback)) / window > 1.5f;
                }
                // True floor-to-hand dribble cycle: the |sin| cusp at the bottom reads as the
                // floor hit (~2.2 bounces/sec, ball visibly pounding the floor). Held = dead still.
                float height = moving
                    ? 0.2f + 3.6f * (float)Math.Abs(Math.Sin(t * 7f + _dribblePhase))
                    : CourtGeometry.HeldBallHeight;
                return new BallState(pos.X, pos.Y, height)
                {
                    Status = moving ? BallStatus.Dribbled : BallStatus.Held,
                    HeldByPlayerId = _playerId
                };
            }
        }

        private class PassSegment : BallSegment
        {
            private readonly CourtPosition _from, _to;
            private readonly PassStyle _style;
            public PassSegment(float t0, float t1, CourtPosition from, CourtPosition to,
                PassStyle style = PassStyle.Chest)
            { T0 = t0; T1 = t1; _from = from; _to = to; _style = style; }
            /// <summary>Where this pass lands — lets smart zone defenders rotate while it's in the air.</summary>
            public CourtPosition Destination => _to;
            public override BallState Sample(float t) => BallFlight.SamplePass(_from, _to, U(t), _style);
        }

        private class ShotSegment : BallSegment
        {
            private readonly CourtPosition _from;
            private readonly float _rimX;
            private readonly ShotType? _type;
            public ShotSegment(float t0, float t1, CourtPosition from, float rimX, ShotType? type)
            { T0 = t0; T1 = t1; _from = from; _rimX = rimX; _type = type; }
            public override BallState Sample(float t) => BallFlight.SampleShot(_from, _rimX, U(t), _type);
        }

        /// <summary>Shot that uses the glass: arc to a backboard point, short hop down onto the
        /// rim. Same [release, rim] window as ShotSegment, so downstream timing is identical.</summary>
        private class BankSegment : BallSegment
        {
            private readonly CourtPosition _from;
            private readonly float _rimX, _glassY, _uGlass;
            private readonly ShotType? _type;
            public BankSegment(float t0, float t1, CourtPosition from, float rimX, float glassY,
                float uGlass, ShotType? type)
            { T0 = t0; T1 = t1; _from = from; _rimX = rimX; _glassY = glassY; _uGlass = uGlass; _type = type; }
            public override BallState Sample(float t) =>
                BallFlight.SampleBankShot(_from, _rimX, _glassY, U(t), _uGlass, _type);
        }

        private class MakeSegment : BallSegment
        {
            private readonly float _rimX;
            private readonly ShotType? _type;
            public MakeSegment(float t0, float t1, float rimX, ShotType? type = null)
            { T0 = t0; T1 = t1; _rimX = rimX; _type = type; }
            public override BallState Sample(float t) => BallFlight.SampleMadeFollowThrough(_rimX, U(t), _type);
        }

        private class CaromSegment : BallSegment
        {
            private readonly float _rimX;
            private readonly CourtPosition _spot;
            public CaromSegment(float t0, float t1, float rimX, CourtPosition spot)
            { T0 = t0; T1 = t1; _rimX = rimX; _spot = spot; }
            public override BallState Sample(float t) => BallFlight.SampleMissCarom(_rimX, _spot, U(t));
        }

        private class BlockSegment : BallSegment
        {
            private readonly CourtPosition _from, _spot;
            public BlockSegment(float t0, float t1, CourtPosition from, CourtPosition spot)
            { T0 = t0; T1 = t1; _from = from; _spot = spot; }
            public override BallState Sample(float t) => BallFlight.SampleBlockDeflection(_from, _spot, U(t));
        }

        private class DeadSegment : BallSegment
        {
            private readonly CourtPosition _pos;
            public DeadSegment(float t0, float t1, CourtPosition pos) { T0 = t0; T1 = t1; _pos = pos; }
            public override BallState Sample(float t) => new BallState(_pos.X, _pos.Y, 1f)
            {
                Status = BallStatus.DeadBall,
                HeldByPlayerId = null
            };
        }
    }
}
