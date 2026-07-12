using System;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>How a pass travels: flat chest pass, off-the-floor bounce pass, or a high lob.</summary>
    public enum PassStyle
    {
        Chest,
        Bounce,
        Lob
    }

    /// <summary>
    /// Pure math for continuous ball trajectories: pass arcs, shot parabolas to the rim,
    /// through-the-net follow-through, rim caroms to a rebound spot. All sampled by
    /// normalized progress u ∈ [0,1] so the choreographer can emit ticks at any density.
    /// Shot flights are differentiated by ShotType so a dunk's low carried thrust looks
    /// nothing like a rainbow three or a soft layup glide.
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
            => ShotDuration(from, rimX, null);

        /// <summary>Flight time by shot type: dunks are a violent instant, threes hang.</summary>
        public static float ShotDuration(CourtPosition from, float rimX, ShotType? type)
        {
            float dist = Dist(from, rimX);
            switch (type)
            {
                case ShotType.Dunk: return Clamp(0.3f + dist / 30f, 0.3f, 0.5f);
                case ShotType.TipIn: return 0.35f;
                case ShotType.Layup: return Clamp(0.45f + dist / 40f, 0.45f, 0.65f);
                case ShotType.Floater: return Clamp(0.9f + dist / 60f, 0.9f, 1.15f);
                case ShotType.Hookshot: return Clamp(0.8f + dist / 60f, 0.8f, 1.05f);
                case ShotType.Heave: return Clamp(1.2f + dist / 40f, 1.4f, 1.9f);
                default:
                    float baseDur = 0.7f + dist / 45f;
                    if (dist > 22f) baseDur *= 1.25f;             // three: long hang
                    return Clamp(baseDur, 0.7f, 1.7f);
            }
        }

        /// <summary>Gather/windup time before release: a dunk explodes, a jumper rises and sets.</summary>
        public static float WindupFor(ShotType? type)
        {
            switch (type)
            {
                case ShotType.Dunk: return 0.25f;
                case ShotType.TipIn: return 0.1f;
                case ShotType.Layup: return 0.3f;
                case ShotType.CatchAndShoot: return 0.25f;
                case ShotType.Floater: return 0.3f;   // quick teardrop pop, distinct from a set jumper
                default: return 0.4f;
            }
        }

        /// <summary>Through-the-net time: a dunk snaps the net; a jumper drips through.</summary>
        public static float MakeFollowThroughFor(ShotType? type)
        {
            switch (type)
            {
                case ShotType.Dunk: return 0.12f;
                case ShotType.TipIn: return 0.2f;
                default: return MakeFollowThroughDuration;
            }
        }

        public const float MakeFollowThroughDuration = 0.3f;
        public const float CaromDuration = 0.9f;

        // ── Sampling ───────────────────────────────────────────────

        /// <summary>Pass in flight. Chest = flat arc; Bounce = off the floor with a cusp at
        /// contact (~60% of the way); Lob = high floating arc for backdoors/hit-aheads.</summary>
        public static BallState SamplePass(CourtPosition from, CourtPosition to, float u,
            PassStyle style = PassStyle.Chest)
        {
            float dist = from.DistanceTo(to);
            float height;
            switch (style)
            {
                case PassStyle.Bounce:
                {
                    // Hand (~3.5 ft) down to the floor at u≈0.6, then up into the catcher's hands.
                    const float contactU = 0.6f;
                    if (u < contactU)
                        height = Lerp(3.5f, 0.2f, u / contactU);
                    else
                        height = Lerp(0.2f, 3f, (u - contactU) / (1f - contactU));
                    break;
                }
                case PassStyle.Lob:
                    height = Lerp(5f, 6f, u) + 5f * Arc(u);   // apex ~10 ft
                    break;
                default:
                    height = 5f + (dist > 25f ? 4f : 1.5f) * Arc(u);
                    break;
            }

            return new BallState(
                Lerp(from.X, to.X, u),
                Lerp(from.Y, to.Y, u),
                height)
            {
                Status = BallStatus.InAir_Pass,
                HeldByPlayerId = null
            };
        }

        /// <summary>
        /// Shot in flight from the shot position to the rim. Shape is keyed by ShotType:
        /// dunk = low fast carried thrust; layup = soft low glide; floater = high touch arc;
        /// three = long-hang rainbow; heave = moonball; jumpers = standard arc by distance.
        /// </summary>
        public static BallState SampleShot(CourtPosition from, float rimX, float u, ShotType? shotType = null)
        {
            ShotShape(shotType, Dist(from, rimX), out float release, out float apexExtra);

            float height = Lerp(release, CourtGeometry.RimHeight, u) + apexExtra * Arc(u);
            return new BallState(
                Lerp(from.X, rimX, u),
                Lerp(from.Y, 0f, u),
                height)
            {
                Status = BallStatus.InAir_Shot,
                HeldByPlayerId = null,
                ShotStyle = shotType
            };
        }

        /// <summary>
        /// Bank shot in flight: the standard per-type arc aimed at a glass point just above the
        /// rim, then a short hop off the backboard down onto the rim center. Occupies the SAME
        /// flight window as a straight shot, so the rim-arrival time (and everything keyed to
        /// it) is unchanged. uGlass = normalized progress at glass contact.
        /// </summary>
        public static BallState SampleBankShot(CourtPosition from, float rimX, float glassY,
            float u, float uGlass, ShotType? shotType)
        {
            float glassX = CourtGeometry.BackboardXFor(rimX > 0f);
            const float glassHeight = 11f;

            if (u < uGlass)
            {
                float v = uGlass <= 0.0001f ? 1f : u / uGlass;
                ShotShape(shotType, Dist(from, rimX), out float release, out float apexExtra);
                float height = Lerp(release, glassHeight, v) + apexExtra * Arc(v);
                return new BallState(
                    Lerp(from.X, glassX, v),
                    Lerp(from.Y, glassY, v),
                    height)
                {
                    Status = BallStatus.InAir_Shot,
                    HeldByPlayerId = null,
                    ShotStyle = shotType
                };
            }

            // Off the glass, down onto the rim (~1-2.5 ft of ground travel over the hop).
            float w = (u - uGlass) / Math.Max(1f - uGlass, 0.0001f);
            return new BallState(
                Lerp(glassX, rimX, w),
                Lerp(glassY, 0f, w),
                Lerp(glassHeight, CourtGeometry.RimHeight, w))
            {
                Status = BallStatus.InAir_Shot,
                HeldByPlayerId = null,
                ShotStyle = shotType
            };
        }

        /// <summary>Release height + arc shape by shot type (shared by straight and bank shots).</summary>
        private static void ShotShape(ShotType? shotType, float dist, out float release, out float apexExtra)
        {
            switch (shotType)
            {
                case ShotType.Dunk:     release = 8.5f; apexExtra = 1.2f; break;
                case ShotType.TipIn:    release = 9.5f; apexExtra = 1.0f; break;
                case ShotType.Layup:    release = 6.5f; apexExtra = 2.0f; break;
                case ShotType.Floater:  release = 8f;   apexExtra = 6f;   break;
                case ShotType.Hookshot: release = 8.5f; apexExtra = 4.5f; break;
                case ShotType.Heave:    release = 7f;   apexExtra = 14f;  break;
                default:
                    release = 7f;
                    apexExtra = dist > 22f ? 9f : dist > 12f ? 5.5f : 4f;
                    break;
            }
        }

        /// <summary>Made shot: ball drops from the rim down through the net (punched through on dunks).</summary>
        public static BallState SampleMadeFollowThrough(float rimX, float u, ShotType? shotType = null)
        {
            float settle = shotType == ShotType.Dunk ? 4.5f : 5.5f;
            return new BallState(rimX, 0f, Lerp(CourtGeometry.RimHeight, settle, u))
            {
                Status = BallStatus.InAir_Shot,
                HeldByPlayerId = null,
                ShotStyle = shotType
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

        /// <summary>
        /// Shot-line-aware carom for long misses: FRONT iron kicks the ball back out along the
        /// rim→shooter line (±25° spray); BACK iron pushes it the opposite way past the rim
        /// toward the baseline. Rebounds land where that shot would actually put them.
        /// </summary>
        public static CourtPosition ComputeReboundSpot(CourtPosition shotPos, float rimX,
            System.Random rng, bool frontIron)
        {
            float dx = shotPos.X - rimX, dy = shotPos.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return ComputeReboundSpot(shotPos, rimX, rng);
            dx /= len; dy /= len;

            float spray = ((float)rng.NextDouble() * 2f - 1f) * 0.436f;   // ±25°
            float cos = (float)Math.Cos(spray), sin = (float)Math.Sin(spray);
            float rx = dx * cos - dy * sin;
            float ry = dx * sin + dy * cos;

            float dist = frontIron
                ? 4f + (float)rng.NextDouble() * 8f       // back toward the shot
                : -(3f + (float)rng.NextDouble() * 4f);   // long, over the back iron

            return FormationLibrary.Clamp(new CourtPosition(rimX + rx * dist, ry * dist));
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
