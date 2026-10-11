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
        const float TargetHeight = 1.65f;
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

        public static GameObject FindPrefab() => AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(AssetName));

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

        public static string Rig()
        {
            string folder = SourceFolder();
            if (folder == null)
                return $"Could not find {ObjFile} and {MtlFile} in {string.Join(" or ", SourceFolders)} (relative to the project folder).";

            string meshPath = GeneratedAssetStore.MeshPath(AssetName);
            string prefabPath = GeneratedAssetStore.PrefabPath(AssetName);
            string avatarPath = $"{GeneratedAssetStore.MeshFolder}/{AssetName}_Avatar.asset";
            var existing = new List<string>();
            foreach (string path in new[] { meshPath, prefabPath, avatarPath })
            {
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                if (!GeneratedAssetStore.IsGenerated(asset)) return $"Stopped: {path} exists but was not created by this tool.";
                existing.Add(path);
            }
            if (existing.Count > 0 && !EditorUtility.DisplayDialog("Replace rigged adventurer?",
                    "The rigged adventurer already exists and will be rebuilt from the OBJ (scenes using it will update).", "Replace", "Cancel"))
                return "Cancelled. Rigged adventurer unchanged.";

            GeneratedAssetStore.EnsureFolders();
            HeroGenerator.Result r;
            List<string> notes;
            Palette palette;
            try
            {
                r = BuildInMemory(folder, out palette, out notes);
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
        public static HeroGenerator.Result BuildInMemory(string folder, out Palette palette, out List<string> notes)
        {
            notes = new List<string>();
            List<(string name, Color colour)> materials = ParseMtl(Path.Combine(folder, MtlFile));
            Dictionary<string, Part> parts = ParseObj(Path.Combine(folder, ObjFile), materials.Select(m => m.name).ToList(), notes);
            foreach (string required in RequiredParts)
                if (!parts.ContainsKey(required)) throw new InvalidDataException($"part '{required}' is missing from the OBJ");

            // One palette row per MTL colour: darker for undersides, lighter for tops (shade 4 ≈ the MTL colour).
            palette = new Palette("AdventurerPalette", materials.Select(m => (Tint(m.colour, 0.72f), Tint(m.colour, 1.3f))).ToArray());

            // 1. Pose: straighten the arms into a T-pose (model units).
            var joints = new Dictionary<string, Vector3>();
            foreach (float side in new[] { -1f, 1f }) StraightenArm(parts, side, joints);

            // 2. Scale to game height (feet already at y ≈ 0).
            float maxY = parts.Values.SelectMany(p => p.Points).Max(v => v.y);
            float s = TargetHeight / maxY;
            foreach (Part part in parts.Values)
                for (int f = 0; f < part.Faces.Count; f++)
                    for (int i = 0; i < part.Faces[f].Length; i++) part.Faces[f][i] *= s;
            foreach (string key in joints.Keys.ToList()) joints[key] *= s;

            // 3. Skeleton (world positions, identity rotations) from measured parts.
            Vector3 C(string name) => parts[name].Centroid;
            float hipsY = 0.75f * s, spineY = 0.9f * s, chestY = 1.08f * s, neckY = 1.29f * s, headY = 1.42f * s;
            var world = new Dictionary<string, Vector3>
            {
                ["Hips"] = new Vector3(0f, hipsY, 0f),
                ["Spine"] = new Vector3(0f, spineY, 0f),
                ["Chest"] = new Vector3(0f, chestY, 0f),
                ["Neck"] = new Vector3(0f, neckY, 0f),
                ["Head"] = new Vector3(0f, headY, 0f),
                ["LeftEye"] = C("Left_EyeWhite"),
                ["RightEye"] = C("Right_EyeWhite"),
                ["Backpack"] = C("BackpackMain"),
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
                world[pre + "UpperLeg"] = new Vector3(legX, 0.7f * s, 0f);
                world[pre + "LowerLeg"] = new Vector3(legX, 0.45f * s, 0f);
                world[pre + "Foot"] = new Vector3(footX, 0.12f * s, 0f);
                world[pre + "Toes"] = new Vector3(footX, 0.04f * s, 0.15f * s);
            }

            string[] order =
            {
                "Hips", "Spine", "Chest", "Neck", "Head", "LeftEye", "RightEye",
                "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
                "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes", "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes", "Backpack",
            };
            var parent = new Dictionary<string, string>
            {
                ["Hips"] = null, ["Spine"] = "Hips", ["Chest"] = "Spine", ["Neck"] = "Chest", ["Head"] = "Neck",
                ["LeftEye"] = "Head", ["RightEye"] = "Head", ["Backpack"] = "Chest",
                ["LeftShoulder"] = "Chest", ["LeftUpperArm"] = "LeftShoulder", ["LeftLowerArm"] = "LeftUpperArm", ["LeftHand"] = "LeftLowerArm",
                ["RightShoulder"] = "Chest", ["RightUpperArm"] = "RightShoulder", ["RightLowerArm"] = "RightUpperArm", ["RightHand"] = "RightLowerArm",
                ["LeftUpperLeg"] = "Hips", ["LeftLowerLeg"] = "LeftUpperLeg", ["LeftFoot"] = "LeftLowerLeg", ["LeftToes"] = "LeftFoot",
                ["RightUpperLeg"] = "Hips", ["RightLowerLeg"] = "RightUpperLeg", ["RightFoot"] = "RightLowerLeg", ["RightToes"] = "RightFoot",
            };

            var root = new GameObject(AssetName);
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
                mb.Skin = WeightsFor(part.Name, s, B, notes);
                Vector3 centre = part.Centroid;
                for (int f = 0; f < part.Faces.Count; f++)
                {
                    Vector3[] face = part.Faces[f];
                    Vector3 faceCentre = face.Aggregate(Vector3.zero, (a, v) => a + v) / face.Length;
                    Vector3 outward = faceCentre - centre;
                    if (outward.sqrMagnitude < 1e-8f) outward = Vector3.Cross(face[1] - face[0], face[2] - face[0]);
                    int row = part.FaceMaterial[f];
                    for (int i = 1; i < face.Length - 1; i++)
                    {
                        Vector3 n = MeshBuilder.OrientedNormal(face[0], face[i], face[i + 1], outward);
                        mb.Triangle(face[0], face[i], face[i + 1], outward, palette.UV(row, 4 + Palette.SlopeShade(n)));
                    }
                }
            }
            mb.Skin = null;

            Mesh mesh = mb.ToMesh(AssetName);
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
            avatar.name = AssetName + "_Avatar";
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
        static Func<Vector3, BoneWeight> WeightsFor(string part, float s, Func<string, int> bone, List<string> notes)
        {
            string side = part.StartsWith("Left_") ? "Left" : part.StartsWith("Right_") ? "Right" : null;
            string bare = side == null ? part : part.Substring(side.Length + 1);
            BoneWeight R(string b) => HeroGenerator.Rigid(bone(b));

            switch (bare)
            {
                case "Head": case "Ear": case "HairCap": case "Nose": case "Smile": case "Eyebrow":
                    return _ => R("Head");
                case "EyeWhite": case "Iris": case "Pupil":
                    return _ => R(side + "Eye");
                case "Neck":
                    return HeroGenerator.Chain(p => p.y, new[] { 1.31f * s, 1.45f * s }, new[] { bone("Chest"), bone("Neck"), bone("Head") }, 0.03f * s);
                case "TunicBody": case "ShirtFront":
                    return HeroGenerator.Chain(p => p.y, new[] { 0.86f * s, 1.0f * s }, new[] { bone("Hips"), bone("Spine"), bone("Chest") }, 0.05f * s);
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
                    return HeroGenerator.Chain(p => -p.y, new[] { -0.69f * s, -0.45f * s },
                        new[] { bone("Hips"), bone(side + "UpperLeg"), bone(side + "LowerLeg") }, 0.04f * s);
                case "Cuff":
                    return _ => R(side + "LowerLeg");
                case "Boot": case "BootSole":
                {
                    Func<Vector3, BoneWeight> ankle = HeroGenerator.Chain(p => -p.y, new[] { -0.15f * s }, new[] { bone(side + "LowerLeg"), bone(side + "Foot") }, 0.03f * s);
                    return p => p.z > 0.13f * s && p.y < 0.12f * s ? R(side + "Toes") : ankle(p);
                }
            }
            if (bare.StartsWith("HairLock")) return _ => R("Head");
            notes.Add($"Unrecognised part '{part}' was attached to the chest.");
            return _ => R("Chest");
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
