using UnityEngine;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Deterministic render-side soft-separation pass (Phase 2 — choreographed contact). Bodies must
    /// never interpenetrate, but the sim's authored positions are the source of truth — so this only
    /// nudges: it computes small per-player XY offsets that push overlapping actors apart, then the
    /// choreographed contact (screens, box-outs, hip-rides) survives because those authored pairs are
    /// allowed a tighter min separation. Pure static, no Unity Play-mode state, so it runs in an
    /// edit-mode test.
    ///
    /// Offsets are recomputed FRESH from the authored positions every frame (never integrated), so
    /// there is no drift. O(45) pairs over 10 actors, fixed index order, 2 relaxation iterations.
    /// </summary>
    public static class SoftSeparation
    {
        // Default body clearance and the tighter clearance for an authored-contact pair (feet).
        private const float MinSeparation = 1.6f;
        private const float ContactSeparation = 1.05f;
        // A DefensiveStance defender this close to the ball-handler counts as authored contact.
        private const float DefenderContactRange = 3f;
        // A player above this jump height is airborne — leave those pairs alone (no floor clash).
        private const float AirborneFeet = 0.5f;
        // Max total displacement any single actor may absorb, so a scrum can't fling anyone.
        private const float MaxOffset = 1.2f;
        private const int Iterations = 2;

        /// <summary>Actions whose owner is deliberately making body contact — they hold their ground
        /// (low push weight) and their pair with the named target gets the tight separation.</summary>
        public static bool IsContactAction(PlayerAction a) =>
            a == PlayerAction.Screening || a == PlayerAction.BoxingOut || a == PlayerAction.PostingUp;

        /// <summary>Resolve per-player XY offsets so no two grounded bodies sit closer than their pair
        /// min separation. <paramref name="positions"/> are the authored (lerped) court-feet spots;
        /// <paramref name="snapshots"/> is index-aligned metadata; <paramref name="offsets"/> is
        /// written (length ≥ count). Caller pre-allocates all arrays — zero allocation here.</summary>
        public static void Resolve(Vector2[] positions, PlayerSnapshot[] snapshots, int count,
            string ballHandlerId, Vector2[] offsets)
        {
            for (int i = 0; i < count; i++) offsets[i] = Vector2.zero;
            if (count < 2) return;

            int handlerIdx = -1;
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(ballHandlerId) && snapshots[i].PlayerId == ballHandlerId)
                    handlerIdx = i;
            }

            for (int iter = 0; iter < Iterations; iter++)
            {
                for (int i = 0; i < count; i++)
                {
                    if (snapshots[i].VerticalOffset > AirborneFeet) continue;
                    for (int j = i + 1; j < count; j++)
                    {
                        if (snapshots[j].VerticalOffset > AirborneFeet) continue;

                        float wi = PushWeight(in snapshots[i], ballHandlerId);
                        float wj = PushWeight(in snapshots[j], ballHandlerId);
                        float total = wi + wj;
                        if (total <= 0f) continue;   // both pinned (e.g. two ball-handlers) — can't move

                        float minSep = IsContactPair(snapshots, i, j, handlerIdx, positions)
                            ? ContactSeparation : MinSeparation;

                        Vector2 pi = positions[i] + offsets[i];
                        Vector2 pj = positions[j] + offsets[j];
                        Vector2 delta = pj - pi;
                        float dist = delta.magnitude;
                        if (dist >= minSep) continue;

                        Vector2 dir;
                        float push;
                        if (dist < 1e-4f)
                        {
                            // Coincident: separate along a fixed axis derived from the index pair so
                            // the outcome stays deterministic.
                            float ang = (i * 9973 + j) * 0.61803f;
                            dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                            push = minSep;
                        }
                        else
                        {
                            dir = delta / dist;
                            push = minSep - dist;
                        }

                        offsets[i] -= dir * (push * (wi / total));
                        offsets[j] += dir * (push * (wj / total));
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                float m = offsets[i].magnitude;
                if (m > MaxOffset) offsets[i] *= MaxOffset / m;
            }
        }

        // Ball-handler is never displaced; a contact-action owner mostly holds ground; everyone else
        // gives way fully.
        private static float PushWeight(in PlayerSnapshot s, string ballHandlerId)
        {
            if (!string.IsNullOrEmpty(ballHandlerId) && s.PlayerId == ballHandlerId) return 0f;
            if (IsContactAction(s.CurrentAction)) return 0.25f;
            return 1f;
        }

        private static bool IsContactPair(PlayerSnapshot[] s, int i, int j, int handlerIdx, Vector2[] pos)
        {
            // Authored screen / box-out / post-up: contact-action owner naming the other as its target.
            if (IsContactAction(s[i].CurrentAction) && s[i].TargetPlayerId == s[j].PlayerId) return true;
            if (IsContactAction(s[j].CurrentAction) && s[j].TargetPlayerId == s[i].PlayerId) return true;

            // On-ball defender riding the ball-handler's hip: a DefensiveStance defender within range.
            if (handlerIdx == i && s[j].DefensiveStance &&
                Vector2.Distance(pos[i], pos[j]) < DefenderContactRange) return true;
            if (handlerIdx == j && s[i].DefensiveStance &&
                Vector2.Distance(pos[i], pos[j]) < DefenderContactRange) return true;

            return false;
        }
    }
}
