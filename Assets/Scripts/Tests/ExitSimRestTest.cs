using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.UI.Match.Playback;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the "exit and sim the rest" control: choosing to finish flips the controller into a
    /// headless-finish mode (Instant, unpaused) and, at Instant, the playback decider animates
    /// nothing — the loop races to the buzzer. The live-state carryover is inherent: it's the same
    /// possession loop and the same box-score accumulator, not a new sim path.
    /// </summary>
    public class ExitSimRestTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            TestFinishFlagAndSpeed();
            TestFinishingSkipsAnimation();
            return (_passed, _failed);
        }

        private void TestFinishFlagAndSpeed()
        {
            var go = new GameObject("__SimRestCtl__");
            try
            {
                var sim = go.AddComponent<MatchSimulationController>();
                if (sim == null) { Assert(false, "controller constructed"); return; }

                Assert(!sim.IsFinishing, "A fresh controller is not finishing");

                sim.FinishRemainingHeadless();

                Assert(sim.IsFinishing, "Choosing SIM REST puts the controller in finishing mode");
                Assert(sim.CurrentSpeed == SimulationSpeed.Instant, "Finishing forces Instant speed");
                Assert(!sim.IsPaused, "Finishing unpauses so the loop can race to the end");
            }
            finally { Object.Destroy(go); }
        }

        private void TestFinishingSkipsAnimation()
        {
            // At Instant (which finishing forces), no possession is animated in any mode.
            var packet = new PossessionPlaybackPacket { AnyScore = true, AnyHighlight = true };
            Assert(!PlaybackDecider.ShouldPlay(packet, ViewingMode.FullMatch, SimulationSpeed.Instant),
                "Finishing (Instant) animates nothing even in FULL");
        }
    }
}
