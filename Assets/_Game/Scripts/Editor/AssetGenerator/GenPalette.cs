using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>Colour ramps in the shared palette texture (one row each).</summary>
    public enum Ramp
    {
        Bark,
        PineGreen,
        LeafGreen,
        RockGrey,
        RockWarm,
        FlowerPink,   // reserved for stage 2 (bushes and flowers)
        FlowerYellow, // reserved for stage 2
        Petal,        // reserved for stage 2
    }

    /// <summary>
    /// The 8×8 palette every generated asset samples. Each row is a dark→light ramp; each
    /// triangle's UV points at one cell, so colour variation needs no vertex colours or custom shader.
    /// </summary>
    public static class GenPalette
    {
        public const int Shades = 8;
        public const int Rows = 8;

        static readonly (Color dark, Color light)[] RampEnds =
        {
            (Hex(0x3B2719), Hex(0x9A6A45)), // Bark
            (Hex(0x173A2A), Hex(0x6BAE62)), // PineGreen (cool, deep)
            (Hex(0x2B5A22), Hex(0xB3DC63)), // LeafGreen (warm, bright)
            (Hex(0x474B55), Hex(0xC4C8D0)), // RockGrey
            (Hex(0x564A40), Hex(0xCDBCA6)), // RockWarm
            (Hex(0xA8325F), Hex(0xFFB3CF)), // FlowerPink
            (Hex(0xC78A12), Hex(0xFFE88A)), // FlowerYellow
            (Hex(0xC9C3D6), Hex(0xFFFFFF)), // Petal
        };

        public static Color ColorAt(Ramp ramp, int shade)
        {
            (Color dark, Color light) = RampEnds[(int)ramp];
            return Color.Lerp(dark, light, Mathf.Clamp(shade, 0, Shades - 1) / (Shades - 1f));
        }

        /// <summary>UV at the centre of the palette cell, so point filtering returns the exact colour.</summary>
        public static Vector2 UV(Ramp ramp, int shade) =>
            new Vector2((Mathf.Clamp(shade, 0, Shades - 1) + 0.5f) / Shades, ((int)ramp + 0.5f) / Rows);

        /// <summary>A base shade near <paramref name="centre"/>, spread more with higher variation (0..1).</summary>
        public static int PickShade(System.Random rng, int centre, float variation)
        {
            int spread = Mathf.RoundToInt(variation * 2f);
            return Mathf.Clamp(centre + rng.Next(-spread, spread + 1), 0, Shades - 1);
        }

        /// <summary>Occasional ±1 shade per face, more often with higher variation (0..1).</summary>
        public static int FaceJitter(System.Random rng, float variation) =>
            rng.NextDouble() < variation * 0.5 ? (rng.Next(2) * 2 - 1) : 0;

        public static Texture2D CreateTexture()
        {
            var tex = new Texture2D(Shades, Rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int row = 0; row < Rows; row++)
                for (int shade = 0; shade < Shades; shade++)
                    tex.SetPixel(shade, row, ColorAt((Ramp)row, shade));
            tex.Apply();
            return tex;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
