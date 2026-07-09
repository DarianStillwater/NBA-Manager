using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 4 load management and form: injured players never suit up in
    /// headless sims, exhausted players get DNP-rest nights (but never in the
    /// playoffs, and never below 8 available), FormTracker turns recent game
    /// logs into a bounded hot/cold streak, and the sim effect stays capped.
    /// </summary>
    public class LoadManagementFormTest : BaseTest
    {
        private PlayerDatabase _db;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestInjuredAndExhaustedSit();
            TestPlayoffsNoRest();
            TestMinimumAvailableFloor();
            TestFormTracker();
            TestFormSimEffectCapped();

            return (_passed, _failed);
        }

        // ==================== FIXTURE ====================

        private Team BuildTeam(string teamId, int count)
        {
            var team = new Team { TeamId = teamId, City = teamId, Nickname = teamId };
            for (int i = 0; i < count; i++)
            {
                string pid = $"{teamId.ToLower()}{i}";
                _db.AddPlayer(new Player
                {
                    PlayerId = pid,
                    FirstName = "T",
                    LastName = pid,
                    TeamId = teamId,
                    Position = (Position)(i % 5 + 1),
                    BirthDate = DateTime.Now.AddYears(-26),
                    Shot_Three = 72, Shot_MidRange = 72, Finishing_Rim = 72,
                    Defense_Perimeter = 72, Defense_Interior = 72, Speed = 72,
                    Vertical = 72, Strength = 72, BallHandling = 72, Passing = 72,
                    BasketballIQ = 72, DefensiveRebound = 72, Block = 72,
                    Finishing_PostMoves = 72, Stamina = 70,
                    Energy = 100, Morale = 75, Form = 50
                });
                team.RosterPlayerIds.Add(pid);
            }
            for (int i = 0; i < 5; i++) team.StartingLineupIds[i] = team.RosterPlayerIds[i];
            return team;
        }

        private (Team home, Team away) FreshLeague(int rosterSize = 11)
        {
            _db = new PlayerDatabase();
            return (BuildTeam("HHH", rosterSize), BuildTeam("VVV", rosterSize));
        }

        private static int MinutesOf(NBAHeadCoach.Core.Simulation.GameResult result, string pid) =>
            result.BoxScore.PlayerStats.TryGetValue(pid, out var s) ? s.Minutes : 0;

        // ==================== TESTS ====================

        private void TestInjuredAndExhaustedSit()
        {
            var (home, away) = FreshLeague();

            var injured = _db.GetPlayer("hhh0");   // a starter
            injured.IsInjured = true;
            var gassed = _db.GetPlayer("hhh1");    // another starter
            gassed.Energy = 20f;

            UnityEngine.Random.InitState(1234);
            var sim = new GameSimulator(_db);
            var result = sim.SimulateGame(home, away, isPlayoff: false);

            AssertEqual(0, MinutesOf(result, "hhh0"), "Injured starter never plays");
            AssertEqual(0, MinutesOf(result, "hhh1"), "Exhausted starter gets a DNP-rest night");
            Assert(MinutesOf(result, "hhh5") > 0 || MinutesOf(result, "hhh6") > 0,
                "Bench fills the vacated minutes");
        }

        private void TestPlayoffsNoRest()
        {
            var (home, away) = FreshLeague();

            _db.GetPlayer("hhh0").IsInjured = true; // injuries still sit in playoffs
            _db.GetPlayer("hhh1").Energy = 20f;     // exhaustion does not

            UnityEngine.Random.InitState(1234);
            var sim = new GameSimulator(_db);
            var result = sim.SimulateGame(home, away, isPlayoff: true);

            AssertEqual(0, MinutesOf(result, "hhh0"), "Injured player sits even in the playoffs");
            Assert(MinutesOf(result, "hhh1") > 0, "Nobody rests a healthy player in the playoffs");
        }

        private void TestMinimumAvailableFloor()
        {
            var (home, away) = FreshLeague(rosterSize: 9);

            // Gas the entire roster — the floor must un-rest people back to 8.
            foreach (var pid in home.RosterPlayerIds)
                _db.GetPlayer(pid).Energy = 20f;

            UnityEngine.Random.InitState(1234);
            var sim = new GameSimulator(_db);
            var result = sim.SimulateGame(home, away, isPlayoff: false);

            int played = home.RosterPlayerIds.Count(pid => MinutesOf(result, pid) > 0);
            Assert(played >= 5, $"A gassed roster still fields a team ({played} played)");
            Assert(result.HomeScore > 0 && result.AwayScore > 0, "Game completes normally");
        }

        private void TestFormTracker()
        {
            _db = new PlayerDatabase();
            var player = new Player
            {
                PlayerId = "form1", FirstName = "F", LastName = "One", TeamId = "AAA",
                Position = Position.ShootingGuard, BirthDate = DateTime.Now.AddYears(-25),
                Energy = 100, Morale = 75, Form = 50
            };
            _db.AddPlayer(player);
            player.CareerStats.Add(new SeasonStats(2026, "AAA"));

            // Five cold games: 6 points on bad shooting, losing margin
            for (int i = 0; i < 5; i++)
            {
                player.CurrentSeasonStats.AddGameFromLog(new GameLog
                {
                    GameId = $"g{i}", Date = new DateTime(2026, 11, 1 + i),
                    Minutes = 30, Points = 6, FGM = 2, FGA = 12, ThreePM = 0, ThreePA = 4,
                    PlusMinus = -12
                });
            }
            FormTracker.Recompute(player);
            float coldForm = player.Form;
            Assert(coldForm < 45f, $"Cold stretch drops form below neutral ({coldForm:0})");
            Assert(coldForm >= FormTracker.MIN_FORM, "Form respects the floor");

            // Five hot games: 28 points on great shooting, winning margin
            for (int i = 5; i < 10; i++)
            {
                player.CurrentSeasonStats.AddGameFromLog(new GameLog
                {
                    GameId = $"g{i}", Date = new DateTime(2026, 11, 1 + i),
                    Minutes = 34, Points = 28, FGM = 11, FGA = 17, ThreePM = 4, ThreePA = 7,
                    PlusMinus = 14
                });
            }
            FormTracker.Recompute(player);
            Assert(player.Form > 60f, $"Hot stretch lifts form well above neutral ({player.Form:0})");
            Assert(player.Form <= FormTracker.MAX_FORM, "Form respects the ceiling");
            Assert(player.Form > coldForm, "Form tracks the direction of recent play");

            // Idle drift cools back toward neutral
            float before = player.Form;
            FormTracker.DriftTowardNeutral(player, 5f);
            Assert(Mathf.Abs(player.Form - FormTracker.NEUTRAL) < Mathf.Abs(before - FormTracker.NEUTRAL),
                "Idle days drift form toward neutral");

            // No games at all → neutral
            var rookie = new Player { PlayerId = "form2", Energy = 100, Form = 80 };
            FormTracker.Recompute(rookie);
            AssertEqual(FormTracker.NEUTRAL, rookie.Form, "No recent games means neutral form");
        }

        private void TestFormSimEffectCapped()
        {
            var hot = new Player { Energy = 100, Morale = 50, Form = 100 };
            var cold = new Player { Energy = 100, Morale = 50, Form = 0 };

            float spread = hot.GetConditionModifier() / cold.GetConditionModifier();
            Assert(spread <= 1.12f,
                $"Form's total sim effect stays small and capped (spread {spread:0.000})");
            Assert(spread > 1.0f, "Hot form still helps");
        }
    }
}
