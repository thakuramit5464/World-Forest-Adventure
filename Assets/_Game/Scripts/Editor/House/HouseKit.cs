using System;
using System.Collections.Generic;
using System.Linq;
using Greenhaven.Editor.AssetGenerator;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.House
{
    /// <summary>Material categories: each is its own material asset (tint them independently), all sampling one palette.</summary>
    public enum HouseMat { Paint, Concrete, Paving, Metal, Wood, Interior, Water }

    /// <summary>Palette rows.</summary>
    public enum HRow { Terracotta, Cream, Teal, Concrete, Paving, Metal, Wood, Stain, Green, Curtain, Water, Brick, Fabric, Grout }

    public static class HousePalette
    {
        public static readonly Palette Palette = new Palette("HousePalette",
            Ramp(0xD0704A), // Terracotta
            Ramp(0xEEDDB6), // Cream (warm)
            Ramp(0x9ED2B2), // Teal (weathered pale mint)
            Ramp(0xC2B394), // Concrete (warm)
            Ramp(0xD3BF97), // Paving (warm sandstone)
            Ramp(0x6E211C), // Metal (dark reddish-brown gates and grilles)
            Ramp(0x5C3421), // Wood (doors)
            Ramp(0x5D5850), // Stain (grime near the ground)
            Ramp(0x86C68E), // Green (interior paint)
            Ramp(0xE3CBAE), // Curtain
            Ramp(0x274A50), // Water
            Ramp(0x7C4A31), // Brick (lattice panel)
            Ramp(0xA8463C), // Fabric (cot)
            Ramp(0x9A8462)); // Grout

        /// <summary>Shade 4 of 0..7 is exactly the colour.</summary>
        static (Color, Color) Ramp(int hex)
        {
            Color c = Palette.Hex(hex);
            Color dark = c * 0.55f, light = c + (c - dark) * 0.75f;
            light = new Color(Mathf.Clamp01(light.r), Mathf.Clamp01(light.g), Mathf.Clamp01(light.b), 1f);
            dark.a = 1f;
            return (dark, light);
        }

        public static Vector2 UV(HRow row, int shade) => Palette.UV((int)row, Mathf.Clamp(shade, 0, 7));
    }

    /// <summary>
    /// Painted plaster with weathering: a grimy band at the foot of the wall, mottled patches above it,
    /// and occasional lighter or darker patches higher up. Deterministic (from position, not randomness).
    /// </summary>
    public readonly struct Paint
    {
        public readonly HRow Row;
        public readonly float StainHeight;

        public Paint(HRow row, float stainHeight)
        {
            Row = row;
            StainHeight = stainHeight;
        }

        public Vector2 UV(Vector3 p, Vector3 n)
        {
            float noise = Mathf.PerlinNoise(p.x * 0.9f + p.z * 0.7f + 31.7f, p.y * 1.3f + 7.1f);
            float fine = Mathf.PerlinNoise(p.x * 3.1f + p.z * 2.3f + 3.3f, p.y * 2.9f + 11.9f);
            int slope = n.y > 0.5f ? 1 : n.y < -0.5f ? -1 : 0;
            if (p.y < StainHeight * (0.55f + 0.6f * noise)) return HousePalette.UV(HRow.Stain, 3 + (fine > 0.6f ? 1 : 0));
            if (p.y < StainHeight * 1.5f && fine > 0.68f) return HousePalette.UV(Row, 3);
            return HousePalette.UV(Row, 4 + slope + (noise > 0.78f ? 1 : noise < 0.18f ? -1 : 0));
        }
    }

    /// <summary>A face style: material and colour for a face (given its centre and normal).</summary>
    public readonly struct FaceStyle
    {
        public readonly HouseMat Mat;
        public readonly Func<Vector3, Vector3, Vector2> Uv;
        public readonly float Cell; // > 0: subdivide into cells this size (for weathering detail)

        public FaceStyle(HouseMat mat, Func<Vector3, Vector3, Vector2> uv, float cell = 0f)
        {
            Mat = mat;
            Uv = uv;
            Cell = cell;
        }

        public static FaceStyle Of(Paint paint) => new FaceStyle(HouseMat.Paint, paint.UV, 0.6f);
        public static FaceStyle Flat(HouseMat mat, HRow row, int shade) => new FaceStyle(mat, (p, n) => HousePalette.UV(row, shade + (n.y > 0.5f ? 1 : n.y < -0.5f ? -1 : 0)));
    }

    /// <summary>An opening in a wall, in wall coordinates: u along the wall from its start, v up from the ground.</summary>
    public struct Opening
    {
        public float U0, U1, V0, V1;
        public Opening(float u0, float u1, float v0, float v1)
        {
            U0 = u0; U1 = u1; V0 = v0; V1 = v1;
        }
    }

    /// <summary>One editable module (wall section, staircase, courtyard…): geometry per material, plus box colliders.</summary>
    public sealed class HousePart
    {
        readonly Dictionary<HouseMat, MeshBuilder> builders = new Dictionary<HouseMat, MeshBuilder>();
        public readonly List<Bounds> Colliders = new List<Bounds>();

        public MeshBuilder this[HouseMat mat]
        {
            get
            {
                if (!builders.TryGetValue(mat, out MeshBuilder b)) builders[mat] = b = new MeshBuilder();
                return b;
            }
        }

        public int Triangles => builders.Values.Sum(b => b.TriangleCount);

        /// <summary>One mesh with a submesh per material used (in a fixed order).</summary>
        public Mesh ToMesh(string name, out HouseMat[] materials)
        {
            var used = Enum.GetValues(typeof(HouseMat)).Cast<HouseMat>().Where(m => builders.ContainsKey(m) && builders[m].TriangleCount > 0).ToArray();
            materials = used;
            var parts = used.Select(m => builders[m].ToMesh(name + "_" + m)).ToArray();
            var combine = parts.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray();
            int vertices = parts.Sum(m => m.vertexCount);
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertices > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.CombineMeshes(combine, false, false);
            mesh.RecalculateBounds();
            foreach (Mesh m in parts) Object.DestroyImmediate(m);
            return mesh;
        }

        // ───────────────────────────── Geometry ─────────────────────────────

        /// <summary>Quad (a, b, c, d counter-clockwise seen from outside), optionally subdivided into cells for weathering detail.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, FaceStyle style)
        {
            MeshBuilder mb = this[style.Mat];
            float lu = Vector3.Distance(a, b), lv = Vector3.Distance(a, d);
            int nu = style.Cell > 0f ? Mathf.Max(1, Mathf.RoundToInt(lu / style.Cell)) : 1;
            int nv = style.Cell > 0f ? Mathf.Max(1, Mathf.RoundToInt(lv / (style.Cell * 0.8f))) : 1;
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    Vector3 P(float u, float v) => Vector3.Lerp(Vector3.Lerp(a, b, u), Vector3.Lerp(d, c, u), v);
                    Vector3 p00 = P(i / (float)nu, j / (float)nv), p10 = P((i + 1f) / nu, j / (float)nv);
                    Vector3 p11 = P((i + 1f) / nu, (j + 1f) / nv), p01 = P(i / (float)nu, (j + 1f) / nv);
                    Vector2 uv = style.Uv((p00 + p11) * 0.5f, outward.normalized);
                    mb.Triangle(p00, p10, p11, outward, uv);
                    mb.Triangle(p00, p11, p01, outward, uv);
                }
        }

        /// <summary>
        /// Box in a frame: <paramref name="origin"/> + u·<paramref name="along"/> + v·up + w·<paramref name="out"/>, with
        /// separate styles for the outside face (+w), the inside face (−w) and the rest. Optional collider.
        /// </summary>
        public void Box(Vector3 origin, Vector3 along, Vector3 outDir, Vector3 min, Vector3 max, FaceStyle front, FaceStyle back, FaceStyle sides, bool collide = true)
        {
            Vector3 up = Vector3.up;
            Vector3 W(float u, float v, float w) => origin + along * u + up * v + outDir * w;
            Vector3 c000 = W(min.x, min.y, min.z), c100 = W(max.x, min.y, min.z), c110 = W(max.x, max.y, min.z), c010 = W(min.x, max.y, min.z);
            Vector3 c001 = W(min.x, min.y, max.z), c101 = W(max.x, min.y, max.z), c111 = W(max.x, max.y, max.z), c011 = W(min.x, max.y, max.z);
            Quad(c001, c101, c111, c011, outDir, front);    // outside
            Quad(c100, c000, c010, c110, -outDir, back);    // inside
            Quad(c010, c011, c111, c110, up, sides);        // top
            Quad(c000, c100, c101, c001, -up, sides);       // bottom
            Quad(c000, c001, c011, c010, -along, sides);    // start end
            Quad(c101, c100, c110, c111, along, sides);     // far end
            if (collide)
            {
                var b = new Bounds(c000, Vector3.zero);
                foreach (Vector3 p in new[] { c100, c110, c010, c001, c101, c111, c011 }) b.Encapsulate(p);
                Colliders.Add(b);
            }
        }

        /// <summary>Axis-aligned box in house space with one style on every face.</summary>
        public void Block(Vector3 centre, Vector3 size, FaceStyle style, bool collide = true) =>
            Box(centre - size * 0.5f, Vector3.right, Vector3.forward, Vector3.zero, size, style, style, style, collide);

        /// <summary>
        /// A straight wall from <paramref name="start"/> to <paramref name="end"/> (on the ground, axis-aligned), thickness
        /// <paramref name="thickness"/> centred on the line, height <paramref name="height"/>, with rectangular openings.
        /// <paramref name="outDir"/> picks which side gets <paramref name="outside"/>. Solid pieces get colliders; openings stay clear.
        /// </summary>
        public void Wall(Vector3 start, Vector3 end, Vector3 outDir, float thickness, float height, FaceStyle outside, FaceStyle inside, FaceStyle edges,
            params Opening[] openings)
        {
            Vector3 along = (end - start);
            float length = along.magnitude;
            along /= length;
            float h = thickness * 0.5f;
            var cuts = openings.OrderBy(o => o.U0).ToList();
            float u = 0f;
            void Piece(float u0, float u1, float v0, float v1)
            {
                if (u1 - u0 < 0.005f || v1 - v0 < 0.005f) return;
                Box(start, along, outDir, new Vector3(u0, v0, -h), new Vector3(u1, v1, h), outside, inside, edges);
            }
            foreach (Opening o in cuts)
            {
                Piece(u, o.U0, 0f, height);
                Piece(o.U0, o.U1, 0f, o.V0);      // below (sill)
                Piece(o.U0, o.U1, o.V1, height);  // above (lintel)
                u = o.U1;
            }
            Piece(u, length, 0f, height);
        }
    }

    /// <summary>Materials and mesh saving for the house (Generated/House).</summary>
    public static class HouseAssets
    {
        public const string Root = GeneratedAssetStore.Root + "/House";
        public const string MeshFolder = Root + "/Meshes", MaterialFolder = Root + "/Materials", PrefabFolder = Root + "/Prefabs";

        public static void EnsureFolders()
        {
            GeneratedAssetStore.EnsureFolders();
            foreach (string folder in new[] { Root, MeshFolder, MaterialFolder, PrefabFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                int slash = folder.LastIndexOf('/');
                AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            }
        }

        /// <summary>One material per category (created once; Inspector tweaks such as tints are kept), all on the shared palette texture.</summary>
        public static Dictionary<HouseMat, Material> EnsureMaterials()
        {
            EnsureFolders();
            Material palette = GeneratedAssetStore.EnsurePaletteMaterial(HousePalette.Palette); // writes the shared palette texture
            Texture texture = palette.GetTexture("_BaseMap");
            var result = new Dictionary<HouseMat, Material>();
            foreach (HouseMat mat in Enum.GetValues(typeof(HouseMat)))
            {
                string path = $"{MaterialFolder}/M_House_{mat}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(palette) { name = $"M_House_{mat}" };
                    if (mat == HouseMat.Water && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
                    AssetDatabase.CreateAsset(m, path);
                    AssetDatabase.SetLabels(m, new[] { GeneratedAssetStore.Label });
                }
                if (m.GetTexture("_BaseMap") != texture)
                {
                    m.SetTexture("_BaseMap", texture);
                    m.mainTexture = texture;
                    EditorUtility.SetDirty(m);
                }
                result[mat] = m;
            }
            return result;
        }
    }
}
