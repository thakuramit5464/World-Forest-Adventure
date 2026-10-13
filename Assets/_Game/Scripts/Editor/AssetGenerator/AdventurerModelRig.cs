using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Greenhaven.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Turns the supplied static "Greenhaven_Adventurer.obj" (44 named parts, flat MTL colours, A-pose)
    /// into an animated Humanoid character:
    ///  1. Parses the OBJ/MTL directly (the file uses 0-based face indices, which Unity's OBJ importer
    ///     would misread, so it is deliberately kept outside Assets/ in ArtSource/).
    ///  2. Straightens the arms into a T-pose by rotating each arm part about the shoulder and elbow
    ///     (joints found from each part's principal axis), and scales the model to game height.
    ///  3. Skins every named part to the matching Humanoid bone (rigid per part, blended across hips,
    ///     knees and ankles), with eye bones for blinking and a backpack bone for sway.
    ///  4. Builds a Humanoid Avatar, one palette material from the MTL colours, and a prefab with
    ///     <see cref="HeroSecondaryMotion"/>, so the existing animator drives it.
    /// The source model is never modified.
    /// </summary>
    public static class AdventurerModelRig
    {
        public const string AssetName = "Gen_Adventurer";
        /// <summary>v2: taller (+12 %) with natural proportions, eyelids, expressive brows and mouth, and a scarf.</summary>
        public const string AssetNameV2 = "Gen_Adventurer2";

        /// <summary>A villager built from the same model: recoloured by MTL material name, with optional parts.</summary>
        public sealed class Variant
        {
            public string Name;
            public Dictionary<string, Color> Colours = new Dictionary<string, Color>();
            public bool Backpack = true, Pouch = true, Scarf = true, Beard;
            public float HairLength = 1f;
            public float Height = 1.84f;
        }

        static Color Rgb(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>The village cast (Greenhaven ▸ Build Villager Characters).</summary>
        public static readonly Variant[] Villagers =
        {
            new Variant { Name = "Mira", Height = 1.74f, Backpack = false, HairLength = 1.25f, Colours = { ["teal"] = Rgb(0.62f, 0.27f, 0.2f), ["trousers"] = Rgb(0.36f, 0.33f, 0.26f), ["scarf"] = Rgb(0.2f, 0.42f, 0.3f), ["hair"] = Rgb(0.25f, 0.14f, 0.08f), ["skin"] = Rgb(0.78f, 0.55f, 0.38f), ["cream"] = Rgb(0.93f, 0.9f, 0.84f) } },
            new Variant { Name = "Rowan", Height = 1.72f, Backpack = false, Pouch = false, Beard = true, HairLength = 0.75f, Colours = { ["teal"] = Rgb(0.4f, 0.28f, 0.18f), ["trousers"] = Rgb(0.3f, 0.28f, 0.25f), ["scarf"] = Rgb(0.6f, 0.6f, 0.62f), ["hair"] = Rgb(0.75f, 0.74f, 0.7f), ["skin"] = Rgb(0.74f, 0.52f, 0.38f), ["cream"] = Rgb(0.8f, 0.76f, 0.66f) } },
            new Variant { Name = "Tomas", Height = 1.8f, Backpack = false, Scarf = false, HairLength = 0.9f, Colours = { ["teal"] = Rgb(0.22f, 0.35f, 0.55f), ["trousers"] = Rgb(0.35f, 0.3f, 0.22f), ["hair"] = Rgb(0.5f, 0.3f, 0.15f), ["skin"] = Rgb(0.55f, 0.36f, 0.24f), ["cream"] = Rgb(0.86f, 0.82f, 0.7f) } },
            new Variant { Name = "Ada", Height = 1.7f, Backpack = false, HairLength = 1.3f, Colours = { ["teal"] = Rgb(0.45f, 0.22f, 0.35f), ["scarf"] = Rgb(0.9f, 0.85f, 0.7f), ["hair"] = Rgb(0.12f, 0.1f, 0.1f), ["trousers"] = Rgb(0.28f, 0.3f, 0.24f), ["skin"] = Rgb(0.66f, 0.45f, 0.3f), ["cream"] = Rgb(0.95f, 0.88f, 0.8f) } },
        };

        public static string VariantAssetName(string name) => AssetNameV2 + "_" + name;
        public static GameObject FindVariantPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(VariantAssetName(name)));
        const float TargetHeight = 1.65f, TargetHeightV2 = 1.84f;
        const string ObjFile = "Greenhaven_Adventurer.obj";
        const string MtlFile = "Greenhaven_Adventurer.mtl";
        static readonly string[] SourceFolders = { "ArtSource/Characters/GreenhavenAdventurer", "greenhaven_adventurer_model" };

        static readonly string[] RequiredParts =
        {
            "Head", "TunicBody", "Left_Sleeve", "Left_Forearm", "Left_Glove", "Right_Sleeve", "Right_Forearm", "Right_Glove",
            "Left_Trouser", "Right_Trouser", "Left_Boot", "Right_Boot", "Left_EyeWhite", "Right_EyeWhite", "BackpackMain",
        };

        sealed class Part
        {
            public string Name;
            public readonly List<Vector3[]> Faces = new List<Vector3[]>();
            public readonly List<int> FaceMaterial = new List<int>();
            public IEnumerable<Vector3> Points => Faces.SelectMany(f => f);
            public Vector3 Centroid => Points.Aggregate(Vector3.zero, (a, p) => a + p) / Mathf.Max(1, Points.Count());
        }

        /// <summary>The preferred rigged adventurer: v2 if it has been built, otherwise v1.</summary>
        public static GameObject FindPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(AssetNameV2)) ?? FindPrefabV1();

        public static GameObject FindPrefabV1() => AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(AssetName));

        public static string SourceFolder()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (string folder in SourceFolders)
            {
                string full = Path.Combine(projectRoot, folder);
                if (File.Exists(Path.Combine(full, ObjFile)) && File.Exists(Path.Combine(full, MtlFile))) return full;
            }
            return null;
        }

        // ───────────────────────────── Menu entry ─────────────────────────────

        public static string Rig() => Rig(false);

        /// <summary>Builds v1 (the original proportions) or v2 (taller, expressive). v1 is never touched by a v2 build.</summary>
        public static string Rig(bool v2) => Rig(v2, null);

        /// <summary>Builds the hero (v1 or v2) or, with a <paramref name="variant"/>, a villager from the same model.</summary>
        public static string Rig(bool v2, Variant variant)
        {
            string folder = SourceFolder();
            if (folder == null)
                return $"Could not find {ObjFile} and {MtlFile} in {string.Join(" or ", SourceFolders)} (relative to the project folder).";

            string name = variant != null ? VariantAssetName(variant.Name) : v2 ? AssetNameV2 : AssetName;
            if (variant != null) v2 = true;
            string meshPath = GeneratedAssetStore.MeshPath(name);
            string prefabPath = GeneratedAssetStore.PrefabPath(name);
            string avatarPath = $"{GeneratedAssetStore.MeshFolder}/{name}_Avatar.asset";
            var existing = new List<string>();
            foreach (string path in new[] { meshPath, prefabPath, avatarPath })
            {
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                if (!GeneratedAssetStore.IsGenerated(asset)) return $"Stopped: {path} exists but was not created by this tool.";
                existing.Add(path);
            }
            if (existing.Count > 0 && !EditorUtility.DisplayDialog("Replace rigged adventurer?",
                    $"{name} already exists and will be rebuilt from the OBJ (scenes using it will update).", "Replace", "Cancel"))
                return "Cancelled. Rigged adventurer unchanged.";

            GeneratedAssetStore.EnsureFolders();
            HeroGenerator.Result r;
            List<string> notes;
            Palette palette;
            Dictionary<string, Transform> extras;
            try
            {
                r = BuildInMemory(folder, out palette, out notes, v2, out extras, variant);
            }
            catch (Exception e)
            {
                return "Could not read the adventurer model: " + e.Message;
            }

            try
            {
                if (!r.Avatar.isValid || !r.Avatar.isHuman)
                {
                    Object.DestroyImmediate(r.Mesh);
                    Object.DestroyImmediate(r.Avatar);
                    return "The rig did not produce a valid Humanoid avatar; nothing was saved. See the Console for AvatarBuilder messages.";
                }
                Material material = GeneratedAssetStore.EnsurePaletteMaterial(palette);
                Mesh meshAsset = GeneratedAssetStore.SaveMesh(r.Mesh, meshPath);
                Avatar avatarAsset = HeroGenerator.SaveAvatar(r.Avatar, avatarPath);
                r.Renderer.sharedMesh = meshAsset;
                r.Renderer.sharedMaterial = material;
                r.Root.GetComponent<Animator>().avatar = avatarAsset;

                var motion = r.Root.AddComponent<HeroSecondaryMotion>();
                var so = new SerializedObject(motion);
                so.FindProperty("leftEye").objectReferenceValue = r.LeftEye;
                so.FindProperty("rightEye").objectReferenceValue = r.RightEye;
                so.FindProperty("backpack").objectReferenceValue = r.Backpack;
                if (v2)
                {
                    so.FindProperty("leftBrow").objectReferenceValue = extras["BrowL"];
                    so.FindProperty("rightBrow").objectReferenceValue = extras["BrowR"];
                    so.FindProperty("mouth").objectReferenceValue = extras["Mouth"];
                    SerializedProperty scarf = so.FindProperty("scarf");
                    scarf.arraySize = 2;
                    scarf.GetArrayElementAtIndex(0).objectReferenceValue = extras["ScarfTail0"];
                    scarf.GetArrayElementAtIndex(1).objectReferenceValue = extras["ScarfTail1"];
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(r.Root, prefabPath, out bool ok);
                if (!ok || prefab == null) return $"Failed to save {prefabPath}.";
                AssetDatabase.SetLabels(prefab, new[] { GeneratedAssetStore.Label });
                AssetDatabase.SaveAssets();

                var problems = new List<string>();
                HeroGenerator.ValidatePrefab(prefab, problems);
                string summary = $"Rigged adventurer saved: {prefabPath} ({meshAsset.triangles.Length / 3} triangles, " +
                                 $"{r.Renderer.bones.Length} bones, valid Humanoid avatar). Source: {folder}";
                if (notes.Count > 0) summary += "\nNotes:\n • " + string.Join("\n • ", notes);
                return problems.Count == 0 ? summary : summary + $"\n{problems.Count} problem(s):\n • " + string.Join("\n • ", problems);
            }
            finally
            {
                Object.DestroyImmediate(r.Root);
            }
        }

        // ───────────────────────────── Build ─────────────────────────────

        /// <summary>Reads, poses, skins and rigs the model in memory (nothing saved).</summary>
        public static HeroGenerator.Result BuildInMemory(string folder, out Palette palette, out List<string> notes) =>
            BuildInMemory(folder, out palette, out notes, false, out _);

        public static HeroGenerator.Result BuildInMemory(string folder, out Palette palette, out List<string> notes, bool v2, out Dictionary<string, Transform> extras) =>
            BuildInMemory(folder, out palette, out notes, v2, out extras, null);

        public static HeroGenerator.Result BuildInMemory(string folder, out Palette palette, out List<string> notes, bool v2, out Dictionary<string, Transform> extras, Variant variant)
        {
            notes = new List<string>();
            extras = new Dictionary<string, Transform>();
            List<(string name, Color colour)> materials = ParseMtl(Path.Combine(folder, MtlFile));
            Dictionary<string, Part> parts = ParseObj(Path.Combine(folder, ObjFile), materials.Select(m => m.name).ToList(), notes);
            foreach (string required in RequiredParts)
                if (!parts.ContainsKey(required)) throw new InvalidDataException($"part '{required}' is missing from the OBJ");
            if (v2)
            {
                foreach (string required in new[] { "Neck", "Smile", "Left_Eyebrow", "Right_Eyebrow" })
                    if (!parts.ContainsKey(required)) throw new InvalidDataException($"part '{required}' (needed for v2) is missing from the OBJ");
                materials.Add(("scarf", new Color(0.88f, 0.64f, 0.13f, 1f))); // mustard scarf
                // Colours from the owner's turnaround sheet (Adventurer Character Turnaround Sheet.png).
                for (int i = 0; i < materials.Count; i++)
                {
                    string m = materials[i].name;
                    if (m == "teal") materials[i] = (m, new Color(0.16f, 0.36f, 0.23f, 1f));          // forest-green jacket
                    else if (m == "trousers") materials[i] = (m, new Color(0.31f, 0.31f, 0.19f, 1f)); // olive trousers
                    else if (m == "darkleather") materials[i] = (m, new Color(0.50f, 0.32f, 0.15f, 1f)); // tan boot soles
                }
                if (variant != null)
                {
                    foreach (KeyValuePair<string, Color> colour in variant.Colours)
                    {
                        int at = materials.FindIndex(m => m.name == colour.Key);
                        if (at >= 0) materials[at] = (colour.Key, colour.Value);
                    }
                    if (!variant.Backpack)
                        foreach (string part in new[] { "BackpackMain", "BackpackFlap", "Left_BackpackStrap", "Right_BackpackStrap" }) parts.Remove(part);
                    if (!variant.Pouch) parts.Remove("Pouch");
                }
            }

            // One palette row per MTL colour: darker for undersides, lighter for tops (shade 4 ≈ the MTL colour).
            // Each villager gets its own palette texture and material; sharing one would recolour every character.
            palette = new Palette(variant != null ? "AdventurerPalette_" + variant.Name : "AdventurerPalette", materials.Select(m => (Tint(m.colour, 0.72f), Tint(m.colour, 1.3f))).ToArray());

            // 1. Pose: straighten the arms into a T-pose (model units).
            var joints = new Dictionary<string, Vector3>();
            foreach (float side in new[] { -1f, 1f }) StraightenArm(parts, side, joints);

            // 1b. v2 proportions; M remaps model heights so bones and weights follow the same change.
            Func<float, float> M = v2 ? Reshape(parts, joints) : (y => y);
            if (variant != null && Mathf.Abs(variant.HairLength - 1f) > 0.01f && parts.ContainsKey("Head"))
            {
                // Longer or shorter hair: scale the locks away from the head's centre (model units).
                Vector3 headC = parts["Head"].Centroid;
                foreach (Part hairLock in parts.Values.Where(p => p.Name.StartsWith("HairLock")))
                    foreach (Vector3[] face in hairLock.Faces)
                        for (int i = 0; i < face.Length; i++) face[i] = headC + (face[i] - headC) * variant.HairLength;
            }

            // 2. Scale to game height (feet already at y ≈ 0).
            float maxY = parts.Values.SelectMany(p => p.Points).Max(v => v.y);
            float s = (variant != null ? variant.Height : v2 ? TargetHeightV2 : TargetHeight) / maxY;
            foreach (Part part in parts.Values)
                for (int f = 0; f < part.Faces.Count; f++)
                    for (int i = 0; i < part.Faces[f].Length; i++) part.Faces[f][i] *= s;
            foreach (string key in joints.Keys.ToList()) joints[key] *= s;

            // 3. Skeleton (world positions, identity rotations) from measured parts.
            Vector3 C(string name) => parts[name].Centroid;
            float hipsY = M(0.75f) * s, spineY = M(0.9f) * s, chestY = M(1.08f) * s, neckY = M(1.29f) * s, headY = M(1.42f) * s;
            var world = new Dictionary<string, Vector3>
            {
                ["Hips"] = new Vector3(0f, hipsY, 0f),
                ["Spine"] = new Vector3(0f, spineY, 0f),
                ["Chest"] = new Vector3(0f, chestY, 0f),
                ["Neck"] = new Vector3(0f, neckY, 0f),
                ["Head"] = new Vector3(0f, headY, 0f),
                ["LeftEye"] = C("Left_EyeWhite"),
                ["RightEye"] = C("Right_EyeWhite"),
                ["Backpack"] = parts.ContainsKey("BackpackMain") ? C("BackpackMain") : new Vector3(0f, chestY, -0.12f * s),
            };
            foreach (float side in new[] { -1f, 1f })
            {
                string pre = side < 0f ? "Left" : "Right";
                Vector3 shoulderJoint = joints[pre + "UpperArm"];
                world[pre + "Shoulder"] = new Vector3(side * 0.09f * s, shoulderJoint.y, 0f);
                world[pre + "UpperArm"] = shoulderJoint;
                world[pre + "LowerArm"] = joints[pre + "LowerArm"];
                world[pre + "Hand"] = joints[pre + "Hand"];
                float legX = C(pre + "_Trouser").x;
                float footX = C(pre + "_Boot").x;
                world[pre + "UpperLeg"] = new Vector3(legX, M(0.7f) * s, 0f);
                world[pre + "LowerLeg"] = new Vector3(legX, M(0.45f) * s, 0f);
                world[pre + "Foot"] = new Vector3(footX, M(0.12f) * s, 0f);
                world[pre + "Toes"] = new Vector3(footX, M(0.04f) * s, 0.15f * s);
            }
            Scarf scarfShape = default;
            if (v2)
            {
                world["BrowL"] = C("Left_Eyebrow");
                world["BrowR"] = C("Right_Eyebrow");
                world["Mouth"] = C("Smile");
                scarfShape = MeasureScarf(parts);
                world["ScarfTail0"] = scarfShape.Tail0;
                world["ScarfTail1"] = scarfShape.Tail1;
            }

            string[] order =
            {
                "Hips", "Spine", "Chest", "Neck", "Head", "LeftEye", "RightEye",
                "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
                "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes", "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes", "Backpack",
            };
            if (v2) order = order.Concat(new[] { "BrowL", "BrowR", "Mouth", "ScarfTail0", "ScarfTail1" }).ToArray();
            var parent = new Dictionary<string, string>
            {
                ["Hips"] = null, ["Spine"] = "Hips", ["Chest"] = "Spine", ["Neck"] = "Chest", ["Head"] = "Neck",
                ["LeftEye"] = "Head", ["RightEye"] = "Head", ["Backpack"] = "Chest",
                ["LeftShoulder"] = "Chest", ["LeftUpperArm"] = "LeftShoulder", ["LeftLowerArm"] = "LeftUpperArm", ["LeftHand"] = "LeftLowerArm",
                ["RightShoulder"] = "Chest", ["RightUpperArm"] = "RightShoulder", ["RightLowerArm"] = "RightUpperArm", ["RightHand"] = "RightLowerArm",
                ["LeftUpperLeg"] = "Hips", ["LeftLowerLeg"] = "LeftUpperLeg", ["LeftFoot"] = "LeftLowerLeg", ["LeftToes"] = "LeftFoot",
                ["RightUpperLeg"] = "Hips", ["RightLowerLeg"] = "RightUpperLeg", ["RightFoot"] = "RightLowerLeg", ["RightToes"] = "RightFoot",
                ["BrowL"] = "Head", ["BrowR"] = "Head", ["Mouth"] = "Head", ["ScarfTail0"] = "Chest", ["ScarfTail1"] = "ScarfTail0",
            };

            var root = new GameObject(v2 ? AssetNameV2 : AssetName);
            var armature = new GameObject("Armature").transform;
            armature.SetParent(root.transform, false);
            var byName = new Dictionary<string, Transform>();
            var bones = new Transform[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                string name = order[i];
                var t = new GameObject(name).transform;
                string p = parent[name];
                t.SetParent(p == null ? armature : byName[p], false);
                t.localPosition = world[name] - (p == null ? Vector3.zero : world[p]);
                byName[name] = t;
                bones[i] = t;
            }
            int B(string name) => Array.IndexOf(order, name);

            // 4. Skinned, faceted mesh on the palette.
            var mb = new MeshBuilder();
            foreach (Part part in parts.Values)
            {
                mb.Skin = WeightsFor(part.Name, s, B, notes, M, v2);
                Vector3 centre = part.Centroid;
                for (int f = 0; f < part.Faces.Count; f++)
                {
                    Vector3[] face = part.Faces[f];
                    Vector3 faceCentre = face.Aggregate(Vector3.zero, (a, v) => a + v) / face.Length;
                    Vector3 outward = faceCentre - centre;
                    if (outward.sqrMagnitude < 1e-8f) outward = Vector3.Cross(face[1] - face[0], face[2] - face[0]);
                    int row = part.FaceMaterial[f];
                    if (v2 && (part.Name.EndsWith("_Glove") || part.Name.EndsWith("_Forearm"))) row = materials.FindIndex(m => m.name == "skin");
                    for (int i = 1; i < face.Length - 1; i++)
                    {
                        Vector3 n = MeshBuilder.OrientedNormal(face[0], face[i], face[i + 1], outward);
                        mb.Triangle(face[0], face[i], face[i + 1], outward, palette.UV(row, 4 + Palette.SlopeShade(n)));
                    }
                }
            }
            if (v2) AddV2Details(mb, parts, palette, materials.FindIndex(m => m.name == "skin"), materials.FindIndex(m => m.name == "scarf"),
                materials.FindIndex(m => m.name == "cream"), materials.FindIndex(m => m.name == "leather"), scarfShape, world, B,
                variant == null || variant.Scarf, variant != null && variant.Beard, materials.FindIndex(m => m.name == "hair"), rngBeard: new System.Random(7));
            mb.Skin = null;

            Mesh mesh = mb.ToMesh(v2 ? AssetNameV2 : AssetName);
            Matrix4x4 rootMatrix = root.transform.localToWorldMatrix;
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * rootMatrix).ToArray();
            mesh.RecalculateBounds();

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var smr = body.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = byName["Hips"];
            smr.localBounds = new Bounds(Vector3.zero, new Vector3(2f, 2.4f, 1.6f));

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, HeroGenerator.Description(root, order));
            avatar.name = (v2 ? AssetNameV2 : AssetName) + "_Avatar";
            if (v2)
                foreach (string extra in new[] { "BrowL", "BrowR", "Mouth", "ScarfTail0", "ScarfTail1" })
                    extras[extra] = byName[extra];
            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;

            return new HeroGenerator.Result
            {
                Root = root, Mesh = mesh, Avatar = avatar, Renderer = smr,
                LeftEye = byName["LeftEye"], RightEye = byName["RightEye"], Backpack = byName["Backpack"],
            };
        }

        /// <summary>
        /// Rotates sleeve, forearm and glove so the arm points straight out (T-pose). Shoulder and
        /// elbow come from the principal axes of the sleeve and forearm parts.
        /// </summary>
        static void StraightenArm(Dictionary<string, Part> parts, float side, Dictionary<string, Vector3> joints)
        {
            string pre = side < 0f ? "Left_" : "Right_";
            Part sleeve = parts[pre + "Sleeve"], forearm = parts[pre + "Forearm"], glove = parts[pre + "Glove"];

            (Vector3 a0, Vector3 a1) = AxisEnds(sleeve.Points);
            Vector3 shoulder = Mathf.Abs(a0.x) < Mathf.Abs(a1.x) ? a0 : a1;
            Vector3 elbow = shoulder == a0 ? a1 : a0;
            (Vector3 f0, Vector3 f1) = AxisEnds(forearm.Points);
            Vector3 wrist = (f0 - elbow).sqrMagnitude > (f1 - elbow).sqrMagnitude ? f0 : f1;

            var outward = new Vector3(side, 0f, 0f);
            Quaternion upper = Quaternion.FromToRotation(elbow - shoulder, outward);
            Vector3 elbowT = shoulder + outward * (elbow - shoulder).magnitude;
            Vector3 wristAfterUpper = shoulder + upper * (wrist - shoulder);
            Quaternion lower = Quaternion.FromToRotation(wristAfterUpper - elbowT, outward);
            Vector3 wristT = elbowT + outward * (wristAfterUpper - elbowT).magnitude;

            void Apply(Part part, bool includesLower)
            {
                foreach (Vector3[] face in part.Faces)
                    for (int i = 0; i < face.Length; i++)
                    {
                        Vector3 p = shoulder + upper * (face[i] - shoulder);
                        face[i] = includesLower ? elbowT + lower * (p - elbowT) : p;
                    }
            }
            Apply(sleeve, false);
            Apply(forearm, true);
            Apply(glove, true);

            string bonePre = side < 0f ? "Left" : "Right";
            joints[bonePre + "UpperArm"] = shoulder;
            joints[bonePre + "LowerArm"] = elbowT;
            joints[bonePre + "Hand"] = wristT;
        }

        /// <summary>Ends of a point set along its principal axis (power iteration on the covariance).</summary>
        static (Vector3, Vector3) AxisEnds(IEnumerable<Vector3> points)
        {
            Vector3[] pts = points.ToArray();
            Vector3 c = pts.Aggregate(Vector3.zero, (a, p) => a + p) / pts.Length;
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (Vector3 p in pts)
            {
                Vector3 d = p - c;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            Vector3 axis = new Vector3(1f, 0.5f, 0.25f).normalized;
            for (int i = 0; i < 32; i++)
            {
                axis = new Vector3(xx * axis.x + xy * axis.y + xz * axis.z, xy * axis.x + yy * axis.y + yz * axis.z, xz * axis.x + yz * axis.y + zz * axis.z);
                if (axis.sqrMagnitude < 1e-12f) { axis = Vector3.right; break; }
                axis.Normalize();
            }
            float min = pts.Min(p => Vector3.Dot(p - c, axis)), max = pts.Max(p => Vector3.Dot(p - c, axis));
            return (c + axis * min, c + axis * max);
        }

        /// <summary>Bone weights per named part (positions are final, scaled game coordinates).</summary>
        static Func<Vector3, BoneWeight> WeightsFor(string part, float s, Func<string, int> bone, List<string> notes, Func<float, float> M, bool v2)
        {
            string side = part.StartsWith("Left_") ? "Left" : part.StartsWith("Right_") ? "Right" : null;
            string bare = side == null ? part : part.Substring(side.Length + 1);
            BoneWeight R(string b) => HeroGenerator.Rigid(bone(b));

            switch (bare)
            {
                case "Head": case "Ear": case "HairCap": case "Nose":
                    return _ => R("Head");
                case "Smile":
                    return _ => R(v2 ? "Mouth" : "Head");
                case "Eyebrow":
                    return _ => R(v2 ? (side == "Left" ? "BrowL" : "BrowR") : "Head");
                case "EyeWhite": case "Iris": case "Pupil":
                    return _ => R(side + "Eye");
                case "Neck":
                    return HeroGenerator.Chain(p => p.y, new[] { M(1.31f) * s, M(1.45f) * s }, new[] { bone("Chest"), bone("Neck"), bone("Head") }, 0.03f * s);
                case "TunicBody": case "ShirtFront":
                    return HeroGenerator.Chain(p => p.y, new[] { M(0.86f) * s, M(1.0f) * s }, new[] { bone("Hips"), bone("Spine"), bone("Chest") }, 0.05f * s);
                case "Belt": case "Buckle": case "Pouch":
                    return _ => R("Hips");
                case "BackpackMain": case "BackpackFlap":
                    return _ => R("Backpack");
                case "BackpackStrap":
                    return _ => R("Chest");
                case "Sleeve":
                    return _ => R(side + "UpperArm");
                case "Forearm":
                    return _ => R(side + "LowerArm");
                case "Glove":
                    return _ => R(side + "Hand");
                case "Trouser":
                    return HeroGenerator.Chain(p => -p.y, new[] { -M(0.69f) * s, -M(0.45f) * s },
                        new[] { bone("Hips"), bone(side + "UpperLeg"), bone(side + "LowerLeg") }, 0.04f * s);
                case "Cuff":
                    return _ => R(side + "LowerLeg");
                case "Boot": case "BootSole":
                {
                    Func<Vector3, BoneWeight> ankle = HeroGenerator.Chain(p => -p.y, new[] { -M(0.15f) * s }, new[] { bone(side + "LowerLeg"), bone(side + "Foot") }, 0.03f * s);
                    return p => p.z > 0.13f * s && p.y < M(0.12f) * s ? R(side + "Toes") : ankle(p);
                }
            }
            if (bare.StartsWith("HairLock")) return _ => R("Head");
            notes.Add($"Unrecognised part '{part}' was attached to the chest.");
            return _ => R("Chest");
        }

        // ───────────────────────────── v2 proportions and details ─────────────────────────────

        /// <summary>
        /// v2 proportions in model units (after the T-pose): longer, tapered legs; a narrower torso with a defined
        /// waist (shoulders keep their width); a slimmer, slightly longer neck; slimmer, slightly longer arms.
        /// Returns the height remapping, so bones and weights follow the same change.
        /// </summary>
        static Func<float, float> Reshape(Dictionary<string, Part> parts, Dictionary<string, Vector3> joints)
        {
            const float legTop = 0.70f, neckBase = 1.29f, neckTop = 1.37f;
            const float kLeg = 1.2f, kTorso = 1.04f, kNeck = 1.6f;
            float a = legTop * kLeg, b = a + (neckBase - legTop) * kTorso, c = b + (neckTop - neckBase) * kNeck;
            Func<float, float> map = y => y <= legTop ? y * kLeg : y <= neckBase ? a + (y - legTop) * kTorso : y <= neckTop ? b + (y - neckBase) * kNeck : c + (y - neckTop);

            void Each(Part part, Func<Vector3, Vector3> f)
            {
                foreach (Vector3[] face in part.Faces)
                    for (int i = 0; i < face.Length; i++) face[i] = f(face[i]);
            }
            Part P(string n) => parts.TryGetValue(n, out Part found) ? found : null;

            foreach (string pre in new[] { "Left", "Right" })
            {
                // Arms: 6 % longer from the shoulder, 10 % slimmer around their axis.
                Vector3 shoulder = joints[pre + "UpperArm"];
                Vector3 Arm(Vector3 v) => new Vector3(shoulder.x + (v.x - shoulder.x) * 1.06f, shoulder.y + (v.y - shoulder.y) * 0.9f, shoulder.z + (v.z - shoulder.z) * 0.9f);
                foreach (string n in new[] { "_Sleeve", "_Forearm", "_Glove" })
                {
                    Part arm = P(pre + n);
                    if (arm != null) Each(arm, Arm);
                }
                joints[pre + "LowerArm"] = Arm(joints[pre + "LowerArm"]);
                joints[pre + "Hand"] = Arm(joints[pre + "Hand"]);

                // Legs: taper from the thigh to the calf and ankle.
                Part trouser = P(pre + "_Trouser");
                if (trouser != null)
                {
                    Vector3 centre = trouser.Centroid;
                    Each(trouser, v =>
                    {
                        float f = v.y >= 0.62f ? 1.04f : v.y >= 0.42f ? Mathf.Lerp(0.95f, 1.04f, (v.y - 0.42f) / 0.2f) : Mathf.Lerp(0.92f, 0.95f, v.y / 0.42f);
                        return new Vector3(centre.x + (v.x - centre.x) * f, v.y, centre.z + (v.z - centre.z) * f);
                    });
                }
            }

            // Torso: hips, waist, chest, shoulders.
            float[] ys = { 0.70f, 0.86f, 1.0f, 1.12f, 1.25f };
            float[] ws = { 0.94f, 0.86f, 0.92f, 0.97f, 1f };
            float Width(float y)
            {
                if (y <= ys[0]) return ws[0];
                for (int i = 1; i < ys.Length; i++)
                    if (y <= ys[i]) return Mathf.Lerp(ws[i - 1], ws[i], Mathf.SmoothStep(0f, 1f, (y - ys[i - 1]) / (ys[i] - ys[i - 1])));
                return ws[ws.Length - 1];
            }
            foreach (string n in new[] { "TunicBody", "ShirtFront", "Belt", "Buckle", "Pouch", "Left_BackpackStrap", "Right_BackpackStrap" })
            {
                Part torso = P(n);
                if (torso != null) Each(torso, v => new Vector3(v.x * Width(v.y), v.y, v.z));
            }
            foreach (string n in new[] { "Left_Eyebrow", "Right_Eyebrow" })
            {
                Part brow = P(n);
                if (brow == null) continue;
                Vector3 bc = brow.Centroid;
                Each(brow, v => new Vector3(bc.x + (v.x - bc.x) * 1.1f, bc.y + 0.01f + (v.y - bc.y) * 1.4f, bc.z + (v.z - bc.z) * 1.15f));
            }
            Part smile = P("Smile");
            if (smile != null)
            {
                Vector3 sc = smile.Centroid;
                Each(smile, v => new Vector3(sc.x + (v.x - sc.x) * 1.25f, sc.y + (v.y - sc.y) * 1.3f, v.z));
            }
            Part neck = P("Neck");
            if (neck != null)
            {
                Vector3 nc = neck.Centroid;
                Each(neck, v => new Vector3(nc.x + (v.x - nc.x) * 0.88f, v.y, nc.z + (v.z - nc.z) * 0.88f));
            }

            foreach (Part part in parts.Values) Each(part, v => new Vector3(v.x, map(v.y), v.z));
            foreach (string k in joints.Keys.ToList()) joints[k] = new Vector3(joints[k].x, map(joints[k].y), joints[k].z);
            return map;
        }

        struct Scarf
        {
            public Vector3 RingCentre, Tail0, Tail1, TailEnd;
            public float RingRadius;
        }

        /// <summary>Scarf placement from the final (scaled) neck and tunic: a loop round the neck base and a tail down the chest.</summary>
        static Scarf MeasureScarf(Dictionary<string, Part> parts)
        {
            Vector3[] neck = parts["Neck"].Points.ToArray();
            Vector3 nc = parts["Neck"].Centroid;
            float minY = neck.Min(p => p.y), maxY = neck.Max(p => p.y);
            float radius = neck.Max(p => new Vector2(p.x - nc.x, p.z - nc.z).magnitude);
            IEnumerable<Vector3> collar = parts["TunicBody"].Points.Where(p => Mathf.Abs(p.x - nc.x) < 0.1f && Mathf.Abs(p.z - nc.z) < 0.12f);
            float collarTop = collar.Any() ? collar.Max(p => p.y) : minY + (maxY - minY) * 0.22f;
            float ringY = Mathf.Clamp(collarTop + 0.012f, minY, maxY - 0.02f);
            IEnumerable<Vector3> atRing = neck.Where(p => Mathf.Abs(p.y - ringY) < 0.03f);
            if (atRing.Any()) radius = atRing.Max(p => new Vector2(p.x - nc.x, p.z - nc.z).magnitude);
            float startY = ringY - 0.03f;
            IEnumerable<Vector3> chest = parts["TunicBody"].Points.Where(p => Mathf.Abs(p.x + 0.05f) < 0.07f && p.y < startY && p.y > startY - 0.3f);
            float frontZ = chest.Any() ? chest.Max(p => p.z) : nc.z + radius * 1.4f;
            var tail0 = new Vector3(-0.05f, startY, frontZ + 0.018f);
            return new Scarf
            {
                RingCentre = new Vector3(nc.x, ringY, nc.z),
                RingRadius = radius + 0.03f,
                Tail0 = tail0,
                Tail1 = tail0 + new Vector3(-0.008f, -0.11f, 0.008f),
                TailEnd = tail0 + new Vector3(-0.016f, -0.22f, 0.014f),
            };
        }

        /// <summary>v2 extras: soft upper eyelids (on the eye bones, so they close with a blink) and the mustard scarf.</summary>
        static void AddV2Details(MeshBuilder mb, Dictionary<string, Part> parts, Palette palette, int skinRow, int scarfRow, int creamRow, int leatherRow,
            Scarf scarf, Dictionary<string, Vector3> joints, Func<string, int> bone, bool withScarf = true, bool beard = false, int hairRow = -1, System.Random rngBeard = null)
        {
            if (beard && hairRow >= 0 && parts.ContainsKey("Head"))
            {
                // A short, full beard under the chin, following the head's lower front.
                Vector3[] head = parts["Head"].Points.ToArray();
                float minY = head.Min(p => p.y), maxY = head.Max(p => p.y), frontZ = head.Max(p => p.z);
                Vector3 hc = parts["Head"].Centroid;
                float width = (head.Max(p => p.x) - head.Min(p => p.x)) * 0.5f;
                mb.Skin = _ => HeroGenerator.Rigid(bone("Head"));
                Shapes.Blob(mb, rngBeard ?? new System.Random(7), new Vector3(hc.x, minY + (maxY - minY) * 0.16f, hc.z + (frontZ - hc.z) * 0.55f),
                    width * 0.95f, new Vector3(1f, 0.55f, 0.75f), 0.08f, 1, n => palette.UV(hairRow, 3 + (n.y > 0.4f ? 1 : 0)));
            }
            // Rolled-up cream sleeve cuffs at the elbows and leather wristbands (arms point along ±X in the T-pose).
            foreach (string pre in new[] { "Left", "Right" })
            {
                float side = pre == "Left" ? -1f : 1f;
                Vector3 elbow = joints[pre + "LowerArm"], wrist = joints[pre + "Hand"];
                float Radius(Vector3 at)
                {
                    IEnumerable<Vector3> near = parts[pre + "_Forearm"].Points.Concat(parts[pre + "_Sleeve"].Points).Where(p => Mathf.Abs(p.x - at.x) < 0.035f);
                    return near.Any() ? near.Max(p => new Vector2(p.y - at.y, p.z - at.z).magnitude) : 0.04f;
                }
                mb.Skin = _ => HeroGenerator.Rigid(bone(pre + "LowerArm"));
                float cuff = Radius(elbow) * 1.15f + 0.006f;
                Shapes.Cylinder(mb, elbow + Vector3.right * side * 0.005f, elbow + Vector3.right * side * 0.06f, cuff, 8,
                    n => palette.UV(creamRow, 4 + Palette.SlopeShade(n)), true, true);
                float band = Radius(wrist) * 1.08f + 0.004f;
                Vector3 bandAt = wrist - Vector3.right * side * 0.035f;
                Shapes.Cylinder(mb, bandAt - Vector3.right * side * 0.012f, bandAt + Vector3.right * side * 0.012f, band, 8,
                    n => palette.UV(leatherRow, 4 + Palette.SlopeShade(n)), true, true);
            }

            foreach (string pre in new[] { "Left", "Right" })
            {
                Vector3[] eye = parts[pre + "_EyeWhite"].Points.ToArray();
                Vector3 min = new Vector3(eye.Min(p => p.x), eye.Min(p => p.y), eye.Min(p => p.z));
                Vector3 max = new Vector3(eye.Max(p => p.x), eye.Max(p => p.y), eye.Max(p => p.z));
                Vector3 c = (min + max) * 0.5f, size = max - min;
                mb.Skin = _ => HeroGenerator.Rigid(bone(pre + "Eye"));
                Shapes.Box(mb, new Vector3(c.x, c.y + size.y * 0.47f, c.z + size.z * 0.3f), new Vector3(size.x * 1.12f, size.y * 0.2f, Mathf.Max(size.z * 0.7f, 0.008f)),
                    n => palette.UV(skinRow, 2 + (n.y > 0.5f ? 1 : 0)));
            }

            if (!withScarf) return;
            // Scarf loop, drooping a little at the front.
            mb.Skin = _ => HeroGenerator.Rigid(bone("Chest"));
            const int around = 14, sides = 6;
            const float tube = 0.03f;
            Vector3 P(float a, float b)
            {
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 q = scarf.RingCentre + dir * (scarf.RingRadius + tube * Mathf.Cos(b)) + Vector3.up * (tube * Mathf.Sin(b));
                q.y -= 0.02f * Mathf.Max(0f, Mathf.Sin(a));
                return q;
            }
            for (int i = 0; i < around; i++)
            {
                float a0 = i * Mathf.PI * 2f / around, a1 = (i + 1) * Mathf.PI * 2f / around;
                for (int j = 0; j < sides; j++)
                {
                    float b0 = j * Mathf.PI * 2f / sides, b1 = (j + 1) * Mathf.PI * 2f / sides;
                    Vector3 p00 = P(a0, b0), p10 = P(a1, b0), p01 = P(a0, b1), p11 = P(a1, b1);
                    float am = (a0 + a1) * 0.5f;
                    Vector3 outward = (p00 + p11) * 0.5f - (scarf.RingCentre + new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * scarf.RingRadius);
                    Vector3 n = MeshBuilder.OrientedNormal(p00, p10, p01, outward);
                    Vector2 uv = palette.UV(scarfRow, 4 + Palette.SlopeShade(n) + (i % 3 == 0 ? -1 : 0));
                    mb.Triangle(p00, p10, p01, outward, uv);
                    mb.Triangle(p01, p10, p11, outward, uv);
                }
            }

            // Tail down the chest in two pieces, so it can swing.
            void Piece(Vector3 from, Vector3 to, float width, string boneName, int shade)
            {
                mb.Skin = _ => HeroGenerator.Rigid(bone(boneName));
                Vector3 mid = (from + to) * 0.5f;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, from - to);
                Shapes.Box(mb, mid, new Vector3(width, Vector3.Distance(from, to) + 0.01f, 0.018f), rot, n => palette.UV(scarfRow, shade + Palette.SlopeShade(n)));
            }
            Piece(scarf.Tail0 + Vector3.up * 0.02f, scarf.Tail1, 0.07f, "ScarfTail0", 4);
            Piece(scarf.Tail1, scarf.TailEnd, 0.075f, "ScarfTail1", 4);
            Piece(scarf.TailEnd + Vector3.up * 0.012f, scarf.TailEnd - Vector3.up * 0.006f, 0.078f, "ScarfTail1", 2); // darker fringe
            Vector3 second = new Vector3(0.035f, 0f, 0.006f);
            Piece(scarf.Tail0 + second + Vector3.up * 0.015f, scarf.Tail1 + second + new Vector3(0.006f, 0.035f, 0.004f), 0.065f, "ScarfTail0", 5);
            Piece(scarf.Tail1 + second + new Vector3(0.006f, 0.04f, 0.004f), scarf.Tail1 + second + new Vector3(0.006f, 0.022f, 0.004f), 0.068f, "ScarfTail0", 3);
        }

        // ───────────────────────────── OBJ / MTL parsing ─────────────────────────────

        static List<(string, Color)> ParseMtl(string path)
        {
            var list = new List<(string, Color)>();
            string current = null;
            foreach (string raw in File.ReadAllLines(path))
            {
                string[] t = raw.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length == 0) continue;
                if (t[0] == "newmtl" && t.Length > 1) current = t[1];
                else if (t[0] == "Kd" && t.Length >= 4 && current != null)
                    list.Add((current, new Color(F(t[1]), F(t[2]), F(t[3]), 1f)));
            }
            if (list.Count == 0) throw new InvalidDataException("no materials (newmtl/Kd) in the MTL file");
            return list;
        }

        static Dictionary<string, Part> ParseObj(string path, List<string> materialNames, List<string> notes)
        {
            string[] lines = File.ReadAllLines(path);
            // The supplied file writes face indices starting at 0 (the OBJ format starts at 1). Detect and accept both.
            bool zeroBased = lines.Any(l => l.StartsWith("f ") && l.Split(' ').Skip(1).Any(tok => tok.Split('/')[0] == "0"));
            if (zeroBased) notes.Add("The OBJ uses 0-based face indices (non-standard); they were read correctly.");

            var vertices = new List<Vector3>();
            var parts = new Dictionary<string, Part>();
            Part current = null;
            int material = 0;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.StartsWith("v "))
                {
                    string[] t = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    vertices.Add(new Vector3(F(t[1]), F(t[2]), F(t[3])));
                }
                else if (line.StartsWith("o ") || line.StartsWith("g "))
                {
                    string name = line.Substring(2).Trim();
                    if (!parts.TryGetValue(name, out current)) parts[name] = current = new Part { Name = name };
                }
                else if (line.StartsWith("usemtl "))
                {
                    material = Mathf.Max(0, materialNames.IndexOf(line.Substring(7).Trim()));
                }
                else if (line.StartsWith("f "))
                {
                    if (current == null) parts["Default"] = current = new Part { Name = "Default" };
                    string[] t = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    var face = new Vector3[t.Length - 1];
                    for (int i = 1; i < t.Length; i++)
                    {
                        int index = int.Parse(t[i].Split('/')[0], CultureInfo.InvariantCulture);
                        index = index < 0 ? vertices.Count + index : zeroBased ? index : index - 1;
                        face[i - 1] = vertices[index];
                    }
                    if (face.Length >= 3)
                    {
                        current.Faces.Add(face);
                        current.FaceMaterial.Add(material);
                    }
                }
            }
            return parts;
        }

        static Color Tint(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
