using System;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Named, qualitative presets for every strategy field the possession sim
    /// actually reads. The in-match overlay builds its cycles from these so a
    /// coach never sees a raw number — and every control is guaranteed to write
    /// a field with a real simulation effect (guarded by StrategyOverlayWiringTest).
    /// </summary>
    public static class StrategyPresets
    {
        public readonly struct Preset
        {
            public readonly string Label;
            public readonly Action<TeamStrategy> Apply;
            public readonly Func<TeamStrategy, bool> Matches;

            public Preset(string label, Action<TeamStrategy> apply, Func<TeamStrategy, bool> matches)
            {
                Label = label; Apply = apply; Matches = matches;
            }
        }

        /// <summary>Index of the preset the current strategy matches, or a sensible middle.</summary>
        public static int CurrentIndex(Preset[] set, TeamStrategy s)
        {
            for (int i = 0; i < set.Length; i++)
                if (set[i].Matches(s)) return i;
            return set.Length / 2;
        }

        // ── Offense ──

        /// <summary>Shot mix: sim weights shots by ThreePointFrequency/MidRangeFrequency/RimAttackFrequency.</summary>
        public static readonly Preset[] ShotMix =
        {
            new Preset("Let It Fly",
                s => { s.ThreePointFrequency = 50; s.MidRangeFrequency = 15; s.RimAttackFrequency = 35; },
                s => s.ThreePointFrequency >= 48),
            new Preset("Balanced",
                s => { s.ThreePointFrequency = 40; s.MidRangeFrequency = 20; s.RimAttackFrequency = 40; },
                s => s.ThreePointFrequency >= 33 && s.ThreePointFrequency < 48 && s.MidRangeFrequency < 30),
            new Preset("Inside-Out",
                s => { s.ThreePointFrequency = 28; s.MidRangeFrequency = 20; s.RimAttackFrequency = 52; },
                s => s.ThreePointFrequency < 33 && s.MidRangeFrequency < 30),
            new Preset("Old School",
                s => { s.ThreePointFrequency = 25; s.MidRangeFrequency = 40; s.RimAttackFrequency = 35; },
                s => s.MidRangeFrequency >= 30),
        };

        /// <summary>Post-ups: sim features bigs when PostUpFrequency > 50 and weights post actions.</summary>
        public static readonly Preset[] PostUps =
        {
            new Preset("Rare", s => s.PostUpFrequency = 10, s => s.PostUpFrequency <= 25),
            new Preset("Occasional", s => s.PostUpFrequency = 40, s => s.PostUpFrequency > 25 && s.PostUpFrequency <= 50),
            new Preset("Featured", s => s.PostUpFrequency = 65, s => s.PostUpFrequency > 50),
        };

        /// <summary>Offensive glass: OffensiveReboundingFocus + SendersOnOffensiveGlass drive OREB chance.</summary>
        public static readonly Preset[] CrashGlass =
        {
            new Preset("Get Back",
                s => { s.OffensiveReboundingFocus = 1; s.SendersOnOffensiveGlass = 1; },
                s => s.OffensiveReboundingFocus <= 1),
            new Preset("Balanced",
                s => { s.OffensiveReboundingFocus = 2; s.SendersOnOffensiveGlass = 2; },
                s => s.OffensiveReboundingFocus == 2 || s.OffensiveReboundingFocus == 3),
            new Preset("Crash Hard",
                s => { s.OffensiveReboundingFocus = 4; s.SendersOnOffensiveGlass = 3; },
                s => s.OffensiveReboundingFocus >= 4),
        };

        /// <summary>Ball movement: BallMovementPriority feeds turnover risk, assisted-look bonus, action mix.</summary>
        public static readonly Preset[] BallMovement =
        {
            new Preset("Hero Ball", ApplyBallMovement(50), s => BMP(s) < 60),
            new Preset("Balanced", ApplyBallMovement(70), s => BMP(s) >= 60 && BMP(s) < 82),
            new Preset("Share It", ApplyBallMovement(92), s => BMP(s) >= 82),
        };

        private static int BMP(TeamStrategy s) => s.OffensiveSystem?.BallMovementPriority ?? 70;
        private static Action<TeamStrategy> ApplyBallMovement(int v) =>
            s => { if (s.OffensiveSystem != null) s.OffensiveSystem.BallMovementPriority = v; };

        // ── Defense ──

        /// <summary>On-ball pressure: sim reads DefensiveAggression (→ OnBallPressure) for turnover pressure.</summary>
        public static readonly Preset[] Pressure =
        {
            new Preset("Sag Off", s => s.DefensiveAggression = 30, s => s.DefensiveAggression < 45),
            new Preset("Standard", s => s.DefensiveAggression = 60, s => s.DefensiveAggression >= 45 && s.DefensiveAggression < 75),
            new Preset("Tight", s => s.DefensiveAggression = 85, s => s.DefensiveAggression >= 75),
        };

        /// <summary>Gambling: GamblingFrequency drives steals, fouls, and clean looks conceded.</summary>
        public static readonly Preset[] Gambling =
        {
            new Preset("Play Safe", ApplyDef(d => d.GamblingFrequency = 10), s => Def(s)?.GamblingFrequency < 20),
            new Preset("Standard", ApplyDef(d => d.GamblingFrequency = 30), s => Def(s)?.GamblingFrequency >= 20 && Def(s)?.GamblingFrequency < 50),
            new Preset("Gamble", ApplyDef(d => d.GamblingFrequency = 70), s => Def(s)?.GamblingFrequency >= 50),
        };

        /// <summary>Contest commitment: ContestingLevel suppresses jumpers.</summary>
        public static readonly Preset[] Contesting =
        {
            new Preset("Stay Down", ApplyDef(d => d.ContestingLevel = 50), s => Def(s)?.ContestingLevel < 62),
            new Preset("Standard", ApplyDef(d => d.ContestingLevel = 70), s => Def(s)?.ContestingLevel >= 62 && Def(s)?.ContestingLevel < 82),
            new Preset("Fly Out", ApplyDef(d => d.ContestingLevel = 90), s => Def(s)?.ContestingLevel >= 82),
        };

        /// <summary>Part-time zone when the base scheme is man (blends toward SecondaryScheme).</summary>
        public static readonly Preset[] ZoneMix =
        {
            new Preset("Never", ApplyDef(d => d.ZoneUsage = 0), s => Def(s)?.ZoneUsage < 15),
            new Preset("Sometimes", ApplyDef(d => d.ZoneUsage = 30), s => Def(s)?.ZoneUsage >= 15 && Def(s)?.ZoneUsage < 50),
            new Preset("Often", ApplyDef(d => d.ZoneUsage = 60), s => Def(s)?.ZoneUsage >= 50),
        };

        private static DefensiveSystem Def(TeamStrategy s) => s.DefensiveSystem;
        private static Action<TeamStrategy> ApplyDef(Action<DefensiveSystem> a) =>
            s => { if (s.DefensiveSystem != null) a(s.DefensiveSystem); };
    }
}
