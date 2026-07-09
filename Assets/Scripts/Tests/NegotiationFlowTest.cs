using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using ContractOffer = NBAHeadCoach.Core.Manager.ContractOffer;
using AgentManager = NBAHeadCoach.Core.Manager.AgentManager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 7-D commit 3: contract talks are genuinely multi-round —
    /// generous offers get accepted, lowballs draw counters that trend downward,
    /// endless stonewalling ends in a walkaway, agents attach automatically, and
    /// in-flight sessions survive a save.
    /// </summary>
    public class NegotiationFlowTest : BaseTest
    {
        private const long MARKET = 20_000_000L;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(20260706);

            TestGenerousOfferAccepted();
            TestLowballDrawsCounters();
            TestStonewallEndsInWalkaway();
            TestSaveRoundTrip();

            return (_passed, _failed);
        }

        private ContractOffer MakeOffer(string playerId, string teamId, int years, long salary)
        {
            return new ContractOffer
            {
                OfferId = Guid.NewGuid().ToString(),
                OfferingTeamId = teamId,
                PlayerId = playerId,
                Years = years,
                AnnualSalary = salary,
                TotalValue = years * salary,
                IncentiveDescriptions = new List<string>(),
                OfferDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddDays(7)
            };
        }

        private void TestGenerousOfferAccepted()
        {
            var agents = new AgentManager();
            var neg = new ContractNegotiationManager(agents);

            var session = neg.StartNegotiation("P1", "Test Star", "BOS", MARKET);
            Assert(session != null && session.Status == NegotiationStatus.InProgress,
                "Talks open with an in-progress session");
            Assert(!string.IsNullOrEmpty(session.AgentId), "An agent attaches automatically");
            Assert(session.PlayerAskingPrice > MARKET, "The agent opens above market");

            // Offer the full asking price — no agent turns that down for long
            var response = neg.SubmitOffer(session.NegotiationId,
                MakeOffer("P1", "BOS", 3, session.PlayerAskingPrice + 1_000_000L));
            AssertEqual(NegotiationStatus.Accepted, response.ResultingStatus,
                "Beating the ask gets a handshake");
            Assert(!string.IsNullOrEmpty(response.Message), "Acceptance comes with words");
        }

        private void TestLowballDrawsCounters()
        {
            var agents = new AgentManager();
            var neg = new ContractNegotiationManager(agents);
            var session = neg.StartNegotiation("P2", "Role Player", "MIA", MARKET);

            var response = neg.SubmitOffer(session.NegotiationId,
                MakeOffer("P2", "MIA", 2, MARKET / 2));
            Assert(response.ResultingStatus == NegotiationStatus.CounterOfferReceived ||
                   response.ResultingStatus == NegotiationStatus.Rejected ||
                   response.ResultingStatus == NegotiationStatus.WalkedAway,
                $"A 50% lowball never gets accepted ({response.ResultingStatus})");

            if (response.ResultingStatus == NegotiationStatus.CounterOfferReceived)
            {
                var counter = response.CounterOffer ?? session.CurrentAgentCounter;
                Assert(counter != null && counter.AnnualSalary > MARKET / 2,
                    "The counter asks for more than the lowball");
                Assert(counter.AnnualSalary <= session.PlayerAskingPrice,
                    "Counters trend down from the opening ask");
            }
        }

        private void TestStonewallEndsInWalkaway()
        {
            var agents = new AgentManager();
            var neg = new ContractNegotiationManager(agents);
            var session = neg.StartNegotiation("P3", "Stubborn Vet", "NYK", MARKET);

            NegotiationStatus last = NegotiationStatus.InProgress;
            for (int round = 0; round < 12; round++)
            {
                var r = neg.SubmitOffer(session.NegotiationId,
                    MakeOffer("P3", "NYK", 1, 2_000_000L));
                last = r.ResultingStatus;
                if (last == NegotiationStatus.WalkedAway || last == NegotiationStatus.Rejected ||
                    last == NegotiationStatus.Expired) break;
            }

            Assert(last == NegotiationStatus.WalkedAway || last == NegotiationStatus.Rejected,
                $"Endless insulting offers end the talks ({last})");
            Assert(!neg.GetNegotiation(session.NegotiationId)?.CanContinue() ?? true,
                "A dead negotiation can't continue");
        }

        private void TestSaveRoundTrip()
        {
            var agents = new AgentManager();
            agents.AssignAgentToPlayer("P4", 80);
            var neg = new ContractNegotiationManager(agents);
            var session = neg.StartNegotiation("P4", "Save Case", "DAL", MARKET);
            neg.SubmitOffer(session.NegotiationId, MakeOffer("P4", "DAL", 2, MARKET / 2));

            var data = new SaveData();
            agents.WriteSave(data);
            neg.WriteSave(data);
            var parsed = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            var agents2 = new AgentManager();
            agents2.ReadSave(parsed, new SaveReadContext(SaveData.CURRENT_VERSION, false, 2026));
            Assert(agents2.GetAgentForPlayer("P4") != null &&
                   agents2.GetAgentForPlayer("P4").AgentId == agents.GetAgentForPlayer("P4").AgentId,
                "Agent assignments survive a save");

            var neg2 = new ContractNegotiationManager(agents2);
            neg2.ReadSave(parsed, new SaveReadContext(SaveData.CURRENT_VERSION, false, 2026));
            var restored = neg2.GetNegotiation(session.NegotiationId);
            if (neg.GetNegotiation(session.NegotiationId)?.CanContinue() == true)
            {
                Assert(restored != null, "In-flight talks survive a save");
                AssertEqual("Save Case", restored?.PlayerName, "Session identity survives");
                Assert(restored.CurrentRound >= 1, "Round count survives");
            }
            else
            {
                Assert(restored == null, "Concluded talks aren't resurrected");
            }
        }
    }
}
