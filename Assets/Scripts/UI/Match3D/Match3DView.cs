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
        private MatchBroadcastCamera _broadcast;
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

        #region Setup

        public void Initialize(Match3DWorld world, Team homeTeam, Team awayTeam,
            List<string> homeLineup, List<string> awayLineup)
        {
            _world = world;
            _actorParent = world != null && world.Root != null ? world.Root.transform : transform;
            _broadcast = world != null ? world.Camera : null;
            _camera = _broadcast != null ? _broadcast.GetComponent<Camera>() : null;
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
            _timeline = packet?.Timeline;
            _cursor = 0;

            if (_timeline == null || _timeline.Count == 0)
            {
                _relTimes = null;
                return;
            }

            float start = packet.StartGameClock;
            _relTimes = new float[_timeline.Count];
            float prev = 0f;
            for (int i = 0; i < _timeline.Count; i++)
            {
                float rel = start - _timeline[i].GameClock;
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
            for (int i = 0; i < first.Players.Length; i++)
            {
                var ps = first.Players[i];
                if (string.IsNullOrEmpty(ps.PlayerId)) continue;
                if (!_actors.TryGetValue(ps.PlayerId, out var actor) || actor == null) continue;
                var cur = actor.transform.localPosition;
                _bridgeFrom[ps.PlayerId] = new Vector2(cur.x, cur.z);
                _bridgeTo[ps.PlayerId] = new Vector2(ps.X, ps.Y);
            }

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
                _broadcast?.Focus(pos);
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
            string heldBy = b.Ball.HeldByPlayerId;

            for (int i = 0; i < 10 && i < a.Players.Length; i++)
            {
                var pa = a.Players[i];
                if (string.IsNullOrEmpty(pa.PlayerId)) continue;
                if (!_actors.TryGetValue(pa.PlayerId, out var actor) || actor == null) continue;

                var pb = i < b.Players.Length && b.Players[i].PlayerId == pa.PlayerId ? b.Players[i] : pa;
                float x = Mathf.Lerp(pa.X, pb.X, u);
                float y = Mathf.Lerp(pa.Y, pb.Y, u);

                // Physical fields lerp for smoothness; scripted state (action/stance) reads the
                // target frame so an edge-triggered clip (shot/dunk/leap) fires promptly.
                var frame = new ActorFrame
                {
                    SpeedFeetPerSec = Mathf.Lerp(pa.SpeedFeetPerSec, pb.SpeedFeetPerSec, u),
                    VerticalOffset = Mathf.Lerp(pa.VerticalOffset, pb.VerticalOffset, u),
                    Action = pb.CurrentAction,
                    ActionPhase = pb.ActionPhase,
                    DefensiveStance = pb.DefensiveStance,
                    HasBall = pb.HasBall,
                };
                actor.SetFrame(x, y, pb.FacingAngle, in frame);

                // Prefer the explicit HeldByPlayerId; fall back to the HasBall flag.
                if (!handlerFound && (pb.HasBall || (!string.IsNullOrEmpty(heldBy) && pb.PlayerId == heldBy)))
                {
                    handlerFound = true;
                    handlerX = x; handlerY = y; handlerFacing = pb.FacingAngle;
                }
            }

            if (_ball != null)
            {
                var status = b.Ball.Status;
                bool carried = handlerFound &&
                    (status == BallStatus.Held || status == BallStatus.Dribbled || status == BallStatus.DeadBall);

                if (carried)
                {
                    _ball.RenderHeld(handlerX, handlerY, handlerFacing);
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
                _broadcast?.Focus(_lastBall);
            }
        }

        /// <summary>TODO(P3+): 3D shot markers / hoop FX. No-op for the P1 capsule milestone —
        /// the ball's arc through the rim already reads the make/miss.</summary>
        public void ResolveShot(ShotMarkerData data, bool made)
        {
        }

        /// <summary>Dim the arena while fast-forwarding so a skipped stretch reads as backgrounded.</summary>
        public void SetFastForward(bool on)
        {
            if (_keyLight != null) _keyLight.intensity = on ? _baseLightIntensity * 0.45f : _baseLightIntensity;
        }

        /// <summary>TODO(P5): coach on the 3D sideline. No sideline actors exist yet.</summary>
        public void SetCoachExcited(bool isHome)
        {
        }

        #endregion
    }
}
