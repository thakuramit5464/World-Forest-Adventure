using System.Collections.Generic;
using System.IO;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenhaven.Editor.River
{
    /// <summary>
    /// Renders a test river (two bends, slow and fast segments, rocks) in memory: Standard quality at two
    /// moments half a second apart, plus Low quality, and reports how much the water changed between the
    /// two frames. Greenhaven ▸ Environment ▸ Capture River Preview saves it under Generated/River; touches no scene.
    /// </summary>
    [InitializeOnLoad]
    public static class RiverPreview
    {
        const int Tile = 420;
        const string DevFlag = "Temp/river_preview.flag";

        static RiverPreview()
        {
            if (File.Exists(DevFlag)) EditorApplication.delayCall += () =>
            {
                File.Delete(DevFlag);
                File.WriteAllText("Temp/river_preview.txt", Render("Temp/river_preview.png"));
            };
        }

        [MenuItem("Greenhaven/Environment/Capture River Preview", priority = 103)]
        static void Menu()
        {
            string path = RiverTools.Root + "/River_Preview.png";
            RiverTools.EnsureMaterials(out _, out _, out _);
            string result = Render(path);
            AssetDatabase.ImportAsset(path);
            EditorUtility.DisplayDialog("River preview", result, "OK");
        }

        public static string Render(string path)
        {
            // A temporary, hidden controller just to build the mesh (never saved, never in the scene).
            var temp = new GameObject("~RiverPreview") { hideFlags = HideFlags.HideAndDontSave };
            RiverFlowController river = temp.AddComponent<RiverFlowController>();
            river.controlPoints = new List<Vector3> { new Vector3(-9f, 0f, 7f), new Vector3(-4f, 0f, 4f), new Vector3(-2f, 0f, -1f), new Vector3(3f, 0f, -3f), new Vector3(9f, 0f, -7f) };
            river.width = 4.2f;
            river.depth = 0.6f;
            river.segments = new List<RiverSegment> { new RiverSegment { fromPoint = 0, speed = 0.6f }, new RiverSegment { fromPoint = 3, speed = 1.7f } };
            river.rocks = new List<Vector3> { new Vector3(-3.3f, 0f, 1.6f), new Vector3(4.5f, 0f, -4.2f) };
            var standard = new Material(Shader.Find("Greenhaven/RiverWater"));
            var low = new Material(standard);
            low.SetFloat("_LowQuality", 1f);
            low.EnableKeyword("_RIVER_LOW");
            low.SetFloat("_SrcBlend", (float)BlendMode.One);
            low.SetFloat("_DstBlend", (float)BlendMode.Zero);
            low.SetFloat("_ZWrite", 1f);
            river.standardMaterial = standard;
            Mesh water = RiverTools.BuildMesh(river, out _, out _, out _);
            Object.DestroyImmediate(temp);

            // River bed and banks underneath, so transparency reads correctly.
            var bedBuilder = new MeshBuilder();
            for (int i = -6; i < 6; i++)
                for (int j = -6; j < 6; j++)
                {
                    Vector3 a = new Vector3(i * 2f, -0.45f, j * 2f), b = a + new Vector3(2f, 0f, 0f), c = a + new Vector3(0f, 0f, 2f), d = a + new Vector3(2f, 0f, 2f);
                    Vector2 uv = GenPalette.UV((i + j) % 3 == 0 ? Ramp.RockWarm : Ramp.LeafGreen, 3);
                    bedBuilder.Triangle(a, c, b, Vector3.up, uv);
                    bedBuilder.Triangle(b, c, d, Vector3.up, uv);
                }
            Mesh bed = bedBuilder.ToMesh("PreviewBed");
            Material nature = GeneratedAssetStore.EnsurePaletteMaterial(GenPalette.Nature);

            var frames = new List<Texture2D>();
            var preview = new PreviewRenderUtility();
            string report;
            try
            {
                preview.camera.fieldOfView = 40f;
                preview.camera.nearClipPlane = 0.1f;
                preview.camera.farClipPlane = 100f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.72f, 0.85f, 0.96f);
                preview.lights[0].intensity = 1.25f;
                preview.lights[0].transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                preview.lights[1].intensity = 0.5f;
                preview.ambientColor = new Color(0.5f, 0.55f, 0.62f);
                // Close views (Standard at t and t + 0.5 s, Low), then Standard from a third-person gameplay height.
                foreach ((Material m, float offset, bool far) in new[] { (standard, 0f, false), (standard, 0.5f, false), (low, 0f, false), (standard, 0f, true) })
                {
                    m.SetFloat("_TimeOffset", offset);
                    preview.BeginStaticPreview(new Rect(0, 0, Tile, Tile));
                    if (far) preview.camera.transform.SetPositionAndRotation(new Vector3(-6f, 3.2f, -9f), Quaternion.Euler(16f, 35f, 0f));
                    else preview.camera.transform.SetPositionAndRotation(new Vector3(0f, 13f, -11f), Quaternion.Euler(50f, 0f, 0f));
                    preview.DrawMesh(bed, Matrix4x4.identity, nature, 0);
                    preview.DrawMesh(water, Matrix4x4.identity, m, 0);
                    preview.camera.Render();
                    frames.Add(preview.EndStaticPreview());
                }
                // How much the picture changed between the two Standard frames (0 = frozen water).
                float diff = 0f;
                int samples = 0;
                for (int y = 0; y < Tile; y += 3)
                    for (int x = 0; x < Tile; x += 3)
                    {
                        Color a = frames[0].GetPixelBilinear(x / (float)Tile, y / (float)Tile), b = frames[1].GetPixelBilinear(x / (float)Tile, y / (float)Tile);
                        diff += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                        samples++;
                    }
                report = $"Saved {path}: Standard at t and t + 0.5 s, Low, and a gameplay-height view. Mean colour change between the two Standard frames: {diff / samples * 255f / 3f:0.00} (0 means frozen).";
            }
            finally
            {
                preview.Cleanup();
            }

            var sheet = new Texture2D(Tile * frames.Count, Tile, TextureFormat.RGB24, false);
            for (int f = 0; f < frames.Count; f++)
            {
                for (int y = 0; y < Tile; y++)
                    for (int x = 0; x < Tile; x++)
                        sheet.SetPixel(f * Tile + x, y, frames[f].GetPixelBilinear((x + 0.5f) / Tile, (y + 0.5f) / Tile));
                Object.DestroyImmediate(frames[f]);
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(water);
            Object.DestroyImmediate(bed);
            Object.DestroyImmediate(standard);
            Object.DestroyImmediate(low);
            return report;
        }
    }
}
