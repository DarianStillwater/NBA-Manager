using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Handles ball positioning and arc animations for passes and shots.
    /// Ball attaches to carrier, detaches during flight with parabolic arcs.
    /// </summary>
    public class BallAnimator : MonoBehaviour
    {
        #region Configuration

        [Header("Visual")]
        [SerializeField] private RectTransform _ballTransform;
        [SerializeField] private Image _ballImage;
        [SerializeField] private Image _shadowImage;
        [SerializeField] private float _ballSize = 40f;
        [SerializeField] private Color _ballColor = new Color(0.9f, 0.5f, 0.1f);

        [Header("Shot Scale Effect")]
        [SerializeField] private float _baseScale = 1f;
        [SerializeField] private float _apexScale = 1.8f;  // Ball grows to 1.8x at shot apex

        [Header("Animation Curves")]
        [SerializeField] private AnimationCurve _shotArcCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve _passArcCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Flight Settings")]
        [SerializeField] private float _shotMaxHeight = 80f;    // Peak height for shot arcs
        [SerializeField] private float _passMaxHeight = 30f;    // Peak height for passes
        [SerializeField] private float _shotDuration = 0.8f;    // Base shot flight time
        [SerializeField] private float _passDuration = 0.4f;    // Base pass flight time
        [SerializeField] private float _passSpeedMultiplier = 0.02f;  // Adjust duration by distance

        [Header("Attachment")]
        [SerializeField] private Vector2 _attachOffset = new Vector2(12f, 12f);

        #endregion

        #region State

        private Transform _attachedPlayer;
        private Coroutine _flightCoroutine;
        private bool _isFlying;
        private Vector2 _currentPosition;
        private float _currentHeight;

        #endregion

        #region Properties

        public bool IsFlying => _isFlying;
        public Vector2 Position => _currentPosition;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_ballTransform == null)
                _ballTransform = GetComponent<RectTransform>();

            SetupVisuals();
        }

        private void LateUpdate()
        {
            // Follow attached player if not flying
            if (!_isFlying && _attachedPlayer != null)
            {
                Vector2 attachPos = (Vector2)_attachedPlayer.localPosition + _attachOffset;
                _ballTransform.anchoredPosition = attachPos;
                _currentPosition = attachPos;
            }

            // Update shadow based on height (simulates 3D depth)
            UpdateShadow();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Attach ball to a player (follows them).
        /// </summary>
        public void AttachToPlayer(Transform playerTransform)
        {
            // Stop any active flight
            if (_flightCoroutine != null)
            {
                StopCoroutine(_flightCoroutine);
                _flightCoroutine = null;
            }

            _isFlying = false;
            _attachedPlayer = playerTransform;
            _currentHeight = 0f;

            if (_ballTransform != null)
                _ballTransform.gameObject.SetActive(true);
        }

        /// <summary>
        /// Detach ball from any player.
        /// </summary>
        public void Detach()
        {
            _attachedPlayer = null;
        }

        /// <summary>
        /// Animate a pass between two positions.
        /// </summary>
        public void AnimatePass(Vector2 from, Vector2 to, Action onComplete = null)
        {
            if (_flightCoroutine != null)
                StopCoroutine(_flightCoroutine);

            _flightCoroutine = StartCoroutine(PassFlightCoroutine(from, to, onComplete));
        }

        /// <summary>
        /// Animate a shot to the basket.
        /// </summary>
        public void AnimateShot(Vector2 from, Vector2 basketPosition, bool made, Action onComplete = null)
        {
            if (_flightCoroutine != null)
                StopCoroutine(_flightCoroutine);

            _flightCoroutine = StartCoroutine(ShotFlightCoroutine(from, basketPosition, made, onComplete));
        }

        /// <summary>
        /// Set ball to a loose position (not attached, not flying).
        /// </summary>
        public void SetLoosePosition(Vector2 position)
        {
            _attachedPlayer = null;
            _isFlying = false;
            _currentPosition = position;
            _currentHeight = 0f;

            if (_ballTransform != null)
                _ballTransform.anchoredPosition = position;
        }

        /// <summary>
        /// Hide the ball (e.g., during dead balls).
        /// </summary>
        public void Hide()
        {
            if (_ballTransform != null)
                _ballTransform.gameObject.SetActive(false);
        }

        /// <summary>
        /// Show the ball.
        /// </summary>
        public void Show()
        {
            if (_ballTransform != null)
                _ballTransform.gameObject.SetActive(true);
        }

        #endregion

        #region Private Methods

        private void SetupVisuals()
        {
            if (_ballImage != null)
            {
                // Only apply fallback color if no sprite loaded (sprite needs white to render correctly)
                if (_ballImage.sprite == null)
                    _ballImage.color = _ballColor;
            }

            if (_shadowImage != null)
            {
                _shadowImage.color = new Color(0, 0, 0, 0.3f);
            }

            if (_ballTransform != null)
            {
                _ballTransform.sizeDelta = new Vector2(_ballSize, _ballSize);
            }
        }

        private void UpdateShadow()
        {
            if (_shadowImage == null) return;

            // Shadow moves down and grows as ball height increases
            float shadowOffset = _currentHeight * 0.3f;
            float shadowScale = 1f + (_currentHeight / 100f) * 0.5f;
            float shadowAlpha = 0.3f - (_currentHeight / 100f) * 0.15f;

            _shadowImage.rectTransform.anchoredPosition = new Vector2(shadowOffset * 0.5f, -shadowOffset);
            _shadowImage.rectTransform.localScale = new Vector3(shadowScale, shadowScale * 0.6f, 1f);
            _shadowImage.color = new Color(0, 0, 0, Mathf.Max(0.1f, shadowAlpha));
        }

        #endregion

        #region Flight Coroutines

        private IEnumerator PassFlightCoroutine(Vector2 from, Vector2 to, Action onComplete)
        {
            _isFlying = true;
            _attachedPlayer = null;

            float distance = Vector2.Distance(from, to);
            float duration = _passDuration + (distance * _passSpeedMultiplier);
            float elapsed = 0f;

            // Adjust max height based on distance
            float maxHeight = Mathf.Clamp(_passMaxHeight * (distance / 200f), 10f, _passMaxHeight);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Horizontal position - linear interpolation
                _currentPosition = Vector2.Lerp(from, to, t);

                // Vertical arc - parabola: 4 * t * (1 - t) gives 0 at t=0, 1 at t=0.5, 0 at t=1
                float arcT = _passArcCurve.Evaluate(t);
                float arcValue = 4f * arcT * (1f - arcT);
                _currentHeight = maxHeight * arcValue;

                // Subtle scale pulse for passes (less dramatic than shots)
                float passScale = Mathf.Lerp(_baseScale, _baseScale * 1.2f, arcValue);
                _ballTransform.localScale = new Vector3(passScale, passScale, 1f);

                // Apply position
                _ballTransform.anchoredPosition = _currentPosition;

                yield return null;
            }

            // Ensure final position and reset scale
            _currentPosition = to;
            _currentHeight = 0f;
            _ballTransform.anchoredPosition = to;
            _ballTransform.localScale = new Vector3(_baseScale, _baseScale, 1f);
            _isFlying = false;

            onComplete?.Invoke();
        }

        private IEnumerator ShotFlightCoroutine(Vector2 from, Vector2 basketPosition, bool made, Action onComplete)
        {
            _isFlying = true;
            _attachedPlayer = null;

            float distance = Vector2.Distance(from, basketPosition);
            float duration = _shotDuration + (distance * 0.005f);
            float elapsed = 0f;

            // Higher arc for longer shots
            float maxHeight = _shotMaxHeight * Mathf.Clamp(distance / 150f, 0.8f, 1.5f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Horizontal position
                _currentPosition = Vector2.Lerp(from, basketPosition, t);

                // Shot arc — peaks around 55% through flight for realistic trajectory
                float arcValue = Mathf.Sin(t * Mathf.PI);
                _currentHeight = maxHeight * arcValue;

                // Scale effect — ball grows at apex (simulates rising toward camera)
                float heightNorm = arcValue; // 0→1→0
                float scale = Mathf.Lerp(_baseScale, _apexScale, heightNorm);
                _ballTransform.localScale = new Vector3(scale, scale, 1f);

                // Apply position (offset upward by height for visual arc)
                _ballTransform.anchoredPosition = _currentPosition + new Vector2(0, _currentHeight * 0.3f);

                yield return null;
            }

            // At basket - handle made/miss
            if (made)
            {
                yield return AnimateThroughNet(basketPosition);
            }
            else
            {
                yield return AnimateRimBounce(basketPosition);
            }

            _isFlying = false;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateThroughNet(Vector2 basketPosition)
        {
            // Quick drop through net — ball shrinks as it falls through
            float duration = 0.2f;
            float elapsed = 0f;
            Vector2 startPos = basketPosition;
            Vector2 endPos = basketPosition + new Vector2(0, -25f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                _currentPosition = Vector2.Lerp(startPos, endPos, t * t); // Accelerate downward
                _currentHeight = Mathf.Lerp(10f, 0f, t);
                // Shrink ball as it drops through net
                float scale = Mathf.Lerp(_baseScale * 1.1f, _baseScale * 0.7f, t);
                _ballTransform.localScale = new Vector3(scale, scale, 1f);
                _ballTransform.anchoredPosition = _currentPosition;

                yield return null;
            }

            _currentPosition = endPos;
            _currentHeight = 0f;
            _ballTransform.anchoredPosition = _currentPosition;
            _ballTransform.localScale = new Vector3(_baseScale, _baseScale, 1f);
        }

        private IEnumerator AnimateRimBounce(Vector2 basketPosition)
        {
            // Bounce off rim — ball pops on impact then bounces away
            float duration = 0.35f;
            float elapsed = 0f;

            // Random bounce direction (more outward)
            float bounceAngle = UnityEngine.Random.Range(-70f, 70f) * Mathf.Deg2Rad;
            float bounceDistance = UnityEngine.Random.Range(40f, 80f);
            Vector2 bounceDir = new Vector2(Mathf.Sin(bounceAngle), -Mathf.Cos(bounceAngle));
            Vector2 endPos = basketPosition + bounceDir * bounceDistance;

            float bounceHeight = 30f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                _currentPosition = Vector2.Lerp(basketPosition, endPos, t);

                // Bounce arc
                float arcValue = 4f * t * (1f - t);
                _currentHeight = bounceHeight * arcValue;

                // Scale pop on rim contact then settle
                float scale;
                if (t < 0.15f)
                    scale = Mathf.Lerp(_baseScale, _baseScale * 1.3f, t / 0.15f); // Impact pop
                else
                    scale = Mathf.Lerp(_baseScale * 1.3f, _baseScale, (t - 0.15f) / 0.85f); // Settle
                _ballTransform.localScale = new Vector3(scale, scale, 1f);

                _ballTransform.anchoredPosition = _currentPosition;

                yield return null;
            }

            _currentPosition = endPos;
            _currentHeight = 0f;
            _ballTransform.anchoredPosition = _currentPosition;
            _ballTransform.localScale = new Vector3(_baseScale, _baseScale, 1f);
        }

        #endregion

        #region Factory

        /// <summary>
        /// Creates a simple ball GameObject programmatically.
        /// </summary>
        public static BallAnimator CreateSimple(Transform parent, float ballSize = 20f)
        {
            // Create root object
            var ballGO = new GameObject("Ball");
            ballGO.transform.SetParent(parent, false);

            var rt = ballGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(ballSize, ballSize);

            // Shadow (behind ball)
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(ballGO.transform, false);
            var shadowRT = shadowGO.AddComponent<RectTransform>();
            shadowRT.sizeDelta = new Vector2(ballSize * 0.9f, ballSize * 0.5f);
            shadowRT.anchoredPosition = new Vector2(2, -5);
            var shadowImage = shadowGO.AddComponent<Image>();
            shadowImage.color = new Color(0, 0, 0, 0.3f);
            var circleSprite = NBAHeadCoach.Core.Util.ArtManager.GetCircleDot();
            if (circleSprite != null) shadowImage.sprite = circleSprite;

            // Ball image with basketball sprite
            var ballImage = ballGO.AddComponent<Image>();
            ballImage.color = Color.white; // White so sprite renders at natural colors
            ballImage.preserveAspect = true;
            var basketballSprite = NBAHeadCoach.Core.Util.ArtManager.GetBasketball();
            Debug.Log($"[BallAnimator] Basketball sprite: {(basketballSprite != null ? $"{basketballSprite.texture.width}x{basketballSprite.texture.height}" : "NULL")}");
            if (basketballSprite != null) ballImage.sprite = basketballSprite;
            else ballImage.color = new Color(0.9f, 0.5f, 0.1f); // Fallback orange
            Debug.Log($"[BallAnimator] Ball created: size={ballSize}, color={ballImage.color}, sprite={ballImage.sprite?.name ?? "none"}");

            // Add the BallAnimator component
            var component = ballGO.AddComponent<BallAnimator>();
            component._ballTransform = rt;
            component._ballImage = ballImage;
            component._shadowImage = shadowImage;
            component._ballSize = ballSize;

            return component;
        }

        #endregion
    }
}
