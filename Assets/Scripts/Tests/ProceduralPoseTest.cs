using UnityEngine;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.UI.Match3D;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Pure checks on the procedural posing table (UI/Match3D/ProceduralPose). No play mode: the
    /// class is a static, allocation-free function of (action, shot, phase, stance, ball, jump).
    /// Covers the weight envelope, per-action distinctness, the identity/no-pose case, and
    /// determinism.
    /// </summary>
    public class ProceduralPoseTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            // ── Weight envelope: 0 at the phase edges, ramps to full mid-action ──
            var jumpEdge0 = Pose(PlayerAction.Shooting, ShotType.Jumper, 0f);
            var jumpEdge1 = Pose(PlayerAction.Shooting, ShotType.Jumper, 1f);
            var jumpMid = Pose(PlayerAction.Shooting, ShotType.Jumper, 0.5f);
            AssertEqual(0f, jumpEdge0.Weight, "JumpShot weight 0 at phase 0");
            AssertEqual(0f, jumpEdge1.Weight, "JumpShot weight 0 at phase 1");
            AssertGreaterThan(jumpMid.Weight, 0.8f, "JumpShot weight >0.8 mid-action");
            // Ramp-in is partial, not full, just inside the leading edge.
            AssertLessThan(Pose(PlayerAction.Shooting, ShotType.Jumper, 0.05f).Weight, 0.9f,
                "JumpShot weight ramping in (not yet full) near phase 0");

            // ── Distinct right-arm shapes per action (same phase) ──
            var dunk = Pose(PlayerAction.Dunking, null, 0.5f, jump: 0.5f);
            var layup = Pose(PlayerAction.Layup, null, 0.5f, jump: 0.5f);
            var acro = Pose(PlayerAction.LayupAcrobatic, null, 0.5f, jump: 0.5f);
            AssertGreaterThan(Quaternion.Angle(dunk.RUpperArm, layup.RUpperArm), 5f,
                "Dunk and Layup differ at the right upper arm");
            AssertGreaterThan(Quaternion.Angle(acro.RUpperArm, layup.RUpperArm), 5f,
                "Acrobatic layup differs from plain Layup at the right upper arm");

            // ── Idle → identity, weight 0 (no pose, zero cost) ──
            var idle = Pose(PlayerAction.Idle, null, 0.5f);
            AssertEqual(0f, idle.Weight, "Idle weight 0");
            Assert(idle.RUpperArm == Quaternion.identity && idle.LUpperArm == Quaternion.identity
                   && idle.Spine == Quaternion.identity && idle.Head == Quaternion.identity,
                "Idle offsets are identity");

            // ── Rebound reach is gated on being airborne ──
            AssertEqual(0f, Pose(PlayerAction.Rebounding, null, 0.5f, jump: 0f).Weight,
                "Rebounding grounded → no pose");
            AssertGreaterThan(Pose(PlayerAction.Rebounding, null, 0.5f, jump: 1.5f).Weight, 0.8f,
                "Rebounding airborne → arms up");

            // ── Defensive stance shows as a held pose when nothing else is active ──
            AssertEqual(0f, Pose(PlayerAction.Idle, null, 0f, stance: false).Weight,
                "No stance, no action → nothing");
            AssertGreaterThan(Pose(PlayerAction.Idle, null, 0f, stance: true).Weight, 0.9f,
                "Defensive stance holds full weight regardless of phase");

            // ── Determinism: same inputs → identical output ──
            var a = Pose(PlayerAction.Shooting, ShotType.Fadeaway, 0.4f);
            var b = Pose(PlayerAction.Shooting, ShotType.Fadeaway, 0.4f);
            Assert(a.RUpperArm == b.RUpperArm && a.RLowerArm == b.RLowerArm
                   && a.Spine == b.Spine && a.Weight == b.Weight,
                "Deterministic (same inputs → same pose)");

            return (_passed, _failed);
        }

        private static ProceduralPose.Targets Pose(PlayerAction action, ShotType? shot, float phase,
            bool stance = false, float jump = 0f)
            => ProceduralPose.Evaluate(action, shot, phase, stance, hasBall: true, verticalOffset: jump);
    }
}
