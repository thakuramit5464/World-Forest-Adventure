using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Greenhaven.Collectibles;
using Greenhaven.Editor.WindTools;
using Greenhaven.Player;
using Greenhaven.Quests;
using Greenhaven.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greenhaven.Editor.House
{
    /// <summary>
    /// Builds Assets/_Game/Scenes/HouseTest.unity: the rural courtyard house prefab on open ground with the existing
    /// player, camera and HUD, then checks that every entrance and the stair are clear and that a CharacterController
    /// with the player's settings can walk into the cream room and up the stair onto the roof. Touches no other scene.
    /// </summary>
    public static class HouseTestSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/HouseTest.unity";

        [MenuItem("Greenhaven/House/Create House Test Scene", priority = 111)]
        static void Menu()
        {
            string result = Build();
            Debug.Log("Greenhaven — House test scene:\n" + result);
            EditorUtility.DisplayDialog("House Test Scene", result, "OK");
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";
            var log = new StringBuilder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuralCourtyardHouse.PrefabPath);
            if (prefab == null)
            {
                log.AppendLine(RuralCourtyardHouse.Generate());
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuralCourtyardHouse.PrefabPath);
                if (prefab == null) return log + "\nThe house prefab could not be created.";
            }
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild house test scene?", $"{ScenePath} will be rebuilt. No other scene is changed.", "Rebuild", "Cancel"))
                return "Cancelled.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            Phase1SceneBuilder.CreateMaterials();
            GreenhavenArt.PrepareImports();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_Grass.mat");
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);
            var house = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            house.transform.position = Vector3.zero;

            GameObject player = Phase1SceneBuilder.BuildPlayer(new Vector3(1.5f, 0.05f, -9f), "Player_HouseTest");
            Phase1SceneBuilder.BuildCamera(player);
            WindSetup.AddToScene();
            var registry = new GameObject("CollectibleRegistry").AddComponent<CollectibleRegistry>();
            var quests = new GameObject("QuestManager").AddComponent<QuestManager>();
            Phase1SceneBuilder.SetRef(quests, "registry", registry);
            Phase1SceneBuilder.BuildHud(player, quests);

            Physics.SyncTransforms();
            Validate(house, player, log);
            int badRefs = Phase1SceneBuilder.ValidateSceneReferences(scene);
            if (badRefs > 0) log.AppendLine($"{badRefs} scene reference problem(s); see Console.");
            EditorSceneManager.SaveScene(scene, ScenePath);
            log.Insert(0, $"House test scene saved to {ScenePath}. Press Play: walk into the courtyard, through the open grille door, and up the stair to the roof.\n");
            return log.ToString();
        }

        /// <summary>Clear-entrance checks and a walk test with the player's CharacterController settings.</summary>
        public static void Validate(GameObject house, GameObject player, StringBuilder log)
        {
            var cc = player.GetComponent<CharacterController>();
            Vector3 origin = house.transform.position;
            var blocked = new List<string>();
            foreach ((string name, Vector3 point) in RuralCourtyardHouse.Entrances)
            {
                Vector3 p = origin + point;
                Collider[] hits = Physics.OverlapCapsule(p + Vector3.up * (cc.radius + 0.05f), p + Vector3.up * (cc.height - cc.radius), cc.radius * 0.95f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Where(c => !c.transform.IsChildOf(player.transform)).ToArray();
                if (hits.Length > 0) blocked.Add($"{name} ({string.Join(", ", hits.Select(h => h.name).Distinct())})");
            }
            log.AppendLine(blocked.Count == 0 ? $"Entrances: all {RuralCourtyardHouse.Entrances.Length} clear for the player's capsule." : "⚠ Blocked: " + string.Join("; ", blocked));

            // Walk test: courtyard → open grille door → cream room; then courtyard → stair → landing → terracotta roof.
            Walk(player, cc, "into the cream room", new[] { new Vector3(4.35f, 0f, -2f), new Vector3(4.35f, 0f, 3.6f), new Vector3(4.35f, 0f, 6.2f), new Vector3(3.2f, 0f, 7.6f) }, 0.3f, log);
            float stairX = -0.975f;
            Walk(player, cc, "up the stair onto the roof", new[] { new Vector3(stairX, 0f, -1.5f), new Vector3(stairX, 0f, 2.8f), new Vector3(stairX, 0f, 7.2f), new Vector3(stairX, 0f, 8.6f), new Vector3(-5.5f, 0f, 8.0f) }, 3.2f, log);
        }

        static void Walk(GameObject player, CharacterController source, string label, Vector3[] path, float expectedFinalY, StringBuilder log)
        {
            bool wasActive = player.activeSelf;
            player.SetActive(false);
            var go = new GameObject("~HouseWalkTest");
            try
            {
                CharacterController cc = go.AddComponent<CharacterController>();
                cc.height = source.height;
                cc.radius = source.radius;
                cc.center = source.center;
                cc.stepOffset = source.stepOffset;
                cc.slopeLimit = source.slopeLimit;
                cc.skinWidth = source.skinWidth;
                go.transform.position = new Vector3(path[0].x, 0.3f, path[0].z);
                Physics.SyncTransforms();
                const float dt = 1f / 50f, speed = 2.5f;
                float vy = 0f;
                int target = 1, stuck = 0;
                float best = float.MaxValue;
                for (int step = 0; step < 6000 && target < path.Length; step++)
                {
                    Vector3 pos = go.transform.position;
                    Vector2 to = new Vector2(path[target].x - pos.x, path[target].z - pos.z);
                    if (to.magnitude < 0.25f)
                    {
                        target++;
                        best = float.MaxValue;
                        stuck = 0;
                        continue;
                    }
                    vy = cc.isGrounded ? -2f : Mathf.Max(vy - 22f * dt, -30f);
                    Vector3 dir = new Vector3(to.x, 0f, to.y).normalized;
                    cc.Move((dir * speed + Vector3.up * vy) * dt);
                    if (to.magnitude < best - 0.01f)
                    {
                        best = to.magnitude;
                        stuck = 0;
                    }
                    else if (++stuck > 150) break;
                }
                bool arrived = target >= path.Length && Mathf.Abs(go.transform.position.y - expectedFinalY) < 0.4f;
                log.AppendLine(arrived
                    ? $"Walk test {label}: reached (height {go.transform.position.y:0.00} m) without jumping."
                    : $"⚠ Walk test {label}: stopped near waypoint {target} at {go.transform.position}.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                player.SetActive(wasActive);
                Physics.SyncTransforms();
            }
        }
    }
}
