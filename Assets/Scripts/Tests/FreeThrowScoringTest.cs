using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    public class FreeThrowScoringTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestScoreConsistencyAcrossGames();
            TestFreeThrowAccuracy();

            return (_passed, _failed);
        }

        private void TestScoreConsistencyAcrossGames()
        {
            var allPlayers = NBAPlayerData.GetAllPlayers();
            var gswPlayers = allPlayers.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating).ToList();
            var lalPlayers = allPlayers.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating).ToList();

            if (gswPlayers.Count < 5 || lalPlayers.Count < 5) return;

            var playerDb = new PlayerDatabase();
            var gswRoster = gswPlayers.Select(ConvertPlayer).ToList();
            var lalRoster = lalPlayers.Select(ConvertPlayer).ToList();
            foreach (var p in gswRoster.Concat(lalRoster)) playerDb.AddPlayer(p);

            var home = new Team
            {
                TeamId = "GSW", RosterPlayerIds = gswRoster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateDefault("GSW"),
                DefensiveStrategy = TeamStrategy.CreateDefault("GSW")
            };
            for (int i = 0; i < 5; i++) home.StartingLineupIds[i] = gswRoster[i].PlayerId;

            var away = new Team
            {
                TeamId = "LAL", RosterPlayerIds = lalRoster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateDefault("LAL"),
                DefensiveStrategy = TeamStrategy.CreateDefault("LAL")
            };
            for (int i = 0; i < 5; i++) away.StartingLineupIds[i] = lalRoster[i].PlayerId;

            int gamesWithMismatch = 0;
            for (int g = 0; g < 10; g++)
            {
                foreach (var p in gswRoster.Concat(lalRoster)) p.Energy = 100f;

                var sim = new GameSimulator(playerDb, seed: g * 13 + 7);
                var result = sim.SimulateGame(home, away);
                var box = result.BoxScore;

                int homePlayerPts = home.RosterPlayerIds
                    .Where(id => box.PlayerStats.ContainsKey(id))
                    .Sum(id => box.PlayerStats[id].Points);

                if (result.HomeScore != homePlayerPts)
                    gamesWithMismatch++;
            }

            AssertEqual(0, gamesWithMismatch,
                $"Score consistency: {gamesWithMismatch}/10 games had team score != player points sum");
        }

        private void TestFreeThrowAccuracy()
        {
            var handler = new FreeThrowHandler();
            var context = new GameContext { Quarter = 2, GameClock = 300f };

            // High FT player
            var good = new Player { PlayerId = "GOOD", FreeThrow = 95, Clutch = 70, Energy = 100 };
            int goodMade = 0, goodAttempts = 0;
            for (int i = 0; i < 100; i++)
            {
                var result = handler.CalculateFreeThrows(good, 2, context);
                goodMade += result.Made;
                goodAttempts += result.Attempts;
            }
            float goodPct = goodAttempts > 0 ? (float)goodMade / goodAttempts : 0;
            AssertGreaterThan(goodPct, 0.75f, $"Good FT shooter ({goodPct:P0}) > 75%");

            // Bad FT player
            var bad = new Player { PlayerId = "BAD", FreeThrow = 30, Clutch = 30, Energy = 100 };
            int badMade = 0, badAttempts = 0;
            for (int i = 0; i < 100; i++)
            {
                var result = handler.CalculateFreeThrows(bad, 2, context);
                badMade += result.Made;
                badAttempts += result.Attempts;
            }
            float badPct = badAttempts > 0 ? (float)badMade / badAttempts : 0;
            AssertLessThan(badPct, 0.65f, $"Bad FT shooter ({badPct:P0}) < 65%");

            AssertGreaterThan(goodPct, badPct, "Good FT% > bad FT%");
        }

        private Player ConvertPlayer(PlayerData d)
        {
            return new Player
            {
                PlayerId = d.PlayerId, FirstName = d.FirstName, LastName = d.LastName,
                Position = (Position)d.Position, BirthDate = System.DateTime.Now.AddYears(-d.Age),
                TeamId = d.TeamId, Finishing_Rim = d.Finishing_Rim, Shot_Close = d.Shot_Close,
                Shot_MidRange = d.Shot_MidRange, Shot_Three = d.Shot_Three, FreeThrow = d.FreeThrow,
                Passing = d.Passing, BallHandling = d.BallHandling, OffensiveIQ = d.OffensiveIQ,
                Defense_Perimeter = d.Defense_Perimeter, Defense_Interior = d.Defense_Interior,
                Steal = d.Steal, Block = d.Block, DefensiveIQ = d.DefensiveIQ,
                DefensiveRebound = d.DefensiveRebound, Speed = d.Speed, Strength = d.Strength,
                Stamina = d.Stamina, BasketballIQ = d.BasketballIQ, Clutch = d.Clutch,
                Energy = 100, Morale = 75, Form = 70
            };
        }
    }
}
