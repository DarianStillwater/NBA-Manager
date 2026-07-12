using System.IO;
using UnityEditor;
using UnityEngine;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>
    /// Captures the Game view to Screenshots/gameview.png so the match can be
    /// inspected headlessly (works even when the display is asleep).
    /// </summary>
    public static class GameViewCapture
    {
        [MenuItem("Tools/NBA Head Coach/Capture Game View")]
        public static void Capture()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "gameview.png");

            // An unfocused editor skips Game-view repaints, so CaptureScreenshot would return a
            // stale backbuffer (sim advances, picture doesn't). Force a fresh repaint first, then
            // capture on the next editor tick so the repaint has actually rendered.
            var gvType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gvType != null)
            {
                var gv = EditorWindow.GetWindow(gvType, false, null, false);
                if (gv != null) gv.Repaint();
            }
            EditorApplication.QueuePlayerLoopUpdate();
            EditorApplication.delayCall += () =>
            {
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[GameViewCapture] Capturing to {path}");
            };
        }
    }
}
