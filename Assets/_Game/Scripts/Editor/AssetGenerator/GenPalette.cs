using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// A small point-filtered palette texture: one dark→light ramp per row. Each triangle's UV
    /// points at one cell, so colour variation needs no vertex colours or custom shader.
    /// </summary>
    public sealed class Palette
    {
        public const int Shades = 8;

        /// <summary>Used in asset names: T_Gen_{Name}.png / M_Gen_{Name}.mat.</summary>
        public readonly string Name;
        readonly (Color dark, Color light)[] ramps;

        public Palette(string name, params (Color dark, Color light)[] ramps)
        {
            Name = name;
            this.ramps = ramps;
        }

        public int Rows => ramps.Length;

        public Color ColorAt(int row, int shade)
        {
            (Color dark, Color light) = ramps[row];
            return Color.Lerp(dark, light, Mathf.Clamp(shade, 0, Shades - 1) / (Shades - 1f));
        }

        /// <summary>UV at the centre of the cell, so point filtering returns the exact colour.</summary>
        public Vector2 UV(int row, int shade) =>
            new Vector2((Mathf.Clamp(shade, 0, Shades - 1) + 0.5f) / Shades, (row + 0.5f) / Rows);

        public Texture2D CreateTexture()
        {
            var tex = new Texture2D(Shades, Rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int row = 0; row < Rows; row++)
                for (int shade = 0; shade < Shades; shade++)
                    tex.SetPixel(shade, row, ColorAt(row, shade));
            tex.Apply();
            return tex;
        }

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        /// <summary>A base shade near <paramref name="centre"/>, spread more with higher variation (0..1).</summary>
        public static int PickShade(System.Random rng, int centre, float variation)
        {
            int spread = Mathf.RoundToInt(variation * 2f);
            return Mathf.Clamp(centre + rng.Next(-spread, spread + 1), 0, Shades - 1);
        }

        /// <summary>Occasional ±1 shade per face, more often with higher variation (0..1).</summary>
        public static int FaceJitter(System.Random rng, float variation) =>
            rng.NextDouble() < variation * 0.5 ? (rng.Next(2) * 2 - 1) : 0;

        /// <summary>Simple stylised lighting cue: tops lighter, undersides darker.</summary>
        public static int SlopeShade(Vector3 normal) => normal.y > 0.5f ? 1 : normal.y < -0.4f ? -1 : 0;
    }

    /// <summary>Colour ramps in the nature palette (one row each).</summary>
    public enum Ramp
    {
        Bark,
        PineGreen,
        LeafGreen,
        RockGrey,
        RockWarm,
        FlowerPink,
        FlowerYellow,
        Petal,
    }

    /// <summary>
    /// The nature palette (trees, rocks, bushes, flowers, ground). Kept byte-for-byte stable:
    /// existing generated meshes' UVs depend on its 8×8 layout.
    /// </summary>
    public static class GenPalette
    {
        public const int Shades = Palette.Shades;
        public const int Rows = 8;

        public static readonly Palette Nature = new Palette("Palette",
            (Palette.Hex(0x3B2719), Palette.Hex(0x9A6A45)), // Bark
            (Palette.Hex(0x173A2A), Palette.Hex(0x6BAE62)), // PineGreen (cool, deep)
            (Palette.Hex(0x2B5A22), Palette.Hex(0xB3DC63)), // LeafGreen (warm, bright)
            (Palette.Hex(0x474B55), Palette.Hex(0xC4C8D0)), // RockGrey
            (Palette.Hex(0x564A40), Palette.Hex(0xCDBCA6)), // RockWarm
            (Palette.Hex(0xA8325F), Palette.Hex(0xFFB3CF)), // FlowerPink
            (Palette.Hex(0xC78A12), Palette.Hex(0xFFE88A)), // FlowerYellow
            (Palette.Hex(0xC9C3D6), Palette.Hex(0xFFFFFF))); // Petal

        public static Color ColorAt(Ramp ramp, int shade) => Nature.ColorAt((int)ramp, shade);
        public static Vector2 UV(Ramp ramp, int shade) => Nature.UV((int)ramp, shade);
        public static int PickShade(System.Random rng, int centre, float variation) => Palette.PickShade(rng, centre, variation);
        public static int FaceJitter(System.Random rng, float variation) => Palette.FaceJitter(rng, variation);
        public static Texture2D CreateTexture() => Nature.CreateTexture();
    }

    /// <summary>Colour ramps in the village/character palette.</summary>
    public enum VRamp
    {
        Skin,
        Hair,
        Tunic,      // teal-green cloth; doubles as planter foliage
        Trousers,
        Leather,
        Accent,     // scarf red, awning stripes, flowers
        Dark,       // eyes, iron, soles
        Wood,
        TimberDark,
        Plaster,
        RoofRed,
        RoofBlue,
        Stone,
        Window,     // warm lit glass, brass
        Water,
        PathDirt,
    }

    public static class VillagePalette
    {
        public static readonly Palette Palette = new Palette("VillagePalette",
            (Palette.Hex(0xB9785A), Palette.Hex(0xFFE0C8)), // Skin
            (Palette.Hex(0x3A2216), Palette.Hex(0xA8683C)), // Hair
            (Palette.Hex(0x1F5446), Palette.Hex(0x7FC9A6)), // Tunic
            (Palette.Hex(0x3E3324), Palette.Hex(0x9C8460)), // Trousers
            (Palette.Hex(0x3A2414), Palette.Hex(0xB07B4A)), // Leather
            (Palette.Hex(0x8C1F24), Palette.Hex(0xFF8A6E)), // Accent
            (Palette.Hex(0x16131A), Palette.Hex(0x5A5560)), // Dark
            (Palette.Hex(0x6B4426), Palette.Hex(0xD9A86A)), // Wood
            (Palette.Hex(0x2E1D14), Palette.Hex(0x7A543A)), // TimberDark
            (Palette.Hex(0xB8A88C), Palette.Hex(0xFFF6E2)), // Plaster
            (Palette.Hex(0x7A2A1E), Palette.Hex(0xE4704E)), // RoofRed
            (Palette.Hex(0x23395E), Palette.Hex(0x7FA6D6)), // RoofBlue
            (Palette.Hex(0x55524F), Palette.Hex(0xCFC8BC)), // Stone
            (Palette.Hex(0xB07A1C), Palette.Hex(0xFFE7A0)), // Window
            (Palette.Hex(0x1F5A86), Palette.Hex(0x8FD3F0)), // Water
            (Palette.Hex(0x6E5536), Palette.Hex(0xE1C99A))); // PathDirt

        public static Vector2 UV(VRamp ramp, int shade) => Palette.UV((int)ramp, shade);
    }
}
