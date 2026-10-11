using System.Collections.Generic;
using System.IO;
using System.Linq;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Wind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Greenhaven.Editor.WindTools
{
    /// <summary>Greenhaven ▸ Wind: add wind to a scene, build a wind test scene, and switch presets in Play mode.</summary>
    public static class WindSetup
    {
        public const string TestScenePath = "Assets/_Game/Scenes/WindTest.unity";

        [MenuItem("Greenhaven/Wind/Add Wind To Open Scene", priority = 300)]
        static void AddMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Wind", "Stop Play mode first.", "OK");
                return;
            }
            bool existed = Object.FindAnyObjectByType<WindManager>() != null;
            Selection.activeObject = AddToScene();
            EditorUtility.DisplayDialog("Wind", existed ? "This scene already has a WindManager (selected)."
                : "Wind added (selected). Save the scene to keep it. Plants sway only if they use the wind material — regenerate trees/grass if needed.", "OK");
        }

        /// <summary>Adds (or returns the existing) WindManager to the open scene.</summary>
        public static WindManager AddToScene()
        {
            WindManager existing = Object.FindAnyObjectByType<WindManager>();
            if (existing != null) return existing;
            var go = new GameObject("Wind");
            var manager = go.AddComponent<WindManager>();
            Undo.RegisterCreatedObjectUndo(go, "Add Wind");
            EditorSceneManager.MarkSceneDirty(go.scene);
            return manager;
        }

        // ───────────────────────────── Test scene ─────────────────────────────

        static readonly AssetCategory[] WindCategories = { AssetCategory.BroadleafTree, AssetCategory.PineTree, AssetCategory.Grass };

        static GameObject Prefab(AssetCategory category, int seed, int variant) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(GeneratedAssetStore.AssetName(Generators.For(category), seed, variant)));

        static bool UsesWindMaterial(GameObject prefab)
        {
            var renderer = prefab != null ? prefab.GetComponent<MeshRenderer>() : null;
            return renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "Greenhaven/WindLit";
        }

        [MenuItem("Greenhaven/Wind/Create Wind Test Scene", priority = 301)]
        static void CreateTestSceneMenu()
        {
            string result = CreateTestScene(GeneratorPrefs.Settings);
            Debug.Log("Greenhaven — Wind test scene:\n" + result);
            EditorUtility.DisplayDialog("Wind Test Scene", result, "OK");
        }

        public static string CreateTestScene(GeneratorSettings settings)
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";

            // Trees and grass must exist and carry wind data (older assets used the static material).
            bool ready = WindCategories.All(c => Enumerable.Range(0, 3).All(v => UsesWindMaterial(Prefab(c, settings.Seed, v))));
            if (!ready)
            {
                if (!EditorUtility.DisplayDialog("Generate wind-ready vegetation?",
                        $"Trees and grass for seed {settings.Seed} are missing or were generated before wind support. " +
                        "Regenerate pine, broadleaf and grass now? (Existing ones are replaced in place, so scenes using them update.)",
                        "Regenerate", "Cancel"))
                    return "Cancelled: no wind-ready vegetation.";
                var requests = WindCategories.Select(c => new GeneratedAssetStore.Request { Generator = Generators.For(c), Count = 3 }).ToList();
                string report = GeneratedAssetStore.Generate(requests, settings);
                if (!WindCategories.All(c => UsesWindMaterial(Prefab(c, settings.Seed, 0)))) return report;
            }

            if (File.Exists(TestScenePath) && !EditorUtility.DisplayDialog("Rebuild wind test scene?",
                    $"{TestScenePath} will be rebuilt. No other scene is changed.", "Rebuild", "Cancel"))
                return "Cancelled.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            Phase1SceneBuilder.CreateMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_Grass.mat");

            var rng = new System.Random(settings.Seed);
            int placed = 0;
            void Place(GameObject prefab, Vector3 position, float yaw, float scale = 1f)
            {
                if (prefab == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = Vector3.one * scale;
                placed++;
            }

            for (int v = 0; v < 3; v++)
            {
                Place(Prefab(AssetCategory.BroadleafTree, settings.Seed, v), new Vector3(-7f + v * 7f, 0f, 9f), v * 70f);
                Place(Prefab(AssetCategory.PineTree, settings.Seed, v), new Vector3(-10f + v * 10f, 0f, 17f), v * 40f);
            }
            // A grass meadow in front of the camera.
            for (int x = 0; x < 12; x++)
                for (int z = 0; z < 6; z++)
                {
                    var p = new Vector3(-9f + x * 1.6f + (float)rng.NextDouble() * 0.8f, 0f, -1f + z * 1.1f + (float)rng.NextDouble() * 0.5f);
                    Place(Prefab(AssetCategory.Grass, settings.Seed, rng.Next(3)), p, (float)rng.NextDouble() * 360f, 0.85f + (float)rng.NextDouble() * 0.4f);
                }

            AddToScene();
            WeatherTools.WeatherSetup.AddToScene(); // to test the weather → wind link

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.85f, 0.96f);
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.2f, -9f), Quaternion.Euler(14f, 0f, 0f));
            cam.gameObject.AddComponent<AudioListener>();
            // Weather was added before the camera existed; point its sky at this camera.
            var weather = Object.FindAnyObjectByType<Greenhaven.Weather.WeatherManager>();
            if (weather != null)
            {
                var so = new SerializedObject(weather);
                so.FindProperty("skyCamera").objectReferenceValue = cam;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene, TestScenePath);
            return $"Wind test scene saved to {TestScenePath} with {placed} plants. Press Play, then use Greenhaven ▸ Wind (or the Wind object's inspector).";
        }

        // ───────────────────────────── Tree comparison ─────────────────────────────

        public const string ComparisonScenePath = "Assets/_Game/Scenes/TreeComparison.unity";
        const string ComparisonFolder = GeneratedAssetStore.Root + "/Comparison";

        [MenuItem("Greenhaven/Wind/Create Tree Comparison Scene", priority = 302)]
        static void ComparisonMenu()
        {
            string result = CreateComparisonScene(GeneratorPrefs.Settings);
            Debug.Log("Greenhaven — Tree comparison:\n" + result);
            EditorUtility.DisplayDialog("Tree Comparison", result, "OK");
        }

        /// <summary>
        /// Side-by-side broadleaf trees: original canopy, improved canopy (static material) and improved
        /// canopy with wind. Comparison meshes live in Generated/Comparison and never replace game prefabs.
        /// </summary>
        public static string CreateComparisonScene(GeneratorSettings settings)
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";
            if (File.Exists(ComparisonScenePath) && !EditorUtility.DisplayDialog("Rebuild tree comparison scene?",
                    $"{ComparisonScenePath} and its comparison meshes in {ComparisonFolder} will be rebuilt. Game prefabs are not touched.",
                    "Rebuild", "Cancel"))
                return "Cancelled.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            GeneratedAssetStore.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(ComparisonFolder)) AssetDatabase.CreateFolder(GeneratedAssetStore.Root, "Comparison");
            Material staticMaterial = GeneratedAssetStore.EnsurePaletteMaterial(GenPalette.Nature);
            Material windMaterial = GeneratedAssetStore.EnsureWindMaterial(GenPalette.Nature, AssetCategory.BroadleafTree);

            var generator = new BroadleafTreeGenerator();
            var original = new Mesh[3];
            var improved = new Mesh[3];
            int originalTris = 0, improvedTris = 0;
            for (int v = 0; v < 3; v++)
            {
                string a = $"Cmp_BroadleafV1_s{settings.Seed}_v{v:00}", b = $"Cmp_BroadleafV2_s{settings.Seed}_v{v:00}";
                original[v] = GeneratedAssetStore.SaveMesh(generator.BuildV1(settings, v, a).Mesh, $"{ComparisonFolder}/{a}.asset");
                improved[v] = GeneratedAssetStore.SaveMesh(generator.Build(settings, v, b).Mesh, $"{ComparisonFolder}/{b}.asset");
                originalTris += original[v].triangles.Length / 3;
                improvedTris += improved[v].triangles.Length / 3;
            }
            AssetDatabase.SaveAssets();

            Phase1SceneBuilder.CreateMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_Grass.mat");

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string[] columns = { "Original", "Improved (static)", "Improved + wind" };
            for (int c = 0; c < 3; c++)
            {
                float x = (c - 1) * 9f;
                var label = new GameObject($"Label_{columns[c]}");
                label.transform.position = new Vector3(x, 0.05f, -3f);
                label.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
                TextMesh tm = label.AddComponent<TextMesh>();
                tm.text = columns[c];
                tm.font = font;
                tm.fontSize = 64;
                tm.characterSize = 0.06f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(0.12f, 0.18f, 0.1f);
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;

                for (int v = 0; v < 3; v++)
                {
                    var tree = new GameObject($"{columns[c]} ({generator.StyleName(v)})");
                    tree.transform.position = new Vector3(x, 0f, v * 9f);
                    tree.AddComponent<MeshFilter>().sharedMesh = c == 0 ? original[v] : improved[v];
                    tree.AddComponent<MeshRenderer>().sharedMaterial = c == 2 ? windMaterial : staticMaterial;
                }
            }

            AddToScene();
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.85f, 0.96f);
            cam.transform.SetPositionAndRotation(new Vector3(0f, 7f, -20f), Quaternion.Euler(14f, 0f, 0f));
            cam.gameObject.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, ComparisonScenePath);
            return $"Comparison scene saved to {ComparisonScenePath}. Triangles for 3 styles: original {originalTris}, improved {improvedTris} " +
                   $"(Foliage Detail {settings.Detail:0.00}). Press Play to see the wind column move.";
        }

        // ───────────────────────────── Play-mode test menu ─────────────────────────────

        static WindManager Manager() => Object.FindAnyObjectByType<WindManager>();
        static bool PlayingWithWind() => EditorApplication.isPlaying && Manager() != null;

        [MenuItem("Greenhaven/Wind/Calm", priority = 320)] static void Calm() => Manager().ApplyPreset(WindPreset.Calm);
        [MenuItem("Greenhaven/Wind/Breeze", priority = 321)] static void Breeze() => Manager().ApplyPreset(WindPreset.Breeze);
        [MenuItem("Greenhaven/Wind/Strong", priority = 322)] static void Strong() => Manager().ApplyPreset(WindPreset.Strong);
        [MenuItem("Greenhaven/Wind/Gust Now", priority = 323)] static void Gust() => Manager().TriggerGust();
        [MenuItem("Greenhaven/Wind/Toggle Wind On-Off", priority = 324)] static void Toggle() => Manager().SetEnabled(!Manager().WindEnabled);

        [MenuItem("Greenhaven/Wind/Calm", true)]
        [MenuItem("Greenhaven/Wind/Breeze", true)]
        [MenuItem("Greenhaven/Wind/Strong", true)]
        [MenuItem("Greenhaven/Wind/Gust Now", true)]
        [MenuItem("Greenhaven/Wind/Toggle Wind On-Off", true)]
        static bool ValidatePlayMenu() => PlayingWithWind();
    }
}
