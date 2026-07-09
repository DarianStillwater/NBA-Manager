using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the season-honors wiring: All-Star selections produce valid 12-man
    /// rosters per conference, season awards vote to real winners, and the
    /// AwardsStore round-trips through a save.
    /// </summary>
    public class AwardsAllStarTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var fixture = BuildLeague();

            TestAllStarSelections(fixture);
            TestSeasonAwards(fixture);
            TestAwardsStoreRoundTrip();

            return (_passed, _failed);
        }

        private void TestAllStarSelections((List<Player> players, Dictionary<string, Team> teams) fx)
        {
            var mgr = new AllStarManager();
            UnityEngine.Random.InitState(777);

            var summary = mgr.RunSelections(2025, new DateTime(2026, 2, 14), fx.players,
                id => fx.teams.TryGetValue(id, out var t) ? t.Conference : null);

            Assert(summary != null, "All-Star selections produced a summary");
            if (summary == null) return;

            AssertEqual(5, summary.EastStarters.Count, "East has 5 starters");
            AssertEqual(5, summary.WestStarters.Count, "West has 5 starters");
            AssertEqual(7, summary.EastReserves.Count, "East has 7 reserves");
            AssertEqual(7, summary.WestReserves.Count, "West has 7 reserves");

            AssertEqual(2, summary.EastStarters.Count(v => v.Position == AllStarPosition.Backcourt),
                "East starters: 2 backcourt");
            AssertEqual(3, summary.EastStarters.Count(v => v.Position == AllStarPosition.Frontcourt),
                "East starters: 3 frontcourt");

            var all = summary.EastStarters.Concat(summary.WestStarters)
                .Concat(summary.EastReserves).Concat(summary.WestReserves).ToList();
            AssertEqual(24, all.Select(v => v.PlayerId).Distinct().Count(),
                "24 distinct All-Stars selected");
            Assert(summary.EastStarters.Concat(summary.EastReserves).All(v => v.Conference == "East"),
                "East roster entirely from the East");
            Assert(summary.WestStarters.Concat(summary.WestReserves).All(v => v.Conference == "West"),
                "West roster entirely from the West");

            // Idempotent per season
            var again = mgr.RunSelections(2025, new DateTime(2026, 2, 15), fx.players, id => "East");
            Assert(ReferenceEquals(summary, again), "Re-running selections returns the same weekend");
        }

        private void TestSeasonAwards((List<Player> players, Dictionary<string, Team> teams) fx)
        {
            var results = AwardManager.VoteSeasonAwards(2025, fx.players, fx.teams);

            Assert(results?.MVP != null, "MVP voted");
            Assert(results?.DPOY != null, "DPOY voted");
            AssertEqual(5, results?.AllNBAFirst?.Count ?? 0, "All-NBA First Team has 5 players");

            string coty = AwardManager.VoteCoachOfYear(2025, fx.teams);
            Assert(!string.IsNullOrEmpty(coty), "Coach of the Year voted");

            var champRoster = fx.players.Where(p => p.TeamId == results.MVP.TeamId).ToList();
            var finalsMvp = AwardManager.VoteFinalsMVP(2025, champRoster, results.MVP.TeamId);
            Assert(finalsMvp != null, "Finals MVP voted from champion roster");
        }

        private void TestAwardsStoreRoundTrip()
        {
            var storeA = new AwardsStore();
            storeA.RecordAllStars(2025, new[] { "p1", "p2", "p3" });
            var results = new AwardVotingResults
            {
                Year = 2025,
                MVP = new Player { PlayerId = "mvp1" },
                DPOY = new Player { PlayerId = "dpoy1" }
            };
            storeA.RecordSeasonAwards(2025, results, "GSW", "fmvp1", "GSW", "LAL");

            var data = new SaveData();
            storeA.WriteSave(data);
            var parsed = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            var storeB = new AwardsStore();
            storeB.ReadSave(parsed, new SaveReadContext(SaveData.CURRENT_VERSION, false, 2025));

            var entry = storeB.GetForSeason(2025);
            Assert(entry != null, "Awards entry survives round trip");
            if (entry == null) return;
            AssertEqual("mvp1", entry.MvpId, "MVP id survives");
            AssertEqual("fmvp1", entry.FinalsMvpId, "Finals MVP id survives");
            AssertEqual("GSW", entry.ChampionTeamId, "Champion survives");
            AssertEqual(3, entry.AllStars.Count, "All-Star list survives");
        }

        /// <summary>
        /// 30-team league: 15 East / 15 West, 16 players per team with graded
        /// synthetic season stats so voting has real signal.
        /// </summary>
        private (List<Player> players, Dictionary<string, Team> teams) BuildLeague()
        {
            var players = new List<Player>();
            var teams = new Dictionary<string, Team>();
            var rng = new System.Random(99);

            for (int t = 0; t < 30; t++)
            {
                string teamId = $"T{t:00}";
                teams[teamId] = new Team
                {
                    TeamId = teamId,
                    Nickname = $"Team {t}",
                    Conference = t < 15 ? "Eastern" : "Western",
                    Wins = 20 + rng.Next(40),
                    Losses = 0
                };
                teams[teamId].Losses = 82 - teams[teamId].Wins;

                for (int i = 0; i < 16; i++)
                {
                    var p = new Player
                    {
                        PlayerId = $"{teamId}_p{i}",
                        FirstName = "Player",
                        LastName = $"{teamId}-{i}",
                        TeamId = teamId,
                        Position = (Position)(i % 5 + 1),
                        BirthDate = DateTime.Now.AddYears(-20 - i % 15),
                        Energy = 100, Morale = 75, Form = 50
                    };
                    p.StartNewSeason(2025, teamId);
                    var s = p.CurrentSeasonStats;
                    int quality = 30 - i * 2 + rng.Next(6); // roster spot 0 = star
                    s.GamesPlayed = 60 + rng.Next(20);
                    s.GamesStarted = i < 5 ? s.GamesPlayed : rng.Next(10);
                    s.Points = Math.Max(4, quality) * s.GamesPlayed;
                    s.Assists = Math.Max(1, quality / 4) * s.GamesPlayed;
                    s.DefensiveRebounds = Math.Max(2, quality / 3) * s.GamesPlayed;
                    players.Add(p);
                }
            }

            return (players, teams);
        }
    }
}
