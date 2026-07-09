using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the zone-visuals fix: defender dots used to render man-to-man no
    /// matter the called scheme. Zone schemes must produce formation-anchored
    /// assignments (shaped like the zone), man stays untouched, junk defenses
    /// keep exactly their chasers in man coverage, and transition possessions
    /// never set a zone.
    /// </summary>
    public class ZoneChoreographyTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestManUnchanged();
            TestZone23Shape();
            TestZone131Shape();
            TestBoxAndOneChaser();
            TestTriangleAndTwoChasers();
            TestTransitionNeverZones();
            TestDeterministicGivenSeed();

            return (_passed, _failed);
        }

        private PossessionScript MakeScript(bool fastBreak = false, bool attacksRight = true)
        {
            return new PossessionScript
            {
                IsFastBreak = fastBreak,
                OffenseAttacksRight = attacksRight
            };
        }

        private ActionPlan MakePlan(int shooter = 2, int handler = 0)
        {
            return new ActionPlan
            {
                Action = OffensiveAction.Isolation,
                Handler = handler,
                Shooter = shooter
            };
        }

        private void TestManUnchanged()
        {
            var d = DefenseChoreographer.BuildPlan(MakePlan(), MakeScript(),
                new System.Random(7), DefensiveSchemeType.ManToManStandard);
            Assert(d.All(a => !a.IsZone), "Man scheme has no zone assignments");
            for (int i = 0; i < 5; i++)
                AssertEqual(i, d[i].ManIndex, $"Man defender {i} guards his own man");
        }

        private void TestZone23Shape()
        {
            var d = DefenseChoreographer.BuildPlan(MakePlan(), MakeScript(),
                new System.Random(7), DefensiveSchemeType.Zone2_3);
            AssertEqual(5, d.Count(a => a.IsZone), "2-3 zone: all five hold zone spots");
            Assert(d.Select(a => (a.ZoneSpot.X, a.ZoneSpot.Y)).Distinct().Count() == 5,
                "2-3 zone spots are distinct");

            // Attacking right: rim at +X. The back line (3 bodies) sits closer to
            // the rim (larger X) than the front two.
            float maxX = d.Max(a => a.ZoneSpot.X);
            int backLine = d.Count(a => a.ZoneSpot.X > maxX - 1f);
            AssertEqual(3, backLine, "2-3: three defenders anchored across the paint");
            Assert(d.All(a => a.ZoneSpot.X > 20f), "2-3 spots are in the defensive half (rim side)");
        }

        private void TestZone131Shape()
        {
            var d = DefenseChoreographer.BuildPlan(MakePlan(), MakeScript(),
                new System.Random(7), DefensiveSchemeType.Zone1_3_1);
            AssertEqual(5, d.Count(a => a.IsZone), "1-3-1: all five zone up");
            // point / middle-three / rover = three distinct depths from the rim
            int depths = d.Select(a => Mathf.RoundToInt(a.ZoneSpot.X)).Distinct().Count();
            AssertEqual(3, depths, "1-3-1 has three distinct lines");
        }

        private void TestBoxAndOneChaser()
        {
            var d = DefenseChoreographer.BuildPlan(MakePlan(shooter: 2), MakeScript(),
                new System.Random(7), DefensiveSchemeType.BoxAndOne);
            AssertEqual(4, d.Count(a => a.IsZone), "Box-and-one: four in the box");
            AssertEqual(1, d.Count(a => !a.IsZone), "Box-and-one: exactly one chaser");
            Assert(!d[2].IsZone && d[2].ManIndex == 2, "The chaser face-guards the star (shooter)");
            Assert(d[2].SagDepth < 0.12f, "The chaser plays tighter than normal man");
        }

        private void TestTriangleAndTwoChasers()
        {
            var d = DefenseChoreographer.BuildPlan(MakePlan(shooter: 2, handler: 0), MakeScript(),
                new System.Random(7), DefensiveSchemeType.TriangleAndTwo);
            AssertEqual(3, d.Count(a => a.IsZone), "Triangle-and-two: three in the triangle");
            Assert(!d[2].IsZone && !d[0].IsZone, "Both chasers man up the shooter and handler");
        }

        private void TestTransitionNeverZones()
        {
            var d = DefenseChoreographer.BuildPlan(MakePlan(), MakeScript(fastBreak: true),
                new System.Random(7), DefensiveSchemeType.Zone2_3);
            Assert(d.All(a => !a.IsZone), "A zone can't set in transition — fast breaks stay man/scramble");
        }

        private void TestDeterministicGivenSeed()
        {
            var a = DefenseChoreographer.BuildPlan(MakePlan(), MakeScript(),
                new System.Random(99), DefensiveSchemeType.Zone3_2);
            var b = DefenseChoreographer.BuildPlan(MakePlan(), MakeScript(),
                new System.Random(99), DefensiveSchemeType.Zone3_2);
            bool same = true;
            for (int i = 0; i < 5; i++)
                same &= a[i].IsZone == b[i].IsZone &&
                        Mathf.Approximately(a[i].ZoneSpot.X, b[i].ZoneSpot.X) &&
                        Mathf.Approximately(a[i].ZoneSpot.Y, b[i].ZoneSpot.Y) &&
                        Mathf.Approximately(a[i].SagDepth, b[i].SagDepth);
            Assert(same, "Zone plan is deterministic for a given seed");

            // Attacking left mirrors the formation to the other rim
            var left = DefenseChoreographer.BuildPlan(MakePlan(), MakeScript(attacksRight: false),
                new System.Random(99), DefensiveSchemeType.Zone2_3);
            Assert(left.All(z => z.ZoneSpot.X < -20f), "Attacking left anchors the zone at the -X rim");
        }
    }
}
