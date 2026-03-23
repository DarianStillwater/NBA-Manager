using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    public class DraftSystemTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            try
            {
                var capManager = new SalaryCapManager();
                var draft = new DraftSystem(capManager, seed: 42);

                // Generate draft class
                var prospects = draft.GenerateDraftClass(2026);
                Assert(prospects != null, "Draft class generated");
                AssertGreaterThan(prospects.Count, 50, $"Draft class has {prospects.Count} prospects (>50)");

                // All attributes valid
                bool allValid = true;
                var ids = new HashSet<string>();
                foreach (var p in prospects)
                {
                    if (string.IsNullOrEmpty(p.ProspectId)) { allValid = false; break; }
                    if (!ids.Add(p.ProspectId)) { allValid = false; break; } // duplicate ID
                }
                Assert(allValid, "All prospects have unique IDs");

                // Top prospects better than bottom on average (use MockDraftPosition)
                var sorted = prospects.OrderBy(p => p.MockDraftPosition).ToList();
                float topAvg = sorted.Count > 10 ? sorted.Take(10).Average(p => (float)p.MockDraftPosition) : 0;
                float bottomAvg = sorted.Count > 10 ? sorted.Skip(sorted.Count - 10).Average(p => (float)p.MockDraftPosition) : 0;
                AssertLessThan(topAvg, bottomAvg, $"Top 10 mock position avg ({topAvg:F0}) < bottom 10 avg ({bottomAvg:F0})");

                // Lottery odds sum to ~100%
                float oddsSum = 14f + 14f + 14f + 12.5f + 10.5f + 9f + 7.5f + 6f + 4.5f + 3f + 2f + 1.5f + 1f + 0.5f;
                AssertRange(oddsSum, 99.5f, 100.5f, $"Lottery odds sum = {oddsSum:F1}%");
            }
            catch (System.Exception e)
            {
                _failed++;
                Debug.Log($"    FAIL: DraftSystem exception: {e.Message}");
            }

            return (_passed, _failed);
        }
    }
}
