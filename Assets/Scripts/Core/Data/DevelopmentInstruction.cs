using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents development instructions for a player.
    /// Players can be assigned focus areas and specific training programs.
    /// </summary>
    [Serializable]
    public class DevelopmentInstruction
    {
        // ==================== IDENTITY ====================
        public string PlayerId;
        public string PlayerName;
        public int Season;

        // ==================== FOCUS AREAS ====================
        /// <summary>
        /// Primary development focus (gets biggest bonus).
        /// </summary>
        public DevelopmentFocus PrimaryFocus;

        /// <summary>
        /// Secondary development focus (smaller bonus).
        /// </summary>
        public DevelopmentFocus SecondaryFocus;

        // ==================== SPECIFIC ATTRIBUTE TARGETS ====================
        /// <summary>
        /// Specific attributes to focus on (max 3).
        /// </summary>
        public List<PlayerAttribute> TargetAttributes = new List<PlayerAttribute>();

        // ==================== TRAINING INTENSITY ====================
        /// <summary>
        /// How intense the offseason training is.
        /// Higher intensity = more development but more injury risk.
        /// </summary>
        public TrainingIntensity Intensity = TrainingIntensity.Normal;

        // ==================== MENTOR ASSIGNMENT ====================
        public string MentorPlayerId;
        public string MentorName;

        // ==================== SPECIAL PROGRAMS ====================
        public List<SpecialProgram> SpecialPrograms = new List<SpecialProgram>();

        // ==================== PLAYER PREFERENCE ====================
        /// <summary>
        /// Whether the player agrees with this development plan.
        /// Disagreement reduces effectiveness.
        /// </summary>
        public bool PlayerAgreed = true;
        public string PlayerPreferredFocus;  // What the player wanted

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Gets development bonus for a specific attribute.
        /// </summary>
        public float GetAttributeBonus(PlayerAttribute attr)
        {
            float bonus = 0f;

            // Primary focus bonus
            if (IsAttributeInFocus(attr, PrimaryFocus))
                bonus += 0.5f;

            // Secondary focus bonus
            if (IsAttributeInFocus(attr, SecondaryFocus))
                bonus += 0.25f;

            // Specific target bonus
            if (TargetAttributes.Contains(attr))
                bonus += 0.3f;

            // Intensity modifier
            bonus *= Intensity switch
            {
                TrainingIntensity.Light => 0.7f,
                TrainingIntensity.Normal => 1.0f,
                TrainingIntensity.Intense => 1.3f,
                TrainingIntensity.Extreme => 1.5f,
                _ => 1.0f
            };

            // Player agreement modifier
            if (!PlayerAgreed)
                bonus *= 0.6f;

            return bonus;
        }

        /// <summary>
        /// Gets injury risk modifier from training intensity.
        /// </summary>
        public float InjuryRiskModifier => Intensity switch
        {
            TrainingIntensity.Light => 0.7f,
            TrainingIntensity.Normal => 1.0f,
            TrainingIntensity.Intense => 1.3f,
            TrainingIntensity.Extreme => 1.6f,
            _ => 1.0f
        };

        /// <summary>
        /// Gets mentor bonus if assigned.
        /// </summary>
        public float MentorBonus => string.IsNullOrEmpty(MentorPlayerId) ? 0f : 0.15f;

        private bool IsAttributeInFocus(PlayerAttribute attr, DevelopmentFocus focus)
        {
            var focusAttributes = GetAttributesForFocus(focus);
            return focusAttributes.Contains(attr);
        }

        /// <summary>
        /// Gets all attributes affected by a development focus.
        /// </summary>
        public static List<PlayerAttribute> GetAttributesForFocus(DevelopmentFocus focus)
        {
            return focus switch
            {
                DevelopmentFocus.Shooting => new List<PlayerAttribute>
                {
                    PlayerAttribute.Shot_Three,
                    PlayerAttribute.Shot_MidRange,
                    PlayerAttribute.FreeThrow,
                    PlayerAttribute.Shot_Close
                },
                DevelopmentFocus.Finishing => new List<PlayerAttribute>
                {
                    PlayerAttribute.Finishing_Rim,
                    PlayerAttribute.Finishing_PostMoves,
                    PlayerAttribute.Shot_Close
                },
                DevelopmentFocus.Playmaking => new List<PlayerAttribute>
                {
                    PlayerAttribute.Passing,
                    PlayerAttribute.BallHandling,
                    PlayerAttribute.OffensiveIQ,
                    PlayerAttribute.SpeedWithBall
                },
                DevelopmentFocus.PerimeterDefense => new List<PlayerAttribute>
                {
                    PlayerAttribute.Defense_Perimeter,
                    PlayerAttribute.Steal,
                    PlayerAttribute.DefensiveIQ,
                    PlayerAttribute.Speed
                },
                DevelopmentFocus.InteriorDefense => new List<PlayerAttribute>
                {
                    PlayerAttribute.Defense_Interior,
                    PlayerAttribute.Block,
                    PlayerAttribute.Defense_PostDefense,
                    PlayerAttribute.DefensiveRebound
                },
                DevelopmentFocus.Rebounding => new List<PlayerAttribute>
                {
                    PlayerAttribute.DefensiveRebound,
                    PlayerAttribute.Strength,
                    PlayerAttribute.Vertical
                },
                DevelopmentFocus.Physical => new List<PlayerAttribute>
                {
                    PlayerAttribute.Strength,
                    PlayerAttribute.Speed,
                    PlayerAttribute.Acceleration,
                    PlayerAttribute.Vertical,
                    PlayerAttribute.Stamina
                },
                DevelopmentFocus.Mental => new List<PlayerAttribute>
                {
                    PlayerAttribute.BasketballIQ,
                    PlayerAttribute.OffensiveIQ,
                    PlayerAttribute.DefensiveIQ,
                    PlayerAttribute.Composure,
                    PlayerAttribute.Consistency
                },
                DevelopmentFocus.PostGame => new List<PlayerAttribute>
                {
                    PlayerAttribute.Finishing_PostMoves,
                    PlayerAttribute.Defense_PostDefense,
                    PlayerAttribute.Strength
                },
                DevelopmentFocus.ThreePointShooting => new List<PlayerAttribute>
                {
                    PlayerAttribute.Shot_Three
                },
                DevelopmentFocus.FreeThrows => new List<PlayerAttribute>
                {
                    PlayerAttribute.FreeThrow
                },
                DevelopmentFocus.Balanced => new List<PlayerAttribute>(),  // No specific bonus
                _ => new List<PlayerAttribute>()
            };
        }

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates default development instruction for a player.
        /// </summary>
        public static DevelopmentInstruction CreateDefault(string playerId, string playerName, int season)
        {
            return new DevelopmentInstruction
            {
                PlayerId = playerId,
                PlayerName = playerName,
                Season = season,
                PrimaryFocus = DevelopmentFocus.Balanced,
                SecondaryFocus = DevelopmentFocus.Balanced,
                Intensity = TrainingIntensity.Normal,
                PlayerAgreed = true
            };
        }

        /// <summary>
        /// Creates instruction based on player weaknesses.
        /// </summary>
        public static DevelopmentInstruction CreateFromWeaknesses(
            Player player,
            int season,
            List<PlayerAttribute> weakAttributes)
        {
            var instruction = CreateDefault(player.PlayerId, player.FullName, season);

            // Determine focus from weak attributes
            if (weakAttributes.Any(a => a == PlayerAttribute.Shot_Three || a == PlayerAttribute.Shot_MidRange))
            {
                instruction.PrimaryFocus = DevelopmentFocus.Shooting;
            }
            else if (weakAttributes.Any(a => a == PlayerAttribute.Defense_Perimeter || a == PlayerAttribute.Steal))
            {
                instruction.PrimaryFocus = DevelopmentFocus.PerimeterDefense;
            }
            else if (weakAttributes.Any(a => a == PlayerAttribute.BallHandling || a == PlayerAttribute.Passing))
            {
                instruction.PrimaryFocus = DevelopmentFocus.Playmaking;
            }

            // Set target attributes (max 3)
            instruction.TargetAttributes = weakAttributes.Take(3).ToList();

            return instruction;
        }
    }

    /// <summary>
    /// Development focus areas.
    /// </summary>
    public enum DevelopmentFocus
    {
        Balanced,           // No specific focus
        Shooting,           // All shooting attributes
        ThreePointShooting, // Three-point only
        FreeThrows,         // Free throw only
        Finishing,          // Rim finishing, post moves
        Playmaking,         // Passing, ball handling
        PerimeterDefense,   // Guarding on perimeter
        InteriorDefense,    // Paint protection
        Rebounding,         // Defensive boards
        Physical,           // Strength, speed, athleticism
        Mental,             // IQ, composure, consistency
        PostGame            // Back-to-basket offense/defense
    }

    /// <summary>
    /// Training intensity levels.
    /// </summary>
    public enum TrainingIntensity
    {
        Light,      // Less development, less injury risk
        Normal,     // Standard development
        Intense,    // More development, more injury risk
        Extreme     // Maximum development, high injury risk
    }

    /// <summary>
    /// Special training programs that provide unique bonuses.
    /// </summary>
    [Serializable]
    public class SpecialProgram
    {
        public string ProgramId;
        public string ProgramName;
        public SpecialProgramType Type;
        public int DurationWeeks;
        public int Cost;

        public float DevelopmentBonus;
        public List<PlayerAttribute> AffectedAttributes = new List<PlayerAttribute>();
        public string Description;

        /// <summary>
        /// Creates predefined special programs.
        /// </summary>
        public static List<SpecialProgram> GetAvailablePrograms()
        {
            return new List<SpecialProgram>
            {
                new SpecialProgram
                {
                    ProgramId = "PROG_SHOOT_CAMP",
                    ProgramName = "Elite Shooting Camp",
                    Type = SpecialProgramType.ShootingCamp,
                    DurationWeeks = 4,
                    Cost = 50_000,
                    DevelopmentBonus = 0.3f,
                    AffectedAttributes = new List<PlayerAttribute>
                    {
                        PlayerAttribute.Shot_Three,
                        PlayerAttribute.Shot_MidRange,
                        PlayerAttribute.FreeThrow
                    },
                    Description = "Intensive shooting camp with NBA shooting coaches."
                },
                new SpecialProgram
                {
                    ProgramId = "PROG_STRENGTH",
                    ProgramName = "Strength & Conditioning Program",
                    Type = SpecialProgramType.StrengthProgram,
                    DurationWeeks = 8,
                    Cost = 40_000,
                    DevelopmentBonus = 0.35f,
                    AffectedAttributes = new List<PlayerAttribute>
                    {
                        PlayerAttribute.Strength,
                        PlayerAttribute.Stamina,
                        PlayerAttribute.Durability
                    },
                    Description = "Professional strength training and conditioning."
                },
                new SpecialProgram
                {
                    ProgramId = "PROG_SKILL_WORK",
                    ProgramName = "Skills Development Retreat",
                    Type = SpecialProgramType.SkillsCamp,
                    DurationWeeks = 3,
                    Cost = 75_000,
                    DevelopmentBonus = 0.25f,
                    AffectedAttributes = new List<PlayerAttribute>
                    {
                        PlayerAttribute.BallHandling,
                        PlayerAttribute.Passing,
                        PlayerAttribute.Finishing_Rim
                    },
                    Description = "Work with elite trainers on fundamental skills."
                },
                new SpecialProgram
                {
                    ProgramId = "PROG_DEFENSE",
                    ProgramName = "Defensive Immersion Camp",
                    Type = SpecialProgramType.DefenseCamp,
                    DurationWeeks = 3,
                    Cost = 45_000,
                    DevelopmentBonus = 0.3f,
                    AffectedAttributes = new List<PlayerAttribute>
                    {
                        PlayerAttribute.Defense_Perimeter,
                        PlayerAttribute.Defense_Interior,
                        PlayerAttribute.DefensiveIQ
                    },
                    Description = "Intensive defensive training with former DPOY coaches."
                },
                new SpecialProgram
                {
                    ProgramId = "PROG_MENTAL",
                    ProgramName = "Mental Performance Training",
                    Type = SpecialProgramType.MentalTraining,
                    DurationWeeks = 6,
                    Cost = 60_000,
                    DevelopmentBonus = 0.4f,
                    AffectedAttributes = new List<PlayerAttribute>
                    {
                        PlayerAttribute.Composure,
                        PlayerAttribute.Clutch,
                        PlayerAttribute.Consistency
                    },
                    Description = "Sports psychology and mental performance coaching."
                },
                new SpecialProgram
                {
                    ProgramId = "PROG_POST",
                    ProgramName = "Big Man Skills Academy",
                    Type = SpecialProgramType.PostCamp,
                    DurationWeeks = 4,
                    Cost = 55_000,
                    DevelopmentBonus = 0.35f,
                    AffectedAttributes = new List<PlayerAttribute>
                    {
                        PlayerAttribute.Finishing_PostMoves,
                        PlayerAttribute.Defense_PostDefense,
                        PlayerAttribute.Block
                    },
                    Description = "Post move development with legendary big men."
                }
            };
        }
    }

    public enum SpecialProgramType
    {
        ShootingCamp,
        StrengthProgram,
        SkillsCamp,
        DefenseCamp,
        MentalTraining,
        PostCamp,
        LeadershipProgram,
        RecoveryProgram
    }

    /// <summary>
    /// Manages all player development instructions for a team.
    /// </summary>
    [Serializable]
    public class TeamDevelopmentPlan
    {
        public string TeamId;
        public int Season;
        public Dictionary<string, DevelopmentInstruction> PlayerInstructions = new();

        /// <summary>
        /// Gets instruction for a specific player.
        /// </summary>
        public DevelopmentInstruction GetInstruction(string playerId)
        {
            return PlayerInstructions.TryGetValue(playerId, out var instruction)
                ? instruction
                : null;
        }

        /// <summary>
        /// Sets instruction for a player.
        /// </summary>
        public void SetInstruction(string playerId, DevelopmentInstruction instruction)
        {
            PlayerInstructions[playerId] = instruction;
        }

        /// <summary>
        /// Gets all players with intense training (injury risk).
        /// </summary>
        public List<string> GetHighIntensityPlayers()
        {
            return PlayerInstructions
                .Where(kvp => kvp.Value.Intensity >= TrainingIntensity.Intense)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Gets total cost of all special programs.
        /// </summary>
        public int GetTotalProgramCost()
        {
            return PlayerInstructions.Values
                .SelectMany(i => i.SpecialPrograms)
                .Sum(p => p.Cost);
        }
    }
}
