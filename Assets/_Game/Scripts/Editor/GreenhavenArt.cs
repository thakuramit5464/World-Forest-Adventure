using System;
using System.Collections.Generic;
using Greenhaven.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Greenhaven.Editor
{
    /// <summary>
    /// Scene-building helpers for imported art: characters + humanoid animation, nature
    /// props, and animals. Used by <see cref="Phase1SceneBuilder"/>. All placement is
    /// authored here with fixed seeds; nothing is generated at runtime.
    /// </summary>
    static class GreenhavenArt
    {
        const string MaterialFolder = "Assets/_Game/Materials";
        const string ControllerFolder = "Assets/_Game/Art/Controllers";
        const string CharacterModelPath = ArtImportSettings.CharactersFolder + "characterMedium.fbx";
        const string CharacterSkinFolder = ArtImportSettings.CharactersFolder + "Textures/";
        const string LibraryPath = ArtImportSettings.AnimationLibraryFolder + "AnimationLibrary_Unity_Standard.fbx";
        const string NatureModels = ArtImportSettings.NatureFolder + "Models/";
        const string NatureTextures = ArtImportSettings.NatureFolder + "Textures/";

        public const float CharacterHeight = 1.75f;
        // Humanoid animation orients the body along the Animator's +Z, so no correction is needed
        // (the 180° the Generic rig needed would now face the character backwards).
        const float CharacterYawOffset = 0f;

        /// <summary>The humanoid clip library (needed by any humanoid character, including the generated hero).</summary>
        public static bool AnimationLibraryAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(LibraryPath) != null;

        public static bool CharacterAvailable =>
            AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath) != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(LibraryPath) != null;

        public static bool NatureAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(NatureModels + "CommonTree_1.fbx") != null;

        public static bool AnimalsAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(ArtImportSettings.AnimalsFolder + "Sheep.fbx") != null;

        // ───────────────────────────── Import preparation ─────────────────────────────

        /// <summary>Models that could not be found during the current build (reported by the builder).</summary>
        public static int MissingModels { get; private set; }

        /// <summary>Makes sure import rules have been applied and nature materials are remapped.</summary>
        public static void PrepareImports()
        {
            MissingModels = 0;
            EnsureFolder(ControllerFolder);
            EnsureHumanoid(CharacterModelPath);
            EnsureHumanoid(LibraryPath);
            if (NatureAvailable) RemapNatureMaterials();
        }

        static void EnsureHumanoid(string path)
        {
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && importer.animationType != ModelImporterAnimationType.Human)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); // re-runs ArtImportSettings
        }

        // FBX material name → (texture, alpha cutout)
        static readonly (string name, string texture, bool cutout)[] NatureMaterialDefs =
        {
            ("Bark_NormalTree", "Bark_NormalTree", false),
            ("Bark_DeadTree", "Bark_DeadTree", false),
            ("Bark_TwistedTree", "Bark_TwistedTree", false),
            ("Leaves_NormalTree", "Leaves_NormalTree_C", true),
            ("Leaves_TwistedTree", "Leaves_TwistedTree_C", true),
            ("Leaves_Pine", "Leaf_Pine_C", true),
            ("Leaves", "Leaves", true),
            ("Flowers", "Flowers", true),
            ("Grass", "Grass", true),
            ("Mushrooms", "Mushrooms", false),
            ("PathRocks", "PathRocks_Diffuse", false),
            ("Rocks", "Rocks_Diffuse", false),
        };

        static void RemapNatureMaterials()
        {
            var byName = new Dictionary<string, Material>();
            foreach (var def in NatureMaterialDefs)
                byName[def.name] = TexturedMaterial("M_Nature_" + def.name, NatureTextures + def.texture + ".png", def.cutout);
            byName["Material"] = byName["Bark_NormalTree"]; // unnamed slot in Bush_Common

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { NatureModels.TrimEnd('/') }))
                {
                    if (!(AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is ModelImporter importer)) continue;
                    Dictionary<AssetImporter.SourceAssetIdentifier, Object> current = importer.GetExternalObjectMap();
                    bool changed = false;
                    foreach (var pair in byName)
                    {
                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                        if (current.TryGetValue(id, out Object existing) && existing == pair.Value) continue;
                        importer.AddRemap(id, pair.Value);
                        changed = true;
                    }
                    if (changed) importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        /// <summary>URP Simple Lit material with a base texture; optional double-sided alpha cutout.</summary>
        public static Material TexturedMaterial(string name, string texturePath, bool cutout)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex == null) Debug.LogWarning($"Greenhaven: texture not found: {texturePath}");
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);

            mat.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_Cull", cutout ? (float)CullMode.Off : (float)CullMode.Back);
            if (cutout)
            {
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = (int)RenderQueue.Geometry;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ───────────────────────────── Model placement ─────────────────────────────

        public enum Fit { Height, Width }

        public struct Placed
        {
            public GameObject Root;
            /// <summary>Model bounds in the root's local (unscaled) space.</summary>
            public Bounds LocalBounds;
        }

        /// <summary>
        /// Instantiates <paramref name="assetPath"/> inside a holder object at <paramref name="position"/>,
        /// scaled so its height (or width) equals <paramref name="size"/>, with its base on the ground.
        /// The model keeps its import rotation; yaw and scale live on the holder.
        /// </summary>
        public static Placed? Place(string assetPath, Transform parent, Vector3 position, float yaw, float size,
            Fit fit = Fit.Height, float sink = 0f, bool isStatic = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"Greenhaven: model not found: {assetPath}");
                MissingModels++;
                return null;
            }

            var holder = new GameObject(asset.name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = position;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
            model.transform.localPosition = Vector3.zero;

            Bounds b = WorldBounds(model, position);
            float measured = fit == Fit.Height ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            float scale = measured > 0.0001f ? size / measured : 1f;

            // Sit the model's base on the holder's origin (optionally sunk into the ground).
            float lift = -(b.min.y - position.y) - sink * b.size.y;
            model.transform.localPosition = new Vector3(0f, lift, 0f);
            var local = new Bounds(new Vector3(b.center.x - position.x, b.size.y * 0.5f - sink * b.size.y, b.center.z - position.z), b.size);

            holder.transform.localScale = Vector3.one * scale;
            holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (isStatic) SetStatic(holder, true);
            return new Placed { Root = holder, LocalBounds = local };
        }

        static Bounds WorldBounds(GameObject go, Vector3 fallbackCenter)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(fallbackCenter, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        public static void SetStatic(GameObject go, bool isStatic)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, isStatic ? StaticEditorFlags.BatchingStatic : 0);
        }

        static void ShadowsOff(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ───────────────────────────── Characters ─────────────────────────────

        /// <summary>Instantiates the rigged character under <paramref name="parent"/> with a skin and controller.</summary>
        public static Animator BuildCharacter(Transform parent, string skinFile, RuntimeAnimatorController controller)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            model.name = "CharacterModel";
            model.transform.localRotation = Quaternion.Euler(0f, CharacterYawOffset, 0f);

            Bounds b = WorldBounds(model, parent.position);
            if (b.size.y > 0.001f)
            {
                float scale = CharacterHeight / b.size.y;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = new Vector3(0f, -(b.min.y - parent.position.y) * scale, 0f);
            }

            Material skin = TexturedMaterial("M_Char_" + skinFile, CharacterSkinFolder + skinFile + ".png", false);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                r.sharedMaterials = mats;
            }

            // Explicit null check: Unity's fake-null objects defeat the ?? operator.
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                Debug.LogError($"Greenhaven: {CharacterModelPath} did not import as a valid Humanoid avatar. " +
                               "Open its Rig tab and use Configure… to fix the bone mapping.", model);
            return animator;
        }

        // ───────────────────────────── Animator controllers ─────────────────────────────

        static AnimationClip Clip(string fbxPath, string name)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (o is AnimationClip clip && clip.name == name) return clip;
            Debug.LogWarning($"Greenhaven: clip '{name}' not found in {fbxPath}.");
            return null;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration, float? exitTime = null)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.duration = duration;
            t.hasExitTime = exitTime.HasValue;
            if (exitTime.HasValue) t.exitTime = exitTime.Value;
            return t;
        }

        /// <summary>
        /// Returns an empty controller at the given name. An existing asset is cleared and reused
        /// (same GUID), so scenes built earlier keep a valid reference when another builder runs.
        /// </summary>
        static AnimatorController NewController(string name)
        {
            string path = $"{ControllerFolder}/{name}.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing == null) return AnimatorController.CreateAnimatorControllerAtPath(path);

            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub != null && sub != existing) Object.DestroyImmediate(sub, true);
            existing.layers = new AnimatorControllerLayer[0];
            existing.parameters = new AnimatorControllerParameter[0];
            existing.AddLayer("Base Layer");
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>
        /// Player: locomotion blend (idle/walk/jog/sprint by m/s), jump start/loop/land, and an
        /// upper-body "Interact" gesture layer so the player can keep walking while reaching.
        /// </summary>
        public static AnimatorController PlayerController(float walkSpeed, float runSpeed)
        {
            AnimatorController c = NewController("Player");
            c.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            c.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            c.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            AnimatorControllerParameter[] parameters = c.parameters; // copy: edit, then assign back
            foreach (AnimatorControllerParameter p in parameters)
                if (p.name == "Grounded") p.defaultBool = true;
            c.parameters = parameters;

            AnimatorStateMachine sm = c.layers[0].stateMachine;
            AnimatorState locomotion = c.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "MoveSpeed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip(LibraryPath, "Idle_Loop"), 0f);
            tree.AddChild(Clip(LibraryPath, "Walk_Loop"), walkSpeed * 0.9f);
            tree.AddChild(Clip(LibraryPath, "Jog_Fwd_Loop"), Mathf.Lerp(walkSpeed, runSpeed, 0.5f));
            tree.AddChild(Clip(LibraryPath, "Sprint_Loop"), runSpeed);

            AnimatorState jumpStart = sm.AddState("JumpStart");
            jumpStart.motion = Clip(LibraryPath, "Jump_Start");
            AnimatorState airborne = sm.AddState("Airborne");
            airborne.motion = Clip(LibraryPath, "Jump_Loop");
            AnimatorState land = sm.AddState("Land");
            land.motion = Clip(LibraryPath, "Jump_Land");
            sm.defaultState = locomotion;

            AnimatorStateTransition toJump = sm.AddAnyStateTransition(jumpStart);
            toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            toJump.duration = 0.05f;
            toJump.canTransitionToSelf = false;
            Transition(jumpStart, airborne, 0.1f, 0.85f);
            Transition(jumpStart, land, 0.05f, 0.25f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            Transition(locomotion, airborne, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            Transition(airborne, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            Transition(land, locomotion, 0.15f, 0.6f);
            Transition(land, locomotion, 0.1f).AddCondition(AnimatorConditionMode.Greater, 1f, "MoveSpeed");

            // Upper-body gesture layer.
            var actions = new AnimatorStateMachine { name = "Actions", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(actions, c);
            c.AddLayer(new AnimatorControllerLayer
            {
                name = "Actions",
                stateMachine = actions,
                avatarMask = UpperBodyMask(),
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
            });
            AnimatorState empty = actions.AddState("Empty");
            empty.writeDefaultValues = false; // pass the base layer through untouched
            AnimatorState interact = actions.AddState("Interact");
            interact.motion = Clip(LibraryPath, "Interact");
            interact.writeDefaultValues = false;
            actions.defaultState = empty;
            AnimatorStateTransition toInteract = actions.AddAnyStateTransition(interact);
            toInteract.AddCondition(AnimatorConditionMode.If, 0f, "Interact");
            toInteract.duration = 0.1f;
            toInteract.canTransitionToSelf = false;
            Transition(interact, empty, 0.2f, 0.85f);

            // IK pass on the base layer enables head/eye look-at (HeroSecondaryMotion.OnAnimatorIK).
            AnimatorControllerLayer[] layers = c.layers; // copy: edit, then assign back
            layers[0].iKPass = true;
            c.layers = layers;

            AssetDatabase.SaveAssets();
            return c;
        }

        static AvatarMask UpperBodyMask()
        {
            string path = $"{ControllerFolder}/UpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                             part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>NPC: idle loop, switching to a talk loop while the "Talking" bool is true.</summary>
        public static AnimatorController NpcController(string name, string idleClip, string talkClip)
        {
            AnimatorController c = NewController(name);
            c.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine sm = c.layers[0].stateMachine;
            AnimatorState idle = sm.AddState("Idle");
            idle.motion = Clip(LibraryPath, idleClip);
            AnimatorState talk = sm.AddState("Talk");
            talk.motion = Clip(LibraryPath, talkClip);
            sm.defaultState = idle;
            Transition(idle, talk, 0.25f).AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            Transition(talk, idle, 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
            AssetDatabase.SaveAssets();
            return c;
        }

        static AnimatorController AnimalController(string animal)
        {
            AnimatorController c = NewController("Animal_" + animal);
            AnimatorState idle = c.layers[0].stateMachine.AddState("Idle");
            idle.motion = Clip(ArtImportSettings.AnimalsFolder + animal + ".fbx", "Idle");
            c.layers[0].stateMachine.defaultState = idle;
            return c;
        }

        // ───────────────────────────── Nature ─────────────────────────────

        static string N(string model) => NatureModels + model + ".fbx";

        static string Pick(Random rng, params string[] options) => options[rng.Next(options.Length)];

        static float Range(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>
        /// Scatters trees, rocks and undergrowth from the Quaternius Stylized Nature MegaKit.
        /// <paramref name="isClear"/>(position, margin) rejects paths, plaza, buildings and landmarks.
        /// <paramref name="pathDistance"/>(position) returns distance to the trail centre line.
        /// </summary>
        public static void BuildNature(Transform env, Func<Vector3, float, bool> isClear, Func<Vector3, float> pathDistance)
        {
            var rng = new Random(20261011);
            Transform trees = Group("Trees", env);
            Transform rocks = Group("Rocks", env);
            Transform plants = Group("Undergrowth", env);
            Transform pebbles = Group("Pebbles", env);
            var treeSpots = new List<Vector3>();

            bool FarFromTrees(Vector3 p, float min)
            {
                foreach (Vector3 t in treeSpots)
                    if ((t - p).sqrMagnitude < min * min) return false;
                return true;
            }

            void Tree(string model, Vector3 p, float height)
            {
                Placed? placed = Place(N(model), trees, p, Range(rng, 0f, 360f), height, sink: 0.02f);
                if (placed == null) return;
                // Trunk collider sized in world units, expressed in the holder's scaled space.
                float s = placed.Value.Root.transform.localScale.x;
                CapsuleCollider trunk = placed.Value.Root.AddComponent<CapsuleCollider>();
                trunk.radius = 0.35f / s;
                trunk.height = 4f / s;
                trunk.center = new Vector3(0f, 2f / s, 0f);
                treeSpots.Add(p);
            }

            // Village ring: broadleaf trees framing the plaza.
            for (int i = 0, placedCount = 0; i < 400 && placedCount < 26; i++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                float radius = Range(rng, 20f, 42f);
                var p = new Vector3(Mathf.Cos(angle) * radius, 0f, 4f + Mathf.Sin(angle) * radius);
                if (p.z > 36f || !isClear(p, 3f) || !FarFromTrees(p, 5f)) continue;
                Tree(Pick(rng, "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5"), p, Range(rng, 6f, 8.5f));
                placedCount++;
            }

            // Whispering Forest (north): twisted and broadleaf trees, denser.
            for (int i = 0, placedCount = 0; i < 600 && placedCount < 34; i++)
            {
                var p = new Vector3(Range(rng, -66f, 66f), 0f, Range(rng, 38f, 78f));
                if (!isClear(p, 2.5f) || !FarFromTrees(p, 4.5f)) continue;
                if (rng.NextDouble() < 0.6)
                    Tree(Pick(rng, "TwistedTree_1", "TwistedTree_2", "TwistedTree_3", "TwistedTree_4", "TwistedTree_5"), p, Range(rng, 5.5f, 8f));
                else
                    Tree(Pick(rng, "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5"), p, Range(rng, 6.5f, 9f));
                placedCount++;
            }

            // Pines along the outer edges, hinting at Cloudpeak.
            for (int i = 0, placedCount = 0; i < 500 && placedCount < 22; i++)
            {
                var p = new Vector3(Range(rng, -68f, 68f), 0f, Range(rng, -55f, 79f));
                bool edge = Mathf.Abs(p.x) > 48f || p.z > 66f || p.z < -40f;
                if (!edge || !isClear(p, 2.5f) || !FarFromTrees(p, 5f)) continue;
                Tree(Pick(rng, "Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5"), p, Range(rng, 8f, 12f));
                placedCount++;
            }

            // A few dead trees deep in the forest for mood.
            for (int i = 0, placedCount = 0; i < 200 && placedCount < 5; i++)
            {
                var p = new Vector3(Range(rng, -60f, 60f), 0f, Range(rng, 50f, 78f));
                if (!isClear(p, 2.5f) || !FarFromTrees(p, 5f)) continue;
                Tree(Pick(rng, "DeadTree_1", "DeadTree_2", "DeadTree_3", "DeadTree_4", "DeadTree_5"), p, Range(rng, 5f, 7f));
                placedCount++;
            }

            // Boulders (solid).
            for (int i = 0, placedCount = 0; i < 300 && placedCount < 18; i++)
            {
                var p = new Vector3(Range(rng, -62f, 62f), 0f, Range(rng, -48f, 74f));
                if (!isClear(p, 2.5f) || !FarFromTrees(p, 3f)) continue;
                Placed? rock = Place(N(Pick(rng, "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3")), rocks, p,
                    Range(rng, 0f, 360f), Range(rng, 1.2f, 2.6f), sink: 0.15f);
                if (rock == null) continue;
                foreach (MeshFilter mf in rock.Value.Root.GetComponentsInChildren<MeshFilter>())
                {
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
                placedCount++;
            }

            // Undergrowth: clustered so it reads as natural patches. No colliders, no shadows.
            void Cluster(int clusters, int perCluster, float spread, Func<Vector3, bool> zone, Func<string> model,
                float minSize, float maxSize, Fit fit = Fit.Height, float margin = 0.8f)
            {
                for (int c = 0, made = 0; c < clusters * 20 && made < clusters; c++)
                {
                    var centre = new Vector3(Range(rng, -64f, 64f), 0f, Range(rng, -52f, 77f));
                    if (!zone(centre) || !isClear(centre, margin)) continue;
                    made++;
                    for (int k = 0; k < perCluster; k++)
                    {
                        Vector3 p = centre + new Vector3(Range(rng, -spread, spread), 0f, Range(rng, -spread, spread));
                        if (!isClear(p, margin)) continue;
                        Placed? plant = Place(N(model()), plants, p, Range(rng, 0f, 360f), Range(rng, minSize, maxSize), fit);
                        if (plant != null) ShadowsOff(plant.Value.Root);
                    }
                }
            }

            bool Anywhere(Vector3 p) => true;
            bool Forest(Vector3 p) => p.z > 32f;
            bool Meadow(Vector3 p) => p.z <= 40f;

            Cluster(45, 5, 2.5f, Anywhere, () => Pick(rng, "Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short", "Grass_Wispy_Tall"), 0.35f, 0.75f);
            Cluster(18, 4, 2f, Meadow, () => Pick(rng, "Flower_3_Group", "Flower_4_Group", "Flower_3_Single", "Flower_4_Single"), 0.35f, 0.6f);
            Cluster(10, 3, 1.5f, Anywhere, () => Pick(rng, "Clover_1", "Clover_2"), 0.15f, 0.25f);
            Cluster(14, 2, 2f, Anywhere, () => Pick(rng, "Bush_Common", "Bush_Common_Flowers"), 1f, 1.6f, margin: 1.5f);
            Cluster(12, 3, 2.5f, Forest, () => Pick(rng, "Fern_1", "Plant_1", "Plant_7"), 0.6f, 1.1f);
            Cluster(6, 2, 2f, Forest, () => Pick(rng, "Plant_1_Big", "Plant_7_Big"), 1f, 1.6f, margin: 1.2f);
            Cluster(8, 3, 1.2f, Forest, () => Pick(rng, "Mushroom_Common", "Mushroom_Laetiporus"), 0.2f, 0.45f);

            // Pebbles lining the trail edges.
            for (int i = 0, made = 0; i < 1500 && made < 45; i++)
            {
                var p = new Vector3(Range(rng, -10f, 14f), 0f, Range(rng, -16f, 46f));
                float d = pathDistance(p);
                if (d < 1.5f || d > 2.6f || !isClear(p, 0f)) continue;
                Placed? pebble = Place(N(Pick(rng, "Pebble_Round_1", "Pebble_Round_2", "Pebble_Round_3", "Pebble_Round_4", "Pebble_Round_5",
                        "Pebble_Square_1", "Pebble_Square_2", "Pebble_Square_3")), pebbles, p, Range(rng, 0f, 360f),
                    Range(rng, 0.2f, 0.45f), Fit.Width, sink: 0.3f);
                if (pebble != null) ShadowsOff(pebble.Value.Root);
                made++;
            }
        }

        /// <summary>Flat stepping stones between two points (no colliders; they sit flush with the ground).</summary>
        public static void SteppingStones(Transform parent, Vector3 from, Vector3 to, int count, int seed)
        {
            var rng = new Random(seed);
            Transform group = Group("SteppingStones", parent);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, (i + 0.5f) / count) + new Vector3(Range(rng, -0.3f, 0.3f), 0f, Range(rng, -0.3f, 0.3f));
                Placed? stone = Place(N(Pick(rng, "RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3",
                    "RockPath_Round_Wide", "RockPath_Square_Small_1")), group, p, Range(rng, 0f, 360f), Range(rng, 0.8f, 1.1f), Fit.Width, sink: 0.5f);
                if (stone != null) ShadowsOff(stone.Value.Root);
            }
        }

        /// <summary>A feature tree (e.g. the Great Old Oak landmark) with a trunk collider.</summary>
        public static bool LandmarkTree(Transform parent, Vector3 position, float height)
        {
            Placed? tree = Place(N("TwistedTree_3"), parent, position, 30f, height, sink: 0.02f);
            if (tree == null) return false;
            float s = tree.Value.Root.transform.localScale.x;
            CapsuleCollider trunk = tree.Value.Root.AddComponent<CapsuleCollider>();
            trunk.radius = 1.1f / s;
            trunk.height = 6f / s;
            trunk.center = new Vector3(0f, 3f / s, 0f);
            return true;
        }

        public static void FlowerBed(Transform parent, Vector3 centre, int seed)
        {
            var rng = new Random(seed);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = centre + new Vector3(Range(rng, -1f, 1f), 0f, Range(rng, -0.8f, 0.8f));
                Placed? f = Place(N(Pick(rng, "Flower_3_Group", "Flower_4_Group", "Bush_Common_Flowers")), parent, p,
                    Range(rng, 0f, 360f), Range(rng, 0.45f, 0.8f));
                if (f != null) ShadowsOff(f.Value.Root);
            }
        }

        // ───────────────────────────── Animals ─────────────────────────────

        /// <summary>Places an idling farm animal with a box collider so the player bumps into it.</summary>
        public static void Animal(Transform parent, string animal, Vector3 position, float yaw, float height)
        {
            Placed? placed = Place(ArtImportSettings.AnimalsFolder + animal + ".fbx", parent, position, yaw, height, isStatic: false);
            if (placed == null) return;
            GameObject root = placed.Value.Root;
            root.name = animal;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = placed.Value.LocalBounds.center;
            box.size = placed.Value.LocalBounds.size * 0.85f;

            Animator animator = root.GetComponentInChildren<Animator>();
            if (animator == null) animator = root.transform.GetChild(0).gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AnimalController(animal);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely; // off-screen animals cost nothing
            animator.gameObject.AddComponent<AnimatorRandomStart>();
        }

        // ───────────────────────────── Utilities ─────────────────────────────

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
