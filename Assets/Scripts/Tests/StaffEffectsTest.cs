using System;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the staff office: employed profiles land in the team-staff index,
    /// the hiring market actually stocks candidates, hire/fire moves people
    /// between market and team, and staff quality feeds the development helper.
    /// </summary>
    public class StaffEffectsTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestEmployedProfileIndexing();
            TestHiringMarketAndHireFire();

            return (_passed, _failed);
        }

        private void TestEmployedProfileIndexing()
        {
            var pm = new PersonnelManager();

            var coach = UnifiedCareerProfile.CreateForCoaching("Test Coach", 2026, 48, false);
            coach.PlayerDevelopment = 90;
            coach.HandleHired(2026, "AAA", "Alpha Aces", UnifiedRole.HeadCoach);
            pm.RegisterProfile(coach);

            Assert(pm.GetHeadCoach("AAA") != null,
                "Registered employed coach appears as the team's head coach");
            AssertEqual(1, pm.GetTeamStaff("AAA").Count, "Team staff index holds the hire");
            Assert(pm.GetDevelopmentQuality("AAA") > 0.7f,
                $"Development quality reflects the staff ({pm.GetDevelopmentQuality("AAA"):0.00})");
            AssertEqual(0.5f, pm.GetDevelopmentQuality("ZZZ"), "Staff-less team gets the neutral default");
            Assert(pm.GetStaffQuality("AAA") > 0, "Staff quality is nonzero with staff");
        }

        private void TestHiringMarketAndHireFire()
        {
            var pm = new PersonnelManager();
            pm.GenerateFreeAgentPool(coachCount: 12, scoutCount: 6);

            var pool = pm.GetUnemployedPool();
            AssertEqual(18, pool.Count, "Hiring market stocks every generated candidate");
            Assert(pool.Any(p => p.CurrentRole == UnifiedRole.Scout), "Market includes scouts");
            Assert(pool.Any(p => UnifiedRoleExtensions.IsCoachingRole(p.CurrentRole)), "Market includes coaches");

            // Hire the best assistant-track candidate onto AAA
            var candidate = pool.First(p => p.CurrentRole != UnifiedRole.HeadCoach);
            var role = candidate.CurrentRole;
            var (hired, hireMessage) = pm.HirePersonnel(candidate.ProfileId, "AAA", "Alpha Aces", role, 1_500_000, 3);

            Assert(hired, $"Hiring from the market succeeds ({hireMessage})");
            Assert(pm.GetTeamStaff("AAA").Any(p => p.ProfileId == candidate.ProfileId),
                "Hired candidate joins the team staff");
            Assert(pm.GetUnemployedPool().All(p => p.ProfileId != candidate.ProfileId),
                "Hired candidate leaves the market");
            AssertEqual("AAA", candidate.CurrentTeamId, "Hire sets the team");
            AssertEqual(1_500_000, candidate.AnnualSalary, "Hire sets the agreed salary");

            // Double-hire is refused
            var (again, _) = pm.HirePersonnel(candidate.ProfileId, "BBB", "Beta Bears", role);
            Assert(!again, "A person on a team's payroll cannot be hired away silently");

            // Fire returns them to the market
            var (fired, fireMessage) = pm.FirePersonnel(candidate.ProfileId, "Restructuring");
            Assert(fired, $"Firing succeeds ({fireMessage})");
            Assert(pm.GetTeamStaff("AAA").All(p => p.ProfileId != candidate.ProfileId),
                "Fired staff leaves the team");
            Assert(pm.GetUnemployedPool().Any(p => p.ProfileId == candidate.ProfileId),
                "Fired staff returns to the market");
        }
    }
}
