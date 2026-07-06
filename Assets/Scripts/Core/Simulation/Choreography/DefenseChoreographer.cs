namespace NBAHeadCoach.Core.Simulation.Choreography
{
    /// <summary>How a defender handles a ball screen.</summary>
    public enum ScreenNavigation
    {
        FightOver,   // trail over the top (default)
        GoUnder,     // slip under the screen
        Switch,      // swap assignments with the screener's defender
        Hedge        // screener's defender shows/hedges, then recovers
    }

    /// <summary>
    /// Per-defender assignment for a possession — decided ONCE (never per tick), which is what
    /// removes the old back-and-forth twitch. Sag depth, matchup, screen navigation, and help
    /// responsibilities are all fixed here so the integrator only chases smooth targets.
    /// </summary>
    public struct DefenderAssignment
    {
        public int ManIndex;         // offensive player this defender guards (swaps on a Switch)
        public float SagDepth;       // 0 = on the man … 1 = at the rim (off-ball sag)
        public float OnBallPressure; // sag used while actually guarding the ball
        public ScreenNavigation Nav;
        public bool IsHelper;        // low man responsible for tagging the roller
        public int TagIndex;         // offensive player the helper rotates to (-1 = none)
    }

    /// <summary>Pure defensive assignment planner. No side effects, no UnityEngine, no per-tick RNG.</summary>
    public static class DefenseChoreographer
    {
        public static DefenderAssignment[] BuildPlan(ActionPlan plan, PossessionScript s, System.Random rng)
        {
            var d = new DefenderAssignment[5];
            for (int i = 0; i < 5; i++)
            {
                d[i] = new DefenderAssignment
                {
                    ManIndex = i,
                    OnBallPressure = 0.12f,
                    SagDepth = SagForRole(i, plan, rng),
                    Nav = ScreenNavigation.FightOver,
                    IsHelper = false,
                    TagIndex = -1
                };
            }

            // On-ball screen coverage (PnR/PnP only): the handler's and screener's defenders
            // share a scheme. Off-ball screens (curls) don't reassign the on-ball defender.
            bool onBallScreen = plan != null &&
                (plan.Action == OffensiveAction.PickAndRoll || plan.Action == OffensiveAction.PickAndPop);
            if (onBallScreen && plan.Screener >= 0 && plan.Screener != plan.Handler)
            {
                var nav = PickNav(rng);
                d[plan.Handler].Nav = nav;
                d[plan.Screener].Nav = nav;

                if (nav == ScreenNavigation.Switch)
                {
                    // Swap the two matchups for the rest of the possession.
                    d[plan.Handler].ManIndex = plan.Screener;
                    d[plan.Screener].ManIndex = plan.Handler;
                }
                else
                {
                    // A weak-side low man tags the roller/popper, then recovers.
                    int helper = WeakSideDefender(plan);
                    if (helper >= 0)
                    {
                        d[helper].IsHelper = true;
                        d[helper].TagIndex = plan.Screener;
                    }
                }
            }

            return d;
        }

        private static float SagForRole(int i, ActionPlan plan, System.Random rng)
        {
            // ±0.03 jitter drawn ONCE (not per tick) so the target is stable.
            float sag = 0.26f + (float)(rng.NextDouble() * 0.06 - 0.03);
            if (i >= 3) sag += 0.08f;                          // bigs sag off toward the rim
            if (plan != null && i == plan.Shooter) sag -= 0.04f; // the shooter's man plays a touch tighter
            if (sag < 0.12f) sag = 0.12f;
            if (sag > 0.42f) sag = 0.42f;
            return sag;
        }

        private static ScreenNavigation PickNav(System.Random rng)
        {
            double r = rng.NextDouble();
            if (r < 0.5) return ScreenNavigation.FightOver;
            if (r < 0.7) return ScreenNavigation.GoUnder;
            if (r < 0.9) return ScreenNavigation.Switch;
            return ScreenNavigation.Hedge;
        }

        private static int WeakSideDefender(ActionPlan plan)
        {
            for (int i = 0; i < 4; i++)
                if (i != plan.Handler && i != plan.Screener && i != plan.Shooter)
                    return i;
            return -1;
        }
    }
}
