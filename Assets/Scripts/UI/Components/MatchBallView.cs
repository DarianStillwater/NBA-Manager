using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Util;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Dumb renderer for the ball: positions a sprite from a BallState (X/Y/Height).
    /// The choreographer bakes all flight into the states, so this class never
    /// synthesizes motion — height lifts the sprite and scales it toward the apex,
    /// with a ground shadow for depth. Replaces BallAnimator for the match view.
    /// </summary>
    public class MatchBallView : MonoBehaviour
    {
        private const float HeightToPixelsPerFoot = 1.2f; // visual lift per foot of height

        private const float SpinDegPerPx = 3f;     // roll rate with horizontal travel

        private RectTransform _rect;
        private RectTransform _shadowRect;
        private Image _ball;
        private Image _shadow;
        private float _baseSize;
        private float _pixelsPerFoot;

        private float _spin;
        private Vector2 _lastPos;
        private bool _hasLast;
        private float _punch;                       // slam scale-punch, decays each frame

        /// <summary>Base airborne spin (deg/sec) by shot style — a carried dunk barely rotates,
        /// a catch-and-shoot three has heavy backspin.</summary>
        private static float SpinFor(NBAHeadCoach.Core.Simulation.ShotType? style)
        {
            switch (style)
            {
                case NBAHeadCoach.Core.Simulation.ShotType.Dunk: return 60f;
                case NBAHeadCoach.Core.Simulation.ShotType.Layup: return 140f;
                case NBAHeadCoach.Core.Simulation.ShotType.TipIn:
                case NBAHeadCoach.Core.Simulation.ShotType.Hookshot: return 220f;
                case NBAHeadCoach.Core.Simulation.ShotType.Floater: return 300f;
                case NBAHeadCoach.Core.Simulation.ShotType.Jumper:
                case NBAHeadCoach.Core.Simulation.ShotType.StepBack:
                case NBAHeadCoach.Core.Simulation.ShotType.Fadeaway:
                case NBAHeadCoach.Core.Simulation.ShotType.PullUp: return 330f;
                case NBAHeadCoach.Core.Simulation.ShotType.CatchAndShoot: return 360f;
                case NBAHeadCoach.Core.Simulation.ShotType.Heave: return 430f;
                default: return 200f;   // passes / caroms / loose balls
            }
        }

        public static MatchBallView Create(RectTransform parent, float ballSize, float pixelsPerFoot)
        {
            // Shadow goes first so it renders under the ball
            var shadowGo = new GameObject("BallShadow", typeof(RectTransform), typeof(Image));
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.SetParent(parent, false);
            shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(ballSize * 0.8f, ballSize * 0.45f);
            var shadow = shadowGo.GetComponent<Image>();
            shadow.sprite = ArtManager.GetCircleDot();
            shadow.color = new Color(0f, 0f, 0f, 0.3f);
            shadow.raycastTarget = false;

            var go = new GameObject("MatchBall", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ballSize, ballSize);

            var img = go.GetComponent<Image>();
            var ballSprite = ArtManager.GetBasketball();
            img.sprite = ballSprite != null ? ballSprite : ArtManager.GetCircleDot();
            img.color = ballSprite != null ? Color.white : new Color(0.95f, 0.55f, 0.15f);
            img.raycastTarget = false;

            var view = go.AddComponent<MatchBallView>();
            view._rect = rect;
            view._shadowRect = shadowRect;
            view._ball = img;
            view._shadow = shadow;
            view._baseSize = ballSize;
            view._pixelsPerFoot = pixelsPerFoot;
            return view;
        }

        /// <summary>Slam punch: brief scale spike (e.g. on a dunk at the rim), decays in Render.</summary>
        public void Punch(float strength)
        {
            _punch = Mathf.Max(_punch, strength);
        }

        /// <summary>Render the ball at an interpolated court position + height (feet).</summary>
        public void Render(Vector2 groundUiPos, float heightFeet, BallStatus status,
            NBAHeadCoach.Core.Simulation.ShotType? style = null)
        {
            float lift = heightFeet * _pixelsPerFoot * HeightToPixelsPerFoot;
            Vector2 ballPos = groundUiPos + Vector2.up * lift;
            _rect.anchoredPosition = ballPos;

            // Spin: the ball rolls in its travel direction while airborne or loose, with a base
            // rate keyed to the shot style (a carried dunk barely turns; a three has heavy
            // backspin). When gripped (held/dribbled/dead) it holds its last angle.
            bool airborne = status == BallStatus.InAir_Shot || status == BallStatus.InAir_Pass || status == BallStatus.Loose;
            if (_hasLast && airborne)
            {
                Vector2 d = ballPos - _lastPos;
                float dir = d.x >= 0f ? -1f : 1f;   // clockwise when moving right
                _spin += dir * (d.magnitude * SpinDegPerPx + SpinFor(style) * Time.deltaTime);
                _rect.localEulerAngles = new Vector3(0f, 0f, _spin);
            }
            _lastPos = ballPos;
            _hasLast = true;

            // Grow toward the apex so arcs read in top-down view; slam punch decays on top.
            _punch = Mathf.MoveTowards(_punch, 0f, Time.deltaTime * 4f);
            float scale = (1f + Mathf.Clamp01(heightFeet / 25f) * 0.7f) * (1f + _punch);
            _rect.sizeDelta = new Vector2(_baseSize * scale, _baseSize * scale);

            _shadowRect.anchoredPosition = groundUiPos;
            float shadowFade = Mathf.Clamp01(1f - heightFeet / 35f);
            _shadow.color = new Color(0f, 0f, 0f, 0.3f * shadowFade);
            float shadowScale = 1f + Mathf.Clamp01(heightFeet / 25f) * 0.5f;
            _shadowRect.sizeDelta = new Vector2(_baseSize * 0.8f * shadowScale, _baseSize * 0.45f * shadowScale);

            // Keep ball + shadow on top of player dots
            _shadowRect.SetAsLastSibling();
            _rect.SetAsLastSibling();
        }

        public void SetVisible(bool visible)
        {
            _ball.enabled = visible;
            _shadow.enabled = visible;
        }
    }
}
