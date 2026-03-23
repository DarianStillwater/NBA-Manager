using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    public class CourtZoneTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            // Home offense (basket at X=42)
            AssertZone(42f, 0f, true, CourtZone.RestrictedArea, "At basket (home)");
            AssertZone(40f, 0f, true, CourtZone.RestrictedArea, "Near basket (home)");
            AssertZone(38f, 5f, true, CourtZone.Paint, "Paint area (home)");
            // ShortMidRange: outside paint box, 4-10ft from basket
            // Paint box: Abs(X-basket)<8 && Abs(Y)<8, so use Y>=8 to escape
            AssertZone(39f, 9f, true, CourtZone.ShortMidRange, "Short mid-range (home)");
            AssertZone(25f, 0f, true, CourtZone.LongMidRange, "Long mid-range (home, 17ft)");
            AssertZone(18f, 0f, true, CourtZone.ThreePoint, "Top of arc three (home, ~24ft)");

            // Corner threes (distance from basket at corners)
            AssertZone(42f, 22f, true, CourtZone.ThreePoint, "Corner three (home, 22ft)");

            // Away offense (basket at X=-42)
            AssertZone(-42f, 0f, false, CourtZone.RestrictedArea, "At basket (away)");
            AssertZone(-38f, 5f, false, CourtZone.Paint, "Paint area (away)");
            AssertZone(-25f, 0f, false, CourtZone.LongMidRange, "Long mid-range (away, 17ft)");
            AssertZone(-18f, 0f, false, CourtZone.ThreePoint, "Top of arc three (away)");
            AssertZone(-42f, 22f, false, CourtZone.ThreePoint, "Corner three (away)");

            // Symmetry: away positions evaluated against HOME basket should be far away
            var awayPos = new CourtPosition(-25f, 0f);
            AssertEqual(CourtZone.ThreePoint, awayPos.GetZone(true),
                "Away pos with home basket = ThreePoint (67ft away)");

            // Center court is always a three regardless
            AssertEqual(CourtZone.ThreePoint, new CourtPosition(0, 0).GetZone(true),
                "Center court = ThreePoint (home)");
            AssertEqual(CourtZone.ThreePoint, new CourtPosition(0, 0).GetZone(false),
                "Center court = ThreePoint (away)");

            // Distance calculations
            var basket = new CourtPosition(42f, 0f);
            var threePointSpot = new CourtPosition(18f, 0f);
            float dist = threePointSpot.DistanceTo(basket);
            AssertRange(dist, 23f, 25f, "Distance from top-of-key three to basket (~24ft)");

            return (_passed, _failed);
        }

        private void AssertZone(float x, float y, bool offensiveHalf, CourtZone expected, string name)
        {
            var pos = new CourtPosition(x, y);
            var actual = pos.GetZone(offensiveHalf);
            if (actual == expected)
            {
                _passed++;
                Debug.Log($"    PASS: {name} = {actual}");
            }
            else
            {
                _failed++;
                Debug.Log($"    FAIL: {name} — expected {expected}, got {actual} (pos={x},{y} offensive={offensiveHalf})");
            }
        }
    }
}
