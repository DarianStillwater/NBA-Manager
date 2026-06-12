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
            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Exit Play Mode")]
        public static void ExitPlayMode()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }
    }
}
