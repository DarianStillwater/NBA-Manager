using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>
    /// One-click developer shortcut into a live match. Arms DebugMatchBootstrap, picks the
    /// 2D or 3D court, opens the Boot scene if needed, and enters play mode — mirroring the
    /// PlayModeStarter pattern so it works through the mcp-unity automation bridge too.
    ///
    /// Pref keys written:
    ///   "DebugMatchBootstrap" = 1   → DebugMatchBootstrap drives Boot → new game → match
    ///   "MatchViewMode3D"     = 1/0 → MatchSceneSetup chooses the 3D world or the 2D court
    /// </summary>
    public static class DebugMatchMenu
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string ArmedPrefKey = "DebugMatchBootstrap";
        private const string ViewMode3DPrefKey = "MatchViewMode3D";

        [MenuItem("Tools/NBA Head Coach/Debug Match (3D)")]
        public static void DebugMatch3D() => LaunchDebugMatch(threeD: true);

        [MenuItem("Tools/NBA Head Coach/Debug Match (2D)")]
        public static void DebugMatch2D() => LaunchDebugMatch(threeD: false);

        private static void LaunchDebugMatch(bool threeD)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[DebugMatchMenu] Already in play mode — exit first, then relaunch.");
                return;
            }

            // Arm the runtime bootstrap and choose the court view mode.
            PlayerPrefs.SetInt(ArmedPrefKey, 1);
            PlayerPrefs.SetInt(ViewMode3DPrefKey, threeD ? 1 : 0);
            PlayerPrefs.Save();

            // Enter play mode from Boot so the normal init flow runs.
            var active = SceneManager.GetActiveScene();
            if (active.path != BootScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    // User cancelled the save prompt — disarm so a later manual Play isn't hijacked.
                    PlayerPrefs.SetInt(ArmedPrefKey, 0);
                    PlayerPrefs.Save();
                    return;
                }
                EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            }

            // A leftover editor pause silently blocks the first frame after entering play
            // mode — automation would wait forever. Always clear it (see PlayModeStarter).
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;

            Debug.Log($"[DebugMatchMenu] Launching debug match ({(threeD ? "3D" : "2D")}) — booting into a live game.");
        }
    }
}
