using UnityEngine;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Elevated sideline broadcast camera. Sits above the near sideline looking across the
    /// court, SmoothDamps its horizontal position to follow the ball's X (clamped so the court
    /// edges stay framed), and eases its FOV in slightly when the play works under either
    /// basket. Driven each frame by Match3DView via Focus(...).
    /// </summary>
    public class MatchBroadcastCamera : MonoBehaviour
    {
        // Rig height/depth: elevated behind the near sideline (−Z), raised angle looking down.
        private const float RigHeight = 27f;
        private const float RigDepth = -56f;
        private const float LookHeight = 4f;            // aim near player torso height
        private const float FollowSmoothTime = 0.45f;   // SmoothDamp horizontal ease
        // Clamp the rig's own X so it never dollies past the sideline extent, but the LOOK target
        // tracks the ball fully so the action stays roughly centered even when the rig is clamped.
        private const float MaxCameraX = 22f;
        private const float MaxLookX = 38f;
        private const float WideFov = 44f;              // covers ~half court at rig distance
        private const float TightFov = 38f;             // ease in under a basket
        private const float NearBasketX = 30f;          // play this deep = under a basket
        private const float FovLerpSpeed = 2.5f;

        private Camera _camera;
        private float _targetCamX;
        private float _targetLookX;
        private float _camX;
        private float _lookX;
        private float _camVel;
        private float _lookVel;
        private float _targetFov = WideFov;
        private bool _hasFocus;

        public void Configure(Camera cam)
        {
            _camera = cam;
            cam.fieldOfView = WideFov;
            var pos = new Vector3(0f, RigHeight, RigDepth);
            transform.localPosition = pos;
            transform.rotation = Quaternion.LookRotation(
                (new Vector3(0f, LookHeight, 0f) - pos).normalized, Vector3.up);
        }

        /// <summary>Point the rig at the current ball world position. Called every rendered frame.
        /// The rig dollies with the ball (clamped to the sideline extent) while the look target
        /// follows the ball fully, so the action frames near horizontal center instead of the edge.</summary>
        public void Focus(Vector3 ballWorld)
        {
            _hasFocus = true;
            _targetCamX = Mathf.Clamp(ballWorld.x, -MaxCameraX, MaxCameraX);
            _targetLookX = Mathf.Clamp(ballWorld.x, -MaxLookX, MaxLookX);
            _targetFov = Mathf.Abs(ballWorld.x) > NearBasketX ? TightFov : WideFov;
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            if (_hasFocus)
            {
                _camX = Mathf.SmoothDamp(_camX, _targetCamX, ref _camVel, FollowSmoothTime);
                _lookX = Mathf.SmoothDamp(_lookX, _targetLookX, ref _lookVel, FollowSmoothTime);
            }

            var pos = new Vector3(_camX, RigHeight, RigDepth);
            transform.localPosition = pos;

            var lookAt = new Vector3(_lookX, LookHeight, 0f);
            transform.rotation = Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up);

            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFov, FovLerpSpeed * Time.deltaTime);
        }
    }
}
