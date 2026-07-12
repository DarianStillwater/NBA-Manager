using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;
using ShotType = NBAHeadCoach.Core.Simulation.ShotType;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Phase 3 — rebound battles + dead-ball completeness + end-of-quarter heave. Builds decided
    /// PossessionScripts directly and choreographs them, so outcomes (winner, spot, timing) are fixed
    /// and only the presentation authoring is exercised.
    ///
    /// 1. Rebound battle: a missed-shot possession sends offensive crashers + boxing-out defenders to
    ///    ring the decided rebound spot; the pairs render as mutual-target box-outs; the decided winner
    ///    takes the board; and the losers' leaps peak BEFORE the winner's (he out-jumps them).
    /// 2. Heave: a sub-2s clock-expiry possession plays honestly — total presentation &lt; 3.5s and the
    ///    ball is still in flight when the game clock hits 0 (release before the buzzer).
    /// </summary>
    public class Phase3ChoreographyTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestReboundBattle();
            TestBuzzerHeave();

            return (_passed, _failed);
        }

        // ── 1. Rebound battle ──────────────────────────────────────────
        private void TestReboundBattle()
        {
            const int rebounder = 4;   // defensive center wins the board
            var script = new PossessionScript
            {
                Offense = BuildLineup("GSW"),
                Defense = BuildLineup("LAL"),
                OffenseAttacksRight = true,
                StartGameClock = 400f,
                Duration = 9f,
                Quarter = 2,
                OffenseStrategy = TeamStrategy.CreateDefault("GSW"),
                DefenseStrategy = TeamStrategy.CreateDefault("LAL"),
                InitialBallHandlerIndex = 0,
                ShooterIndex = 2,
                BallHandlerPassedToShooter = true,
                ShotPosition = new CourtPosition(32f, 6f),   // midrange 2 → miss caroms near the rim
                ShotType = ShotType.Jumper,
                ContestLevel = 0.4f,
                Ending = ScriptEnding.MissedShot,
                RebounderIndex = rebounder,
                RebounderIsDefense = true
            };

            var states = new PossessionChoreographer(new System.Random(4321)).Choreograph(script);
            Assert(states != null && states.Count > 5, "Rebound: choreography produced a timeline");
            if (states == null || states.Count < 5) return;

            var last = states[states.Count - 1];
            var spot = new CourtPosition(last.Ball.X, last.Ball.Y);

            // Convergence around rebound time: scan the settled dead-ball tail for the frame with the
            // most crashers inside 4 ft of the ball. Need ≥2 offense AND ≥2 defenders.
            int maxOff = 0, maxDef = 0;
            for (int f = System.Math.Max(0, states.Count - 10); f < states.Count; f++)
            {
                var ballPos = new CourtPosition(states[f].Ball.X, states[f].Ball.Y);
                int off = 0, def = 0;
                for (int p = 0; p < 5; p++)
                    if (states[f].Players[p].GetPosition().DistanceTo(ballPos) < 4f) off++;
                for (int p = 5; p < 10; p++)
                    if (states[f].Players[p].GetPosition().DistanceTo(ballPos) < 4f) def++;
                if (off > maxOff) maxOff = off;
                if (def > maxDef) maxDef = def;
            }
            AssertGreaterThan(maxOff, 1.5f, "Rebound: ≥2 offensive crashers converge within 4 ft of the spot");
            AssertGreaterThan(maxDef, 1.5f, "Rebound: ≥2 defenders converge within 4 ft of the spot");

            // A box-out pair renders as mutual BoxingOut naming each other (drives P2 contact/lean).
            bool mutualBoxOut = false;
            foreach (var st in states)
            {
                for (int p = 0; p < 5 && !mutualBoxOut; p++)
                {
                    var op = st.Players[p];
                    if (op.CurrentAction != PlayerAction.BoxingOut) continue;
                    for (int q = 5; q < 10; q++)
                    {
                        var dq = st.Players[q];
                        if (dq.CurrentAction == PlayerAction.BoxingOut &&
                            op.TargetPlayerId == dq.PlayerId && dq.TargetPlayerId == op.PlayerId)
                        { mutualBoxOut = true; break; }
                    }
                }
                if (mutualBoxOut) break;
            }
            Assert(mutualBoxOut, "Rebound: a box-out pair exists with mutual TargetPlayerId + BoxingOut");

            // The decided winner secures the board (ends on the ball).
            int winIdx = 5 + rebounder;   // defensive rebounder snapshot index
            float winDist = last.Players[winIdx].GetPosition().DistanceTo(spot);
            AssertLessThan(winDist, 2f, "Rebound: decided winner ends holding the ball");

            // Per-player vertical peaks: the winner out-jumps — highest AND latest peak; at least one
            // loser peaks before him.
            var peak = new float[10];
            var peakT = new float[10];
            foreach (var st in states)
                for (int p = 0; p < 10; p++)
                    if (st.Players[p].VerticalOffset > peak[p])
                    { peak[p] = st.Players[p].VerticalOffset; peakT[p] = st.PresentationTime; }

            bool winnerHighest = true, winnerLatest = true, aLoserLeapedBefore = false;
            for (int p = 0; p < 10; p++)
            {
                if (p == winIdx) continue;
                if (peak[p] > peak[winIdx] + 0.01f) winnerHighest = false;
                if (peak[p] > 0.6f)
                {
                    if (peakT[p] > peakT[winIdx] + 0.001f) winnerLatest = false;
                    if (peakT[p] < peakT[winIdx] - 0.001f) aLoserLeapedBefore = true;
                }
            }
            AssertGreaterThan(peak[winIdx], 0.6f, "Rebound: winner leaps for the board");
            Assert(winnerHighest, "Rebound: winner out-jumps everyone (highest leap)");
            Assert(winnerLatest, "Rebound: no other jumper peaks after the winner");
            Assert(aLoserLeapedBefore, "Rebound: a loser's leap peaks BEFORE the winner's");
        }

        // ── 2. End-of-quarter heave ────────────────────────────────────
        private void TestBuzzerHeave()
        {
            var chor = new PossessionChoreographer(new System.Random(99));
            var script = new PossessionScript
            {
                Offense = BuildLineup("GSW"),
                Defense = BuildLineup("LAL"),
                OffenseAttacksRight = true,
                StartGameClock = 0.4f,     // final tick of the quarter
                Duration = 0.4f,           // consumes the clock → buzzer possession
                Quarter = 1,
                OffenseStrategy = TeamStrategy.CreateDefault("GSW"),
                DefenseStrategy = TeamStrategy.CreateDefault("LAL"),
                InitialBallHandlerIndex = 0,
                ShooterIndex = 0,
                BallHandlerPassedToShooter = false,
                ShotPosition = new CourtPosition(0f, 0f),   // half court
                ShotType = ShotType.Heave,
                Ending = ScriptEnding.MadeShot
            };

            var states = chor.Choreograph(script);
            Assert(states != null && states.Count > 2, "Heave: choreography produced a timeline");
            if (states == null || states.Count < 2) return;

            // The duration floor did NOT inflate the possession into a multi-second set piece.
            AssertLessThan(chor.TotalSeconds, 3.5f, "Heave: total presentation time under 3.5s");

            // Ball must be in flight when the game clock reaches 0 (released before the buzzer).
            bool inFlightAtBuzzer = false;
            bool sawZeroClock = false;
            foreach (var st in states)
            {
                if (st.GameClock <= 0.001f)
                {
                    sawZeroClock = true;
                    if (st.Ball.Status == BallStatus.InAir_Shot) { inFlightAtBuzzer = true; break; }
                }
            }
            Assert(sawZeroClock, "Heave: the game clock reaches 0 within the possession");
            Assert(inFlightAtBuzzer, "Heave: the ball is in flight when the game clock hits 0");

            // The shot type is preserved on the ball so the renderer can distinguish a heave (Phase 4).
            bool heaveStyle = states.Any(s => s.Ball.ShotStyle == ShotType.Heave);
            Assert(heaveStyle, "Heave: ShotType.Heave flows through to the ball's ShotStyle");
        }

        // ── Fixtures ───────────────────────────────────────────────────
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
                TeamId = d.TeamId, HeightInches = d.HeightInches, WeightLbs = d.WeightLbs,
                Finishing_Rim = d.Finishing_Rim, Shot_Close = d.Shot_Close,
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
