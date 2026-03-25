using UnityEngine;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Fade-in + scale-up transition for panel content areas.
    /// Attach after building panel content. Self-destroys when complete.
    /// </summary>
    public class PanelTransition : MonoBehaviour
    {
        public float duration = 0.15f;
        public float startScale = 0.98f;

        private CanvasGroup _cg;
        private float _elapsed;

        private void Start()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
            transform.localScale = Vector3.one * startScale;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / duration);
            _cg.alpha = t;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, t);
            if (t >= 1f) Destroy(this);
        }
    }
}
