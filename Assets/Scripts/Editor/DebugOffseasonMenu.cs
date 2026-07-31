using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>
    /// One-click developer shortcut into a live offseason at a chosen date. Arms
    /// DebugOffseasonBootstrap, opens the Boot scene if needed, and enters play mode —
    /// mirroring DebugMatchMenu so it works through the mcp-unity automation bridge too.
    ///
    /// Pref key written:
    ///   "DebugOffseasonBootstrap" = "MM-dd" → the offseason date to fast-forward to
    /// </summary>
    public static class DebugOffseasonMenu
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string ArmedPrefKey = "DebugOffseasonBootstrap";

        [MenuItem("Tools/NBA Head Coach/Debug Offseason (Workouts Jun 12)")]
        public static void Workouts() => Launch("06-12", "pre-draft workouts + live draft board");

        [MenuItem("Tools/NBA Head Coach/Debug Offseason (Draft Night Jun 22)")]
        public static void DraftNight() => Launch("06-22", "draft night — you'll be on the clock, no auto-pick");

        [MenuItem("Tools/NBA Head Coach/Debug Offseason (FA Market Jul 10)")]
        public static void FreeAgency() => Launch("07-10", "free-agent market, bids and digest");

        [MenuItem("Tools/NBA Head Coach/Debug Offseason (Preseason Oct 8)")]
        public static void Preseason() => Launch("10-08", "training camp + preseason game day (PreGame)");

        private static void Launch(string monthDay, string what)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[DebugOffseasonMenu] Already in play mode — exit first, then relaunch.");
                return;
            }

            PlayerPrefs.SetString(ArmedPrefKey, monthDay);
            PlayerPrefs.Save();

            // Enter play mode from Boot so the normal init flow runs.
            var active = SceneManager.GetActiveScene();
            if (active.path != BootScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    // User cancelled the save prompt — disarm so a later manual Play isn't hijacked.
                    PlayerPrefs.SetString(ArmedPrefKey, "");
                    PlayerPrefs.Save();
                    return;
                }
                EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            }

            // A leftover editor pause silently blocks the first frame after entering play
            // mode — automation would wait forever. Always clear it (see PlayModeStarter).
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;

            Debug.Log($"[DebugOffseasonMenu] Launching debug offseason at {monthDay} — {what}. " +
                      "Entering play mode from Boot now.");
        }
    }
}
