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
    /// Phase 4 — shot-style plumbing + distinct windups. Builds decided PossessionScripts and
    /// choreographs them, so outcomes are fixed and only the presentation authoring is exercised.
    ///
    /// 1. Fadeaway: the shooter's snapshots carry ShotStyle == Fadeaway ONLY during the shot window
    ///    (null before/on other players), and the shooter drifts BACK from the rim — release position
    ///    is farther from the rim than the windup gather.
    /// 2. Catch-and-shoot: ShotStyle == CatchAndShoot during the window, and the release goes up in
    ///    rhythm — within 0.6s of the catch (no dribble/gather delay).
    /// </summary>
    public class Phase4ShotStyleTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            TestFadeaway();
            TestCatchAndShoot();
            return (_passed, _failed);
        }

        // ── 1. Fadeaway: ShotStyle window + backward drift ─────────────
        private void TestFadeaway()
        {
            const int shooter = 0;   // ball-handler shoots off the dribble
            var script = new PossessionScript
            {
                Offense = BuildLineup("GSW"),
                Defense = BuildLineup("LAL"),
                OffenseAttacksRight = true,
                StartGameClock = 500f,
                Duration = 8f,
                Quarter = 2,
                OffenseStrategy = TeamStrategy.CreateDefault("GSW"),
                DefenseStrategy = TeamStrategy.CreateDefault("LAL"),
                InitialBallHandlerIndex = shooter,
                ShooterIndex = shooter,
                BallHandlerPassedToShooter = false,
                ShotPosition = new CourtPosition(30f, 6f),   // midrange
                ShotType = ShotType.Fadeaway,
                ContestLevel = 0.5f,
                Ending = ScriptEnding.MadeShot
            };

            var states = new PossessionChoreographer(new System.Random(2024)).Choreograph(script);
            Assert(states != null && states.Count > 5, "Fadeaway: choreography produced a timeline");
            if (states == null || states.Count < 5) return;

            string shooterId = script.Offense[shooter].PlayerId;
            var rim = new CourtPosition(CourtGeometry.RimXFor(true), 0f);

            // (a) ShotStyle == Fadeaway on the shooter during the window, null elsewhere.
            bool shooterEverStyled = states.Any(st => st.Players.Any(
                p => p.PlayerId == shooterId && p.ShotStyle == ShotType.Fadeaway));
            bool otherPlayersClean = states.All(st => st.Players.All(
                p => p.ShotStyle == null || p.PlayerId == shooterId));
            bool styleAlwaysFadeaway = states.All(st => st.Players.All(
                p => p.ShotStyle == null || (p.PlayerId == shooterId && p.ShotStyle == ShotType.Fadeaway)));
            bool firstFrameClean = states[0].Players.All(p => p.ShotStyle == null);

            Assert(shooterEverStyled, "Fadeaway: shooter carries ShotStyle == Fadeaway during the window");
            Assert(styleAlwaysFadeaway, "Fadeaway: the only stamped ShotStyle is Fadeaway on the shooter");
            Assert(otherPlayersClean, "Fadeaway: no non-shooter ever carries a ShotStyle");
            Assert(firstFrameClean, "Fadeaway: no ShotStyle before the shot window (frame 0 clean)");

            // (b) Backward drift: the windup gather is the shooter's CLOSEST approach to the rim
            //     within the styled window; the release is farther out.
            int releaseFrame = -1;
            for (int f = 0; f < states.Count; f++)
                if (states[f].Ball.Status == BallStatus.InAir_Shot) { releaseFrame = f; break; }
            Assert(releaseFrame > 0, "Fadeaway: a shot release frame exists");
            if (releaseFrame < 0) return;

            float releaseDist = ShooterAt(states, releaseFrame, shooterId).DistanceTo(rim);
            float minWindowDist = float.MaxValue;
            foreach (var st in states)
            {
                var snap = st.Players.First(p => p.PlayerId == shooterId);
                if (snap.ShotStyle == ShotType.Fadeaway)
                {
                    float d = snap.GetPosition().DistanceTo(rim);
                    if (d < minWindowDist) minWindowDist = d;
                }
            }
            AssertGreaterThan(releaseDist, minWindowDist + 0.5f,
                "Fadeaway: release is farther from the rim than the windup gather (drifted back)");
        }

        // ── 2. Catch-and-shoot: quick release, no gather ───────────────
        private void TestCatchAndShoot()
        {
            const int holderIdx = 0;
            const int shooterIdx = 2;
            var script = new PossessionScript
            {
                Offense = BuildLineup("GSW"),
                Defense = BuildLineup("LAL"),
                OffenseAttacksRight = true,
                StartGameClock = 500f,
                Duration = 8f,
                Quarter = 2,
                OffenseStrategy = TeamStrategy.CreateDefault("GSW"),
                DefenseStrategy = TeamStrategy.CreateDefault("LAL"),
                InitialBallHandlerIndex = holderIdx,
                ShooterIndex = shooterIdx,
                BallHandlerPassedToShooter = true,
                ShotPosition = new CourtPosition(24f, 20f),   // corner-ish three
                ShotType = ShotType.CatchAndShoot,
                ContestLevel = 0.3f,
                Ending = ScriptEnding.MadeShot
            };

            var states = new PossessionChoreographer(new System.Random(777)).Choreograph(script);
            Assert(states != null && states.Count > 5, "CatchAndShoot: choreography produced a timeline");
            if (states == null || states.Count < 5) return;

            string shooterId = script.Offense[shooterIdx].PlayerId;

            // (a) ShotStyle == CatchAndShoot on the shooter during the window.
            bool styled = states.Any(st => st.Players.Any(
                p => p.PlayerId == shooterId && p.ShotStyle == ShotType.CatchAndShoot));
            bool styleClean = states.All(st => st.Players.All(
                p => p.ShotStyle == null || (p.PlayerId == shooterId && p.ShotStyle == ShotType.CatchAndShoot)));
            Assert(styled, "CatchAndShoot: shooter carries ShotStyle == CatchAndShoot during the window");
            Assert(styleClean, "CatchAndShoot: the only stamped ShotStyle is CatchAndShoot on the shooter");

            // (c) Release within 0.6s of the catch: catch = first frame the ball is held by the
            //     shooter; release = first in-flight shot frame.
            float catchT = -1f, releaseT = -1f;
            foreach (var st in states)
            {
                if (catchT < 0f && st.Ball.HeldByPlayerId == shooterId) catchT = st.PresentationTime;
                if (catchT >= 0f && st.Ball.Status == BallStatus.InAir_Shot) { releaseT = st.PresentationTime; break; }
            }
            Assert(catchT >= 0f, "CatchAndShoot: the shooter catches the ball");
            Assert(releaseT >= 0f, "CatchAndShoot: the shooter releases the shot");
            if (catchT >= 0f && releaseT >= 0f)
                AssertLessThan(releaseT - catchT, 0.6f, "CatchAndShoot: release within 0.6s of the catch");
        }

        // ── helpers ────────────────────────────────────────────────────
        private static CourtPosition ShooterAt(List<SpatialState> states, int frame, string id)
            => states[frame].Players.First(p => p.PlayerId == id).GetPosition();

        private Player[] BuildLineup(string teamId)
        {
            var all = NBAPlayerData.GetAllPlayers()
                .Where(p => p.TeamId == teamId)
                .OrderByDescending(p => p.OverallRating)
                .Take(5).ToList();
            var players = new Player[5];
            for (int i = 0; i < 5; i++) players[i] = ConvertPlayer(all[i]);
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
