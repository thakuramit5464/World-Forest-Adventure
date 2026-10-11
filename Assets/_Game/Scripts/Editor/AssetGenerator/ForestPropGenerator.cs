using System.Collections.Generic;
using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Forest-floor details on the nature palette: fallen logs (Mossy/Broken/Long), stumps (Low/Tall) and
    /// mushroom clusters (Toadstool/Brown/Golden). Static (no wind data). Pivot at the base centre.
    /// Logs lie along local X.
    /// </summary>
    public sealed class ForestPropGenerator : IAssetGenerator
    {
        readonly AssetCategory kind;

        public ForestPropGenerator(AssetCategory kind) => this.kind = kind;

        public AssetCategory Category => kind;
        public string Prefix => kind.ToString();
        public string DisplayName => kind switch
        {
            AssetCategory.Log => "Fallen Logs",
            AssetCategory.Stump => "Tree Stumps",
            _ => "Mushroom Clusters",
        };
        public string Group => "Nature";
        public int DefaultCount => kind == AssetCategory.Stump ? 2 : 3;
        public Palette Palette => GenPalette.Nature;

        public string StyleName(int variant) => kind switch
        {
            AssetCategory.Log => new[] { "Mossy", "Broken", "Long" }[variant % 3],
            AssetCategory.Stump => variant % 2 == 0 ? "Low" : "Tall",
            _ => new[] { "Toadstool", "Brown", "Golden" }[variant % 3],
        };

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            var mb = new MeshBuilder();
            float size = s.Size <= 0f ? 1f : s.Size;
            switch (kind)
            {
                case AssetCategory.Log:
                {
                    Bounds box = Log(mb, rng, variant % 3, size, s.ColorVariation);
                    return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = ColliderKind.Boxes, Boxes = new[] { box } };
                }
                case AssetCategory.Stump:
                {
                    (float radius, float height) = Stump(mb, rng, variant % 2, size, s.ColorVariation);
                    return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = ColliderKind.TrunkCapsule, TrunkRadius = radius, TrunkHeight = height };
                }
                default:
                    Mushrooms(mb, rng, variant % 3, size, s.ColorVariation);
                    return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = ColliderKind.None };
            }
        }

        static Bounds Log(MeshBuilder mb, System.Random rng, int style, float size, float variation)
        {
            float length = (style == 2 ? Range(rng, 3.4f, 4.2f) : style == 1 ? Range(rng, 1.6f, 2.2f) : Range(rng, 2.3f, 3.0f)) * size;
            float radius = Range(rng, 0.22f, 0.3f) * size;
            int bark = GenPalette.PickShade(rng, 3, variation);
            const int rings = 6;
            var ringList = new Shapes.Ring[rings];
            for (int i = 0; i < rings; i++)
            {
                float t = i / (rings - 1f);
                float r = radius * (1f + Range(rng, -0.08f, 0.08f)) * (style == 1 && i == rings - 1 ? 0.8f : 1f);
                bool moss = style != 1 && rng.NextDouble() < 0.6;
                int jitter = GenPalette.FaceJitter(rng, variation);
                ringList[i] = new Shapes.Ring(new Vector3(-length * 0.5f + t * length, radius, 0f), r, r,
                    n => Mathf.Abs(n.x) > 0.9f
                        ? GenPalette.UV(Ramp.Bark, 6) // cut end: pale heartwood
                        : moss && n.y > 0.55f
                            ? GenPalette.UV(Ramp.LeafGreen, 2 + jitter)
                            : GenPalette.UV(Ramp.Bark, bark + Palette.SlopeShade(n) + jitter));
            }
            Shapes.Loft(mb, Vector3.up, Vector3.forward, ringList, 7, true, true, Range(rng, 0f, 1f));

            // A broken-off branch stub and, on the broken log, a jagged splinter at the snapped end.
            float stubX = Range(rng, -0.25f, 0.25f) * length;
            Vector3 stubDir = new Vector3(Range(rng, -0.3f, 0.3f), 0.8f, Range(rng, 0.4f, 0.8f) * (rng.NextDouble() < 0.5 ? -1f : 1f));
            Shapes.Frustum(mb, new Vector3(stubX, radius * 1.6f, 0f), stubDir, 0.35f * size, 0.07f * size, 0.04f * size, 5,
                n => GenPalette.UV(Ramp.Bark, bark + 1), true);
            if (style == 1)
                Shapes.Frustum(mb, new Vector3(length * 0.5f - 0.05f, radius * 1.2f, 0f), new Vector3(1f, 0.3f, 0f), 0.3f * size,
                    0.09f * size, 0.02f * size, 4, n => GenPalette.UV(Ramp.Bark, 6), true);

            return new Bounds(new Vector3(0f, radius, 0f), new Vector3(length, radius * 2f, radius * 2f));
        }

        static (float radius, float height) Stump(MeshBuilder mb, System.Random rng, int style, float size, float variation)
        {
            float radius = (style == 0 ? Range(rng, 0.32f, 0.4f) : Range(rng, 0.24f, 0.3f)) * size;
            float height = (style == 0 ? Range(rng, 0.35f, 0.5f) : Range(rng, 0.7f, 0.95f)) * size;
            int bark = GenPalette.PickShade(rng, 3, variation);
            System.Func<Vector3, Vector2> barkUv = n => n.y > 0.9f ? GenPalette.UV(Ramp.Bark, 6) : GenPalette.UV(Ramp.Bark, bark + Palette.SlopeShade(n));
            System.Func<Vector3, Vector2> mossUv = n => n.y > 0.9f ? GenPalette.UV(Ramp.Bark, 6) : GenPalette.UV(Ramp.LeafGreen, 2);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 0f, 0f), radius * 1.35f, radius * 1.3f, style == 1 ? mossUv : barkUv),
                new Shapes.Ring(new Vector3(0f, height * 0.25f, 0f), radius * 1.05f, radius * 1.02f, barkUv),
                new Shapes.Ring(new Vector3(0f, height, 0f), radius, radius * 0.97f, barkUv),
            }, 8, false, true, Range(rng, 0f, 1f));

            // Roots flaring into the ground.
            int roots = 3 + rng.Next(2);
            float start = Range(rng, 0f, Mathf.PI * 2f);
            for (int i = 0; i < roots; i++)
            {
                float a = start + i * Mathf.PI * 2f / roots + Range(rng, -0.3f, 0.3f);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Shapes.Frustum(mb, outward * radius * 0.8f + Vector3.up * 0.16f * size, outward + Vector3.down * 0.4f, 0.42f * size,
                    0.1f * size, 0.04f * size, 4, barkUv, true);
            }
            return (radius * 1.1f, height);
        }

        static void Mushrooms(MeshBuilder mb, System.Random rng, int style, float size, float variation)
        {
            int count = 2 + rng.Next(4);
            for (int i = 0; i < count; i++)
            {
                float a = Range(rng, 0f, Mathf.PI * 2f), d = i == 0 ? 0f : Range(rng, 0.08f, 0.28f);
                var foot = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d) * size;
                float scale = (i == 0 ? 1f : Range(rng, 0.45f, 0.85f)) * size;
                float stemHeight = Range(rng, 0.18f, 0.28f) * scale, stemRadius = 0.035f * scale + 0.01f;
                float capRadius = Range(rng, 0.1f, 0.14f) * scale + 0.02f;
                Shapes.Cylinder(mb, foot, foot + Vector3.up * stemHeight, stemRadius, 5, n => GenPalette.UV(Ramp.Petal, 2), false, false);

                int shade = GenPalette.PickShade(rng, style == 2 ? 3 : 2, variation);
                Ramp ramp = style == 0 ? Ramp.FlowerPink : style == 1 ? Ramp.Bark : Ramp.FlowerYellow;
                if (style == 1) shade += 3;
                System.Func<Vector3, Vector2> capUv = n => n.y < -0.3f ? GenPalette.UV(Ramp.Petal, 1) : GenPalette.UV(ramp, shade + Palette.SlopeShade(n));
                Vector3 top = foot + Vector3.up * stemHeight;
                Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
                {
                    new Shapes.Ring(top + Vector3.down * 0.01f, capRadius * 0.55f, capRadius * 0.55f, capUv),
                    new Shapes.Ring(top + Vector3.up * 0.02f * scale, capRadius, capRadius, capUv),
                    new Shapes.Ring(top + Vector3.up * 0.07f * scale, capRadius * 0.78f, capRadius * 0.78f, capUv),
                    new Shapes.Ring(top + Vector3.up * 0.1f * scale, capRadius * 0.35f, capRadius * 0.35f, capUv),
                }, 7, true, true);

                // White spots on toadstools.
                if (style == 0)
                    for (int k = 0; k < 3; k++)
                    {
                        float sa = Range(rng, 0f, Mathf.PI * 2f);
                        Vector3 spot = top + new Vector3(Mathf.Cos(sa) * capRadius * 0.55f, 0.075f * scale, Mathf.Sin(sa) * capRadius * 0.55f);
                        Shapes.Box(mb, spot, Vector3.one * 0.025f * scale + Vector3.one * 0.01f, Quaternion.Euler(0f, sa * Mathf.Rad2Deg, 20f),
                            n => GenPalette.UV(Ramp.Petal, 6));
                    }
            }
        }
    }
}
