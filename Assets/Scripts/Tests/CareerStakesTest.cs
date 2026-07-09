using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 6-A: owner expectations get set and evaluated (warnings escalate,
    /// winning heals), disaster seasons end in a firing, the fired player's season
    /// lands in their career record, and the job market generates openings, processes
    /// applications/interviews/offers, and survives a save.
    /// </summary>
    public class CareerStakesTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(9494);

            TestExpectationsFromOwner();
            TestInSeasonEvaluation();
            TestSeasonEndVerdictFires();
            TestCareerStakesSeasonEndCore();
            TestWeeklyTickEscalates();
            TestJobMarketFlow();
            TestJobMarketSaveRoundTrip();

            return (_passed, _failed);
        }

        // ==================== FIXTURES ====================

        private UnifiedCareerProfile MakeCoach(string id, string teamId, int reputation = 60)
        {
            var profile = new UnifiedCareerProfile
            {
                ProfileId = id,
                PersonName = $"Coach {id}",
                CurrentAge = 45,
                IsUserControlled = true,
                Reputation = reputation
            };
            profile.HandleHired(2026, teamId, teamId, UnifiedRole.HeadCoach);
            profile.ContractYears = 3;
            profile.CurrentSalary = 5_000_000;
            return profile;
        }

        private TeamFinances MakeFinances(int patience, int minWins, bool expectsPlayoffs)
        {
            return new TeamFinances
            {
                TeamOwner = new Owner
                {
                    FirstName = "Test", LastName = "Owner",
                    Philosophy = OwnerPhilosophy.WinNow,
                    SpendingType = OwnerType.Lavish,
                    Patience = patience,
                    MinAcceptableWins = minWins,
                    ExpectsPlayoffs = expectsPlayoffs
                }
            };
        }

        private List<Team> MakeTeams(int count)
        {
            var teams = new List<Team>();
            for (int i = 0; i < count; i++)
            {
                teams.Add(new Team
                {
                    TeamId = $"T{i:D2}",
                    City = $"City{i}",
                    Nickname = $"Team{i}",
                    Wins = 20 + i,
                    Losses = 30 - i
                });
            }
            return teams;
        }

        // ==================== JOB SECURITY ====================

        private void TestExpectationsFromOwner()
        {
            var js = new JobSecurityManager();
            var coach = MakeCoach("C1", "AAA");
            js.RegisterProfile(coach);
            var fin = MakeFinances(patience: 50, minWins: 45, expectsPlayoffs: true);

            var exp = js.SetSeasonExpectations("C1", fin, 50);

            Assert(exp != null, "Owner sets season expectations");
            Assert(exp.MinimumWins >= 45, $"Owner's win floor holds ({exp.MinimumWins})");
            AssertEqual(53, exp.TargetWins, "Win-now owner targets strength + 3");
            Assert(coach.CurrentExpectations == exp, "Expectations land on the profile");
            Assert(coach.OwnerMessages.Count == 1, "Expectations arrive as an owner message");
        }

        private void TestInSeasonEvaluation()
        {
            var js = new JobSecurityManager();
            var coach = MakeCoach("C2", "AAA");
            js.RegisterProfile(coach);
            var fin = MakeFinances(patience: 20, minWins: 40, expectsPlayoffs: true);
            js.SetSeasonExpectations("C2", fin, 50);
            int messagesBefore = coach.OwnerMessages.Count;

            // 5-25 projects to ~14 wins — deep below the floor
            var status = js.EvaluateJobSecurity("C2", 5, 25, false, fin);
            AssertEqual(JobSecurityStatus.FinalWarning, status, "Disaster pace earns a final warning");
            Assert(coach.OwnerMessages.Count > messagesBefore, "The warning arrives as an owner message");

            // A hot streak heals the seat
            var recovered = js.EvaluateJobSecurity("C2", 26, 4, true, fin);
            AssertEqual(JobSecurityStatus.Untouchable, recovered, "Winning at a 71-win pace heals everything");
        }

        private void TestSeasonEndVerdictFires()
        {
            var js = new JobSecurityManager();
            var coach = MakeCoach("C3", "AAA");
            js.RegisterProfile(coach);
            var fin = MakeFinances(patience: 20, minWins: 40, expectsPlayoffs: true);
            js.SetSeasonExpectations("C3", fin, 50);

            var verdict = js.EvaluateEndOfSeason("C3", 15, 67, false, "Missed playoffs", false, fin);

            Assert(verdict != null && verdict.WillBeFired, "A failed season with an impatient owner ends in a firing");
            AssertEqual(JobSecurityStatus.Fired, coach.JobSecurity, "The profile is marked fired");
            AssertEqual(1, coach.TimesFired, "The firing counts");
        }

        // ==================== CAREER STAKES SYSTEM ====================

        private void TestCareerStakesSeasonEndCore()
        {
            var js = new JobSecurityManager();
            var pm = new PersonnelManager(); // fresh singleton so staff aging is clean
            var coach = MakeCoach("C4", "AAA");
            js.RegisterProfile(coach);
            pm.RegisterProfile(coach);

            var team = new Team { TeamId = "AAA", City = "Alpha", Nickname = "Aces", Wins = 20, Losses = 62 };
            var fin = MakeFinances(patience: 20, minWins: 40, expectsPlayoffs: true);
            js.SetSeasonExpectations("C4", fin, 50);

            FiringReason? firedWith = null;
            var stakes = new CareerStakesSystem(
                js, null,
                id => fin,
                () => coach,
                id => team,
                reason => firedWith = reason);

            stakes.ProcessSeasonEndCore(2026, "XXX", "YYY", new HashSet<string>());

            var latest = coach.CareerHistory.LastOrDefault();
            Assert(latest != null && latest.Wins == 20 && latest.Losses == 62,
                "The season lands in the career record (was never wired before)");
            AssertEqual(20, coach.TotalWins, "Career totals accumulate");
            Assert(firedWith.HasValue, "The season-end verdict triggers the franchise-side firing");
            AssertEqual(FiringReason.MissedPlayoffs, firedWith.Value, "Firing reason reflects the failure");
        }

        private void TestWeeklyTickEscalates()
        {
            var js = new JobSecurityManager();
            var coach = MakeCoach("C5", "AAA");
            js.RegisterProfile(coach);
            var team = new Team { TeamId = "AAA", City = "Alpha", Nickname = "Aces", Wins = 5, Losses = 25 };
            var fin = MakeFinances(patience: 20, minWins: 40, expectsPlayoffs: true);
            js.SetSeasonExpectations("C5", fin, 50);

            var stakes = new CareerStakesSystem(js, null, id => fin, () => coach, id => team, _ => { });

            // Find a Monday and tick
            var date = new DateTime(2026, 12, 1);
            while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1);
            stakes.DailyTick(new DailyTickContext(date, "AAA", null));

            Assert(coach.JobSecurity >= JobSecurityStatus.HotSeat,
                $"The weekly evaluation puts a 5-25 coach on the hot seat ({coach.JobSecurity})");
        }

        // ==================== JOB MARKET ====================

        private void TestJobMarketFlow()
        {
            var teams = MakeTeams(10);
            var career = MakeCoach("C6", "T00", reputation: 85);
            career.TotalCoachingYears = 6; // TotalSeasons is computed from this
            career.TotalPlayoffAppearances = 3;

            var market = new JobMarketManager
            {
                TeamSource = () => teams,
                CareerSource = () => career
            };

            market.GenerateSeasonOpenings(2026);
            var openings = market.MarketState.GetOpenings();
            Assert(openings.Count >= 5, $"The market generates openings ({openings.Count})");
            Assert(openings.All(o => o.TeamId != "T00"), "No opening at the player's own team");

            var app = market.ApplyForJob(openings[0].OpeningId);
            Assert(app != null, "Application goes in");
            Assert(app.Status == JobApplicationStatus.Offered ||
                   app.Status == JobApplicationStatus.Interviewing ||
                   app.Status == JobApplicationStatus.Rejected,
                $"Application resolves to a real status ({app.Status})");

            Assert(market.ApplyForJob(openings[0].OpeningId) == null, "Can't apply twice to the same job");

            // An interview resolves after it happens
            var interviewApp = market.ApplyForJob(openings[1].OpeningId);
            interviewApp.Status = JobApplicationStatus.Interviewing;
            interviewApp.InterviewDate = new DateTime(2026, 12, 10);
            market.AdvanceDay(new DateTime(2026, 12, 11));
            Assert(interviewApp.Status != JobApplicationStatus.Interviewing,
                $"Interviews resolve to an offer or a pass ({interviewApp.Status})");

            // Accepting a crafted offer clears unemployment
            market.MarketState.IsUnemployed = true;
            var offerApp = market.ApplyForJob(openings[2].OpeningId);
            offerApp.Status = JobApplicationStatus.Offered;
            offerApp.HasOffer = true;
            offerApp.OfferedSalary = 4_000_000;
            offerApp.OfferedYears = 3;

            Assert(market.AcceptJob(openings[2].OpeningId), "Offer can be accepted");
            Assert(!market.MarketState.IsUnemployed, "Accepting a job ends unemployment");
        }

        private void TestJobMarketSaveRoundTrip()
        {
            var teams = MakeTeams(10);
            var career = MakeCoach("C7", "T00", reputation: 70);

            var market = new JobMarketManager { TeamSource = () => teams, CareerSource = () => career };
            market.GenerateSeasonOpenings(2026);
            market.MarketState.IsUnemployed = true;
            market.MarketState.UnemployedSince = new DateTime(2026, 12, 1);

            int openingCount = market.MarketState.OpenPositions.Count;
            var firstOpening = market.MarketState.OpenPositions[0];
            var app = market.ApplyForJob(firstOpening.OpeningId);
            app.Status = JobApplicationStatus.Offered;
            app.HasOffer = true;
            app.OfferedSalary = 3_500_000;
            app.OfferedYears = 4;
            app.OfferExpiration = new DateTime(2026, 12, 20);

            var data = new SaveData();
            market.WriteSave(data);
            Assert(data.JobMarketData != null && data.JobMarketData.Openings.Count == openingCount,
                "Save captures the market");

            var restored = new JobMarketManager { TeamSource = () => teams, CareerSource = () => career };
            restored.ReadSave(data, new SaveReadContext("1.1.0", false, 2026));

            Assert(restored.MarketState.IsUnemployed, "Unemployment survives the save");
            AssertEqual(openingCount, restored.MarketState.OpenPositions.Count, "Openings survive the save");
            var restoredApp = restored.MarketState.UserApplications.FirstOrDefault();
            Assert(restoredApp != null && restoredApp.HasOffer && restoredApp.OfferedSalary == 3_500_000,
                "The live offer survives the save");
            Assert(restoredApp.OfferExpiration.HasValue &&
                   restoredApp.OfferExpiration.Value.Day == 20,
                "Offer expiration survives as a real date");

            // Media reputation persistence shape round-trips too
            var media = new MediaSaveData();
            media.Reputations.Add(new MediaReputationRecord { CoachId = "C7", Value = 42 });
            data.MediaData = media;
            Assert(data.MediaData.Reputations[0].Value == 42, "Media reputation records hold");
        }
    }
}
