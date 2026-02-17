using UnityEngine;

namespace NBAHeadCoach.UI
{
    public static class UITheme
    {
        // Palettes - "Modern Ultra-Dark"
        public static readonly Color Background = new Color32(5, 5, 10, 255);         // Nearly Black #05050A
        public static readonly Color PanelSurface = new Color32(20, 24, 35, 255);     // Deep Blue-Grey #141823
        public static readonly Color PanelHeader = new Color32(30, 35, 50, 255);      // Lighter Header #1E2332

        // Gradients
        public static readonly Color GradientStart = new Color32(30, 41, 59, 255);    // Slate 800
        public static readonly Color GradientEnd = new Color32(15, 23, 42, 255);      // Slate 900

        // Accents - High Contrast
        public static readonly Color AccentPrimary = new Color32(255, 215, 0, 255);   // Pure Gold #FFD700
        public static readonly Color AccentSecondary = new Color32(96, 165, 250, 255);// Bright Blue #60A5FA

        // Text
        public static readonly Color TextPrimary = new Color32(255, 255, 255, 255);   // Pure White
        public static readonly Color TextSecondary = new Color32(203, 213, 225, 255); // Slate 300 (Much brighter than before)

        // Status
        public static readonly Color Success = new Color32(34, 197, 94, 255);         // Green
        public static readonly Color Warning = new Color32(234, 179, 8, 255);         // Yellow
        public static readonly Color Danger = new Color32(239, 68, 68, 255);          // Red

        // Card Styles (ESPN broadcast aesthetic)
        public static readonly Color CardBackground = new Color32(25, 28, 38, 240);
        public static readonly Color CardOutline = new Color(1f, 1f, 1f, 0.05f);

        // Layout Constants
        public const float CardPadding = 16f;
        public const float CardGap = 12f;
        public const float AccentBorderWidth = 4f;
        public const float SectionUnderlineHeight = 2f;
        public const float HeroHeight = 200f;

        // Fonts
        public static int FontSizeHeroNumber = 36;
        public static int FontSizeScoreNumber = 42;
        public static int FontSizeBroadcastTitle = 28;
        public static int FontSizeHero = 32;
        public static int FontSizeHeader = 20;
        public static int FontSizeBody = 14;
        public static int FontSizeSmall = 12;

        public static string FontName = "LegacyRuntime.ttf";

        /// <summary>
        /// Parse a hex color string (e.g. "#E03A3E") into a Color, with fallback.
        /// </summary>
        public static Color ParseTeamColor(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed))
                return parsed;
            return fallback;
        }

        /// <summary>
        /// Darken a color for broadcast-style backgrounds with minimum brightness floor.
        /// </summary>
        public static Color DarkenColor(Color color, float factor = 0.3f)
        {
            Color darkened = new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
            float brightness = darkened.r * 0.299f + darkened.g * 0.587f + darkened.b * 0.114f;
            if (brightness < 0.03f)
                darkened = Color.Lerp(darkened, new Color(0.1f, 0.1f, 0.15f, 1f), 0.5f);
            return darkened;
        }

        /// <summary>
        /// Create a subtle team-tinted panel background.
        /// </summary>
        public static Color TeamTintedBackground(Color teamColor, float intensity = 0.15f)
        {
            return Color.Lerp(PanelSurface, teamColor, intensity);
        }

        /// <summary>
        /// Create a subtle team-tinted card background.
        /// </summary>
        public static Color TeamTintedCard(Color teamColor, float intensity = 0.1f)
        {
            Color tinted = Color.Lerp(CardBackground, teamColor, intensity);
            tinted.a = CardBackground.a / 255f;
            return tinted;
        }
    }
}
