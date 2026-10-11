using System.Collections.Generic;
using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Stylised broadleaf trees (version 2): flared trunk, branches reaching into the crown, and a full,
    /// layered canopy built from many overlapping faceted leaf clusters — a dark inner core, one cluster on
    /// every branch tip, a shell of clusters over a rounded ellipsoid, and low-poly filler underneath.
    /// Three silhouettes (Round, Tall, Umbrella) cycle by variant.
    ///
    /// Wind data per vertex (see Greenhaven/WindLit): trunk bend grows with height (roots fixed); each
    /// cluster is rigid with its own phase and a canopy weight that grows from the core (calm) to the outer
    /// shell (lively); branches blend from 0 at the trunk to their tip cluster's values, so leaves never
    /// detach. About 900–1,900 triangles depending on Foliage Detail and style.
    /// </summary>
    public sealed class BroadleafTreeGenerator : IAssetGenerator
    {
        static readonly string[] Styles = { "Round", "Tall", "Umbrella" };

        public AssetCategory Category => AssetCategory.BroadleafTree;
        public string Prefix => "Broadleaf";
        public string DisplayName => "Broadleaf Trees";
        public string Group => "Nature";
        public int DefaultCount => 3;
        public Palette Palette => GenPalette.Nature;
        public string StyleName(int variant) => Styles[variant % Styles.Length];

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            int style = variant % Styles.Length;
            float v = s.ColorVariation;
            float detail = s.Detail;

            float height = s.Size * (style == 0 ? 5.5f : style == 1 ? 7f : 5f) * Range(rng, 0.9f, 1.1f);
            float trunkTop = height * (style == 0 ? 0.42f : style == 1 ? 0.45f : 0.42f);
            float trunkRadius = height * 0.05f;
            Vector3 radii = height * (style == 0 ? new Vector3(0.3f, 0.24f, 0.3f)
                : style == 1 ? new Vector3(0.21f, 0.33f, 0.21f)
                : new Vector3(0.37f, 0.16f, 0.37f));
            var centre = new Vector3(0f, trunkTop + radii.y * (style == 2 ? 0.55f : 0.7f), 0f);
            float averageRadius = (radii.x + radii.y + radii.z) / 3f;

            var mb = new MeshBuilder();
            float Bend(float y) => Mathf.Pow(Mathf.Clamp01(y / height), 1.6f);
            // Canopy weight: calm near the core, lively at the outer and upper shell.
            float Outerness(Vector3 c)
            {
                Vector3 local = c - centre;
                float horizontal = new Vector2(local.x / radii.x, local.z / radii.z).magnitude;
                float vertical = Mathf.Clamp01(local.y / radii.y);
                return Mathf.Clamp(0.15f + 0.65f * horizontal + 0.2f * vertical, 0.1f, 1f);
            }

            // Trunk with a flared base (anchored: bend 0 at the ground).
            mb.Wind = p => new Vector4(Bend(p.y), 0f, 0f, 0f);
            int bark = GenPalette.PickShade(rng, 3, v);
            System.Func<Vector3, Vector2> barkUv = n => GenPalette.UV(Ramp.Bark, bark + (n.y > 0.5f ? 1 : 0) + GenPalette.FaceJitter(rng, v));
            Shapes.Frustum(mb, Vector3.zero, Vector3.up, height * 0.06f, trunkRadius * 1.6f, trunkRadius, 7, barkUv, capTop: false);
            Shapes.Frustum(mb, new Vector3(0f, height * 0.06f, 0f), Vector3.up, centre.y - height * 0.06f, trunkRadius, trunkRadius * 0.55f, 7, barkUv, capTop: false);

            // Branches reaching up and out into the crown; each records its tip for a leaf cluster.
            int branchCount = style == 0 ? 5 : style == 1 ? 3 : 6;
            float yawOffset = Range(rng, 0f, 360f);
            var tips = new List<(Vector3 point, float outer, float phase)>();
            for (int b = 0; b < branchCount; b++)
            {
                float yaw = (yawOffset + b * 360f / branchCount + Range(rng, -18f, 18f)) * Mathf.Deg2Rad;
                float up = style == 0 ? Range(rng, 0.35f, 0.6f) : style == 1 ? Range(rng, 0.6f, 0.85f) : Range(rng, 0.1f, 0.3f);
                Vector3 towards = new Vector3(Mathf.Sin(yaw), up, Mathf.Cos(yaw)).normalized;
                Vector3 tip = centre + Vector3.Scale(towards, radii) * Range(rng, 0.62f, 0.78f);
                var start = new Vector3(0f, trunkTop * Range(rng, 0.75f, 0.95f), 0f);
                Vector3 span = tip - start;
                float length = span.magnitude;
                float tipOuter = Outerness(tip);
                float phase = (float)rng.NextDouble();
                // Branch wind blends from the trunk (0) to its tip cluster, so the cluster stays attached.
                mb.Wind = p =>
                {
                    float t = Mathf.Clamp01(Vector3.Dot(p - start, span) / (length * length));
                    return new Vector4(Bend(p.y), tipOuter * t * t, phase, 0f);
                };
                Shapes.Frustum(mb, start, span / length, length, trunkRadius * 0.5f, trunkRadius * 0.22f, 5, barkUv, capTop: true);
                tips.Add((tip, tipOuter, phase));
            }

            int leaf = GenPalette.PickShade(rng, 3, v);
            System.Func<Vector3, Vector2> LeafUv(Ramp ramp, int shade) => n =>
                GenPalette.UV(ramp, shade + (n.y > 0.45f ? 1 : n.y < -0.35f ? -1 : 0) + GenPalette.FaceJitter(rng, v));

            // One rigid leaf cluster: its own rotation, phase and canopy weight.
            void Cluster(Vector3 c, float radius, Vector3 squash, float outer, float phase, Ramp ramp, int shade, int subdivisions, float flutter)
            {
                float bend = Bend(c.y);
                mb.Wind = _ => new Vector4(bend, outer, phase, flutter);
                Quaternion rotation = Quaternion.Euler(Range(rng, -15f, 15f), Range(rng, 0f, 360f), Range(rng, -15f, 15f));
                Shapes.Blob(mb, rng, c, radius, squash, 0.1f, subdivisions, LeafUv(ramp, shade), p => rotation * p);
            }

            // Inner core: darker, cooler green, calm — fills the gaps between outer clusters for depth.
            Cluster(centre, 1f, radii * 0.72f, 0.12f, 0.5f, Ramp.PineGreen, leaf - 1, 1, 0.2f);

            foreach (var t in tips)
                Cluster(t.point + Vector3.up * averageRadius * 0.1f, averageRadius * Range(rng, 0.36f, 0.44f), new Vector3(1f, 0.8f, 1f),
                    t.outer, t.phase, Ramp.LeafGreen, leaf, detail >= 0.5f ? 1 : 0, 0.6f);

            // Shell clusters spread evenly (golden-angle spiral) over the ellipsoid, biased to the top.
            int shellCount = Mathf.RoundToInt(Mathf.Lerp(8f, style == 2 ? 18f : 16f, detail));
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < shellCount; i++)
            {
                float y = 1f - (i + 0.5f) / shellCount * 1.55f; // from the top down to just below the equator
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta = i * golden + yawOffset * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(theta) * ring, y, Mathf.Sin(theta) * ring);
                Vector3 c = centre + Vector3.Scale(direction, radii) * Range(rng, 0.82f, 0.98f);
                int shade = leaf + (y > 0.5f ? 2 : y > 0f ? 1 : 0) + GenPalette.FaceJitter(rng, v);
                int subdivisions = y > -0.1f && detail >= 0.6f ? 1 : 0; // smoother where the sun and camera see most
                Cluster(c, averageRadius * Range(rng, 0.3f, 0.42f), new Vector3(1f, Range(rng, 0.75f, 0.9f), 1f),
                    Outerness(c), (float)rng.NextDouble(), Ramp.LeafGreen, shade, subdivisions, 0.7f);
            }

            // Low-poly filler under the crown so it never looks hollow from below.
            int filler = Mathf.RoundToInt(Mathf.Lerp(2f, 5f, detail));
            for (int i = 0; i < filler; i++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                var direction = new Vector3(Mathf.Cos(angle), Range(rng, -0.6f, 0.2f), Mathf.Sin(angle)).normalized;
                Vector3 c = centre + Vector3.Scale(direction, radii) * 0.55f;
                Cluster(c, averageRadius * Range(rng, 0.3f, 0.4f), new Vector3(1f, 0.8f, 1f), Outerness(c) * 0.6f,
                    (float)rng.NextDouble(), Ramp.LeafGreen, leaf - 1, 0, 0.3f);
            }
            mb.Wind = null;

            return new GeneratedMesh
            {
                Mesh = mb.ToMesh(meshName),
                Collider = ColliderKind.TrunkCapsule,
                TrunkRadius = trunkRadius * 1.2f,
                TrunkHeight = trunkTop,
            };
        }

        /// <summary>
        /// The original (version 1) canopy — one main blob plus a few branch-tip blobs, no wind data.
        /// Kept only for the side-by-side comparison scene.
        /// </summary>
        public GeneratedMesh BuildV1(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            int style = variant % Styles.Length;
            float v = s.ColorVariation;

            float height = s.Size * (style == 0 ? 5.5f : style == 1 ? 7f : 5f) * Range(rng, 0.9f, 1.1f);
            float trunkTop = height * (style == 0 ? 0.45f : style == 1 ? 0.5f : 0.42f);
            float trunkRadius = height * 0.05f;
            var mb = new MeshBuilder();

            int bark = GenPalette.PickShade(rng, 3, v);
            System.Func<Vector3, Vector2> barkUv = n => GenPalette.UV(Ramp.Bark, bark + (n.y > 0.5f ? 1 : 0) + GenPalette.FaceJitter(rng, v));
            Shapes.Frustum(mb, Vector3.zero, Vector3.up, trunkTop, trunkRadius, trunkRadius * 0.65f, 7, barkUv, capTop: false);

            int branchCount = style == 0 ? 3 : style == 1 ? 2 : 4;
            float elevation = style == 0 ? 40f : style == 1 ? 65f : 22f;
            float branchLength = height * (style == 0 ? 0.22f : style == 1 ? 0.2f : 0.3f);
            var tips = new List<Vector3>();
            float yawOffset = Range(rng, 0f, 360f);
            for (int b = 0; b < branchCount; b++)
            {
                float yaw = yawOffset + b * 360f / branchCount + Range(rng, -20f, 20f);
                Vector3 dir = Quaternion.Euler(-(elevation + Range(rng, -8f, 8f)), yaw, 0f) * Vector3.forward;
                var start = new Vector3(0f, trunkTop * Range(rng, 0.8f, 0.95f), 0f);
                float length = branchLength * Range(rng, 0.85f, 1.15f);
                Shapes.Frustum(mb, start, dir, length, trunkRadius * 0.55f, trunkRadius * 0.28f, 5, barkUv, capTop: true);
                tips.Add(start + dir * length);
            }

            Vector3 blobScale = style == 0 ? new Vector3(1f, 0.85f, 1f) : style == 1 ? new Vector3(0.8f, 1.25f, 0.8f) : new Vector3(1.35f, 0.55f, 1.35f);
            float mainRadius = height * (style == 0 ? 0.26f : style == 1 ? 0.22f : 0.24f);
            var canopyCentre = new Vector3(0f, trunkTop + height * (style == 0 ? 0.18f : style == 1 ? 0.25f : 0.12f), 0f);

            int leaf = GenPalette.PickShade(rng, 3, v);
            System.Func<Vector3, Vector2> leafUv = n =>
                GenPalette.UV(Ramp.LeafGreen, leaf + (n.y > 0.45f ? 2 : n.y > 0f ? 1 : n.y < -0.35f ? -1 : 0) + GenPalette.FaceJitter(rng, v));

            Shapes.Blob(mb, rng, canopyCentre, mainRadius, blobScale, 0.12f, 1, leafUv);
            foreach (Vector3 tip in tips)
            {
                float r = mainRadius * Range(rng, 0.55f, 0.75f);
                Shapes.Blob(mb, rng, tip + Vector3.up * r * 0.3f, r, blobScale, 0.12f, 1, leafUv);
            }
            int extra = style == 2 ? 3 : 2;
            for (int e = 0; e < extra; e++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                var offset = new Vector3(Mathf.Cos(angle), Range(rng, -0.2f, 0.4f), Mathf.Sin(angle)) * mainRadius * 0.6f;
                Shapes.Blob(mb, rng, canopyCentre + Vector3.Scale(offset, blobScale), mainRadius * Range(rng, 0.5f, 0.7f), blobScale, 0.12f, 1, leafUv);
            }

            return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = ColliderKind.TrunkCapsule, TrunkRadius = trunkRadius * 1.2f, TrunkHeight = trunkTop };
        }
    }
}
