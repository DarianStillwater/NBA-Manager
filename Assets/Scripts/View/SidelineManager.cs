using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data; // For Player/Team data access

namespace NBAHeadCoach.View
{
    public class SidelineManager : MonoBehaviour
    {
        [Header("Setup")]
        public Transform HomeBenchArea;
        public Transform AwayBenchArea;
        public GameObject CoachPrefab; // Generic Coach Model
        public GameObject BenchPlayerPrefab; // Sitting pose model preferred

        private List<GameObject> _homeBenchVisuals = new List<GameObject>();
        private List<GameObject> _awayBenchVisuals = new List<GameObject>();

        public void InitializeSideLines(Team home, Team away, List<string> homeActive, List<string> awayActive)
        {
            SpawnBench(home, homeActive, HomeBenchArea, _homeBenchVisuals);
            SpawnBench(away, awayActive, AwayBenchArea, _awayBenchVisuals);
            
            SpawnCoach(home, HomeBenchArea);
            SpawnCoach(away, AwayBenchArea);
        }

        private void SpawnBench(Team team, List<string> activeIds, Transform area, List<GameObject> visualList)
        {
            if (area == null) return;

            // Simple row layout for now
            Vector3 startPos = area.position;
            Vector3 offset = area.right * 1.0f; // 1 meter apart

            int spawnedCount = 0;
            
            // In a real implementation, we'd check the full Roster and subtract Active players
            // For now, simple loop to spawn placeholders
            foreach(var pid in team.RosterPlayerIds)
            {
                if (activeIds.Contains(pid)) continue; // On court
                
                Vector3 pos = startPos + (offset * spawnedCount);
                GameObject benchPlayer = Instantiate(BenchPlayerPrefab, pos, area.rotation, area);
                
                // Setup visual (name, jersey)
                var visual = benchPlayer.GetComponent<PlayerVisual>();
                if (visual != null)
                {
                    // visual.Initialize(pid, ...); // Needs PlayerDatabase lookup
                    // Force "Sit" animation state
                    if (visual.Animator != null) visual.Animator.Play("Sit");
                }
                
                visualList.Add(benchPlayer);
                spawnedCount++;
            }
        }

        private void SpawnCoach(Team team, Transform area)
        {
            if (area == null || CoachPrefab == null) return;
            
            // Coach usually stands near the scorer's table or end of bench
            Vector3 coachPos = area.position + (area.forward * 2.0f); // In front of bench
            GameObject coach = Instantiate(CoachPrefab, coachPos, area.rotation, area);
            coach.name = $"Coach_{team.Nickname}";
            
            // Add Coach behavior/animations here
        }
    }
}
