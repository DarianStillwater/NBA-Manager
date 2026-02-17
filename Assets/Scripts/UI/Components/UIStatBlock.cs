using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// ESPN-style stat display: large bold number on top, small gold label below.
    /// Used for record (32-18), streak (W5), home/away records, financial numbers.
    /// </summary>
    public class UIStatBlock : MonoBehaviour
    {
        public Text ValueText { get; private set; }
        public Text LabelText { get; private set; }

        /// <summary>
        /// Create a stat block with a large value and small label.
        /// </summary>
        public static UIStatBlock Create(string name, Transform parent, string label, string value)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Vertical stack
            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            // Hero number (top)
            GameObject valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(go.transform, false);
            Text valueTxt = valueGo.AddComponent<Text>();
            valueTxt.text = value;
            valueTxt.font = Resources.GetBuiltinResource<Font>(UITheme.FontName);
            valueTxt.fontSize = UITheme.FontSizeHeroNumber;
            valueTxt.fontStyle = FontStyle.Bold;
            valueTxt.color = UITheme.TextPrimary;
            valueTxt.alignment = TextAnchor.MiddleCenter;
            valueTxt.raycastTarget = false;
            LayoutElement valueLE = valueGo.AddComponent<LayoutElement>();
            valueLE.preferredHeight = 42f;

            // Label (bottom)
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Text labelTxt = labelGo.AddComponent<Text>();
            labelTxt.text = label;
            labelTxt.font = Resources.GetBuiltinResource<Font>(UITheme.FontName);
            labelTxt.fontSize = UITheme.FontSizeSmall;
            labelTxt.fontStyle = FontStyle.Bold;
            labelTxt.color = UITheme.AccentPrimary;
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.raycastTarget = false;
            LayoutElement labelLE = labelGo.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 18f;

            UIStatBlock block = go.AddComponent<UIStatBlock>();
            block.ValueText = valueTxt;
            block.LabelText = labelTxt;

            return block;
        }

        public void UpdateValue(string value)
        {
            if (ValueText != null) ValueText.text = value;
        }

        public void UpdateLabel(string label)
        {
            if (LabelText != null) LabelText.text = label;
        }
    }
}
