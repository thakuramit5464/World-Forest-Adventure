using System.Collections.Generic;
using Greenhaven.Editor.River;
using Greenhaven.Environment;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>River mesh data: continuous lengthwise UVs (no seams), smooth speed changes between segments, foam where intended.</summary>
    public class RiverTests
    {
        static RiverFlowController MakeRiver(out GameObject go)
        {
            go = new GameObject("~RiverTest") { hideFlags = HideFlags.HideAndDontSave };
            RiverFlowController river = go.AddComponent<RiverFlowController>();
            river.controlPoints = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(6f, 0f, 8f), new Vector3(2f, 0f, 16f), new Vector3(8f, 0f, 24f) };
            river.width = 3f;
            river.segments = new List<RiverSegment> { new RiverSegment { fromPoint = 0, speed = 1f }, new RiverSegment { fromPoint = 2, speed = 2f } };
            river.rocks = new List<Vector3> { new Vector3(6f, 0f, 8f) };
            return river;
        }

        static Mesh Build(out Vector3[] centre, out float[] speeds)
        {
            RiverFlowController river = MakeRiver(out GameObject go);
            Mesh mesh = RiverTools.BuildMesh(river, out centre, out speeds, out _);
            Object.DestroyImmediate(go);
            return mesh;
        }

        [Test]
        public void LengthwiseUVs_IncreaseSmoothly_AlongTheWholeRiver()
        {
            Mesh mesh = Build(out Vector3[] centre, out _);
            var uv = new List<Vector2>();
            mesh.GetUVs(0, uv);
            int across = uv.Count / centre.Length;
            Object.DestroyImmediate(mesh);
            for (int i = 1; i < centre.Length; i++)
            {
                float step = uv[i * across].y - uv[(i - 1) * across].y;
                Assert.That(step, Is.GreaterThan(0f).And.LessThan(1.5f), $"row {i}: v must keep increasing in small steps (no seam or jump)");
            }
        }

        [Test]
        public void SegmentSpeeds_BlendWithoutJumps()
        {
            Mesh mesh = Build(out _, out float[] speeds);
            Object.DestroyImmediate(mesh);
            Assert.That(speeds[0], Is.EqualTo(1f).Within(0.01f));
            Assert.That(speeds[speeds.Length - 1], Is.EqualTo(2f).Within(0.01f));
            for (int i = 1; i < speeds.Length; i++)
                Assert.That(Mathf.Abs(speeds[i] - speeds[i - 1]), Is.LessThan(0.35f), "speed changes gradually between segments");
        }

        [Test]
        public void Foam_GathersAtRocksAndBanks_NotEverywhere()
        {
            Mesh mesh = Build(out _, out _);
            var data = new List<Vector4>();
            mesh.GetUVs(2, data);
            Vector3[] vertices = mesh.vertices;
            Object.DestroyImmediate(mesh);
            float nearRock = 0f, total = 0f;
            foreach (var (v, d) in Zip(vertices, data))
            {
                total += d.x;
                if (new Vector2(v.x - 6f, v.z - 8f).magnitude < 0.4f) nearRock = Mathf.Max(nearRock, d.x);
                Assert.That(d.z, Is.InRange(0f, 1f), "depth 0..1");
            }
            Assert.That(nearRock, Is.GreaterThan(0.6f), "foam around the rock");
            Assert.That(total / data.Count, Is.LessThan(0.45f), "most of the surface is clear water");
        }

        [Test]
        public void SurfaceIsFlatAndFacesUp()
        {
            Mesh mesh = Build(out _, out _);
            Vector3[] normals = mesh.normals;
            int triangles = mesh.triangles.Length / 3;
            Object.DestroyImmediate(mesh);
            foreach (Vector3 n in normals) Assert.That(n.y, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(triangles, Is.GreaterThan(50));
        }

        static IEnumerable<(Vector3, Vector4)> Zip(Vector3[] a, List<Vector4> b)
        {
            for (int i = 0; i < a.Length; i++) yield return (a[i], b[i]);
        }
    }
}
