using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the in-match substitution fix: the sim lineup is the single source
    /// of truth, subs replace in place (preserving position slots), duplicate /
    /// non-roster / fouled-out check-ins are rejected (the old 4-on-5 bug),
    /// foul-outs use the same mechanism, lineup changes are broadcast, and the
    /// team's saved starting five is never mutated by in-game subs.
    /// </summary>
    public class InMatchSubstitutionTest : BaseTest
    {
        private MatchSimulationController _sim;
        private GameObject _go;
        private Team _home, _away;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestInPlaceSubPreservesSlots();
            TestGuards();
            TestManyRandomSubsStayFiveDistinct();
            TestFoulOutSharedPath();
            TestStartingLineupIdsNeverTouched();
            TestInitDeDupes();

            Cleanup();
            return (_passed, _failed);
        }

        private Team MakeTeam(string id, int rosterSize)
        {
            var t = new Team { TeamId = id, City = id, Nickname = id, Abbreviation = id };
            for (int i = 0; i < rosterSize; i++) t.RosterPlayerIds.Add($"{id}_{i}");
            t.StartingLineupIds = t.RosterPlayerIds.Take(5).ToArray();
            return t;
        }

        private void Setup()
        {
            Cleanup();
            _go = new GameObject("__SubTestSim__");
            _sim = _go.AddComponent<MatchSimulationController>();
            _home = MakeTeam("HOM", 12);
            _away = MakeTeam("AWY", 12);
            var coach = new GameCoach(_home.Strategy, PlayBook.CreateDefault("HOM"),
                TeamGameInstructions.CreateDefault("HOM", new List<Player>()));
            _sim.InitializeMatch(_home, _away, _home, coach);
        }

        private void Cleanup()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            _go = null; _sim = null;
        }

        private void TestInPlaceSubPreservesSlots()
        {
            Setup();
            string changedTeam = null, changedOut = null, changedIn = null;
            _sim.OnLineupChanged += (t, o, i) => { changedTeam = t; changedOut = o; changedIn = i; };

            bool ok = _sim.MakeSubstitution("HOM_2", "HOM_7");
            Assert(ok, "Valid substitution succeeds");
            var lineup = _sim.GetLineup("HOM");
            AssertEqual("HOM_7", lineup[2], "Incoming player takes the outgoing player's slot");
            AssertEqual("HOM_0", lineup[0], "Other slots untouched");
            AssertEqual(5, lineup.Distinct().Count(), "Five distinct players on court");
            Assert(changedTeam == "HOM" && changedOut == "HOM_2" && changedIn == "HOM_7",
                "OnLineupChanged fires with team/out/in");
        }

        private void TestGuards()
        {
            Setup();
            Assert(!_sim.MakeSubstitution("HOM_0", "HOM_1"),
                "Subbing in a player already on court is rejected");
            Assert(!_sim.MakeSubstitution("HOM_9", "HOM_10"),
                "Subbing out a player not on court is rejected");
            Assert(!_sim.MakeSubstitution("HOM_0", "AWY_5"),
                "Subbing in a non-roster player is rejected");
            Assert(!_sim.MakeSubstitution("HOM_0", null),
                "Null incoming id is rejected");
            var lineup = _sim.GetLineup("HOM");
            AssertEqual(5, lineup.Distinct().Count(), "Lineup intact after rejected subs");
        }

        private void TestManyRandomSubsStayFiveDistinct()
        {
            Setup();
            var rng = new System.Random(42);
            for (int n = 0; n < 40; n++)
            {
                var lineup = _sim.GetLineup("HOM").ToList();
                var bench = _home.RosterPlayerIds.Where(id => !lineup.Contains(id)).ToList();
                _sim.MakeSubstitution(lineup[rng.Next(5)], bench[rng.Next(bench.Count)]);
            }
            var final = _sim.GetLineup("HOM");
            AssertEqual(5, final.Count, "Still exactly 5 on court after 40 subs");
            AssertEqual(5, final.Distinct().Count(), "All 5 distinct after 40 subs (no 4-on-5)");
            Assert(final.All(id => _home.RosterPlayerIds.Contains(id)), "All on-court ids from roster");
        }

        private void TestFoulOutSharedPath()
        {
            Setup();
            bool eventFired = false;
            _sim.OnLineupChanged += (t, o, i) => eventFired = true;

            // Force 6 fouls on a starter, then invoke the private foul-out handler
            var foulsField = typeof(MatchSimulationController)
                .GetField("_playerFouls", BindingFlags.NonPublic | BindingFlags.Instance);
            var fouls = (Dictionary<string, int>)foulsField.GetValue(_sim);
            fouls["HOM_3"] = 6;
            var handle = typeof(MatchSimulationController)
                .GetMethod("HandleFoulOut", BindingFlags.NonPublic | BindingFlags.Instance);
            handle.Invoke(_sim, new object[] { "HOM_3" });

            var lineup = _sim.GetLineup("HOM");
            Assert(!lineup.Contains("HOM_3"), "Fouled-out player leaves the floor");
            AssertEqual(5, lineup.Distinct().Count(), "Foul-out auto-sub keeps 5 distinct");
            Assert(eventFired, "Foul-out fires OnLineupChanged");
            Assert(_sim.HasFouledOut("HOM_3"), "HasFouledOut reports the foul-out");
            Assert(!_sim.MakeSubstitution(lineup[0], "HOM_3"),
                "Fouled-out player cannot check back in");
        }

        private void TestStartingLineupIdsNeverTouched()
        {
            Setup();
            var before = _home.StartingLineupIds.ToArray();
            _sim.MakeSubstitution("HOM_0", "HOM_8");
            _sim.MakeSubstitution("HOM_1", "HOM_9");
            Assert(before.SequenceEqual(_home.StartingLineupIds),
                "In-match subs never mutate Team.StartingLineupIds (starters restored next game)");
        }

        private void TestInitDeDupes()
        {
            Setup();
            _home.StartingLineupIds = new[] { "HOM_0", "HOM_0", "HOM_1", "HOM_2", "HOM_3" };
            var coach = new GameCoach(_home.Strategy, PlayBook.CreateDefault("HOM"),
                TeamGameInstructions.CreateDefault("HOM", new List<Player>()));
            _sim.InitializeMatch(_home, _away, _home, coach);
            var lineup = _sim.GetLineup("HOM");
            AssertEqual(lineup.Count, lineup.Distinct().Count(),
                "A corrupted saved lineup is de-duped at match init");
        }
    }
}
