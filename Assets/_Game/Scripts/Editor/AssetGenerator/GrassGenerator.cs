using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Grass clumps: 8–13 curved, tapered, double-sided blades leaning outwards, darker at the root and
    /// lighter at the tip. Wind bend grows with the square of height, so tips move most and roots stay
    /// put. Styles: Short, Tall, Wild. Walk-through: no collider. About 100–250 triangles.
    /// </summary>
    public sealed class GrassGenerator : IAssetGenerator
    {
        static readonly string[] Styles = { "Short", "Tall", "Wild" };

        public AssetCategory Category => AssetCategory.Grass;
        public string Prefix => "Grass";
        public string DisplayName => "Grass Clumps";
        public string Group => "Nature";
        public int DefaultCount => 3;
        public Palette Palette => GenPalette.Nature;
        public string StyleName(int variant) => Styles[variant % Styles.Length];

        const int Segments = 3;

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            int style = variant % Styles.Length;
            float v = s.ColorVariation;
            float maxHeight = s.Size * (style == 0 ? 0.35f : style == 1 ? 0.65f : 0.5f);
            int blades = rng.Next(style == 2 ? 10 : 8, style == 2 ? 14 : 12);
            float spread = s.Size * (style == 2 ? 0.2f : 0.12f);

            var mb = new MeshBuilder
            {
                // Tips bend most (squared falloff); slight flutter; grass sways a bit faster than trees.
                Wind = p => new Vector3(0.55f * Mathf.Pow(Mathf.Clamp01(p.y / maxHeight), 2f), 0.15f, 1.4f),
            };
            int baseShade = GenPalette.PickShade(rng, 2, v);

            for (int b = 0; b < blades; b++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 root = outward * Range(rng, 0f, spread);
                float height = maxHeight * Range(rng, style == 2 ? 0.5f : 0.7f, 1f);
                float width = s.Size * Range(rng, 0.03f, 0.045f);
                float lean = Range(rng, 0.15f, style == 2 ? 0.55f : 0.35f) * height;
                Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;

                Vector3 previousCentre = root;
                float previousHalf = width * 0.5f;
                for (int seg = 1; seg <= Segments; seg++)
                {
                    float t = seg / (float)Segments;
                    // Curve outwards more towards the tip.
                    Vector3 centre = root + Vector3.up * (height * t) + outward * (lean * t * t);
                    float half = width * 0.5f * (1f - t * 0.85f);
                    Vector3 a = previousCentre - side * previousHalf, b2 = previousCentre + side * previousHalf;
                    Vector3 c = centre - side * half, d = centre + side * half;
                    Vector3 normal = Vector3.Cross(side, (centre - previousCentre).normalized);
                    if (normal.y < 0f) normal = -normal;
                    int shade = baseShade + seg - 1 + GenPalette.FaceJitter(rng, v);
                    Vector2 front = GenPalette.UV(Ramp.LeafGreen, shade);
                    Vector2 back = GenPalette.UV(Ramp.LeafGreen, shade - 1);
                    if (seg < Segments)
                    {
                        mb.DoubleSided(a, b2, c, normal, front, back);
                        mb.DoubleSided(c, b2, d, normal, front, back);
                    }
                    else
                    {
                        mb.DoubleSided(a, b2, centre, normal, front, back); // pointed tip
                    }
                    previousCentre = centre;
                    previousHalf = half;
                }
            }

            return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = ColliderKind.None };
        }
    }
}
