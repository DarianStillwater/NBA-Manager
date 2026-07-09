using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using EventType = NBAHeadCoach.Core.Simulation.EventType;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the per-scheme defense fix: picking a zone in the UI used to be a
    /// no-op because the sim keyed off ZoneUsage only. Now each scheme has a
    /// distinct profile — verified both as pure math and directionally in seeded
    /// possession batches (2-3 walls the rim vs 3-2, 1-3-1 forces turnovers,
    /// aggressive man fouls more than a 2-3, zones concede the offensive glass).
    /// </summary>
    public class DefensiveSchemeEffectsTest : BaseTest
    {
        private const int POSSESSIONS = 1200;

        private Player[] _offense;
        private Player[] _defense;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(20260709);

            BuildLineups();

            TestProfileShapes();
            TestEffectiveBlending();
            TestZone23WallsTheRim();
            Test131ForcesTurnovers();
            TestAggressiveManFoulsMoreThanZone();
            TestZonesConcedeTheGlass();

            return (_passed, _failed);
        }

        // ── Pure profile math ──

        private void TestProfileShapes()
        {
            var std = DefensiveSchemeProfile.For(DefensiveSchemeType.ManToManStandard);
            Assert(std.InteriorShotMod == 1f && std.ThreeShotMod == 1f && std.TurnoverForcedMod == 1f &&
                   std.FoulMod == 1f && std.OrebConcededBonus == 0f,
                "ManToManStandard is the identity profile (existing sim balance untouched)");

            var z23 = DefensiveSchemeProfile.For(DefensiveSchemeType.Zone2_3);
            var z32 = DefensiveSchemeProfile.For(DefensiveSchemeType.Zone3_2);
            Assert(z23.InteriorShotMod < z32.InteriorShotMod, "2-3 protects the rim harder than 3-2");
            Assert(z32.ThreeShotMod < z23.ThreeShotMod, "3-2 chases shooters off the line harder than 2-3");
            Assert(z23.ThreeShotMod > 1f, "2-3 concedes threes");
            Assert(z32.InteriorShotMod > 1f, "3-2 opens the middle");

            var z131 = DefensiveSchemeProfile.For(DefensiveSchemeType.Zone1_3_1);
            Assert(z131.TurnoverForcedMod > z23.TurnoverForcedMod &&
                   z131.TurnoverForcedMod > z32.TurnoverForcedMod,
                "1-3-1 forces the most turnovers of the zones");
            Assert(z131.OrebConcededBonus >= z23.OrebConcededBonus, "1-3-1 bleeds the glass most");

            var box = DefensiveSchemeProfile.For(DefensiveSchemeType.BoxAndOne);
            Assert(box.StarSuppressionMod < 1f, "Box-and-one suppresses the star");
            Assert(box.OthersBoostMod > 1f, "Box-and-one concedes to the supporting cast");

            var aggressive = DefensiveSchemeProfile.For(DefensiveSchemeType.ManToManAggressive);
            Assert(aggressive.FoulMod > 1f && z23.FoulMod < 1f,
                "Aggressive man reaches; zones foul less");

            foreach (DefensiveSchemeType scheme in System.Enum.GetValues(typeof(DefensiveSchemeType)))
            {
                var p = DefensiveSchemeProfile.For(scheme);
                Assert(p.InteriorShotMod > 0.85f && p.InteriorShotMod < 1.15f &&
                       p.ThreeShotMod > 0.85f && p.ThreeShotMod < 1.15f &&
                       p.FoulMod > 0.8f && p.FoulMod < 1.2f,
                    $"{scheme} modifiers stay within sane bounds");
            }
        }

        private void TestEffectiveBlending()
        {
            var d = new DefensiveSystem(); // man standard, ZoneUsage 0
            var eff = DefensiveSchemeProfile.Effective(d);
            Assert(eff.InteriorShotMod == 1f && eff.OrebConcededBonus == 0f,
                "Default defense resolves to identity");

            d.PrimaryScheme = DefensiveSchemeType.Zone2_3;
            d.ZoneUsage = 0;
            eff = DefensiveSchemeProfile.Effective(d);
            Assert(eff.InteriorShotMod < 1f,
                "A zone PrimaryScheme is full-time zone even with ZoneUsage 0 (the reported bug)");

            d.PrimaryScheme = DefensiveSchemeType.ManToManStandard;
            d.SecondaryScheme = DefensiveSchemeType.Zone2_3;
            d.ZoneUsage = 50;
            eff = DefensiveSchemeProfile.Effective(d);
            var full = DefensiveSchemeProfile.For(DefensiveSchemeType.Zone2_3);
            Assert(eff.InteriorShotMod < 1f && eff.InteriorShotMod > full.InteriorShotMod,
                "Part-time zone blends between man and the secondary scheme");

            Assert(DefensiveSchemeProfile.Effective(null).FoulMod == 1f, "Null defense is neutral");
        }

        // ── Seeded directional sims ──

        private void TestZone23WallsTheRim()
        {
            var rimOffense = TeamStrategy.CreateDefault("GSW");
            rimOffense.RimAttackFrequency = 98;
            rimOffense.ThreePointFrequency = 1;
            rimOffense.MidRangeFrequency = 1;

            float pct23 = FgPct(RunBatch(rimOffense, DefenseWith(DefensiveSchemeType.Zone2_3), 311));
            float pct32 = FgPct(RunBatch(rimOffense, DefenseWith(DefensiveSchemeType.Zone3_2), 311));

            AssertGreaterThan(pct32, pct23 + 0.015f,
                $"Rim-heavy offense shoots worse into a 2-3 than a 3-2 ({pct23:P1} vs {pct32:P1})");
        }

        private void Test131ForcesTurnovers()
        {
            var off = TeamStrategy.CreateDefault("GSW");
            int tos131 = RunBatch(off, DefenseWith(DefensiveSchemeType.Zone1_3_1), 422).Turnovers;
            int tosStd = RunBatch(off, DefenseWith(DefensiveSchemeType.ManToManStandard), 422).Turnovers;

            AssertGreaterThan(tos131, tosStd,
                $"1-3-1 forces more turnovers than standard man ({tos131} vs {tosStd})");
        }

        private void TestAggressiveManFoulsMoreThanZone()
        {
            var off = TeamStrategy.CreateDefault("GSW");
            int foulsAgg = RunBatch(off, DefenseWith(DefensiveSchemeType.ManToManAggressive), 533).Fouls;
            int foulsZone = RunBatch(off, DefenseWith(DefensiveSchemeType.Zone2_3), 533).Fouls;

            AssertGreaterThan(foulsAgg, foulsZone + 5,
                $"Aggressive man fouls more than a 2-3 zone ({foulsAgg} vs {foulsZone})");
        }

        private void TestZonesConcedeTheGlass()
        {
            var off = TeamStrategy.CreateDefault("GSW");
            off.OffensiveReboundingFocus = 4;

            int oreb131 = RunBatch(off, DefenseWith(DefensiveSchemeType.Zone1_3_1), 644).OffRebounds;
            int orebStd = RunBatch(off, DefenseWith(DefensiveSchemeType.ManToManStandard), 644).OffRebounds;

            AssertGreaterThan(oreb131, orebStd,
                $"1-3-1 concedes more offensive boards than man ({oreb131} vs {orebStd})");
        }

        // ── Harness (mirrors DefensiveWiringTest) ──

        private TeamStrategy DefenseWith(DefensiveSchemeType scheme)
        {
            var def = TeamStrategy.CreateDefault("LAL");
            def.DefensiveSystem.PrimaryScheme = scheme;
            return def;
        }

        private float FgPct(Counts c) => (float)c.ShotsMade / Mathf.Max(1, c.ShotsTaken);

        private class Counts
        {
            public int Fouls, Turnovers, ShotsMade, ShotsTaken, OffRebounds;
        }

        private Counts RunBatch(TeamStrategy off, TeamStrategy def, int seed)
        {
            var c = new Counts();
            var sim = new PossessionSimulator(seed);

            for (int i = 0; i < POSSESSIONS; i++)
            {
                if (i % 50 == 0)
                    foreach (var p in _offense.Concat(_defense)) p.Energy = 100f;

                var result = sim.SimulatePossession(_offense, _defense, off, def,
                    600f, 2, true, "GSW", "LAL", 0, false);

                if (result.Outcome == PossessionOutcome.Turnover) c.Turnovers++;
                foreach (var evt in result.Events)
                {
                    switch (evt.Type)
                    {
                        case EventType.Foul: c.Fouls++; break;
                        case EventType.Shot:
                            c.ShotsTaken++;
                            if (evt.Outcome == EventOutcome.Success) c.ShotsMade++;
                            break;
                        case EventType.Rebound:
                            if (evt.IsOffensiveRebound) c.OffRebounds++;
                            break;
                    }
                }
            }
            return c;
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
    }
}
