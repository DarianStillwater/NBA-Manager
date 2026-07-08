using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 7-B commit 3 (coach-only playable): the AI GM installs
    /// deterministically per profile, processes coach requests into verdicts with
    /// reasoning, accumulates a discoverable relationship, and survives a save.
    /// </summary>
    public class CoachOnlyModeTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(20260709);

            TestInitializeIsDeterministicPerProfile();
            TestRequestsGetVerdictsAndHistory();
            TestSaveRoundTrip();

            return (_passed, _failed);
        }

        private void TestInitializeIsDeterministicPerProfile()
        {
            var gm = AIGMController.Instance;
            gm.Initialize("AIGM_7", "Bob Myers", "GSW");

            Assert(gm.IsInitializedFor("GSW"), "Controller reports the team it runs");
            Assert(!gm.IsInitializedFor("LAL"), "Other teams are not claimed");
            AssertEqual("Bob Myers", gm.GMName, "GM name is carried");

            var p1 = gm.GetSaveData().Personality;
            gm.Initialize("AIGM_7", "Bob Myers", "GSW");
            var p2 = gm.GetSaveData().Personality;
            Assert(p1.TradeHappy == p2.TradeHappy && p1.ProtectsStar == p2.ProtectsStar &&
                   p1.CostConscious == p2.CostConscious && p1.ValuesDraftPicks == p2.ValuesDraftPicks,
                "Same profile id regenerates the same hidden personality");
        }

        private void TestRequestsGetVerdictsAndHistory()
        {
            var gm = AIGMController.Instance;
            gm.Initialize("AIGM_TEST", "Test GM", "BOS", new System.Random(42));

            int approved = 0, denied = 0;
            for (int i = 0; i < 20; i++)
            {
                var req = RosterRequest.CreateSigningRequest($"P{i}", $"Player {i}",
                    "We need depth at the wing.");
                var result = gm.ProcessRequest(req);

                if (i == 0)
                {
                    Assert(result != null, "A request always gets a verdict");
                    Assert(!string.IsNullOrEmpty(result.GMResponse), "The verdict comes with reasoning");
                    AssertEqual(result.IsApproved ? RequestStatus.Approved : RequestStatus.Denied,
                        req.Status, "The request status reflects the verdict");
                }
                if (result.IsApproved) approved++; else denied++;
            }

            Assert(approved > 0 && denied > 0,
                $"Over 20 asks the GM both approves and denies ({approved}/{denied})");

            var save = gm.GetSaveData();
            AssertEqual(20, save.TotalRequests, "History counts every request");
            AssertEqual(approved, save.ApprovedRequests, "Approvals tallied");

            string desc = gm.GetKnownPersonalityDescription();
            Assert(!string.IsNullOrEmpty(desc), "Relationship description renders");
        }

        private void TestSaveRoundTrip()
        {
            var gm = AIGMController.Instance;
            gm.Initialize("AIGM_SAVE", "Save GM", "MIA", new System.Random(7));
            gm.ProcessRequest(RosterRequest.CreateWaiveRequest("P1", "End Bench", "Open a spot."));

            var data = gm.GetSaveData();
            var json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<AIGMSaveData>(json);

            AssertEqual("MIA", back.TeamId, "Team id survives JSON");
            AssertEqual("Save GM", back.GmName, "GM name survives JSON");
            Assert(back.Personality != null, "Hidden personality survives JSON");
            AssertEqual(data.TotalRequests, back.TotalRequests, "Request history counts survive");

            gm.Initialize("OTHER", "Other GM", "NYK");
            gm.LoadSaveData(back);
            Assert(gm.IsInitializedFor("MIA"), "LoadSaveData restores the controller's team");
            AssertEqual("Save GM", gm.GMName, "LoadSaveData restores the GM identity");
        }
    }
}
