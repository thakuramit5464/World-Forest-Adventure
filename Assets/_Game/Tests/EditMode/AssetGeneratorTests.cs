using System.Collections.Generic;
using Greenhaven.Editor.AssetGenerator;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>In-memory checks of the procedural generators (no assets are written).</summary>
    public class AssetGeneratorTests
    {
        static IEnumerable<IAssetGenerator> AllGenerators => Generators.All;

        static GeneratorSettings Settings(int seed) => new GeneratorSettings { Seed = seed, Size = 1f, ColorVariation = 0.5f };

        [Test]
        public void EveryVariant_ProducesAStructurallyValidMesh([ValueSource(nameof(AllGenerators))] IAssetGenerator gen,
            [Values(1, 42, 99999)] int seed)
        {
            for (int variant = 0; variant < 6; variant++)
            {
                GeneratedMesh result = gen.Build(Settings(seed), variant, $"{gen.Prefix}_{seed}_{variant}");
                var problems = new List<string>();
                bool valid = MeshValidator.Validate(result.Mesh, problems);
                Object.DestroyImmediate(result.Mesh);
                Assert.That(valid, Is.True, string.Join("\n", problems));
            }
        }

        [Test]
        public void SameSeed_GivesIdenticalGeometry([ValueSource(nameof(AllGenerators))] IAssetGenerator gen)
        {
            Mesh a = gen.Build(Settings(7), 2, "a").Mesh;
            Mesh b = gen.Build(Settings(7), 2, "b").Mesh;
            bool same = GeneratedAssetStore.SameVertices(a, b);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Assert.That(same, Is.True);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentGeometry([ValueSource(nameof(AllGenerators))] IAssetGenerator gen)
        {
            Mesh a = gen.Build(Settings(7), 0, "a").Mesh;
            Mesh b = gen.Build(Settings(8), 0, "b").Mesh;
            bool same = GeneratedAssetStore.SameVertices(a, b);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Assert.That(same, Is.False);
        }

        [Test]
        public void Variants_AreIndependentOfHowManyAreGenerated()
        {
            // Variant 2 must not depend on variants 0 and 1 having been built first.
            var gen = new PineTreeGenerator();
            Mesh direct = gen.Build(Settings(5), 2, "direct").Mesh;
            Object.DestroyImmediate(gen.Build(Settings(5), 0, "x").Mesh);
            Object.DestroyImmediate(gen.Build(Settings(5), 1, "y").Mesh);
            Mesh afterOthers = gen.Build(Settings(5), 2, "after").Mesh;
            Assert.That(GeneratedAssetStore.SameVertices(direct, afterOthers), Is.True);
            Object.DestroyImmediate(direct);
            Object.DestroyImmediate(afterOthers);
        }

        [Test]
        public void Assets_SitOnTheGround_WithPivotAtBaseCentre([ValueSource(nameof(AllGenerators))] IAssetGenerator gen)
        {
            Mesh mesh = gen.Build(Settings(3), 0, "m").Mesh;
            Bounds b = mesh.bounds;
            Object.DestroyImmediate(mesh);
            Assert.That(b.min.y, Is.EqualTo(0f).Within(0.05f), "base should be at y = 0");
            Assert.That(Mathf.Abs(b.center.x), Is.LessThan(b.extents.x * 0.5f + 0.01f), "roughly centred on X");
            Assert.That(Mathf.Abs(b.center.z), Is.LessThan(b.extents.z * 0.5f + 0.01f), "roughly centred on Z");
        }

        [Test]
        public void Rocks_StayWithinConvexColliderLimit()
        {
            var gen = new RockGenerator();
            for (int variant = 0; variant < 6; variant++)
            {
                Mesh mesh = gen.Build(Settings(11), variant, "rock").Mesh;
                int triangles = mesh.triangles.Length / 3;
                Object.DestroyImmediate(mesh);
                Assert.That(triangles, Is.LessThanOrEqualTo(255));
            }
        }

        [Test]
        public void Trees_StayWithinMobileTriangleBudget()
        {
            foreach (IAssetGenerator gen in new IAssetGenerator[] { new PineTreeGenerator(), new BroadleafTreeGenerator() })
            {
                for (int variant = 0; variant < 3; variant++)
                {
                    Mesh mesh = gen.Build(Settings(21), variant, "tree").Mesh;
                    int triangles = mesh.triangles.Length / 3;
                    Object.DestroyImmediate(mesh);
                    Assert.That(triangles, Is.LessThanOrEqualTo(800), $"{gen.Prefix} variant {variant}");
                }
            }
        }

        [Test]
        public void PaletteUVs_PointAtCellCentres()
        {
            Vector2 uv = GenPalette.UV(Ramp.RockGrey, 3);
            Assert.That(uv.x, Is.EqualTo(3.5f / GenPalette.Shades).Within(1e-6f));
            Assert.That(uv.y, Is.EqualTo(((int)Ramp.RockGrey + 0.5f) / GenPalette.Rows).Within(1e-6f));
            // Out-of-range shades clamp instead of sampling a neighbouring ramp.
            Assert.That(GenPalette.UV(Ramp.Bark, 99).x, Is.EqualTo(7.5f / GenPalette.Shades).Within(1e-6f));
            Assert.That(GenPalette.UV(Ramp.Bark, -5).x, Is.EqualTo(0.5f / GenPalette.Shades).Within(1e-6f));
        }
    }
}
