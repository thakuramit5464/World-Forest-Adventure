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
            Assert.That(b.min.y, Is.EqualTo(0f).Within(0.1f), "base should be at y = 0");
            // Pivot inside the footprint (gates and signposts are intentionally asymmetric).
            Assert.That(Mathf.Abs(b.center.x), Is.LessThanOrEqualTo(b.extents.x + 0.01f), "pivot within X footprint");
            Assert.That(Mathf.Abs(b.center.z), Is.LessThanOrEqualTo(b.extents.z + 0.01f), "pivot within Z footprint");
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
        public void Props_AndBuildings_HaveCollidersWhereExpected()
        {
            foreach (IAssetGenerator gen in Generators.All)
            {
                GeneratedMesh result = gen.Build(Settings(4), 0, "c");
                Object.DestroyImmediate(result.Mesh);
                if (gen.Group == "Buildings" || (gen.Group == "Props" && gen.Category != AssetCategory.Bridge))
                {
                    Assert.That(result.Collider, Is.EqualTo(ColliderKind.Boxes), gen.Prefix);
                    Assert.That(result.Boxes, Is.Not.Null.And.Not.Empty, gen.Prefix);
                }
            }
        }

        [Test]
        public void Hero_HasValidSkinnedMesh_AndHumanoidAvatar()
        {
            HeroGenerator.Result hero = HeroGenerator.BuildInMemory(1234, "TestHero");
            try
            {
                var problems = new List<string>();
                Assert.That(MeshValidator.Validate(hero.Mesh, problems), Is.True, string.Join("\n", problems));
                Assert.That(hero.Mesh.boneWeights.Length, Is.EqualTo(hero.Mesh.vertexCount));
                Assert.That(hero.Mesh.bindposes.Length, Is.EqualTo(hero.Renderer.bones.Length));
                Assert.That(hero.Avatar.isValid, Is.True, "avatar valid");
                Assert.That(hero.Avatar.isHuman, Is.True, "avatar humanoid");
                Assert.That(hero.Mesh.bounds.min.y, Is.EqualTo(0f).Within(0.02f), "feet on the ground");
                Assert.That(hero.Mesh.bounds.size.y, Is.EqualTo(HeroGenerator.Height).Within(0.06f), "height");
            }
            finally
            {
                Object.DestroyImmediate(hero.Root);
                Object.DestroyImmediate(hero.Mesh);
                Object.DestroyImmediate(hero.Avatar);
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
