using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// P2 regression guard for the choreographer enrichment (SpeedFeetPerSec, DefensiveStance,
    /// VerticalOffset, ActionPhase + action-window densification + richer FacingAngle).
    ///
    /// 1. Outcomes are bit-identical: two full games with the same seed (global RNG re-seeded,
    ///    player energy reset) must produce identical scores and per-player box-score lines —
    ///    the enrichment fields/frames are post-decision presentation only.
    /// 2. The enriched timeline is well-formed: GameClock non-increasing, every position in
    ///    bounds, some frame shows real speed, defenders enter a stance, and shooting
    ///    possessions produce a real vertical jump with normalized ActionPhase.
    /// </summary>
    public class ChoreographyEnrichmentTest : BaseTest
    {
        private const int GAME_SEED = 20260710;
        private const int POSSESSIONS = 300;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            RunOutcomeInvariance();
            RunTimelineQuality();

            return (_passed, _failed);
        }

        // ── 1. Fixed-seed determinism: two games must be identical down to the box score ──
        private void RunOutcomeInvariance()
        {
            // Build every input fresh per run so no Player/Team/DB state can leak between the
            // two games — determinism then rests solely on the fixed seed + re-seeded global RNG.
            var runA = SimulateSeeded();
            var runB = SimulateSeeded();

            AssertEqual(runA.home, runB.home, "Fixed-seed home score identical across runs");
            AssertEqual(runA.away, runB.away, "Fixed-seed away score identical across runs");
            AssertEqual(runA.quarters, runB.quarters, "Fixed-seed quarter count identical across runs");

            bool linesMatch = runA.lines.Count == runB.lines.Count;
            if (linesMatch)
            {
                foreach (var kv in runA.lines)
                {
                    if (!runB.lines.TryGetValue(kv.Key, out var other) || other != kv.Value)
                    {
                        linesMatch = false;
                        Debug.Log($"    Box-score divergence for {kv.Key}: '{kv.Value}' vs " +
                                  $"'{(runB.lines.ContainsKey(kv.Key) ? runB.lines[kv.Key] : "<missing>")}'");
                        break;
                    }
                }
            }
            Assert(linesMatch, "Fixed-seed per-player box-score lines identical across runs");
        }

        private (int home, int away, int quarters, Dictionary<string, string> lines) SimulateSeeded()
        {
            var db = new PlayerDatabase();
            var gsw = BuildRoster(db, "GSW");
            var lal = BuildRoster(db, "LAL");
            var home = CreateTeam("GSW", gsw);
            var away = CreateTeam("LAL", lal);

            // The foul / free-throw handlers roll on the global RNG, so it must be re-seeded
            // identically before each run for the outcome to reproduce exactly.
            UnityEngine.Random.InitState(GAME_SEED);

            var sim = new GameSimulator(db, seed: GAME_SEED);
            var result = sim.SimulateGame(home, away);

            var lines = new Dictionary<string, string>();
            foreach (var kv in result.BoxScore.PlayerStats)
            {
                var s = kv.Value;
                lines[kv.Key] = $"{s.Points}|{s.TotalFGM}/{s.TotalFGA}|{s.ThreePointMade}/{s.ThreePointAttempts}|" +
                    $"{s.FreeThrowsMade}/{s.FreeThrowAttempts}|{s.OffensiveRebounds}+{s.DefensiveRebounds}|" +
                    $"{s.Assists}|{s.Steals}|{s.Blocks}|{s.Turnovers}|{s.PersonalFouls}|{s.SecondsPlayed:F1}";
            }
            return (result.HomeScore, result.AwayScore, result.Quarters, lines);
        }

        // ── 2. Enriched timeline is well-formed and the new fields are populated ──
        private void RunTimelineQuality()
        {
            var offense = BuildLineup("GSW");
            var defense = BuildLineup("LAL");
            var offS = TeamStrategy.CreateDefault("GSW");
            var defS = TeamStrategy.CreateDefault("LAL");

            var sim = new PossessionSimulator(4242) { SpatialDetail = SpatialDetailLevel.Full };

            int clockViolations = 0, oobPlayers = 0;
            int anySpeedFrames = 0, defenderStanceFrames = 0;
            int shootingPossessions = 0, shootingWithVertical = 0;
            int actionPhaseOutOfRange = 0, verticalOutOfRange = 0;

            for (int i = 0; i < POSSESSIONS; i++)
            {
                if (i % 50 == 0)
                    foreach (var p in offense.Concat(defense)) p.Energy = 100f;

                var result = sim.SimulatePossession(offense, defense, offS, defS,
                    700f - (i * 3f % 650f), (i / 50) % 4 + 1, true, "GSW", "LAL", 0);
                var states = result.SpatialStates;
                if (states == null || states.Count < 5) continue;

                bool possHasShot = states.Any(st => st.Ball.Status == BallStatus.InAir_Shot);
                if (possHasShot) shootingPossessions++;
                float possMaxVertical = 0f;

                for (int s = 0; s < states.Count; s++)
                {
                    var cur = states[s];
                    if (s > 0 && cur.GameClock - states[s - 1].GameClock > 0.001f) clockViolations++;

                    for (int p = 0; p < 10; p++)
                    {
                        var snap = cur.Players[p];
                        if (snap.X < -47f || snap.X > 47f || snap.Y < -25f || snap.Y > 25f) oobPlayers++;

                        if (snap.SpeedFeetPerSec > 0.5f) anySpeedFrames++;
                        if (p >= 5 && snap.DefensiveStance) defenderStanceFrames++;

                        // Sanity bounds on the new normalized/height fields.
                        if (snap.ActionPhase < -0.001f || snap.ActionPhase > 1.001f) actionPhaseOutOfRange++;
                        if (snap.VerticalOffset < -0.001f || snap.VerticalOffset > 5f) verticalOutOfRange++;
                        if (snap.VerticalOffset > possMaxVertical) possMaxVertical = snap.VerticalOffset;
                    }
                }

                if (possHasShot && possMaxVertical > 0.1f) shootingWithVertical++;
            }

            AssertEqual(0, clockViolations, "GameClock non-increasing across enriched timeline");
            AssertEqual(0, oobPlayers, "All positions stay inside court bounds");
            AssertEqual(0, actionPhaseOutOfRange, "ActionPhase stays within [0,1]");
            AssertEqual(0, verticalOutOfRange, "VerticalOffset stays within a sane [0,5] ft range");
            AssertGreaterThan(anySpeedFrames, 0, "Some frames report real movement speed");
            AssertGreaterThan(defenderStanceFrames, 0, "Defenders enter a guarding stance");
            AssertGreaterThan(shootingPossessions, 20, "Sampled enough shooting possessions");
            AssertGreaterThan(shootingWithVertical, 0, "Shooting possessions produce a vertical jump");
        }

        // ── Fixtures (mirrors ChoreographyTest / GameSimulatorIntegrityTest) ──

        private List<Player> BuildRoster(PlayerDatabase db, string teamId)
        {
            var roster = NBAPlayerData.GetAllPlayers()
                .Where(p => p.TeamId == teamId)
                .OrderByDescending(p => p.OverallRating)
                .Select(ConvertPlayer)
                .ToList();
            foreach (var p in roster) db.AddPlayer(p);
            return roster;
        }

        private Player[] BuildLineup(string teamId)
        {
            return NBAPlayerData.GetAllPlayers()
                .Where(p => p.TeamId == teamId)
                .OrderByDescending(p => p.OverallRating)
                .Take(5)
                .Select(ConvertPlayer)
                .ToArray();
        }

        private Team CreateTeam(string teamId, List<Player> roster)
        {
            var team = new Team
            {
                TeamId = teamId,
                RosterPlayerIds = roster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateDefault(teamId),
                DefensiveStrategy = TeamStrategy.CreateDefault(teamId)
            };
            for (int i = 0; i < 5 && i < roster.Count; i++)
                team.StartingLineupIds[i] = roster[i].PlayerId;
            return team;
        }

        private Player ConvertPlayer(PlayerData d)
        {
            return new Player
            {
                PlayerId = d.PlayerId, FirstName = d.FirstName, LastName = d.LastName,
                JerseyNumber = d.JerseyNumber, Position = (Position)d.Position,
                BirthDate = System.DateTime.Now.AddYears(-d.Age),
                HeightInches = d.HeightInches, WeightLbs = d.WeightLbs, TeamId = d.TeamId,
                Finishing_Rim = d.Finishing_Rim, Finishing_PostMoves = d.Finishing_PostMoves,
                Shot_Close = d.Shot_Close, Shot_MidRange = d.Shot_MidRange,
                Shot_Three = d.Shot_Three, FreeThrow = d.FreeThrow,
                Passing = d.Passing, BallHandling = d.BallHandling,
                OffensiveIQ = d.OffensiveIQ, SpeedWithBall = d.SpeedWithBall,
                Defense_Perimeter = d.Defense_Perimeter, Defense_Interior = d.Defense_Interior,
                Defense_PostDefense = d.Defense_PostDefense,
                Steal = d.Steal, Block = d.Block, DefensiveIQ = d.DefensiveIQ,
                DefensiveRebound = d.DefensiveRebound,
                Speed = d.Speed, Acceleration = d.Acceleration,
                Strength = d.Strength, Vertical = d.Vertical, Stamina = d.Stamina,
                BasketballIQ = d.BasketballIQ, Clutch = d.Clutch,
                Energy = 100, Morale = 75, Form = 70
            };
        }
    }
}
