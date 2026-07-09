using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using EventType = NBAHeadCoach.Core.Simulation.EventType;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 5 strategy wiring: the offensive action model shapes usage,
    /// shot zones, assists, and possession length; AutoSetStrategy maps the full
    /// coach personality; and the sim reads defense from the unified Team.Strategy.
    /// </summary>
    public class StrategySimWiringTest : BaseTest
    {
        private const int POSSESSIONS = 800;

        private Player[] _offense;
        private Player[] _defense;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(5150);

            BuildLineups();

            TestIsolationConcentratesUsage();
            TestPostFocusFeedsBigs();
            TestBallMovementCreatesAssists();
            TestShotClockPhilosophyChangesTempo();
            TestDefenseAuthorityIsUnifiedStrategy();
            TestAutoSetStrategyMapsFullPersonality();

            return (_passed, _failed);
        }

        // ==================== FIXTURES ====================

        private void BuildLineups()
        {
            var all = NBAPlayerData.GetAllPlayers();
            var gsw = all.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating).Take(5).ToList();
            var lal = all.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating).Take(5).ToList();

            _offense = gsw.Select(Convert).ToArray();
            _defense = lal.Select(Convert).ToArray();
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

        private class BatchResult
        {
            public Dictionary<string, int> ShotsByPlayer = new Dictionary<string, int>();
            public int ShotPossessions;
            public int PassPossessions;
            public float TotalDuration;
            public int Possessions;
            public int BigManShots;
        }

        private BatchResult RunBatch(TeamStrategy offense, int seed)
        {
            var result = new BatchResult();
            var defStrategy = TeamStrategy.CreateDefault("LAL");
            var sim = new PossessionSimulator(seed);

            for (int i = 0; i < POSSESSIONS; i++)
            {
                if (i % 50 == 0)
                    foreach (var p in _offense.Concat(_defense)) p.Energy = 100f;

                var poss = sim.SimulatePossession(_offense, _defense, offense, defStrategy,
                    600f, 2, true, "GSW", "LAL", 0);

                result.Possessions++;
                result.TotalDuration += poss.StartGameClock - poss.EndGameClock;

                bool hadShot = false, hadPass = false;
                foreach (var evt in poss.Events)
                {
                    if (evt.Type == EventType.Shot || evt.Type == EventType.Block)
                    {
                        hadShot = true;
                        if (!result.ShotsByPlayer.ContainsKey(evt.ActorPlayerId))
                            result.ShotsByPlayer[evt.ActorPlayerId] = 0;
                        result.ShotsByPlayer[evt.ActorPlayerId]++;

                        if (evt.ActorPlayerId == _offense[3].PlayerId ||
                            evt.ActorPlayerId == _offense[4].PlayerId)
                            result.BigManShots++;
                    }
                    if (evt.Type == EventType.Pass) hadPass = true;
                }
                if (hadShot) result.ShotPossessions++;
                if (hadShot && hadPass) result.PassPossessions++;
            }
            return result;
        }

        private float TopUsageShare(BatchResult r)
        {
            int total = r.ShotsByPlayer.Values.Sum();
            return total > 0 ? (float)r.ShotsByPlayer.Values.Max() / total : 0f;
        }

        // ==================== ACTION MODEL ====================

        private void TestIsolationConcentratesUsage()
        {
            var iso = TeamStrategy.CreateDefault("GSW");
            iso.OffensiveSystem.IsolationFrequency = 90;
            iso.OffensiveSystem.PickAndRollFrequency = 5;
            iso.OffensiveSystem.PostUpFrequency = 0;
            iso.PostUpFrequency = 0;
            iso.OffensiveSystem.HandoffFrequency = 0;
            iso.OffensiveSystem.CuttingFrequency = 0;
            iso.OffensiveSystem.BallMovementPriority = 30;

            var spread = TeamStrategy.CreateDefault("GSW");
            spread.OffensiveSystem.IsolationFrequency = 0;
            spread.OffensiveSystem.BallMovementPriority = 95;
            spread.OffensiveSystem.ExtraPassPhilosophy = true;

            float isoShare = TopUsageShare(RunBatch(iso, 4242));
            float spreadShare = TopUsageShare(RunBatch(spread, 4242));

            AssertGreaterThan(isoShare, spreadShare + 0.05f,
                $"Iso-heavy offense concentrates usage (iso {isoShare:P0} vs spread {spreadShare:P0})");
            AssertGreaterThan(isoShare, 0.30f, $"Iso offense runs through its star ({isoShare:P0})");
        }

        private void TestPostFocusFeedsBigs()
        {
            var post = TeamStrategy.CreateDefault("GSW");
            post.PostUpFrequency = 70;
            post.OffensiveSystem.PostUpFrequency = 70;
            post.OffensiveSystem.PickAndRollFrequency = 10;
            post.OffensiveSystem.IsolationFrequency = 5;

            var perimeter = TeamStrategy.CreateDefault("GSW");
            perimeter.PostUpFrequency = 0;
            perimeter.OffensiveSystem.PostUpFrequency = 0;

            var postBatch = RunBatch(post, 777);
            var perimBatch = RunBatch(perimeter, 777);

            float postBigShare = (float)postBatch.BigManShots / Mathf.Max(1, postBatch.ShotsByPlayer.Values.Sum());
            float perimBigShare = (float)perimBatch.BigManShots / Mathf.Max(1, perimBatch.ShotsByPlayer.Values.Sum());

            AssertGreaterThan(postBigShare, perimBigShare + 0.05f,
                $"Post-up offense feeds the bigs (post {postBigShare:P0} vs perimeter {perimBigShare:P0})");
        }

        private void TestBallMovementCreatesAssists()
        {
            var movement = TeamStrategy.CreateDefault("GSW");
            movement.OffensiveSystem.BallMovementPriority = 95;
            movement.OffensiveSystem.ExtraPassPhilosophy = true;

            var heroBall = TeamStrategy.CreateDefault("GSW");
            heroBall.OffensiveSystem.BallMovementPriority = 30;
            heroBall.OffensiveSystem.IsolationFrequency = 50;

            var moveBatch = RunBatch(movement, 1313);
            var heroBatch = RunBatch(heroBall, 1313);

            float movePassRate = (float)moveBatch.PassPossessions / Mathf.Max(1, moveBatch.ShotPossessions);
            float heroPassRate = (float)heroBatch.PassPossessions / Mathf.Max(1, heroBatch.ShotPossessions);

            AssertGreaterThan(movePassRate, heroPassRate + 0.03f,
                $"Ball movement creates more assisted looks ({movePassRate:P0} vs {heroPassRate:P0})");
        }

        private void TestShotClockPhilosophyChangesTempo()
        {
            var early = TeamStrategy.CreateDefault("GSW");
            early.OffensiveSystem.ShotClockApproach = ShotClockApproach.EarlyGoodShot;
            early.OffensiveSystem.TargetShotClockEntry = 18;

            var patient = TeamStrategy.CreateDefault("GSW");
            patient.OffensiveSystem.ShotClockApproach = ShotClockApproach.WorkForBestShot;
            patient.OffensiveSystem.TargetShotClockEntry = 8;

            var earlyBatch = RunBatch(early, 2024);
            var patientBatch = RunBatch(patient, 2024);

            float earlyAvg = earlyBatch.TotalDuration / earlyBatch.Possessions;
            float patientAvg = patientBatch.TotalDuration / patientBatch.Possessions;

            AssertGreaterThan(patientAvg, earlyAvg + 2f,
                $"Shot-clock philosophy changes tempo (early {earlyAvg:F1}s vs patient {patientAvg:F1}s)");
        }

        // ==================== STRATEGY AUTHORITY ====================

        private void TestDefenseAuthorityIsUnifiedStrategy()
        {
            var all = NBAPlayerData.GetAllPlayers();
            var gswRoster = all.Where(p => p.TeamId == "GSW").OrderByDescending(p => p.OverallRating)
                .Take(10).Select(Convert).ToList();
            var lalRoster = all.Where(p => p.TeamId == "LAL").OrderByDescending(p => p.OverallRating)
                .Take(10).Select(Convert).ToList();

            var db = new PlayerDatabase();
            foreach (var p in gswRoster.Concat(lalRoster)) db.AddPlayer(p);

            var home = MakeTeam("GSW", gswRoster);
            var away = MakeTeam("LAL", lalRoster);

            // Pressure defense lives on the unified Strategy object — if the sim still
            // read the legacy DefensiveStrategy field this would have zero effect.
            away.OffensiveStrategy.DefensiveSystem.OnBallPressure = 95;
            int pressTOs = HomeTurnovers(db, home, away, gswRoster, lalRoster);

            away.OffensiveStrategy.DefensiveSystem.OnBallPressure = 15;
            int softTOs = HomeTurnovers(db, home, away, gswRoster, lalRoster);

            AssertGreaterThan(pressTOs, softTOs + 3,
                $"Unified Strategy.DefensiveSystem drives forced turnovers (press {pressTOs} vs soft {softTOs})");
        }

        private Team MakeTeam(string id, List<Player> roster)
        {
            var team = new Team
            {
                TeamId = id, City = id, Nickname = id,
                RosterPlayerIds = roster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateDefault(id),
                DefensiveStrategy = TeamStrategy.CreateDefault(id)
            };
            for (int i = 0; i < 5; i++) team.StartingLineupIds[i] = roster[i].PlayerId;
            return team;
        }

        private int HomeTurnovers(PlayerDatabase db, Team home, Team away,
            List<Player> homeRoster, List<Player> awayRoster)
        {
            int turnovers = 0;
            for (int g = 0; g < 2; g++)
            {
                foreach (var p in homeRoster.Concat(awayRoster)) p.Energy = 100f;
                var sim = new GameSimulator(db, seed: 9000 + g);
                var result = sim.SimulateGame(home, away);
                turnovers += home.RosterPlayerIds
                    .Where(id => result.BoxScore.PlayerStats.ContainsKey(id))
                    .Sum(id => result.BoxScore.PlayerStats[id].Turnovers);
            }
            return turnovers;
        }

        // ==================== PERSONALITY MAPPING ====================

        private void TestAutoSetStrategyMapsFullPersonality()
        {
            var team = new Team { TeamId = "TST", City = "Test", Nickname = "Testers" };
            var coach = new AICoachPersonality
            {
                CoachId = "C1", CoachName = "Aggressor",
                PreferredPace = 106f, ThreePointEmphasis = 48, PostPlayEmphasis = 20,
                IsolationTendency = 55, PickAndRollEmphasis = 70, BallMovementPriority = 80,
                DefensiveAggression = 75, ZoneUsageTendency = 40, SwitchingTendency = 70,
                BlitzingTendency = 60, DoubleTeamTendency = 60,
                FoulingUp3Tendency = false, HoldForLastShotTendency = true, RiskTakingClutch = 70
            };

            team.AutoSetStrategy(coach);
            var s = team.Strategy;

            AssertEqual(106f, s.TargetPace, "Pace maps from personality");
            AssertEqual(48, s.ThreePointFrequency, "3PT emphasis maps");
            AssertEqual(100, s.ThreePointFrequency + s.MidRangeFrequency + s.RimAttackFrequency,
                "Shot mix sums to 100");
            AssertEqual(TransitionPreference.AlwaysPush, s.TransitionOffense, "Fast coach always pushes");
            AssertEqual(55, s.OffensiveSystem.IsolationFrequency, "Iso tendency maps");
            AssertEqual(70, s.OffensiveSystem.PickAndRollFrequency, "PnR emphasis maps");
            Assert(s.OffensiveSystem.ExtraPassPhilosophy, "High ball movement = extra pass");
            AssertEqual(ShotClockApproach.EarlyGoodShot, s.OffensiveSystem.ShotClockApproach,
                "Fast pace shoots early");

            AssertEqual(DefensiveSchemeType.Zone2_3, s.DefensiveSystem.PrimaryScheme,
                "Zone-heavy coach plays zone");
            AssertEqual(40, s.DefensiveSystem.ZoneUsage, "Zone usage maps");
            AssertEqual(75, s.DefensiveSystem.OnBallPressure, "Defensive aggression maps to pressure");
            AssertEqual(DefensiveIntensity.VeryHigh, s.DefensiveSystem.DefensiveIntensity,
                "Aggression maps to intensity");
            AssertEqual(SwitchingLevel.SwitchMost, s.DefensiveSystem.SwitchingLevel, "Switching maps");
            AssertEqual(PnRCoverage.Blitz, s.DefensiveSystem.PickAndRollCoverage,
                "Blitzing coach traps the PnR");

            Assert(!s.CloseGameStrategy.FoulWhenDown3, "Clutch fouling tendency maps");
            AssertEqual(6, s.CloseGameStrategy.SecondsAheadToSlowDown, "Risk taker slows down later");
            AssertEqual(LateGamePlayType.StarIsolation, s.CloseGameStrategy.LateGamePlay,
                "Iso coach closes with the star");
            Assert(ReferenceEquals(team.OffensiveStrategy, team.DefensiveStrategy),
                "One strategy object is the authority for both ends");

            // Conservative coach lands on the other side of every threshold
            var calm = new AICoachPersonality
            {
                CoachId = "C2", CoachName = "Professor",
                PreferredPace = 88f, ThreePointEmphasis = 30, PostPlayEmphasis = 15,
                DefensiveAggression = 25, ZoneUsageTendency = 5, SwitchingTendency = 10,
                BlitzingTendency = 10, DoubleTeamTendency = 15
            };
            team.AutoSetStrategy(calm);
            var c = team.Strategy;

            AssertEqual(DefensiveSchemeType.ManToManConservative, c.DefensiveSystem.PrimaryScheme,
                "Passive coach sags off");
            AssertEqual(DefensiveIntensity.Low, c.DefensiveSystem.DefensiveIntensity,
                "Low aggression = low intensity");
            AssertEqual(TransitionDefensePreference.SprintBack, c.TransitionDefense,
                "Conservative coach sprints back");
            AssertEqual(SwitchingLevel.NoSwitching, c.DefensiveSystem.SwitchingLevel,
                "Low switching = fight through");
            AssertRange(c.OffensiveReboundingFocus, 1, 5, "Rebounding focus in range");
            AssertEqual(ShotClockApproach.WorkForBestShot, c.OffensiveSystem.ShotClockApproach,
                "Slow pace works for best shot");
        }
    }
}
