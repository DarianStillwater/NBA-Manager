using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    public class PlayerTendencyTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestTendencyRanges();
            TestClutchModifier();
            TestEffortModifier();

            return (_passed, _failed);
        }

        private void TestTendencyRanges()
        {
            try
            {
                var tendencies = PlayerTendencies.GenerateForArchetype(PlayerArchetype.Balanced, 85);
                Assert(tendencies != null, "Tendencies generated for archetype");

                // Innate tendencies: [-100, +100]
                AssertRange(tendencies.ShotSelection, -100, 100, "ShotSelection in range");
                AssertRange(tendencies.DefensiveGambling, -100, 100, "DefensiveGambling in range");
                AssertRange(tendencies.ClutchBehavior, -100, 100, "ClutchBehavior in range");
                AssertRange(tendencies.BallMovement, -100, 100, "BallMovement in range");

                // Coachable tendencies: [0, 100]
                AssertRange(tendencies.ScreenSetting, 0, 100, "ScreenSetting in range");
                AssertRange(tendencies.HelpDefenseAggression, 0, 100, "HelpDefenseAggression in range");
                AssertRange(tendencies.CloseoutControl, 0, 100, "CloseoutControl in range");
            }
            catch (System.Exception e)
            {
                _failed++;
                Debug.Log($"    FAIL: Tendency generation: {e.Message}");
            }
        }

        private void TestClutchModifier()
        {
            try
            {
                var clutch = new PlayerTendencies { ClutchBehavior = 100 };
                var choke = new PlayerTendencies { ClutchBehavior = -100 };

                float clutchMod = clutch.GetClutchModifier(true);
                float chokeMod = choke.GetClutchModifier(true);
                float normalMod = clutch.GetClutchModifier(false);

                AssertGreaterThan(clutchMod, 1.0f, $"Clutch player in clutch ({clutchMod:F2}) > 1.0");
                AssertLessThan(chokeMod, 1.0f, $"Choke player in clutch ({chokeMod:F2}) < 1.0");
                AssertRange(normalMod, 0.99f, 1.01f, $"Non-clutch situation ({normalMod:F2}) ~1.0");
            }
            catch (System.Exception e)
            {
                _failed++;
                Debug.Log($"    FAIL: Clutch modifier: {e.Message}");
            }
        }

        private void TestEffortModifier()
        {
            try
            {
                var hustle = new PlayerTendencies { EffortConsistency = 100 };
                var lazy = new PlayerTendencies { EffortConsistency = -100 };

                float hustleMod = hustle.GetEffortModifier(100f, 1);
                float lazyMod = lazy.GetEffortModifier(50f, 4);

                AssertGreaterThan(hustleMod, lazyMod, $"Hustler ({hustleMod:F2}) > lazy tired ({lazyMod:F2})");
                AssertRange(hustleMod, 0.7f, 1.3f, "Effort modifier in reasonable range");
            }
            catch (System.Exception e)
            {
                _failed++;
                Debug.Log($"    FAIL: Effort modifier: {e.Message}");
            }
        }
    }
}
