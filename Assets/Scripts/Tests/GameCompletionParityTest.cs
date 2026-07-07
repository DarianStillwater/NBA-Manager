using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the GameCompletionPipeline contract: every sim path (league auto-sim,
    /// quick-sim, interactive match) must mutate the SAME stat categories for the same
    /// game. A path that skips season stats or morale is the exact bug class the
    /// pipeline exists to prevent.
    /// </summary>
    public class GameCompletionParityTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var sources = new[] { GameSource.LeagueAutoSim, GameSource.QuickSim, GameSource.InteractiveMatch };
            var gamesPlayedBySource = new Dictionary<GameSource, int>();
            var pointsBySource = new Dictionary<GameSource, int>();

            foreach (var source in sources)
            {
                // Fresh fixture per source: same seed => identical simulated game,
                // so any per-source difference comes from the pipeline, not the sim.
                // The sim also draws from Unity's GLOBAL RNG (FoulSystem), so that
                // stream must be pinned too or the three games diverge.
                var fixture = BuildFixture();
                UnityEngine.Random.InitState(987654);
                var sim = new GameSimulator(fixture.Db, seed: 4242);
                var result = sim.SimulateGame(fixture.Home, fixture.Away);

                var gameEvent = new CalendarEvent
                {
                    EventId = $"parity_{source}",
                    Date = new System.DateTime(2026, 1, 15),
                    HomeTeamId = fixture.Home.TeamId,
                    AwayTeamId = fixture.Away.TeamId,
                    Type = CalendarEventType.Game
                };

                // Season/standings omitted (needs a live GameManager) — identically for
                // every source, so parity across the asserted categories still holds.
                var pipeline = new GameCompletionPipeline(
                    fixture.Db, null, new LeagueStatsAggregator(), new InjuryManager(), null,
                    id => id == fixture.Home.TeamId ? fixture.Home : fixture.Away);

                bool hookFired = false;
                pipeline.OnGameCompleted += _ => hookFired = true;

                pipeline.Complete(new GameCompletionContext(
                    gameEvent, result, source, fixture.Home.TeamId));

                Assert(hookFired, $"{source}: OnGameCompleted hook fired");

                // Season stats recorded for participants
                var participants = fixture.Db.GetAllPlayers()
                    .Where(p => p.CurrentSeasonStats != null && p.CurrentSeasonStats.GamesPlayed > 0)
                    .ToList();
                AssertGreaterThan(participants.Count, 9,
                    $"{source}: 10+ participants have season stats recorded");

                int totalGamesPlayed = fixture.Db.GetAllPlayers()
                    .Sum(p => p.CurrentSeasonStats?.GamesPlayed ?? 0);
                int totalPoints = fixture.Db.GetAllPlayers()
                    .Sum(p => p.CurrentSeasonStats?.Points ?? 0);
                gamesPlayedBySource[source] = totalGamesPlayed;
                pointsBySource[source] = totalPoints;

                AssertEqual(result.HomeScore + result.AwayScore, totalPoints,
                    $"{source}: recorded season points == game score");

                // Minutes load recorded for load management (injury step ran)
                int minutesRecorded = fixture.Db.GetAllPlayers()
                    .Sum(p => p.MinutesPlayedThisSeason);
                AssertGreaterThan(minutesRecorded, 0,
                    $"{source}: minutes load recorded for participants");
            }

            // The parity assertion itself: identical categories, identical values.
            AssertEqual(gamesPlayedBySource[GameSource.LeagueAutoSim],
                gamesPlayedBySource[GameSource.InteractiveMatch],
                "Auto-sim and interactive record identical games-played counts");
            AssertEqual(pointsBySource[GameSource.LeagueAutoSim],
                pointsBySource[GameSource.InteractiveMatch],
                "Auto-sim and interactive record identical season points");
            AssertEqual(pointsBySource[GameSource.QuickSim],
                pointsBySource[GameSource.InteractiveMatch],
                "Quick-sim and interactive record identical season points");

            // Score-only fallback must not throw and must not record stats
            var fb = BuildFixture();
            var fbPipeline = new GameCompletionPipeline(fb.Db, null, null, null, null, null);
            fbPipeline.CompleteScoreOnly(new CalendarEvent { EventId = "fb" }, 100, 98);
            AssertEqual(0, fb.Db.GetAllPlayers().Sum(p => p.CurrentSeasonStats?.GamesPlayed ?? 0),
                "Score-only completion records no player stats");

            return (_passed, _failed);
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
