using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Greenhaven.Editor.Cloudpeak;
using Greenhaven.Editor.VillageScene;
using Greenhaven.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.WorldTools
{
    /// <summary>
    /// Links GreenhavenVillage and CloudpeakAdventure: a gate trigger plus an arrival point in each scene,
    /// and both scenes in the build list with the village first. Used by both scene builders (so rebuilds
    /// keep the link) and by a menu that links already-built scenes without rebuilding them.
    /// </summary>
    public static class WorldLinkSetup
    {
        public const string VillageScene = "GreenhavenVillage", CloudpeakScene = "CloudpeakAdventure";
        const string FromVillage = "from_village", FromCloudpeak = "from_cloudpeak";
        const string GateName = "Scene Link", ArrivalName = "Arrival Point";

        // Village north exit: the gate stands at (1.3, 31); the boundary wall is at 35.5.
        static readonly Vector2 VillageTrigger = new Vector2(1.45f, 33.6f), VillageArrival = new Vector2(0.9f, 27.5f);

        /// <summary>Adds the village side of the link to the open scene. <paramref name="ground"/> gives the ground height.</summary>
        public static void AddToVillage(Func<Vector2, float> ground)
        {
            Transform root = Root("World Links");
            AddGate(root, VillageTrigger, ground, 7f, new Vector3(5f, 3.5f, 2.5f), CloudpeakScene, FromVillage, "To the Whispering Forest");
            AddArrival(root, VillageArrival, ground, 180f, FromCloudpeak, "back from Cloudpeak");
        }

        /// <summary>Adds the Cloudpeak side of the link: behind the forest entrance gate, and at the spawn.</summary>
        public static void AddToCloudpeak(CloudpeakWorld world, Func<Vector2, float> ground)
        {
            Transform root = Root("World Links");
            AddGate(root, world.Entrance + new Vector2(0f, -2.8f), ground, 0f, new Vector3(8f, 3.5f, 2.5f), VillageScene, FromCloudpeak, "To Greenhaven Village");
            AddArrival(root, world.Spawn, ground, 0f, FromVillage, "from Greenhaven Village");
        }

        static Transform Root(string name)
        {
            foreach (GameObject existing in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == name))
                Object.DestroyImmediate(existing);
            return new GameObject(name).transform;
        }

        static void AddGate(Transform parent, Vector2 p, Func<Vector2, float> ground, float yaw, Vector3 size, string scene, string arrival, string label)
        {
            var go = new GameObject($"{GateName} — {label}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(p.x, ground(p), p.y), Quaternion.Euler(0f, yaw, 0f));
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.size = size;
            SceneGate gate = go.AddComponent<SceneGate>();
            var so = new SerializedObject(gate);
            so.FindProperty("targetScene").stringValue = scene;
            so.FindProperty("arrivalId").stringValue = arrival;
            so.FindProperty("label").stringValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddArrival(Transform parent, Vector2 p, Func<Vector2, float> ground, float yaw, string id, string label)
        {
            var go = new GameObject($"{ArrivalName} — {label}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(p.x, ground(p) + 0.05f, p.y), Quaternion.Euler(0f, yaw, 0f));
            SceneArrival arrival = go.AddComponent<SceneArrival>();
            var so = new SerializedObject(arrival);
            so.FindProperty("arrivalId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Village first (the game starts there), Cloudpeak second, every other listed scene kept.</summary>
        public static string UpdateBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(VillageSceneBuilder.ScenePath, true),
                new EditorBuildSettingsScene(CloudpeakSceneBuilder.ScenePath, true),
            };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.path == VillageSceneBuilder.ScenePath || s.path == CloudpeakSceneBuilder.ScenePath) continue;
                scenes.Add(new EditorBuildSettingsScene(s.path, s.enabled && !s.path.EndsWith("Scenes/SampleScene.unity")));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            return string.Join("\n", scenes.Select((scene, i) => $"{i}. {(scene.enabled ? "✔" : "✘")} {scene.path}"));
        }

        /// <summary>Ground height from the open scene's colliders (highest solid hit straight down).</summary>
        static float RaycastGround(Vector2 p)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 300f, p.y), Vector3.down, 600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return hits.Length == 0 ? 0f : hits.Max(h => h.point.y);
        }

        [MenuItem("Greenhaven/Link Village and Cloudpeak", priority = 49)]
        static void LinkMenu()
        {
            const string title = "Link Village and Cloudpeak";
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(title, "Stop Play mode first.", "OK");
                return;
            }
            if (!File.Exists(VillageSceneBuilder.ScenePath) || !File.Exists(CloudpeakSceneBuilder.ScenePath))
            {
                EditorUtility.DisplayDialog(title, "Create both scenes first (Greenhaven ▸ Create Village Scene and Create Cloudpeak Adventure Scene).", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog(title,
                    "This adds a gate trigger and an arrival point to GreenhavenVillage and CloudpeakAdventure (replacing any earlier 'World Links'), " +
                    "and puts both scenes first in the build scene list, with the village as the start scene. Nothing else in the scenes changes.",
                    "Link", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene village = EditorSceneManager.OpenScene(VillageSceneBuilder.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            AddToVillage(RaycastGround);
            EditorSceneManager.SaveScene(village);

            Scene cloudpeak = EditorSceneManager.OpenScene(CloudpeakSceneBuilder.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            AddToCloudpeak(CloudpeakWorld.Generate(CloudpeakWindow.SavedSettings), RaycastGround);
            EditorSceneManager.SaveScene(cloudpeak);

            string list = UpdateBuildScenes();
            EditorSceneManager.OpenScene(VillageSceneBuilder.ScenePath, OpenSceneMode.Single);
            string report = "Linked. Walk through the village's north gate to reach the Whispering Forest; the forest entrance gate leads back.\n\n" +
                            $"Build scene list:\n{list}\n\nThe village scene is open: press Play to start there.";
            Debug.Log($"Greenhaven — {title}:\n{report}");
            EditorUtility.DisplayDialog(title, report, "OK");
        }
    }
}
