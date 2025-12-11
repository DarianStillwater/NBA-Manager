using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Animates a pulsing glow effect on an Image component
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class PulseGlow : MonoBehaviour
    {
        [Header("Pulse Settings")]
        public float PulseSpeed = 2f;
        public float MinAlpha = 0.3f;
        public float MaxAlpha = 0.8f;
        public Color GlowColor = new Color(1f, 0.84f, 0f); // Gold

        private Image _image;
        private float _time;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void Update()
        {
            _time += Time.deltaTime * PulseSpeed;
            float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, (Mathf.Sin(_time) + 1f) / 2f);
            _image.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, alpha);
        }
    }
}
