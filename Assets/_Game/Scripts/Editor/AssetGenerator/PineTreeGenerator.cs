using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Stylised pines: tapered trunk plus stacked, drooping foliage cones. Three silhouettes
    /// (Classic, Slender, Bushy) cycle by variant. About 150–250 triangles each.
    /// </summary>
    public sealed class PineTreeGenerator : IAssetGenerator
    {
        static readonly string[] Styles = { "Classic", "Slender", "Bushy" };

        public AssetCategory Category => AssetCategory.PineTree;
        public string Prefix => "Pine";
        public string DisplayName => "Pine Trees";
        public string Group => "Nature";
        public int DefaultCount => 3;
        public Palette Palette => GenPalette.Nature;
        public string StyleName(int variant) => Styles[variant % Styles.Length];

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            int style = variant % Styles.Length;
            float v = s.ColorVariation;

            float height = s.Size * (style == 0 ? 6f : style == 1 ? 8f : 5f) * Range(rng, 0.9f, 1.1f);
            float maxRadius = height * (style == 0 ? 0.30f : style == 1 ? 0.17f : 0.36f);
            int layers = style == 0 ? 5 : style == 1 ? 7 : 4;
            int segments = style == 2 ? 9 : 8;
            float trunkRadius = height * (style == 1 ? 0.028f : style == 2 ? 0.045f : 0.035f);

            var mb = new MeshBuilder();

            int bark = GenPalette.PickShade(rng, 3, v);
            Shapes.Frustum(mb, Vector3.zero, Vector3.up, height * 0.4f, trunkRadius, trunkRadius * 0.55f, 6,
                n => GenPalette.UV(Ramp.Bark, bark + GenPalette.FaceJitter(rng, v)), capTop: false);

            int green = GenPalette.PickShade(rng, 3, v);
            for (int i = 0; i < layers; i++)
            {
                float t = i / (float)(layers - 1);
                float baseY = Mathf.Lerp(height * 0.2f, height * 0.72f, t);
                float layerHeight = height * Mathf.Lerp(0.3f, 0.2f, t) * Range(rng, 0.9f, 1.1f);
                float radius = maxRadius * Mathf.Lerp(1f, 0.3f, t) * Range(rng, 0.9f, 1.1f);
                int shade = green + (i > layers / 2 ? 1 : 0); // upper layers catch more light
                ConeLayer(mb, rng, baseY, layerHeight, radius, layerHeight * 0.15f, segments, shade, v);
            }
            // Pointed tip so every silhouette ends cleanly.
            ConeLayer(mb, rng, height * 0.86f, height * 0.14f, maxRadius * 0.18f, 0f, 6, green + 2, v);

            return new GeneratedMesh
            {
                Mesh = mb.ToMesh(meshName),
                Collider = ColliderKind.TrunkCapsule,
                TrunkRadius = trunkRadius * 1.3f,
                TrunkHeight = height * 0.5f,
            };
        }

        /// <summary>One foliage tier: a cone with a jagged, drooping rim and a concave underside.</summary>
        static void ConeLayer(MeshBuilder mb, System.Random rng, float baseY, float layerHeight, float radius, float droop,
            int segments, int shade, float variation)
        {
            var apex = new Vector3(Range(rng, -0.03f, 0.03f) * radius, baseY + layerHeight, Range(rng, -0.03f, 0.03f) * radius);
            var underside = new Vector3(0f, baseY + layerHeight * 0.18f, 0f);
            float twist = Range(rng, 0f, Mathf.PI * 2f);

            var rim = new Vector3[segments];
            for (int k = 0; k < segments; k++)
            {
                float angle = twist + k * Mathf.PI * 2f / segments;
                float r = radius * Range(rng, 0.88f, 1.08f);
                rim[k] = new Vector3(Mathf.Cos(angle) * r, baseY - droop * Range(rng, 0.6f, 1f), Mathf.Sin(angle) * r);
            }

            for (int k = 0; k < segments; k++)
            {
                Vector3 a = rim[k], b = rim[(k + 1) % segments];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 outward = new Vector3(mid.x, 0f, mid.z).normalized + Vector3.up * 0.3f;
                Vector3 n = MeshBuilder.OrientedNormal(a, b, apex, outward);
                int sideShade = shade + (n.y > 0.55f ? 1 : 0) + GenPalette.FaceJitter(rng, variation);
                mb.Triangle(a, b, apex, outward, GenPalette.UV(Ramp.PineGreen, sideShade));
                mb.Triangle(a, b, underside, Vector3.down, GenPalette.UV(Ramp.PineGreen, shade - 2));
            }
        }
    }
}
