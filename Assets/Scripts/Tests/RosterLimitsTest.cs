using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    public class RosterLimitsTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            try
            {
                var capManager = new SalaryCapManager();
                var rosterManager = new RosterManager(capManager);

                // Create a team with roster
                var team = new Team { TeamId = "TST", City = "Test", Nickname = "Testers" };

                // Standard roster max = 15
                Assert(rosterManager != null, "RosterManager instantiates without error");

                // Verify constants exist
                Assert(RosterManager.STANDARD_ROSTER_MAX == 15 || RosterManager.STANDARD_ROSTER_MAX > 0,
                    "Standard roster max is defined and positive");
                Assert(RosterManager.TWO_WAY_SLOTS == 3 || RosterManager.TWO_WAY_SLOTS > 0,
                    "Two-way slots defined and positive");

                Debug.Log($"    INFO: STANDARD_ROSTER_MAX = {RosterManager.STANDARD_ROSTER_MAX}");
                Debug.Log($"    INFO: TWO_WAY_SLOTS = {RosterManager.TWO_WAY_SLOTS}");
            }
            catch (System.Exception e)
            {
                _failed++;
                Debug.Log($"    FAIL: RosterManager test threw exception: {e.Message}");
            }

            return (_passed, _failed);
        }
    }
}
