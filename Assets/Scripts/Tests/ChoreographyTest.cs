using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;
using EventType = NBAHeadCoach.Core.Simulation.EventType;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Validates the possession choreography engine: continuous ball motion,
    /// players in bounds, shooter at the decided shot location, made shots passing
    /// through the rim, and — critically — that choreography is purely presentational
    /// (identical outcomes with SpatialDetail None vs Full).
    /// </summary>
    public class ChoreographyTest : BaseTest
    {
        private const int POSSESSIONS = 300;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var offense = BuildLineup("GSW");
            var defense = BuildLineup("LAL");
            var offStrategy = TeamStrategy.CreateDefault("GSW");
            var defStrategy = TeamStrategy.CreateDefault("LAL");

            // ── Outcome invariance: None vs Full must decide identically ──
            var simNone = new PossessionSimulator(42) { SpatialDetail = SpatialDetailLevel.None };
            var simFull = new PossessionSimulator(42) { SpatialDetail = SpatialDetailLevel.Full };
            bool outcomesMatch = true;
            for (int i = 0; i < 100; i++)
            {
                var a = Run(simNone, offense, defense, offStrategy, defStrategy, i);
                var b = Run(simFull, offense, defense, offStrategy, defStrategy, i);

                if (a.Outcome != b.Outcome || a.PointsScored != b.PointsScored ||
                    a.Events.Count != b.Events.Count ||
                    !a.Events.Select(e => (e.Type, e.ActorPlayerId, e.PointsScored))
                        .SequenceEqual(b.Events.Select(e => (e.Type, e.ActorPlayerId, e.PointsScored))))
                {
                    outcomesMatch = false;
                    Debug.Log($"    Divergence at possession {i}: {a.Outcome}/{a.PointsScored} vs {b.Outcome}/{b.PointsScored}");
                    break;
                }
            }
            Assert(outcomesMatch, "Choreography never changes outcomes (None == Full, seeded)");

            // ── Spatial quality checks over many choreographed possessions ──
            var sim = new PossessionSimulator(1234) { SpatialDetail = SpatialDetailLevel.Full };

            int ballJumps = 0, oobPlayers = 0, playerJumps = 0, clockViolations = 0;
            int shooterMisplaced = 0, madeWithoutRim = 0, madeChecked = 0;
            int emptyTimelines = 0;

            for (int i = 0; i < POSSESSIONS; i++)
            {
                if (i % 50 == 0)
                    foreach (var p in offense.Concat(defense)) p.Energy = 100f;

                var result = Run(sim, offense, defense, offStrategy, defStrategy, i);
                var states = result.SpatialStates;

                if (states == null || states.Count < 5) { emptyTimelines++; continue; }

                for (int s = 1; s < states.Count; s++)
                {
                    var prev = states[s - 1];
                    var cur = states[s];
                    float dt = prev.GameClock - cur.GameClock;

                    // Clock sanity (non-increasing; allow tail clamp at 0)
                    if (dt < -0.001f) clockViolations++;

                    // Ball continuity: no teleports (≤ 60 ft/s incl. shots; ticks ≤ 0.2s → ≤ 12 ft + margin)
                    float bdx = cur.Ball.X - prev.Ball.X;
                    float bdy = cur.Ball.Y - prev.Ball.Y;
                    if (Mathf.Sqrt(bdx * bdx + bdy * bdy) > 14f) ballJumps++;

                    for (int pIdx = 0; pIdx < 10; pIdx++)
                    {
                        var snap = cur.Players[pIdx];
                        if (snap.X < -47f || snap.X > 47f || snap.Y < -25f || snap.Y > 25f) oobPlayers++;

                        var prevSnap = prev.Players[pIdx];
                        float pdx = snap.X - prevSnap.X;
                        float pdy = snap.Y - prevSnap.Y;
                        if (Mathf.Sqrt(pdx * pdx + pdy * pdy) > 8f) playerJumps++;
                    }
                }

                // Shooter at the decided shot location when the shot leaves the hand
                var shotEvent = result.Events.FirstOrDefault(e =>
                    e.Type == EventType.Shot || e.Type == EventType.Block);
                if (shotEvent != null)
                {
                    var releaseState = states.FirstOrDefault(st => st.Ball.Status == BallStatus.InAir_Shot);
                    if (releaseState != null)
                    {
                        var shooterSnap = releaseState.Players.FirstOrDefault(p => p.PlayerId == shotEvent.ActorPlayerId);
                        if (!string.IsNullOrEmpty(shooterSnap.PlayerId))
                        {
                            float dx = shooterSnap.X - shotEvent.ActorPosition.X;
                            float dy = shooterSnap.Y - shotEvent.ActorPosition.Y;
                            if (Mathf.Sqrt(dx * dx + dy * dy) > 3f) shooterMisplaced++;
                        }
                    }
                }

                // Made field goals pass through the rim (within 2 ft of rim XY at height 9-11)
                if (result.Outcome == PossessionOutcome.Score &&
                    result.Events.Any(e => e.Type == EventType.Shot && e.Outcome == EventOutcome.Success))
                {
                    madeChecked++;
                    bool throughRim = states.Any(st =>
                        st.Ball.Height >= 8.5f && st.Ball.Height <= 11.5f &&
                        Mathf.Abs(Mathf.Abs(st.Ball.X) - CourtGeometry.RimX) < 2.5f &&
                        Mathf.Abs(st.Ball.Y) < 2.5f);
                    if (!throughRim) madeWithoutRim++;
                }
            }

            AssertEqual(0, emptyTimelines, "Every possession produces a spatial timeline");
            AssertEqual(0, clockViolations, "GameClock non-increasing within possessions");
            AssertEqual(0, ballJumps, "Ball never teleports between consecutive states");
            AssertEqual(0, oobPlayers, "All players stay inside court bounds");
            AssertEqual(0, playerJumps, "No player moves faster than sprint speed between ticks");
            Assert(shooterMisplaced <= POSSESSIONS / 50, $"Shooter at decided shot spot on release ({shooterMisplaced} misplaced)");
            Assert(madeChecked > 20, $"Sampled enough made shots ({madeChecked})");
            AssertEqual(0, madeWithoutRim, "Every made FG passes through the rim");

            // Headless mode produces no states at all
            var headless = Run(simNone, offense, defense, offStrategy, defStrategy, 999);
            Assert(headless.SpatialStates == null || headless.SpatialStates.Count == 0,
                "SpatialDetail.None emits no spatial states");

            return (_passed, _failed);
        }

        private static PossessionResult Run(PossessionSimulator sim, Player[] off, Player[] def,
            TeamStrategy offS, TeamStrategy defS, int i)
        {
            return sim.SimulatePossession(off, def, offS, defS,
                700f - (i * 3f % 650f), (i / 50) % 4 + 1, true, "GSW", "LAL", 0);
        }

        private Player[] BuildLineup(string teamId)
        {
            var all = NBAPlayerData.GetAllPlayers()
                .Where(p => p.TeamId == teamId)
                .OrderByDescending(p => p.OverallRating)
                .Take(5).ToList();

            var players = new Player[5];
            for (int i = 0; i < 5; i++)
                players[i] = ConvertPlayer(all[i]);
            return players;
        }

        private Player ConvertPlayer(PlayerData d)
        {
            return new Player
            {
                PlayerId = d.PlayerId, FirstName = d.FirstName, LastName = d.LastName,
                Position = (Position)d.Position, BirthDate = System.DateTime.Now.AddYears(-d.Age),
                TeamId = d.TeamId, Finishing_Rim = d.Finishing_Rim, Shot_Close = d.Shot_Close,
                Shot_MidRange = d.Shot_MidRange, Shot_Three = d.Shot_Three, FreeThrow = d.FreeThrow,
                Passing = d.Passing, BallHandling = d.BallHandling, OffensiveIQ = d.OffensiveIQ,
                SpeedWithBall = d.SpeedWithBall, Defense_Perimeter = d.Defense_Perimeter,
                Defense_Interior = d.Defense_Interior, Steal = d.Steal, Block = d.Block,
                DefensiveIQ = d.DefensiveIQ, DefensiveRebound = d.DefensiveRebound,
                Speed = d.Speed, Strength = d.Strength, Stamina = d.Stamina,
                BasketballIQ = d.BasketballIQ, Clutch = d.Clutch,
                Energy = 100, Morale = 75, Form = 70
            };
        }
    }
}
