using System.Collections.Generic;
using Greenhaven.Editor.AssetGenerator;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>Checks the wind data baked into vegetation meshes (UV2) keeps roots anchored.</summary>
    public class WindTests
    {
        static GeneratorSettings Settings => new GeneratorSettings { Seed = 1234, Size = 1f, ColorVariation = 0.5f };

        static IEnumerable<IAssetGenerator> WindGenerators()
        {
            yield return new BroadleafTreeGenerator();
            yield return new PineTreeGenerator();
            yield return new GrassGenerator();
        }

        [Test]
        public void WindData_IsPresent_AndRootsDoNotMove([ValueSource(nameof(WindGenerators))] IAssetGenerator gen)
        {
            for (int variant = 0; variant < 3; variant++)
            {
                Mesh mesh = gen.Build(Settings, variant, "wind").Mesh;
                var wind = new List<Vector3>();
                mesh.GetUVs(2, wind);
                Vector3[] vertices = mesh.vertices;
                Object.DestroyImmediate(mesh);

                Assert.That(wind.Count, Is.EqualTo(vertices.Length), $"{gen.Prefix} v{variant}: wind data per vertex");
                float maxBend = 0f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].y < 0.02f)
                        Assert.That(wind[i].x, Is.LessThan(0.01f), $"{gen.Prefix} v{variant}: root vertex {i} would move");
                    maxBend = Mathf.Max(maxBend, wind[i].x);
                    Assert.That(wind[i].z, Is.GreaterThan(0f), "frequency multiplier must be positive");
                }
                Assert.That(maxBend, Is.GreaterThan(0.2f), $"{gen.Prefix} v{variant}: tips should sway");
            }
        }

        [Test]
        public void CoincidentVertices_ShareWindData_SoFacetsDoNotCrack()
        {
            Mesh mesh = new BroadleafTreeGenerator().Build(Settings, 0, "wind").Mesh;
            var wind = new List<Vector3>();
            mesh.GetUVs(2, wind);
            Vector3[] vertices = mesh.vertices;
            Object.DestroyImmediate(mesh);

            var seen = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (seen.TryGetValue(vertices[i], out Vector3 previous))
                    Assert.That(Mathf.Abs(previous.x - wind[i].x), Is.LessThan(1e-5f), "same position, same bend");
                else seen[vertices[i]] = wind[i];
            }
        }

        [Test]
        public void StaticAssets_HaveNoWindData()
        {
            Mesh rock = new RockGenerator().Build(Settings, 0, "rock").Mesh;
            var wind = new List<Vector3>();
            rock.GetUVs(2, wind);
            Object.DestroyImmediate(rock);
            Assert.That(wind.Count, Is.EqualTo(0), "rocks never move");
        }
    }
}
