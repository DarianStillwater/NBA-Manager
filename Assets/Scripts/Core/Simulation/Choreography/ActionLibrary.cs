using System;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>
    /// The recognizable half-court action a possession is choreographed as. Chosen purely
    /// from already-decided script fields (shot type, shooter/handler roles, strategy) — no
    /// new outcome RNG — so None-vs-Full outcome invariance is preserved.
    /// </summary>
    public enum OffensiveAction
    {
        Transition,
        PickAndRoll,
        PickAndPop,
        DribbleHandoff,
        OffBallScreenCurl,
        Backdoor,
        PostUp,
        Isolation,
        SpotUp
    }

    /// <summary>
    /// The synthesized plan for one possession: which action, who fills each role, and the
    /// key spots. Beat times (ScreenStartT/EndT, RollStartT) are stamped by the choreographer
    /// as it lays the waypoints, then read by DefenseChoreographer to time its reactions.
    /// </summary>
    public sealed class ActionPlan
    {
        public OffensiveAction Action;
        public int Handler;            // ball-handler (= InitialBallHandlerIndex)
        public int Shooter;            // decided shooter (-1 on non-shot possessions)
        public int Screener = -1;      // on/off-ball screener (-1 if none)
        public int Cutter = -1;        // off-ball cutter / curl man (-1 if none)
        public int PostIndex = -1;     // big who gets an interior touch (-1 if none)
        public int SideY = 1;          // strong-side sign (+1 = +Y, -1 = -Y)
        public bool InteriorTouch;     // route the ball inside before the assist

        public CourtPosition ScreenSpot;   // where the screen is set
        public CourtPosition RollTarget;    // roller's destination (front of rim)
        public CourtPosition PopTarget;      // popper's destination (elbow/arc)

        // Filled while laying waypoints (defense timing):
        public float ScreenStartT, ScreenEndT, RollStartT;
    }

    /// <summary>Pure selection + spot math for the offensive action. No side effects, no UnityEngine.</summary>
    public static class ActionLibrary
    {
        public static ActionPlan Choose(PossessionScript s, System.Random rng)
        {
            int handler = Math.Max(s.InitialBallHandlerIndex, 0);
            int shooter = s.ShooterIndex;
            bool attacksRight = s.OffenseAttacksRight;
            float rimX = CourtGeometry.RimXFor(attacksRight);

            var plan = new ActionPlan
            {
                Handler = handler,
                Shooter = shooter,
                SideY = s.ShotPosition.Y >= 0f ? 1 : -1
            };

            var strat = s.OffenseStrategy;
            bool postHeavy = strat != null && strat.PostUpFrequency > 50;

            // Non-shot possession (turnover/violation/steal): iso feel or transition.
            if (shooter < 0)
            {
                plan.Action = s.IsFastBreak ? OffensiveAction.Transition : OffensiveAction.Isolation;
                return Finalize(plan, s, rng, rimX, attacksRight);
            }

            bool shooterIsBig = shooter >= 3;
            bool passedTo = s.BallHandlerPassedToShooter && shooter != handler;
            bool driveShot = s.ShotType == ShotType.Dunk || s.ShotType == ShotType.Layup || s.ShotType == ShotType.Floater;
            float shotDist = s.ShotPosition.DistanceTo(new CourtPosition(rimX, 0f));
            bool isThree = shotDist > 22f;

            if (s.IsFastBreak)
                plan.Action = OffensiveAction.Transition;
            else if (shooterIsBig && postHeavy && !driveShot)
                plan.Action = OffensiveAction.PostUp;
            else if (shooterIsBig && driveShot && passedTo)
                plan.Action = OffensiveAction.PickAndRoll;
            else if (shooterIsBig && passedTo)
                plan.Action = OffensiveAction.PickAndPop;
            else if (shooterIsBig)
                plan.Action = OffensiveAction.PostUp;
            else if (passedTo && driveShot)
                plan.Action = OffensiveAction.Backdoor;
            else if (passedTo && isThree)
                plan.Action = ThreePref(strat) > rng.NextDouble()
                    ? OffensiveAction.OffBallScreenCurl : OffensiveAction.SpotUp;
            else if (passedTo)
                plan.Action = (rng.NextDouble() < 0.5) ? OffensiveAction.DribbleHandoff : OffensiveAction.SpotUp;
            else
                plan.Action = OffensiveAction.Isolation;   // handler creates for himself

            return Finalize(plan, s, rng, rimX, attacksRight);
        }

        private static ActionPlan Finalize(ActionPlan plan, PossessionScript s, System.Random rng,
            float rimX, bool attacksRight)
        {
            float m = attacksRight ? 1f : -1f;
            int sy = plan.SideY;

            switch (plan.Action)
            {
                case OffensiveAction.PickAndRoll:
                    plan.Screener = plan.Shooter == plan.Handler ? PickBig(s, plan.Handler, plan.Shooter, rng) : plan.Shooter;
                    plan.InteriorTouch = false; // the roll IS the interior action
                    break;
                case OffensiveAction.PickAndPop:
                    plan.Screener = plan.Shooter == plan.Handler ? PickBig(s, plan.Handler, plan.Shooter, rng) : plan.Shooter;
                    break;
                case OffensiveAction.PostUp:
                    plan.PostIndex = plan.Shooter >= 0 ? plan.Shooter : PickBig(s, plan.Handler, -1, rng);
                    plan.InteriorTouch = true;
                    break;
                case OffensiveAction.OffBallScreenCurl:
                    plan.Screener = PickBig(s, plan.Handler, plan.Shooter, rng);
                    plan.Cutter = plan.Shooter;
                    break;
                case OffensiveAction.Backdoor:
                    plan.Cutter = plan.Shooter;
                    break;
                case OffensiveAction.DribbleHandoff:
                case OffensiveAction.Isolation:
                case OffensiveAction.SpotUp:
                case OffensiveAction.Transition:
                    break;
            }

            // Occasionally route the ball through a post touch even on perimeter actions.
            bool postHeavy = s.OffenseStrategy != null && s.OffenseStrategy.PostUpFrequency > 35;
            if (!plan.InteriorTouch && plan.Action != OffensiveAction.PickAndRoll &&
                postHeavy && rng.NextDouble() < 0.4)
            {
                int big = PickBig(s, plan.Handler, plan.Shooter, rng);
                if (big >= 0) { plan.PostIndex = big; plan.InteriorTouch = true; }
            }

            // Key spots (authored attacking +X, mirrored by m).
            plan.ScreenSpot = FormationLibrary.Clamp(new CourtPosition(30f * m, sy * 3f));
            plan.RollTarget = FormationLibrary.Clamp(new CourtPosition(rimX - 4f * m, sy * 2f));
            plan.PopTarget = FormationLibrary.Clamp(new CourtPosition(29f * m, sy * 9f));
            return plan;
        }

        /// <summary>Preference weight for running an off-ball screen (vs. a simple spot-up) for threes.</summary>
        private static double ThreePref(TeamStrategy strat)
        {
            if (strat == null) return 0.5;
            return Math.Min(0.8, 0.35 + strat.ThreePointFrequency / 200.0);
        }

        /// <summary>Pick a big (index 3/4) that isn't the handler/shooter; fall back to any other index.</summary>
        private static int PickBig(PossessionScript s, int exclude1, int exclude2, System.Random rng)
        {
            // Prefer a real big
            int[] bigs = { 4, 3 };
            foreach (int b in bigs)
                if (b != exclude1 && b != exclude2) return b;
            // Fall back to any other index (deterministic-ish tie-break via rng)
            int start = rng.Next(5);
            for (int k = 0; k < 5; k++)
            {
                int i = (start + k) % 5;
                if (i != exclude1 && i != exclude2) return i;
            }
            return -1;
        }
    }
}
