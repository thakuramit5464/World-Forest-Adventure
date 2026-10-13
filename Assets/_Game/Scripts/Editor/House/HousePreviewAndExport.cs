using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Greenhaven.Editor.AssetGenerator;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.House
{
    /// <summary>
    /// Greenhaven ▸ House ▸ Capture House Preview: renders the house (built in memory) from the reference sheet's
    /// angles. Greenhaven ▸ House ▸ Export House to OBJ: writes Exports/House/RuralCourtyardHouse.obj + .mtl + palette
    /// PNG (one OBJ group per module and door, one material per category) for Blender or other tools.
    /// </summary>
    [InitializeOnLoad]
    public static class HousePreviewAndExport
    {
        const int TileW = 640, TileH = 400;
        const string DevFlag = "Temp/house_preview.flag";

        static HousePreviewAndExport()
        {
            if (File.Exists(DevFlag)) EditorApplication.delayCall += () =>
            {
                File.Delete(DevFlag);
                File.WriteAllText("Temp/house_preview.txt", Render("Temp/house_preview.png"));
            };
        }

        [MenuItem("Greenhaven/House/Capture House Preview", priority = 112)]
        static void PreviewMenu()
        {
            HouseAssets.EnsureFolders();
            string path = HouseAssets.Root + "/House_Preview.png";
            string result = Render(path);
            AssetDatabase.ImportAsset(path);
            EditorUtility.DisplayDialog("House preview", result, "OK");
        }

        static Dictionary<HouseMat, Material> InMemoryMaterials(out Texture2D texture)
        {
            texture = HousePalette.Palette.CreateTexture();
            var shared = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            shared.SetTexture("_BaseMap", texture);
            shared.mainTexture = texture;
            return System.Enum.GetValues(typeof(HouseMat)).Cast<HouseMat>().ToDictionary(m => m, _ => shared);
        }

        public static string Render(string path)
        {
            RuralCourtyardHouse.Layout layout = RuralCourtyardHouse.Build();
            Dictionary<HouseMat, Material> materials = InMemoryMaterials(out Texture2D texture);
            GameObject root = RuralCourtyardHouse.Instantiate(layout, materials, false);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var diag = new StringBuilder();
            foreach (MeshRenderer r in renderers)
            {
                Mesh dm = r.GetComponent<MeshFilter>().sharedMesh;
                diag.AppendLine($"{r.name}: {(dm == null ? "no mesh" : $"{dm.vertexCount} verts, {dm.subMeshCount} submeshes, bounds {dm.bounds}, format {dm.indexFormat}, mats {r.sharedMaterials.Length}")}");
            }

            // Front three-quarter (like the main view), straight front, back three-quarter, front-right.
            var views = new[] { (yaw: 22f, pitch: 24f, dist: 27f), (yaw: 0f, pitch: 10f, dist: 30f), (yaw: 200f, pitch: 18f, dist: 30f), (yaw: -40f, pitch: 18f, dist: 27f) };
            var sheet = new Texture2D(TileW * 2, TileH * 2, TextureFormat.RGB24, false);
            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.fieldOfView = 34f;
                preview.camera.nearClipPlane = 0.2f;
                preview.camera.farClipPlane = 200f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.32f, 0.33f, 0.36f);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(42f, 25f, 0f);
                preview.lights[1].intensity = 0.55f;
                preview.ambientColor = new Color(0.5f, 0.5f, 0.55f);
                var focus = new Vector3(0f, 1.6f, 3f);
                for (int v = 0; v < views.Length; v++)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, TileW, TileH));
                    Quaternion rot = Quaternion.Euler(views[v].pitch, views[v].yaw, 0f);
                    preview.camera.transform.SetPositionAndRotation(focus - rot * Vector3.forward * views[v].dist, rot);
                    foreach (MeshRenderer r in renderers)
                    {
                        Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                        for (int s = 0; s < mesh.subMeshCount; s++)
                            preview.DrawMesh(mesh, r.transform.localToWorldMatrix, r.sharedMaterials[Mathf.Min(s, r.sharedMaterials.Length - 1)], s);
                    }
                    preview.camera.Render();
                    Texture2D tile = preview.EndStaticPreview();
                    int ox = (v % 2) * TileW, oy = (1 - v / 2) * TileH;
                    for (int y = 0; y < TileH; y++)
                        for (int x = 0; x < TileW; x++)
                            sheet.SetPixel(ox + x, oy + y, tile.GetPixelBilinear((x + 0.5f) / TileW, (y + 0.5f) / TileH));
                    Object.DestroyImmediate(tile);
                }
            }
            finally
            {
                preview.Cleanup();
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>()) Object.DestroyImmediate(mf.sharedMesh);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(materials[HouseMat.Paint]);
                Object.DestroyImmediate(texture);
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return $"Saved {path}: {layout.Modules.Count} modules, {layout.Fixtures.Count} fixtures, {layout.Triangles} triangles.\n" + diag;
        }

        // ───────────────────────────── OBJ export ─────────────────────────────

        [MenuItem("Greenhaven/House/Export House to OBJ", priority = 113)]
        static void ExportMenu()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuralCourtyardHouse.PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Export house", "Generate the house first (Greenhaven ▸ House ▸ Generate Rural Courtyard House).", "OK");
                return;
            }
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Exports", "House");
            Directory.CreateDirectory(folder);
            string result = Export(prefab, folder);
            Debug.Log("Greenhaven — Export house:\n" + result);
            EditorUtility.DisplayDialog("Export house", result, "OK");
            EditorUtility.RevealInFinder(folder);
        }

        /// <summary>Writes the prefab's meshes as one OBJ (a group per object, faces per material), its MTL and the palette PNG.</summary>
        public static string Export(GameObject prefab, string folder)
        {
            string name = RuralCourtyardHouse.Name;
            var inv = CultureInfo.InvariantCulture;
            var obj = new StringBuilder();
            obj.AppendLine("# Trails of Greenhaven — rural courtyard house (generated). Units: metres, Y up.");
            obj.AppendLine($"mtllib {name}.mtl");
            int vertexOffset = 1;
            var usedMaterials = new HashSet<Material>();
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh;
                var renderer = mf.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null) continue;
                Matrix4x4 m = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                obj.AppendLine($"g {mf.gameObject.name}");
                Vector3[] v = mesh.vertices, n = mesh.normals;
                Vector2[] uv = mesh.uv;
                // Unity is left-handed; OBJ tools expect right-handed, so mirror X and flip the winding.
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(v[i]);
                    obj.AppendLine(string.Format(inv, "v {0:0.####} {1:0.####} {2:0.####}", -p.x, p.y, p.z));
                }
                for (int i = 0; i < n.Length; i++)
                {
                    Vector3 q = m.MultiplyVector(n[i]).normalized;
                    obj.AppendLine(string.Format(inv, "vn {0:0.####} {1:0.####} {2:0.####}", -q.x, q.y, q.z));
                }
                for (int i = 0; i < uv.Length; i++) obj.AppendLine(string.Format(inv, "vt {0:0.#####} {1:0.#####}", uv[i].x, uv[i].y));
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    Material mat = renderer.sharedMaterials[Mathf.Min(s, renderer.sharedMaterials.Length - 1)];
                    usedMaterials.Add(mat);
                    obj.AppendLine($"usemtl {mat.name}");
                    int[] t = mesh.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        int a = t[i] + vertexOffset, b = t[i + 2] + vertexOffset, c = t[i + 1] + vertexOffset;
                        obj.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
                    }
                }
                vertexOffset += v.Length;
            }
            File.WriteAllText(Path.Combine(folder, name + ".obj"), obj.ToString());

            string textureFile = "T_HousePalette.png";
            Texture2D palette = HousePalette.Palette.CreateTexture();
            File.WriteAllBytes(Path.Combine(folder, textureFile), palette.EncodeToPNG());
            Object.DestroyImmediate(palette);
            var mtl = new StringBuilder();
            foreach (Material mat in usedMaterials)
            {
                mtl.AppendLine($"newmtl {mat.name}");
                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                mtl.AppendLine(string.Format(inv, "Kd {0:0.###} {1:0.###} {2:0.###}", c.r, c.g, c.b));
                mtl.AppendLine($"map_Kd {textureFile}");
                mtl.AppendLine();
            }
            File.WriteAllText(Path.Combine(folder, name + ".mtl"), mtl.ToString());
            return $"Exported {name}.obj, {name}.mtl and {textureFile} to {folder}. Import into Blender with File ▸ Import ▸ Wavefront (.obj); use Closest interpolation on the palette texture to keep the flat colours.";
        }
    }
}
