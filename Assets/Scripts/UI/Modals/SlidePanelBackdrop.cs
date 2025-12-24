using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// Semi-transparent backdrop that appears behind modals.
    /// Supports click-away to close and fade animation.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SlidePanelBackdrop : MonoBehaviour, IPointerClickHandler
    {
        [Header("Configuration")]
        [SerializeField] private float _targetAlpha = 0.6f;
        [SerializeField] private float _fadeDuration = 0.2f;
        [SerializeField] private Color _backdropColor = new Color(0f, 0f, 0f, 1f);

        private Image _backdropImage;
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeCoroutine;
        private Action _onClickAway;
        private bool _allowClickAway = true;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _backdropImage = GetComponent<Image>();
            _canvasGroup = GetComponent<CanvasGroup>();

            // Configure image
            _backdropImage.color = _backdropColor;
            _backdropImage.raycastTarget = true;

            // Start hidden
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Show the backdrop with optional click-away callback.
        /// </summary>
        /// <param name="onClickAway">Called when user clicks on backdrop</param>
        /// <param name="allowClickAway">If false, clicking backdrop does nothing</param>
        public void Show(Action onClickAway = null, bool allowClickAway = true)
        {
            _onClickAway = onClickAway;
            _allowClickAway = allowClickAway;

            gameObject.SetActive(true);

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>
        /// Hide the backdrop with fade animation.
        /// </summary>
        /// <param name="onComplete">Called when fade completes</param>
        public void Hide(Action onComplete = null)
        {
            if (!gameObject.activeInHierarchy)
            {
                onComplete?.Invoke();
                return;
            }

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeOut(onComplete));
        }

        /// <summary>
        /// Hide immediately without animation.
        /// </summary>
        public void HideImmediate()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            IsVisible = false;
            _onClickAway = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_allowClickAway && _onClickAway != null)
            {
                _onClickAway.Invoke();
            }
        }

        /// <summary>
        /// Set whether clicking the backdrop triggers the callback.
        /// </summary>
        public void SetClickAwayEnabled(bool enabled)
        {
            _allowClickAway = enabled;
        }

        private IEnumerator FadeIn()
        {
            IsVisible = true;
            _canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, _targetAlpha, t);
                yield return null;
            }

            _canvasGroup.alpha = _targetAlpha;
            _fadeCoroutine = null;
        }

        private IEnumerator FadeOut(Action onComplete)
        {
            _canvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            _canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            IsVisible = false;
            _onClickAway = null;
            _fadeCoroutine = null;

            onComplete?.Invoke();
        }

        private void OnDisable()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }
    }
}
