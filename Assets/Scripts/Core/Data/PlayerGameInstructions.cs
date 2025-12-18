using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Individual player instructions for in-game tactical settings.
    /// Controls offensive and defensive behavior, shot selection, and matchups.
    /// </summary>
    [Serializable]
    public class PlayerGameInstructions
    {
        // ==================== IDENTITY ====================
        public string PlayerId;
        public string PlayerName;

        // ==================== OFFENSIVE INSTRUCTIONS ====================
        public OffensiveInstructions Offense = new OffensiveInstructions();

        // ==================== DEFENSIVE INSTRUCTIONS ====================
        public DefensiveInstructions Defense = new DefensiveInstructions();

        // ==================== REBOUNDING ====================
        public ReboundingInstructions Rebounding = new ReboundingInstructions();

        // ==================== MINUTES & ROTATION ====================
        public RotationInstructions Rotation = new RotationInstructions();

        // ==================== SITUATIONAL ====================
        public SituationalInstructions Situational = new SituationalInstructions();

        // ==================== FACTORY METHODS ====================

        public static PlayerGameInstructions CreateDefault(string playerId, string playerName)
        {
            return new PlayerGameInstructions
            {
                PlayerId = playerId,
                PlayerName = playerName
            };
        }

        /// <summary>
        /// Creates instructions optimized for a star player.
        /// </summary>
        public static PlayerGameInstructions CreateStarPlayer(string playerId, string playerName)
        {
            var instructions = CreateDefault(playerId, playerName);

            instructions.Offense.ShotSelection = ShotSelectionType.GreenLight;
            instructions.Offense.UsageRate = UsageRateLevel.High;
            instructions.Offense.IsIsoOption = true;
            instructions.Offense.IsCloser = true;
            instructions.Offense.PostUpFrequency = 30;

            instructions.Rotation.TargetMinutes = 34;
            instructions.Rotation.MustPlayClutch = true;

            return instructions;
        }

        /// <summary>
        /// Creates instructions for a three-and-D role player.
        /// </summary>
        public static PlayerGameInstructions CreateThreeAndD(string playerId, string playerName)
        {
            var instructions = CreateDefault(playerId, playerName);

            instructions.Offense.ShotSelection = ShotSelectionType.OpenOnly;
            instructions.Offense.ThreePointFrequency = 80;
            instructions.Offense.OnlyShootFromSpots = true;
            instructions.Offense.PreferredSpots = new List<CourtSpot>
            {
                CourtSpot.CornerLeft, CourtSpot.CornerRight,
                CourtSpot.WingLeft, CourtSpot.WingRight
            };

            instructions.Defense.OnBallAggression = DefensiveAggressionLevel.High;
            instructions.Defense.IsLockdownDefender = true;

            return instructions;
        }

        /// <summary>
        /// Creates instructions for a rim-running big.
        /// </summary>
        public static PlayerGameInstructions CreateRimRunner(string playerId, string playerName)
        {
            var instructions = CreateDefault(playerId, playerName);

            instructions.Offense.RollVsPop = RollPopPreference.AlwaysRoll;
            instructions.Offense.FinishAtRim = true;
            instructions.Offense.PostUpFrequency = 20;

            instructions.Rebounding.OffensiveRebounding = OffensiveReboundingLevel.Crash;
            instructions.Rebounding.BoxOutIntensity = BoxOutLevel.Aggressive;

            instructions.Defense.ProtectTheRim = true;
            instructions.Defense.ContestLevel = ContestLevel.Contest;

            return instructions;
        }

        /// <summary>
        /// Creates instructions for a stretch big.
        /// </summary>
        public static PlayerGameInstructions CreateStretchBig(string playerId, string playerName)
        {
            var instructions = CreateDefault(playerId, playerName);

            instructions.Offense.RollVsPop = RollPopPreference.AlwaysPop;
            instructions.Offense.ThreePointFrequency = 50;
            instructions.Offense.SpaceTheFloor = true;

            instructions.Rebounding.OffensiveRebounding = OffensiveReboundingLevel.GetBack;

            return instructions;
        }
    }

    // ==================== OFFENSIVE INSTRUCTIONS ====================

    [Serializable]
    public class OffensiveInstructions
    {
        [Header("Shot Selection")]
        public ShotSelectionType ShotSelection = ShotSelectionType.Normal;

        /// <summary>
        /// Percentage of shots that should be three-pointers (0-100).
        /// </summary>
        [Range(0, 100)] public int ThreePointFrequency = 40;

        /// <summary>
        /// Percentage of shots that should be mid-range (0-100).
        /// </summary>
        [Range(0, 100)] public int MidRangeFrequency = 20;

        /// <summary>
        /// Only shoot from designated spots.
        /// </summary>
        public bool OnlyShootFromSpots = false;
        public List<CourtSpot> PreferredSpots = new List<CourtSpot>();

        [Header("Usage")]
        public UsageRateLevel UsageRate = UsageRateLevel.Normal;

        /// <summary>
        /// Whether player can be used as isolation option.
        /// </summary>
        public bool IsIsoOption = false;
        public IsolationSpot IsoSpot = IsolationSpot.Wing;

        /// <summary>
        /// Whether player is designated closer in clutch.
        /// </summary>
        public bool IsCloser = false;

        [Header("Post Play")]
        [Range(0, 100)] public int PostUpFrequency = 0;
        public PostUpSide PostUpSide = PostUpSide.Both;

        [Header("Pick and Roll")]
        public RollPopPreference RollVsPop = RollPopPreference.ReadDefense;
        public bool IsPrimaryBallHandler = false;
        public bool IsScreener = false;

        [Header("Cutting & Movement")]
        public CuttingFrequency CuttingFrequency = CuttingFrequency.Normal;
        public bool FlashToPost = false;
        public bool SpaceTheFloor = false;
        public bool FinishAtRim = false;

        [Header("Ball Handling")]
        public BallHandlingRole BallHandlingRole = BallHandlingRole.Secondary;
        public bool CanInitiateOffense = true;
        public bool PushInTransition = true;

        [Header("Off-Ball")]
        public PlayerOffBallMovement OffBallMovement = PlayerOffBallMovement.Normal;
        public bool SetScreensOffBall = true;
    }

    public enum ShotSelectionType
    {
        RestrictedOnly,         // Only shoot wide open
        OpenOnly,               // Only shoot when open
        Normal,                 // Standard shot selection
        Aggressive,             // Take shots
        GreenLight              // Shoot whenever
    }

    public enum UsageRateLevel
    {
        VeryLow,                // 10-15% usage
        Low,                    // 15-20% usage
        Normal,                 // 20-25% usage
        High,                   // 25-30% usage
        VeryHigh                // 30%+ usage
    }

    public enum RollPopPreference
    {
        AlwaysRoll,
        PreferRoll,
        ReadDefense,
        PreferPop,
        AlwaysPop
    }

    public enum IsolationSpot
    {
        Wing,
        TopOfKey,
        Post,
        Elbow
    }

    public enum PostUpSide
    {
        Left,
        Right,
        Both
    }

    public enum CuttingFrequency
    {
        Never,
        Rarely,
        Normal,
        Often,
        Constant
    }

    public enum BallHandlingRole
    {
        Primary,                // Main ball handler
        Secondary,              // Can handle when needed
        Limited,                // Avoid handling
        None                    // Never touch ball outside scoring
    }

    public enum PlayerOffBallMovement
    {
        Static,                 // Stand and space
        Limited,                // Minimal movement
        Normal,                 // Standard
        Active,                 // Lots of movement
        Constant                // Never stop moving
    }

    public enum CourtSpot
    {
        CornerLeft,
        CornerRight,
        WingLeft,
        WingRight,
        TopOfKey,
        ElbowLeft,
        ElbowRight,
        ShortCornerLeft,
        ShortCornerRight,
        PostLeft,
        PostRight,
        Paint,
        Rim
    }

    // ==================== DEFENSIVE INSTRUCTIONS ====================

    [Serializable]
    public class DefensiveInstructions
    {
        [Header("On-Ball Defense")]
        public DefensiveAggressionLevel OnBallAggression = DefensiveAggressionLevel.Normal;

        /// <summary>
        /// Whether to play tight or sag off.
        /// </summary>
        public OnBallCoverage OnBallCoverage = OnBallCoverage.Normal;

        [Header("Contesting")]
        public ContestLevel ContestLevel = ContestLevel.Contest;
        public bool StayOnFeet = false;                         // Don't jump on pump fakes

        [Header("Help Defense")]
        public PlayerHelpDefense HelpDefense = PlayerHelpDefense.Normal;
        public bool StuntAndRecover = true;
        public bool ProtectTheRim = false;

        [Header("Pick and Roll Coverage")]
        public PnRDefensiveAction PickAndRollAction = PnRDefensiveAction.HedgeAndRecover;
        public bool SwitchOnScreens = false;
        public bool FightOverScreens = true;
        public bool GoUnderScreens = false;

        [Header("Special Assignments")]
        public bool IsLockdownDefender = false;                 // Assigned to best opponent
        public string SpecificMatchupPlayerId = null;           // Guard specific player
        public bool DoubleTeamOnPost = false;                   // Help double post players
        public bool TrapOnPnR = false;                          // Trap ball handler

        [Header("Gambling")]
        [Range(0, 100)] public int GamblingFrequency = 30;      // How often to reach for steals
        public bool JumpPassingLanes = false;

        [Header("Transition Defense")]
        public TransitionDefenseRole TransitionRole = TransitionDefenseRole.MatchUp;
        public bool SprintBackFirst = false;                    // Always sprint back
    }

    public enum DefensiveAggressionLevel
    {
        Passive,                // Stay back, no fouls
        Low,                    // Conservative
        Normal,                 // Standard
        High,                   // Aggressive
        VeryHigh                // Maximum pressure
    }

    public enum OnBallCoverage
    {
        SagOff,                 // Give space
        Normal,                 // Standard
        Tight,                  // Close coverage
        Deny                    // Deny the ball
    }

    public enum ContestLevel
    {
        NoContest,              // Don't contest (avoid fouls)
        Light,                  // Light contest
        Contest,                // Normal contest
        Hard,                   // Hard contest (foul risk)
        FlyBy                   // Run at shooter
    }

    public enum PlayerHelpDefense
    {
        NoHelp,                 // Stay on man
        Light,                  // Minimal help
        Normal,                 // Standard help
        Active,                 // Frequent help
        Aggressive              // Always help
    }

    public enum PnRDefensiveAction
    {
        GoUnder,                // Go under screen
        FightOver,              // Fight over screen
        HedgeAndRecover,        // Show and recover
        Trap,                   // Trap ball handler
        Switch,                 // Switch assignment
        Drop                    // Big drops back
    }

    public enum TransitionDefenseRole
    {
        SprintBack,             // Always sprint back
        MatchUp,                // Find your man
        ProtectRim,             // Get to rim
        StopBall                // Stop ball handler
    }

    // ==================== REBOUNDING INSTRUCTIONS ====================

    [Serializable]
    public class ReboundingInstructions
    {
        [Header("Offensive Rebounding")]
        public OffensiveReboundingLevel OffensiveRebounding = OffensiveReboundingLevel.Balanced;
        public bool CrashFromPerimeter = false;

        [Header("Defensive Rebounding")]
        public BoxOutLevel BoxOutIntensity = BoxOutLevel.Normal;
        public bool OutletQuickly = true;
        public bool LookForLongPass = false;                    // Full court outlet
    }

    public enum OffensiveReboundingLevel
    {
        GetBack,                // Don't crash, get back on defense
        Balanced,               // Sometimes crash
        Crash,                  // Usually crash
        AlwaysCrash             // Always crash glass
    }

    public enum BoxOutLevel
    {
        None,                   // Don't box out
        Light,                  // Light box out
        Normal,                 // Standard
        Aggressive,             // Physical box out
        Punishing               // Maximum effort
    }

    // ==================== ROTATION INSTRUCTIONS ====================

    [Serializable]
    public class RotationInstructions
    {
        [Header("Minutes")]
        [Range(0, 48)] public int TargetMinutes = 28;
        [Range(0, 12)] public int MinMinutesPerStint = 4;
        [Range(0, 16)] public int MaxMinutesPerStint = 10;

        [Header("Substitution Triggers")]
        [Range(50, 100)] public int FatigueSubThreshold = 70;   // Sub when energy drops below
        public bool SubOnFoulTrouble = true;                    // Sub when in foul trouble
        public int FoulLimitFirstHalf = 2;
        public int FoulLimitSecondHalf = 4;

        [Header("Clutch Time")]
        public bool MustPlayClutch = false;                     // Must be in during clutch
        public bool MustPlayEndOfQuarter = false;

        [Header("Restrictions")]
        public bool AvoidBackToBack = false;                    // Rest on back-to-backs
        public bool LimitedMinutes = false;                     // Injury management
        [Range(0, 40)] public int MaxMinutesIfLimited = 24;
    }

    // ==================== SITUATIONAL INSTRUCTIONS ====================

    [Serializable]
    public class SituationalInstructions
    {
        [Header("End of Game")]
        public bool IsFreeThrowShooter = false;                 // Take intentional FTs
        public bool InboundsTheBall = false;                    // Inbounder
        public bool ReceivesInbound = false;                    // Primary target on inbound

        [Header("Special Situations")]
        public bool FoulToStop3 = false;                        // Foul when up 3
        public bool TakeFoulInTransition = false;               // Take foul to stop break
        public bool IntentionalFoulTarget = false;              // Target for Hack-a-Shaq

        [Header("Matchup Situations")]
        public bool SwitchOntoSmall = true;                     // Can switch onto guards
        public bool SwitchOntoBig = false;                      // Can switch onto bigs
        public bool HiddenOnDefense = false;                    // Hide on worst offensive player
    }

    // ==================== TEAM GAME INSTRUCTIONS ====================

    /// <summary>
    /// Container for all player instructions on a team.
    /// </summary>
    [Serializable]
    public class TeamGameInstructions
    {
        public string TeamId;
        public Dictionary<string, PlayerGameInstructions> PlayerInstructions = new Dictionary<string, PlayerGameInstructions>();

        // ==================== TEAM-WIDE SETTINGS ====================

        [Header("Closing Lineup")]
        public List<string> ClosingLineup = new List<string>();  // 5 player IDs

        [Header("Clutch Settings")]
        public string PrimaryCloser;                             // Who gets ball in clutch
        public string SecondaryCloser;
        public string FreeThrowShooterLatest;                    // Best FT shooter

        [Header("Defensive Matchups")]
        public Dictionary<string, string> PresetMatchups = new Dictionary<string, string>();  // Our player -> their player

        [Header("Hack-a-Shaq")]
        public List<string> HackTargets = new List<string>();    // Opponents to intentionally foul

        // ==================== METHODS ====================

        public PlayerGameInstructions GetInstructions(string playerId)
        {
            return PlayerInstructions.TryGetValue(playerId, out var inst) ? inst : null;
        }

        public void SetInstructions(string playerId, PlayerGameInstructions instructions)
        {
            PlayerInstructions[playerId] = instructions;
        }

        public void SetDefensiveMatchup(string defenderPlayerId, string opponentPlayerId)
        {
            PresetMatchups[defenderPlayerId] = opponentPlayerId;
        }

        public string GetMatchupAssignment(string defenderPlayerId)
        {
            return PresetMatchups.TryGetValue(defenderPlayerId, out var opp) ? opp : null;
        }

        public List<string> GetPlayersWithRole(UsageRateLevel usage)
        {
            return PlayerInstructions
                .Where(kvp => kvp.Value.Offense.UsageRate == usage)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        public List<string> GetIsoPlayers()
        {
            return PlayerInstructions
                .Where(kvp => kvp.Value.Offense.IsIsoOption)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        public List<string> GetLockdownDefenders()
        {
            return PlayerInstructions
                .Where(kvp => kvp.Value.Defense.IsLockdownDefender)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Creates default instructions for all team players.
        /// </summary>
        public static TeamGameInstructions CreateDefault(string teamId, List<Player> players)
        {
            var team = new TeamGameInstructions { TeamId = teamId };

            foreach (var player in players)
            {
                var instructions = PlayerGameInstructions.CreateDefault(player.PlayerId, player.FullName);

                // Adjust based on player attributes and position
                AdjustInstructionsForPlayer(instructions, player);

                team.PlayerInstructions[player.PlayerId] = instructions;
            }

            return team;
        }

        private static void AdjustInstructionsForPlayer(PlayerGameInstructions instructions, Player player)
        {
            // Adjust shot selection based on shooting attributes
            int shootingRating = player.GetAttributeForSimulation(PlayerAttribute.Shot_Three);
            if (shootingRating >= 80)
            {
                instructions.Offense.ThreePointFrequency = 50;
                instructions.Offense.ShotSelection = ShotSelectionType.Aggressive;
            }
            else if (shootingRating < 50)
            {
                instructions.Offense.ThreePointFrequency = 10;
                instructions.Offense.ShotSelection = ShotSelectionType.RestrictedOnly;
            }

            // Adjust defensive based on defensive rating
            int defenseRating = player.GetAttributeForSimulation(PlayerAttribute.Defense_Perimeter);
            if (defenseRating >= 80)
            {
                instructions.Defense.OnBallAggression = DefensiveAggressionLevel.High;
                instructions.Defense.IsLockdownDefender = true;
            }

            // Position-based adjustments
            switch (player.Position)
            {
                case Position.PointGuard:
                    instructions.Offense.BallHandlingRole = BallHandlingRole.Primary;
                    instructions.Offense.CanInitiateOffense = true;
                    instructions.Defense.TransitionRole = TransitionDefenseRole.StopBall;
                    break;

                case Position.Center:
                    instructions.Offense.RollVsPop = RollPopPreference.PreferRoll;
                    instructions.Defense.ProtectTheRim = true;
                    instructions.Rebounding.OffensiveRebounding = OffensiveReboundingLevel.Crash;
                    instructions.Rebounding.BoxOutIntensity = BoxOutLevel.Aggressive;
                    break;

                case Position.ShootingGuard:
                case Position.SmallForward:
                    instructions.Offense.CuttingFrequency = CuttingFrequency.Often;
                    instructions.Offense.OffBallMovement = PlayerOffBallMovement.Active;
                    break;
            }

            // Adjust minutes based on overall
            int overall = player.OverallRating;
            if (overall >= 85)
            {
                instructions.Rotation.TargetMinutes = 34;
                instructions.Rotation.MustPlayClutch = true;
            }
            else if (overall >= 75)
            {
                instructions.Rotation.TargetMinutes = 28;
            }
            else if (overall >= 65)
            {
                instructions.Rotation.TargetMinutes = 18;
            }
            else
            {
                instructions.Rotation.TargetMinutes = 8;
            }
        }
    }
}
