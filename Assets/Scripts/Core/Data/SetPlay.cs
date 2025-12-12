using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents a designed set play with player positions, movements, and actions.
    /// Used for the visual play designer and in-game play calling.
    /// </summary>
    [Serializable]
    public class SetPlay
    {
        // ==================== IDENTITY ====================
        public string PlayId;
        public string PlayName;
        public string Description;
        public string CreatedBy;                                // "User", "Template", "AI"
        public DateTime CreatedDate;

        // ==================== CLASSIFICATION ====================
        public PlayCategory Category;
        public PlaySituation Situation;
        public List<PlayTag> Tags = new List<PlayTag>();

        // ==================== FORMATION ====================
        public StartingFormation Formation = StartingFormation.Horns;

        // ==================== PLAYER ASSIGNMENTS ====================
        /// <summary>
        /// Which position/role runs each spot in the play.
        /// Key is spot number (1-5), value is role.
        /// </summary>
        public Dictionary<int, PlayRole> RoleAssignments = new Dictionary<int, PlayRole>
        {
            { 1, PlayRole.PointGuard },
            { 2, PlayRole.ShootingGuard },
            { 3, PlayRole.SmallForward },
            { 4, PlayRole.PowerForward },
            { 5, PlayRole.Center }
        };

        // ==================== POSITIONS ====================
        /// <summary>
        /// Starting positions for each player (court coordinates).
        /// Key is spot number (1-5).
        /// Court is 94x50 feet, half court is 47x50.
        /// </summary>
        public Dictionary<int, Vector2> StartingPositions = new Dictionary<int, Vector2>();

        // ==================== ACTIONS ====================
        /// <summary>
        /// Sequence of actions in the play.
        /// </summary>
        public List<PlayAction> Actions = new List<PlayAction>();

        // ==================== OPTIONS ====================
        /// <summary>
        /// Alternative reads/options within the play.
        /// </summary>
        public List<PlayOption> Options = new List<PlayOption>();

        // ==================== PLAY EXECUTION ====================
        [Range(1, 24)] public int IdealShotClock = 14;          // Best time to execute
        public float EstimatedDuration = 8f;                     // Seconds to run
        public int Complexity = 1;                               // 1-5, affects learning time

        // ==================== REQUIREMENTS ====================
        public List<PlayRequirement> Requirements = new List<PlayRequirement>();

        // ==================== SUCCESS METRICS ====================
        public int TimesRun = 0;
        public int TimesSuccessful = 0;                         // Resulted in good shot/score
        public float SuccessRate => TimesRun > 0 ? (float)TimesSuccessful / TimesRun : 0f;

        // ==================== COMPUTED PROPERTIES ====================

        public bool IsAfterTimeout => Situation == PlaySituation.AfterTimeout;
        public bool IsEndOfGame => Situation == PlaySituation.LastShot ||
                                   Situation == PlaySituation.NeedThree ||
                                   Situation == PlaySituation.NeedTwo;

        public PlayAction PrimaryAction => Actions.FirstOrDefault();
        public int TotalActions => Actions.Count;

        // ==================== FACTORY METHODS ====================

        public static SetPlay CreateEmpty(string name)
        {
            return new SetPlay
            {
                PlayId = $"PLAY_{Guid.NewGuid().ToString().Substring(0, 8)}",
                PlayName = name,
                CreatedBy = "User",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.HalfCourt,
                Situation = PlaySituation.General
            };
        }

        /// <summary>
        /// Creates a basic pick and roll play.
        /// </summary>
        public static SetPlay CreatePickAndRoll(string ballHandler = "Point Guard", string screener = "Center")
        {
            var play = new SetPlay
            {
                PlayId = "PLAY_PNR_BASIC",
                PlayName = "Basic Pick and Roll",
                Description = "Standard pick and roll action from the top of the key.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.HalfCourt,
                Situation = PlaySituation.General,
                Formation = StartingFormation.Horns,
                Tags = new List<PlayTag> { PlayTag.PickAndRoll, PlayTag.HighPercentage },
                Complexity = 1,
                IdealShotClock = 16
            };

            // Starting positions (half court coordinates)
            play.StartingPositions = new Dictionary<int, Vector2>
            {
                { 1, new Vector2(25, 35) },   // PG at top
                { 2, new Vector2(5, 25) },    // SG left wing
                { 3, new Vector2(45, 25) },   // SF right wing
                { 4, new Vector2(15, 15) },   // PF left elbow
                { 5, new Vector2(25, 20) }    // C near PG for screen
            };

            // Actions
            play.Actions = new List<PlayAction>
            {
                new PlayAction
                {
                    ActionId = 1,
                    ActionType = ActionType.SetScreen,
                    PrimaryPlayer = 5,          // Center screens
                    SecondaryPlayer = 1,        // For point guard
                    TargetPosition = new Vector2(25, 30),
                    Duration = 1.5f,
                    Description = "Center sets ball screen at top of key"
                },
                new PlayAction
                {
                    ActionId = 2,
                    ActionType = ActionType.DribblePenetrate,
                    PrimaryPlayer = 1,
                    TargetPosition = new Vector2(15, 18),
                    Duration = 2f,
                    Description = "Point guard uses screen, attacks downhill"
                },
                new PlayAction
                {
                    ActionId = 3,
                    ActionType = ActionType.RollToBasket,
                    PrimaryPlayer = 5,
                    TargetPosition = new Vector2(25, 5),
                    Duration = 2f,
                    Description = "Center rolls to basket"
                }
            };

            // Options
            play.Options = new List<PlayOption>
            {
                new PlayOption
                {
                    OptionId = 1,
                    Name = "Ball Handler Pull-Up",
                    Trigger = "If defender goes under screen",
                    TargetPlayer = 1,
                    FinishType = FinishType.PullUpJumper,
                    Priority = 1
                },
                new PlayOption
                {
                    OptionId = 2,
                    Name = "Lob to Roll Man",
                    Trigger = "If big's man helps on ball handler",
                    TargetPlayer = 5,
                    FinishType = FinishType.LobFinish,
                    Priority = 2
                },
                new PlayOption
                {
                    OptionId = 3,
                    Name = "Kick to Corner",
                    Trigger = "If help comes from corner",
                    TargetPlayer = 2,
                    FinishType = FinishType.CatchAndShootThree,
                    Priority = 3
                }
            };

            return play;
        }

        /// <summary>
        /// Creates an isolation play.
        /// </summary>
        public static SetPlay CreateIsolation(string isoPlayer = "Best Scorer")
        {
            var play = new SetPlay
            {
                PlayId = "PLAY_ISO_WING",
                PlayName = "Wing Isolation",
                Description = "Clear out for wing isolation.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.HalfCourt,
                Situation = PlaySituation.General,
                Formation = StartingFormation.FiveOut,
                Tags = new List<PlayTag> { PlayTag.Isolation, PlayTag.StarPlayer },
                Complexity = 1,
                IdealShotClock = 14
            };

            play.StartingPositions = new Dictionary<int, Vector2>
            {
                { 1, new Vector2(5, 35) },    // PG weak side corner
                { 2, new Vector2(45, 35) },   // SG strong corner
                { 3, new Vector2(35, 28) },   // SF iso wing
                { 4, new Vector2(5, 20) },    // PF weak side
                { 5, new Vector2(15, 5) }     // C weak side post
            };

            play.Actions = new List<PlayAction>
            {
                new PlayAction
                {
                    ActionId = 1,
                    ActionType = ActionType.ClearOut,
                    PrimaryPlayer = 4,
                    SecondaryPlayer = 5,
                    Description = "Clear weak side for iso space"
                },
                new PlayAction
                {
                    ActionId = 2,
                    ActionType = ActionType.Isolation,
                    PrimaryPlayer = 3,
                    TargetPosition = new Vector2(35, 15),
                    Duration = 4f,
                    Description = "Wing player attacks one-on-one"
                }
            };

            play.Options = new List<PlayOption>
            {
                new PlayOption
                {
                    OptionId = 1,
                    Name = "Drive and Finish",
                    Trigger = "Beat defender off dribble",
                    TargetPlayer = 3,
                    FinishType = FinishType.DriveLayup,
                    Priority = 1
                },
                new PlayOption
                {
                    OptionId = 2,
                    Name = "Step Back Three",
                    Trigger = "Defender plays close",
                    TargetPlayer = 3,
                    FinishType = FinishType.StepBackThree,
                    Priority = 2
                },
                new PlayOption
                {
                    OptionId = 3,
                    Name = "Dump to Big",
                    Trigger = "Help defender comes",
                    TargetPlayer = 5,
                    FinishType = FinishType.PostFinish,
                    Priority = 3
                }
            };

            return play;
        }

        /// <summary>
        /// Creates an after-timeout (ATO) play.
        /// </summary>
        public static SetPlay CreateATOPlay()
        {
            var play = new SetPlay
            {
                PlayId = "PLAY_ATO_HORNS",
                PlayName = "Horns Flare ATO",
                Description = "After-timeout play from horns with flare screen option.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.AfterTimeout,
                Situation = PlaySituation.AfterTimeout,
                Formation = StartingFormation.Horns,
                Tags = new List<PlayTag> { PlayTag.AfterTimeout, PlayTag.HighPercentage, PlayTag.ThreePointer },
                Complexity = 3,
                IdealShotClock = 18
            };

            play.StartingPositions = new Dictionary<int, Vector2>
            {
                { 1, new Vector2(25, 38) },   // PG top
                { 2, new Vector2(5, 15) },    // SG corner
                { 3, new Vector2(45, 15) },   // SF corner
                { 4, new Vector2(15, 25) },   // PF left elbow
                { 5, new Vector2(35, 25) }    // C right elbow
            };

            play.Actions = new List<PlayAction>
            {
                new PlayAction
                {
                    ActionId = 1,
                    ActionType = ActionType.FlareScreen,
                    PrimaryPlayer = 5,
                    SecondaryPlayer = 3,
                    TargetPosition = new Vector2(40, 32),
                    Duration = 2f,
                    Description = "Center sets flare screen for SF"
                },
                new PlayAction
                {
                    ActionId = 2,
                    ActionType = ActionType.CutToPosition,
                    PrimaryPlayer = 3,
                    TargetPosition = new Vector2(42, 35),
                    Duration = 1.5f,
                    Description = "SF uses flare for three-point shot"
                },
                new PlayAction
                {
                    ActionId = 3,
                    ActionType = ActionType.Pass,
                    PrimaryPlayer = 1,
                    SecondaryPlayer = 3,
                    Duration = 1f,
                    Description = "Pass to open shooter"
                }
            };

            play.Options = new List<PlayOption>
            {
                new PlayOption
                {
                    OptionId = 1,
                    Name = "Flare Three",
                    Trigger = "SF open off flare",
                    TargetPlayer = 3,
                    FinishType = FinishType.CatchAndShootThree,
                    Priority = 1
                },
                new PlayOption
                {
                    OptionId = 2,
                    Name = "Screener Pop",
                    Trigger = "Help on SF leaves C open",
                    TargetPlayer = 5,
                    FinishType = FinishType.CatchAndShootMid,
                    Priority = 2
                },
                new PlayOption
                {
                    OptionId = 3,
                    Name = "Ball Screen",
                    Trigger = "Nothing open initially",
                    TargetPlayer = 1,
                    FinishType = FinishType.PullUpJumper,
                    Priority = 3
                }
            };

            return play;
        }

        /// <summary>
        /// Creates a last-shot play.
        /// </summary>
        public static SetPlay CreateLastShotPlay()
        {
            var play = new SetPlay
            {
                PlayId = "PLAY_LAST_SHOT",
                PlayName = "Last Shot Iso",
                Description = "End of game/quarter last shot play.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.Situational,
                Situation = PlaySituation.LastShot,
                Formation = StartingFormation.FiveOut,
                Tags = new List<PlayTag> { PlayTag.Isolation, PlayTag.Clutch, PlayTag.StarPlayer },
                Complexity = 2,
                IdealShotClock = 8
            };

            play.Requirements = new List<PlayRequirement>
            {
                new PlayRequirement { Type = RequirementType.ShotClock, MinValue = 5, MaxValue = 12 },
                new PlayRequirement { Type = RequirementType.HasCloser, Description = "Needs clutch scorer" }
            };

            return play;
        }

        /// <summary>
        /// Creates a sideline out of bounds (SLOB) play.
        /// </summary>
        public static SetPlay CreateSLOBPlay()
        {
            var play = new SetPlay
            {
                PlayId = "PLAY_SLOB_STACK",
                PlayName = "Sideline Stack",
                Description = "Sideline out of bounds with stack formation.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.InboundSideline,
                Situation = PlaySituation.SidelineInbound,
                Formation = StartingFormation.Stack,
                Tags = new List<PlayTag> { PlayTag.Inbound, PlayTag.QuickHitter },
                Complexity = 2,
                EstimatedDuration = 5f
            };

            return play;
        }

        /// <summary>
        /// Creates a baseline out of bounds (BLOB) play.
        /// </summary>
        public static SetPlay CreateBLOBPlay()
        {
            var play = new SetPlay
            {
                PlayId = "PLAY_BLOB_BOX",
                PlayName = "Baseline Box",
                Description = "Baseline out of bounds from box formation.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.InboundBaseline,
                Situation = PlaySituation.BaselineInbound,
                Formation = StartingFormation.Box,
                Tags = new List<PlayTag> { PlayTag.Inbound, PlayTag.QuickHitter },
                Complexity = 2,
                EstimatedDuration = 5f
            };

            return play;
        }

        /// <summary>
        /// Gets all template plays.
        /// </summary>
        public static List<SetPlay> GetAllTemplates()
        {
            return new List<SetPlay>
            {
                CreatePickAndRoll(),
                CreateIsolation(),
                CreateATOPlay(),
                CreateLastShotPlay(),
                CreateSLOBPlay(),
                CreateBLOBPlay(),
                CreateFlexOffense(),
                CreatePrincetonBackdoor(),
                CreateFloppy(),
                CreateSpainPickAndRoll()
            };
        }

        private static SetPlay CreateFlexOffense()
        {
            return new SetPlay
            {
                PlayId = "PLAY_FLEX",
                PlayName = "Flex Cut",
                Description = "Classic flex offense action with baseline screens.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.HalfCourt,
                Situation = PlaySituation.General,
                Formation = StartingFormation.Flex,
                Tags = new List<PlayTag> { PlayTag.Continuity, PlayTag.Cutting },
                Complexity = 3
            };
        }

        private static SetPlay CreatePrincetonBackdoor()
        {
            return new SetPlay
            {
                PlayId = "PLAY_PRINCETON",
                PlayName = "Princeton Backdoor",
                Description = "Princeton-style backdoor cut with high post entry.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.HalfCourt,
                Situation = PlaySituation.General,
                Formation = StartingFormation.Princeton,
                Tags = new List<PlayTag> { PlayTag.Cutting, PlayTag.HighIQ },
                Complexity = 4
            };
        }

        private static SetPlay CreateFloppy()
        {
            return new SetPlay
            {
                PlayId = "PLAY_FLOPPY",
                PlayName = "Floppy",
                Description = "Floppy action to get shooter open off staggered screens.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.HalfCourt,
                Situation = PlaySituation.General,
                Formation = StartingFormation.Floppy,
                Tags = new List<PlayTag> { PlayTag.ThreePointer, PlayTag.Screens },
                Complexity = 2
            };
        }

        private static SetPlay CreateSpainPickAndRoll()
        {
            return new SetPlay
            {
                PlayId = "PLAY_SPAIN_PNR",
                PlayName = "Spain Pick and Roll",
                Description = "Pick and roll with back screen on roller's defender.",
                CreatedBy = "Template",
                CreatedDate = DateTime.Now,
                Category = PlayCategory.HalfCourt,
                Situation = PlaySituation.General,
                Tags = new List<PlayTag> { PlayTag.PickAndRoll, PlayTag.HighIQ, PlayTag.Lob },
                Complexity = 4
            };
        }
    }

    // ==================== PLAY ACTIONS ====================

    [Serializable]
    public class PlayAction
    {
        public int ActionId;
        public ActionType ActionType;
        public int PrimaryPlayer;                               // Spot number (1-5)
        public int SecondaryPlayer;                             // For passes/screens
        public Vector2 TargetPosition;                          // Where to move
        public Vector2 StartPosition;                           // Starting point
        public float Duration = 2f;                             // Seconds for action
        public string Description;
        public bool IsOptional = false;

        // For screen actions
        public ScreenAngle ScreenAngle = ScreenAngle.Straight;

        // For cuts
        public CutType CutType = CutType.BasketCut;
    }

    public enum ActionType
    {
        // Ball movement
        Pass,
        DribbleMove,
        DribblePenetrate,

        // Screens
        SetScreen,
        FlareScreen,
        BackScreen,
        DownScreen,
        CrossScreen,
        PinDown,

        // Cuts
        CutToBasket,
        CutToPosition,
        BackdoorCut,
        FlashToPost,

        // Post play
        PostUp,
        SealDefender,

        // Roll actions
        RollToBasket,
        PopForShot,
        SlipScreen,

        // Spacing
        SpaceToCorner,
        SpaceToWing,
        ClearOut,

        // Finishes
        Isolation,
        Shoot,

        // Special
        HandOff,
        OffBallScreen
    }

    public enum ScreenAngle
    {
        Straight,
        Angled,
        Flat,
        High
    }

    public enum CutType
    {
        BasketCut,
        FlareOut,
        Curl,
        Straight,
        Backdoor
    }

    // ==================== PLAY OPTIONS ====================

    [Serializable]
    public class PlayOption
    {
        public int OptionId;
        public string Name;
        public string Trigger;                                  // When to use this option
        public int TargetPlayer;                                // Who finishes
        public FinishType FinishType;
        public int Priority;                                    // 1 = first look
        public string Description;

        // Option-specific actions
        public List<PlayAction> Actions = new List<PlayAction>();
    }

    public enum FinishType
    {
        // Layups/dunks
        DriveLayup,
        Floater,
        LobFinish,
        PostFinish,

        // Mid-range
        PullUpJumper,
        CatchAndShootMid,
        FadeAway,

        // Three-pointers
        CatchAndShootThree,
        PullUpThree,
        StepBackThree,

        // Other
        FreeThrows,
        ResetPlay
    }

    // ==================== PLAY REQUIREMENTS ====================

    [Serializable]
    public class PlayRequirement
    {
        public RequirementType Type;
        public int MinValue;
        public int MaxValue;
        public string Description;
        public PlayerAttribute? RequiredAttribute;
        public int MinAttributeValue;
    }

    public enum RequirementType
    {
        ShotClock,                  // Specific shot clock range
        ScoreDifferential,          // When up/down by X
        Quarter,                    // Specific quarter
        HasShooter,                 // Needs good 3pt shooter
        HasCloser,                  // Needs clutch player
        HasPostPlayer,              // Needs post scorer
        HasBallHandler,             // Needs elite handler
        MatchupAdvantage            // Mismatch required
    }

    // ==================== FORMATIONS ====================

    public enum StartingFormation
    {
        // Standard formations
        FiveOut,                    // All perimeter
        Horns,                      // Two bigs at elbows
        Stack,                      // Line up on one side
        Box,                        // Box shape

        // Specialty formations
        Flex,                       // Flex offense base
        Princeton,                  // High post, 4-out
        Floppy,                     // Shooter on baseline
        Diamond,                    // Diamond shape
        Triangle,                   // Triangle offense
        Spread,                     // Spread floor
        HighLow,                    // High post + low post
        MotionStrong,               // Motion offense strong side
        MotionWeak                  // Motion offense weak side
    }

    // ==================== PLAY METADATA ====================

    public enum PlayCategory
    {
        HalfCourt,                  // Standard half-court offense
        Transition,                 // Fast break plays
        AfterTimeout,               // ATO plays
        InboundSideline,            // SLOB plays
        InboundBaseline,            // BLOB plays
        Situational,                // End of game/quarter
        Zone,                       // Zone offense
        Press,                      // Press break
        DelayGame                   // Stall/kill clock
    }

    public enum PlaySituation
    {
        General,                    // Any time
        AfterTimeout,               // ATO
        LastShot,                   // End of quarter/game
        NeedThree,                  // Need 3 to tie/win
        NeedTwo,                    // Need 2 to tie/win
        SidelineInbound,            // SLOB
        BaselineInbound,            // BLOB
        VersusZone,                 // Against zone defense
        PressBreaker,               // Breaking full court press
        UpBig,                      // Protecting lead
        DownBig,                    // Need quick points
        CloseGame                   // Tight game
    }

    public enum PlayTag
    {
        // Action tags
        PickAndRoll,
        Isolation,
        Cutting,
        Screening,
        Screens,
        Posting,

        // Shot type tags
        ThreePointer,
        MidRange,
        RimAttack,
        Lob,

        // Situation tags
        Inbound,
        Clutch,
        QuickHitter,
        Continuity,
        AfterTimeout,

        // Quality tags
        HighPercentage,
        StarPlayer,
        TeamBall,
        HighIQ,

        // Personnel tags
        SmallBall,
        TwoBig,
        ThreeGuard
    }

    // ==================== PLAY ROLE ====================

    public enum PlayRole
    {
        PointGuard,
        ShootingGuard,
        SmallForward,
        PowerForward,
        Center,
        BallHandler,
        Screener,
        Shooter,
        Cutter,
        Iso,
        PostPlayer
    }
}
