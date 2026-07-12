using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Phase 1 continuous flow: possessions chain instead of teleporting. A possession's timeline
    /// must OPEN where the previous one ended — each player at his prior-frame position (carried by
    /// PlayerId via PossessionStartContext) — for live-continuation kinds (rebound/steal). Dead-ball
    /// inbounds hold the game clock during the lead-in before the clock starts ticking.
    /// </summary>
    public class PossessionChainTest : BaseTest
    {
        private const float Eps = 1f;   // feet — allowed frame-0 vs prior-frame drift (incl. idle sway)

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var gsw = BuildLineup("GSW");
            var lal = BuildLineup("LAL");
            var gswS = TeamStrategy.CreateDefault("GSW");
            var lalS = TeamStrategy.CreateDefault("LAL");

            TestLiveContinuityIsSeamless(gsw, lal, gswS, lalS);
            TestChainThreadsAcrossPossessions(gsw, lal, gswS, lalS);
            TestInboundHoldsTheClock(gsw, lal, gswS, lalS);
            TestLiveStartTicksImmediately(gsw, lal, gswS, lalS);
            TestPresentationTimeSurvivesClockHold(gsw, lal, gswS, lalS);

            return (_passed, _failed);
        }

        /// <summary>A LiveRebound opening seeds every player at his prior-frame spot: frame 0 of the
        /// new possession sits on top of the seed positions (within idle-sway tolerance).</summary>
        private void TestLiveContinuityIsSeamless(Player[] off, Player[] def, TeamStrategy oS, TeamStrategy dS)
        {
            var sim = new PossessionSimulator(11) { SpatialDetail = SpatialDetailLevel.Full };
            var prior = sim.SimulatePossession(off, def, oS, dS, 600f, 1, true, "GSW", "LAL", 0);
            var lastFrame = prior.SpatialStates[prior.SpatialStates.Count - 1];

            var ctx = MakeContext(PossessionStartKind.LiveRebound, lastFrame, def[4].PlayerId);

            // Possession flips: prior defense (LAL) now has the ball; prior offense (GSW) defends.
            var next = sim.SimulatePossession(def, off, dS, oS, 588f, 1, false, "LAL", "GSW", 0,
                startContext: ctx);

            int off0 = FarthestPlayerDrift(next.SpatialStates[0], lastFrame, out float worst);
            Assert(off0 == 0,
                $"Live rebound: every player opens at his prior-frame spot (worst {worst:F2} ft)");
        }

        /// <summary>Thread a start context possession-to-possession and assert the chain never breaks:
        /// each packet's frame 0 lands on the previous packet's final frame, per PlayerId.</summary>
        private void TestChainThreadsAcrossPossessions(Player[] off, Player[] def, TeamStrategy oS, TeamStrategy dS)
        {
            var sim = new PossessionSimulator(29) { SpatialDetail = SpatialDetailLevel.Full };

            PossessionResult prev = sim.SimulatePossession(off, def, oS, dS, 700f, 1, true, "GSW", "LAL", 0);
            bool offHasBall = false;   // next possession: LAL has the ball
            float clock = 688f;
            int breaks = 0;

            for (int n = 0; n < 6; n++)
            {
                var lastFrame = prev.SpatialStates[prev.SpatialStates.Count - 1];
                // Alternate the two live kinds so both openings are exercised.
                var kind = (n % 2 == 0) ? PossessionStartKind.LiveRebound : PossessionStartKind.LiveSteal;
                var carrier = lastFrame.Ball.HeldByPlayerId;
                var ctx = MakeContext(kind, lastFrame, carrier);

                // Offense/defense swap each trip.
                Player[] o = offHasBall ? off : def;
                Player[] d = offHasBall ? def : off;
                string oId = offHasBall ? "GSW" : "LAL";
                string dId = offHasBall ? "LAL" : "GSW";

                var cur = sim.SimulatePossession(o, d, offHasBall ? oS : dS, offHasBall ? dS : oS,
                    clock, 1, offHasBall, oId, dId, 0, startContext: ctx);

                if (FarthestPlayerDrift(cur.SpatialStates[0], lastFrame, out _) > 0) breaks++;

                prev = cur;
                offHasBall = !offHasBall;
                clock -= 12f;
            }

            AssertEqual(0, breaks, "Chained possessions: frame 0 ≈ prior final frame across 6 trips");
        }

        /// <summary>A made-basket inbound holds the displayed game clock through the lead-in
        /// (ClockStartOffset &gt; 0) — the clock only starts on the catch.</summary>
        private void TestInboundHoldsTheClock(Player[] off, Player[] def, TeamStrategy oS, TeamStrategy dS)
        {
            var sim = new PossessionSimulator(43) { SpatialDetail = SpatialDetailLevel.Full };
            var prior = sim.SimulatePossession(off, def, oS, dS, 600f, 1, true, "GSW", "LAL", 0);
            var lastFrame = prior.SpatialStates[prior.SpatialStates.Count - 1];

            var ctx = MakeContext(PossessionStartKind.MadeBasketInbound, lastFrame, null);
            var next = sim.SimulatePossession(def, off, dS, oS, 588f, 1, false, "LAL", "GSW", 0,
                startContext: ctx);

            AssertGreaterThan(next.ClockStartOffset, 0.01f,
                "Inbound produces a clock-start offset (lead-in before the clock ticks)");

            // Leading frames all read the same game clock while it's held.
            var states = next.SpatialStates;
            float start = states[0].GameClock;
            int held = 0;
            while (held < states.Count && Mathf.Abs(states[held].GameClock - start) < 0.001f) held++;
            Assert(held >= 2, $"Game clock holds constant during the inbound lead-in ({held} frames)");
        }

        /// <summary>A live start has no clock offset — the clock ticks from frame 0.</summary>
        private void TestLiveStartTicksImmediately(Player[] off, Player[] def, TeamStrategy oS, TeamStrategy dS)
        {
            var sim = new PossessionSimulator(51) { SpatialDetail = SpatialDetailLevel.Full };
            var prior = sim.SimulatePossession(off, def, oS, dS, 600f, 1, true, "GSW", "LAL", 0);
            var lastFrame = prior.SpatialStates[prior.SpatialStates.Count - 1];

            var ctx = MakeContext(PossessionStartKind.LiveSteal, lastFrame, lastFrame.Ball.HeldByPlayerId);
            var next = sim.SimulatePossession(def, off, dS, oS, 588f, 1, false, "LAL", "GSW", 0,
                startContext: ctx);

            AssertLessThan(next.ClockStartOffset, 0.01f, "Live steal has no clock-hold offset");
            Assert(next.SpatialStates[1].GameClock < next.SpatialStates[0].GameClock,
                "Live start: game clock ticks immediately (no lead-in hold)");
        }

        /// <summary>An inbound possession's PresentationTime keeps advancing through the clock-hold
        /// lead-in even though GameClock is frozen — the renderer/camera can address those frames.</summary>
        private void TestPresentationTimeSurvivesClockHold(Player[] off, Player[] def, TeamStrategy oS, TeamStrategy dS)
        {
            var sim = new PossessionSimulator(67) { SpatialDetail = SpatialDetailLevel.Full };
            var prior = sim.SimulatePossession(off, def, oS, dS, 600f, 1, true, "GSW", "LAL", 0);
            var lastFrame = prior.SpatialStates[prior.SpatialStates.Count - 1];

            var ctx = MakeContext(PossessionStartKind.MadeBasketInbound, lastFrame, null);
            var next = sim.SimulatePossession(def, off, dS, oS, 588f, 1, false, "LAL", "GSW", 0,
                startContext: ctx);

            var states = next.SpatialStates;
            bool nonDecreasing = true;
            for (int i = 1; i < states.Count; i++)
                if (states[i].PresentationTime < states[i - 1].PresentationTime) nonDecreasing = false;
            Assert(nonDecreasing, "PresentationTime is non-decreasing across the timeline");
            Assert(states[states.Count - 1].PresentationTime > states[0].PresentationTime,
                "PresentationTime advances overall from first to last frame");

            bool foundHeldInboundFrame = states.Any(s =>
                s.Phase == PossessionPhase.Inbound &&
                s.PresentationTime > 0f &&
                Mathf.Abs(s.GameClock - next.StartGameClock) < 0.001f);
            Assert(foundHeldInboundFrame,
                "An inbound lead-in frame has PresentationTime > 0 while GameClock is still held at the start value");
        }

        // ── Helpers ────────────────────────────────────────────────

        /// <summary>Build a start context mirroring MatchSimulationController.DeriveNextStartContext:
        /// all ten prior positions by id, the prior ball spot, and the recovering carrier.</summary>
        private static PossessionStartContext MakeContext(PossessionStartKind kind, SpatialState last, string carrier)
        {
            var prior = new (string playerId, CourtPosition pos)[last.Players.Length];
            for (int i = 0; i < last.Players.Length; i++)
                prior[i] = (last.Players[i].PlayerId, new CourtPosition(last.Players[i].X, last.Players[i].Y));

            return new PossessionStartContext
            {
                Kind = kind,
                PriorPositions = prior,
                BallSpot = new CourtPosition(last.Ball.X, last.Ball.Y),
                BallCarrierId = carrier
            };
        }

        /// <summary>Count players in <paramref name="frame0"/> more than Eps ft from their position in
        /// <paramref name="prior"/> (matched by PlayerId). Reports the worst drift.</summary>
        private static int FarthestPlayerDrift(SpatialState frame0, SpatialState prior, out float worst)
        {
            worst = 0f;
            int bad = 0;
            foreach (var p in frame0.Players)
            {
                var match = prior.Players.FirstOrDefault(q => q.PlayerId == p.PlayerId);
                if (string.IsNullOrEmpty(match.PlayerId)) continue;
                float dx = p.X - match.X, dy = p.Y - match.Y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > worst) worst = d;
                if (d > Eps) bad++;
            }
            return bad;
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
