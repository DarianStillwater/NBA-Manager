using System;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents a position on the basketball court.
    /// Court dimensions: 94 feet long x 50 feet wide.
    /// Origin (0,0) is at center court.
    /// X: -47 to +47 (length), Y: -25 to +25 (width)
    /// </summary>
    [Serializable]
    public struct CourtPosition
    {
        public float X; // -47 to +47 (half court offense is 0 to 47)
        public float Y; // -25 to +25

        public CourtPosition(float x, float y)
        {
            X = Mathf.Clamp(x, -47f, 47f);
            Y = Mathf.Clamp(y, -25f, 25f);
        }

        // ==================== ZONE DETECTION ====================

        /// <summary>
        /// Gets the shooting zone for this position.
        /// </summary>
        public CourtZone GetZone(bool offensiveHalf)
        {
            float basketX = offensiveHalf ? 42f : -42f;
            float distanceToBasket = DistanceTo(new CourtPosition(basketX, 0));

            // In the paint (restricted area + paint)
            if (Mathf.Abs(X - basketX) < 8f && Mathf.Abs(Y) < 8f)
            {
                if (distanceToBasket < 4f)
                    return CourtZone.RestrictedArea;
                return CourtZone.Paint;
            }

            // Three point line is ~23.75 feet from basket (22 in corners)
            bool isCorner = Mathf.Abs(Y) > 20f && Mathf.Abs(X - basketX) < 14f;
            float threePointDistance = isCorner ? 22f : 23.75f;

            if (distanceToBasket >= threePointDistance)
                return CourtZone.ThreePoint;

            if (distanceToBasket < 10f)
                return CourtZone.ShortMidRange;

            return CourtZone.LongMidRange;
        }

        /// <summary>
        /// Gets the side of the court (Left, Center, Right).
        /// </summary>
        public CourtSide GetSide()
        {
            if (Y < -8f) return CourtSide.Left;
            if (Y > 8f) return CourtSide.Right;
            return CourtSide.Center;
        }

        /// <summary>
        /// Distance to another position in feet.
        /// </summary>
        public float DistanceTo(CourtPosition other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Distance to the basket on the offensive half.
        /// </summary>
        public float DistanceToBasket(bool offensiveEnd)
        {
            float basketX = offensiveEnd ? 42f : -42f;
            return DistanceTo(new CourtPosition(basketX, 0));
        }

        // ==================== PREDEFINED POSITIONS ====================

        public static CourtPosition CenterCourt => new CourtPosition(0, 0);
        public static CourtPosition LeftBasket => new CourtPosition(-42f, 0);
        public static CourtPosition RightBasket => new CourtPosition(42f, 0);

        // Offensive positions (right side of court)
        public static CourtPosition PointGuardSpot => new CourtPosition(25f, 0);
        public static CourtPosition LeftWing => new CourtPosition(30f, -18f);
        public static CourtPosition RightWing => new CourtPosition(30f, 18f);
        public static CourtPosition LeftCorner => new CourtPosition(42f, -22f);
        public static CourtPosition RightCorner => new CourtPosition(42f, 22f);
        public static CourtPosition LeftElbow => new CourtPosition(32f, -8f);
        public static CourtPosition RightElbow => new CourtPosition(32f, 8f);
        public static CourtPosition LeftBlock => new CourtPosition(38f, -6f);
        public static CourtPosition RightBlock => new CourtPosition(38f, 6f);
        public static CourtPosition HighPost => new CourtPosition(32f, 0);
        public static CourtPosition LowPost => new CourtPosition(40f, 0);

        // ==================== UTILITY ====================

        /// <summary>
        /// Lerp between two positions.
        /// </summary>
        public static CourtPosition Lerp(CourtPosition a, CourtPosition b, float t)
        {
            return new CourtPosition(
                Mathf.Lerp(a.X, b.X, t),
                Mathf.Lerp(a.Y, b.Y, t)
            );
        }

        /// <summary>
        /// Move towards a target position by a max distance.
        /// </summary>
        public CourtPosition MoveTowards(CourtPosition target, float maxDistance)
        {
            float dist = DistanceTo(target);
            if (dist <= maxDistance)
                return target;

            float t = maxDistance / dist;
            return Lerp(this, target, t);
        }

        /// <summary>
        /// Returns a random position within a radius.
        /// </summary>
        public CourtPosition RandomOffset(float maxRadius)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = UnityEngine.Random.Range(0f, maxRadius);
            return new CourtPosition(
                X + Mathf.Cos(angle) * radius,
                Y + Mathf.Sin(angle) * radius
            );
        }

        public override string ToString()
        {
            return $"({X:F1}, {Y:F1})";
        }

        public Vector2 ToVector2() => new Vector2(X, Y);
        public Vector3 ToVector3(float height = 0) => new Vector3(X, height, Y);
    }

    public enum CourtZone
    {
        RestrictedArea,  // Under the basket
        Paint,           // In the key
        ShortMidRange,   // Close 2-pointers
        LongMidRange,    // Far 2-pointers
        ThreePoint       // Beyond the arc
    }

    public enum CourtSide
    {
        Left,
        Center,
        Right
    }
}
