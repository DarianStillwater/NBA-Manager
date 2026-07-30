using System;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Phase O1 CBA plumbing: qualifying offers / RFA, two-way contracts as real
    /// contracts, cap-exception debits, supermax eligibility, and the free-agent
    /// save record (including the legacy default path).
    /// </summary>
    public class CBAContractsTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestQualifyingOfferAmount();
            TestRFATenderFlow();
            TestTwoWayContracts();
            TestRosterCounts();
            TestMLEDebit();
            TestSupermaxEligibility();
            TestFreeAgentRecordRoundTrip();
            TestRfaEligibilityUsesServiceYears();
            TestBirdYearsCountExpiringSeason();
            TestFreeAgentManagerClear();

            return (_passed, _failed);
        }

        private static Player MakePlayer(string id, int yearsPro, string teamId = "",
            string draftedBy = null, int shooting = 70)
        {
            return new Player
            {
                PlayerId = id, FirstName = "Test", LastName = id,
                TeamId = teamId, DraftedByTeamId = draftedBy ?? teamId,
                YearsPro = yearsPro, Position = Position.PointGuard,
                BirthDate = DateTime.Now.AddYears(-(19 + yearsPro)),
                Shot_Three = shooting, Passing = shooting,
                Energy = 100, Morale = 75
            };
        }

        // ==================== QUALIFYING OFFERS ====================

        private void TestQualifyingOfferAmount()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            var p = MakePlayer("qo1", 3, "BOS");
            db.AddPlayer(p);
            var fam = new FreeAgentManager(cap, db);

            long qo = fam.ComputeQualifyingOfferAmount(p, 8_000_000L);
            AssertEqual(10_000_000L, qo, "QO = 125% of prior salary when that beats the minimum");

            long floorQo = fam.ComputeQualifyingOfferAmount(p, 1_000_000L);
            AssertEqual(LeagueCBA.GetMinimumSalary(4), floorQo,
                "QO floors at the minimum for years-of-service + 1");

            AssertEqual(0L, fam.ComputeQualifyingOfferAmount(null), "Null player has no QO");

            // Falls back to the registered contract when no prior salary is passed
            cap.RegisterContract(new Contract
            {
                PlayerId = "qo1", TeamId = "BOS", YearsRemaining = 1,
                CurrentYearSalary = 4_000_000L
            });
            AssertEqual(5_000_000L, fam.ComputeQualifyingOfferAmount(p),
                "QO reads the live contract when no prior salary is supplied");
        }

        private void TestRFATenderFlow()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            var rfa = MakePlayer("rfa1", 3, "", "BOS");
            var ufa = MakePlayer("ufa1", 8, "", "BOS");
            db.AddPlayer(rfa); db.AddPlayer(ufa);

            var fam = new FreeAgentManager(cap, db);
            fam.AddFreeAgent("rfa1", FreeAgentType.Unrestricted, "BOS", 3);
            fam.AddFreeAgent("ufa1", FreeAgentType.Unrestricted, "BOS", 5);

            Assert(fam.ExtendQualifyingOffer("BOS", "rfa1", 6_000_000L),
                "Original team can tender a qualifying offer");
            Assert(!fam.ExtendQualifyingOffer("LAL", "rfa1", 6_000_000L),
                "Another team cannot tender a QO");

            var tendered = fam.GetFreeAgents().Find(f => f.PlayerId == "rfa1");
            AssertEqual(FreeAgentType.Restricted, tendered.Type, "Tendered player becomes restricted");
            Assert(tendered.HasQualifyingOffer, "Tender flag is set");
            AssertEqual(7_500_000L, tendered.QualifyingOfferAmount, "Tender carries the QO amount");

            var untendered = fam.GetFreeAgents().Find(f => f.PlayerId == "ufa1");
            AssertEqual(FreeAgentType.Unrestricted, untendered.Type, "Untendered player stays unrestricted");
            AssertEqual(0L, untendered.QualifyingOfferAmount, "Untendered player has no QO amount");

            // Bird rights still ride along with the consecutive-seasons count
            AssertEqual(BirdRightsType.Full, tendered.BirdRights, "3 consecutive seasons = full Bird rights");
        }

        // ==================== TWO-WAY CONTRACTS ====================

        private void TestTwoWayContracts()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            var p = MakePlayer("tw1", 1);
            db.AddPlayer(p);
            var fam = new FreeAgentManager(cap, db);
            fam.AddFreeAgent("tw1", FreeAgentType.Unrestricted, "GSW", 0);

            cap.RegisterContract(new Contract
            {
                PlayerId = "vet1", TeamId = "LAL", YearsRemaining = 3,
                CurrentYearSalary = 100_000_000L
            });

            bool signed = fam.ExecuteSigning("LAL", "tw1", new SigningOffer
            {
                AnnualSalary = LeagueCBA.GetTwoWaySalary(),
                Years = 1,
                Method = SigningMethod.TwoWayContract,
                PlayerYearsExperience = 1
            });

            Assert(signed, "Two-way signing executes");
            var contract = cap.GetContract("tw1");
            Assert(contract != null && contract.IsTwoWay, "Two-way signing produces a two-way contract");
            AssertEqual(ContractType.TwoWay, contract.Type, "Contract type is TwoWay");
            AssertEqual(100_000_000L, cap.GetTeamPayroll("LAL"), "Two-way salary is excluded from cap payroll");
            AssertEqual(1, cap.GetTwoWayContractCount("LAL"), "Two-way contract counted");

            // Minimum signing keeps the offered salary but is typed as a minimum deal
            var min = MakePlayer("min1", 6);
            db.AddPlayer(min);
            fam.AddFreeAgent("min1", FreeAgentType.Unrestricted, "GSW", 0);
            fam.ExecuteSigning("LAL", "min1", new SigningOffer
            {
                AnnualSalary = LeagueCBA.GetMinimumSalary(6),
                Years = 1,
                Method = SigningMethod.MinimumSalary,
                PlayerYearsExperience = 6
            });
            AssertEqual(ContractType.Minimum, cap.GetContract("min1").Type,
                "Minimum signing stamps ContractType.Minimum");

            // Two-way slots cap out at 3
            for (int i = 2; i <= 4; i++)
            {
                var extra = MakePlayer($"tw{i}", 1);
                db.AddPlayer(extra);
                fam.AddFreeAgent($"tw{i}", FreeAgentType.Unrestricted, "GSW", 0);
                fam.ExecuteSigning("LAL", $"tw{i}", new SigningOffer
                {
                    AnnualSalary = LeagueCBA.GetTwoWaySalary(),
                    Years = 1,
                    Method = SigningMethod.TwoWayContract,
                    PlayerYearsExperience = 1
                });
            }
            AssertEqual(LeagueCBA.MAX_TWO_WAY_CONTRACTS, cap.GetTwoWayContractCount("LAL"),
                "Two-way signings stop at the 3-slot limit");
        }

        private void TestRosterCounts()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            var fam = new FreeAgentManager(cap, db);

            for (int i = 0; i < 15; i++)
                cap.RegisterContract(new Contract
                {
                    PlayerId = $"std{i}", TeamId = "NYK", YearsRemaining = 2,
                    CurrentYearSalary = 2_000_000L
                });
            cap.RegisterContract(new Contract
            {
                PlayerId = "nykTwoWay", TeamId = "NYK", Type = ContractType.TwoWay,
                YearsRemaining = 1, CurrentYearSalary = LeagueCBA.GetTwoWaySalary()
            });

            AssertEqual(15, cap.GetStandardContractCount("NYK"), "Standard count excludes two-ways");
            AssertEqual(1, cap.GetTwoWayContractCount("NYK"), "Two-way count is separate");

            var p = MakePlayer("full1", 6);
            db.AddPlayer(p);
            fam.AddFreeAgent("full1", FreeAgentType.Unrestricted, "GSW", 0);
            var check = fam.CanSign("NYK", "full1", new SigningOffer
            {
                AnnualSalary = LeagueCBA.GetMinimumSalary(6), Years = 1,
                Method = SigningMethod.MinimumSalary, PlayerYearsExperience = 6
            });
            Assert(!check.IsValid, "Standard signing rejected on a full 15-man roster");

            // The two-way slot is still open on the same full roster
            var twoWayGuy = MakePlayer("full2", 1);
            db.AddPlayer(twoWayGuy);
            fam.AddFreeAgent("full2", FreeAgentType.Unrestricted, "GSW", 0);
            Assert(fam.CanSign("NYK", "full2", new SigningOffer
            {
                AnnualSalary = LeagueCBA.GetTwoWaySalary(), Years = 1,
                Method = SigningMethod.TwoWayContract, PlayerYearsExperience = 1
            }).IsValid, "Two-way signing allowed with 15 standard players");

            // RosterManager's registry count also excludes two-ways
            var roster = new RosterManager(cap);
            roster.AddToStandardRoster("NYK", "std0");
            roster.AddTwoWayPlayer("NYK", "nykTwoWay", 1);
            AssertEqual(1, roster.GetStandardRosterCount("NYK"),
                "RosterManager standard count excludes two-way players");
            AssertEqual(1, roster.GetTwoWayCount("NYK"), "RosterManager tracks the two-way slot");
        }

        // ==================== CAP EXCEPTIONS ====================

        private void TestMLEDebit()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            var fam = new FreeAgentManager(cap, db);

            // Over the cap, under the aprons -> non-taxpayer MLE
            cap.RegisterContract(new Contract
            {
                PlayerId = "bigdeal", TeamId = "MIA", YearsRemaining = 3,
                CurrentYearSalary = 150_000_000L
            });
            AssertEqual(TeamCapStatus.OverCap, cap.GetCapStatus("MIA"), "Team is over the cap");

            var a = MakePlayer("mle1", 5); db.AddPlayer(a);
            var b = MakePlayer("mle2", 5); db.AddPlayer(b);
            fam.AddFreeAgent("mle1", FreeAgentType.Unrestricted, "GSW", 0);
            fam.AddFreeAgent("mle2", FreeAgentType.Unrestricted, "GSW", 0);

            Assert(fam.ExecuteSigning("MIA", "mle1", new SigningOffer
            {
                AnnualSalary = 6_000_000L, Years = 2,
                Method = SigningMethod.MidLevelException, PlayerYearsExperience = 5
            }), "First MLE signing executes");

            AssertEqual(6_000_000L, fam.GetUsage("MIA").MLEUsed, "MLE usage debited by the salary");
            AssertEqual(ContractType.MidLevel, cap.GetContract("mle1").Type,
                "MLE signing stamps ContractType.MidLevel");

            var second = fam.CanSign("MIA", "mle2", new SigningOffer
            {
                AnnualSalary = 8_000_000L, Years = 2,
                Method = SigningMethod.MidLevelException, PlayerYearsExperience = 5
            });
            Assert(!second.IsValid, "Second MLE signing rejected beyond the remaining exception");

            Assert(fam.CanSign("MIA", "mle2", new SigningOffer
            {
                AnnualSalary = LeagueCBA.GetMinimumSalary(5), Years = 1,
                Method = SigningMethod.MinimumSalary, PlayerYearsExperience = 5
            }).IsValid, "Vet minimum is always available to an over-cap team");
        }

        // ==================== SUPERMAX ====================

        private void TestSupermaxEligibility()
        {
            var priorInstance = AwardsStore.Instance;   // restore after — don't clobber it for later tests
            try
            {
                var store = new AwardsStore();   // ctor sets the static Instance
                var lastSeason = store.GetOrCreate(2030);
                lastSeason.MvpId = "star";
                lastSeason.AllNbaFirst.Add("allnba");
                store.GetOrCreate(2029).AllNbaSecond.Add("twotime");
                store.GetOrCreate(2028).AllNbaThird.Add("twotime");

                var star = MakePlayer("star", 8, "BOS", "BOS");
                Assert(LeagueCBA.IsSuperMaxEligible(star, 2030),
                    "8-year MVP on his drafting team is supermax eligible");

                var moved = MakePlayer("star", 8, "LAL", "BOS");
                Assert(!LeagueCBA.IsSuperMaxEligible(moved, 2030),
                    "8-year MVP is ineligible away from his drafting team");

                var vet = MakePlayer("allnba", 11, "LAL", "BOS");
                Assert(LeagueCBA.IsSuperMaxEligible(vet, 2030),
                    "10+ year All-NBA last season is eligible with any team");

                var twotime = MakePlayer("twotime", 12, "LAL", "BOS");
                Assert(LeagueCBA.IsSuperMaxEligible(twotime, 2030),
                    "All-NBA in 2 of the last 3 seasons is eligible");

                var rookie = MakePlayer("star", 1, "BOS", "BOS");
                Assert(!LeagueCBA.IsSuperMaxEligible(rookie, 2030),
                    "Rookie fails the service requirement");

                var awardless = MakePlayer("nobody", 12, "BOS", "BOS");
                Assert(!LeagueCBA.IsSuperMaxEligible(awardless, 2030),
                    "Long service without honors is ineligible");

                Assert(!LeagueCBA.IsSuperMaxEligible(null, 2030), "Null player is ineligible");
            }
            finally
            {
                AwardsStore.SetInstanceForTests(priorInstance);
            }
        }

        // ==================== SAVE ROUND TRIP ====================

        private void TestFreeAgentRecordRoundTrip()
        {
            var record = new FreeAgentRecord
            {
                PlayerId = "rfa1", PreviousTeamId = "BOS", ConsecutiveSeasons = 3,
                TypeInt = (int)FreeAgentType.Restricted,
                HasQualifyingOffer = true, QualifyingOfferAmount = 7_500_000L
            };

            var restored = JsonUtility.FromJson<FreeAgentRecord>(JsonUtility.ToJson(record));
            AssertEqual(3, restored.ConsecutiveSeasons, "ConsecutiveSeasons survives the round trip");
            AssertEqual((int)FreeAgentType.Restricted, restored.TypeInt, "RFA status survives");
            Assert(restored.HasQualifyingOffer, "QO flag survives");
            AssertEqual(7_500_000L, restored.QualifyingOfferAmount, "QO amount survives");

            // Legacy save: only the pre-O1 fields are present
            var legacy = JsonUtility.FromJson<FreeAgentRecord>(
                "{\"PlayerId\":\"old1\",\"PreviousTeamId\":\"BOS\"}");
            AssertEqual(0, legacy.TypeInt, "Legacy record defaults to unrestricted");
            AssertEqual(0, legacy.ConsecutiveSeasons, "Legacy record defaults to 0 Bird seasons");
            Assert(!legacy.HasQualifyingOffer, "Legacy record has no qualifying offer");
            AssertEqual(0L, legacy.QualifyingOfferAmount, "Legacy record has no QO amount");

            // And the restore path maps those defaults onto the live pool
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            db.AddPlayer(MakePlayer("old1", 4));
            var fam = new FreeAgentManager(cap, db);
            fam.AddFreeAgent(legacy.PlayerId, (FreeAgentType)legacy.TypeInt, legacy.PreviousTeamId,
                legacy.ConsecutiveSeasons, legacy.HasQualifyingOffer, legacy.QualifyingOfferAmount);
            var live = fam.GetFreeAgents().Find(f => f.PlayerId == "old1");
            AssertEqual(FreeAgentType.Unrestricted, live.Type, "Legacy free agent restores as UFA");
            AssertEqual(BirdRightsType.None, live.BirdRights, "Legacy free agent has no Bird rights");

            db.AddPlayer(MakePlayer("rfa1", 3));
            fam.AddFreeAgent(restored.PlayerId, (FreeAgentType)restored.TypeInt, restored.PreviousTeamId,
                restored.ConsecutiveSeasons, restored.HasQualifyingOffer, restored.QualifyingOfferAmount);
            var liveRfa = fam.GetFreeAgents().Find(f => f.PlayerId == "rfa1");
            AssertEqual(FreeAgentType.Restricted, liveRfa.Type, "Restored RFA keeps restricted status");
            AssertEqual(7_500_000L, liveRfa.QualifyingOfferAmount, "Restored RFA keeps its QO amount");
            AssertEqual(BirdRightsType.Full, liveRfa.BirdRights, "Restored RFA keeps full Bird rights");
        }

        // ==================== RFA ELIGIBILITY (service years, not YearsPro) ====================

        private void TestRfaEligibilityUsesServiceYears()
        {
            var vetContract = new Contract { Type = ContractType.Standard };
            var vet = MakePlayer("vet1", 0, "BOS", "BOS");
            vet.DraftYear = 2018;
            Assert(!OffseasonManager.IsRfaEligible(vet, vetContract, 2026),
                "8-years-out veteran (DraftYear-derived) is not RFA-tendered");

            var youngContract = new Contract { Type = ContractType.Standard };
            var young = MakePlayer("young1", 0, "BOS", "BOS");
            young.DraftYear = 2024;
            Assert(OffseasonManager.IsRfaEligible(young, youngContract, 2026),
                "2-years-out player is within the RFA service window");
        }

        // ==================== BIRD YEARS OFF-BY-ONE ====================

        private void TestBirdYearsCountExpiringSeason()
        {
            var cap = new SalaryCapManager();
            cap.RegisterContract(new Contract
            {
                PlayerId = "bird1", TeamId = "BOS", YearsRemaining = 1,
                CurrentYearSalary = 4_000_000L, ConsecutiveSeasonsWithTeam = 0
            });

            var expired = cap.AdvanceContractYears();
            var contract = expired.Find(c => c.PlayerId == "bird1");
            AssertEqual(1, contract?.ConsecutiveSeasonsWithTeam ?? -1,
                "Completed final season counts toward Bird rights even though the contract expires");
        }

        // ==================== FREE AGENT POOL CLEAR ====================

        private void TestFreeAgentManagerClear()
        {
            var cap = new SalaryCapManager();
            var db = new PlayerDatabase();
            db.AddPlayer(MakePlayer("clear1", 3));
            var fam = new FreeAgentManager(cap, db);
            fam.AddFreeAgent("clear1", FreeAgentType.Unrestricted);
            Assert(fam.GetFreeAgents().Count > 0, "Pool has a free agent before Clear");

            fam.Clear();
            AssertEqual(0, fam.GetFreeAgents().Count, "Clear empties the free agent pool");
        }
    }
}
