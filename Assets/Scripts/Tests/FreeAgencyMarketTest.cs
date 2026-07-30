using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Phase O2 free-agency market: AI bid generation under the cap, bidding wars,
    /// decision-day resolution, RFA offer sheets (match / no-match / tax-blocked),
    /// player-team bids and pending match decisions, fuzzy agent intel, the daily
    /// digest, and market/exception-usage save round trips.
    /// </summary>
    public class FreeAgencyMarketTest : BaseTest
    {
        private static readonly DateTime Day1 = new DateTime(2026, 7, 6);

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestBidsRespectCapAndSlots();
            TestBiddingWarEscalates();
            TestDecisionDayPicksBestBid();
            TestOfferSheetMatchHeuristics();
            TestPlayerPendingMatchDecision();
            TestAgentIntelIsFuzzyAndStable();
            TestDigestCountsSignings();
            TestMarketSaveRoundTrip();
            TestExceptionUsageRoundTrip();
            TestPoolStillDrainsWithoutSuitors();
            TestMatchDeadlineDayIsUsable();
            TestMatchKeepsSheetYears();
            TestScrapHeapPricing();
            TestOwnContestedFreeAgentSignsInstantly();
            TestRestrictedFreeAgentKeepsFirstRefusal();

            return (_passed, _failed);
        }

        // ==================== RIG ====================

        private class Rig
        {
            public SalaryCapManager Cap = new SalaryCapManager();
            public PlayerDatabase Db = new PlayerDatabase();
            public FreeAgentManager Fam;
            public List<Team> Teams = new List<Team>();
            public string PlayerTeamId = "";
            public FreeAgencyMarket Market;

            public Rig()
            {
                Fam = new FreeAgentManager(Cap, Db);
                Market = new FreeAgencyMarket(Fam, Cap, Db, () => Teams, () => PlayerTeamId, 7);
            }

            public Team AddTeam(string id, long payroll = 0, int wins = 41, int filler = 0)
            {
                var team = new Team { TeamId = id, City = id, Nickname = id, Wins = wins };
                Teams.Add(team);
                if (payroll > 0)
                    Cap.RegisterContract(new Contract
                    {
                        PlayerId = $"{id}_star", TeamId = id, YearsRemaining = 3,
                        CurrentYearSalary = payroll
                    });
                for (int i = 0; i < filler; i++)
                    Cap.RegisterContract(new Contract
                    {
                        PlayerId = $"{id}_f{i}", TeamId = id, YearsRemaining = 2,
                        CurrentYearSalary = 1_000_000L
                    });
                return team;
            }

            public Player AddFreeAgent(string id, int rating, FreeAgentType type = FreeAgentType.Unrestricted,
                string previousTeamId = null, int consecutiveSeasons = 0)
            {
                var p = new Player
                {
                    PlayerId = id, FirstName = "Free", LastName = id,
                    Position = Position.PointGuard,
                    BirthDate = new DateTime(1998, 1, 1),
                    DraftYear = 2018,
                    BallHandling = rating, Passing = rating, Shot_Three = rating,
                    Speed = rating, Defense_Perimeter = rating, BasketballIQ = rating,
                    Energy = 100, Morale = 75
                };
                Db.AddPlayer(p);
                Fam.AddFreeAgent(id, type, previousTeamId, consecutiveSeasons);
                return p;
            }

            public void RunDays(int days, DateTime from)
            {
                for (int i = 0; i < days; i++) Market.RunDay(from.AddDays(i));
            }
        }

        private static NBAHeadCoach.Core.Data.MarketBidRecord Bid(string playerId, string teamId,
            int years, long salary, DateTime decisionDay)
        {
            return new NBAHeadCoach.Core.Data.MarketBidRecord
            {
                PlayerId = playerId, TeamId = teamId, Years = years, AnnualSalary = salary,
                MethodInt = (int)SigningMethod.CapSpace, DecisionDayStr = decisionDay.ToString("o")
            };
        }

        private static NBAHeadCoach.Core.Data.MarketBidRecord Sheet(string playerId, string bidderId,
            string originalTeamId, long salary, DateTime deadline)
        {
            return new NBAHeadCoach.Core.Data.MarketBidRecord
            {
                PlayerId = playerId, TeamId = bidderId, Years = 4, AnnualSalary = salary,
                MethodInt = (int)SigningMethod.CapSpace, IsOfferSheet = true,
                OriginalTeamId = originalTeamId, MatchDeadlineStr = deadline.ToString("o")
            };
        }

        // ==================== BID GENERATION ====================

        private void TestBidsRespectCapAndSlots()
        {
            var rig = new Rig();
            rig.AddTeam("RICH");                                  // full cap space
            rig.AddTeam("FULL", filler: 15);                      // 15 standard contracts
            rig.AddTeam("POOR", payroll: 138_000_000L);           // ~$2.5M of room
            rig.AddFreeAgent("star", 82);                         // $22M market value

            // Collect every bidder seen across the market days (the day a suitor
            // appears is random; who is *allowed* to appear is not)
            var bidders = new HashSet<string>();
            long maxBid = 0;
            for (int i = 0; i < 8; i++)
            {
                rig.Market.RunDay(Day1.AddDays(i));
                foreach (var b in rig.Market.GetBids("star"))
                {
                    bidders.Add(b.TeamId);
                    maxBid = Math.Max(maxBid, b.AnnualAverage);
                }
            }

            Assert(!bidders.Contains("FULL"), "A team with 15 standard contracts never bids");
            Assert(!bidders.Contains("POOR"), "A team without room for the ask never bids");
            Assert(maxBid <= LeagueCBA.SALARY_CAP, "No bid exceeds what a team could physically pay");
            Assert(rig.Market.MarketedPlayerIds.Contains("star") ||
                   !string.IsNullOrEmpty(rig.Db.GetPlayer("star").TeamId),
                "The top free agent is on the market (or already signed off it)");

            // A team with room does bid, on day one
            var solo = new Rig();
            solo.AddTeam("RICH2");
            solo.AddFreeAgent("star2", 82);
            solo.Market.RunDay(Day1);
            var soloBids = solo.Market.GetBids("star2");
            AssertEqual(1, soloBids.Count, "The one team with room puts an offer in on day one");
            AssertEqual("RICH2", soloBids[0].TeamId, "That offer comes from the team with room");
            AssertEqual(22_000_000L, soloBids[0].AnnualAverage, "The opening bid is the market ask");
            Assert(solo.Market.GetDecisionDay("star2").HasValue,
                "A marketed free agent gets a decision day");
        }

        private void TestBiddingWarEscalates()
        {
            var rig = new Rig();
            rig.AddTeam("A"); rig.AddTeam("B");
            rig.AddFreeAgent("war", 82);

            // Seeded market: B leads at $30M, A trails at $22M, decision still days out
            rig.Market.Restore(new List<NBAHeadCoach.Core.Data.MarketBidRecord>
            {
                Bid("war", "A", 3, 22_000_000L, Day1.AddDays(5)),
                Bid("war", "B", 3, 30_000_000L, Day1.AddDays(5))
            }, Day1);

            rig.Market.RunDay(Day1);
            var bids = rig.Market.GetBids("war");
            var trailing = bids.First(b => b.TeamId == "A");

            AssertEqual(2, bids.Count, "Both bids stay live through the day");
            AssertGreaterThan(trailing.AnnualAverage, 22_000_000L,
                "The trailing bidder raises when it's been outbid");
            AssertGreaterThan(bids.Max(b => b.AnnualAverage), 30_000_000L,
                "The bidding war pushes the top offer past the old leader");
            AssertEqual(trailing.AnnualAverage * trailing.Years, trailing.TotalValue,
                "Total value tracks the raised salary");
        }

        private void TestDecisionDayPicksBestBid()
        {
            var rig = new Rig { PlayerTeamId = "MINE" };
            rig.AddTeam("MINE");
            rig.AddTeam("RIVAL1", payroll: 120_000_000L);   // ~$20M of room only
            rig.AddTeam("RIVAL2", payroll: 120_000_000L);
            var target = rig.AddFreeAgent("prize", 82);

            rig.Market.RunDay(Day1);
            bool placed = rig.Market.PlaceBid("prize", 4, 40_000_000L, out string why);
            Assert(placed, $"Your team can place a bid ({why})");
            Assert(rig.Market.GetPlayerTeamBid("prize") != null, "Your bid is queryable");
            Assert(rig.Market.IsContested("prize", "MINE"), "A rival bid marks the free agent contested");

            for (int i = 1; i <= 8 && string.IsNullOrEmpty(target.TeamId); i++)
                rig.Market.RunDay(Day1.AddDays(i));

            AssertEqual("MINE", target.TeamId, "The decision goes to the best offer on the table");
            AssertEqual(0, rig.Market.GetBids("prize").Count, "Bids clear once he signs");

            // Withdraw path
            var rig2 = new Rig { PlayerTeamId = "MINE" };
            rig2.AddTeam("MINE");
            rig2.AddFreeAgent("pull", 82);
            rig2.Market.RunDay(Day1);
            rig2.Market.PlaceBid("pull", 3, 20_000_000L, out _);
            Assert(rig2.Market.WithdrawBid("pull"), "You can withdraw your bid");
            AssertEqual(0, rig2.Market.GetBids("pull").Count, "Withdrawn bid is gone");
        }

        // ==================== RESTRICTED FREE AGENCY ====================

        /// <summary>Seeds an offer sheet through the save path (no AI noise).</summary>
        private static Rig RigWithSheet(long sheetSalary, long originalPayroll, string playerTeam = "",
            int consecutiveSeasons = 3)
        {
            var rig = new Rig { PlayerTeamId = playerTeam };
            rig.AddTeam("BID");
            rig.AddTeam("ORIG", payroll: originalPayroll);
            rig.AddFreeAgent("rfa", 82, FreeAgentType.Restricted, "ORIG", consecutiveSeasons);
            rig.Fam.ExtendQualifyingOffer("ORIG", "rfa", 6_000_000L);

            rig.Market.Restore(new List<NBAHeadCoach.Core.Data.MarketBidRecord>
            {
                Sheet("rfa", "BID", "ORIG", sheetSalary, Day1.AddDays(1))
            }, Day1);
            return rig;
        }

        private void TestOfferSheetMatchHeuristics()
        {
            // Sheets are seeded with a Day1+1 deadline and auto-resolve the day AFTER
            var resolveDay = Day1.AddDays(2);

            // At market value, under the tax: the original team matches
            var fair = RigWithSheet(22_000_000L, 100_000_000L);
            fair.Market.RunDay(resolveDay);
            AssertEqual("ORIG", fair.Db.GetPlayer("rfa").TeamId,
                "AI matches an offer sheet at market value while under the tax");

            // Way over market value: the team lets him walk
            var rich = RigWithSheet(30_000_000L, 100_000_000L);
            rich.Market.RunDay(resolveDay);
            AssertEqual("BID", rich.Db.GetPlayer("rfa").TeamId,
                "AI declines a sheet above ~110% of market value");

            // Matching would cross the tax line: no match
            var taxed = RigWithSheet(22_000_000L, 160_000_000L);
            taxed.Market.RunDay(resolveDay);
            AssertEqual("BID", taxed.Db.GetPlayer("rfa").TeamId,
                "AI declines a match that would push it over the tax line");

            AssertEqual(0, taxed.Market.PendingMatchDecisions.Count,
                "Resolved sheets leave no pending decision");
        }

        private void TestPlayerPendingMatchDecision()
        {
            // Surfaces for the player's team
            var pending = RigWithSheet(22_000_000L, 100_000_000L, playerTeam: "ORIG");
            AssertEqual(1, pending.Market.PendingMatchDecisions.Count,
                "An offer sheet on your own RFA surfaces as a pending decision");

            bool matched = pending.Market.ResolveMatchDecision("rfa", true, out string why);
            Assert(matched, $"You can match the sheet ({why})");
            AssertEqual("ORIG", pending.Db.GetPlayer("rfa").TeamId, "Matching keeps him");
            AssertEqual(0, pending.Market.PendingMatchDecisions.Count, "Decision clears once made");

            // Declining lets the sheet through
            var declined = RigWithSheet(22_000_000L, 100_000_000L, playerTeam: "ORIG");
            Assert(declined.Market.ResolveMatchDecision("rfa", false, out _), "You can decline to match");
            AssertEqual("BID", declined.Db.GetPlayer("rfa").TeamId, "Declining loses him to the bidder");

            // Ignoring it past the deadline auto-resolves with the same heuristic
            var ignored = RigWithSheet(30_000_000L, 100_000_000L, playerTeam: "ORIG");
            ignored.Market.RunDay(Day1.AddDays(2));
            AssertEqual(0, ignored.Market.PendingMatchDecisions.Count,
                "An ignored decision auto-resolves at the deadline");
            AssertEqual("BID", ignored.Db.GetPlayer("rfa").TeamId,
                "Auto-resolution uses the same match heuristic");

            Assert(!ignored.Market.ResolveMatchDecision("rfa", true, out string gone),
                "Resolving a sheet that no longer exists fails cleanly");
            Assert(!string.IsNullOrEmpty(gone), "The failure explains itself");
        }

        // ==================== AGENT INTEL ====================

        private void TestAgentIntelIsFuzzyAndStable()
        {
            var rig = new Rig { PlayerTeamId = "MINE" };
            rig.AddTeam("MINE");
            rig.AddFreeAgent("intel", 82);

            AssertEqual(true, rig.Market.GetAgentIntel("intel").Contains("Quiet"),
                "No bids reads as a quiet market");

            rig.Market.RunDay(Day1);
            rig.Market.PlaceBid("intel", 3, 23_400_000L, out _);

            string first = rig.Market.GetAgentIntel("intel");
            string second = rig.Market.GetAgentIntel("intel");
            AssertEqual(first, second, "Agent intel is stable within the same day");
            Assert(!first.Contains("23.4") && !first.Contains("23400000"),
                "Agent intel never leaks the exact number");
            Assert(first.Contains("around $"), "Agent intel gives a ballpark figure");
            Assert(first.Contains("team") || first.Contains("market"),
                "Agent intel describes how crowded the market is");
        }

        // ==================== DIGEST ====================

        private void TestDigestCountsSignings()
        {
            var rig = new Rig();
            rig.AddTeam("BUY");
            rig.AddFreeAgent("d1", 82);
            rig.AddFreeAgent("d2", 82);

            // Two sheets nobody will match -> two signings on the same day
            rig.Market.Restore(new List<NBAHeadCoach.Core.Data.MarketBidRecord>
            {
                Sheet("d1", "BUY", "GONE", 20_000_000L, Day1),
                Sheet("d2", "BUY", "GONE", 20_000_000L, Day1)
            }, Day1);

            rig.Market.RunDay(Day1.AddDays(1));   // sheets resolve the day after the deadline
            var lines = rig.Market.LastDigest.Split('\n').Where(l => l.Length > 0).ToList();
            AssertEqual(2, lines.Count, "The digest carries one line per move");
            Assert(lines.All(l => l.Contains("BUY")), "Digest lines name the signing team");
        }

        // ==================== PERSISTENCE ====================

        private void TestMarketSaveRoundTrip()
        {
            var rig = new Rig { PlayerTeamId = "MINE" };
            rig.AddTeam("MINE"); rig.AddTeam("OTHER");
            rig.AddFreeAgent("keep", 82);
            rig.Market.RunDay(Day1);
            rig.Market.PlaceBid("keep", 4, 26_000_000L, out _);

            var before = rig.Market.GetBids("keep");
            var day = rig.Market.GetDecisionDay("keep");
            var records = rig.Market.ToSave();
            Assert(records.Count >= 1, "Bids are written to save records");

            var reload = new FreeAgencyMarket(rig.Fam, rig.Cap, rig.Db,
                () => rig.Teams, () => rig.PlayerTeamId, 7);
            reload.Restore(records, Day1);

            var after = reload.GetBids("keep");
            AssertEqual(before.Count, after.Count, "Every bid survives the round trip");
            AssertEqual(before[0].TeamId, after[0].TeamId, "The leading bidder survives");
            AssertEqual(before[0].AnnualAverage, after[0].AnnualAverage, "Bid salary survives");
            AssertEqual(before[0].Years, after[0].Years, "Bid length survives");
            AssertEqual(day, reload.GetDecisionDay("keep"), "The decision day survives");

            // Offer sheets round-trip too
            var sheetRig = RigWithSheet(22_000_000L, 100_000_000L, playerTeam: "ORIG");
            var sheetRecords = sheetRig.Market.ToSave();
            var sheetReload = new FreeAgencyMarket(sheetRig.Fam, sheetRig.Cap, sheetRig.Db,
                () => sheetRig.Teams, () => "ORIG", 7);
            sheetReload.Restore(sheetRecords, Day1);
            AssertEqual(1, sheetReload.PendingMatchDecisions.Count, "A pending offer sheet survives the save");

            // Legacy saves: no market section at all
            var legacy = new FreeAgencyMarket(rig.Fam, rig.Cap, rig.Db,
                () => rig.Teams, () => rig.PlayerTeamId, 7);
            legacy.Restore(null, Day1);
            AssertEqual(0, legacy.GetBids("keep").Count, "A save without market data loads an empty market");
            AssertEqual(0, legacy.PendingMatchDecisions.Count, "No offer sheets from a legacy save");
            Assert(legacy.GetDecisionDay("keep") == null, "No decision days from a legacy save");
        }

        private void TestExceptionUsageRoundTrip()
        {
            var rig = new Rig();
            rig.AddTeam("TAX", payroll: 150_000_000L);   // over the cap, non-taxpayer MLE
            rig.AddFreeAgent("mle1", 75);

            bool signed = rig.Fam.ExecuteSigning("TAX", "mle1", new SigningOffer
            {
                AnnualSalary = 8_000_000L, Years = 2,
                Method = SigningMethod.MidLevelException, PlayerYearsExperience = 8
            });
            Assert(signed, "MLE signing executes");
            AssertEqual(8_000_000L, rig.Fam.GetUsage("TAX").MLEUsed, "MLE usage is debited");

            var records = rig.Fam.GetAllUsage()
                .Select(kv => new NBAHeadCoach.Core.Data.ExceptionUsageRecord
                {
                    TeamId = kv.Key, MLEUsed = kv.Value.MLEUsed,
                    BiAnnualUsed = kv.Value.BiAnnualUsed, TwoWayCount = kv.Value.TwoWayCount
                }).ToList();
            Assert(records.Any(r => r.TeamId == "TAX"), "Exception usage is written to save records");

            rig.Fam.Clear();
            AssertEqual(0L, rig.Fam.GetUsage("TAX").MLEUsed, "Clear zeroes exception usage before a restore");

            foreach (var r in records)
                rig.Fam.RestoreUsage(r.TeamId, r.MLEUsed, r.BiAnnualUsed, r.TwoWayCount);
            AssertEqual(8_000_000L, rig.Fam.GetUsage("TAX").MLEUsed, "MLE usage survives the round trip");

            // Legacy path: nothing restored, nothing thrown
            var fresh = new FreeAgentManager(new SalaryCapManager(), new PlayerDatabase());
            AssertEqual(0L, fresh.GetUsage("ANY").MLEUsed, "A pre-O2 save loads with untouched exceptions");
        }

        // ==================== POOL DRAIN ====================

        private void TestPoolStillDrainsWithoutSuitors()
        {
            var rig = new Rig();
            rig.AddTeam("TIGHT1", payroll: 138_000_000L);   // no room for a max-value FA
            rig.AddTeam("TIGHT2", payroll: 138_000_000L);
            var star = rig.AddFreeAgent("unsignable", 82);  // $22M ask nobody can pay
            var depth = rig.AddFreeAgent("depth", 60);      // minimum-tier ask

            rig.RunDays(6, Day1);

            AssertEqual(0, rig.Market.GetBids("unsignable").Count,
                "Nobody bids on a free agent no team can afford");
            Assert(string.IsNullOrEmpty(star.TeamId), "The unaffordable free agent stays on the market");
            Assert(!string.IsNullOrEmpty(depth.TeamId),
                "An unaffordable star never halts the rest of the market");
        }

        // ==================== DEADLINE BOUNDARIES ====================

        private void TestMatchDeadlineDayIsUsable()
        {
            // Sheet says "match by Day1+1" — that whole day belongs to the player
            var rig = RigWithSheet(22_000_000L, 100_000_000L, playerTeam: "ORIG");
            rig.Market.RunDay(Day1.AddDays(1));
            AssertEqual(1, rig.Market.PendingMatchDecisions.Count,
                "A sheet due today is still the player's call today");
            Assert(string.IsNullOrEmpty(rig.Db.GetPlayer("rfa").TeamId),
                "Nothing auto-resolves on the stated deadline day");

            rig.Market.RunDay(Day1.AddDays(2));
            AssertEqual(0, rig.Market.PendingMatchDecisions.Count,
                "The day after the deadline it auto-resolves");
        }

        // ==================== MATCHING AT EXACT TERMS ====================

        private void TestMatchKeepsSheetYears()
        {
            // 4-year sheet, matchable: the match is 4 years too, never trimmed to 1
            var rig = RigWithSheet(22_000_000L, 100_000_000L);
            rig.Market.RunDay(Day1.AddDays(2));
            AssertEqual("ORIG", rig.Db.GetPlayer("rfa").TeamId, "The sheet is matched");
            AssertEqual(4, rig.Cap.GetContract("rfa")?.YearsRemaining ?? 0,
                "Matching keeps the offer sheet's length");

            // No Bird rights and no room: the match can't be made at exact terms
            var stuck = RigWithSheet(22_000_000L, 165_000_000L, playerTeam: "ORIG",
                consecutiveSeasons: 0);
            Assert(!stuck.Market.ResolveMatchDecision("rfa", true, out string why),
                "A team that can't afford the exact terms can't match");
            Assert(!string.IsNullOrEmpty(why), "The failed match explains itself");
            AssertEqual(1, stuck.Market.PendingMatchDecisions.Count,
                "A failed match leaves the sheet standing");

            stuck.Market.RunDay(Day1.AddDays(2));
            AssertEqual("BID", stuck.Db.GetPlayer("rfa").TeamId,
                "He walks on the sheet once the deadline passes");
        }

        // ==================== SCRAP HEAP PRICING ====================

        private void TestScrapHeapPricing()
        {
            var rich = OffseasonManager.ScrapHeapOffer(40_000_000L, 12_000_000L, 5, 2);
            AssertEqual(12_000_000L, rich.AnnualSalary,
                "A team with room pays a depth free agent his market value");
            AssertEqual(SigningMethod.CapSpace, rich.Method, "Real money goes through cap space");

            var tight = OffseasonManager.ScrapHeapOffer(4_000_000L, 12_000_000L, 5, 2);
            AssertEqual(4_000_000L, tight.AnnualSalary, "The offer is trimmed to the room available");

            var broke = OffseasonManager.ScrapHeapOffer(0L, 12_000_000L, 5, 2);
            AssertEqual(LeagueCBA.GetMinimumSalary(5), broke.AnnualSalary,
                "A team without room offers the minimum");
            AssertEqual(SigningMethod.MinimumSalary, broke.Method, "...via the minimum exception");

            var floor = OffseasonManager.ScrapHeapOffer(40_000_000L, 1_000_000L, 9, 1);
            AssertEqual(LeagueCBA.GetMinimumSalary(9), floor.AnnualSalary,
                "No depth deal lands below the league minimum for his service");
        }

        // ==================== REAL PIPELINE (OffseasonManager) ====================

        /// <summary>
        /// Headless offseason rig: a real GameManager on an INACTIVE GameObject (Awake
        /// never fires, so no scene load and no UI) with the offseason engine forced to
        /// an open July market. Enough to drive OffseasonManager.DailyTick, which is the
        /// only way to exercise RunDailyFreeAgency / the scrap heap / the QO stage.
        /// </summary>
        private class OffRig
        {
            public readonly GameManager Gm;
            public readonly OffseasonManager Off;
            public readonly List<Team> Teams = new List<Team>();
            private readonly GameManager _previous;

            public OffRig(int seasonLabel, int teamCount = 4)
            {
                var go = new GameObject("__FaMarketRig__");
                go.SetActive(false);
                Gm = go.AddComponent<GameManager>();
                try
                {
                    typeof(GameManager)
                        .GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(Gm, null);
                }
                catch (Exception)
                {
                    // Initialize's last line starts the data-load coroutine, which an
                    // inactive GameObject refuses. Every manager is already constructed.
                }

                _previous = GameManager.Instance;
                typeof(GameManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    .GetSetMethod(nonPublic: true).Invoke(null, new object[] { Gm });

                var byId = new Dictionary<string, Team>();
                for (int i = 0; i < teamCount; i++)
                {
                    var team = new Team
                    {
                        TeamId = $"T{i}", City = $"City{i}", Nickname = $"Squad{i}",
                        Abbreviation = $"T{i}", Wins = 41, Losses = 41
                    };
                    Teams.Add(team);
                    byId[team.TeamId] = team;
                }
                Set(Gm, "_allTeams", Teams);
                Set(Gm, "_teamsById", byId);
                Set(Gm, "_playerTeamId", "T0");
                Set(Gm, "_currentSeason", seasonLabel);

                Off = Gm.Offseason;
                Set(Off, "_engineActive", true);
                Set(Off, "_seasonLabel", seasonLabel);
                Set(Off, "_calendarYear", seasonLabel + 1);
                Set(Off, "_postSeasonDone", true);
                Set(Off, "_draftDone", true);
                Set(Off, "_freeAgencyOpen", true);
                Set(Off, "_summerDone", true);
                Set(Off, "_campStarted", true);      // no camp kickoff noise on October ticks
            }

            public Player AddFreeAgent(string id, int rating,
                FreeAgentType type = FreeAgentType.Unrestricted, string previousTeamId = null,
                int consecutiveSeasons = 0)
            {
                var p = new Player
                {
                    PlayerId = id, FirstName = "Free", LastName = id,
                    Position = Position.PointGuard,
                    BirthDate = new DateTime(1998, 1, 1),
                    DraftYear = 2018,
                    BallHandling = rating, Passing = rating, Shot_Three = rating,
                    Speed = rating, Defense_Perimeter = rating, BasketballIQ = rating,
                    Energy = 100, Morale = 75
                };
                Gm.PlayerDatabase.AddPlayer(p);
                Gm.FreeAgents.AddFreeAgent(id, type, previousTeamId, consecutiveSeasons);
                return p;
            }

            public void Tick(DateTime date)
            {
                Set(Gm, "_currentDate", date);
                Off.DailyTick(new DailyTickContext(date, Gm.PlayerTeamId, Gm));
            }

            public void Dispose()
            {
                typeof(GameManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    .GetSetMethod(nonPublic: true).Invoke(null, new object[] { _previous });
                if (Gm != null) UnityEngine.Object.DestroyImmediate(Gm.gameObject);
            }

            private static void Set(object target, string field, object value) =>
                target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(target, value);
        }

        private void TestOwnContestedFreeAgentSignsInstantly()
        {
            var rig = new OffRig(2026);
            try
            {
                var mine = rig.AddFreeAgent("ours", 82, FreeAgentType.Unrestricted, "T0", 3);
                rig.Tick(new DateTime(2027, 7, 7));      // builds the market

                // A rival bid on him, seeded so the state is deterministic
                rig.Off.Market.Restore(new List<NBAHeadCoach.Core.Data.MarketBidRecord>
                {
                    Bid("ours", "T1", 3, 20_000_000L, new DateTime(2027, 7, 11))
                }, new DateTime(2027, 7, 7));
                Assert(rig.Off.Market.IsContested("ours", "T0"),
                    "A rival bid marks your own free agent contested");

                bool signed = rig.Off.FinalizeNegotiatedSigning(rig.Gm, "ours", 3, 24_000_000L,
                    out string why);
                Assert(signed, $"Agreeing terms with your OWN free agent signs him ({why})");
                AssertEqual("T0", mine.TeamId, "He's on your roster, not out to bid");
                Assert(rig.Teams[0].RosterPlayerIds.Contains("ours"), "The roster list has him");
                AssertEqual(0, rig.Off.Market.GetBids("ours").Count,
                    "His market entry is gone — no ghost bids");
                Assert(rig.Off.Market.GetDecisionDay("ours") == null,
                    "And no decision day left on the clock");
            }
            finally { rig.Dispose(); }
        }

        /// <summary>
        /// Drives the REAL RunDailyFreeAgency: a tendered restricted free agent outside
        /// the marketed tier is never scrap-heap signed away, and the scrap heap pays
        /// real money when the signing team has room.
        /// </summary>
        private void TestRestrictedFreeAgentKeepsFirstRefusal()
        {
            var rig = new OffRig(2026);
            try
            {
                // 24 unrestricted free agents: 15 get marketed, the rest are scrap heap.
                // Deep enough that the marketed tier never has to reach down to the RFA.
                for (int i = 0; i < 24; i++) rig.AddFreeAgent($"fa{i:00}", 75);
                var rfa = rig.AddFreeAgent("rfa", 60, FreeAgentType.Restricted, "T1", 3);
                Assert(rig.Gm.FreeAgents.ExtendQualifyingOffer("T1", "rfa", 6_000_000L),
                    "His original team tenders the qualifying offer");
                long qo = rig.Gm.FreeAgents.GetFreeAgents()
                    .First(f => f.PlayerId == "rfa").QualifyingOfferAmount;

                rig.Tick(new DateTime(2027, 7, 7));
                rig.Tick(new DateTime(2027, 7, 8));

                Assert(string.IsNullOrEmpty(rfa.TeamId),
                    "The scrap heap never min-signs a restricted free agent out from under first refusal");

                var scrapSigned = new[] { "fa15", "fa16", "fa17" }
                    .Select(id => rig.Gm.SalaryCapManager.GetContract(id))
                    .Where(c => c != null).ToList();
                Assert(scrapSigned.Count > 0, "Non-marketed free agents still come off the board");
                Assert(scrapSigned.Any(c => c.CurrentYearSalary > LeagueCBA.GetMinimumSalary(9)),
                    "A team with cap room pays a valuable depth free agent above the minimum");

                // October: nobody signed him, so he takes the qualifying offer
                rig.Tick(new DateTime(2027, 10, 1));
                AssertEqual("T1", rfa.TeamId, "An unsigned tendered RFA takes his QO with his old team");
                var deal = rig.Gm.SalaryCapManager.GetContract("rfa");
                AssertEqual(qo, deal?.CurrentYearSalary ?? 0L, "At the qualifying-offer amount");
                AssertEqual(1, deal?.YearsRemaining ?? 0, "For one year");
            }
            finally { rig.Dispose(); }
        }
    }
}
