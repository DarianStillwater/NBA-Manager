using UnityEditor;
using UnityEngine;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>
    /// Forces the KayKit character pack AND the basketball clips under Assets/ThirdParty/Animations
    /// to import as Mecanim <b>Humanoid</b> rigs so their takes retarget onto our one
    /// AnimatorController. Scoped by path so it never touches other project models. GLB files in the
    /// pack are imported by glTFast's own scripted importer (which does not raise
    /// OnPreprocessModel), so only the .fbx is affected here. Animation FBX additionally get loop
    /// flags (by "_Loop" name) and root-motion locked into pose (positions come from the timeline).
    ///
    /// Runs automatically on import — no menu needed. If Unity's auto-avatar mapping can't resolve
    /// this Blender/Rigify skeleton cleanly, the model's Rig ▸ Configure step is a one-time manual
    /// fix (see the risk notes in the phase report).
    /// </summary>
    public class KayKitModelPostprocessor : AssetPostprocessor
    {
        private const string PackFolder = "Assets/ThirdParty/KayKit_Adventurers/";
        private const string AnimFolder = "Assets/ThirdParty/Animations/";

        private void OnPreprocessModel()
        {
            if (!(assetImporter is ModelImporter importer)) return;
            string path = assetPath.Replace('\\', '/');
            bool isPack = path.StartsWith(PackFolder);
            bool isAnim = path.StartsWith(AnimFolder);
            if (!isPack && !isAnim) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;   // keep named bones so we can find hands later

            // Basketball clips (AnimFolder): loop the "_Loop"-named files and bake root motion into
            // the pose so the clip never translates/rotates the actor — position, yaw and jump
            // height all come from the timeline (CharacterBody + PlayerActor3D). The KayKit pack
            // relies on runtime applyRootMotion=false for the same effect; single-take mocap FBX are
            // safer with the clip-level lock too. Guarded to AnimFolder so the working KayKit import
            // (which needs no per-clip tweaks) is left untouched.
            if (isAnim)
            {
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                // Mixamo names every take "mixamo.com" — rename each clip to the canonical
                // FILENAME so the animator builder's name lookup and the _Loop convention work.
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    c.name = clips.Length == 1 ? fileName : $"{fileName}_{i}";
                    if (fileName.EndsWith("_Loop")) { c.loopTime = true; c.loopPose = true; }
                    c.lockRootPositionXZ = true;   // no horizontal drift — XY comes from the timeline
                    c.lockRootHeightY = true;      // no vertical drift — VerticalOffset drives height
                    c.lockRootRotation = true;     // no spin — the actor root owns yaw
                }
                importer.clipAnimations = clips;
            }

            Debug.Log($"[KayKitModelPostprocessor] Forced Humanoid rig on {assetPath}");
        }
    }
}
