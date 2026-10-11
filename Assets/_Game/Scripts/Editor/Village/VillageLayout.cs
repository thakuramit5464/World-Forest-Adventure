using System.Collections.Generic;
using UnityEngine;

namespace Greenhaven.Editor.VillageScene
{
    /// <summary>Oriented rectangle on the ground plane (x, z), with Unity yaw in degrees.</summary>
    public struct Obb
    {
        public Vector2 Centre;
        public Vector2 HalfSize;
        public float Yaw;

        public Obb(Vector2 centre, Vector2 halfSize, float yaw)
        {
            Centre = centre;
            HalfSize = halfSize;
            Yaw = yaw;
        }

        /// <summary>World direction of the local +X axis (matches Quaternion.Euler(0, yaw, 0) * right).</summary>
        public Vector2 AxisX
        {
            get
            {
                float r = Yaw * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(r), -Mathf.Sin(r));
            }
        }

        /// <summary>World direction of the local +Z axis (the "front").</summary>
        public Vector2 AxisZ
        {
            get
            {
                float r = Yaw * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            }
        }

        public bool Contains(Vector2 p, float margin)
        {
            Vector2 d = p - Centre;
            return Mathf.Abs(Vector2.Dot(d, AxisX)) <= HalfSize.x + margin && Mathf.Abs(Vector2.Dot(d, AxisZ)) <= HalfSize.y + margin;
        }

        /// <summary>Separating-axis test between two oriented rectangles.</summary>
        public bool Overlaps(Obb other, float margin = 0f)
        {
            foreach (Vector2 axis in new[] { AxisX, AxisZ, other.AxisX, other.AxisZ })
            {
                float r1 = HalfSize.x * Mathf.Abs(Vector2.Dot(AxisX, axis)) + HalfSize.y * Mathf.Abs(Vector2.Dot(AxisZ, axis));
                float r2 = other.HalfSize.x * Mathf.Abs(Vector2.Dot(other.AxisX, axis)) + other.HalfSize.y * Mathf.Abs(Vector2.Dot(other.AxisZ, axis));
                if (Mathf.Abs(Vector2.Dot(other.Centre - Centre, axis)) > r1 + r2 + margin) return false;
            }
            return true;
        }

        /// <summary>World position of a point given in this rectangle's local (x, z) frame.</summary>
        public Vector2 ToWorld(float localX, float localZ) => Centre + AxisX * localX + AxisZ * localZ;
    }

    /// <summary>
    /// What occupies the village ground: solid footprints, paths, and keep-clear circles. Used to place
    /// props and vegetation without blocking routes or doors, and to validate the hand-authored layout.
    /// </summary>
    public sealed class VillageLayout
    {
        public readonly List<(Obb box, string name)> Footprints = new List<(Obb, string)>();
        public readonly List<(Vector2 a, Vector2 b, float halfWidth)> Paths = new List<(Vector2, Vector2, float)>();
        public readonly List<(Vector2 centre, float radius)> KeepClear = new List<(Vector2, float)>();

        public void AddFootprint(Obb box, string name) => Footprints.Add((box, name));
        public void AddPath(Vector2 a, Vector2 b, float width) => Paths.Add((a, b, width * 0.5f));
        public void AddKeepClear(Vector2 centre, float radius) => KeepClear.Add((centre, radius));

        public float DistanceToPaths(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var path in Paths)
                best = Mathf.Min(best, DistanceToSegment(p, path.a, path.b) - path.halfWidth);
            return best;
        }

        public bool InFootprint(Vector2 p, float margin, out string name)
        {
            foreach (var f in Footprints)
            {
                if (f.box.Contains(p, margin))
                {
                    name = f.name;
                    return true;
                }
            }
            name = null;
            return false;
        }

        /// <summary>True if <paramref name="p"/> is clear of footprints, paths and keep-clear areas by <paramref name="margin"/>.</summary>
        public bool IsFree(Vector2 p, float margin)
        {
            if (InFootprint(p, margin, out _)) return false;
            if (DistanceToPaths(p) < margin) return false;
            foreach (var k in KeepClear)
                if ((p - k.centre).magnitude < k.radius + margin) return false;
            return true;
        }

        /// <summary>True if a rectangle is clear of paths and does not overlap any footprint.</summary>
        public bool IsFree(Obb box, float margin)
        {
            foreach (var f in Footprints)
                if (f.box.Overlaps(box, margin)) return false;
            foreach (var path in Paths)
            {
                // Sample the path centre line; cheap and adequate at village scale.
                float len = Vector2.Distance(path.a, path.b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
                for (int i = 0; i <= steps; i++)
                    if (box.Contains(Vector2.Lerp(path.a, path.b, i / (float)steps), path.halfWidth + margin)) return false;
            }
            return true;
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
