using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;
using EventType = NBAHeadCoach.Core.Simulation.EventType;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards offensive execution variance: selfish low-IQ gunners hijack the
    /// called system more than disciplined high-IQ teams, a HeroBall possession
    /// really is an iso by the deviator (shooter, handler, and drawn action all
    /// agree), and it's all seed-deterministic.
    /// </summary>
    public class OffenseDeviationTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestPropensityShape();
            TestDeviationRateMonotonic();
            TestHeroBallDrawsTheIso();
            TestDeterminism();

            return (_passed, _failed);
        }

        private Player MakePlayer(string id, int iq, int ballMovement = 0, int risk = 0)
        {
            return new Player
            {
                PlayerId = id, FirstName = "H", LastName = id,
                Position = Position.ShootingGuard, BirthDate = System.DateTime.Now.AddYears(-26),
                BasketballIQ = iq, OffensiveIQ = iq, DefensiveIQ = 70, Consistency = 70,
                Shot_Close = 72, Shot_MidRange = 72, Shot_Three = 72, Finishing_Rim = 72,
                FreeThrow = 72, Passing = 70, BallHandling = 72, SpeedWithBall = 70,
                Defense_Perimeter = 70, Defense_Interior = 70, Steal = 60, Block = 60,
                Speed = 70, Acceleration = 70, Strength = 70, Vertical = 70, Stamina = 80,
                DefensiveRebound = 70, Clutch = 60, Composure = 60,
                Energy = 100, Morale = 75, Form = 60,
                Tendencies = new PlayerTendencies { BallMovement = ballMovement, RiskTolerance = risk }
            };
        }

        private void TestPropensityShape()
        {
            var disciplined = MakePlayer("D", 90, ballMovement: 60, risk: -40);
            var gunner = MakePlayer("G", 42, ballMovement: -70, risk: 70);
            Assert(ExecutionVariance.DeviationPropensity(gunner) >
                   ExecutionVariance.DeviationPropensity(disciplined) * 2,
                "Selfish low-IQ gunners hijack plays far more than disciplined vets");
        }

        private void TestDeviationRateMonotonic()
        {
            var smart = Enumerable.Range(0, 5).Select(i => MakePlayer($"S{i}", 88, 50, -30)).ToArray();
            var chaos = Enumerable.Range(0, 5).Select(i => MakePlayer($"C{i}", 45, -60, 60)).ToArray();

            int Count(Player[] lineup, int seed)
            {
                var rng = new System.Random(seed);
                int n = 0;
                for (int i = 0; i < 4000; i++)
                    if (ExecutionVariance.RollOffensiveDeviation(lineup, 1f, rng).type !=
                        OffensiveDeviation.None) n++;
                return n;
            }

            int smartDevs = Count(smart, 55);
            int chaosDevs = Count(chaos, 55);
            Assert(chaosDevs > smartDevs * 2,
                $"Chaotic lineups deviate far more ({chaosDevs} vs {smartDevs} per 4000)");
            Assert(chaosDevs < 4000 / 5, "Even chaos teams mostly run the offense");
        }

        private void TestHeroBallDrawsTheIso()
        {
            var offense = Enumerable.Range(0, 5).Select(i => MakePlayer($"O{i}", 45, -60, 60)).ToArray();
            var defense = Enumerable.Range(0, 5).Select(i => MakePlayer($"X{i}", 70)).ToArray();
            var off = TeamStrategy.CreateDefault("GSW");
            var def = TeamStrategy.CreateDefault("LAL");

            var sim = new PossessionSimulator(9090) { SpatialDetail = SpatialDetailLevel.Full };
            int heroChecked = 0;

            for (int n = 0; n < 400 && heroChecked < 5; n++)
            {
                foreach (var p in offense.Concat(defense)) p.Energy = 100f;
                var r = sim.SimulatePossession(offense, defense, off, def,
                    600f, 2, true, "GSW", "LAL", 0, false);
                if (r.Deviation != OffensiveDeviation.HeroBall) continue;

                var shot = r.Events.FirstOrDefault(e => e.Type == EventType.Shot);
                if (shot == null) continue;   // hero-ball trip ended in a turnover/foul
                heroChecked++;

                Assert(shot.ActorPlayerId == offense[r.OffDeviatorIndex].PlayerId,
                    "The deviator takes the hero-ball shot");
                AssertEqual(HalfCourtAction.Isolation, r.Action,
                    "Hero ball runs as an isolation");
            }
            Assert(heroChecked >= 3, $"Hero-ball possessions occurred and were verified ({heroChecked})");
        }

        private void TestDeterminism()
        {
            var lineup = Enumerable.Range(0, 5).Select(i => MakePlayer($"P{i}", 55, -20, 20)).ToArray();
            var a = new System.Random(77);
            var b = new System.Random(77);
            bool same = true;
            for (int n = 0; n < 500; n++)
            {
                var ra = ExecutionVariance.RollOffensiveDeviation(lineup, 1f, a);
                var rb = ExecutionVariance.RollOffensiveDeviation(lineup, 1f, b);
                same &= ra.type == rb.type && ra.deviatorIndex == rb.deviatorIndex;
            }
            Assert(same, "Same seed produces the same deviations");
        }
    }
}
