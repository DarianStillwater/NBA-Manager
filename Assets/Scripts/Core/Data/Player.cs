using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Complete player data model with all attributes for simulation.
    /// Attributes range from 0-100 where 50 is league average.
    /// NOTE: All skill attributes are HIDDEN from the player - only accessible via scouting reports.
    /// </summary>
    [Serializable]
    public class Player
    {
        // ==================== BIOGRAPHICAL DATA ====================
        [Header("Biographical")]
        public string PlayerId;
        public string FirstName;
        public string LastName;
        public string JerseyNumber;
        public Position Position;
        public DateTime BirthDate;  // Store birthdate, calculate age dynamically
        public int YearsPro; // 0 = Rookie
        public float HeightInches; // e.g., 78 = 6'6"
        public float WeightLbs;
        public string SkinToneCode; // For 3D model customization (e.g., "TONE_03")
        public string HairStyle;
        public string TeamId;

        /// <summary>
        /// Calculates current age based on BirthDate and game's current date.
        /// Falls back to system date if GameManager not available.
        /// </summary>
        public int Age
        {
            get
            {
                DateTime referenceDate = GameManager.Instance?.CurrentDate ?? DateTime.Now;
                int age = referenceDate.Year - BirthDate.Year;
                // Adjust if birthday hasn't occurred yet this year
                if (BirthDate.Date > referenceDate.AddYears(-age))
                    age--;
                return Math.Max(0, age);
            }
        }

        /// <summary>
        /// Birthdate formatted as string (e.g., "September 14, 1998").
        /// Matches official NBA bio format.
        /// </summary>
        public string BirthDateFormatted => BirthDate.ToString("MMMM d, yyyy");

        /// <summary>
        /// Birthdate in short format (e.g., "09/14/1998").
        /// </summary>
        public string BirthDateShort => BirthDate.ToString("MM/dd/yyyy");

        // ==================== HIDDEN POTENTIAL SYSTEM ====================
        [Header("Development (Hidden)")]
        [HideInInspector] [Range(0, 100)] public int HiddenPotential;     // Caps maximum growth
        [HideInInspector] [Range(25, 34)] public int PeakAge;             // When they'll be at their best
        [HideInInspector] [Range(0, 100)] public int DeclineRate;         // How fast they decline (higher = faster)
        [HideInInspector] [Range(0, 100)] public int InjuryProneness;     // Likelihood of injury
        [HideInInspector] public int InjuryHistoryCount;                  // Past injuries affect decline

        // ==================== OFFENSIVE ATTRIBUTES (HIDDEN) ====================
        [Header("Offense - Scoring")]
        [HideInInspector] [Range(0, 100)] public int Finishing_Rim;        // Layups, dunks at rim
        [HideInInspector] [Range(0, 100)] public int Finishing_PostMoves;  // Back-to-basket scoring
        [HideInInspector] [Range(0, 100)] public int Shot_Close;           // Floaters, close shots
        [HideInInspector] [Range(0, 100)] public int Shot_MidRange;        // Pull-up jumpers
        [HideInInspector] [Range(0, 100)] public int Shot_Three;           // 3-point shooting
        [HideInInspector] [Range(0, 100)] public int FreeThrow;            // Free throw accuracy

        [Header("Offense - Playmaking")]
        [HideInInspector] [Range(0, 100)] public int Passing;              // Pass accuracy & vision
        [HideInInspector] [Range(0, 100)] public int BallHandling;         // Dribble moves, control
        [HideInInspector] [Range(0, 100)] public int OffensiveIQ;          // Reads, positioning
        [HideInInspector] [Range(0, 100)] public int SpeedWithBall;        // Movement while dribbling

        // ==================== DEFENSIVE ATTRIBUTES (HIDDEN) ====================
        [Header("Defense")]
        [HideInInspector] [Range(0, 100)] public int Defense_Perimeter;    // Guarding wings/guards
        [HideInInspector] [Range(0, 100)] public int Defense_Interior;     // Protecting the paint
        [HideInInspector] [Range(0, 100)] public int Defense_PostDefense;  // Guarding back-to-basket
        [HideInInspector] [Range(0, 100)] public int Steal;                // Ball-hawking ability
        [HideInInspector] [Range(0, 100)] public int Block;                // Shot-blocking
        [HideInInspector] [Range(0, 100)] public int DefensiveIQ;          // Help defense, rotations
        [HideInInspector] [Range(0, 100)] public int DefensiveRebound;     // Boxing out, grabbing boards

        // ==================== PHYSICAL ATTRIBUTES (HIDDEN) ====================
        [Header("Physical")]
        [HideInInspector] [Range(0, 100)] public int Speed;                // Foot speed without ball
        [HideInInspector] [Range(0, 100)] public int Acceleration;         // First step quickness
        [HideInInspector] [Range(0, 100)] public int Strength;             // Body control, physicality
        [HideInInspector] [Range(0, 100)] public int Vertical;             // Leaping ability
        [HideInInspector] [Range(0, 100)] public int Stamina;              // How long before fatigue
        [HideInInspector] [Range(0, 100)] public int Durability;           // Injury resistance
        [HideInInspector] [Range(0, 100)] public int Wingspan;             // Relative to height (affects contests)

        // ==================== MENTAL ATTRIBUTES (HIDDEN) ====================
        [Header("Mental")]
        [HideInInspector] [Range(0, 100)] public int BasketballIQ;         // Overall court awareness
        [HideInInspector] [Range(0, 100)] public int Clutch;               // Performance in big moments
        [HideInInspector] [Range(0, 100)] public int Consistency;          // Game-to-game variance
        [HideInInspector] [Range(0, 100)] public int WorkEthic;            // Training improvement rate
        [HideInInspector] [Range(0, 100)] public int Coachability;         // Responds to adjustments

        // ==================== PERSONALITY TRAITS (HIDDEN) ====================
        [Header("Personality")]
        [HideInInspector] [Range(0, 100)] public int Ego;                  // Demands shots, attention
        [HideInInspector] [Range(0, 100)] public int Leadership;           // Boosts teammates
        [HideInInspector] [Range(0, 100)] public int Composure;            // Handles pressure
        [HideInInspector] [Range(0, 100)] public int Aggression;           // Playing style intensity

        // ==================== DYNAMIC STATE (Changes during game) ====================
        [Header("Current State")]
        [Range(0, 100)] public float Energy;             // Depletes during play
        [Range(0, 100)] public float Morale;             // Affected by game events
        [Range(0, 100)] public float Form;               // Hot/cold streak

        // ==================== INJURY STATE ====================
        [Header("Injury Status")]
        public bool IsInjured;
        public InjuryType CurrentInjuryType;             // Enum-based injury type
        public InjurySeverity CurrentInjurySeverity;     // Severity of current injury
        public int InjuryDaysRemaining;
        public int OriginalInjuryDays;                   // Original recovery time
        public DateTime InjuryDate;                      // When injury occurred

        /// <summary>
        /// History of injuries to each body part for re-injury risk calculation.
        /// </summary>
        public List<InjuryHistory> InjuryHistoryList = new List<InjuryHistory>();

        /// <summary>
        /// Recent minutes played for load management (last 7 days).
        /// </summary>
        public List<MinutesRecord> RecentMinutes = new List<MinutesRecord>();

        /// <summary>
        /// Legacy string property for backward compatibility.
        /// </summary>
        [Obsolete("Use CurrentInjuryType enum instead")]
        public string InjuryType
        {
            get => CurrentInjuryType.ToString();
            set
            {
                if (Enum.TryParse<InjuryType>(value, out var parsed))
                    CurrentInjuryType = parsed;
            }
        }

        // ==================== DEVELOPMENT TRACKING ====================
        [Header("Development")]
        public int MinutesPlayedThisSeason;
        public int GamesPlayedThisSeason;
        public string DevelopmentFocus;                   // Current focus area
        public List<DevelopmentLog> DevelopmentHistory = new List<DevelopmentLog>();

        // ==================== COMPUTED PROPERTIES ====================
        public string FullName => $"{FirstName} {LastName}";

        /// <summary>
        /// Height formatted as feet and inches (e.g., "6'2"" for 74 inches).
        /// Matches official NBA bio format.
        /// </summary>
        public string HeightFormatted
        {
            get
            {
                int feet = (int)(HeightInches / 12);
                int inches = (int)(HeightInches % 12);
                return $"{feet}'{inches}\"";
            }
        }

        /// <summary>
        /// Height formatted with metric equivalent (e.g., "6'2\" (1.88m)").
        /// </summary>
        public string HeightWithMetric
        {
            get
            {
                int feet = (int)(HeightInches / 12);
                int inches = (int)(HeightInches % 12);
                float meters = HeightInches * 0.0254f;
                return $"{feet}'{inches}\" ({meters:F2}m)";
            }
        }

        /// <summary>
        /// Alias for OverallRating for UI compatibility
        /// </summary>
        public int Overall => OverallRating;

        /// <summary>
        /// Position as string for UI compatibility
        /// </summary>
        public string PositionString => Position switch
        {
            Position.PointGuard => "PG",
            Position.ShootingGuard => "SG",
            Position.SmallForward => "SF",
            Position.PowerForward => "PF",
            Position.Center => "C",
            _ => "??"
        };

        // ==================== UI ATTRIBUTE HELPERS ====================
        // These provide simplified attribute access for UI panels

        /// <summary>Simplified inside scoring for UI</summary>
        public int InsideScoring => (Finishing_Rim + Finishing_PostMoves + Shot_Close) / 3;

        /// <summary>Simplified mid-range scoring for UI</summary>
        public int MidRangeScoring => Shot_MidRange;

        /// <summary>Simplified three-point shooting for UI</summary>
        public int ThreePointScoring => Shot_Three;

        /// <summary>Simplified perimeter defense for UI</summary>
        public int PerimeterDefense => Defense_Perimeter;

        /// <summary>Simplified interior defense for UI</summary>
        public int InteriorDefense => Defense_Interior;

        /// <summary>Simplified rebounding for UI</summary>
        public int Rebounding => DefensiveRebound;

        /// <summary>Simplified athleticism for UI</summary>
        public int Athleticism => (Speed + Acceleration + Vertical + Strength) / 4;

        /// <summary>Free throw ability</summary>
        public int FreeThrowAbility => FreeThrow;

        // ==================== CONTRACT ACCESS ====================
        // Contract is managed externally by SalaryCapManager, but we provide access here for UI

        /// <summary>
        /// Gets the player's current contract from SalaryCapManager.
        /// Returns null if no contract found.
        /// </summary>
        public Contract CurrentContract
        {
            get
            {
                // Access via GameManager singleton
                var salaryMgr = NBAHeadCoach.Core.GameManager.Instance?.SalaryCapManager;
                return salaryMgr?.GetContract(PlayerId);
            }
        }

        /// <summary>
        /// Gets the player's annual salary (shortcut).
        /// Returns 0 if no contract.
        /// </summary>
        public long AnnualSalary => CurrentContract?.CurrentYearSalary ?? 0;

        /// <summary>
        /// Position-weighted overall rating. Used internally for simulation.
        /// </summary>
        public int OverallRating
        {
            get
            {
                float score = Position switch
                {
                    Position.PointGuard => (BallHandling * 0.2f + Passing * 0.2f + Shot_Three * 0.15f +
                                           Speed * 0.15f + Defense_Perimeter * 0.15f + BasketballIQ * 0.15f),
                    Position.ShootingGuard => (Shot_Three * 0.2f + Shot_MidRange * 0.15f + Finishing_Rim * 0.15f +
                                              Defense_Perimeter * 0.2f + BallHandling * 0.15f + Speed * 0.15f),
                    Position.SmallForward => (Shot_Three * 0.15f + Finishing_Rim * 0.2f + Defense_Perimeter * 0.15f +
                                             Defense_Interior * 0.15f + Speed * 0.15f + Vertical * 0.1f + Strength * 0.1f),
                    Position.PowerForward => (Finishing_Rim * 0.2f + Defense_Interior * 0.2f + DefensiveRebound * 0.15f +
                                             Shot_MidRange * 0.15f + Strength * 0.15f + Block * 0.15f),
                    Position.Center => (Defense_Interior * 0.2f + Block * 0.2f + DefensiveRebound * 0.2f +
                                       Finishing_Rim * 0.2f + Strength * 0.1f + Finishing_PostMoves * 0.1f),
                    _ => 50
                };
                return Mathf.RoundToInt(score);
            }
        }

        /// <summary>
        /// Development phase based on age.
        /// </summary>
        public DevelopmentPhase CurrentDevelopmentPhase
        {
            get
            {
                if (Age <= 22) return DevelopmentPhase.PrimeDevelopment;
                if (Age <= 26) return DevelopmentPhase.ContinuedDevelopment;
                if (Age <= PeakAge) return DevelopmentPhase.Peak;
                if (Age <= 34) return DevelopmentPhase.EarlyDecline;
                return DevelopmentPhase.SteepDecline;
            }
        }

        /// <summary>
        /// Development rate multiplier based on age and phase.
        /// </summary>
        public float DevelopmentMultiplier
        {
            get
            {
                return CurrentDevelopmentPhase switch
                {
                    DevelopmentPhase.PrimeDevelopment => 1.5f,      // 19-22: Highest growth
                    DevelopmentPhase.ContinuedDevelopment => 1.0f,  // 23-26: Normal growth
                    DevelopmentPhase.Peak => 0.3f,                  // 27-30: Minimal growth
                    DevelopmentPhase.EarlyDecline => 0f,            // 31-34: No growth
                    DevelopmentPhase.SteepDecline => 0f,            // 35+: No growth
                    _ => 0.5f
                };
            }
        }

        /// <summary>
        /// Decline rate multiplier based on age and injury history.
        /// </summary>
        public float DeclineMultiplier
        {
            get
            {
                float baseDecline = CurrentDevelopmentPhase switch
                {
                    DevelopmentPhase.PrimeDevelopment => 0f,
                    DevelopmentPhase.ContinuedDevelopment => 0f,
                    DevelopmentPhase.Peak => 0.1f,      // Slight maintenance decline
                    DevelopmentPhase.EarlyDecline => 0.5f,
                    DevelopmentPhase.SteepDecline => 1.0f,
                    _ => 0f
                };

                // Injury history accelerates decline
                float injuryModifier = 1f + (InjuryHistoryCount * 0.1f);

                // DeclineRate attribute affects speed
                float personalDecline = DeclineRate / 100f;

                return baseDecline * injuryModifier * (0.5f + personalDecline);
            }
        }

        /// <summary>
        /// Potential alias for UI compatibility (maps to HiddenPotential).
        /// </summary>
        public int Potential => HiddenPotential;

        /// <summary>
        /// How much room for growth this player has.
        /// </summary>
        public int GrowthRoom
        {
            get
            {
                if (CurrentDevelopmentPhase >= DevelopmentPhase.EarlyDecline)
                    return 0;

                return Math.Max(0, HiddenPotential - OverallRating);
            }
        }

        // ==================== ATTRIBUTE ACCESS (For Simulation Only) ====================

        /// <summary>
        /// Gets an attribute value for simulation. Should only be called by simulation code.
        /// UI should use scouting reports instead.
        /// </summary>
        public int GetAttributeForSimulation(PlayerAttribute attribute)
        {
            return attribute switch
            {
                PlayerAttribute.Finishing_Rim => Finishing_Rim,
                PlayerAttribute.Finishing_PostMoves => Finishing_PostMoves,
                PlayerAttribute.Shot_Close => Shot_Close,
                PlayerAttribute.Shot_MidRange => Shot_MidRange,
                PlayerAttribute.Shot_Three => Shot_Three,
                PlayerAttribute.FreeThrow => FreeThrow,
                PlayerAttribute.Passing => Passing,
                PlayerAttribute.BallHandling => BallHandling,
                PlayerAttribute.OffensiveIQ => OffensiveIQ,
                PlayerAttribute.SpeedWithBall => SpeedWithBall,
                PlayerAttribute.Defense_Perimeter => Defense_Perimeter,
                PlayerAttribute.Defense_Interior => Defense_Interior,
                PlayerAttribute.Defense_PostDefense => Defense_PostDefense,
                PlayerAttribute.Steal => Steal,
                PlayerAttribute.Block => Block,
                PlayerAttribute.DefensiveIQ => DefensiveIQ,
                PlayerAttribute.DefensiveRebound => DefensiveRebound,
                PlayerAttribute.Speed => Speed,
                PlayerAttribute.Acceleration => Acceleration,
                PlayerAttribute.Strength => Strength,
                PlayerAttribute.Vertical => Vertical,
                PlayerAttribute.Stamina => Stamina,
                PlayerAttribute.Durability => Durability,
                PlayerAttribute.Wingspan => Wingspan,
                PlayerAttribute.BasketballIQ => BasketballIQ,
                PlayerAttribute.Clutch => Clutch,
                PlayerAttribute.Consistency => Consistency,
                PlayerAttribute.WorkEthic => WorkEthic,
                PlayerAttribute.Coachability => Coachability,
                PlayerAttribute.Ego => Ego,
                PlayerAttribute.Leadership => Leadership,
                PlayerAttribute.Composure => Composure,
                PlayerAttribute.Aggression => Aggression,
                _ => 50
            };
        }

        /// <summary>
        /// Sets an attribute value. Used by development system.
        /// </summary>
        internal void SetAttribute(PlayerAttribute attribute, int value)
        {
            value = Mathf.Clamp(value, 0, 100);

            switch (attribute)
            {
                case PlayerAttribute.Finishing_Rim: Finishing_Rim = value; break;
                case PlayerAttribute.Finishing_PostMoves: Finishing_PostMoves = value; break;
                case PlayerAttribute.Shot_Close: Shot_Close = value; break;
                case PlayerAttribute.Shot_MidRange: Shot_MidRange = value; break;
                case PlayerAttribute.Shot_Three: Shot_Three = value; break;
                case PlayerAttribute.FreeThrow: FreeThrow = value; break;
                case PlayerAttribute.Passing: Passing = value; break;
                case PlayerAttribute.BallHandling: BallHandling = value; break;
                case PlayerAttribute.OffensiveIQ: OffensiveIQ = value; break;
                case PlayerAttribute.SpeedWithBall: SpeedWithBall = value; break;
                case PlayerAttribute.Defense_Perimeter: Defense_Perimeter = value; break;
                case PlayerAttribute.Defense_Interior: Defense_Interior = value; break;
                case PlayerAttribute.Defense_PostDefense: Defense_PostDefense = value; break;
                case PlayerAttribute.Steal: Steal = value; break;
                case PlayerAttribute.Block: Block = value; break;
                case PlayerAttribute.DefensiveIQ: DefensiveIQ = value; break;
                case PlayerAttribute.DefensiveRebound: DefensiveRebound = value; break;
                case PlayerAttribute.Speed: Speed = value; break;
                case PlayerAttribute.Acceleration: Acceleration = value; break;
                case PlayerAttribute.Strength: Strength = value; break;
                case PlayerAttribute.Vertical: Vertical = value; break;
                case PlayerAttribute.Stamina: Stamina = value; break;
                case PlayerAttribute.Durability: Durability = value; break;
                case PlayerAttribute.Wingspan: Wingspan = value; break;
                case PlayerAttribute.BasketballIQ: BasketballIQ = value; break;
                case PlayerAttribute.Clutch: Clutch = value; break;
                case PlayerAttribute.Consistency: Consistency = value; break;
                case PlayerAttribute.WorkEthic: WorkEthic = value; break;
                case PlayerAttribute.Coachability: Coachability = value; break;
                case PlayerAttribute.Ego: Ego = value; break;
                case PlayerAttribute.Leadership: Leadership = value; break;
                case PlayerAttribute.Composure: Composure = value; break;
                case PlayerAttribute.Aggression: Aggression = value; break;
            }
        }

        /// <summary>
        /// Modifies an attribute by a delta amount.
        /// </summary>
        internal void ModifyAttribute(PlayerAttribute attribute, int delta)
        {
            int current = GetAttributeForSimulation(attribute);
            SetAttribute(attribute, current + delta);
        }

        // ==================== CONDITION METHODS ====================

        /// <summary>
        /// Returns shooting percentage modifier based on energy and morale.
        /// </summary>
        public float GetConditionModifier()
        {
            float energyMod = Mathf.Lerp(0.7f, 1.0f, Energy / 100f);
            float moraleMod = Mathf.Lerp(0.9f, 1.1f, Morale / 100f);
            float formMod = Mathf.Lerp(0.85f, 1.15f, Form / 100f);
            return energyMod * moraleMod * formMod;
        }

        /// <summary>
        /// Depletes energy based on action intensity.
        /// </summary>
        public void ConsumeEnergy(float amount)
        {
            float staminaMultiplier = Mathf.Lerp(1.5f, 0.5f, Stamina / 100f);
            Energy = Mathf.Max(0, Energy - (amount * staminaMultiplier));
        }

        /// <summary>
        /// Restores energy (called when player is on bench).
        /// </summary>
        public void RestoreEnergy(float amount)
        {
            Energy = Mathf.Min(100, Energy + amount);
        }

        /// <summary>
        /// Adjusts morale based on event.
        /// </summary>
        public void AdjustMorale(float amount)
        {
            Morale = Mathf.Clamp(Morale + amount, 0, 100);
        }

        // ==================== INJURY METHODS ====================

        /// <summary>
        /// Get current injury status for game day availability.
        /// </summary>
        public InjuryStatus CurrentInjuryStatus
        {
            get
            {
                if (!IsInjured || InjuryDaysRemaining <= 0)
                    return InjuryStatus.Healthy;
                if (InjuryDaysRemaining >= 5)
                    return InjuryStatus.Out;
                if (InjuryDaysRemaining >= 3)
                    return InjuryStatus.Doubtful;
                if (InjuryDaysRemaining >= 1)
                    return InjuryStatus.Questionable;
                return InjuryStatus.Probable;
            }
        }

        /// <summary>
        /// Get total minutes played in the last N days.
        /// </summary>
        public int GetMinutesInLastDays(int days)
        {
            if (RecentMinutes == null) return 0;
            var cutoff = GameManager.Instance?.CurrentDate.AddDays(-days) ?? DateTime.Now.AddDays(-days);
            return RecentMinutes
                .Where(r => r.Date >= cutoff)
                .Sum(r => r.Minutes);
        }

        /// <summary>
        /// Record minutes played for load management tracking.
        /// </summary>
        public void RecordMinutesPlayed(int minutes, DateTime date)
        {
            RecentMinutes ??= new List<MinutesRecord>();
            RecentMinutes.Add(new MinutesRecord { Date = date, Minutes = minutes });

            // Keep only last 14 days of data
            var cutoff = date.AddDays(-14);
            RecentMinutes.RemoveAll(r => r.Date < cutoff);
        }

        /// <summary>
        /// Get re-injury risk modifier for a specific body part.
        /// </summary>
        public float GetReInjuryRisk(InjuryType injuryType)
        {
            if (InjuryHistoryList == null) return 0f;

            float totalRisk = 0f;
            foreach (var history in InjuryHistoryList)
            {
                if (InjuryRecoveryTables.AffectsSameArea(history.BodyPart, injuryType))
                {
                    totalRisk += history.ReInjuryRiskModifier;
                }
            }
            return totalRisk;
        }

        /// <summary>
        /// Add an injury to this player's history.
        /// </summary>
        public void AddInjuryToHistory(InjuryType type)
        {
            InjuryHistoryList ??= new List<InjuryHistory>();

            var existing = InjuryHistoryList.FirstOrDefault(h =>
                InjuryRecoveryTables.AffectsSameArea(h.BodyPart, type));

            if (existing != null)
            {
                existing.TimesInjured++;
                existing.LastInjuryDate = GameManager.Instance?.CurrentDate ?? DateTime.Now;
            }
            else
            {
                InjuryHistoryList.Add(new InjuryHistory
                {
                    BodyPart = type,
                    TimesInjured = 1,
                    LastInjuryDate = GameManager.Instance?.CurrentDate ?? DateTime.Now
                });
            }

            // Also increment the legacy counter
            InjuryHistoryCount++;
        }

        /// <summary>
        /// Check if player can play (not Out status).
        /// </summary>
        public bool CanPlay => CurrentInjuryStatus != InjuryStatus.Out;

        /// <summary>
        /// Check if playing this player carries injury risk (Questionable/Doubtful).
        /// </summary>
        public bool HasInjuryRisk => CurrentInjuryStatus == InjuryStatus.Questionable ||
                                      CurrentInjuryStatus == InjuryStatus.Doubtful ||
                                      CurrentInjuryStatus == InjuryStatus.Probable;

        // ==================== HISTORY & AWARDS ====================
        public List<SeasonStats> CareerStats = new List<SeasonStats>();
        public List<AwardHistory> Awards = new List<AwardHistory>();

        public SeasonStats CurrentSeasonStats
        {
            get
            {
                if (CareerStats == null || CareerStats.Count == 0) return null;
                return CareerStats[CareerStats.Count - 1];
            }
        }

        // ==================== INITIALIZATION ====================

        /// <summary>
        /// Initializes hidden development attributes for a new player.
        /// </summary>
        public void InitializeDevelopmentAttributes(System.Random rng = null)
        {
            rng ??= new System.Random();

            // Potential is based on current overall + variance
            int baselineAdd = rng.Next(5, 25);  // 5-25 points above current
            HiddenPotential = Math.Min(100, OverallRating + baselineAdd);

            // Young players have higher potential ceiling
            if (Age <= 22)
            {
                HiddenPotential = Math.Min(100, HiddenPotential + rng.Next(5, 15));
            }

            // Peak age (when they'll be at their best)
            PeakAge = 27 + rng.Next(0, 6);  // 27-32

            // Decline rate varies by player
            DeclineRate = 30 + rng.Next(0, 50);  // 30-79

            // Injury proneness
            InjuryProneness = 20 + rng.Next(0, 60);  // 20-79
            InjuryHistoryCount = 0;
        }

        /// <summary>
        /// Creates a star player with high potential.
        /// </summary>
        public void InitializeAsStarPlayer(System.Random rng = null)
        {
            rng ??= new System.Random();

            HiddenPotential = 90 + rng.Next(0, 11);  // 90-100
            PeakAge = 28 + rng.Next(0, 4);           // 28-31
            DeclineRate = 20 + rng.Next(0, 30);      // 20-49 (slower decline)
            InjuryProneness = 10 + rng.Next(0, 40);  // 10-49 (more durable)
        }
    }

    // ==================== SUPPORTING ENUMS ====================

    public enum Position
    {
        PointGuard = 1,
        ShootingGuard = 2,
        SmallForward = 3,
        PowerForward = 4,
        Center = 5
    }

    public enum DevelopmentPhase
    {
        PrimeDevelopment,       // 19-22: Highest growth potential
        ContinuedDevelopment,   // 23-26: Moderate growth
        Peak,                   // 27-30: Minimal growth, maintain
        EarlyDecline,           // 31-34: Skills start decreasing
        SteepDecline            // 35+: Accelerated decline
    }

    /// <summary>
    /// All player attributes that can be developed or declined.
    /// </summary>
    public enum PlayerAttribute
    {
        // Offensive
        Finishing_Rim,
        Finishing_PostMoves,
        Shot_Close,
        Shot_MidRange,
        Shot_Three,
        FreeThrow,
        Passing,
        BallHandling,
        OffensiveIQ,
        SpeedWithBall,

        // Defensive
        Defense_Perimeter,
        Defense_Interior,
        Defense_PostDefense,
        Steal,
        Block,
        DefensiveIQ,
        DefensiveRebound,

        // Physical
        Speed,
        Acceleration,
        Strength,
        Vertical,
        Stamina,
        Durability,
        Wingspan,

        // Mental
        BasketballIQ,
        Clutch,
        Consistency,
        WorkEthic,
        Coachability,

        // Personality
        Ego,
        Leadership,
        Composure,
        Aggression
    }

    /// <summary>
    /// Attribute categories for development focus.
    /// </summary>
    public enum AttributeCategory
    {
        Scoring,
        Shooting,
        Playmaking,
        PerimeterDefense,
        InteriorDefense,
        Rebounding,
        Physical,
        Mental
    }

    /// <summary>
    /// Utility methods for player-related formatting and conversions.
    /// </summary>
    public static class PlayerUtils
    {
        /// <summary>
        /// Converts height in inches to feet/inches string (e.g., 74 → "6'2"").
        /// </summary>
        public static string FormatHeight(float heightInches)
        {
            int feet = (int)(heightInches / 12);
            int inches = (int)(heightInches % 12);
            return $"{feet}'{inches}\"";
        }

        /// <summary>
        /// Converts height in inches to feet/inches with metric (e.g., 74 → "6'2\" (1.88m)").
        /// </summary>
        public static string FormatHeightWithMetric(float heightInches)
        {
            int feet = (int)(heightInches / 12);
            int inches = (int)(heightInches % 12);
            float meters = heightInches * 0.0254f;
            return $"{feet}'{inches}\" ({meters:F2}m)";
        }

        /// <summary>
        /// Parses a height string (e.g., "6'2\"" or "6-2") to total inches.
        /// </summary>
        public static float ParseHeight(string heightStr)
        {
            if (string.IsNullOrEmpty(heightStr)) return 0;

            // Try "6'2"" format
            var match = System.Text.RegularExpressions.Regex.Match(heightStr, @"(\d+)'(\d+)");
            if (match.Success)
            {
                int feet = int.Parse(match.Groups[1].Value);
                int inches = int.Parse(match.Groups[2].Value);
                return feet * 12 + inches;
            }

            // Try "6-2" format
            match = System.Text.RegularExpressions.Regex.Match(heightStr, @"(\d+)-(\d+)");
            if (match.Success)
            {
                int feet = int.Parse(match.Groups[1].Value);
                int inches = int.Parse(match.Groups[2].Value);
                return feet * 12 + inches;
            }

            return 0;
        }

        /// <summary>
        /// Formats weight for display (e.g., 215 → "215 lbs" or "215 lbs (98 kg)").
        /// </summary>
        public static string FormatWeight(float weightLbs, bool includeMetric = false)
        {
            if (includeMetric)
            {
                float kg = weightLbs * 0.453592f;
                return $"{weightLbs:F0} lbs ({kg:F0} kg)";
            }
            return $"{weightLbs:F0} lbs";
        }
    }

    /// <summary>
    /// Categorizes attributes for development purposes.
    /// </summary>
    public static class PlayerAttributeHelper
    {
        public static AttributeCategory GetCategory(PlayerAttribute attr)
        {
            return attr switch
            {
                PlayerAttribute.Finishing_Rim or
                PlayerAttribute.Finishing_PostMoves or
                PlayerAttribute.Shot_Close => AttributeCategory.Scoring,

                PlayerAttribute.Shot_MidRange or
                PlayerAttribute.Shot_Three or
                PlayerAttribute.FreeThrow => AttributeCategory.Shooting,

                PlayerAttribute.Passing or
                PlayerAttribute.BallHandling or
                PlayerAttribute.OffensiveIQ or
                PlayerAttribute.SpeedWithBall => AttributeCategory.Playmaking,

                PlayerAttribute.Defense_Perimeter or
                PlayerAttribute.Steal or
                PlayerAttribute.DefensiveIQ => AttributeCategory.PerimeterDefense,

                PlayerAttribute.Defense_Interior or
                PlayerAttribute.Defense_PostDefense or
                PlayerAttribute.Block => AttributeCategory.InteriorDefense,

                PlayerAttribute.DefensiveRebound => AttributeCategory.Rebounding,

                PlayerAttribute.Speed or
                PlayerAttribute.Acceleration or
                PlayerAttribute.Strength or
                PlayerAttribute.Vertical or
                PlayerAttribute.Stamina or
                PlayerAttribute.Durability or
                PlayerAttribute.Wingspan => AttributeCategory.Physical,

                _ => AttributeCategory.Mental
            };
        }

        /// <summary>
        /// Gets attributes that decline first with age (physical).
        /// </summary>
        public static PlayerAttribute[] GetFastDeclineAttributes()
        {
            return new[]
            {
                PlayerAttribute.Speed,
                PlayerAttribute.Acceleration,
                PlayerAttribute.Vertical,
                PlayerAttribute.Stamina
            };
        }

        /// <summary>
        /// Gets attributes that decline slowly with age (skill-based).
        /// </summary>
        public static PlayerAttribute[] GetSlowDeclineAttributes()
        {
            return new[]
            {
                PlayerAttribute.Shot_Three,
                PlayerAttribute.Shot_MidRange,
                PlayerAttribute.FreeThrow,
                PlayerAttribute.Passing,
                PlayerAttribute.OffensiveIQ,
                PlayerAttribute.DefensiveIQ
            };
        }

        /// <summary>
        /// Gets attributes that may not decline at all.
        /// </summary>
        public static PlayerAttribute[] GetNoDeclineAttributes()
        {
            return new[]
            {
                PlayerAttribute.BasketballIQ,
                PlayerAttribute.Leadership,
                PlayerAttribute.Composure
            };
        }
    }

    /// <summary>
    /// Log entry for player development changes.
    /// </summary>
    [Serializable]
    public class DevelopmentLog
    {
        public int Season;
        public PlayerAttribute Attribute;
        public int PreviousValue;
        public int NewValue;
        public string Reason;  // "Offseason training", "In-season development", "Age decline"
        public DateTime Date;

        public int Change => NewValue - PreviousValue;
        public bool IsImprovement => Change > 0;
    }

    /// <summary>
    /// Record of minutes played on a specific date for load management.
    /// </summary>
    [Serializable]
    public class MinutesRecord
    {
        public DateTime Date;
        public int Minutes;
        public string GameId;         // Optional: reference to the game
        public bool WasBackToBack;    // Was this a back-to-back game?
    }
}
