using System;
using System.Collections.Generic;
using Greenhaven.Editor.AssetGenerator;
using UnityEngine;

namespace Greenhaven.Editor.Wildlife
{
    /// <summary>
    /// Builds one rigidly skinned, flat-shaded animal mesh plus its bone list. Parts are authored in model
    /// space (metres, +Z forward, feet at y = 0) and each part is weighted fully to the bone given when it
    /// is added. Bones rest at identity rotation, so bind poses are pure translations.
    /// </summary>
    public sealed class AnimalBuilder
    {
        public readonly MeshBuilder Mesh = new MeshBuilder();
        readonly List<(string name, int parent, Vector3 position)> bones = new List<(string, int, Vector3)>();
        int current;

        /// <summary>Uniform scale applied to every authored position and size.</summary>
        public float Scale = 1f;
        /// <summary>Optional reshaping of authored positions before scaling (e.g. longer deer legs).</summary>
        public Func<Vector3, Vector3> Warp;

        public AnimalBuilder() => Mesh.Skin = _ => new BoneWeight { boneIndex0 = current, weight0 = 1f };

        public int BoneCount => bones.Count;

        Vector3 S(Vector3 p) => (Warp != null ? Warp(p) : p) * Scale;

        public int Bone(string name, int parent, Vector3 position)
        {
            bones.Add((name, parent, S(position)));
            return bones.Count - 1;
        }

        /// <summary>
        /// A tube bending through <paramref name="centres"/> with elliptical rings (x = side radius, y = the other
        /// radius). Ring frames are parallel-transported, so curved horns and tails do not twist.
        /// <paramref name="uv"/> receives the face normal and the face centre in authored (unscaled) space.
        /// </summary>
        public void Tube(int bone, Vector3[] centres, Vector2[] radii, int segments, Func<Vector3, Vector3, Vector2> uv, bool capStart = true, bool capEnd = true)
        {
            current = bone;
            int n = centres.Length;
            var c = new Vector3[n];
            for (int i = 0; i < n; i++) c[i] = S(centres[i]);
            var ring = new Vector3[n, segments];
            Vector3 u = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 t = (c[Mathf.Min(i + 1, n - 1)] - c[Mathf.Max(i - 1, 0)]).normalized;
                if (i == 0)
                {
                    u = Vector3.Cross(Vector3.up, t);
                    if (u.sqrMagnitude < 1e-6f) u = Vector3.right;
                }
                else u -= t * Vector3.Dot(u, t);
                u.Normalize();
                Vector3 v = Vector3.Cross(t, u);
                for (int k = 0; k < segments; k++)
                {
                    float a = k * Mathf.PI * 2f / segments;
                    ring[i, k] = c[i] + u * (Mathf.Cos(a) * radii[i].x * Scale) + v * (Mathf.Sin(a) * radii[i].y * Scale);
                }
            }
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 axis = (c[i] + c[i + 1]) * 0.5f;
                for (int k = 0; k < segments; k++)
                {
                    int k1 = (k + 1) % segments;
                    Vector3 p00 = ring[i, k], p01 = ring[i, k1], p10 = ring[i + 1, k], p11 = ring[i + 1, k1];
                    Vector3 centre = (p00 + p01 + p10 + p11) * 0.25f;
                    Vector3 outward = centre - axis;
                    Vector3 normal = MeshBuilder.OrientedNormal(p00, p01, p10, outward);
                    Vector2 tex = uv(normal, centre / Scale);
                    Mesh.Triangle(p00, p01, p10, outward, tex);
                    Mesh.Triangle(p10, p01, p11, outward, tex);
                }
            }
            if (n < 2) return;
            Vector3 startDir = (c[0] - c[1]).normalized, endDir = (c[n - 1] - c[n - 2]).normalized;
            for (int k = 0; k < segments; k++)
            {
                int k1 = (k + 1) % segments;
                if (capStart) Mesh.Triangle(ring[0, k], ring[0, k1], c[0], startDir, uv(startDir, centres[0]));
                if (capEnd) Mesh.Triangle(ring[n - 1, k], ring[n - 1, k1], c[n - 1], endDir, uv(endDir, centres[n - 1]));
            }
        }

        public void Blob(int bone, System.Random rng, Vector3 centre, float radius, Vector3 scale, Func<Vector3, Vector2> uv, float noise = 0.05f)
        {
            current = bone;
            Shapes.Blob(Mesh, rng, S(centre), radius * Scale, scale, noise, 1, uv);
        }

        public void Box(int bone, Vector3 centre, Vector3 size, Quaternion rotation, Func<Vector3, Vector2> uv)
        {
            current = bone;
            Shapes.Box(Mesh, S(centre), size * Scale, rotation, uv);
        }

        public void Box(int bone, Vector3 centre, Vector3 size, Func<Vector3, Vector2> uv) => Box(bone, centre, size, Quaternion.identity, uv);

        /// <summary>Tapered cone from <paramref name="from"/> to <paramref name="to"/> (open base, capped tip).</summary>
        public void Cone(int bone, Vector3 from, Vector3 to, float startRadius, float endRadius, int segments, Func<Vector3, Vector2> uv)
        {
            current = bone;
            Vector3 a = S(from), b = S(to);
            Shapes.Frustum(Mesh, a, b - a, Vector3.Distance(a, b), startRadius * Scale, Mathf.Max(endRadius, 0.001f) * Scale, segments, uv, true);
        }

        /// <summary>The finished mesh with bind poses (bones rest at their positions with identity rotation).</summary>
        public Mesh ToMesh(string name)
        {
            Mesh mesh = Mesh.ToMesh(name);
            var bind = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++) bind[i] = Matrix4x4.Translate(-bones[i].position);
            mesh.bindposes = bind;
            return mesh;
        }

        /// <summary>Creates the bone transforms under <paramref name="root"/> (which must sit at the origin with identity rotation).</summary>
        public Transform[] CreateBones(Transform root)
        {
            var t = new Transform[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                (string name, int parent, Vector3 position) = bones[i];
                var go = new GameObject(name);
                t[i] = go.transform;
                t[i].SetParent(parent < 0 ? root : t[parent], false);
                t[i].localPosition = parent < 0 ? position : position - bones[parent].position;
            }
            return t;
        }

        public Transform Find(Transform[] t, string name)
        {
            for (int i = 0; i < bones.Count; i++)
                if (bones[i].name == name) return t[i];
            return null;
        }
    }
}
