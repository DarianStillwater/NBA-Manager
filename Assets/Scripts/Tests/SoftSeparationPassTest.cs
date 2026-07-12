using UnityEngine;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.UI.Match3D;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Phase 2 render-side soft-separation: bodies never interpenetrate, the ball-handler is pinned,
    /// authored contact pairs keep their tight spacing, airborne pairs are left alone, offsets are
    /// clamped, and the pass is deterministic. Pure math — no play mode needed.
    /// </summary>
    public class SoftSeparationTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            // (a) Two overlapping non-contact players get pushed to the default min separation.
            {
                var pos = new[] { new Vector2(0f, 0f), new Vector2(0.5f, 0f) };
                var snaps = new[] { Snap("p0"), Snap("p1") };
                var off = new Vector2[2];
                SoftSeparation.Resolve(pos, snaps, 2, "none", off);
                float d = Vector2.Distance(pos[0] + off[0], pos[1] + off[1]);
                AssertGreaterThan(d, 1.55f, "Overlapping non-contact pair separated to >=1.55 ft");
            }

            // (b) The ball-handler is never displaced; the other player yields fully.
            {
                var pos = new[] { new Vector2(0f, 0f), new Vector2(0.05f, 0f) };
                var snaps = new[] { Snap("p0"), Snap("p1") };
                var off = new Vector2[2];
                SoftSeparation.Resolve(pos, snaps, 2, "p0", off);
                AssertLessThan(off[0].magnitude, 0.001f, "Ball-handler offset stays zero");
                AssertGreaterThan(off[1].magnitude, 0.001f, "Non-handler absorbs the push");
            }

            // (c) An authored-contact pair at ~1.05 ft is preserved, not pushed to the default 1.6.
            {
                var pos = new[] { new Vector2(0f, 0f), new Vector2(1.05f, 0f) };
                var snaps = new[] { Snap("p0", PlayerAction.Screening, target: "p1"), Snap("p1") };
                var off = new Vector2[2];
                SoftSeparation.Resolve(pos, snaps, 2, "none", off);
                float d = Vector2.Distance(pos[0] + off[0], pos[1] + off[1]);
                AssertLessThan(d, 1.2f, "Authored-contact pair kept tight (not pushed to 1.6)");
            }

            // (d) Determinism: identical inputs → bit-identical outputs.
            {
                var pos = new[] { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.2f, 0.3f) };
                var snaps = new[] { Snap("p0"), Snap("p1"), Snap("p2") };
                var offA = new Vector2[3];
                var offB = new Vector2[3];
                SoftSeparation.Resolve(pos, snaps, 3, "p0", offA);
                SoftSeparation.Resolve(pos, snaps, 3, "p0", offB);
                bool same = true;
                for (int i = 0; i < 3; i++)
                    same &= offA[i].x == offB[i].x && offA[i].y == offB[i].y;
                Assert(same, "Same inputs produce identical outputs");
            }

            // (e) An airborne pair (both jumping) is untouched.
            {
                var pos = new[] { new Vector2(0f, 0f), new Vector2(0.1f, 0f) };
                var snaps = new[] { Snap("p0", vert: 1f), Snap("p1", vert: 1f) };
                var off = new Vector2[2];
                SoftSeparation.Resolve(pos, snaps, 2, "none", off);
                Assert(off[0] == Vector2.zero && off[1] == Vector2.zero, "Airborne pair not separated");
            }

            // (f) Total offset is clamped to <=1.2 ft even when the raw push would exceed it.
            {
                var pos = new[] { new Vector2(0f, 0f), new Vector2(0.05f, 0f) };
                var snaps = new[] { Snap("p0"), Snap("p1") };
                var off = new Vector2[2];
                SoftSeparation.Resolve(pos, snaps, 2, "p0", off);
                AssertLessThan(off[1].magnitude, 1.201f, "Offset clamped at 1.2 ft ceiling");
                AssertGreaterThan(off[1].magnitude, 1.1f, "Clamp actually engaged (push exceeded 1.2)");
            }

            return (_passed, _failed);
        }

        private static PlayerSnapshot Snap(string id, PlayerAction action = PlayerAction.Idle,
            string target = null, bool stance = false, float vert = 0f)
        {
            var s = new PlayerSnapshot(id, 0f, 0f);
            s.CurrentAction = action;
            s.TargetPlayerId = target;
            s.DefensiveStance = stance;
            s.VerticalOffset = vert;
            return s;
        }
    }
}
