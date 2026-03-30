using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Menu
{
    /// <summary>
    /// Shared static UI helpers for main menu screens (menu, wizard, load game).
    /// </summary>
    public static class MenuUI
    {
        public static GameObject CreateChild(RectTransform parent, string name)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }

        public static GameObject CreateChild(Transform parent, string name)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }

        public static RectTransform Stretch(GameObject go)
        { var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero; return rt; }

        public static Text MkText(RectTransform parent, string content, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.text = content; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize; t.fontStyle = style; t.color = color; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text PlaceLabel(RectTransform parent, string text, float padX, ref float y)
        {
            var container = CreateChild(parent, "Lbl_" + text);
            var cr = container.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
            cr.pivot = new Vector2(0.5f, 1);
            cr.offsetMin = new Vector2(padX, 0); cr.offsetMax = new Vector2(-padX, 0);
            cr.anchoredPosition = new Vector2(0, y); cr.sizeDelta = new Vector2(cr.sizeDelta.x, 18);
            var t = MkText(cr, text, 11, FontStyle.Bold, UITheme.AccentPrimary);
            t.alignment = TextAnchor.MiddleLeft;
            Stretch(t.gameObject);
            y -= 20;
            return t;
        }

        public static InputField PlaceInput(RectTransform parent, string value, string placeholder, float padX, ref float y)
        {
            var go = CreateChild(parent, "Input");
            var gr = go.GetComponent<RectTransform>(); gr.anchorMin = new Vector2(0, 1); gr.anchorMax = new Vector2(1, 1);
            gr.pivot = new Vector2(0.5f, 1); gr.offsetMin = new Vector2(padX, 0); gr.offsetMax = new Vector2(-padX, 0);
            gr.anchoredPosition = new Vector2(0, y); gr.sizeDelta = new Vector2(gr.sizeDelta.x, 32);
            go.AddComponent<Image>().color = UITheme.PanelSurface;

            var tg = CreateChild(gr, "Text"); Stretch(tg);
            tg.GetComponent<RectTransform>().offsetMin = new Vector2(10, 2); tg.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -2);
            var t = tg.AddComponent<Text>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 14; t.color = Color.white; t.supportRichText = false;

            var pg = CreateChild(gr, "PH"); Stretch(pg);
            pg.GetComponent<RectTransform>().offsetMin = new Vector2(10, 2); pg.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -2);
            var pt = pg.AddComponent<Text>(); pt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pt.fontSize = 14; pt.fontStyle = FontStyle.Italic; pt.color = new Color(1, 1, 1, 0.3f); pt.text = placeholder;

            var input = go.AddComponent<InputField>(); input.textComponent = t; input.placeholder = pt; input.text = value;
            y -= 36;
            return input;
        }

        public static Text PlaceText(RectTransform parent, string text, int size, FontStyle style, Color color, float padX, ref float y, float height)
        {
            var t = MkText(parent, text, size, style, color);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = t.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1); r.offsetMin = new Vector2(padX, 0); r.offsetMax = new Vector2(-padX, 0);
            r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(r.sizeDelta.x, height);
            y -= height + 2;
            return t;
        }

        public static void MkMenuBtn(RectTransform parent, string label, Color color, System.Action onClick)
        {
            var go = CreateChild(parent, $"Btn_{label}");
            go.AddComponent<LayoutElement>().preferredHeight = 50;
            go.AddComponent<Image>().color = new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.9f);
            var btn = go.AddComponent<Button>();
            var c = btn.colors; c.highlightedColor = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 1f); btn.colors = c;
            btn.onClick.AddListener(() => onClick());
            var accent = CreateChild(go.GetComponent<RectTransform>(), "Accent");
            var ar = accent.GetComponent<RectTransform>(); ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0, 1); ar.sizeDelta = new Vector2(4, 0);
            accent.AddComponent<Image>().color = color;
            var t = MkText(go.GetComponent<RectTransform>(), label, 18, FontStyle.Bold, Color.white);
            t.alignment = TextAnchor.MiddleCenter; Stretch(t.gameObject);
        }
    }
}
