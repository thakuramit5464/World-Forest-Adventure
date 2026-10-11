using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Clustered faceted shrubs with flat bottoms that sit on the ground. Styles: Round, Wide,
    /// Flowering (with small blossoms). Walk-through: no collider. About 250–450 triangles.
    /// </summary>
    public sealed class BushGenerator : IAssetGenerator
    {
        static readonly string[] Styles = { "Round", "Wide", "Flowering" };

        public AssetCategory Category => AssetCategory.Bush;
        public string Prefix => "Bush";
        public string DisplayName => "Bushes";
        public string Group => "Nature";
        public int DefaultCount => 3;
        public Palette Palette => GenPalette.Nature;
        public string StyleName(int variant) => Styles[variant % Styles.Length];

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            int style = variant % Styles.Length;
            float v = s.ColorVariation;
            float size = s.Size * Range(rng, 0.85f, 1.15f);
            var mb = new MeshBuilder();

            int leaf = GenPalette.PickShade(rng, 3, v);
            System.Func<Vector3, Vector2> leafUv = n =>
                GenPalette.UV(Ramp.LeafGreen, leaf + (n.y > 0.45f ? 2 : n.y > 0f ? 1 : -1) + GenPalette.FaceJitter(rng, v));

            int blobs = style == 1 ? 5 : 4;
            float spread = style == 1 ? 0.75f : 0.4f;
            Vector3 squash = style == 1 ? new Vector3(1.15f, 0.75f, 1.15f) : new Vector3(1f, 0.85f, 1f);
            var tops = new Vector3[blobs];
            for (int i = 0; i < blobs; i++)
            {
                float angle = i * Mathf.PI * 2f / blobs + Range(rng, -0.4f, 0.4f);
                float r = (i == 0 ? 0.62f : Range(rng, 0.42f, 0.55f)) * size;
                var centre = i == 0
                    ? new Vector3(0f, r * 0.75f, 0f)
                    : new Vector3(Mathf.Cos(angle) * spread * size, r * 0.6f, Mathf.Sin(angle) * spread * size);
                // Flatten anything below the ground so the bush sits cleanly.
                float floor = -centre.y;
                Shapes.Blob(mb, rng, centre, r, squash, 0.15f, 1, leafUv, p => p.y < floor ? new Vector3(p.x, floor, p.z) : p);
                tops[i] = centre + Vector3.up * r * squash.y * 0.85f;
            }

            if (style == 2)
            {
                // Blossoms dotted over the upper surface.
                Ramp blossom = rng.NextDouble() < 0.5 ? Ramp.FlowerPink : Ramp.FlowerYellow;
                for (int f = 0; f < 10; f++)
                {
                    Vector3 basePoint = tops[rng.Next(tops.Length)];
                    var offset = new Vector3(Range(rng, -0.3f, 0.3f), Range(rng, -0.12f, 0.05f), Range(rng, -0.3f, 0.3f)) * size;
                    int shade = GenPalette.PickShade(rng, 5, v);
                    Shapes.Blob(mb, rng, basePoint + offset, 0.07f * size, Vector3.one, 0.1f, 0, _ => GenPalette.UV(blossom, shade));
                }
            }

            return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = ColliderKind.None };
        }
    }
}
