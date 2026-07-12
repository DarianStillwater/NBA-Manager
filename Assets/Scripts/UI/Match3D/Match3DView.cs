using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.UI;
using NBAHeadCoach.UI.Match;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// 3D consumer of the same possession timeline the 2D court renders. Ports
    /// MatchCourtView's exact sampling: BeginPossession caches the dense SpatialStates and their
    /// per-state relative times; RenderAt(t) binary-advances a cursor and lerps the bracketing
    /// pair; BridgeRender slides actors + ball across the inter-possession gap. Court feet map
    /// 1:1 to world units (X→X, Y→Z, ball Height→Y), so no projection math is needed.
    ///
    /// Implements IMatchView so the playback director drives it identically to the 2D court.
    /// Coach reactions and shot markers are 3D-TODO no-ops for now (P1 scope is capsules on a
    /// court); position, ball, fast-forward, and lineup handling are fully live.
    /// </summary>
    public class Match3DView : MonoBehaviour, IMatchView
    {
        private Match3DWorld _world;
        private Transform _actorParent;
        private Camera _camera;
        private CameraDirector _director;
        private Light _keyLight;
        private float _baseLightIntensity = 1.15f;

        private readonly Dictionary<string, PlayerActor3D> _actors = new Dictionary<string, PlayerActor3D>();
        private List<string> _homeLineup = new List<string>();
        private List<string> _awayLineup = new List<string>();
        private Color _homeColor = new Color(0.2f, 0.4f, 0.8f);
        private Color _awayColor = new Color(0.8f, 0.2f, 0.2f);
        private Ball3D _ball;

        // Active possession timeline (identical bookkeeping to MatchCourtView).
        private List<SpatialState> _timeline;
        private float[] _relTimes;
        private int _cursor;

        // Inter-possession bridge, stored in court feet.
        private readonly Dictionary<string, Vector2> _bridgeFrom = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Vector2> _bridgeTo = new Dictionary<string, Vector2>();
        private Vector3 _bridgeBallFrom, _bridgeBallTo;
        private bool _bridgeReady;
        private Vector3 _lastBall;

        // Per-frame scratch for the compute → soft-separation → commit passes in RenderAt.
        // Pre-allocated (max 10 actors) so RenderAt never allocates.
        private readonly Vector2[] _sepPositions = new Vector2[10];
        private readonly PlayerSnapshot[] _sepSnaps = new PlayerSnapshot[10];
        private readonly Vector2[] _sepOffsets = new Vector2[10];
        private readonly ActorFrame[] _sepFrames = new ActorFrame[10];
        private readonly PlayerActor3D[] _sepActors = new PlayerActor3D[10];
        private readonly float[] _sepFacing = new float[10];
        private readonly Vector3[] _actorWorld = new Vector3[10];

        #region Setup

        public void Initialize(Match3DWorld world, Team homeTeam, Team awayTeam,
            List<string> homeLineup, List<string> awayLineup)
        {
            _world = world;
            _actorParent = world != null && world.Root != null ? world.Root.transform : transform;
            _director = world != null ? world.Camera : null;
            _camera = _director != null ? _director.GetComponent<Camera>() : null;
            _keyLight = _actorParent != null ? _actorParent.GetComponentInChildren<Light>() : null;
            if (_keyLight != null) _baseLightIntensity = _keyLight.intensity;

            ResolveTeamColors(homeTeam, awayTeam);
            _homeLineup = new List<string>(homeLineup);
            _awayLineup = new List<string>(awayLineup);

            ClearActors();

            foreach (var id in Take5(homeLineup)) CreateActor(id, true);
            foreach (var id in Take5(awayLineup)) CreateActor(id, false);

            _ball = Ball3D.Create(_actorParent);
        }

        private static IEnumerable<string> Take5(List<string> ids)
        {
            int n = Mathf.Min(5, ids.Count);
            for (int i = 0; i < n; i++) yield return ids[i];
        }

        private PlayerActor3D CreateActor(string playerId, bool isHome)
        {
            if (string.IsNullOrEmpty(playerId) || _actors.ContainsKey(playerId))
                return string.IsNullOrEmpty(playerId) ? null : _actors.GetValueOrDefault(playerId);

            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);
            int jersey = int.TryParse(player?.JerseyNumber, out var jn) ? jn : -1;
            var color = isHome ? _homeColor : _awayColor;
            var appearance = PlayerAppearance.For(player, color, jersey);

            var actor = PlayerActor3D.Create(_actorParent, _camera, playerId, appearance);
            actor.SetPositionImmediate(0f, isHome ? -2f : 2f);
            _actors[playerId] = actor;
            return actor;
        }

        private void RemoveActor(string playerId)
        {
            if (_actors.TryGetValue(playerId, out var a))
            {
                if (a != null) Destroy(a.gameObject);
                _actors.Remove(playerId);
            }
        }

        private void ClearActors()
        {
            foreach (var kvp in _actors)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            _actors.Clear();
        }

        // ── Team colors ──

        /// <summary>Resolve home/away display colors from the team objects, mirroring the 2D dot
        /// resolution (home primary, away secondary, wash-out fallback). The one deliberate
        /// difference: the too-similar contrast fallback stays team-faithful — it falls back to the
        /// away team's OTHER real color (e.g. Clippers navy → Clippers red against Lakers purple)
        /// instead of the shared 2D path's +0.5 hue rotation, which turned navy into a gold that
        /// read as the home team's secondary.</summary>
        private void ResolveTeamColors(Team homeTeam, Team awayTeam)
        {
            _homeColor = ResolveTeamColor(homeTeam, preferSecondary: false);
            _awayColor = ResolveTeamColor(awayTeam, preferSecondary: true);

            if (ColorDistance(_homeColor, _awayColor) < 0.5f)
            {
                Color alt = ResolveTeamColor(awayTeam, preferSecondary: false);
                if (ColorDistance(_homeColor, alt) > ColorDistance(_homeColor, _awayColor))
                    _awayColor = alt;

                // Still colliding → separate by brightness while preserving hue (stays in-family).
                if (ColorDistance(_homeColor, _awayColor) < 0.5f)
                {
                    Color.RGBToHSV(_awayColor, out float h, out float s, out float v);
                    _awayColor = Color.HSVToRGB(h, Mathf.Max(s, 0.6f), v > 0.55f ? 0.4f : 0.9f);
                }
            }
        }

        private static Color ResolveTeamColor(Team team, bool preferSecondary)
        {
            Color primary = UITheme.ParseTeamColor(team?.PrimaryColor, Color.gray);
            Color secondary = UITheme.ParseTeamColor(team?.SecondaryColor, Color.gray);

            Color chosen = preferSecondary ? secondary : primary;
            Color other = preferSecondary ? primary : secondary;

            if (IsWashedOut(chosen) && !IsWashedOut(other)) chosen = other;
            if (IsWashedOut(chosen))
            {
                Color.RGBToHSV(chosen, out float h, out float s, out float v);
                chosen = Color.HSVToRGB(h, Mathf.Max(s, 0.25f), 0.7f);
            }
            return chosen;
        }

        private static bool IsWashedOut(Color c) => c.r > 0.8f && c.g > 0.8f && c.b > 0.8f;

        private static float ColorDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        /// <summary>Diff-based lineup update — mirrors MatchCourtView.UpdateLineup so the 3D floor
        /// tracks subs/foul-outs when the scene routes them here.</summary>
        public void UpdateLineup(List<string> homeLineup, List<string> awayLineup)
        {
            var oldHome = new HashSet<string>(_homeLineup);
            var oldAway = new HashSet<string>(_awayLineup);

            foreach (var id in _homeLineup) if (!homeLineup.Contains(id)) RemoveActor(id);
            foreach (var id in _awayLineup) if (!awayLineup.Contains(id)) RemoveActor(id);
            foreach (var id in homeLineup) if (!oldHome.Contains(id)) CreateActor(id, true);
            foreach (var id in awayLineup) if (!oldAway.Contains(id)) CreateActor(id, false);

            _homeLineup = new List<string>(homeLineup);
            _awayLineup = new List<string>(awayLineup);
        }

        /// <summary>Swap one player: the incoming actor spawns at the outgoing actor's spot.</summary>
        public void ApplySubstitution(string outId, string inId, bool isHome)
        {
            if (string.IsNullOrEmpty(outId) || string.IsNullOrEmpty(inId)) return;

            Vector3 spot = _actors.TryGetValue(outId, out var outActor) && outActor != null
                ? outActor.transform.localPosition : Vector3.zero;

            RemoveActor(outId);
            var inActor = CreateActor(inId, isHome);
            if (inActor != null) inActor.SetPositionImmediate(spot.x, spot.z);

            var lineup = isHome ? _homeLineup : _awayLineup;
            int idx = lineup.IndexOf(outId);
            if (idx >= 0) lineup[idx] = inId;
        }

        #endregion

        #region IMatchView

        public void BeginPossession(PossessionPlaybackPacket packet)
        {
            // Pre-plan the camera cuts for this possession from its known timeline (logs the plan).
            // Doesn't change the live rig — the bridge keeps the previous shot until the first Tick.
            _director?.BeginPossession(packet);

            _timeline = packet?.Timeline;
            _cursor = 0;

            if (_timeline == null || _timeline.Count == 0)
            {
                _relTimes = null;
                return;
            }

            float start = packet.StartGameClock;
            // Legacy timelines (predating PresentationTime) never set it past 0; fall back to the
            // old GameClock reconstruction for those so old saves/tests keep working.
            bool legacy = _timeline.Count <= 1 || _timeline[_timeline.Count - 1].PresentationTime <= 0f;
            _relTimes = new float[_timeline.Count];
            float prev = 0f;
            for (int i = 0; i < _timeline.Count; i++)
            {
                float rel = legacy ? start - _timeline[i].GameClock : _timeline[i].PresentationTime;
                if (rel < prev) rel = prev;   // keep strictly non-decreasing at clamped boundaries
                _relTimes[i] = rel;
                prev = rel;
            }

            CaptureBridge();
        }

        private void CaptureBridge()
        {
            _bridgeFrom.Clear();
            _bridgeTo.Clear();
            _bridgeReady = false;
            if (_timeline == null || _timeline.Count == 0) return;

            var first = _timeline[0];
            float maxDisp = 0f;
            for (int i = 0; i < first.Players.Length; i++)
            {
                var ps = first.Players[i];
                if (string.IsNullOrEmpty(ps.PlayerId)) continue;
                if (!_actors.TryGetValue(ps.PlayerId, out var actor) || actor == null) continue;
                var cur = actor.transform.localPosition;
                var from = new Vector2(cur.x, cur.z);
                var to = new Vector2(ps.X, ps.Y);
                _bridgeFrom[ps.PlayerId] = from;
                _bridgeTo[ps.PlayerId] = to;
                maxDisp = Mathf.Max(maxDisp, Vector2.Distance(from, to));
            }

            // Continuous-flow timelines start where the last ended, so the slide is imperceptible —
            // skip it. The bridge stays the fallback for subs / quarter starts / skips (big moves).
            if (!ActorMotion.ShouldBridge(maxDisp)) return;

            _bridgeBallTo = new Vector3(first.Ball.X, Mathf.Max(first.Ball.Height, 0.1f), first.Ball.Y);
            _bridgeBallFrom = _lastBall == Vector3.zero ? _bridgeBallTo : _lastBall;
            _bridgeReady = true;
        }

        public void BridgeRender(float u)
        {
            if (!_bridgeReady) return;
            u = Mathf.Clamp01(u);

            foreach (var kvp in _bridgeTo)
            {
                if (!_actors.TryGetValue(kvp.Key, out var actor) || actor == null) continue;
                var from = _bridgeFrom.TryGetValue(kvp.Key, out var f) ? f : kvp.Value;
                var p = Vector2.Lerp(from, kvp.Value, u);
                actor.SetPositionImmediate(p.x, p.y);
            }

            if (_ball != null)
            {
                var pos = Vector3.Lerp(_bridgeBallFrom, _bridgeBallTo, u);
                _ball.Render(pos.x, pos.z, pos.y);
                _lastBall = pos;
                _director?.TickBridge(pos);
            }
        }

        public void RenderAt(float t)
        {
            if (_timeline == null || _timeline.Count == 0) return;

            while (_cursor < _timeline.Count - 2 && _relTimes[_cursor + 1] <= t)
                _cursor++;

            var a = _timeline[_cursor];
            var b = _cursor + 1 < _timeline.Count ? _timeline[_cursor + 1] : a;
            float span = _relTimes[Mathf.Min(_cursor + 1, _timeline.Count - 1)] - _relTimes[_cursor];
            float u = span <= 0.0001f ? 1f : Mathf.Clamp01((t - _relTimes[_cursor]) / span);

            // Track the ball-handler so a Held/Dribbled ball rides his hands rather than the
            // possibly-stale BallState coordinate (the sim leaves it near center while held).
            bool handlerFound = false;
            float handlerX = 0f, handlerY = 0f, handlerFacing = 0f;
            PlayerActor3D handlerActor = null;
            string heldBy = b.Ball.HeldByPlayerId;

            // ── Compute pass: gather each live actor's authored (lerped) position + frame into the
            //    scratch arrays. Scripted state (action/stance) reads the target frame so an
            //    edge-triggered clip (shot/dunk/leap) fires promptly; physical fields lerp. ──
            int n = 0;
            for (int i = 0; i < 10 && i < a.Players.Length; i++)
            {
                var pa = a.Players[i];
                if (string.IsNullOrEmpty(pa.PlayerId)) continue;
                if (!_actors.TryGetValue(pa.PlayerId, out var actor) || actor == null) continue;

                var pb = i < b.Players.Length && b.Players[i].PlayerId == pa.PlayerId ? b.Players[i] : pa;

                _sepActors[n] = actor;
                _sepSnaps[n] = pb;
                _sepFacing[n] = pb.FacingAngle;
                _sepPositions[n] = new Vector2(Mathf.Lerp(pa.X, pb.X, u), Mathf.Lerp(pa.Y, pb.Y, u));
                _sepFrames[n] = new ActorFrame
                {
                    SpeedFeetPerSec = Mathf.Lerp(pa.SpeedFeetPerSec, pb.SpeedFeetPerSec, u),
                    VerticalOffset = Mathf.Lerp(pa.VerticalOffset, pb.VerticalOffset, u),
                    Action = pb.CurrentAction,
                    ActionPhase = pb.ActionPhase,
                    DefensiveStance = pb.DefensiveStance,
                    HasBall = pb.HasBall,
                };
                n++;
            }

            // Resolve the ball-handler id (explicit HeldByPlayerId, else the HasBall flag) so the
            // separation pass pins him and never displaces the carry point.
            string ballHandlerId = heldBy;
            if (string.IsNullOrEmpty(ballHandlerId))
                for (int i = 0; i < n; i++)
                    if (_sepSnaps[i].HasBall) { ballHandlerId = _sepSnaps[i].PlayerId; break; }

            // ── Separation pass: fresh per-frame XY nudges so bodies never interpenetrate. ──
            SoftSeparation.Resolve(_sepPositions, _sepSnaps, n, ballHandlerId, _sepOffsets);

            // ── Commit pass: apply offset positions + resolve choreographed contact lean, then
            //    hand each actor its frame. Contact direction points into the TargetPlayerId partner. ──
            for (int i = 0; i < n; i++)
            {
                var s = _sepSnaps[i];
                Vector2 fp = _sepPositions[i] + _sepOffsets[i];

                _sepFrames[i].HasContact = false;
                if (SoftSeparation.IsContactAction(s.CurrentAction) && !string.IsNullOrEmpty(s.TargetPlayerId))
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i || _sepSnaps[j].PlayerId != s.TargetPlayerId) continue;
                        Vector2 d = (_sepPositions[j] + _sepOffsets[j]) - fp;
                        if (d.sqrMagnitude > 1e-4f)
                        {
                            d.Normalize();
                            _sepFrames[i].HasContact = true;
                            _sepFrames[i].ContactDir = d;
                        }
                        break;
                    }
                }

                var actor = _sepActors[i];
                actor.SetFrame(fp.x, fp.y, _sepFacing[i], in _sepFrames[i]);
                _actorWorld[i] = new Vector3(fp.x, 0f, fp.y);

                if (!handlerFound &&
                    (s.HasBall || (!string.IsNullOrEmpty(heldBy) && s.PlayerId == heldBy)))
                {
                    handlerFound = true;
                    handlerX = fp.x; handlerY = fp.y; handlerFacing = _sepFacing[i];
                    handlerActor = actor;
                }
            }

            if (_ball != null)
            {
                var status = b.Ball.Status;
                bool dribbled = status == BallStatus.Dribbled;
                bool carried = handlerFound &&
                    (status == BallStatus.Held || dribbled || status == BallStatus.DeadBall);

                if (carried)
                {
                    // Ride the actual hand bone when the character rig exposes one, so the ball
                    // follows the retargeted pose; fall back to the fixed-offset carry otherwise.
                    // Dribbling always uses the AUTHORED floor-to-hand bounce height (|sin| cusp =
                    // floor hit) — held/deadball uses the hand's natural height.
                    float authoredHeight = Mathf.Lerp(a.Ball.Height, b.Ball.Height, u);
                    Transform hand = handlerActor != null ? handlerActor.HandBone : null;

                    if (hand != null && _actorParent != null)
                    {
                        Vector3 lp = _actorParent.InverseTransformPoint(hand.position);
                        float h = dribbled ? authoredHeight : lp.y;
                        _ball.RenderHand(lp.x, lp.z, h, handlerFacing);
                    }
                    else if (dribbled)
                    {
                        // No hand bone: bounce at the handler's planar spot with the authored height.
                        _ball.RenderHand(handlerX, handlerY, authoredHeight, handlerFacing);
                    }
                    else
                    {
                        _ball.RenderHeld(handlerX, handlerY, handlerFacing);
                    }
                }
                else
                {
                    // In flight / loose: honor the choreographed BallState coordinate + arc height.
                    float bx = Mathf.Lerp(a.Ball.X, b.Ball.X, u);
                    float by = Mathf.Lerp(a.Ball.Y, b.Ball.Y, u);
                    float bh = Mathf.Lerp(a.Ball.Height, b.Ball.Height, u);
                    _ball.Render(bx, by, bh);
                }

                _lastBall = _ball.WorldFocusPoint;
                _director?.Tick(t, _lastBall, _actorWorld, n);
            }

            // Declutter jersey labels: only the ball-handler and players near the ball keep theirs.
            var ballXZ = new Vector3(_lastBall.x, 0f, _lastBall.z);
            foreach (var kvp in _actors)
                if (kvp.Value != null) kvp.Value.SetLabelFocus(ballXZ);

            NudgeOverlappingLabels();
        }

        // Anti-overlap: when actors cluster within ~1.5 ft, stack their number labels vertically so
        // the text doesn't pile onto one another. O(n^2) over ≤10 actors — trivial.
        private const float LabelOverlapFeet = 1.5f;
        private const float LabelStackStepFeet = 1.3f;
        private readonly List<PlayerActor3D> _nudgeScratch = new List<PlayerActor3D>();

        private void NudgeOverlappingLabels()
        {
            _nudgeScratch.Clear();
            foreach (var kvp in _actors)
            {
                if (kvp.Value == null) continue;
                kvp.Value.SetLabelExtraHeight(0f);
                _nudgeScratch.Add(kvp.Value);
            }

            float sqr = LabelOverlapFeet * LabelOverlapFeet;
            for (int i = 0; i < _nudgeScratch.Count; i++)
            {
                int stack = 0;
                var pi = _nudgeScratch[i].PlanarPosition;
                for (int j = 0; j < i; j++)
                {
                    if ((pi - _nudgeScratch[j].PlanarPosition).sqrMagnitude <= sqr) stack++;
                }
                if (stack > 0) _nudgeScratch[i].SetLabelExtraHeight(stack * LabelStackStepFeet);
            }
        }

        /// <summary>Made-shot hoop FX: punch the net at whichever basket the shot went to, and add a
        /// short camera shake on a dunk. Degrades to nothing if the world/hoops aren't present.</summary>
        public void ResolveShot(ShotMarkerData data, bool made)
        {
            if (!made || _world == null) return;

            var hoop = _world.HoopNearestX(data.Position.X);
            hoop?.Punch();

            // ShotType is ambiguous here (Core.Data vs Core.Simulation both imported) — qualify.
            if (data.ShotType == NBAHeadCoach.Core.Data.ShotType.Dunk)
                _director?.Shake(1.3f, 0.4f);
        }

        // ── Jumbotron relay (driven by MatchSceneSetup from the same director scoreboard/clock
        //    events the 2D HUD consumes — no independent sim polling). ──

        /// <summary>Push a scoreboard snapshot to the 3D jumbotron. Mirrors the 2D HUD update.</summary>
        public void UpdateScoreboard(ScoreboardUpdate update)
        {
            if (update == null || _world == null || _world.Jumbotron == null) return;
            _world.Jumbotron.SetScore(update.HomeScore, update.AwayScore);
            _world.Jumbotron.SetClock(update.Quarter, update.Clock);
        }

        /// <summary>Push a per-frame clock tick to the jumbotron (score untouched, as with the HUD).</summary>
        public void UpdateClock(float gameClock, float shotClock)
        {
            if (_world == null || _world.Jumbotron == null) return;
            _world.Jumbotron.SetClock(0, gameClock);
        }

        /// <summary>Dim the arena while fast-forwarding so a skipped stretch reads as backgrounded.</summary>
        public void SetFastForward(bool on)
        {
            if (_keyLight != null) _keyLight.intensity = on ? _baseLightIntensity * 0.45f : _baseLightIntensity;
            _director?.SetSkip(on);
        }

        /// <summary>TODO(P5): coach on the 3D sideline. No sideline actors exist yet.</summary>
        public void SetCoachExcited(bool isHome)
        {
        }

        #endregion
    }
}
