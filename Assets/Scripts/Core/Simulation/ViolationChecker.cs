using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Checks for violations (traveling, backcourt, 3-second, etc.)
    /// based on player attributes. Violations count as turnovers.
    /// </summary>
    public class ViolationChecker
    {
        // ==================== BASE RATES ====================

        // These are per-possession base probabilities
        private const float BASE_TRAVEL_RATE = 0.008f;        // ~0.8% per possession
        private const float BASE_BACKCOURT_RATE = 0.003f;     // ~0.3% per possession
        private const float BASE_THREE_SECOND_RATE = 0.010f;  // ~1% per possession (for bigs)
        private const float BASE_DOUBLE_DRIBBLE_RATE = 0.002f;// ~0.2% per possession

        // ==================== PUBLIC METHODS ====================

        /// <summary>
        /// Check for any violation during a possession.
        /// Returns null if no violation, otherwise returns the violation detail.
        /// </summary>
        public ViolationEventDetail CheckForViolation(
            Player ballHandler,
            List<Player> offensePlayers,
            ShotType? attemptedShot,
            float shotClockRemaining,
            bool hasCrossedHalfcourt)
        {
            // Check traveling
            var travelingResult = CheckTraveling(ballHandler, attemptedShot);
            if (travelingResult != null) return travelingResult;

            // Check backcourt violation
            var backcourtResult = CheckBackcourt(ballHandler, hasCrossedHalfcourt, shotClockRemaining);
            if (backcourtResult != null) return backcourtResult;

            // Check 3-second violation (for bigs in the paint)
            var threeSecondResult = CheckThreeSecond(offensePlayers);
            if (threeSecondResult != null) return threeSecondResult;

            // Check double dribble
            var doubleDribbleResult = CheckDoubleDribble(ballHandler);
            if (doubleDribbleResult != null) return doubleDribbleResult;

            return null;
        }

        /// <summary>
        /// Check for traveling violation.
        /// Affected by: BallHandling, Energy, shot type (layup attempts are riskier)
        /// </summary>
        public ViolationEventDetail CheckTraveling(Player ballHandler, ShotType? attemptedShot)
        {
            float probability = BASE_TRAVEL_RATE;

            // Low BallHandling increases traveling chance
            probability += (50 - ballHandler.BallHandling) / 400f;  // +12.5% at 0 BallHandling

            // Fatigue increases travel chance
            probability += (100 - ballHandler.Energy) / 1000f;  // +10% at 0 energy

            // Euro step / gather step moves are riskier
            if (attemptedShot == ShotType.Layup)
            {
                probability += 0.010f;
            }
            else if (attemptedShot == ShotType.Dunk)
            {
                probability += 0.005f;
            }

            // BasketballIQ helps avoid travels
            probability -= (ballHandler.BasketballIQ - 50) / 800f;  // -6.25% at 100 IQ

            probability = Mathf.Clamp(probability, 0.002f, 0.05f);

            if (UnityEngine.Random.value < probability)
            {
                return new ViolationEventDetail
                {
                    ViolationType = ViolationType.Traveling,
                    ViolatorId = ballHandler.PlayerId,
                    Description = $"{ballHandler.LastName ?? ballHandler.FirstName} called for traveling"
                };
            }

            return null;
        }

        /// <summary>
        /// Check for backcourt violation.
        /// Affected by: BasketballIQ, pressure situations
        /// </summary>
        public ViolationEventDetail CheckBackcourt(Player ballHandler, bool hasCrossedHalfcourt, float shotClockRemaining)
        {
            // Can only have backcourt violation if already crossed half
            if (!hasCrossedHalfcourt) return null;

            float probability = BASE_BACKCOURT_RATE;

            // Low Basketball IQ increases chance
            probability += (50 - ballHandler.BasketballIQ) / 800f;  // +6.25% at 0 IQ

            // Pressure situations (low shot clock) increase chance
            if (shotClockRemaining < 10f)
            {
                probability += 0.005f;
            }

            // Composure helps avoid mental mistakes
            probability -= (ballHandler.Composure - 50) / 1000f;  // -5% at 100 composure

            probability = Mathf.Clamp(probability, 0.001f, 0.02f);

            if (UnityEngine.Random.value < probability)
            {
                return new ViolationEventDetail
                {
                    ViolationType = ViolationType.Backcourt,
                    ViolatorId = ballHandler.PlayerId,
                    Description = $"Backcourt violation on {ballHandler.LastName ?? ballHandler.FirstName}"
                };
            }

            return null;
        }

        /// <summary>
        /// Check for 3-second violation in the paint.
        /// Only affects Centers and Power Forwards.
        /// </summary>
        public ViolationEventDetail CheckThreeSecond(List<Player> offensePlayers)
        {
            if (offensePlayers == null) return null;

            foreach (var player in offensePlayers)
            {
                // Only check bigs who might camp in the paint
                if (!IsBigMan(player)) continue;

                float probability = BASE_THREE_SECOND_RATE;

                // Low Basketball IQ increases chance
                probability += (50 - player.BasketballIQ) / 500f;  // +10% at 0 IQ

                // Veterans are more aware
                if (player.YearsInLeague >= 5)
                {
                    probability -= 0.003f;
                }

                probability = Mathf.Clamp(probability, 0.002f, 0.03f);

                if (UnityEngine.Random.value < probability)
                {
                    return new ViolationEventDetail
                    {
                        ViolationType = ViolationType.ThreeSecond,
                        ViolatorId = player.PlayerId,
                        Description = $"3-second violation on {player.LastName ?? player.FirstName}"
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Check for double dribble violation.
        /// Rare, mostly affects low BallHandling players.
        /// </summary>
        public ViolationEventDetail CheckDoubleDribble(Player ballHandler)
        {
            float probability = BASE_DOUBLE_DRIBBLE_RATE;

            // Low BallHandling increases chance
            probability += (50 - ballHandler.BallHandling) / 600f;  // +8.3% at 0 BallHandling

            // Very rare for high IQ players
            if (ballHandler.BasketballIQ >= 70)
            {
                probability *= 0.5f;
            }

            probability = Mathf.Clamp(probability, 0.0005f, 0.02f);

            if (UnityEngine.Random.value < probability)
            {
                return new ViolationEventDetail
                {
                    ViolationType = ViolationType.DoubleDribble,
                    ViolatorId = ballHandler.PlayerId,
                    Description = $"Double dribble on {ballHandler.LastName ?? ballHandler.FirstName}"
                };
            }

            return null;
        }

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Check if player is a big man (Center or Power Forward).
        /// </summary>
        private bool IsBigMan(Player player)
        {
            var pos = player.PrimaryPosition?.ToUpper();
            return pos == "C" || pos == "PF" || pos == "CENTER" || pos == "POWER FORWARD";
        }

        /// <summary>
        /// Create a turnover event from a violation.
        /// </summary>
        public PossessionEvent CreateViolationTurnover(
            ViolationEventDetail violation,
            Player violator,
            float gameClock,
            int quarter)
        {
            return new PossessionEvent
            {
                Type = EventType.Turnover,
                GameClock = gameClock,
                Quarter = quarter,
                ActorPlayerId = violator.PlayerId,
                Outcome = EventOutcome.Fail,
                Description = violation.Description,
                ViolationDetail = violation
            };
        }

        /// <summary>
        /// Get a qualitative assessment of a player's ball security (no numbers).
        /// </summary>
        public string GetBallSecurityAssessment(Player player)
        {
            int bh = player.BallHandling;
            int iq = player.BasketballIQ;
            int avg = (bh + iq) / 2;

            if (avg >= 85) return "Excellent ball security";
            if (avg >= 70) return "Good ball security";
            if (avg >= 55) return "Average ball handler";
            if (avg >= 40) return "Turnover prone";
            return "Careless with the ball";
        }
    }
}
