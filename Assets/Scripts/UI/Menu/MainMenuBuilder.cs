using System;
using UnityEngine;
using UnityEngine.UI;
using static NBAHeadCoach.UI.Menu.MenuUI;

namespace NBAHeadCoach.UI.Menu
{
    /// <summary>
    /// Builds the main menu screen (New Game, Load Game, Quit).
    /// </summary>
    public static class MainMenuBuilder
    {
        public static void Build(RectTransform root, Action onNewGame, Action onLoadGame)
        {
            foreach (Transform child in root) UnityEngine.Object.Destroy(child.gameObject);
            var center = CreateChild(root, "Center");
            var cr = center.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.3f, 0.2f); cr.anchorMax = new Vector2(0.7f, 0.8f); cr.sizeDelta = Vector2.zero;

            var vlg = center.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20; vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true;

            var title = MkText(cr, "NBA HEAD COACH", 36, FontStyle.Bold, UITheme.AccentPrimary);
            title.alignment = TextAnchor.MiddleCenter;
            (title.gameObject.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>()).preferredHeight = 60;

            var sub = MkText(cr, "Franchise Management Simulation", 16, FontStyle.Normal, UITheme.TextSecondary);
            sub.alignment = TextAnchor.MiddleCenter;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            CreateChild(cr, "Spacer").AddComponent<LayoutElement>().preferredHeight = 40;

            MkMenuBtn(cr, "NEW GAME", UITheme.AccentPrimary, onNewGame);
            MkMenuBtn(cr, "LOAD GAME", UITheme.AccentSecondary, onLoadGame);
            MkMenuBtn(cr, "QUIT", UITheme.TextSecondary, () => {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
    }
}
