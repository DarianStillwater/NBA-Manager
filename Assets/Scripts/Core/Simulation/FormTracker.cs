using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Real hot/cold streaks: Player.Form is recomputed from the last five game
    /// logs after every completed game (recent games weigh more), and drifts
    /// back toward neutral on idle days. Form reaches the sim through one small
    /// capped funnel — Player.GetConditionModifier (±5% at the extremes).
    /// </summary>
    public static class FormTracker
    {
        public const float NEUTRAL = 50f;
        public const float MIN_FORM = 20f;
        public const float MAX_FORM = 90f;
        private const int WINDOW = 5;

        private static readonly float[] Weights = { 1.0f, 0.8f, 0.6f, 0.4f, 0.25f }; // newest first

        /// <summary>
        /// Recompute a player's form from their recent game logs. Call after the
        /// game has been recorded to season stats.
        /// </summary>
        public static void Recompute(Player player)
        {
            if (player == null) return;

            var season = player.CurrentSeasonStats;
            var recent = season?.GetRecentGames(WINDOW);
            if (recent == null || recent.Count == 0)
            {
                player.Form = NEUTRAL;
                return;
            }

            float ppg = season.PPG;
            float weightedScore = 0f, totalWeight = 0f;

            // recent list is oldest→newest; walk backwards so index 0 of Weights
            // lands on the most recent game.
            for (int i = 0; i < recent.Count; i++)
            {
                var game = recent[recent.Count - 1 - i];
                float w = Weights[Mathf.Min(i, Weights.Length - 1)];
                weightedScore += GameScore(game, ppg) * w;
                totalWeight += w;
            }

            float avg = totalWeight > 0f ? weightedScore / totalWeight : 0f;
            float form = NEUTRAL + Mathf.Clamp(avg * 2.2f, -30f, 40f);
            player.Form = Mathf.Clamp(form, MIN_FORM, MAX_FORM);
        }

        /// <summary>
        /// One game's deviation from this player's own baseline. Positive = hot.
        /// </summary>
        private static float GameScore(GameLog game, float seasonPpg)
        {
            float score = 0.45f * (game.Points - seasonPpg);

            if (game.FGA >= 4)
            {
                float efg = (game.FGM + 0.5f * game.ThreePM) / game.FGA;
                score += 40f * (efg - 0.5f);
            }

            score += 0.08f * game.PlusMinus;
            return score;
        }

        /// <summary>Idle-day drift toward neutral (rust and cooling off).</summary>
        public static void DriftTowardNeutral(Player player, float step = 0.75f)
        {
            if (player == null) return;
            player.Form = Mathf.MoveTowards(player.Form, NEUTRAL, step);
        }
    }
}
