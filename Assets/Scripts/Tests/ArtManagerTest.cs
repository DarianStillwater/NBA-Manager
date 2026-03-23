using UnityEngine;
using NBAHeadCoach.Core.Util;

namespace NBAHeadCoach.Tests
{
    public class ArtManagerTest : MonoBehaviour
    {
        [ContextMenu("Test ArtManager")]
        public void RunTest()
        {
            // Test direct Resources.Load
            var tex = Resources.Load<Texture2D>("Art/Teams/logo_lal");
            Debug.Log($"[ArtManagerTest] Direct Texture2D load: {(tex != null ? $"OK ({tex.width}x{tex.height})" : "NULL")}");

            var sprite = Resources.Load<Sprite>("Art/Teams/logo_lal");
            Debug.Log($"[ArtManagerTest] Direct Sprite load: {(sprite != null ? "OK" : "NULL")}");

            // Test ArtManager
            var logo = ArtManager.GetTeamLogo("LAL");
            Debug.Log($"[ArtManagerTest] ArtManager.GetTeamLogo(LAL): {(logo != null ? "OK" : "NULL")}");

            var court = ArtManager.GetHalfCourt();
            Debug.Log($"[ArtManagerTest] ArtManager.GetHalfCourt(): {(court != null ? "OK" : "NULL")}");

            // List all Resources/Art files
            var all = Resources.LoadAll<Texture2D>("Art");
            Debug.Log($"[ArtManagerTest] Total textures in Resources/Art: {all.Length}");

            var allTeams = Resources.LoadAll<Texture2D>("Art/Teams");
            Debug.Log($"[ArtManagerTest] Total textures in Resources/Art/Teams: {allTeams.Length}");
        }

        private void Start()
        {
            RunTest();
        }
    }
}
