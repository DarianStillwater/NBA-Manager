using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>
    /// One-shot migration: creates the URP pipeline asset + renderer under
    /// Assets/Settings and assigns them to Graphics and all Quality levels.
    /// Safe to re-run; overwrites the same asset paths.
    /// </summary>
    public static class URPMigrationTool
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/URP_Renderer.asset";
        private const string PipelinePath = SettingsFolder + "/URP_Pipeline.asset";

        [MenuItem("Tools/NBA Head Coach/Migrate To URP")]
        public static void Migrate()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);

            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int currentLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentLevel, false);

            AssetDatabase.SaveAssets();
            Debug.Log("[URPMigrationTool] URP pipeline created and assigned to Graphics + all Quality levels.");
        }
    }
}
