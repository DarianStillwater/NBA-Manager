using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Generates random draft prospects with realistic attributes.
    /// </summary>
    public class ProspectGenerator
    {
        private System.Random _rng;
        
        // Name pools for random generation
        private static readonly string[] FirstNames = {
            "James", "Marcus", "DeShawn", "Tyler", "Brandon", "Kevin", "Chris", "Anthony",
            "Jaylen", "Jalen", "Cam", "Zion", "Trae", "Luka", "Ja", "LaMelo", "Cade",
            "Evan", "Scottie", "Paolo", "Victor", "Scoot", "Chet", "Jabari", "Keegan",
            "Jordan", "Michael", "Kobe", "LeBron", "Dwyane", "Carmelo", "Russell", "Stephen",
            "Damian", "Kyrie", "Kawhi", "Giannis", "Joel", "Nikola", "Donovan", "Jayson",
            "Devin", "Zach", "Bam", "Jimmy", "Darius", "Coby", "Tyrese", "Immanuel",
            "RJ", "Keldon", "Desmond", "Cole", "Austin", "Jaden", "Franz", "Alperen",
            "Ayo", "Bones", "Herb", "Ziaire", "Josh", "Tre", "Davion", "Moses",
            "Trey", "Jonathan", "Bennedict", "Kai", "JT", "Max", "Wendell", "Patrick"
        };
        
        private static readonly string[] LastNames = {
            "Williams", "Johnson", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor",
            "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson", "Garcia",
            "Robinson", "Clark", "Lewis", "Lee", "Walker", "Hall", "Allen", "Young",
            "King", "Wright", "Scott", "Green", "Baker", "Adams", "Nelson", "Hill",
            "Mitchell", "Roberts", "Carter", "Phillips", "Evans", "Turner", "Torres", "Parker",
            "Collins", "Edwards", "Stewart", "Morris", "Murphy", "Rogers", "Reed", "Cook",
            "Morgan", "Bell", "Cooper", "Richardson", "Barnes", "Cunningham", "Holmgren", "Banchero",
            "Wembanyama", "Henderson", "Murray", "Ivey", "Mathurin", "Sharpe", "Sochan", "Griffin"
        };
        
        private static readonly string[] CollegeNames = {
            "Duke", "Kentucky", "Kansas", "North Carolina", "UCLA", "Gonzaga", "Michigan",
            "Arizona", "Villanova", "UConn", "Baylor", "Texas", "Auburn", "Alabama",
            "Tennessee", "Arkansas", "Ohio State", "Michigan State", "Indiana", "Louisville",
            "Florida", "Georgetown", "Syracuse", "Memphis", "LSU", "Purdue", "Virginia",
            "Houston", "Creighton", "Marquette", "San Diego State", "Miami", "Illinois"
        };
        
        private static readonly string[] InternationalCountries = {
            "France", "Spain", "Serbia", "Slovenia", "Germany", "Australia", "Canada",
            "Greece", "Nigeria", "Cameroon", "Turkey", "Lithuania", "Croatia", "Italy",
            "New Zealand", "Brazil", "Argentina", "Dominican Republic", "Puerto Rico", "Japan"
        };

        public ProspectGenerator(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        // ==================== MAIN GENERATION ====================

        /// <summary>
        /// Generates a full draft class of 120 prospects.
        /// </summary>
        public List<DraftProspect> GenerateDraftClass(int draftYear)
        {
            var prospects = new List<DraftProspect>();
            
            // Generate different tiers
            // Tier 1: Elite prospects (projected top 5) - ~5 players
            for (int i = 0; i < 5; i++)
                prospects.Add(GenerateProspect(draftYear, ProspectTier.Elite));
            
            // Tier 2: Lottery prospects (6-14) - ~9 players
            for (int i = 0; i < 9; i++)
                prospects.Add(GenerateProspect(draftYear, ProspectTier.Lottery));
            
            // Tier 3: First round (15-30) - ~16 players
            for (int i = 0; i < 16; i++)
                prospects.Add(GenerateProspect(draftYear, ProspectTier.FirstRound));
            
            // Tier 4: Second round (31-60) - ~30 players
            for (int i = 0; i < 30; i++)
                prospects.Add(GenerateProspect(draftYear, ProspectTier.SecondRound));
            
            // Tier 5: Undrafted/G-League - ~60 players
            for (int i = 0; i < 60; i++)
                prospects.Add(GenerateProspect(draftYear, ProspectTier.Undrafted));
            
            // Assign unique IDs and mock draft positions
            for (int i = 0; i < prospects.Count; i++)
            {
                prospects[i].ProspectId = $"PROSPECT_{draftYear}_{i:D3}";
            }
            
            // Sort by overall potential for mock draft order
            prospects = prospects.OrderByDescending(p => p.ProjectedOverall + p.Potential * 0.5f).ToList();
            for (int i = 0; i < prospects.Count; i++)
            {
                prospects[i].MockDraftPosition = i + 1;
            }
            
            return prospects;
        }

        /// <summary>
        /// Generates a single prospect based on tier.
        /// </summary>
        public DraftProspect GenerateProspect(int draftYear, ProspectTier tier)
        {
            var position = GeneratePosition();
            var physicals = GeneratePhysicals(position);
            var ratings = GenerateRatings(tier);
            var potential = GeneratePotential(tier);
            
            bool isInternational = _rng.NextDouble() < 0.15; // 15% international
            
            var prospect = new DraftProspect
            {
                DraftYear = draftYear,
                FirstName = FirstNames[_rng.Next(FirstNames.Length)],
                LastName = LastNames[_rng.Next(LastNames.Length)],
                Position = position,
                SecondaryPosition = GetSecondaryPosition(position),
                Age = GenerateAge(isInternational),
                
                // Physical attributes
                Height = physicals.height,
                Weight = physicals.weight,
                Wingspan = physicals.wingspan,
                
                // Background
                College = isInternational ? null : CollegeNames[_rng.Next(CollegeNames.Length)],
                Country = isInternational ? InternationalCountries[_rng.Next(InternationalCountries.Length)] : "USA",
                IsInternational = isInternational,
                
                // Tier and potential
                Tier = tier,
                Potential = potential,
                Ceiling = GenerateCeiling(tier, potential),
                Floor = GenerateFloor(tier, potential),
                BustProbability = GenerateBustProbability(tier),
                
                // Skill ratings (with uncertainty)
                Scoring = ratings.scoring,
                Shooting = ratings.shooting,
                Playmaking = ratings.playmaking,
                Defense = ratings.defense,
                Rebounding = ratings.rebounding,
                Athleticism = ratings.athleticism,
                BBIQ = ratings.bbiq,
                
                // Calculated overall
                ProjectedOverall = CalculateOverall(ratings),
                
                // Personality
                Personality = Personality.GenerateRandom(_rng),
                
                // Scouting
                ScoutingLevel = 0 // Unknown until scouted
            };
            
            return prospect;
        }

        // ==================== ATTRIBUTE GENERATION ====================

        private Position GeneratePosition()
        {
            // Distribution weighted toward wings
            int roll = _rng.Next(100);
            if (roll < 15) return Position.PointGuard;      // 15%
            if (roll < 35) return Position.ShootingGuard;   // 20%
            if (roll < 55) return Position.SmallForward;    // 20%
            if (roll < 80) return Position.PowerForward;    // 25%
            return Position.Center;                          // 20%
        }

        private Position? GetSecondaryPosition(Position primary)
        {
            if (_rng.NextDouble() < 0.3) return null; // 30% no secondary
            
            return primary switch
            {
                Position.PointGuard => Position.ShootingGuard,
                Position.ShootingGuard => _rng.NextDouble() < 0.5 ? Position.PointGuard : Position.SmallForward,
                Position.SmallForward => _rng.NextDouble() < 0.5 ? Position.ShootingGuard : Position.PowerForward,
                Position.PowerForward => _rng.NextDouble() < 0.5 ? Position.SmallForward : Position.Center,
                Position.Center => Position.PowerForward,
                _ => null
            };
        }

        private (int height, int weight, int wingspan) GeneratePhysicals(Position position)
        {
            // Height in inches, weight in lbs, wingspan in inches
            return position switch
            {
                Position.PointGuard => (
                    72 + _rng.Next(6),   // 6'0" - 6'5"
                    170 + _rng.Next(30),  // 170-200 lbs
                    72 + _rng.Next(10)    // Wingspan
                ),
                Position.ShootingGuard => (
                    75 + _rng.Next(5),   // 6'3" - 6'7"
                    185 + _rng.Next(30),
                    77 + _rng.Next(8)
                ),
                Position.SmallForward => (
                    78 + _rng.Next(5),   // 6'6" - 6'10"
                    200 + _rng.Next(30),
                    80 + _rng.Next(8)
                ),
                Position.PowerForward => (
                    80 + _rng.Next(5),   // 6'8" - 7'0"
                    220 + _rng.Next(35),
                    83 + _rng.Next(8)
                ),
                Position.Center => (
                    82 + _rng.Next(6),   // 6'10" - 7'3"
                    240 + _rng.Next(40),
                    86 + _rng.Next(8)
                ),
                _ => (78, 200, 80)
            };
        }

        private int GenerateAge(bool isInternational)
        {
            // US players: 19-22, International: 18-25
            if (isInternational)
                return 18 + _rng.Next(8);
            return 19 + _rng.Next(4);
        }

        private (int scoring, int shooting, int playmaking, int defense, int rebounding, int athleticism, int bbiq) GenerateRatings(ProspectTier tier)
        {
            var (min, max) = tier switch
            {
                ProspectTier.Elite => (65, 85),
                ProspectTier.Lottery => (55, 75),
                ProspectTier.FirstRound => (45, 70),
                ProspectTier.SecondRound => (35, 60),
                ProspectTier.Undrafted => (25, 50),
                _ => (30, 50)
            };
            
            int Range() => min + _rng.Next(max - min);
            
            return (Range(), Range(), Range(), Range(), Range(), Range(), Range());
        }

        private int GeneratePotential(ProspectTier tier)
        {
            return tier switch
            {
                ProspectTier.Elite => 85 + _rng.Next(15),      // 85-99
                ProspectTier.Lottery => 75 + _rng.Next(15),    // 75-89
                ProspectTier.FirstRound => 65 + _rng.Next(15), // 65-79
                ProspectTier.SecondRound => 50 + _rng.Next(20),// 50-69
                ProspectTier.Undrafted => 35 + _rng.Next(25),  // 35-59
                _ => 50
            };
        }

        private int GenerateCeiling(ProspectTier tier, int potential)
        {
            // Ceiling can exceed potential but with diminishing returns
            int ceilingBonus = _rng.Next(10);
            if (tier == ProspectTier.Elite && _rng.NextDouble() < 0.2)
                ceilingBonus += 5; // Some elite prospects have superstar ceilings
            
            return Math.Min(99, potential + ceilingBonus);
        }

        private int GenerateFloor(ProspectTier tier, int potential)
        {
            int floorPenalty = _rng.Next(15);
            return Math.Max(25, potential - floorPenalty);
        }

        private float GenerateBustProbability(ProspectTier tier)
        {
            return tier switch
            {
                ProspectTier.Elite => 0.05f + (float)_rng.NextDouble() * 0.10f,       // 5-15%
                ProspectTier.Lottery => 0.10f + (float)_rng.NextDouble() * 0.15f,     // 10-25%
                ProspectTier.FirstRound => 0.15f + (float)_rng.NextDouble() * 0.20f,  // 15-35%
                ProspectTier.SecondRound => 0.30f + (float)_rng.NextDouble() * 0.30f, // 30-60%
                ProspectTier.Undrafted => 0.50f + (float)_rng.NextDouble() * 0.40f,   // 50-90%
                _ => 0.5f
            };
        }

        private int CalculateOverall(
            (int scoring, int shooting, int playmaking, int defense, int rebounding, int athleticism, int bbiq) ratings)
        {
            // Weighted average
            float overall = 
                ratings.scoring * 0.20f +
                ratings.shooting * 0.15f +
                ratings.playmaking * 0.15f +
                ratings.defense * 0.15f +
                ratings.rebounding * 0.10f +
                ratings.athleticism * 0.15f +
                ratings.bbiq * 0.10f;
            
            return Mathf.RoundToInt(overall);
        }
    }

    // ==================== PROSPECT DATA ====================

    [Serializable]
    public class DraftProspect
    {
        public string ProspectId;
        public int DraftYear;
        public int MockDraftPosition;
        
        // Identity
        public string FirstName;
        public string LastName;
        public string FullName => $"{FirstName} {LastName}";
        public int Age;
        
        // Physical
        public Position Position;
        public Position? SecondaryPosition;
        public int Height; // inches
        public int Weight; // lbs
        public int Wingspan; // inches
        public string HeightDisplay => $"{Height / 12}'{Height % 12}\"";
        
        // Background
        public string College;
        public string Country;
        public bool IsInternational;
        
        // Tier and Potential
        public ProspectTier Tier;
        public int Potential;   // Hidden true potential
        public int Ceiling;     // Best case scenario
        public int Floor;       // Worst case scenario
        public float BustProbability;
        
        // Skill Ratings (current - will grow to potential)
        public int Scoring;
        public int Shooting;
        public int Playmaking;
        public int Defense;
        public int Rebounding;
        public int Athleticism;
        public int BBIQ;
        public int ProjectedOverall;
        
        // Personality
        public Personality Personality;
        
        // Scouting (0-100, higher = more accurate intel)
        public int ScoutingLevel;
    }

    public enum ProspectTier
    {
        Elite,       // Projected top 5
        Lottery,     // Projected 6-14
        FirstRound,  // Projected 15-30
        SecondRound, // Projected 31-60
        Undrafted    // Unlikely to be drafted
    }

    public enum Position
    {
        PointGuard,
        ShootingGuard,
        SmallForward,
        PowerForward,
        Center
    }
}
