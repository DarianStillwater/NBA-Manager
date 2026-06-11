using NBAHeadCoach.Core;
using NBAHeadCoach.UI.Match.Playback;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Tests the pure playback decision logic for the FM-style match viewing modes.
    /// </summary>
    public class PlaybackDeciderTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var empty = Packet();                                       // routine empty possession
            var scoreOnly = Packet(anyScore: true);                     // quiet two-pointer
            var defOnly = Packet(anyDefensiveHighlight: true);          // steal/block, no flag
            var highlight = Packet(anyHighlight: true);                 // dunk/three/clutch
            var scoreAndHighlight = Packet(anyScore: true, anyHighlight: true);

            // Full match plays everything
            Assert(PlaybackDecider.ShouldPlay(empty, ViewingMode.FullMatch, SimulationSpeed.Normal),
                "Full match plays empty possession");
            Assert(PlaybackDecider.ShouldPlay(highlight, ViewingMode.FullMatch, SimulationSpeed.Slow),
                "Full match plays highlight");

            // Key highlights: only flagged highlights
            Assert(PlaybackDecider.ShouldPlay(highlight, ViewingMode.KeyHighlights, SimulationSpeed.Normal),
                "Key plays highlight");
            Assert(!PlaybackDecider.ShouldPlay(scoreOnly, ViewingMode.KeyHighlights, SimulationSpeed.Normal),
                "Key skips quiet score");
            Assert(!PlaybackDecider.ShouldPlay(empty, ViewingMode.KeyHighlights, SimulationSpeed.Normal),
                "Key skips empty possession");
            Assert(!PlaybackDecider.ShouldPlay(defOnly, ViewingMode.KeyHighlights, SimulationSpeed.Normal),
                "Key skips unflagged defensive play");

            // Extended highlights: scores, defensive plays, and highlights
            Assert(PlaybackDecider.ShouldPlay(scoreOnly, ViewingMode.ExtendedHighlights, SimulationSpeed.Normal),
                "Extended plays quiet score");
            Assert(PlaybackDecider.ShouldPlay(defOnly, ViewingMode.ExtendedHighlights, SimulationSpeed.Normal),
                "Extended plays defensive highlight");
            Assert(PlaybackDecider.ShouldPlay(scoreAndHighlight, ViewingMode.ExtendedHighlights, SimulationSpeed.Normal),
                "Extended plays score+highlight");
            Assert(!PlaybackDecider.ShouldPlay(empty, ViewingMode.ExtendedHighlights, SimulationSpeed.Normal),
                "Extended skips empty possession");

            // Instant speed skips everything in every mode
            Assert(!PlaybackDecider.ShouldPlay(highlight, ViewingMode.FullMatch, SimulationSpeed.Instant),
                "Instant skips even in Full match");
            Assert(!PlaybackDecider.ShouldPlay(highlight, ViewingMode.KeyHighlights, SimulationSpeed.Instant),
                "Instant skips highlights");

            // Null safety
            Assert(!PlaybackDecider.ShouldPlay(null, ViewingMode.FullMatch, SimulationSpeed.Normal),
                "Null packet never plays");

            // Speed multiplier mapping
            AssertEqual(1f, PlaybackDecider.SpeedMultiplier(SimulationSpeed.Slow), "Slow = 1x");
            AssertEqual(2f, PlaybackDecider.SpeedMultiplier(SimulationSpeed.Normal), "Normal = 2x");
            AssertEqual(4f, PlaybackDecider.SpeedMultiplier(SimulationSpeed.Fast), "Fast = 4x");
            AssertEqual(8f, PlaybackDecider.SpeedMultiplier(SimulationSpeed.VeryFast), "VeryFast = 8x");

            // Skip duration clamps
            AssertEqual(0.5f, PlaybackDecider.SkipWallSeconds(SimulationSpeed.Slow), "Skip at 1x = 0.5s");
            AssertRange(PlaybackDecider.SkipWallSeconds(SimulationSpeed.VeryFast), 0.06f, 0.0625f, "Skip at 8x clamped low");
            AssertEqual(0.06f, PlaybackDecider.SkipWallSeconds(SimulationSpeed.Instant), "Skip at Instant = floor");

            // Event offset math
            AssertEqual(3f, PlaybackDecider.EventOffset(700f, 697f, 15f), "Offset = start - event clock");
            AssertEqual(0f, PlaybackDecider.EventOffset(700f, 701f, 15f), "Future event clamps to 0");
            AssertEqual(15f, PlaybackDecider.EventOffset(700f, 600f, 15f), "Past-duration event clamps to max");

            // Packet duration math
            var p = Packet(); p.StartGameClock = 700f; p.EndGameClock = 686f; p.TailSeconds = 3f;
            AssertEqual(14f, p.DurationGameSeconds, "DurationGameSeconds");
            AssertEqual(17f, p.TotalPlaybackSeconds, "TotalPlaybackSeconds includes FT tail");

            return (_passed, _failed);
        }

        private static PossessionPlaybackPacket Packet(
            bool anyHighlight = false, bool anyScore = false, bool anyDefensiveHighlight = false)
        {
            return new PossessionPlaybackPacket
            {
                StartGameClock = 700f,
                EndGameClock = 685f,
                Quarter = 1,
                AnyHighlight = anyHighlight,
                AnyScore = anyScore,
                AnyDefensiveHighlight = anyDefensiveHighlight
            };
        }
    }
}
