using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Animates a number counting up from 0 to target value
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class AnimatedNumber : MonoBehaviour
    {
        [Header("Settings")]
        public float TargetValue = 100f;
        public float Duration = 1.5f;
        public string Prefix = "";
        public string Suffix = "%";
        public int DecimalPlaces = 0;

        private Text _text;
        private float _currentValue = 0f;
        private float _velocity = 0f;
        private bool _started = false;

        private void Awake()
        {
            _text = GetComponent<Text>();
        }

        private void OnEnable()
        {
            _started = true;
            _currentValue = 0f;
        }

        private void Update()
        {
            if (!_started) return;

            _currentValue = Mathf.SmoothDamp(_currentValue, TargetValue, ref _velocity, Duration * 0.3f);
            
            if (Mathf.Abs(_currentValue - TargetValue) < 0.1f)
            {
                _currentValue = TargetValue;
                _started = false;
            }

            string format = DecimalPlaces > 0 ? $"F{DecimalPlaces}" : "F0";
            _text.text = $"{Prefix}{_currentValue.ToString(format)}{Suffix}";
        }

        public void SetTarget(float target)
        {
            TargetValue = target;
            _started = true;
            _currentValue = 0f;
        }
    }
}
