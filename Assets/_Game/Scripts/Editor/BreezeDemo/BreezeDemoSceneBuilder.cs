using System.Collections.Generic;
using System.IO;
using System.Linq;
using Greenhaven.Collectibles;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Player;
using Greenhaven.Quests;
using Greenhaven.Editor.WindTools;
using Greenhaven.UI;
using Greenhaven.Wind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.BreezeDemo
{
    /// <summary>
    /// Builds Assets/_Game/Scenes/GreenhavenBreezeDemo.unity: one compact, playable meadow showing the shared
    /// breeze (trees, grass, flowers, bushes, a swinging sign, drifting leaves) with the existing player,
    /// camera and mobile controls. Reuses generated prefabs; never touches other scenes.
    /// </summary>
    public static class BreezeDemoSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/GreenhavenBreezeDemo.unity";
        const string AssetFolder = GeneratedAssetStore.Root + "/BreezeDemo";
        const float Half = 24f;
        const float PathHalfWidth = 1.3f;
        const float ClearingRadius = 4.5f;
        static readonly Vector2 Clearing = new Vector2(0f, 17f);
        static readonly Vector2 SpawnXZ = new Vector2(0f, -21.5f);
        static readonly Vector2[] PathPoints =
        {
            new Vector2(0f, -23f), new Vector2(-3f, -12f), new Vector2(2f, -2f), new Vector2(-1.5f, 7f), new Vector2(0f, 14f),
        };

        static readonly AssetCategory[] StaticTypes = { AssetCategory.Rock, AssetCategory.Bench, AssetCategory.Signpost };

        static GeneratorSettings settings;

        [MenuItem("Greenhaven/Create Breeze Demo Scene", priority = 44)]
        static void Menu()
        {
            string result = Build(GeneratorPrefs.Settings);
            Debug.Log("Greenhaven — Breeze demo:\n" + result);
            EditorUtility.DisplayDialog("Breeze Demo Scene", result.Length > 1200 ? result.Substring(0, 1200) + "…" : result, "OK");
        }

        public static string Build(GeneratorSettings s)
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";
            settings = s;

            // 1. Assets: wind types must exist and use their wind material; static props must exist.
            var requests = new List<GeneratedAssetStore.Request>();
            foreach (AssetCategory type in AmbientMotionSetup.NotWindReady(s.Seed))
                requests.Add(new GeneratedAssetStore.Request { Generator = Generators.For(type), Count = Generators.For(type).DefaultCount });
            foreach (AssetCategory type in StaticTypes)
                if (Prefab(type, 0) == null) requests.Add(new GeneratedAssetStore.Request { Generator = Generators.For(type), Count = Generators.For(type).DefaultCount });
            string generation = null;
            if (requests.Count > 0)
            {
                string list = string.Join(", ", requests.Select(r => r.Generator.DisplayName));
                if (!EditorUtility.DisplayDialog("Prepare breeze assets?",
                        $"These need generating or upgrading to wind-ready versions for seed {s.Seed}: {list}. " +
                        "Existing ones are replaced in place (you will be asked to confirm).", "Continue", "Cancel"))
                    return "Cancelled: wind-ready assets are missing.";
                generation = GeneratedAssetStore.Generate(requests, s).Split('\n')[0];
            }

            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild breeze demo?",
                    $"{ScenePath} will be rebuilt. No other scene is changed.", "Rebuild", "Cancel"))
                return "Cancelled.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            GeneratedAssetStore.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder(GeneratedAssetStore.Root, "BreezeDemo");
            Phase1SceneBuilder.CreateMaterials();
            GreenhavenArt.PrepareImports();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 95f;

            Transform env = new GameObject("Environment").transform;
            BuildGround(env);
            BuildPath(env);
            Transform props = Group("Props", env);
            var rng = new System.Random(s.Seed ^ 0xB2EE2E);
            BuildVegetation(Group("Vegetation", env), rng);

            // Gameplay: existing player, camera, mobile HUD.
            GameObject player = Phase1SceneBuilder.BuildPlayer(new Vector3(SpawnXZ.x, 0.05f, SpawnXZ.y), "Player_BreezeDemo");
            Phase1SceneBuilder.BuildCamera(player);
            var registry = new GameObject("CollectibleRegistry").AddComponent<CollectibleRegistry>();
            var quests = new GameObject("QuestManager").AddComponent<QuestManager>();
            Phase1SceneBuilder.SetRef(quests, "registry", registry);
            DialogueView dialogue = Phase1SceneBuilder.BuildHud(player, quests);

            BuildProps(props, dialogue);

            // Shared breeze + environmental motion + debug.
            WindManager wind = WindTools.WindSetup.AddToScene();
            LeafDrift leaves = AmbientMotionSetup.AddLeafDrift();
            AmbienceController ambience = AmbientMotionSetup.AddAmbience();
            AmbientMotionSetup.AddDebugPanel(wind, leaves, ambience, player, visibleOnStart: true);

            // 4. Validate: one of each manager/listener/player, valid references.
            var report = new List<string>();
            if (generation != null) report.Add(generation);
            int badRefs = Phase1SceneBuilder.ValidateSceneReferences(scene);
            int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            int players = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Length;
            int winds = Object.FindObjectsByType<WindManager>(FindObjectsSortMode.None).Length;
            report.Add($"Checks: {players} player, {listeners} audio listener, {winds} wind manager, {(badRefs == 0 ? "references OK" : badRefs + " reference problem(s)")}.");
            if (players != 1 || listeners != 1 || winds != 1) report.Add("⚠ Expected exactly one of each.");

            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Insert(0, $"Breeze demo saved to {ScenePath}. Press Play; F2 = breeze debug panel, F3 = animation readout.");
            return string.Join("\n", report);
        }

        // ───────────────────────────── Terrain and path ─────────────────────────────

        static void BuildGround(Transform env)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(env, false);
            ground.transform.localScale = new Vector3(Half * 0.2f, 1f, Half * 0.2f);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_Grass.mat");
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

            Transform walls = Group("Boundaries", env);
            void Wall(Vector3 centre, Vector3 size)
            {
                var go = new GameObject("Wall");
                go.transform.SetParent(walls, false);
                go.transform.position = centre;
                go.AddComponent<BoxCollider>().size = size;
            }
            Wall(new Vector3(0f, 3f, Half), new Vector3(Half * 2f, 6f, 1f));
            Wall(new Vector3(0f, 3f, -Half), new Vector3(Half * 2f, 6f, 1f));
            Wall(new Vector3(Half, 3f, 0f), new Vector3(1f, 6f, Half * 2f));
            Wall(new Vector3(-Half, 3f, 0f), new Vector3(1f, 6f, Half * 2f));
        }

        static void BuildPath(Transform env)
        {
            var mb = new MeshBuilder();
            var rng = new System.Random(settings.Seed ^ 0x9A7);
            Vector3 G(Vector2 p, float y) => new Vector3(p.x, y, p.y);
            for (int i = 0; i < PathPoints.Length - 1; i++)
            {
                Vector2 a = PathPoints[i], b = PathPoints[i + 1];
                Vector2 dir = (b - a).normalized;
                var side = new Vector2(-dir.y, dir.x) * PathHalfWidth;
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
                for (int k = 0; k < steps; k++)
                {
                    Vector2 p0 = Vector2.Lerp(a, b, k / (float)steps), p1 = Vector2.Lerp(a, b, (k + 1) / (float)steps);
                    Vector2 uv = VillagePalette.UV(VRamp.PathDirt, 4 + Palette.FaceJitter(rng, 0.6f));
                    mb.Triangle(G(p0 - side, 0.02f), G(p0 + side, 0.02f), G(p1 - side, 0.02f), Vector3.up, uv);
                    mb.Triangle(G(p1 - side, 0.02f), G(p0 + side, 0.02f), G(p1 + side, 0.02f), Vector3.up, uv);
                }
            }
            // Rounded joints and the clearing.
            void Disc(Vector2 c, float r, int segments, float y, int shade)
            {
                for (int k = 0; k < segments; k++)
                {
                    float a0 = k * Mathf.PI * 2f / segments, a1 = (k + 1) * Mathf.PI * 2f / segments;
                    mb.Triangle(G(c, y), G(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, y), G(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, y),
                        Vector3.up, VillagePalette.UV(VRamp.PathDirt, shade));
                }
            }
            foreach (Vector2 p in PathPoints) Disc(p, PathHalfWidth, 8, 0.018f, 4);
            Disc(Clearing, ClearingRadius, 24, 0.019f, 5);

            Mesh mesh = GeneratedAssetStore.SaveMesh(mb.ToMesh($"Gen_BreezePath_s{settings.Seed}"), $"{AssetFolder}/Gen_BreezePath_s{settings.Seed}.asset");
            var go = new GameObject("Path");
            go.transform.SetParent(env, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GeneratedAssetStore.EnsurePaletteMaterial(VillagePalette.Palette);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        static float PathDistance(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < PathPoints.Length - 1; i++)
            {
                Vector2 a = PathPoints[i], ab = PathPoints[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>Clear of the path (by <paramref name="margin"/> beyond its edge), the clearing, the spawn and the walls.</summary>
        static bool IsClear(Vector2 p, float margin) =>
            Mathf.Abs(p.x) < Half - 1.5f && Mathf.Abs(p.y) < Half - 1.5f &&
            PathDistance(p) > PathHalfWidth + margin &&
            Vector2.Distance(p, Clearing) > ClearingRadius + margin &&
            Vector2.Distance(p, SpawnXZ) > 2.5f + margin;

        // ───────────────────────────── Vegetation ─────────────────────────────

        static void BuildVegetation(Transform parent, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var big = new List<Vector2>();
            bool Spaced(Vector2 p, float min) => big.All(q => (q - p).sqrMagnitude >= min * min);

            void Scatter(AssetCategory type, int count, float margin, float spacing, float minScale, float maxScale, System.Func<Vector2> sample, Transform group)
            {
                int variants = Generators.For(type).DefaultCount;
                for (int attempt = 0, placed = 0; attempt < count * 60 && placed < count; attempt++)
                {
                    Vector2 p = sample();
                    if (!IsClear(p, margin) || (spacing > 0f && !Spaced(p, spacing))) continue;
                    if (Place(Prefab(type, rng.Next(variants)), group, new Vector3(p.x, 0f, p.y), R(0f, 360f), R(minScale, maxScale)) == null) return;
                    if (spacing > 0f) big.Add(p);
                    placed++;
                }
            }

            Vector2 Anywhere() => new Vector2(R(-Half, Half), R(-Half, Half));
            // Points just beside the path, so grass and flowers frame the walk.
            Vector2 NearPath()
            {
                int i = rng.Next(PathPoints.Length - 1);
                Vector2 a = PathPoints[i], b = PathPoints[i + 1];
                Vector2 on = Vector2.Lerp(a, b, R(0f, 1f));
                Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized;
                return on + normal * (rng.NextDouble() < 0.5 ? -1f : 1f) * R(PathHalfWidth + 0.2f, PathHalfWidth + 4f);
            }

            Transform trees = Group("Trees", parent), plants = Group("Plants", parent), rocks = Group("Rocks", parent);
            Scatter(AssetCategory.BroadleafTree, 9, 4f, 6.5f, 0.85f, 1.15f, Anywhere, trees);
            Scatter(AssetCategory.PineTree, 8, 3.5f, 5.5f, 0.9f, 1.2f, () => new Vector2(R(-Half, Half), R(6f, Half)) * (rng.NextDouble() < 0.5 ? 1f : -1f), trees);
            Scatter(AssetCategory.Bush, 12, 1.2f, 2.2f, 0.85f, 1.15f, Anywhere, plants);
            Scatter(AssetCategory.Rock, 8, 1.5f, 2.5f, 0.7f, 1.3f, Anywhere, rocks);
            Scatter(AssetCategory.Grass, 90, 0.1f, 0f, 0.8f, 1.25f, NearPath, plants);
            Scatter(AssetCategory.Grass, 60, 0.3f, 0f, 0.8f, 1.2f, Anywhere, plants);
            Scatter(AssetCategory.Flower, 30, 0.2f, 0f, 0.9f, 1.2f, NearPath, plants);
            // A ring of flowers and grass around the clearing.
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f + R(-0.1f, 0.1f);
                Vector2 p = Clearing + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ClearingRadius + R(0.3f, 1.2f));
                if (PathDistance(p) < PathHalfWidth + 0.3f) continue;
                AssetCategory type = i % 2 == 0 ? AssetCategory.Flower : AssetCategory.Grass;
                Place(Prefab(type, rng.Next(Generators.For(type).DefaultCount)), plants, new Vector3(p.x, 0f, p.y), R(0f, 360f));
            }
        }

        // ───────────────────────────── Props ─────────────────────────────

        static void BuildProps(Transform parent, DialogueView dialogue)
        {
            // Bench in the clearing, facing back down the path.
            Place(Prefab(AssetCategory.Bench, 0), parent, new Vector3(Clearing.x, 0f, Clearing.y + 2.2f), 180f);

            GameObject sign = Place(Prefab(AssetCategory.Signpost, 0), parent, new Vector3(2.6f, 0f, -19f), 200f);
            if (sign != null)
                AmbientMotionSetup.Readable(sign, dialogue, "Breezy Meadow — follow the path to the clearing.",
                    "Watch the trees, grass and flowers move with the wind. Press F2 for wind controls, F3 for the animation readout.");

            AmbientMotionSetup.AddHangingSign(parent, new Vector3(3.6f, 0f, 12.5f), -100f, settings.Seed, dialogue,
                "The Swinging Sign — it creaks a little louder when the wind picks up.");
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        static GameObject Prefab(AssetCategory type, int variant) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(GeneratedAssetStore.AssetName(Generators.For(type), settings.Seed, variant)));

        static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, float scale = 1f)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
