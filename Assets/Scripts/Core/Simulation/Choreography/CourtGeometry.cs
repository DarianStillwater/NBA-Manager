namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>
    /// Shared court geometry constants used by BOTH the choreographer and the
    /// court renderer, so the ball's flight always lands on the drawn rim.
    /// Court space: X [-47, +47] ft (94 ft), Y [-25, +25] ft (50 ft), origin at center court.
    /// </summary>
    public static class CourtGeometry
    {
        public const float HalfLength = 47f;
        public const float HalfWidth = 25f;

        /// <summary>Rim center distance from center court (5.25 ft from baseline).</summary>
        public const float RimX = 41.75f;

        /// <summary>Backboard plane distance from center court (4 ft from baseline).</summary>
        public const float BackboardX = 43f;

        /// <summary>Rim height in feet.</summary>
        public const float RimHeight = 10f;

        /// <summary>Free throw line distance from center court (19 ft from baseline → 47-19=28).</summary>
        public const float FreeThrowLineX = 28f;

        /// <summary>Ball height while held/dribbled.</summary>
        public const float HeldBallHeight = 4f;

        public static float RimXFor(bool attacksRight) => attacksRight ? RimX : -RimX;
        public static float BackboardXFor(bool attacksRight) => attacksRight ? BackboardX : -BackboardX;
    }
}
