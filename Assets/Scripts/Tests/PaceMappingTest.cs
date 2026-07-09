using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the pace fix: UI pace controls used to write only the PacePreference
    /// enum while the possession loop reads TargetPace — a dead toggle.
    /// ApplyPacePreference must keep both fields coherent, and faster settings
    /// must actually produce more possessions in seeded games.
    /// </summary>
    public class PaceMappingTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestMapping();
            TestFastOutpacesSlowInSimulation();

            return (_passed, _failed);
        }

        private void TestMapping()
        {
            var s = TeamStrategy.CreateDefault("TST");

            s.ApplyPacePreference(PacePreference.Deliberate);
            AssertEqual(92f, s.TargetPace, "Deliberate maps to TargetPace 92");
            AssertEqual(PacePreference.Deliberate, s.PacePreference, "Enum kept in sync");

            s.ApplyPacePreference(PacePreference.Balanced);
            AssertEqual(100f, s.TargetPace, "Balanced maps to TargetPace 100");

            s.ApplyPacePreference(PacePreference.PushWhenPossible);
            AssertEqual(104f, s.TargetPace, "PushWhenPossible maps to TargetPace 104");

            s.ApplyPacePreference(PacePreference.AlwaysPush);
            AssertEqual(108f, s.TargetPace, "AlwaysPush maps to TargetPace 108");

            Assert(TeamStrategy.TargetPaceFor(PacePreference.AlwaysPush) >
                   TeamStrategy.TargetPaceFor(PacePreference.Deliberate),
                "Pace mapping is monotonic");
        }

        private void TestFastOutpacesSlowInSimulation()
        {
            var allPlayers = NBAPlayerData.GetAllPlayers();
            var gsw = allPlayers.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating)
                .Select(ConvertToPlayer).ToList();
            var lal = allPlayers.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating)
                .Select(ConvertToPlayer).ToList();
            if (gsw.Count < 5 || lal.Count < 5) { Assert(false, "Fixture players available"); return; }

            long slowEvents = RunGames(gsw, lal, PacePreference.Deliberate);
            ResetEnergy(gsw, lal);
            long fastEvents = RunGames(gsw, lal, PacePreference.AlwaysPush);

            Assert(fastEvents > slowEvents,
                $"AlwaysPush produces more shot events than Deliberate across seeded games ({fastEvents} vs {slowEvents})");
        }

        private long RunGames(List<Player> home, List<Player> away, PacePreference pace)
        {
            var db = new PlayerDatabase();
            foreach (var p in home.Concat(away)) db.AddPlayer(p);
            var homeTeam = MakeTeam("GSW", home, pace);
            var awayTeam = MakeTeam("LAL", away, pace);

            long events = 0;
            for (int g = 0; g < 3; g++)
            {
                var sim = new GameSimulator(db, seed: g * 13 + 7);
                var result = sim.SimulateGame(homeTeam, awayTeam);
                // Possession proxy: every possession ends in a shot, turnover, or FTs.
                events += result.BoxScore.PlayerStats.Values.Sum(st =>
                    st.FieldGoalAttempts + st.ThreePointAttempts + st.Turnovers);
                ResetEnergy(home, away);
            }
            return events;
        }

        private Team MakeTeam(string id, List<Player> roster, PacePreference pace)
        {
            var team = new Team
            {
                TeamId = id, City = id, Nickname = id,
                RosterPlayerIds = roster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateDefault(id),
                DefensiveStrategy = TeamStrategy.CreateDefault(id)
            };
            for (int i = 0; i < 5; i++) team.StartingLineupIds[i] = roster[i].PlayerId;
            team.OffensiveStrategy.ApplyPacePreference(pace);
            return team;
        }

        private void ResetEnergy(List<Player> a, List<Player> b)
        {
            foreach (var p in a.Concat(b)) p.Energy = 100f;
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
