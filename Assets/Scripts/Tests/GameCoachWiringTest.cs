using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;
using NBAHeadCoach.Core.Simulation;
using DefensiveScheme = NBAHeadCoach.Core.Gameplay.DefensiveScheme;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 5-D: GameCoach carries a real playbook and player
    /// instructions, sideline tactic changes write through to the live
    /// TeamStrategy the possession engine reads, and play calls force the
    /// next possession's action.
    /// </summary>
    public class GameCoachWiringTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(8383);

            TestPlaybookAndPlayCalls();
            TestTacticsWriteThrough();
            TestCalledActionForcesPossession();

            return (_passed, _failed);
        }

        private Player MakePlayer(string id, Position pos)
        {
            return new Player
            {
                PlayerId = id, FirstName = "P", LastName = id, Position = pos,
                BirthDate = System.DateTime.Now.AddYears(-27), TeamId = "TST",
                Finishing_Rim = 70, Shot_Close = 65, Shot_MidRange = 65, Shot_Three = 60,
                FreeThrow = 75, Passing = 65, BallHandling = 65, OffensiveIQ = 65,
                Defense_Perimeter = 60, Defense_Interior = 60, Steal = 55, Block = 50,
                DefensiveIQ = 60, DefensiveRebound = 60, Speed = 65, Strength = 60,
                Stamina = 70, BasketballIQ = 65, Clutch = 60, Composure = 60,
                Aggression = 50, Energy = 100, Morale = 75, Form = 50
            };
        }

        private void TestPlaybookAndPlayCalls()
        {
            var strategy = TeamStrategy.CreateDefault("TST");
            var players = new List<Player>
            {
                MakePlayer("t1", Position.PointGuard), MakePlayer("t2", Position.ShootingGuard),
                MakePlayer("t3", Position.SmallForward), MakePlayer("t4", Position.PowerForward),
                MakePlayer("t5", Position.Center)
            };

            var playbook = PlayBook.CreateDefault("TST");
            var instructions = TeamGameInstructions.CreateDefault("TST", players);
            var coach = new GameCoach(strategy, playbook, instructions);

            Assert(playbook.Plays.Count > 0, $"Default playbook has plays ({playbook.Plays.Count})");

            var play = coach.CallSetPlay(playbook.Plays[0].PlayId);
            Assert(play != null, "Calling a set play returns the play (playbook is loaded)");
            Assert(coach.PendingActionCall.HasValue, "A set play queues an action for the sim");

            var consumed = coach.ConsumePendingActionCall();
            Assert(consumed.HasValue, "The queued action can be consumed");
            Assert(!coach.PendingActionCall.HasValue, "Consumption clears the call (one use)");

            coach.CallQuickAction(QuickActionType.PickAndRoll);
            AssertEqual(HalfCourtAction.PickAndRoll, coach.ConsumePendingActionCall() ?? HalfCourtAction.Motion,
                "Quick PnR call queues a PnR possession");

            coach.CallQuickAction(QuickActionType.Isolation);
            AssertEqual(HalfCourtAction.Isolation, coach.ConsumePendingActionCall() ?? HalfCourtAction.Motion,
                "Quick iso call queues an iso possession");

            Assert(coach.GetRecommendedPlays() != null, "Play recommendations come back non-null");
        }

        private void TestTacticsWriteThrough()
        {
            var strategy = TeamStrategy.CreateDefault("TST");
            var coach = new GameCoach(strategy, PlayBook.CreateDefault("TST"),
                TeamGameInstructions.CreateDefault("TST", new List<Player>()));

            coach.SetPace(GamePace.Push);
            AssertEqual(106f, strategy.TargetPace, "Push pace writes through to TargetPace");
            AssertEqual(PacePreference.AlwaysPush, strategy.PacePreference, "Push pace preference");
            AssertEqual(TransitionPreference.AggressivePush, strategy.TransitionOffense,
                "Push pace turns on the break");

            coach.SetDefense(DefensiveScheme.Zone23);
            AssertEqual(DefensiveSchemeType.Zone2_3, strategy.DefensiveSystem.PrimaryScheme,
                "Zone call writes through to the scheme");
            Assert(strategy.DefensiveSystem.ZoneUsage >= 70, "Zone call raises zone usage");

            coach.SetDefense(DefensiveScheme.ManToMan);
            AssertEqual(0, strategy.DefensiveSystem.ZoneUsage, "Back to man clears zone usage");

            coach.SetIntensity(IntensityLevel.Aggressive);
            AssertEqual(DefensiveIntensity.VeryHigh, strategy.DefensiveSystem.DefensiveIntensity,
                "Aggressive intensity writes through");
            AssertEqual(80, strategy.DefensiveSystem.OnBallPressure, "Aggressive pressure writes through");

            coach.SetPickAndRollCoverage(PnRCoverageScheme.Blitz);
            AssertEqual(PnRCoverage.Blitz, strategy.DefensiveSystem.PickAndRollCoverage,
                "Blitz coverage writes through");

            coach.SetTransitionDefense(TransitionDefenseLevel.SprintBack);
            AssertEqual(TransitionDefensePreference.SprintBack, strategy.TransitionDefense,
                "Transition defense writes through");

            coach.SetOffense(OffensiveScheme.PostUp);
            AssertEqual(OffensiveSystemType.PostUpFocused, strategy.OffensiveSystem.PrimarySystem,
                "Offensive scheme writes through");

            coach.SetHackAShaq(true);
            Assert(strategy.DefensiveSystem.IntentionalFoulPoorShooters,
                "Hack-a-Shaq writes through to fouling strategy");

            coach.SetFullCourtPress(true);
            AssertEqual(DefensiveSchemeType.FullCourtPress, strategy.DefensiveSystem.PrimaryScheme,
                "Press toggle writes through to the scheme");
            coach.SetFullCourtPress(false);
            AssertEqual(DefensiveSchemeType.ManToManStandard, strategy.DefensiveSystem.PrimaryScheme,
                "Press off restores man-to-man");
        }

        private void TestCalledActionForcesPossession()
        {
            var offense = new Player[5];
            var defense = new Player[5];
            var positions = new[] { Position.PointGuard, Position.ShootingGuard,
                Position.SmallForward, Position.PowerForward, Position.Center };
            for (int i = 0; i < 5; i++)
            {
                offense[i] = MakePlayer($"o{i}", positions[i]);
                defense[i] = MakePlayer($"d{i}", positions[i]);
            }

            var off = TeamStrategy.CreateDefault("OFF");
            var def = TeamStrategy.CreateDefault("DEF");
            var sim = new PossessionSimulator(99);

            var seen = new HashSet<HalfCourtAction>();
            bool allForced = true;
            for (int i = 0; i < 60; i++)
            {
                if (i % 30 == 0)
                    foreach (var p in offense.Concat(defense)) p.Energy = 100f;

                // Forced possession: the coach called iso
                var forced = sim.SimulatePossession(offense, defense, off, def,
                    600f, 2, true, "OFF", "DEF", 0, false, HalfCourtAction.Isolation);
                if (forced.Action != HalfCourtAction.Isolation) allForced = false;

                // Free possession: the mix varies
                var free = sim.SimulatePossession(offense, defense, off, def,
                    600f, 2, true, "OFF", "DEF", 0);
                seen.Add(free.Action);
            }

            Assert(allForced, "A called play forces the possession's action");
            Assert(seen.Count >= 3, $"Uncalled possessions vary the action mix ({seen.Count} kinds)");
        }
    }
}
