using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Defines a practice drill with its effects on skills, tendencies, and fatigue.
    /// Each drill type targets specific areas of development.
    /// </summary>
    [Serializable]
    public class PracticeDrill
    {
        // ==================== IDENTITY ====================
        public string DrillId;
        public string Name;
        public string Description;
        public DrillCategory Category;
        public DrillType Type;

        // ==================== REQUIREMENTS ====================

        /// <summary>Minimum players needed for this drill</summary>
        public int MinPlayers = 1;

        /// <summary>Maximum players that can participate effectively</summary>
        public int MaxPlayers = 15;

        /// <summary>Recommended duration in minutes</summary>
        public int RecommendedDuration = 15;

        /// <summary>Positions this drill is most effective for</summary>
        public List<Position> TargetPositions = new List<Position>();

        // ==================== EFFECTS ====================

        /// <summary>Skills this drill develops (attribute -> effectiveness 0-1)</summary>
        public Dictionary<PlayerAttribute, float> SkillEffects = new Dictionary<PlayerAttribute, float>();

        /// <summary>Tendencies this drill can improve</summary>
        public Dictionary<string, float> TendencyEffects = new Dictionary<string, float>();

        /// <summary>Base fatigue cost per 15 minutes</summary>
        [Range(1, 20)] public float FatigueCost = 5f;

        /// <summary>Injury risk multiplier (1.0 = normal)</summary>
        [Range(0.5f, 3f)] public float InjuryRisk = 1.0f;

        /// <summary>How engaging this drill is for players (affects morale)</summary>
        [Range(0, 100)] public int Engagement = 50;

        // ==================== GAME PREP EFFECTS ====================

        /// <summary>Whether this drill helps with opponent preparation</summary>
        public bool HelpsOpponentPrep;

        /// <summary>Play familiarity bonus when drilling specific plays</summary>
        public float PlayFamiliarityBonus = 0f;

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>Is this a team drill requiring multiple players?</summary>
        public bool IsTeamDrill => MinPlayers >= 5;

        /// <summary>Primary skill category this drill targets</summary>
        public AttributeCategory PrimaryTargetCategory
        {
            get
            {
                if (SkillEffects.Count == 0) return AttributeCategory.Mental;

                float maxEffect = 0;
                PlayerAttribute maxAttr = PlayerAttribute.BasketballIQ;

                foreach (var kvp in SkillEffects)
                {
                    if (kvp.Value > maxEffect)
                    {
                        maxEffect = kvp.Value;
                        maxAttr = kvp.Key;
                    }
                }

                return PlayerAttributeHelper.GetCategory(maxAttr);
            }
        }

        // ==================== PREDEFINED DRILLS ====================

        /// <summary>
        /// Gets all predefined drills organized by category.
        /// </summary>
        public static Dictionary<DrillCategory, List<PracticeDrill>> GetAllDrills()
        {
            return new Dictionary<DrillCategory, List<PracticeDrill>>
            {
                { DrillCategory.Shooting, GetShootingDrills() },
                { DrillCategory.Ballhandling, GetBallhandlingDrills() },
                { DrillCategory.Defense, GetDefensiveDrills() },
                { DrillCategory.TeamOffense, GetTeamOffenseDrills() },
                { DrillCategory.TeamDefense, GetTeamDefenseDrills() },
                { DrillCategory.Conditioning, GetConditioningDrills() },
                { DrillCategory.FilmStudy, GetFilmStudyDrills() },
                { DrillCategory.Scrimmage, GetScrimmageDrills() }
            };
        }

        public static List<PracticeDrill> GetShootingDrills()
        {
            return new List<PracticeDrill>
            {
                new PracticeDrill
                {
                    DrillId = "shoot_spot",
                    Name = "Spot-Up Shooting",
                    Description = "Catch and shoot from designated spots around the arc",
                    Category = DrillCategory.Shooting,
                    Type = DrillType.SpotShooting,
                    MinPlayers = 1,
                    MaxPlayers = 5,
                    RecommendedDuration = 15,
                    FatigueCost = 3f,
                    InjuryRisk = 0.5f,
                    Engagement = 60,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Shot_Three, 0.8f },
                        { PlayerAttribute.Shot_MidRange, 0.5f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "shoot_off_dribble",
                    Name = "Off-Dribble Shooting",
                    Description = "Pull-up jumpers off various dribble moves",
                    Category = DrillCategory.Shooting,
                    Type = DrillType.OffDribbleShooting,
                    MinPlayers = 1,
                    MaxPlayers = 4,
                    RecommendedDuration = 15,
                    FatigueCost = 5f,
                    InjuryRisk = 0.8f,
                    Engagement = 65,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Shot_MidRange, 0.7f },
                        { PlayerAttribute.Shot_Three, 0.4f },
                        { PlayerAttribute.BallHandling, 0.2f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "shoot_contested",
                    Name = "Contested Shot Drill",
                    Description = "Shooting with a defender closing out",
                    Category = DrillCategory.Shooting,
                    Type = DrillType.ContestedShooting,
                    MinPlayers = 2,
                    MaxPlayers = 6,
                    RecommendedDuration = 12,
                    FatigueCost = 6f,
                    InjuryRisk = 1.0f,
                    Engagement = 70,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Shot_Three, 0.5f },
                        { PlayerAttribute.Shot_MidRange, 0.6f },
                        { PlayerAttribute.Composure, 0.3f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "ShotDiscipline", 0.3f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "shoot_free_throw",
                    Name = "Free Throw Practice",
                    Description = "Focused free throw repetitions",
                    Category = DrillCategory.Shooting,
                    Type = DrillType.FreeThrowPractice,
                    MinPlayers = 1,
                    MaxPlayers = 10,
                    RecommendedDuration = 10,
                    FatigueCost = 1f,
                    InjuryRisk = 0.2f,
                    Engagement = 40,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.FreeThrow, 1.0f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "shoot_post",
                    Name = "Post Scoring Moves",
                    Description = "Back-to-basket footwork and scoring",
                    Category = DrillCategory.Shooting,
                    Type = DrillType.PostMoves,
                    MinPlayers = 1,
                    MaxPlayers = 4,
                    RecommendedDuration = 15,
                    FatigueCost = 6f,
                    InjuryRisk = 1.0f,
                    Engagement = 55,
                    TargetPositions = new List<Position> { Position.PowerForward, Position.Center },
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Finishing_PostMoves, 0.9f },
                        { PlayerAttribute.Shot_Close, 0.4f },
                        { PlayerAttribute.Strength, 0.2f }
                    }
                }
            };
        }

        public static List<PracticeDrill> GetBallhandlingDrills()
        {
            return new List<PracticeDrill>
            {
                new PracticeDrill
                {
                    DrillId = "handle_basic",
                    Name = "Ball Handling Fundamentals",
                    Description = "Stationary and moving dribble drills",
                    Category = DrillCategory.Ballhandling,
                    Type = DrillType.BallHandlingBasic,
                    MinPlayers = 1,
                    MaxPlayers = 10,
                    RecommendedDuration = 12,
                    FatigueCost = 4f,
                    InjuryRisk = 0.5f,
                    Engagement = 50,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.BallHandling, 0.8f },
                        { PlayerAttribute.SpeedWithBall, 0.3f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "handle_pressure",
                    Name = "Pressure Handling",
                    Description = "Handling the ball against defensive pressure",
                    Category = DrillCategory.Ballhandling,
                    Type = DrillType.PressureHandling,
                    MinPlayers = 2,
                    MaxPlayers = 6,
                    RecommendedDuration = 15,
                    FatigueCost = 7f,
                    InjuryRisk = 1.0f,
                    Engagement = 75,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.BallHandling, 0.7f },
                        { PlayerAttribute.Composure, 0.4f },
                        { PlayerAttribute.OffensiveIQ, 0.2f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "handle_passing",
                    Name = "Passing Drills",
                    Description = "Various passing techniques and reads",
                    Category = DrillCategory.Ballhandling,
                    Type = DrillType.PassingDrills,
                    MinPlayers = 2,
                    MaxPlayers = 8,
                    RecommendedDuration = 12,
                    FatigueCost = 4f,
                    InjuryRisk = 0.6f,
                    Engagement = 60,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Passing, 0.9f },
                        { PlayerAttribute.OffensiveIQ, 0.3f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "OffBallMovement", 0.2f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "handle_finishing",
                    Name = "Finishing at the Rim",
                    Description = "Layup and dunk finishing techniques",
                    Category = DrillCategory.Ballhandling,
                    Type = DrillType.FinishingDrills,
                    MinPlayers = 1,
                    MaxPlayers = 6,
                    RecommendedDuration = 15,
                    FatigueCost = 6f,
                    InjuryRisk = 1.2f,
                    Engagement = 70,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Finishing_Rim, 0.9f },
                        { PlayerAttribute.Shot_Close, 0.5f }
                    }
                }
            };
        }

        public static List<PracticeDrill> GetDefensiveDrills()
        {
            return new List<PracticeDrill>
            {
                new PracticeDrill
                {
                    DrillId = "def_closeout",
                    Name = "Closeout Drill",
                    Description = "Proper closeout technique and recovery",
                    Category = DrillCategory.Defense,
                    Type = DrillType.CloseoutDrill,
                    MinPlayers = 2,
                    MaxPlayers = 8,
                    RecommendedDuration = 12,
                    FatigueCost = 6f,
                    InjuryRisk = 1.0f,
                    Engagement = 65,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Defense_Perimeter, 0.7f },
                        { PlayerAttribute.Speed, 0.2f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "CloseoutControl", 0.6f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "def_help_rotation",
                    Name = "Help & Rotation",
                    Description = "Help defense positioning and rotations",
                    Category = DrillCategory.Defense,
                    Type = DrillType.HelpRotation,
                    MinPlayers = 4,
                    MaxPlayers = 10,
                    RecommendedDuration = 15,
                    FatigueCost = 7f,
                    InjuryRisk = 0.9f,
                    Engagement = 60,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.DefensiveIQ, 0.8f },
                        { PlayerAttribute.Defense_Interior, 0.4f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "HelpDefenseAggression", 0.5f },
                        { "DefensiveCommunication", 0.4f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "def_pnr_coverage",
                    Name = "Pick & Roll Coverage",
                    Description = "Defending the pick and roll",
                    Category = DrillCategory.Defense,
                    Type = DrillType.PnRCoverage,
                    MinPlayers = 4,
                    MaxPlayers = 8,
                    RecommendedDuration = 15,
                    FatigueCost = 8f,
                    InjuryRisk = 1.1f,
                    Engagement = 70,
                    HelpsOpponentPrep = true,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.DefensiveIQ, 0.6f },
                        { PlayerAttribute.Defense_Perimeter, 0.4f },
                        { PlayerAttribute.Defense_Interior, 0.4f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "PnRCoverageExecution", 0.7f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "def_post",
                    Name = "Post Defense",
                    Description = "Defending back-to-basket players",
                    Category = DrillCategory.Defense,
                    Type = DrillType.PostDefense,
                    MinPlayers = 2,
                    MaxPlayers = 6,
                    RecommendedDuration = 12,
                    FatigueCost = 7f,
                    InjuryRisk = 1.2f,
                    Engagement = 55,
                    TargetPositions = new List<Position> { Position.PowerForward, Position.Center },
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Defense_PostDefense, 0.9f },
                        { PlayerAttribute.Strength, 0.3f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "PostDefenseAggression", 0.5f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "def_transition",
                    Name = "Transition Defense",
                    Description = "Getting back and stopping the fast break",
                    Category = DrillCategory.Defense,
                    Type = DrillType.TransitionDefense,
                    MinPlayers = 5,
                    MaxPlayers = 10,
                    RecommendedDuration = 12,
                    FatigueCost = 9f,
                    InjuryRisk = 1.0f,
                    Engagement = 65,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Speed, 0.4f },
                        { PlayerAttribute.DefensiveIQ, 0.3f },
                        { PlayerAttribute.Stamina, 0.3f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "TransitionDefense", 0.7f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "def_box_out",
                    Name = "Box Out Drills",
                    Description = "Rebounding positioning and contact",
                    Category = DrillCategory.Defense,
                    Type = DrillType.BoxOutDrill,
                    MinPlayers = 2,
                    MaxPlayers = 10,
                    RecommendedDuration = 10,
                    FatigueCost = 6f,
                    InjuryRisk = 1.1f,
                    Engagement = 55,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.DefensiveRebound, 0.8f },
                        { PlayerAttribute.Strength, 0.3f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "BoxOutDiscipline", 0.7f }
                    }
                }
            };
        }

        public static List<PracticeDrill> GetTeamOffenseDrills()
        {
            return new List<PracticeDrill>
            {
                new PracticeDrill
                {
                    DrillId = "team_halfcourt_sets",
                    Name = "Half-Court Sets",
                    Description = "Running through offensive plays",
                    Category = DrillCategory.TeamOffense,
                    Type = DrillType.HalfCourtSets,
                    MinPlayers = 5,
                    MaxPlayers = 10,
                    RecommendedDuration = 20,
                    FatigueCost = 6f,
                    InjuryRisk = 0.8f,
                    Engagement = 65,
                    HelpsOpponentPrep = true,
                    PlayFamiliarityBonus = 0.8f,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.OffensiveIQ, 0.6f },
                        { PlayerAttribute.BasketballIQ, 0.3f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "OffBallMovement", 0.4f },
                        { "ScreenSetting", 0.3f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "team_fast_break",
                    Name = "Fast Break Offense",
                    Description = "Transition offense execution",
                    Category = DrillCategory.TeamOffense,
                    Type = DrillType.FastBreak,
                    MinPlayers = 5,
                    MaxPlayers = 10,
                    RecommendedDuration = 15,
                    FatigueCost = 8f,
                    InjuryRisk = 1.0f,
                    Engagement = 75,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.SpeedWithBall, 0.5f },
                        { PlayerAttribute.Passing, 0.4f },
                        { PlayerAttribute.Finishing_Rim, 0.3f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "team_press_break",
                    Name = "Press Break",
                    Description = "Breaking full-court pressure",
                    Category = DrillCategory.TeamOffense,
                    Type = DrillType.PressBreak,
                    MinPlayers = 5,
                    MaxPlayers = 10,
                    RecommendedDuration = 12,
                    FatigueCost = 7f,
                    InjuryRisk = 0.9f,
                    Engagement = 60,
                    HelpsOpponentPrep = true,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.BallHandling, 0.4f },
                        { PlayerAttribute.Composure, 0.5f },
                        { PlayerAttribute.Passing, 0.3f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "team_screen_action",
                    Name = "Screen & Action",
                    Description = "Setting screens and reading the defense",
                    Category = DrillCategory.TeamOffense,
                    Type = DrillType.ScreenAction,
                    MinPlayers = 4,
                    MaxPlayers = 10,
                    RecommendedDuration = 15,
                    FatigueCost = 6f,
                    InjuryRisk = 1.0f,
                    Engagement = 60,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.OffensiveIQ, 0.5f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "ScreenSetting", 0.7f }
                    }
                }
            };
        }

        public static List<PracticeDrill> GetTeamDefenseDrills()
        {
            return new List<PracticeDrill>
            {
                new PracticeDrill
                {
                    DrillId = "team_shell_defense",
                    Name = "Shell Defense",
                    Description = "Team defensive positioning and rotations",
                    Category = DrillCategory.TeamDefense,
                    Type = DrillType.ShellDefense,
                    MinPlayers = 5,
                    MaxPlayers = 10,
                    RecommendedDuration = 15,
                    FatigueCost = 7f,
                    InjuryRisk = 0.8f,
                    Engagement = 55,
                    HelpsOpponentPrep = true,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.DefensiveIQ, 0.7f },
                        { PlayerAttribute.Defense_Perimeter, 0.3f }
                    },
                    TendencyEffects = new Dictionary<string, float>
                    {
                        { "HelpDefenseAggression", 0.4f },
                        { "DefensiveCommunication", 0.5f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "team_zone_defense",
                    Name = "Zone Defense",
                    Description = "Zone defensive principles and rotations",
                    Category = DrillCategory.TeamDefense,
                    Type = DrillType.ZoneDefense,
                    MinPlayers = 5,
                    MaxPlayers = 10,
                    RecommendedDuration = 15,
                    FatigueCost = 6f,
                    InjuryRisk = 0.7f,
                    Engagement = 50,
                    HelpsOpponentPrep = true,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.DefensiveIQ, 0.6f }
                    }
                }
            };
        }

        public static List<PracticeDrill> GetConditioningDrills()
        {
            return new List<PracticeDrill>
            {
                new PracticeDrill
                {
                    DrillId = "cond_sprints",
                    Name = "Court Sprints",
                    Description = "Full-court sprints and suicides",
                    Category = DrillCategory.Conditioning,
                    Type = DrillType.Sprints,
                    MinPlayers = 1,
                    MaxPlayers = 15,
                    RecommendedDuration = 10,
                    FatigueCost = 12f,
                    InjuryRisk = 1.3f,
                    Engagement = 30,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Speed, 0.6f },
                        { PlayerAttribute.Acceleration, 0.5f },
                        { PlayerAttribute.Stamina, 0.7f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "cond_endurance",
                    Name = "Endurance Training",
                    Description = "Extended cardio and running",
                    Category = DrillCategory.Conditioning,
                    Type = DrillType.Endurance,
                    MinPlayers = 1,
                    MaxPlayers = 15,
                    RecommendedDuration = 15,
                    FatigueCost = 10f,
                    InjuryRisk = 1.0f,
                    Engagement = 35,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Stamina, 0.9f }
                    }
                }
            };
        }

        public static List<PracticeDrill> GetFilmStudyDrills()
        {
            return new List<PracticeDrill>
            {
                new PracticeDrill
                {
                    DrillId = "film_opponent",
                    Name = "Opponent Film Study",
                    Description = "Studying upcoming opponent tendencies",
                    Category = DrillCategory.FilmStudy,
                    Type = DrillType.OpponentFilm,
                    MinPlayers = 5,
                    MaxPlayers = 15,
                    RecommendedDuration = 30,
                    FatigueCost = 0f,
                    InjuryRisk = 0f,
                    Engagement = 45,
                    HelpsOpponentPrep = true,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.BasketballIQ, 0.4f },
                        { PlayerAttribute.DefensiveIQ, 0.3f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "film_self",
                    Name = "Self-Scout Film",
                    Description = "Reviewing own performance and mistakes",
                    Category = DrillCategory.FilmStudy,
                    Type = DrillType.SelfScoutFilm,
                    MinPlayers = 1,
                    MaxPlayers = 15,
                    RecommendedDuration = 20,
                    FatigueCost = 0f,
                    InjuryRisk = 0f,
                    Engagement = 40,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.BasketballIQ, 0.3f },
                        { PlayerAttribute.OffensiveIQ, 0.2f },
                        { PlayerAttribute.DefensiveIQ, 0.2f }
                    }
                }
            };
        }

        public static List<PracticeDrill> GetScrimmageDrills()
        {
            return new List<PracticeDrill>
            {
                new PracticeDrill
                {
                    DrillId = "scrim_full",
                    Name = "Full Scrimmage",
                    Description = "5v5 game simulation",
                    Category = DrillCategory.Scrimmage,
                    Type = DrillType.FullScrimmage,
                    MinPlayers = 10,
                    MaxPlayers = 15,
                    RecommendedDuration = 20,
                    FatigueCost = 10f,
                    InjuryRisk = 1.5f,
                    Engagement = 85,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.BasketballIQ, 0.3f },
                        { PlayerAttribute.OffensiveIQ, 0.2f },
                        { PlayerAttribute.DefensiveIQ, 0.2f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "scrim_controlled",
                    Name = "Controlled Scrimmage",
                    Description = "5v5 with specific emphasis or rules",
                    Category = DrillCategory.Scrimmage,
                    Type = DrillType.ControlledScrimmage,
                    MinPlayers = 10,
                    MaxPlayers = 15,
                    RecommendedDuration = 15,
                    FatigueCost = 8f,
                    InjuryRisk = 1.2f,
                    Engagement = 75,
                    HelpsOpponentPrep = true,
                    PlayFamiliarityBonus = 0.5f,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.BasketballIQ, 0.4f }
                    }
                },
                new PracticeDrill
                {
                    DrillId = "scrim_situational",
                    Name = "Situational Practice",
                    Description = "End of game, foul situations, etc.",
                    Category = DrillCategory.Scrimmage,
                    Type = DrillType.SituationalPractice,
                    MinPlayers = 10,
                    MaxPlayers = 15,
                    RecommendedDuration = 15,
                    FatigueCost = 6f,
                    InjuryRisk = 1.0f,
                    Engagement = 70,
                    HelpsOpponentPrep = true,
                    SkillEffects = new Dictionary<PlayerAttribute, float>
                    {
                        { PlayerAttribute.Clutch, 0.5f },
                        { PlayerAttribute.Composure, 0.4f },
                        { PlayerAttribute.FreeThrow, 0.2f }
                    }
                }
            };
        }

        /// <summary>
        /// Gets a specific drill by ID.
        /// </summary>
        public static PracticeDrill GetDrillById(string drillId)
        {
            foreach (var category in GetAllDrills().Values)
            {
                foreach (var drill in category)
                {
                    if (drill.DrillId == drillId)
                        return drill;
                }
            }
            return null;
        }
    }

    // ==================== ENUMS ====================

    public enum DrillCategory
    {
        Shooting,
        Ballhandling,
        Defense,
        TeamOffense,
        TeamDefense,
        Conditioning,
        FilmStudy,
        Scrimmage
    }

    public enum DrillType
    {
        // Shooting
        SpotShooting,
        OffDribbleShooting,
        ContestedShooting,
        FreeThrowPractice,
        PostMoves,

        // Ballhandling
        BallHandlingBasic,
        PressureHandling,
        PassingDrills,
        FinishingDrills,

        // Defense
        CloseoutDrill,
        HelpRotation,
        PnRCoverage,
        PostDefense,
        TransitionDefense,
        BoxOutDrill,

        // Team Offense
        HalfCourtSets,
        FastBreak,
        PressBreak,
        ScreenAction,

        // Team Defense
        ShellDefense,
        ZoneDefense,

        // Conditioning
        Sprints,
        Endurance,

        // Film
        OpponentFilm,
        SelfScoutFilm,

        // Scrimmage
        FullScrimmage,
        ControlledScrimmage,
        SituationalPractice
    }
}
