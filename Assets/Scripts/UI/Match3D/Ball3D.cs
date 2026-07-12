using UnityEngine;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// The 3D basketball: a small orange sphere placed directly from BallState. Court feet map
    /// 1:1 to world units, and Height (feet) maps straight to world Y, so a shot arc, a bounce,
    /// and a held ball at ~4 ft all read correctly without extra scaling.
    /// </summary>
    public class Ball3D : MonoBehaviour
    {
        private const float DiameterFeet = 1.0f;
        private const float HeldDiameterFeet = 1.18f; // slightly larger while carried so it reads
        private const float HandHeightFeet = 3.3f;   // hip/hand carry height for a held ball
        private const float HandForwardFeet = 1.1f;  // offset ahead of the handler along facing
        // Push the carried ball toward the broadcast sideline (−Z, the default camera side) so an
        // elevated sideline view never sees it fully occluded behind the holder's body.
        private const float HeldCameraSideOffset = -0.7f;

        public static Ball3D Create(Transform parent)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Ball3D";
            sphere.transform.SetParent(parent, false);
            sphere.transform.localScale = Vector3.one * DiameterFeet;

            var col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Brighter, more saturated basketball orange with a slight self-glow so it pops
            // against the floor under broadcast lighting.
            var mat = Match3DMaterials.CreateLit(new Color(1f, 0.5f, 0.13f));
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", new Color(0.85f, 0.34f, 0.06f) * 0.35f);
            }
            sphere.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var ball = sphere.AddComponent<Ball3D>();
            ball.transform.localPosition = new Vector3(0f, CourtGeometry.HeldBallHeight, 0f);
            return ball;
        }

        /// <summary>Place the ball from a court-space BallState. Height (feet) → world Y.</summary>
        public void Render(BallState state)
        {
            SetLooseScale();
            transform.localPosition = new Vector3(state.X, Mathf.Max(state.Height, 0.1f), state.Y);
        }

        /// <summary>Place the ball at explicit interpolated court coordinates.</summary>
        public void Render(float courtX, float courtY, float heightFeet)
        {
            SetLooseScale();
            transform.localPosition = new Vector3(courtX, Mathf.Max(heightFeet, 0.1f), courtY);
        }

        private void SetLooseScale()
        {
            if (transform.localScale.x != DiameterFeet)
                transform.localScale = Vector3.one * DiameterFeet;
        }

        /// <summary>Carry the ball at the handler's hands: at hand height, offset slightly ahead
        /// along the handler's facing (court radians). Used while the ball is Held/Dribbled so it
        /// tracks the ball-handler instead of the stale/centered BallState coordinate.</summary>
        public void RenderHeld(float handlerX, float handlerY, float facingRad)
        {
            float fx = handlerX + Mathf.Cos(facingRad) * HandForwardFeet;
            float fy = handlerY + Mathf.Sin(facingRad) * HandForwardFeet + HeldCameraSideOffset;
            transform.localPosition = new Vector3(fx, HandHeightFeet, fy);
            if (transform.localScale.x != HeldDiameterFeet)
                transform.localScale = Vector3.one * HeldDiameterFeet;
        }

        // Nudge the ball just clear of the palm so it doesn't clip the hand mesh when riding the bone.
        private const float HandBoneNudgeFeet = 0.35f;

        /// <summary>Carry the ball at an explicit planar spot + height, held scale. Used when the ball
        /// rides the actual hand bone (height = bone height) or dribbles at the handler's hand XZ with
        /// the choreographed floor-to-hand bounce height. A small forward nudge along facing keeps it
        /// off the palm and toward the broadcast sideline so the holder never fully occludes it.</summary>
        public void RenderHand(float x, float z, float heightFeet, float facingRad)
        {
            float fx = x + Mathf.Cos(facingRad) * HandBoneNudgeFeet;
            float fz = z + Mathf.Sin(facingRad) * HandBoneNudgeFeet + HeldCameraSideOffset;
            transform.localPosition = new Vector3(fx, Mathf.Max(heightFeet, 0.1f), fz);
            if (transform.localScale.x != HeldDiameterFeet)
                transform.localScale = Vector3.one * HeldDiameterFeet;
        }

        public Vector3 WorldFocusPoint => transform.localPosition;
    }
}
