using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BasePanel : MonoBehaviour
    {
        [Header("Configuration")]
        public string PanelId;
        public string Title;

        [Header("Transition")]
        [SerializeField] protected float _fadeDuration = 0.15f;
        [SerializeField] protected bool _useFadeTransition = false;

        private CanvasGroup _canvasGroup;
        private Coroutine _fadeCoroutine;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// Initialize the panel. Override in derived classes for custom setup.
        /// Called after Awake to set up UI elements and data.
        /// </summary>
        public virtual void Initialize() { }

        public virtual void Show()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            StopFade();
            gameObject.SetActive(true);

            if (_useFadeTransition && _fadeDuration > 0)
            {
                _canvasGroup.alpha = 0;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = true;
                _fadeCoroutine = StartCoroutine(FadeInCoroutine());
            }
            else
            {
                _canvasGroup.alpha = 1;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
                OnShown();
            }
        }

        public virtual void Hide()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            StopFade();
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            OnHidden();
        }

        private IEnumerator FadeInCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = true;
            _fadeCoroutine = null;
            OnShown();
        }

        private void StopFade()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
