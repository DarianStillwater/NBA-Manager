using System.Collections.Generic;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>
    /// How much spatial detail the possession simulator should produce.
    /// None  — headless sims (league auto-sim, quick sim): no SpatialStates, zero choreography cost.
    /// Full  — interactive match: dense choreographed SpatialStates for the court view.
    /// </summary>
    public enum SpatialDetailLevel
    {
        None,
        Full
    }

    /// <summary>
    /// How the possession ends, from the choreographer's perspective.
    /// </summary>
    public enum ScriptEnding
    {
        MadeShot,
        MissedShot,
        BlockedShot,
        AndOneShot,        // made shot + shooting foul
        ShootingFoulMissed, // foul, shot missed, free throws follow
        Turnover,
        Violation,
        Steal
    }

    /// <summary>
    /// Phase of the possession a spatial state belongs to (presentational hint for the view).
    /// </summary>
    public enum PossessionPhase
    {
        Advance,
        SetOffense,
        Action,
        Shot,
        Resolution,
        FreeThrow
    }

    /// <summary>
    /// Everything the choreographer needs to turn an already-DECIDED possession into a
    /// believable spatial timeline. Built by PossessionSimulator during its decision phase.
    /// Choreography is purely presentational — it must never influence outcomes or stats.
    /// </summary>
    public class PossessionScript
    {
        // Personnel (index 0-4 = PG, SG, SF, PF, C — the simulator's lineup convention)
        public Player[] Offense;
        public Player[] Defense;

        // Context
        public bool OffenseAttacksRight;     // _isOffenseHome: true → attacks +X basket
        public float StartGameClock;
        public float Duration;               // decided possession duration in game seconds
        public int Quarter;
        public TeamStrategy OffenseStrategy;
        public TeamStrategy DefenseStrategy;

        // Ball movement decisions
        public int InitialBallHandlerIndex;
        public int ShooterIndex = -1;        // -1 when the possession ends before a shot
        public bool BallHandlerPassedToShooter;

        // Decided final geometry (after RepositionShooterForZone)
        public CourtPosition ShotPosition;
        public CourtPosition[] OffenseSpots = new CourtPosition[5];
        public CourtPosition[] DefenseSpots = new CourtPosition[5];

        // Shot decisions
        public ShotType? ShotType;
        public float ContestLevel;           // 0 open … 1 smothered (matches ExecuteShot math)

        // Ending
        public ScriptEnding Ending;
        public int PointsScored;

        // Decided turnover kind (Ending == Turnover) — the choreographer stages the
        // matching visual (OOB sail / loose-ball scoop / whistle dead-ball).
        public TurnoverKind? Turnover;

        // Rebound decisions (valid for MissedShot / BlockedShot / ShootingFoulMissed last-FT-miss)
        public int RebounderIndex = -1;
        public bool RebounderIsDefense;

        // Free throws (from the foul event; presentational make/miss ordering only)
        public FreeThrowResult FreeThrows;

        // Execution variance facts (decided in the outcome layer; drawn here).
        public LapseType Lapse = LapseType.None;
        public int LapseDefenderIndex = -1;
        public OffensiveDeviation Deviation = OffensiveDeviation.None;
        public int OffDeviatorIndex = -1;

        // Presentation hints
        public bool IsFastBreak;             // short possession → faster advance phase

        // The already-built event list (Screen/Cut/PostUp/Pass beats are read from here)
        public List<PossessionEvent> Events;
    }
}
