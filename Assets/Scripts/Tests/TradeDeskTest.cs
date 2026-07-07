using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the Phase 3 trade desk: agreed-trade finalization with roster sync
    /// and lineup repair, incoming-offer acceptance and persistence, market date
    /// gating, and the AI-to-AI league trade builder.
    /// </summary>
    public class TradeDeskTest : BaseTest
    {
        private PlayerDatabase _db;
        private SalaryCapManager _cap;
        private TradeSystem _trades;
        private Dictionary<string, Team> _teams;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestAgreedTradeExecutesAndSyncsRosters();
            TestAgreedTradeRejectsRosterViolation();
            TestIncomingOfferAcceptance();
            TestOfferPersistenceAndHusks();
            TestMarketDateGating();
            TestAiToAiLeagueTrade();

            return (_passed, _failed);
        }

        // ==================== FIXTURE ====================

        private void ResetLeague()
        {
            _db = new PlayerDatabase();
            _cap = new SalaryCapManager();
            _trades = new TradeSystem(_cap, _db);
            _teams = new Dictionary<string, Team>();

            // What GameManager.OnTradeExecutedHandler does for the live game.
            _trades.OnTradeExecuted += p => TradeDeskSystem.ApplyRosterSync(p, LookupTeam);
        }

        private Team LookupTeam(string id) => _teams.TryGetValue(id, out var t) ? t : null;

        private void AddPlayer(string pid, string teamId, int rating, int age)
        {
            _db.AddPlayer(new Player
            {
                PlayerId = pid,
                FirstName = "T",
                LastName = pid,
                TeamId = teamId,
                Position = Position.SmallForward,
                BirthDate = DateTime.Now.AddYears(-age),
                Shot_Three = rating, Finishing_Rim = rating, Defense_Perimeter = rating,
                Defense_Interior = rating, Speed = rating, Vertical = rating, Strength = rating,
                Energy = 100, Morale = 75
            });
        }

        private Team BuildTeam(string teamId, int count, long salaryEach, int rating, int age)
        {
            var team = new Team { TeamId = teamId, City = teamId, Nickname = teamId };
            for (int i = 0; i < count; i++)
            {
                string pid = $"{teamId.ToLower()}{i}";
                AddPlayer(pid, teamId, rating, age);
                _cap.RegisterContract(new Contract
                {
                    PlayerId = pid, TeamId = teamId,
                    YearsRemaining = 2, CurrentYearSalary = salaryEach
                });
                team.RosterPlayerIds.Add(pid);
            }
            for (int i = 0; i < 5 && i < count; i++)
                team.StartingLineupIds[i] = team.RosterPlayerIds[i];
            _teams[teamId] = team;
            return team;
        }

        private TradeProposal SwapProposal(string playerA, string teamA, string playerB, string teamB)
        {
            var proposal = new TradeProposal { ProposedDate = new DateTime(2027, 1, 15) };
            proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.Player, PlayerId = playerA,
                Salary = _cap.GetContract(playerA)?.CurrentYearSalary ?? 0,
                SendingTeamId = teamA, ReceivingTeamId = teamB
            });
            proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.Player, PlayerId = playerB,
                Salary = _cap.GetContract(playerB)?.CurrentYearSalary ?? 0,
                SendingTeamId = teamB, ReceivingTeamId = teamA
            });
            return proposal;
        }

        // ==================== TESTS ====================

        private void TestAgreedTradeExecutesAndSyncsRosters()
        {
            ResetLeague();
            var a = BuildTeam("AAA", 13, 8_000_000L, 75, 27);
            var b = BuildTeam("BBB", 13, 8_000_000L, 74, 28);

            // aaa0 is a starter — the lineup must repair after the trade.
            var result = _trades.FinalizeAgreedTrade(SwapProposal("aaa0", "AAA", "bbb7", "BBB"));

            AssertEqual(TradeStatus.Completed, result.Status, "Agreed swap completes");
            AssertEqual("BBB", _cap.GetContract("aaa0").TeamId, "Contract follows the player out");
            AssertEqual("AAA", _cap.GetContract("bbb7").TeamId, "Incoming contract lands");
            AssertEqual("BBB", _db.GetPlayer("aaa0").TeamId, "PlayerDatabase team updated");

            Assert(!a.RosterPlayerIds.Contains("aaa0"), "Traded player leaves the sending roster");
            Assert(a.RosterPlayerIds.Contains("bbb7"), "Incoming player joins the receiving roster");
            AssertEqual(13, a.RosterPlayerIds.Count, "Sender roster count preserved");
            AssertEqual(13, b.RosterPlayerIds.Count, "Receiver roster count preserved");

            Assert(a.StartingLineupIds.All(id =>
                string.IsNullOrEmpty(id) || a.RosterPlayerIds.Contains(id)),
                "Broken starting lineup repaired after trade");
        }

        private void TestAgreedTradeRejectsRosterViolation()
        {
            ResetLeague();
            BuildTeam("AAA", 12, 8_000_000L, 75, 27);
            BuildTeam("BBB", 13, 8_000_000L, 74, 28);

            // One-way deal drops AAA to 11 players — below the league minimum.
            var proposal = new TradeProposal { ProposedDate = new DateTime(2027, 1, 15) };
            proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.Player, PlayerId = "aaa3", Salary = 8_000_000L,
                SendingTeamId = "AAA", ReceivingTeamId = "BBB"
            });

            var result = _trades.FinalizeAgreedTrade(proposal);

            AssertEqual(TradeStatus.Invalid, result.Status, "Roster-minimum violation blocks the deal");
            AssertEqual("AAA", _cap.GetContract("aaa3").TeamId, "Blocked deal moves nothing");
            AssertEqual(12, _teams["AAA"].RosterPlayerIds.Count, "Roster untouched by blocked deal");
        }

        private TradeDeskSystem BuildDesk(AITradeOfferGenerator offerGen, AITradeEvaluator evaluator)
        {
            return new TradeDeskSystem(_db, _cap, _trades, evaluator, offerGen, null,
                LookupTeam, () => _teams.Values.ToList());
        }

        private void TestIncomingOfferAcceptance()
        {
            ResetLeague();
            var plr = BuildTeam("PLR", 13, 8_000_000L, 78, 26);
            var off = BuildTeam("OFF", 13, 8_000_000L, 73, 29);

            var evaluator = new AITradeEvaluator(_cap, _db);
            var offerGen = new AITradeOfferGenerator(_db, _cap, evaluator.ValueCalculator);
            var desk = BuildDesk(offerGen, evaluator);

            var offer = new IncomingTradeOffer
            {
                OfferId = "offer-1",
                OfferingTeamId = "OFF",
                Proposal = SwapProposal("plr2", "PLR", "off9", "OFF"),
                OfferMessage = "We want plr2.",
                ReceivedAtStr = new DateTime(2027, 1, 10).ToString("o"),
                ExpiresAtStr = new DateTime(2027, 1, 20).ToString("o"),
                Status = IncomingOfferStatus.Pending
            };
            offerGen.RestoreFromSave(new IncomingOffersSaveData
            {
                Offers = new List<IncomingTradeOffer> { offer }
            });

            AssertEqual(1, offerGen.GetPendingOffers().Count, "Injected offer is pending");

            var result = desk.AcceptIncomingOffer("offer-1", new DateTime(2027, 1, 15));

            AssertEqual(TradeStatus.Completed, result.Status, "Accepted offer executes");
            AssertEqual(IncomingOfferStatus.Accepted, offer.Status, "Offer marked accepted");
            Assert(plr.RosterPlayerIds.Contains("off9") && !plr.RosterPlayerIds.Contains("plr2"),
                "Accepted offer moves both players");
            AssertEqual(0, offerGen.GetPendingOffers().Count, "No pending offers remain");

            var again = desk.AcceptIncomingOffer("offer-1", new DateTime(2027, 1, 16));
            AssertEqual(TradeStatus.Invalid, again.Status, "Second acceptance is refused");
        }

        private void TestOfferPersistenceAndHusks()
        {
            ResetLeague();
            var evaluator = new AITradeEvaluator(_cap, _db);
            var offerGen = new AITradeOfferGenerator(_db, _cap, evaluator.ValueCalculator);

            // Husk: a pre-1.1 save lost the proposal — must self-expire, not surface.
            var husk = new IncomingTradeOffer
            {
                OfferId = "husk-1", OfferingTeamId = "OFF",
                Proposal = null, Status = IncomingOfferStatus.Pending
            };
            var dated = new IncomingTradeOffer
            {
                OfferId = "dated-1", OfferingTeamId = "OFF",
                Proposal = new TradeProposal(), Status = IncomingOfferStatus.Rejected,
                ReceivedAtStr = new DateTime(2027, 1, 4).ToString("o"),
                ExpiresAtStr = new DateTime(2027, 1, 9).ToString("o")
            };
            offerGen.RestoreFromSave(new IncomingOffersSaveData
            {
                Offers = new List<IncomingTradeOffer> { husk, dated }
            });

            AssertEqual(IncomingOfferStatus.Expired, husk.Status, "Proposal-less offer expires on load");
            AssertEqual(new DateTime(2027, 1, 4), dated.ReceivedAt, "ReceivedAt round-trips via ISO string");
            AssertEqual(new DateTime(2027, 1, 9), dated.ExpiresAt, "ExpiresAt round-trips via ISO string");

            var saved = offerGen.CreateSaveData();
            Assert(saved.Offers.All(o => !string.IsNullOrEmpty(o.ExpiresAtStr)),
                "Save stamps ISO date strings on every offer");
        }

        private void TestMarketDateGating()
        {
            ResetLeague();
            var evaluator = new AITradeEvaluator(_cap, _db);
            var offerGen = new AITradeOfferGenerator(_db, _cap, evaluator.ValueCalculator);
            var desk = BuildDesk(offerGen, evaluator);

            AssertEqual(0, desk.RunLeagueTradeDay(new DateTime(2027, 2, 7), "PLR"),
                "No league trades the day after the deadline");
            AssertEqual(0, desk.RunLeagueTradeDay(new DateTime(2027, 4, 1), "PLR"),
                "No league trades in the stretch run");
            AssertEqual(0, desk.RunLeagueTradeDay(new DateTime(2026, 8, 15), "PLR"),
                "No league trades in August");
            AssertEqual(0, desk.RunLeagueTradeDay(new DateTime(2026, 10, 20), "PLR"),
                "No league trades before opening week");
        }

        private void TestAiToAiLeagueTrade()
        {
            ResetLeague();

            // Seller: rebuilding, one movable veteran on real money among cheap kids.
            var sel = BuildTeam("SEL", 14, 2_500_000L, 70, 23);
            AddPlayer("selvet", "SEL", 76, 31);
            _cap.RegisterContract(new Contract
            {
                PlayerId = "selvet", TeamId = "SEL",
                YearsRemaining = 2, CurrentYearSalary = 18_000_000L
            });
            sel.RosterPlayerIds.Add("selvet");

            // Buyer: contender with a matching mid-tier salary to send back.
            var buy = BuildTeam("BUY", 13, 6_000_000L, 72, 27);
            AddPlayer("buymid", "BUY", 74, 27);
            _cap.RegisterContract(new Contract
            {
                PlayerId = "buymid", TeamId = "BUY",
                YearsRemaining = 3, CurrentYearSalary = 15_000_000L
            });
            buy.RosterPlayerIds.Add("buymid");

            var evaluator = new AITradeEvaluator(_cap, _db);
            var offerGen = new AITradeOfferGenerator(_db, _cap, evaluator.ValueCalculator);

            var sellerFo = FrontOfficeProfile.CreateAverage("SEL", "Seller GM");
            sellerFo.CurrentSituation = TeamSituation.Rebuilding;
            sellerFo.ValuePreferences = TradeValuePreferences.ForSituation(TeamSituation.Rebuilding);
            var buyerFo = FrontOfficeProfile.CreateAverage("BUY", "Buyer GM");
            buyerFo.CurrentSituation = TeamSituation.Contending;
            buyerFo.ValuePreferences = TradeValuePreferences.ForSituation(TeamSituation.Contending);

            foreach (var fo in new[] { sellerFo, buyerFo })
            {
                offerGen.RegisterFrontOffice(fo);
                evaluator.RegisterFrontOffice(fo);
            }

            var desk = BuildDesk(offerGen, evaluator);

            UnityEngine.Random.InitState(424242);
            bool executed = false;
            for (int i = 0; i < 300 && !executed; i++)
            {
                executed = desk.TryExecuteLeagueTrade(new DateTime(2027, 1, 20), "PLR");
            }

            Assert(executed, "AI-to-AI league trade executes within 300 attempts");

            if (executed)
            {
                Assert(!sel.RosterPlayerIds.Contains("selvet"),
                    "Seller moved the veteran");
                Assert(buy.RosterPlayerIds.Contains("selvet"),
                    "Buyer received the veteran");
                Assert(sel.RosterPlayerIds.Count >= 12 && sel.RosterPlayerIds.Count <= 15,
                    "Seller roster stays within league limits");
                Assert(buy.RosterPlayerIds.Count >= 12 && buy.RosterPlayerIds.Count <= 15,
                    "Buyer roster stays within league limits");

                // Contract registry and rosters agree on who plays where.
                foreach (var team in new[] { sel, buy })
                {
                    Assert(team.RosterPlayerIds.All(id => _cap.GetContract(id)?.TeamId == team.TeamId),
                        $"{team.TeamId} contracts match roster after trade");
                }
            }
        }
    }
}
