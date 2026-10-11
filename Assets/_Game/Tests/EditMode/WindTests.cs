using System.Collections.Generic;
using Greenhaven.Editor.AssetGenerator;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>
    /// Wind data baked into vegetation meshes (UV2: x = trunk bend, y = canopy weight, z = cluster phase,
    /// w = flutter) must keep roots anchored and keep clusters coherent.
    /// </summary>
    public class WindTests
    {
        static GeneratorSettings Settings => new GeneratorSettings { Seed = 1234, Size = 1f, ColorVariation = 0.5f, FoliageDetail = 1f };

        static IEnumerable<IAssetGenerator> WindGenerators()
        {
            yield return new BroadleafTreeGenerator();
            yield return new PineTreeGenerator();
            yield return new GrassGenerator();
            yield return new BushGenerator();
            yield return new FlowerGenerator();
        }

        static List<Vector4> Wind(Mesh mesh)
        {
            var wind = new List<Vector4>();
            mesh.GetUVs(2, wind);
            return wind;
        }

        [Test]
        public void WindData_IsPresent_AndRootsDoNotMove([ValueSource(nameof(WindGenerators))] IAssetGenerator gen)
        {
            for (int variant = 0; variant < 3; variant++)
            {
                Mesh mesh = gen.Build(Settings, variant, "wind").Mesh;
                List<Vector4> wind = Wind(mesh);
                Vector3[] vertices = mesh.vertices;
                Object.DestroyImmediate(mesh);

                Assert.That(wind.Count, Is.EqualTo(vertices.Length), $"{gen.Prefix} v{variant}: wind data per vertex");
                float maxMotion = 0f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector4 w = wind[i];
                    if (vertices[i].y < 0.02f)
                    {
                        Assert.That(w.x, Is.LessThan(0.01f), $"{gen.Prefix} v{variant}: root vertex {i} trunk bend");
                        Assert.That(w.y, Is.LessThan(0.01f), $"{gen.Prefix} v{variant}: root vertex {i} canopy weight");
                        Assert.That(w.w, Is.LessThan(0.01f), $"{gen.Prefix} v{variant}: root vertex {i} flutter");
                    }
                    Assert.That(w.z, Is.InRange(0f, 1f), "phase is 0..1");
                    maxMotion = Mathf.Max(maxMotion, Mathf.Max(w.x, w.y));
                }
                Assert.That(maxMotion, Is.GreaterThan(0.2f), $"{gen.Prefix} v{variant}: something should sway");
            }
        }

        [Test]
        public void BroadleafCanopy_HasManyIndependentClusters_CalmInsideLivelyOutside()
        {
            Mesh mesh = new BroadleafTreeGenerator().Build(Settings, 0, "canopy").Mesh;
            List<Vector4> wind = Wind(mesh);
            Object.DestroyImmediate(mesh);

            var phases = new HashSet<float>();
            float minFoliage = 1f, maxFoliage = 0f;
            foreach (Vector4 w in wind)
            {
                if (w.w <= 0f) continue; // wood
                phases.Add(Mathf.Round(w.z * 1000f));
                minFoliage = Mathf.Min(minFoliage, w.y);
                maxFoliage = Mathf.Max(maxFoliage, w.y);
            }
            Assert.That(phases.Count, Is.GreaterThanOrEqualTo(12), "many clusters with their own phase (not one rigid mass)");
            Assert.That(minFoliage, Is.LessThan(0.3f), "inner canopy is calm");
            Assert.That(maxFoliage, Is.GreaterThan(0.75f), "outer canopy is lively");
        }

        [Test]
        public void CoincidentVertices_ShareWindData_SoFacetsDoNotCrack()
        {
            Mesh mesh = new BroadleafTreeGenerator().Build(Settings, 0, "wind").Mesh;
            List<Vector4> wind = Wind(mesh);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Object.DestroyImmediate(mesh);

            // Within one triangle's neighbourhood: same position must mean same trunk bend and canopy weight
            // for vertices of the same part (parts are identified by their phase).
            var seen = new Dictionary<(Vector3, float), Vector4>();
            foreach (int i in triangles)
            {
                var key = (vertices[i], Mathf.Round(wind[i].z * 1000f));
                if (seen.TryGetValue(key, out Vector4 previous))
                {
                    Assert.That(Mathf.Abs(previous.x - wind[i].x), Is.LessThan(1e-5f), "same position, same trunk bend");
                    Assert.That(Mathf.Abs(previous.y - wind[i].y), Is.LessThan(1e-5f), "same position, same canopy weight");
                }
                else seen[key] = wind[i];
            }
        }

        [Test]
        public void LowerFoliageDetail_ReducesTriangles()
        {
            var gen = new BroadleafTreeGenerator();
            GeneratorSettings low = Settings;
            low.FoliageDetail = 0.4f;
            Mesh full = gen.Build(Settings, 0, "full").Mesh;
            Mesh reduced = gen.Build(low, 0, "low").Mesh;
            int fullTris = full.triangles.Length / 3, lowTris = reduced.triangles.Length / 3;
            Object.DestroyImmediate(full);
            Object.DestroyImmediate(reduced);
            Assert.That(lowTris, Is.LessThan(fullTris * 0.7f), $"low detail {lowTris} vs full {fullTris}");
        }

        [Test]
        public void StaticAssets_HaveNoWindData()
        {
            Mesh rock = new RockGenerator().Build(Settings, 0, "rock").Mesh;
            int count = Wind(rock).Count;
            Object.DestroyImmediate(rock);
            Assert.That(count, Is.EqualTo(0), "rocks never move");
        }
    }
}
