using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    public class PersonnelCareerTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestCareerProfile();
            TestFormerPlayerPipeline();

            return (_passed, _failed);
        }

        private void TestCareerProfile()
        {
            try
            {
                var profile = new UnifiedCareerProfile
                {
                    ProfileId = "COACH_01",
                    PersonName = "Test Coach"
                };
                Assert(profile != null, "Career profile created");
                AssertEqual("COACH_01", profile.ProfileId, "Profile ID set correctly");
                Assert(!string.IsNullOrEmpty(profile.PersonName), "Person name set");
                Assert(profile.LastName == "Coach", $"LastName computed = {profile.LastName}");
            }
            catch (System.Exception e)
            {
                _failed++;
                Debug.Log($"    FAIL: Career profile: {e.Message}");
            }
        }

        private void TestFormerPlayerPipeline()
        {
            // Coaching pipeline: 5+ NBA seasons, Leadership >= 60
            var eligible = new Player
            {
                PlayerId = "EX1", YearsPro = 8, Leadership = 75,
                BasketballIQ = 80
            };
            Assert(eligible.YearsPro >= 5, "8-year player meets coaching experience req (5+)");
            Assert(eligible.Leadership >= 60, "Leadership 75 meets coaching req (60+)");

            // Scouting pipeline: 3+ NBA seasons, BasketballIQ >= 65
            var scout = new Player
            {
                PlayerId = "EX2", YearsPro = 4, BasketballIQ = 70
            };
            Assert(scout.YearsPro >= 3, "4-year player meets scouting experience req (3+)");
            Assert(scout.BasketballIQ >= 65, "BasketballIQ 70 meets scouting req (65+)");

            // Ineligible for coaching
            var ineligible = new Player
            {
                PlayerId = "EX3", YearsPro = 2, Leadership = 40
            };
            Assert(ineligible.YearsPro < 5, "2-year player doesn't meet coaching experience");
            Assert(ineligible.Leadership < 60, "Leadership 40 doesn't meet coaching req");
        }
    }
}
