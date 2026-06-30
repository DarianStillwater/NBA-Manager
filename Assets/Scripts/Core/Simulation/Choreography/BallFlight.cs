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
        public const float CaromDuration = 0.9f;

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

        /// <summary>
        /// Missed shot: the ball ricochets off the rim, then falls and takes one decaying
        /// bounce on its way to the rebound spot. Horizontal travel is delayed slightly so the
        /// initial kick reads as coming off the rim before the ball carries to the floor.
        /// </summary>
        public static BallState SampleMissCarom(float rimX, CourtPosition reboundSpot, float u)
        {
            // Horizontal: hold at the rim during the kick, then ease out to the rebound spot.
            float travel = SmoothStep(Clamp((u - 0.12f) / 0.88f, 0f, 1f));
            float x = Lerp(rimX, reboundSpot.X, travel);
            float y = Lerp(0f, reboundSpot.Y, travel);

            const float apex = CourtGeometry.RimHeight + 2.5f; // ~12.5 ft kick off the iron
            const float floorH = 0.9f;

            float h;
            if (u < 0.18f)
            {
                h = Lerp(CourtGeometry.RimHeight, apex, u / 0.18f); // sharp pop up off the rim
            }
            else
            {
                // f: 0 at the kick apex → 1 at settle. |cos| gives apex→floor→one bounce→floor;
                // the (1-f) envelope decays the bounce so it dies at the rebound spot.
                float f = (u - 0.18f) / 0.82f;
                float arch = (float)Math.Abs(Math.Cos(f * Math.PI * 1.5f));
                float decay = (float)Math.Pow(1f - f, 1.8);
                h = floorH + (apex - floorH) * arch * decay;
            }

            return new BallState(x, y, h)
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

        /// <summary>Hermite smoothstep on [0,1].</summary>
        private static float SmoothStep(float u) => u * u * (3f - 2f * u);

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
