using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the offseason engine's mechanical pieces: contract season advancement
    /// and expiry, free-agent conversion and signing, the draft (lottery, order,
    /// 60 AI picks producing real players with rookie contracts), and retirement
    /// evaluation inputs.
    /// </summary>
    public class OffseasonEngineTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestContractAdvancement();
            TestFreeAgencySigning();
            TestLottery();
            TestFullDraft();
            TestManualPickAndPrune();
            TestRetirementEvaluation();

            return (_passed, _failed);
        }

        private void TestContractAdvancement()
        {
            var cap = new SalaryCapManager();
            cap.RegisterContract(new Contract
            {
                PlayerId = "exp1", TeamId = "GSW", YearsRemaining = 1,
                CurrentYearSalary = 10_000_000L
            });
            cap.RegisterContract(new Contract
            {
                PlayerId = "mid1", TeamId = "GSW", YearsRemaining = 2,
                CurrentYearSalary = 20_000_000L, AnnualRaisePercent = 8f,
                ConsecutiveSeasonsWithTeam = 3
            });
            cap.RegisterContract(new Contract
            {
                PlayerId = "long1", TeamId = "LAL", YearsRemaining = 4,
                CurrentYearSalary = 30_000_000L
            });

            var expired = cap.AdvanceContractYears();

            AssertEqual(1, expired.Count, "Exactly the final-year contract expires");
            AssertEqual("exp1", expired[0].PlayerId, "Expiring player identified");
            Assert(cap.GetContract("exp1") == null, "Expired contract removed from registry");

            var mid = cap.GetContract("mid1");
            AssertEqual(1, mid.YearsRemaining, "Mid contract decrements to final year");
            AssertEqual(21_600_000L, mid.CurrentYearSalary, "Salary rolls by the annual raise");
            AssertEqual(4, mid.ConsecutiveSeasonsWithTeam, "Bird-rights seasons accrue");
            AssertEqual(3, cap.GetContract("long1").YearsRemaining, "Long contract decrements");
        }

        private void TestFreeAgencySigning()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            var player = new Player
            {
                PlayerId = "fa1", FirstName = "Free", LastName = "Agent",
                TeamId = "", Position = Position.PointGuard,
                BirthDate = DateTime.Now.AddYears(-28),
                Shot_Three = 80, Passing = 80, Energy = 100, Morale = 75
            };
            db.AddPlayer(player);

            var fam = new FreeAgentManager(cap, db);
            fam.AddFreeAgent("fa1", FreeAgentType.Unrestricted, "GSW", 2);
            AssertEqual(1, fam.GetFreeAgents().Count, "Player enters the FA pool");

            bool signed = fam.ExecuteSigning("LAL", "fa1", new SigningOffer
            {
                AnnualSalary = 1_200_000L,
                Years = 2,
                Method = SigningMethod.MinimumSalary,
                PlayerYearsExperience = 6
            });

            Assert(signed, "Minimum signing executes");
            AssertEqual("LAL", player.TeamId, "Signing sets the player's team");
            var contract = cap.GetContract("fa1");
            Assert(contract != null && contract.TeamId == "LAL", "Contract registered to the new team");
            AssertEqual(0, fam.GetFreeAgents().Count, "Signed player leaves the FA pool");
        }

        private void TestLottery()
        {
            var draft = new DraftSystem(new SalaryCapManager(), null, seed: 42);
            var lotteryTeams = Enumerable.Range(1, 14).Select(i => $"L{i:00}").ToList();

            var resultA = draft.RunLottery(lotteryTeams, new System.Random(7));
            var resultB = draft.RunLottery(lotteryTeams, new System.Random(7));

            AssertEqual(14, resultA.Count, "Lottery returns all 14 teams");
            AssertEqual(14, resultA.Select(r => r.TeamId).Distinct().Count(),
                "Lottery result is a permutation");
            AssertEqual(string.Join(",", resultA.Select(r => r.TeamId)),
                string.Join(",", resultB.Select(r => r.TeamId)),
                "Lottery deterministic under the same rng");
        }

        private void TestFullDraft()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            var draft = new DraftSystem(cap, db, seed: 42);

            var prospects = draft.GenerateDraftClass(2026);
            AssertGreaterThan(prospects.Count, 59, "Draft class has 60+ prospects");

            var teamIds = Enumerable.Range(1, 30).Select(i => $"D{i:00}").ToList();
            draft.SetDraftOrder(teamIds, teamIds);

            int made = 0;
            for (int pick = 1; pick <= 60; pick++)
            {
                string teamId = draft.GetTeamAtPick(pick);
                var selection = draft.AISelectPick(pick, teamId);
                if (selection?.DraftedPlayer != null) made++;
            }

            AssertEqual(60, made, "All 60 picks produce drafted players");
            var results = draft.GetDraftResults();
            AssertEqual(60, results.Select(r => r.Prospect.ProspectId).Distinct().Count(),
                "No prospect drafted twice");
            AssertEqual(60, results.Count(r => cap.GetContract(r.DraftedPlayer.PlayerId) != null),
                "Every pick gets a rookie-scale contract");
            Assert(results.All(r => db.GetPlayer(r.DraftedPlayer.PlayerId) != null),
                "Every drafted player registered in the database");
            Assert(results.All(r => r.DraftedPlayer.TeamId == r.TeamId),
                "Drafted players assigned to their drafting team");

            // Talent gradient: top-10 picks outrate picks 51-60 on average
            float topAvg = (float)results.Take(10).Average(r => r.DraftedPlayer.OverallRating);
            float botAvg = (float)results.Skip(50).Average(r => r.DraftedPlayer.OverallRating);
            AssertGreaterThan(topAvg, botAvg, "Draft order reflects talent");
        }

        private void TestManualPickAndPrune()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            var draft = new DraftSystem(cap, db, seed: 7);
            var prospects = draft.GenerateDraftClass(2027);
            var teamIds = Enumerable.Range(1, 30).Select(i => $"M{i:00}").ToList();
            draft.SetDraftOrder(teamIds, teamIds);

            // The user-pick primitive: pick a SPECIFIC prospect, not best-available
            var target = prospects.OrderBy(p => p.MockDraftPosition).Skip(4).First();
            var selection = draft.MakePick(1, teamIds[0], target.ProspectId);
            Assert(selection?.DraftedPlayer != null, "Manual pick drafts the chosen prospect");
            AssertEqual(target.ProspectId, selection.Prospect.ProspectId, "Chosen prospect selected");
            Assert(draft.GetProspect(target.ProspectId) == null, "Chosen prospect leaves the pool");

            // AI continues around the manual pick without re-drafting him
            var next = draft.AISelectPick(2, teamIds[1]);
            Assert(next?.Prospect?.ProspectId != target.ProspectId, "AI cannot re-draft a taken prospect");

            // Mid-draft load reconciliation: prune prospects whose player already exists
            var draftB = new DraftSystem(new SalaryCapManager(), db, seed: 7);
            draftB.GenerateDraftClass(2027);
            int before = draftB.GetProspects().Count;
            int pruned = draftB.RemoveProspects(p => db.GetPlayer($"draft_2027_{p.ProspectId}") != null);
            AssertEqual(2, pruned, "Regenerated class prunes the two already-drafted prospects");
            AssertEqual(before - 2, draftB.GetProspects().Count, "Pool shrinks by the pruned count");
        }

        private void TestRetirementEvaluation()
        {
            var rm = new RetirementManager();
            var vet = new PlayerCareerData
            {
                PlayerId = "old1",
                FullName = "Aging Veteran",
                Age = 40,
                SeasonsPlayed = 18,
                GamesPlayed = 1300,
                CareerPoints = 25000,
                CurrentTeamId = "GSW",
                CurrentRating = 68,
                PeakRating = 92,
                BasketballIQ = 85,
                Leadership = 90
            };

            var factors = rm.EvaluateRetirements(new List<PlayerCareerData> { vet });

            AssertEqual(1, factors.Count, "40-year-old evaluated for retirement");
            AssertRange(factors[0].OverallRetirementProbability, 0f, 1f,
                "Retirement probability in [0,1]");
            AssertGreaterThan(factors[0].OverallRetirementProbability, 0.3f,
                "An 18-year vet at 40 has meaningful retirement odds");

            var young = new PlayerCareerData { PlayerId = "kid1", Age = 25, CurrentTeamId = "GSW" };
            var none = rm.EvaluateRetirements(new List<PlayerCareerData> { young });
            AssertEqual(0, none.Count, "25-year-old is not evaluated");
        }
    }
}
