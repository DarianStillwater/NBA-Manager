using System;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;
using ShotType = NBAHeadCoach.Core.Simulation.ShotType;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Energetic-neutral radio broadcast calls for the in-match narration bar.
    /// Turns choreographed NarrationBeats into short vivid lines timed to the on-screen
    /// action ("Screen from Allen up top." → "Mitchell turns the corner!" → "GOOD! CLE by 6!").
    ///
    /// Uses its own downstream-only System.Random for phrase variety — the same safe pattern
    /// as PlayByPlayGenerator: text generation runs after all outcomes are decided and is never
    /// touched by the determinism tests.
    /// </summary>
    public static class RadioNarrator
    {
        private static readonly System.Random _rng = new System.Random();
        private static readonly System.Collections.Generic.Dictionary<NarrationBeatKind, int> _lastPick =
            new System.Collections.Generic.Dictionary<NarrationBeatKind, int>();

        /// <summary>Build the narration-bar line for a beat. Actor/target already resolved.</summary>
        public static NarrationLine Narrate(NarrationBeat beat, PlayByPlayContext ctx,
            Player actor, Player target)
        {
            string a = PlayByPlayGenerator.GetDisplayName(actor, "He");
            string t = PlayByPlayGenerator.GetDisplayName(target, "the open man");

            string text;
            var style = NarrationStyle.Normal;

            switch (beat.Kind)
            {
                case NarrationBeatKind.BringUp:
                    text = Pick(beat.Kind,
                        $"{a} across midcourt.",
                        $"{a} brings it up.",
                        $"{a} walks it into the frontcourt.",
                        $"{a} pushing the pace.",
                        $"{a} surveys the floor.");
                    break;

                case NarrationBeatKind.SwingPass:
                    text = Pick(beat.Kind,
                        $"Swung to {t}.",
                        $"{a} reverses it.",
                        $"Around the horn to {t}.",
                        $"{a} moves it on.",
                        $"Over to {t}.");
                    break;

                case NarrationBeatKind.ScreenSet:
                    text = Pick(beat.Kind,
                        $"Screen from {a} up top.",
                        $"{a} sets it for {t}.",
                        $"Here comes {a} with the pick.",
                        $"{a} plants the screen.",
                        $"High screen, {a}.");
                    break;

                case NarrationBeatKind.Roll:
                    text = beat.Action == OffensiveAction.PickAndPop
                        ? Pick(beat.Kind, $"{a} pops out!", $"{a} spaces to the elbow.", $"{a} steps back out.")
                        : Pick(beat.Kind, $"{a} rolls hard to the rim!", $"{a} dives to the basket!", $"{a} on the roll!");
                    break;

                case NarrationBeatKind.Curl:
                    text = Pick(beat.Kind,
                        $"{a} curls off it!",
                        $"{a} tight off the screen.",
                        $"{a} snakes free!",
                        $"{a} on the move.");
                    break;

                case NarrationBeatKind.BackdoorCut:
                    text = Pick(beat.Kind,
                        $"{a} cuts backdoor!",
                        $"{a} slips behind the defense!",
                        $"Back door — {a}!",
                        $"{a} dives to the rim!");
                    style = NarrationStyle.Excited;
                    break;

                case NarrationBeatKind.PostMove:
                    text = Pick(beat.Kind,
                        $"{a} goes to work down low.",
                        $"{a} backing his man down.",
                        $"{a} on the block.",
                        $"{a} muscles for position.");
                    break;

                case NarrationBeatKind.PostFeed:
                    text = Pick(beat.Kind,
                        $"Entry to {t} on the block.",
                        $"{a} feeds the post.",
                        $"Inside to {t}.",
                        $"{a} drops it down low.");
                    break;

                case NarrationBeatKind.KickOut:
                    text = Pick(beat.Kind,
                        $"Kicked back out to {t}!",
                        $"{a} sprays it to {t}.",
                        $"Out of the post to {t}.",
                        $"Back outside to {t}.");
                    break;

                case NarrationBeatKind.Handoff:
                    text = Pick(beat.Kind,
                        $"Hand-off to {t}.",
                        $"{a} dribbles into the exchange with {t}.",
                        $"{a} and {t} on the give.");
                    break;

                case NarrationBeatKind.IsoJab:
                    text = Pick(beat.Kind,
                        $"{a}, one-on-one.",
                        $"{a} sizes him up.",
                        $"Jab step, {a}.",
                        $"Clear-out for {a}.");
                    break;

                case NarrationBeatKind.Drive:
                    text = Pick(beat.Kind,
                        $"{a} turns the corner!",
                        $"{a} attacks the seam!",
                        $"{a} gets a step!",
                        $"{a} into the paint!",
                        $"{a} puts it on the deck!",
                        $"{a} downhill!");
                    style = NarrationStyle.Excited;
                    break;

                case NarrationBeatKind.AssistPass:
                    text = Pick(beat.Kind,
                        $"Finds {t} open!",
                        $"{a} with the look to {t}!",
                        $"Drops it to {t}!",
                        $"{a} threads it to {t}!");
                    break;

                case NarrationBeatKind.ShotWindup:
                    text = WindupCall(beat, ctx, a);
                    style = beat.IsThree || beat.ShotType == ShotType.Dunk
                        ? NarrationStyle.Excited : NarrationStyle.Normal;
                    break;

                case NarrationBeatKind.RimResult:
                    text = ResultCall(beat, ctx, a);
                    style = beat.Made ? NarrationStyle.Excited : NarrationStyle.Normal;
                    break;

                case NarrationBeatKind.ReboundScramble:
                    text = Pick(beat.Kind,
                        $"{a} pulls it down.",
                        $"Board to {a}.",
                        $"{a} cleans it up.",
                        $"Rebound {a}.");
                    break;

                case NarrationBeatKind.StealJump:
                    text = Pick(beat.Kind,
                        $"Picked off by {a}!",
                        $"{a} jumps the lane — taken away!",
                        $"{a} cuts out the pass!");
                    style = NarrationStyle.Excited;
                    break;

                case NarrationBeatKind.OobTurnover:
                    text = Pick(beat.Kind,
                        $"{a} throws it away — out of bounds!",
                        $"Loose! {a} can't keep it in!",
                        "It sails out of bounds — turnover!");
                    break;

                case NarrationBeatKind.LooseBallTurnover:
                    text = Pick(beat.Kind,
                        $"{a} loses the handle — it's loose on the floor!",
                        $"It squirts away from {a} — the defense comes up with it!",
                        $"{a} coughs it up! Scooped by the defense!");
                    style = NarrationStyle.Excited;
                    break;

                case NarrationBeatKind.Violation:
                    text = beat.Turnover switch
                    {
                        TurnoverKind.Traveled => Pick(beat.Kind,
                            $"Traveling on {a} — they wave it off.",
                            $"{a} shuffles his feet — whistle, travel."),
                        TurnoverKind.OffensiveFoul => Pick(beat.Kind,
                            $"Offensive foul on {a}! The whistle blows.",
                            $"{a} bowls him over — charge!"),
                        _ => Pick(beat.Kind,
                            "Whistle — the play is dead.",
                            "The officials wave it off.",
                            "Violation on the floor.")
                    };
                    break;

                case NarrationBeatKind.FreeThrowSetup:
                    int attempts = Math.Max(beat.TargetIndex, 1);
                    text = Pick(beat.Kind,
                        $"{a} to the line for {attempts}.",
                        $"{a} steps up.",
                        $"Free throws coming for {a}.",
                        $"{a} at the stripe.");
                    break;

                case NarrationBeatKind.FreeThrowAttempt:
                    text = beat.Made
                        ? Pick(beat.Kind, "Drops.", "Good.", "Count it.", "Swish.")
                        : Pick(beat.Kind, "Rims out!", "No good.", "Front iron.", "Won't go.");
                    break;

                default:
                    text = "";
                    break;
            }

            return new NarrationLine { Text = text, Style = style };
        }

        // ── Shot calls ──────────────────────────────────────────────

        private static string WindupCall(NarrationBeat beat, PlayByPlayContext ctx, string a)
        {
            string call;
            switch (beat.ShotType)
            {
                case ShotType.Dunk:
                case ShotType.Layup:
                    call = Pick(beat.Kind, $"{a} takes it all the way...", $"{a} up and under...",
                        $"{a} rises at the rim...", $"{a} goes up strong...");
                    break;
                case ShotType.Floater:
                    call = Pick(beat.Kind, $"{a} floats one up high...", $"Soft touch from {a}...",
                        $"{a} lofts the runner...");
                    break;
                case ShotType.Hookshot:
                    call = Pick(beat.Kind, $"{a} turns to the hook...", $"Baby hook from {a}...");
                    break;
                case ShotType.Heave:
                    call = $"{a} HEAVES from beyond half court...!";
                    break;
                case ShotType.TipIn:
                    call = $"{a} tips it back up...";
                    break;
                default:
                    call = beat.IsThree
                        ? Pick(beat.Kind, $"{a} lets it fly from deep...", $"{a} from way downtown...",
                            $"{a} launches the three...", $"{a} with plenty of room from distance...")
                        : Pick(beat.Kind, $"Pull-up from the elbow, {a}...", $"{a} rises from mid-range...",
                            $"{a} squares up...", $"{a} with the jumper...");
                    break;
            }

            if (beat.ContestLevel > 0.6f) call = call.TrimEnd('.', '!') + "... over a hand!";
            if (ctx != null && ctx.IsClutchTime) call += " Clock running down!";
            return call;
        }

        private static string ResultCall(NarrationBeat beat, PlayByPlayContext ctx, string a)
        {
            if (!beat.Made)
                return Pick(beat.Kind, "Off the iron!", "No good!", "It rims out!", "Short — off the rim!");

            // Score tag from the ACTUAL decided points — never a guess from distance.
            int pts = beat.PointsScored > 0 ? beat.PointsScored : (beat.IsThree ? 3 : 2);
            string tag = ScoreTag(ctx, pts);
            if (beat.ShotType == ShotType.Dunk)
                return Pick(beat.Kind, $"THROWS IT DOWN! {tag}", $"HAMMERED HOME! {tag}", $"OH, WHAT A SLAM! {tag}");
            if (beat.IsThree)
                return Pick(beat.Kind, $"SPLASH! {tag}", $"BANG! {tag}", $"GOT IT from deep! {tag}", $"NAILS IT! {tag}");
            return Pick(beat.Kind, $"GOOD! {tag}", $"And it drops! {tag}", $"Count it! {tag}");
        }

        /// <summary>"CLE by 6." / "Tie game." / "CLE within 2." from the post-shot score.</summary>
        private static string ScoreTag(PlayByPlayContext ctx, int points)
        {
            if (ctx == null) return "";
            int home = ctx.HomeScore + (ctx.HomeOnOffense ? points : 0);
            int away = ctx.AwayScore + (ctx.HomeOnOffense ? 0 : points);
            string offAbbr = ctx.OffenseTeam?.Abbreviation ?? "";
            if (home == away) return "Tie game!";

            bool offenseLeads = ctx.HomeOnOffense ? home > away : away > home;
            int margin = Math.Abs(home - away);
            return offenseLeads ? $"{offAbbr} by {margin}." : $"{offAbbr} within {margin}.";
        }

        /// <summary>Pick a template, never repeating the previous choice for that beat kind.</summary>
        private static string Pick(NarrationBeatKind kind, params string[] pool)
        {
            if (pool.Length == 1) return pool[0];
            int i = _rng.Next(pool.Length);
            if (_lastPick.TryGetValue(kind, out int last) && i == last)
                i = (i + 1) % pool.Length;
            _lastPick[kind] = i;
            return pool[i];
        }
    }
}
