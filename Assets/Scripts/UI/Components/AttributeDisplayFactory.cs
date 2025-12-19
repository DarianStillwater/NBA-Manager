using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Factory for creating consistent attribute display rows across panels.
    /// Eliminates duplicate code in StaffPanel, StaffHiringPanel, RosterPanel, etc.
    /// </summary>
    public static class AttributeDisplayFactory
    {
        // Color thresholds for rating display
        private const int ELITE_THRESHOLD = 85;
        private const int GOOD_THRESHOLD = 75;
        private const int AVERAGE_THRESHOLD = 65;

        // Standard colors
        private static readonly Color EliteColor = Color.green;
        private static readonly Color GoodColor = new Color(0.4f, 0.7f, 1f);  // Light blue
        private static readonly Color AverageColor = Color.yellow;
        private static readonly Color BelowAverageColor = Color.gray;

        /// <summary>
        /// Gets the color for a rating value.
        /// </summary>
        public static Color GetRatingColor(int rating)
        {
            if (rating >= ELITE_THRESHOLD) return EliteColor;
            if (rating >= GOOD_THRESHOLD) return GoodColor;
            if (rating >= AVERAGE_THRESHOLD) return AverageColor;
            return BelowAverageColor;
        }

        /// <summary>
        /// Creates a horizontal attribute row with name and colored value.
        /// </summary>
        public static GameObject CreateAttributeRow(
            Transform parent,
            string name,
            int value,
            float labelWidth = 120f,
            float valueWidth = 40f,
            int fontSize = 11)
        {
            var row = new GameObject("Attribute_" + name);
            row.transform.SetParent(parent, false);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Name label
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            var nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = labelWidth;
            var nameText = nameObj.AddComponent<Text>();
            nameText.text = name;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = fontSize;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;

            // Value
            var valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            var valueLayout = valueObj.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = valueWidth;
            var valueText = valueObj.AddComponent<Text>();
            valueText.text = value.ToString();
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = fontSize;
            valueText.color = GetRatingColor(value);
            valueText.alignment = TextAnchor.MiddleRight;

            return row;
        }

        /// <summary>
        /// Populates a container with attribute rows from a dictionary.
        /// </summary>
        public static void PopulateAttributeContainer(
            Transform container,
            System.Collections.Generic.Dictionary<string, int> attributes,
            float labelWidth = 120f,
            float valueWidth = 40f)
        {
            // Clear existing children
            foreach (Transform child in container)
            {
                Object.Destroy(child.gameObject);
            }

            // Create rows
            foreach (var attr in attributes)
            {
                CreateAttributeRow(container, attr.Key, attr.Value, labelWidth, valueWidth);
            }
        }

        /// <summary>
        /// Sets the color of a Text component based on rating.
        /// Useful for rating text fields in detail panels.
        /// </summary>
        public static void ApplyRatingColor(Text text, int rating)
        {
            if (text != null)
            {
                text.color = GetRatingColor(rating);
            }
        }
    }
}
