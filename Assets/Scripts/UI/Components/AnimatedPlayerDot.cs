using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Individual animated player dot on the court visualization.
    /// Shows team color, jersey number, and tooltip on hover.
    /// </summary>
    public class AnimatedPlayerDot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        #region Configuration

        [Header("Visual Elements")]
        [SerializeField] private Image _dotImage;
        [SerializeField] private Image _highlightRing;
        [SerializeField] private Text _jerseyNumberText;

        [Header("Tooltip")]
        [SerializeField] private CanvasGroup _tooltipGroup;
        [SerializeField] private RectTransform _tooltipRect;
        [SerializeField] private Text _tooltipNameText;
        [SerializeField] private Text _tooltipStatsText;
        [SerializeField] private Image _energyBarFill;

        [Header("Animation")]
        [SerializeField] private float _positionLerpSpeed = 8f;
        [SerializeField] private float _tooltipFadeSpeed = 10f;

        #endregion

        #region State

        private string _playerId;
        private int _jerseyNumber;
        private bool _isHomeTeam;
        private Color _teamColor;
        private RectTransform _rectTransform;

        private Vector2 _currentPosition;
        private Vector2 _targetPosition;
        private bool _hasBall;
        private bool _showingTooltip;
        private float _tooltipAlpha;

        // Cached player data for tooltip
        private string _playerName;
        private int _currentPoints;
        private int _currentRebounds;
        private int _currentAssists;
        private float _energy = 100f;

        #endregion

        #region Properties

        public string PlayerId => _playerId;
        public Vector2 Position => _currentPosition;
        public bool HasBall => _hasBall;
        public bool IsHomeTeam => _isHomeTeam;
        public RectTransform RectTransform => _rectTransform;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
                _rectTransform = gameObject.AddComponent<RectTransform>();

            // Initialize tooltip as hidden
            if (_tooltipGroup != null)
            {
                _tooltipGroup.alpha = 0f;
                _tooltipGroup.blocksRaycasts = false;
            }
        }

        private void Update()
        {
            // Smooth position interpolation
            if (Vector2.Distance(_currentPosition, _targetPosition) > 0.1f)
            {
                _currentPosition = Vector2.Lerp(_currentPosition, _targetPosition, _positionLerpSpeed * Time.deltaTime);
                _rectTransform.anchoredPosition = _currentPosition;
            }

            // Tooltip fade animation
            float targetAlpha = _showingTooltip ? 1f : 0f;
            if (Mathf.Abs(_tooltipAlpha - targetAlpha) > 0.01f)
            {
                _tooltipAlpha = Mathf.Lerp(_tooltipAlpha, targetAlpha, _tooltipFadeSpeed * Time.deltaTime);
                if (_tooltipGroup != null)
                    _tooltipGroup.alpha = _tooltipAlpha;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the player dot with team and player info.
        /// </summary>
        public void Initialize(string playerId, int jerseyNumber, string playerName,
                              Color teamColor, bool isHomeTeam)
        {
            _playerId = playerId;
            _jerseyNumber = jerseyNumber;
            _playerName = playerName;
            _teamColor = teamColor;
            _isHomeTeam = isHomeTeam;

            // Set visual elements
            if (_dotImage != null)
                _dotImage.color = teamColor;

            if (_jerseyNumberText != null)
                _jerseyNumberText.text = jerseyNumber > 0 ? jerseyNumber.ToString() : "";

            if (_highlightRing != null)
                _highlightRing.gameObject.SetActive(false);

            if (_tooltipNameText != null)
                _tooltipNameText.text = playerName;

            gameObject.name = $"PlayerDot_{playerName}_{jerseyNumber}";
        }

        /// <summary>
        /// Set the target position for smooth animation.
        /// </summary>
        public void SetTargetPosition(Vector2 position)
        {
            _targetPosition = position;
        }

        /// <summary>
        /// Immediately set position without animation.
        /// </summary>
        public void SetPositionImmediate(Vector2 position)
        {
            _currentPosition = position;
            _targetPosition = position;
            _rectTransform.anchoredPosition = position;
        }

        /// <summary>
        /// Set whether this player has the ball (shows highlight ring).
        /// </summary>
        public void SetHasBall(bool hasBall)
        {
            _hasBall = hasBall;
            if (_highlightRing != null)
                _highlightRing.gameObject.SetActive(hasBall);
        }

        /// <summary>
        /// Update the tooltip stats display.
        /// </summary>
        public void UpdateStats(int points, int rebounds, int assists, float energy)
        {
            _currentPoints = points;
            _currentRebounds = rebounds;
            _currentAssists = assists;
            _energy = energy;

            RefreshTooltip();
        }

        /// <summary>
        /// Update stats from a Player object.
        /// </summary>
        public void UpdateFromPlayer(Player player, PlayerGameStats gameStats)
        {
            if (player != null)
            {
                _playerName = player.DisplayName;
                _energy = player.Energy;

                if (_tooltipNameText != null)
                    _tooltipNameText.text = _playerName;
            }

            if (gameStats != null)
            {
                _currentPoints = gameStats.Points;
                _currentRebounds = gameStats.Rebounds;
                _currentAssists = gameStats.Assists;
            }

            RefreshTooltip();
        }

        /// <summary>
        /// Flash the dot with a color (for events like made shots).
        /// </summary>
        public void Flash(Color flashColor, float duration = 0.5f)
        {
            StopAllCoroutines();
            StartCoroutine(FlashCoroutine(flashColor, duration));
        }

        #endregion

        #region Tooltip Events

        public void OnPointerEnter(PointerEventData eventData)
        {
            _showingTooltip = true;
            RefreshTooltip();
            PositionTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _showingTooltip = false;
        }

        private void RefreshTooltip()
        {
            if (_tooltipStatsText != null)
            {
                _tooltipStatsText.text = $"PTS: {_currentPoints}  REB: {_currentRebounds}  AST: {_currentAssists}";
            }

            if (_energyBarFill != null)
            {
                _energyBarFill.fillAmount = _energy / 100f;

                // Color based on energy level
                if (_energy > 70f)
                    _energyBarFill.color = new Color(0.2f, 0.8f, 0.2f); // Green
                else if (_energy > 40f)
                    _energyBarFill.color = new Color(0.9f, 0.7f, 0.1f); // Yellow
                else
                    _energyBarFill.color = new Color(0.9f, 0.2f, 0.2f); // Red
            }
        }

        private void PositionTooltip()
        {
            if (_tooltipRect == null) return;

            // Position tooltip above the dot
            _tooltipRect.anchoredPosition = new Vector2(0, 40);

            // TODO: Check screen bounds and flip if needed
        }

        #endregion

        #region Coroutines

        private System.Collections.IEnumerator FlashCoroutine(Color flashColor, float duration)
        {
            Color originalColor = _teamColor;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / duration);

                if (_dotImage != null)
                    _dotImage.color = Color.Lerp(originalColor, flashColor, t);

                yield return null;
            }

            if (_dotImage != null)
                _dotImage.color = originalColor;
        }

        #endregion

        #region Factory

        /// <summary>
        /// Creates a simple player dot GameObject programmatically.
        /// Used when no prefab is assigned.
        /// </summary>
        public static AnimatedPlayerDot CreateSimple(Transform parent, float dotSize,
                                                     string playerId, int jerseyNumber,
                                                     string playerName, Color teamColor, bool isHome)
        {
            // Create root object
            var dotGO = new GameObject($"PlayerDot_{playerName}");
            dotGO.transform.SetParent(parent, false);

            var rt = dotGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(dotSize, dotSize);

            // Add dot image (circle)
            var dotImage = dotGO.AddComponent<Image>();
            dotImage.color = teamColor;
            // Note: You'd assign a circle sprite here in the editor

            // Add highlight ring
            var ringGO = new GameObject("HighlightRing");
            ringGO.transform.SetParent(dotGO.transform, false);
            var ringRT = ringGO.AddComponent<RectTransform>();
            ringRT.sizeDelta = new Vector2(dotSize + 8, dotSize + 8);
            ringRT.anchoredPosition = Vector2.zero;
            var ringImage = ringGO.AddComponent<Image>();
            ringImage.color = new Color(1f, 0.8f, 0.2f, 0.8f); // Gold highlight
            ringGO.SetActive(false);

            // Add jersey number text
            var textGO = new GameObject("JerseyNumber");
            textGO.transform.SetParent(dotGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.sizeDelta = new Vector2(dotSize, dotSize);
            textRT.anchoredPosition = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = jerseyNumber > 0 ? jerseyNumber.ToString() : "";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(dotSize * 0.4f);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            // Add tooltip
            var tooltipGO = new GameObject("Tooltip");
            tooltipGO.transform.SetParent(dotGO.transform, false);
            var tooltipRT = tooltipGO.AddComponent<RectTransform>();
            tooltipRT.sizeDelta = new Vector2(140, 60);
            tooltipRT.anchoredPosition = new Vector2(0, 45);
            var tooltipBG = tooltipGO.AddComponent<Image>();
            tooltipBG.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var tooltipCanvasGroup = tooltipGO.AddComponent<CanvasGroup>();
            tooltipCanvasGroup.alpha = 0f;
            tooltipCanvasGroup.blocksRaycasts = false;

            // Tooltip name text
            var nameTextGO = new GameObject("NameText");
            nameTextGO.transform.SetParent(tooltipGO.transform, false);
            var nameTextRT = nameTextGO.AddComponent<RectTransform>();
            nameTextRT.anchorMin = new Vector2(0, 0.5f);
            nameTextRT.anchorMax = new Vector2(1, 1);
            nameTextRT.offsetMin = new Vector2(5, 0);
            nameTextRT.offsetMax = new Vector2(-5, -3);
            var nameText = nameTextGO.AddComponent<Text>();
            nameText.text = playerName;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 12;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = Color.white;

            // Tooltip stats text
            var statsTextGO = new GameObject("StatsText");
            statsTextGO.transform.SetParent(tooltipGO.transform, false);
            var statsTextRT = statsTextGO.AddComponent<RectTransform>();
            statsTextRT.anchorMin = new Vector2(0, 0);
            statsTextRT.anchorMax = new Vector2(1, 0.5f);
            statsTextRT.offsetMin = new Vector2(5, 3);
            statsTextRT.offsetMax = new Vector2(-5, 0);
            var statsText = statsTextGO.AddComponent<Text>();
            statsText.text = "PTS: 0  REB: 0  AST: 0";
            statsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statsText.fontSize = 10;
            statsText.alignment = TextAnchor.MiddleLeft;
            statsText.color = new Color(0.8f, 0.8f, 0.8f);

            // Energy bar background
            var energyBGGO = new GameObject("EnergyBG");
            energyBGGO.transform.SetParent(tooltipGO.transform, false);
            var energyBGRT = energyBGGO.AddComponent<RectTransform>();
            energyBGRT.anchorMin = new Vector2(0.7f, 0.1f);
            energyBGRT.anchorMax = new Vector2(0.95f, 0.4f);
            energyBGRT.offsetMin = Vector2.zero;
            energyBGRT.offsetMax = Vector2.zero;
            var energyBG = energyBGGO.AddComponent<Image>();
            energyBG.color = new Color(0.2f, 0.2f, 0.2f);

            // Energy bar fill
            var energyFillGO = new GameObject("EnergyFill");
            energyFillGO.transform.SetParent(energyBGGO.transform, false);
            var energyFillRT = energyFillGO.AddComponent<RectTransform>();
            energyFillRT.anchorMin = Vector2.zero;
            energyFillRT.anchorMax = Vector2.one;
            energyFillRT.offsetMin = Vector2.zero;
            energyFillRT.offsetMax = Vector2.zero;
            var energyFill = energyFillGO.AddComponent<Image>();
            energyFill.color = new Color(0.2f, 0.8f, 0.2f);
            energyFill.type = Image.Type.Filled;
            energyFill.fillMethod = Image.FillMethod.Horizontal;
            energyFill.fillAmount = 1f;

            // Add the AnimatedPlayerDot component
            var component = dotGO.AddComponent<AnimatedPlayerDot>();
            component._dotImage = dotImage;
            component._highlightRing = ringImage;
            component._jerseyNumberText = text;
            component._tooltipGroup = tooltipCanvasGroup;
            component._tooltipRect = tooltipRT;
            component._tooltipNameText = nameText;
            component._tooltipStatsText = statsText;
            component._energyBarFill = energyFill;

            // Initialize
            component.Initialize(playerId, jerseyNumber, playerName, teamColor, isHome);

            return component;
        }

        #endregion
    }
}
