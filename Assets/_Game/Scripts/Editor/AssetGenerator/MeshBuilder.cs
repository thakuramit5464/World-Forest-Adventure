using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Accumulates flat-shaded (faceted) triangles. Each triangle gets its own three vertices
    /// with an explicit face normal, so lighting is crisp and normals are always correct.
    /// Winding is fixed per triangle from an outward hint, so callers never worry about vertex order.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        readonly List<BoneWeight> boneWeights = new List<BoneWeight>();
        readonly List<Vector4> windData = new List<Vector4>();

        public int TriangleCount => triangles.Count / 3;

        /// <summary>
        /// Optional skinning: when set, every vertex added gets a bone weight computed from its
        /// position. Change it between body parts to weight each part to its bones.
        /// </summary>
        public Func<Vector3, BoneWeight> Skin;

        /// <summary>
        /// Optional wind data (UV2, for the Greenhaven/WindLit shader): x = trunk bend (0 at the roots),
        /// y = canopy weight (0 inner to 1 outer), z = cluster phase 0..1, w = flutter. Evaluated per vertex
        /// position, so coincident vertices get identical values. Parts added while it is null get zero
        /// (no movement).
        /// </summary>
        public Func<Vector3, Vector4> Wind;

        /// <summary>Face normal of (a, b, c) flipped to agree with <paramref name="outward"/>.</summary>
        public static Vector3 OrientedNormal(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0f) n = -n;
            return n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
        }

        /// <summary>Adds a triangle facing <paramref name="outward"/>. Degenerate triangles are skipped.</summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector2 uv)
        {
            // Unity treats a triangle as front-facing where cross(b - a, c - a) points.
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, outward) < 0f)
            {
                (b, c) = (c, b);
                n = -n;
            }
            n.Normalize();

            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
            if (Skin != null)
            {
                boneWeights.Add(Skin(a));
                boneWeights.Add(Skin(b));
                boneWeights.Add(Skin(c));
            }
            if (Wind != null || windData.Count > 0)
            {
                while (windData.Count < i) windData.Add(Vector4.zero); // earlier parts had no wind
                windData.Add(Wind != null ? Wind(a) : Vector4.zero);
                windData.Add(Wind != null ? Wind(b) : Vector4.zero);
                windData.Add(Wind != null ? Wind(c) : Vector4.zero);
            }
        }

        /// <summary>Adds the triangle facing both ways (thin cards such as petals and leaves).</summary>
        public void DoubleSided(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector2 uvFront, Vector2 uvBack)
        {
            Triangle(a, b, c, outward, uvFront);
            Triangle(a, b, c, -outward, uvBack);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            if (boneWeights.Count == vertices.Count && boneWeights.Count > 0) mesh.boneWeights = boneWeights.ToArray();
            if (windData.Count == vertices.Count && windData.Count > 0) mesh.SetUVs(2, windData);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Reusable low-poly shapes built on <see cref="MeshBuilder"/>.</summary>
    public static class Shapes
    {
        /// <summary>
        /// Tapered tube from <paramref name="start"/> along <paramref name="axis"/> (trunks, branches).
        /// The base is left open (it sits in the ground or inside a trunk); the top can be capped.
        /// </summary>
        public static void Frustum(MeshBuilder mb, Vector3 start, Vector3 axis, float length, float startRadius, float endRadius,
            int segments, Func<Vector3, Vector2> uvForNormal, bool capTop)
        {
            axis = axis.normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 side2 = Vector3.Cross(axis, side);
            Vector3 end = start + axis * length;

            for (int k = 0; k < segments; k++)
            {
                float a0 = k * Mathf.PI * 2f / segments, a1 = (k + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0);
                Vector3 d1 = side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1);
                Vector3 b0 = start + d0 * startRadius, b1 = start + d1 * startRadius;
                Vector3 t0 = end + d0 * endRadius, t1 = end + d1 * endRadius;
                Vector3 outward = (d0 + d1).normalized;
                Vector2 uv = uvForNormal(MeshBuilder.OrientedNormal(b0, b1, t0, outward));
                mb.Triangle(b0, b1, t0, outward, uv);
                mb.Triangle(t0, b1, t1, outward, uv);
                if (capTop) mb.Triangle(t0, t1, end, axis, uvForNormal(axis));
            }
        }

        /// <summary>Oriented box (12 triangles). <paramref name="rotation"/> is applied around the centre.</summary>
        public static void Box(MeshBuilder mb, Vector3 centre, Vector3 size, Quaternion rotation, Func<Vector3, Vector2> uvForNormal)
        {
            Vector3 hx = rotation * new Vector3(size.x * 0.5f, 0f, 0f);
            Vector3 hy = rotation * new Vector3(0f, size.y * 0.5f, 0f);
            Vector3 hz = rotation * new Vector3(0f, 0f, size.z * 0.5f);
            void Face(Vector3 n, Vector3 u, Vector3 v)
            {
                Vector3 c = centre + n;
                Vector3 p0 = c - u - v, p1 = c + u - v, p2 = c + u + v, p3 = c - u + v;
                Vector2 uv = uvForNormal(n.normalized);
                mb.Triangle(p0, p1, p2, n, uv);
                mb.Triangle(p0, p2, p3, n, uv);
            }
            Face(hx, hy, hz);
            Face(-hx, hy, hz);
            Face(hy, hx, hz);
            Face(-hy, hx, hz);
            Face(hz, hx, hy);
            Face(-hz, hx, hy);
        }

        /// <summary>Axis-aligned box helper.</summary>
        public static void Box(MeshBuilder mb, Vector3 centre, Vector3 size, Func<Vector3, Vector2> uvForNormal) =>
            Box(mb, centre, size, Quaternion.identity, uvForNormal);

        /// <summary>One cross-section of a <see cref="Loft"/>: centre, elliptical radii, and the colour of the band above it.</summary>
        public struct Ring
        {
            public Vector3 Centre;
            public float RadiusU, RadiusV;
            public Func<Vector3, Vector2> Uv;

            public Ring(Vector3 centre, float radiusU, float radiusV, Func<Vector3, Vector2> uv)
            {
                Centre = centre;
                RadiusU = radiusU;
                RadiusV = radiusV;
                Uv = uv;
            }
        }

        /// <summary>
        /// Lofted tube through elliptical rings lying in the plane spanned by <paramref name="u"/> and
        /// <paramref name="v"/> (limbs, torso, barrels, posts). Each band takes the colour of its lower ring.
        /// </summary>
        public static void Loft(MeshBuilder mb, Vector3 u, Vector3 v, Ring[] rings, int segments, bool capStart, bool capEnd, float twist = 0f,
            bool inward = false)
        {
            u = u.normalized;
            v = v.normalized;
            Vector3 Point(Ring r, int k)
            {
                float a = twist + k * Mathf.PI * 2f / segments;
                return r.Centre + u * (Mathf.Cos(a) * r.RadiusU) + v * (Mathf.Sin(a) * r.RadiusV);
            }

            for (int i = 0; i < rings.Length - 1; i++)
            {
                Ring r0 = rings[i], r1 = rings[i + 1];
                for (int k = 0; k < segments; k++)
                {
                    Vector3 a = Point(r0, k), b = Point(r0, k + 1), c = Point(r1, k), d = Point(r1, k + 1);
                    Vector3 axisMid = (r0.Centre + r1.Centre) * 0.5f;
                    Vector3 outward = (a + b + c + d) * 0.25f - axisMid;
                    if (inward) outward = -outward; // e.g. the inside wall of a well
                    Vector2 uv = r0.Uv(MeshBuilder.OrientedNormal(a, b, c, outward));
                    mb.Triangle(a, b, c, outward, uv);
                    mb.Triangle(c, b, d, outward, uv);
                }
            }

            if (rings.Length < 2) return;
            Vector3 startDir = rings[0].Centre - rings[1].Centre;
            Vector3 endDir = rings[rings.Length - 1].Centre - rings[rings.Length - 2].Centre;
            for (int k = 0; k < segments; k++)
            {
                if (capStart)
                    mb.Triangle(Point(rings[0], k), Point(rings[0], k + 1), rings[0].Centre, startDir, rings[0].Uv(startDir.normalized));
                if (capEnd)
                {
                    Ring last = rings[rings.Length - 1];
                    mb.Triangle(Point(last, k), Point(last, k + 1), last.Centre, endDir, last.Uv(endDir.normalized));
                }
            }
        }

        /// <summary>Straight prism/cylinder between two points (posts, barrels, poles).</summary>
        public static void Cylinder(MeshBuilder mb, Vector3 bottom, Vector3 top, float radius, int segments,
            Func<Vector3, Vector2> uv, bool capBottom = false, bool capTop = true)
        {
            Vector3 axis = (top - bottom).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            Loft(mb, u, v, new[] { new Ring(bottom, radius, radius, uv), new Ring(top, radius, radius, uv) }, segments, capBottom, capTop);
        }

        /// <summary>
        /// Noisy faceted sphere ("blob") for canopies and rocks. Noise is per shared vertex,
        /// so the surface stays closed before it is split into flat faces.
        /// </summary>
        public static void Blob(MeshBuilder mb, System.Random rng, Vector3 centre, float radius, Vector3 scale, float noise,
            int subdivisions, Func<Vector3, Vector2> uvForNormal, Func<Vector3, Vector3> reshape = null)
        {
            Icosphere.Create(subdivisions, out List<Vector3> unit, out List<int> tris);
            var points = new Vector3[unit.Count];
            for (int i = 0; i < unit.Count; i++)
            {
                float r = radius * (1f + ((float)rng.NextDouble() * 2f - 1f) * noise);
                Vector3 p = Vector3.Scale(unit[i] * r, scale);
                points[i] = centre + (reshape != null ? reshape(p) : p);
            }
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = points[tris[t]], b = points[tris[t + 1]], c = points[tris[t + 2]];
                Vector3 outward = (a + b + c) / 3f - centre;
                mb.Triangle(a, b, c, outward, uvForNormal(MeshBuilder.OrientedNormal(a, b, c, outward)));
            }
        }
    }

    /// <summary>Unit icosphere (icosahedron with midpoint subdivision).</summary>
    public static class Icosphere
    {
        public static void Create(int subdivisions, out List<Vector3> vertices, out List<int> triangles)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
            triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(a, b, vertices, midpoints);
                    int bc = Midpoint(b, c, vertices, midpoints);
                    int ca = Midpoint(c, a, vertices, midpoints);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = next;
            }
        }

        static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            index = vertices.Count;
            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            cache[key] = index;
            return index;
        }
    }
}
