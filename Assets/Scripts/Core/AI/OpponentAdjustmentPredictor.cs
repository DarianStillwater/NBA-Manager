using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// Predicts opponent coach adjustments based on game state, personality, and tendencies.
    /// Uses scouting data to anticipate AI moves and recommend counter-strategies.
    /// </summary>
    public class OpponentAdjustmentPredictor
    {
        // ==================== SINGLETON ====================
        private static OpponentAdjustmentPredictor _instance;
        public static OpponentAdjustmentPredictor Instance => _instance ??= new OpponentAdjustmentPredictor();

        // ==================== STATE ====================

        /// <summary>
        /// Cached opponent profiles for quick access.
        /// </summary>
        private Dictionary<string, OpponentTendencyProfile> _profileCache =
            new Dictionary<string, OpponentTendencyProfile>();

        /// <summary>
        /// Adjustment history for this game (for pattern detection).
        /// </summary>
        private List<DetectedAdjustment> _gameAdjustmentHistory = new List<DetectedAdjustment>();

        // ==================== PREDICTION METHODS ====================

        /// <summary>
        /// Predicts the most likely adjustments the opponent will make.
        /// </summary>
        public List<PredictedAdjustment> PredictAdjustments(
            OpponentTendencyProfile profile,
            GameState gameState,
            AICoachPersonality coachPersonality = null)
        {
            var predictions = new List<PredictedAdjustment>();

            if (profile == null)
                return predictions;

            // Determine current game situation
            var situation = ClassifyGameSituation(gameState);

            // Get base predictions from profile
            var patternPredictions = GetPatternBasedPredictions(profile, situation);
            predictions.AddRange(patternPredictions);

            // Add personality-based predictions
            if (coachPersonality != null)
            {
                var personalityPredictions = GetPersonalityBasedPredictions(coachPersonality, gameState);
                predictions.AddRange(personalityPredictions);
            }

            // Add reactive predictions (based on what's happening in the game)
            var reactivePredictions = GetReactivePredictions(profile, gameState);
            predictions.AddRange(reactivePredictions);

            // Merge duplicates and calculate final confidence
            predictions = MergePredictions(predictions);

            // Sort by confidence
            return predictions
                .OrderByDescending(p => p.Confidence)
                .Take(5)
                .ToList();
        }

        /// <summary>
        /// Predicts adjustments after timeout.
        /// </summary>
        public List<PredictedAdjustment> PredictTimeoutAdjustments(
            OpponentTendencyProfile profile,
            GameState gameState)
        {
            var predictions = new List<PredictedAdjustment>();

            // Timeouts usually mean bigger adjustments
            var situation = ClassifyGameSituation(gameState);

            // More likely to change defensive scheme after timeout
            if (gameState.OpponentPointsLast5Minutes > 15)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.ChangeDefense,
                    Description = "Likely to change defensive scheme after getting scored on",
                    Confidence = 70,
                    ExpectedChange = profile.SecondaryDefense.ToString(),
                    CounterStrategy = "Be ready to attack their secondary defense"
                });
            }

            // If their offense is struggling, expect play call change
            if (gameState.OpponentOffensiveRatingLast5 < 95)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.ChangePlayCalling,
                    Description = "Likely to change play calling after offensive struggles",
                    Confidence = 65,
                    ExpectedChange = GetAlternatePlayStyle(profile),
                    CounterStrategy = "Scout their secondary plays"
                });
            }

            // Lineup change likely after timeout
            if (situation == GameSituation.LosingBig)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.ChangeLineup,
                    Description = "Likely to go with different lineup",
                    Confidence = 60,
                    ExpectedChange = "Expect their best lineup or experimental lineup",
                    CounterStrategy = "Be ready to adjust matchups"
                });
            }

            return predictions.OrderByDescending(p => p.Confidence).ToList();
        }

        /// <summary>
        /// Predicts clutch time adjustments.
        /// </summary>
        public ClutchPrediction PredictClutchStrategy(
            OpponentTendencyProfile profile,
            GameState gameState)
        {
            var prediction = new ClutchPrediction
            {
                GoToPlayerId = profile.ClutchGoToPlayer,
                SecondaryOptionId = profile.ClutchSecondaryOption,
                ExpectedPlayType = profile.PreferredClutchPlay,
                WillCallTimeout = profile.ClutchTimeoutTendency > 60,
                DefensiveApproach = profile.LateGameDefenseStrategy
            };

            // Adjust based on score
            int scoreDiff = gameState.TeamScore - gameState.OpponentScore;

            if (scoreDiff > 0) // Opponent trailing
            {
                prediction.Confidence = 80;
                prediction.CounterStrategy = $"Expect {profile.PreferredClutchPlay} for {profile.ClutchGoToPlayer}. " +
                    "Prepare to double or trap.";

                // Trailing teams more likely to go for 3
                if (scoreDiff >= 3)
                {
                    prediction.ExpectedPlayType = ClutchPlayType.OffBallScreen;
                    prediction.Note = "Need 3 pointer - expect kick-out or catch-and-shoot";
                }
            }
            else if (scoreDiff < 0) // Opponent leading
            {
                prediction.Confidence = 70;
                prediction.CounterStrategy = "They'll try to run clock. Foul if necessary.";
                prediction.DefensiveApproach = LateGameDefense.FoulToStop;
            }
            else // Tied
            {
                prediction.Confidence = 75;
                prediction.CounterStrategy = $"Last shot situation - expect {profile.PreferredClutchPlay}";
            }

            return prediction;
        }

        /// <summary>
        /// Predicts halftime adjustments.
        /// </summary>
        public List<PredictedAdjustment> PredictHalftimeAdjustments(
            OpponentTendencyProfile profile,
            GameState firstHalfState)
        {
            var predictions = new List<PredictedAdjustment>();

            // Get profile's halftime patterns
            foreach (var pattern in profile.HalftimeAdjustments)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = pattern.Type,
                    Description = pattern.Description,
                    Confidence = pattern.Likelihood,
                    CounterStrategy = pattern.CounterStrategy
                });
            }

            // Add reactive predictions based on first half
            int scoreDiff = firstHalfState.TeamScore - firstHalfState.OpponentScore;

            if (scoreDiff > 10) // We're winning big
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.ChangePace,
                    Description = "Expect them to speed up pace in second half",
                    Confidence = 65,
                    CounterStrategy = "Control pace, don't let them speed you up"
                });

                if (profile.ZoneUsage < 30)
                {
                    predictions.Add(new PredictedAdjustment
                    {
                        Type = AdjustmentType.ChangeDefense,
                        Description = "Might go to zone to change things up",
                        Confidence = 50,
                        CounterStrategy = "Have zone offense ready"
                    });
                }
            }
            else if (scoreDiff < -10) // They're winning big
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.ChangePace,
                    Description = "Expect them to slow down and protect lead",
                    Confidence = 60,
                    CounterStrategy = "Force turnovers, create tempo"
                });
            }

            // Check first half offensive/defensive efficiency
            if (firstHalfState.TeamOffensiveRating > 115)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.ChangeDefense,
                    Description = "You're scoring well - expect defensive adjustments",
                    Confidence = 70,
                    CounterStrategy = "Be ready for more trapping or zone"
                });
            }

            return predictions.OrderByDescending(p => p.Confidence).ToList();
        }

        /// <summary>
        /// Detects if opponent has made an adjustment.
        /// </summary>
        public DetectedAdjustment DetectAdjustment(
            GameState previousState,
            GameState currentState,
            OpponentTendencyProfile profile)
        {
            // Compare states to detect changes

            // Lineup change detection
            if (previousState.OpponentLineupHash != currentState.OpponentLineupHash)
            {
                return new DetectedAdjustment
                {
                    Type = AdjustmentType.ChangeLineup,
                    DetectedTime = DateTime.Now,
                    GameClock = currentState.GameClock,
                    Quarter = currentState.Quarter,
                    Description = "Opponent changed lineup"
                };
            }

            // Defensive scheme change detection
            if (currentState.OpponentZoneDetected && !previousState.OpponentZoneDetected)
            {
                return new DetectedAdjustment
                {
                    Type = AdjustmentType.ChangeDefense,
                    DetectedTime = DateTime.Now,
                    GameClock = currentState.GameClock,
                    Quarter = currentState.Quarter,
                    Description = "Opponent switched to zone defense"
                };
            }

            // Pace change detection
            float paceDiff = currentState.CurrentPace - previousState.CurrentPace;
            if (Mathf.Abs(paceDiff) > 10)
            {
                return new DetectedAdjustment
                {
                    Type = AdjustmentType.ChangePace,
                    DetectedTime = DateTime.Now,
                    GameClock = currentState.GameClock,
                    Quarter = currentState.Quarter,
                    Description = paceDiff > 0 ? "Opponent speeding up pace" : "Opponent slowing down pace"
                };
            }

            // Trap detection
            if (currentState.OpponentTrapsDetected > previousState.OpponentTrapsDetected + 2)
            {
                return new DetectedAdjustment
                {
                    Type = AdjustmentType.IncreaseTrapping,
                    DetectedTime = DateTime.Now,
                    GameClock = currentState.GameClock,
                    Quarter = currentState.Quarter,
                    Description = "Opponent increasing trapping frequency"
                };
            }

            return null;
        }

        /// <summary>
        /// Gets a counter-strategy for a detected adjustment.
        /// </summary>
        public CounterStrategy GetCounterStrategy(
            DetectedAdjustment adjustment,
            OpponentTendencyProfile profile)
        {
            return adjustment.Type switch
            {
                AdjustmentType.ChangeDefense => new CounterStrategy
                {
                    Description = "Attack their secondary defense",
                    TacticalChanges = new List<string>
                    {
                        "Run plays designed for zone if they switched to zone",
                        "Use ball movement to find open shots",
                        "Be patient and don't force"
                    },
                    TargetPlayers = profile.DefensiveWeakLinks,
                    Urgency = StrategyUrgency.Medium
                },

                AdjustmentType.IncreaseTrapping => new CounterStrategy
                {
                    Description = "Beat the trap with quick ball movement",
                    TacticalChanges = new List<string>
                    {
                        "Quick passes out of traps",
                        "Attack 4-on-3 advantages",
                        "Get ball to middle of floor",
                        "Use press break sets"
                    },
                    Urgency = StrategyUrgency.High
                },

                AdjustmentType.ChangePace => new CounterStrategy
                {
                    Description = "Control the pace to your preference",
                    TacticalChanges = new List<string>
                    {
                        "Push tempo if they're slowing down",
                        "Slow down if they're speeding up",
                        "Use your timeouts to disrupt their rhythm"
                    },
                    Urgency = StrategyUrgency.Medium
                },

                AdjustmentType.ChangeLineup => new CounterStrategy
                {
                    Description = "Evaluate matchups and adjust",
                    TacticalChanges = new List<string>
                    {
                        "Check for size mismatches",
                        "Exploit any defensive weak links",
                        "Consider your own lineup adjustment"
                    },
                    Urgency = StrategyUrgency.Low
                },

                _ => new CounterStrategy
                {
                    Description = "Monitor and adjust as needed",
                    Urgency = StrategyUrgency.Low
                }
            };
        }

        // ==================== HELPER METHODS ====================

        private GameSituation ClassifyGameSituation(GameState state)
        {
            int scoreDiff = state.TeamScore - state.OpponentScore;

            // Clutch time check
            if (state.Quarter >= 4 && state.GameClock <= 300 && Math.Abs(scoreDiff) <= 5)
                return GameSituation.ClutchTime;

            // End of quarter check
            if (state.GameClock <= 120)
                return GameSituation.EndOfQuarter;

            // Score differential checks
            if (scoreDiff >= 15)
                return GameSituation.WinningBig;
            if (scoreDiff <= -15)
                return GameSituation.LosingBig;

            // Efficiency checks
            if (state.OpponentOffensiveRatingLast5 < 90)
                return GameSituation.OffenseStruggling;
            if (state.TeamOffensiveRating > 120)
                return GameSituation.DefenseStruggling;

            return GameSituation.Normal;
        }

        private List<PredictedAdjustment> GetPatternBasedPredictions(
            OpponentTendencyProfile profile,
            GameSituation situation)
        {
            var predictions = new List<PredictedAdjustment>();

            var patterns = situation switch
            {
                GameSituation.LosingBig => profile.WhenLosingBig,
                GameSituation.WinningBig => profile.WhenWinningBig,
                GameSituation.OffenseStruggling => profile.WhenOffenseStruggles,
                GameSituation.DefenseStruggling => profile.WhenDefenseStruggles,
                _ => new List<AdjustmentPattern>()
            };

            foreach (var pattern in patterns)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = pattern.Type,
                    Description = pattern.Description,
                    Confidence = pattern.Likelihood,
                    CounterStrategy = pattern.CounterStrategy
                });
            }

            return predictions;
        }

        private List<PredictedAdjustment> GetPersonalityBasedPredictions(
            AICoachPersonality personality,
            GameState state)
        {
            var predictions = new List<PredictedAdjustment>();

            // Aggressive coaches more likely to trap when losing
            if (personality?.Aggression >= 70 && state.OpponentScore < state.TeamScore)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.IncreaseTrapping,
                    Description = "Aggressive coach likely to increase pressure",
                    Confidence = 60,
                    CounterStrategy = "Be ready for traps and pressure"
                });
            }

            return predictions;
        }

        private List<PredictedAdjustment> GetReactivePredictions(
            OpponentTendencyProfile profile,
            GameState state)
        {
            var predictions = new List<PredictedAdjustment>();

            // If we're hitting threes, expect closeout adjustments
            if (state.TeamThreePointPercentage > 40)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.ChangeDefense,
                    Description = "Expect tighter closeouts on shooters",
                    Confidence = 55,
                    CounterStrategy = "Use shot fakes, attack closeouts"
                });
            }

            // If we're scoring in paint, expect help defense adjustments
            if (state.TeamPointsInPaint > 30)
            {
                predictions.Add(new PredictedAdjustment
                {
                    Type = AdjustmentType.ChangeDefense,
                    Description = "Expect more help defense in paint",
                    Confidence = 50,
                    CounterStrategy = "Kick out to open shooters"
                });
            }

            return predictions;
        }

        private List<PredictedAdjustment> MergePredictions(List<PredictedAdjustment> predictions)
        {
            return predictions
                .GroupBy(p => p.Type)
                .Select(g => new PredictedAdjustment
                {
                    Type = g.Key,
                    Description = g.First().Description,
                    Confidence = Math.Min(95, g.Sum(p => p.Confidence) / g.Count() + g.Count() * 5),
                    CounterStrategy = g.First().CounterStrategy,
                    ExpectedChange = g.First().ExpectedChange
                })
                .ToList();
        }

        private string GetAlternatePlayStyle(OpponentTendencyProfile profile)
        {
            // If they're PnR heavy, alternate is probably iso
            if (profile.PnRFrequency >= 60)
                return "More isolation plays";
            if (profile.IsolationFrequency >= 50)
                return "More pick and roll";
            return "Different play mix";
        }

        /// <summary>
        /// Caches a profile for the current game.
        /// </summary>
        public void CacheProfile(OpponentTendencyProfile profile)
        {
            if (profile != null)
                _profileCache[profile.TeamId] = profile;
        }

        /// <summary>
        /// Clears game state (call at start of new game).
        /// </summary>
        public void ResetForNewGame()
        {
            _gameAdjustmentHistory.Clear();
        }
    }

    // ==================== SUPPORTING TYPES ====================

    /// <summary>
    /// A predicted adjustment the opponent might make.
    /// </summary>
    public class PredictedAdjustment
    {
        public AdjustmentType Type;
        public string Description;
        public int Confidence; // 0-100
        public string ExpectedChange;
        public string CounterStrategy;
        public string Note;
    }

    /// <summary>
    /// Prediction for clutch situation.
    /// </summary>
    public class ClutchPrediction
    {
        public string GoToPlayerId;
        public string SecondaryOptionId;
        public ClutchPlayType ExpectedPlayType;
        public bool WillCallTimeout;
        public LateGameDefense DefensiveApproach;
        public int Confidence;
        public string CounterStrategy;
        public string Note;
    }

    /// <summary>
    /// A detected adjustment during the game.
    /// </summary>
    public class DetectedAdjustment
    {
        public AdjustmentType Type;
        public DateTime DetectedTime;
        public float GameClock;
        public int Quarter;
        public string Description;
    }

    /// <summary>
    /// Counter-strategy to respond to an adjustment.
    /// </summary>
    public class CounterStrategy
    {
        public string Description;
        public List<string> TacticalChanges = new List<string>();
        public List<string> TargetPlayers = new List<string>();
        public StrategyUrgency Urgency;
    }

    /// <summary>
    /// How urgent a counter-strategy is.
    /// </summary>
    public enum StrategyUrgency
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Game state for prediction.
    /// </summary>
    public class GameState
    {
        public int Quarter;
        public float GameClock;
        public int TeamScore;
        public int OpponentScore;
        public float CurrentPace;
        public float TeamOffensiveRating;
        public float OpponentOffensiveRatingLast5;
        public float TeamThreePointPercentage;
        public int TeamPointsInPaint;
        public int OpponentPointsLast5Minutes;
        public string OpponentLineupHash;
        public bool OpponentZoneDetected;
        public int OpponentTrapsDetected;
        public bool JustCalledTimeout;
    }
}
