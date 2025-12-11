using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Scrolling news/scores ticker at the bottom of the screen
    /// </summary>
    public class ScrollingTicker : MonoBehaviour
    {
        [Header("Settings")]
        public float ScrollSpeed = 50f;
        public string[] Messages = new string[] 
        {
            "🏀 BREAKING: Lakers acquire draft pick in blockbuster trade",
            "📊 Tatum leads league in scoring with 32.5 PPG",
            "🔥 Warriors on 8-game winning streak",
            "📰 Coach of the Year race heating up",
            "⭐ All-Star voting opens next week"
        };

        private Text _text;
        private RectTransform _rt;
        private float _startX;
        private float _textWidth;
        private int _currentIndex = 0;

        private void Start()
        {
            _text = GetComponent<Text>();
            _rt = GetComponent<RectTransform>();
            
            if (_text == null)
            {
                _text = gameObject.AddComponent<Text>();
                _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _text.fontSize = 14;
                _text.color = new Color(0.8f, 0.8f, 0.8f);
                _text.alignment = TextAnchor.MiddleLeft;
            }

            UpdateMessage();
            _startX = Screen.width + 100;
            _rt.anchoredPosition = new Vector2(_startX, _rt.anchoredPosition.y);
        }

        private void Update()
        {
            _rt.anchoredPosition -= new Vector2(ScrollSpeed * Time.deltaTime, 0);

            // Reset when off screen
            if (_rt.anchoredPosition.x < -_textWidth - 100)
            {
                _currentIndex = (_currentIndex + 1) % Messages.Length;
                UpdateMessage();
                _rt.anchoredPosition = new Vector2(_startX, _rt.anchoredPosition.y);
            }
        }

        private void UpdateMessage()
        {
            _text.text = Messages[_currentIndex];
            // Estimate text width
            _textWidth = _text.preferredWidth;
        }
    }
}
