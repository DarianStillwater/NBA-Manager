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
    ///  2. writes <see cref="ControllerPath"/> — Locomotion + DribbleMove Speed blend trees, a
    ///     defensive stance/slide pair, flat shot/pass/rebound/etc. action states crossfaded by
    ///     name, and legacy Jump/Throw Any-State triggers as fallback; missing clips are skipped,
    ///  3. writes a URP body material + a Resources player prefab wired with the model, the
    ///     controller, and the humanoid avatar, so <c>CharacterBody</c> can Resources.Load it.
    ///
    /// Safe to re-run; overwrites the same asset paths.
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        private const string PackFolder = "Assets/ThirdParty/KayKit_Adventurers";
        private const string AnimFolder = "Assets/ThirdParty/Animations";   // user-dropped basketball clips (may not exist yet)
        private const string FbxPath = PackFolder + "/Rogue.fbx";
        private const string TexturePath = PackFolder + "/rogue_texture.png";

        private const string GameAssetsFolder = "Assets/GameAssets";
        private const string ControllerPath = GameAssetsFolder + "/CharacterAnimator.controller";
        private const string MaterialPath = GameAssetsFolder + "/CharacterBody.mat";
        private const string HeadMaterialPath = GameAssetsFolder + "/CharacterHead.mat";

        private const string ResourcesFolder = "Assets/Resources";
        private const string ResourcesSubFolder = "Match3D";
        private const string PrefabPath = ResourcesFolder + "/" + ResourcesSubFolder + "/PlayerCharacter.prefab";

        // Role → candidate clip names (first match wins). KayKit clip names are verified from the
        // pack's glTF; basketball clips resolve to the canonical filenames the user drops into
        // AnimFolder (the AnimationClip name equals the FBX filename for single-take exports).
        private static readonly string[] IdleNames = { "Idle", "Unarmed_Idle" };
        private static readonly string[] WalkNames = { "Walking_A", "Walking_C", "Walking_B", "Walk" };
        private static readonly string[] RunNames = { "Running_A", "Running_B", "Run" };
        private static readonly string[] JumpNames = { "Jump_Full_Long", "Jump_Full_Short", "Jump_Start" };
        private static readonly string[] ThrowNames = { "Throw", "1H_Ranged_Shoot", "Spellcast_Shoot" };
        private static readonly string[] StanceNames = { "Blocking", "Block", "2H_Melee_Idle", "Unarmed_Pose" };

        // Basketball clips (AnimFolder). "_Loop" suffix → KayKitModelPostprocessor turns loop time on.
        private static readonly string[] DribbleIdleNames = { "DribbleIdle_Loop", "DribbleIdle" };
        private static readonly string[] DribbleWalkNames = { "DribbleWalk_Loop", "DribbleWalk" };
        private static readonly string[] DribbleRunNames = { "DribbleRun_Loop", "DribbleRun" };
        private static readonly string[] SlideNames = { "DefensiveSlide_Loop", "DefensiveSlide" };
        private static readonly string[] JumpShotNames = { "JumpShot" };
        private static readonly string[] FadeawayNames = { "Fadeaway" };
        private static readonly string[] FloaterNames = { "Floater" };
        private static readonly string[] LayupNames = { "Layup" };
        private static readonly string[] LayupAcrobaticNames = { "LayupAcrobatic" };
        private static readonly string[] DunkNames = { "Dunk" };
        private static readonly string[] HeaveNames = { "Heave" };
        private static readonly string[] ChestPassNames = { "ChestPass" };
        private static readonly string[] BouncePassNames = { "BouncePass" };
        private static readonly string[] CatchNames = { "Catch" };
        private static readonly string[] StealNames = { "Steal" };
        private static readonly string[] BlockNames = { "Block" };
        private static readonly string[] ReboundNames = { "Rebound" };
        private static readonly string[] BoxOutNames = { "BoxOut_Loop", "BoxOut" };
        private static readonly string[] ScreenNames = { "Screen_Loop", "Screen" };
        private static readonly string[] CelebrateNames = { "Celebrate" };
        private static readonly string[] SitNames = { "Sit_Loop", "Sit" };   // Phase 5; resolved, state not built yet

        [MenuItem("Tools/NBA Head Coach/Build Character Animator")]
        public static void Build()
        {
            FixupAnimFolderImports();
            var clips = FindClips();
            if (clips.Count == 0)
            {
                Debug.LogError($"[CharacterAnimatorBuilder] No AnimationClips found under {PackFolder}. " +
                               "Make sure the KayKit FBX imported (Humanoid) before running this.");
                return;
            }

            EnsureFolder(GameAssetsFolder);
            var controller = BuildController(clips);
            BuildPrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CharacterAnimatorBuilder] Done. Controller: {ControllerPath}  Prefab: {PrefabPath}");
        }

        // ── Clip discovery + mapping ──

        /// <summary>
        /// Normalize the user-dropped basketball FBX imports at build time. Mixamo names every take
        /// "mixamo.com", and OnPreprocessModel can't see take info on a virgin import — so the menu
        /// action itself renames each clip to its canonical FILENAME, applies the _Loop convention,
        /// and locks root motion, reimporting only when something actually changed.
        /// </summary>
        private static void FixupAnimFolderImports()
        {
            if (!AssetDatabase.IsValidFolder(AnimFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AnimFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;

                var clips = mi.clipAnimations;
                if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;

                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                bool loop = file.EndsWith("_Loop");
                bool dirty = false;
                for (int i = 0; i < clips.Length; i++)
                {
                    string want = clips.Length == 1 ? file : $"{file}_{i}";
                    if (clips[i].name != want) { clips[i].name = want; dirty = true; }
                    if (clips[i].loopTime != loop) { clips[i].loopTime = loop; clips[i].loopPose = loop; dirty = true; }
                    if (!clips[i].lockRootPositionXZ)
                    {
                        clips[i].lockRootPositionXZ = true;
                        clips[i].lockRootHeightY = true;
                        clips[i].lockRootRotation = true;
                        dirty = true;
                    }
                }
                if (dirty)
                {
                    mi.clipAnimations = clips;
                    mi.SaveAndReimport();
                    Debug.Log($"[CharacterAnimatorBuilder] Normalized import: {path} → clip \"{file}\"{(loop ? " (loop)" : "")}");
                }
            }
        }

        private static Dictionary<string, AnimationClip> FindClips()
        {
            var map = new Dictionary<string, AnimationClip>();
            // Scan the KayKit pack + the basketball-clip drop folder. FindAssets warns if handed a
            // non-existent folder, so only pass folders that are actually present (AnimFolder is
            // absent until the user drops FBX clips in — that's the normal zero-clip state).
            var folders = new List<string> { PackFolder };
            if (AssetDatabase.IsValidFolder(AnimFolder)) folders.Add(AnimFolder);

            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", folders.ToArray()))
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

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            // Locomotion / legacy (KayKit).
            var idle = Pick(clips, IdleNames, "Idle");
            var walk = Pick(clips, WalkNames, "Walk");
            var run = Pick(clips, RunNames, "Run");
            var jump = Pick(clips, JumpNames, "Jump");
            var shot = Pick(clips, ThrowNames, "Throw");
            var stance = Pick(clips, StanceNames, "Stance");

            // Basketball clips. Missing shot/pass/layup variants alias onto their base state so the
            // state still exists (CharacterBody's map stays simple); base missing → state skipped.
            var dribIdle = Pick(clips, DribbleIdleNames, "DribIdle");
            var dribWalk = Pick(clips, DribbleWalkNames, "DribWalk");
            var dribRun = Pick(clips, DribbleRunNames, "DribRun");
            var slide = Pick(clips, SlideNames, "Slide");

            var jumpShot = Pick(clips, JumpShotNames, "JumpShot");
            var fadeaway = Alias(Pick(clips, FadeawayNames, "Fadeaway"), jumpShot, "Fadeaway", "JumpShot");
            var floater = Alias(Pick(clips, FloaterNames, "Floater"), jumpShot, "Floater", "JumpShot");
            var heave = Alias(Pick(clips, HeaveNames, "Heave"), jumpShot, "Heave", "JumpShot");
            var layup = Pick(clips, LayupNames, "Layup");
            var layupAcro = Alias(Pick(clips, LayupAcrobaticNames, "LayupAcro"), layup, "LayupAcrobatic", "Layup");
            var dunk = Pick(clips, DunkNames, "Dunk");
            var chestPass = Pick(clips, ChestPassNames, "ChestPass");
            var bouncePass = Alias(Pick(clips, BouncePassNames, "BouncePass"), chestPass, "BouncePass", "ChestPass");
            var catchClip = Pick(clips, CatchNames, "Catch");
            var steal = Pick(clips, StealNames, "Steal");
            var block = Pick(clips, BlockNames, "Block");
            var rebound = Pick(clips, ReboundNames, "Rebound");
            var boxOut = Pick(clips, BoxOutNames, "BoxOut");
            var screen = Pick(clips, ScreenNames, "Screen");
            var celebrate = Pick(clips, CelebrateNames, "Celebrate");
            Pick(clips, SitNames, "Sit");   // Phase 5 — resolve into the table; no state built yet

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Stance", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dribbling", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
            // Drives locomotion-family clip playback rate so foot cadence matches the measured body
            // speed (anti-skate); CharacterBody sets it, defaulting to 1 if this param is absent.
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "MotionRate",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });

            var sm = controller.layers[0].stateMachine;

            // Locomotion: 1D Speed blend (idle/walk/run), played back at MotionRate.
            var locomotion = Blend1D(controller, "Locomotion",
                (idle, 0f), (walk, 0.35f), (run, 1f));
            sm.defaultState = locomotion;

            // DribbleMove: same 1D blend with the ball, entered while the Dribbling bool is set.
            AnimatorState dribble = null;
            if (dribIdle != null || dribWalk != null || dribRun != null)
            {
                dribble = Blend1D(controller, "DribbleMove",
                    (dribIdle ?? idle, 0f), (dribWalk ?? walk, 0.35f), (dribRun ?? run, 1f));

                var toDrib = locomotion.AddTransition(dribble);
                toDrib.hasExitTime = false; toDrib.duration = 0.15f;
                toDrib.AddCondition(AnimatorConditionMode.If, 0f, "Dribbling");

                var fromDrib = dribble.AddTransition(locomotion);
                fromDrib.hasExitTime = false; fromDrib.duration = 0.15f;
                fromDrib.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dribbling");
            }

            // Defensive stance: crouched idle at low speed, slide clip (loop) while moving. Slide is
            // a locomotion-family state so it also rides MotionRate. If no slide clip, the crouch
            // covers all stance speeds (previous behavior).
            if (stance != null)
            {
                var stanceState = sm.AddState("Stance");
                stanceState.motion = stance;

                var toStance = locomotion.AddTransition(stanceState);
                toStance.hasExitTime = false; toStance.duration = 0.15f;
                toStance.AddCondition(AnimatorConditionMode.If, 0f, "Stance");
                // With a slide clip the crouch is only the low-speed pose; without one it covers all
                // stance speeds, so the speed guard is added only when a slide state will exist.
                if (slide != null)
                    toStance.AddCondition(AnimatorConditionMode.Less, 0.30f, "Speed");

                var stanceOffBool = stanceState.AddTransition(locomotion);
                stanceOffBool.hasExitTime = false; stanceOffBool.duration = 0.15f;
                stanceOffBool.AddCondition(AnimatorConditionMode.IfNot, 0f, "Stance");

                if (slide != null)
                {
                    var slideState = sm.AddState("DefensiveSlide");
                    slideState.motion = slide;
                    slideState.speedParameterActive = true;
                    slideState.speedParameter = "MotionRate";

                    var toSlideFromLoco = locomotion.AddTransition(slideState);
                    toSlideFromLoco.hasExitTime = false; toSlideFromLoco.duration = 0.15f;
                    toSlideFromLoco.AddCondition(AnimatorConditionMode.If, 0f, "Stance");
                    toSlideFromLoco.AddCondition(AnimatorConditionMode.Greater, 0.30f, "Speed");

                    var toSlide = stanceState.AddTransition(slideState);
                    toSlide.hasExitTime = false; toSlide.duration = 0.15f;
                    toSlide.AddCondition(AnimatorConditionMode.Greater, 0.35f, "Speed");

                    var fromSlideToCrouch = slideState.AddTransition(stanceState);
                    fromSlideToCrouch.hasExitTime = false; fromSlideToCrouch.duration = 0.15f;
                    fromSlideToCrouch.AddCondition(AnimatorConditionMode.Less, 0.25f, "Speed");

                    var slideOff = slideState.AddTransition(locomotion);
                    slideOff.hasExitTime = false; slideOff.duration = 0.15f;
                    slideOff.AddCondition(AnimatorConditionMode.IfNot, 0f, "Stance");
                }
                else
                {
                    // Legacy fallback: leave the crouch when moving fast even without a slide clip.
                    var stanceOffMove = stanceState.AddTransition(locomotion);
                    stanceOffMove.hasExitTime = false; stanceOffMove.duration = 0.15f;
                    stanceOffMove.AddCondition(AnimatorConditionMode.Greater, 0.40f, "Speed");
                }
            }

            // Flat action states — no Any-State web. CharacterBody crossfades into them by name.
            // One-shots return to Locomotion on exit time; loops stay until code crossfades out.
            AddOneShot(sm, locomotion, jumpShot, "JumpShot");
            AddOneShot(sm, locomotion, fadeaway, "Fadeaway");
            AddOneShot(sm, locomotion, floater, "Floater");
            AddOneShot(sm, locomotion, layup, "Layup");
            AddOneShot(sm, locomotion, layupAcro, "LayupAcrobatic");
            AddOneShot(sm, locomotion, dunk, "Dunk");
            AddOneShot(sm, locomotion, heave, "Heave");
            AddOneShot(sm, locomotion, chestPass, "ChestPass");
            AddOneShot(sm, locomotion, bouncePass, "BouncePass");
            AddOneShot(sm, locomotion, catchClip, "Catch");
            AddOneShot(sm, locomotion, steal, "Steal");
            AddOneShot(sm, locomotion, block, "Block");
            AddOneShot(sm, locomotion, rebound, "Rebound");
            AddOneShot(sm, locomotion, celebrate, "Celebrate");
            AddLoop(sm, boxOut, "BoxOut");
            AddLoop(sm, screen, "Screen");

            // Keep the legacy Any-State Jump/Throw triggers for CapsuleBody-era timelines and as the
            // CharacterBody fallback when an action state is absent.
            AddTriggerState(sm, locomotion, jump, "JumpTrigger", "Jump");
            AddTriggerState(sm, locomotion, shot, "ThrowTrigger", "Throw");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>Create a Simple1D Speed blend tree state driven at MotionRate. Null children are
        /// skipped; the state always exists (idle is present in the KayKit pack).</summary>
        private static AnimatorState Blend1D(AnimatorController controller, string name,
            params (AnimationClip clip, float threshold)[] children)
        {
            var state = controller.CreateBlendTreeInController(name, out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            state.speedParameterActive = true;
            state.speedParameter = "MotionRate";
            foreach (var (clip, threshold) in children)
                if (clip != null) tree.AddChild(clip, threshold);
            return state;
        }

        /// <summary>Fall back to a base clip when a variant is missing so the named state still
        /// exists. Returns primary if present, else logs the alias and returns the base.</summary>
        private static AnimationClip Alias(AnimationClip primary, AnimationClip baseClip,
            string state, string baseName)
        {
            if (primary != null) return primary;
            if (baseClip != null)
                Debug.Log($"[CharacterAnimatorBuilder] {state} → aliased to {baseName} (variant clip absent).");
            return baseClip;
        }

        /// <summary>Flat one-shot state that returns to locomotion on exit time (no clip → skipped;
        /// CharacterBody then uses the legacy trigger). Never on Any State.</summary>
        private static void AddOneShot(AnimatorStateMachine sm, AnimatorState locomotion,
            AnimationClip clip, string stateName)
        {
            if (clip == null) return;
            var state = sm.AddState(stateName);
            state.motion = clip;
            var exit = state.AddTransition(locomotion);
            exit.hasExitTime = true; exit.exitTime = 0.9f; exit.duration = 0.15f;
        }

        /// <summary>Flat looping state with NO exit transition — CharacterBody crossfades back to
        /// locomotion when the action ends. No clip → skipped.</summary>
        private static void AddLoop(AnimatorStateMachine sm, AnimationClip clip, string stateName)
        {
            if (clip == null) return;
            var state = sm.AddState(stateName);
            state.motion = clip;
        }

        /// <summary>Legacy one-shot reachable from Any State via a trigger, returning to locomotion
        /// on exit time. No-op if the clip is missing.</summary>
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
