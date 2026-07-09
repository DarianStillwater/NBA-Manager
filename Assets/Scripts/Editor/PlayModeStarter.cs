using UnityEditor;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>
    /// Menu hooks so automation (mcp-unity execute_menu_item) can toggle play mode —
    /// the built-in Edit/Play item is not executable through that bridge.
    /// </summary>
    public static class PlayModeStarter
    {
        [MenuItem("Tools/Enter Play Mode")]
        public static void EnterPlayMode()
        {
            // A leftover editor pause silently blocks the first frame after entering
            // play mode — automation would wait forever. Always clear it.
            EditorApplication.isPaused = false;
            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Exit Play Mode")]
        public static void ExitPlayMode()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }

        [MenuItem("Tools/Play Mode Status")]
        public static void PlayModeStatus()
        {
            UnityEngine.Debug.Log(
                $"[PlayModeStatus] isPlaying={EditorApplication.isPlaying} " +
                $"willChange={EditorApplication.isPlayingOrWillChangePlaymode} " +
                $"isPaused={EditorApplication.isPaused} " +
                $"isCompiling={EditorApplication.isCompiling} " +
                $"isUpdating={EditorApplication.isUpdating}");
        }
    }
}
