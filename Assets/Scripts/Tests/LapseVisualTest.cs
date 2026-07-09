using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the lapse VISUALS: what the outcome layer decided is what the dots
    /// draw. A BlownRotation culprit is caught flat-footed in the run-up to the
    /// shot while his teammates keep moving, and lapse facts survive onto the
    /// possession result in full-detail playback.
    /// </summary>
    public class LapseVisualTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            TestBlownRotationFreezesTheCulprit();
            TestLapseNarrationBeats();
            return (_passed, _failed);
        }

        private Player MakePlayer(string id, int overall)
        {
            return new Player
            {
                PlayerId = id, FirstName = "V", LastName = id,
                Position = Position.SmallForward, BirthDate = System.DateTime.Now.AddYears(-26),
                DefensiveIQ = overall, BasketballIQ = overall, Consistency = 50,
                Shot_Close = 70, Shot_MidRange = 70, Shot_Three = 70, Finishing_Rim = 70,
                FreeThrow = 70, Passing = 70, BallHandling = 70, OffensiveIQ = 70,
                SpeedWithBall = 70, Defense_Perimeter = 65, Defense_Interior = 65,
                Steal = 60, Block = 60, Speed = 70, Acceleration = 70, Strength = 70,
                Vertical = 70, Stamina = 80, DefensiveRebound = 70, Clutch = 60,
                Composure = 60, Energy = 100, Morale = 75, Form = 60,
                Tendencies = new PlayerTendencies { CloseoutControl = 30 }
            };
        }

        private void TestBlownRotationFreezesTheCulprit()
        {
            var offense = Enumerable.Range(0, 5).Select(i => MakePlayer($"O{i}", 70)).ToArray();
            var defense = Enumerable.Range(0, 5).Select(i => MakePlayer($"X{i}", 40)).ToArray();
            var off = TeamStrategy.CreateDefault("GSW");
            var def = TeamStrategy.CreateDefault("LAL");
            def.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.Zone2_3;

            var sim = new PossessionSimulator(31337) { SpatialDetail = SpatialDetailLevel.Full };
            int verified = 0, factChecked = 0;

            for (int n = 0; n < 600 && verified < 4; n++)
            {
                foreach (var p in offense.Concat(defense)) p.Energy = 100f;
                var r = sim.SimulatePossession(offense, defense, off, def,
                    600f, 2, true, "GSW", "LAL", 0, false);

                if (r.Lapse != LapseType.None && r.SpatialStates != null && r.SpatialStates.Count > 0)
                    factChecked++;

                if (r.Lapse != LapseType.BlownRotation || r.WasFastBreak) continue;
                if (r.SpatialStates == null || r.SpatialStates.Count < 10) continue;

                // Find the shot window: last stretch of the Shot phase run-up.
                var states = r.SpatialStates;
                int shotIdx = states.FindIndex(s => s.Phase == PossessionPhase.Shot);
                if (shotIdx < 8) continue;

                int culprit = 5 + r.LapseDefenderIndex;
                int windowStart = Mathf.Max(1, shotIdx - 6);

                float culpritMove = 0f;
                var othersMove = new float[5];
                for (int s = windowStart; s <= shotIdx; s++)
                {
                    for (int d = 5; d < 10; d++)
                    {
                        float dx = states[s].Players[d].X - states[s - 1].Players[d].X;
                        float dy = states[s].Players[d].Y - states[s - 1].Players[d].Y;
                        float step = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d == culprit) culpritMove += step;
                        else othersMove[d - 5] += step;
                    }
                }
                float maxOther = othersMove.Max();

                verified++;
                Assert(culpritMove < maxOther,
                    $"Blown-rotation culprit is the flat-footed one ({culpritMove:F1}ft vs busiest teammate {maxOther:F1}ft)");
            }

            Assert(verified >= 2, $"Blown-rotation possessions found and verified ({verified})");
            Assert(factChecked > 0, "Lapse facts reach the possession result in full detail");
        }

        private void TestLapseNarrationBeats()
        {
            var offense = Enumerable.Range(0, 5).Select(i => MakePlayer($"O{i}", 70)).ToArray();
            var defense = Enumerable.Range(0, 5).Select(i => MakePlayer($"X{i}", 40)).ToArray();
            var off = TeamStrategy.CreateDefault("GSW");
            var def = TeamStrategy.CreateDefault("LAL");
            def.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.Zone1_3_1;

            var lapseKinds = new[]
            {
                NarrationBeatKind.LateCloseout, NarrationBeatKind.BlownRotation,
                NarrationBeatKind.MissedHelp, NarrationBeatKind.HeroBall
            };

            var sim = new PossessionSimulator(60606) { SpatialDetail = SpatialDetailLevel.Full };
            int narrated = 0, cleanChecked = 0;
            bool cleanStaysQuiet = true;

            for (int n = 0; n < 500 && (narrated < 3 || cleanChecked < 20); n++)
            {
                foreach (var p in offense.Concat(defense)) p.Energy = 100f;
                var r = sim.SimulatePossession(offense, defense, off, def,
                    600f, 2, true, "GSW", "LAL", 0, false);
                if (r.NarrationBeats == null) continue;

                bool hasShot = r.Events.Any(e => e.Type == NBAHeadCoach.Core.Simulation.EventType.Shot);
                bool hasLapseBeat = r.NarrationBeats.Any(b => lapseKinds.Contains(b.Kind));

                if (r.Lapse != LapseType.None && hasShot && hasLapseBeat) narrated++;
                if (r.Lapse == LapseType.None && r.Deviation == OffensiveDeviation.None)
                {
                    cleanChecked++;
                    if (hasLapseBeat) cleanStaysQuiet = false;
                }
            }

            Assert(narrated >= 3, $"Lapse possessions with shots emit a narration beat ({narrated})");
            Assert(cleanStaysQuiet, "Clean possessions never narrate a lapse");
            Assert(cleanChecked >= 20, $"Enough clean possessions checked ({cleanChecked})");
        }
    }
}
