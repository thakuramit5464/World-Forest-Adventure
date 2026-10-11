using System.Collections.Generic;
using System.IO;
using Greenhaven.CameraRig;
using Greenhaven.Collectibles;
using Greenhaven.Core;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Interaction;
using Greenhaven.Player;
using Greenhaven.Quests;
using Greenhaven.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Greenhaven.Editor
{
    /// <summary>
    /// Builds the Phase 1 playable slice of Greenhaven Village from placeholder
    /// primitives. Re-runnable: Greenhaven ▸ Build Phase 1 Scene.
    /// Layout is hand-authored here (fixed seed for scatter), not generated at runtime.
    /// </summary>
    public static class Phase1SceneBuilder
    {
        const string GameRoot = "Assets/_Game";
        const string ScenePath = GameRoot + "/Scenes/GreenhavenRegion.unity";
        const string MaterialFolder = GameRoot + "/Materials";
        const string PrefabFolder = GameRoot + "/Prefabs";
        const int IgnoreRaycastLayer = 2; // built-in; keeps the player out of camera/interaction casts

        static readonly Vector3 SpawnPoint = new Vector3(0f, 0.05f, -12f);
        static readonly Color SkyColor = new Color(0.72f, 0.85f, 0.96f);

        // Path waypoints (x, z): spawn → plaza → winding north to the forest gate.
        static readonly Vector2[] PathPoints =
        {
            new Vector2(0f, -16f), new Vector2(0f, -3f), new Vector2(0f, 11f),
            new Vector2(2.5f, 20f), new Vector2(5.5f, 28f), new Vector2(4f, 36f), new Vector2(4f, 46f),
        };

        static Dictionary<string, Material> materials;

        [MenuItem("Greenhaven/Build Original Region Scene (Phase 1)", priority = 100)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play mode first", "The Greenhaven scene can only be rebuilt outside Play mode.", "OK");
                return;
            }
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild Greenhaven scene?",
                    $"{ScenePath} already exists and will be replaced. Manual edits to that scene will be lost.",
                    "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(GameRoot + "/Scenes");
            CreateMaterials();
            GreenhavenArt.PrepareImports();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            Transform env = new GameObject("Environment").transform;
            BuildGround(env);
            BuildPath(env);
            BuildVillage(env);
            BuildLandmarks(env);
            BuildJumpCourse(env);
            if (GreenhavenArt.NatureAvailable)
            {
                GreenhavenArt.BuildNature(env, IsClearForScatter, PathDistance);
                // Stepping stones from the plaza toward Lookout Hill.
                GreenhavenArt.SteppingStones(env, new Vector3(8f, 0f, 9f), new Vector3(12.5f, 0f, 18f), 8, 7);
            }
            else
            {
                BuildScatter(env);
            }
            BuildFarm(env);
            BuildBackdrop(env);
            BuildBoundaries(env);

            GameObject player = BuildPlayer(SpawnPoint);
            BuildCamera(player);
            WeatherTools.WeatherSetup.AddToScene();
            WindTools.WindSetup.AddToScene();
            CollectibleRegistry registry = new GameObject("CollectibleRegistry").AddComponent<CollectibleRegistry>();
            QuestManager quests = BuildQuests(registry, out QuestDefinition supplies, out QuestDefinition request);
            DialogueView dialogue = BuildHud(player, quests);
            BuildNpcs(dialogue, quests, supplies, request);
            BuildSign(dialogue);
            BuildSupplies(registry);
            BuildSpectacles(registry);

            int badRefs = ValidateSceneReferences(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            if (badRefs == 0 && GreenhavenArt.MissingModels == 0) Debug.Log($"Greenhaven: built {ScenePath}. Press Play to explore.");
            else Debug.LogError($"Greenhaven: built {ScenePath} with {badRefs} bad reference(s) and {GreenhavenArt.MissingModels} missing model placement(s); see messages above.");
        }

        // ───────────────────────────── Materials ─────────────────────────────

        internal static void CreateMaterials()
        {
            materials = new Dictionary<string, Material>();
            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("Greenhaven: URP shaders not found. Is the Universal Render Pipeline active?");
                shader = Shader.Find("Standard");
            }

            void Add(string key, Color color)
            {
                string path = $"{MaterialFolder}/M_{key}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.SetColor("_BaseColor", color);
                mat.color = color;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
                if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
                mat.DisableKeyword("_SPECULAR_COLOR");
                EditorUtility.SetDirty(mat);
                materials[key] = mat;
            }

            Add("Grass", new Color(0.49f, 0.74f, 0.36f));
            Add("GrassDark", new Color(0.36f, 0.6f, 0.3f));
            Add("Path", new Color(0.86f, 0.72f, 0.5f));
            Add("Plaza", new Color(0.8f, 0.7f, 0.56f));
            Add("Stone", new Color(0.62f, 0.62f, 0.66f));
            Add("StoneWarm", new Color(0.72f, 0.66f, 0.58f));
            Add("Wall", new Color(0.97f, 0.91f, 0.78f));
            Add("Timber", new Color(0.5f, 0.33f, 0.2f));
            Add("RoofRed", new Color(0.84f, 0.36f, 0.27f));
            Add("RoofBlue", new Color(0.33f, 0.5f, 0.78f));
            Add("RoofOrange", new Color(0.93f, 0.6f, 0.26f));
            Add("Trunk", new Color(0.45f, 0.3f, 0.2f));
            Add("Leaf1", new Color(0.3f, 0.66f, 0.32f));
            Add("Leaf2", new Color(0.44f, 0.76f, 0.3f));
            Add("Leaf3", new Color(0.2f, 0.52f, 0.36f));
            Add("Water", new Color(0.3f, 0.6f, 0.85f));
            Add("FlowerPink", new Color(0.96f, 0.55f, 0.7f));
            Add("FlowerYellow", new Color(1f, 0.84f, 0.3f));
            Add("HillFar", new Color(0.5f, 0.66f, 0.62f));
            Add("Mountain", new Color(0.56f, 0.62f, 0.76f));
            Add("Snow", new Color(0.95f, 0.97f, 1f));
            Add("Skin", new Color(0.96f, 0.78f, 0.62f));
            Add("Tunic", new Color(0.2f, 0.55f, 0.6f));
            Add("Backpack", new Color(0.72f, 0.45f, 0.22f));
            Add("Dark", new Color(0.15f, 0.12f, 0.12f));
            Add("NpcDress", new Color(0.85f, 0.45f, 0.55f));
            Add("Apron", new Color(0.98f, 0.96f, 0.9f));
            Add("Flag", new Color(0.95f, 0.3f, 0.3f));
            AssetDatabase.SaveAssets();
        }

        // ───────────────────────────── Lighting ─────────────────────────────

        internal static void BuildLighting()
        {
            var sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            // Cheap storybook ambience: tri-colour ambient + linear fog, no skybox.
            RenderSettings.skybox = null;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.78f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.74f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.34f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 170f;
        }

        // ───────────────────────────── Environment ─────────────────────────────

        static void BuildGround(Transform env)
        {
            GameObject ground = Prim(PrimitiveType.Plane, "Ground", env, new Vector3(0f, 0f, 10f), new Vector3(15f, 1f, 15f), "Grass");
            ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            GameObject plaza = Prim(PrimitiveType.Cylinder, "Plaza", env, new Vector3(0f, 0f, 4f), new Vector3(17f, 0.02f, 17f), "Plaza", collider: false);
            plaza.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildPath(Transform env)
        {
            Transform path = Group("Path", env);
            for (int i = 0; i < PathPoints.Length - 1; i++)
            {
                Vector3 a = new Vector3(PathPoints[i].x, 0.015f, PathPoints[i].y);
                Vector3 b = new Vector3(PathPoints[i + 1].x, 0.015f, PathPoints[i + 1].y);
                Vector3 mid = (a + b) * 0.5f;
                float length = Vector3.Distance(a, b);
                GameObject seg = Prim(PrimitiveType.Cube, $"PathSegment{i}", path, mid, new Vector3(2.6f, 0.02f, length), "Path",
                    collider: false, rotation: Quaternion.LookRotation(b - a));
                seg.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

                // Round joints so bends look like a trodden trail rather than boxes.
                GameObject joint = Prim(PrimitiveType.Cylinder, $"PathJoint{i}", path, b + Vector3.up * 0.002f, new Vector3(2.6f, 0.01f, 2.6f), "Path", collider: false);
                joint.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void BuildVillage(Transform env)
        {
            Transform village = Group("Greenhaven Village", env);
            House(village, "Bakery", new Vector3(-11f, 0f, 2f), 90f, "RoofRed");
            House(village, "Cottage", new Vector3(11.5f, 0f, 6f), -90f, "RoofBlue");
            House(village, "Elder's House", new Vector3(-8.5f, 0f, 15f), 135f, "RoofOrange");

            // Flower beds by the doors.
            if (GreenhavenArt.NatureAvailable)
            {
                GreenhavenArt.FlowerBed(village, new Vector3(-8f, 0f, 0f), 1);
                GreenhavenArt.FlowerBed(village, new Vector3(8.5f, 0f, 8f), 2);
                GreenhavenArt.FlowerBed(village, new Vector3(-5.5f, 0f, 13f), 3);
            }
            else
            {
                Flowers(village, new Vector3(-8f, 0f, 0f), 5, 1);
                Flowers(village, new Vector3(8.5f, 0f, 8f), 5, 2);
                Flowers(village, new Vector3(-5.5f, 0f, 12.5f), 4, 3);
            }

            // Fence along the south edge of the plaza.
            for (int i = 0; i < 6; i++)
            {
                float x = -10f + i * 1.6f;
                Prim(PrimitiveType.Cube, "FencePost", village, new Vector3(x, 0.45f, -6f), new Vector3(0.15f, 0.9f, 0.15f), "Timber");
            }
            Prim(PrimitiveType.Cube, "FenceRail", village, new Vector3(-6f, 0.65f, -6f), new Vector3(8.2f, 0.1f, 0.08f), "Timber", collider: false);
        }

        static void House(Transform parent, string name, Vector3 position, float yaw, string roof)
        {
            Transform house = Group(name, parent);
            house.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            LocalPrim(PrimitiveType.Cube, "Walls", house, new Vector3(0f, 1.6f, 0f), new Vector3(5f, 3.2f, 4.2f), "Wall");
            // A cube rotated 45° on its long axis reads as a gable roof.
            LocalPrim(PrimitiveType.Cube, "Roof", house, new Vector3(0f, 3.2f, 0f), new Vector3(3.8f, 3.8f, 4.7f), roof,
                rotation: Quaternion.Euler(0f, 90f, 45f), collider: false);
            LocalPrim(PrimitiveType.Cube, "Door", house, new Vector3(0f, 0.9f, 2.12f), new Vector3(1.1f, 1.8f, 0.1f), "Timber", collider: false);
            LocalPrim(PrimitiveType.Cube, "WindowL", house, new Vector3(-1.5f, 1.9f, 2.12f), new Vector3(0.8f, 0.8f, 0.08f), "Water", collider: false);
            LocalPrim(PrimitiveType.Cube, "WindowR", house, new Vector3(1.5f, 1.9f, 2.12f), new Vector3(0.8f, 0.8f, 0.08f), "Water", collider: false);
            LocalPrim(PrimitiveType.Cube, "Chimney", house, new Vector3(1.4f, 4.4f, -0.8f), new Vector3(0.6f, 1.6f, 0.6f), "StoneWarm", collider: false);
        }

        static void Flowers(Transform parent, Vector3 center, int count, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = center + new Vector3((float)rng.NextDouble() * 2f - 1f, 0.15f, (float)rng.NextDouble() * 2f - 1f);
                GameObject f = Prim(PrimitiveType.Sphere, "Flower", parent, p, Vector3.one * 0.3f, i % 2 == 0 ? "FlowerPink" : "FlowerYellow", collider: false);
                f.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void BuildLandmarks(Transform env)
        {
            Transform landmarks = Group("Landmarks", env);

            // 1. Village well (plaza centre).
            Transform well = Group("Village Well", landmarks);
            well.position = new Vector3(0f, 0f, 4f);
            ConvexCollider(LocalPrim(PrimitiveType.Cylinder, "Ring", well, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.45f, 2.4f), "Stone"));
            LocalPrim(PrimitiveType.Cylinder, "Water", well, new Vector3(0f, 0.9f, 0f), new Vector3(1.9f, 0.01f, 1.9f), "Water", collider: false);
            LocalPrim(PrimitiveType.Cube, "PostL", well, new Vector3(-1f, 1.5f, 0f), new Vector3(0.18f, 2.1f, 0.18f), "Timber");
            LocalPrim(PrimitiveType.Cube, "PostR", well, new Vector3(1f, 1.5f, 0f), new Vector3(0.18f, 2.1f, 0.18f), "Timber");
            LocalPrim(PrimitiveType.Cube, "Roof", well, new Vector3(0f, 2.75f, 0f), new Vector3(1.4f, 1.4f, 2.5f), "RoofRed",
                rotation: Quaternion.Euler(0f, 90f, 45f), collider: false);

            // 2. Great Old Oak (west, visible from the plaza).
            Transform oak = Group("Great Old Oak", landmarks);
            oak.position = new Vector3(-20f, 0f, 30f);
            if (!GreenhavenArt.NatureAvailable || !GreenhavenArt.LandmarkTree(oak, oak.position, 13f))
                BuildPrimitiveOak(oak);

            // 3. Whispering Forest gate (end of the path, north).
            Transform gate = Group("Whispering Forest Gate", landmarks);
            gate.position = new Vector3(4f, 0f, 46f);
            LocalPrim(PrimitiveType.Cube, "PillarL", gate, new Vector3(-2.6f, 2.5f, 0f), new Vector3(1f, 5f, 1f), "StoneWarm");
            LocalPrim(PrimitiveType.Cube, "PillarR", gate, new Vector3(2.6f, 2.5f, 0f), new Vector3(1f, 5f, 1f), "StoneWarm");
            LocalPrim(PrimitiveType.Cube, "Lintel", gate, new Vector3(0f, 5.3f, 0f), new Vector3(7f, 0.8f, 1.3f), "StoneWarm");
            LocalPrim(PrimitiveType.Sphere, "Moss", gate, new Vector3(-1.5f, 5.8f, 0f), new Vector3(2f, 0.6f, 1.4f), "Leaf2", collider: false);
        }

        static void BuildPrimitiveOak(Transform oak)
        {
            LocalPrim(PrimitiveType.Cylinder, "Trunk", oak, new Vector3(0f, 4f, 0f), new Vector3(2.2f, 4f, 2.2f), "Trunk");
            LocalPrim(PrimitiveType.Sphere, "Canopy1", oak, new Vector3(0f, 9.5f, 0f), new Vector3(10f, 7f, 10f), "Leaf3", collider: false);
            LocalPrim(PrimitiveType.Sphere, "Canopy2", oak, new Vector3(3f, 8f, 2f), new Vector3(6f, 5f, 6f), "Leaf1", collider: false);
            LocalPrim(PrimitiveType.Sphere, "Canopy3", oak, new Vector3(-3f, 8.5f, -1.5f), new Vector3(6f, 5f, 6f), "Leaf1", collider: false);
        }

        // Farm pen (east of the plaza) and a meadow (west) for the animals.
        static readonly Vector3 PenCenter = new Vector3(17f, 0f, -4f);
        static readonly Vector2 PenHalfSize = new Vector2(4f, 3f);
        static readonly Vector3 MeadowCenter = new Vector3(-28f, 0f, 6f);
        const float MeadowRadius = 7f;

        static void BuildFarm(Transform env)
        {
            if (!GreenhavenArt.AnimalsAvailable) return;
            Transform farm = Group("Farm", env);

            // Fence around the pen, with a gap on the plaza (west) side.
            Transform fence = Group("PenFence", farm);
            float x0 = PenCenter.x - PenHalfSize.x, x1 = PenCenter.x + PenHalfSize.x;
            float z0 = PenCenter.z - PenHalfSize.y, z1 = PenCenter.z + PenHalfSize.y;
            void Rail(Vector3 a, Vector3 b)
            {
                Vector3 mid = (a + b) * 0.5f;
                float len = Vector3.Distance(a, b);
                Prim(PrimitiveType.Cube, "Rail", fence, mid + Vector3.up * 0.55f, new Vector3(0.1f, 0.9f, len), "Timber",
                    rotation: Quaternion.LookRotation(b - a));
                for (float t = 0f; t <= len + 0.01f; t += 2f)
                    Prim(PrimitiveType.Cube, "Post", fence, Vector3.Lerp(a, b, t / len) + Vector3.up * 0.5f, new Vector3(0.15f, 1f, 0.15f), "Timber", collider: false);
            }
            Rail(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0));
            Rail(new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1));
            Rail(new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1));
            Rail(new Vector3(x0, 0f, z1), new Vector3(x0, 0f, PenCenter.z + 0.8f));
            Rail(new Vector3(x0, 0f, PenCenter.z - 0.8f), new Vector3(x0, 0f, z0));

            GreenhavenArt.Animal(farm, "Sheep", PenCenter + new Vector3(-1.5f, 0f, 1.2f), 30f, 1.0f);
            GreenhavenArt.Animal(farm, "Sheep", PenCenter + new Vector3(0.5f, 0f, 1.8f), 160f, 0.95f);
            GreenhavenArt.Animal(farm, "Sheep", PenCenter + new Vector3(2.2f, 0f, 0.4f), 250f, 1.05f);
            GreenhavenArt.Animal(farm, "Pig", PenCenter + new Vector3(-1f, 0f, -1.5f), 90f, 0.8f);
            GreenhavenArt.Animal(farm, "Pig", PenCenter + new Vector3(1.8f, 0f, -1.6f), 300f, 0.75f);

            GreenhavenArt.Animal(farm, "Pug", new Vector3(-2.4f, 0f, 6.2f), 140f, 0.45f); // napping by the well
            GreenhavenArt.Animal(farm, "Cow", MeadowCenter + new Vector3(-1.5f, 0f, 0f), 60f, 1.6f);
            GreenhavenArt.Animal(farm, "Horse", MeadowCenter + new Vector3(2.5f, 0f, 2f), 210f, 1.9f);
        }

        static void BuildJumpCourse(Transform env)
        {
            // Lookout platform east of the village: stepping stones need jumps, the ramp does not.
            Transform course = Group("Lookout Hill", env);
            course.position = new Vector3(20f, 0f, 20f);
            float[] heights = { 0.5f, 1.0f, 1.5f, 2.0f };
            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                LocalPrim(PrimitiveType.Cube, $"Step{i}", course, new Vector3(-7f + i * 1.8f, h * 0.5f, -1f), new Vector3(1.5f, h, 1.5f), "Stone");
            }
            LocalPrim(PrimitiveType.Cube, "Platform", course, new Vector3(3f, 1.25f, 0f), new Vector3(6f, 2.5f, 6f), "StoneWarm");
            // Ramp: 7.5 m long, rising 2.5 m (~20°), on the far side.
            LocalPrim(PrimitiveType.Cube, "Ramp", course, new Vector3(3f, 1.15f, 6.4f), new Vector3(3f, 0.3f, 7.5f), "StoneWarm",
                rotation: Quaternion.Euler(20f, 0f, 0f));
            LocalPrim(PrimitiveType.Cylinder, "FlagPole", course, new Vector3(4.5f, 4f, -1.5f), new Vector3(0.12f, 1.5f, 0.12f), "Timber");
            LocalPrim(PrimitiveType.Cube, "Flag", course, new Vector3(5.1f, 5f, -1.5f), new Vector3(1.1f, 0.6f, 0.05f), "Flag", collider: false);
        }

        static void BuildScatter(Transform env)
        {
            Transform trees = Group("Trees", env);
            Transform rocks = Group("Rocks", env);
            var rng = new System.Random(20261011);
            string[] leaves = { "Leaf1", "Leaf2", "Leaf3" };

            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < 75; attempt++)
            {
                // Denser toward the north (Whispering Forest side).
                float x = Mathf.Lerp(-66f, 66f, (float)rng.NextDouble());
                float z = Mathf.Lerp(-50f, 76f, Mathf.Sqrt((float)rng.NextDouble()));
                Vector3 p = new Vector3(x, 0f, z);
                if (!IsClearForScatter(p, 3.5f)) continue;

                float s = 0.8f + (float)rng.NextDouble() * 0.6f;
                string leaf = leaves[rng.Next(leaves.Length)];
                if (rng.NextDouble() < 0.5) RoundTree(trees, p, s, leaf);
                else PineTree(trees, p, s, leaf);
                placed++;
            }

            int rocksPlaced = 0;
            for (int attempt = 0; attempt < 300 && rocksPlaced < 28; attempt++)
            {
                Vector3 p = new Vector3(Mathf.Lerp(-60f, 60f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-45f, 70f, (float)rng.NextDouble()));
                if (!IsClearForScatter(p, 2.5f)) continue;
                float s = 0.4f + (float)rng.NextDouble() * 1.4f;
                Vector3 scale = new Vector3(s * 1.4f, s * 0.8f, s);
                GameObject rock = Prim(PrimitiveType.Sphere, "Rock", rocks, p + Vector3.up * scale.y * 0.3f, scale,
                    rng.NextDouble() < 0.5 ? "Stone" : "StoneWarm", collider: false,
                    rotation: Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                if (s > 0.8f) ConvexCollider(rock);
                else rock.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                rocksPlaced++;
            }
        }

        static bool IsClearForScatter(Vector3 p, float margin)
        {
            // Plaza and houses.
            if (new Vector2(p.x, p.z - 4f).magnitude < 17f + margin) return false;
            // Spawn area.
            if (new Vector2(p.x - SpawnPoint.x, p.z - SpawnPoint.z).magnitude < 6f + margin) return false;
            // Landmarks and jump course.
            if (new Vector2(p.x + 20f, p.z - 30f).magnitude < 7f + margin) return false;
            if (new Vector2(p.x - 4f, p.z - 46f).magnitude < 5f + margin) return false;
            if (p.x > 10f && p.x < 27f && p.z > 15f && p.z < 30f) return false;
            // Farm pen, meadow and the elder's bench.
            if (Mathf.Abs(p.x - PenCenter.x) < PenHalfSize.x + 1f + margin && Mathf.Abs(p.z - PenCenter.z) < PenHalfSize.y + 1f + margin) return false;
            if (new Vector2(p.x - MeadowCenter.x, p.z - MeadowCenter.z).magnitude < MeadowRadius + margin) return false;
            // Path corridor.
            for (int i = 0; i < PathPoints.Length - 1; i++)
            {
                if (DistanceToSegment(new Vector2(p.x, p.z), PathPoints[i], PathPoints[i + 1]) < 1.5f + margin) return false;
            }
            return true;
        }

        /// <summary>Planar distance from <paramref name="p"/> to the trail centre line.</summary>
        static float PathDistance(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < PathPoints.Length - 1; i++)
                best = Mathf.Min(best, DistanceToSegment(new Vector2(p.x, p.z), PathPoints[i], PathPoints[i + 1]));
            return best;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static void RoundTree(Transform parent, Vector3 p, float s, string leaf)
        {
            Transform tree = Group("RoundTree", parent);
            tree.position = p;
            tree.localScale = Vector3.one * s;
            LocalPrim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 1.2f, 0f), new Vector3(0.45f, 1.2f, 0.45f), "Trunk");
            LocalPrim(PrimitiveType.Sphere, "Canopy", tree, new Vector3(0f, 3.4f, 0f), new Vector3(3.2f, 2.8f, 3.2f), leaf, collider: false);
        }

        static void PineTree(Transform parent, Vector3 p, float s, string leaf)
        {
            Transform tree = Group("PineTree", parent);
            tree.position = p;
            tree.localScale = Vector3.one * s;
            LocalPrim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 0.9f, 0f), new Vector3(0.35f, 0.9f, 0.35f), "Trunk");
            LocalPrim(PrimitiveType.Sphere, "Lower", tree, new Vector3(0f, 2.4f, 0f), new Vector3(2.8f, 1.8f, 2.8f), leaf, collider: false);
            LocalPrim(PrimitiveType.Sphere, "Upper", tree, new Vector3(0f, 3.8f, 0f), new Vector3(1.8f, 1.8f, 1.8f), leaf, collider: false);
        }

        static void BuildBackdrop(Transform env)
        {
            // Distant silhouettes outside the playable bounds: no colliders, no shadows.
            Transform backdrop = Group("Backdrop", env);
            void Far(string name, Vector3 p, Vector3 scale, string mat)
            {
                GameObject go = Prim(PrimitiveType.Sphere, name, backdrop, p, scale, mat, collider: false);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Far("Cloudpeak", new Vector3(10f, 0f, 150f), new Vector3(70f, 110f, 60f), "Mountain");
            Far("CloudpeakSnow", new Vector3(10f, 44f, 147f), new Vector3(30f, 26f, 28f), "Snow");
            Far("RidgeWest", new Vector3(-70f, 0f, 125f), new Vector3(70f, 50f, 50f), "Mountain");
            Far("RidgeEast", new Vector3(80f, 0f, 120f), new Vector3(80f, 60f, 50f), "Mountain");
            Far("HillW", new Vector3(-100f, -5f, 20f), new Vector3(60f, 30f, 90f), "HillFar");
            Far("HillE", new Vector3(100f, -5f, 30f), new Vector3(60f, 26f, 100f), "HillFar");
            Far("HillS", new Vector3(0f, -6f, -85f), new Vector3(150f, 26f, 40f), "HillFar");
        }

        static void BuildBoundaries(Transform env)
        {
            Transform bounds = Group("Boundaries", env);
            void Wall(string name, Vector3 center, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(bounds, false);
                go.transform.position = center;
                go.AddComponent<BoxCollider>().size = size;
            }
            // Ground plane spans x -75..75, z -65..85.
            Wall("North", new Vector3(0f, 5f, 82f), new Vector3(150f, 10f, 1f));
            Wall("South", new Vector3(0f, 5f, -62f), new Vector3(150f, 10f, 1f));
            Wall("East", new Vector3(72f, 5f, 10f), new Vector3(1f, 10f, 150f));
            Wall("West", new Vector3(-72f, 5f, 10f), new Vector3(1f, 10f, 150f));
        }

        // ───────────────────────────── Characters ─────────────────────────────

        /// <summary>
        /// Player with CharacterController + gameplay components. Visual preference: the generated hero
        /// (Greenhaven ▸ Generate Hero), then the Kenney character, then a primitive placeholder.
        /// Requires <see cref="CreateMaterials"/> to have run (for the placeholder).
        /// </summary>
        internal static GameObject BuildPlayer(Vector3 spawn, string prefabName = "Player", GameObject heroOverride = null)
        {
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(spawn, Quaternion.identity);

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.04f;

            player.AddComponent<PlayerInputReader>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<AnimationDebugOverlay>(); // F3 in the Editor/dev builds

            Transform visual = Group("Visual", player.transform);
            // Preference: explicit override → rigged Greenhaven Adventurer model → generated hero (v2, then v1).
            GameObject hero = heroOverride;
            if (hero == null) hero = AdventurerModelRig.FindPrefab();
            if (hero == null) hero = HeroGenerator.FindPrefab(GeneratorPrefs.Settings.Seed);
            if (hero != null && GreenhavenArt.AnimationLibraryAvailable)
            {
                var heroMotor = new SerializedObject(player.GetComponent<PlayerMotor>());
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(hero, visual);
                Animator heroAnimator = instance.GetComponent<Animator>();
                heroAnimator.runtimeAnimatorController = GreenhavenArt.PlayerController(
                    heroMotor.FindProperty("walkSpeed").floatValue, heroMotor.FindProperty("runSpeed").floatValue);
                heroAnimator.applyRootMotion = false;
                player.AddComponent<PlayerAnimator>();
                SetLayerRecursively(player, IgnoreRaycastLayer);
                return SaveAndConnectPrefab(player, prefabName);
            }
            if (GreenhavenArt.CharacterAvailable)
            {
                var motorSo = new SerializedObject(player.GetComponent<PlayerMotor>());
                float walk = motorSo.FindProperty("walkSpeed").floatValue;
                float run = motorSo.FindProperty("runSpeed").floatValue;
                GreenhavenArt.BuildCharacter(visual, "adventurerSkin", GreenhavenArt.PlayerController(walk, run));
                player.AddComponent<PlayerAnimator>();
                SetLayerRecursively(player, IgnoreRaycastLayer);
                return SaveAndConnectPrefab(player, prefabName);
            }

            // Fallback placeholder adventurer: readable silhouette with a backpack so facing is obvious.
            LocalPrim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.75f, 0f), new Vector3(0.62f, 0.55f, 0.55f), "Tunic", collider: false);
            LocalPrim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.52f, 0f), Vector3.one * 0.5f, "Skin", collider: false);
            LocalPrim(PrimitiveType.Sphere, "Hair", visual, new Vector3(0f, 1.63f, -0.05f), new Vector3(0.53f, 0.36f, 0.53f), "Timber", collider: false);
            LocalPrim(PrimitiveType.Sphere, "EyeL", visual, new Vector3(-0.09f, 1.55f, 0.22f), Vector3.one * 0.07f, "Dark", collider: false);
            LocalPrim(PrimitiveType.Sphere, "EyeR", visual, new Vector3(0.09f, 1.55f, 0.22f), Vector3.one * 0.07f, "Dark", collider: false);
            LocalPrim(PrimitiveType.Cube, "Backpack", visual, new Vector3(0f, 0.95f, -0.32f), new Vector3(0.46f, 0.55f, 0.24f), "Backpack", collider: false);
            LocalPrim(PrimitiveType.Cube, "Bedroll", visual, new Vector3(0f, 1.28f, -0.34f), new Vector3(0.5f, 0.14f, 0.18f), "RoofRed", collider: false);

            SetLayerRecursively(player, IgnoreRaycastLayer);
            return SaveAndConnectPrefab(player, prefabName);
        }

        static void BuildNpcs(DialogueView dialogue, QuestManager quests, QuestDefinition supplies, QuestDefinition request)
        {
            BuildNpc(dialogue, "Mira", new Vector3(3.5f, 0f, -0.5f), 200f, "miraSkin", "Idle_Loop", "Idle_Talking_Loop",
                quests, new QuestLines
                {
                    Quest = supplies,
                    Offer = new[]
                    {
                        "Oh! A new face in Greenhaven. Welcome, traveller!",
                        "Could you help me? Three supply crates fell off my cart somewhere along the forest trail.",
                        "Rowan swears one rolled all the way up Lookout Hill, east of here. Bring them back and I'll bake you something special!",
                    },
                    Active = new[] { "Any luck with the crates? Try the trail north... and the top of Lookout Hill." },
                    Complete = new[]
                    {
                        "You found all three! Greenhaven owes you one, explorer.",
                        "Here, still-warm honey bread. Oh, and Elder Rowan was asking for you. He's on the bench by his house.",
                    },
                    Done = new[] { "Thanks again for the supplies! The whole village eats well tonight." },
                },
                "Hello again, traveller! The well behind me is the heart of our little plaza.");

            // Elder Rowan sits on a bench beside the Elder's House.
            Vector3 benchPos = new Vector3(-7.9f, 0f, 11.3f);
            const float benchYaw = 135f;
            Transform bench = new GameObject("Bench").transform;
            bench.SetPositionAndRotation(benchPos, Quaternion.Euler(0f, benchYaw, 0f));
            LocalPrim(PrimitiveType.Cube, "Seat", bench, new Vector3(0f, 0.42f, -0.15f), new Vector3(1.8f, 0.1f, 0.5f), "Timber");
            LocalPrim(PrimitiveType.Cube, "Back", bench, new Vector3(0f, 0.8f, -0.4f), new Vector3(1.8f, 0.6f, 0.08f), "Timber", collider: false);
            LocalPrim(PrimitiveType.Cube, "LegL", bench, new Vector3(-0.75f, 0.2f, -0.15f), new Vector3(0.1f, 0.4f, 0.45f), "Timber", collider: false);
            LocalPrim(PrimitiveType.Cube, "LegR", bench, new Vector3(0.75f, 0.2f, -0.15f), new Vector3(0.1f, 0.4f, 0.45f), "Timber", collider: false);
            BuildNpc(dialogue, "Rowan", benchPos + Quaternion.Euler(0f, benchYaw, 0f) * new Vector3(0f, 0f, RowanSeatForwardOffset), benchYaw,
                "elderSkin", "Sitting_Idle_Loop", "Sitting_Talking_Loop",
                quests, new QuestLines
                {
                    Quest = request,
                    Offer = new[]
                    {
                        "Mira tells me you're quite the finder of lost things!",
                        "I dropped my spectacles while resting under the Great Old Oak, west of the village. Would you fetch them for an old man?",
                    },
                    Active = new[] { "My spectacles should be by the roots of the Great Old Oak. West of here, you can't miss it." },
                    Complete = new[]
                    {
                        "My spectacles! Now I can read the old map again...",
                        "It shows a path up to Cloudpeak. When you're ready, we'll talk about the Mountain Clue.",
                    },
                    Done = new[] { "Cloudpeak awaits, explorer. That clue won't solve itself!" },
                },
                // Before Mira's quest is done, Rowan just chats.
                "Ah, young explorer. I am Rowan, elder of Greenhaven.",
                "When I was your age I climbed all the way to Cloudpeak. The view... you never forget it.",
                "Mira looks worried about something. Have you spoken to her by the well?");
        }

        /// <summary>Quest-specific dialogue for an NPC quest giver.</summary>
        sealed class QuestLines
        {
            public QuestDefinition Quest;
            public string[] Offer, Active, Complete, Done;
        }

        // How far in front of the bench's centre Rowan's root sits. Tune if the sitting pose floats or clips.
        const float RowanSeatForwardOffset = 0.1f;

        static void BuildNpc(DialogueView dialogue, string speaker, Vector3 position, float yaw, string skin,
            string idleClip, string talkClip, QuestManager quests, QuestLines questLines, params string[] lines)
        {
            var npc = new GameObject("NPC_" + speaker);
            npc.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            CapsuleCollider col = npc.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f, 0f);
            col.height = 1.7f;
            col.radius = 0.35f;

            Transform visual = Group("Visual", npc.transform);
            Animator animator = null;
            if (GreenhavenArt.CharacterAvailable)
            {
                animator = GreenhavenArt.BuildCharacter(visual, skin, GreenhavenArt.NpcController("NPC_" + speaker, idleClip, talkClip));
                animator.gameObject.AddComponent<AnimatorRandomStart>();
            }
            else
            {
                LocalPrim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.72f, 0f), new Vector3(0.7f, 0.55f, 0.6f), "NpcDress", collider: false);
                LocalPrim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.47f, 0f), Vector3.one * 0.48f, "Skin", collider: false);
            }

            DialogueInteractable talk = npc.AddComponent<DialogueInteractable>();
            var so = new SerializedObject(talk);
            so.FindProperty("speakerName").stringValue = speaker;
            so.FindProperty("promptVerb").stringValue = "Talk to";
            // A seated elder should not swivel on the bench.
            so.FindProperty("faceInteractor").boolValue = !idleClip.StartsWith("Sitting");
            SetStrings(so.FindProperty("lines"), lines);
            so.ApplyModifiedPropertiesWithoutUndo();

            QuestGiver giver = null;
            if (questLines != null && questLines.Quest != null)
            {
                giver = npc.AddComponent<QuestGiver>();
                var gso = new SerializedObject(giver);
                gso.FindProperty("quest").objectReferenceValue = questLines.Quest;
                gso.FindProperty("npcId").stringValue = speaker.ToLowerInvariant();
                SetStrings(gso.FindProperty("offerLines"), questLines.Offer ?? new string[0]);
                SetStrings(gso.FindProperty("activeLines"), questLines.Active ?? new string[0]);
                SetStrings(gso.FindProperty("completeLines"), questLines.Complete ?? new string[0]);
                SetStrings(gso.FindProperty("doneLines"), questLines.Done ?? new string[0]);
                gso.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject instance = SaveAndConnectPrefab(npc, "NPC_" + speaker);
            // Scene references applied as instance overrides (prefab assets cannot reference scene objects).
            SetRef(instance.GetComponent<DialogueInteractable>(), "dialogueView", dialogue);
            if (animator != null) SetRef(instance.GetComponent<DialogueInteractable>(), "talkAnimator", animator);
            if (giver != null)
            {
                SetRef(instance.GetComponent<QuestGiver>(), "manager", quests);
                SetRef(instance.GetComponent<DialogueInteractable>(), "questGiver", instance.GetComponent<QuestGiver>());
            }
        }

        // ───────────────────────────── Quests ─────────────────────────────

        const string QuestFolder = GameRoot + "/ScriptableObjects/Quests";

        static QuestManager BuildQuests(CollectibleRegistry registry, out QuestDefinition supplies, out QuestDefinition request)
        {
            EnsureFolder(QuestFolder);
            supplies = QuestAsset("Quest_MissingSupplies", q => q.Configure("missing_supplies", "The Missing Supplies",
                "Mira's supply crates fell off her cart along the forest trail.", null,
                new QuestStep("Find the missing supply crates", ObjectiveType.Collect, "supply", 3),
                new QuestStep("Bring the supplies back to Mira", ObjectiveType.TalkTo, "mira")));
            QuestDefinition suppliesRef = supplies;
            request = QuestAsset("Quest_VillagersRequest", q => q.Configure("villagers_request", "A Villager's Request",
                "Elder Rowan lost his spectacles near the Great Old Oak.", suppliesRef,
                new QuestStep("Find Rowan's spectacles near the Great Old Oak", ObjectiveType.Collect, "spectacles"),
                new QuestStep("Return the spectacles to Rowan", ObjectiveType.TalkTo, "rowan")));

            QuestManager manager = new GameObject("QuestManager").AddComponent<QuestManager>();
            var so = new SerializedObject(manager);
            SerializedProperty list = so.FindProperty("quests");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = supplies;
            list.GetArrayElementAtIndex(1).objectReferenceValue = request;
            so.FindProperty("registry").objectReferenceValue = registry;
            so.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        static QuestDefinition QuestAsset(string fileName, System.Action<QuestDefinition> configure)
        {
            string path = $"{QuestFolder}/{fileName}.asset";
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(quest, path);
            }
            configure(quest);
            EditorUtility.SetDirty(quest);
            AssetDatabase.SaveAssets();
            return quest;
        }

        static void BuildSupplies(CollectibleRegistry registry)
        {
            // "The Missing Supplies": two along the forest trail, one rewarding the Lookout Hill jumps.
            Transform group = Group("Supplies", null);
            SupplyCrate(group, registry, "supply_01", new Vector3(4.6f, 0f, 19f));
            SupplyCrate(group, registry, "supply_02", new Vector3(7.6f, 0f, 31f));
            SupplyCrate(group, registry, "supply_03", new Vector3(23.5f, 2.5f, 19.5f));
        }

        static void SupplyCrate(Transform parent, CollectibleRegistry registry, string id, Vector3 position)
        {
            CollectibleProp(parent, registry, id, "supply", "Supply Crate", position, 0.45f, visual =>
            {
                LocalPrim(PrimitiveType.Cube, "Crate", visual, Vector3.zero, Vector3.one * 0.6f, "Timber", collider: false);
                LocalPrim(PrimitiveType.Cube, "BandH", visual, Vector3.zero, new Vector3(0.62f, 0.1f, 0.62f), "FlowerYellow", collider: false);
                LocalPrim(PrimitiveType.Cube, "BandV", visual, Vector3.zero, new Vector3(0.1f, 0.62f, 0.62f), "FlowerYellow", collider: false);
            });
        }

        static void BuildSpectacles(CollectibleRegistry registry)
        {
            // "A Villager's Request": by the roots of the Great Old Oak. Oversized so they read at phone scale.
            CollectibleProp(null, registry, "spectacles_01", "spectacles", "Rowan's Spectacles", new Vector3(-17.4f, 0f, 27.2f), 0.5f, visual =>
            {
                Quaternion lens = Quaternion.Euler(90f, 0f, 0f);
                LocalPrim(PrimitiveType.Cylinder, "LensL", visual, new Vector3(-0.2f, 0f, 0f), new Vector3(0.3f, 0.02f, 0.3f), "Water", collider: false, rotation: lens);
                LocalPrim(PrimitiveType.Cylinder, "LensR", visual, new Vector3(0.2f, 0f, 0f), new Vector3(0.3f, 0.02f, 0.3f), "Water", collider: false, rotation: lens);
                LocalPrim(PrimitiveType.Cube, "Bridge", visual, new Vector3(0f, 0.04f, 0f), new Vector3(0.12f, 0.03f, 0.03f), "FlowerYellow", collider: false);
                LocalPrim(PrimitiveType.Cube, "ArmL", visual, new Vector3(-0.36f, 0.04f, -0.15f), new Vector3(0.03f, 0.03f, 0.3f), "FlowerYellow", collider: false);
                LocalPrim(PrimitiveType.Cube, "ArmR", visual, new Vector3(0.36f, 0.04f, -0.15f), new Vector3(0.03f, 0.03f, 0.3f), "FlowerYellow", collider: false);
            });
        }

        /// <summary>A bobbing, spinning pickup with a stable ID and a trigger the interactor can find.</summary>
        static void CollectibleProp(Transform parent, CollectibleRegistry registry, string id, string itemType, string displayName,
            Vector3 position, float visualHeight, System.Action<Transform> buildVisual)
        {
            var root = new GameObject($"Collectible_{id}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true; // detectable by the interactor, never blocks movement
            trigger.center = new Vector3(0f, 0.5f, 0f);
            trigger.radius = 0.6f;

            Transform visual = Group("Visual", root.transform);
            visual.localPosition = new Vector3(0f, visualHeight, 0f);
            buildVisual(visual);
            // Bobbing objects must not be static-batched.
            foreach (Transform t in root.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

            Collectible c = root.AddComponent<Collectible>();
            var so = new SerializedObject(c);
            so.FindProperty("collectibleId").stringValue = id;
            so.FindProperty("itemType").stringValue = itemType;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("registry").objectReferenceValue = registry;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildSign(DialogueView dialogue)
        {
            Transform sign = new GameObject("Signpost").transform;
            sign.SetPositionAndRotation(new Vector3(2.2f, 0f, -9f), Quaternion.Euler(0f, 180f, 0f));
            LocalPrim(PrimitiveType.Cube, "Post", sign, new Vector3(0f, 0.8f, 0f), new Vector3(0.15f, 1.6f, 0.15f), "Timber");
            LocalPrim(PrimitiveType.Cube, "Board", sign, new Vector3(0f, 1.45f, 0.05f), new Vector3(1.2f, 0.5f, 0.08f), "Path", collider: false);

            DialogueInteractable read = sign.gameObject.AddComponent<DialogueInteractable>();
            var so = new SerializedObject(read);
            so.FindProperty("speakerName").stringValue = "Signpost";
            so.FindProperty("promptVerb").stringValue = "Read";
            so.FindProperty("faceInteractor").boolValue = false;
            SetStrings(so.FindProperty("lines"), "North: Whispering Forest.   East: Lookout Hill.   Here: Greenhaven Village.");
            so.FindProperty("dialogueView").objectReferenceValue = dialogue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void BuildCamera(GameObject player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = SpawnPoint + new Vector3(0f, 3f, -5.5f);

            ThirdPersonCameraRig rig = camGo.AddComponent<ThirdPersonCameraRig>();
            SetRef(rig, "target", player.transform);
            SetRef(rig, "input", player.GetComponent<PlayerInputReader>());
        }

        // ───────────────────────────── HUD ─────────────────────────────

        internal static DialogueView BuildHud(GameObject player, QuestManager quests)
        {
            PlayerInputReader input = player.GetComponent<PlayerInputReader>();
            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // With no actions asset assigned, the module falls back to its default UI actions in OnEnable.
            es.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // landscape: scale by height so buttons keep physical size
            canvasGo.AddComponent<GraphicRaycaster>();

            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Look area first so it sits behind every control.
            RectTransform look = UiRect("TouchLookArea", canvasGo.transform, new Vector2(0.4f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            Image lookImage = look.gameObject.AddComponent<Image>();
            lookImage.color = new Color(0f, 0f, 0f, 0f); // invisible but still receives raycasts
            SetRef(look.gameObject.AddComponent<TouchLookArea>(), "input", input);

            RectTransform safe = UiRect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Joystick (bottom-left). The knob carries OnScreenStick, emulating the gamepad left stick.
            RectTransform stickBase = UiRect("JoystickBase", safe, Vector2.zero, Vector2.zero, new Vector2(270f, 250f), new Vector2(320f, 320f));
            Image baseImg = stickBase.gameObject.AddComponent<Image>();
            baseImg.sprite = circle;
            baseImg.color = new Color(1f, 1f, 1f, 0.28f);
            RectTransform knob = UiRect("JoystickKnob", stickBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            Image knobImg = knob.gameObject.AddComponent<Image>();
            knobImg.sprite = circle;
            knobImg.color = new Color(1f, 1f, 1f, 0.8f);
            OnScreenStick stick = knob.gameObject.AddComponent<OnScreenStick>();
            SetControlPath(stick, "<Gamepad>/leftStick");
            stick.movementRange = 110f;
            stick.behaviour = OnScreenStick.Behaviour.RelativePositionWithStaticOrigin;

            // Jump (large, bottom-right) and Interact (above-left of Jump).
            RectTransform jump = RoundButton("JumpButton", safe, new Vector2(-200f, 190f), 210f, circle, font, "Jump", new Color(0.4f, 0.75f, 1f, 0.85f));
            SetControlPath(jump.gameObject.AddComponent<OnScreenButton>(), "<Gamepad>/buttonSouth");

            RectTransform interact = RoundButton("InteractButton", safe, new Vector2(-420f, 300f), 170f, circle, font, "Interact", new Color(1f, 0.8f, 0.35f, 0.9f));
            SetControlPath(interact.gameObject.AddComponent<OnScreenButton>(), "<Gamepad>/buttonWest");
            CanvasGroup interactGroup = interact.gameObject.AddComponent<CanvasGroup>();

            // Interaction prompt (centre-bottom, above the dialogue area).
            RectTransform prompt = UiRect("InteractionPrompt", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 360f), new Vector2(520f, 80f));
            Image promptBg = prompt.gameObject.AddComponent<Image>();
            promptBg.sprite = rounded;
            promptBg.type = Image.Type.Sliced;
            promptBg.color = new Color(0.1f, 0.12f, 0.16f, 0.7f);
            promptBg.raycastTarget = false;
            Text promptText = UiText("Text", prompt, font, 40, TextAnchor.MiddleCenter, Color.white);

            // Quest tracker (top-left): title + current objective.
            RectTransform tracker = UiRect("QuestTracker", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f, -95f), new Vector2(600f, 130f));
            Image trackerBg = tracker.gameObject.AddComponent<Image>();
            trackerBg.sprite = rounded;
            trackerBg.type = Image.Type.Sliced;
            trackerBg.color = new Color(0.1f, 0.12f, 0.16f, 0.7f);
            trackerBg.raycastTarget = false;
            Text trackerTitle = UiText("Title", tracker, font, 34, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.45f));
            trackerTitle.fontStyle = FontStyle.Bold;
            trackerTitle.rectTransform.offsetMin = new Vector2(24f, 0f);
            trackerTitle.rectTransform.offsetMax = new Vector2(-20f, -14f);
            Text trackerObjective = UiText("Objective", tracker, font, 32, TextAnchor.LowerLeft, Color.white);
            trackerObjective.rectTransform.offsetMin = new Vector2(24f, 16f);
            trackerObjective.rectTransform.offsetMax = new Vector2(-20f, 0f);
            QuestTrackerView trackerView = canvasGo.AddComponent<QuestTrackerView>();
            SetRef(trackerView, "manager", quests);
            SetRef(trackerView, "root", tracker.gameObject);
            SetRef(trackerView, "titleText", trackerTitle);
            SetRef(trackerView, "objectiveText", trackerObjective);

            // Quest milestone banner (top-centre).
            RectTransform toast = UiRect("QuestToast", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(820f, 170f));
            Image toastBg = toast.gameObject.AddComponent<Image>();
            toastBg.sprite = rounded;
            toastBg.type = Image.Type.Sliced;
            toastBg.color = new Color(1f, 0.96f, 0.86f, 0.95f);
            toastBg.raycastTarget = false;
            CanvasGroup toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            Text toastHeader = UiText("Header", toast, font, 34, TextAnchor.UpperCenter, new Color(0.65f, 0.3f, 0.25f));
            toastHeader.fontStyle = FontStyle.Bold;
            toastHeader.rectTransform.offsetMax = new Vector2(0f, -18f);
            Text toastTitle = UiText("Title", toast, font, 50, TextAnchor.LowerCenter, new Color(0.2f, 0.17f, 0.15f));
            toastTitle.rectTransform.offsetMin = new Vector2(0f, 22f);
            QuestToastView toastView = canvasGo.AddComponent<QuestToastView>();
            SetRef(toastView, "manager", quests);
            SetRef(toastView, "group", toastGroup);
            SetRef(toastView, "headerText", toastHeader);
            SetRef(toastView, "titleText", toastTitle);

            InteractionPromptView promptView = canvasGo.AddComponent<InteractionPromptView>();
            SetRef(promptView, "interactor", interactor);
            SetRef(promptView, "promptRoot", prompt.gameObject);
            SetRef(promptView, "promptText", promptText);
            SetRef(promptView, "interactButton", interactGroup);

            // Dialogue panel (bottom centre, between the controls).
            var dialogueRoot = UiRect("Dialogue", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform panel = UiRect("Panel", dialogueRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(980f, 230f));
            Image panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.sprite = rounded;
            panelBg.type = Image.Type.Sliced;
            panelBg.color = new Color(1f, 0.97f, 0.9f, 0.95f);
            Text speaker = UiText("Speaker", panel, font, 38, TextAnchor.UpperLeft, new Color(0.65f, 0.3f, 0.25f));
            speaker.fontStyle = FontStyle.Bold;
            speaker.rectTransform.offsetMin = new Vector2(36f, 0f);
            speaker.rectTransform.offsetMax = new Vector2(-36f, -18f);
            Text body = UiText("Body", panel, font, 34, TextAnchor.UpperLeft, new Color(0.2f, 0.17f, 0.15f));
            body.rectTransform.offsetMin = new Vector2(36f, 20f);
            body.rectTransform.offsetMax = new Vector2(-36f, -70f);
            Text hint = UiText("Hint", panel, font, 24, TextAnchor.LowerRight, new Color(0.45f, 0.4f, 0.35f));
            hint.text = "Tap or press Interact to continue";
            hint.rectTransform.offsetMin = new Vector2(36f, 12f);
            hint.rectTransform.offsetMax = new Vector2(-30f, 0f);
            // Dialogue root itself must not block touches; only the panel does.
            DialogueView dialogue = dialogueRoot.gameObject.AddComponent<DialogueView>();
            SetRef(dialogue, "panel", panel.gameObject);
            SetRef(dialogue, "speakerText", speaker);
            SetRef(dialogue, "bodyText", body);
            SetRef(dialogue, "interactor", interactor);
            panel.gameObject.SetActive(false);

            return dialogue;
        }

        static RectTransform RoundButton(string name, Transform parent, Vector2 offsetFromBottomRight, float size, Sprite sprite, Font font, string label, Color color)
        {
            RectTransform rt = UiRect(name, parent, new Vector2(1f, 0f), new Vector2(1f, 0f), offsetFromBottomRight, new Vector2(size, size));
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            Text t = UiText("Label", rt, font, Mathf.RoundToInt(size * 0.2f), TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.15f));
            t.text = label;
            return rt;
        }

        static RectTransform UiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            if (anchorMin == anchorMax) rt.sizeDelta = size;
            else
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            return rt;
        }

        static Text UiText(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
        {
            RectTransform rt = UiRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        /// <summary>
        /// Our scene components must reference scene objects, never prefab/model assets
        /// (an asset's components never run). Returns the number of offending fields.
        /// </summary>
        internal static int ValidateSceneReferences(Scene scene)
        {
            int bad = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string ns = mb.GetType().Namespace;
                    if (ns == null || !ns.StartsWith("Greenhaven")) continue;
                    SerializedProperty it = new SerializedObject(mb).GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object value = it.objectReferenceValue;
                        if ((value is GameObject || value is Component) && EditorUtility.IsPersistent(value))
                        {
                            Debug.LogError($"Greenhaven builder: {mb.GetType().Name}.{it.name} on '{mb.name}' points at asset '{value.name}' instead of a scene object.", mb);
                            bad++;
                        }
                    }
                }
            }
            return bad;
        }

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 worldPos, Vector3 scale, string mat,
            bool collider = true, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, rotation ?? Quaternion.identity);
            go.transform.localScale = scale;
            Finish(go, mat, collider);
            return go;
        }

        static GameObject LocalPrim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, string mat,
            bool collider = true, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
            Finish(go, mat, collider);
            return go;
        }

        static void Finish(GameObject go, string mat, bool collider)
        {
            go.GetComponent<Renderer>().sharedMaterial = materials[mat];
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            // Characters are moved at runtime; everything else built here is static scenery.
            if (go.transform.root.name != "Player" && !go.transform.root.name.StartsWith("NPC"))
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Swap a primitive's default collider for a convex mesh collider (flat cylinders/squashed spheres).</summary>
        static void ConvexCollider(GameObject go)
        {
            Collider existing = go.GetComponent<Collider>();
            if (existing != null) Object.DestroyImmediate(existing);
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        /// <summary>
        /// Saves <paramref name="go"/> as a prefab and links it. Returns the SCENE INSTANCE:
        /// SaveAsPrefabAssetAndConnect itself returns the asset, which must never be wired into scene references.
        /// </summary>
        static GameObject SaveAndConnectPrefab(GameObject go, string prefabName)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
            return go;
        }

        internal static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"Greenhaven builder: '{property}' not found on {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Writes the serialized control path directly. Using the public setter at edit time
        /// would register a virtual gamepad in the Editor.
        /// </summary>
        static void SetControlPath(Object onScreenControl, string path)
        {
            var so = new SerializedObject(onScreenControl);
            so.FindProperty("m_ControlPath").stringValue = path;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetStrings(SerializedProperty array, params string[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
