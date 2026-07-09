using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using EventType = NBAHeadCoach.Core.Simulation.EventType;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 5-B: defensive schemes shape shots/steals/fouls, rebounding
    /// focus earns second chances, and transition preferences create real fast
    /// breaks — all decision-side, neutral at scheme defaults.
    /// </summary>
    public class DefensiveWiringTest : BaseTest
    {
        private const int POSSESSIONS = 800;

        private Player[] _offense;
        private Player[] _defense;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(6161);

            BuildLineups();

            TestGamblingDefenseCreatesSteals();
            TestPackedPaintWallsOffTheRim();
            TestReboundingFocusEarnsSecondChances();
            TestTransitionPreferencesCreateFastBreaks();
            TestAggressiveDefenseFoulsMore();
            TestBlitzForcesPnRTurnovers();

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

        private class Counts
        {
            public int Steals, Fouls, Turnovers, FastBreaks;
            public int ShotsMade, ShotsTaken;
            public int OffRebounds, TotalRebounds;
            public int Possessions;
        }

        private Counts RunBatch(TeamStrategy off, TeamStrategy def, int seed, bool liveBall = false)
        {
            var c = new Counts();
            var sim = new PossessionSimulator(seed);

            for (int i = 0; i < POSSESSIONS; i++)
            {
                if (i % 50 == 0)
                    foreach (var p in _offense.Concat(_defense)) p.Energy = 100f;

                var result = sim.SimulatePossession(_offense, _defense, off, def,
                    600f, 2, true, "GSW", "LAL", 0, liveBall);

                c.Possessions++;
                if (result.WasFastBreak) c.FastBreaks++;
                if (result.Outcome == PossessionOutcome.Turnover) c.Turnovers++;

                foreach (var evt in result.Events)
                {
                    switch (evt.Type)
                    {
                        case EventType.Steal: c.Steals++; break;
                        case EventType.Foul: c.Fouls++; break;
                        case EventType.Shot:
                            c.ShotsTaken++;
                            if (evt.Outcome == EventOutcome.Success) c.ShotsMade++;
                            break;
                        case EventType.Rebound:
                            c.TotalRebounds++;
                            if (evt.IsOffensiveRebound) c.OffRebounds++;
                            break;
                    }
                }
            }
            return c;
        }

        private void TestGamblingDefenseCreatesSteals()
        {
            var off = TeamStrategy.CreateDefault("GSW");

            var gambling = TeamStrategy.CreateDefault("LAL");
            gambling.DefensiveSystem.GamblingFrequency = 100;
            gambling.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.FullCourtPress;

            var conservative = TeamStrategy.CreateDefault("LAL");
            conservative.DefensiveSystem.GamblingFrequency = 0;

            int gambleSteals = RunBatch(off, gambling, 111).Steals;
            int safeSteals = RunBatch(off, conservative, 111).Steals;

            AssertGreaterThan(gambleSteals, safeSteals + 5,
                $"Gambling press defense creates steals ({gambleSteals} vs {safeSteals})");
        }

        private void TestPackedPaintWallsOffTheRim()
        {
            var rimOffense = TeamStrategy.CreateDefault("GSW");
            rimOffense.RimAttackFrequency = 98;
            rimOffense.ThreePointFrequency = 1;
            rimOffense.MidRangeFrequency = 1;

            var packed = TeamStrategy.CreateDefault("LAL");
            packed.DefensiveSystem.HelpDefense = HelpDefenseLevel.PackedPaint;
            packed.DefensiveSystem.ZoneUsage = 100;

            var open = TeamStrategy.CreateDefault("LAL");
            open.DefensiveSystem.HelpDefense = HelpDefenseLevel.NoHelp;

            var packedBatch = RunBatch(rimOffense, packed, 222);
            var openBatch = RunBatch(rimOffense, open, 222);

            float packedPct = (float)packedBatch.ShotsMade / Mathf.Max(1, packedBatch.ShotsTaken);
            float openPct = (float)openBatch.ShotsMade / Mathf.Max(1, openBatch.ShotsTaken);

            AssertGreaterThan(openPct, packedPct + 0.02f,
                $"Packed paint + zone walls off the rim (open {openPct:P0} vs packed {packedPct:P0})");
        }

        private void TestReboundingFocusEarnsSecondChances()
        {
            var crash = TeamStrategy.CreateDefault("GSW");
            crash.OffensiveReboundingFocus = 5;
            crash.SendersOnOffensiveGlass = 3;

            var getBack = TeamStrategy.CreateDefault("GSW");
            getBack.OffensiveReboundingFocus = 1;
            getBack.SendersOnOffensiveGlass = 1;
            getBack.TransitionDefense = TransitionDefensePreference.SprintBack;

            var def = TeamStrategy.CreateDefault("LAL");

            var crashBatch = RunBatch(crash, def, 333);
            var backBatch = RunBatch(getBack, def, 333);

            float crashRate = (float)crashBatch.OffRebounds / Mathf.Max(1, crashBatch.TotalRebounds);
            float backRate = (float)backBatch.OffRebounds / Mathf.Max(1, backBatch.TotalRebounds);

            AssertGreaterThan(crashRate, backRate + 0.05f,
                $"Crashing the glass earns second chances (crash {crashRate:P0} vs get-back {backRate:P0})");
        }

        private void TestTransitionPreferencesCreateFastBreaks()
        {
            var push = TeamStrategy.CreateDefault("GSW");
            push.TransitionOffense = TransitionPreference.AlwaysPush;

            var walk = TeamStrategy.CreateDefault("GSW");
            walk.TransitionOffense = TransitionPreference.NoPush;

            var def = TeamStrategy.CreateDefault("LAL");

            int pushBreaks = RunBatch(push, def, 444, liveBall: true).FastBreaks;
            int walkBreaks = RunBatch(walk, def, 444, liveBall: true).FastBreaks;

            AssertGreaterThan(pushBreaks, walkBreaks + 50,
                $"Push philosophy creates fast breaks ({pushBreaks} vs {walkBreaks})");

            // Getting back in transition takes breaks away
            var sprintBack = TeamStrategy.CreateDefault("LAL");
            sprintBack.TransitionDefense = TransitionDefensePreference.SprintBack;

            var gambleBack = TeamStrategy.CreateDefault("LAL");
            gambleBack.TransitionDefense = TransitionDefensePreference.GambleForSteals;

            int vsSprint = RunBatch(push, sprintBack, 555, liveBall: true).FastBreaks;
            int vsGamble = RunBatch(push, gambleBack, 555, liveBall: true).FastBreaks;

            AssertGreaterThan(vsGamble, vsSprint + 20,
                $"Sprinting back kills fast breaks ({vsSprint} vs gambling {vsGamble})");

            // Dead-ball starts rarely turn into breaks even for pushers
            int deadBall = RunBatch(push, def, 444, liveBall: false).FastBreaks;
            AssertGreaterThan(pushBreaks, deadBall + 50,
                $"Fast breaks need a live ball ({pushBreaks} live vs {deadBall} dead)");
        }

        private void TestAggressiveDefenseFoulsMore()
        {
            var off = TeamStrategy.CreateDefault("GSW");

            var physical = TeamStrategy.CreateDefault("LAL");
            physical.DefensiveSystem.DefensiveIntensity = DefensiveIntensity.VeryHigh;
            physical.DefensiveSystem.GamblingFrequency = 100;

            var disciplined = TeamStrategy.CreateDefault("LAL");
            disciplined.DefensiveSystem.DefensiveIntensity = DefensiveIntensity.Low;
            disciplined.DefensiveSystem.GamblingFrequency = 0;

            int physicalFouls = RunBatch(off, physical, 666).Fouls;
            int cleanFouls = RunBatch(off, disciplined, 666).Fouls;

            AssertGreaterThan(physicalFouls, cleanFouls + 10,
                $"Physical gambling defense fouls more ({physicalFouls} vs {cleanFouls})");
        }

        private void TestBlitzForcesPnRTurnovers()
        {
            var pnrOffense = TeamStrategy.CreateDefault("GSW");
            pnrOffense.OffensiveSystem.PrimarySystem = OffensiveSystemType.PickAndRollHeavy;
            pnrOffense.OffensiveSystem.PickAndRollFrequency = 90;
            pnrOffense.OffensiveSystem.IsolationFrequency = 0;
            pnrOffense.OffensiveSystem.CuttingFrequency = 0;
            pnrOffense.OffensiveSystem.HandoffFrequency = 0;

            var blitz = TeamStrategy.CreateDefault("LAL");
            blitz.DefensiveSystem.PickAndRollCoverage = PnRCoverage.Blitz;
            blitz.DefensiveSystem.PrimaryScheme = DefensiveSchemeType.HalfCourtTrap;

            var drop = TeamStrategy.CreateDefault("LAL");
            drop.DefensiveSystem.PickAndRollCoverage = PnRCoverage.DropCoverage;

            // Aggregate three seeds: the blitz edge (~+3pp on PnR trips) is real but
            // small enough that a single 800-possession seed can noise out.
            int blitzTOs = 0, dropTOs = 0;
            foreach (int seed in new[] { 888, 1777, 2666 })
            {
                blitzTOs += RunBatch(pnrOffense, blitz, seed).Turnovers;
                dropTOs += RunBatch(pnrOffense, drop, seed).Turnovers;
            }

            AssertGreaterThan(blitzTOs, dropTOs + 5,
                $"Blitzing the PnR forces turnovers ({blitzTOs} vs {dropTOs})");
        }
    }
}
