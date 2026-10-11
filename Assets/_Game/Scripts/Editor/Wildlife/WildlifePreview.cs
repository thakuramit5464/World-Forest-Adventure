using System.Collections.Generic;
using System.IO;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Wildlife;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.Wildlife
{
    /// <summary>
    /// Renders the animals (from the current settings, in memory) to a contact sheet: each species from
    /// the side, three-quarter front and front. Greenhaven ▸ Wildlife ▸ Capture Previews saves it under
    /// Generated/Wildlife/Preview; touches no scene.
    /// </summary>
    [InitializeOnLoad]
    public static class WildlifePreview
    {
        const int Tile = 300;
        const string DevFlag = "Temp/wildlife_preview.flag";

        // Development aid: if the flag file exists after a compile, render to Temp (no assets are written).
        static WildlifePreview()
        {
            if (File.Exists(DevFlag)) EditorApplication.delayCall += () =>
            {
                File.Delete(DevFlag);
                Render(WildlifeGeneratorSettings.LoadOrCreate(createAsset: false), "Temp/wildlife_preview.png", inMemoryMaterial: true);
            };
        }

        [MenuItem("Greenhaven/Wildlife/Capture Previews", priority = 73)]
        static void CaptureMenu()
        {
            WildlifeGenerator.EnsureFolders();
            string path = WildlifeGenerator.PreviewFolder + "/Wildlife_Previews.png";
            string result = Render(WildlifeGeneratorSettings.LoadOrCreate(), path, inMemoryMaterial: false);
            AssetDatabase.ImportAsset(path);
            EditorUtility.DisplayDialog("Wildlife previews", result, "OK");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static string Render(WildlifeGeneratorSettings s, string path, bool inMemoryMaterial)
        {
            Palette palette = s.ToPalette();
            Material material;
            Texture2D paletteTexture = null;
            if (inMemoryMaterial)
            {
                paletteTexture = palette.CreateTexture();
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap", paletteTexture);
                material.mainTexture = paletteTexture;
            }
            else material = WildlifeGenerator.EnsureMaterial(s);

            var rows = new List<(Mesh mesh, float height)>();
            foreach (AnimalKind kind in WildlifeGenerator.Kinds)
            {
                Mesh mesh = AnimalModels.Build(kind, 0, s, palette).ToMesh($"Preview_{kind}");
                rows.Add((mesh, mesh.bounds.size.magnitude));
            }

            float[] yaws = { 90f, 35f, 180f };
            var sheet = new Texture2D(Tile * yaws.Length, Tile * rows.Count, TextureFormat.RGB24, false);
            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.fieldOfView = 26f;
                preview.camera.nearClipPlane = 0.02f;
                preview.camera.farClipPlane = 50f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.78f, 0.87f, 0.95f);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, -35f, 0f);
                preview.lights[1].intensity = 0.7f;
                preview.ambientColor = new Color(0.45f, 0.48f, 0.55f);
                for (int r = 0; r < rows.Count; r++)
                {
                    Mesh mesh = rows[r].mesh;
                    Bounds b = mesh.bounds;
                    float distance = b.size.magnitude * 1.9f + 0.2f;
                    for (int c = 0; c < yaws.Length; c++)
                    {
                        preview.BeginStaticPreview(new Rect(0, 0, Tile, Tile));
                        Quaternion view = Quaternion.Euler(12f, yaws[c], 0f);
                        preview.camera.transform.position = b.center + view * (Vector3.back * distance);
                        preview.camera.transform.rotation = view;
                        preview.DrawMesh(mesh, Matrix4x4.identity, material, 0);
                        preview.camera.Render();
                        Texture2D tile = preview.EndStaticPreview();
                        Copy(tile, sheet, c * Tile, (rows.Count - 1 - r) * Tile);
                        Object.DestroyImmediate(tile);
                    }
                }
            }
            finally
            {
                preview.Cleanup();
                foreach ((Mesh mesh, _) in rows) Object.DestroyImmediate(mesh);
                if (inMemoryMaterial)
                {
                    Object.DestroyImmediate(material);
                    Object.DestroyImmediate(paletteTexture);
                }
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return $"Saved {path}: fox, deer, rabbit, goat and bird (variant 0) from the side, three-quarter front and front.";
        }

        static void Copy(Texture2D tile, Texture2D sheet, int x, int y)
        {
            // EndStaticPreview may return a different size on high-DPI displays; sample to fit.
            for (int py = 0; py < Tile; py++)
                for (int px = 0; px < Tile; px++)
                    sheet.SetPixel(x + px, y + py, tile.GetPixelBilinear((px + 0.5f) / Tile, (py + 0.5f) / Tile));
        }
    }
}
