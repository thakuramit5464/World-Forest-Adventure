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


        /// <summary>
        /// Plant types animated by Greenhaven/WindLit, with their material defaults:
        /// (trunk sway m, sway frequency, outer canopy m, inner canopy, gust response, flutter m).
        /// Each type gets ONE shared material, so it can be tuned per type while instances still batch.
        /// </summary>
        public static readonly Dictionary<AssetCategory, (float trunk, float frequency, float canopy, float inner, float gust, float flutter)> WindCategories =
            new Dictionary<AssetCategory, (float, float, float, float, float, float)>
            {
                [AssetCategory.BroadleafTree] = (0.12f, 0.9f, 0.32f, 0.25f, 0.6f, 0.02f),
                [AssetCategory.PineTree] = (0.15f, 0.7f, 0.22f, 0.3f, 0.5f, 0.012f),
                [AssetCategory.Grass] = (0f, 1.4f, 0.16f, 0.1f, 0.8f, 0.01f),
                [AssetCategory.Bush] = (0f, 1.1f, 0.08f, 0.3f, 0.6f, 0.012f),
                [AssetCategory.Flower] = (0f, 1.6f, 0.07f, 0.1f, 0.8f, 0.006f),
            };

        public struct Request
        {
            public IAssetGenerator Generator;
            public int Count;
        }

        public static string AssetName(IAssetGenerator generator, int seed, int variant) => $"Gen_{generator.Prefix}_s{seed}_v{variant:00}";
        public static string MeshPath(string name) => $"{MeshFolder}/{name}.asset";
        public static string PrefabPath(string name) => $"{PrefabFolder}/{name}.prefab";

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
            var materials = new Dictionary<(Palette, AssetCategory?), Material>();
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
                    AssetCategory? windType = WindCategories.ContainsKey(gen.Category) ? gen.Category : (AssetCategory?)null;
                    if (!materials.TryGetValue((gen.Palette, windType), out Material material))
                        materials[(gen.Palette, windType)] = material = windType.HasValue
                            ? EnsureWindMaterial(gen.Palette, windType.Value)
                            : EnsurePaletteMaterial(gen.Palette);
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

        /// <summary>
        /// Saves <paramref name="fresh"/> at <paramref name="path"/>, replacing an existing generated mesh in place
        /// (same GUID). Optional settings are stored for determinism checks.
        /// </summary>
        public static Mesh SaveMesh(Mesh fresh, string path, GeneratorSettings? settings = null)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh asset;
            if (existing != null)
            {
                // Copy into the existing asset so its GUID (and every reference to it) survives.
                CopyMeshData(fresh, existing);
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
            if (settings.HasValue)
            {
                AssetImporter importer = AssetImporter.GetAtPath(path);
                string json = JsonUtility.ToJson(settings.Value);
                if (importer != null && importer.userData != json)
                {
                    importer.userData = json;
                    importer.SaveAndReimport();
                    asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                }
            }
            return asset;
        }

        /// <summary>
        /// Explicit copy through the Mesh API. (EditorUtility.CopySerialized lost the vertex data of
        /// skinned meshes that were in use by a renderer, leaving an empty mesh.)
        /// </summary>
        static void CopyMeshData(Mesh source, Mesh target)
        {
            target.Clear();
            target.indexFormat = source.indexFormat;
            target.vertices = source.vertices;
            target.normals = source.normals;
            target.uv = source.uv;
            var wind = new List<Vector4>();
            source.GetUVs(2, wind);
            if (wind.Count == source.vertexCount) target.SetUVs(2, wind); // wind data for Greenhaven/WindLit
            BoneWeight[] weights = source.boneWeights;
            if (weights.Length == source.vertexCount && weights.Length > 0) target.boneWeights = weights;
            target.bindposes = source.bindposes;
            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i, false);
            target.RecalculateBounds();
        }

        static void SavePrefab(string name, Mesh mesh, Material material, GeneratedMesh generated)
        {
            var go = new GameObject(name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;

                switch (generated.Collider)
                {
                    case ColliderKind.ConvexMesh:
                    case ColliderKind.Mesh:
                        MeshCollider mc = go.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                        mc.convex = generated.Collider == ColliderKind.ConvexMesh;
                        break;
                    case ColliderKind.TrunkCapsule:
                        // Only the trunk is solid: cheap, and the player can brush through foliage.
                        CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                        capsule.radius = generated.TrunkRadius;
                        capsule.height = Mathf.Max(generated.TrunkHeight, generated.TrunkRadius * 2f);
                        capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                        break;
                    case ColliderKind.Boxes:
                        foreach (Bounds b in generated.Boxes ?? new Bounds[0])
                        {
                            BoxCollider box = go.AddComponent<BoxCollider>();
                            box.center = b.center;
                            box.size = b.size;
                        }
                        break;
                    case ColliderKind.None:
                        break; // walk-through decoration
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

        /// <summary>The shared nature material (trees, rocks, bushes, flowers).</summary>
        public static Material EnsurePaletteMaterial() => EnsurePaletteMaterial(GenPalette.Nature);

        /// <summary>
        /// One shared material per palette: URP Simple Lit sampling the palette texture with point filtering.
        /// </summary>
        public static Material EnsurePaletteMaterial(Palette paletteDef)
        {
            EnsureFolders();
            string texturePath = $"{MaterialFolder}/T_Gen_{paletteDef.Name}.png";
            string materialPath = $"{MaterialFolder}/M_Gen_{paletteDef.Name}.mat";

            // The palette is fixed data; only rewrite the PNG if its content changed.
            Texture2D source = paletteDef.CreateTexture();
            byte[] png = source.EncodeToPNG();
            Object.DestroyImmediate(source);
            string fullPath = Path.GetFullPath(texturePath);
            if (!File.Exists(fullPath) || !File.ReadAllBytes(fullPath).SequenceEqual(png))
            {
                File.WriteAllBytes(fullPath, png);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            }
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter ti)
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
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            AssetDatabase.SetLabels(palette, new[] { Label });

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = $"M_Gen_{paletteDef.Name}" };
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
                if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
                AssetDatabase.CreateAsset(material, materialPath);
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
        /// Shared wind material for one plant type (Greenhaven/WindLit), e.g. M_Gen_Wind_Broadleaf. Defaults
        /// come from <see cref="WindCategories"/> on creation; later tuning on the asset is preserved.
        /// </summary>
        public static Material EnsureWindMaterial(Palette paletteDef, AssetCategory type)
        {
            Material palette = EnsurePaletteMaterial(paletteDef); // makes sure the palette texture exists
            string name = $"M_Gen_Wind_{Generators.For(type).Prefix}";
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Greenhaven/WindLit");
                if (shader == null)
                {
                    Debug.LogWarning("Greenhaven: Greenhaven/WindLit shader not found; using the static palette material.");
                    return palette;
                }
                material = new Material(shader) { name = name };
                var d = WindCategories[type];
                material.SetFloat("_TrunkSway", d.trunk);
                material.SetFloat("_SwayFrequency", d.frequency);
                material.SetFloat("_CanopySway", d.canopy);
                material.SetFloat("_InnerCanopy", d.inner);
                material.SetFloat("_GustResponse", d.gust);
                material.SetFloat("_FlutterAmplitude", d.flutter);
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SetLabels(material, new[] { Label });
            }
            Texture texture = palette.GetTexture("_BaseMap");
            if (material.GetTexture("_BaseMap") != texture)
            {
                material.SetTexture("_BaseMap", texture);
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
            var expectedColliders = new Dictionary<string, ColliderKind>();

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
                    expectedColliders[mesh.name] = again.Collider;
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
                var skinned = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinned != null)
                {
                    HeroGenerator.ValidatePrefab(prefab, problems);
                    continue;
                }
                var filter = prefab.GetComponent<MeshFilter>();
                var renderer = prefab.GetComponent<MeshRenderer>();
                if (filter == null || !IsPersistent(filter.sharedMesh)) problems.Add($"{prefab.name}: MeshFilter mesh is missing or not a saved asset.");
                if (renderer == null || renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(m => !IsPersistent(m)))
                    problems.Add($"{prefab.name}: material is missing or not a saved asset.");
                var meshCollider = prefab.GetComponent<MeshCollider>();
                if (meshCollider != null && !IsPersistent(meshCollider.sharedMesh)) problems.Add($"{prefab.name}: MeshCollider mesh is not a saved asset.");
                bool wantsCollider = !expectedColliders.TryGetValue(prefab.name, out ColliderKind kind) || kind != ColliderKind.None;
                if (wantsCollider && prefab.GetComponent<Collider>() == null) problems.Add($"{prefab.name}: no collider.");
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
