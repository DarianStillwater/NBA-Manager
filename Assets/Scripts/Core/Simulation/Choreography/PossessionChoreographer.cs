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
        private CourtPosition _reboundSpot;
        private List<(float t, PossessionPhase phase)> _phaseMarks;
        private ActionPlan _action;              // the synthesized offensive action for this possession
        private DefenderAssignment[] _defPlan;   // per-defender assignment (sag/nav/help), decided once
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
            _heldBy = -1;
            _ftStartT = -1f;
            _ftCrashT = -1f;
            _ftDefSpots = null;
            _ftCrashSpots = null;
            _ftLaneDefender = null;
            _looseBallT = -1f;
            _looseBallDefIdx = -1;
            _action = ActionLibrary.Choose(script, _rng);

            BuildOffenseAndBall();
            _defPlan = DefenseChoreographer.BuildPlan(_action, script, _rng, DefensiveScheme);

            // Builders don't run in strict time order — sort and clamp for consumers.
            for (int i = 0; i < _beats.Count; i++)
                _beats[i].T = Math.Min(Math.Max(_beats[i].T, 0f), _totalT);
            _beats.Sort((a, b) => a.T.CompareTo(b.T));

            return Emit();
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
            float duration = Math.Max(_s.Duration, 3f);
            float advanceEnd = _s.IsFastBreak
                ? Math.Min(2f, duration * 0.35f)
                : Math.Min(4f, duration * 0.3f);

            // Time reserved at the end of the live window for the shot sequence
            bool hasShot = _s.ShooterIndex >= 0 &&
                           (_s.Ending == ScriptEnding.MadeShot || _s.Ending == ScriptEnding.MissedShot ||
                            _s.Ending == ScriptEnding.BlockedShot || _s.Ending == ScriptEnding.AndOneShot ||
                            _s.Ending == ScriptEnding.ShootingFoulMissed);
            float shotFlight = hasShot ? BallFlight.ShotDuration(_s.ShotPosition, _rimX, _s.ShotType) : 0f;
            float windup = hasShot ? BallFlight.WindupFor(_s.ShotType) : 0f;

            // ── Phase A/B spots (needed early: the advance window is distance-derived) ──
            var startSpots = FormationLibrary.GetBackcourtSpots(_s.OffenseAttacksRight, false, _rng);
            var formation = FormationLibrary.GetHalfCourtSpots(_s.OffenseStrategy, _s.OffenseAttacksRight, _rng);

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

            _liveEndT = duration;
            float shotReleaseT = duration - shotFlight;          // ball reaches rim at ~duration
            _shotStartT = hasShot ? shotReleaseT : -1f;
            float actionEnd = hasShot ? Math.Max(advanceEnd + 0.5f, shotReleaseT - windup) : duration;

            _phaseMarks.Add((0f, PossessionPhase.Advance));
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

            // Ball: dribbled up by the initial handler, held continuously until the action starts
            // (no gap, so the ball tracks the handler smoothly into the first pass/shot).
            int holder = _s.InitialBallHandlerIndex;
            float ballCursor = 0f;
            AddHeld(ref ballCursor, actionStart, holder, dribbled: true);
            Beat(advanceEnd * 0.5f, NarrationBeatKind.BringUp, holder);

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

            float holdEnd = _action.ScreenStartT + 0.5f;
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
                _action.ScreenEndT = _action.ScreenStartT + 0.5f;
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
            _offense[post].Stamp(t1 + 0.8f, PlayerAction.PostingUp);
            Beat(t1 + 0.4f, NarrationBeatKind.PostMove, post);
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
                float passT = Math.Max(actionEnd - 0.6f, _phaseMarks[2].t);
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

            // The shot itself
            var shotAction = _s.ShotType == ShotType.Dunk ? PlayerAction.Dunking
                            : _s.ShotType == ShotType.Layup || _s.ShotType == ShotType.TipIn ? PlayerAction.Layup
                            : PlayerAction.Shooting;
            _offense[shooter].Add(shotReleaseT, _s.ShotPosition, shotAction);

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

        private void BuildReboundScramble(float ballArriveT, CourtPosition spot)
        {
            if (_s.RebounderIndex < 0) return;

            if (_s.RebounderIsDefense)
            {
                // Defender rebounds — handled by the defense integrator via _reboundSpot;
                // offense players near the rim box out.
                BoxOutNearestOffense(ballArriveT, spot, 2);
            }
            else
            {
                int r = _s.RebounderIndex;
                _offense[r].Add(ballArriveT, spot, PlayerAction.Rebounding);
                BoxOutNearestOffense(ballArriveT, spot, 1, exclude: r);
            }

            _ball.Add(new DeadSegment(ballArriveT, ballArriveT + 1.0f, spot));
        }

        private void BoxOutNearestOffense(float t, CourtPosition spot, int count, int exclude = -1)
        {
            // The bigs crash; everyone else holds position
            int added = 0;
            for (int i = 4; i >= 0 && added < count; i--)
            {
                if (i == exclude) continue;
                var near = NearPoint(spot, 4f);
                _offense[i].Add(t + 0.1f, near, PlayerAction.BoxingOut);
                added++;
            }
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
            _phaseMarks.Add((endT - 0.3f, PossessionPhase.Resolution));
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

            // Defense starts in transition retreat
            _defPos = FormationLibrary.GetBackcourtSpots(_s.OffenseAttacksRight, true, _rng);

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
                prevT = t;

                var seg = BallSegmentAt(t);
                bool inFlight = seg is PassSegment || seg is ShotSegment || seg is BankSegment ||
                                seg is CaromSegment || seg is BlockSegment;
                t += inFlight ? FlightTick : Tick;
            }

            return states;
        }

        private SpatialState Sample(float t)
        {
            float gameClock = Math.Max(_s.StartGameClock - t, 0f);
            float possClock = Math.Max(24f - t, 0f);
            var state = new SpatialState(gameClock, _s.Quarter, possClock)
            {
                Phase = PhaseAt(t)
            };

            var ball = SampleBall(t);
            state.Ball = ball;

            for (int i = 0; i < 5; i++)
            {
                var (pos, action, target) = _offense[i].Sample(t);
                state.Players[i] = new PlayerSnapshot(_s.Offense[i].PlayerId, pos.X, pos.Y)
                {
                    HasBall = ball.HeldByPlayerId == _s.Offense[i].PlayerId,
                    CurrentAction = action,
                    TargetPlayerId = target,
                    FacingAngle = FacingFor(pos)
                };

                state.Players[i + 5] = new PlayerSnapshot(_s.Defense[i].PlayerId, _defPos[i].X, _defPos[i].Y)
                {
                    CurrentAction = DefenseActionAt(i, t),
                    FacingAngle = FacingFor(_defPos[i], invert: true)
                };
            }

            return state;
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

        private float FacingFor(CourtPosition pos, bool invert = false)
        {
            float dx = _rimX - pos.X;
            float dy = -pos.Y;
            float angle = (float)Math.Atan2(dy, dx);
            return invert ? angle + (float)Math.PI : angle;
        }

        // ── Defense: integrate toward shadow targets each tick ──

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
                else if (resolution && _s.Ending == ScriptEnding.MadeShot && i == 0)
                {
                    // Point-of-attack defender collects the made ball under the rim
                    target = rim;
                }
                else if (IsContestMoment(t) && man == _s.ShooterIndex)
                {
                    // At the shot, the contest distance is the exact inverse of the
                    // decided ContestLevel so the visual matches the probability math.
                    float contestDist = (1f - _s.ContestLevel) * 6f;
                    var shooterPos = _offense[_s.ShooterIndex].PositionAt(t);
                    target = shooterPos.MoveTowards(rim, contestDist);
                    speed = DefenseSprint;
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
                         t >= _action.RollStartT && t <= _action.RollStartT + 0.9f)
                {
                    // Low man tags the roller/popper, then recovers to his own man.
                    var roller = _offense[_defPlan[i].TagIndex].PositionAt(t);
                    target = Blend(roller, rim, 0.4f);
                }
                else if (_defPlan[i].IsZone)
                {
                    // Zone: hold the formation anchor, shading toward the ball —
                    // never chasing a man across the floor.
                    var ballPos = new CourtPosition(ball.X, ball.Y);
                    target = Blend(_defPlan[i].ZoneSpot, ballPos, 0.30f);
                }
                else
                {
                    var manPos = _offense[man].PositionAt(t);
                    float sag = SagFor(i, ball);
                    target = Blend(manPos, rim, sag);

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

        private bool IsContestMoment(float t) =>
            _shotStartT > 0f && _s.ShooterIndex >= 0 && t > _shotStartT - 0.8f && t < _shotStartT + 0.3f;

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
