using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the energy-carryover contract: GameSimulator must NOT reset energy to
    /// 100 at tip-off — players enter with whatever the season left them and leave
    /// with post-game values. Plus a schedule-density smoke test: a game-plus-rest-day
    /// cycle with the daily recovery constants (+5 game day, +15 rest day, mirroring
    /// SeasonController.ProcessDailyEvents) must be sustainable, not a death spiral.
    /// </summary>
    public class EnergyCarryoverTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestNoTipOffReset();
            TestZeroEnergyGuard();
            TestScheduleDensitySmoke();

            return (_passed, _failed);
        }

        private void TestNoTipOffReset()
        {
            // In-game bench recovery (0.8/s) means post-game energy says little about
            // entry energy — the honest proof that there is NO tip-off reset is that
            // ENTRY energy changes the game. Same seeds, only entry energy differs:
            // with the old reset both games were bit-identical; with carryover a
            // 30-energy roster subs constantly and plays a different game.
            var fresh = PlayOneGame(entryEnergy: 100f);
            var gassed = PlayOneGame(entryEnergy: 30f);

            bool differs = fresh.Result.HomeScore != gassed.Result.HomeScore
                        || fresh.Result.AwayScore != gassed.Result.AwayScore
                        || fresh.StarterMinutes != gassed.StarterMinutes;
            Assert(differs, "Entry energy changes the simulated game (no tip-off reset)");

            foreach (var p in gassed.Db.GetAllPlayers())
            {
                if (p.Energy < 0f || p.Energy > 100f)
                {
                    Assert(false, $"{p.LastName} energy {p.Energy:F0} out of [0,100]");
                    return;
                }
            }
            Assert(true, "All energies within [0,100] after game");
        }

        private (PlayerDatabase Db, NBAHeadCoach.Core.Simulation.GameResult Result, int StarterMinutes)
            PlayOneGame(float entryEnergy)
        {
            var (db, home, away) = BuildFixture();
            foreach (var p in db.GetAllPlayers()) p.Energy = entryEnergy;

            UnityEngine.Random.InitState(24680);
            var sim = new GameSimulator(db, seed: 1337);
            var result = sim.SimulateGame(home, away);

            int starterMinutes = home.StartingLineupIds.Concat(away.StartingLineupIds)
                .Where(id => !string.IsNullOrEmpty(id) && result.BoxScore.PlayerStats.ContainsKey(id))
                .Sum(id => result.BoxScore.PlayerStats[id].Minutes);

            return (db, result, starterMinutes);
        }

        private void TestZeroEnergyGuard()
        {
            // Raw never-initialized Players (tools/tests) default to Energy 0 — the
            // guard must give them a full tank instead of simulating exhausted ghosts.
            var (db, home, away) = BuildFixture();
            foreach (var p in db.GetAllPlayers()) p.Energy = 0f;

            UnityEngine.Random.InitState(13579);
            var sim = new GameSimulator(db, seed: 7331);
            var result = sim.SimulateGame(home, away);

            AssertRange(result.HomeScore, 70, 170, "Zero-energy-guard game produces a sane score");
        }

        private void TestScheduleDensitySmoke()
        {
            var (db, home, away) = BuildFixture();
            // Everyone starts a 10-game stretch at full health
            foreach (var p in db.GetAllPlayers()) p.Energy = 100f;

            const int GAMES = 10;
            for (int g = 0; g < GAMES; g++)
            {
                // Game-day morning recovery (+5), then the game
                foreach (var p in db.GetAllPlayers())
                    p.Energy = Mathf.Min(100f, p.Energy + 5f);

                UnityEngine.Random.InitState(1000 + g);
                var sim = new GameSimulator(db, seed: 500 + g);
                sim.SimulateGame(home, away);

                // One rest day between games (+15)
                foreach (var p in db.GetAllPlayers())
                    p.Energy = Mathf.Min(100f, p.Energy + 15f);
            }

            float rosterAvg = db.GetAllPlayers().Average(p => p.Energy);
            AssertGreaterThan(rosterAvg, 45f,
                $"Roster average energy sustainable over {GAMES}-game stretch");

            float starterMin = home.StartingLineupIds.Concat(away.StartingLineupIds)
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => db.GetPlayer(id))
                .Where(p => p != null)
                .Min(p => p.Energy);
            AssertGreaterThan(starterMin, 10f,
                "No starter ground to dust by the stretch");
        }

        private (PlayerDatabase Db, Team Home, Team Away) BuildFixture()
        {
            var allPlayers = NBAPlayerData.GetAllPlayers();
            var gsw = allPlayers.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating).ToList();
            var lal = allPlayers.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating).ToList();

            var db = new PlayerDatabase();
            var gswRoster = gsw.Select(Convert).ToList();
            var lalRoster = lal.Select(Convert).ToList();
            foreach (var p in gswRoster.Concat(lalRoster)) db.AddPlayer(p);

            return (db, CreateTeam("GSW", gswRoster), CreateTeam("LAL", lalRoster));
        }

        private Team CreateTeam(string teamId, List<Player> roster)
        {
            var team = new Team
            {
                TeamId = teamId, City = teamId, Nickname = teamId,
                RosterPlayerIds = roster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateDefault(teamId),
                DefensiveStrategy = TeamStrategy.CreateDefault(teamId)
            };
            for (int i = 0; i < 5 && i < roster.Count; i++)
                team.StartingLineupIds[i] = roster[i].PlayerId;
            return team;
        }

        private Player Convert(PlayerData d)
        {
            return new Player
            {
                PlayerId = d.PlayerId, FirstName = d.FirstName, LastName = d.LastName,
                Position = (Position)d.Position, BirthDate = System.DateTime.Now.AddYears(-d.Age),
                TeamId = d.TeamId, Finishing_Rim = d.Finishing_Rim, Shot_Close = d.Shot_Close,
                Shot_MidRange = d.Shot_MidRange, Shot_Three = d.Shot_Three, FreeThrow = d.FreeThrow,
                Passing = d.Passing, BallHandling = d.BallHandling, OffensiveIQ = d.OffensiveIQ,
                SpeedWithBall = d.SpeedWithBall, Defense_Perimeter = d.Defense_Perimeter,
                Defense_Interior = d.Defense_Interior, Steal = d.Steal, Block = d.Block,
                DefensiveIQ = d.DefensiveIQ, DefensiveRebound = d.DefensiveRebound,
                Speed = d.Speed, Strength = d.Strength, Stamina = d.Stamina,
                BasketballIQ = d.BasketballIQ, Clutch = d.Clutch,
                Energy = 100, Morale = 75, Form = 70
            };
        }
    }
}
