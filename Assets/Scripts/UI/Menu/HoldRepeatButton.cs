using UnityEngine;
using UnityEngine.EventSystems;

namespace NBAHeadCoach.UI.Menu
{
    /// <summary>
    /// Hold-to-repeat button: fires on first click, then repeats with acceleration while held.
    /// Slow start (3/sec), ramps to 20+/sec after ~2 seconds. SpeedMultiplier scales the ramp.
    /// </summary>
    public class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action OnTick;
        public float SpeedMultiplier = 1f;

        private bool _held;
        private float _holdTime;
        private float _nextTick;

        public void OnPointerDown(PointerEventData eventData)
        {
            _held = true;
            _holdTime = 0f;
            _nextTick = 0f;
            OnTick?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _held = false;
        }

        private void Update()
        {
            if (!_held) return;
            _holdTime += Time.deltaTime;

            float t = Mathf.Clamp01(_holdTime * SpeedMultiplier / 2f);
            float interval = Mathf.Lerp(0.33f, 0.05f, t * t);

            _nextTick -= Time.deltaTime;
            if (_nextTick <= 0f)
            {
                if (_holdTime > 0.4f)
                    OnTick?.Invoke();
                _nextTick = interval;
            }
        }
    }
}
