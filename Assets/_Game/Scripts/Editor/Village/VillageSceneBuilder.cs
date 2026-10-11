using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Greenhaven.Collectibles;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Editor.WindTools;
using Greenhaven.Interaction;
using Greenhaven.Quests;
using Greenhaven.UI;
using Greenhaven.Wind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.VillageScene
{
    /// <summary>
    /// Builds Assets/_Game/Scenes/GreenhavenVillage.unity from generated assets: terrain with hills as a
    /// natural boundary, a paved square with the well, six buildings facing the square, paths to every
    /// door, gardens, a stream crossed by the bridge, and a gated exit north toward the Whispering Forest.
    /// The layout is hand-authored; only vegetation scatter uses the seed. Never touches other scenes.
    /// </summary>
    public static class VillageSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/GreenhavenVillage.unity";
        const float SquareRadius = 8.5f;
        const float WorldHalf = 38f;
        const float WallRadius = 35.5f;
        const float BridgeHalfWidth = 1.3f;
        static readonly Vector2 SpawnXZ = new Vector2(0f, -29f);

        struct Slot
        {
            public AssetCategory Kind;
            public int Variant;
            public Vector2 Position;
            public string Name;

            public Slot(AssetCategory kind, int variant, float x, float z, string name)
            {
                Kind = kind;
                Variant = variant;
                Position = new Vector2(x, z);
                Name = name;
            }
        }

        // Six buildings on a ring around the square, all facing it; the main road (x ≈ 0) stays open.
        static readonly Slot[] BuildingSlots =
        {
            new Slot(AssetCategory.Shop, 0, -10.5f, -11f, "General Store"),
            new Slot(AssetCategory.House, 0, 11f, -11.5f, "Miller House"),
            new Slot(AssetCategory.Cottage, 0, -15.5f, 1f, "Baker's Cottage"),
            new Slot(AssetCategory.Cottage, 1, 15.5f, 1.5f, "Weaver's Cottage"),
            new Slot(AssetCategory.House, 1, -11f, 10f, "Elder's House"),
            new Slot(AssetCategory.Cottage, 2, 10.5f, 10f, "Shepherd's Cottage"),
        };

        // Main road: south approach → square → bridge → gated north exit.
        static readonly Vector2[] MainRoad =
        {
            new Vector2(0f, -37f), new Vector2(0f, -SquareRadius + 0.6f),
        };
        static readonly Vector2[] NorthRoad =
        {
            new Vector2(0f, SquareRadius - 0.6f), new Vector2(0f, 13.6f),
        };
        static readonly Vector2[] ExitRoad =
        {
            new Vector2(0f, 20.4f), new Vector2(0f, 25f), new Vector2(1.2f, 31f), new Vector2(1.6f, 37f),
        };

        static GeneratorSettings settings;
        static float seedOffset;
        static StringBuilder log;
        static List<string> missing;

        // ───────────────────────────── Entry point ─────────────────────────────

        public static string Build(GeneratorSettings s, float treeDensity)
        {
            if (EditorApplication.isPlaying) return "Stop Play mode before creating the village scene.";
            settings = s;
            seedOffset = (Mathf.Abs(s.Seed) % 1000) * 0.37f;
            log = new StringBuilder();

            // 1. Make sure the generated assets for this seed exist.
            if (!EnsureAssets(out string why)) return why;

            // 2. Confirm before replacing the village scene (and its generated terrain meshes).
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild village scene?",
                    $"{ScenePath} and its generated terrain/path/water meshes for seed {s.Seed} will be rebuilt. " +
                    "No other scene is changed.", "Rebuild", "Cancel"))
                return "Cancelled. Village scene unchanged.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            Phase1SceneBuilder.CreateMaterials();
            GreenhavenArt.PrepareImports();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 120f;

            missing = new List<string>();
            var layout = new VillageLayout();
            Transform env = new GameObject("Environment").transform;
            Transform buildings = Group("Buildings", env);
            Transform props = Group("Props", env);
            Transform vegetation = Group("Vegetation", env);

            // 3. Plan buildings first: their doors decide where side paths go.
            var doors = PlanBuildings(layout);
            PlanPaths(layout, doors);
            layout.AddKeepClear(Vector2.zero, SquareRadius + 0.6f);              // square
            layout.AddKeepClear(SpawnXZ, 3f);                                     // spawn
            layout.AddKeepClear(new Vector2(0f, StreamZ(0f)), 3.4f);              // bridge

            // 4. Terrain, paths, square and stream as generated meshes.
            BuildTerrain(env, layout);

            // 5. Buildings, square, road furniture, gardens, exit, stream blockers, boundaries.
            PlaceBuildings(buildings, layout);
            DialogueView dialogue = BuildGameplay(out GameObject player);
            WorldTools.WorldLinkSetup.AddToVillage(p => Height(p.x, p.y)); // north gate → Cloudpeak, and the arrival back
            BuildSquare(props, layout, dialogue);
            BuildRoad(props, layout, dialogue);
            foreach (Slot slot in BuildingSlots.Where(b => b.Kind == AssetCategory.Cottage && b.Variant < 2))
                BuildGarden(props, layout, slot);
            BuildShopYard(props, layout);
            BuildHousePlanters(props, layout);
            BuildStreamBlockers(env);
            BuildBoundaries(env);
            BuildHangingSign(props, layout, dialogue);

            // 6. Vegetation from the existing nature generators.
            BuildVegetation(vegetation, layout, treeDensity);

            // 7. Environmental motion: drifting leaves, optional ambience, F2 breeze panel (hidden at start).
            LeafDrift leaves = AmbientMotionSetup.AddLeafDrift();
            AmbienceController ambience = AmbientMotionSetup.AddAmbience();
            AmbientMotionSetup.AddDebugPanel(Object.FindFirstObjectByType<WindManager>(), leaves, ambience, player, visibleOnStart: false);

            // 8. Validate and save.
            ValidateLayout(layout, doors, player);
            int badRefs = Phase1SceneBuilder.ValidateSceneReferences(scene);
            if (badRefs > 0) log.AppendLine($"{badRefs} scene reference problem(s); see Console.");
            if (missing.Count > 0) log.AppendLine("Missing prefabs: " + string.Join(", ", missing.Distinct()));
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (File.Exists(Cloudpeak.CloudpeakSceneBuilder.ScenePath))
                log.AppendLine("Linked to CloudpeakAdventure through the north gate. Build scene list:\n" + WorldTools.WorldLinkSetup.UpdateBuildScenes());

            int objects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
            log.Insert(0, $"Village scene saved to {ScenePath} ({objects} transforms). It is not added to Build Settings.\n");
            return log.ToString();
        }

        static bool EnsureAssets(out string message)
        {
            message = null;
            // The village does not use the forest-floor props (logs, stumps, mushrooms); only missing types are generated.
            List<IAssetGenerator> absent = GeneratedAssetStore.MissingGenerators(settings.Seed, Generators.All.Where(g => !(g is ForestPropGenerator)));
            if (absent.Count > 0)
            {
                if (!EditorUtility.DisplayDialog("Generate village assets?",
                        $"Generated prefabs for seed {settings.Seed} are missing ({string.Join(", ", absent.Select(g => g.DisplayName))}). Generate them now?",
                        "Generate", "Cancel"))
                {
                    message = "Cancelled: village assets for this seed are missing. Use Greenhaven ▸ Generate Village Assets first.";
                    return false;
                }
                log.AppendLine(GeneratedAssetStore.GenerateMissing(absent, settings).Split('\n')[0]);
            }
            // Vegetation made before the wind system uses the static material: offer to upgrade it in place.
            AssetCategory[] stale = AmbientMotionSetup.NotWindReady(settings.Seed);
            if (stale.Length > 0)
            {
                string list = string.Join(", ", stale.Select(t => Generators.For(t).DisplayName));
                if (EditorUtility.DisplayDialog("Upgrade vegetation for wind?",
                        $"These plants for seed {settings.Seed} are not wind-ready yet: {list}. Regenerate them so they sway?\n" +
                        "Existing ones are replaced in place (you will be asked to confirm). Skip keeps them static.", "Upgrade", "Skip"))
                    log.AppendLine(GeneratedAssetStore.Generate(
                        stale.Select(t => new GeneratedAssetStore.Request { Generator = Generators.For(t), Count = Generators.For(t).DefaultCount }).ToList(),
                        settings).Split('\n')[0]);
                else
                    log.AppendLine($"Wind upgrade skipped: {list} stay static.");
            }
            if (HeroGenerator.FindPrefab(settings.Seed) == null &&
                EditorUtility.DisplayDialog("Generate hero?", $"No generated hero found. Generate the hero for seed {settings.Seed} now?\n" +
                                                             "(Otherwise the existing character is used.)", "Generate", "Skip"))
                log.AppendLine(HeroGenerator.Generate(settings.Seed));
            return true;
        }

        // ───────────────────────────── Terrain ─────────────────────────────

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        /// <summary>Centre line of the stream crossing the village north of the square.</summary>
        public static float StreamZ(float x) => 17f + 1.6f * Mathf.Sin(x / 7f);

        /// <summary>
        /// Ground height: flat village bowl, hills rising beyond ~24 m (a natural boundary), an open
        /// corridor along the main road, and a shallow stream bed.
        /// </summary>
        public static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float noise = Mathf.PerlinNoise(x * 0.07f + seedOffset, z * 0.07f + seedOffset);
            float hills = Smooth(24f, 36f, r) * (3.2f + 2.2f * noise);
            if (Mathf.Abs(z) > 16f) hills *= Mathf.Lerp(0.12f, 1f, Smooth(3.5f, 10f, Mathf.Abs(x)));
            float bed = -0.45f * (1f - Smooth(1f, 2.2f, Mathf.Abs(z - StreamZ(x))));
            return hills + bed;
        }

        static Vector3 OnGround(Vector2 p, float lift = 0f) => new Vector3(p.x, Height(p.x, p.y) + lift, p.y);

        static void BuildTerrain(Transform env, VillageLayout layout)
        {
            string suffix = $"_s{settings.Seed}";
            Material nature = GeneratedAssetStore.EnsurePaletteMaterial(GenPalette.Nature);
            Material village = GeneratedAssetStore.EnsurePaletteMaterial(VillagePalette.Palette);

            // Ground: faceted grid; sloped and high faces darker, stream banks muddy.
            var ground = new MeshBuilder();
            const float cell = 2f;
            int cells = Mathf.RoundToInt(WorldHalf * 2f / cell);
            for (int i = 0; i < cells; i++)
            {
                for (int j = 0; j < cells; j++)
                {
                    float x0 = -WorldHalf + i * cell, z0 = -WorldHalf + j * cell, x1 = x0 + cell, z1 = z0 + cell;
                    Vector3 a = new Vector3(x0, Height(x0, z0), z0), b = new Vector3(x1, Height(x1, z0), z0);
                    Vector3 c = new Vector3(x0, Height(x0, z1), z1), d = new Vector3(x1, Height(x1, z1), z1);
                    foreach (var (p, q, r) in new[] { (a, c, b), (b, c, d) })
                    {
                        Vector3 n = MeshBuilder.OrientedNormal(p, q, r, Vector3.up);
                        float avgY = (p.y + q.y + r.y) / 3f;
                        float noise = Mathf.PerlinNoise((p.x + 100f) * 0.15f + seedOffset, (p.z + 100f) * 0.15f);
                        Vector2 uv = avgY < -0.2f
                            ? GenPalette.UV(Ramp.RockWarm, 3)
                            : GenPalette.UV(Ramp.LeafGreen, 3 + Mathf.RoundToInt(noise * 2f) - (n.y < 0.9f ? 1 : 0) - (avgY > 3f ? 1 : 0));
                        ground.Triangle(p, q, r, Vector3.up, uv);
                    }
                }
            }
            GameObject groundGo = MeshObject("Ground", env, ground.ToMesh("Gen_VillageGround" + suffix), nature);
            groundGo.AddComponent<MeshCollider>().sharedMesh = groundGo.GetComponent<MeshFilter>().sharedMesh;

            // Paths (follow the ground, slightly raised) and the paved square.
            var paths = new MeshBuilder();
            var rng = new System.Random(settings.Seed ^ 0x2A7);
            foreach (var path in layout.Paths) PathStrip(paths, rng, path.a, path.b, path.halfWidth);
            const int squareSegments = 32;
            for (int k = 0; k < squareSegments; k++)
            {
                float a0 = k * Mathf.PI * 2f / squareSegments, a1 = (k + 1) * Mathf.PI * 2f / squareSegments;
                Vector3 Ring(float a, float r) => new Vector3(Mathf.Cos(a) * r, 0.035f, Mathf.Sin(a) * r);
                paths.Triangle(new Vector3(0f, 0.035f, 0f), Ring(a0, SquareRadius), Ring(a1, SquareRadius), Vector3.up,
                    VillagePalette.UV(VRamp.Stone, 5 + (k % 3 == 0 ? 1 : 0)));
                paths.Triangle(Ring(a0, SquareRadius), Ring(a0, SquareRadius + 0.6f), Ring(a1, SquareRadius), Vector3.up, VillagePalette.UV(VRamp.Stone, 3));
                paths.Triangle(Ring(a1, SquareRadius), Ring(a0, SquareRadius + 0.6f), Ring(a1, SquareRadius + 0.6f), Vector3.up, VillagePalette.UV(VRamp.Stone, 3));
            }
            MeshObject("PathsAndSquare", env, paths.ToMesh("Gen_VillagePaths" + suffix), village);

            // Stream surface.
            var water = new MeshBuilder();
            const float halfWidth = 1.2f, step = 1.5f;
            for (float x = -WorldHalf; x < WorldHalf; x += step)
            {
                float xb = x + step;
                Vector3 a = new Vector3(x, -0.18f, StreamZ(x) - halfWidth), b = new Vector3(x, -0.18f, StreamZ(x) + halfWidth);
                Vector3 c = new Vector3(xb, -0.18f, StreamZ(xb) - halfWidth), d = new Vector3(xb, -0.18f, StreamZ(xb) + halfWidth);
                Vector2 uv = VillagePalette.UV(VRamp.Water, 4 + (Mathf.RoundToInt(x) % 3 == 0 ? 1 : 0));
                water.Triangle(a, b, c, Vector3.up, uv);
                water.Triangle(c, b, d, Vector3.up, uv);
            }
            MeshObject("Stream", env, water.ToMesh("Gen_VillageWater" + suffix), village);
        }

        static void PathStrip(MeshBuilder mb, System.Random rng, Vector2 a, Vector2 b, float halfWidth)
        {
            Vector2 dir = (b - a).normalized;
            var side = new Vector2(-dir.y, dir.x) * halfWidth;
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len));
            for (int i = 0; i < steps; i++)
            {
                Vector2 p0 = Vector2.Lerp(a, b, i / (float)steps), p1 = Vector2.Lerp(a, b, (i + 1) / (float)steps);
                Vector3 l0 = OnGround(p0 - side, 0.03f), r0 = OnGround(p0 + side, 0.03f);
                Vector3 l1 = OnGround(p1 - side, 0.03f), r1 = OnGround(p1 + side, 0.03f);
                Vector2 uv = VillagePalette.UV(VRamp.PathDirt, 4 + Palette.FaceJitter(rng, 0.6f));
                mb.Triangle(l0, r0, l1, Vector3.up, uv);
                mb.Triangle(l1, r0, r1, Vector3.up, uv);
            }
            // Round caps so joints and bends read as worn trails.
            foreach (Vector2 end in new[] { a, b })
            {
                const int segs = 8;
                for (int k = 0; k < segs; k++)
                {
                    float a0 = k * Mathf.PI * 2f / segs, a1 = (k + 1) * Mathf.PI * 2f / segs;
                    Vector2 e0 = end + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * halfWidth;
                    Vector2 e1 = end + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * halfWidth;
                    mb.Triangle(OnGround(end, 0.028f), OnGround(e0, 0.028f), OnGround(e1, 0.028f), Vector3.up, VillagePalette.UV(VRamp.PathDirt, 4));
                }
            }
        }

        // ───────────────────────────── Buildings and paths ─────────────────────────────

        struct Door
        {
            public string Name;
            public Vector2 Point;
            public Obb Footprint;
        }

        static float FacingCentre(Vector2 p) => Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg;

        static List<Door> PlanBuildings(VillageLayout layout)
        {
            var doors = new List<Door>();
            foreach (Slot slot in BuildingSlots)
            {
                BuildingGenerator.Dimensions d = BuildingGenerator.BaseDimensions(slot.Kind);
                float w = d.Width * settings.Size, depth = d.Depth * settings.Size;
                float yaw = FacingCentre(slot.Position);
                var footprint = new Obb(slot.Position, new Vector2((w + 0.3f) * 0.5f + 0.1f, (depth + 0.3f) * 0.5f + 0.1f), yaw);
                layout.AddFootprint(footprint, slot.Name);
                float doorX = slot.Kind == AssetCategory.Shop ? w * 0.5f - 1.3f
                    : slot.Kind == AssetCategory.Cottage && slot.Variant % 3 == 2 ? 0.85f : 0f;
                doors.Add(new Door { Name = slot.Name, Point = footprint.ToWorld(doorX, depth * 0.5f + 1.2f), Footprint = footprint });
            }
            return doors;
        }

        static void PlanPaths(VillageLayout layout, List<Door> doors)
        {
            AddPolyline(layout, MainRoad, 3f);
            AddPolyline(layout, NorthRoad, 3f);
            AddPolyline(layout, ExitRoad, 3f);
            foreach (Door door in doors)
                layout.AddPath(door.Point, door.Point.normalized * (SquareRadius - 0.3f), 1.8f);
        }

        static void AddPolyline(VillageLayout layout, Vector2[] points, float width)
        {
            for (int i = 0; i < points.Length - 1; i++) layout.AddPath(points[i], points[i + 1], width);
        }

        static void PlaceBuildings(Transform parent, VillageLayout layout)
        {
            foreach (Slot slot in BuildingSlots)
            {
                GameObject prefab = Prefab(slot.Kind, slot.Variant);
                if (prefab == null) continue;
                GameObject go = Place(prefab, parent, OnGround(slot.Position), FacingCentre(slot.Position));
                go.name = slot.Name;
            }
        }

        // ───────────────────────────── Gameplay objects ─────────────────────────────

        static DialogueView BuildGameplay(out GameObject player)
        {
            player = Phase1SceneBuilder.BuildPlayer(OnGround(SpawnXZ, 0.05f));
            Phase1SceneBuilder.BuildCamera(player);
            WeatherTools.WeatherSetup.AddToScene();
            WindTools.WindSetup.AddToScene();
            var registry = new GameObject("CollectibleRegistry").AddComponent<CollectibleRegistry>();
            var quests = new GameObject("QuestManager").AddComponent<QuestManager>();
            Phase1SceneBuilder.SetRef(quests, "registry", registry);
            return Phase1SceneBuilder.BuildHud(player, quests);
        }

        static void Readable(GameObject sign, DialogueView dialogue, params string[] lines)
        {
            DialogueInteractable read = sign.AddComponent<DialogueInteractable>();
            var so = new SerializedObject(read);
            so.FindProperty("speakerName").stringValue = "Signpost";
            so.FindProperty("promptVerb").stringValue = "Read";
            so.FindProperty("faceInteractor").boolValue = false;
            Phase1SceneBuilder.SetStrings(so.FindProperty("lines"), lines);
            so.FindProperty("dialogueView").objectReferenceValue = dialogue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A wind-swung hanging sign beside the main road, at the first free candidate spot.</summary>
        static void BuildHangingSign(Transform parent, VillageLayout layout, DialogueView dialogue)
        {
            // (post, yaw): the arm points away from the road so the board hangs beside it, facing walkers.
            var candidates = new[] { (new Vector2(-3.2f, -15f), 180f), (new Vector2(3.2f, -18f), 0f), (new Vector2(-3.2f, -21f), 180f), (new Vector2(3.4f, 23f), 0f) };
            foreach (var (post, yaw) in candidates)
            {
                Vector3 arm3 = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                var arm = new Vector2(arm3.x, arm3.z);
                if (!layout.IsFree(post, 0.4f) || !layout.IsFree(post + arm * 0.7f, 0.3f) || Mathf.Abs(post.y - StreamZ(post.x)) < 3f) continue;
                AmbientMotionSetup.AddHangingSign(parent, OnGround(post), yaw, settings.Seed, dialogue,
                    "The Breezy Bough — Greenhaven's notice sign. It creaks when the wind picks up.",
                    "Press F2 to open the breeze controls.");
                layout.AddKeepClear(post + arm * 0.45f, 0.9f);
                return;
            }
            log.AppendLine("⚠ No free spot for the hanging sign.");
        }

        // ───────────────────────────── Square, road, gardens ─────────────────────────────

        static void BuildSquare(Transform parent, VillageLayout layout, DialogueView dialogue)
        {
            Transform square = Group("Village Square", parent);
            Place(Prefab(AssetCategory.Well, 0), square, OnGround(Vector2.zero), 0f);
            layout.AddKeepClear(Vector2.zero, 1.2f);

            // Benches facing the well, in the gaps between the road and the diagonal door paths.
            float[] benchAngles = { 67f, 113f, 247f, 293f };
            for (int i = 0; i < benchAngles.Length; i++)
            {
                float a = benchAngles[i] * Mathf.Deg2Rad;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6.8f;
                TryPlace(Prefab(AssetCategory.Bench, i % 2), square, layout, p, FacingCentre(p), new Vector2(0.85f, 0.3f));
            }

            // Lanterns at the four road/square entrances.
            foreach (var p in new[] { new Vector2(2.4f, SquareRadius + 0.7f), new Vector2(-2.4f, SquareRadius + 0.7f),
                         new Vector2(2.4f, -SquareRadius - 0.7f), new Vector2(-2.4f, -SquareRadius - 0.7f) })
                Place(Prefab(AssetCategory.Lantern, 0), square, OnGround(p), p.x > 0f ? -90f : 90f);

            // Flowers around the well.
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f + 0.2f;
                Place(Prefab(AssetCategory.Flower, i % 4), square, OnGround(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.45f), i * 47f);
            }

            GameObject sign = Place(Prefab(AssetCategory.Signpost, 0), square, OnGround(new Vector2(2.7f, SquareRadius + 1.6f)), 200f);
            if (sign != null)
                Readable(sign, dialogue, "North: the Whispering Forest — cross the bridge and follow the trail.",
                    "West: the Baker's Cottage. East: the Weaver's Cottage. South: the road home.");
        }

        static void BuildRoad(Transform parent, VillageLayout layout, DialogueView dialogue)
        {
            Transform road = Group("Road", parent);

            // Lanterns along the south approach and at both ends of the bridge.
            var lamps = new List<(Vector2 p, float yaw)>
            {
                (new Vector2(2.4f, -16f), -90f), (new Vector2(-2.4f, -23f), 90f),
                (new Vector2(2.1f, 12.7f), -90f), (new Vector2(-2.1f, 12.7f), 90f),
                (new Vector2(2.1f, 21.3f), -90f), (new Vector2(-2.1f, 21.3f), 90f),
            };
            foreach (var (p, yaw) in lamps) Place(Prefab(AssetCategory.Lantern, 1), road, OnGround(p), yaw);

            // Welcome sign at the spawn.
            GameObject welcome = Place(Prefab(AssetCategory.Signpost, 1), road, OnGround(new Vector2(2.7f, -27f)), 160f);
            if (welcome != null)
                Readable(welcome, dialogue, "Welcome to Greenhaven!", "The village square is straight ahead. Mind the stream to the north.");

            // Bridge over the stream on the main road.
            Place(Prefab(AssetCategory.Bridge, 0), road, new Vector3(0f, 0f, StreamZ(0f)), 0f);

            // Gated exit north, fenced to either side.
            Vector2 gateAt = new Vector2(1.3f, 31f);
            Vector2 roadDir = (ExitRoad[3] - ExitRoad[2]).normalized;
            float gateYaw = Mathf.Atan2(roadDir.x, roadDir.y) * Mathf.Rad2Deg;
            Place(Prefab(AssetCategory.Gate, 0), road, OnGround(gateAt), gateYaw);
            var gateObb = new Obb(gateAt, new Vector2(1.2f, 0.3f), gateYaw);
            layout.AddKeepClear(gateAt, 1.6f);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 p = gateObb.ToWorld(side * (2.15f + i * PropGenerator.FenceLength), 0f);
                    Place(Prefab(AssetCategory.Fence, i % 2), road, OnGround(p), gateYaw);
                    layout.AddFootprint(new Obb(p, new Vector2(1f, 0.15f), gateYaw), "Exit fence");
                }
            }
            GameObject exitSign = Place(Prefab(AssetCategory.Signpost, 0), road, OnGround(gateAt + new Vector2(2.3f, -1.8f)), 200f);
            if (exitSign != null)
                Readable(exitSign, dialogue, "The Whispering Forest lies beyond these hills.",
                    "(The forest trail opens in a future update. For now, explore the village!)");
        }

        static void BuildGarden(Transform parent, VillageLayout layout, Slot slot)
        {
            Obb house = layout.Footprints.First(f => f.name == slot.Name).box;
            const float gardenW = 6f, gardenD = 4f;
            // Try beside the cottage (left, then right); keep the first spot that blocks nothing.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 centre = house.ToWorld(side * (house.HalfSize.x + gardenW * 0.5f + 0.8f), -0.4f);
                var garden = new Obb(centre, new Vector2(gardenW * 0.5f, gardenD * 0.5f), house.Yaw);
                if (!layout.IsFree(garden, 0.4f) || centre.magnitude > 24f) continue;

                Transform g = Group($"Garden ({slot.Name})", parent);
                layout.AddFootprint(garden, $"Garden ({slot.Name})");
                // Fence perimeter: 3 sections front/back, 2 per side; gate in the middle of the front.
                for (int i = 0; i < 3; i++)
                {
                    float x = -gardenW * 0.5f + PropGenerator.FenceLength * (i + 0.5f);
                    Vector2 front = garden.ToWorld(x, gardenD * 0.5f), back = garden.ToWorld(x, -gardenD * 0.5f);
                    Place(i == 1 ? Prefab(AssetCategory.Gate, 0) : Prefab(AssetCategory.Fence, 1), g, OnGround(front), house.Yaw);
                    Place(Prefab(AssetCategory.Fence, 1), g, OnGround(back), house.Yaw);
                }
                for (int i = 0; i < 2; i++)
                {
                    float z = -gardenD * 0.5f + PropGenerator.FenceLength * (i + 0.5f);
                    foreach (float sx in new[] { -1f, 1f })
                        Place(Prefab(AssetCategory.Fence, 1), g, OnGround(garden.ToWorld(sx * gardenW * 0.5f, z)), house.Yaw + 90f);
                }
                // Neat rows of flowers and a couple of bushes inside.
                for (int r = 0; r < 2; r++)
                    for (int c = 0; c < 5; c++)
                        Place(Prefab(AssetCategory.Flower, (r * 5 + c) % 4), g, OnGround(garden.ToWorld(-2.2f + c * 1.1f, -1.1f + r * 1.1f)), c * 61f);
                Place(Prefab(AssetCategory.Bush, 2), g, OnGround(garden.ToWorld(-2.2f, 1.1f)), 30f);
                Place(Prefab(AssetCategory.Bush, 0), g, OnGround(garden.ToWorld(2.2f, 1.1f)), 200f);
                return;
            }
            log.AppendLine($"No clear spot for a garden beside {slot.Name}; skipped.");
        }

        static void BuildShopYard(Transform parent, VillageLayout layout)
        {
            Slot shop = BuildingSlots.First(b => b.Kind == AssetCategory.Shop);
            Obb box = layout.Footprints.First(f => f.name == shop.Name).box;
            Transform yard = Group("Shop Yard", parent);
            // Barrels and stacked crates against the side wall away from the door.
            var spots = new (float x, float z, AssetCategory kind, int variant, float y)[]
            {
                (-box.HalfSize.x - 0.6f, box.HalfSize.y - 0.9f, AssetCategory.Barrel, 0, 0f),
                (-box.HalfSize.x - 0.6f, box.HalfSize.y - 1.8f, AssetCategory.Barrel, 1, 0f),
                (-box.HalfSize.x - 0.7f, box.HalfSize.y - 3.0f, AssetCategory.Crate, 0, 0f),
                (-box.HalfSize.x - 0.7f, box.HalfSize.y - 3.0f, AssetCategory.Crate, 1, 0.62f),
            };
            foreach (var s in spots)
            {
                Vector2 p = box.ToWorld(s.x, s.z);
                if (s.y == 0f && !layout.IsFree(p, 0.5f)) continue;
                GameObject go = Place(Prefab(s.kind, s.variant), yard, OnGround(p, s.y), box.Yaw + s.z * 13f);
                if (go != null && s.y == 0f) layout.AddKeepClear(p, 0.5f);
            }
        }

        static void BuildHousePlanters(Transform parent, VillageLayout layout)
        {
            Transform planters = Group("Planters", parent);
            foreach (Slot slot in BuildingSlots.Where(b => b.Kind == AssetCategory.House))
            {
                Obb box = layout.Footprints.First(f => f.name == slot.Name).box;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 p = box.ToWorld(side * 1.9f, box.HalfSize.y + 0.35f); // clear of the door path
                    TryPlace(Prefab(AssetCategory.Planter, side < 0f ? 0 : 1), planters, layout, p, box.Yaw, new Vector2(0.64f, 0.29f));
                }
            }
        }

        static void BuildStreamBlockers(Transform env)
        {
            // Invisible walls along the stream so the bridge is the way across.
            Transform blockers = Group("StreamBlockers", env);
            void Block(float x0, float x1)
            {
                float xc = (x0 + x1) * 0.5f;
                Vector3 dir = new Vector3(x1 - x0, 0f, StreamZ(x1) - StreamZ(x0));
                var go = new GameObject("Blocker");
                go.transform.SetParent(blockers, false);
                go.transform.SetPositionAndRotation(new Vector3(xc, 0.5f, StreamZ(xc)), Quaternion.LookRotation(dir));
                go.AddComponent<BoxCollider>().size = new Vector3(0.6f, 2.2f, dir.magnitude + 0.1f);
            }
            for (float x = -WorldHalf; x < WorldHalf; x += 2f)
            {
                float x1 = x + 2f;
                if (x1 <= -BridgeHalfWidth || x >= BridgeHalfWidth) Block(x, x1);
                else if (x < -BridgeHalfWidth) Block(x, -BridgeHalfWidth);
                else if (x1 > BridgeHalfWidth) Block(BridgeHalfWidth, x1);
            }
        }

        static void BuildBoundaries(Transform env)
        {
            Transform bounds = Group("Boundaries", env);
            float side = 2f * WallRadius * Mathf.Tan(Mathf.PI / 8f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var go = new GameObject($"Wall{i}");
                go.transform.SetParent(bounds, false);
                go.transform.SetPositionAndRotation(new Vector3(Mathf.Sin(a) * WallRadius, 4f, Mathf.Cos(a) * WallRadius), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f));
                go.AddComponent<BoxCollider>().size = new Vector3(side + 1f, 14f, 1f);
            }
        }

        // ───────────────────────────── Vegetation ─────────────────────────────

        static void BuildVegetation(Transform parent, VillageLayout layout, float density)
        {
            var rng = new System.Random(settings.Seed ^ 0x5EED);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var trees = new List<Vector2>();
            bool Spaced(Vector2 p, float min) => trees.All(t => (t - p).sqrMagnitude >= min * min);
            bool NearStream(Vector2 p, float m) => Mathf.Abs(p.y - StreamZ(p.x)) < 2.4f + m;
            bool Inside(Vector2 p) => p.magnitude < WallRadius - 1f;

            Transform treeGroup = Group("Trees", parent), rockGroup = Group("Rocks", parent);
            Transform bushGroup = Group("Bushes", parent), flowerGroup = Group("Flowers", parent);
            Transform grassGroup = Group("Grass", parent);
            int grassVariants = Generators.For(AssetCategory.Grass).DefaultCount;
            int flowerVariants = Generators.For(AssetCategory.Flower).DefaultCount;

            void Scatter(int count, float minR, float maxR, float spacing, float margin, AssetCategory kind, int variants,
                Transform group, float minScale, float maxScale, float sink, bool isTree)
            {
                for (int attempt = 0, placed = 0; attempt < count * 40 && placed < count; attempt++)
                {
                    float a = R(0f, Mathf.PI * 2f), r = R(minR, maxR);
                    var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                    if (!Inside(p) || NearStream(p, margin) || !layout.IsFree(p, margin)) continue;
                    if (isTree && !Spaced(p, spacing)) continue;
                    GameObject go = Place(Prefab(kind, rng.Next(variants)), group, OnGround(p, -sink), R(0f, 360f), R(minScale, maxScale));
                    if (go == null) return;
                    if (isTree) trees.Add(p);
                    placed++;
                }
            }

            Scatter(Mathf.RoundToInt(24 * density), 18f, 30f, 4.5f, 2f, AssetCategory.BroadleafTree, 3, treeGroup, 0.85f, 1.2f, 0.05f, true);
            Scatter(Mathf.RoundToInt(26 * density), 26f, WallRadius - 1f, 4f, 1.5f, AssetCategory.PineTree, 3, treeGroup, 0.9f, 1.3f, 0.05f, true);
            Scatter(Mathf.RoundToInt(12 * density), 22f, WallRadius - 1f, 0f, 1.2f, AssetCategory.Rock, 6, rockGroup, 0.8f, 1.4f, 0.08f, false);
            Scatter(Mathf.RoundToInt(16 * density), 10f, 24f, 0f, 1f, AssetCategory.Bush, 3, bushGroup, 0.8f, 1.15f, 0f, false);

            Scatter(Mathf.RoundToInt(130 * density), SquareRadius + 1.5f, 30f, 0f, 0.3f, AssetCategory.Grass, grassVariants, grassGroup, 0.8f, 1.25f, 0f, false);

            // Grass and the odd flower along path edges, so the walk is framed by moving plants.
            float totalLength = layout.Paths.Sum(path => Vector2.Distance(path.a, path.b));
            int edgeTarget = Mathf.RoundToInt(110 * density);
            for (int attempt = 0, placed = 0; totalLength > 0f && attempt < edgeTarget * 30 && placed < edgeTarget; attempt++)
            {
                // A segment picked by length, then a point just beside it.
                float pick = R(0f, totalLength);
                var seg = layout.Paths[layout.Paths.Count - 1];
                foreach (var path in layout.Paths)
                {
                    float len = Vector2.Distance(path.a, path.b);
                    if (pick <= len)
                    {
                        seg = path;
                        break;
                    }
                    pick -= len;
                }
                Vector2 dir = (seg.b - seg.a).normalized;
                Vector2 p = Vector2.Lerp(seg.a, seg.b, R(0f, 1f)) +
                            new Vector2(-dir.y, dir.x) * (rng.NextDouble() < 0.5 ? -1f : 1f) * (seg.halfWidth + R(0.35f, 2.2f));
                if (!Inside(p) || NearStream(p, 0f) || !layout.IsFree(p, 0.25f)) continue;
                GameObject go = rng.NextDouble() < 0.2
                    ? Place(Prefab(AssetCategory.Flower, rng.Next(flowerVariants)), flowerGroup, OnGround(p), R(0f, 360f), R(0.9f, 1.15f))
                    : Place(Prefab(AssetCategory.Grass, rng.Next(grassVariants)), grassGroup, OnGround(p), R(0f, 360f), R(0.8f, 1.2f));
                if (go == null) break;
                placed++;
            }

            // Rocks along the stream banks.
            for (int i = 0, placed = 0; i < 200 && placed < Mathf.RoundToInt(8 * density); i++)
            {
                float x = R(-30f, 30f);
                if (Mathf.Abs(x) < 4f) continue;
                var p = new Vector2(x, StreamZ(x) + (rng.NextDouble() < 0.5 ? -1f : 1f) * R(2.5f, 3.1f));
                if (!Inside(p) || !layout.IsFree(p, 0.6f)) continue;
                Place(Prefab(AssetCategory.Rock, rng.Next(6)), rockGroup, OnGround(p, -0.1f), R(0f, 360f), R(0.5f, 0.9f));
                placed++;
            }

            // Bushes tucked behind each building's back corners.
            foreach (var f in layout.Footprints.Where(f => BuildingSlots.Any(b => b.Name == f.name)))
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 p = f.box.ToWorld(side * (f.box.HalfSize.x - 0.3f), -f.box.HalfSize.y - 0.7f);
                    if (layout.IsFree(p, 0.3f)) Place(Prefab(AssetCategory.Bush, rng.Next(3)), bushGroup, OnGround(p), R(0f, 360f));
                }

            // Flower patches in the meadows between buildings.
            for (int patch = 0, made = 0; patch < 300 && made < Mathf.RoundToInt(12 * density); patch++)
            {
                float a = R(0f, Mathf.PI * 2f), r = R(SquareRadius + 2f, 24f);
                var c = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                if (NearStream(c, 0.5f) || !layout.IsFree(c, 0.8f)) continue;
                made++;
                int flowers = rng.Next(3, 6);
                for (int k = 0; k < flowers; k++)
                {
                    Vector2 p = c + new Vector2(R(-1.2f, 1.2f), R(-1.2f, 1.2f));
                    if (layout.IsFree(p, 0.2f)) Place(Prefab(AssetCategory.Flower, rng.Next(flowerVariants)), flowerGroup, OnGround(p), R(0f, 360f), R(0.9f, 1.2f));
                }
            }
        }

        // ───────────────────────────── Validation ─────────────────────────────

        static void ValidateLayout(VillageLayout layout, List<Door> doors, GameObject player)
        {
            int issues = 0;
            var buildings = layout.Footprints.Where(f => BuildingSlots.Any(b => b.Name == f.name)).ToList();
            for (int i = 0; i < buildings.Count; i++)
                for (int j = i + 1; j < buildings.Count; j++)
                    if (buildings[i].box.Overlaps(buildings[j].box, 1.5f))
                    {
                        log.AppendLine($"⚠ {buildings[i].name} and {buildings[j].name} are closer than 1.5 m.");
                        issues++;
                    }

            foreach (Door door in doors)
            {
                // The door approach must not be inside any other footprint (houses, gardens, fences).
                foreach (var f in layout.Footprints)
                {
                    if (f.box.Centre == door.Footprint.Centre) continue;
                    if (f.box.Contains(door.Point, 0.5f))
                    {
                        log.AppendLine($"⚠ The door of {door.Name} is blocked by {f.name}.");
                        issues++;
                    }
                }
            }

            Vector2 spawn = new Vector2(player.transform.position.x, player.transform.position.z);
            if (layout.InFootprint(spawn, 0.6f, out string blocker))
            {
                log.AppendLine($"⚠ Player spawn is inside {blocker}.");
                issues++;
            }
            if (Mathf.Abs(player.transform.position.y - Height(spawn.x, spawn.y)) > 0.2f)
            {
                log.AppendLine("⚠ Player spawn is not on the ground.");
                issues++;
            }

            log.AppendLine(issues == 0
                ? $"Layout checks passed: {buildings.Count} buildings with clearance, {doors.Count} doors reachable, spawn on clear ground."
                : $"Layout checks found {issues} issue(s).");
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        static GameObject Prefab(AssetCategory category, int variant)
        {
            IAssetGenerator gen = Generators.For(category);
            string path = GeneratedAssetStore.PrefabPath(GeneratedAssetStore.AssetName(gen, settings.Seed, variant));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) missing.Add(Path.GetFileNameWithoutExtension(path));
            return prefab;
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, float scale = 1f)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Places a prop only where it blocks no path or footprint, and records its footprint.</summary>
        static void TryPlace(GameObject prefab, Transform parent, VillageLayout layout, Vector2 p, float yaw, Vector2 halfSize)
        {
            var box = new Obb(p, halfSize, yaw);
            if (!layout.IsFree(box, 0.2f)) return;
            if (Place(prefab, parent, OnGround(p), yaw) != null) layout.AddFootprint(box, prefab.name);
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            Mesh asset = GeneratedAssetStore.SaveMesh(mesh, GeneratedAssetStore.MeshPath(mesh.name));
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = asset;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
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
