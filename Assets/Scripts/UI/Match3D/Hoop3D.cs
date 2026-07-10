using UnityEngine;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// A basket's runtime handle: owns the procedurally-built net so the view can trigger a quick
    /// "punch" squash-and-settle when a shot goes through this rim. Purely cosmetic — a made-shot
    /// event calls <see cref="Punch"/> on whichever hoop is nearest the shot, and the net snaps down
    /// then eases back to rest. Attached to the hoop group GameObject by <see cref="Match3DSceneBuilder"/>.
    /// </summary>
    public class Hoop3D : MonoBehaviour
    {
        private Transform _net;
        private Vector3 _restScale;
        private float _punch;          // 0 = rest, 1 = fully punched
        private const float PunchDecay = 4.5f; // eases back to rest in ~0.22s

        public float RimX { get; private set; }

        public void Configure(Transform net, float rimX)
        {
            _net = net;
            RimX = rimX;
            _restScale = net != null ? net.localScale : Vector3.one;
        }

        /// <summary>Kick the net: squash vertically + bulge outward, then settle. Idempotent and
        /// safe to call with no net (degrades to a no-op).</summary>
        public void Punch()
        {
            if (_net == null) return;
            _punch = 1f;
        }

        private void Update()
        {
            if (_net == null || _punch <= 0.0001f) return;

            _punch = Mathf.Max(0f, _punch - PunchDecay * Time.deltaTime);
            // Overshoot curve: strong down-stretch that swings back through rest.
            float swing = Mathf.Sin(_punch * Mathf.PI);
            float yScale = 1f + 0.55f * _punch;        // net stretches downward when hit
            float radial = 1f + 0.18f * swing;         // brief outward bulge
            _net.localScale = new Vector3(
                _restScale.x * radial,
                _restScale.y * yScale,
                _restScale.z * radial);

            if (_punch <= 0.0001f) _net.localScale = _restScale;
        }
    }
}
