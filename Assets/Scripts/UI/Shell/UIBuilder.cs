using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Shell
{
    /// <summary>
    /// Shared static helpers for programmatic UI construction.
    /// Used by GameShell, MenuShell, and all panel classes.
    /// </summary>
    public static class UIBuilder
    {
        public static GameObject Child(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static Text Text(RectTransform parent, string name, string content,
            int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static RectTransform Card(RectTransform parent, string title, Color accentColor)
        {
            var card = Child(parent, $"Card_{title}");
            card.AddComponent<Image>().color = UITheme.CardBackground;

            var header = Child(card.GetComponent<RectTransform>(), "Header");
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0, 1);
            hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(0.5f, 1);
            hr.sizeDelta = new Vector2(0, UITheme.FMCardHeaderHeight);
            header.AddComponent<Image>().color = UITheme.FMCardHeaderBg;

            var accent = Child(hr, "Accent");
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = Vector2.zero;
            ar.anchorMax = new Vector2(0, 1);
            ar.pivot = new Vector2(0, 0.5f);
            ar.sizeDelta = new Vector2(3, 0);
            accent.AddComponent<Image>().color = accentColor;

            var titleText = Text(hr, "Title", title, 12, FontStyle.Bold, UITheme.AccentPrimary);
            Stretch(titleText.gameObject);
            titleText.GetComponent<RectTransform>().offsetMin = new Vector2(12, 0);
            titleText.alignment = TextAnchor.MiddleLeft;

            return card.GetComponent<RectTransform>();
        }

        public static RectTransform ScrollArea(RectTransform parent)
        {
            var scroll = parent.gameObject.AddComponent<ScrollRect>();
            var viewport = Child(parent, "Viewport");
            Stretch(viewport);
            viewport.AddComponent<RectMask2D>();

            var content = Child(viewport.GetComponent<RectTransform>(), "Content");
            var contentRect = Stretch(content);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30;

            return contentRect;
        }

        public static RectTransform TableRow(RectTransform parent, float height, Color bgColor)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = height;
            row.AddComponent<Image>().color = bgColor;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(12, 8, 0, 0);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            return row.GetComponent<RectTransform>();
        }

        public static void TableCell(RectTransform row, string content, float width,
            FontStyle style = FontStyle.Normal, Color? color = null)
        {
            var cell = Text(row, "Cell", content, 12, style, color ?? UITheme.TextSecondary);
            cell.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            cell.alignment = TextAnchor.MiddleLeft;
            cell.horizontalOverflow = HorizontalWrapMode.Wrap;
            cell.verticalOverflow = VerticalWrapMode.Truncate;
        }

        public static void FillCard(Text text)
        {
            var rt = text.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 8);
            rt.offsetMax = new Vector2(-12, -(UITheme.FMCardHeaderHeight + 4));
            text.alignment = TextAnchor.UpperLeft;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public static void Divider(Transform parent)
        {
            var div = new GameObject("Divider", typeof(RectTransform));
            div.transform.SetParent(parent, false);
            div.AddComponent<LayoutElement>().preferredHeight = UITheme.FMNavDividerHeight;
            div.AddComponent<Image>().color = UITheme.FMDivider;
        }
    }
}
