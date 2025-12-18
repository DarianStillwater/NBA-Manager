using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Comprehensive team strategy configuration.
    /// Covers all offensive systems, defensive systems, and strategic preferences.
    /// NBA-level tactical depth with granular control.
    /// </summary>
    [Serializable]
    public class TeamStrategy
    {
        // ==================== IDENTITY ====================
        public string TeamId;
        public string StrategyName;
        public string Description;

        // ==================== OFFENSIVE SYSTEM ====================
        public OffensiveSystem OffensiveSystem = new OffensiveSystem();

        // ==================== DEFENSIVE SYSTEM ====================
        public DefensiveSystem DefensiveSystem = new DefensiveSystem();

        // ==================== PACE & TEMPO ====================
        [Header("Pace Settings")]
        [Range(80, 110)] public float TargetPace = 100f;  // Possessions per 48 minutes
        public PacePreference PacePreference = PacePreference.Balanced;

        // ==================== SHOT SELECTION ====================
        [Header("Shot Selection")]
        [Range(0, 100)] public int ThreePointFrequency = 40;       // % of FGA that are 3s
        [Range(0, 100)] public int MidRangeFrequency = 20;         // % of FGA that are mid-range
        [Range(0, 100)] public int RimAttackFrequency = 35;        // % of FGA at the rim
        [Range(0, 100)] public int PostUpFrequency = 15;           // % of plays that are post-ups

        // ==================== TRANSITION ====================
        [Header("Transition")]
        public TransitionPreference TransitionOffense = TransitionPreference.OpportunisticPush;
        public TransitionDefensePreference TransitionDefense = TransitionDefensePreference.BalancedTransition;

        // ==================== REBOUNDING ====================
        [Header("Rebounding")]
        [Range(1, 5)] public int OffensiveReboundingFocus = 2;     // 1=Get back, 5=Crash hard
        public int SendersOnOffensiveGlass = 2;                     // Players sent to offensive boards

        // ==================== END-OF-GAME ====================
        [Header("End of Game")]
        public EndOfGameStrategy CloseGameStrategy = new EndOfGameStrategy();

        // ==================== UI COMPATIBILITY PROPERTIES ====================

        /// <summary>
        /// Pace as int (1-100) for UI compatibility. Maps to TargetPace.
        /// </summary>
        public int Pace
        {
            get => Mathf.RoundToInt((TargetPace - 80f) / 30f * 100f);  // 80-110 -> 0-100
            set => TargetPace = 80f + (value / 100f * 30f);            // 0-100 -> 80-110
        }

        /// <summary>
        /// Three point rate as float (0-1) for UI compatibility
        /// </summary>
        public float ThreePointRate
        {
            get => ThreePointFrequency / 100f;
            set => ThreePointFrequency = Mathf.RoundToInt(Mathf.Clamp01(value) * 100);
        }

        /// <summary>
        /// Pick and roll frequency - shortcut to OffensiveSystem.PickAndRollFrequency
        /// </summary>
        public int PickAndRollFrequency
        {
            get => OffensiveSystem?.PickAndRollFrequency ?? 30;
            set { if (OffensiveSystem != null) OffensiveSystem.PickAndRollFrequency = value; }
        }

        /// <summary>
        /// Defensive aggression (0-100) - maps to DefensiveSystem.OnBallPressure
        /// </summary>
        public int DefensiveAggression
        {
            get => DefensiveSystem?.OnBallPressure ?? 50;
            set { if (DefensiveSystem != null) DefensiveSystem.OnBallPressure = value; }
        }

        // ==================== FACTORY METHODS ====================

        public static TeamStrategy CreateDefault(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Balanced Approach",
                Description = "Standard balanced offensive and defensive system."
            };
        }

        /// <summary>
        /// Creates a strategy based on a real NBA team template.
        /// </summary>
        public static TeamStrategy CreateFromTemplate(string teamId, StrategyTemplate template)
        {
            return template switch
            {
                StrategyTemplate.WarriorsMotion => CreateWarriorsStyle(teamId),
                StrategyTemplate.HeatZone => CreateHeatStyle(teamId),
                StrategyTemplate.CelticsSwitching => CreateCelticsStyle(teamId),
                StrategyTemplate.SpursBallMovement => CreateSpursStyle(teamId),
                StrategyTemplate.RocketsMorball => CreateRocketsStyle(teamId),
                StrategyTemplate.MavericksDoncic => CreateMavericksStyle(teamId),
                StrategyTemplate.NuggetsJokic => CreateNuggetsStyle(teamId),
                StrategyTemplate.GrizzliesGrit => CreateGrizzliesStyle(teamId),
                StrategyTemplate.SunsSpeedball => CreateSunsStyle(teamId),
                StrategyTemplate.BucksGiannis => CreateBucksStyle(teamId),
                _ => CreateDefault(teamId)
            };
        }

        private static TeamStrategy CreateWarriorsStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Motion Heavy",
                Description = "Warriors-style motion offense with heavy off-ball movement and three-point shooting.",
                TargetPace = 103f,
                PacePreference = PacePreference.PushWhenPossible,
                ThreePointFrequency = 45,
                MidRangeFrequency = 15,
                RimAttackFrequency = 35,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.MotionOffense,
                    SecondarySystem = OffensiveSystemType.PickAndRollHeavy,
                    OffBallMovement = OffBallMovementLevel.VeryHigh,
                    ScreeningFrequency = 75,
                    CuttingFrequency = 80,
                    SpacingWidth = SpacingLevel.Wide,
                    BallHandlerCount = 3,
                    ShotClockApproach = ShotClockApproach.EarlyGoodShot
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.SwitchEverything,
                    SwitchingLevel = SwitchingLevel.SwitchAll,
                    HelpDefense = HelpDefenseLevel.ActiveHelp,
                    PickAndRollCoverage = PnRCoverage.SwitchAll
                },
                TransitionOffense = TransitionPreference.AggressivePush
            };
        }

        private static TeamStrategy CreateHeatStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Heat Culture",
                Description = "Miami Heat-style culture with zone defense, physicality, and shooting.",
                TargetPace = 96f,
                PacePreference = PacePreference.Balanced,
                ThreePointFrequency = 42,
                MidRangeFrequency = 18,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.MotionOffense,
                    HandoffFrequency = 60,
                    CuttingFrequency = 70,
                    SpacingWidth = SpacingLevel.Wide
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.Zone2_3,
                    SecondaryScheme = DefensiveSchemeType.ManToManAggressive,
                    ZoneUsage = 40,  // 40% zone
                    DefensiveIntensity = DefensiveIntensity.High,
                    HelpDefense = HelpDefenseLevel.PackedPaint
                }
            };
        }

        private static TeamStrategy CreateCelticsStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Switch Everything",
                Description = "Boston Celtics-style switching defense with versatile players.",
                TargetPace = 99f,
                ThreePointFrequency = 44,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.PickAndRollHeavy,
                    SecondarySystem = OffensiveSystemType.IsoHeavy,
                    IsolationFrequency = 25
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.SwitchEverything,
                    SwitchingLevel = SwitchingLevel.SwitchAll,
                    PickAndRollCoverage = PnRCoverage.SwitchAll,
                    DefensiveIntensity = DefensiveIntensity.High,
                    ContestingLevel = 90
                }
            };
        }

        private static TeamStrategy CreateSpursStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Beautiful Game",
                Description = "San Antonio Spurs-style ball movement with extra pass philosophy.",
                TargetPace = 94f,
                PacePreference = PacePreference.Deliberate,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.MotionOffense,
                    BallMovementPriority = 95,
                    ExtraPassPhilosophy = true,
                    ScreeningFrequency = 80,
                    CuttingFrequency = 75,
                    SpacingWidth = SpacingLevel.Normal
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.ManToManConservative,
                    PickAndRollCoverage = PnRCoverage.DropCoverage,
                    HelpDefense = HelpDefenseLevel.TeamHelp
                }
            };
        }

        private static TeamStrategy CreateRocketsStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Moreyball Analytics",
                Description = "Analytics-driven offense focusing on threes and rim attempts.",
                TargetPace = 102f,
                ThreePointFrequency = 50,
                MidRangeFrequency = 5,  // Almost no mid-range
                RimAttackFrequency = 40,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.IsoHeavy,
                    SecondarySystem = OffensiveSystemType.PickAndRollHeavy,
                    IsolationFrequency = 35,
                    SpacingWidth = SpacingLevel.ExtraWide
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.SwitchEverything
                }
            };
        }

        private static TeamStrategy CreateMavericksStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Star PnR Focus",
                Description = "Pick and roll heavy offense with elite ball handler.",
                TargetPace = 98f,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.PickAndRollHeavy,
                    PickAndRollFrequency = 60,
                    BallHandlerCount = 2,
                    SpacingWidth = SpacingLevel.Wide
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.ManToManStandard,
                    PickAndRollCoverage = PnRCoverage.DropCoverage
                }
            };
        }

        private static TeamStrategy CreateNuggetsStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Point Center Offense",
                Description = "Offense running through playmaking center.",
                TargetPace = 96f,
                PostUpFrequency = 30,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.PostUpFocused,
                    SecondarySystem = OffensiveSystemType.MotionOffense,
                    HandoffFrequency = 50,
                    CuttingFrequency = 70,
                    BallMovementPriority = 85
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.ManToManConservative,
                    PickAndRollCoverage = PnRCoverage.DropCoverage
                }
            };
        }

        private static TeamStrategy CreateGrizzliesStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Grit and Grind",
                Description = "Physical, defense-first approach with tough inside play.",
                TargetPace = 92f,
                PacePreference = PacePreference.Deliberate,
                ThreePointFrequency = 32,
                RimAttackFrequency = 45,
                OffensiveReboundingFocus = 4,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.PostUpFocused,
                    ShotClockApproach = ShotClockApproach.WorkForBestShot
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.ManToManAggressive,
                    DefensiveIntensity = DefensiveIntensity.VeryHigh,
                    ContestingLevel = 90,
                    HelpDefense = HelpDefenseLevel.PackedPaint
                }
            };
        }

        private static TeamStrategy CreateSunsStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Seven Seconds or Less",
                Description = "Fast-paced offense with quick shots and transition focus.",
                TargetPace = 108f,
                PacePreference = PacePreference.AlwaysPush,
                ThreePointFrequency = 38,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.FastBreakTransition,
                    SecondarySystem = OffensiveSystemType.PickAndRollHeavy,
                    ShotClockApproach = ShotClockApproach.EarlyGoodShot
                },
                TransitionOffense = TransitionPreference.AlwaysPush,
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.ManToManStandard
                }
            };
        }

        private static TeamStrategy CreateBucksStyle(string teamId)
        {
            return new TeamStrategy
            {
                TeamId = teamId,
                StrategyName = "Paint Beast",
                Description = "Offense built around dominant interior presence.",
                TargetPace = 99f,
                RimAttackFrequency = 50,
                ThreePointFrequency = 35,
                OffensiveSystem = new OffensiveSystem
                {
                    PrimarySystem = OffensiveSystemType.IsoHeavy,
                    SecondarySystem = OffensiveSystemType.PickAndRollHeavy,
                    IsolationFrequency = 30,
                    SpacingWidth = SpacingLevel.ExtraWide
                },
                DefensiveSystem = new DefensiveSystem
                {
                    PrimaryScheme = DefensiveSchemeType.ManToManStandard,
                    PickAndRollCoverage = PnRCoverage.DropCoverage,
                    HelpDefense = HelpDefenseLevel.PackedPaint
                }
            };
        }
    }

    // ==================== OFFENSIVE SYSTEM ====================

    [Serializable]
    public class OffensiveSystem
    {
        [Header("Primary Approach")]
        public OffensiveSystemType PrimarySystem = OffensiveSystemType.MotionOffense;
        public OffensiveSystemType SecondarySystem = OffensiveSystemType.PickAndRollHeavy;

        [Header("Ball Movement")]
        [Range(0, 100)] public int BallMovementPriority = 70;      // How much to value passing
        public bool ExtraPassPhilosophy = false;                   // Always look for better shot
        public OffBallMovementLevel OffBallMovement = OffBallMovementLevel.Moderate;

        [Header("Pick and Roll")]
        [Range(0, 100)] public int PickAndRollFrequency = 30;      // % of half-court possessions
        public PnRBallHandlerAction PnRBallHandlerAction = PnRBallHandlerAction.ReadAndReact;
        public PnRScreenerAction PnRScreenerAction = PnRScreenerAction.RollOrPop;

        [Header("Isolation")]
        [Range(0, 100)] public int IsolationFrequency = 15;        // % of half-court possessions
        public IsolationLocation PreferredIsoLocation = IsolationLocation.Wing;

        [Header("Post Play")]
        [Range(0, 100)] public int PostUpFrequency = 10;
        public PostUpStyle PostStyle = PostUpStyle.FaceUp;

        [Header("Actions")]
        [Range(0, 100)] public int ScreeningFrequency = 60;        // Off-ball screens
        [Range(0, 100)] public int CuttingFrequency = 50;          // Basket cuts
        [Range(0, 100)] public int HandoffFrequency = 30;          // Dribble handoffs

        [Header("Spacing")]
        public SpacingLevel SpacingWidth = SpacingLevel.Normal;
        public int BallHandlerCount = 2;                           // Players who can initiate offense

        [Header("Shot Clock Management")]
        public ShotClockApproach ShotClockApproach = ShotClockApproach.Balanced;
        [Range(5, 24)] public int TargetShotClockEntry = 14;       // When to look to score
    }

    public enum OffensiveSystemType
    {
        MotionOffense,          // Constant movement, read and react
        PickAndRollHeavy,       // PnR focused
        IsoHeavy,               // Star isolation focused
        PostUpFocused,          // Inside-out through post
        ThreePointOriented,     // Shoot lots of 3s
        FastBreakTransition,    // Push pace, early offense
        PrincetonOffense,       // Cutting, backdoors, patience
        TriangleOffense,        // Triangle concepts
        FlexOffense,            // Flex screens and cuts
        HornsSet,               // Start in horns formation
        FiveOut                 // All perimeter, no post
    }

    public enum OffBallMovementLevel
    {
        Minimal,                // Stand and space
        Moderate,               // Some movement
        High,                   // Active cutting/screening
        VeryHigh                // Constant motion
    }

    public enum SpacingLevel
    {
        Tight,                  // Closer to paint
        Normal,                 // Standard NBA spacing
        Wide,                   // Stretch the floor
        ExtraWide               // Maximum spacing
    }

    public enum ShotClockApproach
    {
        EarlyGoodShot,          // Take first good look
        Balanced,               // Standard
        WorkForBestShot,        // Patient, find best shot
        RunClockLate            // Late game clock management
    }

    public enum PnRBallHandlerAction
    {
        AttackRim,              // Go to rim
        PullUpMidRange,         // Stop and pop mid-range
        ThreeBall,              // Pull-up three
        PassFirst,              // Look to pass
        ReadAndReact            // Read defense
    }

    public enum PnRScreenerAction
    {
        AlwaysRoll,             // Roll to rim
        AlwaysPop,              // Pop for shot
        RollOrPop,              // Read defense
        SlipEarly               // Slip before contact
    }

    public enum IsolationLocation
    {
        Wing,                   // Wings
        TopOfKey,               // Top of key
        PostArea,               // Low block
        Elbow                   // High post/elbow
    }

    public enum PostUpStyle
    {
        BackToBasket,           // Traditional post moves
        FaceUp,                 // Face up from post
        Mixed                   // Both
    }

    // ==================== DEFENSIVE SYSTEM ====================

    [Serializable]
    public class DefensiveSystem
    {
        [Header("Primary Scheme")]
        public DefensiveSchemeType PrimaryScheme = DefensiveSchemeType.ManToManStandard;
        public DefensiveSchemeType SecondaryScheme = DefensiveSchemeType.Zone2_3;
        [Range(0, 100)] public int ZoneUsage = 0;                  // % of possessions in zone

        [Header("Man-to-Man Settings")]
        public DefensiveIntensity DefensiveIntensity = DefensiveIntensity.Normal;
        [Range(0, 100)] public int OnBallPressure = 60;            // How tight to guard ball
        [Range(0, 100)] public int ContestingLevel = 70;           // Contest shots vs stay grounded
        [Range(0, 100)] public int GamblingFrequency = 30;         // Reach for steals

        [Header("Help Defense")]
        public HelpDefenseLevel HelpDefense = HelpDefenseLevel.TeamHelp;
        public bool StuntAndRecover = true;                        // Show help and recover

        [Header("Switching")]
        public SwitchingLevel SwitchingLevel = SwitchingLevel.SwitchSome;
        public bool SwitchOneToFour = false;                       // Guards switch with forwards
        public bool SwitchOneToFive = false;                       // Guards switch with centers

        [Header("Pick and Roll Coverage")]
        public PnRCoverage PickAndRollCoverage = PnRCoverage.DropCoverage;
        public PnRCoverage SpecialPnRCoverage = PnRCoverage.Blitz; // For star ball handlers

        [Header("Closeouts")]
        public CloseoutStyle CloseoutStyle = CloseoutStyle.Contest;

        [Header("Fouling Strategy")]
        public bool IntentionalFoulPoorShooters = false;           // Hack-a-Shaq
        [Range(0, 50)] public int FreeThrowThresholdToFoul = 40;   // FT% below which to foul
    }

    public enum DefensiveSchemeType
    {
        ManToManStandard,       // Standard man-to-man
        ManToManAggressive,     // Tight, physical
        ManToManConservative,   // Sag off, protect paint
        SwitchEverything,       // Switch all screens
        Zone2_3,                // 2-3 zone
        Zone3_2,                // 3-2 zone
        Zone1_3_1,              // 1-3-1 zone
        Zone1_2_2,              // 1-2-2 zone
        BoxAndOne,              // Box zone + one man
        TriangleAndTwo,         // Triangle zone + two men
        FullCourtPress,         // Full court pressure
        HalfCourtTrap,          // Trap at half court
        MatchupZone             // Zone with man principles
    }

    public enum DefensiveIntensity
    {
        Low,                    // Conservative, no fouls
        Normal,                 // Standard
        High,                   // Active, some foul risk
        VeryHigh                // Maximum pressure
    }

    public enum HelpDefenseLevel
    {
        NoHelp,                 // Stay home on man
        LimitedHelp,            // Only help on drives
        TeamHelp,               // Standard help defense
        ActiveHelp,             // Aggressive help rotations
        PackedPaint             // Wall off the paint
    }

    public enum SwitchingLevel
    {
        NoSwitching,            // Fight through everything
        SwitchLittle,           // Only switch when necessary
        SwitchSome,             // Standard switching
        SwitchMost,             // Switch most screens
        SwitchAll               // Switch everything
    }

    public enum PnRCoverage
    {
        DropCoverage,           // Big drops back
        ShowAndRecover,         // Big shows then recovers
        Hedge,                  // Big hedges hard
        Blitz,                  // Trap ball handler
        SwitchAll,              // Switch everything
        Ice,                    // Force baseline
        UnderScreen             // Go under screen
    }

    public enum CloseoutStyle
    {
        Contest,                // Run at shooter
        StayGrounded,           // Contest but don't jump
        RunOff                  // Run shooter off line
    }

    // ==================== TRANSITION ====================

    public enum TransitionPreference
    {
        NoPush,                 // Always set up half court
        OpportunisticPush,      // Push if clear advantage
        PushWhenPossible,       // Push often
        AggressivePush,         // Always push
        AlwaysPush              // Seven seconds or less
    }

    public enum TransitionDefensePreference
    {
        SprintBack,             // Everyone sprints back
        BalancedTransition,     // Standard
        GambleForSteals,        // Take chances
        MatchupTransition       // Find your man in transition
    }

    // ==================== PACE ====================

    public enum PacePreference
    {
        Deliberate,             // Slow pace
        Balanced,               // Normal
        PushWhenPossible,       // Faster
        AlwaysPush              // Maximum pace
    }

    // ==================== END OF GAME ====================

    [Serializable]
    public class EndOfGameStrategy
    {
        [Header("Offensive Late Game")]
        public string GoToPlayerId;                                // Star to feed late
        public LateGamePlayType LateGamePlay = LateGamePlayType.BestOption;
        public bool SpreadFloor = true;
        public bool UseScreens = true;

        [Header("Defensive Late Game")]
        public bool FoulWhenDown3 = true;                          // Foul up 3 to prevent tie
        public bool IntentionalFoulWhenDown = true;                // Foul to stop clock when behind
        public bool PlayStraightUpLate = false;                    // Trust defense in clutch

        [Header("Clock Management")]
        public bool RunClockWhenAhead = true;
        public int SecondsAheadToSlowDown = 10;                    // Point lead to start slowing
        [Range(2, 10)] public int TargetLastShotClock = 4;         // When to shoot on last shot
    }

    public enum LateGamePlayType
    {
        BestOption,             // Read and react
        StarIsolation,          // Clear out for star
        PickAndRoll,            // PnR with closer
        PostUp,                 // Post up scorer
        ThreePointer            // Look for 3
    }

    // ==================== STRATEGY TEMPLATES ====================

    public enum StrategyTemplate
    {
        Default,
        WarriorsMotion,         // Motion offense, switch defense
        HeatZone,               // Zone defense, physical
        CelticsSwitching,       // Switch everything
        SpursBallMovement,      // Beautiful game
        RocketsMorball,         // Analytics three-or-rim
        MavericksDoncic,        // Star PnR heavy
        NuggetsJokic,           // Point center
        GrizzliesGrit,          // Grit and grind defense
        SunsSpeedball,          // Seven seconds or less
        BucksGiannis            // Paint beast
    }
}
