using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Components;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the substitution walk: the reserve and the starter trade places (on-court/bench
    /// membership swaps, bench size stays constant), and once the walk is finalized the incoming
    /// player stands where the outgoing player was — i.e. he walked onto the floor, not popped in.
    /// </summary>
    public class SubstitutionWalkTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            TestSubSwapsSeats();
            return (_passed, _failed);
        }

        private static Team MakeTeam(string id)
        {
            var t = new Team { TeamId = id, Name = id, Abbreviation = id };
            t.RosterPlayerIds = Enumerable.Range(0, 12).Select(i => $"{id}_p{i}").ToList();
            for (int i = 0; i < 5; i++) t.StartingLineupIds[i] = t.RosterPlayerIds[i];
            return t;
        }

        private void TestSubSwapsSeats()
        {
            var home = MakeTeam("HOM");
            var away = MakeTeam("AWY");
            var homeLineup = home.StartingLineupIds.ToList();
            var awayLineup = away.StartingLineupIds.ToList();

            var go = new GameObject("__CourtViewSubTest__", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(940f, 500f);
            var view = go.AddComponent<MatchCourtView>();

            try
            {
                view.Setup(null, null, new Color(0.2f, 0.4f, 0.8f), new Color(0.8f, 0.2f, 0.2f));
                view.Initialize(home, away, homeLineup, awayLineup);

                string outId = "HOM_p2";   // a starter
                string inId = "HOM_p7";     // a reserve

                Assert(view.IsOnCourt(outId) && !view.IsOnBench(outId), "Starter begins on the court");
                Assert(view.IsOnBench(inId) && !view.IsOnCourt(inId), "Reserve begins on the bench");
                view.TryGetActorPosition(outId, out var starterCourtPos);
                int benchBefore = view.BenchCount(true);

                view.AnimateSubstitution(outId, inId, isHome: true);

                Assert(view.IsOnCourt(inId) && !view.IsOnBench(inId), "Reserve is now on the court");
                Assert(view.IsOnBench(outId) && !view.IsOnCourt(outId), "Starter is now on the bench");
                Assert(view.BenchCount(true) == benchBefore, "Bench size unchanged after a straight swap");

                // The incoming player takes the exact court spot the outgoing player held.
                view.FinalizeSubstitutionWalks();
                Assert(view.TryGetActorPosition(inId, out var inFinal), "Incoming player has a resolved position");
                float dist = Vector2.Distance(inFinal, starterCourtPos);
                Assert(dist < 0.5f, $"Incoming player walked onto the outgoing player's spot ({dist:F2}px)");
            }
            finally
            {
                Object.Destroy(go);
            }
        }
    }
}
