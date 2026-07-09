using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the expanded in-match strategy overlay: every preset cycle must
    /// write exactly the strategy fields the possession sim reads (no more
    /// dead toggles), presets must round-trip through CurrentIndex, and the
    /// values must sit in simulation-meaningful ranges (e.g. "Featured" post-ups
    /// cross the sim's >50 threshold).
    /// </summary>
    public class StrategyOverlayWiringTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestShotMix();
            TestPostUpsAndGlass();
            TestBallMovement();
            TestDefensePresets();
            TestRoundTrips();

            return (_passed, _failed);
        }

        private TeamStrategy Fresh() => TeamStrategy.CreateDefault("TST");

        private void TestShotMix()
        {
            var s = Fresh();
            StrategyPresets.ShotMix[0].Apply(s); // Let It Fly
            Assert(s.ThreePointFrequency >= 48, "Let It Fly raises three-point frequency");

            StrategyPresets.ShotMix[3].Apply(s); // Old School
            Assert(s.MidRangeFrequency >= 30, "Old School lives in the mid-range");

            foreach (var p in StrategyPresets.ShotMix)
            {
                var t = Fresh();
                p.Apply(t);
                int sum = t.ThreePointFrequency + t.MidRangeFrequency + t.RimAttackFrequency;
                AssertEqual(100, sum, $"Shot mix '{p.Label}' frequencies sum to 100");
            }
        }

        private void TestPostUpsAndGlass()
        {
            var s = Fresh();
            StrategyPresets.PostUps[2].Apply(s); // Featured
            Assert(s.PostUpFrequency > 50, "'Featured' post-ups cross the sim's featured threshold (>50)");
            StrategyPresets.PostUps[0].Apply(s);
            Assert(s.PostUpFrequency <= 25, "'Rare' post-ups drop low");

            StrategyPresets.CrashGlass[2].Apply(s); // Crash Hard
            Assert(s.OffensiveReboundingFocus >= 4 && s.SendersOnOffensiveGlass >= 3,
                "'Crash Hard' raises both OREB focus and senders (both sim-read)");
            StrategyPresets.CrashGlass[0].Apply(s);
            Assert(s.OffensiveReboundingFocus <= 1, "'Get Back' pulls everyone out");
        }

        private void TestBallMovement()
        {
            var s = Fresh();
            StrategyPresets.BallMovement[2].Apply(s); // Share It
            Assert(s.OffensiveSystem.BallMovementPriority >= 82,
                "'Share It' writes OffensiveSystem.BallMovementPriority (sim-read)");
            StrategyPresets.BallMovement[0].Apply(s);
            Assert(s.OffensiveSystem.BallMovementPriority < 60, "'Hero Ball' lowers it");
        }

        private void TestDefensePresets()
        {
            var s = Fresh();

            StrategyPresets.Pressure[2].Apply(s); // Tight
            Assert(s.DefensiveAggression >= 75 && s.DefensiveSystem.OnBallPressure >= 75,
                "'Tight' pressure writes OnBallPressure via DefensiveAggression (sim-read)");

            StrategyPresets.Gambling[2].Apply(s);
            Assert(s.DefensiveSystem.GamblingFrequency >= 50,
                "'Gamble' raises GamblingFrequency (steals/fouls/looks all sim-read)");

            StrategyPresets.Contesting[2].Apply(s);
            Assert(s.DefensiveSystem.ContestingLevel >= 82, "'Fly Out' raises ContestingLevel (sim-read)");

            StrategyPresets.ZoneMix[1].Apply(s);
            AssertEqual(30, s.DefensiveSystem.ZoneUsage, "'Sometimes' zone mix sets the blend weight");
            StrategyPresets.ZoneMix[0].Apply(s);
            AssertEqual(0, s.DefensiveSystem.ZoneUsage, "'Never' clears the zone blend");
        }

        private void TestRoundTrips()
        {
            var sets = new (string name, StrategyPresets.Preset[] set)[]
            {
                ("ShotMix", StrategyPresets.ShotMix),
                ("PostUps", StrategyPresets.PostUps),
                ("CrashGlass", StrategyPresets.CrashGlass),
                ("BallMovement", StrategyPresets.BallMovement),
                ("Pressure", StrategyPresets.Pressure),
                ("Gambling", StrategyPresets.Gambling),
                ("Contesting", StrategyPresets.Contesting),
                ("ZoneMix", StrategyPresets.ZoneMix),
            };

            foreach (var (name, set) in sets)
            {
                bool ok = true;
                for (int i = 0; i < set.Length; i++)
                {
                    var s = Fresh();
                    set[i].Apply(s);
                    if (StrategyPresets.CurrentIndex(set, s) != i) ok = false;
                }
                Assert(ok, $"{name}: applying each preset round-trips through CurrentIndex");
                Assert(set.Select(p => p.Label).Distinct().Count() == set.Length,
                    $"{name}: preset labels are unique");
            }

            var fresh = Fresh();
            foreach (var (name, set) in sets)
            {
                int idx = StrategyPresets.CurrentIndex(set, fresh);
                Assert(idx >= 0 && idx < set.Length, $"{name}: default strategy resolves to a valid preset");
            }
        }
    }
}
