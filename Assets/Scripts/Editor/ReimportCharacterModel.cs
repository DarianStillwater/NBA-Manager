using UnityEditor;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>Forces a reimport of the KayKit character FBX so the Humanoid-rig
    /// postprocessor applies (first import happens before the postprocessor compiles).</summary>
    public static class ReimportCharacterModel
    {
        [MenuItem("Tools/NBA Head Coach/Reimport Character Model")]
        public static void Reimport()
        {
            AssetDatabase.ImportAsset(
                "Assets/ThirdParty/KayKit_Adventurers/Rogue.fbx",
                ImportAssetOptions.ForceUpdate);
        }
    }
}
