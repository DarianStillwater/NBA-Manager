using System;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    public class PlayerDevelopmentTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestDevelopmentPhases();
            TestGrowthMultipliers();

            return (_passed, _failed);
        }

        private void TestDevelopmentPhases()
        {
            var young = MakePlayer(20);
            var mid = MakePlayer(25);
            var peak = MakePlayer(28);
            var declining = MakePlayer(33);
            var old = MakePlayer(36);

            Assert(young.CurrentDevelopmentPhase == DevelopmentPhase.PrimeDevelopment, $"Age 20 = PrimeDevelopment (got {young.CurrentDevelopmentPhase})");
            Assert(mid.CurrentDevelopmentPhase == DevelopmentPhase.ContinuedDevelopment, $"Age 25 = ContinuedDevelopment (got {mid.CurrentDevelopmentPhase})");
            Assert(peak.CurrentDevelopmentPhase == DevelopmentPhase.Peak, $"Age 28 = Peak (got {peak.CurrentDevelopmentPhase})");
            Assert(declining.CurrentDevelopmentPhase == DevelopmentPhase.EarlyDecline, $"Age 33 = EarlyDecline (got {declining.CurrentDevelopmentPhase})");
            Assert(old.CurrentDevelopmentPhase == DevelopmentPhase.SteepDecline, $"Age 36 = SteepDecline (got {old.CurrentDevelopmentPhase})");
        }

        private void TestGrowthMultipliers()
        {
            var young = MakePlayer(20);
            var mid = MakePlayer(25);
            var peak = MakePlayer(28);

            float youngMult = young.DevelopmentMultiplier;
            float midMult = mid.DevelopmentMultiplier;
            float peakMult = peak.DevelopmentMultiplier;

            AssertGreaterThan(youngMult, midMult, $"Young mult ({youngMult:F1}) > mid ({midMult:F1})");
            AssertGreaterThan(midMult, peakMult, $"Mid mult ({midMult:F1}) > peak ({peakMult:F1})");
            AssertGreaterThan(youngMult, 1.0f, "Young growth multiplier > 1.0");
        }

        private Player MakePlayer(int age)
        {
            return new Player
            {
                PlayerId = $"DEV_{age}",
                BirthDate = DateTime.Now.AddYears(-age),
                Position = Position.ShootingGuard,
                HiddenPotential = 85, PeakAge = 28,
                Finishing_Rim = 60, Shot_Three = 60, Shot_MidRange = 60,
                Defense_Perimeter = 60, Speed = 60,
                Energy = 100, Morale = 75, Form = 70
            };
        }
    }
}
