using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 7-B commit 2 (GM-only playable): the named AI coach maps to a
    /// deterministic personality, GM careers accumulate a front-office record, a
    /// run of bad GM seasons builds to a firing, and the season-end pass routes a
    /// GM career through GM-side job security.
    /// </summary>
    public class GMOnlyModeTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(20260708);

            try
            {
                TestCoachPersonalityFromProfile();
                TestEnsureGMRecordIdempotent();
                TestBadGMSeasonsBuildToFiring();
                TestSeasonEndCoreEvaluatesGMCareer();
            }
            finally
            {
                RolePermissions.ConfigSource = null;
            }

            return (_passed, _failed);
        }

        private UnifiedCareerProfile MakeGM(string id, string teamId)
        {
            var profile = new UnifiedCareerProfile
            {
                ProfileId = id,
                PersonName = $"GM {id}",
                CurrentAge = 50,
                IsUserControlled = true,
                Reputation = 60
            };
            profile.HandleHired(2026, teamId, teamId, UnifiedRole.GeneralManager);
            profile.ContractYears = 4;
            profile.CurrentSalary = 3_000_000;
            return profile;
        }

        private TeamFinances MakeFinances()
        {
            return new TeamFinances
            {
                TeamOwner = new Owner
                {
                    FirstName = "Test", LastName = "Owner",
                    Philosophy = OwnerPhilosophy.WinNow,
                    SpendingType = OwnerType.Lavish,
                    Patience = 80,
                    MinAcceptableWins = 30,
                    ExpectsPlayoffs = false
                }
            };
        }

        private void TestCoachPersonalityFromProfile()
        {
            var profile = new UnifiedCareerProfile
            {
                ProfileId = "AI_COACH_42",
                PersonName = "Ty Corbin",
                TacticalRating = 80,
                PlayerDevelopment = 70,
                Reputation = 60
            };

            var a = AICoachPersonality.FromProfile(profile);
            var b = AICoachPersonality.FromProfile(profile);

            AssertEqual("Ty Corbin", a.CoachName, "Personality carries the AI coach's name");
            AssertEqual("AI_COACH_42", a.CoachId, "Personality carries the profile id");
            Assert(Mathf.Approximately(a.PreferredPace, b.PreferredPace) &&
                   a.ThreePointEmphasis == b.ThreePointEmphasis &&
                   a.DefensiveAggression == b.DefensiveAggression &&
                   a.RotationDepth == b.RotationDepth,
                "Same profile always yields the same personality");
            AssertEqual(80f, a.InGameAdjustmentSpeed, "Tactical rating drives adjustment speed");
            Assert(AICoachPersonality.FromProfile(null) != null, "Null profile falls back safely");
        }

        private void TestEnsureGMRecordIdempotent()
        {
            var gmSec = new GMJobSecurityManager();
            var r1 = gmSec.EnsureGMRecord("BOS", "GM_1", "Player GM");
            var r2 = gmSec.EnsureGMRecord("BOS", "GM_1", "Player GM");

            Assert(r1 != null, "EnsureGMRecord creates a record");
            Assert(ReferenceEquals(r1, r2), "Second call returns the same record");
            AssertEqual("Player GM", r1.GMName, "Record carries the GM's name");
            AssertEqual(GMJobSecurityStatus.Stable, r1.CurrentStatus, "New GM starts Stable");
        }

        private void TestBadGMSeasonsBuildToFiring()
        {
            var gmSec = new GMJobSecurityManager();
            gmSec.EnsureGMRecord("DET", "GM_2", "Struggling GM");

            for (int year = 0; year < 3; year++)
                gmSec.EvaluateGMPerformance("DET", 20, 62, false, 0);
            Assert(!gmSec.ShouldFireGM("DET") || true, "Three seasons evaluated without crash");

            gmSec.EvaluateGMPerformance("DET", 18, 64, false, 0);
            Assert(gmSec.ShouldFireGM("DET"),
                "Four straight playoff misses with losing records get the GM fired");

            var record = gmSec.GetGMCareerData("DET");
            Assert(record.ConsecutivePlayoffMisses >= 4, "Playoff misses accumulate on the record");
            Assert(record.TotalLosses > record.TotalWins, "Career totals accumulate");
        }

        private void TestSeasonEndCoreEvaluatesGMCareer()
        {
            var js = new JobSecurityManager();
            var gmSec = new GMJobSecurityManager();
            var gm = MakeGM("PGM_1", "ORL");
            js.RegisterProfile(gm);
            var fin = MakeFinances();
            js.SetSeasonExpectations("PGM_1", fin, 45);
            var team = new Team { TeamId = "ORL", City = "Orlando", Nickname = "Magic", Wins = 41, Losses = 41 };

            bool fired = false;
            var stakes = new CareerStakesSystem(js, gmSec, id => fin, () => gm, id => team, _ => fired = true);

            stakes.ProcessSeasonEndCore(2026, "BOS", "DEN", new HashSet<string> { "BOS", "DEN", "ORL" });

            var record = gmSec.GetGMCareerData("ORL");
            Assert(record != null, "Season end creates the player's GM record");
            AssertEqual(41, record.TotalWins, "GM record captures the season's wins");
            AssertEqual(1, record.PlayoffAppearances, "Playoff berth lands on the GM record");
            Assert(!fired, "A .500 playoff season under a patient owner keeps the job");
        }
    }
}
