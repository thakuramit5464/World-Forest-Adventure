using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Renders the rigged adventurer v1 and v2 side by side (front, three-quarter, side) in a pose sampled from
    /// the animation library, built in memory from the OBJ (nothing saved, no scene touched).
    /// Greenhaven ▸ Capture Adventurer Preview writes Generated/Preview/Adventurer_v1_v2.png.
    /// </summary>
    [InitializeOnLoad]
    public static class AdventurerPreview
    {
        const int TileW = 300, TileH = 420;
        const string DevFlag = "Temp/adventurer_preview.flag";

        static AdventurerPreview()
        {
            // One-shot development hook: build the v2 prefab (same as Greenhaven ▸ Build Adventurer v2).
            if (File.Exists("Temp/adventurer_build.flag")) EditorApplication.delayCall += () =>
            {
                File.Delete("Temp/adventurer_build.flag");
                var sb = new System.Text.StringBuilder(AdventurerModelRig.Rig(true));
                foreach (AdventurerModelRig.Variant v in AdventurerModelRig.Villagers) sb.AppendLine().Append(AdventurerModelRig.Rig(true, v).Split('\n')[0]);
                string result = sb.ToString();
                Debug.Log("Greenhaven — Build Adventurer v2 and villagers:\n" + result);
                File.WriteAllText("Temp/adventurer_build.txt", result);
            };
            if (File.Exists("Temp/villagers_preview.flag")) EditorApplication.delayCall += () =>
            {
                File.Delete("Temp/villagers_preview.flag");
                File.WriteAllText("Temp/villagers_preview.txt", RenderCast("Temp/villagers_preview.png"));
            };
            if (File.Exists(DevFlag)) EditorApplication.delayCall += () =>
            {
                string clip = File.ReadAllText(DevFlag).Trim();
                File.Delete(DevFlag);
                File.WriteAllText("Temp/adventurer_preview.txt", Render("Temp/adventurer_preview.png", string.IsNullOrEmpty(clip) ? "Idle_Loop" : clip, 0.4f));
            };
        }

        /// <summary>Hero v2 and every villager side by side (front and three-quarter), for checking they read as different people.</summary>
        [MenuItem("Greenhaven/Capture Villager Line-up", priority = 22)]
        static void CastMenu()
        {
            GeneratedAssetStore.EnsureFolders();
            string path = GeneratedAssetStore.Root + "/Preview/Villagers.png";
            string result = RenderCast(path);
            AssetDatabase.ImportAsset(path);
            EditorUtility.DisplayDialog("Villager line-up", result, "OK");
        }

        public static string RenderCast(string path)
        {
            string folder = AdventurerModelRig.SourceFolder();
            if (folder == null) return "Adventurer OBJ not found.";
            AnimationClip clip = FindClip("Idle_Loop");
            var cast = new List<(string name, AdventurerModelRig.Variant variant)> { ("Hero", null) };
            foreach (AdventurerModelRig.Variant v in AdventurerModelRig.Villagers) cast.Add((v.Name, v));
            var rows = new List<(Mesh mesh, Material material, Texture2D texture, float height)>();
            foreach ((string name, AdventurerModelRig.Variant variant) in cast)
            {
                HeroGenerator.Result r = AdventurerModelRig.BuildInMemory(folder, out Palette palette, out _, true, out _, variant);
                foreach (Transform t in r.Root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                if (clip != null) clip.SampleAnimation(r.Root, 0.4f);
                var baked = new Mesh();
                r.Renderer.BakeMesh(baked);
                Texture2D texture = palette.CreateTexture();
                var material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                material.SetTexture("_BaseMap", texture);
                material.mainTexture = texture;
                rows.Add((baked, material, texture, r.Mesh.bounds.max.y));
                Object.DestroyImmediate(r.Root);
                Object.DestroyImmediate(r.Mesh);
                Object.DestroyImmediate(r.Avatar);
            }
            float[] yaws = { 180f, 215f };
            var sheet = new Texture2D(TileW * rows.Count, TileH * yaws.Length, TextureFormat.RGB24, false);
            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.fieldOfView = 24f;
                preview.camera.nearClipPlane = 0.05f;
                preview.camera.farClipPlane = 50f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.66f, 0.82f, 0.97f);
                preview.lights[0].intensity = 1.25f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, 150f, 0f);
                preview.lights[1].intensity = 0.6f;
                preview.ambientColor = new Color(0.5f, 0.52f, 0.58f);
                float tallest = rows.Max(r => r.height);
                for (int m = 0; m < rows.Count; m++)
                    for (int c = 0; c < yaws.Length; c++)
                    {
                        preview.BeginStaticPreview(new Rect(0, 0, TileW, TileH));
                        Quaternion view = Quaternion.Euler(4f, yaws[c], 0f);
                        Vector3 focus = new Vector3(0f, tallest * 0.5f, 0f);
                        preview.camera.transform.SetPositionAndRotation(focus - view * Vector3.forward * (tallest * 2.6f), view);
                        preview.DrawMesh(rows[m].mesh, Matrix4x4.identity, rows[m].material, 0);
                        preview.camera.Render();
                        Texture2D tile = preview.EndStaticPreview();
                        for (int y = 0; y < TileH; y++)
                            for (int x = 0; x < TileW; x++)
                                sheet.SetPixel(m * TileW + x, (yaws.Length - 1 - c) * TileH + y, tile.GetPixelBilinear((x + 0.5f) / TileW, (y + 0.5f) / TileH));
                        Object.DestroyImmediate(tile);
                    }
            }
            finally
            {
                preview.Cleanup();
                foreach (var r in rows)
                {
                    Object.DestroyImmediate(r.mesh);
                    Object.DestroyImmediate(r.material);
                    Object.DestroyImmediate(r.texture);
                }
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return $"Saved {path}: {string.Join(", ", cast.Select(c => c.name))} (front, three-quarter).";
        }

        [MenuItem("Greenhaven/Capture Adventurer Preview", priority = 22)]
        static void Menu()
        {
            GeneratedAssetStore.EnsureFolders();
            string path = GeneratedAssetStore.Root + "/Preview/Adventurer_v1_v2.png";
            string result = Render(path, "Idle_Loop", 0.4f);
            AssetDatabase.ImportAsset(path);
            EditorUtility.DisplayDialog("Adventurer preview", result, "OK");
        }

        static AnimationClip FindClip(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets("AnimationLibrary_Unity_Standard"))
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (o is AnimationClip c && c.name == name) return c;
            return null;
        }

        public static string Render(string path, string clipName, float time)
        {
            string folder = AdventurerModelRig.SourceFolder();
            if (folder == null) return "Adventurer OBJ not found.";
            AnimationClip clip = FindClip(clipName);
            var rows = new List<(Mesh mesh, Material material, Texture2D texture, float height)>();
            var notes = new List<string>();
            foreach (bool v2 in new[] { false, true })
            {
                HeroGenerator.Result r = AdventurerModelRig.BuildInMemory(folder, out Palette palette, out List<string> n, v2, out _);
                notes.AddRange(n.Select(x => (v2 ? "v2: " : "v1: ") + x));
                foreach (Transform t in r.Root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                if (clip != null) clip.SampleAnimation(r.Root, time);
                var baked = new Mesh();
                r.Renderer.BakeMesh(baked);
                float height = r.Mesh.bounds.max.y;
                Texture2D texture = palette.CreateTexture();
                var material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                material.SetTexture("_BaseMap", texture);
                material.mainTexture = texture;
                rows.Add((baked, material, texture, height));
                Object.DestroyImmediate(r.Root);
                Object.DestroyImmediate(r.Mesh);
                Object.DestroyImmediate(r.Avatar);
            }

            float[] yaws = { 180f, 215f, 270f };
            var sheet = new Texture2D(TileW * yaws.Length * 2, TileH, TextureFormat.RGB24, false);
            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.fieldOfView = 24f;
                preview.camera.nearClipPlane = 0.05f;
                preview.camera.farClipPlane = 50f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.66f, 0.82f, 0.97f);
                preview.lights[0].intensity = 1.25f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, 150f, 0f);
                preview.lights[1].intensity = 0.6f;
                preview.ambientColor = new Color(0.5f, 0.52f, 0.58f);
                float tallest = rows.Max(r => r.height);
                for (int m = 0; m < rows.Count; m++)
                    for (int c = 0; c < yaws.Length; c++)
                    {
                        preview.BeginStaticPreview(new Rect(0, 0, TileW, TileH));
                        // Same camera for both versions, so their heights compare directly.
                        Quaternion view = Quaternion.Euler(4f, yaws[c], 0f);
                        Vector3 focus = new Vector3(0f, tallest * 0.5f, 0f);
                        preview.camera.transform.SetPositionAndRotation(focus - view * Vector3.forward * (tallest * 2.6f), view);
                        preview.DrawMesh(rows[m].mesh, Matrix4x4.identity, rows[m].material, 0);
                        preview.camera.Render();
                        Texture2D tile = preview.EndStaticPreview();
                        for (int y = 0; y < TileH; y++)
                            for (int x = 0; x < TileW; x++)
                                sheet.SetPixel((m * yaws.Length + c) * TileW + x, y, tile.GetPixelBilinear((x + 0.5f) / TileW, (y + 0.5f) / TileH));
                        Object.DestroyImmediate(tile);
                    }
            }
            finally
            {
                preview.Cleanup();
                foreach (var r in rows)
                {
                    Object.DestroyImmediate(r.mesh);
                    Object.DestroyImmediate(r.material);
                    Object.DestroyImmediate(r.texture);
                }
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return $"Saved {path}: v1 ({rows[0].height:0.00} m) then v2 ({rows[1].height:0.00} m), front / three-quarter / side, pose '{clipName}'{(clip == null ? " (clip not found: bind pose)" : "")}." +
                   (notes.Count > 0 ? "\n" + string.Join("\n", notes.Distinct()) : "");
        }
    }
}
