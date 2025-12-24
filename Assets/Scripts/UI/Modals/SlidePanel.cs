using System;
using System.Collections;
using UnityEngine;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// Base class for slide-in modal panels.
    /// Extends BasePanel with animated slide transitions.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SlidePanel : BasePanel
    {
        public enum SlideDirection
        {
            Right,
            Left,
            Top,
            Bottom
        }

        [Header("Slide Configuration")]
        [SerializeField] private SlideDirection _slideFrom = SlideDirection.Right;
        [SerializeField] private float _slideDuration = 0.25f;
        [SerializeField] private AnimationCurve _slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float _slideOffset = 400f;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Vector2 _shownPosition;
        private Vector2 _hiddenPosition;
        private Coroutine _currentAnimation;
        private bool _isAnimating;

        public bool IsAnimating => _isAnimating;
        public bool IsVisible { get; private set; }

        // Events
        public event Action OnSlideInComplete;
        public event Action OnSlideOutComplete;

        protected override void Awake()
        {
            base.Awake();
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();

            // Store shown position and calculate hidden position
            _shownPosition = _rectTransform.anchoredPosition;
            CalculateHiddenPosition();

            // Start hidden
            _rectTransform.anchoredPosition = _hiddenPosition;
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        private void CalculateHiddenPosition()
        {
            _hiddenPosition = _slideFrom switch
            {
                SlideDirection.Right => _shownPosition + new Vector2(_slideOffset, 0),
                SlideDirection.Left => _shownPosition + new Vector2(-_slideOffset, 0),
                SlideDirection.Top => _shownPosition + new Vector2(0, _slideOffset),
                SlideDirection.Bottom => _shownPosition + new Vector2(0, -_slideOffset),
                _ => _shownPosition + new Vector2(_slideOffset, 0)
            };
        }

        /// <summary>
        /// Show the panel with slide-in animation.
        /// </summary>
        public void ShowSlide()
        {
            if (_isAnimating) StopAnimation();

            gameObject.SetActive(true);
            _currentAnimation = StartCoroutine(SlideInCoroutine());
        }

        /// <summary>
        /// Hide the panel with slide-out animation.
        /// </summary>
        /// <param name="onComplete">Callback when animation completes</param>
        public void HideSlide(Action onComplete = null)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_isAnimating) StopAnimation();

            _currentAnimation = StartCoroutine(SlideOutCoroutine(onComplete));
        }

        /// <summary>
        /// Show immediately without animation (override base).
        /// </summary>
        public override void Show()
        {
            ShowSlide();
        }

        /// <summary>
        /// Hide immediately without animation (override base).
        /// </summary>
        public override void Hide()
        {
            HideSlide();
        }

        /// <summary>
        /// Hide immediately without animation.
        /// </summary>
        public void HideImmediate()
        {
            if (_isAnimating) StopAnimation();

            _rectTransform.anchoredPosition = _hiddenPosition;
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            IsVisible = false;
            OnHidden();
        }

        private IEnumerator SlideInCoroutine()
        {
            _isAnimating = true;
            IsVisible = true;

            float elapsed = 0f;
            Vector2 startPos = _hiddenPosition;

            while (elapsed < _slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _slideDuration);
                float curveT = _slideCurve.Evaluate(t);

                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, _shownPosition, curveT);
                _canvasGroup.alpha = curveT;

                yield return null;
            }

            // Ensure final state
            _rectTransform.anchoredPosition = _shownPosition;
            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            _isAnimating = false;
            _currentAnimation = null;

            OnShown();
            OnSlideInComplete?.Invoke();
        }

        private IEnumerator SlideOutCoroutine(Action onComplete)
        {
            _isAnimating = true;

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            Vector2 startPos = _rectTransform.anchoredPosition;

            while (elapsed < _slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _slideDuration);
                float curveT = _slideCurve.Evaluate(t);

                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, _hiddenPosition, curveT);
                _canvasGroup.alpha = 1f - curveT;

                yield return null;
            }

            // Ensure final state
            _rectTransform.anchoredPosition = _hiddenPosition;
            _canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            IsVisible = false;

            _isAnimating = false;
            _currentAnimation = null;

            OnHidden();
            OnSlideOutComplete?.Invoke();
            onComplete?.Invoke();
        }

        private void StopAnimation()
        {
            if (_currentAnimation != null)
            {
                StopCoroutine(_currentAnimation);
                _currentAnimation = null;
            }
            _isAnimating = false;
        }

        /// <summary>
        /// Set the slide direction at runtime.
        /// </summary>
        public void SetSlideDirection(SlideDirection direction)
        {
            _slideFrom = direction;
            CalculateHiddenPosition();
        }

        /// <summary>
        /// Set the slide duration at runtime.
        /// </summary>
        public void SetSlideDuration(float duration)
        {
            _slideDuration = Mathf.Max(0.05f, duration);
        }

        private void OnDisable()
        {
            StopAnimation();
        }
    }
}
