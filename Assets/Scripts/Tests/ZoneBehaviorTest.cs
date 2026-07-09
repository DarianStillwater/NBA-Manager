using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the organic-zone targeting math: the defender whose area holds the
    /// ball closes out between ball and rim, neighbors pinch the gap, the weak
    /// side sinks into the paint, a 1-3-1 converges two trappers on a corner
    /// ball, and a 2-3 drops the off top man to the elbow on a wing ball.
    /// Pure and deterministic — same inputs, same targets.
    /// </summary>
    public class ZoneBehaviorTest : BaseTest
    {
        private const float RimX = 41.75f; // attacking right

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestBallAreaClosesOut();
            TestWeakSideSinks();
            TestCornerTrap131();
            TestWingElbowDrop23();
            TestRolesDiffer();
            TestDeterminism();

            return (_passed, _failed);
        }

        /// <summary>2-3 formation attacking right (rim at +X).</summary>
        private DefenderAssignment[] Plan23()
        {
            var s = new PossessionScript { OffenseAttacksRight = true, IsFastBreak = false };
            return DefenseChoreographer.BuildPlan(null, s, new System.Random(5), DefensiveSchemeType.Zone2_3);
        }

        private DefenderAssignment[] Plan131()
        {
            var s = new PossessionScript { OffenseAttacksRight = true, IsFastBreak = false };
            return DefenseChoreographer.BuildPlan(null, s, new System.Random(5), DefensiveSchemeType.Zone1_3_1);
        }

        private void TestBallAreaClosesOut()
        {
            var plan = Plan23();
            var ball = new CourtPosition(25f, 8f);   // right wing, high

            int owner = -1; float best = float.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                float d = plan[i].ZoneSpot.DistanceTo(ball);
                if (d < best) { best = d; owner = i; }
            }

            var (target, role) = ZoneBehavior.ComputeTarget(DefensiveSchemeType.Zone2_3, plan, owner, ball, RimX);
            AssertEqual(ZoneRole.BallArea, role, "Nearest-area defender takes the ball");
            Assert(target.DistanceTo(ball) < 2f, "Closeout target is at the ball");
            Assert(target.X > ball.X, "Closeout positions between ball and the rim");
        }

        private void TestWeakSideSinks()
        {
            var plan = Plan23();
            var ball = new CourtPosition(25f, 18f);  // deep right side

            // The far-side back-line defender (most negative Y anchor) is weak side.
            int weak = 0; float minY = float.MaxValue;
            for (int i = 0; i < 5; i++)
                if (plan[i].ZoneSpot.Y < minY) { minY = plan[i].ZoneSpot.Y; weak = i; }

            var (target, role) = ZoneBehavior.ComputeTarget(DefensiveSchemeType.Zone2_3, plan, weak, ball, RimX);
            Assert(role == ZoneRole.WeakSide || role == ZoneRole.Adjacent,
                $"Far-side defender is weak-side/adjacent ({role})");
            Assert(target.DistanceTo(plan[weak].ZoneSpot) > 0.5f, "Weak side doesn't stand frozen on his anchor");
            Assert(System.Math.Abs(target.Y) < System.Math.Abs(plan[weak].ZoneSpot.Y) + 0.1f,
                "Weak side sinks toward the middle, not away from it");
        }

        private void TestCornerTrap131()
        {
            var plan = Plan131();
            var corner = new CourtPosition(43f, 20f); // deep right corner

            int traps = 0;
            for (int i = 0; i < 5; i++)
            {
                if (!plan[i].IsZone) continue;
                var (target, role) = ZoneBehavior.ComputeTarget(DefensiveSchemeType.Zone1_3_1, plan, i, corner, RimX);
                if (role == ZoneRole.Trap)
                {
                    traps++;
                    Assert(target.DistanceTo(corner) < 3.5f, "Trapper converges on the corner ball");
                }
            }
            AssertEqual(2, traps, "1-3-1 sends exactly two trappers to a corner ball");

            // Same ball, 2-3 scheme: no trap.
            var plan23 = Plan23();
            int traps23 = 0;
            for (int i = 0; i < 5; i++)
            {
                var (_, role) = ZoneBehavior.ComputeTarget(DefensiveSchemeType.Zone2_3, plan23, i, corner, RimX);
                if (role == ZoneRole.Trap) traps23++;
            }
            AssertEqual(0, traps23, "A 2-3 never corner-traps");
        }

        private void TestWingElbowDrop23()
        {
            var plan = Plan23();
            var wing = new CourtPosition(28f, 12f);  // right wing

            // Identify the two front-line defenders (farthest anchors from the rim).
            var front = Enumerable.Range(0, 5)
                .OrderByDescending(i => System.Math.Abs(RimX - plan[i].ZoneSpot.X))
                .Take(2).ToArray();

            // Whichever front man does NOT own the ball should collapse toward the elbow.
            int owner = -1; float best = float.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                float d = plan[i].ZoneSpot.DistanceTo(wing);
                if (d < best) { best = d; owner = i; }
            }
            int offTop = front.First(i => i != owner);

            var (target, role) = ZoneBehavior.ComputeTarget(DefensiveSchemeType.Zone2_3, plan, offTop, wing, RimX);
            Assert(role == ZoneRole.Adjacent, "Off top man rotates (adjacent), not frozen");
            Assert(target.X > plan[offTop].ZoneSpot.X - 0.1f,
                "Off top man drops toward the elbow/high post, protecting the middle");
            Assert(System.Math.Abs(target.Y) < System.Math.Abs(plan[offTop].ZoneSpot.Y) + 5f,
                "Elbow drop heads to the middle of the floor");
        }

        private void TestRolesDiffer()
        {
            var plan = Plan23();
            var ball = new CourtPosition(25f, 8f);
            var roles = Enumerable.Range(0, 5)
                .Select(i => ZoneBehavior.ComputeTarget(DefensiveSchemeType.Zone2_3, plan, i, ball, RimX).role)
                .ToArray();
            Assert(roles.Distinct().Count() >= 2,
                "Zone defenders play distinct roles, not one synchronized slide");
            AssertEqual(1, roles.Count(r => r == ZoneRole.BallArea), "Exactly one defender owns the ball");
        }

        private void TestDeterminism()
        {
            var plan = Plan23();
            var ball = new CourtPosition(30f, -14f);
            for (int i = 0; i < 5; i++)
            {
                var a = ZoneBehavior.ComputeTarget(DefensiveSchemeType.Zone2_3, plan, i, ball, RimX);
                var b = ZoneBehavior.ComputeTarget(DefensiveSchemeType.Zone2_3, plan, i, ball, RimX);
                Assert(Mathf.Approximately(a.target.X, b.target.X) &&
                       Mathf.Approximately(a.target.Y, b.target.Y) && a.role == b.role,
                    $"Defender {i} targeting is pure/deterministic");
            }
        }
    }
}
