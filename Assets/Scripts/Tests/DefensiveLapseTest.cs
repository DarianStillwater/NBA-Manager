using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using EventType = NBAHeadCoach.Core.Simulation.EventType;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the defensive lapse system: schemes are executed by players, not
    /// spreadsheets. Low-IQ / undisciplined / tired defenders blow assignments
    /// more often, complex schemes are harder to run than standard man, lapses
    /// genuinely loosen the shooter's look (lower contest, better FG%), and the
    /// whole thing is seed-deterministic.
    /// </summary>
    public class DefensiveLapseTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestPropensityShape();
            TestLapseRateMonotonicInIQ();
            TestSchemeComplexity();
            TestLapsesLoosenTheLook();
            TestDeterminism();

            return (_passed, _failed);
        }

        private Player MakeDefender(string id, int iq, int consistency = 70, int closeout = 60, float energy = 100f)
        {
            return new Player
            {
                PlayerId = id, FirstName = "L", LastName = id,
                Position = Position.SmallForward, BirthDate = System.DateTime.Now.AddYears(-27),
                DefensiveIQ = iq, Consistency = consistency,
                Defense_Perimeter = 70, Defense_Interior = 70, Steal = 60, Block = 60,
                Speed = 70, Acceleration = 70, Strength = 70, Vertical = 70, Stamina = 80,
                BasketballIQ = iq, Shot_Close = 70, Shot_MidRange = 70, Shot_Three = 70,
                Finishing_Rim = 70, FreeThrow = 70, Passing = 70, BallHandling = 70,
                OffensiveIQ = 70, SpeedWithBall = 70, DefensiveRebound = 70,
                Clutch = 60, Composure = 60, Energy = energy, Morale = 75, Form = 60,
                Tendencies = new PlayerTendencies { CloseoutControl = closeout }
            };
        }

        private void TestPropensityShape()
        {
            var elite = MakeDefender("E", 92, 88, 85);
            var poor = MakeDefender("P", 40, 45, 30);
            var tired = MakeDefender("T", 40, 45, 30, energy: 40f);

            Assert(ExecutionVariance.LapsePropensity(elite) < ExecutionVariance.LapsePropensity(poor),
                "Poor defenders are more lapse-prone than elite ones");
            Assert(ExecutionVariance.LapsePropensity(tired) >= ExecutionVariance.LapsePropensity(poor),
                "Tired legs lapse at least as much");
            Assert(ExecutionVariance.LapsePropensity(elite) >= 0.002f &&
                   ExecutionVariance.LapsePropensity(poor) <= 0.045f,
                "Propensity stays within calibrated bounds");
        }

        private void TestLapseRateMonotonicInIQ()
        {
            var elite = Enumerable.Range(0, 5).Select(i => MakeDefender($"E{i}", 90, 85, 80)).ToArray();
            var poor = Enumerable.Range(0, 5).Select(i => MakeDefender($"P{i}", 42, 45, 30)).ToArray();

            int Count(Player[] lineup, int seed)
            {
                var rng = new System.Random(seed);
                int lapses = 0;
                for (int n = 0; n < 4000; n++)
                    if (ExecutionVariance.RollDefensiveLapse(lineup,
                        DefensiveSchemeType.Zone2_3, 1f, rng).type != LapseType.None) lapses++;
                return lapses;
            }

            int eliteLapses = Count(elite, 99);
            int poorLapses = Count(poor, 99);
            Assert(poorLapses > eliteLapses * 2,
                $"Low-IQ lineups blow far more assignments ({poorLapses} vs {eliteLapses} per 4000)");
            Assert(eliteLapses > 0, "Even elite lineups aren't perfect");
            Assert(poorLapses < 4000 * 0.25f, "Lapses stay occasional, not constant");
        }

        private void TestSchemeComplexity()
        {
            Assert(ExecutionVariance.SchemeComplexity(DefensiveSchemeType.Zone1_3_1) >
                   ExecutionVariance.SchemeComplexity(DefensiveSchemeType.ManToManStandard),
                "Zones are harder to execute than standard man");
            Assert(ExecutionVariance.SchemeComplexity(DefensiveSchemeType.BoxAndOne) >
                   ExecutionVariance.SchemeComplexity(DefensiveSchemeType.Zone2_3),
                "Junk defenses are the hardest to run");
            AssertEqual(1.0f, ExecutionVariance.SchemeComplexity(DefensiveSchemeType.ManToManStandard),
                "Standard man is the execution baseline");
        }

        private void TestLapsesLoosenTheLook()
        {
            var offense = Enumerable.Range(0, 5).Select(i => MakeDefender($"O{i}", 70)).ToArray();
            var defense = Enumerable.Range(0, 5).Select(i => MakeDefender($"D{i}", 45, 45, 35)).ToArray();
            var off = TeamStrategy.CreateDefault("GSW");
            var def = TeamStrategy.CreateDefault("LAL");
            def.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.Zone1_3_1;

            var sim = new PossessionSimulator(4242);
            float lapseContest = 0f, cleanContest = 0f;
            int lapseShots = 0, cleanShots = 0;

            for (int n = 0; n < 2500; n++)
            {
                if (n % 50 == 0) foreach (var p in offense.Concat(defense)) p.Energy = 100f;
                var r = sim.SimulatePossession(offense, defense, off, def,
                    600f, 2, true, "GSW", "LAL", 0, false);

                var shot = r.Events.FirstOrDefault(e => e.Type == EventType.Shot);
                if (shot == null) continue;
                bool coupledLapse = r.Lapse == LapseType.LateCloseout || r.Lapse == LapseType.BlownRotation;
                if (coupledLapse) { lapseContest += shot.ContestLevel; lapseShots++; }
                else if (r.Lapse == LapseType.None) { cleanContest += shot.ContestLevel; cleanShots++; }
            }

            Assert(lapseShots >= 25, $"Enough lapse shots sampled ({lapseShots})");
            float avgLapse = lapseContest / Mathf.Max(1, lapseShots);
            float avgClean = cleanContest / Mathf.Max(1, cleanShots);
            Assert(avgLapse < avgClean - 0.05f,
                $"Lapse possessions produce visibly cleaner looks (contest {avgLapse:F2} vs {avgClean:F2})");
        }

        private void TestDeterminism()
        {
            var lineup = Enumerable.Range(0, 5).Select(i => MakeDefender($"D{i}", 55)).ToArray();
            var a = new System.Random(31);
            var b = new System.Random(31);
            for (int n = 0; n < 500; n++)
            {
                var ra = ExecutionVariance.RollDefensiveLapse(lineup, DefensiveSchemeType.Zone2_3, 1f, a);
                var rb = ExecutionVariance.RollDefensiveLapse(lineup, DefensiveSchemeType.Zone2_3, 1f, b);
                if (ra.type != rb.type || ra.defenderIndex != rb.defenderIndex)
                {
                    Assert(false, "Same seed produces the same lapses");
                    return;
                }
            }
            Assert(true, "Same seed produces the same lapses");
        }
    }
}
