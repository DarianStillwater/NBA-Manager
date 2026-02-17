using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// ESPN-style section header with bold title and gold accent underline.
    /// Used for "LAST GAME", "ROSTER", "FINANCIALS" section labels.
    /// </summary>
    public class UISectionHeader : MonoBehaviour
    {
        private Text _titleText;
        private Text _subtitleText;

        /// <summary>
        /// Create a section header with title and accent underline.
        /// </summary>
        public static UISectionHeader Create(string name, Transform parent, string title)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;

            LayoutElement rootLE = go.AddComponent<LayoutElement>();
            rootLE.preferredHeight = 36f;

            // Title row (horizontal for title + optional subtitle)
            GameObject titleRow = new GameObject("TitleRow", typeof(RectTransform));
            titleRow.transform.SetParent(go.transform, false);
            HorizontalLayoutGroup hlg = titleRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 12f;
            LayoutElement titleRowLE = titleRow.AddComponent<LayoutElement>();
            titleRowLE.preferredHeight = 26f;

            // Title text
            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(titleRow.transform, false);
            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.text = title;
            titleTxt.font = Resources.GetBuiltinResource<Font>(UITheme.FontName);
            titleTxt.fontSize = UITheme.FontSizeHeader;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.color = UITheme.TextPrimary;
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.raycastTarget = false;
            LayoutElement titleLE = titleGo.AddComponent<LayoutElement>();
            titleLE.preferredWidth = 300f;

            // Subtitle text (hidden by default)
            GameObject subtitleGo = new GameObject("Subtitle", typeof(RectTransform));
            subtitleGo.transform.SetParent(titleRow.transform, false);
            Text subtitleTxt = subtitleGo.AddComponent<Text>();
            subtitleTxt.text = "";
            subtitleTxt.font = Resources.GetBuiltinResource<Font>(UITheme.FontName);
            subtitleTxt.fontSize = UITheme.FontSizeSmall;
            subtitleTxt.color = UITheme.TextSecondary;
            subtitleTxt.alignment = TextAnchor.MiddleLeft;
            subtitleTxt.raycastTarget = false;
            subtitleGo.SetActive(false);
            LayoutElement subtitleLE = subtitleGo.AddComponent<LayoutElement>();
            subtitleLE.preferredWidth = 200f;

            // Gold underline
            GameObject underline = new GameObject("Underline", typeof(RectTransform));
            underline.transform.SetParent(go.transform, false);
            Image underlineImg = underline.AddComponent<Image>();
            underlineImg.color = UITheme.AccentPrimary;
            LayoutElement underlineLE = underline.AddComponent<LayoutElement>();
            underlineLE.preferredHeight = UITheme.SectionUnderlineHeight;

            UISectionHeader header = go.AddComponent<UISectionHeader>();
            header._titleText = titleTxt;
            header._subtitleText = subtitleTxt;

            return header;
        }

        public void SetTitle(string title)
        {
            if (_titleText != null) _titleText.text = title;
        }

        public void SetSubtitle(string subtitle)
        {
            if (_subtitleText != null)
            {
                _subtitleText.text = subtitle;
                _subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }
        }
    }
}
