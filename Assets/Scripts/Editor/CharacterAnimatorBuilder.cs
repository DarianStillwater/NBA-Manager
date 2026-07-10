using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NBAHeadCoach.EditorTools
{
    /// <summary>
    /// One-shot builder for the 3D match character rig. Run <b>Tools ▸ NBA Head Coach ▸ Build
    /// Character Animator</b> after the KayKit FBX has imported as Humanoid. It:
    ///  1. finds the pack's imported AnimationClips and maps them to gameplay roles (logs the map),
    ///  2. writes <see cref="ControllerPath"/> — a Speed blend tree (idle/walk/run) plus Stance
    ///     (bool), Jump and Throw (triggers), guarding any clips that are missing,
    ///  3. writes a URP body material + a Resources player prefab wired with the model, the
    ///     controller, and the humanoid avatar, so <c>CharacterBody</c> can Resources.Load it.
    ///
    /// Safe to re-run; overwrites the same asset paths.
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        private const string PackFolder = "Assets/ThirdParty/KayKit_Adventurers";
        private const string FbxPath = PackFolder + "/Rogue.fbx";
        private const string TexturePath = PackFolder + "/rogue_texture.png";

        private const string GameAssetsFolder = "Assets/GameAssets";
        private const string ControllerPath = GameAssetsFolder + "/CharacterAnimator.controller";
        private const string MaterialPath = GameAssetsFolder + "/CharacterBody.mat";
        private const string HeadMaterialPath = GameAssetsFolder + "/CharacterHead.mat";

        private const string ResourcesFolder = "Assets/Resources";
        private const string ResourcesSubFolder = "Match3D";
        private const string PrefabPath = ResourcesFolder + "/" + ResourcesSubFolder + "/PlayerCharacter.prefab";

        // Role → candidate clip names (first match wins). Names verified from the pack's glTF.
        private static readonly string[] IdleNames = { "Idle", "Unarmed_Idle" };
        private static readonly string[] WalkNames = { "Walking_A", "Walking_C", "Walking_B" };
        private static readonly string[] RunNames = { "Running_A", "Running_B" };
        private static readonly string[] JumpNames = { "Jump_Full_Long", "Jump_Full_Short", "Jump_Start" };
        private static readonly string[] ThrowNames = { "Throw", "1H_Ranged_Shoot", "Spellcast_Shoot" };
        private static readonly string[] StanceNames = { "Blocking", "Block", "2H_Melee_Idle", "Unarmed_Pose" };

        [MenuItem("Tools/NBA Head Coach/Build Character Animator")]
        public static void Build()
        {
            var clips = FindClips();
            if (clips.Count == 0)
            {
                Debug.LogError($"[CharacterAnimatorBuilder] No AnimationClips found under {PackFolder}. " +
                               "Make sure the KayKit FBX imported (Humanoid) before running this.");
                return;
            }

            var idle = Pick(clips, IdleNames, "Idle");
            var walk = Pick(clips, WalkNames, "Walk");
            var run = Pick(clips, RunNames, "Run");
            var jump = Pick(clips, JumpNames, "Jump");
            var shot = Pick(clips, ThrowNames, "Throw");
            var stance = Pick(clips, StanceNames, "Stance");

            EnsureFolder(GameAssetsFolder);
            var controller = BuildController(idle, walk, run, jump, shot, stance);
            BuildPrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CharacterAnimatorBuilder] Done. Controller: {ControllerPath}  Prefab: {PrefabPath}");
        }

        // ── Clip discovery + mapping ──

        private static Dictionary<string, AnimationClip> FindClips()
        {
            var map = new Dictionary<string, AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { PackFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        map[clip.name] = clip;
                }
                // The clip may also be the main asset (single-take files).
                var main = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (main != null && !main.name.StartsWith("__preview__")) map[main.name] = main;
            }
            return map;
        }

        private static AnimationClip Pick(Dictionary<string, AnimationClip> clips, string[] candidates, string role)
        {
            foreach (var name in candidates)
                if (clips.TryGetValue(name, out var clip))
                {
                    Debug.Log($"[CharacterAnimatorBuilder] {role,-7} → \"{name}\"");
                    return clip;
                }
            Debug.LogWarning($"[CharacterAnimatorBuilder] {role,-7} → (none of {string.Join(", ", candidates)}) — state skipped.");
            return null;
        }

        // ── AnimatorController ──

        private static AnimatorController BuildController(
            AnimationClip idle, AnimationClip walk, AnimationClip run,
            AnimationClip jump, AnimationClip shot, AnimationClip stance)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Stance", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            // Locomotion: 1D Speed blend (normalized 0..1).
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 0.35f);
            if (run != null) tree.AddChild(run, 1f);
            sm.defaultState = locomotion;

            // Defensive stance (crouched idle), gated on the Stance bool at low speed.
            if (stance != null)
            {
                var stanceState = sm.AddState("Stance");
                stanceState.motion = stance;

                var toStance = locomotion.AddTransition(stanceState);
                toStance.hasExitTime = false; toStance.duration = 0.15f;
                toStance.AddCondition(AnimatorConditionMode.If, 0f, "Stance");
                toStance.AddCondition(AnimatorConditionMode.Less, 0.30f, "Speed");

                var stanceOffBool = stanceState.AddTransition(locomotion);
                stanceOffBool.hasExitTime = false; stanceOffBool.duration = 0.15f;
                stanceOffBool.AddCondition(AnimatorConditionMode.IfNot, 0f, "Stance");

                var stanceOffMove = stanceState.AddTransition(locomotion);
                stanceOffMove.hasExitTime = false; stanceOffMove.duration = 0.15f;
                stanceOffMove.AddCondition(AnimatorConditionMode.Greater, 0.40f, "Speed");
            }

            AddTriggerState(sm, locomotion, jump, "Jump", "Jump");
            AddTriggerState(sm, locomotion, shot, "Throw", "Throw");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>Add a one-shot state reachable from Any State via a trigger, returning to
        /// locomotion on exit time. No-op if the clip is missing.</summary>
        private static void AddTriggerState(AnimatorStateMachine sm, AnimatorState locomotion,
            AnimationClip clip, string stateName, string trigger)
        {
            if (clip == null) return;

            var state = sm.AddState(stateName);
            state.motion = clip;

            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false; enter.duration = 0.05f; enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);

            var exit = state.AddTransition(locomotion);
            exit.hasExitTime = true; exit.exitTime = 0.85f; exit.duration = 0.15f;
        }

        // ── Body material + Resources prefab ──

        private static void BuildPrefab(AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                Debug.LogError($"[CharacterAnimatorBuilder] Model not found at {FbxPath}; prefab not built.");
                return;
            }

            var avatar = AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxPath)
                .OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
                Debug.LogWarning("[CharacterAnimatorBuilder] No Humanoid Avatar on the FBX — did it import as Humanoid? " +
                                 "Reimport the model with the KayKit postprocessor active, then re-run.");

            var bodyMat = BuildBodyMaterial();
            var headMat = BuildHeadMaterial();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "PlayerCharacter";
            // Model prefabs instantiate as read-only linked instances; unpack so deleting prop
            // children and reassigning materials bakes cleanly into the saved prefab.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            StripProps(instance);

            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = false;

            // Head keeps the atlas material (face + hair show through, mildly skin-tinted at
            // runtime); every other part gets the flat, atlas-free material so its runtime team
            // tint renders at full strength.
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var chosen = IsHeadMesh(r.gameObject.name) ? headMat : bodyMat;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = chosen;
                r.sharedMaterials = mats;
            }

            EnsureFolder(ResourcesFolder);
            EnsureFolder(ResourcesFolder + "/" + ResourcesSubFolder);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
        }

        // The Rogue's body is split into these named skinned meshes; everything else in the
        // hierarchy with a Renderer is a prop (Knife/Knife_Offhand, 1H/2H_Crossbow, Rogue_Cape)
        // and gets stripped so players hold and wear nothing. Allowlist by prefix so extra body
        // parts survive but any prop is dropped. Bone transforms have no Renderer and are untouched.
        private static readonly string[] BodyMeshPrefixes =
            { "Rogue_Body", "Rogue_Head", "Rogue_Arm", "Rogue_Leg" };

        private static void StripProps(GameObject instance)
        {
            var props = new List<GameObject>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                if (!IsBodyMesh(r.gameObject.name)) props.Add(r.gameObject);

            foreach (var go in props)
            {
                Debug.Log($"[CharacterAnimatorBuilder] Stripped prop mesh: {go.name}");
                Object.DestroyImmediate(go);
            }
        }

        private static bool IsBodyMesh(string name)
        {
            foreach (var p in BodyMeshPrefixes)
                if (name.StartsWith(p)) return true;
            return false;
        }

        private static bool IsHeadMesh(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return n == "rogue_head" || n.Contains("head") || n.Contains("face");
        }

        private static Material BuildBodyMaterial()
        {
            // Plain URP/Lit, matte, pure white base. Deliberately NO albedo texture: the atlas is a
            // green tunic gradient that washed out the team tint (both teams read green). With a flat
            // base the per-player MPB tint becomes the whole visible color → unmistakable team ID.
            var mat = NewLit("CharacterBody");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            AssetDatabase.DeleteAsset(MaterialPath);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        private static Material BuildHeadMaterial()
        {
            // Head KEEPS the atlas albedo so the face + hair regions read (a flat head looked
            // bald/uncanny). The runtime mildly tints it toward the player's skin tone via MPB, so
            // hair stays dark and skin varies per player. White base preserves the atlas colors.
            var mat = NewLit("CharacterHead");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                mat.mainTexture = tex;
            }
            else
            {
                Debug.LogWarning($"[CharacterAnimatorBuilder] Head atlas not found at {TexturePath}; " +
                                 "head will render flat-tinted.");
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            AssetDatabase.DeleteAsset(HeadMaterialPath);
            AssetDatabase.CreateAsset(mat, HeadMaterialPath);
            return mat;
        }

        private static Material NewLit(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);
            return mat;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
