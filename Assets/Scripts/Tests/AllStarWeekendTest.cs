using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 7-C commit 1: the All-Star game is a REAL simulation over
    /// synthetic conference teams (box score, MVP), the contests crown real named
    /// players seeded by hidden skills, nothing leaks into season stats or energy,
    /// and the whole weekend survives a save.
    /// </summary>
    public class AllStarWeekendTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(20260215);

            var (players, teams, db) = BuildLeague();
            var mgr = new AllStarManager();
            mgr.PlayerSource = id => db.GetPlayer(id);

            var weekend = mgr.RunSelections(2026, new DateTime(2027, 2, 14), players,
                id => teams.TryGetValue(id, out var t) ? t.Conference : null);
            Assert(weekend != null, "Selections produced a weekend");
            if (weekend == null) return (_passed, _failed);

            TestWeekendRunsForReal(mgr, weekend, players, db);
            TestSaveRoundTrip(mgr, weekend);
            TestBestRecordCoachRule();

            return (_passed, _failed);
        }

        private void TestWeekendRunsForReal(AllStarManager mgr, AllStarWeekendSummary weekend,
            List<Player> players, PlayerDatabase db)
        {
            // Season-leak sentinels
            var participants = weekend.GetAllSelected()
                .Select(v => db.GetPlayer(v.PlayerId)).Where(p => p != null).ToList();
            foreach (var p in participants) p.Energy = 80f;
            var statSnapshot = participants.ToDictionary(p => p.PlayerId,
                p => p.CurrentSeasonStats?.Points ?? 0);

            var result = mgr.RunAllStarWeekend(db);

            Assert(result?.AllStarGameResult != null, "The All-Star game was played");
            var game = result.AllStarGameResult;
            Assert(game.EastScore >= 60 && game.WestScore >= 60,
                $"Real simulation produces a real score ({game.EastScore}-{game.WestScore})");
            Assert(game.EastScore != game.WestScore, "Somebody won");
            Assert(!string.IsNullOrEmpty(game.MvpName) && !game.MvpName.StartsWith("Player_"),
                $"MVP is a real named player ({game.MvpName})");
            Assert(game.PlayerStats.Count >= 20, $"Box lines for the participants ({game.PlayerStats.Count})");
            Assert(game.PlayerStats.Values.Sum(s => s.Points) > 0, "Points attributed to players");

            Assert(result.ThreePointContestResult != null &&
                   !string.IsNullOrEmpty(result.ThreePointContestResult.WinnerName) &&
                   !result.ThreePointContestResult.WinnerName.StartsWith("Player_"),
                $"3PT contest crowns a real name ({result.ThreePointContestResult?.WinnerName})");
            Assert(result.DunkContestResult != null &&
                   !string.IsNullOrEmpty(result.DunkContestResult.WinnerName),
                "Dunk contest crowns a winner");

            // The exhibition costs the season nothing
            Assert(participants.All(p => Mathf.Approximately(p.Energy, 80f)),
                "Energy restored after the exhibition");
            Assert(participants.All(p => (p.CurrentSeasonStats?.Points ?? 0) == statSnapshot[p.PlayerId]),
                "Season stats untouched by the All-Star game");

            // Idempotent
            var again = mgr.RunAllStarWeekend(db);
            Assert(ReferenceEquals(result, again) && ReferenceEquals(again.AllStarGameResult, game),
                "Re-running the weekend doesn't replay the game");
        }

        private void TestSaveRoundTrip(AllStarManager mgr, AllStarWeekendSummary weekend)
        {
            var data = new SaveData();
            mgr.WriteSave(data);
            var parsed = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            var restored = new AllStarManager();
            restored.ReadSave(parsed, new SaveReadContext(SaveData.CURRENT_VERSION, false, 2026));

            var w = restored.CurrentWeekend;
            Assert(w != null, "Weekend survives a save");
            if (w == null) return;
            AssertEqual(2026, w.Season, "Season survives");
            AssertEqual(24, w.GetAllSelected().Count, "All 24 selections survive");
            AssertEqual(weekend.AllStarGameResult.MvpId, w.AllStarGameResult?.MvpId, "MVP survives");
            AssertEqual(weekend.AllStarGameResult.EastScore, w.AllStarGameResult?.EastScore ?? -1,
                "Final score survives");
            Assert(w.AllStarGameResult.PlayerStats.Count > 0, "Box lines survive the flattening");
            AssertEqual(weekend.ThreePointContestResult.WinnerId, w.ThreePointContestResult?.WinnerId,
                "Contest winner survives");
        }

        private void TestBestRecordCoachRule()
        {
            var mine = new Team { TeamId = "ME", Conference = "East", Wins = 40, Losses = 12 };
            var rival = new Team { TeamId = "RV", Conference = "East", Wins = 38, Losses = 14 };
            var westBest = new Team { TeamId = "WB", Conference = "West", Wins = 45, Losses = 7 };
            var all = new List<Team> { mine, rival, westBest };

            Assert(AllStarManager.TeamLeadsConference(mine, all),
                "Best record in the conference earns the All-Star bench");
            Assert(!AllStarManager.TeamLeadsConference(rival, all),
                "Second place doesn't coach");
            Assert(AllStarManager.TeamLeadsConference(westBest, all),
                "The other conference judges independently");

            rival.Wins = 40; rival.Losses = 12;
            Assert(AllStarManager.TeamLeadsConference(mine, all),
                "Ties break toward the checked team");
        }

        /// <summary>30 teams, 8 players each, with season stats and hidden attributes for the sim.</summary>
        private (List<Player>, Dictionary<string, Team>, PlayerDatabase) BuildLeague()
        {
            var players = new List<Player>();
            var teams = new Dictionary<string, Team>();
            var db = new PlayerDatabase();
            var rng = new System.Random(777);
            string[] first = { "James", "Kevin", "Luka", "Jayson", "Devin", "Tyrese", "Anthony", "Shai" };

            for (int t = 0; t < 30; t++)
            {
                string teamId = $"T{t:00}";
                teams[teamId] = new Team
                {
                    TeamId = teamId,
                    Nickname = $"Team {t}",
                    Conference = t < 15 ? "East" : "West"
                };

                for (int i = 0; i < 8; i++)
                {
                    int quality = 90 - i * 6 + rng.Next(6);
                    var p = new Player
                    {
                        PlayerId = $"{teamId}_p{i}",
                        FirstName = first[i % first.Length],
                        LastName = $"{(char)('A' + t % 26)}{i}son",
                        TeamId = teamId,
                        Position = (Position)(i % 5 + 1),
                        BirthDate = DateTime.Now.AddYears(-22 - i),
                        Energy = 100, Morale = 75, Form = 50,
                        Shot_Three = quality - rng.Next(15),
                        Shot_MidRange = quality - rng.Next(15),
                        Shot_Close = quality - rng.Next(10),
                        Finishing_Rim = quality - rng.Next(10),
                        Passing = quality - rng.Next(20),
                        BallHandling = quality - rng.Next(20),
                        Defense_Perimeter = quality - rng.Next(20),
                        Defense_Interior = quality - rng.Next(20),
                        DefensiveRebound = quality - rng.Next(15),
                        Speed = quality - rng.Next(15),
                        Vertical = quality - rng.Next(15),
                        Strength = quality - rng.Next(20),
                        Stamina = 80,
                        HeightInches = 74 + i % 5 * 3,
                        WeightLbs = 190 + i % 5 * 15
                    };
                    p.StartNewSeason(2026, teamId);
                    var s = p.CurrentSeasonStats;
                    s.GamesPlayed = 50;
                    s.GamesStarted = i < 5 ? 50 : 5;
                    s.Points = Math.Max(6, 30 - i * 3) * s.GamesPlayed;
                    s.Assists = Math.Max(1, 8 - i) * s.GamesPlayed;
                    s.DefensiveRebounds = Math.Max(2, 9 - i) * s.GamesPlayed;

                    players.Add(p);
                    db.AddPlayer(p);
                }
            }

            return (players, teams, db);
        }
    }
}
