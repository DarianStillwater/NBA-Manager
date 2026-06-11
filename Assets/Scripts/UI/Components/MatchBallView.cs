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

        private RectTransform _rect;
        private RectTransform _shadowRect;
        private Image _ball;
        private Image _shadow;
        private float _baseSize;
        private float _pixelsPerFoot;

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

        /// <summary>Render the ball at an interpolated court position + height (feet).</summary>
        public void Render(Vector2 groundUiPos, float heightFeet, BallStatus status)
        {
            float lift = heightFeet * _pixelsPerFoot * HeightToPixelsPerFoot;
            _rect.anchoredPosition = groundUiPos + Vector2.up * lift;

            // Grow toward the apex so arcs read in top-down view
            float scale = 1f + Mathf.Clamp01(heightFeet / 25f) * 0.7f;
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
