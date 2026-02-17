using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Reusable styled card container with ESPN broadcast aesthetic.
    /// Dark background, subtle outline, optional colored left-accent border.
    /// </summary>
    public class UICardContainer : MonoBehaviour
    {
        public RectTransform ContentArea { get; private set; }

        private Image _backgroundImage;
        private Image _accentBorder;
        private CanvasGroup _canvasGroup;
        private UIGradient _gradient;

        /// <summary>
        /// Create a styled card container as a child of the given parent.
        /// </summary>
        public static UICardContainer Create(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Background
            Image bg = go.AddComponent<Image>();
            bg.color = UITheme.CardBackground;

            // Subtle outline
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = UITheme.CardOutline;
            outline.effectDistance = new Vector2(1, -1);

            // Layout
            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(
                (int)UITheme.CardPadding, (int)UITheme.CardPadding,
                (int)UITheme.CardPadding, (int)UITheme.CardPadding
            );
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // CanvasGroup for fade effects
            CanvasGroup cg = go.AddComponent<CanvasGroup>();

            UICardContainer card = go.AddComponent<UICardContainer>();
            card._backgroundImage = bg;
            card._canvasGroup = cg;
            card.ContentArea = rt;

            return card;
        }

        /// <summary>
        /// Add or update a 4px colored accent border on the left edge of the card.
        /// </summary>
        public void SetAccentBorder(Color color)
        {
            if (_accentBorder == null)
            {
                GameObject borderGo = new GameObject("AccentBorder", typeof(RectTransform));
                borderGo.transform.SetParent(transform, false);
                borderGo.transform.SetAsFirstSibling();

                RectTransform brt = borderGo.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0, 0);
                brt.anchorMax = new Vector2(0, 1);
                brt.pivot = new Vector2(0, 0.5f);
                brt.sizeDelta = new Vector2(UITheme.AccentBorderWidth, 0);
                brt.anchoredPosition = Vector2.zero;

                // Exclude from VerticalLayoutGroup so it stays 4px wide
                LayoutElement borderLE = borderGo.AddComponent<LayoutElement>();
                borderLE.ignoreLayout = true;

                _accentBorder = borderGo.AddComponent<Image>();

                // Adjust padding to account for border
                VerticalLayoutGroup vlg = GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.padding.left = (int)(UITheme.CardPadding + UITheme.AccentBorderWidth + 4);
                }
            }

            _accentBorder.color = color;
        }

        /// <summary>
        /// Set the card background color (override default).
        /// </summary>
        public void SetBackgroundColor(Color color)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = color;
        }

        /// <summary>
        /// Apply a vertical gradient (top to bottom) on the card background.
        /// </summary>
        public void SetGradient(Color topColor, Color bottomColor)
        {
            if (_gradient == null)
                _gradient = gameObject.AddComponent<UIGradient>();
            _gradient.m_color1 = topColor;
            _gradient.m_color2 = bottomColor;
        }

        /// <summary>
        /// Remove gradient effect.
        /// </summary>
        public void ClearGradient()
        {
            if (_gradient != null)
            {
                Destroy(_gradient);
                _gradient = null;
            }
        }
    }
}
