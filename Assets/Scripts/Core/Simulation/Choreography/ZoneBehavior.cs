using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>What a zone defender is doing right now, given where the ball is.</summary>
    public enum ZoneRole
    {
        BallArea,   // the ball is in my area — close out and pressure
        Adjacent,   // one pass away — pinch the gap toward the ball
        WeakSide,   // far side — sink toward the paint, split two men
        Trap        // converge on the ball with a partner (1-3-1 corner trap)
    }

    /// <summary>
    /// Pure per-tick zone targeting. Real zones don't slide around as a rigid
    /// shape: the defender whose area holds the ball closes out tight, the
    /// neighbors pinch the gaps, the weak side sinks into the paint, and a
    /// 1-3-1 converges two bodies when the ball reaches a corner. This class
    /// turns (scheme, formation anchors, ball position) into per-defender
    /// targets — deterministic, no RNG, no state — so it is directly testable
    /// and safe inside the choreographer's no-per-tick-RNG rule.
    /// </summary>
    public static class ZoneBehavior
    {
        public static (CourtPosition target, ZoneRole role) ComputeTarget(
            DefensiveSchemeType scheme, DefenderAssignment[] plan, int i, CourtPosition ball, float rimX)
        {
            float sign = rimX > 0f ? 1f : -1f;
            var rim = new CourtPosition(rimX, 0f);
            var paint = new CourtPosition(rimX - sign * 7f, 0f);

            // Rank zone defenders by how close their area anchor is to the ball.
            int owner = -1, second = -1;
            float ownerDist = float.MaxValue, secondDist = float.MaxValue;
            for (int d = 0; d < plan.Length; d++)
            {
                if (!plan[d].IsZone) continue;
                float dist = plan[d].ZoneSpot.DistanceTo(ball);
                if (dist < ownerDist) { second = owner; secondDist = ownerDist; owner = d; ownerDist = dist; }
                else if (dist < secondDist) { second = d; secondDist = dist; }
            }

            var mySpot = plan[i].ZoneSpot;
            float myDist = mySpot.DistanceTo(ball);

            // 1-3-1 corner trap: two out-of-bounds lines make the corner the
            // primary trap zone — the two nearest defenders converge on the catch.
            if (scheme == DefensiveSchemeType.Zone1_3_1 && InCornerBox(ball, rimX, sign) &&
                (i == owner || i == second))
            {
                // Trappers arrive on either shoulder of the ball, sealing the sideline.
                float side = i == owner ? 0.9f : -0.9f;
                var trapSpot = new CourtPosition(
                    ball.X + sign * 1.2f,                     // between ball and midcourt escape
                    ball.Y + (ball.Y > 0 ? -1f : 1f) * side); // shoulder offsets differ per trapper
                return (trapSpot, ZoneRole.Trap);
            }

            // Ball in my area: close out — position between ball and the rim.
            if (i == owner)
                return (ball.MoveTowards(rim, 1.2f), ZoneRole.BallArea);

            // 2-3 family: on a wing ball, the OTHER top-line defender drops to the
            // elbow to wall off the high post (the signature 2-3 rotation).
            if ((scheme == DefensiveSchemeType.Zone2_3 || scheme == DefensiveSchemeType.MatchupZone) &&
                IsWingBall(ball, rimX, sign) && IsTopLine(plan, i, rimX, sign) && i != owner)
            {
                var elbow = new CourtPosition(rimX - sign * 13f, ball.Y > 0 ? 4f : -4f);
                return (Blend(mySpot, elbow, 0.7f), ZoneRole.Adjacent);
            }

            // One pass away: pinch the gap toward the ball.
            if (myDist < 18f)
                return (Blend(mySpot, ball, 0.40f), ZoneRole.Adjacent);

            // Weak side: sink toward the paint, shading a step toward the ball side.
            var sink = Blend(mySpot, paint, 0.45f);
            float shade = ball.Y > 0f ? 2.5f : -2.5f;
            return (new CourtPosition(sink.X, sink.Y + shade * 0.5f), ZoneRole.WeakSide);
        }

        /// <summary>Corner box: deep and wide on the defense's end.</summary>
        public static bool InCornerBox(CourtPosition ball, float rimX, float sign)
        {
            return sign * ball.X > CourtGeometry.HalfLength - 10f &&
                   System.Math.Abs(ball.Y) > 13f;
        }

        private static bool IsWingBall(CourtPosition ball, float rimX, float sign)
        {
            float fromBaseline = CourtGeometry.HalfLength - sign * ball.X;
            return System.Math.Abs(ball.Y) > 8f && fromBaseline > 10f && fromBaseline < 26f;
        }

        /// <summary>Top-line = zone anchor farther from the rim than the formation's median.</summary>
        private static bool IsTopLine(DefenderAssignment[] plan, int i, float rimX, float sign)
        {
            float mine = System.Math.Abs(rimX - plan[i].ZoneSpot.X);
            int farther = 0, zoners = 0;
            for (int d = 0; d < plan.Length; d++)
            {
                if (!plan[d].IsZone) continue;
                zoners++;
                if (System.Math.Abs(rimX - plan[d].ZoneSpot.X) > mine) farther++;
            }
            // In a 2-3 the two front men are the two farthest from the rim.
            return zoners > 0 && farther <= 1;
        }

        private static CourtPosition Blend(CourtPosition a, CourtPosition b, float u)
        {
            return new CourtPosition(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u);
        }
    }
}
