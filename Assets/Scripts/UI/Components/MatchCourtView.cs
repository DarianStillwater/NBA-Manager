using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// FM-style 2D court renderer with exact timeline playback.
    /// The playback director calls BeginPossession(packet) then RenderAt(t) every frame;
    /// this view binary-advances a cursor over the dense choreographed SpatialStates and
    /// lerps the bracketing pair — no buffering, no rubber-banding, correct at any speed.
    /// Supersedes AnimatedCourtView (left in the project, no longer attached).
    /// </summary>
    public class MatchCourtView : MonoBehaviour
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
        private readonly List<ShotMarkerUI> _shotMarkers = new List<ShotMarkerUI>();
        private const int MaxShotMarkers = 40;

        private CanvasGroup _dimGroup;
        private Text _ffBadge;

        // Active possession timeline
        private List<SpatialState> _timeline;
        private float[] _relTimes;          // seconds from possession start per state
        private int _cursor;

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

            foreach (var id in homeLineup.Take(5)) CreateDot(id, homeTeam, true);
            foreach (var id in awayLineup.Take(5)) CreateDot(id, awayTeam, false);

            _ball = MatchBallView.Create(_courtRect, _dotSize * 0.6f, PixelsPerFoot);

            BuildFastForwardBadge();
        }

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
                // keep strictly non-decreasing even at clamped clock boundaries
                if (rel < prev) rel = prev;
                _relTimes[i] = rel;
                prev = rel;
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
            }

            // Ball
            if (_ball != null)
            {
                float bx = Mathf.Lerp(a.Ball.X, b.Ball.X, u);
                float by = Mathf.Lerp(a.Ball.Y, b.Ball.Y, u);
                float bh = Mathf.Lerp(a.Ball.Height, b.Ball.Height, u);
                _ball.Render(CourtToUI(bx, by), bh, b.Ball.Status);
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
                if (made) hoop.PlaySwish();
                else hoop.PlayRimOut();
            }
        }

        /// <summary>Dim the court and show the fast-forward badge during skips.</summary>
        public void SetFastForward(bool on)
        {
            if (_dimGroup != null) _dimGroup.alpha = on ? 0.65f : 1f;
            if (_ffBadge != null) _ffBadge.enabled = on;
        }

        #endregion

        #region Internals

        private void CreateDot(string playerId, Team team, bool isHome)
        {
            if (string.IsNullOrEmpty(playerId) || _playerDots.ContainsKey(playerId)) return;

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

            foreach (var marker in _shotMarkers)
                if (marker != null) marker.Remove();
            _shotMarkers.Clear();

            if (_ball != null) Destroy(_ball.gameObject);
            if (_leftHoop != null) Destroy(_leftHoop.gameObject);
            if (_rightHoop != null) Destroy(_rightHoop.gameObject);
            _ball = null; _leftHoop = null; _rightHoop = null;
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
            float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            if (lum > 0.85f)
                return isHome ? new Color(0.2f, 0.4f, 0.9f) : new Color(0.9f, 0.2f, 0.2f);
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
