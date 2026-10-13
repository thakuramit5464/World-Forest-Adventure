using System;
using System.IO;
using System.Linq;
using Greenhaven.Editor.AssetGenerator;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.UI
{
    /// <summary>
    /// Draws the HUD's sprites in code (no external art): soft circle, ring, parchment panel with a wood
    /// border (9-sliced), key cap, and the jump / interact icons. Saved once to Generated/UI as PNGs with
    /// sprite import settings; rewritten only when the drawing changes.
    /// </summary>
    public static class HudSprites
    {
        public const string Folder = GeneratedAssetStore.Root + "/UI";

        public static readonly Color Parchment = Palette.Hex(0xF6ECD6);
        public static readonly Color ParchmentDark = Palette.Hex(0xE6D6B4);
        public static readonly Color Ink = Palette.Hex(0x2A1E16);
        public static readonly Color Wood = Palette.Hex(0x6B4426);
        public static readonly Color WoodLight = Palette.Hex(0xB07B4A);
        public static readonly Color Teal = Palette.Hex(0x2E7D68);
        public static readonly Color TealLight = Palette.Hex(0x7FC9A6);
        public static readonly Color Gold = Palette.Hex(0xD9A040);
        public static readonly Color Accent = Palette.Hex(0x8C3A2A);

        public sealed class Set
        {
            public Sprite Circle, Ring, Panel, KeyCap, IconJump, IconInteract;
        }

        public static Set Ensure()
        {
            GeneratedAssetStore.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(GeneratedAssetStore.Root, "UI");
            return new Set
            {
                Circle = Save("UI_Circle", 256, Circle, Vector4.zero),
                Ring = Save("UI_Ring", 256, Ring, Vector4.zero),
                Panel = Save("UI_Panel", 128, Panel, new Vector4(40f, 40f, 40f, 40f)),
                KeyCap = Save("UI_KeyCap", 96, KeyCap, new Vector4(30f, 30f, 30f, 30f)),
                IconJump = Save("UI_IconJump", 128, IconJump, Vector4.zero),
                IconInteract = Save("UI_IconInteract", 128, IconInteract, Vector4.zero),
            };
        }

        // ───────────────────────────── Drawing (signed distances, anti-aliased) ─────────────────────────────

        static float Fill(float d) => Mathf.Clamp01(0.5f - d); // 1 inside, soft 1-px edge

        static float Circle(float x, float y, Color[] c)
        {
            float d = new Vector2(x - 0.5f, y - 0.5f).magnitude - 0.47f;
            c[0] = Color.white;
            return Fill(d * 256f);
        }

        static float Ring(float x, float y, Color[] c)
        {
            float d = Mathf.Abs(new Vector2(x - 0.5f, y - 0.5f).magnitude - 0.42f) - 0.05f;
            c[0] = Color.white;
            return Fill(d * 256f);
        }

        static float RoundedRect(Vector2 p, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - Vector2.one * radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        static float Panel(float x, float y, Color[] c)
        {
            var p = new Vector2(x - 0.5f, y - 0.5f);
            float outer = RoundedRect(p, new Vector2(0.47f, 0.47f), 0.2f) * 128f;
            float inner = outer + 5f;
            float fill = Fill(outer);
            float borderMix = Fill(outer) - Fill(inner);
            c[0] = Color.Lerp(Parchment, Wood, Mathf.Clamp01(borderMix));
            return fill;
        }

        static float KeyCap(float x, float y, Color[] c)
        {
            var p = new Vector2(x - 0.5f, y - 0.5f);
            float outer = RoundedRect(p, new Vector2(0.46f, 0.46f), 0.16f) * 96f;
            float inner = outer + 4f;
            c[0] = Color.Lerp(Parchment, Ink, Mathf.Clamp01(Fill(outer) - Fill(inner)));
            return Fill(outer);
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static float IconJump(float x, float y, Color[] c)
        {
            // A thick upward chevron with a short landing line beneath.
            var p = new Vector2(x - 0.5f, y - 0.5f);
            float d = Mathf.Min(Segment(p, new Vector2(-0.3f, 0.02f), new Vector2(0f, 0.34f)), Segment(p, new Vector2(0.3f, 0.02f), new Vector2(0f, 0.34f))) - 0.075f;
            d = Mathf.Min(d, Segment(p, new Vector2(-0.22f, -0.3f), new Vector2(0.22f, -0.3f)) - 0.06f);
            c[0] = Color.white;
            return Fill(d * 128f);
        }

        static float IconInteract(float x, float y, Color[] c)
        {
            // Speech bubble: rounded body with a small tail bottom-left, and three dots cut out.
            var p = new Vector2(x - 0.5f, y - 0.5f);
            float body = RoundedRect(p - new Vector2(0f, 0.06f), new Vector2(0.36f, 0.25f), 0.14f);
            float tail = Segment(p, new Vector2(-0.16f, -0.16f), new Vector2(-0.3f, -0.34f)) - 0.07f;
            float d = Mathf.Min(body, tail);
            float dots = float.MaxValue;
            foreach (float dx in new[] { -0.17f, 0f, 0.17f }) dots = Mathf.Min(dots, Vector2.Distance(p, new Vector2(dx, 0.06f)) - 0.05f);
            c[0] = Color.white;
            return Fill(d * 128f) * (1f - Fill(dots * 128f));
        }

        // ───────────────────────────── Saving ─────────────────────────────

        delegate float Draw(float x, float y, Color[] colour);

        static Sprite Save(string name, int size, Draw draw, Vector4 border)
        {
            string path = $"{Folder}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var c = new Color[1];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = draw((x + 0.5f) / size, (y + 0.5f) / size, c);
                    pixels[x + y * size] = new Color(c[0].r, c[0].g, c[0].b, a);
                }
            tex.SetPixels(pixels);
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            string full = Path.GetFullPath(path);
            if (!File.Exists(full) || !File.ReadAllBytes(full).SequenceEqual(png))
            {
                File.WriteAllBytes(full, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = ti.textureType != TextureImporterType.Sprite || ti.spriteBorder != border || ti.mipmapEnabled || !ti.alphaIsTransparency ||
                             ti.textureCompression != TextureImporterCompression.Uncompressed || ti.filterMode != FilterMode.Bilinear;
                if (dirty)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.spriteBorder = border;
                    ti.spritePixelsPerUnit = 100f;
                    ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.filterMode = FilterMode.Bilinear;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.SaveAndReimport();
                }
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) AssetDatabase.SetLabels(sprite.texture, new[] { GeneratedAssetStore.Label });
            return sprite;
        }
    }
}
