using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Animated 2D court visualization with smooth player/ball movement.
    /// Consumes SpatialState snapshots and interpolates for 60fps display.
    /// </summary>
    public class AnimatedCourtView : MonoBehaviour
    {
        #region Configuration

        [Header("Court")]
        [SerializeField] private RectTransform _courtRect;
        [SerializeField] private Image _courtBackground;
        [SerializeField] private Sprite _halfCourtSprite;

        [Header("Player Dots")]
        [SerializeField] private GameObject _playerDotPrefab;
        [SerializeField] private float _dotSize = 35f;
        [SerializeField] private Color _homeTeamColor = new Color(0.2f, 0.4f, 0.8f);
        [SerializeField] private Color _awayTeamColor = new Color(0.8f, 0.2f, 0.2f);

        [Header("Ball")]
        [SerializeField] private BallAnimator _ballAnimator;
        [SerializeField] private float _ballSize = 20f;

        [Header("Shot Markers")]
        [SerializeField] private GameObject _shotMarkerPrefab;
        [SerializeField] private int _maxShotMarkers = 50;

        [Header("Animation")]
        [SerializeField] private float _interpolationSpeed = 4f;

        [Header("Basket Position")]
        [SerializeField] private Vector2 _basketUIPosition = new Vector2(420, 0);  // Right side of court

        #endregion

        #region State

        // Player tracking
        private Dictionary<string, AnimatedPlayerDot> _playerDots = new Dictionary<string, AnimatedPlayerDot>();
        private List<string> _homeLineup = new List<string>();
        private List<string> _awayLineup = new List<string>();

        // State interpolation
        private Queue<SpatialState> _stateBuffer = new Queue<SpatialState>();
        private SpatialState _currentState;
        private SpatialState _targetState;
        private float _interpolationT;

        // Shot markers
        private List<ShotMarkerUI> _shotMarkers = new List<ShotMarkerUI>();

        // Team info
        private Team _homeTeam;
        private Team _awayTeam;
        private bool _homeOnOffense;

        // Ball state
        private string _currentBallCarrierId;
        private bool _ballInFlight;

        #endregion

        #region Properties

        public bool IsAnimating => _stateBuffer.Count > 0 || _currentState != null;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_courtRect == null)
                _courtRect = GetComponent<RectTransform>();

            if (_courtBackground != null && _halfCourtSprite != null)
                _courtBackground.sprite = _halfCourtSprite;

            // Create ball animator if not assigned
            if (_ballAnimator == null)
            {
                _ballAnimator = BallAnimator.CreateSimple(_courtRect, _ballSize);
            }
        }

        private void Update()
        {
            // Process state buffer and interpolate
            ProcessStateBuffer();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the court view with teams and lineups.
        /// </summary>
        public void Initialize(Team homeTeam, Team awayTeam,
                              List<string> homeLineup, List<string> awayLineup)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _homeLineup = new List<string>(homeLineup);
            _awayLineup = new List<string>(awayLineup);

            // Clear existing
            ClearAll();

            // Create player dots
            CreatePlayerDots(homeLineup, homeTeam, true);
            CreatePlayerDots(awayLineup, awayTeam, false);

            Debug.Log($"[AnimatedCourtView] Initialized with {_playerDots.Count} players");
        }

        /// <summary>
        /// Enqueue a spatial state for playback.
        /// </summary>
        public void EnqueueState(SpatialState state)
        {
            if (state == null) return;

            // Limit buffer size to prevent memory issues
            while (_stateBuffer.Count > 20)
            {
                _stateBuffer.Dequeue();
            }

            _stateBuffer.Enqueue(state);
        }

        /// <summary>
        /// Add a shot marker at the given position.
        /// </summary>
        public void AddShotMarker(ShotMarkerData data)
        {
            // Convert court position to UI position
            Vector2 uiPos = CourtToUIPosition(data.Position.X, data.Position.Y);

            // Create marker
            ShotMarkerUI marker;
            if (_shotMarkerPrefab != null)
            {
                var markerGO = Instantiate(_shotMarkerPrefab, _courtRect);
                marker = markerGO.GetComponent<ShotMarkerUI>();
                if (marker == null)
                    marker = markerGO.AddComponent<ShotMarkerUI>();
                marker.Initialize(data, uiPos);
            }
            else
            {
                marker = ShotMarkerUI.CreateSimple(_courtRect, data.Made, data.Points, uiPos);
            }

            _shotMarkers.Add(marker);

            // Limit marker count
            while (_shotMarkers.Count > _maxShotMarkers)
            {
                var oldest = _shotMarkers[0];
                _shotMarkers.RemoveAt(0);
                if (oldest != null)
                    oldest.Remove();
            }
        }

        /// <summary>
        /// Update which team is on offense (affects court orientation).
        /// </summary>
        public void SetOffense(bool homeOnOffense)
        {
            _homeOnOffense = homeOnOffense;
        }

        /// <summary>
        /// Update lineup after substitution.
        /// </summary>
        public void UpdateLineup(List<string> homeLineup, List<string> awayLineup)
        {
            // Find players who left/entered
            var oldHome = new HashSet<string>(_homeLineup);
            var oldAway = new HashSet<string>(_awayLineup);
            var newHome = new HashSet<string>(homeLineup);
            var newAway = new HashSet<string>(awayLineup);

            // Remove players who left
            foreach (var id in oldHome.Where(id => !newHome.Contains(id)))
            {
                RemovePlayerDot(id);
            }
            foreach (var id in oldAway.Where(id => !newAway.Contains(id)))
            {
                RemovePlayerDot(id);
            }

            // Add players who entered
            foreach (var id in newHome.Where(id => !oldHome.Contains(id)))
            {
                CreateSinglePlayerDot(id, _homeTeam, true);
            }
            foreach (var id in newAway.Where(id => !oldAway.Contains(id)))
            {
                CreateSinglePlayerDot(id, _awayTeam, false);
            }

            _homeLineup = new List<string>(homeLineup);
            _awayLineup = new List<string>(awayLineup);
        }

        /// <summary>
        /// Clear all shot markers.
        /// </summary>
        public void ClearShotMarkers()
        {
            foreach (var marker in _shotMarkers)
            {
                if (marker != null)
                    marker.Remove();
            }
            _shotMarkers.Clear();
        }

        /// <summary>
        /// Clear everything and reset.
        /// </summary>
        public void ClearAll()
        {
            // Clear player dots
            foreach (var kvp in _playerDots)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            _playerDots.Clear();

            // Clear shot markers
            ClearShotMarkers();

            // Clear state buffer
            _stateBuffer.Clear();
            _currentState = null;
            _targetState = null;
        }

        #endregion

        #region State Processing

        private void ProcessStateBuffer()
        {
            // No states to process
            if (_stateBuffer.Count == 0 && _targetState == null) return;

            // Get next target state if needed
            if (_targetState == null && _stateBuffer.Count > 0)
            {
                _currentState = _stateBuffer.Count > 1 ? _stateBuffer.Dequeue() : null;
                _targetState = _stateBuffer.Dequeue();
                _interpolationT = 0f;

                // If no current state, jump to target immediately
                if (_currentState == null)
                {
                    ApplyStateImmediate(_targetState);
                    _targetState = null;
                    return;
                }
            }

            // Interpolate between states
            if (_currentState != null && _targetState != null)
            {
                _interpolationT += Time.deltaTime * _interpolationSpeed;

                if (_interpolationT >= 1f)
                {
                    // Finished interpolating to target
                    ApplyStateImmediate(_targetState);
                    _currentState = _targetState;
                    _targetState = null;
                    _interpolationT = 0f;
                }
                else
                {
                    // Interpolate
                    InterpolateStates(_currentState, _targetState, _interpolationT);
                }
            }
        }

        private void InterpolateStates(SpatialState from, SpatialState to, float t)
        {
            // Interpolate player positions
            for (int i = 0; i < 10; i++)
            {
                if (i >= from.Players.Length || i >= to.Players.Length) continue;

                var fromSnap = from.Players[i];
                var toSnap = to.Players[i];

                if (string.IsNullOrEmpty(fromSnap.PlayerId)) continue;

                if (_playerDots.TryGetValue(fromSnap.PlayerId, out var dot))
                {
                    float x = Mathf.Lerp(fromSnap.X, toSnap.X, t);
                    float y = Mathf.Lerp(fromSnap.Y, toSnap.Y, t);

                    Vector2 uiPos = CourtToUIPosition(x, y);
                    dot.SetTargetPosition(uiPos);
                    dot.SetHasBall(toSnap.HasBall);
                }
            }

            // Handle ball
            UpdateBallFromState(to);
        }

        private void ApplyStateImmediate(SpatialState state)
        {
            if (state == null) return;

            // Apply player positions
            for (int i = 0; i < state.Players.Length; i++)
            {
                var snap = state.Players[i];
                if (string.IsNullOrEmpty(snap.PlayerId)) continue;

                if (_playerDots.TryGetValue(snap.PlayerId, out var dot))
                {
                    Vector2 uiPos = CourtToUIPosition(snap.X, snap.Y);
                    dot.SetPositionImmediate(uiPos);
                    dot.SetHasBall(snap.HasBall);
                }
            }

            // Handle ball
            UpdateBallFromState(state);
        }

        private void UpdateBallFromState(SpatialState state)
        {
            if (_ballAnimator == null || state == null) return;

            var ball = state.Ball;

            // Check for ball flight
            if (ball.Status == BallStatus.InAir_Pass || ball.Status == BallStatus.InAir_Shot)
            {
                if (!_ballInFlight)
                {
                    _ballInFlight = true;

                    // Start flight animation
                    Vector2 fromPos = _ballAnimator.Position;
                    Vector2 toPos = CourtToUIPosition(ball.X, ball.Y);

                    if (ball.Status == BallStatus.InAir_Shot)
                    {
                        // Find shot target (basket)
                        _ballAnimator.AnimateShot(fromPos, _basketUIPosition, true, () =>
                        {
                            _ballInFlight = false;
                        });
                    }
                    else
                    {
                        // Pass to target position
                        _ballAnimator.AnimatePass(fromPos, toPos, () =>
                        {
                            _ballInFlight = false;
                        });
                    }
                }
                // Let flight animation continue
            }
            else if (ball.Status == BallStatus.Held || ball.Status == BallStatus.Dribbled)
            {
                _ballInFlight = false;

                // Attach to carrier
                if (!string.IsNullOrEmpty(ball.HeldByPlayerId))
                {
                    if (_playerDots.TryGetValue(ball.HeldByPlayerId, out var carrierDot))
                    {
                        _ballAnimator.AttachToPlayer(carrierDot.RectTransform);
                        _currentBallCarrierId = ball.HeldByPlayerId;
                    }
                }
            }
            else if (ball.Status == BallStatus.Loose)
            {
                _ballInFlight = false;
                Vector2 ballPos = CourtToUIPosition(ball.X, ball.Y);
                _ballAnimator.SetLoosePosition(ballPos);
                _currentBallCarrierId = null;
            }
        }

        #endregion

        #region Player Dot Management

        private void CreatePlayerDots(List<string> lineup, Team team, bool isHome)
        {
            for (int i = 0; i < lineup.Count && i < 5; i++)
            {
                CreateSinglePlayerDot(lineup[i], team, isHome);
            }
        }

        private void CreateSinglePlayerDot(string playerId, Team team, bool isHome)
        {
            if (string.IsNullOrEmpty(playerId) || _playerDots.ContainsKey(playerId)) return;
            if (_courtRect == null) return;

            // Get player info
            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);
            int jerseyNumber = player?.JerseyNumber ?? 0;
            string playerName = player?.DisplayName ?? playerId;
            Color teamColor = isHome ? _homeTeamColor : _awayTeamColor;

            AnimatedPlayerDot dot;

            if (_playerDotPrefab != null)
            {
                var dotGO = Instantiate(_playerDotPrefab, _courtRect);
                dot = dotGO.GetComponent<AnimatedPlayerDot>();
                if (dot == null)
                    dot = dotGO.AddComponent<AnimatedPlayerDot>();
                dot.Initialize(playerId, jerseyNumber, playerName, teamColor, isHome);
            }
            else
            {
                dot = AnimatedPlayerDot.CreateSimple(_courtRect, _dotSize,
                    playerId, jerseyNumber, playerName, teamColor, isHome);
            }

            // Set initial position (center court)
            dot.SetPositionImmediate(Vector2.zero);

            _playerDots[playerId] = dot;
        }

        private void RemovePlayerDot(string playerId)
        {
            if (_playerDots.TryGetValue(playerId, out var dot))
            {
                if (dot != null)
                    Destroy(dot.gameObject);
                _playerDots.Remove(playerId);
            }
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>
        /// Convert court coordinates to UI position.
        /// Court: X: 0 to 47 (half court), Y: -25 to +25
        /// UI: Centered in _courtRect
        /// </summary>
        private Vector2 CourtToUIPosition(float courtX, float courtY)
        {
            if (_courtRect == null) return Vector2.zero;

            // Normalize to 0-1 range
            // Half court offense attacking right: X: 0 to 47, Y: -25 to +25
            float normalizedX = Mathf.Clamp01(courtX / 47f);
            float normalizedY = (courtY + 25f) / 50f;

            // Convert to UI coordinates (centered)
            float uiX = normalizedX * _courtRect.rect.width - _courtRect.rect.width / 2f;
            float uiY = normalizedY * _courtRect.rect.height - _courtRect.rect.height / 2f;

            return new Vector2(uiX, uiY);
        }

        /// <summary>
        /// Convert UI position back to court coordinates.
        /// </summary>
        private Vector2 UIToCourtPosition(Vector2 uiPos)
        {
            if (_courtRect == null) return Vector2.zero;

            float normalizedX = (uiPos.x + _courtRect.rect.width / 2f) / _courtRect.rect.width;
            float normalizedY = (uiPos.y + _courtRect.rect.height / 2f) / _courtRect.rect.height;

            float courtX = normalizedX * 47f;
            float courtY = normalizedY * 50f - 25f;

            return new Vector2(courtX, courtY);
        }

        #endregion
    }
}
