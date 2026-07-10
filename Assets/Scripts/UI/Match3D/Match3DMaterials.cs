using UnityEngine;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Shared material/shader helpers for the runtime-built 3D match world. The project
    /// migrated to URP, so materials use "Universal Render Pipeline/Lit"; if that shader
    /// isn't present (e.g. a built-in-pipeline fallback) we drop to "Standard". Color and
    /// texture property names differ between the two pipelines, so both are set.
    /// </summary>
    public static class Match3DMaterials
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private static Shader _litShader;

        public static Shader LitShader
        {
            get
            {
                if (_litShader == null)
                    _litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                return _litShader;
            }
        }

        /// <summary>An opaque lit material tinted <paramref name="color"/>, low specular so the
        /// court and capsules read as matte broadcast surfaces rather than glossy plastic.</summary>
        public static Material CreateLit(Color color)
        {
            var mat = new Material(LitShader);
            ApplyColor(mat, color);
            // Matte-ish across both pipelines (guarded — the property name differs by shader).
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, 0.1f);
            if (mat.HasProperty(GlossinessId)) mat.SetFloat(GlossinessId, 0.1f);
            return mat;
        }

        /// <summary>Sets base color on whichever property the active shader exposes.</summary>
        public static void ApplyColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
            mat.color = color;
        }

        /// <summary>Sets the albedo texture on whichever property the active shader exposes.</summary>
        public static void ApplyTexture(Material mat, Texture texture)
        {
            if (mat == null) return;
            if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, texture);
            if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, texture);
            mat.mainTexture = texture;
        }

        /// <summary>Writes a team color into a MaterialPropertyBlock for per-actor tinting
        /// without spawning a material instance per player.</summary>
        public static void ApplyColor(MaterialPropertyBlock block, Color color)
        {
            if (block == null) return;
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
        }
    }
}
