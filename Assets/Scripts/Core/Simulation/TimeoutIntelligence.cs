using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// AI decision logic for calling timeouts.
    /// Evaluates game situations and recommends timeouts with priorities.
    /// </summary>
    public class TimeoutIntelligence
    {
        // ==================== THRESHOLDS ====================

        private const int RUN_THRESHOLD = 8;               // Points for "opponent run"
        private const float CLUTCH_CLOCK = 300f;           // Last 5 minutes
        private const int FATIGUE_THRESHOLD = 55;          // Energy % to consider tired
        private const int TRAILING_THRESHOLD = 8;          // Max points down to advance ball
        private const float LAST_TWO_MINUTES = 120f;       // For advance ball rule

        // ==================== CONTEXT ====================

        /// <summary>
        /// Context needed for timeout decisions.
        /// </summary>
        public class TimeoutContext
        {
            public int Quarter;
            public float GameClock;
            public int ScoreDifferential;          // Positive = this team leading
            public int TimeoutsRemaining;
            public int TimeoutsUsedFirstHalf;
            public int OpponentRunPoints;          // Consecutive points by opponent
            public bool OpponentJustScored;
            public bool OpponentAtFreeThrowLine;
            public int FreeThrowsRemaining;
            public bool HasPossession;
            public Dictionary<string, float> PlayerEnergy;  // playerId -> energy %
            public List<string> CurrentLineup;

            public bool IsClutchTime => Quarter >= 4 && GameClock <= CLUTCH_CLOCK &&
                                        Math.Abs(ScoreDifferential) <= 5;
            public bool IsLastTwoMinutes => GameClock <= LAST_TWO_MINUTES && Quarter >= 4;
            public bool IsEndOfHalf => (Quarter == 2 || Quarter >= 4) && GameClock <= LAST_TWO_MINUTES;
        }

        // ==================== MAIN DECISION METHOD ====================

        /// <summary>
        /// Evaluate whether to call a timeout and why.
        /// Returns decision with priority and reason.
        /// </summary>
        public TimeoutDecision ShouldCallTimeout(TimeoutContext ctx)
        {
            if (ctx.TimeoutsRemaining <= 0)
            {
                return new TimeoutDecision { ShouldCall = false, Priority = TimeoutPriority.None };
            }

            // Priority 1: Stop opponent run (8+ unanswered points)
            if (ctx.OpponentRunPoints >= RUN_THRESHOLD)
            {
                return new TimeoutDecision
                {
                    ShouldCall = true,
                    Reason = TimeoutReason.StopRun,
                    Priority = TimeoutPriority.Critical,
                    Description = $"Stop opponent's {ctx.OpponentRunPoints}-0 run"
                };
            }

            // Priority 2: Ice shooter before clutch free throws
            if (ctx.IsClutchTime && ctx.OpponentAtFreeThrowLine && ctx.FreeThrowsRemaining > 0)
            {
                // Only ice if FTs matter (close game)
                if (Math.Abs(ctx.ScoreDifferential) <= 3)
                {
                    return new TimeoutDecision
                    {
                        ShouldCall = true,
                        Reason = TimeoutReason.IcingShooter,
                        Priority = TimeoutPriority.High,
                        Description = "Icing shooter in clutch situation"
                    };
                }
            }

            // Priority 3: Advance ball in late game (last 2 minutes, trailing)
            if (ctx.IsLastTwoMinutes && ctx.HasPossession &&
                ctx.ScoreDifferential < 0 && ctx.ScoreDifferential >= -TRAILING_THRESHOLD)
            {
                return new TimeoutDecision
                {
                    ShouldCall = true,
                    Reason = TimeoutReason.AdvanceBall,
                    Priority = TimeoutPriority.High,
                    Description = "Advance ball to frontcourt"
                };
            }

            // Priority 4: Draw up play after opponent scores in clutch
            if (ctx.IsClutchTime && ctx.OpponentJustScored && ctx.HasPossession &&
                Math.Abs(ctx.ScoreDifferential) <= 5)
            {
                return new TimeoutDecision
                {
                    ShouldCall = true,
                    Reason = TimeoutReason.DrawUpPlay,
                    Priority = TimeoutPriority.Medium,
                    Description = "Draw up ATO play in clutch"
                };
            }

            // Priority 5: Rest fatigued star players
            var fatigueAlerts = GetFatiguedPlayers(ctx.PlayerEnergy, ctx.CurrentLineup);
            if (fatigueAlerts.Count >= 2 && !ctx.IsClutchTime)
            {
                return new TimeoutDecision
                {
                    ShouldCall = true,
                    Reason = TimeoutReason.RestPlayers,
                    Priority = TimeoutPriority.Medium,
                    Description = $"{fatigueAlerts.Count} players need rest"
                };
            }

            // Priority 6: End of half - use-or-lose timeout
            if (ctx.Quarter == 2 && ctx.GameClock <= 120f && ctx.TimeoutsUsedFirstHalf < 2)
            {
                return new TimeoutDecision
                {
                    ShouldCall = true,
                    Reason = TimeoutReason.Mandatory,
                    Priority = TimeoutPriority.Low,
                    Description = "Use-or-lose first half timeout"
                };
            }

            return new TimeoutDecision { ShouldCall = false, Priority = TimeoutPriority.None };
        }

        /// <summary>
        /// Get list of fatigued players in current lineup.
        /// </summary>
        private List<string> GetFatiguedPlayers(Dictionary<string, float> energy, List<string> lineup)
        {
            if (energy == null || lineup == null) return new List<string>();

            return lineup
                .Where(id => energy.TryGetValue(id, out float e) && e < FATIGUE_THRESHOLD)
                .ToList();
        }

        // ==================== RUN TRACKING ====================

        private int _runPoints;
        private string _runTeamId;

        /// <summary>
        /// Track points scored for run detection.
        /// </summary>
        public void TrackScore(string scoringTeamId, int points)
        {
            if (_runTeamId == scoringTeamId)
            {
                _runPoints += points;
            }
            else
            {
                // New team scored, reset run
                _runTeamId = scoringTeamId;
                _runPoints = points;
            }
        }

        /// <summary>
        /// Reset run tracking (after timeout or quarter).
        /// </summary>
        public void ResetRun()
        {
            _runPoints = 0;
            _runTeamId = null;
        }

        /// <summary>
        /// Get current run points for a team.
        /// </summary>
        public int GetOpponentRunPoints(string myTeamId)
        {
            if (_runTeamId != null && _runTeamId != myTeamId)
            {
                return _runPoints;
            }
            return 0;
        }

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Get a qualitative description of why a timeout should be called.
        /// For use in play-by-play or coaching suggestions.
        /// </summary>
        public string GetTimeoutAdvice(TimeoutReason reason)
        {
            return reason switch
            {
                TimeoutReason.StopRun => "Your team is struggling. Consider a timeout to stop the bleeding.",
                TimeoutReason.RestPlayers => "Your players are getting tired. A timeout would help them recover.",
                TimeoutReason.DrawUpPlay => "This is a crucial possession. A timeout could help get a good look.",
                TimeoutReason.EndOfGame => "Late game situation. Manage the clock carefully.",
                TimeoutReason.IcingShooter => "The opponent has a key free throw. Consider icing the shooter.",
                TimeoutReason.AdvanceBall => "You can advance the ball to the frontcourt with a timeout.",
                TimeoutReason.Mandatory => "You'll lose this timeout if you don't use it before halftime.",
                _ => "A timeout may be beneficial here."
            };
        }

        /// <summary>
        /// Determine if timeout is strategically valuable based on situation.
        /// Returns value from 0-100.
        /// </summary>
        public int EvaluateTimeoutValue(TimeoutContext ctx)
        {
            var decision = ShouldCallTimeout(ctx);

            if (!decision.ShouldCall) return 0;

            return decision.Priority switch
            {
                TimeoutPriority.Critical => 100,
                TimeoutPriority.High => 75,
                TimeoutPriority.Medium => 50,
                TimeoutPriority.Low => 25,
                _ => 0
            };
        }
    }
}
