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

        private CanvasGroup _canvasGroup;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            // Only register if PanelId is set (may not be set yet if created via AddComponent)
            if (UIManager.Instance != null && !string.IsNullOrEmpty(PanelId))
                UIManager.Instance.RegisterPanel(PanelId, this);
        }

        /// <summary>
        /// Initialize the panel. Override in derived classes for custom setup.
        /// Called after Awake to set up UI elements and data.
        /// </summary>
        public virtual void Initialize() { }

        public virtual void Show()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            OnShown();
        }

        public virtual void Hide()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false); // Optimization
            OnHidden();
        }
        
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
