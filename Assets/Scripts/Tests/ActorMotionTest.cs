using NBAHeadCoach.UI.Match3D;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Pure render-side motion math (Phase 1 weight/anti-skate + bridge demotion): MotionRate
    /// clamping, bridge-skip threshold, and lean-angle clamping. No play mode needed.
    /// </summary>
    public class ActorMotionTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            // ── MotionRate: measured foot speed / blended clip reference speed, clamped 0.7–1.4. ──
            // At the walk clip's authored speed (blend 0 → ref = WalkRef), rate ≈ 1.
            AssertRange(ActorMotion.MotionRate(ActorMotion.WalkRefFeetPerSec, 0f), 0.99f, 1.01f,
                "MotionRate ~1 at walk cadence");
            // At the run clip's authored speed (blend 1 → ref = RunRef), rate ≈ 1.
            AssertRange(ActorMotion.MotionRate(ActorMotion.RunRefFeetPerSec, 1f), 0.99f, 1.01f,
                "MotionRate ~1 at run cadence");
            // Far too slow for the blend → clamped up at 0.7 (never freezes mid-stride).
            AssertEqual(0.7f, ActorMotion.MotionRate(0f, 1f), "MotionRate clamps to 0.7 floor");
            // Far too fast for the blend → clamped at 1.4 (never moon-walks).
            AssertEqual(1.4f, ActorMotion.MotionRate(100f, 0f), "MotionRate clamps to 1.4 ceiling");

            // ── Bridge-skip threshold: skip when first-frame displacement is small (continuous). ──
            Assert(!ActorMotion.ShouldBridge(0f), "No bridge when players already in place");
            Assert(!ActorMotion.ShouldBridge(ActorMotion.BridgeSkipFeet - 0.5f),
                "No bridge just under threshold");
            Assert(ActorMotion.ShouldBridge(ActorMotion.BridgeSkipFeet + 0.5f),
                "Bridge when displacement exceeds threshold (sub/quarter/skip)");
            Assert(ActorMotion.ShouldBridge(40f), "Bridge on a full-court teleport");

            // ── Lean: scaled + clamped to the subtle broadcast range. ──
            AssertEqual(0f, ActorMotion.LeanPitchDegrees(0f, 8f), "No pitch at zero accel");
            AssertLessThan(ActorMotion.LeanPitchDegrees(-500f, 8f), -7.99f, "Hard decel clamps back-lean");
            AssertGreaterThan(ActorMotion.LeanPitchDegrees(500f, 8f), 7.99f, "Hard accel clamps fwd-lean");
            AssertRange(ActorMotion.BankRollDegrees(1000f, 8f), 7.99f, 8.01f, "Sharp turn clamps bank roll");
            AssertEqual(0f, ActorMotion.BankRollDegrees(0f, 8f), "No roll when not turning");

            return (_passed, _failed);
        }
    }
}
