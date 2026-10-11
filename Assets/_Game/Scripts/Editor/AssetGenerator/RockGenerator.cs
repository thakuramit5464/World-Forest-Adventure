using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Faceted rocks: a noisy icosphere, stretched per style (Boulder, Slab, Spire), with a
    /// flattened base so it sits on the ground. 80 triangles, so a convex MeshCollider stays
    /// well under Unity's 255-triangle convex limit. Pivot at the base centre.
    /// </summary>
    public sealed class RockGenerator : IAssetGenerator
    {
        static readonly string[] Styles = { "Boulder", "Slab", "Spire" };

        public AssetCategory Category => AssetCategory.Rock;
        public string Prefix => "Rock";
        public string DisplayName => "Rocks";
        public string StyleName(int variant) => Styles[variant % Styles.Length];

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            int style = variant % Styles.Length;
            float v = s.ColorVariation;

            // Size classes rotate independently of style so a set mixes small, medium and large.
            float sizeClass = 0.7f + 0.45f * (variant * 7 % 3);
            float size = s.Size * sizeClass * Range(rng, 0.9f, 1.1f);
            Vector3 stretch = style == 0 ? new Vector3(1f, 0.75f, 1.1f) : style == 1 ? new Vector3(1.6f, 0.42f, 1.2f) : new Vector3(0.7f, 1.45f, 0.75f);
            float noise = style == 0 ? 0.18f : style == 1 ? 0.15f : 0.22f;
            Quaternion yaw = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);

            // Flatten everything below 25% of the height into a base, then lift so the base sits at y = 0.
            float halfHeight = stretch.y * size;
            float floor = -halfHeight * 0.5f;
            Ramp ramp = rng.NextDouble() < 0.5 ? Ramp.RockGrey : Ramp.RockWarm;
            int baseShade = GenPalette.PickShade(rng, 3, v);
            var centre = new Vector3(0f, -floor, 0f);

            var mb = new MeshBuilder();
            Shapes.Blob(mb, rng, centre, size, stretch, noise, 1,
                n => GenPalette.UV(ramp, baseShade + (n.y > 0.6f ? 2 : n.y < -0.5f ? -2 : 0) + GenPalette.FaceJitter(rng, v)),
                p =>
                {
                    p = yaw * p;
                    if (p.y < floor) p.y = floor;
                    return p;
                });

            return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = ColliderKind.ConvexMesh };
        }
    }
}
