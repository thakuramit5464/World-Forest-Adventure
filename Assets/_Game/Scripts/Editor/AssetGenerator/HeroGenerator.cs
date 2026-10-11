using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Procedural hero: a stylised young adventurer (big friendly head, tunic, belt, trousers, boots,
    /// scarf, backpack with bedroll) built from lofted, faceted body parts that are smoothly skinned to
    /// a skeleton using Unity Humanoid bone names, in T-pose facing +Z. A Humanoid Avatar is generated
    /// with <see cref="AvatarBuilder"/>, so the project's existing humanoid animator (Universal Animation
    /// Library clips) drives it — no procedural animation is needed.
    ///
    /// Limits (by design for a code-built model): mitten hands with no finger bones, rigid head and
    /// feet, simple joint blending; clothing can intersect at extreme poses.
    /// </summary>
    public static class HeroGenerator
    {
        public const float Height = 1.74f;

        struct BoneDef
        {
            public string Name, Parent;
            public Vector3 Local;

            public BoneDef(string name, string parent, Vector3 local)
            {
                Name = name;
                Parent = parent;
                Local = local;
            }
        }

        // T-pose skeleton (metres). Names match HumanTrait.BoneName so the avatar maps 1:1.
        static readonly BoneDef[] Bones =
        {
            new BoneDef("Hips", null, new Vector3(0f, 0.86f, 0f)),
            new BoneDef("Spine", "Hips", new Vector3(0f, 0.10f, 0f)),
            new BoneDef("Chest", "Spine", new Vector3(0f, 0.14f, 0f)),
            new BoneDef("Neck", "Chest", new Vector3(0f, 0.17f, 0f)),
            new BoneDef("Head", "Neck", new Vector3(0f, 0.09f, 0f)),
            new BoneDef("LeftShoulder", "Chest", new Vector3(-0.05f, 0.12f, 0f)),
            new BoneDef("LeftUpperArm", "LeftShoulder", new Vector3(-0.12f, 0f, 0f)),
            new BoneDef("LeftLowerArm", "LeftUpperArm", new Vector3(-0.24f, 0f, 0f)),
            new BoneDef("LeftHand", "LeftLowerArm", new Vector3(-0.215f, 0f, 0f)),
            new BoneDef("RightShoulder", "Chest", new Vector3(0.05f, 0.12f, 0f)),
            new BoneDef("RightUpperArm", "RightShoulder", new Vector3(0.12f, 0f, 0f)),
            new BoneDef("RightLowerArm", "RightUpperArm", new Vector3(0.24f, 0f, 0f)),
            new BoneDef("RightHand", "RightLowerArm", new Vector3(0.215f, 0f, 0f)),
            new BoneDef("LeftUpperLeg", "Hips", new Vector3(-0.095f, -0.05f, 0f)),
            new BoneDef("LeftLowerLeg", "LeftUpperLeg", new Vector3(0f, -0.36f, 0f)),
            new BoneDef("LeftFoot", "LeftLowerLeg", new Vector3(0f, -0.36f, 0f)),
            new BoneDef("LeftToes", "LeftFoot", new Vector3(0f, -0.06f, 0.11f)),
            new BoneDef("RightUpperLeg", "Hips", new Vector3(0.095f, -0.05f, 0f)),
            new BoneDef("RightLowerLeg", "RightUpperLeg", new Vector3(0f, -0.36f, 0f)),
            new BoneDef("RightFoot", "RightLowerLeg", new Vector3(0f, -0.36f, 0f)),
            new BoneDef("RightToes", "RightFoot", new Vector3(0f, -0.06f, 0.11f)),
        };

        public sealed class Result
        {
            public GameObject Root;
            public Mesh Mesh;
            public Avatar Avatar;
            public SkinnedMeshRenderer Renderer;
        }

        public static string AssetName(int seed) => $"Gen_Hero_s{seed}";
        static string AvatarPath(string name) => $"{GeneratedAssetStore.MeshFolder}/{name}_Avatar.asset";

        // ───────────────────────────── Build ─────────────────────────────

        /// <summary>Builds the hero hierarchy, skinned mesh and avatar in memory (nothing saved).</summary>
        public static Result BuildInMemory(int seed, string name)
        {
            var rng = new System.Random(unchecked(seed * 7919 + 17));
            var root = new GameObject(name);
            var armature = new GameObject("Armature").transform;
            armature.SetParent(root.transform, false);

            var byName = new Dictionary<string, Transform>();
            var bones = new Transform[Bones.Length];
            for (int i = 0; i < Bones.Length; i++)
            {
                BoneDef def = Bones[i];
                var t = new GameObject(def.Name).transform;
                t.SetParent(def.Parent == null ? armature : byName[def.Parent], false);
                t.localPosition = def.Local;
                byName[def.Name] = t;
                bones[i] = t;
            }
            int B(string boneName) => Array.FindIndex(Bones, b => b.Name == boneName);

            // A little seeded variety, kept inside a friendly, coherent range.
            var look = new Look
            {
                Skin = Mathf.Clamp(Palette.PickShade(rng, 4, 0.8f), 2, 6),
                Hair = Mathf.Clamp(Palette.PickShade(rng, 3, 1f), 1, 6),
                Tunic = Mathf.Clamp(Palette.PickShade(rng, 3, 0.6f), 2, 5),
                Trousers = Mathf.Clamp(Palette.PickShade(rng, 3, 0.6f), 2, 5),
                Leather = Mathf.Clamp(Palette.PickShade(rng, 3, 0.6f), 2, 4),
            };

            var mb = new MeshBuilder();
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0f ? "Left" : "Right";
                Leg(mb, side, look, B("Hips"), B(s + "UpperLeg"), B(s + "LowerLeg"), B(s + "Foot"), B(s + "Toes"));
                Arm(mb, rng, side, look, B(s + "Shoulder"), B(s + "UpperArm"), B(s + "LowerArm"), B(s + "Hand"));
            }
            Torso(mb, rng, look, B("Hips"), B("Spine"), B("Chest"), B("Neck"), B("Head"));
            Head(mb, rng, look, B("Head"));
            Backpack(mb, look, B("Chest"));

            Mesh mesh = mb.ToMesh(name);
            Matrix4x4 rootMatrix = root.transform.localToWorldMatrix;
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * rootMatrix).ToArray();
            mesh.RecalculateBounds();

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var smr = body.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = byName["Hips"];
            // Generous bounds (relative to the hips) so animated limbs are never culled early.
            smr.localBounds = new Bounds(Vector3.zero, new Vector3(2f, 2.4f, 1.6f));

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, Description(root));
            avatar.name = name + "_Avatar";
            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;

            return new Result { Root = root, Mesh = mesh, Avatar = avatar, Renderer = smr };
        }

        struct Look
        {
            public int Skin, Hair, Tunic, Trousers, Leather;
        }

        static HumanDescription Description(GameObject root)
        {
            var names = new HashSet<string>(Bones.Select(b => b.Name));
            HumanBone[] human = HumanTrait.BoneName
                .Where(names.Contains)
                .Select(n => new HumanBone { humanName = n, boneName = n, limit = new HumanLimit { useDefaultValues = true } })
                .ToArray();
            SkeletonBone[] skeleton = root.GetComponentsInChildren<Transform>(true)
                .Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale })
                .ToArray();
            return new HumanDescription
            {
                human = human,
                skeleton = skeleton,
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };
        }

        // ───────────────────────────── Skinning helpers ─────────────────────────────

        static BoneWeight Rigid(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        static BoneWeight Blend(int a, int b, float towardsB)
        {
            if (towardsB <= 0.001f) return Rigid(a);
            if (towardsB >= 0.999f) return Rigid(b);
            return new BoneWeight { boneIndex0 = a, weight0 = 1f - towardsB, boneIndex1 = b, weight1 = towardsB };
        }

        /// <summary>
        /// Weights along a limb: <paramref name="bones"/>[i] owns the region before joints[i]; vertices
        /// within <paramref name="blend"/> of a joint are shared linearly between the two neighbours.
        /// </summary>
        static Func<Vector3, BoneWeight> Chain(Func<Vector3, float> coord, float[] joints, int[] bones, float blend) => p =>
        {
            float s = coord(p);
            for (int j = 0; j < joints.Length; j++)
            {
                if (s < joints[j] - blend) return Rigid(bones[j]);
                if (s <= joints[j] + blend) return Blend(bones[j], bones[j + 1], (s - (joints[j] - blend)) / (2f * blend));
            }
            return Rigid(bones[bones.Length - 1]);
        };

        static Func<Vector3, Vector2> V(VRamp ramp, int shade) => GenUtil.Village(ramp, shade);
        static Func<Vector3, Vector2> Flat(VRamp ramp, int shade) => GenUtil.VillageFlat(ramp, shade);

        // ───────────────────────────── Body parts ─────────────────────────────

        static void Leg(MeshBuilder mb, float side, Look look, int hips, int upper, int lower, int foot, int toes)
        {
            float x = side * 0.095f;
            mb.Skin = Chain(p => -p.y, new[] { -0.84f, -0.45f, -0.1f }, new[] { hips, upper, lower, foot }, 0.05f);
            var cloth = V(VRamp.Trousers, look.Trousers);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(x, 0.92f, 0f), 0.095f, 0.09f, cloth),
                new Shapes.Ring(new Vector3(x, 0.80f, 0f), 0.088f, 0.085f, cloth),
                new Shapes.Ring(new Vector3(x, 0.62f, 0f), 0.078f, 0.075f, cloth),
                new Shapes.Ring(new Vector3(x, 0.47f, 0.005f), 0.068f, 0.068f, cloth),
                new Shapes.Ring(new Vector3(x, 0.32f, 0f), 0.062f, 0.06f, cloth),
                new Shapes.Ring(new Vector3(x, 0.2f, 0f), 0.058f, 0.056f, cloth),
            }, 8, false, false);

            // Boot shaft overlapping the trouser cuff.
            var boot = V(VRamp.Leather, look.Leather);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(x, 0.27f, 0f), 0.077f, 0.077f, V(VRamp.Leather, look.Leather + 1)),
                new Shapes.Ring(new Vector3(x, 0.22f, 0f), 0.075f, 0.075f, boot),
                new Shapes.Ring(new Vector3(x, 0.09f, 0.01f), 0.072f, 0.078f, boot),
            }, 8, false, false);

            // Foot and sole: back half follows the foot, toe cap follows the toes.
            mb.Skin = p => p.z > 0.11f ? Rigid(toes) : Rigid(foot);
            Shapes.Box(mb, new Vector3(x, 0.06f, 0.04f), new Vector3(0.13f, 0.1f, 0.27f), V(VRamp.Leather, look.Leather));
            Shapes.Box(mb, new Vector3(x, 0.012f, 0.04f), new Vector3(0.14f, 0.024f, 0.285f), Flat(VRamp.Dark, 2));
            mb.Skin = null;
        }

        static void Arm(MeshBuilder mb, System.Random rng, float side, Look look, int shoulder, int upper, int lower, int hand)
        {
            const float y = 1.22f;
            Vector3 At(float s, float dz = 0f) => new Vector3(side * s, y, dz);
            mb.Skin = Chain(p => side * p.x, new[] { 0.13f, 0.41f, 0.625f }, new[] { shoulder, upper, lower, hand }, 0.045f);
            var sleeve = V(VRamp.Tunic, look.Tunic);
            var skin = V(VRamp.Skin, look.Skin);
            Shapes.Loft(mb, Vector3.up, Vector3.forward, new[]
            {
                new Shapes.Ring(At(0.09f), 0.07f, 0.068f, sleeve),
                new Shapes.Ring(At(0.18f), 0.062f, 0.06f, sleeve),
                new Shapes.Ring(At(0.3f), 0.055f, 0.053f, sleeve),
                new Shapes.Ring(At(0.355f), 0.054f, 0.052f, V(VRamp.Leather, look.Leather + 1)), // cuff
                new Shapes.Ring(At(0.385f), 0.049f, 0.047f, skin),
                new Shapes.Ring(At(0.5f), 0.045f, 0.043f, skin),
                new Shapes.Ring(At(0.62f), 0.04f, 0.038f, skin),
            }, 8, false, false);

            // Mitten hand and thumb.
            mb.Skin = _ => Rigid(hand);
            Shapes.Blob(mb, rng, At(0.69f, 0.005f) + Vector3.down * 0.005f, 1f, new Vector3(0.065f, 0.055f, 0.05f), 0.04f, 1, skin);
            Shapes.Blob(mb, rng, At(0.665f, 0.045f) + Vector3.down * 0.01f, 1f, new Vector3(0.03f, 0.025f, 0.025f), 0.04f, 0, skin);
            mb.Skin = null;
        }

        static void Torso(MeshBuilder mb, System.Random rng, Look look, int hips, int spine, int chest, int neck, int head)
        {
            mb.Skin = Chain(p => p.y, new[] { 0.93f, 1.06f }, new[] { hips, spine, chest }, 0.05f);
            var tunic = V(VRamp.Tunic, look.Tunic);
            var tunicLight = V(VRamp.Tunic, look.Tunic + 1);
            var belt = V(VRamp.Leather, look.Leather - 1);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 0.7f, 0f), 0.205f, 0.155f, tunic),       // hem, flared
                new Shapes.Ring(new Vector3(0f, 0.78f, 0f), 0.185f, 0.14f, tunic),
                new Shapes.Ring(new Vector3(0f, 0.85f, 0f), 0.175f, 0.13f, belt),        // belt band
                new Shapes.Ring(new Vector3(0f, 0.91f, 0f), 0.172f, 0.128f, tunic),
                new Shapes.Ring(new Vector3(0f, 1.0f, 0f), 0.175f, 0.13f, tunicLight),
                new Shapes.Ring(new Vector3(0f, 1.12f, 0.005f), 0.19f, 0.135f, tunicLight),
                new Shapes.Ring(new Vector3(0f, 1.21f, 0f), 0.18f, 0.125f, tunic),
                new Shapes.Ring(new Vector3(0f, 1.26f, 0f), 0.13f, 0.1f, tunic),
                new Shapes.Ring(new Vector3(0f, 1.29f, 0f), 0.07f, 0.07f, tunic),        // collar
            }, 10, true, false);

            mb.Skin = _ => Rigid(hips);
            Shapes.Box(mb, new Vector3(0f, 0.88f, 0.13f), new Vector3(0.07f, 0.05f, 0.02f), Flat(VRamp.Window, 5));      // buckle
            Shapes.Box(mb, new Vector3(0.15f, 0.84f, 0.085f), new Vector3(0.07f, 0.08f, 0.05f), V(VRamp.Leather, look.Leather)); // pouch

            // Neck and the signature red scarf.
            mb.Skin = Chain(p => p.y, new[] { 1.27f, 1.37f }, new[] { chest, neck, head }, 0.03f);
            var skin = V(VRamp.Skin, look.Skin);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 1.25f, 0f), 0.055f, 0.055f, skin),
                new Shapes.Ring(new Vector3(0f, 1.41f, 0f), 0.052f, 0.052f, skin),
            }, 8, false, false);
            mb.Skin = _ => Rigid(chest);
            var scarf = V(VRamp.Accent, 4);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 1.235f, 0.005f), 0.1f, 0.09f, scarf),
                new Shapes.Ring(new Vector3(0f, 1.3f, 0.005f), 0.085f, 0.078f, scarf),
            }, 10, true, true);
            Shapes.Box(mb, new Vector3(0.05f, 1.17f, -0.12f), new Vector3(0.08f, 0.16f, 0.03f), Quaternion.Euler(10f, 0f, -12f), V(VRamp.Accent, 3));
            mb.Skin = null;
        }

        static void Head(MeshBuilder mb, System.Random rng, Look look, int head)
        {
            mb.Skin = _ => Rigid(head);
            var skin = V(VRamp.Skin, look.Skin);
            var centre = new Vector3(0f, 1.51f, 0.01f);
            Shapes.Blob(mb, rng, centre, 1f, new Vector3(0.185f, 0.2f, 0.18f), 0.02f, 1, skin);

            // Hair cap: a slightly larger blob trimmed to a hairline that is high at the front, low at the back.
            var hairCentre = new Vector3(0f, 1.535f, -0.012f);
            Shapes.Blob(mb, rng, hairCentre, 1f, new Vector3(0.198f, 0.205f, 0.195f), 0.05f, 1, V(VRamp.Hair, look.Hair), p =>
            {
                float front = Mathf.Clamp01((p.z + 0.02f) / 0.16f);
                float hairline = Mathf.Lerp(-0.13f, 0.035f, front);
                return p.y < hairline ? new Vector3(p.x, hairline, p.z) : p;
            });
            // Fringe tufts for a readable silhouette.
            for (int i = -1; i <= 1; i++)
                Shapes.Box(mb, new Vector3(i * 0.065f, 1.6f, 0.16f), new Vector3(0.075f, 0.05f, 0.05f), Quaternion.Euler(25f, i * 15f, i * -10f),
                    Flat(VRamp.Hair, look.Hair + 1));

            // Face: eyes with highlights, brows, nose, mouth, ears.
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Box(mb, new Vector3(s * 0.062f, 1.515f, 0.175f), new Vector3(0.04f, 0.05f, 0.02f), Flat(VRamp.Dark, 0));
                Shapes.Box(mb, new Vector3(s * 0.055f, 1.53f, 0.186f), new Vector3(0.012f, 0.012f, 0.006f), Flat(VRamp.Plaster, 7));
                Shapes.Box(mb, new Vector3(s * 0.062f, 1.575f, 0.172f), new Vector3(0.055f, 0.014f, 0.015f), Quaternion.Euler(0f, 0f, s * -8f),
                    Flat(VRamp.Hair, look.Hair - 1));
                Shapes.Blob(mb, rng, new Vector3(s * 0.188f, 1.5f, 0f), 1f, new Vector3(0.025f, 0.045f, 0.035f), 0.05f, 0, skin);
            }
            Shapes.Box(mb, new Vector3(0f, 1.485f, 0.185f), new Vector3(0.03f, 0.035f, 0.03f), skin);
            Shapes.Box(mb, new Vector3(0f, 1.44f, 0.172f), new Vector3(0.05f, 0.012f, 0.01f), Flat(VRamp.Accent, 1));
            mb.Skin = null;
        }

        static void Backpack(MeshBuilder mb, Look look, int chest)
        {
            mb.Skin = _ => Rigid(chest);
            var pack = V(VRamp.Leather, look.Leather + 1);
            Shapes.Box(mb, new Vector3(0f, 1.07f, -0.205f), new Vector3(0.27f, 0.32f, 0.13f), pack);
            Shapes.Box(mb, new Vector3(0f, 1.215f, -0.2f), new Vector3(0.285f, 0.06f, 0.145f), V(VRamp.Leather, look.Leather - 1));
            Shapes.Box(mb, new Vector3(0f, 1.0f, -0.28f), new Vector3(0.18f, 0.13f, 0.04f), V(VRamp.Leather, look.Leather));
            Shapes.Loft(mb, Vector3.up, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(-0.16f, 1.275f, -0.19f), 0.055f, 0.055f, V(VRamp.Accent, 2)),
                new Shapes.Ring(new Vector3(0.16f, 1.275f, -0.19f), 0.055f, 0.055f, V(VRamp.Accent, 2)),
            }, 8, true, true);
            var strap = Flat(VRamp.Leather, look.Leather - 1);
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Box(mb, new Vector3(s * 0.09f, 1.275f, -0.19f), new Vector3(0.02f, 0.125f, 0.125f), strap);   // bedroll strap
                Shapes.Box(mb, new Vector3(s * 0.085f, 1.13f, 0.125f), new Vector3(0.035f, 0.24f, 0.02f), strap);   // front strap
                Shapes.Box(mb, new Vector3(s * 0.085f, 1.245f, -0.03f), new Vector3(0.035f, 0.025f, 0.27f), strap); // over the shoulder
            }
            mb.Skin = null;
        }

        // ───────────────────────────── Save / find / validate ─────────────────────────────

        /// <summary>Generates and saves the hero mesh, avatar and prefab for <paramref name="seed"/>.</summary>
        public static string Generate(int seed)
        {
            string name = AssetName(seed);
            string meshPath = GeneratedAssetStore.MeshPath(name);
            string prefabPath = GeneratedAssetStore.PrefabPath(name);
            string avatarPath = AvatarPath(name);

            var existing = new List<string>();
            foreach (string path in new[] { meshPath, prefabPath, avatarPath })
            {
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                if (!GeneratedAssetStore.IsGenerated(asset))
                {
                    EditorUtility.DisplayDialog("Hero generation stopped", $"{path} exists but was not created by the generator. Nothing was changed.", "OK");
                    return $"Stopped: {path} is not a generated asset.";
                }
                existing.Add(path);
            }
            if (existing.Count > 0 && !EditorUtility.DisplayDialog("Replace hero?",
                    $"The hero for seed {seed} already exists and will be replaced (scenes using it will update):\n\n{string.Join("\n", existing)}",
                    "Replace", "Cancel"))
                return "Cancelled. Hero unchanged.";

            GeneratedAssetStore.EnsureFolders();
            Result r = BuildInMemory(seed, name);
            try
            {
                if (!r.Avatar.isValid || !r.Avatar.isHuman)
                {
                    Object.DestroyImmediate(r.Mesh);
                    Object.DestroyImmediate(r.Avatar);
                    return "Hero avatar is not a valid Humanoid; nothing was saved. See the Console for AvatarBuilder messages.";
                }

                Material material = GeneratedAssetStore.EnsurePaletteMaterial(VillagePalette.Palette);
                Mesh meshAsset = GeneratedAssetStore.SaveMesh(r.Mesh, meshPath);
                Avatar avatarAsset = SaveAvatar(r.Avatar, avatarPath);

                r.Renderer.sharedMesh = meshAsset;
                r.Renderer.sharedMaterial = material;
                r.Root.GetComponent<Animator>().avatar = avatarAsset;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(r.Root, prefabPath, out bool ok);
                if (!ok || prefab == null) return $"Failed to save {prefabPath}.";
                AssetDatabase.SetLabels(prefab, new[] { GeneratedAssetStore.Label });
                AssetDatabase.SaveAssets();

                var problems = new List<string>();
                ValidatePrefab(prefab, problems);
                return problems.Count == 0
                    ? $"Hero saved: {prefabPath} ({meshAsset.triangles.Length / 3} triangles, {Bones.Length} bones, valid Humanoid avatar)."
                    : $"Hero saved with {problems.Count} problem(s):\n • " + string.Join("\n • ", problems);
            }
            finally
            {
                Object.DestroyImmediate(r.Root);
            }
        }

        static Avatar SaveAvatar(Avatar fresh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            Avatar asset;
            if (existing != null)
            {
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(existing);
                asset = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(fresh, path);
                asset = fresh;
            }
            AssetDatabase.SetLabels(asset, new[] { GeneratedAssetStore.Label });
            return asset;
        }

        /// <summary>The hero prefab for <paramref name="seed"/>, or any generated hero, or null.</summary>
        public static GameObject FindPrefab(int seed)
        {
            var exact = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(AssetName(seed)));
            if (exact != null) return exact;
            foreach (string guid in AssetDatabase.FindAssets("Gen_Hero_s t:Prefab", new[] { GeneratedAssetStore.PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (GeneratedAssetStore.IsGenerated(prefab)) return prefab;
            }
            return null;
        }

        public static void ValidatePrefab(GameObject prefab, List<string> problems)
        {
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = prefab.GetComponent<Animator>();
            Mesh mesh = smr != null ? smr.sharedMesh : null;
            if (smr == null || mesh == null || !AssetDatabase.Contains(mesh))
            {
                problems.Add($"{prefab.name}: skinned mesh missing or not a saved asset.");
                return;
            }
            MeshValidator.Validate(mesh, problems);
            if (smr.sharedMaterial == null || !AssetDatabase.Contains(smr.sharedMaterial)) problems.Add($"{prefab.name}: material missing or not a saved asset.");
            if (mesh.boneWeights.Length != mesh.vertexCount) problems.Add($"{prefab.name}: {mesh.boneWeights.Length} bone weights for {mesh.vertexCount} vertices.");
            if (smr.bones.Length != mesh.bindposes.Length || smr.bones.Any(b => b == null)) problems.Add($"{prefab.name}: bones and bind poses do not match.");
            if (animator == null || animator.avatar == null || !AssetDatabase.Contains(animator.avatar)) problems.Add($"{prefab.name}: avatar missing or not a saved asset.");
            else if (!animator.avatar.isValid || !animator.avatar.isHuman) problems.Add($"{prefab.name}: avatar is not a valid Humanoid.");
        }
    }
}
