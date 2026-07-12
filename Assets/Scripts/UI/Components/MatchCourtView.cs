using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;
using NBAHeadCoach.UI.Match;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// FM-style 2D court renderer with exact timeline playback.
    /// The playback director calls BeginPossession(packet) then RenderAt(t) every frame;
    /// this view binary-advances a cursor over the dense choreographed SpatialStates and
    /// lerps the bracketing pair — no buffering, no rubber-banding, correct at any speed.
    /// </summary>
    public class MatchCourtView : MonoBehaviour, IMatchView
    {
        private RectTransform _courtRect;
        private Image _courtBackground;
        private Color _homeTeamColor = new Color(0.2f, 0.4f, 0.8f);
        private Color _awayTeamColor = new Color(0.8f, 0.2f, 0.2f);
        private float _dotSize = 36f;

        private readonly Dictionary<string, AnimatedPlayerDot> _playerDots = new Dictionary<string, AnimatedPlayerDot>();
        private List<string> _homeLineup = new List<string>();
        private List<string> _awayLineup = new List<string>();
        private Team _homeTeam;
        private Team _awayTeam;

        private MatchBallView _ball;
        private HoopView _leftHoop;
        private HoopView _rightHoop;
        private Image _rimOverlayLeft;    // drawn above the ball while a make drops through
        private Image _rimOverlayRight;
        private readonly List<ShotMarkerUI> _shotMarkers = new List<ShotMarkerUI>();
        private const int MaxShotMarkers = 40;

        private CanvasGroup _dimGroup;
        private Text _ffBadge;

        // Ambient (non-timeline) actors: benches + coaches. Purely decorative — driven by
        // discrete events (subs, timeouts, ejections), never by the possession SpatialStates.
        private readonly Dictionary<string, AnimatedPlayerDot> _benchDots = new Dictionary<string, AnimatedPlayerDot>();
        private readonly List<string> _homeBench = new List<string>();
        private readonly List<string> _awayBench = new List<string>();
        private CoachView _homeCoach;
        private CoachView _awayCoach;
        private const int MaxBench = 10;

        // Active possession timeline
        private List<SpatialState> _timeline;
        private float[] _relTimes;          // seconds from possession start per state
        private int _cursor;

        // Inter-possession bridge: slide dots + ball from the previous possession's final frame
        // into this one's opening frame so motion is continuous rather than teleporting.
        private readonly Dictionary<string, Vector2> _bridgeFrom = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Vector2> _bridgeTo = new Dictionary<string, Vector2>();
        private Vector2 _bridgeBallFrom, _bridgeBallTo;
        private BallState _bridgeBallTarget;
        private bool _bridgeReady;
        private Vector2 _lastBallUi;

        public float PixelsPerFoot => _courtRect != null ? _courtRect.rect.height / 50f : 10f;

        #region Setup

        public void Setup(Image courtBackground, Sprite courtSprite, Color homeColor, Color awayColor)
        {
            _courtRect = GetComponent<RectTransform>();
            _courtBackground = courtBackground;
            _homeTeamColor = EnsureVisible(homeColor, true);
            _awayTeamColor = EnsureVisible(awayColor, false);

            if (_courtBackground != null && courtSprite != null)
                _courtBackground.sprite = courtSprite;

            _dimGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        public void Initialize(Team homeTeam, Team awayTeam, List<string> homeLineup, List<string> awayLineup)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _homeLineup = new List<string>(homeLineup);
            _awayLineup = new List<string>(awayLineup);

            ClearAll();

            // Hoops at both ends, sized to the court rect
            _leftHoop = HoopView.Create(_courtRect, rightEnd: false, CourtToUI, PixelsPerFoot);
            _rightHoop = HoopView.Create(_courtRect, rightEnd: true, CourtToUI, PixelsPerFoot);
            _rimOverlayLeft = HoopView.CreateRimOverlay(_courtRect,
                CourtToUI(-CourtGeometry.RimX, 0f), PixelsPerFoot);
            _rimOverlayRight = HoopView.CreateRimOverlay(_courtRect,
                CourtToUI(CourtGeometry.RimX, 0f), PixelsPerFoot);

            foreach (var id in homeLineup.Take(5)) CreateDot(id, homeTeam, true);
            foreach (var id in awayLineup.Take(5)) CreateDot(id, awayTeam, false);

            _ball = MatchBallView.Create(_courtRect, _dotSize * 0.72f, PixelsPerFoot);

            BuildBenchAndCoach(homeTeam, homeLineup, true);
            BuildBenchAndCoach(awayTeam, awayLineup, false);

            BuildFastForwardBadge();
        }

        #region Bench & coach (ambient actors)

        /// <summary>Bench occupants = the team's roster ids not currently on the floor. Reads the raw
        /// RosterPlayerIds (not Team.Roster) so it never depends on the PlayerDatabase being loaded.</summary>
        private void BuildBenchAndCoach(Team team, List<string> lineup, bool isHome)
        {
            var bench = isHome ? _homeBench : _awayBench;
            bench.Clear();

            var onCourt = new HashSet<string>(lineup);
            var ids = team?.RosterPlayerIds ?? new List<string>();
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || onCourt.Contains(id)) continue;
                bench.Add(id);
                if (bench.Count >= MaxBench) break;
            }

            float benchSize = _dotSize * 0.62f;
            for (int i = 0; i < bench.Count; i++)
                CreateBenchDot(bench[i], team, isHome, BenchSlotPosition(i, bench.Count, isHome, benchSize));

            var coach = CoachView.Create(_courtRect, _dotSize * 0.8f, isHome,
                isHome ? _homeTeamColor : _awayTeamColor, CoachSlotPosition(isHome, benchSize));
            if (isHome) _homeCoach = coach; else _awayCoach = coach;
        }

        /// <summary>Bench dots sit in a row just beyond the sideline (home below, away above the court
        /// rect), spread across the middle of the court width, with the coach at the scorer's-table end.</summary>
        private Vector2 BenchSlotPosition(int index, int count, bool isHome, float benchSize)
        {
            float h = _courtRect != null ? _courtRect.rect.height : 500f;
            float w = _courtRect != null ? _courtRect.rect.width : 940f;
            // Just inside the sideline (home bottom, away top) so the bench stays on-screen and
            // clear of the scoreboard/ticker that sit above and below the court view.
            float y = (isHome ? -1f : 1f) * (h / 2f - benchSize * 0.7f);

            float span = w * 0.62f;
            float startX = -span / 2f + span * 0.14f;   // leave room at the left end for the coach
            float step = count > 1 ? (span * 0.86f) / (count - 1) : 0f;
            float x = startX + step * index;
            return new Vector2(x, y);
        }

        private Vector2 CoachSlotPosition(bool isHome, float benchSize)
        {
            float h = _courtRect != null ? _courtRect.rect.height : 500f;
            float w = _courtRect != null ? _courtRect.rect.width : 940f;
            float y = (isHome ? -1f : 1f) * (h / 2f - benchSize * 0.7f);
            return new Vector2(-w * 0.31f, y);
        }

        private AnimatedPlayerDot CreateBenchDot(string playerId, Team team, bool isHome, Vector2 pos)
        {
            if (string.IsNullOrEmpty(playerId) || _benchDots.ContainsKey(playerId))
                return string.IsNullOrEmpty(playerId) ? null : _benchDots.GetValueOrDefault(playerId);

            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);
            int jersey = int.TryParse(player?.JerseyNumber, out var jn) ? jn : -1;
            string name = player?.DisplayName ?? playerId;
            var color = isHome ? _homeTeamColor : _awayTeamColor;

            float benchSize = _dotSize * 0.62f;
            var dot = AnimatedPlayerDot.CreateSimple(_courtRect, benchSize, playerId, jersey, name, color, isHome);
            dot.SetPositionImmediate(pos);

            // Seated on the bench: dimmed so it reads as off-court.
            var cg = dot.gameObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = dot.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0.7f;

            var outline = dot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);

            _benchDots[playerId] = dot;
            return dot;
        }

        /// <summary>Animate a substitution as a swap of seats: the incoming reserve walks from his
        /// bench spot onto the floor (taking the outgoing player's position), while the outgoing
        /// player walks off to the seat the reserve just vacated. Bench layout stays stable because
        /// the two simply trade places. Falls back to an instant swap if either dot is missing.</summary>
        public void AnimateSubstitution(string outId, string inId, bool isHome)
        {
            if (string.IsNullOrEmpty(outId) || string.IsNullOrEmpty(inId)) return;

            var bench = isHome ? _homeBench : _awayBench;
            int benchIdx = bench.IndexOf(inId);

            // Both dots must be where we expect (incoming on bench, outgoing on court).
            if (benchIdx < 0 || !_benchDots.TryGetValue(inId, out var inBenchDot) || inBenchDot == null ||
                !_playerDots.TryGetValue(outId, out var outCourtDot) || outCourtDot == null)
            {
                // Structures out of sync (e.g. missing DB entry) — hard swap to stay consistent.
                var team = isHome ? _homeTeam : _awayTeam;
                var newHome = new List<string>(_homeLineup);
                var newAway = new List<string>(_awayLineup);
                var target = isHome ? newHome : newAway;
                int li = target.IndexOf(outId);
                if (li >= 0) target[li] = inId;
                UpdateLineup(newHome, newAway);
                return;
            }

            Vector2 benchSeat = inBenchDot.RectTransform.anchoredPosition;   // the vacated seat
            Vector2 courtSpot = outCourtDot.RectTransform.anchoredPosition;  // the spot being taken
            var team2 = isHome ? _homeTeam : _awayTeam;

            // Incoming: destroy the small bench dot, spawn a full-size on-court dot at the bench,
            // then walk it to the court spot (AnimatedPlayerDot lerps toward its target on its own).
            RemoveBenchDot(inId);
            bench[benchIdx] = outId;
            var inCourtDot = CreateDot(inId, team2, isHome);
            if (inCourtDot != null)
            {
                inCourtDot.SetPositionImmediate(benchSeat);
                inCourtDot.SetTargetPosition(courtSpot);
            }
            _homeLineup = isHome ? ReplaceId(_homeLineup, outId, inId) : _homeLineup;
            _awayLineup = !isHome ? ReplaceId(_awayLineup, outId, inId) : _awayLineup;

            // Outgoing: destroy the on-court dot, spawn a small bench dot at the court spot,
            // then walk it to the vacated seat.
            RemoveDot(outId);
            var outBenchDot = CreateBenchDot(outId, team2, isHome, courtSpot);
            _benchDots[outId] = outBenchDot;
            if (outBenchDot != null) outBenchDot.SetTargetPosition(benchSeat);
        }

        private static List<string> ReplaceId(List<string> src, string oldId, string newId)
        {
            var copy = new List<string>(src);
            int i = copy.IndexOf(oldId);
            if (i >= 0) copy[i] = newId;
            return copy;
        }

        private void RemoveBenchDot(string playerId)
        {
            if (_benchDots.TryGetValue(playerId, out var dot))
            {
                if (dot != null) Destroy(dot.gameObject);
                _benchDots.Remove(playerId);
            }
        }

        /// <summary>Snap all in-flight substitution walks to their targets. Called at the start of a
        /// possession so a walk never fights the timeline's SetPositionImmediate.</summary>
        public void FinalizeSubstitutionWalks()
        {
            foreach (var kvp in _playerDots)
                if (kvp.Value != null) kvp.Value.SnapToTarget();
            foreach (var kvp in _benchDots)
                if (kvp.Value != null) kvp.Value.SnapToTarget();
        }

        // ── Coach behaviour relays (driven by the playback director / scene events) ──

        /// <summary>Make a coach jump on an exciting play (fast break, dunk).</summary>
        public void SetCoachExcited(bool isHome)
        {
            var coach = isHome ? _homeCoach : _awayCoach;
            coach?.SetState(CoachView.CoachState.Excited);
        }

        /// <summary>Move a coach into (or out of) the timeout huddle at bench center.</summary>
        public void SetCoachHuddle(bool isHome, bool huddling)
        {
            var coach = isHome ? _homeCoach : _awayCoach;
            coach?.SetState(huddling ? CoachView.CoachState.Huddle : CoachView.CoachState.Idle);
        }

        /// <summary>Eject a coach: he fades out and removes himself. The reference is dropped
        /// immediately so he can never return this game.</summary>
        public void EjectCoach(bool isHome)
        {
            var coach = isHome ? _homeCoach : _awayCoach;
            coach?.Eject();
            if (isHome) _homeCoach = null; else _awayCoach = null;
        }

        // Test/inspection accessors
        public int BenchCount(bool isHome) => (isHome ? _homeBench : _awayBench).Count;
        public CoachView.CoachState? GetCoachState(bool isHome)
        {
            var coach = isHome ? _homeCoach : _awayCoach;
            return coach != null ? coach.State : (CoachView.CoachState?)null;
        }
        public bool IsOnBench(string playerId) => _benchDots.ContainsKey(playerId);
        public bool IsOnCourt(string playerId) => _playerDots.ContainsKey(playerId);
        public bool HasCoach(bool isHome) => (isHome ? _homeCoach : _awayCoach) != null;

        public bool TryGetActorPosition(string playerId, out Vector2 pos)
        {
            if (_playerDots.TryGetValue(playerId, out var d) && d != null)
            { pos = d.RectTransform.anchoredPosition; return true; }
            if (_benchDots.TryGetValue(playerId, out var b) && b != null)
            { pos = b.RectTransform.anchoredPosition; return true; }
            pos = Vector2.zero; return false;
        }

        #endregion

        public void UpdateLineup(List<string> homeLineup, List<string> awayLineup)
        {
            var oldHome = new HashSet<string>(_homeLineup);
            var oldAway = new HashSet<string>(_awayLineup);

            foreach (var id in _homeLineup.Where(id => !homeLineup.Contains(id)).ToList()) RemoveDot(id);
            foreach (var id in _awayLineup.Where(id => !awayLineup.Contains(id)).ToList()) RemoveDot(id);
            foreach (var id in homeLineup.Where(id => !oldHome.Contains(id))) CreateDot(id, _homeTeam, true);
            foreach (var id in awayLineup.Where(id => !oldAway.Contains(id))) CreateDot(id, _awayTeam, false);

            _homeLineup = new List<string>(homeLineup);
            _awayLineup = new List<string>(awayLineup);
        }

        #endregion

        #region Timeline playback

        /// <summary>Cache a possession's timeline and reset the playback cursor.</summary>
        public void BeginPossession(PossessionPlaybackPacket packet)
        {
            // A live possession is about to drive positions via SetPositionImmediate — make sure any
            // in-flight substitution walk has resolved so the two don't fight.
            FinalizeSubstitutionWalks();

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
                // keep strictly non-decreasing even at clamped clock boundaries
                if (rel < prev) rel = prev;
                _relTimes[i] = rel;
                prev = rel;
            }

            CaptureBridge();
        }

        /// <summary>Snapshot where the dots + ball are now vs. where this possession opens, so the
        /// director can slide them across the gap instead of snapping.</summary>
        private void CaptureBridge()
        {
            _bridgeFrom.Clear();
            _bridgeTo.Clear();
            _bridgeReady = false;
            if (_timeline == null || _timeline.Count == 0) return;

            var first = _timeline[0];
            float maxDispUi = 0f;
            for (int i = 0; i < first.Players.Length; i++)
            {
                var ps = first.Players[i];
                if (string.IsNullOrEmpty(ps.PlayerId)) continue;
                if (!_playerDots.TryGetValue(ps.PlayerId, out var dot) || dot == null) continue;
                var from = dot.RectTransform.anchoredPosition;
                var to = CourtToUI(ps.X, ps.Y);
                _bridgeFrom[ps.PlayerId] = from;
                _bridgeTo[ps.PlayerId] = to;
                maxDispUi = Mathf.Max(maxDispUi, Vector2.Distance(from, to));
            }

            // Twin of the 3D threshold: continuous-flow timelines start where the last ended, so the
            // slide is imperceptible — skip it. Convert the UI displacement back to feet to compare.
            float maxDispFeet = PixelsPerFoot > 0.01f ? maxDispUi / PixelsPerFoot : maxDispUi;
            if (!NBAHeadCoach.UI.Match3D.ActorMotion.ShouldBridge(maxDispFeet)) return;

            _bridgeBallTarget = first.Ball;
            _bridgeBallTo = CourtToUI(first.Ball.X, first.Ball.Y);
            _bridgeBallFrom = _lastBallUi == Vector2.zero ? _bridgeBallTo : _lastBallUi;
            _bridgeReady = true;
        }

        /// <summary>Render the transition frame at fraction u (0 = previous end, 1 = this start).</summary>
        public void BridgeRender(float u)
        {
            if (!_bridgeReady) return;
            u = Mathf.Clamp01(u);

            foreach (var kvp in _bridgeTo)
            {
                if (!_playerDots.TryGetValue(kvp.Key, out var dot) || dot == null) continue;
                var from = _bridgeFrom.TryGetValue(kvp.Key, out var f) ? f : kvp.Value;
                dot.SetPositionImmediate(Vector2.Lerp(from, kvp.Value, u));
            }

            if (_ball != null)
            {
                var pos = Vector2.Lerp(_bridgeBallFrom, _bridgeBallTo, u);
                _ball.Render(pos, _bridgeBallTarget.Height, BallStatus.Held, _bridgeBallTarget.ShotStyle, false);
                _lastBallUi = pos;
            }
        }

        /// <summary>Render the court at playback time t (seconds from possession start).</summary>
        public void RenderAt(float t)
        {
            if (_timeline == null || _timeline.Count == 0) return;

            // Advance cursor (t is monotonic within a possession)
            while (_cursor < _timeline.Count - 2 && _relTimes[_cursor + 1] <= t)
                _cursor++;

            var a = _timeline[_cursor];
            var b = _cursor + 1 < _timeline.Count ? _timeline[_cursor + 1] : a;
            float span = _relTimes[Mathf.Min(_cursor + 1, _timeline.Count - 1)] - _relTimes[_cursor];
            float u = span <= 0.0001f ? 1f : Mathf.Clamp01((t - _relTimes[_cursor]) / span);

            // Players
            for (int i = 0; i < 10 && i < a.Players.Length; i++)
            {
                var pa = a.Players[i];
                if (string.IsNullOrEmpty(pa.PlayerId)) continue;
                if (!_playerDots.TryGetValue(pa.PlayerId, out var dot) || dot == null) continue;

                var pb = i < b.Players.Length && b.Players[i].PlayerId == pa.PlayerId ? b.Players[i] : pa;
                float x = Mathf.Lerp(pa.X, pb.X, u);
                float y = Mathf.Lerp(pa.Y, pb.Y, u);
                dot.SetPositionImmediate(CourtToUI(x, y));
                dot.SetHasBall(pb.HasBall);
                dot.SetFacing(pb.FacingAngle);
                dot.SetAction(pb.CurrentAction);
            }

            // Ball
            if (_ball != null)
            {
                float bx = Mathf.Lerp(a.Ball.X, b.Ball.X, u);
                float by = Mathf.Lerp(a.Ball.Y, b.Ball.Y, u);
                float bh = Mathf.Lerp(a.Ball.Height, b.Ball.Height, u);

                // A shot descending into the cylinder renders UNDER the rim ring and shrinks,
                // so a make visibly drops INSIDE the hoop instead of landing beside it.
                // Miss caroms are Loose and kick upward first, so they never trigger.
                bool throughRim = b.Ball.Status == BallStatus.InAir_Shot &&
                                  bh < CourtGeometry.RimHeight + 0.3f &&
                                  b.Ball.Height <= a.Ball.Height + 0.01f &&
                                  Mathf.Abs(Mathf.Abs(bx) - CourtGeometry.RimX) < 1.2f &&
                                  Mathf.Abs(by) < 1.2f;

                var ballUi = CourtToUI(bx, by);
                _ball.Render(ballUi, bh, b.Ball.Status, b.Ball.ShotStyle, throughRim);
                _lastBallUi = ballUi;

                var overlay = bx >= 0f ? _rimOverlayRight : _rimOverlayLeft;
                var other = bx >= 0f ? _rimOverlayLeft : _rimOverlayRight;
                if (other != null && other.enabled) other.enabled = false;
                if (overlay != null)
                {
                    if (overlay.enabled != throughRim) overlay.enabled = throughRim;
                    // Above the ball, which just re-sorted itself to last
                    if (throughRim) overlay.transform.SetAsLastSibling();
                }
            }
        }

        /// <summary>Drop a shot marker and play hoop FX for a resolved shot.</summary>
        public void ResolveShot(ShotMarkerData data, bool made)
        {
            var marker = ShotMarkerUI.CreateSimple(_courtRect, made, data.Points, CourtToUI(data.Position.X, data.Position.Y));
            // markers sit under dots/ball
            marker.transform.SetSiblingIndex(1);
            _shotMarkers.Add(marker);
            while (_shotMarkers.Count > MaxShotMarkers)
            {
                var oldest = _shotMarkers[0];
                _shotMarkers.RemoveAt(0);
                if (oldest != null) oldest.Remove();
            }

            var hoop = data.Position.X >= 0f ? _rightHoop : _leftHoop;
            if (hoop != null)
            {
                if (made && data.ShotType == NBAHeadCoach.Core.Data.ShotType.Dunk)
                {
                    hoop.PlayDunk();                    // violent slam
                    _ball?.Punch(0.6f);
                }
                else if (made && data.ShotType == NBAHeadCoach.Core.Data.ShotType.ThreePointer)
                {
                    hoop.PlaySwish(1.7f);               // big splash from deep
                }
                else if (made) hoop.PlaySwish();
                else hoop.PlayRimOut();
            }
        }

        /// <summary>Push live box-score stats into every dot's hover tooltip.</summary>
        public void RefreshPlayerStats(BoxScore live)
        {
            if (live == null) return;
            var db = GameManager.Instance?.PlayerDatabase;
            foreach (var kvp in _playerDots)
            {
                if (kvp.Value == null) continue;
                kvp.Value.UpdateFromPlayer(db?.GetPlayer(kvp.Key), live.GetPlayerStats(kvp.Key));
            }
        }

        /// <summary>Dim the court and show the fast-forward badge during skips.</summary>
        public void SetFastForward(bool on)
        {
            if (_dimGroup != null) _dimGroup.alpha = on ? 0.65f : 1f;
            if (_ffBadge != null) _ffBadge.enabled = on;
        }

        /// <summary>Show/hide the 2D court's own visuals (wood, dots, ball, hoops, markers, coaches)
        /// without touching externally-parented siblings like the narration bar. Used by the 3D
        /// view toggle: when 3D is live the 2D court hides so the 3D camera shows through the
        /// transparent overlay. Toggles the objects this view created — nothing else.</summary>
        public void SetCourtVisible(bool visible)
        {
            if (_courtBackground != null) _courtBackground.enabled = visible;

            foreach (var kvp in _playerDots)
                if (kvp.Value != null) kvp.Value.gameObject.SetActive(visible);
            foreach (var kvp in _benchDots)
                if (kvp.Value != null) kvp.Value.gameObject.SetActive(visible);
            foreach (var marker in _shotMarkers)
                if (marker != null) marker.gameObject.SetActive(visible);

            if (_ball != null) _ball.gameObject.SetActive(visible);
            if (_leftHoop != null) _leftHoop.gameObject.SetActive(visible);
            if (_rightHoop != null) _rightHoop.gameObject.SetActive(visible);
            if (_homeCoach != null) _homeCoach.gameObject.SetActive(visible);
            if (_awayCoach != null) _awayCoach.gameObject.SetActive(visible);

            // Rim overlays manage their own enabled flag inside RenderAt; force them off when hiding.
            if (!visible)
            {
                if (_rimOverlayLeft != null) _rimOverlayLeft.enabled = false;
                if (_rimOverlayRight != null) _rimOverlayRight.enabled = false;
            }
        }

        #endregion

        #region Internals

        private AnimatedPlayerDot CreateDot(string playerId, Team team, bool isHome)
        {
            if (string.IsNullOrEmpty(playerId) || _playerDots.ContainsKey(playerId))
                return string.IsNullOrEmpty(playerId) ? null : _playerDots.GetValueOrDefault(playerId);

            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);
            int jersey = int.TryParse(player?.JerseyNumber, out var jn) ? jn : -1;
            string name = player?.DisplayName ?? playerId;
            var color = isHome ? _homeTeamColor : _awayTeamColor;

            var dot = AnimatedPlayerDot.CreateSimple(_courtRect, _dotSize, playerId, jersey, name, color, isHome);
            dot.SetPositionImmediate(Vector2.zero);

            // Dark outline so dots read clearly on the light court wood
            var outline = dot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            _playerDots[playerId] = dot;
            return dot;
        }

        private void RemoveDot(string playerId)
        {
            if (_playerDots.TryGetValue(playerId, out var dot))
            {
                if (dot != null) Destroy(dot.gameObject);
                _playerDots.Remove(playerId);
            }
        }

        private void ClearAll()
        {
            foreach (var kvp in _playerDots)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            _playerDots.Clear();

            foreach (var kvp in _benchDots)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            _benchDots.Clear();
            _homeBench.Clear();
            _awayBench.Clear();

            if (_homeCoach != null) Destroy(_homeCoach.gameObject);
            if (_awayCoach != null) Destroy(_awayCoach.gameObject);
            _homeCoach = null; _awayCoach = null;

            foreach (var marker in _shotMarkers)
                if (marker != null) marker.Remove();
            _shotMarkers.Clear();

            if (_ball != null) Destroy(_ball.gameObject);
            if (_leftHoop != null) Destroy(_leftHoop.gameObject);
            if (_rightHoop != null) Destroy(_rightHoop.gameObject);
            if (_rimOverlayLeft != null) Destroy(_rimOverlayLeft.gameObject);
            if (_rimOverlayRight != null) Destroy(_rimOverlayRight.gameObject);
            _ball = null; _leftHoop = null; _rightHoop = null;
            _rimOverlayLeft = null; _rimOverlayRight = null;
            _timeline = null;
        }

        private void BuildFastForwardBadge()
        {
            var go = new GameObject("FFBadge", typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_courtRect, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.92f);
            rect.sizeDelta = new Vector2(280f, 30f);

            _ffBadge = go.GetComponent<Text>();
            _ffBadge.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _ffBadge.fontSize = 20;
            _ffBadge.fontStyle = FontStyle.Bold;
            _ffBadge.alignment = TextAnchor.MiddleCenter;
            _ffBadge.color = new Color(1f, 0.84f, 0f, 0.95f);
            _ffBadge.text = ">> FAST FORWARD";
            _ffBadge.raycastTarget = false;
            _ffBadge.enabled = false;
        }

        private Color EnsureVisible(Color c, bool isHome)
        {
            // Final safety: a near-white color washes out on the wood. Darken while keeping
            // hue rather than swapping to a generic color (the team color is already resolved
            // upstream; this only catches pale residue). Dark colors are left untouched.
            if (c.r > 0.8f && c.g > 0.8f && c.b > 0.8f)
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                return Color.HSVToRGB(h, Mathf.Max(s, 0.25f), 0.7f);
            }
            return c;
        }

        /// <summary>Court feet (X[-47,47], Y[-25,25]) → local UI position centered in the rect.</summary>
        private Vector2 CourtToUI(float courtX, float courtY)
        {
            if (_courtRect == null) return Vector2.zero;
            float nx = (courtX + CourtGeometry.HalfLength) / (CourtGeometry.HalfLength * 2f);
            float ny = (courtY + CourtGeometry.HalfWidth) / (CourtGeometry.HalfWidth * 2f);
            return new Vector2(
                nx * _courtRect.rect.width - _courtRect.rect.width / 2f,
                ny * _courtRect.rect.height - _courtRect.rect.height / 2f);
        }

        #endregion
    }
}
