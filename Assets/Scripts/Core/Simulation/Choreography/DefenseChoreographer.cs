using NBAHeadCoach.Core.Data;

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
    /// Zone assignments anchor to a formation spot instead of a man.
    /// </summary>
    public struct DefenderAssignment
    {
        public int ManIndex;         // offensive player this defender guards (swaps on a Switch)
        public float SagDepth;       // 0 = on the man … 1 = at the rim (off-ball sag)
        public float OnBallPressure; // sag used while actually guarding the ball
        public ScreenNavigation Nav;
        public bool IsHelper;        // low man responsible for tagging the roller
        public int TagIndex;         // offensive player the helper rotates to (-1 = none)
        public bool IsZone;          // hold a zone spot (shifted toward the ball) instead of man-tracking
        public CourtPosition ZoneSpot;
    }

    /// <summary>Pure defensive assignment planner. No side effects, no UnityEngine, no per-tick RNG.</summary>
    public static class DefenseChoreographer
    {
        public static DefenderAssignment[] BuildPlan(ActionPlan plan, PossessionScript s, System.Random rng)
            => BuildPlan(plan, s, rng, DefensiveSchemeType.ManToManStandard);

        public static DefenderAssignment[] BuildPlan(ActionPlan plan, PossessionScript s,
            System.Random rng, DefensiveSchemeType scheme)
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

            // Zone schemes hold formation spots instead of man-tracking. A zone can't
            // set in transition, so fast breaks always render as scramble/man.
            if (DefensiveSchemeProfile.IsZoneScheme(scheme) && s != null && !s.IsFastBreak)
            {
                ApplyZoneFormation(d, plan, s, scheme);
                return d;   // zones don't switch or hedge ball screens
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

        /// <summary>
        /// Formation anchors in attack space: (distance from the rim toward halfcourt,
        /// lateral). Junk defenses (box-and-one, triangle-and-two) leave chasers in
        /// man assignments on the primary threat(s); everyone else zones up.
        /// </summary>
        private static void ApplyZoneFormation(DefenderAssignment[] d, ActionPlan plan,
            PossessionScript s, DefensiveSchemeType scheme)
        {
            float rimX = CourtGeometry.RimXFor(s.OffenseAttacksRight);
            float sign = s.OffenseAttacksRight ? 1f : -1f;
            CourtPosition Spot(float fromRim, float lateral) =>
                new CourtPosition(rimX - sign * fromRim, lateral);

            (float, float)[] spots = scheme switch
            {
                // two up top, three across the paint
                DefensiveSchemeType.Zone2_3 => new[] { (16f, -7f), (16f, 7f), (6f, -9f), (6f, 0f), (6f, 9f) },
                DefensiveSchemeType.MatchupZone => new[] { (16f, -7f), (16f, 7f), (6f, -9f), (6f, 0f), (6f, 9f) },
                // three high chasing the arc, two low
                DefensiveSchemeType.Zone3_2 => new[] { (17f, -10f), (17f, 0f), (17f, 10f), (7f, -6f), (7f, 6f) },
                // point, three across the free-throw line, baseline rover
                DefensiveSchemeType.Zone1_3_1 => new[] { (20f, 0f), (11f, -11f), (11f, 0f), (11f, 11f), (3f, 0f) },
                // point, two wings, two low
                DefensiveSchemeType.Zone1_2_2 => new[] { (18f, 0f), (12f, -9f), (12f, 9f), (5f, -7f), (5f, 7f) },
                // box for the four zoners (chaser slot replaced below)
                DefensiveSchemeType.BoxAndOne => new[] { (12f, -6f), (12f, 6f), (5f, -6f), (5f, 6f), (12f, 0f) },
                DefensiveSchemeType.TriangleAndTwo => new[] { (13f, 0f), (5f, -7f), (5f, 7f), (14f, -8f), (14f, 8f) },
                _ => new[] { (16f, -7f), (16f, 7f), (6f, -9f), (6f, 0f), (6f, 9f) }
            };

            // Junk defenses: who stays man-to-man on the ball threats.
            int chaser1 = -1, chaser2 = -1;
            if (scheme == DefensiveSchemeType.BoxAndOne)
                chaser1 = plan != null && plan.Shooter >= 0 ? plan.Shooter : 0;
            else if (scheme == DefensiveSchemeType.TriangleAndTwo)
            {
                chaser1 = plan != null && plan.Shooter >= 0 ? plan.Shooter : 0;
                chaser2 = plan != null && plan.Handler >= 0 && plan.Handler != chaser1 ? plan.Handler
                        : (chaser1 == 0 ? 1 : 0);
            }

            int spotIdx = 0;
            for (int i = 0; i < 5; i++)
            {
                if (i == chaser1 || i == chaser2)
                {
                    d[i].ManIndex = i;               // face-guard: tighter than normal man
                    d[i].SagDepth = 0.08f;
                    d[i].OnBallPressure = 0.08f;
                    continue;
                }
                var (fromRim, lateral) = spots[spotIdx++];
                d[i].IsZone = true;
                d[i].ZoneSpot = Spot(fromRim, lateral);
            }
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
