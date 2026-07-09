using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using EventType = NBAHeadCoach.Core.Simulation.EventType;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 5-C: the EndOfGameStrategy struct finally plays basketball —
    /// run the clock with a lead, hurry a deficit, hold for the last shot, give
    /// intentional fouls (up 3 or trailing, only in the bonus), feed the closer,
    /// and chase threes when down late.
    /// </summary>
    public class EndOfGameTest : BaseTest
    {
        private Player[] _offense;
        private Player[] _defense;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(7272);

            BuildLineups();

            TestRunClockWithLead();
            TestTrailingUrgency();
            TestHoldForLastShot();
            TestIntentionalFoulWhenTrailing();
            TestFoulUpThree();
            TestGoToPlayerGetsFed();
            TestChasingThrees();

            return (_passed, _failed);
        }

        private void BuildLineups()
        {
            var all = NBAPlayerData.GetAllPlayers();
            _offense = all.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating)
                .Take(5).Select(Convert).ToArray();
            _defense = all.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating)
                .Take(5).Select(Convert).ToArray();
        }

        private Player Convert(PlayerData d)
        {
            return new Player
            {
                PlayerId = d.PlayerId, FirstName = d.FirstName, LastName = d.LastName,
                Position = (Position)d.Position, BirthDate = System.DateTime.Now.AddYears(-d.Age),
                TeamId = d.TeamId, Finishing_Rim = d.Finishing_Rim,
                Finishing_PostMoves = d.Finishing_PostMoves, Shot_Close = d.Shot_Close,
                Shot_MidRange = d.Shot_MidRange, Shot_Three = d.Shot_Three, FreeThrow = d.FreeThrow,
                Passing = d.Passing, BallHandling = d.BallHandling, OffensiveIQ = d.OffensiveIQ,
                SpeedWithBall = d.SpeedWithBall, Defense_Perimeter = d.Defense_Perimeter,
                Defense_Interior = d.Defense_Interior, Steal = d.Steal, Block = d.Block,
                DefensiveIQ = d.DefensiveIQ, DefensiveRebound = d.DefensiveRebound,
                Speed = d.Speed, Strength = d.Strength, Stamina = d.Stamina,
                BasketballIQ = d.BasketballIQ, Clutch = d.Clutch, Composure = d.Composure,
                Aggression = d.Aggression, Energy = 100, Morale = 75, Form = 70
            };
        }

        private void ResetEnergy()
        {
            foreach (var p in _offense.Concat(_defense)) p.Energy = 100f;
        }

        private void TestRunClockWithLead()
        {
            var off = TeamStrategy.CreateDefault("GSW"); // RunClockWhenAhead default true, lead 10
            var def = TeamStrategy.CreateDefault("LAL");

            float milkTotal = 0f, neutralTotal = 0f;
            var sim = new PossessionSimulator(11);
            for (int i = 0; i < 200; i++)
            {
                if (i % 50 == 0) ResetEnergy();
                // Q4, 2:30 left, up 12
                var up = sim.SimulatePossession(_offense, _defense, off, def, 150f, 4, true, "GSW", "LAL", 12);
                milkTotal += up.StartGameClock - up.EndGameClock;
                // Same spot, tied game
                var tied = sim.SimulatePossession(_offense, _defense, off, def, 150f, 4, true, "GSW", "LAL", 0);
                neutralTotal += tied.StartGameClock - tied.EndGameClock;
            }

            float milkAvg = milkTotal / 200f, neutralAvg = neutralTotal / 200f;
            AssertGreaterThan(milkAvg, 19f, $"Leading team milks the clock ({milkAvg:F1}s avg)");
            AssertGreaterThan(milkAvg, neutralAvg + 4f,
                $"Run-clock beats neutral tempo ({milkAvg:F1}s vs {neutralAvg:F1}s)");
        }

        private void TestTrailingUrgency()
        {
            var off = TeamStrategy.CreateDefault("GSW");
            var def = TeamStrategy.CreateDefault("LAL");

            float total = 0f;
            var sim = new PossessionSimulator(22);
            for (int i = 0; i < 200; i++)
            {
                if (i % 50 == 0) ResetEnergy();
                // Q4, 50 seconds left, down 8
                var r = sim.SimulatePossession(_offense, _defense, off, def, 50f, 4, true, "GSW", "LAL", -8);
                total += r.StartGameClock - r.EndGameClock;
            }

            float avg = total / 200f;
            AssertLessThan(avg, 9f, $"Trailing team hurries ({avg:F1}s avg)");
        }

        private void TestHoldForLastShot()
        {
            var off = TeamStrategy.CreateDefault("GSW"); // TargetLastShotClock default 4
            var def = TeamStrategy.CreateDefault("LAL");

            int heldCount = 0;
            var sim = new PossessionSimulator(33);
            for (int i = 0; i < 200; i++)
            {
                if (i % 50 == 0) ResetEnergy();
                // 26 seconds left in Q2, tied — kill the quarter
                var r = sim.SimulatePossession(_offense, _defense, off, def, 26f, 2, true, "GSW", "LAL", 0);
                if (r.EndGameClock <= 7f) heldCount++;
            }

            AssertGreaterThan(heldCount, 170,
                $"Offense holds for the quarter's last shot ({heldCount}/200 ended under 7s)");
        }

        private void TestIntentionalFoulWhenTrailing()
        {
            var off = TeamStrategy.CreateDefault("GSW");
            var def = TeamStrategy.CreateDefault("LAL"); // IntentionalFoulWhenDown default true

            var sim = new PossessionSimulator(44);
            // Defense has 4 team fouls — the give-foul puts them in the bonus
            for (int f = 0; f < 4; f++) sim.FoulSystem.AddTeamFoul("LAL");

            ResetEnergy();
            // Q4, 30 seconds left, offense up 5 (defense trailing, must foul)
            var r = sim.SimulatePossession(_offense, _defense, off, def, 30f, 4, true, "GSW", "LAL", 5);

            float duration = r.StartGameClock - r.EndGameClock;
            AssertLessThan(duration, 4f, $"Intentional foul comes fast ({duration:F1}s)");
            Assert(r.Events.Any(e => e.Type == EventType.Foul), "A foul was given");
            Assert(r.Events.Any(e => e.Type == EventType.FreeThrow),
                "Bonus free throws follow the deliberate foul");
            Assert(r.Outcome == PossessionOutcome.Foul || r.Outcome == PossessionOutcome.Score,
                "Possession resolves at the line");

            // Same spot but the defense is NOT in the bonus: no free foul mechanic
            var sim2 = new PossessionSimulator(44);
            ResetEnergy();
            var r2 = sim2.SimulatePossession(_offense, _defense, off, def, 30f, 4, true, "GSW", "LAL", 5);
            float duration2 = r2.StartGameClock - r2.EndGameClock;
            Assert(duration2 > 4f || !r2.Events.Any(e => e.Type == EventType.Foul),
                $"No give-foul outside the bonus ({duration2:F1}s)");
        }

        private void TestFoulUpThree()
        {
            var off = TeamStrategy.CreateDefault("GSW");

            var fouling = TeamStrategy.CreateDefault("LAL"); // FoulWhenDown3 default true
            var playing = TeamStrategy.CreateDefault("LAL");
            playing.CloseGameStrategy.FoulWhenDown3 = false;

            // Defense up 3, 15 seconds left, in the bonus territory
            var simA = new PossessionSimulator(55);
            for (int f = 0; f < 4; f++) simA.FoulSystem.AddTeamFoul("LAL");
            ResetEnergy();
            var withFoul = simA.SimulatePossession(_offense, _defense, off, fouling, 15f, 4, true, "GSW", "LAL", -3);

            float durA = withFoul.StartGameClock - withFoul.EndGameClock;
            AssertLessThan(durA, 4f, $"Up 3, the defense gives the foul ({durA:F1}s)");
            Assert(withFoul.Events.Any(e => e.Type == EventType.Foul), "Foul denies the tying three");

            var simB = new PossessionSimulator(55);
            for (int f = 0; f < 4; f++) simB.FoulSystem.AddTeamFoul("LAL");
            ResetEnergy();
            var straightUp = simB.SimulatePossession(_offense, _defense, off, playing, 15f, 4, true, "GSW", "LAL", -3);
            float durB = straightUp.StartGameClock - straightUp.EndGameClock;
            AssertGreaterThan(durB, 3.5f, $"Coach who trusts the defense plays it out ({durB:F1}s)");
        }

        private void TestGoToPlayerGetsFed()
        {
            var def = TeamStrategy.CreateDefault("LAL");

            // The closer: the weakest shooter, so the bias has to fight the talent weighting
            var closer = _offense.OrderBy(p => (p.Shot_Three + p.Shot_MidRange + p.Finishing_Rim) / 3f).First();

            var featuring = TeamStrategy.CreateDefault("GSW");
            featuring.CloseGameStrategy.GoToPlayerId = closer.PlayerId;

            var normal = TeamStrategy.CreateDefault("GSW");

            int fedShots = CountShotsBy(featuring, def, closer.PlayerId, 66);
            int normalShots = CountShotsBy(normal, def, closer.PlayerId, 66);

            AssertGreaterThan(fedShots, normalShots + 10,
                $"The designated closer gets fed in clutch time ({fedShots} vs {normalShots})");
        }

        private int CountShotsBy(TeamStrategy off, TeamStrategy def, string playerId, int seed)
        {
            int shots = 0;
            var sim = new PossessionSimulator(seed);
            for (int i = 0; i < 400; i++)
            {
                if (i % 50 == 0) ResetEnergy();
                // Q4, 3 minutes left, down 2 — clutch context
                var r = sim.SimulatePossession(_offense, _defense, off, def, 180f, 4, true, "GSW", "LAL", -2);
                shots += r.Events.Count(e => (e.Type == EventType.Shot || e.Type == EventType.Block) &&
                                             e.ActorPlayerId == playerId);
            }
            return shots;
        }

        private void TestChasingThrees()
        {
            var off = TeamStrategy.CreateDefault("GSW");
            var def = TeamStrategy.CreateDefault("LAL");

            int desperateThrees = CountThrees(off, def, 25f, -4, 77);
            int neutralThrees = CountThrees(off, def, 400f, 0, 77);

            AssertGreaterThan(desperateThrees, neutralThrees + 15,
                $"Down 4 with 25s left, the offense hunts threes ({desperateThrees} vs {neutralThrees})");
        }

        private int CountThrees(TeamStrategy off, TeamStrategy def, float clock, int diff, int seed)
        {
            int threes = 0;
            var sim = new PossessionSimulator(seed);
            for (int i = 0; i < 300; i++)
            {
                if (i % 50 == 0) ResetEnergy();
                var r = sim.SimulatePossession(_offense, _defense, off, def, clock, 4, true, "GSW", "LAL", diff);
                foreach (var e in r.Events)
                {
                    if (e.Type != EventType.Shot && e.Type != EventType.Block) continue;
                    if (e.ActorPosition.GetZone(true) == CourtZone.ThreePoint) threes++;
                }
            }
            return threes;
        }
    }
}
