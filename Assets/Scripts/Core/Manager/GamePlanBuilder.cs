using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Builds and manages pre-game preparation and game plans.
    /// Integrates scouting data to create actionable strategies.
    /// </summary>
    public class GamePlanBuilder : MonoBehaviour
    {
        // ==================== SINGLETON ====================
        private static GamePlanBuilder _instance;
        public static GamePlanBuilder Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GamePlanBuilder>();
                    if (_instance == null)
                    {
                        var go = new GameObject("GamePlanBuilder");
                        _instance = go.AddComponent<GamePlanBuilder>();
                    }
                }
                return _instance;
            }
        }

        // ==================== STATE ====================

        /// <summary>
        /// Current active game plan.
        /// </summary>
        public GamePlan CurrentGamePlan { get; private set; }

        /// <summary>
        /// Historical game plans for reference.
        /// </summary>
        public List<GamePlan> GamePlanHistory = new List<GamePlan>();

        // ==================== EVENTS ====================

        public event Action<GamePlan> OnGamePlanCreated;
        public event Action<GamePlan> OnGamePlanUpdated;

        // ==================== GAME PLAN CREATION ====================

        /// <summary>
        /// Creates a new game plan for an upcoming opponent.
        /// </summary>
        public GamePlan CreateGamePlan(
            string teamId,
            OpponentTendencyProfile opponentProfile,
            List<Player> roster,
            List<Player> opponentRoster,
            float scoutingQuality,
            float practicePrep)
        {
            var plan = new GamePlan
            {
                PlanId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                OpponentTeamId = opponentProfile.TeamId,
                OpponentName = opponentProfile.TeamName,
                CreatedDate = DateTime.Now,
                ScoutingQuality = scoutingQuality,
                PracticePreparation = practicePrep
            };

            // Import scouting data
            plan.OpponentProfile = opponentProfile;

            // Generate matchup recommendations
            plan.MatchupAssignments = GenerateMatchupRecommendations(roster, opponentRoster, opponentProfile);

            // Generate offensive approach
            plan.OffensiveApproach = GenerateOffensiveApproach(opponentProfile);

            // Generate defensive approach
            plan.DefensiveApproach = GenerateDefensiveApproach(opponentProfile);

            // Generate focus points
            plan.KeyFocusPoints = GenerateFocusPoints(opponentProfile);

            // Generate contingency plans
            plan.ContingencyPlans = GenerateContingencyPlans(opponentProfile);

            // Calculate preparation bonus
            plan.PreparationBonus = CalculatePreparationBonus(scoutingQuality, practicePrep);

            CurrentGamePlan = plan;
            OnGamePlanCreated?.Invoke(plan);

            Debug.Log($"[GamePlanBuilder] Created game plan vs {opponentProfile.TeamName} " +
                     $"(Prep Bonus: {plan.PreparationBonus:P0})");

            return plan;
        }

        /// <summary>
        /// Creates a quick game plan with minimal preparation.
        /// </summary>
        public GamePlan CreateQuickPlan(
            string teamId,
            string opponentTeamId,
            string opponentName)
        {
            var basicProfile = OpponentTendencyProfile.CreateBasic(opponentTeamId, opponentName);

            return new GamePlan
            {
                PlanId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                OpponentTeamId = opponentTeamId,
                OpponentName = opponentName,
                CreatedDate = DateTime.Now,
                ScoutingQuality = 0.3f,
                PracticePreparation = 0f,
                OpponentProfile = basicProfile,
                PreparationBonus = 0f,
                OffensiveApproach = new OffensivePlan { Style = "Standard" },
                DefensiveApproach = new DefensivePlan { Scheme = DefensiveScheme.ManToMan }
            };
        }

        // ==================== MATCHUP GENERATION ====================

        private List<MatchupAssignment> GenerateMatchupRecommendations(
            List<Player> roster,
            List<Player> opponentRoster,
            OpponentTendencyProfile profile)
        {
            var assignments = new List<MatchupAssignment>();

            if (roster == null || opponentRoster == null)
                return assignments;

            // Get opponent starters (or best players)
            var opponentStarters = opponentRoster
                .OrderByDescending(p => p.OverallRating)
                .Take(5)
                .ToList();

            // Get our best defenders
            var defenders = roster
                .OrderByDescending(p => GetDefensiveRating(p))
                .ToList();

            // Find priority matchups (opponent key players)
            var priorityPlayers = profile.PriorityDefenders;

            foreach (var opponent in opponentStarters)
            {
                var assignment = new MatchupAssignment
                {
                    OpponentPlayerId = opponent.PlayerId,
                    OpponentName = opponent.FullName,
                    OpponentPosition = opponent.PositionString
                };

                // Find best defender for this player
                var bestDefender = FindBestDefender(opponent, defenders, assignments);
                if (bestDefender != null)
                {
                    assignment.DefenderId = bestDefender.PlayerId;
                    assignment.DefenderName = bestDefender.FullName;
                    assignment.MatchupQuality = CalculateMatchupQuality(bestDefender, opponent);
                    assignment.Strategy = GetMatchupStrategy(bestDefender, opponent, profile);
                }

                // Check if this is a priority defensive assignment
                assignment.IsPriority = priorityPlayers.Contains(opponent.PlayerId);

                assignments.Add(assignment);
            }

            return assignments;
        }

        private Player FindBestDefender(
            Player opponent,
            List<Player> defenders,
            List<MatchupAssignment> existingAssignments)
        {
            var assignedDefenders = existingAssignments.Select(a => a.DefenderId).ToList();
            var availableDefenders = defenders.Where(d => !assignedDefenders.Contains(d.PlayerId)).ToList();

            // Score each defender for this matchup
            var scored = availableDefenders.Select(d => new
            {
                Defender = d,
                Score = ScoreDefenderForMatchup(d, opponent)
            }).OrderByDescending(x => x.Score);

            return scored.FirstOrDefault()?.Defender;
        }

        private float ScoreDefenderForMatchup(Player defender, Player opponent)
        {
            float score = 0f;

            // Position match
            if (defender.Position == opponent.Position)
                score += 20;
            else if (IsAdjacentPosition(defender.Position, opponent.Position))
                score += 10;

            // Size comparison
            float heightDiff = defender.HeightInches - opponent.HeightInches;
            if (heightDiff >= 0 && heightDiff <= 3)
                score += 10;
            else if (heightDiff < -3)
                score -= 10;

            // Defensive ratings
            score += defender.Defense_Perimeter / 5f;
            score += defender.Defense_Interior / 5f;

            // Speed for guarding quick players
            if (opponent.Speed > 70)
                score += (defender.Speed - opponent.Speed) / 3f;

            return score;
        }

        private bool IsAdjacentPosition(Position p1, Position p2)
        {
            int diff = Math.Abs((int)p1 - (int)p2);
            return diff == 1;
        }

        private int GetDefensiveRating(Player p)
        {
            return (p.Defense_Perimeter + p.Defense_Interior + p.DefensiveIQ) / 3;
        }

        private int CalculateMatchupQuality(Player defender, Player opponent)
        {
            float score = ScoreDefenderForMatchup(defender, opponent);
            return Mathf.Clamp(Mathf.RoundToInt(50 + score), 0, 100);
        }

        private string GetMatchupStrategy(Player defender, Player opponent, OpponentTendencyProfile profile)
        {
            // Check opponent tendencies
            var tendencies = profile.KeyPlayerTendencies
                .FirstOrDefault(t => t.PlayerId == opponent.PlayerId);

            if (tendencies != null)
            {
                if (tendencies.UsageRate > 25)
                    return "High usage player - deny ball, force help";
                if (tendencies.DefensiveWeaknesses.Count > 0)
                    return $"Target on offense: {tendencies.DefensiveWeaknesses.First()}";
            }

            return "Standard defense";
        }

        // ==================== APPROACH GENERATION ====================

        private OffensivePlan GenerateOffensiveApproach(OpponentTendencyProfile profile)
        {
            var plan = new OffensivePlan();

            // Analyze opponent defense to determine approach
            if (profile.ZoneUsage >= 40)
            {
                plan.Style = "Zone Attack";
                plan.PrimaryAction = "High-low action and skip passes";
                plan.Emphasis = new List<string>
                {
                    "Patient ball movement",
                    "Attack gaps in zone",
                    "Short corner three-pointers"
                };
            }
            else if (profile.SwitchTendency >= 60)
            {
                plan.Style = "Switch Exploit";
                plan.PrimaryAction = "Hunt mismatches";
                plan.Emphasis = new List<string>
                {
                    "Screen to create switches",
                    "Post up smaller defenders",
                    "Iso against weak defenders"
                };
            }
            else if (profile.TrapFrequency >= 40)
            {
                plan.Style = "Trap Breaker";
                plan.PrimaryAction = "Quick ball movement out of traps";
                plan.Emphasis = new List<string>
                {
                    "Short passes to middle",
                    "Attack 4-on-3",
                    "Look for skip passes"
                };
            }
            else
            {
                plan.Style = "Standard";
                plan.PrimaryAction = "Execute your offense";
                plan.Emphasis = new List<string>
                {
                    "Run your sets",
                    "Push in transition",
                    "Get to your spots"
                };
            }

            // Set pace recommendation
            if (profile.PreferredPace > 60)
                plan.PacePreference = "Slow them down, control tempo";
            else if (profile.PreferredPace < 40)
                plan.PacePreference = "Push tempo, make them run";
            else
                plan.PacePreference = "Play your preferred pace";

            // Target players based on defensive weak links
            plan.TargetPlayers = profile.DefensiveWeakLinks.ToList();

            return plan;
        }

        private DefensivePlan GenerateDefensiveApproach(OpponentTendencyProfile profile)
        {
            var plan = new DefensivePlan();

            // Base scheme recommendation
            if (profile.ThreePointRate >= 50)
            {
                plan.Scheme = DefensiveScheme.ManToMan;
                plan.SchemeNotes = "Man-to-man to contest shooters";
                plan.CloseoutIntensity = 80;
            }
            else if (profile.PostUpFrequency >= 40)
            {
                plan.Scheme = DefensiveScheme.ManToMan;
                plan.SchemeNotes = "Front the post, help from weak side";
            }
            else if (profile.PnRFrequency >= 60)
            {
                plan.Scheme = DefensiveScheme.ManToMan;
                plan.SchemeNotes = "Aggressive PnR coverage";
                plan.PnRCoverage = "Switch or hedge hard";
            }
            else
            {
                plan.Scheme = DefensiveScheme.ManToMan;
                plan.SchemeNotes = "Standard man defense";
            }

            // Key defensive emphases
            plan.Emphasis = new List<string>();

            if (profile.TransitionRate >= 50)
                plan.Emphasis.Add("Sprint back in transition");

            if (profile.OffensiveReboundingRate >= 40)
                plan.Emphasis.Add("Box out aggressively");

            if (profile.PnRFrequency >= 50)
                plan.Emphasis.Add($"PnR Coverage: {plan.PnRCoverage ?? "Show and recover"}");

            // Set player-specific assignments
            plan.SpecialAssignments = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(profile.ClutchGoToPlayer))
            {
                plan.SpecialAssignments[profile.ClutchGoToPlayer] = "Limit touches, force help";
            }

            // Transition defense
            plan.TransitionDefense = profile.TransitionRate >= 45
                ? "Sprint back, no leaks"
                : "Controlled transition defense";

            return plan;
        }

        // ==================== FOCUS POINTS & CONTINGENCIES ====================

        private List<GamePlanFocus> GenerateFocusPoints(OpponentTendencyProfile profile)
        {
            var focuses = new List<GamePlanFocus>();

            // Exploit weaknesses
            foreach (var weakness in profile.Weaknesses.Take(3))
            {
                focuses.Add(new GamePlanFocus
                {
                    Area = weakness.Category.ToString(),
                    Priority = weakness.Severity >= 70 ? FocusPriority.Critical : FocusPriority.High,
                    Description = weakness.ExploitStrategy,
                    ActionItems = new List<string> { weakness.Description }
                });
            }

            // Neutralize strengths
            foreach (var strength in profile.Strengths.Take(2))
            {
                focuses.Add(new GamePlanFocus
                {
                    Area = strength.Category.ToString(),
                    Priority = FocusPriority.Medium,
                    Description = strength.NeutralizationStrategy,
                    ActionItems = new List<string> { $"Counter their {strength.Category}" }
                });
            }

            return focuses;
        }

        private List<ContingencyPlan> GenerateContingencyPlans(OpponentTendencyProfile profile)
        {
            var contingencies = new List<ContingencyPlan>();

            // If they go zone
            contingencies.Add(new ContingencyPlan
            {
                Trigger = "If they go zone",
                Response = "Run zone offense package",
                Details = new List<string>
                {
                    "Ball movement to find gaps",
                    "High-low action",
                    "Short corner threes"
                }
            });

            // If they start trapping
            contingencies.Add(new ContingencyPlan
            {
                Trigger = "If they increase trapping",
                Response = "Press break offense",
                Details = new List<string>
                {
                    "Quick passes to middle",
                    "4-on-3 attack",
                    "Timeout if needed"
                }
            });

            // If we're losing
            contingencies.Add(new ContingencyPlan
            {
                Trigger = "If down by 10+",
                Response = "Increase intensity",
                Details = new List<string>
                {
                    "Full court pressure",
                    "Push pace",
                    "Go to best players"
                }
            });

            // Based on their known adjustments
            foreach (var pattern in profile.WhenLosingBig.Take(2))
            {
                contingencies.Add(new ContingencyPlan
                {
                    Trigger = $"When they {pattern.Description}",
                    Response = pattern.CounterStrategy,
                    Details = pattern.Triggers
                });
            }

            return contingencies;
        }

        // ==================== PREPARATION BONUS ====================

        private float CalculatePreparationBonus(float scoutingQuality, float practicePrep)
        {
            // Scouting provides knowledge (40% weight)
            // Practice provides execution (60% weight)
            float bonus = (scoutingQuality * 0.4f) + (practicePrep * 0.6f);

            // Cap at 15% bonus
            return Mathf.Min(0.15f, bonus * 0.15f);
        }

        /// <summary>
        /// Gets the preparation bonus for a specific aspect.
        /// </summary>
        public float GetAspectBonus(string aspect)
        {
            if (CurrentGamePlan == null)
                return 0f;

            return aspect switch
            {
                "Offense" => CurrentGamePlan.PreparationBonus * 0.8f,
                "Defense" => CurrentGamePlan.PreparationBonus,
                "Clutch" => CurrentGamePlan.PreparationBonus * 0.5f, // Clutch is less predictable
                _ => CurrentGamePlan.PreparationBonus
            };
        }

        // ==================== GAME PLAN UPDATES ====================

        /// <summary>
        /// Updates the game plan based on in-game observations.
        /// </summary>
        public void UpdateGamePlan(string adjustment, string reason)
        {
            if (CurrentGamePlan == null)
                return;

            CurrentGamePlan.InGameAdjustments.Add(new GamePlanAdjustment
            {
                Timestamp = DateTime.Now,
                Adjustment = adjustment,
                Reason = reason
            });

            OnGamePlanUpdated?.Invoke(CurrentGamePlan);
        }

        /// <summary>
        /// Archives the current game plan after a game.
        /// </summary>
        public void ArchiveGamePlan(bool won, int scoreDiff)
        {
            if (CurrentGamePlan == null)
                return;

            CurrentGamePlan.GameResult = won ? "Win" : "Loss";
            CurrentGamePlan.ScoreDifferential = scoreDiff;

            GamePlanHistory.Add(CurrentGamePlan);
            CurrentGamePlan = null;
        }

        // ==================== LIFECYCLE ====================

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
    }

    // ==================== DATA TYPES ====================

    /// <summary>
    /// Complete game plan for an opponent.
    /// </summary>
    [Serializable]
    public class GamePlan
    {
        public string PlanId;
        public string TeamId;
        public string OpponentTeamId;
        public string OpponentName;
        public DateTime CreatedDate;

        public float ScoutingQuality;
        public float PracticePreparation;
        public float PreparationBonus;

        public OpponentTendencyProfile OpponentProfile;

        public List<MatchupAssignment> MatchupAssignments = new List<MatchupAssignment>();
        public OffensivePlan OffensiveApproach;
        public DefensivePlan DefensiveApproach;
        public List<GamePlanFocus> KeyFocusPoints = new List<GamePlanFocus>();
        public List<ContingencyPlan> ContingencyPlans = new List<ContingencyPlan>();

        public List<GamePlanAdjustment> InGameAdjustments = new List<GamePlanAdjustment>();

        public string GameResult;
        public int ScoreDifferential;
    }

    /// <summary>
    /// Defensive matchup assignment.
    /// </summary>
    [Serializable]
    public class MatchupAssignment
    {
        public string OpponentPlayerId;
        public string OpponentName;
        public string OpponentPosition;
        public string DefenderId;
        public string DefenderName;
        public int MatchupQuality;
        public string Strategy;
        public bool IsPriority;
    }

    /// <summary>
    /// Offensive approach for the game.
    /// </summary>
    [Serializable]
    public class OffensivePlan
    {
        public string Style;
        public string PrimaryAction;
        public string PacePreference;
        public List<string> Emphasis = new List<string>();
        public List<string> TargetPlayers = new List<string>();
    }

    /// <summary>
    /// Defensive approach for the game.
    /// </summary>
    [Serializable]
    public class DefensivePlan
    {
        public DefensiveScheme Scheme;
        public string SchemeNotes;
        public string PnRCoverage;
        public int CloseoutIntensity;
        public string TransitionDefense;
        public List<string> Emphasis = new List<string>();
        public Dictionary<string, string> SpecialAssignments = new Dictionary<string, string>();
    }

    /// <summary>
    /// A focus point for the game plan.
    /// </summary>
    [Serializable]
    public class GamePlanFocus
    {
        public string Area;
        public FocusPriority Priority;
        public string Description;
        public List<string> ActionItems = new List<string>();
    }

    /// <summary>
    /// A contingency plan for specific situations.
    /// </summary>
    [Serializable]
    public class ContingencyPlan
    {
        public string Trigger;
        public string Response;
        public List<string> Details = new List<string>();
    }

    /// <summary>
    /// In-game adjustment to the plan.
    /// </summary>
    [Serializable]
    public class GamePlanAdjustment
    {
        public DateTime Timestamp;
        public string Adjustment;
        public string Reason;
    }

    public enum FocusPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}
