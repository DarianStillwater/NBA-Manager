using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Compact colored pill for status indicators (WIN/LOSS badges, injury status, etc.).
    /// ESPN broadcast style: bold white text on colored background.
    /// </summary>
    public class UIBadge : MonoBehaviour
    {
        private Image _backgroundImage;
        private Text _labelText;

        /// <summary>
        /// Create a badge with given text and background color.
        /// </summary>
        public static UIBadge Create(string name, Transform parent, string text, Color bgColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            // Background
            Image bg = go.AddComponent<Image>();
            bg.color = bgColor;

            // Auto-size to content
            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 4, 4);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            ContentSizeFitter csf = go.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Label
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Text labelTxt = labelGo.AddComponent<Text>();
            labelTxt.text = text;
            labelTxt.font = Resources.GetBuiltinResource<Font>(UITheme.FontName);
            labelTxt.fontSize = UITheme.FontSizeSmall;
            labelTxt.fontStyle = FontStyle.Bold;
            labelTxt.color = UITheme.TextPrimary;
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.raycastTarget = false;

            UIBadge badge = go.AddComponent<UIBadge>();
            badge._backgroundImage = bg;
            badge._labelText = labelTxt;

            return badge;
        }

        public static UIBadge CreateWinBadge(Transform parent)
        {
            return Create("WinBadge", parent, "WIN", UITheme.Success);
        }

        public static UIBadge CreateLossBadge(Transform parent)
        {
            return Create("LossBadge", parent, "LOSS", UITheme.Danger);
        }

        public void SetText(string text)
        {
            if (_labelText != null) _labelText.text = text;
        }

        public void SetColor(Color color)
        {
            if (_backgroundImage != null) _backgroundImage.color = color;
        }
    }
}
