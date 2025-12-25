using System;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Handles free throw calculations using quick-result mode.
    /// Instantly calculates outcomes without animation/pause.
    /// </summary>
    public class FreeThrowHandler
    {
        // ==================== CONSTANTS ====================

        private const float CLUTCH_PENALTY = 0.02f;       // -2% on last FT in clutch
        private const float ICING_PENALTY = 0.03f;        // -3% if timeout just called
        private const float MIN_FT_PCT = 0.20f;           // Floor for terrible shooters
        private const float MAX_FT_PCT = 0.98f;           // Ceiling for great shooters

        // ==================== PUBLIC METHODS ====================

        /// <summary>
        /// Calculate free throw results using quick-result mode.
        /// Returns immediately with made/missed counts and play-by-play text.
        /// </summary>
        /// <param name="shooter">The player shooting free throws</param>
        /// <param name="attempts">Number of free throws to shoot</param>
        /// <param name="context">Current game context</param>
        /// <returns>FreeThrowResult with outcomes and description</returns>
        public FreeThrowResult CalculateFreeThrows(Player shooter, int attempts, GameContext context)
        {
            if (attempts <= 0 || shooter == null)
            {
                return new FreeThrowResult
                {
                    Attempts = 0,
                    Made = 0,
                    ShooterId = shooter?.PlayerId,
                    PlayByPlayText = ""
                };
            }

            int made = 0;

            for (int i = 0; i < attempts; i++)
            {
                float probability = CalculateSingleFtProbability(shooter, context, i, attempts);
                if (UnityEngine.Random.value < probability)
                {
                    made++;
                }
            }

            return new FreeThrowResult
            {
                Attempts = attempts,
                Made = made,
                ShooterId = shooter.PlayerId,
                PlayByPlayText = GenerateFtDescription(shooter, made, attempts),
                WasIced = context.TimeoutJustCalled
            };
        }

        /// <summary>
        /// Calculate probability for a single free throw attempt.
        /// </summary>
        /// <param name="shooter">The shooter</param>
        /// <param name="context">Game context</param>
        /// <param name="shotNumber">Which shot in the sequence (0-indexed)</param>
        /// <param name="totalShots">Total shots in sequence</param>
        /// <returns>Probability from 0 to 1</returns>
        public float CalculateSingleFtProbability(Player shooter, GameContext context, int shotNumber, int totalShots)
        {
            // Base probability from FreeThrow attribute (0-100 -> 0-1)
            float basePct = shooter.FreeThrow / 100f;

            // Clutch pressure modifier
            if (context.IsClutchTime)
            {
                // Clutch attribute helps under pressure (0-100)
                float clutchMod = (shooter.Clutch - 50) / 400f;  // +/-12.5%
                basePct += clutchMod;
            }

            // Composure affects consistency (0-100)
            float composureMod = (shooter.Composure - 50) / 600f;  // +/-8%
            basePct += composureMod;

            // "Icing" the shooter (timeout called before FTs)
            if (context.TimeoutJustCalled)
            {
                basePct -= ICING_PENALTY;
            }

            // Last free throw in clutch is psychologically harder
            if (context.IsClutchTime && shotNumber == totalShots - 1 && totalShots > 1)
            {
                basePct -= CLUTCH_PENALTY;
            }

            // Slight momentum effect - making previous FTs helps
            // (Not implemented as we don't track individual results mid-sequence)

            return Mathf.Clamp(basePct, MIN_FT_PCT, MAX_FT_PCT);
        }

        /// <summary>
        /// Generate play-by-play description for free throw sequence.
        /// </summary>
        private string GenerateFtDescription(Player shooter, int made, int attempts)
        {
            string name = shooter.LastName;
            if (string.IsNullOrEmpty(name))
                name = shooter.FirstName ?? "Player";

            if (attempts == 1)
            {
                return made == 1
                    ? $"{name} makes the free throw."
                    : $"{name} misses the free throw.";
            }

            if (made == attempts)
            {
                return attempts == 2
                    ? $"{name} makes both free throws."
                    : $"{name} makes all {attempts} free throws.";
            }

            if (made == 0)
            {
                return attempts == 2
                    ? $"{name} misses both free throws."
                    : $"{name} misses all {attempts} free throws.";
            }

            return $"{name} makes {made} of {attempts} free throws.";
        }

        /// <summary>
        /// Create free throw possession events for the box score.
        /// </summary>
        public PossessionEvent CreateFreeThrowEvent(
            Player shooter,
            FreeThrowResult result,
            float gameClock,
            int quarter)
        {
            return new PossessionEvent
            {
                Type = EventType.FreeThrow,
                GameClock = gameClock,
                Quarter = quarter,
                ActorPlayerId = shooter.PlayerId,
                Outcome = result.Made > 0 ? EventOutcome.Success : EventOutcome.Fail,
                PointsScored = result.Made,
                Description = result.PlayByPlayText,
                FreeThrowResult = result
            };
        }

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Get qualitative shooting description (no numbers shown to user).
        /// </summary>
        public string GetShooterQuality(Player shooter)
        {
            int ft = shooter.FreeThrow;

            if (ft >= 90) return "Elite free throw shooter";
            if (ft >= 80) return "Very good from the line";
            if (ft >= 70) return "Reliable free throw shooter";
            if (ft >= 60) return "Average from the line";
            if (ft >= 50) return "Below average free throw shooter";
            return "Poor free throw shooter";
        }

        /// <summary>
        /// Determine if icing a shooter is likely to work.
        /// Returns true if shooter has low Clutch or Composure.
        /// </summary>
        public bool ShouldIceShooter(Player shooter)
        {
            // Icing works better on low Clutch/Composure players
            return shooter.Clutch < 60 || shooter.Composure < 60;
        }

        /// <summary>
        /// Calculate expected points from free throws (for AI decisions).
        /// </summary>
        public float ExpectedFreeThrowPoints(Player shooter, int attempts, GameContext context)
        {
            float totalExpected = 0f;
            for (int i = 0; i < attempts; i++)
            {
                totalExpected += CalculateSingleFtProbability(shooter, context, i, attempts);
            }
            return totalExpected;
        }
    }
}
