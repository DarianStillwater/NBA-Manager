using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using EventType = NBAHeadCoach.Core.Simulation.EventType;

namespace NBAHeadCoach.Tests
{
    public class PossessionOutcomeTest : BaseTest
    {
        private const int POSSESSIONS = 1000;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var allPlayers = NBAPlayerData.GetAllPlayers();
            var gswPlayers = allPlayers.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating).Take(5).ToList();
            var lalPlayers = allPlayers.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating).Take(5).ToList();

            var playerDb = new PlayerDatabase();
            var offense = new Player[5];
            var defense = new Player[5];
            for (int i = 0; i < 5; i++)
            {
                offense[i] = ConvertPlayer(gswPlayers[i]);
                defense[i] = ConvertPlayer(lalPlayers[i]);
                playerDb.AddPlayer(offense[i]);
                playerDb.AddPlayer(defense[i]);
            }

            var offStrategy = TeamStrategy.CreateDefault("GSW");
            var defStrategy = TeamStrategy.CreateDefault("LAL");

            int scores = 0, misses = 0, turnovers = 0, blocks = 0, fouls = 0;
            int assists = 0, madeShots = 0, threeAttempts = 0;
            int totalPoints = 0;

            var sim = new PossessionSimulator(42);
            for (int i = 0; i < POSSESSIONS; i++)
            {
                // Reset energy periodically
                if (i % 50 == 0)
                    foreach (var p in offense.Concat(defense)) p.Energy = 100f;

                var result = sim.SimulatePossession(offense, defense, offStrategy, defStrategy,
                    700f - (i * 3f % 700f), (i / 50) % 4 + 1, true, "GSW", "LAL", 0);

                // Valid outcome
                Assert(result.Outcome != 0 || result.Events.Count > 0,
                    $"Poss {i}: has outcome or events");

                // Duration check (can be short near end of quarter)
                float duration = result.StartGameClock - result.EndGameClock;
                Assert(duration >= 0.5f && duration <= 26f, $"Poss {i}: duration {duration:F1}s in range");

                // Points check
                Assert(result.PointsScored >= 0 && result.PointsScored <= 4,
                    $"Poss {i}: points {result.PointsScored} in [0,4]");

                totalPoints += result.PointsScored;

                // Count outcomes
                switch (result.Outcome)
                {
                    case PossessionOutcome.Score: scores++; break;
                    case PossessionOutcome.Miss: misses++; break;
                    case PossessionOutcome.Turnover: turnovers++; break;
                    case PossessionOutcome.Block: blocks++; break;
                    case PossessionOutcome.Foul: fouls++; break;
                }

                // Count event types
                bool hasMadeShot = false;
                bool hasPass = false;
                foreach (var evt in result.Events)
                {
                    if (evt.Type == EventType.Shot && evt.Outcome == EventOutcome.Success) { madeShots++; hasMadeShot = true; }
                    if (evt.Type == EventType.Pass && evt.Outcome == EventOutcome.Success) hasPass = true;
                    if (evt.Type == EventType.Shot)
                    {
                        var zone = evt.ActorPosition.GetZone(true);
                        if (zone == CourtZone.ThreePoint) threeAttempts++;
                    }
                }
                // Only count assist if there was a pass AND a made shot
                if (hasMadeShot && hasPass) assists++;
            }

            // Statistical distribution checks
            float toRate = (float)turnovers / POSSESSIONS * 100f;
            AssertRange(toRate, 8f, 25f, $"Turnover rate {toRate:F1}%");

            float scoreRate = (float)scores / POSSESSIONS * 100f;
            AssertRange(scoreRate, 30f, 65f, $"Scoring rate {scoreRate:F1}%");

            float assistRate = madeShots > 0 ? (float)assists / madeShots * 100f : 0;
            AssertRange(assistRate, 25f, 90f, $"Assist rate on makes {assistRate:F1}%");

            float threeRate = POSSESSIONS > 0 ? (float)threeAttempts / POSSESSIONS * 100f : 0;
            AssertRange(threeRate, 10f, 60f, $"Three-point attempt rate {threeRate:F1}%");

            float ppg = (float)totalPoints / POSSESSIONS;
            AssertRange(ppg, 0.7f, 1.3f, $"Points per possession {ppg:F2}");

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
