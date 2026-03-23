using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    public class GameSimulatorIntegrityTest : BaseTest
    {
        private const int GAMES_TO_SIMULATE = 10;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var allPlayers = NBAPlayerData.GetAllPlayers();
            var gswPlayers = allPlayers.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating).ToList();
            var lalPlayers = allPlayers.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating).ToList();

            if (gswPlayers.Count < 5 || lalPlayers.Count < 5)
            {
                _failed++;
                Debug.Log("    FAIL: Not enough players for test");
                return (_passed, _failed);
            }

            var playerDb = new PlayerDatabase();
            var gswRoster = gswPlayers.Select(p => ConvertToPlayer(p)).ToList();
            var lalRoster = lalPlayers.Select(p => ConvertToPlayer(p)).ToList();
            foreach (var p in gswRoster.Concat(lalRoster)) playerDb.AddPlayer(p);

            var homeTeam = CreateTeam("GSW", "Golden State", "Warriors", gswRoster);
            var awayTeam = CreateTeam("LAL", "Los Angeles", "Lakers", lalRoster);

            for (int g = 0; g < GAMES_TO_SIMULATE; g++)
            {
                var sim = new GameSimulator(playerDb, seed: g * 7 + 42);
                var result = sim.SimulateGame(homeTeam, awayTeam);
                ValidateGameResult(result, homeTeam, awayTeam, g + 1);

                // Reset energy for next game
                foreach (var p in gswRoster.Concat(lalRoster)) p.Energy = 100f;
            }

            return (_passed, _failed);
        }

        private void ValidateGameResult(NBAHeadCoach.Core.Simulation.GameResult result, Team home, Team away, int gameNum)
        {
            string prefix = $"Game {gameNum}";
            var box = result.BoxScore;

            // Score consistency: team score = sum of player points
            int homePlayerPts = home.RosterPlayerIds
                .Where(id => box.PlayerStats.ContainsKey(id))
                .Sum(id => box.PlayerStats[id].Points);
            int awayPlayerPts = away.RosterPlayerIds
                .Where(id => box.PlayerStats.ContainsKey(id))
                .Sum(id => box.PlayerStats[id].Points);

            AssertEqual(result.HomeScore, homePlayerPts,
                $"{prefix}: Home score ({result.HomeScore}) = sum player pts ({homePlayerPts})");
            AssertEqual(result.AwayScore, awayPlayerPts,
                $"{prefix}: Away score ({result.AwayScore}) = sum player pts ({awayPlayerPts})");

            // Realistic score range
            AssertRange(result.HomeScore, 70, 160, $"{prefix}: Home score in range");
            AssertRange(result.AwayScore, 70, 160, $"{prefix}: Away score in range");

            // No ties
            Assert(result.HomeScore != result.AwayScore, $"{prefix}: No tie in final score");

            // Bench rotation: at least 1 bench player has minutes
            int homeBenchWithMinutes = home.RosterPlayerIds
                .Where(id => !home.StartingLineupIds.Contains(id))
                .Count(id => box.PlayerStats.ContainsKey(id) && box.PlayerStats[id].Minutes > 0);
            AssertGreaterThan(homeBenchWithMinutes, 0, $"{prefix}: Home bench players got minutes");

            // Stat validity per player
            foreach (var pid in home.RosterPlayerIds.Concat(away.RosterPlayerIds))
            {
                if (!box.PlayerStats.ContainsKey(pid)) continue;
                var s = box.PlayerStats[pid];
                if (s.Minutes == 0 && s.Points == 0) continue;

                Assert(s.ThreePointMade <= s.ThreePointAttempts, $"{prefix}: {pid} 3PM <= 3PA");
                Assert(s.TotalFGM <= s.TotalFGA, $"{prefix}: {pid} FGM <= FGA");
                Assert(s.Points >= 0, $"{prefix}: {pid} points >= 0");
                Assert(s.PersonalFouls <= 6, $"{prefix}: {pid} fouls ({s.PersonalFouls}) <= 6");
                Assert(s.OffensiveRebounds >= 0 && s.DefensiveRebounds >= 0, $"{prefix}: {pid} rebounds >= 0");
            }

            // Team-level checks
            int homeReb = home.RosterPlayerIds
                .Where(id => box.PlayerStats.ContainsKey(id))
                .Sum(id => box.PlayerStats[id].TotalRebounds);
            AssertGreaterThan(homeReb, 0, $"{prefix}: Home team has rebounds");
        }

        private Team CreateTeam(string teamId, string city, string nickname, List<Player> roster)
        {
            var team = new Team
            {
                TeamId = teamId, City = city, Nickname = nickname,
                RosterPlayerIds = roster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateDefault(teamId),
                DefensiveStrategy = TeamStrategy.CreateDefault(teamId)
            };
            for (int i = 0; i < 5 && i < roster.Count; i++)
                team.StartingLineupIds[i] = roster[i].PlayerId;
            return team;
        }

        private Player ConvertToPlayer(PlayerData d)
        {
            return new Player
            {
                PlayerId = d.PlayerId, FirstName = d.FirstName, LastName = d.LastName,
                JerseyNumber = d.JerseyNumber, Position = (Position)d.Position,
                BirthDate = System.DateTime.Now.AddYears(-d.Age),
                HeightInches = d.HeightInches, WeightLbs = d.WeightLbs, TeamId = d.TeamId,
                Finishing_Rim = d.Finishing_Rim, Finishing_PostMoves = d.Finishing_PostMoves,
                Shot_Close = d.Shot_Close, Shot_MidRange = d.Shot_MidRange,
                Shot_Three = d.Shot_Three, FreeThrow = d.FreeThrow,
                Passing = d.Passing, BallHandling = d.BallHandling,
                OffensiveIQ = d.OffensiveIQ, SpeedWithBall = d.SpeedWithBall,
                Defense_Perimeter = d.Defense_Perimeter, Defense_Interior = d.Defense_Interior,
                Defense_PostDefense = d.Defense_PostDefense,
                Steal = d.Steal, Block = d.Block, DefensiveIQ = d.DefensiveIQ,
                DefensiveRebound = d.DefensiveRebound,
                Speed = d.Speed, Acceleration = d.Acceleration,
                Strength = d.Strength, Vertical = d.Vertical, Stamina = d.Stamina,
                BasketballIQ = d.BasketballIQ, Clutch = d.Clutch,
                Leadership = d.Leadership, Composure = d.Composure, Aggression = d.Aggression,
                Energy = 100, Morale = 75, Form = 70
            };
        }
    }
}
