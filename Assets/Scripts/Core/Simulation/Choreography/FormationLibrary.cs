using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>
    /// Static formation templates (court spots by lineup index 0-4 = PG, SG, SF, PF, C).
    /// All spots are authored for a team attacking +X and mirrored via xMult.
    /// Pure C#, seeded jitter only — never UnityEngine.Random.
    /// </summary>
    public static class FormationLibrary
    {
        /// <summary>
        /// Half-court offensive formation chosen from team strategy.
        /// </summary>
        public static CourtPosition[] GetHalfCourtSpots(TeamStrategy strategy, bool attacksRight, System.Random rng)
        {
            float m = attacksRight ? 1f : -1f;
            CourtPosition[] spots;

            if (strategy != null && strategy.ThreePointFrequency > 60)
            {
                // 5-out spread: everyone on/near the arc
                spots = new[]
                {
                    new CourtPosition(25f * m, 0f),     // PG top
                    new CourtPosition(30f * m, -18f),   // SG left wing
                    new CourtPosition(30f * m, 18f),    // SF right wing
                    new CourtPosition(42f * m, -22f),   // PF left corner
                    new CourtPosition(33f * m, 9f)      // C high slot
                };
            }
            else if (strategy != null && strategy.PostUpFrequency > 50)
            {
                // Post-heavy: bigs inside, shooters spaced
                spots = new[]
                {
                    new CourtPosition(25f * m, 0f),     // PG top
                    new CourtPosition(29f * m, -17f),   // SG left wing
                    new CourtPosition(42f * m, 22f),    // SF right corner
                    new CourtPosition(32f * m, -8f),    // PF left elbow/high post
                    new CourtPosition(40f * m, 6f)      // C low post
                };
            }
            else
            {
                // Default balanced set
                spots = new[]
                {
                    new CourtPosition(25f * m, 0f),     // PG top
                    new CourtPosition(28f * m, -16f),   // SG left wing
                    new CourtPosition(28f * m, 16f),    // SF right wing
                    new CourtPosition(36f * m, -8f),    // PF left block
                    new CourtPosition(36f * m, 8f)      // C right block
                };
            }

            // Small seeded variation so consecutive possessions don't look stamped
            for (int i = 0; i < 5; i++)
                spots[i] = Jitter(spots[i], 1.5f, rng);

            return spots;
        }

        /// <summary>
        /// Backcourt starting spread (where players begin the advance phase) —
        /// in the half the offense is coming FROM.
        /// </summary>
        public static CourtPosition[] GetBackcourtSpots(bool attacksRight, bool isDefense, System.Random rng)
        {
            // Offense starts around its own backcourt; defense retreats ahead of them.
            float m = attacksRight ? 1f : -1f;
            float baseX = isDefense ? 18f : -14f; // defense already back near its basket side

            var spots = new CourtPosition[5];
            float[] lanes = { 0f, -14f, 14f, -7f, 7f };
            for (int i = 0; i < 5; i++)
            {
                float x = (baseX - i * 1.5f) * m;
                spots[i] = Jitter(new CourtPosition(x, lanes[i]), 2f, rng);
            }
            return spots;
        }

        /// <summary>
        /// NBA free-throw alignment, OFFENSE (indexed by lineup slot 0-4): shooter alone at the
        /// line, the two lane rebounders (lane1/lane2) in the second lane slots on BOTH sides of
        /// the key, the remaining two spaced behind the arc.
        /// </summary>
        public static CourtPosition[] GetFreeThrowOffenseSpots(bool attacksRight, int shooterIdx,
            int lane1, int lane2, System.Random rng)
        {
            float m = attacksRight ? 1f : -1f;
            var spots = new CourtPosition[5];

            bool arcTop = true;
            for (int i = 0; i < 5; i++)
            {
                if (i == shooterIdx)
                    spots[i] = new CourtPosition(CourtGeometry.FreeThrowLineX * m, 0f);
                else if (i == lane1)
                    spots[i] = new CourtPosition(36.5f * m, -8f);   // second lane slot, low side
                else if (i == lane2)
                    spots[i] = new CourtPosition(36.5f * m, 8f);    // second lane slot, high side
                else
                {
                    spots[i] = Jitter(new CourtPosition(21f * m, arcTop ? 13f : -13f), 1.5f, rng);
                    arcTop = false;
                }
            }
            return spots;
        }

        /// <summary>
        /// NBA free-throw alignment, DEFENSE (indexed by lineup slot 0-4): block1/block2 on the
        /// low blocks — the lane slots nearest the rim belong to the defense by rule — third one
        /// slot above the offensive rebounder on the low side, the other two beyond the arc.
        /// </summary>
        public static CourtPosition[] GetFreeThrowDefenseSpots(bool offenseAttacksRight,
            int block1, int block2, int third, System.Random rng)
        {
            float m = offenseAttacksRight ? 1f : -1f;
            var spots = new CourtPosition[5];

            bool arcTop = true;
            for (int i = 0; i < 5; i++)
            {
                if (i == block1)
                    spots[i] = new CourtPosition(40f * m, -8f);     // low block, low side
                else if (i == block2)
                    spots[i] = new CourtPosition(40f * m, 8f);      // low block, high side
                else if (i == third)
                    spots[i] = new CourtPosition(33f * m, -8f);     // third lane slot
                else
                {
                    spots[i] = Jitter(new CourtPosition(19f * m, arcTop ? 10f : -10f), 1.5f, rng);
                    arcTop = false;
                }
            }
            return spots;
        }

        public static CourtPosition Jitter(CourtPosition pos, float radius, System.Random rng)
        {
            float dx = ((float)rng.NextDouble() * 2f - 1f) * radius;
            float dy = ((float)rng.NextDouble() * 2f - 1f) * radius;
            return Clamp(new CourtPosition(pos.X + dx, pos.Y + dy));
        }

        public static CourtPosition Clamp(CourtPosition pos)
        {
            float x = pos.X, y = pos.Y;
            if (x > CourtGeometry.HalfLength - 1f) x = CourtGeometry.HalfLength - 1f;
            if (x < -CourtGeometry.HalfLength + 1f) x = -CourtGeometry.HalfLength + 1f;
            if (y > CourtGeometry.HalfWidth - 1f) y = CourtGeometry.HalfWidth - 1f;
            if (y < -CourtGeometry.HalfWidth + 1f) y = -CourtGeometry.HalfWidth + 1f;
            return new CourtPosition(x, y);
        }
    }
}
