using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.UI.Components;

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

        // ═══════════════════════════════════════════
        //  VISUAL EFFECT HELPERS
        // ═══════════════════════════════════════════

        /// <summary>Apply vertical gradient. Set Image.color=white BEFORE calling.</summary>
        public static UIGradient ApplyGradient(GameObject go, Color top, Color bottom)
        {
            var grad = go.AddComponent<UIGradient>();
            grad.m_color1 = top;
            grad.m_color2 = bottom;
            return grad;
        }

        /// <summary>Add drop shadow for depth.</summary>
        public static Shadow ApplyShadow(GameObject go, float offsetX = 2f, float offsetY = -2f, float alpha = 0.35f)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(offsetX, offsetY);
            shadow.effectColor = new Color(0, 0, 0, alpha);
            return shadow;
        }

        /// <summary>Add outline border.</summary>
        public static Outline ApplyOutline(GameObject go, Color color, float dist = 1f)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(dist, dist);
            return outline;
        }

        // ═══════════════════════════════════════════
        //  CARD (with gradient + shadow)
        // ═══════════════════════════════════════════

        public static RectTransform Card(RectTransform parent, string title, Color accentColor)
        {
            var card = Child(parent, $"Card_{title}");
            var cardImg = card.AddComponent<Image>();
            cardImg.color = UITheme.CardFrosted;  // frosted glass — direct color, no gradient
            ApplyShadow(card, 2, -2, 0.4f);
            ApplyOutline(card, new Color(1f, 1f, 1f, 0.06f), 1f);  // subtle border

            var header = Child(card.GetComponent<RectTransform>(), "Header");
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0, 1);
            hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(0.5f, 1);
            hr.sizeDelta = new Vector2(0, UITheme.FMCardHeaderHeight);
            header.AddComponent<Image>().color = UITheme.CardHeaderFrosted;

            var accent = Child(hr, "Accent");
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = Vector2.zero;
            ar.anchorMax = new Vector2(0, 1);
            ar.pivot = new Vector2(0, 0.5f);
            ar.sizeDelta = new Vector2(4, 0);
            accent.AddComponent<Image>().color = accentColor;

            var titleText = Text(hr, "Title", title, 12, FontStyle.Bold, UITheme.AccentPrimary);
            Stretch(titleText.gameObject);
            titleText.GetComponent<RectTransform>().offsetMin = new Vector2(14, 0);
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

        /// <summary>
        /// Creates a fixed VLG content area (no scroll). Use instead of ScrollArea for no-scroll panels.
        /// </summary>
        public static RectTransform FixedArea(RectTransform parent, int spacing = 2, int padding = 8, bool expandRows = false)
        {
            var content = Child(parent, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            // Stretch to fill parent completely
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(padding, padding, padding, padding);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false; // rows opt-in via flexibleHeight=1 (TableRow with height=0)
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return contentRect;
        }

        public static RectTransform TableRow(RectTransform parent, float height, Color bgColor)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var le = row.AddComponent<LayoutElement>();
            if (height > 0)
            {
                le.preferredHeight = height;
                le.minHeight = height;
                le.flexibleHeight = 0;
            }
            else
            {
                le.flexibleHeight = 1; // expand to fill available space
            }
            row.AddComponent<Image>().color = bgColor;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(12, 8, 0, 0);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            return row.GetComponent<RectTransform>();
        }

        public static void TableCell(RectTransform row, string content, float width,
            FontStyle style = FontStyle.Normal, Color? color = null, int fontSize = 12)
        {
            var cell = Text(row, "Cell", content, fontSize, style, color ?? UITheme.TextSecondary);
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
