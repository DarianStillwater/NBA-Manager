using System;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    public class PlayerAttributeTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestOverallRating();
            TestAge();
            TestEnergySystem();
            TestConditionModifier();

            return (_passed, _failed);
        }

        private void TestOverallRating()
        {
            // All-50s player should have OVR near 50
            var avg = CreatePlayer(50);
            AssertRange(avg.OverallRating, 35, 65, $"All-50s player OVR={avg.OverallRating} near 50");

            // All-90s player should have high OVR
            var elite = CreatePlayer(90);
            AssertRange(elite.OverallRating, 75, 100, $"All-90s player OVR={elite.OverallRating} high");

            // All-10s player should have low OVR
            var bad = CreatePlayer(10);
            AssertRange(bad.OverallRating, 0, 30, $"All-10s player OVR={bad.OverallRating} low");

            AssertGreaterThan(elite.OverallRating, avg.OverallRating, "Elite > Average OVR");
            AssertGreaterThan(avg.OverallRating, bad.OverallRating, "Average > Bad OVR");
        }

        private void TestAge()
        {
            var player = new Player
            {
                PlayerId = "AGE_TEST",
                BirthDate = new DateTime(2000, 6, 15),
                Position = Position.PointGuard
            };
            int expectedAge = DateTime.Now.Year - 2000;
            if (DateTime.Now < new DateTime(DateTime.Now.Year, 6, 15))
                expectedAge--;
            AssertEqual(expectedAge, player.Age, $"Age computed correctly ({expectedAge})");
        }

        private void TestEnergySystem()
        {
            var player = CreatePlayer(50);
            player.Energy = 100f;
            player.Stamina = 50;

            player.ConsumeEnergy(20f);
            AssertLessThan(player.Energy, 100f, "Energy decreased after ConsumeEnergy");
            AssertGreaterThan(player.Energy, 0f, "Energy still positive");

            float afterConsume = player.Energy;
            player.RestoreEnergy(10f);
            AssertGreaterThan(player.Energy, afterConsume, "Energy increased after RestoreEnergy");

            // Clamp to 0
            player.ConsumeEnergy(999f);
            AssertRange(player.Energy, 0f, 0.01f, "Energy clamped to 0");

            // Clamp to 100
            player.RestoreEnergy(999f);
            AssertRange(player.Energy, 99.9f, 100.1f, "Energy clamped to 100");
        }

        private void TestConditionModifier()
        {
            var fresh = CreatePlayer(50);
            fresh.Energy = 100f; fresh.Morale = 75; fresh.Form = 70;
            float freshMod = fresh.GetConditionModifier();
            AssertRange(freshMod, 0.85f, 1.15f, $"Fresh condition modifier {freshMod:F2} near 1.0");

            var tired = CreatePlayer(50);
            tired.Energy = 10f; tired.Morale = 75; tired.Form = 70;
            float tiredMod = tired.GetConditionModifier();
            AssertLessThan(tiredMod, freshMod, "Tired modifier < fresh modifier");
            AssertGreaterThan(tiredMod, 0.5f, "Tired modifier still > 0.5");
        }

        private Player CreatePlayer(int allStats)
        {
            return new Player
            {
                PlayerId = $"TEST_{allStats}", Position = Position.PointGuard,
                BirthDate = DateTime.Now.AddYears(-25),
                Finishing_Rim = allStats, Shot_Close = allStats,
                Shot_MidRange = allStats, Shot_Three = allStats,
                FreeThrow = allStats, Passing = allStats, BallHandling = allStats,
                OffensiveIQ = allStats, SpeedWithBall = allStats,
                Defense_Perimeter = allStats, Defense_Interior = allStats,
                Steal = allStats, Block = allStats, DefensiveIQ = allStats,
                DefensiveRebound = allStats, Speed = allStats, Acceleration = allStats,
                Strength = allStats, Vertical = allStats, Stamina = allStats,
                BasketballIQ = allStats, Clutch = allStats,
                Energy = 100, Morale = 75, Form = 70
            };
        }
    }
}
