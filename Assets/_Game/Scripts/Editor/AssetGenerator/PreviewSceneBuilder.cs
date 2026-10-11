using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Builds a separate showcase scene (Generated/Preview/GeneratedAssetsPreview.unity) with every
    /// generated prefab laid out in rows by category. Never touches the game scenes or Build Settings.
    /// </summary>
    public static class PreviewSceneBuilder
    {
        const string ScenePath = GeneratedAssetStore.PreviewFolder + "/GeneratedAssetsPreview.unity";
        const string GroundMaterialPath = GeneratedAssetStore.MaterialFolder + "/M_Gen_PreviewGround.mat";

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "Stop Play mode before building the preview scene.";

            // Group generated prefabs by category prefix, in a stable order.
            var rows = new SortedDictionary<string, List<GameObject>>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { GeneratedAssetStore.PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!GeneratedAssetStore.IsGenerated(prefab)) continue;
                string[] parts = prefab.name.Split('_');
                string category = parts.Length > 1 ? parts[1] : "Other";
                if (!rows.TryGetValue(category, out List<GameObject> list)) rows[category] = list = new List<GameObject>();
                list.Add(prefab);
            }
            if (rows.Count == 0) return "No generated prefabs yet. Generate some assets first.";

            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Replace preview scene?",
                    $"{ScenePath} will be rebuilt. Only the preview scene is affected.", "Rebuild", "Cancel"))
                return "Cancelled. Preview scene unchanged.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            GeneratedAssetStore.EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.78f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.74f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.34f, 0.28f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 6f);
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            float z = -12f;
            int total = 0;
            foreach (KeyValuePair<string, List<GameObject>> row in rows)
            {
                float x = -((row.Value.Count - 1) * 4.5f) * 0.5f;
                var label = new GameObject($"Label_{row.Key}");
                label.transform.SetPositionAndRotation(new Vector3(x - 6f, 0.05f, z), Quaternion.Euler(90f, 0f, 0f));
                TextMesh text = label.AddComponent<TextMesh>();
                text.text = row.Key;
                text.font = font;
                text.characterSize = 0.35f;
                text.fontSize = 48;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(0.15f, 0.2f, 0.12f);
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;

                foreach (GameObject prefab in row.Value)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.position = new Vector3(x, 0f, z);
                    x += 4.5f;
                    total++;
                }
                z += 12f;
            }

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.85f, 0.96f);
            cam.transform.SetPositionAndRotation(new Vector3(0f, 14f, -34f), Quaternion.Euler(22f, 0f, 0f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"Preview scene saved to {ScenePath} with {total} prefab(s) in {rows.Count} row(s). It is not added to Build Settings.";
        }

        static Material GroundMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.42f));
            AssetDatabase.CreateAsset(mat, GroundMaterialPath);
            AssetDatabase.SetLabels(mat, new[] { GeneratedAssetStore.Label });
            return mat;
        }
    }
}
