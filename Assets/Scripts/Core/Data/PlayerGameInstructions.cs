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

        // ==================== TENDENCY CONFLICT CALCULATION ====================

        /// <summary>
        /// Calculates the overall effectiveness modifier based on how well instructions
        /// align with the player's tendencies. Returns 0.7 (severe conflicts) to 1.3 (great synergy).
        /// </summary>
        public float CalculateTendencyCompatibility(Player player)
        {
            if (player?.Tendencies == null)
                return 1.0f;

            var tendencies = player.Tendencies;
            float totalModifier = 1.0f;
            int factorCount = 0;

            // OFFENSIVE INSTRUCTION CONFLICTS

            // Shot selection vs shot selection tendency
            float shotSelectionConflict = CalculateShotSelectionConflict(tendencies);
            totalModifier += shotSelectionConflict;
            factorCount++;

            // Ball handling role vs ball movement tendency
            float ballHandlingConflict = CalculateBallHandlingConflict(tendencies);
            totalModifier += ballHandlingConflict;
            factorCount++;

            // Off-ball movement vs off-ball movement tendency
            float offBallConflict = CalculateOffBallConflict(tendencies);
            totalModifier += offBallConflict;
            factorCount++;

            // DEFENSIVE INSTRUCTION CONFLICTS

            // Defensive aggression vs defensive gambling tendency
            float defensiveConflict = CalculateDefensiveConflict(tendencies);
            totalModifier += defensiveConflict;
            factorCount++;

            // Help defense vs help defense tendency
            float helpDefenseConflict = CalculateHelpDefenseConflict(tendencies);
            totalModifier += helpDefenseConflict;
            factorCount++;

            // Average out the factors
            if (factorCount > 0)
            {
                totalModifier = totalModifier / factorCount;
            }

            return Mathf.Clamp(totalModifier, 0.7f, 1.3f);
        }

        private float CalculateShotSelectionConflict(PlayerTendencies tendencies)
        {
            // Shot selection instruction vs ShotSelection tendency (-100 to +100)
            // -100 = Quick trigger (wants to shoot often)
            // +100 = Patient shooter (waits for good looks)

            float instructionValue = Offense.ShotSelection switch
            {
                ShotSelectionType.RestrictedOnly => 100f,  // Very patient
                ShotSelectionType.OpenOnly => 60f,         // Patient
                ShotSelectionType.Normal => 0f,            // Neutral
                ShotSelectionType.Aggressive => -40f,      // Quick
                ShotSelectionType.GreenLight => -80f,      // Very quick
                _ => 0f
            };

            // Calculate alignment (-200 to +200 range difference)
            float difference = Math.Abs(instructionValue - tendencies.ShotSelection);

            // Convert to modifier: 0 diff = +0.1 bonus, 200 diff = -0.2 penalty
            return 0.1f - (difference / 666f);
        }

        private float CalculateBallHandlingConflict(PlayerTendencies tendencies)
        {
            // Ball handling role vs BallMovement tendency
            // -100 = Ball stopper (wants to hold)
            // +100 = Willing passer (wants to move ball)

            float instructionValue = Offense.BallHandlingRole switch
            {
                BallHandlingRole.Primary => -40f,          // Holds ball more
                BallHandlingRole.Secondary => 0f,          // Neutral
                BallHandlingRole.Limited => 40f,           // Pass quickly
                BallHandlingRole.None => 80f,              // Never hold
                _ => 0f
            };

            float difference = Math.Abs(instructionValue - tendencies.BallMovement);
            return 0.05f - (difference / 1000f);
        }

        private float CalculateOffBallConflict(PlayerTendencies tendencies)
        {
            // Off-ball movement instruction vs OffBallMovement coachable tendency (0-100)
            float instructionValue = Offense.OffBallMovement switch
            {
                PlayerOffBallMovement.Static => 10f,
                PlayerOffBallMovement.Limited => 30f,
                PlayerOffBallMovement.Normal => 50f,
                PlayerOffBallMovement.Active => 70f,
                PlayerOffBallMovement.Constant => 90f,
                _ => 50f
            };

            float difference = Math.Abs(instructionValue - tendencies.OffBallMovement);
            return 0.05f - (difference / 500f);
        }

        private float CalculateDefensiveConflict(PlayerTendencies tendencies)
        {
            // Defensive aggression instruction vs DefensiveGambling tendency
            // -100 = Conservative (stays in position)
            // +100 = Aggressive (gambles for steals)

            float instructionValue = Defense.OnBallAggression switch
            {
                DefensiveAggressionLevel.Passive => -80f,
                DefensiveAggressionLevel.Low => -40f,
                DefensiveAggressionLevel.Normal => 0f,
                DefensiveAggressionLevel.High => 40f,
                DefensiveAggressionLevel.VeryHigh => 80f,
                _ => 0f
            };

            float difference = Math.Abs(instructionValue - tendencies.DefensiveGambling);
            return 0.1f - (difference / 500f);
        }

        private float CalculateHelpDefenseConflict(PlayerTendencies tendencies)
        {
            // Help defense instruction vs HelpDefenseAggression tendency (0-100)
            // Low = stays home, High = over-helps

            float instructionValue = Defense.HelpDefense switch
            {
                PlayerHelpDefense.NoHelp => 20f,
                PlayerHelpDefense.Light => 35f,
                PlayerHelpDefense.Normal => 50f,
                PlayerHelpDefense.Active => 65f,
                PlayerHelpDefense.Aggressive => 80f,
                _ => 50f
            };

            float difference = Math.Abs(instructionValue - tendencies.HelpDefenseAggression);
            return 0.05f - (difference / 500f);
        }

        /// <summary>
        /// Returns a list of specific instruction conflicts for UI display.
        /// </summary>
        public List<InstructionConflict> GetTendencyConflicts(Player player)
        {
            var conflicts = new List<InstructionConflict>();

            if (player?.Tendencies == null)
                return conflicts;

            var tendencies = player.Tendencies;

            // Check shot selection conflicts
            if (tendencies.ShotSelection < -40 && Offense.ShotSelection == ShotSelectionType.RestrictedOnly)
            {
                conflicts.Add(new InstructionConflict
                {
                    InstructionArea = "Shot Selection",
                    Instruction = "Restricted Only",
                    TendencyDescription = "Quick trigger shooter",
                    Severity = ConflictSeverity.Major,
                    PerformanceImpact = -0.15f,
                    Suggestion = "Consider allowing more shot attempts or accept lower efficiency"
                });
            }
            else if (tendencies.ShotSelection > 40 && Offense.ShotSelection == ShotSelectionType.GreenLight)
            {
                conflicts.Add(new InstructionConflict
                {
                    InstructionArea = "Shot Selection",
                    Instruction = "Green Light",
                    TendencyDescription = "Patient shooter",
                    Severity = ConflictSeverity.Minor,
                    PerformanceImpact = -0.05f,
                    Suggestion = "Player will likely pass up shots anyway - consider Normal instead"
                });
            }

            // Check ball handling conflicts
            if (tendencies.BallMovement < -40 && Offense.BallHandlingRole == BallHandlingRole.None)
            {
                conflicts.Add(new InstructionConflict
                {
                    InstructionArea = "Ball Handling",
                    Instruction = "Never Handle",
                    TendencyDescription = "Ball dominant player",
                    Severity = ConflictSeverity.Major,
                    PerformanceImpact = -0.12f,
                    Suggestion = "Player will be frustrated and less effective - give some ball handling duties"
                });
            }

            // Check defensive conflicts
            if (tendencies.DefensiveGambling < -40 && Defense.OnBallAggression == DefensiveAggressionLevel.VeryHigh)
            {
                conflicts.Add(new InstructionConflict
                {
                    InstructionArea = "Defensive Aggression",
                    Instruction = "Very High",
                    TendencyDescription = "Conservative defender",
                    Severity = ConflictSeverity.Moderate,
                    PerformanceImpact = -0.08f,
                    Suggestion = "Player will struggle with aggressive gambling - reduce aggression"
                });
            }
            else if (tendencies.DefensiveGambling > 40 && Defense.OnBallAggression == DefensiveAggressionLevel.Passive)
            {
                conflicts.Add(new InstructionConflict
                {
                    InstructionArea = "Defensive Aggression",
                    Instruction = "Passive",
                    TendencyDescription = "Aggressive defender",
                    Severity = ConflictSeverity.Moderate,
                    PerformanceImpact = -0.08f,
                    Suggestion = "Player wants to pressure - let them be more aggressive"
                });
            }

            // Check help defense conflicts
            if (tendencies.HelpDefenseAggression < 30 && Defense.HelpDefense == PlayerHelpDefense.Aggressive)
            {
                conflicts.Add(new InstructionConflict
                {
                    InstructionArea = "Help Defense",
                    Instruction = "Aggressive Help",
                    TendencyDescription = "Stays home on assignment",
                    Severity = ConflictSeverity.Minor,
                    PerformanceImpact = -0.05f,
                    Suggestion = "Player prefers staying with their man - help rotations will be late"
                });
            }
            else if (tendencies.HelpDefenseAggression > 70 && Defense.HelpDefense == PlayerHelpDefense.NoHelp)
            {
                conflicts.Add(new InstructionConflict
                {
                    InstructionArea = "Help Defense",
                    Instruction = "No Help",
                    TendencyDescription = "Tends to over-help",
                    Severity = ConflictSeverity.Minor,
                    PerformanceImpact = -0.05f,
                    Suggestion = "Player naturally wants to help - may leave shooter open anyway"
                });
            }

            // Check effort-related conflicts
            if (tendencies.EffortConsistency < -40 && Rotation.TargetMinutes > 32)
            {
                conflicts.Add(new InstructionConflict
                {
                    InstructionArea = "Playing Time",
                    Instruction = $"{Rotation.TargetMinutes} minutes",
                    TendencyDescription = "Inconsistent effort player",
                    Severity = ConflictSeverity.Minor,
                    PerformanceImpact = -0.05f,
                    Suggestion = "Player coasts when tired - consider shorter stints with rest"
                });
            }

            return conflicts;
        }

        /// <summary>
        /// Returns a list of synergies where instructions align well with tendencies.
        /// </summary>
        public List<InstructionSynergy> GetTendencySynergies(Player player)
        {
            var synergies = new List<InstructionSynergy>();

            if (player?.Tendencies == null)
                return synergies;

            var tendencies = player.Tendencies;

            // Good shot selection alignment
            if (tendencies.ShotSelection > 30 &&
                (Offense.ShotSelection == ShotSelectionType.OpenOnly || Offense.ShotSelection == ShotSelectionType.RestrictedOnly))
            {
                synergies.Add(new InstructionSynergy
                {
                    Area = "Shot Selection",
                    Description = "Patient shooter with selective shooting instruction",
                    PerformanceBonus = 0.08f
                });
            }
            else if (tendencies.ShotSelection < -30 && Offense.ShotSelection == ShotSelectionType.GreenLight)
            {
                synergies.Add(new InstructionSynergy
                {
                    Area = "Shot Selection",
                    Description = "Quick trigger shooter given green light",
                    PerformanceBonus = 0.06f
                });
            }

            // Good defensive alignment
            if (tendencies.DefensiveGambling > 30 && Defense.OnBallAggression == DefensiveAggressionLevel.High)
            {
                synergies.Add(new InstructionSynergy
                {
                    Area = "Defensive Aggression",
                    Description = "Aggressive defender with matching instruction",
                    PerformanceBonus = 0.07f
                });
            }

            // Effort and minutes alignment
            if (tendencies.EffortConsistency > 40 && Rotation.TargetMinutes >= 32)
            {
                synergies.Add(new InstructionSynergy
                {
                    Area = "Playing Time",
                    Description = "High motor player gets heavy minutes",
                    PerformanceBonus = 0.05f
                });
            }

            return synergies;
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
        /// Gets the total tendency compatibility score for the entire team.
        /// </summary>
        public float GetTeamTendencyCompatibility(Func<string, Player> getPlayer)
        {
            if (PlayerInstructions.Count == 0)
                return 1.0f;

            float totalCompatibility = 0f;
            int count = 0;

            foreach (var kvp in PlayerInstructions)
            {
                var player = getPlayer?.Invoke(kvp.Key);
                if (player != null)
                {
                    totalCompatibility += kvp.Value.CalculateTendencyCompatibility(player);
                    count++;
                }
            }

            return count > 0 ? totalCompatibility / count : 1.0f;
        }

        /// <summary>
        /// Gets all conflicts across the team.
        /// </summary>
        public Dictionary<string, List<InstructionConflict>> GetAllTeamConflicts(Func<string, Player> getPlayer)
        {
            var allConflicts = new Dictionary<string, List<InstructionConflict>>();

            foreach (var kvp in PlayerInstructions)
            {
                var player = getPlayer?.Invoke(kvp.Key);
                if (player != null)
                {
                    var conflicts = kvp.Value.GetTendencyConflicts(player);
                    if (conflicts.Count > 0)
                    {
                        allConflicts[kvp.Key] = conflicts;
                    }
                }
            }

            return allConflicts;
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

    // ==================== TENDENCY CONFLICT SUPPORT CLASSES ====================

    /// <summary>
    /// Represents a conflict between a player's instruction and their natural tendency.
    /// </summary>
    [Serializable]
    public class InstructionConflict
    {
        /// <summary>Area of the instruction (e.g., "Shot Selection", "Defensive Aggression")</summary>
        public string InstructionArea;

        /// <summary>The specific instruction setting</summary>
        public string Instruction;

        /// <summary>Description of the conflicting tendency</summary>
        public string TendencyDescription;

        /// <summary>How severe the conflict is</summary>
        public ConflictSeverity Severity;

        /// <summary>Performance impact as a modifier (e.g., -0.15 = 15% penalty)</summary>
        public float PerformanceImpact;

        /// <summary>Suggested resolution</summary>
        public string Suggestion;

        public override string ToString()
        {
            return $"{InstructionArea}: {Instruction} conflicts with {TendencyDescription} ({Severity})";
        }
    }

    /// <summary>
    /// Represents a positive synergy between instruction and tendency.
    /// </summary>
    [Serializable]
    public class InstructionSynergy
    {
        /// <summary>Area of the synergy</summary>
        public string Area;

        /// <summary>Description of the synergy</summary>
        public string Description;

        /// <summary>Performance bonus as a modifier (e.g., 0.08 = 8% bonus)</summary>
        public float PerformanceBonus;

        public override string ToString()
        {
            return $"{Area}: {Description} (+{PerformanceBonus:P0})";
        }
    }

    /// <summary>
    /// Severity levels for instruction-tendency conflicts.
    /// </summary>
    public enum ConflictSeverity
    {
        /// <summary>Minor impact, player will adapt but not optimally</summary>
        Minor,

        /// <summary>Noticeable impact on performance</summary>
        Moderate,

        /// <summary>Significant impact, consider changing instruction</summary>
        Major
    }
}
