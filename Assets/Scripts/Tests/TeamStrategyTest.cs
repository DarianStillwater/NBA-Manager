using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    public class TeamStrategyTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestDefaultStrategy();
            TestWarriorsTemplate();
            TestAllTemplates();

            return (_passed, _failed);
        }

        private void TestDefaultStrategy()
        {
            var strat = TeamStrategy.CreateDefault("TST");
            Assert(strat != null, "Default strategy created");
            Assert(strat.ThreePointFrequency >= 0, "3PT frequency >= 0");
            Assert(strat.MidRangeFrequency >= 0, "Mid-range frequency >= 0");
            Assert(strat.RimAttackFrequency >= 0, "Rim frequency >= 0");
            Assert(strat.TargetPace >= 80 && strat.TargetPace <= 120, $"Pace {strat.TargetPace} in [80,120]");

            float shotSum = strat.ThreePointFrequency + strat.MidRangeFrequency + strat.RimAttackFrequency;
            AssertRange(shotSum, 70f, 130f, $"Shot frequencies sum to {shotSum} (~100)");
        }

        private void TestWarriorsTemplate()
        {
            var gsw = TeamStrategy.CreateFromTemplate("GSW", StrategyTemplate.WarriorsMotion);
            Assert(gsw != null, "Warriors template created");
            AssertGreaterThan(gsw.ThreePointFrequency, 35f, $"Warriors 3PT freq ({gsw.ThreePointFrequency}) > 35");
            AssertGreaterThan(gsw.TargetPace, 95f, $"Warriors pace ({gsw.TargetPace}) > 95");
        }

        private void TestAllTemplates()
        {
            var templates = System.Enum.GetValues(typeof(StrategyTemplate));
            foreach (StrategyTemplate tmpl in templates)
            {
                try
                {
                    var strat = TeamStrategy.CreateFromTemplate("TST", tmpl);
                    Assert(strat != null, $"Template {tmpl} creates non-null strategy");
                    Assert(strat.TargetPace > 0, $"Template {tmpl} has positive pace");
                }
                catch (System.Exception e)
                {
                    _failed++;
                    Debug.Log($"    FAIL: Template {tmpl} threw: {e.Message}");
                }
            }
        }
    }
}
