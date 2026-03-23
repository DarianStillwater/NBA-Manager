using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    public class PracticeSystemTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestMentorEligibility();

            return (_passed, _failed);
        }

        private void TestMentorEligibility()
        {
            // Veteran can mentor (4+ years)
            var vet = new Player
            {
                PlayerId = "VET", YearsPro = 8, Leadership = 75
            };
            Assert(vet.CanBeMentor, "8-year vet with leadership can mentor");

            // Rookie can be mentored (<=4 years)
            var rookie = new Player
            {
                PlayerId = "ROOK", YearsPro = 1
            };
            Assert(rookie.CanBeMentored, "1st year player can be mentored");

            // Veteran cannot be mentored
            var oldVet = new Player
            {
                PlayerId = "OLD", YearsPro = 12
            };
            Assert(!oldVet.CanBeMentored, "12-year vet cannot be mentored");

            // Rookie cannot mentor
            var youngPlayer = new Player
            {
                PlayerId = "YOUNG", YearsPro = 2, Leadership = 40
            };
            Assert(!youngPlayer.CanBeMentor, "2nd year player cannot mentor");
        }
    }
}
