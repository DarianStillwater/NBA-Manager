using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Rigged, animated player body: instantiates the character prefab the editor tool generates
    /// (<see cref="ResourcePath"/>, built by Tools ▸ NBA Head Coach ▸ Build Character Animator) and
    /// drives its Animator from the timeline. Data drives everything physical — position/yaw from
    /// the actor root, jump height from <see cref="ActorFrame.VerticalOffset"/> applied as a root Y
    /// offset — while clips add flavor (leg cycle, shot motion, defensive crouch).
    ///
    /// Animator contract (see CharacterAnimatorBuilder):
    ///   Speed (float, 0..1 normalized) → idle/walk/run blend
    ///   MotionRate (float)             → locomotion playback rate (anti-skate)
    ///   Stance (bool)                  → crouched defensive idle / defensive slide (by Speed)
    ///   Dribbling (bool)               → DribbleMove blend tree (idle/walk/run with ball)
    ///   flat action states             → (Action, ShotStyle) crossfaded via DriveActionState
    ///   Jump / Throw (triggers)        → legacy fallback when an action state is absent
    ///
    /// Build returns false when the prefab isn't present so the actor falls back to a capsule —
    /// which is the normal state until the one-time editor build steps have been run.
    /// </summary>
    public class CharacterBody : IActorBody
    {
        public const string ResourcePath = "Match3D/PlayerCharacter";

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int StanceHash = Animator.StringToHash("Stance");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int ThrowHash = Animator.StringToHash("Throw");
        private static readonly int MotionRateHash = Animator.StringToHash("MotionRate");
        private static readonly int DribblingHash = Animator.StringToHash("Dribbling");

        // Action-clip state hashes (built by CharacterAnimatorBuilder). A state may be absent (its
        // clip wasn't dropped in yet) — HasStateCached guards that and CharacterBody falls back to
        // the legacy Jump/Throw triggers so the current controller keeps working.
        private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
        private static readonly int JumpShotHash = Animator.StringToHash("JumpShot");
        private static readonly int FadeawayHash = Animator.StringToHash("Fadeaway");
        private static readonly int FloaterHash = Animator.StringToHash("Floater");
        private static readonly int LayupHash = Animator.StringToHash("Layup");
        private static readonly int LayupAcrobaticHash = Animator.StringToHash("LayupAcrobatic");
        private static readonly int DunkHash = Animator.StringToHash("Dunk");
        private static readonly int HeaveHash = Animator.StringToHash("Heave");
        private static readonly int ChestPassHash = Animator.StringToHash("ChestPass");
        private static readonly int BouncePassHash = Animator.StringToHash("BouncePass");
        private static readonly int CatchHash = Animator.StringToHash("Catch");
        private static readonly int StealHash = Animator.StringToHash("Steal");
        private static readonly int BlockHash = Animator.StringToHash("Block");
        private static readonly int ReboundHash = Animator.StringToHash("Rebound");
        private static readonly int BoxOutHash = Animator.StringToHash("BoxOut");
        private static readonly int ScreenHash = Animator.StringToHash("Screen");
        private static readonly int CelebrateHash = Animator.StringToHash("Celebrate");

        // Feet/sec that maps to a full-sprint blend of 1.0.
        private const float SprintFeetPerSec = 24f;
        // The character model's native forward may not be +Z; adjust if it imports facing away.
        private const float ModelForwardYawOffset = 0f;
        // Lean: subtle broadcast range + how fast the model settles toward the target tilt.
        private const float MaxLeanDeg = 8f;
        private const float LeanLerpSpeed = 8f;
        // Extra body tilt braced INTO a choreographed contact partner (Phase 2), on top of the
        // accel/turn lean. Broadcast-subtle; composed absolutely per frame, never accumulated.
        private const float ContactLeanDeg = 6f;

        private static GameObject _prefab;
        private static bool _prefabResolved;

        private Transform _model;
        private Animator _animator;
        private Transform _handBone;

        // Procedural-pose bones (resolved once). Any may be null on a generic rig → Apply skips it.
        private Transform _poseLUp, _poseLLo, _poseRUp, _poseRLo, _poseSpine, _poseHead;
        private bool _poseBonesResolved;
        // Procedural yields to a real ThirdParty clip when one actually drives the action (arms keep
        // this much flavor over the aliased base clip); full weight on the legacy fallback path.
        // ponytail: one global yield weight — swap for per-clip weights when real clips land and some
        // states want more procedural help than others.
        private const float ProceduralYieldWeight = 0.35f;
        private bool _hasSpeed, _hasStance, _hasJump, _hasThrow, _hasMotionRate, _hasDribbling;

        private float _heightFeet = 6.5f;
        private bool _lastStance;
        private bool _lastDribbling;
        // Hash of the action-state currently driven (0 = locomotion family). Edge-triggers the
        // crossfade and tells us when a loop state needs a crossfade back to locomotion.
        private int _lastActionHash;
        // HasState is a per-controller query; cache results so we hit the Animator once per state.
        private readonly Dictionary<int, bool> _hasState = new Dictionary<int, bool>();

        public float VisualHeightFeet => _heightFeet;

        public Transform GetHandBone() => _handBone;

        public bool Build(Transform root, PlayerAppearance appearance)
        {
            _heightFeet = appearance != null ? appearance.HeightFeet : 6.5f;

            var prefab = LoadPrefab();
            if (prefab == null) return false;

            var go = Object.Instantiate(prefab, root, false);
            go.name = "CharacterModel";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, ModelForwardYawOffset, 0f);
            _model = go.transform;

            ScaleToHeight(go, _heightFeet);
            ApplyAppearance(go, appearance);

            _animator = go.GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;   // sim positions drive translation
                _hasSpeed = HasParam(_animator, SpeedHash, AnimatorControllerParameterType.Float);
                _hasStance = HasParam(_animator, StanceHash, AnimatorControllerParameterType.Bool);
                _hasJump = HasParam(_animator, JumpHash, AnimatorControllerParameterType.Trigger);
                _hasThrow = HasParam(_animator, ThrowHash, AnimatorControllerParameterType.Trigger);
                _hasMotionRate = HasParam(_animator, MotionRateHash, AnimatorControllerParameterType.Float);
                _hasDribbling = HasParam(_animator, DribblingHash, AnimatorControllerParameterType.Bool);

                // GetBoneTransform is only valid for a Humanoid rig with a valid avatar; guard so an
                // old/generic controller returns null (ball then uses fixed-offset carry math).
                if (_animator.isHuman)
                    _handBone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            return true;
        }

        public void Animate(in ActorFrame frame, float dt)
        {
            if (_model == null) return;

            // Data drives height; the jump clip only adds a knees-tuck/arms flavor on top.
            var p = _model.localPosition;
            float y = Mathf.Max(0f, frame.VerticalOffset);
            if (!Mathf.Approximately(p.y, y))
                _model.localPosition = new Vector3(p.x, y, p.z);

            // Accel/decel pitch + turn bank, applied on the model's LOCAL rotation (the actor root
            // owns yaw), slerped so plant-and-cut reads without snapping.
            float pitch = ActorMotion.LeanPitchDegrees(frame.PlanarAccelFeetPerSec2, MaxLeanDeg);
            float roll = ActorMotion.BankRollDegrees(frame.YawRateDegPerSec, MaxLeanDeg);

            // Contact brace: lean into the partner. ContactDir is a world court-plane vector; convert
            // to the actor root's local frame (which carries yaw) so forward→pitch, sideways→roll.
            if (frame.HasContact && _model.parent != null)
            {
                Vector3 local = _model.parent.InverseTransformDirection(
                    new Vector3(frame.ContactDir.x, 0f, frame.ContactDir.y));
                pitch += Mathf.Clamp(local.z, -1f, 1f) * ContactLeanDeg;
                roll += Mathf.Clamp(-local.x, -1f, 1f) * ContactLeanDeg;
            }

            Quaternion leanTarget = Quaternion.Euler(pitch, ModelForwardYawOffset, roll);
            _model.localRotation = Quaternion.Slerp(_model.localRotation, leanTarget, LeanLerpSpeed * dt);

            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            // Blend + clip cadence come from the MEASURED body speed so feet match real motion.
            float measured = frame.MeasuredSpeedFeetPerSec;
            float blend = Mathf.Clamp01(measured / SprintFeetPerSec);
            if (_hasSpeed)
                _animator.SetFloat(SpeedHash, blend);
            if (_hasMotionRate)
                _animator.SetFloat(MotionRateHash, ActorMotion.MotionRate(measured, blend));

            if (_hasStance && frame.DefensiveStance != _lastStance)
            {
                _animator.SetBool(StanceHash, frame.DefensiveStance);
                _lastStance = frame.DefensiveStance;
            }

            // Carrying the ball routes locomotion through the DribbleMove blend tree (idle/walk/run
            // with the ball). The controller handles the Locomotion↔DribbleMove transition.
            bool dribbling = frame.HasBall;
            if (_hasDribbling && dribbling != _lastDribbling)
            {
                _animator.SetBool(DribblingHash, dribbling);
                _lastDribbling = dribbling;
            }

            DriveActionState(frame.Action, frame.ShotStyle);
        }

        // ── Procedural posing (LateUpdate, after the animator) ──
        // Layers arm/spine/head shapes over the generic base clips so shots/dunks/passes read even
        // when no dedicated basketball clip exists. Rotations only, composed absolutely each frame
        // (target = animator pose * offset), so nothing accumulates.
        public void PoseLate(in ActorFrame frame)
        {
            // GetBoneTransform is only valid on a Humanoid rig; a generic controller poses nothing.
            if (_animator == null || !_animator.isHuman) return;

            ResolvePoseBones();

            var t = ProceduralPose.Evaluate(frame.Action, frame.ShotStyle, frame.ActionPhase,
                frame.DefensiveStance, frame.HasBall, frame.VerticalOffset);
            if (t.Weight <= 0f) return;   // no pose for this action → zero cost

            // Yield if a real clip is actually driving this action. Today no basketball states exist
            // in the controller, so HasStateCached is false and procedural runs at full weight.
            int state = ResolveStateHash(frame.Action, frame.ShotStyle);
            float w = t.Weight * (state != 0 && HasStateCached(state) ? ProceduralYieldWeight : 1f);
            if (w <= 0f) return;

            ApplyPose(_poseRUp, t.RUpperArm, w);
            ApplyPose(_poseRLo, t.RLowerArm, w);
            ApplyPose(_poseLUp, t.LUpperArm, w);
            ApplyPose(_poseLLo, t.LLowerArm, w);
            ApplyPose(_poseSpine, t.Spine, w);
            ApplyPose(_poseHead, t.Head, w);
        }

        private static void ApplyPose(Transform bone, Quaternion offset, float weight)
        {
            if (bone == null) return;
            bone.localRotation = Quaternion.Slerp(bone.localRotation, bone.localRotation * offset, weight);
        }

        private void ResolvePoseBones()
        {
            if (_poseBonesResolved) return;
            _poseBonesResolved = true;
            _poseLUp = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _poseLLo = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _poseRUp = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _poseRLo = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _poseSpine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _poseHead = _animator.GetBoneTransform(HumanBodyBones.Head);
        }

        // ── Action → state map ──
        // Edge-triggered: crossfade once when the resolved target state changes. One-shots exit
        // themselves via exit-time; loops (BoxOut/Screen) are crossfaded back to locomotion here
        // when their action ends. When a state is absent (clip not dropped in), fall back to the
        // legacy Jump/Throw triggers so the pre-Phase-4 controller still animates.
        private void DriveActionState(PlayerAction action, ShotType? shot)
        {
            int target = ResolveStateHash(action, shot);
            if (target == _lastActionHash) return;

            if (target != 0)
            {
                if (HasStateCached(target))
                    _animator.CrossFadeInFixedTime(target, 0.12f);
                else
                    FireLegacyTrigger(action);   // state missing → old trigger web
            }
            else
            {
                // Returning to the locomotion family. One-shots already exited on their own; a loop
                // (BoxOut/Screen) has no exit transition, so crossfade it out explicitly.
                if (IsLoopState(_lastActionHash) && HasStateCached(LocomotionHash))
                    _animator.CrossFadeInFixedTime(LocomotionHash, 0.15f);
            }
            _lastActionHash = target;
        }

        /// <summary>Resolve the animator state for (action, shot variant). 0 = no dedicated state
        /// (idle/run/dribble/defend handled by the locomotion + stance machinery). An unknown or
        /// null ShotStyle on a Shooting action falls through to the generic JumpShot.</summary>
        private static int ResolveStateHash(PlayerAction action, ShotType? shot)
        {
            switch (action)
            {
                case PlayerAction.Shooting:
                    switch (shot)
                    {
                        case ShotType.Fadeaway:
                        case ShotType.StepBack: return FadeawayHash;
                        case ShotType.Floater:
                        case ShotType.Hookshot: return FloaterHash;
                        case ShotType.Heave:    return HeaveHash;
                        default:                return JumpShotHash; // Jumper/CatchAndShoot/PullUp/TipIn/null
                    }
                case PlayerAction.Dunking:        return DunkHash;
                case PlayerAction.Layup:          return LayupHash;
                case PlayerAction.LayupAcrobatic: return LayupAcrobaticHash;
                case PlayerAction.Passing:     return ChestPassHash;  // BouncePass indistinguishable on ActorFrame — always chest for now
                case PlayerAction.Catching:    return CatchHash;
                case PlayerAction.Rebounding:  return ReboundHash;
                case PlayerAction.BoxingOut:   return BoxOutHash;     // loop
                case PlayerAction.Screening:   return ScreenHash;     // loop
                case PlayerAction.Celebrating: return CelebrateHash;
                default:                       return 0;
            }
            // Steal/Block states exist in the controller but have no source action in PlayerAction
            // yet (no Stealing/Blocking). They light up automatically once those actions arrive.
        }

        private static bool IsLoopState(int hash) => hash == BoxOutHash || hash == ScreenHash;

        private void FireLegacyTrigger(PlayerAction action)
        {
            if (_hasThrow && (action == PlayerAction.Shooting || action == PlayerAction.Dunking))
                _animator.SetTrigger(ThrowHash);
            else if (_hasJump && (action == PlayerAction.Layup || action == PlayerAction.Rebounding))
                _animator.SetTrigger(JumpHash);
        }

        private bool HasStateCached(int hash)
        {
            if (hash == 0) return false;
            if (_hasState.TryGetValue(hash, out var has)) return has;
            has = _animator.HasState(0, hash);
            _hasState[hash] = has;
            return has;
        }

        // ── Prefab loading (cached; resolved once per session) ──

        private static GameObject LoadPrefab()
        {
            if (!_prefabResolved)
            {
                _prefab = Resources.Load<GameObject>(ResourcePath);
                _prefabResolved = true;
                if (_prefab == null)
                    Debug.Log($"[CharacterBody] No character prefab at Resources/{ResourcePath} — " +
                              "using capsule bodies. Run Tools ▸ NBA Head Coach ▸ Build Character Animator.");
            }
            return _prefab;
        }

        // ── Appearance ──

        /// <summary>Uniform-scale the instantiated model so its rendered height equals
        /// <paramref name="targetFeet"/>, measured from combined renderer bounds — immune to the
        /// pack's native import scale.</summary>
        private static void ScaleToHeight(GameObject go, float targetFeet)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float native = bounds.size.y;
            if (native <= 0.001f) native = 1.8f;   // safety if bounds aren't ready
            float factor = targetFeet / native;
            go.transform.localScale = go.transform.localScale * factor;
        }

        // How strongly a player's skin tone tints the head atlas (0 = raw atlas, 1 = flat skin).
        // Kept mild so the atlas's face + hair detail survives while still varying per player.
        private const float HeadSkinTint = 0.3f;
        // Guarded diagnostic budget: log the per-part color assignment for the first dozen actors
        // built this session, then go quiet. Confirms the team/skin bucketing definitively.
        private static int _logBudget = 12;

        /// <summary>Tint each body part via a MaterialPropertyBlock. Classification DEFAULTS to
        /// TEAM color — only an explicit head part gets skin — so a player can never come out
        /// uniform-less. The head keeps its atlas material (face + hair) mildly nudged toward the
        /// player's skin tone; the torso/arms/legs render flat, full team color for unmistakable
        /// broadcast-distance ID. Uses the index-less SetPropertyBlock (covers every submesh) and
        /// includeInactive so no renderer is ever skipped.</summary>
        private static void ApplyAppearance(GameObject go, PlayerAppearance appearance)
        {
            if (appearance == null) return;

            bool log = _logBudget > 0;
            if (log) _logBudget--;

            Color head = Color.Lerp(Color.white, appearance.Skin, HeadSkinTint);

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                bool isHead = IsHeadPart(r.gameObject.name);
                Color c = isHead ? head : appearance.Jersey;   // default bucket is TEAM

                var block = new MaterialPropertyBlock();
                Match3DMaterials.ApplyColor(block, c);
                r.SetPropertyBlock(block);

                if (log)
                    Debug.Log($"[CharacterBody] {appearance.DebugId} part='{r.gameObject.name}' " +
                              $"bucket={(isHead ? "SKIN" : "TEAM")} color=({c.r:F2},{c.g:F2},{c.b:F2})");
            }
        }

        /// <summary>Only the head mesh shows skin tone; every other part defaults to team color.
        /// Exact "Rogue_Head" match first, with a loose head/face fallback for other packs.</summary>
        private static bool IsHeadPart(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return n == "rogue_head" || n.Contains("head") || n.Contains("face");
        }

        private static bool HasParam(Animator animator, int hash, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (var p in animator.parameters)
                if (p.nameHash == hash) return p.type == type;
            return false;
        }
    }
}
