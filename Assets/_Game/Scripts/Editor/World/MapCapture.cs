using System.IO;
using System.Linq;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greenhaven.Editor.WorldTools
{
    /// <summary>
    /// Captures the open scene from straight above as its map picture (Generated/Maps/Map_{scene}.png), gives
    /// it a parchment wash, and adds a <see cref="SceneMap"/> to the scene with the covered world area.
    /// Call at the end of a scene build, once everything is placed.
    /// </summary>
    public static class MapCapture
    {
        public const string Folder = GeneratedAssetStore.Root + "/Maps";
        static readonly Color Parchment = new Color(0.96f, 0.92f, 0.84f);

        public static SceneMap Capture(string sceneName, string title, Rect worldRect, int size = 1024)
        {
            GeneratedAssetStore.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(GeneratedAssetStore.Root, "Maps");
            string path = $"{Folder}/Map_{sceneName}.png";

            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var camGo = new GameObject("~MapCamera");
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(worldRect.width, worldRect.height) * 0.5f;
                cam.aspect = 1f;
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Parchment;
                cam.cullingMask = ~(1 << 5); // everything but UI
                cam.transform.position = new Vector3(worldRect.center.x, 200f, worldRect.center.y);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                rt = new RenderTexture(size, size, 24);
                cam.targetTexture = rt;
                cam.Render();
                tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                RenderTexture.active = null;

                // Parchment wash: lift and warm the colours, darken the edges a little.
                Color[] px = tex.GetPixels();
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Color c = px[x + y * size];
                        float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                        c = Color.Lerp(c, new Color(grey, grey, grey), 0.25f);
                        c = Color.Lerp(c, Parchment, 0.3f);
                        float ex = Mathf.Abs(x / (float)size - 0.5f) * 2f, ey = Mathf.Abs(y / (float)size - 0.5f) * 2f;
                        float edge = Mathf.Clamp01((Mathf.Max(ex, ey) - 0.8f) / 0.2f);
                        c = Color.Lerp(c, c * 0.8f, edge);
                        px[x + y * size] = c;
                    }
                tex.SetPixels(px);
                File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            }
            finally
            {
                RenderSettings.fog = fog;
                if (rt != null) rt.Release();
                if (tex != null) Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) AssetDatabase.SetLabels(sprite.texture, new[] { GeneratedAssetStore.Label });

            foreach (SceneMap old in Object.FindObjectsByType<SceneMap>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
            var mapGo = new GameObject("SceneMap");
            SceneMap map = mapGo.AddComponent<SceneMap>();
            var so = new SerializedObject(map);
            so.FindProperty("title").stringValue = title;
            so.FindProperty("map").objectReferenceValue = sprite;
            // The picture is square and covers the larger side centred on the rect; store exactly what it shows.
            float side = Mathf.Max(worldRect.width, worldRect.height);
            so.FindProperty("worldRect").rectValue = new Rect(worldRect.center.x - side * 0.5f, worldRect.center.y - side * 0.5f, side, side);
            so.ApplyModifiedPropertiesWithoutUndo();
            return map;
        }

        [MenuItem("Greenhaven/Capture Map of Open Scene", priority = 62)]
        static void Menu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.gameObject.layer == 5 || r.name.StartsWith("Distant")) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            var rect = any ? new Rect(b.min.x, b.min.z, b.size.x, b.size.z) : new Rect(-50f, -50f, 100f, 100f);
            Capture(scene.name, scene.name, rect);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Map captured", $"Saved {Folder}/Map_{scene.name}.png covering {rect}. Save the scene to keep the SceneMap.", "OK");
        }
    }
}
