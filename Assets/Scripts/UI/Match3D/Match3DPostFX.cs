using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Adds a subtle URP post-processing look (broadcast bloom + a slight vignette) to the match
    /// camera, built entirely in code — a global <see cref="Volume"/> with an in-memory
    /// <see cref="VolumeProfile"/>. Everything is wrapped so that if the URP volume stack or an
    /// override type is unavailable at runtime the arena still renders; the failure logs once.
    /// </summary>
    public static class Match3DPostFX
    {
        private static bool _loggedFailure;

        public static void TryEnable(Camera cam, Transform parent)
        {
            if (cam == null) return;
            try
            {
                // The camera must opt in to post-processing under URP.
                var camData = cam.GetUniversalAdditionalCameraData();
                if (camData != null) camData.renderPostProcessing = true;

                var profile = ScriptableObject.CreateInstance<VolumeProfile>();

                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(0.55f);
                bloom.threshold.Override(1.05f);
                bloom.scatter.Override(0.6f);
                bloom.tint.Override(new Color(1f, 0.98f, 0.92f));

                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.28f);
                vignette.smoothness.Override(0.4f);
                vignette.color.Override(Color.black);

                var volGo = new GameObject("MatchPostFXVolume");
                if (parent != null) volGo.transform.SetParent(parent, false);
                var volume = volGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.weight = 1f;
                volume.profile = profile;
            }
            catch (System.Exception e)
            {
                if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    Debug.LogWarning($"[Match3DPostFX] Post-processing unavailable, skipping: {e.Message}");
                }
            }
        }
    }
}
