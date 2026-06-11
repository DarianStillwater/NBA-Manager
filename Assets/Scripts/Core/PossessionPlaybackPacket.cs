using System.Collections.Generic;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// A fully-simulated possession packaged for presentation.
    /// Built by MatchSimulationController after the sim has already applied the
    /// possession's effects (score, fouls, stats); the playback director decides
    /// whether to PLAY it on the court view or SKIP it (fast-forward), then calls
    /// MatchSimulationController.CompletePresentation() to release the sim loop.
    /// </summary>
    public class PossessionPlaybackPacket
    {
        /// <summary>Dense choreographed states, GameClock descending within the possession.</summary>
        public List<SpatialState> Timeline = new List<SpatialState>();

        /// <summary>Presentation events (ticker/scoreboard/shot FX) with playback offsets.</summary>
        public List<TimedPlaybackEvent> Events = new List<TimedPlaybackEvent>();

        public float StartGameClock;
        public float EndGameClock;
        public int Quarter;
        public bool OffenseIsHome;

        // Cached decision flags for PlaybackDecider (computed once at packet build)
        public bool AnyHighlight;            // any PBP entry flagged IsHighlight
        public bool AnyScore;                // any points scored (FG or FT)
        public bool AnyDefensiveHighlight;   // block or steal

        /// <summary>Extra presentation time appended past the live timeline (free throws etc.).</summary>
        public float TailSeconds;

        public float DurationGameSeconds => StartGameClock - EndGameClock;
        public float TotalPlaybackSeconds => DurationGameSeconds + TailSeconds;
    }

    /// <summary>
    /// A single presentation side-effect scheduled at an offset (seconds from possession start).
    /// Exactly one payload field is populated per event.
    /// </summary>
    public class TimedPlaybackEvent
    {
        /// <summary>Seconds from possession start in game time (StartGameClock − evt.GameClock), clamped ≥ 0.
        /// Free-throw events sit past DurationGameSeconds in the tail.</summary>
        public float Offset;

        public PlayByPlayEntry Entry;        // ticker row (nullable)
        public ScoreboardUpdate Scoreboard;  // scoreboard snapshot AFTER this event (nullable)
        public ShotMarkerData? ShotMarker;   // shot marker + hoop FX trigger
        public bool ShotMade;
        public int ShotPoints;
    }
}
