using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Master test runner that executes all test classes and aggregates results.
    /// Attach to any GameObject or run via Unity MCP.
    /// </summary>
    public class TestRunner : MonoBehaviour
    {
        [SerializeField] private bool _runOnStart = true;

        private int _totalPassed;
        private int _totalFailed;
        private List<string> _failedTests = new List<string>();

        private void Start()
        {
            if (_runOnStart) RunAllTests();
        }

        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            _totalPassed = 0;
            _totalFailed = 0;
            _failedTests.Clear();

            Debug.Log("\n" + new string('=', 70));
            Debug.Log("  NBA HEAD COACH — FULL TEST SUITE");
            Debug.Log(new string('=', 70));

            RunTest<CourtZoneTest>("Court Zone Classification");
            RunTest<ShotCalculatorTest>("Shot Calculator Math");
            RunTest<GameSimulatorIntegrityTest>("Game Simulator Integrity");
            RunTest<PossessionOutcomeTest>("Possession Outcomes");
            RunTest<FreeThrowScoringTest>("Free Throw Scoring");
            RunTest<LeagueCBATest>("League CBA Constants");
            RunTest<RosterLimitsTest>("Roster Limits");
            RunTest<TradeValidatorTest>("Trade Validator");
            RunTest<EnergyRotationTest>("Energy & Rotation");
            RunTest<PlayerDevelopmentTest>("Player Development");
            RunTest<PlayerAttributeTest>("Player Attributes");
            RunTest<DraftSystemTest>("Draft System");
            RunTest<MoraleChemistryTest>("Morale & Chemistry");
            RunTest<PlayerTendencyTest>("Player Tendencies");
            RunTest<ViolationSystemTest>("Violations & Fouls");
            RunTest<TeamStrategyTest>("Team Strategy");
            RunTest<PracticeSystemTest>("Practice & Mentorship");
            RunTest<PersonnelCareerTest>("Personnel & Career");
            RunTest<TimeoutIntelligenceTest>("Timeout Intelligence");

            // Summary
            Debug.Log("\n" + new string('=', 70));
            Debug.Log($"  TEST RESULTS: {_totalPassed} PASSED, {_totalFailed} FAILED ({_totalPassed + _totalFailed} total)");
            Debug.Log(new string('=', 70));

            if (_failedTests.Count > 0)
            {
                Debug.Log("\n  FAILED TESTS:");
                foreach (var name in _failedTests)
                    Debug.Log($"    - {name}");
            }
            else
            {
                Debug.Log("\n  ALL TESTS PASSED!");
            }

            // Write to file
            try
            {
                var path = System.IO.Path.Combine(Application.dataPath, "..", "test_results.txt");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"TEST RESULTS: {_totalPassed} PASSED, {_totalFailed} FAILED ({_totalPassed + _totalFailed} total)");
                if (_failedTests.Count > 0)
                {
                    sb.AppendLine("\nFAILED TESTS:");
                    foreach (var name in _failedTests)
                        sb.AppendLine($"  - {name}");
                }
                else
                {
                    sb.AppendLine("ALL TESTS PASSED!");
                }
                System.IO.File.WriteAllText(path, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not write test results file: {e.Message}");
            }

            Debug.Log("");
        }

        private void RunTest<T>(string testName) where T : MonoBehaviour, ITestable
        {
            Debug.Log($"\n--- {testName} ---");
            try
            {
                var go = new GameObject($"Test_{typeof(T).Name}");
                var test = go.AddComponent<T>();
                var (passed, failed) = test.RunAndReport();
                DestroyImmediate(go);

                _totalPassed += passed;
                _totalFailed += failed;
                if (failed > 0)
                    _failedTests.Add($"{testName} ({failed} failures)");

                Debug.Log($"  [{testName}] {passed} passed, {failed} failed");
            }
            catch (Exception e)
            {
                _totalFailed++;
                _failedTests.Add($"{testName} (EXCEPTION: {e.Message})");
                Debug.LogError($"  [{testName}] EXCEPTION: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Interface all test classes must implement for TestRunner integration.
    /// </summary>
    public interface ITestable
    {
        (int passed, int failed) RunAndReport();
    }

    /// <summary>
    /// Base class with assertion helpers for all tests.
    /// </summary>
    public abstract class BaseTest : MonoBehaviour, ITestable
    {
        protected int _passed;
        protected int _failed;

        public abstract (int passed, int failed) RunAndReport();

        protected void Assert(bool condition, string testName)
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"    PASS: {testName}");
            }
            else
            {
                _failed++;
                Debug.Log($"    FAIL: {testName}");
            }
        }

        protected void AssertEqual<T>(T expected, T actual, string testName)
        {
            bool eq = expected != null ? expected.Equals(actual) : actual == null || actual.Equals(default(T));
            if (eq)
            {
                _passed++;
                Debug.Log($"    PASS: {testName}");
            }
            else
            {
                _failed++;
                Debug.Log($"    FAIL: {testName} — expected {expected}, got {actual}");
            }
        }

        protected void AssertRange(float value, float min, float max, string testName)
        {
            if (value >= min && value <= max)
            {
                _passed++;
                Debug.Log($"    PASS: {testName} ({value:F2})");
            }
            else
            {
                _failed++;
                Debug.Log($"    FAIL: {testName} — {value:F2} not in [{min:F2}, {max:F2}]");
            }
        }

        protected void AssertGreaterThan(float value, float threshold, string testName)
        {
            Assert(value > threshold, $"{testName} ({value:F2} > {threshold:F2})");
        }

        protected void AssertLessThan(float value, float threshold, string testName)
        {
            Assert(value < threshold, $"{testName} ({value:F2} < {threshold:F2})");
        }
    }
}
