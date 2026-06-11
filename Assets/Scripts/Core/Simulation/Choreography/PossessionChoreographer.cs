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

        private const float RunSpeed = 18f;      // ft/s
        private const float SprintSpeed = 27f;   // ft/s
        private const float DefenseSpeed = 20f;  // ft/s

        private readonly System.Random _rng;

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

        public PossessionChoreographer(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }

        public List<SpatialState> Choreograph(PossessionScript script)
        {
            _s = script;
            _rimX = CourtGeometry.RimXFor(script.OffenseAttacksRight);
            _offense = new Track[5];
            _ball = new List<BallSegment>();
            _phaseMarks = new List<(float, PossessionPhase)>();
            _shotStartT = -1f;
            _heldBy = -1;

            BuildOffenseAndBall();
            return Emit();
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
            float shotFlight = hasShot ? BallFlight.ShotDuration(_s.ShotPosition, _rimX) : 0f;
            float windup = hasShot ? 0.4f : 0f;

            _liveEndT = duration;
            float shotReleaseT = duration - shotFlight;          // ball reaches rim at ~duration
            _shotStartT = hasShot ? shotReleaseT : -1f;
            float actionEnd = hasShot ? Math.Max(advanceEnd + 0.5f, shotReleaseT - windup) : duration;

            _phaseMarks.Add((0f, PossessionPhase.Advance));
            _phaseMarks.Add((advanceEnd, PossessionPhase.SetOffense));
            _phaseMarks.Add((Math.Min(advanceEnd + 1f, actionEnd), PossessionPhase.Action));
            if (hasShot) _phaseMarks.Add((shotReleaseT - windup, PossessionPhase.Shot));

            // ── Phase A/B: backcourt → formation ──
            var startSpots = FormationLibrary.GetBackcourtSpots(_s.OffenseAttacksRight, false, _rng);
            var formation = FormationLibrary.GetHalfCourtSpots(_s.OffenseStrategy, _s.OffenseAttacksRight, _rng);

            for (int i = 0; i < 5; i++)
            {
                _offense[i] = new Track(startSpots[i]);
                float arrive = advanceEnd + 0.4f * (float)_rng.NextDouble();
                var action = i == _s.InitialBallHandlerIndex ? PlayerAction.Dribbling
                           : _s.IsFastBreak ? PlayerAction.Sprinting : PlayerAction.Running;
                _offense[i].Add(arrive, formation[i], action);
            }

            // Ball: dribbled up by the initial handler
            int holder = _s.InitialBallHandlerIndex;
            float ballCursor = 0f;
            AddHeld(ref ballCursor, advanceEnd + 0.2f, holder, dribbled: true);

            // ── Phase C: action beats ──
            float actionStart = advanceEnd + 0.6f;
            float cursor = actionStart;

            // Synthesized cosmetic swing passes (must return ball to the initial handler
            // before the decided pass so the assist credit matches the visual).
            holder = BuildSwingPasses(holder, ref cursor, actionEnd, formation);

            // Cosmetic screen before a drive (big walks up to screen for the handler)
            if (hasShot && IsDriveShot() && cursor + 1.4f < actionEnd && _s.ShooterIndex != 4)
            {
                float screenT = cursor + 0.4f;
                _offense[4].Add(screenT, NearPoint(_offense[holder].PositionAt(screenT), 3f), PlayerAction.Screening);
                cursor = screenT + 0.4f;
            }

            if (hasShot)
            {
                BuildShotSequence(holder, actionEnd, shotReleaseT, windup, shotFlight);
            }
            else
            {
                BuildNonShotEnding(holder);
            }

            // Everyone not otherwise scripted drifts/sways at their formation spot
            for (int i = 0; i < 5; i++)
                _offense[i].SwayUntil(_totalT, _rng);
        }

        /// <summary>Swing the ball around the perimeter and back. Returns the final holder (initial handler).</summary>
        private int BuildSwingPasses(int holder, ref float cursor, float actionEnd, CourtPosition[] formation)
        {
            int initial = holder;
            // Only the perimeter trio (PG/SG/SF) swing the ball cosmetically
            int[] perimeter = { 0, 1, 2 };

            int maxPasses = (int)((actionEnd - cursor) / 1.6f);
            int passes = Math.Min(maxPasses, 1 + _rng.Next(3)); // 1-3 if time allows
            if (passes % 2 == 1) passes++;                       // even count → ball returns
            passes = Math.Min(passes, maxPasses);

            for (int p = 0; p < passes && cursor + 1.5f < actionEnd; p++)
            {
                bool returning = p == passes - 1;
                int target;
                if (returning)
                {
                    target = initial;
                }
                else
                {
                    do { target = perimeter[_rng.Next(perimeter.Length)]; }
                    while (target == holder);
                }
                if (target == holder) break;

                var from = _offense[holder].PositionAt(cursor);
                var to = _offense[target].PositionAt(cursor);
                float flight = BallFlight.PassDuration(from, to);

                _offense[holder].Stamp(cursor, PlayerAction.Passing, _s.Offense[target].PlayerId);
                _ball.Add(new PassSegment(cursor, cursor + flight, from, to));
                _offense[target].Stamp(cursor + flight, PlayerAction.Catching);

                holder = target;
                cursor += flight + 0.6f + (float)_rng.NextDouble() * 0.4f;
                AddHeldSegmentEndingAt(cursor, holder);
            }

            return holder;
        }

        private void BuildShotSequence(int holder, float actionEnd, float shotReleaseT, float windup, float shotFlight)
        {
            int shooter = _s.ShooterIndex;

            // Decided pass to the shooter (the assist) — shooter catches at/near the shot spot
            if (_s.BallHandlerPassedToShooter && holder != shooter)
            {
                float passT = Math.Max(actionEnd - 0.6f, _phaseMarks[2].t);
                // Shooter relocates to the catch spot just before the pass
                _offense[shooter].Add(passT, _s.ShotPosition, PlayerAction.Cutting);

                var from = _offense[holder].PositionAt(passT);
                float flight = BallFlight.PassDuration(from, _s.ShotPosition);

                _offense[holder].Stamp(passT, PlayerAction.Passing, _s.Offense[shooter].PlayerId);
                EndHeldAt(passT);
                _ball.Add(new PassSegment(passT, passT + flight, from, _s.ShotPosition));
                _offense[shooter].Stamp(passT + flight, PlayerAction.Catching);

                AddHeldFromTo(passT + flight, shotReleaseT, shooter, dribbled: false);
            }
            else
            {
                // Shooter has the ball. Drives go rim-ward; jumpers step to the spot.
                var driveAction = IsDriveShot() ? PlayerAction.Sprinting : PlayerAction.Dribbling;
                float arriveT = shotReleaseT - 0.15f;
                _offense[shooter].Add(arriveT, _s.ShotPosition, driveAction);
                AddHeldFromCursorTo(shotReleaseT, shooter, dribbled: true);
            }

            // The shot itself
            var shotAction = _s.ShotType == ShotType.Dunk ? PlayerAction.Dunking
                            : _s.ShotType == ShotType.Layup || _s.ShotType == ShotType.TipIn ? PlayerAction.Layup
                            : PlayerAction.Shooting;
            _offense[shooter].Add(shotReleaseT, _s.ShotPosition, shotAction);

            bool made = _s.Ending == ScriptEnding.MadeShot || _s.Ending == ScriptEnding.AndOneShot;
            bool blocked = _s.Ending == ScriptEnding.BlockedShot;

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
            }
            else
            {
                float rimT = shotReleaseT + shotFlight;
                _ball.Add(new ShotSegment(shotReleaseT, rimT, _s.ShotPosition, _rimX, _s.ShotType));

                if (made)
                {
                    float netT = rimT + BallFlight.MakeFollowThroughDuration;
                    _ball.Add(new MakeSegment(rimT, netT, _rimX));
                    _offense[shooter].Stamp(rimT + 0.1f, PlayerAction.Celebrating);
                    _ball.Add(new DeadSegment(netT, netT + 0.8f, new CourtPosition(_rimX, 0f)));
                    _totalT = netT + 0.8f;
                }
                else
                {
                    _reboundSpot = BallFlight.ComputeReboundSpot(_s.ShotPosition, _rimX, _rng);
                    float caromEnd = rimT + BallFlight.CaromDuration;
                    _ball.Add(new CaromSegment(rimT, caromEnd, _rimX, _reboundSpot));
                    BuildReboundScramble(caromEnd, _reboundSpot);
                    _totalT = caromEnd + 1.0f;
                }
                _phaseMarks.Add((rimT, PossessionPhase.Resolution));
            }

            // Free throws appended after the live resolution
            if (_s.FreeThrows != null && _s.FreeThrows.Attempts > 0)
                BuildFreeThrows();
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
                    break;
                }
                default: // Turnover / Violation: dribble dies, dead ball
                {
                    EndHeldAt(endT - 0.3f);
                    var pos = _offense[holder].PositionAt(endT - 0.3f);
                    _ball.Add(new DeadSegment(endT - 0.3f, endT + 0.7f, pos));
                    _offense[holder].Stamp(endT - 0.3f, PlayerAction.Idle);
                    _totalT = endT + 0.7f;
                    break;
                }
            }
            _phaseMarks.Add((endT - 0.3f, PossessionPhase.Resolution));
        }

        private void BuildFreeThrows()
        {
            int shooter = _s.ShooterIndex >= 0 ? _s.ShooterIndex : _s.InitialBallHandlerIndex;
            float setupStart = _totalT;
            float setupEnd = setupStart + 1.2f;

            _phaseMarks.Add((setupStart, PossessionPhase.FreeThrow));

            var offSpots = FormationLibrary.GetFreeThrowOffenseSpots(_s.OffenseAttacksRight, shooter, _rng);
            for (int i = 0; i < 5; i++)
                _offense[i].Add(setupEnd, offSpots[i], PlayerAction.Running);

            var ftLine = offSpots[shooter];
            float cursor = setupEnd + 0.3f;
            int made = _s.FreeThrows.Made;

            for (int a = 0; a < _s.FreeThrows.Attempts; a++)
            {
                bool thisMade = a < made; // presentational ordering: makes first
                _ball.Add(new DeadSegment(cursor - 0.3f, cursor, ftLine));
                _offense[shooter].Stamp(cursor, PlayerAction.Shooting);

                float flight = 0.9f;
                _ball.Add(new ShotSegment(cursor, cursor + flight, ftLine, _rimX, null));

                if (thisMade)
                {
                    _ball.Add(new MakeSegment(cursor + flight, cursor + flight + 0.3f, _rimX));
                    _ball.Add(new DeadSegment(cursor + flight + 0.3f, cursor + flight + 0.9f, new CourtPosition(_rimX, 0f)));
                }
                else
                {
                    var spot = BallFlight.ComputeReboundSpot(ftLine, _rimX, _rng);
                    _ball.Add(new CaromSegment(cursor + flight, cursor + flight + 0.5f, _rimX, spot));
                    _ball.Add(new DeadSegment(cursor + flight + 0.5f, cursor + flight + 0.9f, spot));
                }

                cursor += flight + 1.0f;
            }

            _totalT = cursor + 0.3f;
        }

        // ════════════════════════════════════════════════════════════════
        //  Ball segment helpers (keep the ball continuous across the timeline)
        // ════════════════════════════════════════════════════════════════

        private float _heldUntil;
        private int _heldBy = -1;

        private void AddHeld(ref float cursor, float until, int holderIdx, bool dribbled)
        {
            _ball.Add(new HeldSegment(cursor, until, _offense[holderIdx],
                _s.Offense[holderIdx].PlayerId, dribbled));
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
                _s.Offense[holderIdx].PlayerId, dribbled));
            _heldBy = holderIdx;
            _heldUntil = until;
        }

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
            while (t <= _totalT + 0.001f)
            {
                float dt = t - prevT;
                IntegrateDefense(t, dt);
                states.Add(Sample(t));
                prevT = t;

                bool inFlight = BallSegmentAt(t) is PassSegment || BallSegmentAt(t) is ShotSegment ||
                                BallSegmentAt(t) is CaromSegment || BallSegmentAt(t) is BlockSegment;
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

            // Gap safety: ball with current/last holder
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

            for (int i = 0; i < 5; i++)
            {
                CourtPosition target;
                float speed = DefenseSpeed;

                if (defenseRebounds && i == _s.RebounderIndex)
                {
                    target = _reboundSpot;
                    speed = SprintSpeed;
                }
                else if (resolution && _s.Ending == ScriptEnding.MadeShot && i == 0)
                {
                    // Point-of-attack defender collects the made ball under the rim
                    target = new CourtPosition(_rimX, 0f);
                }
                else if (IsContestMoment(t) && i == _s.ShooterIndex)
                {
                    // At the shot, the contest distance is the exact inverse of the
                    // decided ContestLevel so the visual matches the probability math.
                    float contestDist = (1f - _s.ContestLevel) * 6f;
                    var shooterPos = _offense[_s.ShooterIndex].PositionAt(t);
                    target = shooterPos.MoveTowards(new CourtPosition(_rimX, 0f), contestDist);
                    speed = SprintSpeed;
                }
                else
                {
                    var man = _offense[i].PositionAt(t);
                    float sag = SagFor(i, ball);
                    var rim = new CourtPosition(_rimX, 0f);
                    target = Blend(man, rim, sag);
                }

                _defPos[i] = _defPos[i].MoveTowards(target, speed * dt);
            }
        }

        private bool IsContestMoment(float t) =>
            _shotStartT > 0f && _s.ShooterIndex >= 0 && t > _shotStartT - 0.8f && t < _shotStartT + 0.3f;

        private float SagFor(int defenderIdx, BallState ball)
        {
            bool guardsBall = ball.HeldByPlayerId != null &&
                              _s.Offense[defenderIdx].PlayerId == ball.HeldByPlayerId;
            if (guardsBall) return 0.12f;

            // One pass away = adjacent perimeter neighbor of the ball handler; weak side sags more
            return _rng.NextDouble() < 0.5 ? 0.22f : 0.34f;
        }

        private PlayerAction DefenseActionAt(int i, float t)
        {
            if (IsContestMoment(t) && i == _s.ShooterIndex) return PlayerAction.Contesting;
            if (t > _liveEndT && _s.RebounderIsDefense && i == _s.RebounderIndex) return PlayerAction.Rebounding;
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

            public HeldSegment(float t0, float t1, Track holder, string playerId, bool dribbled)
            {
                T0 = t0; T1 = t1; _holder = holder; _playerId = playerId; _dribbled = dribbled;
            }

            public override BallState Sample(float t)
            {
                var pos = _holder.PositionAt(t);
                float height = _dribbled
                    ? 1.5f + 1.5f * (float)Math.Abs(Math.Sin(t * 6f))   // dribble bounce
                    : CourtGeometry.HeldBallHeight;
                return new BallState(pos.X, pos.Y, height)
                {
                    Status = _dribbled ? BallStatus.Dribbled : BallStatus.Held,
                    HeldByPlayerId = _playerId
                };
            }
        }

        private class PassSegment : BallSegment
        {
            private readonly CourtPosition _from, _to;
            public PassSegment(float t0, float t1, CourtPosition from, CourtPosition to)
            { T0 = t0; T1 = t1; _from = from; _to = to; }
            public override BallState Sample(float t) => BallFlight.SamplePass(_from, _to, U(t));
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

        private class MakeSegment : BallSegment
        {
            private readonly float _rimX;
            public MakeSegment(float t0, float t1, float rimX) { T0 = t0; T1 = t1; _rimX = rimX; }
            public override BallState Sample(float t) => BallFlight.SampleMadeFollowThrough(_rimX, U(t));
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
