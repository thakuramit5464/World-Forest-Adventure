using System;
using System.Collections.Generic;
using System.Linq;
using Greenhaven.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Procedural hero (version 2): a storybook young adventurer — big friendly head with layered hair
    /// and an expressive face, teal tunic with trim over cream sleeves, leather gloves, belt, trousers,
    /// detailed boots, and a rounded backpack. Faceted, lofted parts are smoothly skinned to a skeleton
    /// using Unity Humanoid bone names (plus optional eye bones and a non-human backpack bone), in T-pose
    /// facing +Z. <see cref="AvatarBuilder"/> generates a Humanoid Avatar, so the existing humanoid
    /// animator (Universal Animation Library clips) drives the body; <see cref="HeroSecondaryMotion"/>
    /// adds blinking, look-at and backpack sway on top.
    ///
    /// Saved as Gen_Hero2_s{seed}; version 1 assets (Gen_Hero_s{seed}) are left untouched as a backup.
    /// Limits of a code-built model: no finger bones (gloved mitten hands), rigid head and feet, linear
    /// joint blends; clothing can intersect in extreme poses.
    /// </summary>
    public static class HeroGenerator
    {
        public const float Height = 1.64f; // top of the cowlick; the head itself tops out near 1.62 m
        const string NamePrefix = "Gen_Hero2_s";
        const string LegacyPrefix = "Gen_Hero_s";

        internal struct BoneDef
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

        // T-pose skeleton (metres). Human bone names match HumanTrait.BoneName; "Backpack" is a non-human
        // extra driven by HeroSecondaryMotion.
        static readonly BoneDef[] Bones =
        {
            new BoneDef("Hips", null, new Vector3(0f, 0.78f, 0f)),
            new BoneDef("Spine", "Hips", new Vector3(0f, 0.09f, 0f)),
            new BoneDef("Chest", "Spine", new Vector3(0f, 0.13f, 0f)),
            new BoneDef("Neck", "Chest", new Vector3(0f, 0.15f, 0f)),
            new BoneDef("Head", "Neck", new Vector3(0f, 0.08f, 0f)),
            new BoneDef("LeftEye", "Head", new Vector3(-0.068f, 0.135f, 0.165f)),
            new BoneDef("RightEye", "Head", new Vector3(0.068f, 0.135f, 0.165f)),
            new BoneDef("LeftShoulder", "Chest", new Vector3(-0.05f, 0.11f, 0f)),
            new BoneDef("LeftUpperArm", "LeftShoulder", new Vector3(-0.1f, 0f, 0f)),
            new BoneDef("LeftLowerArm", "LeftUpperArm", new Vector3(-0.21f, 0f, 0f)),
            new BoneDef("LeftHand", "LeftLowerArm", new Vector3(-0.19f, 0f, 0f)),
            new BoneDef("RightShoulder", "Chest", new Vector3(0.05f, 0.11f, 0f)),
            new BoneDef("RightUpperArm", "RightShoulder", new Vector3(0.1f, 0f, 0f)),
            new BoneDef("RightLowerArm", "RightUpperArm", new Vector3(0.21f, 0f, 0f)),
            new BoneDef("RightHand", "RightLowerArm", new Vector3(0.19f, 0f, 0f)),
            new BoneDef("LeftUpperLeg", "Hips", new Vector3(-0.09f, -0.05f, 0f)),
            new BoneDef("LeftLowerLeg", "LeftUpperLeg", new Vector3(0f, -0.33f, 0f)),
            new BoneDef("LeftFoot", "LeftLowerLeg", new Vector3(0f, -0.32f, 0f)),
            new BoneDef("LeftToes", "LeftFoot", new Vector3(0f, -0.055f, 0.1f)),
            new BoneDef("RightUpperLeg", "Hips", new Vector3(0.09f, -0.05f, 0f)),
            new BoneDef("RightLowerLeg", "RightUpperLeg", new Vector3(0f, -0.33f, 0f)),
            new BoneDef("RightFoot", "RightLowerLeg", new Vector3(0f, -0.32f, 0f)),
            new BoneDef("RightToes", "RightFoot", new Vector3(0f, -0.055f, 0.1f)),
            new BoneDef("Backpack", "Chest", new Vector3(0f, 0.02f, -0.19f)),
        };

        public static int BoneCount => Bones.Length;

        public sealed class Result
        {
            public GameObject Root;
            public Mesh Mesh;
            public Avatar Avatar;
            public SkinnedMeshRenderer Renderer;
            public Transform LeftEye, RightEye, Backpack;
        }

        public static string AssetName(int seed) => NamePrefix + seed;
        static string AvatarPath(string name) => $"{GeneratedAssetStore.MeshFolder}/{name}_Avatar.asset";

        struct Look
        {
            public int Skin, Hair, Tunic, Trousers, Leather;
        }

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

            var look = new Look
            {
                Skin = Mathf.Clamp(Palette.PickShade(rng, 5, 0.8f), 3, 6),
                Hair = Mathf.Clamp(Palette.PickShade(rng, 3, 1f), 2, 5),
                Tunic = Mathf.Clamp(Palette.PickShade(rng, 3, 0.5f), 2, 4),
                Trousers = Mathf.Clamp(Palette.PickShade(rng, 3, 0.5f), 2, 4),
                Leather = Mathf.Clamp(Palette.PickShade(rng, 3, 0.5f), 2, 4),
            };

            var mb = new MeshBuilder();
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0f ? "Left" : "Right";
                Leg(mb, rng, side, look, B("Hips"), B(s + "UpperLeg"), B(s + "LowerLeg"), B(s + "Foot"), B(s + "Toes"));
                Arm(mb, rng, side, look, B(s + "Shoulder"), B(s + "UpperArm"), B(s + "LowerArm"), B(s + "Hand"));
            }
            Torso(mb, look, B("Hips"), B("Spine"), B("Chest"), B("Neck"), B("Head"));
            Head(mb, rng, look, B("Head"), B("LeftEye"), B("RightEye"));
            Backpack(mb, rng, look, B("Chest"), B("Backpack"));

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

            return new Result
            {
                Root = root, Mesh = mesh, Avatar = avatar, Renderer = smr,
                LeftEye = byName["LeftEye"], RightEye = byName["RightEye"], Backpack = byName["Backpack"],
            };
        }

        static HumanDescription Description(GameObject root) => Description(root, Bones.Select(b => b.Name));

        /// <summary>Humanoid description: every transform under root is the T-pose; listed names map 1:1 to human bones.</summary>
        internal static HumanDescription Description(GameObject root, IEnumerable<string> boneNames)
        {
            var names = new HashSet<string>(boneNames);
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

        internal static BoneWeight Rigid(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        internal static BoneWeight Blend(int a, int b, float towardsB)
        {
            if (towardsB <= 0.001f) return Rigid(a);
            if (towardsB >= 0.999f) return Rigid(b);
            return new BoneWeight { boneIndex0 = a, weight0 = 1f - towardsB, boneIndex1 = b, weight1 = towardsB };
        }

        /// <summary>
        /// Weights along a limb: <paramref name="bones"/>[i] owns the region before joints[i]; vertices
        /// within <paramref name="blend"/> of a joint are shared linearly between the two neighbours.
        /// </summary>
        internal static Func<Vector3, BoneWeight> Chain(Func<Vector3, float> coord, float[] joints, int[] bones, float blend) => p =>
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

        /// <summary>A small rotated, squashed blob: hair tufts, cheeks, toe caps.</summary>
        static void Tuft(MeshBuilder mb, System.Random rng, Vector3 centre, Vector3 scale, Quaternion rotation, Func<Vector3, Vector2> uv, int subdivisions = 0) =>
            Shapes.Blob(mb, rng, centre, 1f, scale, 0.06f, subdivisions, uv, p => rotation * p);

        // ───────────────────────────── Body parts ─────────────────────────────

        static void Leg(MeshBuilder mb, System.Random rng, float side, Look look, int hips, int upper, int lower, int foot, int toes)
        {
            float x = side * 0.09f;
            mb.Skin = Chain(p => -p.y, new[] { -0.76f, -0.4f, -0.085f }, new[] { hips, upper, lower, foot }, 0.045f);
            var cloth = V(VRamp.Trousers, look.Trousers);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(x, 0.8f, 0f), 0.088f, 0.085f, cloth),
                new Shapes.Ring(new Vector3(x, 0.7f, 0f), 0.084f, 0.081f, cloth),
                new Shapes.Ring(new Vector3(x, 0.55f, 0f), 0.074f, 0.072f, cloth),
                new Shapes.Ring(new Vector3(x, 0.41f, 0.006f), 0.068f, 0.068f, V(VRamp.Trousers, look.Trousers - 1)), // knee
                new Shapes.Ring(new Vector3(x, 0.3f, 0f), 0.064f, 0.062f, cloth),
                new Shapes.Ring(new Vector3(x, 0.21f, 0f), 0.06f, 0.058f, cloth),
            }, 8, false, false);

            // Boot shaft with a folded, lighter cuff.
            var boot = V(VRamp.Leather, look.Leather);
            var cuff = V(VRamp.Leather, look.Leather + 2);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(x, 0.275f, 0f), 0.079f, 0.078f, cuff),
                new Shapes.Ring(new Vector3(x, 0.235f, 0f), 0.077f, 0.076f, boot),
                new Shapes.Ring(new Vector3(x, 0.225f, 0f), 0.072f, 0.07f, boot),
                new Shapes.Ring(new Vector3(x, 0.12f, 0.005f), 0.068f, 0.072f, boot),
                new Shapes.Ring(new Vector3(x, 0.08f, 0.01f), 0.068f, 0.076f, boot),
            }, 8, false, false);
            // Ankle strap and buckle (follow the lower leg/foot blend).
            Shapes.Box(mb, new Vector3(x, 0.135f, 0f), new Vector3(0.152f, 0.025f, 0.152f), Flat(VRamp.Leather, look.Leather - 1));
            Shapes.Box(mb, new Vector3(x + side * 0.077f, 0.135f, 0.02f), new Vector3(0.01f, 0.03f, 0.03f), Flat(VRamp.Window, 5));

            // Foot: body, rounded toe cap, sole and heel. Toe cap follows the toes.
            mb.Skin = p => p.z > 0.1f ? Rigid(toes) : Rigid(foot);
            Shapes.Box(mb, new Vector3(x, 0.055f, 0.03f), new Vector3(0.115f, 0.09f, 0.22f), boot);
            Tuft(mb, rng, new Vector3(x, 0.05f, 0.135f), new Vector3(0.06f, 0.045f, 0.058f), Quaternion.identity, V(VRamp.Leather, look.Leather - 1));
            Shapes.Box(mb, new Vector3(x, 0.012f, 0.04f), new Vector3(0.125f, 0.024f, 0.26f), Flat(VRamp.Dark, 2));
            Shapes.Box(mb, new Vector3(x, 0.03f, -0.065f), new Vector3(0.115f, 0.05f, 0.065f), Flat(VRamp.Dark, 3));
            mb.Skin = null;
        }

        static void Arm(MeshBuilder mb, System.Random rng, float side, Look look, int shoulder, int upper, int lower, int hand)
        {
            const float y = 1.11f;
            Vector3 At(float s, float dz = 0f, float dy = 0f) => new Vector3(side * s, y + dy, dz);
            mb.Skin = Chain(p => side * p.x, new[] { 0.1f, 0.36f, 0.545f }, new[] { shoulder, upper, lower, hand }, 0.04f);
            var tunic = V(VRamp.Tunic, look.Tunic);
            var sleeve = V(VRamp.Plaster, 6);
            var glove = V(VRamp.Leather, look.Leather + 1);
            Shapes.Loft(mb, Vector3.up, Vector3.forward, new[]
            {
                new Shapes.Ring(At(0.07f), 0.062f, 0.06f, tunic),            // tunic shoulder cap
                new Shapes.Ring(At(0.15f), 0.058f, 0.056f, tunic),
                new Shapes.Ring(At(0.19f), 0.056f, 0.054f, sleeve),          // cream sleeve
                new Shapes.Ring(At(0.3f), 0.053f, 0.051f, sleeve),
                new Shapes.Ring(At(0.4f), 0.048f, 0.046f, V(VRamp.Plaster, 5)), // sleeve cuff
                new Shapes.Ring(At(0.43f), 0.05f, 0.048f, glove),            // glove cuff flare
                new Shapes.Ring(At(0.47f), 0.042f, 0.04f, glove),
                new Shapes.Ring(At(0.535f), 0.038f, 0.036f, glove),
            }, 8, false, false);

            // Gloved hand and thumb.
            mb.Skin = _ => Rigid(hand);
            Shapes.Blob(mb, rng, At(0.6f, 0.004f, -0.004f), 1f, new Vector3(0.058f, 0.05f, 0.048f), 0.04f, 1, glove);
            Tuft(mb, rng, At(0.575f, 0.042f, -0.01f), new Vector3(0.026f, 0.022f, 0.022f), Quaternion.identity, glove);
            mb.Skin = null;
        }

        static void Torso(MeshBuilder mb, Look look, int hips, int spine, int chest, int neck, int head)
        {
            mb.Skin = Chain(p => p.y, new[] { 0.84f, 0.95f }, new[] { hips, spine, chest }, 0.05f);
            var tunic = V(VRamp.Tunic, look.Tunic);
            var tunicLight = V(VRamp.Tunic, look.Tunic + 1);
            var collar = V(VRamp.Plaster, 6);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 0.62f, 0f), 0.198f, 0.152f, V(VRamp.Window, 3)), // gold hem trim
                new Shapes.Ring(new Vector3(0f, 0.655f, 0f), 0.19f, 0.146f, tunic),
                new Shapes.Ring(new Vector3(0f, 0.72f, 0f), 0.175f, 0.135f, tunic),
                new Shapes.Ring(new Vector3(0f, 0.77f, 0f), 0.167f, 0.127f, V(VRamp.Leather, look.Leather - 1)), // belt
                new Shapes.Ring(new Vector3(0f, 0.83f, 0f), 0.163f, 0.123f, tunic),
                new Shapes.Ring(new Vector3(0f, 0.92f, 0f), 0.168f, 0.126f, tunicLight),
                new Shapes.Ring(new Vector3(0f, 1.02f, 0.004f), 0.18f, 0.131f, tunicLight),
                new Shapes.Ring(new Vector3(0f, 1.09f, 0f), 0.17f, 0.12f, tunic),
                new Shapes.Ring(new Vector3(0f, 1.13f, 0f), 0.12f, 0.095f, collar),                 // cream collar
                new Shapes.Ring(new Vector3(0f, 1.16f, 0f), 0.065f, 0.065f, collar),
            }, 12, true, false);

            // Chest laces.
            mb.Skin = _ => Rigid(chest);
            for (int i = 0; i < 3; i++)
                Shapes.Box(mb, new Vector3(0f, 0.99f + i * 0.04f, 0.13f), new Vector3(0.05f, 0.01f, 0.01f), Quaternion.Euler(0f, 0f, i % 2 == 0 ? 20f : -20f),
                    Flat(VRamp.Plaster, 6));

            // Belt hardware and gear on the hips.
            mb.Skin = _ => Rigid(hips);
            Shapes.Box(mb, new Vector3(0f, 0.8f, 0.128f), new Vector3(0.06f, 0.045f, 0.02f), Flat(VRamp.Window, 6));
            Shapes.Box(mb, new Vector3(0f, 0.8f, 0.135f), new Vector3(0.03f, 0.02f, 0.012f), Flat(VRamp.Leather, 0));
            Shapes.Box(mb, new Vector3(0.15f, 0.76f, 0.07f), new Vector3(0.07f, 0.08f, 0.05f), V(VRamp.Leather, look.Leather + 1));      // pouch
            Shapes.Box(mb, new Vector3(0.15f, 0.795f, 0.07f), new Vector3(0.074f, 0.022f, 0.054f), Flat(VRamp.Leather, look.Leather - 1)); // pouch flap
            Shapes.Box(mb, new Vector3(-0.155f, 0.74f, 0.04f), new Vector3(0.035f, 0.12f, 0.03f), Quaternion.Euler(0f, 0f, 10f), Flat(VRamp.TimberDark, 3)); // sheath

            // Neck and neckerchief.
            mb.Skin = Chain(p => p.y, new[] { 1.13f, 1.22f }, new[] { chest, neck, head }, 0.03f);
            var skin = V(VRamp.Skin, look.Skin);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 1.1f, 0f), 0.05f, 0.05f, skin),
                new Shapes.Ring(new Vector3(0f, 1.25f, 0.005f), 0.048f, 0.048f, skin),
            }, 8, false, false);
            mb.Skin = _ => Rigid(chest);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 1.105f, 0.006f), 0.088f, 0.08f, V(VRamp.Accent, 4)),
                new Shapes.Ring(new Vector3(0f, 1.15f, 0.006f), 0.075f, 0.07f, V(VRamp.Accent, 4)),
            }, 10, true, true);
            Shapes.Box(mb, new Vector3(0f, 1.09f, 0.085f), new Vector3(0.04f, 0.035f, 0.025f), Quaternion.Euler(0f, 0f, 45f), Flat(VRamp.Accent, 3));
            mb.Skin = null;
        }

        static void Head(MeshBuilder mb, System.Random rng, Look look, int head, int leftEye, int rightEye)
        {
            mb.Skin = _ => Rigid(head);
            var skin = V(VRamp.Skin, look.Skin);
            Shapes.Blob(mb, rng, new Vector3(0f, 1.37f, 0.01f), 1f, new Vector3(0.205f, 0.215f, 0.195f), 0.01f, 2, skin);

            // Layered hair: cap trimmed to a hairline, then tufts for a readable silhouette.
            var hair = V(VRamp.Hair, look.Hair);
            var hairLight = V(VRamp.Hair, look.Hair + 1);
            Shapes.Blob(mb, rng, new Vector3(0f, 1.395f, -0.012f), 1f, new Vector3(0.217f, 0.215f, 0.21f), 0.04f, 1, hair, p =>
            {
                float front = Mathf.Clamp01((p.z + 0.03f) / 0.17f);
                float hairline = Mathf.Lerp(-0.15f, 0.045f, front);
                return p.y < hairline ? new Vector3(p.x, hairline, p.z) : p;
            });
            float[] fringeX = { -0.11f, -0.04f, 0.03f, 0.1f };
            for (int i = 0; i < fringeX.Length; i++)
                Tuft(mb, rng, new Vector3(fringeX[i], 1.475f - Mathf.Abs(fringeX[i]) * 0.25f, 0.172f), new Vector3(0.055f, 0.032f, 0.065f),
                    Quaternion.Euler(38f, fringeX[i] * 120f, -fringeX[i] * 160f), i % 2 == 0 ? hair : hairLight);
            foreach (float s in new[] { -1f, 1f })
                Tuft(mb, rng, new Vector3(s * 0.195f, 1.33f, 0.045f), new Vector3(0.03f, 0.075f, 0.042f), Quaternion.Euler(-8f, 0f, s * 8f), hair);
            for (int i = -1; i <= 1; i++)
                Tuft(mb, rng, new Vector3(i * 0.08f, 1.3f, -0.18f), new Vector3(0.06f, 0.065f, 0.04f), Quaternion.Euler(-30f, i * 18f, 0f), i == 0 ? hairLight : hair);
            Tuft(mb, rng, new Vector3(0.025f, 1.6f, -0.045f), new Vector3(0.028f, 0.06f, 0.028f), Quaternion.Euler(-25f, 0f, 18f), hairLight);

            // Eyes on their own bones so they can blink: white, pupil, highlight.
            foreach (var (side, bone) in new[] { (-1f, leftEye), (1f, rightEye) })
            {
                mb.Skin = _ => Rigid(bone);
                float x = side * 0.068f;
                Shapes.Box(mb, new Vector3(x, 1.365f, 0.192f), new Vector3(0.058f, 0.07f, 0.03f), Flat(VRamp.Plaster, 7));
                Shapes.Box(mb, new Vector3(x + side * 0.004f, 1.36f, 0.203f), new Vector3(0.036f, 0.052f, 0.02f), Flat(VRamp.Dark, 0));
                Shapes.Box(mb, new Vector3(x - side * 0.006f, 1.378f, 0.212f), new Vector3(0.014f, 0.014f, 0.006f), Flat(VRamp.Plaster, 7));
            }

            // Brows, nose, smile, cheeks, ears.
            mb.Skin = _ => Rigid(head);
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Box(mb, new Vector3(s * 0.068f, 1.432f, 0.193f), new Vector3(0.066f, 0.018f, 0.02f), Quaternion.Euler(0f, s * -8f, s * -10f),
                    Flat(VRamp.Hair, look.Hair - 1));
                Tuft(mb, rng, new Vector3(s * 0.112f, 1.318f, 0.168f), new Vector3(0.034f, 0.02f, 0.012f), Quaternion.Euler(0f, s * 30f, 0f),
                    Flat(VRamp.Accent, 6));
                Tuft(mb, rng, new Vector3(s * 0.205f, 1.36f, 0f), new Vector3(0.028f, 0.05f, 0.04f), Quaternion.identity, skin);
                Shapes.Box(mb, new Vector3(s * 0.026f, 1.291f, 0.188f), new Vector3(0.022f, 0.011f, 0.011f), Quaternion.Euler(0f, 0f, s * 25f), Flat(VRamp.Accent, 1));
            }
            Tuft(mb, rng, new Vector3(0f, 1.33f, 0.205f), new Vector3(0.025f, 0.028f, 0.025f), Quaternion.identity, skin);
            Shapes.Box(mb, new Vector3(0f, 1.284f, 0.192f), new Vector3(0.036f, 0.012f, 0.012f), Flat(VRamp.Accent, 1));
            mb.Skin = null;
        }

        static void Backpack(MeshBuilder mb, System.Random rng, Look look, int chest, int backpackBone)
        {
            // Pack body and its gear sway on the Backpack bone; straps stay on the chest.
            mb.Skin = _ => Rigid(backpackBone);
            var pack = V(VRamp.Leather, look.Leather + 1);
            var dark = V(VRamp.Leather, look.Leather - 1);
            Shapes.Blob(mb, rng, new Vector3(0f, 0.99f, -0.215f), 1f, new Vector3(0.14f, 0.165f, 0.075f), 0.02f, 1, pack);
            Shapes.Box(mb, new Vector3(0f, 1.115f, -0.205f), new Vector3(0.25f, 0.05f, 0.155f), Quaternion.Euler(-10f, 0f, 0f), dark);
            Shapes.Box(mb, new Vector3(0f, 1.075f, -0.292f), new Vector3(0.03f, 0.04f, 0.012f), Flat(VRamp.Window, 5));
            Shapes.Box(mb, new Vector3(0f, 0.93f, -0.29f), new Vector3(0.16f, 0.11f, 0.045f), V(VRamp.Leather, look.Leather));
            Shapes.Loft(mb, Vector3.up, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(-0.15f, 1.17f, -0.2f), 0.05f, 0.05f, V(VRamp.Accent, 2)),
                new Shapes.Ring(new Vector3(0.15f, 1.17f, -0.2f), 0.05f, 0.05f, V(VRamp.Accent, 2)),
            }, 8, true, true);
            Shapes.Cylinder(mb, new Vector3(0.165f, 0.9f, -0.2f), new Vector3(0.165f, 1.01f, -0.2f), 0.032f, 8, V(VRamp.TimberDark, 4));
            foreach (float s in new[] { -1f, 1f })
                Shapes.Box(mb, new Vector3(s * 0.08f, 1.17f, -0.2f), new Vector3(0.02f, 0.115f, 0.115f), Flat(VRamp.Leather, look.Leather - 1));

            mb.Skin = _ => Rigid(chest);
            var strap = Flat(VRamp.Leather, look.Leather - 1);
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Box(mb, new Vector3(s * 0.08f, 1.02f, 0.125f), new Vector3(0.032f, 0.22f, 0.018f), strap);
                Shapes.Box(mb, new Vector3(s * 0.08f, 1.125f, -0.03f), new Vector3(0.032f, 0.022f, 0.25f), strap);
            }
            Shapes.Box(mb, new Vector3(0f, 0.98f, 0.132f), new Vector3(0.16f, 0.02f, 0.015f), strap);
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

                // Blinking, look-at and backpack sway on top of the Animator.
                var motion = r.Root.AddComponent<HeroSecondaryMotion>();
                var so = new SerializedObject(motion);
                so.FindProperty("leftEye").objectReferenceValue = r.LeftEye;
                so.FindProperty("rightEye").objectReferenceValue = r.RightEye;
                so.FindProperty("backpack").objectReferenceValue = r.Backpack;
                so.ApplyModifiedPropertiesWithoutUndo();

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

        internal static Avatar SaveAvatar(Avatar fresh, string path)
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

        /// <summary>
        /// The hero prefab for <paramref name="seed"/>: version 2 first, then the version 1 backup,
        /// then any generated hero. Null if none exists.
        /// </summary>
        public static GameObject FindPrefab(int seed)
        {
            foreach (string name in new[] { NamePrefix + seed, LegacyPrefix + seed })
            {
                var exact = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(name));
                if (exact != null) return exact;
            }
            foreach (string prefix in new[] { NamePrefix, LegacyPrefix })
            {
                foreach (string guid in AssetDatabase.FindAssets(prefix + " t:Prefab", new[] { GeneratedAssetStore.PrefabFolder }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (prefab != null && prefab.name.StartsWith(prefix) && GeneratedAssetStore.IsGenerated(prefab)) return prefab;
                }
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
