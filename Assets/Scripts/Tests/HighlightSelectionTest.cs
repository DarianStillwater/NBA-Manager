using System;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards which plays are flagged as highlights. Fast-break buckets and buzzer-beaters now
    /// qualify (previously they didn't unless they were also a dunk/three/clutch), while blocks,
    /// dunks and threes stay highlights and a quiet mid-quarter jumper does not.
    /// </summary>
    public class HighlightSelectionTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            Run();
            return (_passed, _failed);
        }

        private static Player Stub(string id) =>
            new Player { PlayerId = id, FirstName = "A", LastName = id, Position = Position.SmallForward };

        // A calm mid-second-quarter context: nothing is "clutch", so only the play itself can flag.
        private static PlayByPlayContext CalmContext() => new PlayByPlayContext
        {
            Quarter = 2, GameClock = 400f, HomeScore = 40, AwayScore = 38, HomeOnOffense = true
        };

        private bool IsHighlight(PossessionEvent evt) =>
            PlayByPlayGenerator.Generate(evt, CalmContext(), Stub).IsHighlight;

        private void Run()
        {
            // Fast-break layup — a plain 2, but on the break → highlight now.
            Assert(IsHighlight(new PossessionEvent
            {
                Type = EventType.Shot, Outcome = EventOutcome.Success, ShotType = NBAHeadCoach.Core.Simulation.ShotType.Layup,
                PointsScored = 2, IsFastBreak = true, GameClock = 400f, ActorPlayerId = "p1"
            }), "Fast-break bucket is a highlight");

            // Buzzer-beater — made shot as the clock hits zero.
            Assert(IsHighlight(new PossessionEvent
            {
                Type = EventType.Shot, Outcome = EventOutcome.Success, ShotType = NBAHeadCoach.Core.Simulation.ShotType.Jumper,
                PointsScored = 2, GameClock = 0.3f, ActorPlayerId = "p1"
            }), "Buzzer-beater is a highlight");

            // Block stays a highlight.
            Assert(IsHighlight(new PossessionEvent
            {
                Type = EventType.Block, GameClock = 400f, ActorPlayerId = "p1", DefenderPlayerId = "p2"
            }), "Block is a highlight");

            // Dunk stays a highlight.
            Assert(IsHighlight(new PossessionEvent
            {
                Type = EventType.Shot, Outcome = EventOutcome.Success, ShotType = NBAHeadCoach.Core.Simulation.ShotType.Dunk,
                PointsScored = 2, GameClock = 400f, ActorPlayerId = "p1"
            }), "Dunk is a highlight");

            // A quiet half-court 2 in the middle of the quarter is NOT a highlight.
            Assert(!IsHighlight(new PossessionEvent
            {
                Type = EventType.Shot, Outcome = EventOutcome.Success, ShotType = NBAHeadCoach.Core.Simulation.ShotType.Jumper,
                PointsScored = 2, IsFastBreak = false, GameClock = 400f, ActorPlayerId = "p1"
            }), "A quiet mid-quarter jumper is not a highlight");
        }
    }
}
