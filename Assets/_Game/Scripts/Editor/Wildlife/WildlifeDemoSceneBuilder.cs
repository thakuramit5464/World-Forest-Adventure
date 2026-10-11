using System.IO;
using System.Linq;
using Greenhaven.Collectibles;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Editor.WindTools;
using Greenhaven.Quests;
using Greenhaven.UI;
using Greenhaven.Wildlife;
using Greenhaven.Wind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greenhaven.Editor.Wildlife
{
    /// <summary>
    /// Builds Assets/_Game/Scenes/WildlifeDemo.unity: a small meadow with a path, a copse, a bush thicket,
    /// a rocky mound, a fence and a bench, populated with each species in its habitat, plus the existing
    /// player, camera, HUD, weather and wind. Touches no other scene.
    /// </summary>
    public static class WildlifeDemoSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/WildlifeDemo.unity";
        const float Half = 30f;
        static readonly Vector2 Mound = new Vector2(15f, -13f);
        const float MoundRadius = 8f, MoundHeight = 2.6f;

        [MenuItem("Greenhaven/Wildlife/Create Wildlife Demo Scene", priority = 90)]
        static void Menu()
        {
            string result = Build();
            Debug.Log("Greenhaven — Wildlife demo:\n" + result);
            EditorUtility.DisplayDialog("Wildlife Demo Scene", result.Length > 1400 ? result.Substring(0, 1400) + "…" : result, "OK");
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";
            if (WildlifeGenerator.Kinds.Any(k => WildlifeGenerator.VariantsOnDisk(k) == 0))
            {
                if (!EditorUtility.DisplayDialog("Generate wildlife?", "Some animal prefabs are missing. Generate the wildlife now?", "Generate", "Cancel"))
                    return "Cancelled: generate the wildlife first (Greenhaven ▸ Wildlife ▸ Generate Wildlife).";
                WildlifeGenerator.Generate(WildlifeGeneratorSettings.LoadOrCreate(), WildlifeGenerator.Kinds);
            }
            GeneratorSettings assets = GeneratorPrefs.Settings;
            AssetCategory[] needed = { AssetCategory.BroadleafTree, AssetCategory.PineTree, AssetCategory.Bush, AssetCategory.Rock, AssetCategory.Grass, AssetCategory.Flower,
                AssetCategory.Fence, AssetCategory.Bench, AssetCategory.Stump, AssetCategory.Log, AssetCategory.Signpost };
            var absent = GeneratedAssetStore.MissingGenerators(assets.Seed, needed.Select(Generators.For));
            if (absent.Count > 0)
            {
                if (!EditorUtility.DisplayDialog("Generate scenery?", $"Missing: {string.Join(", ", absent.Select(g => g.DisplayName))}. Generate them now?", "Generate", "Cancel"))
                    return "Cancelled: scenery assets are missing.";
                GeneratedAssetStore.GenerateMissing(absent, assets);
            }
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild wildlife demo?", $"{ScenePath} will be rebuilt. No other scene is changed.", "Rebuild", "Cancel"))
                return "Cancelled.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            Phase1SceneBuilder.CreateMaterials();
            GreenhavenArt.PrepareImports();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();
            var rng = new System.Random(assets.Seed ^ 0xD3E0);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            GameObject P(AssetCategory c, int v) => AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(GeneratedAssetStore.AssetName(Generators.For(c), assets.Seed, v)));
            Transform env = new GameObject("Environment").transform;

            BuildGround(env);
            GameObject Place(AssetCategory c, int v, Vector2 p, float yaw, float scale = 1f)
            {
                GameObject prefab = P(c, v);
                if (prefab == null) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, env);
                Vector3 ground = Raycast(p);
                go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = Vector3.one * scale;
                return go;
            }
            Physics.SyncTransforms();
            // Copse (deer and fox habitat), thicket (rabbits), mound rocks (goats), fence and bench (bird perches).
            for (int i = 0; i < 9; i++) Place(i % 3 == 0 ? AssetCategory.PineTree : AssetCategory.BroadleafTree, i % 3, new Vector2(R(-24f, 22f), R(18f, 26f)), R(0f, 360f), R(0.9f, 1.2f));
            for (int i = 0; i < 4; i++) Place(AssetCategory.BroadleafTree, i % 3, new Vector2(R(-25f, -18f), R(-4f, 14f)), R(0f, 360f));
            for (int i = 0; i < 10; i++) Place(AssetCategory.Bush, i % 3, new Vector2(-14f + R(-4f, 4f), -10f + R(-4f, 4f)), R(0f, 360f), R(0.9f, 1.3f));
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Place(AssetCategory.Rock, i % 6, Mound + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * MoundRadius * R(0.55f, 0.95f), R(0f, 360f), R(0.9f, 1.6f));
            }
            for (int i = 0; i < 5; i++) Place(AssetCategory.Fence, i % 2, new Vector2(-6f + i * PropGenerator.FenceLength, 6f), 0f);
            Place(AssetCategory.Bench, 0, new Vector2(4f, -2.5f), 180f);
            Place(AssetCategory.Stump, 0, new Vector2(-8f, 12f), 0f);
            Place(AssetCategory.Log, 0, new Vector2(8f, 14f), 30f);
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector2(R(-Half + 2f, Half - 2f), R(-Half + 2f, Half - 2f));
                if (Mathf.Abs(p.x) < 2.2f) continue; // keep the path clear
                Place(i % 4 == 0 ? AssetCategory.Flower : AssetCategory.Grass, i % 3, p, R(0f, 360f), R(0.8f, 1.2f));
            }

            // Gameplay.
            GameObject player = Phase1SceneBuilder.BuildPlayer(Raycast(new Vector2(0f, -24f)) + Vector3.up * 0.05f, "Player_WildlifeDemo");
            Phase1SceneBuilder.BuildCamera(player);
            WeatherTools.WeatherSetup.AddToScene();
            WindManager wind = WindSetup.AddToScene();
            var registry = new GameObject("CollectibleRegistry").AddComponent<CollectibleRegistry>();
            var quests = new GameObject("QuestManager").AddComponent<QuestManager>();
            Phase1SceneBuilder.SetRef(quests, "registry", registry);
            DialogueView dialogue = Phase1SceneBuilder.BuildHud(player, quests);
            GameObject sign = Place(AssetCategory.Signpost, 0, new Vector2(2.6f, -20f), 200f);
            if (sign != null)
                AmbientMotionSetup.Readable(sign, dialogue, "Wildlife Meadow — foxes by the trees, deer in the open, rabbits in the thicket, goats on the rocks, birds on the fence.",
                    "Walk slowly to get closer; run and they notice you sooner. F4 shows what each animal is doing.");
            LeafDrift leaves = AmbientMotionSetup.AddLeafDrift();
            AmbienceController ambience = AmbientMotionSetup.AddAmbience();

            // Animals in their habitats.
            Physics.SyncTransforms();
            WildlifePlacement.Context ctx = WildlifePlacement.Begin(WildlifeGeneratorSettings.LoadOrCreate().seed);
            Vector3 Around(Vector2 c, float r)
            {
                float a = R(0f, Mathf.PI * 2f), d = r * Mathf.Sqrt((float)rng.NextDouble());
                return new Vector3(c.x + Mathf.Cos(a) * d, 0f, c.y + Mathf.Sin(a) * d);
            }
            WildlifePlacement.SpawnGroup(ctx, AnimalKind.Fox, 2, () => Around(new Vector2(-12f, 12f), 6f), _ => true, 4f);
            WildlifePlacement.SpawnGroup(ctx, AnimalKind.Deer, 3, () => Around(new Vector2(11f, 9f), 6f), _ => true, 2.5f);
            WildlifePlacement.SpawnGroup(ctx, AnimalKind.Rabbit, 4, () => Around(new Vector2(-14f, -10f), 6f), _ => true, 1.5f);
            WildlifePlacement.SpawnGroup(ctx, AnimalKind.Goat, 2, () => Around(Mound, MoundRadius * 0.45f), _ => true, 2f);
            var perches = ctx.Perches.ToList();
            for (int i = 0; i < 4 && perches.Count > 0; i++)
            {
                Vector3 home = perches[(i * 3) % perches.Count];
                Vector3[] others = perches.Where(p => p != home).OrderBy(p => (p - home).sqrMagnitude).Take(3).Prepend(home).ToArray();
                WildlifePlacement.Spawn(ctx, AnimalKind.Bird, home, R(0f, 360f), others);
            }
            string placement = WildlifePlacement.Finish(ctx);
            AmbientMotionSetup.AddDebugPanel(wind, leaves, ambience, player, visibleOnStart: false);

            int badRefs = Phase1SceneBuilder.ValidateSceneReferences(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"Wildlife demo saved to {ScenePath}. Press Play: walk up the path; F4 = wildlife states, F2 = wind, F3 = player animation.\n" +
                   placement + (badRefs > 0 ? $"\n{badRefs} scene reference problem(s); see Console." : "");
        }

        static Vector3 Raycast(Vector2 p) =>
            Physics.Raycast(new Vector3(p.x, 50f, p.y), Vector3.down, out RaycastHit hit, 100f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                ? hit.point : new Vector3(p.x, 0f, p.y);

        /// <summary>Flat meadow with a gentle rocky mound (one walkable mesh collider) and a dirt path.</summary>
        static void BuildGround(Transform env)
        {
            const float cell = 1.5f;
            int cells = Mathf.RoundToInt(Half * 2f / cell);
            float H(float x, float z)
            {
                float d = Vector2.Distance(new Vector2(x, z), Mound) / MoundRadius;
                return MoundHeight * Mathf.SmoothStep(1f, 0f, d) + 0.25f * Mathf.PerlinNoise(x * 0.08f + 3f, z * 0.08f + 7f);
            }
            var ground = new MeshBuilder();
            for (int i = 0; i < cells; i++)
                for (int j = 0; j < cells; j++)
                {
                    float x0 = -Half + i * cell, z0 = -Half + j * cell, x1 = x0 + cell, z1 = z0 + cell;
                    Vector3 a = new Vector3(x0, H(x0, z0), z0), b = new Vector3(x1, H(x1, z0), z0), c = new Vector3(x0, H(x0, z1), z1), d = new Vector3(x1, H(x1, z1), z1);
                    foreach (var (p, q, r) in new[] { (a, c, b), (b, c, d) })
                    {
                        Vector3 n = MeshBuilder.OrientedNormal(p, q, r, Vector3.up);
                        float mound = Vector2.Distance(new Vector2(p.x, p.z), Mound) / MoundRadius;
                        Vector2 uv = mound < 0.7f && n.y < 0.95f ? GenPalette.UV(Ramp.RockGrey, 3 + (n.y > 0.9f ? 1 : 0)) : GenPalette.UV(Ramp.LeafGreen, 3 + ((i + j) % 3 == 0 ? 1 : 0));
                        ground.Triangle(p, q, r, Vector3.up, uv);
                    }
                }
            string folder = GeneratedAssetStore.Root + "/Wildlife";
            WildlifeGenerator.EnsureFolders();
            Mesh mesh = GeneratedAssetStore.SaveMesh(ground.ToMesh("Gen_WildlifeDemoGround"), $"{folder}/Meshes/Gen_WildlifeDemoGround.asset");
            var go = new GameObject("Ground");
            go.transform.SetParent(env, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GeneratedAssetStore.EnsurePaletteMaterial(GenPalette.Nature);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            // A straight dirt path up the middle (also the trail the animals keep off).
            var path = new MeshBuilder();
            for (float z = -Half; z < Half; z += 1f)
            {
                Vector3 a = new Vector3(-1.2f, H(-1.2f, z) + 0.04f, z), b = new Vector3(1.2f, H(1.2f, z) + 0.04f, z);
                Vector3 c = new Vector3(-1.2f, H(-1.2f, z + 1f) + 0.04f, z + 1f), d = new Vector3(1.2f, H(1.2f, z + 1f) + 0.04f, z + 1f);
                Vector2 uv = VillagePalette.UV(VRamp.PathDirt, 4);
                path.Triangle(a, c, b, Vector3.up, uv);
                path.Triangle(b, c, d, Vector3.up, uv);
            }
            Mesh pathMesh = GeneratedAssetStore.SaveMesh(path.ToMesh("Gen_WildlifeDemoPath"), $"{folder}/Meshes/Gen_WildlifeDemoPath.asset");
            var pathGo = new GameObject("Path");
            pathGo.transform.SetParent(env, false);
            pathGo.AddComponent<MeshFilter>().sharedMesh = pathMesh;
            pathGo.AddComponent<MeshRenderer>().sharedMaterial = GeneratedAssetStore.EnsurePaletteMaterial(VillagePalette.Palette);

            Transform walls = new GameObject("Boundaries").transform;
            walls.SetParent(env, false);
            foreach ((Vector3 c, Vector3 s) in new[]
                     {
                         (new Vector3(0f, 3f, Half), new Vector3(Half * 2f, 6f, 1f)), (new Vector3(0f, 3f, -Half), new Vector3(Half * 2f, 6f, 1f)),
                         (new Vector3(Half, 3f, 0f), new Vector3(1f, 6f, Half * 2f)), (new Vector3(-Half, 3f, 0f), new Vector3(1f, 6f, Half * 2f)),
                     })
            {
                var w = new GameObject("Wall");
                w.transform.SetParent(walls, false);
                w.transform.position = c;
                w.AddComponent<BoxCollider>().size = s;
            }
        }
    }
}
