using System;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>
    /// Pure math for continuous ball trajectories: pass arcs, shot parabolas to the rim,
    /// through-the-net follow-through, rim caroms to a rebound spot. All sampled by
    /// normalized progress u ∈ [0,1] so the choreographer can emit ticks at any density.
    /// </summary>
    public static class BallFlight
    {
        // ── Durations ──────────────────────────────────────────────

        public static float PassDuration(CourtPosition from, CourtPosition to)
        {
            float dist = from.DistanceTo(to);
            return Clamp(dist / 40f, 0.25f, 0.8f);
        }

        public static float ShotDuration(CourtPosition from, float rimX)
        {
            float dist = Dist(from, rimX);
            return Clamp(0.7f + dist / 45f, 0.7f, 1.3f);
        }

        public const float MakeFollowThroughDuration = 0.3f;
        public const float CaromDuration = 0.7f;

        // ── Sampling ───────────────────────────────────────────────

        /// <summary>Pass in flight: flat-ish arc between two players.</summary>
        public static BallState SamplePass(CourtPosition from, CourtPosition to, float u)
        {
            float dist = from.DistanceTo(to);
            float bump = (dist > 25f ? 4f : 1.5f) * Arc(u);
            return new BallState(
                Lerp(from.X, to.X, u),
                Lerp(from.Y, to.Y, u),
                5f + bump)
            {
                Status = BallStatus.InAir_Pass,
                HeldByPlayerId = null
            };
        }

        /// <summary>
        /// Jump shot in flight from the shot position to the rim.
        /// Apex height scales with distance (3PT ~7 ft above release line, short ~3.5).
        /// </summary>
        public static BallState SampleShot(CourtPosition from, float rimX, float u, ShotType? shotType = null)
        {
            float dist = Dist(from, rimX);
            float apexExtra =
                shotType == ShotType.Heave ? 12f :
                dist > 22f ? 7f :
                dist > 12f ? 5.5f : 3.5f;

            float height = Lerp(7f, CourtGeometry.RimHeight, u) + apexExtra * Arc(u);
            return new BallState(
                Lerp(from.X, rimX, u),
                Lerp(from.Y, 0f, u),
                height)
            {
                Status = BallStatus.InAir_Shot,
                HeldByPlayerId = null
            };
        }

        /// <summary>Made shot: ball drops from the rim down through the net.</summary>
        public static BallState SampleMadeFollowThrough(float rimX, float u)
        {
            return new BallState(rimX, 0f, Lerp(CourtGeometry.RimHeight, 5.5f, u))
            {
                Status = BallStatus.InAir_Shot,
                HeldByPlayerId = null
            };
        }

        /// <summary>Missed shot: ball pops off the rim and caroms to the rebound spot.</summary>
        public static BallState SampleMissCarom(float rimX, CourtPosition reboundSpot, float u)
        {
            float height = Lerp(CourtGeometry.RimHeight, 6f, u) + 2.5f * Arc(u);
            return new BallState(
                Lerp(rimX, reboundSpot.X, u),
                Lerp(0f, reboundSpot.Y, u),
                height)
            {
                Status = BallStatus.Loose,
                HeldByPlayerId = null
            };
        }

        /// <summary>Blocked shot: ball deflects up and out toward the recovery spot.</summary>
        public static BallState SampleBlockDeflection(CourtPosition from, CourtPosition recoverySpot, float u)
        {
            float height = Lerp(8f, 5f, u) + 3f * Arc(u);
            return new BallState(
                Lerp(from.X, recoverySpot.X, u),
                Lerp(from.Y, recoverySpot.Y, u),
                height)
            {
                Status = BallStatus.Loose,
                HeldByPlayerId = null
            };
        }

        /// <summary>
        /// Where a missed shot caroms to: 4-12 ft from the rim, biased away from
        /// the shot side, longer for deeper shots.
        /// </summary>
        public static CourtPosition ComputeReboundSpot(CourtPosition shotPos, float rimX, System.Random rng)
        {
            float shotDist = Dist(shotPos, rimX);
            float caromDist = 4f + (float)rng.NextDouble() * 8f + (shotDist > 22f ? 2f : 0f);

            // Bounce direction: biased to the opposite Y side of the shot, back toward the floor
            float dirY = shotPos.Y >= 0f ? -1f : 1f;
            float y = dirY * ((float)rng.NextDouble() * 0.7f + 0.3f) * caromDist;
            float backX = rimX > 0f ? -1f : 1f;
            float x = rimX + backX * ((float)rng.NextDouble() * 0.8f + 0.2f) * caromDist;

            return FormationLibrary.Clamp(new CourtPosition(x, y));
        }

        // ── Helpers ────────────────────────────────────────────────

        private static float Dist(CourtPosition from, float rimX)
        {
            float dx = from.X - rimX;
            float dy = from.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Lerp(float a, float b, float u) => a + (b - a) * u;

        /// <summary>Parabolic bump: 0 at u=0 and u=1, 1 at u=0.5.</summary>
        private static float Arc(float u) => 4f * u * (1f - u);

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
