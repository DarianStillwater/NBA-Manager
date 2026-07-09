using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Util;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Head-coach icon standing on the sideline in front of his bench. A decorative,
    /// ambient actor — never part of the possession SpatialState timeline. Its behaviour
    /// state machine (pacing, jumping on fast breaks, moving into the timeout huddle,
    /// vanishing on ejection) is layered on in later commits; C1 draws the static icon and
    /// exposes the position/removal API the director will drive.
    /// </summary>
    public class CoachView : MonoBehaviour
    {
        public enum CoachState { Idle, Excited, Huddle, Ejected }

        private RectTransform _rect;
        private Image _body;
        private Text _label;
        private bool _isHome;
        private Color _teamColor;

        public bool IsHome => _isHome;
        public CoachState State { get; private set; } = CoachState.Idle;
        public bool Ejected => State == CoachState.Ejected;

        /// <summary>Home coach sits on the −Y (bottom) sideline, away coach on the +Y (top).</summary>
        public static CoachView Create(RectTransform parent, float size, bool isHome,
            Color teamColor, Vector2 anchoredPos, string label = "HC")
        {
            var go = new GameObject(isHome ? "CoachHome" : "CoachAway", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = anchoredPos;

            // Square-ish body so the coach reads distinctly from the round player dots.
            var body = go.GetComponent<Image>();
            var sq = ArtManager.GetCircleDot();   // reuse; tinted + squared via scale below
            if (sq != null) body.sprite = sq;
            body.color = teamColor;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Label ("HC") centered
            var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var trt = textGO.GetComponent<RectTransform>();
            trt.SetParent(rect, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(size * 0.42f);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            float lum = teamColor.r * 0.299f + teamColor.g * 0.587f + teamColor.b * 0.114f;
            text.color = lum > 0.5f ? Color.black : Color.white;
            text.text = string.IsNullOrEmpty(label) ? "HC" : label;
            text.raycastTarget = false;

            var coach = go.AddComponent<CoachView>();
            coach._rect = rect;
            coach._body = body;
            coach._label = text;
            coach._isHome = isHome;
            coach._teamColor = teamColor;
            return coach;
        }

        public Vector2 AnchoredPosition => _rect != null ? _rect.anchoredPosition : Vector2.zero;

        public void SetPositionImmediate(Vector2 pos)
        {
            if (_rect != null) _rect.anchoredPosition = pos;
        }

        /// <summary>Change the behaviour state. Ejected is terminal — the icon is expected to be
        /// removed by the owner (mirrors a fouled-out player leaving the floor).</summary>
        public void SetState(CoachState state)
        {
            if (State == CoachState.Ejected) return;   // stays gone
            State = state;
        }
    }
}
