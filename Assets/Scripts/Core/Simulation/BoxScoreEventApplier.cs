using System.Linq;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Applies a decided possession's events to a BoxScore — the ONE place per-player
    /// stats are counted, shared by the headless GameSimulator and the interactive
    /// MatchSimulationController so both paths produce identical box scores.
    /// Team score totals stay caller-side (the two engines track them differently),
    /// as do free throws (each engine has its own FT processing pass).
    /// </summary>
    public static class BoxScoreEventApplier
    {
        public static void Apply(BoxScore box, PossessionResult result, bool isHomePossession,
            string defenseTeamId, int quarter)
        {
            foreach (var evt in result.Events)
            {
                switch (evt.Type)
                {
                    case EventType.Shot:
                        ProcessShot(box, evt, isHomePossession);
                        break;
                    case EventType.Steal:
                        box.AddSteal(evt.ActorPlayerId);
                        if (evt.TargetPlayerId != null)
                            box.AddTurnover(evt.TargetPlayerId);
                        break;
                    case EventType.Block:
                        box.AddBlock(evt.DefenderPlayerId);
                        break;
                    case EventType.Rebound:
                        box.AddRebound(evt.ActorPlayerId, isOffensive: evt.IsOffensiveRebound);
                        break;
                    case EventType.Turnover:
                        box.AddTurnover(evt.ActorPlayerId);
                        break;
                    case EventType.Foul:
                        ProcessFoul(box, evt, defenseTeamId, quarter);
                        break;
                }
            }

            // Assist: the successful pass to the made shot's shooter
            if (result.PointsScored > 0)
            {
                var shotEvent = result.Events.LastOrDefault(e => e.Type == EventType.Shot);
                if (shotEvent != null)
                {
                    var passEvent = result.Events.LastOrDefault(e =>
                        e.Type == EventType.Pass &&
                        e.TargetPlayerId == shotEvent.ActorPlayerId &&
                        e.Outcome == EventOutcome.Success);

                    if (passEvent != null)
                        box.AddAssist(passEvent.ActorPlayerId);
                }
            }
        }

        private static void ProcessShot(BoxScore box, PossessionEvent evt, bool isHomePossession)
        {
            var zone = evt.ActorPosition.GetZone(isHomePossession);
            bool isThree = zone == CourtZone.ThreePoint;
            bool made = evt.Outcome == EventOutcome.Success;

            box.AddShotAttempt(evt.ActorPlayerId, isThree);
            if (made)
                box.AddShotMade(evt.ActorPlayerId, isThree, evt.PointsScored);
        }

        private static void ProcessFoul(BoxScore box, PossessionEvent evt, string defenseTeamId, int quarter)
        {
            if (evt.FoulDetail == null) return;

            // Record personal foul in box score
            if (evt.DefenderPlayerId != null &&
                evt.FoulDetail.FoulType != FoulType.Technical)
            {
                box.AddFoul(evt.DefenderPlayerId);
            }

            // Track team fouls in box score
            box.AddTeamFoul(defenseTeamId, quarter);
        }
    }
}
