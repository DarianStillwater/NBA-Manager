using UnityEngine;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Pure render-side motion math shared by the actor/body layer (and unit-tested in isolation):
    /// turning measured body motion into animator inputs + lean angles, plus the inter-possession
    /// bridge-skip threshold. No scene state — just the small formulas, so the numbers are one place.
    /// </summary>
    public static class ActorMotion
    {
        // Below this first-frame displacement (feet) the incoming timeline already starts where the
        // previous one ended, so the bridge slide is imperceptible and skipped. The bridge stays the
        // fallback for subs / quarter starts / skips, which move players far past this.
        public const float BridgeSkipFeet = 3f;

        // Locomotion clip authored ground speeds (ft/s): the walk clip ≈ Walk, the run clip ≈ Run.
        // Reference speed lerps between them by blend position so MotionRate sits near 1 at each
        // clip's natural cadence and only stretches/squashes for off-cadence motion. Tuning knobs.
        public const float WalkRefFeetPerSec = 5f;
        public const float RunRefFeetPerSec = 14f;

        private const float AccelToPitch = 0.25f;   // deg pitch per ft/s^2 — tuning knob
        private const float YawToRoll = 0.03f;       // deg roll per deg/s yaw — tuning knob

        /// <summary>True when the first-frame displacement is large enough that sliding the actors
        /// across it reads; below the threshold the bridge is a no-op (timelines already continuous).</summary>
        public static bool ShouldBridge(float maxDisplacementFeet) => maxDisplacementFeet >= BridgeSkipFeet;

        /// <summary>Playback-rate multiplier for the locomotion blend tree: measured foot speed over
        /// the blended clip reference speed, clamped so feet never moon-walk or freeze.</summary>
        public static float MotionRate(float measuredFeetPerSec, float speedBlend01)
        {
            float reference = Mathf.Lerp(WalkRefFeetPerSec, RunRefFeetPerSec, Mathf.Clamp01(speedBlend01));
            if (reference < 0.1f) return 1f;
            return Mathf.Clamp(measuredFeetPerSec / reference, 0.7f, 1.4f);
        }

        /// <summary>Forward/back body pitch (deg) from planar acceleration: lean into accel, rock back
        /// on decel, clamped to a subtle broadcast range.</summary>
        public static float LeanPitchDegrees(float planarAccelFeetPerSec2, float maxDeg) =>
            Mathf.Clamp(planarAccelFeetPerSec2 * AccelToPitch, -maxDeg, maxDeg);

        /// <summary>Bank roll (deg) into a turn from yaw rate.</summary>
        public static float BankRollDegrees(float yawRateDegPerSec, float maxDeg) =>
            Mathf.Clamp(yawRateDegPerSec * YawToRoll, -maxDeg, maxDeg);
    }
}
