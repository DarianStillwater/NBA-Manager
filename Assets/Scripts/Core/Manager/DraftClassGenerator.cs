using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Draft prospect position
    /// </summary>
    [Serializable]
    public enum DraftPosition
    {
        PointGuard,
        ShootingGuard,
        SmallForward,
        PowerForward,
        Center
    }

    /// <summary>
    /// Prospect archetype/player comparison
    /// </summary>
    [Serializable]
    public enum ProspectArchetype
    {
        ScoringGuard,
        FloorGeneral,
        ThreeAndD,
        Slasher,
        StretchBig,
        RimProtector,
        TwoWayWing,
        Playmaker,
        PostScorer,
        Athletic​Freak,
        SharpShooter,
        Versatile​Big,
        ComboGuard,
        PointForward
    }

    /// <summary>
    /// Draft class overall strength tier
    /// </summary>
    [Serializable]
    public enum DraftClassStrength
    {
        Generational,       // Multiple franchise players (2003, 2018)
        Excellent,          // Strong top end, deep class
        Good,               // Solid contributors throughout
        Average,            // Mix of hits and misses
        Weak,               // Limited star potential
        Historically​Bad     // Very few contributors
    }

    /// <summary>
    /// Scout's grade on prospect
    /// </summary>
    [Serializable]
    public class ScoutGrade
    {
        public string ScoutId;
        public string ScoutName;
        public int OverallGrade;            // 0-100
        public int CeilingGrade;            // 0-100
        public int FloorGrade;              // 0-100
        public string ComparisonPlayer;     // "Reminds me of..."
        public string StrengthNote;
        public string ConcernNote;
        public int ProjectedPick;
    }

    /// <summary>
    /// Draft prospect with hidden attributes
    /// </summary>
    [Serializable]
    public class DraftProspect
    {
        public string ProspectId;
        public string FirstName;
        public string LastName;
        public string FullName => $"{FirstName} {LastName}";

        [Header("Bio")]
        public int Age;
        public float Height;            // Inches
        public float Weight;            // Pounds
        public float Wingspan;          // Inches
        public string College;
        public string Country;
        public bool IsInternational;
        public int YearsInCollege;      // 0 for one-and-done international

        [Header("Position")]
        public DraftPosition PrimaryPosition;
        public DraftPosition SecondaryPosition;
        public ProspectArchetype Archetype;

        [Header("Visible Stats (College/International)")]
        public float PointsPerGame;
        public float ReboundsPerGame;
        public float AssistsPerGame;
        public float StealsPerGame;
        public float BlocksPerGame;
        public float FieldGoalPercentage;
        public float ThreePointPercentage;
        public float FreeThrowPercentage;

        [Header("Hidden True Attributes")]
        [HideInInspector] public int TruePotential;         // 60-99
        [HideInInspector] public int TrueBasketballIQ;      // 40-99
        [HideInInspector] public int TrueWorkEthic;         // 40-99
        [HideInInspector] public int TrueAthletic​ism;      // 50-99
        [HideInInspector] public int TrueShootingAbility;   // 40-99
        [HideInInspector] public int TrueDefense;           // 40-99
        [HideInInspector] public int TruePlaymaking;        // 40-99
        [HideInInspector] public int TrueIntangibles;       // 40-99

        [Header("Hidden Concerns")]
        [HideInInspector] public bool HasInjuryRisk;
        [HideInInspector] public bool HasAttitudeIssues;
        [HideInInspector] public bool IsOverhyped;
        [HideInInspector] public bool IsSleeperProspect;
        [HideInInspector] public float BustProbability;     // 0-1

        [Header("Draft Board Status")]
        public int ConsensusRank;
        public int HighestMockRank;
        public int LowestMockRank;
        public List<ScoutGrade> ScoutGrades = new List<ScoutGrade>();

        [Header("Pre-Draft")]
        public bool DeclaredForDraft;
        public bool WithdrewFromDraft;
        public bool AttendedCombine;
        public List<string> WorkoutTeamIds = new List<string>();
        public float CombineStockChange;    // -10 to +10

        public float AverageScoutGrade => ScoutGrades.Count > 0
            ? (float)ScoutGrades.Average(g => g.OverallGrade)
            : 0f;

        public string HeightDisplay => $"{(int)(Height / 12)}'{(int)(Height % 12)}\"";

        /// <summary>
        /// Overall rating (alias for AverageScoutGrade as int)
        /// </summary>
        public int Overall => Mathf.RoundToInt(AverageScoutGrade);

        /// <summary>
        /// Potential alias for UI compatibility
        /// </summary>
        public int Potential => TruePotential;
    }

    /// <summary>
    /// Mock draft projection
    /// </summary>
    [Serializable]
    public class MockDraft
    {
        public string MockDraftId;
        public string SourceName;       // ESPN, The Athletic, etc.
        public DateTime PublishedDate;
        public List<MockDraftPick> Picks = new List<MockDraftPick>();
        public List<string> RisersNoted = new List<string>();
        public List<string> FallersNoted = new List<string>();
    }

    /// <summary>
    /// Single mock draft pick projection
    /// </summary>
    [Serializable]
    public class MockDraftPick
    {
        public int PickNumber;
        public string TeamId;
        public string ProspectId;
        public string Reasoning;
    }

    /// <summary>
    /// Big board ranking
    /// </summary>
    [Serializable]
    public class BigBoard
    {
        public string BoardId;
        public string CreatorName;      // Scout, media outlet, or player's own
        public DateTime LastUpdated;
        public List<BigBoardEntry> Rankings = new List<BigBoardEntry>();
        public string TopTierCutoff;    // "Top 5 are franchise changers"
        public string Notes;
    }

    /// <summary>
    /// Individual big board entry
    /// </summary>
    [Serializable]
    public class BigBoardEntry
    {
        public int Rank;
        public string ProspectId;
        public string Tier;             // "Tier 1", "Tier 2", etc.
        public string Brief;            // One-line summary
        public int PreviousRank;
        public int RankChange => PreviousRank - Rank;   // Positive = rising
    }

    /// <summary>
    /// Draft combine results
    /// </summary>
    [Serializable]
    public class CombineResults
    {
        public string ProspectId;

        [Header("Measurements")]
        public float HeightNoShoes;
        public float HeightWithShoes;
        public float Weight;
        public float Wingspan;
        public float StandingReach;
        public float HandLength;
        public float HandWidth;
        public float BodyFatPercentage;

        [Header("Athletic Testing")]
        public float LaneAgility;           // Seconds
        public float ThreeQuarterSprint;    // Seconds
        public float StandingVertical;      // Inches
        public float MaxVertical;           // Inches
        public float BenchPress;            // Reps at 185lbs

        [Header("Performance")]
        public float ShootingDrill;         // Percentage
        public bool ParticipatedInScrimmages;
        public float ScrimmageGrade;

        [Header("Interviews")]
        public int TeamsInterviewed;
        public float InterviewGrade;        // How well they presented

        [Header("Stock Movement")]
        public StockMovement OverallStockChange;
        public string StockChangeReason;
    }

    [Serializable]
    public enum StockMovement
    {
        RocketingUp,
        Rising,
        Steady,
        Falling,
        Plummeting
    }

    /// <summary>
    /// Historical draft class for comparison
    /// </summary>
    [Serializable]
    public class HistoricalDraftClass
    {
        public int Year;
        public DraftClassStrength Strength;
        public List<string> NotablePlayers = new List<string>();
        public float AllStarsProduced;
        public float HOFProjections;
        public string Summary;
    }

    /// <summary>
    /// Draft class comparison analysis
    /// </summary>
    [Serializable]
    public class DraftClassComparison
    {
        public int CurrentYear;
        public DraftClassStrength ProjectedStrength;
        public string MostSimilarHistoricalYear;
        public string TopProspectComparison;
        public string DepthAssessment;
        public List<string> PositionStrengths = new List<string>();
        public List<string> PositionWeaknesses = new List<string>();
    }

    /// <summary>
    /// Generates draft classes with mock drafts, big boards, and class analysis
    /// </summary>
    public class DraftClassGenerator : MonoBehaviour
    {
        public static DraftClassGenerator Instance { get; private set; }

        [Header("Current Draft Class")]
        [SerializeField] private int draftYear;
        [SerializeField] private DraftClassStrength classStrength;
        [SerializeField] private List<DraftProspect> prospects = new List<DraftProspect>();

        [Header("Pre-Draft Coverage")]
        [SerializeField] private List<MockDraft> mockDrafts = new List<MockDraft>();
        [SerializeField] private List<BigBoard> bigBoards = new List<BigBoard>();
        [SerializeField] private List<CombineResults> combineResults = new List<CombineResults>();

        [Header("Historical Data")]
        [SerializeField] private List<HistoricalDraftClass> historicalClasses = new List<HistoricalDraftClass>();

        [Header("Generation Settings")]
        [SerializeField] private int prospectsPerClass = 60;
        [SerializeField, Range(0f, 1f)] private float internationalRatio = 0.25f;
        [SerializeField, Range(0f, 1f)] private float oneAndDoneRatio = 0.35f;

        // Name generation pools
        private readonly string[] firstNames = {
            "Marcus", "James", "Anthony", "Chris", "Michael", "David", "Kevin", "Brandon",
            "Tyler", "Jordan", "Jaylen", "Jalen", "Devin", "Tyrese", "Cade", "Scottie",
            "Paolo", "Victor", "Scoot", "Amen", "Ausar", "Bronny", "Cooper", "Zach",
            "Isaiah", "Trey", "Cam", "Franz", "Evan", "Jabari", "Keegan", "Bennedict"
        };

        private readonly string[] lastNames = {
            "Williams", "Johnson", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor",
            "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson", "Garcia",
            "Martinez", "Robinson", "Clark", "Rodriguez", "Lewis", "Lee", "Walker", "Hall",
            "Young", "King", "Wright", "Scott", "Green", "Adams", "Baker", "Nelson"
        };

        private readonly string[] colleges = {
            "Duke", "Kentucky", "Kansas", "North Carolina", "UCLA", "Gonzaga", "Michigan",
            "Villanova", "Arizona", "Baylor", "Auburn", "Tennessee", "Texas", "Houston",
            "Arkansas", "LSU", "UConn", "Alabama", "Purdue", "Indiana", "Ohio State",
            "Florida", "Memphis", "Oregon", "USC", "Stanford", "Georgetown", "Syracuse"
        };

        private readonly string[] internationalTeams = {
            "Real Madrid", "Barcelona", "Fenerbahce", "CSKA Moscow", "Olimpia Milano",
            "Maccabi Tel Aviv", "Partizan Belgrade", "NBL Australia", "New Zealand Breakers",
            "Adelaide 36ers", "Ignite", "Overtime Elite", "Metropolitans 92"
        };

        // Events
        public event Action<List<DraftProspect>> OnDraftClassGenerated;
        public event Action<MockDraft> OnMockDraftReleased;
        public event Action<CombineResults> OnCombineResultsReleased;
        public event Action<DraftProspect> OnProspectStockChange;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeHistoricalClasses();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Generate a complete draft class for a given year
        /// </summary>
        public List<DraftProspect> GenerateDraftClass(int year)
        {
            draftYear = year;
            prospects.Clear();
            mockDrafts.Clear();
            bigBoards.Clear();
            combineResults.Clear();

            // Determine class strength randomly with weights
            classStrength = DetermineClassStrength();

            // Generate prospects
            for (int i = 0; i < prospectsPerClass; i++)
            {
                var prospect = GenerateProspect(i);
                prospects.Add(prospect);
            }

            // Sort by true potential for initial consensus
            var sortedByPotential = prospects.OrderByDescending(p => p.TruePotential +
                (p.TrueWorkEthic * 0.3f) + (UnityEngine.Random.Range(-10f, 10f))).ToList();

            // Assign consensus ranks with some variance from true skill
            for (int i = 0; i < sortedByPotential.Count; i++)
            {
                var prospect = sortedByPotential[i];
                prospect.ConsensusRank = i + 1;

                // Add variance for overhyped/sleeper prospects
                if (prospect.IsOverhyped)
                {
                    prospect.ConsensusRank = Mathf.Max(1, prospect.ConsensusRank - UnityEngine.Random.Range(5, 15));
                }
                if (prospect.IsSleeperProspect)
                {
                    prospect.ConsensusRank += UnityEngine.Random.Range(10, 25);
                }
            }

            // Re-sort by consensus
            prospects = prospects.OrderBy(p => p.ConsensusRank).ToList();

            // Generate initial mock drafts
            GenerateInitialMockDrafts();

            // Generate big boards
            GenerateBigBoards();

            OnDraftClassGenerated?.Invoke(prospects);
            Debug.Log($"Generated {year} draft class: {classStrength} strength, {prospects.Count} prospects");

            return prospects;
        }

        private DraftClassStrength DetermineClassStrength()
        {
            float roll = UnityEngine.Random.value;

            // Weighted distribution
            if (roll < 0.05f) return DraftClassStrength.Generational;
            if (roll < 0.20f) return DraftClassStrength.Excellent;
            if (roll < 0.45f) return DraftClassStrength.Good;
            if (roll < 0.75f) return DraftClassStrength.Average;
            if (roll < 0.92f) return DraftClassStrength.Weak;
            return DraftClassStrength.Historically​Bad;
        }

        private DraftProspect GenerateProspect(int index)
        {
            bool isInternational = UnityEngine.Random.value < internationalRatio;
            bool isOneAndDone = !isInternational && UnityEngine.Random.value < oneAndDoneRatio;

            var position = (DraftPosition)UnityEngine.Random.Range(0, 5);
            var archetype = GetArchetypeForPosition(position);

            var prospect = new DraftProspect
            {
                ProspectId = $"PROSPECT_{draftYear}_{index:D3}",
                FirstName = firstNames[UnityEngine.Random.Range(0, firstNames.Length)],
                LastName = lastNames[UnityEngine.Random.Range(0, lastNames.Length)],
                Age = isOneAndDone ? 19 : UnityEngine.Random.Range(19, 23),
                PrimaryPosition = position,
                SecondaryPosition = GetSecondaryPosition(position),
                Archetype = archetype,
                IsInternational = isInternational,
                YearsInCollege = isInternational ? 0 : (isOneAndDone ? 1 : UnityEngine.Random.Range(2, 5)),
                DeclaredForDraft = true
            };

            // Physical attributes based on position
            SetPhysicalAttributes(prospect);

            // College/International team
            prospect.College = isInternational
                ? internationalTeams[UnityEngine.Random.Range(0, internationalTeams.Length)]
                : colleges[UnityEngine.Random.Range(0, colleges.Length)];

            prospect.Country = isInternational
                ? GetRandomInternationalCountry()
                : "USA";

            // Generate stats
            GenerateCollegeStats(prospect);

            // Hidden attributes based on class strength
            GenerateHiddenAttributes(prospect, index);

            // Generate scout grades
            GenerateScoutGrades(prospect);

            return prospect;
        }

        private void SetPhysicalAttributes(DraftProspect prospect)
        {
            // Height in inches based on position
            prospect.Height = prospect.PrimaryPosition switch
            {
                DraftPosition.PointGuard => UnityEngine.Random.Range(72f, 78f),      // 6'0" - 6'6"
                DraftPosition.ShootingGuard => UnityEngine.Random.Range(74f, 80f),   // 6'2" - 6'8"
                DraftPosition.SmallForward => UnityEngine.Random.Range(77f, 82f),    // 6'5" - 6'10"
                DraftPosition.PowerForward => UnityEngine.Random.Range(79f, 84f),    // 6'7" - 7'0"
                DraftPosition.Center => UnityEngine.Random.Range(82f, 88f),          // 6'10" - 7'4"
                _ => 78f
            };

            // Weight correlated with height
            prospect.Weight = prospect.Height * 2.8f + UnityEngine.Random.Range(-20f, 30f);

            // Wingspan typically 2-6 inches more than height
            prospect.Wingspan = prospect.Height + UnityEngine.Random.Range(2f, 8f);
        }

        private void GenerateCollegeStats(DraftProspect prospect)
        {
            // Stats based on archetype and draft position projection
            float tierMultiplier = 1f - (prospect.ConsensusRank / 60f) * 0.3f;

            prospect.PointsPerGame = prospect.Archetype switch
            {
                ProspectArchetype.ScoringGuard => UnityEngine.Random.Range(18f, 26f) * tierMultiplier,
                ProspectArchetype.SharpShooter => UnityEngine.Random.Range(15f, 22f) * tierMultiplier,
                ProspectArchetype.RimProtector => UnityEngine.Random.Range(10f, 16f) * tierMultiplier,
                _ => UnityEngine.Random.Range(12f, 20f) * tierMultiplier
            };

            prospect.ReboundsPerGame = prospect.PrimaryPosition switch
            {
                DraftPosition.Center => UnityEngine.Random.Range(8f, 12f),
                DraftPosition.PowerForward => UnityEngine.Random.Range(6f, 10f),
                _ => UnityEngine.Random.Range(3f, 7f)
            };

            prospect.AssistsPerGame = prospect.Archetype switch
            {
                ProspectArchetype.FloorGeneral => UnityEngine.Random.Range(6f, 10f),
                ProspectArchetype.Playmaker => UnityEngine.Random.Range(5f, 8f),
                ProspectArchetype.PointForward => UnityEngine.Random.Range(4f, 7f),
                _ => UnityEngine.Random.Range(1.5f, 4f)
            };

            prospect.StealsPerGame = UnityEngine.Random.Range(0.5f, 2.5f);
            prospect.BlocksPerGame = prospect.PrimaryPosition >= DraftPosition.PowerForward
                ? UnityEngine.Random.Range(1f, 3f)
                : UnityEngine.Random.Range(0.2f, 1f);

            prospect.FieldGoalPercentage = UnityEngine.Random.Range(0.42f, 0.58f);
            prospect.ThreePointPercentage = UnityEngine.Random.Range(0.28f, 0.42f);
            prospect.FreeThrowPercentage = UnityEngine.Random.Range(0.65f, 0.88f);
        }

        private void GenerateHiddenAttributes(DraftProspect prospect, int index)
        {
            // Base potential influenced by class strength and draft position
            int basePotential = classStrength switch
            {
                DraftClassStrength.Generational => 75,
                DraftClassStrength.Excellent => 70,
                DraftClassStrength.Good => 65,
                DraftClassStrength.Average => 60,
                DraftClassStrength.Weak => 55,
                DraftClassStrength.Historically​Bad => 50,
                _ => 60
            };

            // Top prospects have higher potential
            float positionBonus = Mathf.Max(0, (30 - index) * 0.5f);

            prospect.TruePotential = Mathf.Clamp(
                basePotential + (int)positionBonus + UnityEngine.Random.Range(-10, 15),
                50, 99);

            // Other hidden attributes
            prospect.TrueBasketballIQ = UnityEngine.Random.Range(50, 95);
            prospect.TrueWorkEthic = UnityEngine.Random.Range(45, 98);
            prospect.TrueAthletic​ism = UnityEngine.Random.Range(55, 99);
            prospect.TrueShootingAbility = UnityEngine.Random.Range(45, 95);
            prospect.TrueDefense = UnityEngine.Random.Range(45, 90);
            prospect.TruePlaymaking = UnityEngine.Random.Range(40, 95);
            prospect.TrueIntangibles = UnityEngine.Random.Range(50, 95);

            // Hidden concerns
            prospect.HasInjuryRisk = UnityEngine.Random.value < 0.15f;
            prospect.HasAttitudeIssues = UnityEngine.Random.value < 0.10f;

            // 15% chance of being overhyped (true potential lower than perceived)
            prospect.IsOverhyped = UnityEngine.Random.value < 0.15f;
            if (prospect.IsOverhyped)
            {
                prospect.TruePotential = Mathf.Max(50, prospect.TruePotential - UnityEngine.Random.Range(10, 20));
            }

            // 10% chance of being a sleeper (true potential higher than perceived)
            prospect.IsSleeperProspect = !prospect.IsOverhyped && UnityEngine.Random.value < 0.10f;
            if (prospect.IsSleeperProspect)
            {
                prospect.TruePotential = Mathf.Min(99, prospect.TruePotential + UnityEngine.Random.Range(10, 20));
            }

            // Bust probability based on various factors
            prospect.BustProbability = CalculateBustProbability(prospect);
        }

        private float CalculateBustProbability(DraftProspect prospect)
        {
            float bustChance = 0.15f;   // Base 15%

            if (prospect.TrueWorkEthic < 60) bustChance += 0.15f;
            if (prospect.TrueBasketballIQ < 55) bustChance += 0.10f;
            if (prospect.HasInjuryRisk) bustChance += 0.10f;
            if (prospect.HasAttitudeIssues) bustChance += 0.12f;
            if (prospect.IsOverhyped) bustChance += 0.20f;

            // High potential reduces bust chance
            if (prospect.TruePotential > 85) bustChance -= 0.10f;
            if (prospect.TrueWorkEthic > 85) bustChance -= 0.08f;

            return Mathf.Clamp01(bustChance);
        }

        private void GenerateScoutGrades(DraftProspect prospect)
        {
            string[] scoutNames = { "ESPN Scout", "Athletic Analyst", "Team Scout A", "Team Scout B", "Draft Expert" };

            foreach (var scoutName in scoutNames)
            {
                // Scouts have varying accuracy - some closer to true potential
                float accuracy = UnityEngine.Random.Range(0.6f, 0.95f);
                int perceivedGrade = Mathf.RoundToInt(
                    Mathf.Lerp(prospect.TruePotential + UnityEngine.Random.Range(-15, 15),
                              prospect.TruePotential, accuracy));

                var grade = new ScoutGrade
                {
                    ScoutId = Guid.NewGuid().ToString(),
                    ScoutName = scoutName,
                    OverallGrade = Mathf.Clamp(perceivedGrade, 40, 99),
                    CeilingGrade = Mathf.Clamp(perceivedGrade + UnityEngine.Random.Range(5, 15), 50, 99),
                    FloorGrade = Mathf.Clamp(perceivedGrade - UnityEngine.Random.Range(10, 25), 35, 85),
                    ProjectedPick = EstimatePickFromGrade(perceivedGrade),
                    ComparisonPlayer = GetPlayerComparison(prospect),
                    StrengthNote = GetStrengthNote(prospect),
                    ConcernNote = GetConcernNote(prospect)
                };

                prospect.ScoutGrades.Add(grade);
            }

            // Calculate mock rank range
            var projectedPicks = prospect.ScoutGrades.Select(g => g.ProjectedPick).ToList();
            prospect.HighestMockRank = projectedPicks.Min();
            prospect.LowestMockRank = projectedPicks.Max();
        }

        private int EstimatePickFromGrade(int grade)
        {
            if (grade >= 90) return UnityEngine.Random.Range(1, 4);
            if (grade >= 85) return UnityEngine.Random.Range(2, 8);
            if (grade >= 80) return UnityEngine.Random.Range(5, 15);
            if (grade >= 75) return UnityEngine.Random.Range(10, 25);
            if (grade >= 70) return UnityEngine.Random.Range(15, 35);
            if (grade >= 65) return UnityEngine.Random.Range(25, 45);
            return UnityEngine.Random.Range(35, 60);
        }

        private string GetPlayerComparison(DraftProspect prospect)
        {
            return prospect.Archetype switch
            {
                ProspectArchetype.ScoringGuard => new[] { "Devin Booker", "Donovan Mitchell", "Jaylen Brown" }[UnityEngine.Random.Range(0, 3)],
                ProspectArchetype.FloorGeneral => new[] { "Chris Paul", "Tyrese Haliburton", "Darius Garland" }[UnityEngine.Random.Range(0, 3)],
                ProspectArchetype.ThreeAndD => new[] { "Mikal Bridges", "OG Anunoby", "Herb Jones" }[UnityEngine.Random.Range(0, 3)],
                ProspectArchetype.RimProtector => new[] { "Rudy Gobert", "Myles Turner", "Robert Williams" }[UnityEngine.Random.Range(0, 3)],
                ProspectArchetype.StretchBig => new[] { "Karl-Anthony Towns", "Kristaps Porzingis", "Brook Lopez" }[UnityEngine.Random.Range(0, 3)],
                ProspectArchetype.Playmaker => new[] { "LaMelo Ball", "Trae Young", "Ja Morant" }[UnityEngine.Random.Range(0, 3)],
                ProspectArchetype.TwoWayWing => new[] { "Kawhi Leonard", "Jimmy Butler", "Paul George" }[UnityEngine.Random.Range(0, 3)],
                ProspectArchetype.Athletic​Freak => new[] { "Zion Williamson", "Ja Morant", "Anthony Edwards" }[UnityEngine.Random.Range(0, 3)],
                _ => "Versatile prospect"
            };
        }

        private string GetStrengthNote(DraftProspect prospect)
        {
            var strengths = new List<string>();

            if (prospect.TrueAthletic​ism > 85) strengths.Add("Elite athleticism");
            if (prospect.TrueShootingAbility > 85) strengths.Add("Pure shooter");
            if (prospect.TrueDefense > 80) strengths.Add("Defensive stopper");
            if (prospect.TruePlaymaking > 85) strengths.Add("Excellent court vision");
            if (prospect.TrueBasketballIQ > 85) strengths.Add("High basketball IQ");
            if (prospect.Wingspan > prospect.Height + 6) strengths.Add("Exceptional wingspan");

            if (strengths.Count == 0) strengths.Add("Solid all-around game");

            return strengths[UnityEngine.Random.Range(0, strengths.Count)];
        }

        private string GetConcernNote(DraftProspect prospect)
        {
            var concerns = new List<string>();

            if (prospect.HasInjuryRisk) concerns.Add("Injury history");
            if (prospect.HasAttitudeIssues) concerns.Add("Character questions");
            if (prospect.TrueShootingAbility < 60) concerns.Add("Questionable shooting");
            if (prospect.TrueDefense < 55) concerns.Add("Defensive liability");
            if (prospect.TrueWorkEthic < 60) concerns.Add("Motor concerns");
            if (prospect.Age >= 22) concerns.Add("Age/limited upside");

            if (concerns.Count == 0) concerns.Add("Minor technical refinements needed");

            return concerns[UnityEngine.Random.Range(0, concerns.Count)];
        }

        /// <summary>
        /// Generate multiple mock drafts from different sources
        /// </summary>
        public void GenerateInitialMockDrafts()
        {
            string[] sources = { "ESPN", "The Athletic", "Bleacher Report", "NBADraft.net", "Sports Illustrated" };

            foreach (var source in sources)
            {
                var mock = GenerateMockDraft(source);
                mockDrafts.Add(mock);
                OnMockDraftReleased?.Invoke(mock);
            }
        }

        private MockDraft GenerateMockDraft(string source)
        {
            var mock = new MockDraft
            {
                MockDraftId = Guid.NewGuid().ToString(),
                SourceName = source,
                PublishedDate = DateTime.Now
            };

            // Create varied rankings based on consensus + randomness
            var rankedProspects = prospects
                .OrderBy(p => p.ConsensusRank + UnityEngine.Random.Range(-8, 8))
                .ToList();

            // Would integrate with actual draft order
            for (int pick = 1; pick <= 60; pick++)
            {
                if (pick - 1 < rankedProspects.Count)
                {
                    mock.Picks.Add(new MockDraftPick
                    {
                        PickNumber = pick,
                        TeamId = $"TEAM_{pick:D2}",     // Placeholder
                        ProspectId = rankedProspects[pick - 1].ProspectId,
                        Reasoning = GenerateDraftReasoning(rankedProspects[pick - 1], pick)
                    });
                }
            }

            // Identify risers and fallers vs consensus
            foreach (var pick in mock.Picks.Take(30))
            {
                var prospect = prospects.First(p => p.ProspectId == pick.ProspectId);
                int change = prospect.ConsensusRank - pick.PickNumber;

                if (change > 5)
                    mock.RisersNoted.Add($"{prospect.FullName} (#{prospect.ConsensusRank} → #{pick.PickNumber})");
                else if (change < -5)
                    mock.FallersNoted.Add($"{prospect.FullName} (#{prospect.ConsensusRank} → #{pick.PickNumber})");
            }

            return mock;
        }

        private string GenerateDraftReasoning(DraftProspect prospect, int pick)
        {
            if (pick <= 5)
                return $"Franchise-altering talent with {GetStrengthNote(prospect).ToLower()}.";
            if (pick <= 14)
                return $"High upside prospect who can contribute immediately.";
            if (pick <= 30)
                return $"Solid value pick with starter potential.";
            return $"Developmental prospect worth the gamble.";
        }

        /// <summary>
        /// Generate big boards from various sources
        /// </summary>
        public void GenerateBigBoards()
        {
            // Consensus big board
            var consensus = new BigBoard
            {
                BoardId = Guid.NewGuid().ToString(),
                CreatorName = "Consensus Rankings",
                LastUpdated = DateTime.Now,
                Notes = $"{classStrength} draft class with {GetPositionStrengthSummary()}"
            };

            foreach (var prospect in prospects.OrderBy(p => p.ConsensusRank).Take(60))
            {
                consensus.Rankings.Add(new BigBoardEntry
                {
                    Rank = prospect.ConsensusRank,
                    ProspectId = prospect.ProspectId,
                    Tier = GetTier(prospect.ConsensusRank),
                    Brief = $"{prospect.Archetype} from {prospect.College}",
                    PreviousRank = prospect.ConsensusRank
                });
            }

            bigBoards.Add(consensus);
        }

        private string GetTier(int rank)
        {
            if (rank <= 5) return "Tier 1 - Franchise Cornerstone";
            if (rank <= 10) return "Tier 2 - All-Star Potential";
            if (rank <= 20) return "Tier 3 - Quality Starter";
            if (rank <= 30) return "Tier 4 - Rotation Player";
            if (rank <= 45) return "Tier 5 - Developmental";
            return "Tier 6 - Long Shot";
        }

        private string GetPositionStrengthSummary()
        {
            var positionCounts = prospects.Take(20)
                .GroupBy(p => p.PrimaryPosition)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key.ToString())
                .ToList();

            if (positionCounts.Count > 0)
                return $"Strong at {positionCounts[0]}";
            return "Balanced across positions";
        }

        /// <summary>
        /// Simulate draft combine and update prospect stock
        /// </summary>
        public void SimulateCombine()
        {
            foreach (var prospect in prospects.Take(40))
            {
                if (!prospect.AttendedCombine && UnityEngine.Random.value > 0.15f)
                {
                    prospect.AttendedCombine = true;
                    var results = SimulateCombineResults(prospect);
                    combineResults.Add(results);
                    OnCombineResultsReleased?.Invoke(results);

                    // Update stock
                    prospect.CombineStockChange = results.OverallStockChange switch
                    {
                        StockMovement.RocketingUp => UnityEngine.Random.Range(6f, 10f),
                        StockMovement.Rising => UnityEngine.Random.Range(2f, 6f),
                        StockMovement.Steady => UnityEngine.Random.Range(-1f, 1f),
                        StockMovement.Falling => UnityEngine.Random.Range(-6f, -2f),
                        StockMovement.Plummeting => UnityEngine.Random.Range(-10f, -6f),
                        _ => 0f
                    };

                    if (Math.Abs(prospect.CombineStockChange) > 3f)
                    {
                        OnProspectStockChange?.Invoke(prospect);
                    }
                }
            }
        }

        private CombineResults SimulateCombineResults(DraftProspect prospect)
        {
            var results = new CombineResults
            {
                ProspectId = prospect.ProspectId,
                HeightNoShoes = prospect.Height - 1f,
                HeightWithShoes = prospect.Height,
                Weight = prospect.Weight,
                Wingspan = prospect.Wingspan,
                StandingReach = prospect.Height + prospect.Wingspan * 0.4f,
                HandLength = UnityEngine.Random.Range(8.5f, 10.5f),
                HandWidth = UnityEngine.Random.Range(9f, 11.5f),
                BodyFatPercentage = UnityEngine.Random.Range(4f, 12f),
                LaneAgility = UnityEngine.Random.Range(10.5f, 12.5f),
                ThreeQuarterSprint = UnityEngine.Random.Range(3.0f, 3.5f),
                StandingVertical = UnityEngine.Random.Range(26f, 36f),
                MaxVertical = UnityEngine.Random.Range(32f, 44f),
                BenchPress = UnityEngine.Random.Range(0, 20),
                ShootingDrill = UnityEngine.Random.Range(0.55f, 0.85f),
                ParticipatedInScrimmages = UnityEngine.Random.value > 0.3f,
                ScrimmageGrade = UnityEngine.Random.Range(60f, 95f),
                TeamsInterviewed = UnityEngine.Random.Range(8, 25),
                InterviewGrade = UnityEngine.Random.Range(65f, 95f)
            };

            // Determine stock movement based on performance vs expectations
            float performanceVsExpectation = UnityEngine.Random.Range(-1f, 1f);

            // True athleticism influences combine performance
            if (prospect.TrueAthletic​ism > 85)
                performanceVsExpectation += 0.3f;
            else if (prospect.TrueAthletic​ism < 60)
                performanceVsExpectation -= 0.2f;

            results.OverallStockChange = performanceVsExpectation switch
            {
                > 0.6f => StockMovement.RocketingUp,
                > 0.2f => StockMovement.Rising,
                > -0.2f => StockMovement.Steady,
                > -0.6f => StockMovement.Falling,
                _ => StockMovement.Plummeting
            };

            results.StockChangeReason = results.OverallStockChange switch
            {
                StockMovement.RocketingUp => "Impressive athletic testing exceeded expectations",
                StockMovement.Rising => "Solid showing across all drills",
                StockMovement.Steady => "Performed as expected",
                StockMovement.Falling => "Athletic testing below projections",
                StockMovement.Plummeting => "Struggled in multiple areas, medical concerns arose",
                _ => "Standard combine performance"
            };

            return results;
        }

        /// <summary>
        /// Get draft class comparison with historical classes
        /// </summary>
        public DraftClassComparison GetClassComparison()
        {
            var comparison = new DraftClassComparison
            {
                CurrentYear = draftYear,
                ProjectedStrength = classStrength
            };

            // Find most similar historical class
            var similarClass = historicalClasses.FirstOrDefault(h => h.Strength == classStrength);
            comparison.MostSimilarHistoricalYear = similarClass != null
                ? $"{similarClass.Year} ({string.Join(", ", similarClass.NotablePlayers.Take(3))})"
                : "Unique class";

            // Top prospect comparison
            var topProspect = prospects.FirstOrDefault();
            if (topProspect != null)
            {
                comparison.TopProspectComparison = $"{topProspect.FullName} projects as {topProspect.ScoutGrades.FirstOrDefault()?.ComparisonPlayer ?? "impact player"}";
            }

            // Depth assessment
            int lotteryGradeCount = prospects.Count(p => p.AverageScoutGrade >= 80);
            comparison.DepthAssessment = lotteryGradeCount switch
            {
                >= 8 => "Exceptionally deep - lottery talent available late",
                >= 5 => "Good depth - quality players throughout first round",
                >= 3 => "Top heavy - significant drop-off after top tier",
                _ => "Shallow - few impact players available"
            };

            // Position analysis
            var positionGroups = prospects.Take(30).GroupBy(p => p.PrimaryPosition);
            foreach (var group in positionGroups.OrderByDescending(g => g.Count()))
            {
                if (group.Count() >= 8)
                    comparison.PositionStrengths.Add($"{group.Key} ({group.Count()} in first round)");
                else if (group.Count() <= 3)
                    comparison.PositionWeaknesses.Add($"{group.Key} (only {group.Count()} in first round)");
            }

            return comparison;
        }

        private void InitializeHistoricalClasses()
        {
            historicalClasses = new List<HistoricalDraftClass>
            {
                new HistoricalDraftClass {
                    Year = 2003,
                    Strength = DraftClassStrength.Generational,
                    NotablePlayers = new List<string> { "LeBron James", "Carmelo Anthony", "Chris Bosh", "Dwyane Wade" },
                    AllStarsProduced = 8,
                    Summary = "Greatest draft class ever"
                },
                new HistoricalDraftClass {
                    Year = 2018,
                    Strength = DraftClassStrength.Excellent,
                    NotablePlayers = new List<string> { "Luka Doncic", "Trae Young", "Shai Gilgeous-Alexander" },
                    AllStarsProduced = 5,
                    Summary = "Multiple franchise players"
                },
                new HistoricalDraftClass {
                    Year = 2013,
                    Strength = DraftClassStrength.Weak,
                    NotablePlayers = new List<string> { "Giannis Antetokounmpo", "Rudy Gobert" },
                    AllStarsProduced = 3,
                    Summary = "Hidden gems emerged late"
                }
            };
        }

        private ProspectArchetype GetArchetypeForPosition(DraftPosition position)
        {
            var archetypes = position switch
            {
                DraftPosition.PointGuard => new[] { ProspectArchetype.FloorGeneral, ProspectArchetype.ScoringGuard, ProspectArchetype.ComboGuard },
                DraftPosition.ShootingGuard => new[] { ProspectArchetype.ScoringGuard, ProspectArchetype.ThreeAndD, ProspectArchetype.SharpShooter },
                DraftPosition.SmallForward => new[] { ProspectArchetype.TwoWayWing, ProspectArchetype.Slasher, ProspectArchetype.PointForward },
                DraftPosition.PowerForward => new[] { ProspectArchetype.StretchBig, ProspectArchetype.Athletic​Freak, ProspectArchetype.Versatile​Big },
                DraftPosition.Center => new[] { ProspectArchetype.RimProtector, ProspectArchetype.PostScorer, ProspectArchetype.StretchBig },
                _ => new[] { ProspectArchetype.TwoWayWing }
            };

            return archetypes[UnityEngine.Random.Range(0, archetypes.Length)];
        }

        private DraftPosition GetSecondaryPosition(DraftPosition primary)
        {
            return primary switch
            {
                DraftPosition.PointGuard => DraftPosition.ShootingGuard,
                DraftPosition.ShootingGuard => UnityEngine.Random.value > 0.5f ? DraftPosition.PointGuard : DraftPosition.SmallForward,
                DraftPosition.SmallForward => UnityEngine.Random.value > 0.5f ? DraftPosition.ShootingGuard : DraftPosition.PowerForward,
                DraftPosition.PowerForward => UnityEngine.Random.value > 0.5f ? DraftPosition.SmallForward : DraftPosition.Center,
                DraftPosition.Center => DraftPosition.PowerForward,
                _ => primary
            };
        }

        private string GetRandomInternationalCountry()
        {
            var countries = new[] { "France", "Australia", "Spain", "Serbia", "Germany", "Canada", "Greece", "Slovenia", "Nigeria", "Cameroon" };
            return countries[UnityEngine.Random.Range(0, countries.Length)];
        }

        /// <summary>
        /// Get all prospects
        /// </summary>
        public List<DraftProspect> Prospects => prospects;

        /// <summary>
        /// Get all mock drafts
        /// </summary>
        public List<MockDraft> MockDrafts => mockDrafts;

        /// <summary>
        /// Get all big boards
        /// </summary>
        public List<BigBoard> BigBoards => bigBoards;

        /// <summary>
        /// Get prospect by ID
        /// </summary>
        public DraftProspect GetProspect(string prospectId)
        {
            return prospects.FirstOrDefault(p => p.ProspectId == prospectId);
        }

        /// <summary>
        /// Get draft class strength
        /// </summary>
        public DraftClassStrength ClassStrength => classStrength;
    }
}
