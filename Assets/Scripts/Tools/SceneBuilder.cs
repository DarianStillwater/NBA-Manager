using UnityEngine;
using NBAHeadCoach.View;

namespace NBAHeadCoach.Tools
{
    public class SceneBuilder : MonoBehaviour
    {
        [ContextMenu("Build Match Scene Hierarchy")]
        public void BuildScene()
        {
            // 1. Root Objects
            GameObject viewRoot = new GameObject("ViewRoot");
            GameObject envRoot = new GameObject("Environment");
            
            // 2. Court
            GameObject court = GameObject.CreatePrimitive(PrimitiveType.Plane);
            court.name = "Court";
            court.transform.parent = envRoot.transform;
            court.transform.localScale = new Vector3(9.4f, 1, 5.0f); // 94x50 feet approx mapping

            // 3. Camera Rig
            GameObject camRig = new GameObject("CameraRig");
            Camera cam = camRig.AddComponent<Camera>();
            camRig.tag = "MainCamera";
            var director = camRig.AddComponent<CameraDirector>();
            
            // 4. Managers
            GameObject managers = new GameObject("MatchManagers");
            var viewer = managers.AddComponent<MatchViewer>();
            var sidelines = managers.AddComponent<SidelineManager>();
            var interact = managers.AddComponent<InteractionManager>();
            
            // 5. Connect References
            viewer.CourtCenter = court.transform;
            viewer.CamDirector = director;
            viewer.Sidelines = sidelines;
            
            interact.MainCamera = cam;
            
            sidelines.HomeBenchArea = CreateAnchor("HomeBench", envRoot.transform, new Vector3(0, 0, -30));
            sidelines.AwayBenchArea = CreateAnchor("AwayBench", envRoot.transform, new Vector3(0, 0, 30)); // Opposite side? Or same side usually? Same side in NBA usually.
            // Let's reset anchors to standard NBA sideline (Z = -30 approx)
            sidelines.HomeBenchArea.position = new Vector3(-20, 0, -30);
            sidelines.AwayBenchArea.position = new Vector3(20, 0, -30);

            Debug.Log("Scene Hierarchy Built! Please assign Prefabs (Player, Ball) to MatchViewer.");
        }

        private Transform CreateAnchor(string name, Transform parent, Vector3 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.parent = parent;
            go.transform.position = pos;
            return go.transform;
        }
    }
}
