using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// 2D half-court diagram showing player positions during match simulation.
    /// Players are represented as colored dots with jersey numbers.
    /// </summary>
    public class CourtDiagramView : MonoBehaviour
    {
        #region Configuration

        [Header("Court")]
        [SerializeField] private RectTransform _courtRect;
        [SerializeField] private Image _courtBackground;
        [SerializeField] private Sprite _halfCourtSprite;

        [Header("Player Dots")]
        [SerializeField] private GameObject _playerDotPrefab;
        [SerializeField] private float _dotSize = 30f;
        [SerializeField] private Color _homeTeamColor = new Color(0.2f, 0.4f, 0.8f);
        [SerializeField] private Color _awayTeamColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color _ballCarrierHighlight = new Color(1f, 0.8f, 0.2f);

        [Header("Ball")]
        [SerializeField] private GameObject _ballIndicator;
        [SerializeField] private float _ballSize = 15f;

        [Header("Animation")]
        [SerializeField] private float _positionLerpSpeed = 5f;
        [SerializeField] private bool _animatePositions = true;

        #endregion

        #region State

        private Dictionary<string, PlayerDotUI> _playerDots = new Dictionary<string, PlayerDotUI>();
        private List<string> _homeLineup = new List<string>();
        private List<string> _awayLineup = new List<string>();
        private string _ballCarrierId;
        private bool _homeHasPossession = true;

        // Court positions (normalized 0-1 coordinates on half-court)
        private static readonly Vector2[] OFFENSIVE_POSITIONS = new Vector2[]
        {
            new Vector2(0.5f, 0.85f),   // PG - top of key
            new Vector2(0.15f, 0.7f),   // SG - left wing
            new Vector2(0.85f, 0.7f),   // SF - right wing
            new Vector2(0.3f, 0.4f),    // PF - left block
            new Vector2(0.7f, 0.4f)     // C - right block
        };

        private static readonly Vector2[] DEFENSIVE_POSITIONS = new Vector2[]
        {
            new Vector2(0.5f, 0.7f),    // PG defender
            new Vector2(0.2f, 0.55f),   // SG defender
            new Vector2(0.8f, 0.55f),   // SF defender
            new Vector2(0.35f, 0.3f),   // PF defender
            new Vector2(0.65f, 0.3f)    // C defender
        };

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_courtBackground != null && _halfCourtSprite != null)
            {
                _courtBackground.sprite = _halfCourtSprite;
            }
        }

        private void Update()
        {
            if (_animatePositions)
            {
                UpdateDotPositions();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the court diagram with team lineups
        /// </summary>
        public void Initialize(List<string> homeLineup, List<string> awayLineup, Team homeTeam, Team awayTeam)
        {
            _homeLineup = new List<string>(homeLineup);
            _awayLineup = new List<string>(awayLineup);

            // Clear existing dots
            ClearDots();

            // Create dots for all players
            CreatePlayerDots(homeLineup, homeTeam, true);
            CreatePlayerDots(awayLineup, awayTeam, false);

            // Set initial positions
            UpdateAllPositions();
        }

        /// <summary>
        /// Update which team has possession
        /// </summary>
        public void SetPossession(bool homeHasPossession)
        {
            _homeHasPossession = homeHasPossession;
            UpdateAllPositions();
        }

        /// <summary>
        /// Highlight the ball carrier
        /// </summary>
        public void SetBallCarrier(string playerId)
        {
            // Remove highlight from previous carrier
            if (!string.IsNullOrEmpty(_ballCarrierId) && _playerDots.ContainsKey(_ballCarrierId))
            {
                _playerDots[_ballCarrierId].SetHighlight(false);
            }

            _ballCarrierId = playerId;

            // Add highlight to new carrier
            if (!string.IsNullOrEmpty(playerId) && _playerDots.ContainsKey(playerId))
            {
                _playerDots[playerId].SetHighlight(true);
                UpdateBallPosition();
            }
        }

        /// <summary>
        /// Update lineup after substitution
        /// </summary>
        public void UpdateLineup(List<string> homeLineup, List<string> awayLineup, Team homeTeam, Team awayTeam)
        {
            // Find players who left/entered
            var oldHome = new HashSet<string>(_homeLineup);
            var oldAway = new HashSet<string>(_awayLineup);
            var newHome = new HashSet<string>(homeLineup);
            var newAway = new HashSet<string>(awayLineup);

            // Remove players who left
            foreach (var id in oldHome)
            {
                if (!newHome.Contains(id) && _playerDots.ContainsKey(id))
                {
                    Destroy(_playerDots[id].gameObject);
                    _playerDots.Remove(id);
                }
            }
            foreach (var id in oldAway)
            {
                if (!newAway.Contains(id) && _playerDots.ContainsKey(id))
                {
                    Destroy(_playerDots[id].gameObject);
                    _playerDots.Remove(id);
                }
            }

            // Add players who entered
            foreach (var id in newHome)
            {
                if (!oldHome.Contains(id))
                {
                    CreateSinglePlayerDot(id, homeTeam, true, homeLineup.IndexOf(id));
                }
            }
            foreach (var id in newAway)
            {
                if (!oldAway.Contains(id))
                {
                    CreateSinglePlayerDot(id, awayTeam, false, awayLineup.IndexOf(id));
                }
            }

            _homeLineup = new List<string>(homeLineup);
            _awayLineup = new List<string>(awayLineup);

            UpdateAllPositions();
        }

        /// <summary>
        /// Flash a player dot (for events like made shots, steals)
        /// </summary>
        public void FlashPlayer(string playerId, Color flashColor)
        {
            if (_playerDots.TryGetValue(playerId, out var dot))
            {
                dot.Flash(flashColor);
            }
        }

        #endregion

        #region Internal Methods

        private void CreatePlayerDots(List<string> lineup, Team team, bool isHome)
        {
            for (int i = 0; i < lineup.Count && i < 5; i++)
            {
                CreateSinglePlayerDot(lineup[i], team, isHome, i);
            }
        }

        private void CreateSinglePlayerDot(string playerId, Team team, bool isHome, int positionIndex)
        {
            if (string.IsNullOrEmpty(playerId) || _playerDots.ContainsKey(playerId)) return;
            if (_courtRect == null) return;

            GameObject dotObj;
            if (_playerDotPrefab != null)
            {
                dotObj = Instantiate(_playerDotPrefab, _courtRect);
            }
            else
            {
                // Create simple dot if no prefab
                dotObj = new GameObject($"PlayerDot_{playerId}");
                dotObj.transform.SetParent(_courtRect, false);

                var image = dotObj.AddComponent<Image>();
                image.color = isHome ? _homeTeamColor : _awayTeamColor;

                // Make it circular
                var rt = dotObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(_dotSize, _dotSize);
            }

            var dotUI = dotObj.GetComponent<PlayerDotUI>();
            if (dotUI == null)
            {
                dotUI = dotObj.AddComponent<PlayerDotUI>();
            }

            // Get player info
            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);
            int jerseyNumber = player?.JerseyNumber ?? 0;
            string displayName = player?.LastName ?? playerId;

            dotUI.Initialize(playerId, jerseyNumber, displayName, isHome ? _homeTeamColor : _awayTeamColor);
            dotUI.PositionIndex = positionIndex;
            dotUI.IsHomeTeam = isHome;

            _playerDots[playerId] = dotUI;
        }

        private void UpdateAllPositions()
        {
            foreach (var kvp in _playerDots)
            {
                var dot = kvp.Value;
                Vector2 targetPos = GetTargetPosition(dot.PositionIndex, dot.IsHomeTeam);
                dot.TargetPosition = targetPos;

                if (!_animatePositions)
                {
                    dot.SetPositionImmediate(targetPos);
                }
            }

            UpdateBallPosition();
        }

        private Vector2 GetTargetPosition(int positionIndex, bool isHomeTeam)
        {
            if (positionIndex < 0 || positionIndex >= 5) positionIndex = 0;

            // Determine if team is on offense or defense
            bool isOnOffense = (isHomeTeam && _homeHasPossession) || (!isHomeTeam && !_homeHasPossession);

            Vector2 normalizedPos = isOnOffense
                ? OFFENSIVE_POSITIONS[positionIndex]
                : DEFENSIVE_POSITIONS[positionIndex];

            // Convert to actual position in court rect
            if (_courtRect != null)
            {
                float x = normalizedPos.x * _courtRect.rect.width - _courtRect.rect.width / 2;
                float y = normalizedPos.y * _courtRect.rect.height - _courtRect.rect.height / 2;
                return new Vector2(x, y);
            }

            return Vector2.zero;
        }

        private void UpdateDotPositions()
        {
            foreach (var kvp in _playerDots)
            {
                kvp.Value.UpdatePosition(_positionLerpSpeed * Time.deltaTime);
            }
        }

        private void UpdateBallPosition()
        {
            if (_ballIndicator == null) return;

            if (!string.IsNullOrEmpty(_ballCarrierId) && _playerDots.TryGetValue(_ballCarrierId, out var dot))
            {
                _ballIndicator.SetActive(true);
                _ballIndicator.transform.localPosition = dot.transform.localPosition + new Vector3(_dotSize * 0.3f, _dotSize * 0.3f, 0);
            }
            else
            {
                _ballIndicator.SetActive(false);
            }
        }

        private void ClearDots()
        {
            foreach (var kvp in _playerDots)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _playerDots.Clear();
        }

        #endregion
    }

    /// <summary>
    /// UI component for individual player dot on court diagram
    /// </summary>
    public class PlayerDotUI : MonoBehaviour
    {
        [SerializeField] private Image _dotImage;
        [SerializeField] private Image _highlightRing;
        [SerializeField] private Text _numberText;

        private string _playerId;
        private Color _baseColor;
        private Color _currentColor;
        private float _flashTimer;
        private Color _flashColor;

        public int PositionIndex { get; set; }
        public bool IsHomeTeam { get; set; }
        public Vector2 TargetPosition { get; set; }

        private void Awake()
        {
            if (_dotImage == null)
                _dotImage = GetComponent<Image>();
        }

        private void Update()
        {
            // Handle flash effect
            if (_flashTimer > 0)
            {
                _flashTimer -= Time.deltaTime;
                float t = _flashTimer / 0.5f;
                _dotImage.color = Color.Lerp(_baseColor, _flashColor, t);
            }
        }

        public void Initialize(string playerId, int jerseyNumber, string displayName, Color teamColor)
        {
            _playerId = playerId;
            _baseColor = teamColor;
            _currentColor = teamColor;

            if (_dotImage != null)
                _dotImage.color = teamColor;

            if (_numberText != null)
                _numberText.text = jerseyNumber > 0 ? jerseyNumber.ToString() : "";

            if (_highlightRing != null)
                _highlightRing.gameObject.SetActive(false);

            gameObject.name = $"Dot_{displayName}";
        }

        public void SetHighlight(bool highlighted)
        {
            if (_highlightRing != null)
            {
                _highlightRing.gameObject.SetActive(highlighted);
            }
        }

        public void Flash(Color color)
        {
            _flashColor = color;
            _flashTimer = 0.5f;
        }

        public void UpdatePosition(float lerpFactor)
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, TargetPosition, lerpFactor);
            }
        }

        public void SetPositionImmediate(Vector2 position)
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = position;
            }
        }
    }
}
