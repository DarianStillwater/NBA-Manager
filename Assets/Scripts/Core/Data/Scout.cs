using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Scout data model with detailed attributes for evaluating players and prospects.
    /// Scouts have varying abilities in different areas affecting report accuracy.
    /// </summary>
    [Serializable]
    public class Scout
    {
        // ==================== IDENTITY ====================
        public string ScoutId;
        public string FirstName;
        public string LastName;
        public string TeamId;

        public string FullName => $"{FirstName} {LastName}";

        // ==================== CORE ATTRIBUTES (0-100) ====================
        [Header("Evaluation Skills")]
        [Range(0, 100)] public int EvaluationAccuracy;      // Overall accuracy of reports
        [Range(0, 100)] public int ProspectEvaluation;      // Skill at evaluating draft prospects
        [Range(0, 100)] public int ProEvaluation;           // Skill at evaluating current NBA players
        [Range(0, 100)] public int PotentialAssessment;     // Ability to gauge player ceilings

        [Header("Connections & Access")]
        [Range(0, 100)] public int CollegeConnections;      // Access to college prospects/info
        [Range(0, 100)] public int InternationalConnections; // Access to international prospects
        [Range(0, 100)] public int AgentRelationships;      // Intel on player intentions

        [Header("Work Attributes")]
        [Range(0, 100)] public int WorkRate;                // Players scouted per week
        [Range(0, 100)] public int AttentionToDetail;       // Catches subtle things
        [Range(0, 100)] public int CharacterJudgment;       // Evaluates personality/intangibles

        // ==================== SPECIALIZATION ====================
        public ScoutSpecialization PrimarySpecialization;
        public ScoutSpecialization SecondarySpecialization;

        // ==================== CAREER INFO ====================
        public int ExperienceYears;
        public int Age;
        public List<string> PreviousTeams = new List<string>();

        // Notable past evaluations (for reputation)
        public List<ScoutingHit> NotableHits = new List<ScoutingHit>();    // Correct evaluations
        public List<ScoutingMiss> NotableMisses = new List<ScoutingMiss>(); // Wrong evaluations

        // ==================== CONTRACT ====================
        public int AnnualSalary;
        public int ContractYearsRemaining;
        public DateTime ContractStartDate;

        // ==================== FORMER PLAYER INFO ====================
        /// <summary>
        /// Whether this scout was a former NBA player
        /// </summary>
        public bool IsFormerPlayer;

        /// <summary>
        /// Reference to original player ID if this scout is a former player
        /// </summary>
        public string FormerPlayerId;

        // ==================== STATE ====================
        public bool IsAvailable = true;  // Not on assignment
        public string CurrentAssignmentId;

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Overall rating based on weighted attributes.
        /// </summary>
        public int OverallRating
        {
            get
            {
                float score = (EvaluationAccuracy * 0.25f) +
                             (ProspectEvaluation * 0.15f) +
                             (ProEvaluation * 0.15f) +
                             (PotentialAssessment * 0.15f) +
                             (WorkRate * 0.1f) +
                             (AttentionToDetail * 0.1f) +
                             (CharacterJudgment * 0.1f);
                return Mathf.RoundToInt(score);
            }
        }

        /// <summary>
        /// How many players this scout can evaluate per week.
        /// </summary>
        public int WeeklyCapacity
        {
            get
            {
                // Base of 2, up to 6 based on WorkRate
                return 2 + Mathf.FloorToInt(WorkRate / 25f);
            }
        }

        /// <summary>
        /// Gets effectiveness for a specific scouting target type.
        /// </summary>
        public float GetEffectivenessForTarget(ScoutingTargetType targetType)
        {
            float baseEffectiveness = EvaluationAccuracy / 100f;
            float specializationBonus = 0f;
            float skillBonus = 0f;

            switch (targetType)
            {
                case ScoutingTargetType.CollegeProspect:
                    specializationBonus = (PrimarySpecialization == ScoutSpecialization.College) ? 0.15f :
                                         (SecondarySpecialization == ScoutSpecialization.College) ? 0.08f : 0f;
                    skillBonus = (ProspectEvaluation + CollegeConnections) / 200f * 0.2f;
                    break;

                case ScoutingTargetType.InternationalProspect:
                    specializationBonus = (PrimarySpecialization == ScoutSpecialization.International) ? 0.15f :
                                         (SecondarySpecialization == ScoutSpecialization.International) ? 0.08f : 0f;
                    skillBonus = (ProspectEvaluation + InternationalConnections) / 200f * 0.2f;
                    break;

                case ScoutingTargetType.NBAPlayer:
                    specializationBonus = (PrimarySpecialization == ScoutSpecialization.Pro) ? 0.15f :
                                         (SecondarySpecialization == ScoutSpecialization.Pro) ? 0.08f : 0f;
                    skillBonus = ProEvaluation / 100f * 0.2f;
                    break;

                case ScoutingTargetType.OpponentTeam:
                    specializationBonus = (PrimarySpecialization == ScoutSpecialization.Advance) ? 0.15f :
                                         (SecondarySpecialization == ScoutSpecialization.Advance) ? 0.08f : 0f;
                    skillBonus = (ProEvaluation + AttentionToDetail) / 200f * 0.2f;
                    break;
            }

            return Mathf.Clamp01(baseEffectiveness + specializationBonus + skillBonus);
        }

        /// <summary>
        /// Market value based on attributes and experience.
        /// </summary>
        public int MarketValue
        {
            get
            {
                int baseValue = 50_000;
                int skillValue = OverallRating * 1500; // Up to 150k from skill
                int expValue = ExperienceYears * 5000;  // Up to 75k from experience
                return baseValue + skillValue + expValue;
            }
        }

        // ==================== FACTORY METHODS ====================

        public static Scout CreateRandom(string teamId, System.Random rng = null)
        {
            rng ??= new System.Random();

            var firstNames = new[] { "Mike", "John", "Bob", "Steve", "Tom", "Bill", "Dave", "Jim", "Dan", "Rick",
                                     "Mark", "Chris", "Paul", "Kevin", "Brian", "Gary", "Tim", "Joe", "Jeff", "Scott" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Davis", "Miller", "Wilson", "Moore",
                                    "Taylor", "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson" };

            var specializations = (ScoutSpecialization[])Enum.GetValues(typeof(ScoutSpecialization));
            var primarySpec = specializations[rng.Next(specializations.Length)];
            ScoutSpecialization secondarySpec;
            do { secondarySpec = specializations[rng.Next(specializations.Length)]; }
            while (secondarySpec == primarySpec);

            int experience = rng.Next(1, 25);
            int age = 28 + experience + rng.Next(-3, 8);

            // Skill ranges based on experience
            int minSkill = 35 + Math.Min(experience, 15);
            int maxSkill = 65 + Math.Min(experience * 2, 30);

            var scout = new Scout
            {
                ScoutId = $"SCOUT_{Guid.NewGuid().ToString().Substring(0, 8)}",
                FirstName = firstNames[rng.Next(firstNames.Length)],
                LastName = lastNames[rng.Next(lastNames.Length)],
                TeamId = teamId,

                EvaluationAccuracy = rng.Next(minSkill, maxSkill + 1),
                ProspectEvaluation = rng.Next(minSkill, maxSkill + 1),
                ProEvaluation = rng.Next(minSkill, maxSkill + 1),
                PotentialAssessment = rng.Next(minSkill - 10, maxSkill + 1),
                CollegeConnections = rng.Next(30, 90),
                InternationalConnections = rng.Next(20, 80),
                AgentRelationships = rng.Next(25, 75),
                WorkRate = rng.Next(40, 90),
                AttentionToDetail = rng.Next(minSkill, maxSkill + 1),
                CharacterJudgment = rng.Next(35, 85),

                PrimarySpecialization = primarySpec,
                SecondarySpecialization = secondarySpec,
                ExperienceYears = experience,
                Age = age
            };

            // Set salary based on market value with some variance
            scout.AnnualSalary = (int)(scout.MarketValue * (0.8f + rng.NextDouble() * 0.4f));
            scout.ContractYearsRemaining = rng.Next(1, 4);

            return scout;
        }

        public static Scout CreateElite(string teamId)
        {
            var rng = new System.Random();
            var scout = CreateRandom(teamId, rng);

            // Boost all attributes to elite levels
            scout.EvaluationAccuracy = 85 + rng.Next(15);
            scout.ProspectEvaluation = 80 + rng.Next(20);
            scout.ProEvaluation = 80 + rng.Next(20);
            scout.PotentialAssessment = 80 + rng.Next(20);
            scout.AttentionToDetail = 80 + rng.Next(20);
            scout.CharacterJudgment = 75 + rng.Next(25);
            scout.ExperienceYears = 15 + rng.Next(10);
            scout.AnnualSalary = scout.MarketValue + 50000;

            return scout;
        }

        /// <summary>
        /// Creates a scout from a former player's data with derived attributes.
        /// Used by FormerPlayerScout when a former player becomes a scout.
        /// </summary>
        public static Scout CreateFromFormerPlayer(
            string teamId,
            string firstName,
            string lastName,
            string formerPlayerId,
            int evaluationAccuracy,
            int prospectEvaluation,
            int proEvaluation,
            int potentialAssessment,
            int collegeConnections,
            int internationalConnections,
            int agentRelationships,
            int workRate,
            int attentionToDetail,
            int characterJudgment,
            ScoutSpecialization primarySpec,
            ScoutSpecialization secondarySpec,
            int experience,
            int age)
        {
            var rng = new System.Random();

            var scout = new Scout
            {
                ScoutId = $"SCOUT_{Guid.NewGuid().ToString().Substring(0, 8)}",
                FirstName = firstName,
                LastName = lastName,
                TeamId = teamId,

                // Mark as former player
                IsFormerPlayer = true,
                FormerPlayerId = formerPlayerId,

                // Core attributes
                EvaluationAccuracy = evaluationAccuracy,
                ProspectEvaluation = prospectEvaluation,
                ProEvaluation = proEvaluation,
                PotentialAssessment = potentialAssessment,
                CollegeConnections = collegeConnections,
                InternationalConnections = internationalConnections,
                AgentRelationships = agentRelationships,
                WorkRate = workRate,
                AttentionToDetail = attentionToDetail,
                CharacterJudgment = characterJudgment,

                // Specializations
                PrimarySpecialization = primarySpec,
                SecondarySpecialization = secondarySpec,

                // Career
                ExperienceYears = experience,
                Age = age
            };

            // Set salary based on market value
            scout.AnnualSalary = (int)(scout.MarketValue * (0.9f + rng.NextDouble() * 0.2f));
            scout.ContractYearsRemaining = rng.Next(2, 4);

            return scout;
        }
    }

    /// <summary>
    /// Scout specialization areas.
    /// </summary>
    public enum ScoutSpecialization
    {
        College,        // US college prospects
        International,  // Overseas players and prospects
        Pro,           // Current NBA players
        Advance,       // Opponent scouting and game prep
        Regional       // Specific geographic region expertise
    }

    /// <summary>
    /// Types of scouting targets.
    /// </summary>
    public enum ScoutingTargetType
    {
        CollegeProspect,
        InternationalProspect,
        NBAPlayer,
        OpponentTeam
    }

    /// <summary>
    /// Record of a correct scouting evaluation.
    /// </summary>
    [Serializable]
    public class ScoutingHit
    {
        public string PlayerName;
        public int DraftYear;
        public string EvaluationSummary;  // e.g., "Identified as future All-Star"
        public int ProjectedPick;
        public int ActualPick;
    }

    /// <summary>
    /// Record of an incorrect scouting evaluation.
    /// </summary>
    [Serializable]
    public class ScoutingMiss
    {
        public string PlayerName;
        public int DraftYear;
        public string EvaluationSummary;  // e.g., "Labeled as bust"
        public string Outcome;            // What actually happened
    }
}
