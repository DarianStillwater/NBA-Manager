using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;
using SimShotType = NBAHeadCoach.Core.Simulation.ShotType;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Dynamic multi-camera broadcast director. Owns ONE Unity <see cref="Camera"/> and moves it
    /// between virtual shot rigs (no extra Camera components). The full possession timeline is known
    /// at <see cref="BeginPossession"/>, so cuts are PRE-PLANNED from the PlayerAction stream, the
    /// free-throw tail, and the buzzer-beater clock — then executed frame-by-frame during playback.
    ///
    /// Execution model: <see cref="Tick"/> selects the active planned cut by playback time and HARD
    /// CUTS (teleports) the rig when the shot changes, SmoothDamping the framing WITHIN a rig for a
    /// settled look. During bridges (<see cref="TickBridge"/>) the previous shot is retained; during
    /// fast-forward skips (<see cref="SetSkip"/>) the rig is forced to a wide Broadcast.
    ///
    /// Coordinate contract matches the rest of Match3D: the camera is a child of the world root,
    /// 1 unit = 1 foot, court X[-47,47] → local X, court Y[-25,25] → local Z, height → local Y.
    /// Replaces the always-on <see cref="MatchBroadcastCamera"/>; its sideline-follow math lives on
    /// as the Broadcast shot below.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        public enum CameraShot { Broadcast, BaselineLow, UnderRim, FreeThrow, BuzzerBeater }

        private struct PlannedCut
        {
            public float StartTime;   // playback seconds (matches Match3DView.RenderAt's t)
            public CameraShot Shot;
        }

        // ── Broadcast rig (ported verbatim from MatchBroadcastCamera) ──
        private const float BroadcastHeight = 27f;
        private const float BroadcastDepth = -56f;
        private const float BroadcastLookHeight = 4f;
        private const float BroadcastSmoothTime = 0.45f;
        private const float MaxCameraX = 22f;
        private const float MaxLookX = 38f;
        private const float WideFov = 44f;
        private const float TightFov = 38f;
        private const float NearBasketX = 30f;

        // The near sideline the fixed rigs favour (Broadcast sits on −Z, so that is the camera side).
        private const float CameraSideZ = -1f;

        // ── Cut-planning constants ──
        private const float MinShotDuration = 1.2f;   // never cut twice within this window
        private const float PostShotHold = 0.8f;      // hold on a rim cam after the shot resolves
        private const float BuzzerClockThreshold = 3f;

        private Camera _camera;

        // Plan for the current possession + a cursor into it.
        private readonly List<PlannedCut> _plan = new List<PlannedCut>();
        private int _planCursor;

        // Possession geometry resolved once at plan time.
        private float _attackSign = 1f;               // +1 = offense attacks +X rim, −1 = −X
        private Vector3 _rimWorld = new Vector3(CourtGeometry.RimX, CourtGeometry.RimHeight, 0f);

        // Live rig state (smoothed within a shot; snapped on a cut).
        private CameraShot _activeShot = CameraShot.Broadcast;
        private Vector3 _pos;
        private Vector3 _look;
        private float _fov = WideFov;
        private Vector3 _posVel;
        private Vector3 _lookVel;
        private float _shotElapsed;   // seconds since the last hard cut (drives dramatic drift)

        private bool _skip;

        public void Configure(Camera cam)
        {
            _camera = cam;
            _fov = WideFov;
            cam.fieldOfView = WideFov;
            _activeShot = CameraShot.Broadcast;
            _pos = new Vector3(0f, BroadcastHeight, BroadcastDepth);
            _look = new Vector3(0f, BroadcastLookHeight, 0f);
            _posVel = Vector3.zero;
            _lookVel = Vector3.zero;
            Apply();
        }

        // ── Planning ──

        /// <summary>Scan the packet's timeline once and pre-plan the camera cuts for this possession.
        /// Logs one line summarising the plan (visible in Editor.log) for verification.</summary>
        public void BeginPossession(PossessionPlaybackPacket packet)
        {
            _plan.Clear();
            _planCursor = 0;
            // Do NOT touch _activeShot here — the inter-possession bridge keeps the previous rig
            // until the first Tick(t=0) selects this possession's opening (Broadcast) cut.

            _plan.Add(new PlannedCut { StartTime = 0f, Shot = CameraShot.Broadcast });

            var timeline = packet?.Timeline;
            if (timeline == null || timeline.Count == 0)
            {
                LogPlan(packet);
                return;
            }

            float start = packet.StartGameClock;

            // Resolve the attacking rim from the shot (falls back to the last ball position).
            _attackSign = ResolveAttackSign(packet, out int shotFrame, out bool isDunk, out bool isLayup);
            _rimWorld = new Vector3(_attackSign * CourtGeometry.RimX, CourtGeometry.RimHeight, 0f);

            float PlaybackTimeOf(int frameIdx)
            {
                float rel = start - timeline[Mathf.Clamp(frameIdx, 0, timeline.Count - 1)].GameClock;
                return Mathf.Max(rel, 0f);
            }

            bool isBuzzer = packet.Quarter >= 4 &&
                            packet.EndGameClock < BuzzerClockThreshold &&
                            shotFrame >= 0;

            if (isBuzzer)
            {
                // Dramatic elevated-corner hold on the final shot; no return to Broadcast — the
                // possession ends on the held frame.
                _plan.Add(new PlannedCut { StartTime = PlaybackTimeOf(shotFrame), Shot = CameraShot.BuzzerBeater });
            }
            else if (shotFrame >= 0 && (isDunk || isLayup))
            {
                float cutIn = PlaybackTimeOf(shotFrame);
                _plan.Add(new PlannedCut
                {
                    StartTime = cutIn,
                    Shot = isDunk ? CameraShot.UnderRim : CameraShot.BaselineLow
                });

                // Cut back to Broadcast after the shot resolves + a short hold.
                float resolve = ResolveShotOffset(packet, cutIn);
                _plan.Add(new PlannedCut { StartTime = resolve + PostShotHold, Shot = CameraShot.Broadcast });
            }
            // Plain jumpers / non-shot possessions stay on Broadcast.

            // Free-throw tail: a foul draws FTs appended past the live window (TailSeconds > 0).
            if (packet.TailSeconds > 0.1f)
            {
                float ftStart = packet.LiveSeconds > 0.01f ? packet.LiveSeconds : packet.DurationGameSeconds;
                _plan.Add(new PlannedCut { StartTime = ftStart, Shot = CameraShot.FreeThrow });
            }

            EnforceMinDuration(packet.PlaybackSeconds);
            LogPlan(packet);
        }

        /// <summary>Determine which rim the offense attacks, plus whether the resolving shot is a
        /// dunk/layup and the timeline frame where the shot action begins.</summary>
        private static float ResolveAttackSign(PossessionPlaybackPacket packet,
            out int shotFrame, out bool isDunk, out bool isLayup)
        {
            shotFrame = -1;
            isDunk = false;
            isLayup = false;

            var timeline = packet.Timeline;
            float shooterX = 0f;
            bool found = false;

            for (int i = 0; i < timeline.Count && !found; i++)
            {
                var players = timeline[i].Players;
                for (int p = 0; p < players.Length; p++)
                {
                    var act = players[p].CurrentAction;
                    if (act == PlayerAction.Dunking || act == PlayerAction.Layup ||
                        act == PlayerAction.Shooting)
                    {
                        shotFrame = i;
                        shooterX = players[p].X;
                        isDunk = act == PlayerAction.Dunking;
                        isLayup = act == PlayerAction.Layup;
                        found = true;
                        break;
                    }
                }
            }

            // The ball's shot style is a more reliable dunk cue than the (possibly-late) action flag.
            if (found)
            {
                for (int i = shotFrame; i < timeline.Count; i++)
                {
                    var style = timeline[i].Ball.ShotStyle;
                    if (style.HasValue)
                    {
                        isDunk = style.Value == SimShotType.Dunk;
                        isLayup = style.Value == SimShotType.Layup ||
                                  style.Value == SimShotType.Floater ||
                                  style.Value == SimShotType.Hookshot ||
                                  style.Value == SimShotType.TipIn;
                        break;
                    }
                }
                return shooterX >= 0f ? 1f : -1f;
            }

            // No shot action: aim toward wherever the ball ended up.
            var last = timeline[timeline.Count - 1];
            return last.Ball.X >= 0f ? 1f : -1f;
        }

        /// <summary>Playback offset of the resolving field-goal marker (make/miss), so the rim cam
        /// holds until the ball has cleared the rim. Falls back to the cut-in time.</summary>
        private static float ResolveShotOffset(PossessionPlaybackPacket packet, float fallback)
        {
            float best = fallback;
            var events = packet.Events;
            if (events != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].ShotMarker.HasValue && events[i].Offset > best)
                        best = events[i].Offset;
                }
            }
            return best;
        }

        /// <summary>Sort the plan and push any cut that lands within MinShotDuration of its
        /// predecessor forward, then drop cuts pushed past the end of playback.</summary>
        private void EnforceMinDuration(float totalSeconds)
        {
            _plan.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            for (int i = 1; i < _plan.Count; i++)
            {
                float minStart = _plan[i - 1].StartTime + MinShotDuration;
                if (_plan[i].StartTime < minStart)
                {
                    var c = _plan[i];
                    c.StartTime = minStart;
                    _plan[i] = c;
                }
            }

            if (totalSeconds > 0.01f)
            {
                for (int i = _plan.Count - 1; i >= 1; i--)
                {
                    if (_plan[i].StartTime >= totalSeconds)
                        _plan.RemoveAt(i);
                }
            }
        }

        private void LogPlan(PossessionPlaybackPacket packet)
        {
            var sb = new StringBuilder();
            sb.Append("[CameraDirector] Q");
            sb.Append(packet != null ? packet.Quarter : 0);
            if (packet != null)
            {
                sb.Append(' ');
                sb.Append(packet.StartGameClock.ToString("0.0"));
                sb.Append("->");
                sb.Append(packet.EndGameClock.ToString("0.0"));
            }
            sb.Append(" cuts:");
            for (int i = 0; i < _plan.Count; i++)
            {
                sb.Append(' ');
                sb.Append(_plan[i].Shot);
                sb.Append('@');
                sb.Append(_plan[i].StartTime.ToString("0.0"));
                if (i < _plan.Count - 1) sb.Append(',');
            }
            Debug.Log(sb.ToString());
        }

        // ── Execution ──

        /// <summary>Drive the rig at playback time t (seconds from possession start), following the
        /// ball's world position. Hard-cuts when the planned shot changes, SmoothDamps otherwise.</summary>
        public void Tick(float t, Vector3 ballWorld)
        {
            if (_camera == null) return;

            CameraShot want = _skip ? CameraShot.Broadcast : SelectShot(t);
            if (want != _activeShot) HardCut(want, ballWorld);

            ComputeTarget(_activeShot, ballWorld, out Vector3 targetPos, out Vector3 targetLook, out float targetFov);

            float smooth = SmoothTimeFor(_activeShot);
            float dt = Time.deltaTime;
            _shotElapsed += dt;

            _pos = Vector3.SmoothDamp(_pos, targetPos, ref _posVel, smooth);
            _look = Vector3.SmoothDamp(_look, targetLook, ref _lookVel, smooth);
            _fov = Mathf.Lerp(_fov, targetFov, 3f * dt);
            Apply();
        }

        /// <summary>Inter-possession bridge: keep the current rig, just track the sliding ball so the
        /// hand-off doesn't jump. Never advances the plan.</summary>
        public void TickBridge(Vector3 ballWorld)
        {
            if (_camera == null) return;
            ComputeTarget(_activeShot, ballWorld, out Vector3 targetPos, out Vector3 targetLook, out float targetFov);
            float smooth = SmoothTimeFor(_activeShot);
            _pos = Vector3.SmoothDamp(_pos, targetPos, ref _posVel, smooth);
            _look = Vector3.SmoothDamp(_look, targetLook, ref _lookVel, smooth);
            _fov = Mathf.Lerp(_fov, targetFov, 3f * Time.deltaTime);
            Apply();
        }

        /// <summary>Fast-forward skip: force a wide Broadcast and freeze cut selection.</summary>
        public void SetSkip(bool on)
        {
            if (_skip == on) return;
            _skip = on;
            if (on && _activeShot != CameraShot.Broadcast)
            {
                // Snap to Broadcast so the skipped stretch reads as a stable wide.
                _activeShot = CameraShot.Broadcast;
                _shotElapsed = 0f;
                _posVel = Vector3.zero;
                _lookVel = Vector3.zero;
            }
        }

        private CameraShot SelectShot(float t)
        {
            // Advance the cursor to the latest cut whose start time has passed.
            while (_planCursor + 1 < _plan.Count && _plan[_planCursor + 1].StartTime <= t)
                _planCursor++;
            // A rewind shouldn't happen (t is monotonic per possession), but stay safe.
            while (_planCursor > 0 && _plan[_planCursor].StartTime > t)
                _planCursor--;
            return _plan.Count > 0 ? _plan[_planCursor].Shot : CameraShot.Broadcast;
        }

        private void HardCut(CameraShot shot, Vector3 ballWorld)
        {
            _activeShot = shot;
            _shotElapsed = 0f;
            ComputeTarget(shot, ballWorld, out Vector3 targetPos, out Vector3 targetLook, out float targetFov);
            _pos = targetPos;
            _look = targetLook;
            _fov = targetFov;
            _posVel = Vector3.zero;
            _lookVel = Vector3.zero;
            Apply();
        }

        private void Apply()
        {
            transform.localPosition = _pos;
            var dir = _look - _pos;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            if (_camera != null) _camera.fieldOfView = _fov;
        }

        private static float SmoothTimeFor(CameraShot shot)
        {
            switch (shot)
            {
                case CameraShot.Broadcast: return BroadcastSmoothTime;
                case CameraShot.BuzzerBeater: return 0.9f;   // slow, dramatic settle
                default: return 0.3f;                        // fixed rigs track the ball gently
            }
        }

        /// <summary>Per-shot target framing: where the rig wants to sit, look, and how wide.</summary>
        private void ComputeTarget(CameraShot shot, Vector3 ballWorld,
            out Vector3 pos, out Vector3 look, out float fov)
        {
            float rimX = _rimWorld.x;

            switch (shot)
            {
                case CameraShot.BaselineLow:
                    // Low camera near the attacking baseline corner (camera-side sideline), looking
                    // back up the paint toward the rim — reads a driving layup.
                    pos = new Vector3(_attackSign * 44f, 7f, 19f * CameraSideZ);
                    look = new Vector3(rimX, 6f, ballWorld.z * 0.4f);
                    fov = 52f;
                    return;

                case CameraShot.UnderRim:
                    // Behind/under the backboard looking back out at the incoming dunker.
                    pos = new Vector3(_attackSign * 50f, 11f, 0f);
                    look = new Vector3(rimX - _attackSign * 12f, 8f, ballWorld.z * 0.3f);
                    fov = 58f;
                    return;

                case CameraShot.FreeThrow:
                    // Behind the FT shooter (nearer center than the FT line) at head height,
                    // framing shooter + rim down the lane.
                    pos = new Vector3(_attackSign * 18f, 6.5f, 0f);
                    look = new Vector3(rimX, 9f, 0f);
                    fov = 34f;
                    return;

                case CameraShot.BuzzerBeater:
                    // Elevated corner with a slow tightening zoom for drama.
                    pos = new Vector3(_attackSign * 38f, 24f, 30f * CameraSideZ);
                    look = new Vector3(rimX, 9f, 0f);
                    fov = Mathf.Lerp(46f, 38f, Mathf.Clamp01(_shotElapsed / 2.5f));
                    return;

                default: // Broadcast
                    float camX = Mathf.Clamp(ballWorld.x, -MaxCameraX, MaxCameraX);
                    float lookX = Mathf.Clamp(ballWorld.x, -MaxLookX, MaxLookX);
                    pos = new Vector3(camX, BroadcastHeight, BroadcastDepth);
                    look = new Vector3(lookX, BroadcastLookHeight, 0f);
                    fov = Mathf.Abs(ballWorld.x) > NearBasketX ? TightFov : WideFov;
                    return;
            }
        }
    }
}
