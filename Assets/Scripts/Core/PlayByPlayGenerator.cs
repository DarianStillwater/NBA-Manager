using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Generates broadcast-style play-by-play text descriptions.
    /// Provides dramatic commentary for highlights and clutch moments.
    /// </summary>
    public static class PlayByPlayGenerator
    {
        private static readonly System.Random _rng = new System.Random();

        #region Main API

        /// <summary>
        /// Generate a play-by-play entry with enhanced descriptions
        /// </summary>
        public static PlayByPlayEntry Generate(
            PossessionEvent evt,
            GameContext context,
            Func<string, Player> getPlayer)
        {
            var actor = getPlayer(evt.ActorPlayerId);
            var target = evt.TargetPlayerId != null ? getPlayer(evt.TargetPlayerId) : null;
            var defender = evt.DefenderPlayerId != null ? getPlayer(evt.DefenderPlayerId) : null;

            bool isHighlight = DetermineIfHighlight(evt, context);
            bool isClutch = context.IsClutchTime;

            string description = GenerateDescription(evt, actor, target, defender, context, isHighlight);

            return new PlayByPlayEntry
            {
                Clock = FormatClock(evt.GameClock),
                Quarter = evt.Quarter,
                Team = context.OffenseTeam?.Abbreviation ?? "",
                Description = description,
                IsHighlight = isHighlight,
                Type = GetPlayByPlayType(evt),
                ActorPlayerId = evt.ActorPlayerId,
                Points = evt.PointsScored,
                HomeScore = context.HomeScore + (context.HomeOnOffense ? evt.PointsScored : 0),
                AwayScore = context.AwayScore + (context.HomeOnOffense ? 0 : evt.PointsScored)
            };
        }

        /// <summary>
        /// Generate a game event description (quarter start/end, etc.)
        /// </summary>
        public static PlayByPlayEntry GenerateGameEvent(
            GameEventType eventType,
            GameContext context,
            string additionalInfo = null)
        {
            string description = eventType switch
            {
                GameEventType.TipOff => $"TIP-OFF: {context.AwayTeam?.Name} @ {context.HomeTeam?.Name}",
                GameEventType.QuarterStart => GetQuarterStartText(context.Quarter),
                GameEventType.QuarterEnd => GetQuarterEndText(context),
                GameEventType.HalftimeStart => GetHalftimeText(context),
                GameEventType.OvertimeStart => $"OVERTIME! Five more minutes to decide it.",
                GameEventType.GameEnd => GetGameEndText(context),
                GameEventType.ClutchTime => "CLUTCH TIME! Under 5 minutes, game within 5 points.",
                _ => additionalInfo ?? ""
            };

            return new PlayByPlayEntry
            {
                Clock = FormatClock(context.GameClock),
                Quarter = context.Quarter,
                Team = "",
                Description = description,
                IsHighlight = true,
                Type = PlayByPlayType.GameEvent
            };
        }

        #endregion

        #region Description Generation

        private static string GenerateDescription(
            PossessionEvent evt,
            Player actor,
            Player target,
            Player defender,
            GameContext context,
            bool isHighlight)
        {
            string actorName = GetDisplayName(actor, evt.ActorPlayerId);
            string targetName = target != null ? GetDisplayName(target, evt.TargetPlayerId) : "";
            string defenderName = defender != null ? GetDisplayName(defender, evt.DefenderPlayerId) : "";

            // Use dramatic descriptions for highlights
            if (isHighlight && context.IsClutchTime)
            {
                return GenerateClutchDescription(evt, actorName, targetName, defenderName, context);
            }

            if (isHighlight)
            {
                return GenerateHighlightDescription(evt, actorName, targetName, defenderName, context);
            }

            // Standard descriptions
            return GenerateStandardDescription(evt, actorName, targetName, defenderName, context);
        }

        private static string GenerateStandardDescription(
            PossessionEvent evt,
            string actor,
            string target,
            string defender,
            GameContext context)
        {
            return evt.Type switch
            {
                EventType.Shot when evt.Outcome == EventOutcome.Success =>
                    GenerateMadeShotDescription(evt, actor, context),

                EventType.Shot when evt.Outcome == EventOutcome.Fail =>
                    GenerateMissedShotDescription(evt, actor, defender),

                EventType.Block => $"{defender} BLOCKS {actor}!",

                EventType.Steal => $"{actor} with the steal!",

                EventType.Rebound => evt.IsFastBreak
                    ? $"{actor} rebounds and pushes!"
                    : $"{actor} pulls down the rebound.",

                EventType.Turnover => GetTurnoverDescription(actor),

                EventType.Foul => $"Foul called on {defender}.",

                EventType.FreeThrow when evt.Outcome == EventOutcome.Success =>
                    $"{actor} sinks the free throw.",

                EventType.FreeThrow when evt.Outcome == EventOutcome.Fail =>
                    $"{actor} misses the free throw.",

                EventType.Pass when evt.Outcome == EventOutcome.Success =>
                    $"{actor} finds {target}.",

                EventType.Pass when evt.Outcome == EventOutcome.Fail =>
                    $"Pass intercepted by {defender}!",

                EventType.Dribble => GetDribbleDescription(actor, evt.ActorPosition),

                EventType.Screen => $"{actor} sets the screen.",

                EventType.Timeout => "Timeout on the floor.",

                _ => evt.Description ?? "..."
            };
        }

        private static string GenerateHighlightDescription(
            PossessionEvent evt,
            string actor,
            string target,
            string defender,
            GameContext context)
        {
            return evt.Type switch
            {
                EventType.Shot when evt.Outcome == EventOutcome.Success && evt.ShotType == ShotType.Dunk =>
                    GetDunkHighlight(actor, defender, context),

                EventType.Shot when evt.Outcome == EventOutcome.Success && evt.PointsScored == 3 =>
                    GetThreePointerHighlight(actor, context),

                EventType.Shot when evt.Outcome == EventOutcome.Success && evt.IsAndOne =>
                    $"{actor} AND ONE! {GetReaction()}",

                EventType.Block =>
                    GetBlockHighlight(actor, defender),

                EventType.Steal =>
                    $"{actor} STEALS IT! {(evt.IsFastBreak ? "Here comes the break!" : "")}",

                EventType.Shot when evt.Outcome == EventOutcome.Fail && evt.ContestLevel > 0.8f =>
                    $"{actor} forces a tough shot... blocked away by {defender}!",

                _ => GenerateStandardDescription(evt, actor, target, defender, context)
            };
        }

        private static string GenerateClutchDescription(
            PossessionEvent evt,
            string actor,
            string target,
            string defender,
            GameContext context)
        {
            int scoreDiff = context.HomeOnOffense
                ? context.HomeScore - context.AwayScore
                : context.AwayScore - context.HomeScore;

            return evt.Type switch
            {
                EventType.Shot when evt.Outcome == EventOutcome.Success && evt.PointsScored == 3 =>
                    GetClutchThreeDescription(actor, scoreDiff, context),

                EventType.Shot when evt.Outcome == EventOutcome.Success =>
                    GetClutchScoreDescription(actor, evt.PointsScored, scoreDiff, context),

                EventType.Shot when evt.Outcome == EventOutcome.Fail =>
                    GetClutchMissDescription(actor, scoreDiff),

                EventType.Block =>
                    $"HUGE BLOCK BY {defender.ToUpper()}! {actor}'s shot is denied!",

                EventType.Steal =>
                    $"{actor} WITH THE CLUTCH STEAL! This could seal it!",

                EventType.Turnover =>
                    $"TURNOVER! {actor} coughs it up at the worst possible time!",

                EventType.FreeThrow when evt.Outcome == EventOutcome.Success =>
                    $"{actor} knocks down the clutch free throw. Ice in his veins.",

                EventType.FreeThrow when evt.Outcome == EventOutcome.Fail =>
                    $"{actor} MISSES! The pressure was too much!",

                _ => GenerateHighlightDescription(evt, actor, target, defender, context)
            };
        }

        #endregion

        #region Shot Descriptions

        private static string GenerateMadeShotDescription(PossessionEvent evt, string actor, GameContext context)
        {
            string shotDesc = GetShotTypeDescription(evt.ShotType);
            string locationDesc = GetLocationDescription(evt.ActorPosition);
            string contestDesc = evt.ContestLevel > 0.6f ? " over the defender" : "";

            if (evt.IsFastBreak)
            {
                return $"{actor} finishes in transition! {shotDesc}{contestDesc}.";
            }

            return $"{actor} {shotDesc} {locationDesc}{contestDesc}. GOOD!";
        }

        private static string GenerateMissedShotDescription(PossessionEvent evt, string actor, string defender)
        {
            string shotDesc = GetShotTypeDescription(evt.ShotType);

            if (evt.ContestLevel > 0.7f && !string.IsNullOrEmpty(defender))
            {
                return $"{actor}'s {shotDesc} contested by {defender}. No good.";
            }

            return $"{actor}'s {shotDesc} rims out.";
        }

        private static string GetShotTypeDescription(ShotType? shotType)
        {
            return shotType switch
            {
                ShotType.Dunk => "throws it down",
                ShotType.Layup => "lays it in",
                ShotType.Floater => "floats one",
                ShotType.Hookshot => "hooks it",
                ShotType.Jumper => "pulls up",
                ShotType.StepBack => "steps back",
                ShotType.Fadeaway => "fades away",
                ShotType.CatchAndShoot => "catches and fires",
                ShotType.PullUp => "pulls up in transition",
                ShotType.Heave => "heaves from beyond half court",
                ShotType.TipIn => "tips it in",
                _ => "shoots"
            };
        }

        private static string GetLocationDescription(CourtPosition pos)
        {
            if (pos == null) return "";

            var zone = pos.GetZone(true);
            var side = pos.GetSide();

            string sideStr = side switch
            {
                CourtSide.Left => "from the left wing",
                CourtSide.Right => "from the right wing",
                CourtSide.Center => "from the top",
                _ => ""
            };

            return zone switch
            {
                CourtZone.RestrictedArea => "at the rim",
                CourtZone.Paint => "in the paint",
                CourtZone.ShortMidRange => "from the elbow",
                CourtZone.LongMidRange => $"from mid-range {sideStr}",
                CourtZone.ThreePoint => $"from deep {sideStr}",
                CourtZone.Corner => side == CourtSide.Left ? "from the left corner" : "from the right corner",
                _ => ""
            };
        }

        #endregion

        #region Highlight Descriptions

        private static string GetDunkHighlight(string actor, string defender, GameContext context)
        {
            var dunkPhrases = new[]
            {
                $"{actor} THROWS IT DOWN!",
                $"{actor} with the THUNDEROUS slam!",
                $"{actor} HAMMERS it home!",
                $"POSTER! {actor} dunks all over {defender}!",
                $"{actor} takes it to the rack and FLUSHES IT!",
                $"OH MY! {actor} with the vicious dunk!"
            };

            return dunkPhrases[_rng.Next(dunkPhrases.Length)];
        }

        private static string GetThreePointerHighlight(string actor, GameContext context)
        {
            var threePhrases = new[]
            {
                $"{actor} for THREE... BANG!",
                $"{actor} from DOWNTOWN! SPLASH!",
                $"{actor} drains the three!",
                $"DEEP THREE by {actor}!",
                $"{actor} with the dagger from deep!",
                $"Nothing but net! {actor} hits from three!"
            };

            return threePhrases[_rng.Next(threePhrases.Length)];
        }

        private static string GetBlockHighlight(string actor, string defender)
        {
            var blockPhrases = new[]
            {
                $"{defender} says NO WAY! Block on {actor}!",
                $"GET THAT OUT OF HERE! {defender} with the rejection!",
                $"{defender} SWATS IT AWAY! Huge block!",
                $"BLOCKED BY {defender.ToUpper()}!",
                $"{defender} sends it into the stands!"
            };

            return blockPhrases[_rng.Next(blockPhrases.Length)];
        }

        private static string GetClutchThreeDescription(string actor, int scoreDiff, GameContext context)
        {
            if (scoreDiff == -3 || scoreDiff == 0)
            {
                return $"{actor} for THREE... TIE GAME!!! What a moment!";
            }
            if (scoreDiff < 0)
            {
                return $"{actor} DRILLS THE THREE! They're back in this!";
            }
            return $"{actor} puts the DAGGER in from three! This one's over!";
        }

        private static string GetClutchScoreDescription(string actor, int points, int scoreDiff, GameContext context)
        {
            if (scoreDiff == 0)
            {
                return $"{actor} gives them the LEAD! Clutch bucket!";
            }
            if (scoreDiff < 0 && scoreDiff + points >= 0)
            {
                return points == 2
                    ? $"{actor} ties it up! We're headed to overtime!"
                    : $"{actor} cuts into the deficit!";
            }
            return $"{actor} extends the lead! Ice cold!";
        }

        private static string GetClutchMissDescription(string actor, int scoreDiff)
        {
            if (scoreDiff < 0)
            {
                return $"{actor} can't connect! They needed that one badly!";
            }
            return $"{actor}'s shot won't fall. Clock is ticking!";
        }

        #endregion

        #region Other Descriptions

        private static string GetTurnoverDescription(string actor)
        {
            var phrases = new[]
            {
                $"Turnover by {actor}.",
                $"{actor} gives it away.",
                $"{actor} with the costly turnover.",
                $"Loose ball! {actor} loses it."
            };
            return phrases[_rng.Next(phrases.Length)];
        }

        private static string GetDribbleDescription(string actor, CourtPosition pos)
        {
            var side = pos?.GetSide() ?? CourtSide.Center;
            string direction = side == CourtSide.Left ? "left" : side == CourtSide.Right ? "right" : "to the basket";
            return $"{actor} drives {direction}.";
        }

        private static string GetReaction()
        {
            var reactions = new[]
            {
                "What a play!",
                "Incredible!",
                "He's heating up!",
                "Unbelievable!",
                "How did he do that?!"
            };
            return reactions[_rng.Next(reactions.Length)];
        }

        private static string GetQuarterStartText(int quarter)
        {
            return quarter switch
            {
                1 => "First quarter underway!",
                2 => "Second quarter begins.",
                3 => "Second half underway!",
                4 => "Fourth quarter - here we go!",
                _ => $"Overtime period {quarter - 4} begins!"
            };
        }

        private static string GetQuarterEndText(GameContext context)
        {
            int quarter = context.Quarter;
            string score = $"{context.AwayTeam?.Abbreviation} {context.AwayScore} - {context.HomeTeam?.Abbreviation} {context.HomeScore}";

            return quarter switch
            {
                1 => $"End of the first quarter. {score}",
                2 => $"That's halftime! {score}",
                3 => $"End of the third. {score}",
                4 when context.HomeScore == context.AwayScore => $"End of regulation! We're tied! Heading to overtime!",
                4 => $"FINAL: {score}",
                _ => $"End of overtime {quarter - 4}. {score}"
            };
        }

        private static string GetHalftimeText(GameContext context)
        {
            int diff = Math.Abs(context.HomeScore - context.AwayScore);
            string leader = context.HomeScore > context.AwayScore
                ? context.HomeTeam?.Name
                : context.AwayTeam?.Name;

            if (diff == 0) return "All tied up at the half!";
            if (diff >= 20) return $"{leader} dominating! Up by {diff} at the break.";
            if (diff >= 10) return $"{leader} in control, up {diff} at halftime.";
            return $"Close game at the half. {leader} leads by {diff}.";
        }

        private static string GetGameEndText(GameContext context)
        {
            string winner = context.HomeScore > context.AwayScore
                ? context.HomeTeam?.Name
                : context.AwayTeam?.Name;

            int diff = Math.Abs(context.HomeScore - context.AwayScore);

            if (diff == 1) return $"FINAL! {winner} wins it by ONE in a thriller!";
            if (diff <= 5) return $"What a game! {winner} holds on to win!";
            if (diff >= 20) return $"FINAL: {winner} cruises to victory!";
            return $"That's the game! {winner} gets the win!";
        }

        #endregion

        #region Helpers

        private static bool DetermineIfHighlight(PossessionEvent evt, GameContext context)
        {
            // Dunks are always highlights
            if (evt.ShotType == ShotType.Dunk && evt.Outcome == EventOutcome.Success)
                return true;

            // Made threes are highlights
            if (evt.PointsScored == 3)
                return true;

            // Blocks and steals are highlights
            if (evt.Type == EventType.Block || evt.Type == EventType.Steal)
                return true;

            // And-ones are highlights
            if (evt.IsAndOne)
                return true;

            // Clutch time makes everything more important
            if (context.IsClutchTime && evt.PointsScored > 0)
                return true;

            // Big defensive plays when protecting lead
            if (context.IsClutchTime && (evt.Type == EventType.Block || evt.Type == EventType.Steal))
                return true;

            return false;
        }

        private static PlayByPlayType GetPlayByPlayType(PossessionEvent evt)
        {
            return evt.Type switch
            {
                EventType.Shot when evt.Outcome == EventOutcome.Success => PlayByPlayType.Score,
                EventType.FreeThrow when evt.Outcome == EventOutcome.Success => PlayByPlayType.Score,
                EventType.Block => PlayByPlayType.DefensivePlay,
                EventType.Steal => PlayByPlayType.DefensivePlay,
                EventType.Rebound => PlayByPlayType.DefensivePlay,
                EventType.Turnover => PlayByPlayType.Turnover,
                EventType.Foul => PlayByPlayType.Foul,
                EventType.Timeout => PlayByPlayType.Timeout,
                EventType.Substitution => PlayByPlayType.Substitution,
                _ => PlayByPlayType.Regular
            };
        }

        private static string GetDisplayName(Player player, string fallbackId)
        {
            if (player == null) return fallbackId ?? "Unknown";
            return player.LastName ?? player.DisplayName ?? fallbackId;
        }

        private static string FormatClock(float seconds)
        {
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{mins}:{secs:D2}";
        }

        #endregion
    }

    /// <summary>
    /// Game context for play-by-play generation
    /// </summary>
    public class GameContext
    {
        public Team HomeTeam { get; set; }
        public Team AwayTeam { get; set; }
        public Team OffenseTeam { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int Quarter { get; set; }
        public float GameClock { get; set; }
        public bool HomeOnOffense { get; set; }

        public bool IsClutchTime => Quarter >= 4 && GameClock <= 300 && Math.Abs(HomeScore - AwayScore) <= 5;
        public bool IsTied => HomeScore == AwayScore;
        public int ScoreDifferential => HomeScore - AwayScore;
    }

    /// <summary>
    /// Types of game events for play-by-play
    /// </summary>
    public enum GameEventType
    {
        TipOff,
        QuarterStart,
        QuarterEnd,
        HalftimeStart,
        HalftimeEnd,
        OvertimeStart,
        GameEnd,
        ClutchTime,
        MediaTimeout,
        InjuryTimeout
    }
}
