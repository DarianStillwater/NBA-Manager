using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Persistent shot location marker on the court visualization.
    /// Green for makes, red for misses. Can fade over time.
    /// </summary>
    public class ShotMarkerUI : MonoBehaviour
    {
        #region Configuration

        [Header("Visual")]
        [SerializeField] private Image _markerImage;
        [SerializeField] private Text _pointsText;
        [SerializeField] private float _markerSize = 16f;

        [Header("Colors")]
        [SerializeField] private Color _madeColor = new Color(0.2f, 0.8f, 0.2f, 0.85f);
        [SerializeField] private Color _missColor = new Color(0.9f, 0.2f, 0.2f, 0.7f);
        [SerializeField] private Color _threePointColor = new Color(0.3f, 0.9f, 0.3f, 0.9f);

        [Header("Animation")]
        [SerializeField] private float _appearDuration = 0.15f;
        [SerializeField] private float _fadeStartTime = 30f;   // Start fading after 30 seconds
        [SerializeField] private float _fadeDuration = 5f;     // Fade out over 5 seconds

        #endregion

        #region State

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private bool _made;
        private int _points;
        private float _createdTime;
        private bool _shouldFade;
        private Coroutine _fadeCoroutine;

        #endregion

        #region Properties

        public bool Made => _made;
        public int Points => _points;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
                _rectTransform = gameObject.AddComponent<RectTransform>();

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void Update()
        {
            // Check if should start fading
            if (_shouldFade && _fadeCoroutine == null)
            {
                float elapsed = Time.time - _createdTime;
                if (elapsed >= _fadeStartTime)
                {
                    _fadeCoroutine = StartCoroutine(FadeAndDestroy());
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the shot marker.
        /// </summary>
        public void Initialize(bool made, int points, Vector2 position)
        {
            _made = made;
            _points = points;
            _createdTime = Time.time;
            _shouldFade = true;

            // Set position
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = position;
                _rectTransform.sizeDelta = new Vector2(_markerSize, _markerSize);
            }

            // Set color based on made/miss and points
            if (_markerImage != null)
            {
                if (made)
                {
                    _markerImage.color = points >= 3 ? _threePointColor : _madeColor;
                }
                else
                {
                    _markerImage.color = _missColor;
                }
            }

            // Show points text for makes
            if (_pointsText != null)
            {
                if (made && points > 0)
                {
                    _pointsText.gameObject.SetActive(true);
                    _pointsText.text = $"+{points}";
                    _pointsText.color = points >= 3 ? _threePointColor : _madeColor;
                }
                else
                {
                    _pointsText.gameObject.SetActive(false);
                }
            }

            // Play appear animation
            StartCoroutine(AppearAnimation());
        }

        /// <summary>
        /// Initialize from shot marker data.
        /// </summary>
        public void Initialize(ShotMarkerData data, Vector2 uiPosition)
        {
            Initialize(data.Made, data.Points, uiPosition);
        }

        /// <summary>
        /// Disable automatic fading.
        /// </summary>
        public void DisableFade()
        {
            _shouldFade = false;
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }

        /// <summary>
        /// Manually trigger fade out and destroy.
        /// </summary>
        public void FadeOut()
        {
            if (_fadeCoroutine == null)
                _fadeCoroutine = StartCoroutine(FadeAndDestroy());
        }

        /// <summary>
        /// Immediately destroy the marker.
        /// </summary>
        public void Remove()
        {
            Destroy(gameObject);
        }

        #endregion

        #region Coroutines

        private IEnumerator AppearAnimation()
        {
            // Start small and transparent
            _rectTransform.localScale = Vector3.zero;
            _canvasGroup.alpha = 0f;

            float elapsed = 0f;

            while (elapsed < _appearDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _appearDuration;

                // Ease out elastic effect
                float scale = EaseOutBack(t);
                _rectTransform.localScale = Vector3.one * scale;
                _canvasGroup.alpha = t;

                yield return null;
            }

            _rectTransform.localScale = Vector3.one;
            _canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeAndDestroy()
        {
            float elapsed = 0f;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _fadeDuration;

                _canvasGroup.alpha = 1f - t;

                yield return null;
            }

            Destroy(gameObject);
        }

        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }

        #endregion

        #region Factory

        /// <summary>
        /// Creates a simple shot marker GameObject programmatically.
        /// </summary>
        public static ShotMarkerUI CreateSimple(Transform parent, bool made, int points, Vector2 position)
        {
            var markerGO = new GameObject(made ? "ShotMade" : "ShotMiss");
            markerGO.transform.SetParent(parent, false);

            var rt = markerGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(16, 16);
            rt.anchoredPosition = position;

            var canvasGroup = markerGO.AddComponent<CanvasGroup>();

            // Marker image
            var image = markerGO.AddComponent<Image>();
            // Default circle - would use sprites in real implementation

            // Points text (for makes)
            var textGO = new GameObject("PointsText");
            textGO.transform.SetParent(markerGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchoredPosition = new Vector2(0, 15);
            textRT.sizeDelta = new Vector2(30, 20);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 10;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            textGO.SetActive(false);

            // Add component and initialize
            var component = markerGO.AddComponent<ShotMarkerUI>();
            component._markerImage = image;
            component._pointsText = text;
            component._rectTransform = rt;
            component._canvasGroup = canvasGroup;

            component.Initialize(made, points, position);

            return component;
        }

        #endregion
    }
}
