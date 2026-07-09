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
        private CanvasGroup _cg;
        private bool _isHome;
        private Color _teamColor;

        private Vector2 _basePos;       // his spot on the sideline
        private float _huddleX;         // bench center — where the team gathers on a timeout
        private float _clock;           // local animation clock (accumulated deltaTime)
        private float _excitedTimer;    // counts down while jumping after a big play

        private const float PaceAmplitude = 10f;   // sideline pacing sweep (px)
        private const float PaceSpeed = 0.7f;
        private const float HopAmplitude = 12f;     // vertical jump on exciting plays (px)
        private const float ExcitedDuration = 1.4f;

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

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            var coach = go.AddComponent<CoachView>();
            coach._rect = rect;
            coach._body = body;
            coach._label = text;
            coach._cg = cg;
            coach._isHome = isHome;
            coach._teamColor = teamColor;
            coach._basePos = anchoredPos;
            coach._huddleX = 0f;   // bench center
            return coach;
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            Vector2 pos = _basePos;

            switch (State)
            {
                case CoachState.Idle:
                    // Pace back and forth along the sideline.
                    pos.x = _basePos.x + Mathf.Sin(_clock * PaceSpeed * Mathf.PI * 2f) * PaceAmplitude;
                    break;

                case CoachState.Excited:
                    // Jump up and down; auto-return to pacing when the moment passes.
                    _excitedTimer -= Time.deltaTime;
                    pos.y = _basePos.y + Mathf.Abs(Mathf.Sin(_clock * 12f)) * HopAmplitude * (_isHome ? -1f : 1f) * -1f;
                    if (_excitedTimer <= 0f) State = CoachState.Idle;
                    break;

                case CoachState.Huddle:
                    // Slide toward bench center and hold while the team gathers.
                    pos.x = Mathf.Lerp(_rect.anchoredPosition.x, _huddleX, 6f * Time.deltaTime);
                    break;

                case CoachState.Ejected:
                    // Fade out, then remove the icon entirely (like a fouled-out player leaving).
                    if (_cg != null)
                    {
                        _cg.alpha = Mathf.MoveTowards(_cg.alpha, 0f, 3f * Time.deltaTime);
                        if (_cg.alpha <= 0.01f) { Destroy(gameObject); return; }
                    }
                    else { Destroy(gameObject); return; }
                    return;   // don't reposition while fading
            }

            if (_rect != null) _rect.anchoredPosition = pos;
        }

        public Vector2 AnchoredPosition => _rect != null ? _rect.anchoredPosition : Vector2.zero;

        public void SetPositionImmediate(Vector2 pos)
        {
            if (_rect != null) _rect.anchoredPosition = pos;
        }

        /// <summary>Change the behaviour state. Ejected is terminal — once set the icon fades and
        /// removes itself and no later state takes effect (mirrors a fouled-out player leaving).</summary>
        public void SetState(CoachState state)
        {
            if (State == CoachState.Ejected) return;   // stays gone
            if (state == CoachState.Excited) _excitedTimer = ExcitedDuration;
            State = state;
        }

        public void Eject() => SetState(CoachState.Ejected);
    }
}
