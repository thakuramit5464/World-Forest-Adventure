using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Small flowers: stem, two leaves, a ring of double-sided petals and a round centre.
    /// Styles: Single, Cluster, Tall. Walk-through: no collider. About 60–250 triangles.
    /// </summary>
    public sealed class FlowerGenerator : IAssetGenerator
    {
        static readonly string[] Styles = { "Single", "Cluster", "Tall" };
        static readonly Ramp[] PetalRamps = { Ramp.FlowerPink, Ramp.FlowerYellow, Ramp.Petal };

        public AssetCategory Category => AssetCategory.Flower;
        public string Prefix => "Flower";
        public string DisplayName => "Flowers";
        public string Group => "Nature";
        public int DefaultCount => 4;
        public Palette Palette => GenPalette.Nature;
        public string StyleName(int variant) => Styles[variant % Styles.Length];

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            int style = variant % Styles.Length;
            float v = s.ColorVariation;
            var mb = new MeshBuilder();

            // Variants also rotate through petal colours so a set stays varied but coherent.
            Ramp petals = PetalRamps[(variant / Styles.Length + variant) % PetalRamps.Length];
            int count = style == 1 ? 5 : 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 root = i == 0 ? Vector3.zero : new Vector3(Range(rng, -0.18f, 0.18f), 0f, Range(rng, -0.18f, 0.18f)) * s.Size;
                float height = s.Size * (style == 2 ? Range(rng, 0.55f, 0.7f) : Range(rng, 0.25f, 0.4f));
                // Wind: the stem bends from a fixed root (squared falloff); head, leaves and petals follow
                // because the data depends only on height. Each flower has its own phase.
                float stemHeight = height;
                float phase = Mathf.Repeat(i * 0.618f + variant * 0.31f, 1f);
                mb.Wind = p =>
                {
                    float h = Mathf.Clamp01(p.y / stemHeight);
                    return new Vector4(0f, h * h, phase, 0.2f * h);
                };
                Flower(mb, rng, root, height, s.Size * (style == 2 ? 0.11f : 0.08f), petals, v);
            }

            return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = ColliderKind.None };
        }

        static void Flower(MeshBuilder mb, System.Random rng, Vector3 root, float height, float petalLength, Ramp petalRamp, float v)
        {
            int stemShade = GenPalette.PickShade(rng, 2, v);
            // Slight lean so clusters don't look like a grid.
            Vector3 top = root + new Vector3(Range(rng, -0.04f, 0.04f), height, Range(rng, -0.04f, 0.04f));
            Shapes.Cylinder(mb, root, top, 0.012f, 4, _ => GenPalette.UV(Ramp.LeafGreen, stemShade), capTop: false);

            // Two leaves part-way up the stem.
            for (int l = 0; l < 2; l++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(angle), 0.6f, Mathf.Sin(angle)).normalized;
                Vector3 attach = Vector3.Lerp(root, top, Range(rng, 0.25f, 0.5f));
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * 0.025f;
                Vector3 tip = attach + dir * height * 0.35f;
                Vector3 mid = (attach + tip) * 0.5f;
                Vector3 normal = Vector3.Cross(side, dir);
                Vector2 uv = GenPalette.UV(Ramp.LeafGreen, stemShade + 1);
                mb.DoubleSided(attach, mid + side, tip, normal, uv, uv);
                mb.DoubleSided(attach, tip, mid - side, normal, uv, uv);
            }

            // Petals: diamonds around the head, tilted upwards.
            int petals = rng.Next(5, 7);
            int shade = GenPalette.PickShade(rng, 4, v);
            float twist = Range(rng, 0f, Mathf.PI * 2f);
            for (int p = 0; p < petals; p++)
            {
                float a = twist + p * Mathf.PI * 2f / petals;
                var outDir = new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)).normalized;
                Vector3 side = Vector3.Cross(outDir, Vector3.up).normalized * petalLength * 0.35f;
                Vector3 tip = top + outDir * petalLength;
                Vector3 mid = top + outDir * petalLength * 0.45f;
                Vector3 up = Vector3.Cross(side, outDir);
                if (up.y < 0f) up = -up;
                Vector2 front = GenPalette.UV(petalRamp, shade + (p % 2));
                Vector2 back = GenPalette.UV(petalRamp, shade - 1);
                mb.DoubleSided(top, mid + side, tip, up, front, back);
                mb.DoubleSided(top, tip, mid - side, up, front, back);
            }

            // Centre.
            Ramp centreRamp = petalRamp == Ramp.FlowerYellow ? Ramp.Bark : Ramp.FlowerYellow;
            Shapes.Blob(mb, rng, top + Vector3.up * 0.01f, petalLength * 0.3f, new Vector3(1f, 0.6f, 1f), 0.05f, 0,
                _ => GenPalette.UV(centreRamp, centreRamp == Ramp.Bark ? 2 : 5));
        }
    }
}
