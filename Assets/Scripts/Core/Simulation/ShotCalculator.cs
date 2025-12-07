using System;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Calculates shot success probability based on player attributes,
    /// defender proximity, and situational modifiers.
    /// </summary>
    public static class ShotCalculator
    {
        // Base percentages by zone (league average)
        private static readonly float BASE_RESTRICTED_AREA_PCT = 0.65f;
        private static readonly float BASE_PAINT_PCT = 0.42f;
        private static readonly float BASE_SHORT_MID_PCT = 0.40f;
        private static readonly float BASE_LONG_MID_PCT = 0.38f;
        private static readonly float BASE_THREE_PCT = 0.36f;

        /// <summary>
        /// Calculates the probability of making a shot.
        /// </summary>
        /// <param name="shooter">The player taking the shot</param>
        /// <param name="position">Where on the court the shot is taken</param>
        /// <param name="defender">The nearest defender (can be null for open shots)</param>
        /// <param name="defenderDistance">Distance to nearest defender in feet</param>
        /// <param name="shotType">Type of shot being attempted</param>
        /// <param name="isFastBreak">Is this a transition opportunity</param>
        /// <param name="secondsRemaining">Shot clock or game clock pressure</param>
        /// <param name="isHomeTeam">Is the shooter on the home team</param>
        /// <returns>Probability from 0 to 1</returns>
        public static float CalculateShotProbability(
            Player shooter,
            CourtPosition position,
            Player defender,
            float defenderDistance,
            ShotType shotType,
            bool isFastBreak = false,
            float secondsRemaining = 24f,
            bool isHomeTeam = false)
        {
            // Use isHomeTeam to determine which basket to calculate zones from
            // Home attacks basket at X=42, Away attacks basket at X=-42
            CourtZone zone = position.GetZone(isHomeTeam);
            
            // 1. Get base percentage for zone
            float basePct = GetBasePercentage(zone);
            
            // 2. Apply shooter's skill modifier (-15% to +15%)
            int shooterSkill = GetShooterSkillForZone(shooter, zone);
            float skillModifier = (shooterSkill - 50f) / 50f * 0.15f;
            
            // 3. Apply shot type modifier
            float shotTypeModifier = GetShotTypeModifier(shotType, shooter);
            
            // 4. Apply defender contest modifier (-25% for tight defense)
            float contestModifier = CalculateContestModifier(defender, defenderDistance, zone);
            
            // 5. Apply shooter condition (energy, morale, form)
            float conditionModifier = shooter.GetConditionModifier();
            
            // 6. Apply situational modifiers
            float situationalModifier = 1f;
            
            // Fast break bonus
            if (isFastBreak)
                situationalModifier += 0.08f;
            
            // Shot clock pressure
            if (secondsRemaining < 4f)
                situationalModifier -= 0.10f;
            else if (secondsRemaining < 7f)
                situationalModifier -= 0.05f;
            
            // HOME COURT ADVANTAGE: ~3% boost (matches real NBA stats)
            if (isHomeTeam)
                situationalModifier += 0.03f;
            
            // 7. Calculate final probability
            float finalPct = basePct;
            finalPct += skillModifier;
            finalPct += shotTypeModifier;
            finalPct *= (1f + contestModifier);
            finalPct *= conditionModifier;
            finalPct *= situationalModifier;
            
            // Clamp to reasonable bounds
            return Mathf.Clamp(finalPct, 0.02f, 0.95f);
        }

        /// <summary>
        /// Gets the base percentage for a court zone.
        /// </summary>
        private static float GetBasePercentage(CourtZone zone)
        {
            return zone switch
            {
                CourtZone.RestrictedArea => BASE_RESTRICTED_AREA_PCT,
                CourtZone.Paint => BASE_PAINT_PCT,
                CourtZone.ShortMidRange => BASE_SHORT_MID_PCT,
                CourtZone.LongMidRange => BASE_LONG_MID_PCT,
                CourtZone.ThreePoint => BASE_THREE_PCT,
                _ => 0.35f
            };
        }

        /// <summary>
        /// Gets the relevant shooting skill for a zone.
        /// </summary>
        private static int GetShooterSkillForZone(Player shooter, CourtZone zone)
        {
            return zone switch
            {
                CourtZone.RestrictedArea => shooter.Finishing_Rim,
                CourtZone.Paint => (shooter.Finishing_Rim + shooter.Shot_Close) / 2,
                CourtZone.ShortMidRange => shooter.Shot_Close,
                CourtZone.LongMidRange => shooter.Shot_MidRange,
                CourtZone.ThreePoint => shooter.Shot_Three,
                _ => 50
            };
        }

        /// <summary>
        /// Gets modifier based on shot type and player abilities.
        /// </summary>
        private static float GetShotTypeModifier(ShotType shotType, Player shooter)
        {
            return shotType switch
            {
                ShotType.Dunk => 0.20f + (shooter.Vertical - 50) / 500f,
                ShotType.Layup => 0.05f,
                ShotType.Floater => -0.02f,
                ShotType.Hookshot => (shooter.Finishing_PostMoves - 50) / 400f,
                ShotType.CatchAndShoot => 0.03f,
                ShotType.PullUp => -0.03f,
                ShotType.StepBack => -0.05f + (shooter.BallHandling - 50) / 500f,
                ShotType.Fadeaway => -0.08f + (shooter.Shot_MidRange - 50) / 400f,
                ShotType.Heave => -0.30f,
                ShotType.TipIn => 0.10f,
                _ => 0f
            };
        }

        /// <summary>
        /// Calculates the defensive contest modifier.
        /// Returns a negative value (penalty) based on defender proximity and skill.
        /// </summary>
        private static float CalculateContestModifier(Player defender, float distance, CourtZone zone)
        {
            if (defender == null || distance > 10f)
                return 0.05f; // Wide open bonus
            
            if (distance > 6f)
                return 0.02f; // Open

            // Get relevant defensive skill
            int defSkill = zone == CourtZone.RestrictedArea || zone == CourtZone.Paint
                ? defender.Defense_Interior
                : defender.Defense_Perimeter;
            
            // Calculate contest level (0-1)
            float contestLevel = Mathf.Clamp01((6f - distance) / 6f);
            contestLevel *= (defSkill / 100f);
            
            // Wingspan helps with contests
            contestLevel *= 1f + (defender.Wingspan - 50) / 200f;
            
            // Convert to negative modifier
            return -contestLevel * 0.25f;
        }

        /// <summary>
        /// Determines the best shot type based on player attributes and situation.
        /// </summary>
        public static ShotType DetermineShotType(
            Player shooter,
            CourtPosition position,
            float defenderDistance,
            bool isFastBreak)
        {
            CourtZone zone = position.GetZone(true); // Static method - assumes right side for shot type
            
            // At the rim
            if (zone == CourtZone.RestrictedArea)
            {
                // Dunk if athletic enough and not heavily contested
                if (shooter.Vertical >= 70 && defenderDistance > 3f)
                    return ShotType.Dunk;
                if (isFastBreak && shooter.Vertical >= 60)
                    return ShotType.Dunk;
                return ShotType.Layup;
            }
            
            // In the paint
            if (zone == CourtZone.Paint)
            {
                if (shooter.Finishing_PostMoves >= 70)
                    return UnityEngine.Random.value > 0.5f ? ShotType.Hookshot : ShotType.Layup;
                if (shooter.Shot_Close >= 70)
                    return ShotType.Floater;
                return ShotType.Layup;
            }
            
            // Mid-range and beyond
            if (defenderDistance < 4f && shooter.BallHandling >= 75)
                return ShotType.StepBack;
            
            if (shooter.Finishing_PostMoves >= 75 && zone == CourtZone.ShortMidRange)
                return ShotType.Fadeaway;
            
            return ShotType.CatchAndShoot;
        }

        /// <summary>
        /// Calculates expected points per shot (for AI decision making).
        /// </summary>
        public static float CalculateExpectedValue(
            Player shooter,
            CourtPosition position,
            Player defender,
            float defenderDistance,
            ShotType shotType)
        {
            float probability = CalculateShotProbability(
                shooter, position, defender, defenderDistance, shotType);
            
            CourtZone zone = position.GetZone(true); // Static method - uses right side basket
            int points = zone == CourtZone.ThreePoint ? 3 : 2;
            
            // Factor in potential and-one
            float andOneProbability = 0.05f;
            float expectedFreeThrowPoints = andOneProbability * (shooter.FreeThrow / 100f);
            
            return (probability * points) + expectedFreeThrowPoints;
        }
    }
}
