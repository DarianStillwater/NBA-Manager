using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the scheme-familiarity window: a mid-game scheme change raises the
    /// defense's lapse risk for the next ~6 possessions and then settles; no
    /// change means no elevation; smart lineups adjust faster than low-IQ ones;
    /// and the clock resets per game.
    /// </summary>
    public class SchemeFamiliarityTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestWindowElevatesThenDecays();
            TestNoChangeNoElevation();
            TestIQScalesAdjustment();
            TestResetPerGame();

            return (_passed, _failed);
        }

        private Player MakePlayer(string id, int iq)
        {
            return new Player
            {
                PlayerId = id, FirstName = "F", LastName = id,
                Position = Position.PowerForward, BirthDate = System.DateTime.Now.AddYears(-26),
                DefensiveIQ = iq, BasketballIQ = iq, Consistency = 70,
                Defense_Perimeter = 70, Defense_Interior = 70, Steal = 60, Block = 60,
                Speed = 70, Acceleration = 70, Strength = 70, Vertical = 70, Stamina = 80,
                Shot_Close = 70, Shot_MidRange = 70, Shot_Three = 70, Finishing_Rim = 70,
                FreeThrow = 70, Passing = 70, BallHandling = 70, OffensiveIQ = 70,
                SpeedWithBall = 70, DefensiveRebound = 70, Clutch = 60, Composure = 60,
                Energy = 100, Morale = 75, Form = 60
            };
        }

        private float MultOf(PossessionSimulator sim) =>
            (float)typeof(PossessionSimulator)
                .GetField("_currentDefFamiliarityMult", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sim);

        private (PossessionSimulator sim, Player[] off, Player[] def, TeamStrategy offS, TeamStrategy defS)
            Setup(int defIQ = 70)
        {
            var off = Enumerable.Range(0, 5).Select(i => MakePlayer($"O{i}", 70)).ToArray();
            var def = Enumerable.Range(0, 5).Select(i => MakePlayer($"D{i}", defIQ)).ToArray();
            var offS = TeamStrategy.CreateDefault("GSW");
            var defS = TeamStrategy.CreateDefault("LAL");
            return (new PossessionSimulator(1234), off, def, offS, defS);
        }

        private void Run(PossessionSimulator sim, Player[] off, Player[] def,
            TeamStrategy offS, TeamStrategy defS)
        {
            foreach (var p in off.Concat(def)) p.Energy = 100f;
            sim.SimulatePossession(off, def, offS, defS, 600f, 2, true, "GSW", "LAL", 0, false);
        }

        private void TestWindowElevatesThenDecays()
        {
            var (sim, off, def, offS, defS) = Setup();

            for (int i = 0; i < 10; i++) Run(sim, off, def, offS, defS);
            AssertEqual(1f, MultOf(sim), "Settled scheme carries no extra risk");

            defS.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.Zone2_3;
            Run(sim, off, def, offS, defS);
            float justChanged = MultOf(sim);
            Assert(justChanged > 1.3f, $"Fresh scheme change spikes lapse risk ({justChanged:F2})");

            float prev = justChanged;
            bool decays = true;
            for (int i = 0; i < 6; i++)
            {
                Run(sim, off, def, offS, defS);
                float m = MultOf(sim);
                if (m > prev + 0.001f) decays = false;
                prev = m;
            }
            Assert(decays, "Risk decays possession by possession");
            AssertEqual(1f, MultOf(sim), "Back to baseline after the adjustment window");
        }

        private void TestNoChangeNoElevation()
        {
            var (sim, off, def, offS, defS) = Setup();
            bool alwaysNeutral = true;
            for (int i = 0; i < 8; i++)
            {
                Run(sim, off, def, offS, defS);
                if (MultOf(sim) != 1f) alwaysNeutral = false;
            }
            Assert(alwaysNeutral, "No scheme change → no elevation (base scheme is practiced)");
        }

        private void TestIQScalesAdjustment()
        {
            var (simA, offA, defSmart, offSA, defSA) = Setup(defIQ: 95);
            var (simB, offB, defDull, offSB, defSB) = Setup(defIQ: 45);

            Run(simA, offA, defSmart, offSA, defSA);
            Run(simB, offB, defDull, offSB, defSB);
            defSA.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.Zone1_3_1;
            defSB.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.Zone1_3_1;
            Run(simA, offA, defSmart, offSA, defSA);
            Run(simB, offB, defDull, offSB, defSB);

            Assert(MultOf(simB) > MultOf(simA),
                $"Low-IQ lineups struggle longer with a new scheme ({MultOf(simB):F2} vs {MultOf(simA):F2})");
        }

        private void TestResetPerGame()
        {
            var (sim, off, def, offS, defS) = Setup();
            Run(sim, off, def, offS, defS);
            defS.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.Zone3_2;
            Run(sim, off, def, offS, defS);
            Assert(MultOf(sim) > 1f, "Change registers before reset");

            sim.ResetGameState();
            Run(sim, off, def, offS, defS);
            AssertEqual(1f, MultOf(sim),
                "New game: whatever scheme you start with counts as practiced");
        }
    }
}
