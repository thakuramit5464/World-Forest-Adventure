using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Greenhaven.Collectibles;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Editor.WindTools;
using Greenhaven.Player;
using Greenhaven.Quests;
using Greenhaven.UI;
using Greenhaven.Wind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.Cloudpeak
{
    /// <summary>
    /// Builds Assets/_Game/Scenes/CloudpeakAdventure.unity: the Whispering Forest, foothills and Cloudpeak
    /// mountain with one continuous trail from the forest entrance (reached from Greenhaven Village's
    /// north gate) to the summit. Terrain and trail come from <see cref="CloudpeakWorld"/>; vegetation and
    /// props reuse the generated asset library. Never touches other scenes.
    /// </summary>
    public static class CloudpeakSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/CloudpeakAdventure.unity";
        const string AssetFolder = GeneratedAssetStore.Root + "/Cloudpeak";
        const int ChunkCells = 25;

        static readonly AssetCategory[] Needed =
        {
            AssetCategory.PineTree, AssetCategory.BroadleafTree, AssetCategory.Rock, AssetCategory.Bush, AssetCategory.Flower, AssetCategory.Grass,
            AssetCategory.Log, AssetCategory.Stump, AssetCategory.Mushroom, AssetCategory.Bridge, AssetCategory.Bench, AssetCategory.Signpost,
            AssetCategory.Lantern, AssetCategory.Gate,
        };

        static CloudpeakWorld world;
        static GeneratorSettings assets;
        static System.Random rng;
        static StringBuilder log;
        static List<string> missing;
        static Material nature, village;
        static readonly List<(Vector2 p, float r)> reserved = new List<(Vector2, float)>();
        static readonly List<GameObject> walkable = new List<GameObject>();
        static Vector3 summitMarker;

        // ───────────────────────────── Entry point ─────────────────────────────

        public static string Build(CloudpeakSettings settings, GeneratorSettings assetSettings)
        {
            if (EditorApplication.isPlaying) return "Stop Play mode before creating the Cloudpeak scene.";
            assets = assetSettings;
            log = new StringBuilder();
            missing = new List<string>();
            reserved.Clear();
            walkable.Clear();

            // 1. Terrain and trail as data first: refuse to build a world whose trail fails the checks.
            world = CloudpeakWorld.Generate(settings);
            CloudpeakWorld.Report report = world.Validate();
            if (!report.Passed &&
                !EditorUtility.DisplayDialog("Trail check failed", report + "\n\nBuild the scene anyway (for inspection)?", "Build anyway", "Cancel"))
                return "Cancelled: the trail failed its checks.\n" + report;

            // 2. Assets: generate only what is missing; offer to upgrade non-swaying plants.
            if (!EnsureAssets(out string why)) return why;

            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild Cloudpeak scene?",
                    $"{ScenePath} and its generated terrain meshes (Generated/Cloudpeak) for seed {world.Settings.Seed} will be rebuilt. " +
                    "No other scene is changed.", "Rebuild", "Cancel"))
                return "Cancelled. Cloudpeak scene unchanged.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            GeneratedAssetStore.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder(GeneratedAssetStore.Root, "Cloudpeak");
            Phase1SceneBuilder.CreateMaterials();
            GreenhavenArt.PrepareImports();
            nature = GeneratedAssetStore.EnsurePaletteMaterial(GenPalette.Nature);
            village = GeneratedAssetStore.EnsurePaletteMaterial(VillagePalette.Palette);
            rng = new System.Random(world.Settings.Seed ^ 0x7EE5);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = world.Settings.HalfSize * 3.4f;

            Transform env = new GameObject("Environment").transform;
            Transform terrain = Group("Terrain", env), props = Group("Props", env), mountain = Group("Mountain Details", env);
            Transform vegetation = Group("Vegetation", env), poi = Group("Points of Interest", env);

            // 3. Terrain, trail surface, brook, bridge, boundaries, distant peaks.
            BuildTerrain(terrain);
            BuildTrailSurface(terrain);
            BuildBrookAndBridge(terrain);
            BuildBoundaries(env);
            BuildDistantPeaks(env);

            // 4. Gameplay (player, camera, HUD, weather, wind) — needed before readable props.
            DialogueView dialogue = BuildGameplay(out GameObject player, out CollectibleRegistry registry);
            WorldTools.WorldLinkSetup.AddToCloudpeak(world, p => world.Surface(p.x, p.y)); // entrance gate → village, and the arrival from it

            // 5. Points of interest, summit landmark, signs, feathers.
            BuildPointsOfInterest(poi, dialogue, registry);

            // 6. Mountain rock formations, then vegetation by zone.
            BuildMountainDetails(mountain);
            BuildVegetation(vegetation);

            // 7. Environmental motion: drifting leaves, optional ambience, F2 panel.
            LeafDrift leaves = AmbientMotionSetup.AddLeafDrift();
            AmbienceController ambience = AmbientMotionSetup.AddAmbience();
            AmbientMotionSetup.AddDebugPanel(Object.FindFirstObjectByType<WindManager>(), leaves, ambience, player, visibleOnStart: false);

            // 8. Validate: data checks, obstacle sweep, a CharacterController walk from the entrance to the summit.
            log.AppendLine(report.ToString());
            CloudpeakValidation.Run(world, player, walkable, summitMarker, log);
            int badRefs = Phase1SceneBuilder.ValidateSceneReferences(scene);
            if (badRefs > 0) log.AppendLine($"{badRefs} scene reference problem(s); see Console.");
            if (missing.Count > 0) log.AppendLine("Missing prefabs: " + string.Join(", ", missing.Distinct()));
            int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            int winds = Object.FindObjectsByType<WindManager>(FindObjectsSortMode.None).Length;
            int players = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Length;
            log.AppendLine($"Scene: {players} player, {listeners} audio listener, {winds} wind manager.");

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (File.Exists(VillageScene.VillageSceneBuilder.ScenePath))
                log.AppendLine("Linked to GreenhavenVillage through the entrance gate. Build scene list:\n" + WorldTools.WorldLinkSetup.UpdateBuildScenes());
            int objects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
            log.Insert(0, $"Cloudpeak scene saved to {ScenePath} ({objects} transforms, terrain seed {world.Settings.Seed}). Not added to Build Settings.\n");
            return log.ToString();
        }

        static bool EnsureAssets(out string message)
        {
            message = null;
            List<IAssetGenerator> absent = GeneratedAssetStore.MissingGenerators(assets.Seed, Needed.Select(Generators.For));
            if (absent.Count > 0)
            {
                if (!EditorUtility.DisplayDialog("Generate Cloudpeak assets?",
                        $"Generated prefabs for seed {assets.Seed} are missing ({string.Join(", ", absent.Select(g => g.DisplayName))}). Generate them now?",
                        "Generate", "Cancel"))
                {
                    message = "Cancelled: assets for this seed are missing. Use Greenhaven ▸ Generate Assets first.";
                    return false;
                }
                log.AppendLine(GeneratedAssetStore.GenerateMissing(absent, assets).Split('\n')[0]);
            }
            AssetCategory[] stale = AmbientMotionSetup.NotWindReady(assets.Seed);
            if (stale.Length > 0)
            {
                string list = string.Join(", ", stale.Select(t => Generators.For(t).DisplayName));
                if (EditorUtility.DisplayDialog("Upgrade vegetation for wind?",
                        $"These plants for seed {assets.Seed} are not wind-ready yet: {list}. Regenerate them so they sway?\n" +
                        "Existing ones are replaced in place (you will be asked to confirm). Skip keeps them static.", "Upgrade", "Skip"))
                    log.AppendLine(GeneratedAssetStore.GenerateMissing(stale.Select(Generators.For), assets).Split('\n')[0]);
                else
                    log.AppendLine($"Wind upgrade skipped: {list} stay static.");
            }
            return true;
        }

        // ───────────────────────────── Terrain ─────────────────────────────

        static void BuildTerrain(Transform parent)
        {
            int seed = world.Settings.Seed;
            float peak = world.Settings.PeakHeight;
            int chunks = Mathf.CeilToInt(world.Cells / (float)ChunkCells);
            int triangles = 0;
            for (int cz = 0; cz < chunks; cz++)
            {
                for (int cx = 0; cx < chunks; cx++)
                {
                    var mb = new MeshBuilder();
                    var noise = new System.Random(seed ^ (cx * 31 + cz * 977));
                    for (int j = cz * ChunkCells; j < Mathf.Min(world.Cells, (cz + 1) * ChunkCells); j++)
                    {
                        for (int i = cx * ChunkCells; i < Mathf.Min(world.Cells, (cx + 1) * ChunkCells); i++)
                        {
                            Vector3 V(int a, int b) => new Vector3(world.Origin + a * world.Cell, world.HeightAt(a, b), world.Origin + b * world.Cell);
                            Vector3 a0 = V(i, j), b0 = V(i + 1, j), c0 = V(i, j + 1), d0 = V(i + 1, j + 1);
                            foreach (var (p, q, r) in new[] { (a0, c0, b0), (b0, c0, d0) })
                            {
                                Vector3 n = MeshBuilder.OrientedNormal(p, q, r, Vector3.up);
                                mb.Triangle(p, q, r, Vector3.up, GroundColour((p + q + r) / 3f, n, peak, noise));
                            }
                        }
                    }
                    if (mb.TriangleCount == 0) continue;
                    triangles += mb.TriangleCount;
                    string name = $"Gen_CloudpeakGround_s{seed}_{cx}_{cz}";
                    Mesh mesh = GeneratedAssetStore.SaveMesh(mb.ToMesh(name), $"{AssetFolder}/{name}.asset");
                    GameObject go = MeshObject($"Ground {cx},{cz}", parent, mesh, nature);
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    walkable.Add(go);
                }
            }
            log.AppendLine($"Terrain: {world.Cells}×{world.Cells} cells of {world.Cell:0.00} m in {chunks * chunks} chunks, {triangles} triangles.");
        }

        static Vector2 GroundColour(Vector3 centre, Vector3 n, float peak, System.Random noise)
        {
            var p = new Vector2(centre.x, centre.z);
            float t = Vector2.Distance(p, world.Mountain) / world.Settings.MountainRadius;
            float slopeDeg = Vector3.Angle(n, Vector3.up);
            float jitter = Mathf.PerlinNoise(centre.x * 0.11f + 7f, centre.z * 0.11f + 3f);
            int faceJitter = noise.NextDouble() < 0.25 ? (noise.Next(2) * 2 - 1) : 0;

            if (world.DistanceToTrailEdge(p, 2f) < 0.4f) return GenPalette.UV(Ramp.RockWarm, 3 + faceJitter); // packed earth under the trail
            float bank = world.NaturalHeight(centre.x, centre.z, brook: false) - centre.y;
            if (bank > 0.45f && Mathf.Abs(centre.z - world.BrookZ(centre.x)) < 3.2f) return GenPalette.UV(Ramp.RockWarm, 2); // brook bed
            if (slopeDeg > 42f) return GenPalette.UV(Ramp.RockGrey, 2 + faceJitter + (centre.y > peak * 0.6f ? 1 : 0)); // cliffs
            if (slopeDeg > 32f) return GenPalette.UV(jitter > 0.5f ? Ramp.RockGrey : Ramp.RockWarm, 3 + faceJitter); // exposed rock
            if (centre.y > peak * 0.86f) return jitter > 0.45f ? GenPalette.UV(Ramp.Petal, 3 + faceJitter) : GenPalette.UV(Ramp.RockGrey, 5); // frosty summit
            if (centre.y > peak * 0.45f) return GenPalette.UV(Ramp.LeafGreen, 5 + faceJitter - (jitter > 0.7f ? 2 : 0)); // alpine meadow
            if (t < 1f) return GenPalette.UV(Ramp.LeafGreen, 4 + faceJitter - (slopeDeg > 20f ? 1 : 0)); // foothills
            // Forest floor: mossy, darker under the canopy.
            return jitter > 0.55f ? GenPalette.UV(Ramp.PineGreen, 3 + faceJitter) : GenPalette.UV(Ramp.LeafGreen, 3 + faceJitter);
        }

        static void BuildTrailSurface(Transform parent)
        {
            var mb = new MeshBuilder();
            var jitter = new System.Random(world.Settings.Seed ^ 0x7A11);
            List<TrailPoint> trail = world.Trail;
            Vector2 Side(int i)
            {
                Vector2 d = (trail[Mathf.Min(i + 1, trail.Count - 1)].Position - trail[Mathf.Max(i - 1, 0)].Position).normalized;
                return new Vector2(-d.y, d.x);
            }
            for (int i = 0; i < trail.Count - 1; i++)
            {
                TrailPoint a = trail[i], b = trail[i + 1];
                if (a.Surface == TrailSurface.Bridge || b.Surface == TrailSurface.Bridge) continue;
                Vector2 sa = Side(i) * a.HalfWidth, sb = Side(i + 1) * b.HalfWidth;
                Vector3 la = world.OnSurface(a.Position - sa, 0.05f), ca = world.OnSurface(a.Position, 0.05f), ra = world.OnSurface(a.Position + sa, 0.05f);
                Vector3 lb = world.OnSurface(b.Position - sb, 0.05f), cb = world.OnSurface(b.Position, 0.05f), rb = world.OnSurface(b.Position + sb, 0.05f);
                bool stone = a.Surface == TrailSurface.Stone;
                Vector2 uvL = stone ? VillagePalette.UV(VRamp.Stone, 4 + ((i / 2) % 2) + Palette.FaceJitter(jitter, 0.5f)) : VillagePalette.UV(VRamp.PathDirt, 3 + Palette.FaceJitter(jitter, 0.6f));
                Vector2 uvR = stone ? VillagePalette.UV(VRamp.Stone, 5 - ((i / 2) % 2) + Palette.FaceJitter(jitter, 0.5f)) : VillagePalette.UV(VRamp.PathDirt, 4 + Palette.FaceJitter(jitter, 0.6f));
                mb.Triangle(la, ca, lb, Vector3.up, uvL);
                mb.Triangle(lb, ca, cb, Vector3.up, uvL);
                mb.Triangle(ca, ra, cb, Vector3.up, uvR);
                mb.Triangle(cb, ra, rb, Vector3.up, uvR);
            }

            // Stone paving on the rest stops, viewpoint and summit (the glade stays grassy).
            foreach (Platform pl in world.Platforms)
            {
                if (pl.Name == "Whispering Glade") continue;
                const int segments = 20;
                for (int k = 0; k < segments; k++)
                {
                    float a0 = k * Mathf.PI * 2f / segments, a1 = (k + 1) * Mathf.PI * 2f / segments;
                    Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                    for (int ring = 0; ring < 2; ring++)
                    {
                        float r0 = pl.Radius * ring * 0.5f, r1 = pl.Radius * (ring + 1) * 0.5f;
                        Vector2 uv = VillagePalette.UV(VRamp.Stone, 4 + ((k + ring) % 2));
                        Vector3 p00 = world.OnSurface(pl.Centre + d0 * r0, 0.035f), p01 = world.OnSurface(pl.Centre + d1 * r0, 0.035f);
                        Vector3 p10 = world.OnSurface(pl.Centre + d0 * r1, 0.035f), p11 = world.OnSurface(pl.Centre + d1 * r1, 0.035f);
                        if (ring == 0) mb.Triangle(p00, p10, p11, Vector3.up, uv);
                        else
                        {
                            mb.Triangle(p00, p10, p11, Vector3.up, uv);
                            mb.Triangle(p00, p11, p01, Vector3.up, uv);
                        }
                    }
                }
            }
            string name = $"Gen_CloudpeakTrail_s{world.Settings.Seed}";
            Mesh mesh = GeneratedAssetStore.SaveMesh(mb.ToMesh(name), $"{AssetFolder}/{name}.asset");
            MeshObject("Trail", parent, mesh, village); // visual only: the carved terrain underneath is the walkable surface
            log.AppendLine($"Trail: {world.Trail[world.Trail.Count - 1].Distance:0} m, {world.Hairpins.Count} switchback hairpin(s), {mb.TriangleCount} triangles.");
        }

        static void BuildBrookAndBridge(Transform parent)
        {
            var mb = new MeshBuilder();
            float H = world.Settings.HalfSize;
            const float step = 2f, halfWidth = 1.5f;
            for (float x = -H; x < H - 0.01f; x += step)
            {
                float xb = Mathf.Min(x + step, H);
                float ya = world.Surface(x, world.BrookZ(x)) + 0.45f, yb = world.Surface(xb, world.BrookZ(xb)) + 0.45f;
                Vector3 a = new Vector3(x, ya, world.BrookZ(x) - halfWidth), b = new Vector3(x, ya, world.BrookZ(x) + halfWidth);
                Vector3 c = new Vector3(xb, yb, world.BrookZ(xb) - halfWidth), d = new Vector3(xb, yb, world.BrookZ(xb) + halfWidth);
                Vector2 uv = VillagePalette.UV(VRamp.Water, 4 + (Mathf.RoundToInt(x / step) % 3 == 0 ? 1 : 0));
                mb.Triangle(a, b, c, Vector3.up, uv);
                mb.Triangle(c, b, d, Vector3.up, uv);
            }
            string name = $"Gen_CloudpeakBrook_s{world.Settings.Seed}";
            MeshObject("Whispering Brook", parent, GeneratedAssetStore.SaveMesh(mb.ToMesh(name), $"{AssetFolder}/{name}.asset"), village);

            float yaw = Mathf.Atan2(world.BridgeDirection.x, world.BridgeDirection.y) * Mathf.Rad2Deg;
            var deck = new Vector3(world.BridgeCentre.x, world.BridgeDeckHeight - 0.2f + 0.03f, world.BridgeCentre.y); // deck ends meet the banks
            GameObject bridge = Place(Prefab(AssetCategory.Bridge, 0), parent, deck, yaw);
            if (bridge != null)
            {
                bridge.name = "Brook Bridge";
                walkable.Add(bridge);
            }
            reserved.Add((world.BridgeCentre, 5f));
        }

        static void BuildBoundaries(Transform env)
        {
            // Hills around the edge are the real barrier; these low-cost walls only stop players who climb them.
            Transform walls = Group("Boundary (backstop)", env);
            float H = world.Settings.HalfSize;
            void Wall(Vector3 centre, Vector3 size)
            {
                var go = new GameObject("Wall");
                go.transform.SetParent(walls, false);
                go.transform.position = centre;
                go.AddComponent<BoxCollider>().size = size;
            }
            Wall(new Vector3(0f, 20f, H + 0.5f), new Vector3(H * 2f + 2f, 80f, 1f));
            Wall(new Vector3(0f, 20f, -H - 0.5f), new Vector3(H * 2f + 2f, 80f, 1f));
            Wall(new Vector3(H + 0.5f, 20f, 0f), new Vector3(1f, 80f, H * 2f + 2f));
            Wall(new Vector3(-H - 0.5f, 20f, 0f), new Vector3(1f, 80f, H * 2f + 2f));
        }

        static void BuildDistantPeaks(Transform env)
        {
            string name = $"Gen_CloudpeakDistant_s{world.Settings.Seed}";
            Mesh mesh = GeneratedAssetStore.SaveMesh(CloudpeakMeshes.DistantPeaks(world.Settings.Seed, name, world.Settings.HalfSize), $"{AssetFolder}/{name}.asset");
            GameObject go = MeshObject("Distant Peaks (no collision)", env, mesh, nature);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ───────────────────────────── Gameplay ─────────────────────────────

        static DialogueView BuildGameplay(out GameObject player, out CollectibleRegistry registry)
        {
            Vector3 spawn = world.OnSurface(world.Spawn, 0.05f);
            player = Phase1SceneBuilder.BuildPlayer(spawn, "Player_Cloudpeak");
            Phase1SceneBuilder.BuildCamera(player);
            Camera cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.farClipPlane = world.Settings.HalfSize * 4.2f;
                cam.transform.position = spawn + new Vector3(0f, 3f, -5.5f);
            }
            WeatherTools.WeatherSetup.AddToScene();
            WindSetup.AddToScene();
            registry = new GameObject("CollectibleRegistry").AddComponent<CollectibleRegistry>();

            Phase1SceneBuilder.EnsureFolder(Phase1SceneBuilder.QuestFolder);
            QuestDefinition feathers = Phase1SceneBuilder.QuestAsset("Quest_CloudFeathers", q => q.Configure("cloud_feathers", "Feathers of Cloudpeak",
                "Cloud feathers drift down the mountain. Gather them on the way to the summit.", null,
                new QuestStep("Gather the cloud feathers along the Cloudpeak Trail", ObjectiveType.Collect, "cloud_feather", FeatherCount)));
            var quests = new GameObject("QuestManager").AddComponent<QuestManager>();
            var so = new SerializedObject(quests);
            SerializedProperty list = so.FindProperty("quests");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = feathers;
            so.FindProperty("registry").objectReferenceValue = registry;
            so.ApplyModifiedPropertiesWithoutUndo();
            QuestAutoStart starter = quests.gameObject.AddComponent<QuestAutoStart>();
            Phase1SceneBuilder.SetRef(starter, "manager", quests);
            Phase1SceneBuilder.SetRef(starter, "quest", feathers);
            reserved.Add((world.Spawn, 3f));
            return Phase1SceneBuilder.BuildHud(player, quests);
        }

        // ───────────────────────────── Points of interest ─────────────────────────────

        const int FeatherCount = 5;

        /// <summary>A point beside trail sample <paramref name="index"/>, <paramref name="offset"/> metres beyond the edge, and the yaw facing back to the trail.</summary>
        static (Vector2 p, float yawToTrail) BesideTrail(int index, float side, float offset)
        {
            List<TrailPoint> t = world.Trail;
            index = Mathf.Clamp(index, 1, t.Count - 2);
            Vector2 d = (t[index + 1].Position - t[index - 1].Position).normalized;
            Vector2 normal = new Vector2(-d.y, d.x) * side;
            Vector2 p = t[index].Position + normal * (t[index].HalfWidth + offset);
            return (p, Mathf.Atan2(-normal.x, -normal.y) * Mathf.Rad2Deg);
        }

        static int TrailIndexNear(Vector2 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < world.Trail.Count; i++)
            {
                float d = (world.Trail[i].Position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        static int TrailIndexAtDistance(float distance)
        {
            for (int i = 0; i < world.Trail.Count; i++)
                if (world.Trail[i].Distance >= distance) return i;
            return world.Trail.Count - 1;
        }

        static void Sign(Transform parent, DialogueView dialogue, int trailIndex, float side, params string[] lines)
        {
            (Vector2 p, float yaw) = BesideTrail(trailIndex, side, 0.9f);
            GameObject sign = Place(Prefab(AssetCategory.Signpost, trailIndex % 2), parent, world.OnSurface(p), yaw);
            if (sign == null) return;
            sign.name = "Signpost";
            AmbientMotionSetup.Readable(sign, dialogue, lines);
            reserved.Add((p, 1.6f));
        }

        static void Feather(Transform parent, CollectibleRegistry registry, int number, Vector2 p, float lift = 0f)
        {
            string meshName = "Gen_CloudFeather";
            string path = $"{AssetFolder}/{meshName}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path) ?? GeneratedAssetStore.SaveMesh(CloudpeakMeshes.Feather(meshName), path);
            Phase1SceneBuilder.CollectibleProp(parent, registry, $"cloud_feather_{number:00}", "cloud_feather", "Cloud Feather",
                world.OnSurface(p, lift), 0.7f, visual =>
                {
                    var go = new GameObject("Feather");
                    go.transform.SetParent(visual, false);
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                    go.transform.localScale = Vector3.one * 1.6f;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = village;
                });
            reserved.Add((p, 1.2f));
        }

        static void BuildPointsOfInterest(Transform parent, DialogueView dialogue, CollectibleRegistry registry)
        {
            List<TrailPoint> trail = world.Trail;
            float length = trail[trail.Count - 1].Distance;
            Transform feathers = Group("Cloud Feathers", parent);

            // Entrance from the village: gate, sign.
            Transform entrance = Group("Forest Entrance", parent);
            int gateIndex = TrailIndexAtDistance(2.5f);
            Vector2 gateAt = trail[gateIndex].Position;
            Vector2 gateDir = trail[gateIndex + 2].Position - trail[gateIndex].Position;
            GameObject gate = Place(Prefab(AssetCategory.Gate, 0), entrance, world.OnSurface(gateAt), Mathf.Atan2(gateDir.x, gateDir.y) * Mathf.Rad2Deg);
            if (gate != null) gate.name = "Forest Gate (from Greenhaven Village)";
            reserved.Add((gateAt, 3f));
            Sign(entrance, dialogue, TrailIndexAtDistance(7f), 1f,
                "Cloudpeak Trail — through the Whispering Forest to the summit.",
                $"The summit is about {Mathf.RoundToInt(length / 10f) * 10} m along the trail. Greenhaven Village lies back through the gate.");

            // 1. Whispering Glade (forest clearing).
            Transform glade = Group("Whispering Glade", parent);
            Vector2 g = world.Glade;
            int gi = TrailIndexNear(g);
            Vector2 gd = (trail[Mathf.Min(gi + 2, trail.Count - 1)].Position - trail[Mathf.Max(gi - 2, 0)].Position).normalized;
            var gSide = new Vector2(-gd.y, gd.x);
            Vector2 toPeak = (world.Mountain - g).normalized;
            // Benches and stump on the side away from the summit view, so the clearing frames Cloudpeak.
            float viewSide = Vector2.Dot(gSide, toPeak) > 0f ? -1f : 1f;
            Vector2 benchAt = g + gSide * viewSide * 4.2f;
            Place(Prefab(AssetCategory.Bench, 0), glade, world.OnSurface(benchAt), Mathf.Atan2(toPeak.x, toPeak.y) * Mathf.Rad2Deg);
            reserved.Add((benchAt, 1.8f));
            Vector2 stumpAt = g + gSide * viewSide * 4.6f + gd * 3f;
            Place(Prefab(AssetCategory.Stump, 0), glade, world.OnSurface(stumpAt, -0.05f), Rand(0f, 360f));
            reserved.Add((stumpAt, 1f));
            Vector2 logAt = g + gSide * viewSide * 4.4f - gd * 3.4f;
            Place(Prefab(AssetCategory.Log, 0), glade, world.OnSurface(logAt, -0.08f), Mathf.Atan2(gd.x, gd.y) * Mathf.Rad2Deg + 90f);
            reserved.Add((logAt, 2f));
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2f / 18f + Rand(-0.1f, 0.1f);
                Vector2 p = g + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rand(6.2f, 7.6f);
                if (world.DistanceToTrailEdge(p) < 0.4f) continue;
                AssetCategory type = i % 3 == 0 ? AssetCategory.Mushroom : AssetCategory.Flower;
                Place(Prefab(type, rng.Next(Generators.For(type).DefaultCount)), glade, world.OnSurface(p), Rand(0f, 360f), Rand(0.9f, 1.2f), cullBelow: 0.03f);
            }
            Sign(glade, dialogue, gi - 6, -viewSide, "Whispering Glade — listen to the leaves.",
                "Look north between the trees: the peak with the red banner is Cloudpeak, the end of the trail.");
            Feather(feathers, registry, 1, stumpAt + gd * 1.2f);

            // Bridge sign and a feather by a fallen log after the bridge.
            int bi = TrailIndexNear(world.BridgeCentre);
            Sign(parent, dialogue, bi - 10, 1f, "Whispering Brook — cross by the bridge. The water is cold!");
            (Vector2 afterLog, float afterYaw) = BesideTrail(bi + 14, -1f, 1.6f);
            Place(Prefab(AssetCategory.Log, 1), parent, world.OnSurface(afterLog, -0.08f), afterYaw + 90f);
            reserved.Add((afterLog, 1.8f));
            Feather(feathers, registry, 2, afterLog + (afterLog - trail[bi + 14].Position).normalized * 1.4f);

            // 2. Foothill Rest (rest stop at the start of the climb).
            Transform rest = Group("Foothill Rest", parent);
            Platform restPl = world.Platforms.First(p => p.Name == "Foothill Rest");
            Vector2 restOut = (restPl.Centre - world.Mountain).normalized;
            float faceMountain = Mathf.Atan2(-restOut.x, -restOut.y) * Mathf.Rad2Deg;
            Vector2 restBench = restPl.Centre + restOut * 2.2f;
            Place(Prefab(AssetCategory.Bench, 1), rest, world.OnSurface(restBench), faceMountain);
            Vector2 lanternAt = restPl.Centre + new Vector2(-restOut.y, restOut.x) * 3.3f;
            Place(Prefab(AssetCategory.Lantern, 0), rest, world.OnSurface(lanternAt), faceMountain);
            reserved.Add((restPl.Centre, restPl.Radius + 0.5f));
            Sign(rest, dialogue, TrailIndexNear(world.FoothillRest) + 4, -1f, "Foothill Rest — the climb starts here.",
                "The switchbacks keep the path gentle. Above them, the Eagle Ledge looks out over the whole forest.");
            Feather(feathers, registry, 3, restPl.Centre - new Vector2(-restOut.y, restOut.x) * 3f, 0.05f);

            // Switchback marker at the first hairpin.
            if (world.Hairpins.Count > 0)
            {
                int hi = TrailIndexNear(world.Hairpins[0]);
                Sign(parent, dialogue, hi - 14, 1f, "Switchback ahead — the path turns back on itself and keeps climbing.");
            }

            // 3. Eagle Ledge (viewpoint on the shelf).
            Transform ledge = Group("Eagle Ledge", parent);
            Platform ledgePl = world.Platforms.First(p => p.Name == "Eagle Ledge");
            Vector2 outward = world.LedgeOutward;
            Place(Prefab(AssetCategory.Bench, 0), ledge, world.OnSurface(ledgePl.Centre + outward * 0.6f), Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg);
            WallArc(ledge, ledgePl.Centre, ledgePl.Radius - 0.3f, Mathf.Atan2(outward.y, outward.x), 1.25f, 5);
            reserved.Add((ledgePl.Centre, ledgePl.Radius + 0.5f));
            Sign(ledge, dialogue, TrailIndexNear(world.Ledge) - 8, -1f, "Eagle Ledge — the Whispering Forest lies below.",
                "From here the trail circles the summit cone on a ledge cut into the rock. Keep to the path.");
            Feather(feathers, registry, 4, ledgePl.Centre - new Vector2(-outward.y, outward.x) * 2.6f);

            // 4. Summit: ruined arch on the final approach, shrine, cairn with a swinging banner, overlook wall.
            BuildSummit(Group("Cloudpeak Summit", parent), dialogue, feathers, registry);
        }

        static void BuildSummit(Transform parent, DialogueView dialogue, Transform feathers, CollectibleRegistry registry)
        {
            int seed = world.Settings.Seed;
            Vector2 c = world.Mountain;
            Vector2 approach = world.SummitApproach;
            var across = new Vector2(-approach.y, approach.x);
            float faceApproach = Mathf.Atan2(approach.x, approach.y) * Mathf.Rad2Deg;
            Platform summit = world.Platforms.First(p => p.Name == "Cloudpeak Summit");

            // Ruined arch spanning the final approach.
            Vector2 archAt = c - approach * 3f;
            Mesh arch = SaveOnce($"Gen_CloudpeakArch_s{seed}", () => CloudpeakMeshes.Arch(seed, $"Gen_CloudpeakArch_s{seed}", out _));
            Object.DestroyImmediate(CloudpeakMeshes.Arch(seed, "tmp", out Bounds[] archBoxes));
            Solid("Ruined Arch", parent, arch, world.OnSurface(archAt, -0.05f), faceApproach, archBoxes);
            reserved.Add((archAt, 3.2f));

            // Shrine beyond the trail's end.
            Vector2 shrineAt = c + approach * 2.6f;
            Mesh shrine = SaveOnce($"Gen_CloudpeakShrine_s{seed}", () => CloudpeakMeshes.Shrine(seed, $"Gen_CloudpeakShrine_s{seed}", out _));
            Object.DestroyImmediate(CloudpeakMeshes.Shrine(seed, "tmp", out Bounds shrineBox));
            GameObject shrineGo = Solid("Summit Shrine", parent, shrine, world.OnSurface(shrineAt, -0.05f), faceApproach + 180f, new[] { shrineBox });
            AmbientMotionSetup.Readable(shrineGo, dialogue, "Cloudpeak Summit — you made it!",
                "The whole Whispering Forest lies below, and Greenhaven Village beyond the trees. The wind always blows up here.");
            reserved.Add((shrineAt, 1.8f));

            // Cairn with a tall flagpole and a banner that swings in the wind (visible from below).
            Vector2 cairnAt = c + across * 3f + approach * 0.5f;
            Mesh cairn = SaveOnce($"Gen_CloudpeakCairn_s{seed}", () => CloudpeakMeshes.Cairn(seed, $"Gen_CloudpeakCairn_s{seed}", out _));
            Object.DestroyImmediate(CloudpeakMeshes.Cairn(seed, "tmp", out float poleTop));
            GameObject cairnGo = Solid("Summit Cairn", parent, cairn, world.OnSurface(cairnAt, -0.05f), Rand(0f, 360f),
                new[] { new Bounds(new Vector3(0f, 0.7f, 0f), new Vector3(1.2f, 1.4f, 1.1f)) });
            Mesh banner = SaveOnce("Gen_CloudpeakBanner", () => CloudpeakMeshes.Banner("Gen_CloudpeakBanner"));
            var bannerGo = new GameObject("Banner (swings)");
            bannerGo.transform.SetParent(cairnGo.transform, false);
            bannerGo.transform.localPosition = new Vector3(0.04f, poleTop - 0.15f, 0f); // hangs from a crossbar and swings about it (local X)
            bannerGo.AddComponent<MeshFilter>().sharedMesh = banner;
            bannerGo.AddComponent<MeshRenderer>().sharedMaterial = village;
            bannerGo.AddComponent<WindSwing>();
            summitMarker = cairnGo.transform.position + Vector3.up * poleTop;
            reserved.Add((cairnAt, 1.4f));

            // Overlook wall along the edge facing the forest; gaps for the arrival and the view.
            Vector2 toForest = (world.Entrance - c).normalized;
            float forestAngle = Mathf.Atan2(toForest.y, toForest.x);
            float arrivalAngle = Mathf.Atan2(-approach.y, -approach.x);
            WallArc(parent, c, summit.Radius - 0.4f, forestAngle, 1.3f, 6, arrivalAngle);
            Place(Prefab(AssetCategory.Bench, 1), parent, world.OnSurface(c + toForest * (summit.Radius - 2.2f)), Mathf.Atan2(toForest.x, toForest.y) * Mathf.Rad2Deg);

            // Boulders around the rest of the rim (but not across the arrival).
            for (int i = 0; i < 9; i++)
            {
                float a = forestAngle + Mathf.PI * 0.55f + i * (Mathf.PI * 0.9f / 8f);
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, arrivalAngle * Mathf.Rad2Deg)) < 28f) continue;
                Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (summit.Radius + Rand(0.2f, 1.2f));
                Place(Prefab(AssetCategory.Rock, rng.Next(6)), parent, world.OnSurface(p, -0.25f), Rand(0f, 360f), Rand(0.8f, 1.4f));
            }
            Feather(feathers, registry, 5, shrineAt + across * 1.8f);
        }

        /// <summary>Low wall segments along an arc (radius around <paramref name="centre"/>), skipping any near <paramref name="gapAngle"/>.</summary>
        static void WallArc(Transform parent, Vector2 centre, float radius, float midAngle, float halfSpan, int segments, float? gapAngle = null)
        {
            int seed = world.Settings.Seed;
            float segAngle = halfSpan * 2f / segments;
            float length = 2f * radius * Mathf.Sin(segAngle * 0.5f) * 0.92f;
            string name = $"Gen_CloudpeakWall_{Mathf.RoundToInt(length * 10f)}_s{seed}";
            Mesh mesh = SaveOnce(name, () => CloudpeakMeshes.Wall(seed, name, length, out _));
            Object.DestroyImmediate(CloudpeakMeshes.Wall(seed, "tmp", length, out Bounds box));
            for (int i = 0; i < segments; i++)
            {
                float a = midAngle - halfSpan + segAngle * (i + 0.5f);
                if (gapAngle.HasValue && Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, gapAngle.Value * Mathf.Rad2Deg)) < 30f) continue;
                if (i == segments / 2 && segments > 4 && !gapAngle.HasValue) continue; // a gap to step up to the view
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 p = centre + dir * radius;
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; // local Z = outward, wall along local X
                Solid("Overlook Wall", parent, mesh, world.OnSurface(p, -0.1f), yaw, new[] { box });
            }
        }

        // ───────────────────────────── Mountain details ─────────────────────────────

        static void BuildMountainDetails(Transform parent)
        {
            float density = Mathf.Max(0.2f, world.Settings.VegetationDensity);
            Transform cliffs = Group("Cliff Rocks", parent), boulders = Group("Trail Boulders", parent);
            var placed = new List<Vector2>();
            int cliffTarget = Mathf.RoundToInt(42 * density), cliffCount = 0;

            // Large rocks where the ground is steep: exposed faces and ridge crests, never on the trail.
            for (int attempt = 0; attempt < cliffTarget * 80 && cliffCount < cliffTarget; attempt++)
            {
                Vector2 p = world.Mountain + Random2(world.Settings.MountainRadius * 1.05f);
                if (!Inside(p, 4f)) continue;
                float slope = Vector3.Angle(world.SurfaceNormal(p, 1f), Vector3.up);
                if (slope < 34f || world.DistanceToTrailEdge(p) < 3.5f || IsReserved(p, 3f) || !Spaced(placed, p, 6f)) continue;
                Place(Prefab(AssetCategory.Rock, rng.Next(6)), cliffs, world.OnSurface(p, -0.4f), Rand(0f, 360f), Rand(1.6f, 3.2f));
                placed.Add(p);
                cliffCount++;
            }

            // Boulders beside the climb (and at hairpins) so the route reads clearly; the trail stays open.
            int boulderCount = 0;
            for (int i = 30; i < world.Trail.Count - 30; i += 9)
            {
                TrailPoint tp = world.Trail[i];
                if (tp.Section == TrailSection.Forest || rng.NextDouble() > 0.35 * density) continue;
                (Vector2 p, _) = BesideTrail(i, rng.NextDouble() < 0.5 ? -1f : 1f, Rand(1.0f, 2.6f));
                if (world.DistanceToTrailEdge(p) < 0.8f || IsReserved(p, 1.5f)) continue;
                Place(Prefab(AssetCategory.Rock, rng.Next(6)), boulders, world.OnSurface(p, -0.15f), Rand(0f, 360f), Rand(0.6f, 1.2f), cullBelow: 0.01f);
                boulderCount++;
            }
            foreach (Vector2 h in world.Hairpins)
                if (!IsReserved(h, 2f) && world.DistanceToTrailEdge(h) > 1.2f)
                    Place(Prefab(AssetCategory.Rock, rng.Next(6)), boulders, world.OnSurface(h, -0.3f), Rand(0f, 360f), Rand(1.2f, 1.8f));
            log.AppendLine($"Mountain: {cliffCount} cliff rocks, {boulderCount} trail boulders, {world.Hairpins.Count} hairpin boulder(s).");
        }

        // ───────────────────────────── Vegetation ─────────────────────────────

        static void BuildVegetation(Transform parent)
        {
            float d = world.Settings.VegetationDensity;
            Transform trees = Group("Trees", parent), small = Group("Undergrowth", parent), grass = Group("Grass and Flowers", parent);
            Transform floor = Group("Logs, Stumps and Mushrooms", parent);
            var treeSpots = new List<Vector2>();
            float H = world.Settings.HalfSize;
            Vector2 Anywhere() => new Vector2(Rand(-H, H), Rand(-H, H));

            // Density by region: dense forest, sparse foothills, open mountain.
            float ForestWeight(Vector2 p)
            {
                float t = Vector2.Distance(p, world.Mountain) / world.Settings.MountainRadius;
                return t > 1.05f ? 1f : t > 0.8f ? 0.35f : 0f;
            }
            float MountainPineWeight(Vector2 p)
            {
                float t = Vector2.Distance(p, world.Mountain) / world.Settings.MountainRadius;
                if (t > 0.85f || t < 0.15f) return 0f;
                return Vector3.Angle(world.SurfaceNormal(p, 1f), Vector3.up) < 28f ? 0.6f : 0f;
            }
            // No tall trees in the view corridors from the glade and the rest stop toward the summit.
            bool InView(Vector2 p)
            {
                foreach (Vector2 from in new[] { world.Glade, world.FoothillRest })
                {
                    Vector2 to = world.Mountain - from, rel = p - from;
                    float along = Vector2.Dot(rel, to.normalized);
                    if (along < 0f || along > to.magnitude * 0.75f) continue;
                    float off = Mathf.Abs(Vector2.Dot(rel, new Vector2(-to.y, to.x).normalized));
                    if (off < 4f + along * 0.12f) return true;
                }
                return false;
            }

            int Scatter(AssetCategory type, int count, Transform group, System.Func<Vector2> sample, System.Func<Vector2, float> weight,
                float trailClearance, float spacing, List<Vector2> spacingSet, float minScale, float maxScale, float sink, float cull = 0f, bool tree = false)
            {
                int variants = Generators.For(type).DefaultCount, placedCount = 0;
                for (int attempt = 0; attempt < count * 50 && placedCount < count; attempt++)
                {
                    Vector2 p = sample();
                    if (!Inside(p, 5f) || rng.NextDouble() > weight(p)) continue;
                    if (world.DistanceToTrailEdge(p, trailClearance + 1f) < trailClearance || IsReserved(p, trailClearance * 0.6f)) continue;
                    if (OnPlatform(p, tree ? 2.5f : 0.3f) || InBrook(p)) continue;
                    if (tree && InView(p)) continue;
                    if (spacingSet != null && !Spaced(spacingSet, p, spacing)) continue;
                    if (Vector3.Angle(world.SurfaceNormal(p, 0.8f), Vector3.up) > (tree ? 33f : 40f)) continue;
                    if (Place(Prefab(type, rng.Next(variants)), group, world.OnSurface(p, -sink), Rand(0f, 360f), Rand(minScale, maxScale), cull) == null) break;
                    spacingSet?.Add(p);
                    placedCount++;
                }
                return placedCount;
            }

            var counts = new StringBuilder();
            int broad = Scatter(AssetCategory.BroadleafTree, Mathf.RoundToInt(70 * d), trees, Anywhere, ForestWeight, 3f, 4.6f, treeSpots, 0.85f, 1.25f, 0.05f, tree: true);
            int pine = Scatter(AssetCategory.PineTree, Mathf.RoundToInt(65 * d), trees, Anywhere, p => Mathf.Max(ForestWeight(p), 0.5f * (1f - Mathf.Abs(Vector2.Distance(p, world.Mountain) / world.Settings.MountainRadius - 0.95f) * 4f)),
                2.8f, 4.2f, treeSpots, 0.9f, 1.35f, 0.05f, tree: true);
            int alpine = Scatter(AssetCategory.PineTree, Mathf.RoundToInt(40 * d), trees, () => world.Mountain + Random2(world.Settings.MountainRadius), MountainPineWeight,
                2.5f, 3.6f, treeSpots, 0.5f, 0.8f, 0.05f, tree: true);
            int bushes = Scatter(AssetCategory.Bush, Mathf.RoundToInt(90 * d), small, Anywhere, p => 0.25f + 0.75f * ForestWeight(p), 1.0f, 1.8f, treeSpots, 0.8f, 1.2f, 0f, 0.015f);
            int grassCount = Scatter(AssetCategory.Grass, Mathf.RoundToInt(320 * d), grass, Anywhere, p => Vector2.Distance(p, world.Mountain) < world.Settings.MountainRadius * 0.2f ? 0.3f : 0.9f,
                0.3f, 0f, null, 0.8f, 1.25f, 0f, 0.03f);
            int flowers = Scatter(AssetCategory.Flower, Mathf.RoundToInt(100 * d), grass, Anywhere, p => ForestWeight(p) > 0.5f ? 0.5f : 1f, 0.3f, 0f, null, 0.9f, 1.2f, 0f, 0.03f);

            // Grass and flowers framing the trail.
            int edge = 0, edgeTarget = Mathf.RoundToInt(240 * d);
            for (int attempt = 0; attempt < edgeTarget * 20 && edge < edgeTarget; attempt++)
            {
                int i = rng.Next(world.Trail.Count);
                (Vector2 p, _) = BesideTrail(i, rng.NextDouble() < 0.5 ? -1f : 1f, Rand(0.3f, 2.2f));
                if (!Inside(p, 5f) || world.DistanceToTrailEdge(p) < 0.25f || IsReserved(p, 0.6f) || InBrook(p) || OnPlatform(p, 0f)) continue;
                AssetCategory type = rng.NextDouble() < 0.22 ? AssetCategory.Flower : AssetCategory.Grass;
                Place(Prefab(type, rng.Next(Generators.For(type).DefaultCount)), grass, world.OnSurface(p), Rand(0f, 360f), Rand(0.8f, 1.2f), 0.03f);
                edge++;
            }

            // Forest floor: logs and stumps among the trees, mushrooms beside trunks and logs.
            int logs = Scatter(AssetCategory.Log, Mathf.RoundToInt(14 * d), floor, Anywhere, ForestWeight, 2f, 3f, treeSpots, 0.9f, 1.15f, 0.08f, 0.012f);
            int stumps = Scatter(AssetCategory.Stump, Mathf.RoundToInt(12 * d), floor, Anywhere, ForestWeight, 1.5f, 2.5f, treeSpots, 0.9f, 1.2f, 0.05f, 0.012f);
            int mushrooms = 0, mushroomTarget = Mathf.RoundToInt(45 * d);
            for (int attempt = 0; attempt < mushroomTarget * 20 && mushrooms < mushroomTarget && treeSpots.Count > 0; attempt++)
            {
                Vector2 near = treeSpots[rng.Next(treeSpots.Count)];
                if (ForestWeight(near) < 0.5f) continue;
                float a = Rand(0f, Mathf.PI * 2f);
                Vector2 p = near + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rand(0.7f, 1.6f);
                if (world.DistanceToTrailEdge(p) < 0.4f || IsReserved(p, 0.5f) || InBrook(p)) continue;
                Place(Prefab(AssetCategory.Mushroom, rng.Next(3)), floor, world.OnSurface(p), Rand(0f, 360f), Rand(0.8f, 1.3f), 0.04f);
                mushrooms++;
            }
            log.AppendLine($"Vegetation: {broad} broadleaf, {pine} forest pines, {alpine} mountain pines, {bushes} bushes, {grassCount + edge} grass/flower clumps " +
                           $"({edge} along the trail), {flowers} flowers, {logs} logs, {stumps} stumps, {mushrooms} mushroom clusters.");
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static Vector2 Random2(float radius)
        {
            float a = Rand(0f, Mathf.PI * 2f), r = radius * Mathf.Sqrt((float)rng.NextDouble());
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        static bool Inside(Vector2 p, float margin) => Mathf.Abs(p.x) < world.Settings.HalfSize - margin && Mathf.Abs(p.y) < world.Settings.HalfSize - margin;

        static bool Spaced(List<Vector2> set, Vector2 p, float min)
        {
            float m2 = min * min;
            foreach (Vector2 q in set)
                if ((q - p).sqrMagnitude < m2) return false;
            return true;
        }

        static bool IsReserved(Vector2 p, float margin)
        {
            foreach ((Vector2 c, float r) in reserved)
                if ((p - c).magnitude < r + margin) return true;
            return false;
        }

        static bool OnPlatform(Vector2 p, float margin)
        {
            foreach (Platform pl in world.Platforms)
                if (Vector2.Distance(p, pl.Centre) < pl.Radius + margin) return true;
            return false;
        }

        static bool InBrook(Vector2 p) => Mathf.Abs(p.y - world.BrookZ(p.x)) < 3.2f && Vector2.Distance(p, world.Mountain) > world.Settings.MountainRadius * 0.8f;

        static GameObject Prefab(AssetCategory category, int variant)
        {
            IAssetGenerator gen = Generators.For(category);
            string path = GeneratedAssetStore.PrefabPath(GeneratedAssetStore.AssetName(gen, assets.Seed, variant));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) missing.Add(Path.GetFileNameWithoutExtension(path));
            return prefab;
        }

        /// <summary>Instantiates a prefab. <paramref name="cullBelow"/> &gt; 0 adds an LOD group that culls it below that screen height.</summary>
        static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, float scale = 1f, float cullBelow = 0f)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            if (cullBelow > 0f)
            {
                LODGroup lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(cullBelow, go.GetComponentsInChildren<Renderer>()) });
                lod.RecalculateBounds();
            }
            return go;
        }

        static Mesh SaveOnce(string name, System.Func<Mesh> build) =>
            GeneratedAssetStore.SaveMesh(build(), $"{AssetFolder}/{name}.asset");

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        static GameObject Solid(string name, Transform parent, Mesh mesh, Vector3 position, float yaw, Bounds[] boxes)
        {
            GameObject go = MeshObject(name, parent, mesh, village);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            foreach (Bounds b in boxes)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
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
