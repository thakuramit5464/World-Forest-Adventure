using System.Collections.Generic;
using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Stylised broadleaf trees: tapered trunk, a few angled branches, and clustered faceted
    /// canopy blobs. Three silhouettes (Round, Tall, Umbrella) cycle by variant. About 450–650 triangles.
    /// </summary>
    public sealed class BroadleafTreeGenerator : IAssetGenerator
    {
        static readonly string[] Styles = { "Round", "Tall", "Umbrella" };

        public AssetCategory Category => AssetCategory.BroadleafTree;
        public string Prefix => "Broadleaf";
        public string DisplayName => "Broadleaf Trees";
        public string StyleName(int variant) => Styles[variant % Styles.Length];

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
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

            // Branches fan out from near the trunk top; each ends in a canopy blob.
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
            {
                int shade = leaf + (n.y > 0.45f ? 2 : n.y > 0f ? 1 : n.y < -0.35f ? -1 : 0) + GenPalette.FaceJitter(rng, v);
                return GenPalette.UV(Ramp.LeafGreen, shade);
            };

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

            return new GeneratedMesh
            {
                Mesh = mb.ToMesh(meshName),
                Collider = ColliderKind.TrunkCapsule,
                TrunkRadius = trunkRadius * 1.2f,
                TrunkHeight = trunkTop,
            };
        }
    }
}
