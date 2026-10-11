using System.Collections.Generic;
using System.IO;
using System.Linq;
using Greenhaven.Collectibles;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Editor.WindTools;
using Greenhaven.Environment;
using Greenhaven.Quests;
using Greenhaven.UI;
using Greenhaven.Wind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greenhaven.Editor.River
{
    /// <summary>
    /// Builds Assets/_Game/Scenes/RiverDemo.unity: a meadow with a winding river carved into the ground
    /// (S-bends, a slow pool, a faster narrows), a bridge, banks with pebbles and plants, and the existing
    /// player, camera, weather and wind. Touches no other scene.
    /// </summary>
    public static class RiverDemoSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/RiverDemo.unity";
        const float Half = 34f, WaterLevel = -0.3f, BedDepth = 0.95f, RiverWidth = 3.6f;

        // Downstream from north-west to south-east through two bends.
        static readonly Vector3[] Course =
        {
            new Vector3(-30f, 0f, 24f), new Vector3(-20f, 0f, 20f), new Vector3(-12f, 0f, 10f), new Vector3(-12f, 0f, 0f),
            new Vector3(-4f, 0f, -6f), new Vector3(6f, 0f, -4f), new Vector3(14f, 0f, -10f), new Vector3(20f, 0f, -20f), new Vector3(30f, 0f, -26f),
        };

        [MenuItem("Greenhaven/Environment/Create River Demo Scene", priority = 102)]
        static void Menu()
        {
            string result = Build();
            Debug.Log("Greenhaven — River demo:\n" + result);
            EditorUtility.DisplayDialog("River Demo Scene", result, "OK");
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";
            GeneratorSettings assets = GeneratorPrefs.Settings;
            AssetCategory[] needed = { AssetCategory.Rock, AssetCategory.Grass, AssetCategory.Flower, AssetCategory.Bush, AssetCategory.BroadleafTree, AssetCategory.PineTree, AssetCategory.Bridge, AssetCategory.Signpost };
            var absent = GeneratedAssetStore.MissingGenerators(assets.Seed, needed.Select(Generators.For));
            if (absent.Count > 0)
            {
                if (!EditorUtility.DisplayDialog("Generate scenery?", $"Missing: {string.Join(", ", absent.Select(g => g.DisplayName))}. Generate them now?", "Generate", "Cancel"))
                    return "Cancelled: scenery assets are missing.";
                GeneratedAssetStore.GenerateMissing(absent, assets);
            }
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild river demo?", $"{ScenePath} will be rebuilt. No other scene is changed.", "Rebuild", "Cancel"))
                return "Cancelled.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            Phase1SceneBuilder.CreateMaterials();
            GreenhavenArt.PrepareImports();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();
            Transform env = new GameObject("Environment").transform;

            List<Vector3> centre = DenseCourse();
            BuildGround(env, centre);
            Physics.SyncTransforms();

            // The river: a slow pool in the first bend, a fast narrows after the bridge.
            var points = Course.Select(p => new Vector3(p.x, WaterLevel, p.z)).ToList();
            RiverFlowController river = RiverTools.CreateRiver("Flowing River (generated)", "RiverDemo", env, points, RiverWidth + 1.4f, 0.7f, 7); // edges tuck under the banks
            river.segments = new List<RiverSegment>
            {
                new RiverSegment { fromPoint = 0, speed = 1f }, new RiverSegment { fromPoint = 2, speed = 0.55f },
                new RiverSegment { fromPoint = 4, speed = 1f }, new RiverSegment { fromPoint = 6, speed = 1.8f },
            };
            RiverTools.Rebuild(river);

            GameObject P(AssetCategory c, int v) => AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(GeneratedAssetStore.AssetName(Generators.For(c), assets.Seed, v)));
            GameObject Place(GameObject prefab, Vector3 at, float yaw, float scale = 1f)
            {
                if (prefab == null) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, env);
                go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = Vector3.one * scale;
                return go;
            }

            // Bridge across the straight between the bends, and a path leading to it.
            Vector3 bridgeAt = new Vector3(-8f, 0f, -3f);
            Vector3 flow = (Course[5] - Course[3]).normalized;
            float bridgeYaw = Mathf.Atan2(-flow.z, flow.x) * Mathf.Rad2Deg; // span across the flow
            Vector3 across = Quaternion.Euler(0f, bridgeYaw, 0f) * Vector3.forward;
            Place(P(AssetCategory.Bridge, 0), new Vector3(bridgeAt.x, 0f - 0.2f + 0.03f, bridgeAt.z), bridgeYaw);
            BuildPath(env, bridgeAt - across * 3.2f - across * 22f, bridgeAt - across * 3.2f);
            BuildPath(env, bridgeAt + across * 3.2f, bridgeAt + across * 3.2f + across * 18f);

            // Trees and bushes away from the water.
            var rng = new System.Random(assets.Seed ^ 0x51AE);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0, placed = 0; i < 400 && placed < 26; i++)
            {
                var p = new Vector3(R(-Half + 3f, Half - 3f), 0f, R(-Half + 3f, Half - 3f));
                if (DistanceTo(centre, p) < RiverWidth + 4f || Vector3.Distance(p, bridgeAt) < 8f) continue;
                if (!Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f)) continue;
                AssetCategory kind = placed % 3 == 0 ? AssetCategory.PineTree : placed % 3 == 1 ? AssetCategory.BroadleafTree : AssetCategory.Bush;
                Place(P(kind, rng.Next(3)), hit.point, R(0f, 360f), R(0.85f, 1.2f));
                placed++;
            }
            Physics.SyncTransforms();
            string banks = RiverTools.AddBankDetails(river, assets);

            // Gameplay.
            Vector3 spawn = bridgeAt - across * 20f;
            Physics.Raycast(spawn + Vector3.up * 20f, Vector3.down, out RaycastHit spawnHit, 40f);
            GameObject player = Phase1SceneBuilder.BuildPlayer(spawnHit.point + Vector3.up * 0.05f, "Player_RiverDemo");
            player.transform.rotation = Quaternion.LookRotation(across);
            Phase1SceneBuilder.BuildCamera(player);
            WeatherTools.WeatherSetup.AddToScene();
            WindManager wind = WindSetup.AddToScene();
            var registry = new GameObject("CollectibleRegistry").AddComponent<CollectibleRegistry>();
            var quests = new GameObject("QuestManager").AddComponent<QuestManager>();
            Phase1SceneBuilder.SetRef(quests, "registry", registry);
            DialogueView dialogue = Phase1SceneBuilder.BuildHud(player, quests);
            GameObject sign = Place(P(AssetCategory.Signpost, 0), spawnHit.point + across * 2f + Vector3.Cross(Vector3.up, across) * 2.2f, bridgeYaw + 180f);
            if (sign != null)
                AmbientMotionSetup.Readable(sign, dialogue, "Willow Run — watch the water slow in the pool, then race through the narrows below the bridge.",
                    "Select the river in the Editor to change its flow, foam, colours and quality (Low / Standard).");
            LeafDrift leaves = AmbientMotionSetup.AddLeafDrift();
            AmbienceController ambience = AmbientMotionSetup.AddAmbience();
            AmbientMotionSetup.AddDebugPanel(wind, leaves, ambience, player, visibleOnStart: false);

            int badRefs = Phase1SceneBuilder.ValidateSceneReferences(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"River demo saved to {ScenePath}. Press Play and cross the bridge.\nRiver: {river.Length:0} m, segments slow → normal → fast. {banks}" +
                   (badRefs > 0 ? $"\n{badRefs} scene reference problem(s); see Console." : "");
        }

        static List<Vector3> DenseCourse()
        {
            var result = new List<Vector3>();
            for (int i = 0; i < Course.Length - 1; i++)
            {
                Vector3 p0 = Course[Mathf.Max(0, i - 1)], p1 = Course[i], p2 = Course[i + 1], p3 = Course[Mathf.Min(Course.Length - 1, i + 2)];
                for (int k = 0; k < 20; k++)
                {
                    float t = k / 20f, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(Course[Course.Length - 1]);
            return result;
        }

        static float DistanceTo(List<Vector3> line, Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector3 a = line[i], ab = line[i + 1] - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>Meadow with a river channel: flat bed under the water, sloping banks, faceted like the other scenes.</summary>
        static void BuildGround(Transform env, List<Vector3> centre)
        {
            const float cell = 1.25f;
            int cells = Mathf.RoundToInt(Half * 2f / cell);
            float half = RiverWidth * 0.5f;
            float H(float x, float z)
            {
                var p = new Vector3(x, 0f, z);
                float d = DistanceTo(centre, p);
                float meadow = 0.35f * Mathf.PerlinNoise(x * 0.07f + 11f, z * 0.07f + 5f);
                float channel = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.55f, half + 1.4f, d));
                return Mathf.Lerp(meadow, -BedDepth, channel);
            }
            var heights = new float[cells + 1, cells + 1];
            for (int i = 0; i <= cells; i++)
                for (int j = 0; j <= cells; j++)
                    heights[i, j] = H(-Half + i * cell, -Half + j * cell);
            var mb = new MeshBuilder();
            var rng = new System.Random(5);
            for (int i = 0; i < cells; i++)
                for (int j = 0; j < cells; j++)
                {
                    Vector3 V(int a, int b) => new Vector3(-Half + a * cell, heights[a, b], -Half + b * cell);
                    Vector3 a0 = V(i, j), b0 = V(i + 1, j), c0 = V(i, j + 1), d0 = V(i + 1, j + 1);
                    foreach (var (p, q, r) in new[] { (a0, c0, b0), (b0, c0, d0) })
                    {
                        float y = (p.y + q.y + r.y) / 3f;
                        Vector3 n = MeshBuilder.OrientedNormal(p, q, r, Vector3.up);
                        Vector2 uv = y < WaterLevel - 0.1f ? GenPalette.UV(Ramp.RockWarm, 2 + rng.Next(2))      // river bed
                            : y < WaterLevel + 0.25f ? GenPalette.UV(Ramp.RockWarm, 4)                             // wet shore
                            : n.y < 0.85f ? GenPalette.UV(Ramp.LeafGreen, 2)
                            : GenPalette.UV(Ramp.LeafGreen, 3 + (rng.NextDouble() < 0.3 ? 1 : 0));
                        mb.Triangle(p, q, r, Vector3.up, uv);
                    }
                }
            Mesh mesh = GeneratedAssetStore.SaveMesh(mb.ToMesh("Gen_RiverDemoGround"), $"{RiverTools.Root}/Meshes/Gen_RiverDemoGround.asset");
            var go = new GameObject("Ground");
            go.transform.SetParent(env, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GeneratedAssetStore.EnsurePaletteMaterial(GenPalette.Nature);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

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

        static void BuildPath(Transform env, Vector3 from, Vector3 to)
        {
            var mb = new MeshBuilder();
            Vector3 dir = (to - from).normalized, side = Vector3.Cross(Vector3.up, dir) * 1.1f;
            int steps = Mathf.CeilToInt(Vector3.Distance(from, to));
            Vector3 G(Vector3 p) => Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f) ? hit.point + Vector3.up * 0.04f : p;
            for (int k = 0; k < steps; k++)
            {
                Vector3 a = Vector3.Lerp(from, to, k / (float)steps), b = Vector3.Lerp(from, to, (k + 1f) / steps);
                Vector2 uv = VillagePalette.UV(VRamp.PathDirt, 4);
                mb.Triangle(G(a - side), G(b - side), G(a + side), Vector3.up, uv);
                mb.Triangle(G(a + side), G(b - side), G(b + side), Vector3.up, uv);
            }
            string name = $"Gen_RiverDemoPath_{Mathf.RoundToInt(from.x)}_{Mathf.RoundToInt(from.z)}";
            Mesh mesh = GeneratedAssetStore.SaveMesh(mb.ToMesh(name), $"{RiverTools.Root}/Meshes/{name}.asset");
            var go = new GameObject("Path");
            go.transform.SetParent(env, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GeneratedAssetStore.EnsurePaletteMaterial(VillagePalette.Palette);
        }
    }
}
