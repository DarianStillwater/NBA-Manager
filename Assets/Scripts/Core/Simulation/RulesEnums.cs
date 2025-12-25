using System;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Types of fouls that can occur during a game.
    /// </summary>
    public enum FoulType
    {
        /// <summary>Standard defensive foul - not on shooting motion</summary>
        Personal,

        /// <summary>Foul during shooting motion - 2 or 3 FTs</summary>
        Shooting,

        /// <summary>Foul away from the ball</summary>
        Loose,

        /// <summary>Offensive foul (charge, illegal screen) - turnover</summary>
        Offensive,

        /// <summary>Unsportsmanlike conduct - 1 FT, retain possession</summary>
        Technical,

        /// <summary>Unnecessary contact - 2 FTs, retain possession</summary>
        Flagrant1,

        /// <summary>Unnecessary and excessive - 2 FTs, retain possession, ejection</summary>
        Flagrant2
    }

    /// <summary>
    /// Types of violations that result in turnovers.
    /// </summary>
    public enum ViolationType
    {
        /// <summary>Illegal footwork while holding the ball</summary>
        Traveling,

        /// <summary>Ball returned to backcourt after crossing half</summary>
        Backcourt,

        /// <summary>Offensive player in lane for 3+ seconds</summary>
        ThreeSecond,

        /// <summary>Failed to shoot within 24 seconds</summary>
        ShotClock,

        /// <summary>Ball went out of bounds off offensive player</summary>
        OutOfBounds,

        /// <summary>Double dribble</summary>
        DoubleDribble
    }

    /// <summary>
    /// Scenarios that determine how many free throws are awarded.
    /// </summary>
    public enum FreeThrowScenario
    {
        /// <summary>No free throws awarded</summary>
        None,

        /// <summary>Made shot + shooting foul = 1 FT</summary>
        AndOne,

        /// <summary>Shooting foul on 2-point attempt = 2 FTs</summary>
        TwoShots,

        /// <summary>Shooting foul on 3-point attempt = 3 FTs</summary>
        ThreeShots,

        /// <summary>Non-shooting foul when team is in bonus = 2 FTs</summary>
        Bonus,

        /// <summary>Technical foul = 1 FT, retain possession</summary>
        Technical,

        /// <summary>Flagrant foul = 2 FTs, retain possession</summary>
        Flagrant
    }

    /// <summary>
    /// Reasons for calling a timeout.
    /// </summary>
    public enum TimeoutReason
    {
        /// <summary>Coach decision - general</summary>
        Coach,

        /// <summary>Stop opponent scoring run</summary>
        StopRun,

        /// <summary>Rest fatigued players</summary>
        RestPlayers,

        /// <summary>Draw up a specific play</summary>
        DrawUpPlay,

        /// <summary>End of game/half management</summary>
        EndOfGame,

        /// <summary>Required TV timeout (not used currently)</summary>
        Mandatory,

        /// <summary>Advance ball to frontcourt (last 2 minutes)</summary>
        AdvanceBall,

        /// <summary>Ice opponent shooter before free throws</summary>
        IcingShooter
    }

    /// <summary>
    /// Priority levels for AI timeout decisions.
    /// Higher priority = more likely to call timeout.
    /// </summary>
    public enum TimeoutPriority
    {
        None = 0,
        Low = 25,
        Medium = 50,
        High = 75,
        Critical = 100
    }

    /// <summary>
    /// Result of a free throw attempt.
    /// </summary>
    [Serializable]
    public class FreeThrowResult
    {
        public int Attempts;
        public int Made;
        public string ShooterId;
        public string PlayByPlayText;
        public bool WasIced;  // Timeout called before FTs

        public int Missed => Attempts - Made;
        public float Percentage => Attempts > 0 ? (float)Made / Attempts : 0f;
    }

    /// <summary>
    /// Context for game situations affecting foul/timeout decisions.
    /// </summary>
    [Serializable]
    public class GameContext
    {
        public int Quarter;
        public float GameClock;
        public float ShotClock;
        public int ScoreDifferential;  // Positive = offense leading
        public bool IsClutchTime;      // Q4/OT, < 5 min, within 5 pts
        public bool TimeoutJustCalled; // For "icing" shooter
        public int OpponentRunPoints;  // Points in current run
        public bool OpponentJustScored;

        public bool IsLastTwoMinutes => GameClock <= 120f && Quarter >= 4;
        public bool IsEndOfHalf => (Quarter == 2 || Quarter >= 4) && GameClock <= 120f;
    }

    /// <summary>
    /// Result of a timeout decision evaluation.
    /// </summary>
    [Serializable]
    public class TimeoutDecision
    {
        public bool ShouldCall;
        public TimeoutReason Reason;
        public TimeoutPriority Priority;
        public string Description;
    }

    /// <summary>
    /// Detailed foul event information.
    /// </summary>
    [Serializable]
    public class FoulEventDetail
    {
        public FoulType FoulType;
        public string FoulerId;         // Player who committed foul
        public string FouledPlayerId;   // Player who was fouled
        public int FreeThrowsAwarded;
        public FreeThrowScenario Scenario;
        public bool IsBonusSituation;
        public bool IsDoubleBonusSituation;
        public int FoulerTotalFouls;    // After this foul
        public int TeamFoulsThisQuarter;
        public bool FoulerFouledOut;    // 6th foul
        public bool FoulerEjected;      // 2nd technical or flagrant 2
    }

    /// <summary>
    /// Detailed violation event information.
    /// </summary>
    [Serializable]
    public class ViolationEventDetail
    {
        public ViolationType ViolationType;
        public string ViolatorId;
        public string Description;
    }
}
