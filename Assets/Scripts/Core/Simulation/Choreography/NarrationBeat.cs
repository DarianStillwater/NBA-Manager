using System.Collections.Generic;

namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>What kind of on-court moment a narration beat describes.</summary>
    public enum NarrationBeatKind
    {
        BringUp,
        SwingPass,
        ScreenSet,
        Roll,
        Curl,
        BackdoorCut,
        PostMove,
        PostFeed,
        KickOut,
        Handoff,
        IsoJab,
        Drive,
        AssistPass,
        ShotWindup,
        RimResult,
        ReboundScramble,
        StealJump,
        OobTurnover,
        Violation,
        FreeThrowSetup,
        FreeThrowAttempt
    }

    /// <summary>
    /// A timed moment in the choreographed possession, recorded by the choreographer as it
    /// lays waypoints and consumed by the radio narration bar (and to re-time the ticker's
    /// existing lines to when things actually happen on screen). Purely presentational —
    /// beats are NOT PossessionEvents and never touch outcomes or stats.
    /// </summary>
    public sealed class NarrationBeat
    {
        /// <summary>Seconds from possession start — equals the playback offset.</summary>
        public float T;
        public NarrationBeatKind Kind;

        /// <summary>Offense lineup index 0-4 (-1 = n/a). ActorIsDefense flips the roster.</summary>
        public int ActorIndex = -1;
        public int TargetIndex = -1;
        public bool ActorIsDefense;

        public ShotType? ShotType;
        public OffensiveAction Action;
        public float ContestLevel;
        public bool Made;
        public bool IsThree;
    }
}
