using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// Defines AI coach personality and decision-making tendencies.
    /// Each AI coach has distinct philosophies affecting tactics, rotations, and adjustments.
    /// </summary>
    [Serializable]
    public class AICoachPersonality
    {
        // ==================== IDENTITY ====================
        public string CoachId;
        public string CoachName;

        // ==================== OFFENSIVE PHILOSOPHY ====================
        [Header("Offensive Tendencies")]
        public OffensivePhilosophy OffensiveStyle = OffensivePhilosophy.Balanced;

        [Range(80, 110)] public float PreferredPace = 98f;
        [Range(0, 100)] public int ThreePointEmphasis = 40;       // How much they value 3s
        [Range(0, 100)] public int IsolationTendency = 20;        // Reliance on star isos
        [Range(0, 100)] public int PickAndRollEmphasis = 40;      // PnR usage
        [Range(0, 100)] public int PostPlayEmphasis = 15;         // Post-up frequency
        [Range(0, 100)] public int BallMovementPriority = 60;     // Extra pass philosophy

        // ==================== DEFENSIVE PHILOSOPHY ====================
        [Header("Defensive Tendencies")]
        public DefensivePhilosophy DefensiveStyle = DefensivePhilosophy.Balanced;

        [Range(0, 100)] public int DefensiveAggression = 50;      // How aggressive on D
        [Range(0, 100)] public int ZoneUsageTendency = 10;        // Likelihood of zone
        [Range(0, 100)] public int SwitchingTendency = 30;        // Switch everything
        [Range(0, 100)] public int BlitzingTendency = 25;         // Trap ball handlers
        [Range(0, 100)] public int DoubleTeamTendency = 20;       // Double team stars

        // ==================== ROTATION PHILOSOPHY ====================
        [Header("Rotation Tendencies")]
        public RotationPhilosophy RotationStyle = RotationPhilosophy.Standard;

        [Range(7, 12)] public int RotationDepth = 9;              // How many players used
        [Range(0, 100)] public int YoungPlayerTrust = 50;         // Willingness to play youth
        [Range(0, 100)] public int VeteranPreference = 50;        // Preference for vets
        [Range(0, 100)] public int LoadManagementTendency = 40;   // Rest star players
        [Range(0, 100)] public int MatchupBasedSubbing = 50;      // Sub for matchups

        // ==================== ADJUSTMENT TENDENCIES ====================
        [Header("Adjustment Tendencies")]
        [Range(0, 100)] public int InGameAdjustmentSpeed = 50;    // Quick to adjust
        [Range(0, 100)] public int HalftimeAdjustmentQuality = 50;
        [Range(0, 100)] public int PlayoffAdjustments = 60;       // Better in playoffs
        [Range(0, 100)] public int Stubbornness = 50;             // Stick to system

        // ==================== PREDICTABILITY & PATTERNS ====================
        [Header("Predictability")]
        /// <summary>How predictable this coach is (0 = unpredictable, 100 = very predictable)</summary>
        [Range(0, 100)] public int Predictability = 50;

        /// <summary>Overall aggression level for opponent profiling</summary>
        [Range(0, 100)] public int Aggression = 50;

        /// <summary>Common adjustments when losing big</summary>
        public List<string> WhenLosingBigPatterns = new List<string>();

        /// <summary>Common adjustments when winning big</summary>
        public List<string> WhenWinningBigPatterns = new List<string>();

        /// <summary>Typical halftime adjustments</summary>
        public List<string> HalftimePatterns = new List<string>();

        // ==================== TIMEOUT USAGE ====================
        [Header("Timeout Tendencies")]
        [Range(0, 100)] public int TimeoutAggressiveness = 50;    // Quick to call timeout
        [Range(0, 100)] public int ATOPlayQuality = 60;           // Quality of ATO plays
        public int PointsRunBeforeTimeout = 8;                    // When to stop run

        // ==================== CLUTCH TENDENCIES ====================
        [Header("Clutch Tendencies")]
        [Range(0, 100)] public int ClutchPressure = 50;           // How they handle pressure
        [Range(0, 100)] public int RiskTakingClutch = 50;         // Risky plays in clutch
        public bool FoulingUp3Tendency = true;                    // Foul when up 3
        public bool HoldForLastShotTendency = true;

        // ==================== PERSONALITY TRAITS ====================
        [Header("Personality")]
        public CoachTemperament Temperament = CoachTemperament.Calm;
        [Range(0, 100)] public int TechnicalFoulRisk = 20;        // Likely to argue
        [Range(0, 100)] public int MotivationAbility = 60;        // Rally the team
        [Range(0, 100)] public int MediaManagement = 50;          // Handle media

        // ==================== DECISION MAKING ====================

        /// <summary>
        /// Gets offensive scheme based on game situation.
        /// </summary>
        public OffensiveScheme GetOffensiveScheme(int scoreDiff, int quarter, float clockSeconds)
        {
            // Late game adjustments
            if (quarter == 4 && clockSeconds < 120)
            {
                if (scoreDiff > 10)
                    return OffensiveScheme.Motion;  // Run clock
                if (scoreDiff < -10)
                    return OffensiveScheme.ThreeHeavy;  // Desperate 3s
            }

            // Based on philosophy
            return OffensiveStyle switch
            {
                OffensivePhilosophy.FastPaced => OffensiveScheme.FastBreak,
                OffensivePhilosophy.SlowPaced => OffensiveScheme.PostUp,
                OffensivePhilosophy.StarHeavy => OffensiveScheme.Isolation,
                OffensivePhilosophy.TeamOriented => OffensiveScheme.Motion,
                OffensivePhilosophy.ThreePointHeavy => OffensiveScheme.ThreeHeavy,
                OffensivePhilosophy.InsideOut => OffensiveScheme.PostUp,
                OffensivePhilosophy.PnRHeavy => OffensiveScheme.PickAndRoll,
                _ => OffensiveScheme.Motion
            };
        }

        /// <summary>
        /// Gets defensive scheme based on opponent and situation.
        /// </summary>
        public DefensiveScheme GetDefensiveScheme(bool opponentHasShooters, bool opponentHasPostPlayer, int quarter)
        {
            // Zone tendencies
            var rng = new System.Random();
            if (rng.Next(100) < ZoneUsageTendency)
            {
                if (!opponentHasShooters)
                    return DefensiveScheme.Zone23;
            }

            // Switching tendencies
            if (rng.Next(100) < SwitchingTendency)
                return DefensiveScheme.SwitchAll;

            // Based on philosophy
            return DefensiveStyle switch
            {
                DefensivePhilosophy.Aggressive => DefensiveScheme.ManToMan,
                DefensivePhilosophy.Conservative => DefensiveScheme.ManToMan,
                DefensivePhilosophy.ZoneHeavy => DefensiveScheme.Zone23,
                DefensivePhilosophy.SwitchHeavy => DefensiveScheme.SwitchAll,
                DefensivePhilosophy.TrapHeavy => DefensiveScheme.HalfCourtTrap,
                _ => DefensiveScheme.ManToMan
            };
        }

        /// <summary>
        /// Decides whether to call timeout based on situation.
        /// </summary>
        public bool ShouldCallTimeout(int opponentRun, int scoreDiff, float clockSeconds, int quarter, int timeoutsLeft)
        {
            if (timeoutsLeft <= 0)
                return false;

            // Opponent on a run
            if (opponentRun >= PointsRunBeforeTimeout)
                return true;

            // Late game situations
            if (quarter == 4 && clockSeconds < 60 && Math.Abs(scoreDiff) <= 5)
                return true;

            // Aggressive timeout callers
            if (TimeoutAggressiveness > 70 && opponentRun >= 6)
                return true;

            return false;
        }

        /// <summary>
        /// Gets rotation decisions based on game situation.
        /// </summary>
        public int GetRotationDepth(bool isPlayoffs, int scoreDiff, int quarter)
        {
            int depth = RotationDepth;

            // Playoffs = tighter rotation
            if (isPlayoffs)
            {
                depth = Math.Max(7, depth - 2);
            }

            // Close game late = starters only
            if (quarter >= 4 && Math.Abs(scoreDiff) <= 10)
            {
                depth = Math.Min(depth, 8);
            }

            return depth;
        }

        /// <summary>
        /// Decides if should play young player or veteran.
        /// </summary>
        public bool PreferYoungPlayer(int youngOverall, int vetOverall, int youngPotential, bool isPlayoffs)
        {
            // Always play better player in playoffs
            if (isPlayoffs)
                return youngOverall > vetOverall;

            // Based on trust level
            int threshold = 100 - YoungPlayerTrust;
            int overallDiff = vetOverall - youngOverall;

            return overallDiff < threshold / 10;
        }

        /// <summary>
        /// Makes mid-game adjustment decision.
        /// </summary>
        public bool ShouldMakeAdjustment(int scoreDiff, int previousSchemeDuration, float opponentPoints3Q)
        {
            // Stubborn coaches don't adjust
            var rng = new System.Random();
            if (rng.Next(100) < Stubbornness)
                return false;

            // Down by a lot = adjust
            if (scoreDiff < -15)
                return true;

            // Quick adjusters change if not working
            if (InGameAdjustmentSpeed > 70 && scoreDiff < -8 && previousSchemeDuration > 5)
                return true;

            return false;
        }

        /// <summary>
        /// Gets clutch play call decision.
        /// </summary>
        public PlayType GetClutchPlayCall(bool needThree, List<string> bestPlayers)
        {
            // Need three
            if (needThree)
                return PlayType.SpotUp3;

            // Star heavy coaches go isolation
            if (IsolationTendency > 60)
                return PlayType.Isolation;

            // Risk takers in clutch
            if (RiskTakingClutch > 60)
                return PlayType.Isolation;

            // Ball movement coaches run sets
            if (BallMovementPriority > 70)
                return PlayType.MotionOffense;

            return PlayType.PickAndRoll;  // Safe default
        }

        /// <summary>
        /// Decides on double team strategy.
        /// </summary>
        public DoubleTeamDecision GetDoubleTeamDecision(
            string opponentStarId,
            int starPoints,
            int starAssists,
            bool opponentHasShooters)
        {
            // Don't double if opponent has shooters
            if (opponentHasShooters && DoubleTeamTendency < 70)
            {
                return new DoubleTeamDecision
                {
                    ShouldDouble = false,
                    Reason = "Opponent has shooters"
                };
            }

            // Star going off
            if (starPoints > 30)
            {
                return new DoubleTeamDecision
                {
                    ShouldDouble = true,
                    Trigger = DoubleTeamTrigger.OnCatch,
                    Reason = "Star player is hot"
                };
            }

            // Based on tendency
            var rng = new System.Random();
            if (rng.Next(100) < DoubleTeamTendency)
            {
                return new DoubleTeamDecision
                {
                    ShouldDouble = true,
                    Trigger = DoubleTeamTrigger.OnPost,
                    Reason = "Coach prefers to double"
                };
            }

            return new DoubleTeamDecision { ShouldDouble = false };
        }

        /// <summary>
        /// Decides technical foul risk from arguing.
        /// </summary>
        public bool WillArgueCall(int morale, bool badCall, int currentTechs)
        {
            if (currentTechs >= 1)
                return false;  // Already have one

            var rng = new System.Random();
            int threshold = TechnicalFoulRisk;

            if (badCall)
                threshold += 20;

            if (Temperament == CoachTemperament.Fiery)
                threshold += 30;

            if (morale < 40)
                threshold += 15;  // Try to rally

            return rng.Next(100) < threshold;
        }

        // ==================== ADJUSTMENT PATTERN GENERATION ====================

        /// <summary>
        /// Generates adjustment patterns based on personality traits.
        /// Called when creating an opponent profile.
        /// </summary>
        public void GenerateAdjustmentPatterns()
        {
            WhenLosingBigPatterns.Clear();
            WhenWinningBigPatterns.Clear();
            HalftimePatterns.Clear();

            // When losing big - based on aggression and style
            if (Aggression >= 60 || DefensiveAggression >= 60)
            {
                WhenLosingBigPatterns.Add("Increase full court pressure");
                WhenLosingBigPatterns.Add("More aggressive double teams");
            }
            if (PreferredPace < 95)
            {
                WhenLosingBigPatterns.Add("Speed up pace");
            }
            if (ThreePointEmphasis >= 40)
            {
                WhenLosingBigPatterns.Add("Increase three-point attempts");
            }
            if (IsolationTendency >= 40)
            {
                WhenLosingBigPatterns.Add("More isolation plays for star");
            }
            WhenLosingBigPatterns.Add("Tighter rotation to best players");

            // When winning big - based on control and system
            if (Stubbornness >= 60)
            {
                WhenWinningBigPatterns.Add("Stick with what's working");
            }
            else
            {
                WhenWinningBigPatterns.Add("Slow down pace to protect lead");
                WhenWinningBigPatterns.Add("Conservative play calling");
            }
            if (YoungPlayerTrust >= 60)
            {
                WhenWinningBigPatterns.Add("Give bench players minutes");
            }
            WhenWinningBigPatterns.Add("Focus on not turning ball over");

            // Halftime adjustments
            if (HalftimeAdjustmentQuality >= 60)
            {
                HalftimePatterns.Add("Major defensive scheme change");
                HalftimePatterns.Add("New offensive sets");
            }
            if (ZoneUsageTendency >= 30)
            {
                HalftimePatterns.Add("Switch to zone defense");
            }
            if (BlitzingTendency >= 40)
            {
                HalftimePatterns.Add("Increase trapping frequency");
            }
            HalftimePatterns.Add("Matchup adjustments based on first half");
        }

        /// <summary>
        /// Gets the likely adjustment for a specific situation.
        /// </summary>
        public string GetLikelyAdjustment(string situation)
        {
            var rng = new System.Random();
            List<string> patterns = situation switch
            {
                "LosingBig" => WhenLosingBigPatterns,
                "WinningBig" => WhenWinningBigPatterns,
                "Halftime" => HalftimePatterns,
                _ => new List<string>()
            };

            if (patterns.Count == 0)
                return "No adjustment expected";

            // Predictable coaches use first pattern more often
            if (Predictability >= 70 || rng.Next(100) < Predictability)
                return patterns[0];

            return patterns[rng.Next(patterns.Count)];
        }

        /// <summary>
        /// Gets the probability that the coach will make a specific adjustment.
        /// </summary>
        public int GetAdjustmentProbability(Data.AdjustmentType adjustmentType, int scoreDiff, int quarter)
        {
            int baseProbability = 50;

            // Score-based modifiers
            if (scoreDiff < -15)
            {
                if (adjustmentType == Data.AdjustmentType.ChangePace ||
                    adjustmentType == Data.AdjustmentType.IncreaseTrapping)
                    baseProbability += 25;
            }
            else if (scoreDiff > 15)
            {
                if (adjustmentType == Data.AdjustmentType.ChangePace)
                    baseProbability += 20;
            }

            // Personality modifiers
            baseProbability += (InGameAdjustmentSpeed - 50) / 5;
            baseProbability -= (Stubbornness - 50) / 5;

            // Quarter modifiers - more adjustments late
            if (quarter >= 3)
                baseProbability += 10;
            if (quarter == 4)
                baseProbability += 15;

            return Mathf.Clamp(baseProbability, 5, 95);
        }

        /// <summary>
        /// Generates an opponent tendency profile from this coach's personality.
        /// </summary>
        public Data.OpponentTendencyProfile GenerateOpponentProfile(string teamId, string teamName)
        {
            var profile = new Data.OpponentTendencyProfile
            {
                TeamId = teamId,
                TeamName = teamName,
                HeadCoachId = CoachId,
                HeadCoachName = CoachName,
                LastUpdated = DateTime.Now,
                DataQuality = 70, // Base quality for personality-derived profile

                // Coach personality
                CoachPredictability = Predictability,
                CoachAggression = Aggression,

                // Offensive tendencies (from personality)
                PnRFrequency = PickAndRollEmphasis,
                IsolationFrequency = IsolationTendency,
                PostUpFrequency = PostPlayEmphasis,
                ThreePointRate = ThreePointEmphasis,
                PreferredPace = (int)PreferredPace,
                BallMovement = BallMovementPriority,

                // Defensive tendencies
                ZoneUsage = ZoneUsageTendency,
                TrapFrequency = BlitzingTendency,
                SwitchTendency = SwitchingTendency,
                HelpDefenseAggression = DefensiveAggression,

                // Primary schemes
                PrimaryDefense = GetDefensiveScheme(false, false, 1) == DefensiveScheme.Zone23
                    ? Data.DefensiveScheme.Zone23
                    : Data.DefensiveScheme.ManToMan,

                // Clutch tendencies
                PreferredClutchPlay = IsolationTendency >= 60
                    ? Data.ClutchPlayType.Isolation
                    : Data.ClutchPlayType.PickAndRoll,
                ClutchTimeoutTendency = TimeoutAggressiveness,
                IntentionalFoulTendency = FoulingUp3Tendency ? 70 : 30
            };

            // Generate adjustment patterns
            GenerateAdjustmentPatterns();

            // Add adjustment patterns to profile
            foreach (var pattern in WhenLosingBigPatterns)
            {
                profile.WhenLosingBig.Add(new Data.AdjustmentPattern
                {
                    Type = ParseAdjustmentType(pattern),
                    Description = pattern,
                    Likelihood = Predictability >= 60 ? 70 : 50,
                    CounterStrategy = GetCounterFor(pattern)
                });
            }

            foreach (var pattern in WhenWinningBigPatterns)
            {
                profile.WhenWinningBig.Add(new Data.AdjustmentPattern
                {
                    Type = ParseAdjustmentType(pattern),
                    Description = pattern,
                    Likelihood = Predictability >= 60 ? 70 : 50,
                    CounterStrategy = GetCounterFor(pattern)
                });
            }

            foreach (var pattern in HalftimePatterns)
            {
                profile.HalftimeAdjustments.Add(new Data.AdjustmentPattern
                {
                    Type = ParseAdjustmentType(pattern),
                    Description = pattern,
                    Likelihood = HalftimeAdjustmentQuality,
                    CounterStrategy = GetCounterFor(pattern)
                });
            }

            return profile;
        }

        private Data.AdjustmentType ParseAdjustmentType(string pattern)
        {
            if (pattern.Contains("pace") || pattern.Contains("Pace"))
                return Data.AdjustmentType.ChangePace;
            if (pattern.Contains("defense") || pattern.Contains("Defense") || pattern.Contains("zone") || pattern.Contains("Zone"))
                return Data.AdjustmentType.ChangeDefense;
            if (pattern.Contains("lineup") || pattern.Contains("Lineup") || pattern.Contains("rotation") || pattern.Contains("bench"))
                return Data.AdjustmentType.ChangeLineup;
            if (pattern.Contains("trap") || pattern.Contains("Trap") || pattern.Contains("pressure"))
                return Data.AdjustmentType.IncreaseTrapping;
            return Data.AdjustmentType.ChangePlayCalling;
        }

        private string GetCounterFor(string pattern)
        {
            if (pattern.Contains("pressure") || pattern.Contains("trap"))
                return "Use press break, quick passes to middle";
            if (pattern.Contains("zone"))
                return "Run zone offense, attack gaps";
            if (pattern.Contains("pace") && pattern.Contains("Speed"))
                return "Control tempo, use clock";
            if (pattern.Contains("pace") && pattern.Contains("Slow"))
                return "Push tempo when possible";
            if (pattern.Contains("isolation"))
                return "Be ready for star isolations, prepare help";
            return "Observe and adjust";
        }

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates a random AI coach personality.
        /// </summary>
        public static AICoachPersonality CreateRandom(string coachId, string coachName, System.Random rng = null)
        {
            rng ??= new System.Random();

            var personalities = new[]
            {
                CreateFastPacedOffensive(coachId, coachName, rng),
                CreateDefenseFirst(coachId, coachName, rng),
                CreatePlayerDevelopment(coachId, coachName, rng),
                CreateStarHeavy(coachId, coachName, rng),
                CreateAnalytics(coachId, coachName, rng),
                CreateOldSchool(coachId, coachName, rng)
            };

            return personalities[rng.Next(personalities.Length)];
        }

        public static AICoachPersonality CreateFastPacedOffensive(string coachId, string coachName, System.Random rng = null)
        {
            rng ??= new System.Random();
            return new AICoachPersonality
            {
                CoachId = coachId,
                CoachName = coachName,
                OffensiveStyle = OffensivePhilosophy.FastPaced,
                PreferredPace = 105 + rng.Next(5),
                ThreePointEmphasis = 45 + rng.Next(15),
                BallMovementPriority = 50 + rng.Next(20),
                DefensiveStyle = DefensivePhilosophy.Balanced,
                DefensiveAggression = 40 + rng.Next(20),
                RotationDepth = 9 + rng.Next(2),
                InGameAdjustmentSpeed = 60 + rng.Next(20),
                Temperament = CoachTemperament.Energetic
            };
        }

        public static AICoachPersonality CreateDefenseFirst(string coachId, string coachName, System.Random rng = null)
        {
            rng ??= new System.Random();
            return new AICoachPersonality
            {
                CoachId = coachId,
                CoachName = coachName,
                OffensiveStyle = OffensivePhilosophy.SlowPaced,
                PreferredPace = 92 + rng.Next(5),
                ThreePointEmphasis = 30 + rng.Next(15),
                DefensiveStyle = DefensivePhilosophy.Aggressive,
                DefensiveAggression = 70 + rng.Next(20),
                ZoneUsageTendency = 30 + rng.Next(20),
                SwitchingTendency = 20 + rng.Next(30),
                RotationDepth = 8 + rng.Next(2),
                InGameAdjustmentSpeed = 50 + rng.Next(20),
                Stubbornness = 60 + rng.Next(20),
                Temperament = CoachTemperament.Fiery
            };
        }

        public static AICoachPersonality CreatePlayerDevelopment(string coachId, string coachName, System.Random rng = null)
        {
            rng ??= new System.Random();
            return new AICoachPersonality
            {
                CoachId = coachId,
                CoachName = coachName,
                OffensiveStyle = OffensivePhilosophy.TeamOriented,
                PreferredPace = 98 + rng.Next(5),
                BallMovementPriority = 70 + rng.Next(20),
                IsolationTendency = 10 + rng.Next(15),
                DefensiveStyle = DefensivePhilosophy.Balanced,
                RotationStyle = RotationPhilosophy.DevelopmentFocused,
                RotationDepth = 10 + rng.Next(2),
                YoungPlayerTrust = 70 + rng.Next(20),
                VeteranPreference = 30 + rng.Next(20),
                InGameAdjustmentSpeed = 40 + rng.Next(20),
                Temperament = CoachTemperament.Calm
            };
        }

        public static AICoachPersonality CreateStarHeavy(string coachId, string coachName, System.Random rng = null)
        {
            rng ??= new System.Random();
            return new AICoachPersonality
            {
                CoachId = coachId,
                CoachName = coachName,
                OffensiveStyle = OffensivePhilosophy.StarHeavy,
                PreferredPace = 95 + rng.Next(8),
                IsolationTendency = 50 + rng.Next(25),
                PickAndRollEmphasis = 40 + rng.Next(20),
                BallMovementPriority = 30 + rng.Next(20),
                DefensiveStyle = DefensivePhilosophy.Balanced,
                RotationStyle = RotationPhilosophy.StarMinutes,
                RotationDepth = 8 + rng.Next(2),
                LoadManagementTendency = 20 + rng.Next(20),
                ClutchPressure = 40 + rng.Next(30),
                RiskTakingClutch = 60 + rng.Next(25),
                Temperament = CoachTemperament.Intense
            };
        }

        public static AICoachPersonality CreateAnalytics(string coachId, string coachName, System.Random rng = null)
        {
            rng ??= new System.Random();
            return new AICoachPersonality
            {
                CoachId = coachId,
                CoachName = coachName,
                OffensiveStyle = OffensivePhilosophy.ThreePointHeavy,
                PreferredPace = 100 + rng.Next(8),
                ThreePointEmphasis = 60 + rng.Next(20),
                PostPlayEmphasis = 5 + rng.Next(10),
                DefensiveStyle = DefensivePhilosophy.SwitchHeavy,
                SwitchingTendency = 60 + rng.Next(25),
                RotationStyle = RotationPhilosophy.MatchupBased,
                MatchupBasedSubbing = 70 + rng.Next(20),
                InGameAdjustmentSpeed = 70 + rng.Next(20),
                Stubbornness = 20 + rng.Next(20),
                Temperament = CoachTemperament.Calculated
            };
        }

        public static AICoachPersonality CreateOldSchool(string coachId, string coachName, System.Random rng = null)
        {
            rng ??= new System.Random();
            return new AICoachPersonality
            {
                CoachId = coachId,
                CoachName = coachName,
                OffensiveStyle = OffensivePhilosophy.InsideOut,
                PreferredPace = 90 + rng.Next(8),
                ThreePointEmphasis = 25 + rng.Next(15),
                PostPlayEmphasis = 30 + rng.Next(20),
                DefensiveStyle = DefensivePhilosophy.Conservative,
                ZoneUsageTendency = 15 + rng.Next(15),
                RotationStyle = RotationPhilosophy.Standard,
                VeteranPreference = 70 + rng.Next(20),
                YoungPlayerTrust = 30 + rng.Next(20),
                Stubbornness = 60 + rng.Next(25),
                TechnicalFoulRisk = 30 + rng.Next(25),
                Temperament = CoachTemperament.OldSchool
            };
        }
    }

    // ==================== ENUMS ====================

    public enum OffensivePhilosophy
    {
        Balanced,
        FastPaced,
        SlowPaced,
        StarHeavy,
        TeamOriented,
        ThreePointHeavy,
        InsideOut,
        PnRHeavy
    }

    public enum DefensivePhilosophy
    {
        Balanced,
        Aggressive,
        Conservative,
        ZoneHeavy,
        SwitchHeavy,
        TrapHeavy
    }

    public enum RotationPhilosophy
    {
        Standard,           // Normal rotation
        StarMinutes,        // Stars play big minutes
        DevelopmentFocused, // Play young players
        MatchupBased,       // Sub for matchups
        Platoon             // Two distinct units
    }

    public enum CoachTemperament
    {
        Calm,
        Fiery,
        Energetic,
        Intense,
        Calculated,
        OldSchool
    }

    // ==================== RESULT CLASSES ====================

    public class DoubleTeamDecision
    {
        public bool ShouldDouble;
        public DoubleTeamTrigger Trigger;
        public string Reason;
    }

    /// <summary>
    /// AI game plan created before each game.
    /// </summary>
    [Serializable]
    public class AIGamePlan
    {
        public string GameId;
        public string OpponentId;

        // Offensive game plan
        public OffensiveScheme PrimaryOffense;
        public OffensiveScheme SecondaryOffense;
        public string GoToPlayerId;                  // Star to feed
        public int TargetPace;
        public int ThreePointAttemptTarget;

        // Defensive game plan
        public DefensiveScheme PrimaryDefense;
        public DefensiveScheme SecondaryDefense;
        public string PlayerToStop;                  // Key opponent to contain
        public bool DoubleTeamStar;
        public PnRCoverageScheme PnRCoverage;

        // Rotation
        public List<string> StartingLineup;
        public List<string> ClosingLineup;
        public int RotationDepth;
        public Dictionary<string, int> TargetMinutes;

        // Adjustments
        public List<AIAdjustment> PreparedAdjustments;
    }

    /// <summary>
    /// A prepared adjustment the AI coach can make.
    /// </summary>
    [Serializable]
    public class AIAdjustment
    {
        public AdjustmentType Type;
        public string Trigger;                       // When to make adjustment
        public string Description;
        public bool IsUsed;

        // Adjustment details
        public OffensiveScheme? NewOffense;
        public DefensiveScheme? NewDefense;
        public string SubOut;
        public string SubIn;
    }

    public enum AdjustmentType
    {
        OffensiveScheme,
        DefensiveScheme,
        RotationChange,
        DoubleTeamChange,
        PaceChange,
        MatchupChange
    }

    /// <summary>
    /// Manages AI coaching decisions during a game.
    /// </summary>
    public class AICoachController
    {
        private AICoachPersonality _personality;
        private AIGamePlan _gamePlan;
        private GameCoach _gameCoach;
        private System.Random _rng;

        public AICoachController(AICoachPersonality personality, GameCoach gameCoach)
        {
            _personality = personality;
            _gameCoach = gameCoach;
            _rng = new System.Random();
        }

        /// <summary>
        /// Creates game plan before a game.
        /// </summary>
        public AIGamePlan CreateGamePlan(
            string opponentId,
            List<Player> ownTeam,
            List<Player> opponentTeam,
            TeamStrategy opponentStrategy)
        {
            var plan = new AIGamePlan
            {
                OpponentId = opponentId,
                TargetPace = (int)_personality.PreferredPace
            };

            // Set offensive scheme based on personality
            plan.PrimaryOffense = _personality.GetOffensiveScheme(0, 1, 720);
            plan.SecondaryOffense = OffensiveScheme.Motion;

            // Find star player
            plan.GoToPlayerId = ownTeam.OrderByDescending(p => p.OverallRating).First().PlayerId;

            // Defensive scheme based on opponent
            bool hasShooters = opponentTeam.Any(p => p.GetAttributeForSimulation(PlayerAttribute.Shot_Three) >= 75);
            bool hasPost = opponentTeam.Any(p => p.GetAttributeForSimulation(PlayerAttribute.Finishing_PostMoves) >= 75);
            plan.PrimaryDefense = _personality.GetDefensiveScheme(hasShooters, hasPost, 1);

            // Player to stop
            plan.PlayerToStop = opponentTeam.OrderByDescending(p => p.OverallRating).First().PlayerId;

            // Rotation
            plan.RotationDepth = _personality.GetRotationDepth(false, 0, 1);
            plan.StartingLineup = GetStartingLineup(ownTeam);
            plan.ClosingLineup = GetClosingLineup(ownTeam);

            // Target minutes
            plan.TargetMinutes = new Dictionary<string, int>();
            var sortedPlayers = ownTeam.OrderByDescending(p => p.OverallRating).ToList();
            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                int minutes = i switch
                {
                    < 5 => 30 + (5 - i) * 2,   // Starters: 30-38
                    < 8 => 15 + (8 - i) * 3,   // Key bench: 15-24
                    _ => 5 + _rng.Next(10)     // Deep bench: 5-15
                };
                plan.TargetMinutes[sortedPlayers[i].PlayerId] = minutes;
            }

            return plan;
        }

        /// <summary>
        /// Makes in-game decision based on current state.
        /// </summary>
        public void ProcessPossession(
            int scoreDiff,
            int quarter,
            float clockSeconds,
            int opponentRun,
            bool hasPossession)
        {
            // Check timeout
            if (_personality.ShouldCallTimeout(opponentRun, scoreDiff, clockSeconds, quarter, _gameCoach.TimeoutsRemaining))
            {
                _gameCoach.CallTimeout(TimeoutReason.StopRun);
            }

            // Check adjustments
            if (_personality.ShouldMakeAdjustment(scoreDiff, 5, 0))
            {
                MakeAdjustment(scoreDiff, quarter);
            }
        }

        private void MakeAdjustment(int scoreDiff, int quarter)
        {
            // Down big = speed up
            if (scoreDiff < -15)
            {
                _gameCoach.SetPace(GamePace.Push);
                _gameCoach.SetOffense(OffensiveScheme.ThreeHeavy);
            }
            // Up big = slow down
            else if (scoreDiff > 15)
            {
                _gameCoach.SetPace(GamePace.Slow);
                _gameCoach.SetOffense(OffensiveScheme.Motion);
            }
        }

        private List<string> GetStartingLineup(List<Player> team)
        {
            // Get best player at each position
            var lineup = new List<string>();
            var positions = new[] { Position.PointGuard, Position.ShootingGuard, Position.SmallForward, Position.PowerForward, Position.Center };

            foreach (var pos in positions)
            {
                var best = team
                    .Where(p => p.Position == pos && !lineup.Contains(p.PlayerId))
                    .OrderByDescending(p => p.OverallRating)
                    .FirstOrDefault();

                if (best != null)
                    lineup.Add(best.PlayerId);
            }

            // Fill remaining spots
            while (lineup.Count < 5)
            {
                var best = team
                    .Where(p => !lineup.Contains(p.PlayerId))
                    .OrderByDescending(p => p.OverallRating)
                    .FirstOrDefault();

                if (best != null)
                    lineup.Add(best.PlayerId);
            }

            return lineup;
        }

        private List<string> GetClosingLineup(List<Player> team)
        {
            // Closing lineup = best 5 overall (may not be positional)
            return team
                .OrderByDescending(p => p.OverallRating)
                .Take(5)
                .Select(p => p.PlayerId)
                .ToList();
        }
    }
}
