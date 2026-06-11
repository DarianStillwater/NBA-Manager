using System;
using NBAHeadCoach.Core;

namespace NBAHeadCoach.UI.Match.Playback
{
    /// <summary>
    /// FM-style viewing modes for the interactive match.
    /// </summary>
    public enum ViewingMode
    {
        KeyHighlights,       // only flagged highlight possessions play out
        ExtendedHighlights,  // all scoring plays + defensive highlights play out
        FullMatch            // every possession plays out
    }

    /// <summary>
    /// Pure decision logic for the playback director: should a possession be
    /// PLAYED on the court or SKIPPED (fast-forward)? Kept free of UnityEngine
    /// types so it's unit-testable in the custom TestRunner.
    /// </summary>
    public static class PlaybackDecider
    {
        /// <summary>
        /// Decide whether to visually play a possession.
        /// Instant speed always skips (race to decision points).
        /// </summary>
        public static bool ShouldPlay(PossessionPlaybackPacket packet, ViewingMode mode, SimulationSpeed speed)
        {
            if (packet == null) return false;
            if (speed == SimulationSpeed.Instant) return false;

            switch (mode)
            {
                case ViewingMode.FullMatch:
                    return true;
                case ViewingMode.ExtendedHighlights:
                    return packet.AnyScore || packet.AnyHighlight || packet.AnyDefensiveHighlight;
                case ViewingMode.KeyHighlights:
                    return packet.AnyHighlight;
                default:
                    return true;
            }
        }

        /// <summary>Playback rate for shown possessions (game seconds per wall second).</summary>
        public static float SpeedMultiplier(SimulationSpeed speed)
        {
            switch (speed)
            {
                case SimulationSpeed.Slow: return 1f;
                case SimulationSpeed.Normal: return 2f;
                case SimulationSpeed.Fast: return 4f;
                case SimulationSpeed.VeryFast: return 8f;
                case SimulationSpeed.Instant: return 64f;
                default: return 2f;
            }
        }

        /// <summary>Wall-clock duration of a SKIP (clock-spin window).</summary>
        public static float SkipWallSeconds(SimulationSpeed speed)
        {
            float t = 0.5f / SpeedMultiplier(speed);
            return Math.Max(0.06f, Math.Min(0.5f, t));
        }

        /// <summary>
        /// Playback offset (seconds from possession start) for an event at the given game clock.
        /// </summary>
        public static float EventOffset(float startGameClock, float eventGameClock, float maxDuration)
        {
            float offset = startGameClock - eventGameClock;
            if (offset < 0f) offset = 0f;
            if (offset > maxDuration) offset = maxDuration;
            return offset;
        }
    }
}
