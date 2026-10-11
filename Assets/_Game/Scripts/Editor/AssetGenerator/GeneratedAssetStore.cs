using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Saves generated meshes, the shared palette material and prefabs as persistent assets.
    /// Safety rules: only writes "Gen_*" assets inside <see cref="Root"/>; never overwrites an asset
    /// that lacks the generator label; asks once before replacing; replaces meshes in place so
    /// existing prefab and scene references keep their GUIDs.
    /// </summary>
    public static class GeneratedAssetStore
    {
        public const string Root = "Assets/_Game/Generated";
        public const string MeshFolder = Root + "/Meshes";
        public const string MaterialFolder = Root + "/Materials";
        public const string PrefabFolder = Root + "/Prefabs";
        public const string PreviewFolder = Root + "/Preview";
        public const string Label = "GreenhavenGenerated";

        const string PaletteTexturePath = MaterialFolder + "/T_Gen_Palette.png";
        const string PaletteMaterialPath = MaterialFolder + "/M_Gen_Palette.mat";

        public struct Request
        {
            public IAssetGenerator Generator;
            public int Count;
        }

        public static string AssetName(IAssetGenerator generator, int seed, int variant) => $"Gen_{generator.Prefix}_s{seed}_v{variant:00}";
        static string MeshPath(string name) => $"{MeshFolder}/{name}.asset";
        static string PrefabPath(string name) => $"{PrefabFolder}/{name}.prefab";

        /// <summary>Generates and saves the requested assets. Returns a human-readable report.</summary>
        public static string Generate(IReadOnlyList<Request> requests, GeneratorSettings settings)
        {
            var planned = new List<(IAssetGenerator gen, int variant, string name)>();
            foreach (Request r in requests)
                for (int v = 0; v < r.Count; v++)
                    planned.Add((r.Generator, v, AssetName(r.Generator, settings.Seed, v)));
            if (planned.Count == 0) return "Nothing selected.";

            // Safety: refuse to touch anything at our target paths that the generator did not create.
            var existing = new List<string>();
            var foreign = new List<string>();
            foreach (var p in planned)
            {
                foreach (string path in new[] { MeshPath(p.name), PrefabPath(p.name) })
                {
                    Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null) continue;
                    (IsGenerated(asset) ? existing : foreign).Add(path);
                }
            }
            if (foreign.Count > 0)
            {
                string list = string.Join("\n", foreign.Take(10));
                EditorUtility.DisplayDialog("Generation stopped",
                    $"These paths are occupied by assets the generator did not create, so nothing was changed:\n\n{list}", "OK");
                return "Stopped: target paths are occupied by non-generated assets:\n" + string.Join("\n", foreign);
            }
            if (existing.Count > 0)
            {
                string list = string.Join("\n", existing.Take(12)) + (existing.Count > 12 ? $"\n…and {existing.Count - 12} more" : "");
                if (!EditorUtility.DisplayDialog("Replace generated assets?",
                        $"{existing.Count} generated asset(s) with seed {settings.Seed} already exist and will be replaced " +
                        $"(prefabs and scenes using them will update):\n\n{list}", "Replace", "Cancel"))
                    return "Cancelled. No assets were changed.";
            }

            EnsureFolders();
            Material material = EnsurePaletteMaterial();
            var report = new StringBuilder();
            int created = 0, replaced = 0, triangles = 0;

            try
            {
                for (int i = 0; i < planned.Count; i++)
                {
                    var (gen, variant, name) = planned[i];
                    EditorUtility.DisplayProgressBar("Generating Greenhaven assets", name, i / (float)planned.Count);

                    GeneratedMesh generated = gen.Build(settings, variant, name);
                    bool existed = AssetDatabase.LoadMainAssetAtPath(MeshPath(name)) != null;
                    Mesh meshAsset = SaveMesh(generated.Mesh, MeshPath(name), settings);
                    SavePrefab(name, meshAsset, material, generated);

                    triangles += meshAsset.triangles.Length / 3;
                    if (existed) replaced++;
                    else created++;
                    report.AppendLine($"{name}  ({gen.StyleName(variant)}, {meshAsset.triangles.Length / 3} tris)");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            report.Insert(0, $"Generated {planned.Count} asset(s): {created} new, {replaced} replaced, {triangles} triangles total.\n");
            return report.ToString();
        }

        static Mesh SaveMesh(Mesh fresh, string path, GeneratorSettings settings)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh asset;
            if (existing != null)
            {
                // Copy into the existing asset so its GUID (and every reference to it) survives.
                existing.Clear();
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(existing);
                asset = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(fresh, path);
                asset = fresh;
            }
            AssetDatabase.SetLabels(asset, new[] { Label });
            AssetDatabase.SaveAssetIfDirty(asset);

            // Remember the settings so validation can regenerate and compare.
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null && importer.userData != JsonUtility.ToJson(settings))
            {
                importer.userData = JsonUtility.ToJson(settings);
                importer.SaveAndReimport();
                asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
            return asset;
        }

        static void SavePrefab(string name, Mesh mesh, Material material, GeneratedMesh generated)
        {
            var go = new GameObject(name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;

                if (generated.Collider == ColliderKind.ConvexMesh)
                {
                    MeshCollider mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.convex = true;
                }
                else
                {
                    // Only the trunk is solid: cheap, and the player can brush through foliage.
                    CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                    capsule.radius = generated.TrunkRadius;
                    capsule.height = Mathf.Max(generated.TrunkHeight, generated.TrunkRadius * 2f);
                    capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                }
                // Scenery: eligible for static batching when placed in a scene.
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

                string path = PrefabPath(name);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
                if (!success || prefab == null) Debug.LogError($"Greenhaven generator: failed to save prefab {path}.");
                else AssetDatabase.SetLabels(prefab, new[] { Label });
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        public static bool IsGenerated(Object asset) => asset != null && AssetDatabase.GetLabels(asset).Contains(Label);

        public static void EnsureFolders()
        {
            foreach (string folder in new[] { Root, MeshFolder, MaterialFolder, PrefabFolder, PreviewFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        /// <summary>The single shared material: URP Simple Lit sampling the 8×8 palette with point filtering.</summary>
        public static Material EnsurePaletteMaterial()
        {
            EnsureFolders();

            // The palette is fixed data; only rewrite the PNG if its content changed.
            Texture2D source = GenPalette.CreateTexture();
            byte[] png = source.EncodeToPNG();
            Object.DestroyImmediate(source);
            string fullPath = Path.GetFullPath(PaletteTexturePath);
            if (!File.Exists(fullPath) || !File.ReadAllBytes(fullPath).SequenceEqual(png))
            {
                File.WriteAllBytes(fullPath, png);
                AssetDatabase.ImportAsset(PaletteTexturePath, ImportAssetOptions.ForceUpdate);
            }
            if (AssetImporter.GetAtPath(PaletteTexturePath) is TextureImporter ti)
            {
                bool dirty = ti.filterMode != FilterMode.Point || ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Uncompressed
                             || ti.wrapMode != TextureWrapMode.Clamp || ti.npotScale != TextureImporterNPOTScale.None;
                if (dirty)
                {
                    ti.filterMode = FilterMode.Point;
                    ti.mipmapEnabled = false;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.SaveAndReimport();
                }
            }
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexturePath);
            AssetDatabase.SetLabels(palette, new[] { Label });

            var material = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = "M_Gen_Palette" };
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
                if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
                AssetDatabase.CreateAsset(material, PaletteMaterialPath);
                AssetDatabase.SetLabels(material, new[] { Label });
            }
            // Keep user tweaks (smoothness etc.); just make sure the palette is assigned.
            if (material.GetTexture("_BaseMap") != palette)
            {
                material.SetTexture("_BaseMap", palette);
                material.mainTexture = palette;
                material.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        /// <summary>
        /// Checks every generated mesh and prefab: mesh structure, persistent references, and that
        /// regenerating with the stored settings reproduces identical vertices (determinism).
        /// </summary>
        public static string ValidateAll()
        {
            var problems = new List<string>();
            int meshes = 0, prefabs = 0, deterministic = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!IsGenerated(mesh)) continue;
                meshes++;
                MeshValidator.Validate(mesh, problems);

                if (TryParseName(mesh.name, out IAssetGenerator gen, out int seed, out int variant))
                {
                    string json = AssetImporter.GetAtPath(path)?.userData;
                    if (string.IsNullOrEmpty(json))
                    {
                        problems.Add($"{mesh.name}: no stored settings; cannot check determinism.");
                        continue;
                    }
                    GeneratorSettings settings = JsonUtility.FromJson<GeneratorSettings>(json);
                    GeneratedMesh again = gen.Build(settings, variant, mesh.name);
                    if (SameVertices(mesh, again.Mesh)) deterministic++;
                    else problems.Add($"{mesh.name}: regenerating with seed {seed} produced different geometry.");
                    Object.DestroyImmediate(again.Mesh);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!IsGenerated(prefab)) continue;
                prefabs++;
                var filter = prefab.GetComponent<MeshFilter>();
                var renderer = prefab.GetComponent<MeshRenderer>();
                if (filter == null || !IsPersistent(filter.sharedMesh)) problems.Add($"{prefab.name}: MeshFilter mesh is missing or not a saved asset.");
                if (renderer == null || renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(m => !IsPersistent(m)))
                    problems.Add($"{prefab.name}: material is missing or not a saved asset.");
                var meshCollider = prefab.GetComponent<MeshCollider>();
                if (meshCollider != null && !IsPersistent(meshCollider.sharedMesh)) problems.Add($"{prefab.name}: MeshCollider mesh is not a saved asset.");
                if (prefab.GetComponent<Collider>() == null) problems.Add($"{prefab.name}: no collider.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Checked {meshes} mesh(es) and {prefabs} prefab(s); {deterministic} mesh(es) regenerated identically from their seed.");
            if (problems.Count == 0) sb.AppendLine("No problems found.");
            else
            {
                sb.AppendLine($"{problems.Count} problem(s):");
                foreach (string p in problems) sb.AppendLine(" • " + p);
            }
            return sb.ToString();
        }

        static bool IsPersistent(Object o) => o != null && AssetDatabase.Contains(o) && AssetDatabase.GetAssetPath(o).StartsWith(Root);

        static bool TryParseName(string name, out IAssetGenerator gen, out int seed, out int variant)
        {
            gen = null;
            seed = variant = 0;
            // Gen_{Prefix}_s{seed}_v{variant}
            string[] parts = name.Split('_');
            if (parts.Length != 4 || parts[0] != "Gen") return false;
            gen = Generators.ForPrefix(parts[1]);
            return gen != null
                   && parts[2].StartsWith("s") && int.TryParse(parts[2].Substring(1), out seed)
                   && parts[3].StartsWith("v") && int.TryParse(parts[3].Substring(1), out variant);
        }

        public static bool SameVertices(Mesh a, Mesh b)
        {
            Vector3[] va = a.vertices, vb = b.vertices;
            if (va.Length != vb.Length || a.triangles.Length != b.triangles.Length) return false;
            for (int i = 0; i < va.Length; i++)
                if ((va[i] - vb[i]).sqrMagnitude > 1e-10f) return false;
            return true;
        }
    }
}
