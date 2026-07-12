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
    ///   Stance (bool)                  → crouched defensive idle
    ///   Jump  (trigger)                → leap (layup/rebound)
    ///   Throw (trigger)                → shot/dunk release
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
        private bool _hasSpeed, _hasStance, _hasJump, _hasThrow, _hasMotionRate;

        private float _heightFeet = 6.5f;
        private PlayerAction _lastAction = PlayerAction.Idle;
        private bool _lastStance;

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

            // Edge-triggered action clips: fire once when the scripted action starts.
            if (frame.Action != _lastAction)
            {
                if (_hasThrow && IsShot(frame.Action)) _animator.SetTrigger(ThrowHash);
                else if (_hasJump && IsLeap(frame.Action)) _animator.SetTrigger(JumpHash);
                _lastAction = frame.Action;
            }
        }

        private static bool IsShot(PlayerAction a) =>
            a == PlayerAction.Shooting || a == PlayerAction.Dunking;

        private static bool IsLeap(PlayerAction a) =>
            a == PlayerAction.Layup || a == PlayerAction.Rebounding;

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
