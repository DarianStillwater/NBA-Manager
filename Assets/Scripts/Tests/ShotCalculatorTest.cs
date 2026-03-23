using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using ShotType = NBAHeadCoach.Core.Simulation.ShotType;

namespace NBAHeadCoach.Tests
{
    public class ShotCalculatorTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestBasePercentages();
            TestSkillModifier();
            TestContestModifier();
            TestConditionModifier();
            TestSituationalModifiers();
            TestClampBounds();

            return (_passed, _failed);
        }

        private Player CreateTestPlayer(int allSkills, float energy = 100f)
        {
            return new Player
            {
                PlayerId = "TEST", FirstName = "Test", LastName = "Player",
                Finishing_Rim = allSkills, Shot_Close = allSkills,
                Shot_MidRange = allSkills, Shot_Three = allSkills,
                FreeThrow = allSkills, Defense_Perimeter = allSkills,
                Defense_Interior = allSkills, Block = allSkills,
                Energy = energy, Morale = 75, Form = 70,
                Wingspan = 50, Stamina = 50
            };
        }

        private void TestBasePercentages()
        {
            var avg = CreateTestPlayer(50);
            // No defender (null or far away)
            var rimPos = new CourtPosition(40f, 0f); // near basket
            var threePos = new CourtPosition(18f, 0f); // three-point range

            float rimPct = ShotCalculator.CalculateShotProbability(avg, rimPos, null, 15f, ShotType.Layup);
            float threePct = ShotCalculator.CalculateShotProbability(avg, threePos, null, 15f, ShotType.Jumper);

            AssertRange(rimPct, 0.40f, 0.80f, "Rim shot base pct (average player, open)");
            AssertRange(threePct, 0.20f, 0.50f, "Three-point base pct (average player, open)");
            AssertGreaterThan(rimPct, threePct, "Rim pct > Three pct");
        }

        private void TestSkillModifier()
        {
            var elite = CreateTestPlayer(90);
            var poor = CreateTestPlayer(10);
            var threePos = new CourtPosition(18f, 0f);

            float elitePct = ShotCalculator.CalculateShotProbability(elite, threePos, null, 15f, ShotType.Jumper);
            float poorPct = ShotCalculator.CalculateShotProbability(poor, threePos, null, 15f, ShotType.Jumper);

            AssertGreaterThan(elitePct, poorPct, "Elite shooter > poor shooter on 3PT");
            AssertGreaterThan(elitePct - poorPct, 0.05f, "Skill spread > 5%");
        }

        private void TestContestModifier()
        {
            var shooter = CreateTestPlayer(70);
            var defender = CreateTestPlayer(70);
            var pos = new CourtPosition(30f, 0f);

            float openPct = ShotCalculator.CalculateShotProbability(shooter, pos, defender, 10f, ShotType.Jumper);
            float contestedPct = ShotCalculator.CalculateShotProbability(shooter, pos, defender, 2f, ShotType.Jumper);

            AssertGreaterThan(openPct, contestedPct, "Open shot > contested shot");
        }

        private void TestConditionModifier()
        {
            var fresh = CreateTestPlayer(70, energy: 100f);
            var tired = CreateTestPlayer(70, energy: 20f);
            var pos = new CourtPosition(30f, 0f);

            float freshPct = ShotCalculator.CalculateShotProbability(fresh, pos, null, 15f, ShotType.Jumper);
            float tiredPct = ShotCalculator.CalculateShotProbability(tired, pos, null, 15f, ShotType.Jumper);

            AssertGreaterThan(freshPct, tiredPct, "Fresh player > tired player");
        }

        private void TestSituationalModifiers()
        {
            var player = CreateTestPlayer(70);
            var pos = new CourtPosition(30f, 0f);

            // Fast break bonus
            float normalPct = ShotCalculator.CalculateShotProbability(player, pos, null, 15f, ShotType.Jumper, isFastBreak: false);
            float fbPct = ShotCalculator.CalculateShotProbability(player, pos, null, 15f, ShotType.Jumper, isFastBreak: true);
            AssertGreaterThan(fbPct, normalPct, "Fast break bonus");

            // Shot clock pressure
            float relaxed = ShotCalculator.CalculateShotProbability(player, pos, null, 15f, ShotType.Jumper, secondsRemaining: 20f);
            float pressured = ShotCalculator.CalculateShotProbability(player, pos, null, 15f, ShotType.Jumper, secondsRemaining: 2f);
            AssertGreaterThan(relaxed, pressured, "Shot clock pressure penalty");

            // Home court
            float home = ShotCalculator.CalculateShotProbability(player, pos, null, 15f, ShotType.Jumper, isHomeTeam: true);
            float away = ShotCalculator.CalculateShotProbability(player, pos, null, 15f, ShotType.Jumper, isHomeTeam: false);
            AssertGreaterThan(home, away, "Home court advantage");
        }

        private void TestClampBounds()
        {
            var extreme = CreateTestPlayer(100, energy: 100f);
            extreme.Morale = 100; extreme.Form = 100;
            var pos = new CourtPosition(40f, 0f);

            float maxPct = ShotCalculator.CalculateShotProbability(extreme, pos, null, 15f, ShotType.Layup, isFastBreak: true, isHomeTeam: true);
            AssertLessThan(maxPct, 0.96f, "Max probability clamped <= 0.95");

            var terrible = CreateTestPlayer(0, energy: 5f);
            terrible.Morale = 0; terrible.Form = 0;
            float minPct = ShotCalculator.CalculateShotProbability(terrible, pos, extreme, 1f, ShotType.Jumper, secondsRemaining: 1f);
            AssertGreaterThan(minPct, 0.01f, "Min probability clamped >= 0.02");
        }
    }
}
