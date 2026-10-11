using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Wildlife;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.Wildlife
{
    /// <summary>
    /// Greenhaven ▸ Wildlife ▸ Generate Wildlife: builds the five original animals (and their variants) as
    /// skinned meshes on one shared palette material and saves them as prefabs with the rig, procedural
    /// motion and behaviour components. Only labelled generated assets are replaced (after confirmation);
    /// species tuning assets are created once and never overwritten.
    /// </summary>
    public static class WildlifeGenerator
    {
        public const string Root = GeneratedAssetStore.Root + "/Wildlife";
        public const string MeshFolder = Root + "/Meshes", PrefabFolder = Root + "/Prefabs", SpeciesFolder = Root + "/Species", PreviewFolder = Root + "/Preview";
        public static readonly AnimalKind[] Kinds = { AnimalKind.Fox, AnimalKind.Deer, AnimalKind.Rabbit, AnimalKind.Goat, AnimalKind.Bird };

        public static string PrefabPath(AnimalKind kind, int variant) => $"{PrefabFolder}/{AnimalModels.PrefabName(kind)}_v{variant:00}.prefab";
        static string MeshName(AnimalKind kind, int seed, int variant) => $"Gen_Wild{kind}_s{seed}_v{variant:00}";
        public static string SpeciesPath(AnimalKind kind) => $"{SpeciesFolder}/WildlifeSpecies_{kind}.asset";

        public static void EnsureFolders()
        {
            GeneratedAssetStore.EnsureFolders();
            foreach (string folder in new[] { Root, MeshFolder, PrefabFolder, SpeciesFolder, PreviewFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                int slash = folder.LastIndexOf('/');
                AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            }
        }

        [MenuItem("Greenhaven/Wildlife/Generate Wildlife", priority = 70)]
        static void GenerateMenu()
        {
            string report = Generate(WildlifeGeneratorSettings.LoadOrCreate(), Kinds);
            Debug.Log("Greenhaven — Generate Wildlife:\n" + report);
            EditorUtility.DisplayDialog("Generate Wildlife", report.Length > 1400 ? report.Substring(0, 1400) + "…" : report, "OK");
        }

        [MenuItem("Greenhaven/Wildlife/Generate Fox Only (benchmark)", priority = 71)]
        static void GenerateFoxMenu()
        {
            string report = Generate(WildlifeGeneratorSettings.LoadOrCreate(), new[] { AnimalKind.Fox });
            Debug.Log("Greenhaven — Generate Fox:\n" + report);
            EditorUtility.DisplayDialog("Generate Fox", report, "OK");
        }

        [MenuItem("Greenhaven/Wildlife/Edit Wildlife Settings", priority = 72)]
        static void EditSettingsMenu() => Selection.activeObject = WildlifeGeneratorSettings.LoadOrCreate();

        public static Material EnsureMaterial(WildlifeGeneratorSettings s) => GeneratedAssetStore.EnsurePaletteMaterial(s.ToPalette());

        public static string Generate(WildlifeGeneratorSettings s, IEnumerable<AnimalKind> kinds)
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";
            EnsureFolders();
            var planned = new List<(AnimalKind kind, int variant)>();
            foreach (AnimalKind kind in kinds)
                for (int v = 0; v < s.VariantCount(kind); v++) planned.Add((kind, v));

            // Safety: never touch files at our paths that the generator did not create.
            var existing = new List<string>();
            foreach ((AnimalKind kind, int variant) in planned)
            {
                foreach (string path in new[] { PrefabPath(kind, variant), $"{MeshFolder}/{MeshName(kind, s.seed, variant)}.asset" })
                {
                    Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null) continue;
                    if (!GeneratedAssetStore.IsGenerated(asset)) return $"Stopped: {path} exists but was not made by the generator. Move or rename it first.";
                    existing.Add(path);
                }
            }
            if (existing.Count > 0 && !EditorUtility.DisplayDialog("Replace generated wildlife?",
                    $"{existing.Count} generated wildlife asset(s) will be replaced in place (references in scenes keep working). " +
                    "Species tuning (Generated/Wildlife/Species) is not changed.", "Replace", "Cancel"))
                return "Cancelled. Nothing was changed.";

            Material material = EnsureMaterial(s);
            Palette palette = s.ToPalette();
            var report = new StringBuilder();
            int totalTriangles = 0;
            foreach ((AnimalKind kind, int variant) in planned)
            {
                WildlifeSpecies species = EnsureSpecies(kind);
                AnimalBuilder builder = AnimalModels.Build(kind, variant, s, palette);
                string meshName = MeshName(kind, s.seed, variant);
                Mesh mesh = GeneratedAssetStore.SaveMesh(builder.ToMesh(meshName), $"{MeshFolder}/{meshName}.asset");
                string prefabPath = PrefabPath(kind, variant);
                GameObject root = Assemble($"{AnimalModels.PrefabName(kind)}_v{variant:00}", builder, mesh, material, species, variant);
                try
                {
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
                    if (!ok || prefab == null)
                    {
                        report.AppendLine($"⚠ Could not save {prefabPath}.");
                        continue;
                    }
                    AssetDatabase.SetLabels(prefab, new[] { GeneratedAssetStore.Label });
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
                int triangles = mesh.triangles.Length / 3;
                totalTriangles += triangles;
                string problem = Validate(prefabPath);
                report.AppendLine($"{AnimalModels.PrefabName(kind)} v{variant:00} ({AnimalModels.StyleName(kind, variant)}): {triangles} triangles, {builder.BoneCount} bones{(problem == null ? "" : " ⚠ " + problem)}");
            }
            AssetDatabase.SaveAssets();
            report.Insert(0, $"Generated {planned.Count} animal prefab(s) in {PrefabFolder} ({totalTriangles} triangles in total), one shared material M_Gen_WildlifePalette.\n");
            return report.ToString();
        }

        /// <summary>Creates the species tuning asset with defaults if it does not exist yet (never overwrites it).</summary>
        public static WildlifeSpecies EnsureSpecies(AnimalKind kind)
        {
            EnsureFolders();
            string path = SpeciesPath(kind);
            var species = AssetDatabase.LoadAssetAtPath<WildlifeSpecies>(path);
            if (species != null) return species;
            species = ScriptableObject.CreateInstance<WildlifeSpecies>();
            species.ApplyDefaults(kind);
            AssetDatabase.CreateAsset(species, path);
            return species;
        }

        /// <summary>Prefab hierarchy: root (agent, motion, rig) → bones + "Model" (skinned renderer). No colliders.</summary>
        public static GameObject Assemble(string name, AnimalBuilder builder, Mesh mesh, Material material, WildlifeSpecies species, int variant)
        {
            var root = new GameObject(name);
            Transform[] bones = builder.CreateBones(root.transform);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer smr = model.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterial = material;
            smr.quality = SkinQuality.Bone1;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = ShadowCastingMode.On;
            Bounds local = mesh.bounds;
            local.center -= bones[0].localPosition;
            local.Expand(local.size.magnitude * 0.35f);
            smr.localBounds = local;

            WildlifeAgent agent = root.AddComponent<WildlifeAgent>(); // adds AnimalMotion and AnimalRig
            agent.Configure(species, 1000 + variant, null);
            AnimalRig rig = root.GetComponent<AnimalRig>();
            rig.meshRenderer = smr;
            rig.body = builder.Find(bones, AnimalModels.Body);
            rig.neck = builder.Find(bones, AnimalModels.Neck);
            rig.head = builder.Find(bones, AnimalModels.Head);
            rig.ears = Existing(builder, bones, AnimalModels.EarL, AnimalModels.EarR);
            rig.tail = Existing(builder, bones, AnimalModels.Tail0, AnimalModels.Tail1);
            rig.legs = species.kind == AnimalKind.Bird
                ? Existing(builder, bones, AnimalModels.LegL, AnimalModels.LegR)
                : Existing(builder, bones, AnimalModels.LegFL, AnimalModels.LegFR, AnimalModels.LegHL, AnimalModels.LegHR);
            rig.wings = Existing(builder, bones, AnimalModels.WingL, AnimalModels.WingR);
            return root;
        }

        static Transform[] Existing(AnimalBuilder builder, Transform[] bones, params string[] names) =>
            names.Select(n => builder.Find(bones, n)).Where(t => t != null).ToArray();

        /// <summary>Checks a saved prefab: renderer, bones, bind poses, rig and species. Null if fine.</summary>
        public static string Validate(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return "prefab missing";
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) return "no skinned mesh";
            if (smr.bones.Length != smr.sharedMesh.bindposes.Length || smr.bones.Any(b => b == null)) return "bones and bind poses do not match";
            if (smr.sharedMesh.boneWeights.Length != smr.sharedMesh.vertexCount) return "missing bone weights";
            if (smr.sharedMaterial == null) return "no material";
            var rig = prefab.GetComponent<AnimalRig>();
            if (rig == null || rig.body == null || rig.head == null || rig.legs.Length < 2) return "rig incomplete";
            var agent = prefab.GetComponent<WildlifeAgent>();
            if (agent == null || agent.Species == null) return "no species";
            if (prefab.GetComponentsInChildren<Collider>().Length > 0) return "unexpected collider";
            return null;
        }

        public static GameObject LoadPrefab(AnimalKind kind, int variant) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(kind, variant));

        public static int VariantsOnDisk(AnimalKind kind)
        {
            int n = 0;
            while (n < 8 && LoadPrefab(kind, n) != null) n++;
            return n;
        }
    }
}
