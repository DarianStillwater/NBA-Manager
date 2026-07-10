using UnityEditor;
using UnityEngine;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>
    /// Forces the KayKit character pack's FBX to import as a Mecanim <b>Humanoid</b> rig so its
    /// 75 animation takes retarget onto our one AnimatorController. Scoped by path so it never
    /// touches other project models. GLB files in the pack are imported by glTFast's own scripted
    /// importer (which does not raise OnPreprocessModel), so only the .fbx is affected here.
    ///
    /// Runs automatically on import — no menu needed. If Unity's auto-avatar mapping can't resolve
    /// this Blender/Rigify skeleton cleanly, the model's Rig ▸ Configure step is a one-time manual
    /// fix (see the risk notes in the phase report).
    /// </summary>
    public class KayKitModelPostprocessor : AssetPostprocessor
    {
        private const string PackFolder = "Assets/ThirdParty/KayKit_Adventurers/";

        private void OnPreprocessModel()
        {
            if (!(assetImporter is ModelImporter importer)) return;
            if (!assetPath.Replace('\\', '/').StartsWith(PackFolder)) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;   // keep named bones so we can find hands later

            Debug.Log($"[KayKitModelPostprocessor] Forced Humanoid rig on {assetPath}");
        }
    }
}
