using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Broadcast-style narration bar overlaid at the bottom-center of the court during
    /// highlights. Shows one radio-call line at a time ("Mitchell turns the corner!"),
    /// with escalating treatments: Excited = gold + pop, Special = badge chip ("SLAM!",
    /// "DAGGER!") + flash + shake. Runtime-generated UI, no assets. Hidden during skips.
    /// </summary>
    public class NarrationBarView : MonoBehaviour
    {
        private const float IdleFadeSeconds = 2.5f;

        private CanvasGroup _group;
        private RectTransform _rect;
        private Image _background;
        private Text _text;
        private RectTransform _badgeRect;
        private Image _badgeBg;
        private Text _badgeText;
        private Coroutine _fx;
        private float _lastShowTime;
        private bool _holdCurrent;   // windup lines ("...") hold until replaced

        private static readonly Color BgColor = new Color(0.08f, 0.09f, 0.14f, 0.85f);
        private static readonly Color GoldText = new Color(1f, 0.84f, 0.25f);
        private static readonly Color BadgeColor = new Color(0.95f, 0.25f, 0.2f);

        public static NarrationBarView Create(RectTransform courtRect)
        {
            var go = new GameObject("NarrationBar", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(courtRect, false);
            // Bottom-center of the court, clear of both hoops
            rect.anchorMin = new Vector2(0.22f, 0.03f);
            rect.anchorMax = new Vector2(0.78f, 0.115f);
            rect.sizeDelta = Vector2.zero;

            var view = go.AddComponent<NarrationBarView>();
            view._rect = rect;

            view._group = go.AddComponent<CanvasGroup>();
            view._group.alpha = 0f;
            view._group.blocksRaycasts = false;
            view._group.interactable = false;

            view._background = go.AddComponent<Image>();
            view._background.color = BgColor;
            view._background.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Call text (centered)
            var textGo = new GameObject("Call", typeof(RectTransform), typeof(Text));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 2f); textRect.offsetMax = new Vector2(-14f, -2f);
            view._text = textGo.GetComponent<Text>();
            view._text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._text.fontSize = 15;
            view._text.fontStyle = FontStyle.Bold;
            view._text.alignment = TextAnchor.MiddleCenter;
            view._text.color = Color.white;
            view._text.raycastTarget = false;
            view._text.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Badge chip (hidden unless Special): sits at the left edge of the bar
            var badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            view._badgeRect = badgeGo.GetComponent<RectTransform>();
            view._badgeRect.SetParent(rect, false);
            view._badgeRect.anchorMin = new Vector2(0f, 0.5f);
            view._badgeRect.anchorMax = new Vector2(0f, 0.5f);
            view._badgeRect.pivot = new Vector2(0f, 0.5f);
            view._badgeRect.anchoredPosition = new Vector2(6f, 0f);
            view._badgeRect.sizeDelta = new Vector2(110f, 24f);
            view._badgeBg = badgeGo.GetComponent<Image>();
            view._badgeBg.color = BadgeColor;
            view._badgeBg.raycastTarget = false;

            var badgeTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var btr = badgeTextGo.GetComponent<RectTransform>();
            btr.SetParent(view._badgeRect, false);
            btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one;
            btr.sizeDelta = Vector2.zero;
            view._badgeText = badgeTextGo.GetComponent<Text>();
            view._badgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._badgeText.fontSize = 12;
            view._badgeText.fontStyle = FontStyle.Bold;
            view._badgeText.alignment = TextAnchor.MiddleCenter;
            view._badgeText.color = Color.white;
            view._badgeText.raycastTarget = false;

            badgeGo.SetActive(false);
            return view;
        }

        /// <summary>Display a narration line, replacing whatever is showing.</summary>
        public void Show(NarrationLine line)
        {
            if (line == null || string.IsNullOrEmpty(line.Text)) return;

            if (_fx != null) StopCoroutine(_fx);
            _lastShowTime = Time.time;
            _holdCurrent = line.Text.EndsWith("...");   // windups hold for the result

            _text.text = line.Text;
            bool special = line.Style == NarrationStyle.Special && !string.IsNullOrEmpty(line.Badge);
            _badgeRect.gameObject.SetActive(special);
            if (special) _badgeText.text = line.Badge;

            switch (line.Style)
            {
                case NarrationStyle.Special:
                    _text.color = Color.white;
                    _text.fontSize = 17;
                    _fx = StartCoroutine(SpecialRoutine());
                    break;
                case NarrationStyle.Excited:
                    _text.color = GoldText;
                    _text.fontSize = 16;
                    _fx = StartCoroutine(PopRoutine());
                    break;
                default:
                    _text.color = Color.white;
                    _text.fontSize = 15;
                    _background.color = BgColor;
                    _group.alpha = 1f;
                    _rect.localScale = Vector3.one;
                    _fx = null;
                    break;
            }
        }

        /// <summary>Hide instantly (fast-forward skips, possession boundaries).</summary>
        public void HideImmediate()
        {
            if (_fx != null) { StopCoroutine(_fx); _fx = null; }
            _group.alpha = 0f;
            _rect.localScale = Vector3.one;
            _background.color = BgColor;
            _badgeRect.gameObject.SetActive(false);
            _holdCurrent = false;
        }

        private void Update()
        {
            // Auto-fade after idle (unless a windup "..." is holding for its result)
            if (_group.alpha > 0f && !_holdCurrent && Time.time - _lastShowTime > IdleFadeSeconds)
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.deltaTime * 3f);
        }

        private IEnumerator PopRoutine()
        {
            _group.alpha = 1f;
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.18f);
                float s = 1f + 0.08f * Mathf.Sin(u * Mathf.PI);
                _rect.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _rect.localScale = Vector3.one;
            _fx = null;
        }

        private IEnumerator SpecialRoutine()
        {
            _group.alpha = 1f;
            var basePos = _rect.anchoredPosition;
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.45f);
                // Background flash red → dark, brief shake, scale punch
                _background.color = Color.Lerp(new Color(0.8f, 0.15f, 0.1f, 0.95f), BgColor, u);
                float s = 1f + 0.14f * (1f - u);
                _rect.localScale = new Vector3(s, s, 1f);
                _rect.anchoredPosition = basePos + new Vector2(Mathf.Sin(u * 45f) * 3f * (1f - u), 0f);
                yield return null;
            }
            _rect.anchoredPosition = basePos;
            _rect.localScale = Vector3.one;
            _background.color = BgColor;
            _fx = null;
        }
    }
}
