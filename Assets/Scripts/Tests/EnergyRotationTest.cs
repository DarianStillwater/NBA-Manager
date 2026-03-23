using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    public class EnergyRotationTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var allPlayers = NBAPlayerData.GetAllPlayers();
            var gswPlayers = allPlayers.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating).ToList();
            var lalPlayers = allPlayers.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating).ToList();

            if (gswPlayers.Count < 6 || lalPlayers.Count < 6)
            {
                _failed++;
                Debug.Log("    FAIL: Need 6+ players per team for rotation test");
                return (_passed, _failed);
            }

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

            var sim = new GameSimulator(playerDb, seed: 42);
            var result = sim.SimulateGame(home, away);
            var box = result.BoxScore;

            // Check starters have < 100 energy (fatigue occurred)
            var starter = playerDb.GetPlayer(home.StartingLineupIds[0]);
            if (starter != null)
                AssertLessThan(starter.Energy, 100f, $"Starter {starter.LastName} energy < 100 after game");

            // Bench players got minutes
            int benchWithMinutes = 0;
            foreach (var pid in home.RosterPlayerIds)
            {
                if (home.StartingLineupIds.Contains(pid)) continue;
                if (box.PlayerStats.ContainsKey(pid) && box.PlayerStats[pid].Minutes > 0)
                    benchWithMinutes++;
            }
            AssertGreaterThan(benchWithMinutes, 0, "At least 1 bench player got minutes");

            // Starters play more than bench
            float avgStarterMin = (float)home.StartingLineupIds
                .Where(id => box.PlayerStats.ContainsKey(id))
                .Average(id => (float)box.PlayerStats[id].Minutes);
            var benchWithMin = home.RosterPlayerIds
                .Where(id => !home.StartingLineupIds.Contains(id) && box.PlayerStats.ContainsKey(id) && box.PlayerStats[id].Minutes > 0)
                .ToList();
            float avgBenchMin = benchWithMin.Count > 0
                ? (float)benchWithMin.Average(id => (float)box.PlayerStats[id].Minutes) : 0f;

            if (avgBenchMin > 0)
                AssertGreaterThan(avgStarterMin, avgBenchMin, $"Starter avg min ({avgStarterMin:F0}) > bench avg ({avgBenchMin:F0})");

            // No player plays all 48 minutes
            bool anyPlayedFull = home.RosterPlayerIds
                .Any(id => box.PlayerStats.ContainsKey(id) && box.PlayerStats[id].Minutes >= 48);
            Assert(!anyPlayedFull, "No player plays full 48 minutes");

            return (_passed, _failed);
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
