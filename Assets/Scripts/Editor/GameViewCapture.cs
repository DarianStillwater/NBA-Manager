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
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[GameViewCapture] Capturing to {path}");
        }
    }
}
