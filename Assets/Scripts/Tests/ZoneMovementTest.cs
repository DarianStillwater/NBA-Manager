using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the organic-zone integration: zone defenders in full-detail
    /// playback no longer slide in synchronized formation — roles and per-player
    /// execution params (reaction delay from DefensiveIQ, speed from
    /// Speed/Acceleration, closeout tightness) produce distinct movement, the
    /// ball gets pressured, the weak side sinks, and the per-tick movement cap
    /// still holds.
    /// </summary>
    public class ZoneMovementTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestExecutionParamsFollowAttributes();
            TestZonePossessionMovement();

            return (_passed, _failed);
        }

        private Player MakePlayer(string id, int overall)
        {
            return new Player
            {
                PlayerId = id, FirstName = "Z", LastName = id,
                Position = Position.ShootingGuard, BirthDate = System.DateTime.Now.AddYears(-26),
                Shot_Close = overall, Shot_MidRange = overall, Shot_Three = overall,
                Finishing_Rim = overall, FreeThrow = overall, Passing = overall,
                BallHandling = overall, OffensiveIQ = overall, SpeedWithBall = overall,
                Defense_Perimeter = overall, Defense_Interior = overall, Steal = overall,
                Block = overall, DefensiveIQ = overall, DefensiveRebound = overall,
                Speed = overall, Acceleration = overall, Strength = overall,
                Vertical = overall, Stamina = 80, BasketballIQ = overall,
                Clutch = overall, Composure = overall, Aggression = 50,
                Energy = 100, Morale = 75, Form = 60
            };
        }

        private void TestExecutionParamsFollowAttributes()
        {
            var smart = Enumerable.Range(0, 5).Select(i => MakePlayer($"S{i}", 90)).ToArray();
            var dull = Enumerable.Range(0, 5).Select(i => MakePlayer($"D{i}", 42)).ToArray();

            var smartScript = new PossessionScript { OffenseAttacksRight = true, Defense = smart };
            var dullScript = new PossessionScript { OffenseAttacksRight = true, Defense = dull };

            var smartPlan = DefenseChoreographer.BuildPlan(null, smartScript,
                new System.Random(11), DefensiveSchemeType.Zone2_3);
            var dullPlan = DefenseChoreographer.BuildPlan(null, dullScript,
                new System.Random(11), DefensiveSchemeType.Zone2_3);

            for (int i = 0; i < 5; i++)
            {
                Assert(smartPlan[i].ReactDelay < dullPlan[i].ReactDelay,
                    $"High DefensiveIQ reacts earlier than low (def {i})");
                Assert(smartPlan[i].SpeedMult > dullPlan[i].SpeedMult,
                    $"Fast players rotate faster (def {i})");
                Assert(smartPlan[i].CloseoutTightness > dullPlan[i].CloseoutTightness,
                    $"Disciplined defenders close out tighter (def {i})");
                Assert(smartPlan[i].SpeedMult <= 1.15f && dullPlan[i].SpeedMult >= 0.85f,
                    $"Speed multiplier bounded (def {i})");
                Assert(smartPlan[i].ReactDelay >= -0.25f && dullPlan[i].ReactDelay <= 0.7f,
                    $"Reaction delay in sane range (def {i})");
            }
            Assert(smartPlan[0].ReactDelay < 0.05f, "Elite IQ anticipates (≈on the release)");
        }

        private void TestZonePossessionMovement()
        {
            var offense = Enumerable.Range(0, 5).Select(i => MakePlayer($"O{i}", 75)).ToArray();
            // Mixed defense so execution params differ per defender
            int[] ratings = { 88, 45, 70, 60, 80 };
            var defense = Enumerable.Range(0, 5).Select(i => MakePlayer($"X{i}", ratings[i])).ToArray();

            var off = TeamStrategy.CreateDefault("GSW");
            var def = TeamStrategy.CreateDefault("LAL");
            def.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.Zone2_3;

            var sim = new PossessionSimulator(777) { SpatialDetail = SpatialDetailLevel.Full };

            int analyzed = 0, pressured = 0, sank = 0, desynced = 0;
            float maxStep = 0f;

            for (int n = 0; n < 30; n++)
            {
                foreach (var p in offense.Concat(defense)) p.Energy = 100f;
                var result = sim.SimulatePossession(offense, defense, off, def,
                    600f, 2, true, "GSW", "LAL", 0, false);
                if (result.WasFastBreak || result.SpatialStates == null ||
                    result.SpatialStates.Count < 6) continue;
                analyzed++;

                var states = result.SpatialStates;
                bool thisPressured = false, thisSank = false, thisDesynced = false;

                for (int s = 1; s < states.Count; s++)
                {
                    var prev = states[s - 1];
                    var cur = states[s];
                    float minStep = float.MaxValue, maxStepHere = 0f;

                    for (int d = 5; d < 10; d++)
                    {
                        float dx = cur.Players[d].X - prev.Players[d].X;
                        float dy = cur.Players[d].Y - prev.Players[d].Y;
                        float step = Mathf.Sqrt(dx * dx + dy * dy);
                        if (step > maxStep) maxStep = step;
                        if (step < minStep) minStep = step;
                        if (step > maxStepHere) maxStepHere = step;

                        // pressure: some defender near the held ball
                        if (cur.Ball.Status == BallStatus.Held)
                        {
                            float bx = cur.Players[d].X - cur.Ball.X;
                            float by = cur.Players[d].Y - cur.Ball.Y;
                            if (Mathf.Sqrt(bx * bx + by * by) < 4.5f) thisPressured = true;
                        }
                        // weak-side sink: someone in the paint band
                        if (Mathf.Abs(cur.Players[d].X - 41.75f) < 14f &&
                            Mathf.Abs(cur.Players[d].Y) < 8f) thisSank = true;
                    }
                    // desync: in one tick, one defender moves while another stands
                    if (maxStepHere > 0.8f && minStep < 0.25f) thisDesynced = true;
                }

                if (thisPressured) pressured++;
                if (thisSank) sank++;
                if (thisDesynced) desynced++;
            }

            Assert(analyzed >= 10, $"Enough half-court zone possessions analyzed ({analyzed})");
            Assert(maxStep <= 8f, $"Per-tick defender movement stays under the 8ft cap ({maxStep:F1})");
            Assert(pressured >= analyzed / 2,
                $"The ball gets pressured in most zone possessions ({pressured}/{analyzed})");
            Assert(sank >= analyzed / 2,
                $"Somebody protects the paint in most possessions ({sank}/{analyzed})");
            Assert(desynced >= analyzed / 2,
                $"Defenders move on their own timing, not in sync ({desynced}/{analyzed})");
        }
    }
}
