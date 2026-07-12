using UnityEngine;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Bone-level procedural posing for the 3D match view — the $0 stand-in for paid basketball
    /// animation clips. Pure and static, allocation-free: given an actor's action state it returns
    /// local-rotation OFFSETS for six upper-body bones plus a 0..1 weight. CharacterBody.PoseLate
    /// applies them AFTER the Animator (LateUpdate), so the arms shape a shot/dunk/pass/etc. over
    /// the generic KayKit base clips instead of the whole body T-posing through a Throw trigger.
    ///
    /// The offset is composed as <c>bone.localRotation * offset</c> — a delta in the bone's own
    /// space on top of whatever the animator evaluated this frame — then Slerp-blended by weight.
    /// Rotations only; positions are never touched.
    ///
    /// ponytail: axis signs + magnitudes are hand-tuned best-guesses against the KayKit humanoid,
    /// meant to be dialed in against the live rig. Every pose routes through the axis convention
    /// documented below so tuning stays in ONE place — flip a sign here, not in ten poses. When
    /// real ThirdParty clips land, CharacterBody lowers the procedural weight (see PoseLate); this
    /// table doesn't change.
    /// </summary>
    public static class ProceduralPose
    {
        // ── Axis convention (tune against the live rig; all poses obey it) ──
        // Arms rest at the sides. For an UPPER-ARM bone: local -X = flexion (raise arm FORWARD-up),
        // local +Z = abduction (raise arm OUT to the side). For a LOWER-ARM bone: local -X = elbow
        // flexion (bend), 0 = straight. Spine/Head: -X = lean/look back-up, +X = crunch/look down,
        // +Y = twist, +Z = tilt to the model's left. Signs are empirical — if the rig reads
        // mirrored, negate here and every pose follows.

        /// <summary>Six upper-body bone offsets + an overall blend weight. Default is all-identity,
        /// weight 0 (no pose → CharacterBody writes nothing → zero cost).</summary>
        public struct Targets
        {
            public Quaternion LUpperArm, LLowerArm, RUpperArm, RLowerArm, Spine, Head;
            public float Weight;
        }

        // Ramp the weight in over the first RampFrac of the action and out over the last RampFrac so
        // a pose eases on and off instead of popping. 0 at the phase 0/1 edges, 1 across the middle.
        private const float RampFrac = 0.15f;

        private static float Envelope(float phase)
        {
            if (phase <= 0f || phase >= 1f) return 0f;
            if (phase < RampFrac) return phase / RampFrac;
            if (phase > 1f - RampFrac) return (1f - phase) / RampFrac;
            return 1f;
        }

        private static Quaternion Q(float x, float y, float z) => Quaternion.Euler(x, y, z);

        /// <param name="hasBall">Reserved for future ball-gated poses; the sim already implies ball
        /// possession via the action, so no pose keys off it today.</param>
        /// <param name="verticalOffset">Jump height (feet); gates the rebound reach to the airborne
        /// window so a grounded "Rebounding" tag doesn't throw the arms up.</param>
        public static Targets Evaluate(PlayerAction action, ShotType? shot, float phase,
            bool defensiveStance, bool hasBall, float verticalOffset)
        {
            var t = new Targets
            {
                LUpperArm = Quaternion.identity,
                LLowerArm = Quaternion.identity,
                RUpperArm = Quaternion.identity,
                RLowerArm = Quaternion.identity,
                Spine = Quaternion.identity,
                Head = Quaternion.identity,
                Weight = 0f
            };

            float env = Envelope(phase);

            switch (action)
            {
                case PlayerAction.Shooting:
                    switch (shot)
                    {
                        case ShotType.Fadeaway:
                        case ShotType.StepBack:
                            JumpShot(ref t, phase);
                            t.Spine = Q(-12f, 0f, 0f);   // stronger lean-back
                            t.Head = Q(-8f, 0f, 0f);      // head up
                            break;
                        case ShotType.Floater:
                            Floater(ref t, phase);
                            break;
                        case ShotType.Hookshot:
                            Hookshot(ref t);
                            break;
                        case ShotType.Heave:
                            Heave(ref t, phase);
                            break;
                        default:                          // Jumper / CatchAndShoot / PullUp / TipIn / null
                            JumpShot(ref t, phase);
                            break;
                    }
                    t.Weight = env;
                    break;

                case PlayerAction.Dunking:
                    Dunk(ref t, phase);
                    t.Weight = env;
                    break;

                case PlayerAction.Layup:
                    Layup(ref t, phase);
                    t.Weight = env;
                    break;

                case PlayerAction.LayupAcrobatic:
                    LayupAcrobatic(ref t, phase);
                    t.Weight = env;
                    break;

                case PlayerAction.Passing:
                    Passing(ref t, phase);
                    t.Weight = env;
                    break;

                case PlayerAction.Catching:
                    // Both arms forward, hands ~chest height, brief.
                    t.RUpperArm = Q(-45f, 0f, 0f);
                    t.LUpperArm = Q(-45f, 0f, 0f);
                    t.RLowerArm = Q(-80f, 0f, 0f);
                    t.LLowerArm = Q(-80f, 0f, 0f);
                    t.Weight = env;
                    break;

                case PlayerAction.Rebounding:
                    // Both arms straight up through the leap; only while actually airborne.
                    if (verticalOffset > 0.2f)
                    {
                        t.RUpperArm = Q(-175f, 0f, 0f);
                        t.LUpperArm = Q(-175f, 0f, 0f);
                        t.Weight = env;
                    }
                    break;

                case PlayerAction.BoxingOut:
                    // Low-wide brace: arms abducted ~40°, elbows bent, spine hinged forward. Held.
                    t.RUpperArm = Q(-10f, 0f, 40f);
                    t.LUpperArm = Q(-10f, 0f, -40f);
                    t.RLowerArm = Q(-60f, 0f, 0f);
                    t.LLowerArm = Q(-60f, 0f, 0f);
                    t.Spine = Q(15f, 0f, 0f);
                    t.Weight = env;
                    break;

                case PlayerAction.Screening:
                    // Forearms crossed over the chest, upright spine. Held.
                    t.RUpperArm = Q(-30f, 0f, 20f);
                    t.LUpperArm = Q(-30f, 0f, -20f);
                    t.RLowerArm = Q(-100f, -30f, 0f);
                    t.LLowerArm = Q(-100f, 30f, 0f);
                    t.Weight = env;
                    break;

                default:
                    // No dedicated pose. Show active-hands defensive stance if that's all that's on.
                    if (defensiveStance)
                    {
                        t.RUpperArm = Q(-20f, 0f, 60f);   // half-raised, mirror-symmetric abduction
                        t.LUpperArm = Q(-20f, 0f, -60f);
                        t.RLowerArm = Q(-50f, 0f, 0f);
                        t.LLowerArm = Q(-50f, 0f, 0f);
                        // Stance is a state, not a phased window — hold it full instead of enveloping
                        // by a phase that idle actions don't advance.
                        t.Weight = 1f;
                    }
                    break;
            }

            return t;
        }

        // ── Pose builders (offsets only; caller sets Weight) ──

        // Both upper arms raise forward-up to ~150° flexion; right forearm extends toward release as
        // phase→0.65; left guide arm drops after release; slight spine back-arch.
        private static void JumpShot(ref Targets t, float phase)
        {
            float rel = Mathf.Clamp01(phase / 0.65f);                 // 0 → 1 at release
            float guideFade = phase > 0.65f ? Mathf.Lerp(1f, 0.3f, (phase - 0.65f) / 0.35f) : 1f;
            t.RUpperArm = Q(-150f, 0f, 0f);
            t.LUpperArm = Q(-150f * guideFade, 0f, 0f);
            t.RLowerArm = Q(Mathf.Lerp(-90f, 0f, rel), 0f, 0f);       // bent → extended at release
            t.LLowerArm = Q(-70f, 0f, 0f);                            // guide hand stays bent
            t.Spine = Q(-5f, 0f, 0f);
        }

        // Right arm alone, quick full extension high; left arm low for balance.
        private static void Floater(ref Targets t, float phase)
        {
            float rel = Mathf.Clamp01(phase / 0.5f);                  // quicker release than a jumper
            t.RUpperArm = Q(-165f, 0f, 0f);
            t.RLowerArm = Q(Mathf.Lerp(-70f, 0f, rel), 0f, 0f);
            t.LUpperArm = Q(-20f, 0f, 0f);
        }

        // Right arm sweeps overhead sideways (abduction ~160°); spine tilts left; head toward rim.
        private static void Hookshot(ref Targets t)
        {
            t.RUpperArm = Q(-20f, 0f, 160f);
            t.RLowerArm = Q(-20f, 0f, 0f);
            t.LUpperArm = Q(0f, 0f, 25f);
            t.Spine = Q(0f, 0f, 8f);
            t.Head = Q(-10f, 20f, 0f);
        }

        // Both arms overhead ~170°; elbows bent early then extended hard at ~0.7 (rim); spine crunch
        // forward at the slam.
        private static void Dunk(ref Targets t, float phase)
        {
            float ext = Mathf.Clamp01((phase - 0.4f) / 0.3f);        // extend hard around phase 0.7
            float elbow = Mathf.Lerp(-80f, 0f, ext);
            t.RUpperArm = Q(-170f, 0f, 0f);
            t.LUpperArm = Q(-170f, 0f, 0f);
            t.RLowerArm = Q(elbow, 0f, 0f);
            t.LLowerArm = Q(elbow, 0f, 0f);
            float crunch = phase > 0.7f ? Mathf.Lerp(0f, 12f, (phase - 0.7f) / 0.3f) : 0f;
            t.Spine = Q(crunch, 0f, 0f);
        }

        // Right arm scoop rising to ~160° flexion; left arm drives up bent (knee-drive feel).
        private static void Layup(ref Targets t, float phase)
        {
            t.RUpperArm = Q(Mathf.Lerp(-60f, -160f, phase), 0f, 0f);
            t.RLowerArm = Q(-30f, 0f, 0f);
            t.LUpperArm = Q(-120f, 0f, 0f);
            t.LLowerArm = Q(-90f, 0f, 0f);
        }

        // Cross-body reverse: right arm across the chest early, sweeping to an over-shoulder finish
        // (yaw + roll the plain layup lacks); spine twist; head follows.
        private static void LayupAcrobatic(ref Targets t, float phase)
        {
            t.RUpperArm = Q(Mathf.Lerp(-40f, -150f, phase),
                            Mathf.Lerp(60f, -30f, phase),
                            40f);
            t.RLowerArm = Q(-50f, 0f, 0f);
            t.LUpperArm = Q(-100f, 0f, 0f);
            t.Spine = Q(0f, 15f, 0f);
            t.Head = Q(-5f, 20f, 0f);
        }

        // Both arms sling from the chest, extending forward-up together; big spine arch then whip.
        private static void Heave(ref Targets t, float phase)
        {
            float rel = Mathf.Clamp01(phase / 0.7f);
            float armUp = Mathf.Lerp(-30f, -140f, rel);
            float elbow = Mathf.Lerp(-80f, -10f, rel);
            t.RUpperArm = Q(armUp, 0f, 0f);
            t.LUpperArm = Q(armUp, 0f, 0f);
            t.RLowerArm = Q(elbow, 0f, 0f);
            t.LLowerArm = Q(elbow, 0f, 0f);
            float arch = phase < 0.7f ? Mathf.Lerp(-15f, 0f, phase / 0.7f) : 0f;
            t.Spine = Q(arch, 0f, 0f);
        }

        // Both forearms extend forward from the chest at phase ~0.5 (release), then relax back.
        private static void Passing(ref Targets t, float phase)
        {
            float elbow = phase <= 0.5f
                ? Mathf.Lerp(-90f, -10f, phase / 0.5f)               // wind → extend at release
                : Mathf.Lerp(-10f, -70f, (phase - 0.5f) / 0.5f);     // relax after
            t.RUpperArm = Q(-40f, 0f, 0f);
            t.LUpperArm = Q(-40f, 0f, 0f);
            t.RLowerArm = Q(elbow, 0f, 0f);
            t.LLowerArm = Q(elbow, 0f, 0f);
        }
    }
}
