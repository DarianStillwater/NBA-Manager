using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Coach data model with detailed attributes affecting team performance and player development.
    /// Coaches have varying abilities in different areas like tactics, development, and game management.
    /// </summary>
    [Serializable]
    public class Coach
    {
        // ==================== IDENTITY ====================
        public string CoachId;
        public string FirstName;
        public string LastName;
        public string TeamId;

        public string FullName => $"{FirstName} {LastName}";

        // ==================== POSITION ====================
        public CoachPosition Position;
        public bool IsHeadCoach => Position == CoachPosition.HeadCoach;

        // ==================== CORE ATTRIBUTES (0-100) ====================
        [Header("Tactical Ability")]
        [Range(0, 100)] public int OffensiveScheme;       // Ability to design/run offensive systems
        [Range(0, 100)] public int DefensiveScheme;       // Ability to design/run defensive systems
        [Range(0, 100)] public int InGameAdjustments;     // Ability to adjust tactics mid-game
        [Range(0, 100)] public int PlayDesign;            // Quality of set plays and ATO plays

        [Header("Game Management")]
        [Range(0, 100)] public int GameManagement;        // Timeouts, rotations, end-game decisions
        [Range(0, 100)] public int ClockManagement;       // End of quarter/game situations
        [Range(0, 100)] public int RotationManagement;    // Substitution patterns and lineup usage

        [Header("Player Development")]
        [Range(0, 100)] public int PlayerDevelopment;     // Overall development ability
        [Range(0, 100)] public int GuardDevelopment;      // Specialty: developing guards
        [Range(0, 100)] public int BigManDevelopment;     // Specialty: developing bigs
        [Range(0, 100)] public int ShootingCoaching;      // Ability to improve shooting
        [Range(0, 100)] public int DefenseCoaching;       // Ability to improve defense

        [Header("Leadership & Communication")]
        [Range(0, 100)] public int Motivation;            // Ability to motivate players
        [Range(0, 100)] public int Communication;         // Clear instructions, player rapport
        [Range(0, 100)] public int ManManagement;         // Handling egos, rotations, benching stars
        [Range(0, 100)] public int MediaHandling;         // Press conferences, public perception

        [Header("Scouting & Preparation")]
        [Range(0, 100)] public int ScoutingAbility;       // Preparing for opponents
        [Range(0, 100)] public int TalentEvaluation;      // Identifying player strengths/weaknesses

        // ==================== EXPERIENCE & REPUTATION ====================
        public int ExperienceYears;
        public int Age;
        public int CareerWins;
        public int CareerLosses;
        public int PlayoffAppearances;
        public int ChampionshipsWon;

        [Range(0, 100)] public int Reputation;            // League-wide reputation (affects hiring, FA)

        public List<string> PreviousTeams = new List<string>();
        public List<CoachingAward> Awards = new List<CoachingAward>();

        // ==================== SPECIALIZATIONS ====================
        public List<CoachSpecialization> Specializations = new List<CoachSpecialization>();

        // ==================== COACHING STYLE ====================
        public CoachingStyle PrimaryStyle;
        public CoachingPhilosophy OffensivePhilosophy;
        public CoachingPhilosophy DefensivePhilosophy;

        // ==================== CONTRACT ====================
        public int AnnualSalary;
        public int ContractYearsRemaining;
        public DateTime ContractStartDate;
        public int BuyoutAmount;  // If fired early

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Overall coaching rating based on weighted attributes.
        /// </summary>
        public int OverallRating
        {
            get
            {
                float score = Position switch
                {
                    CoachPosition.HeadCoach => (OffensiveScheme * 0.15f + DefensiveScheme * 0.15f +
                                               GameManagement * 0.15f + PlayerDevelopment * 0.1f +
                                               Motivation * 0.15f + Communication * 0.1f +
                                               InGameAdjustments * 0.1f + ManManagement * 0.1f),

                    CoachPosition.AssistantCoach => (PlayerDevelopment * 0.25f + Communication * 0.2f +
                                                    ScoutingAbility * 0.2f + OffensiveScheme * 0.15f +
                                                    DefensiveScheme * 0.1f + Motivation * 0.1f),

                    CoachPosition.DefensiveCoordinator => (DefensiveScheme * 0.35f + DefenseCoaching * 0.25f +
                                                          Communication * 0.15f + ScoutingAbility * 0.15f +
                                                          InGameAdjustments * 0.1f),

                    CoachPosition.OffensiveCoordinator => (OffensiveScheme * 0.35f + PlayDesign * 0.25f +
                                                          Communication * 0.15f + ScoutingAbility * 0.15f +
                                                          InGameAdjustments * 0.1f),

                    CoachPosition.PlayerDevelopment => (PlayerDevelopment * 0.4f + Communication * 0.2f +
                                                       Motivation * 0.2f + TalentEvaluation * 0.2f),

                    CoachPosition.ShootingCoach => (ShootingCoaching * 0.5f + PlayerDevelopment * 0.25f +
                                                   Communication * 0.15f + Motivation * 0.1f),

                    CoachPosition.BigManCoach => (BigManDevelopment * 0.4f + PlayerDevelopment * 0.25f +
                                                 DefenseCoaching * 0.15f + Communication * 0.1f +
                                                 Motivation * 0.1f),

                    CoachPosition.GuardSkillsCoach => (GuardDevelopment * 0.4f + PlayerDevelopment * 0.25f +
                                                      ShootingCoaching * 0.15f + Communication * 0.1f +
                                                      Motivation * 0.1f),

                    _ => 50
                };
                return Mathf.RoundToInt(score);
            }
        }

        /// <summary>
        /// Career win percentage.
        /// </summary>
        public float WinPercentage
        {
            get
            {
                int totalGames = CareerWins + CareerLosses;
                return totalGames > 0 ? (float)CareerWins / totalGames : 0f;
            }
        }

        /// <summary>
        /// Market value based on attributes and experience.
        /// </summary>
        public int MarketValue
        {
            get
            {
                int baseValue = Position switch
                {
                    CoachPosition.HeadCoach => 2_000_000,
                    CoachPosition.AssistantCoach => 500_000,
                    CoachPosition.DefensiveCoordinator => 800_000,
                    CoachPosition.OffensiveCoordinator => 800_000,
                    CoachPosition.PlayerDevelopment => 400_000,
                    _ => 300_000
                };

                int skillValue = OverallRating * 50_000;
                int expValue = Math.Min(ExperienceYears, 20) * 25_000;
                int winValue = (int)(WinPercentage * 500_000);
                int champValue = ChampionshipsWon * 500_000;

                return baseValue + skillValue + expValue + winValue + champValue;
            }
        }

        /// <summary>
        /// Gets development bonus for a specific player based on coach's specializations.
        /// </summary>
        public float GetDevelopmentBonus(Position playerPosition)
        {
            float bonus = PlayerDevelopment / 100f * 0.3f;  // Base 0-30% from development skill

            // Position-specific bonuses
            if (playerPosition == Data.Position.PointGuard || playerPosition == Data.Position.ShootingGuard)
            {
                bonus += GuardDevelopment / 100f * 0.15f;
            }
            else if (playerPosition == Data.Position.PowerForward || playerPosition == Data.Position.Center)
            {
                bonus += BigManDevelopment / 100f * 0.15f;
            }

            // Specialization bonuses
            foreach (var spec in Specializations)
            {
                if (IsSpecializationRelevant(spec, playerPosition))
                {
                    bonus += 0.05f;
                }
            }

            return Mathf.Clamp(bonus, 0f, 0.5f);  // Cap at 50% bonus
        }

        private bool IsSpecializationRelevant(CoachSpecialization spec, Position position)
        {
            return spec switch
            {
                CoachSpecialization.GuardDevelopment => position == Data.Position.PointGuard || position == Data.Position.ShootingGuard,
                CoachSpecialization.BigManDevelopment => position == Data.Position.PowerForward || position == Data.Position.Center,
                CoachSpecialization.ShootingDevelopment => true,  // All positions benefit
                CoachSpecialization.DefensiveDevelopment => true,
                CoachSpecialization.PlaymakingDevelopment => position == Data.Position.PointGuard,
                _ => false
            };
        }

        // ==================== FACTORY METHODS ====================

        public static Coach CreateRandom(string teamId, CoachPosition position, System.Random rng = null)
        {
            rng ??= new System.Random();

            var firstNames = new[] { "Mike", "John", "Steve", "Tom", "Bill", "Dave", "Jim", "Rick",
                                     "Mark", "Chris", "Paul", "Kevin", "Brian", "Tim", "Joe", "Nick",
                                     "Erik", "Tyronn", "Ime", "Monty", "Taylor", "Jason", "Quin", "Frank" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Davis", "Miller", "Wilson",
                                    "Moore", "Taylor", "Anderson", "Thomas", "Jackson", "White", "Harris",
                                    "Budenholzer", "Spoelstra", "Kerr", "Rivers", "Popovich", "Nurse", "Jenkins" };

            int experience = rng.Next(1, 30);
            int age = 35 + experience + rng.Next(-5, 10);

            // Skill ranges based on experience and position
            int minSkill = 35 + Math.Min(experience, 15);
            int maxSkill = 60 + Math.Min(experience * 2, 35);

            var coach = new Coach
            {
                CoachId = $"COACH_{Guid.NewGuid().ToString().Substring(0, 8)}",
                FirstName = firstNames[rng.Next(firstNames.Length)],
                LastName = lastNames[rng.Next(lastNames.Length)],
                TeamId = teamId,
                Position = position,

                // Tactical
                OffensiveScheme = rng.Next(minSkill, maxSkill + 1),
                DefensiveScheme = rng.Next(minSkill, maxSkill + 1),
                InGameAdjustments = rng.Next(minSkill - 5, maxSkill + 1),
                PlayDesign = rng.Next(minSkill - 5, maxSkill + 1),

                // Game Management
                GameManagement = rng.Next(minSkill, maxSkill + 1),
                ClockManagement = rng.Next(minSkill - 10, maxSkill + 1),
                RotationManagement = rng.Next(minSkill, maxSkill + 1),

                // Development
                PlayerDevelopment = rng.Next(minSkill, maxSkill + 1),
                GuardDevelopment = rng.Next(35, 90),
                BigManDevelopment = rng.Next(35, 90),
                ShootingCoaching = rng.Next(35, 90),
                DefenseCoaching = rng.Next(35, 90),

                // Leadership
                Motivation = rng.Next(minSkill, maxSkill + 1),
                Communication = rng.Next(minSkill, maxSkill + 1),
                ManManagement = rng.Next(35, 85),
                MediaHandling = rng.Next(40, 80),

                // Scouting
                ScoutingAbility = rng.Next(40, 85),
                TalentEvaluation = rng.Next(40, 85),

                // Career
                ExperienceYears = experience,
                Age = age,
                CareerWins = experience * rng.Next(20, 50),
                CareerLosses = experience * rng.Next(20, 50),
                PlayoffAppearances = rng.Next(0, experience / 3),
                ChampionshipsWon = rng.NextDouble() < 0.1 ? rng.Next(1, 3) : 0,
                Reputation = 40 + rng.Next(40),

                // Style
                PrimaryStyle = (CoachingStyle)rng.Next(Enum.GetValues(typeof(CoachingStyle)).Length),
                OffensivePhilosophy = (CoachingPhilosophy)rng.Next(Enum.GetValues(typeof(CoachingPhilosophy)).Length),
                DefensivePhilosophy = (CoachingPhilosophy)rng.Next(Enum.GetValues(typeof(CoachingPhilosophy)).Length)
            };

            // Add 1-3 random specializations
            var allSpecs = (CoachSpecialization[])Enum.GetValues(typeof(CoachSpecialization));
            int specCount = rng.Next(1, 4);
            var usedSpecs = new HashSet<CoachSpecialization>();
            for (int i = 0; i < specCount; i++)
            {
                var spec = allSpecs[rng.Next(allSpecs.Length)];
                if (usedSpecs.Add(spec))
                {
                    coach.Specializations.Add(spec);
                }
            }

            // Set salary based on position and quality
            coach.AnnualSalary = (int)(coach.MarketValue * (0.8f + rng.NextDouble() * 0.4f));
            coach.ContractYearsRemaining = rng.Next(1, 5);

            return coach;
        }

        public static Coach CreateEliteHeadCoach(string teamId)
        {
            var rng = new System.Random();
            var coach = CreateRandom(teamId, CoachPosition.HeadCoach, rng);

            // Boost all attributes to elite levels
            coach.OffensiveScheme = 85 + rng.Next(15);
            coach.DefensiveScheme = 85 + rng.Next(15);
            coach.InGameAdjustments = 85 + rng.Next(15);
            coach.GameManagement = 85 + rng.Next(15);
            coach.PlayerDevelopment = 80 + rng.Next(20);
            coach.Motivation = 85 + rng.Next(15);
            coach.Communication = 80 + rng.Next(20);
            coach.ManManagement = 80 + rng.Next(20);
            coach.Reputation = 85 + rng.Next(15);
            coach.ExperienceYears = 15 + rng.Next(15);
            coach.ChampionshipsWon = 1 + rng.Next(5);
            coach.PlayoffAppearances = 10 + rng.Next(15);
            coach.AnnualSalary = 8_000_000 + rng.Next(4_000_000);

            return coach;
        }

        /// <summary>
        /// Creates a complete coaching staff for a team.
        /// </summary>
        public static List<Coach> CreateCoachingStaff(string teamId, int headCoachQuality = 0)
        {
            var rng = new System.Random(teamId.GetHashCode());
            var staff = new List<Coach>();

            // Head Coach (quality 0 = random, 1 = above average, 2 = elite)
            var headCoach = headCoachQuality switch
            {
                2 => CreateEliteHeadCoach(teamId),
                1 => CreateAboveAverageCoach(teamId, CoachPosition.HeadCoach, rng),
                _ => CreateRandom(teamId, CoachPosition.HeadCoach, rng)
            };
            staff.Add(headCoach);

            // Required staff positions
            staff.Add(CreateRandom(teamId, CoachPosition.AssistantCoach, rng));
            staff.Add(CreateRandom(teamId, CoachPosition.AssistantCoach, rng));
            staff.Add(CreateRandom(teamId, CoachPosition.DefensiveCoordinator, rng));
            staff.Add(CreateRandom(teamId, CoachPosition.OffensiveCoordinator, rng));
            staff.Add(CreateRandom(teamId, CoachPosition.PlayerDevelopment, rng));
            staff.Add(CreateRandom(teamId, CoachPosition.ShootingCoach, rng));
            staff.Add(CreateRandom(teamId, CoachPosition.BigManCoach, rng));
            staff.Add(CreateRandom(teamId, CoachPosition.GuardSkillsCoach, rng));

            return staff;
        }

        private static Coach CreateAboveAverageCoach(string teamId, CoachPosition position, System.Random rng)
        {
            var coach = CreateRandom(teamId, position, rng);

            // Boost key attributes by 10-20 points
            coach.OffensiveScheme = Math.Min(100, coach.OffensiveScheme + rng.Next(10, 20));
            coach.DefensiveScheme = Math.Min(100, coach.DefensiveScheme + rng.Next(10, 20));
            coach.GameManagement = Math.Min(100, coach.GameManagement + rng.Next(10, 20));
            coach.PlayerDevelopment = Math.Min(100, coach.PlayerDevelopment + rng.Next(10, 20));
            coach.Motivation = Math.Min(100, coach.Motivation + rng.Next(10, 20));

            return coach;
        }
    }

    // ==================== ENUMS ====================

    /// <summary>
    /// Coaching staff positions.
    /// </summary>
    public enum CoachPosition
    {
        HeadCoach,
        AssistantCoach,
        DefensiveCoordinator,
        OffensiveCoordinator,
        PlayerDevelopment,
        ShootingCoach,
        BigManCoach,
        GuardSkillsCoach
    }

    /// <summary>
    /// Coaching specializations that provide bonuses.
    /// </summary>
    public enum CoachSpecialization
    {
        GuardDevelopment,
        BigManDevelopment,
        ShootingDevelopment,
        DefensiveDevelopment,
        PlaymakingDevelopment,
        RookieDevelopment,
        VeteranManagement,
        OffensiveSchemes,
        DefensiveSchemes,
        PickAndRoll,
        Transition,
        ThreePointOffense
    }

    /// <summary>
    /// Coach's primary interaction style with players.
    /// </summary>
    public enum CoachingStyle
    {
        Demanding,           // Tough love, high expectations
        Supportive,          // Encouraging, positive reinforcement
        HandsOff,            // Let players figure it out
        PlayerDevelopment,   // Focus on growth over wins
        WinNow,              // Results-oriented
        Analytical,          // Data-driven decisions
        OldSchool,           // Traditional approaches
        Innovative           // New ideas and systems
    }

    /// <summary>
    /// Coaching philosophy for offense/defense.
    /// </summary>
    public enum CoachingPhilosophy
    {
        // Offensive
        FastPace,
        SlowPace,
        ThreePointHeavy,
        InsideOut,
        BallMovement,
        IsoHeavy,
        PickAndRollFocused,

        // Defensive
        Aggressive,
        Conservative,
        SwitchEverything,
        DropCoverage,
        ZoneHeavy,
        ManToMan,
        TrapHeavy
    }

    /// <summary>
    /// Coaching awards and achievements.
    /// </summary>
    [Serializable]
    public class CoachingAward
    {
        public int Year;
        public CoachingAwardType Type;
        public string TeamId;
    }

    public enum CoachingAwardType
    {
        CoachOfTheYear,
        AllStarCoach,
        ConferenceChampion,
        NBAChampion,
        DevelopmentCoachOfYear
    }
}
