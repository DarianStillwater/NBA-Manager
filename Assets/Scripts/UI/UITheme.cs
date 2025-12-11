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

        // Fonts
        public static int FontSizeHero = 32;
        public static int FontSizeHeader = 20;
        public static int FontSizeBody = 14;
        public static int FontSizeSmall = 12;

        public static string FontName = "LegacyRuntime.ttf";
    }
}
