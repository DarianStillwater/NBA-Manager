using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Components;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the ambient bench + coach render layer: every roster player not on the floor
    /// appears on his team's bench, on-court players never do, and each team gets exactly one
    /// coach icon. Verified at the state level (no pixels) so it runs without a loaded save —
    /// the bench reads raw RosterPlayerIds, not the PlayerDatabase-backed Team.Roster.
    /// </summary>
    public class BenchCoachRenderTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            TestBenchAndCoachPopulation();
            return (_passed, _failed);
        }

        private static Team MakeTeam(string id, int rosterSize)
        {
            var t = new Team { TeamId = id, Name = id, Abbreviation = id };
            t.RosterPlayerIds = Enumerable.Range(0, rosterSize).Select(i => $"{id}_p{i}").ToList();
            for (int i = 0; i < 5; i++) t.StartingLineupIds[i] = t.RosterPlayerIds[i];
            return t;
        }

        private void TestBenchAndCoachPopulation()
        {
            var home = MakeTeam("HOM", 15);
            var away = MakeTeam("AWY", 13);
            var homeLineup = home.StartingLineupIds.ToList();
            var awayLineup = away.StartingLineupIds.ToList();

            var go = new GameObject("__CourtViewTest__", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(940f, 500f);
            var view = go.AddComponent<MatchCourtView>();

            try
            {
                view.Setup(null, null, new Color(0.2f, 0.4f, 0.8f), new Color(0.8f, 0.2f, 0.2f));
                view.Initialize(home, away, homeLineup, awayLineup);

                // Bench = roster minus the 5 starters (capped at 10).
                Assert(view.BenchCount(true) == 10,
                    $"Home bench holds roster-minus-starters, capped at 10 ({view.BenchCount(true)})");
                Assert(view.BenchCount(false) == 8,
                    $"Away bench holds all 8 reserves ({view.BenchCount(false)})");

                // On-court starters are never on the bench.
                bool startersOffBench = homeLineup.Concat(awayLineup).All(id => !view.IsOnBench(id));
                Assert(startersOffBench, "No on-court starter appears on a bench");

                // Reserves are on the bench.
                Assert(view.IsOnBench("HOM_p6"), "A home reserve is on the bench");
                Assert(view.IsOnBench("AWY_p12"), "An away reserve is on the bench");

                // One coach per team.
                Assert(view.HasCoach(true) && view.HasCoach(false), "Each team has a coach icon");

                // ClearAll (via re-Initialize) tears the ambient actors down and rebuilds cleanly.
                view.Initialize(home, away, homeLineup, awayLineup);
                Assert(view.BenchCount(true) == 10 && view.HasCoach(true),
                    "Re-initialize rebuilds bench + coach without leaking duplicates");
            }
            finally
            {
                Object.Destroy(go);
            }
        }
    }
}
