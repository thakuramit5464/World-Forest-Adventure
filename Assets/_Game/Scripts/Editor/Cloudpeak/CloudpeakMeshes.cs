using System.Collections.Generic;
using Greenhaven.Editor.AssetGenerator;
using UnityEngine;

namespace Greenhaven.Editor.Cloudpeak
{
    /// <summary>
    /// One-off meshes for the Cloudpeak scene on the shared palettes: the ruined summit arch, the shrine,
    /// the summit cairn and flag, low stone walls, the Cloud Feather pickup and distant mountain silhouettes.
    /// Pivots at the base centre; "front" is +Z.
    /// </summary>
    public static class CloudpeakMeshes
    {
        static System.Func<Vector3, Vector2> Stone(System.Random rng, int shade) => n => VillagePalette.UV(VRamp.Stone, shade + Palette.SlopeShade(n) + Palette.FaceJitter(rng, 0.5f));

        /// <summary>Ruined arch: two block pillars (one broken) and a partly fallen ring of voussoirs. Gap along X, walk through along Z.</summary>
        public static Mesh Arch(int seed, string name, out Bounds[] colliders)
        {
            var rng = new System.Random(seed ^ 0xA2C4);
            var mb = new MeshBuilder();
            const float gap = 3.0f, pillar = 0.75f, pillarHeight = 3.0f;
            float px = gap * 0.5f + pillar * 0.5f;
            var boxes = new List<Bounds>();
            foreach (float side in new[] { -1f, 1f })
            {
                bool broken = side > 0f;
                int blocks = broken ? 3 : 5;
                float y = 0f, blockHeight = pillarHeight / 5f;
                for (int b = 0; b < blocks; b++)
                {
                    float w = pillar * (b == 0 ? 1.15f : 1f) * (1f + (float)rng.NextDouble() * 0.06f);
                    var offset = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.08f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.08f);
                    Shapes.Box(mb, new Vector3(side * px, y + blockHeight * 0.5f, 0f) + offset, new Vector3(w, blockHeight * 0.96f, w),
                        Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 8f, 0f), Stone(rng, 4));
                    y += blockHeight;
                }
                boxes.Add(new Bounds(new Vector3(side * px, y * 0.5f, 0f), new Vector3(pillar * 1.15f, y, pillar * 1.15f)));
            }

            // Arch ring from the intact pillar over the gap; the last voussoirs have fallen.
            const int voussoirs = 9;
            float radius = px;
            for (int v = 0; v < voussoirs; v++)
            {
                if (v >= voussoirs - 3) continue; // missing on the broken side
                float a = Mathf.PI * (1f - (v + 0.5f) / voussoirs);
                var centre = new Vector3(Mathf.Cos(a) * radius, pillarHeight + Mathf.Sin(a) * radius * 0.85f, 0f);
                Quaternion tilt = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
                Shapes.Box(mb, centre, new Vector3(0.5f, 0.62f, pillar * 0.95f), tilt, Stone(rng, 5));
            }
            // Fallen blocks at the foot of the broken pillar.
            Shapes.Box(mb, new Vector3(px + 1.1f, 0.28f, 0.9f), new Vector3(0.6f, 0.55f, 0.65f), Quaternion.Euler(8f, 35f, 0f), Stone(rng, 3));
            Shapes.Box(mb, new Vector3(px + 0.4f, 0.24f, -1.2f), new Vector3(0.5f, 0.48f, 0.6f), Quaternion.Euler(-6f, -20f, 12f), Stone(rng, 3));
            // Moss on the crown.
            Shapes.Box(mb, new Vector3(-px, pillarHeight + 0.05f, 0f), new Vector3(pillar * 0.9f, 0.08f, pillar * 0.9f), GenUtil.VillageFlat(VRamp.Tunic, 3));
            colliders = boxes.ToArray();
            return mb.ToMesh(name);
        }

        /// <summary>Small stone shrine: stepped plinth, four posts, pyramid roof and a glowing crystal.</summary>
        public static Mesh Shrine(int seed, string name, out Bounds collider)
        {
            var rng = new System.Random(seed ^ 0x5481);
            var mb = new MeshBuilder();
            Shapes.Box(mb, new Vector3(0f, 0.15f, 0f), new Vector3(1.9f, 0.3f, 1.9f), Stone(rng, 3));
            Shapes.Box(mb, new Vector3(0f, 0.42f, 0f), new Vector3(1.4f, 0.24f, 1.4f), Stone(rng, 4));
            foreach (float x in new[] { -0.55f, 0.55f })
                foreach (float z in new[] { -0.55f, 0.55f })
                    Shapes.Box(mb, new Vector3(x, 1.1f, z), new Vector3(0.16f, 1.15f, 0.16f), Stone(rng, 5));
            // Pyramid roof.
            var top = new Vector3(0f, 2.25f, 0f);
            Vector3[] eaves = { new Vector3(-0.8f, 1.68f, -0.8f), new Vector3(0.8f, 1.68f, -0.8f), new Vector3(0.8f, 1.68f, 0.8f), new Vector3(-0.8f, 1.68f, 0.8f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = eaves[i], b = eaves[(i + 1) % 4];
                Vector3 outward = (a + b) * 0.5f - new Vector3(0f, 1.9f, 0f);
                mb.Triangle(a, b, top, outward, VillagePalette.UV(VRamp.RoofBlue, 4 + (i % 2)));
                mb.Triangle(a, b, new Vector3(0f, 1.68f, 0f), Vector3.down, VillagePalette.UV(VRamp.RoofBlue, 2));
            }
            // Crystal on a pedestal.
            Shapes.Box(mb, new Vector3(0f, 0.75f, 0f), new Vector3(0.3f, 0.42f, 0.3f), Stone(rng, 6));
            Shapes.Blob(mb, rng, new Vector3(0f, 1.17f, 0f), 0.2f, new Vector3(0.8f, 1.5f, 0.8f), 0.05f, 0, n => VillagePalette.UV(VRamp.Window, n.y > 0f ? 7 : 6));
            collider = new Bounds(new Vector3(0f, 1.1f, 0f), new Vector3(1.9f, 2.2f, 1.9f));
            return mb.ToMesh(name);
        }

        /// <summary>Cairn of stacked flat stones with a tall flagpole; the banner is a separate swinging mesh.</summary>
        public static Mesh Cairn(int seed, string name, out float poleTop)
        {
            var rng = new System.Random(seed ^ 0xCA19);
            var mb = new MeshBuilder();
            float y = 0f;
            for (int i = 0; i < 6; i++)
            {
                float w = 1.3f - i * 0.17f, h = 0.22f + (float)rng.NextDouble() * 0.08f;
                Shapes.Box(mb, new Vector3(((float)rng.NextDouble() - 0.5f) * 0.1f, y + h * 0.5f, ((float)rng.NextDouble() - 0.5f) * 0.1f),
                    new Vector3(w, h, w * 0.9f), Quaternion.Euler(0f, (float)rng.NextDouble() * 60f, 0f), Stone(rng, 3 + i % 3));
                y += h * 0.95f;
            }
            poleTop = y + 4.6f;
            Shapes.Cylinder(mb, new Vector3(0f, y - 0.2f, 0f), new Vector3(0f, poleTop, 0f), 0.06f, 6, GenUtil.Village(VRamp.Wood, 4));
            Shapes.Box(mb, new Vector3(0f, poleTop + 0.06f, 0f), new Vector3(0.16f, 0.12f, 0.16f), GenUtil.VillageFlat(VRamp.Window, 6));
            return mb.ToMesh(name);
        }

        /// <summary>Pennant hanging from its hinge (pivot) along local +X, for <see cref="Greenhaven.Wind.WindSwing"/>.</summary>
        public static Mesh Banner(string name)
        {
            var mb = new MeshBuilder();
            Vector2 red = VillagePalette.UV(VRamp.Accent, 4), redBack = VillagePalette.UV(VRamp.Accent, 3);
            Vector2 gold = VillagePalette.UV(VRamp.Window, 6);
            // Pennant hangs below a short top bar so it swings about the bar.
            mb.DoubleSided(new Vector3(0.05f, 0f, 0f), new Vector3(1.4f, -0.12f, 0f), new Vector3(0.05f, -0.9f, 0f), Vector3.back, red, redBack);
            mb.DoubleSided(new Vector3(1.4f, -0.12f, 0f), new Vector3(1.2f, -0.5f, 0f), new Vector3(0.05f, -0.9f, 0f), Vector3.back, red, redBack);
            Shapes.Box(mb, new Vector3(0.75f, 0.02f, 0f), new Vector3(1.5f, 0.06f, 0.06f), GenUtil.VillageFlat(VRamp.Wood, 4));
            Shapes.Box(mb, new Vector3(0.45f, -0.4f, 0f), new Vector3(0.25f, 0.25f, 0.03f), Quaternion.Euler(0f, 0f, 45f), _ => gold);
            return mb.ToMesh(name);
        }

        /// <summary>Low dry-stone wall along local X, <paramref name="length"/> long.</summary>
        public static Mesh Wall(int seed, string name, float length, out Bounds collider)
        {
            var rng = new System.Random(seed ^ 0x3A11);
            var mb = new MeshBuilder();
            int stones = Mathf.Max(2, Mathf.RoundToInt(length / 0.55f));
            float w = length / stones;
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < stones - row; i++)
                {
                    float x = -length * 0.5f + w * (i + 0.5f + row * 0.5f);
                    float h = 0.3f + (float)rng.NextDouble() * 0.08f;
                    Shapes.Box(mb, new Vector3(x, row * 0.32f + h * 0.5f, 0f), new Vector3(w * 0.95f, h, 0.5f - row * 0.08f),
                        Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 10f, 0f), Stone(rng, 3 + row));
                }
            collider = new Bounds(new Vector3(0f, 0.45f, 0f), new Vector3(length, 0.9f, 0.5f));
            return mb.ToMesh(name);
        }

        /// <summary>A soft cloud-white feather with a sky-blue tip (collectible visual).</summary>
        public static Mesh Feather(string name)
        {
            var mb = new MeshBuilder();
            Vector2 white = VillagePalette.UV(VRamp.Plaster, 7), whiteBack = VillagePalette.UV(VRamp.Plaster, 6);
            Vector2 blue = VillagePalette.UV(VRamp.RoofBlue, 6);
            var points = new[]
            {
                new Vector3(0f, -0.3f, 0f), new Vector3(0.09f, -0.1f, 0f), new Vector3(0.12f, 0.12f, 0f), new Vector3(0.06f, 0.3f, 0f),
                new Vector3(0f, 0.38f, 0f), new Vector3(-0.06f, 0.3f, 0f), new Vector3(-0.12f, 0.12f, 0f), new Vector3(-0.09f, -0.1f, 0f),
            };
            var centre = new Vector3(0f, 0.06f, 0f);
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 a = points[i], b = points[(i + 1) % points.Length];
                bool tip = a.y > 0.2f && b.y > 0.2f;
                mb.DoubleSided(centre, b, a, Vector3.back, tip ? blue : white, tip ? blue : whiteBack);
            }
            Shapes.Box(mb, new Vector3(0f, -0.36f, 0f), new Vector3(0.02f, 0.14f, 0.02f), GenUtil.VillageFlat(VRamp.Wood, 5));
            return mb.ToMesh(name);
        }

        /// <summary>Ring of large, faceted, snow-capped peaks far outside the playable area (no collision).</summary>
        public static Mesh DistantPeaks(int seed, string name, float halfSize)
        {
            var rng = new System.Random(seed ^ 0xD157);
            var mb = new MeshBuilder();
            const int peaks = 11;
            for (int i = 0; i < peaks; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.6f) / peaks * Mathf.PI * 2f;
                float dist = halfSize * (1.75f + (float)rng.NextDouble() * 0.7f);
                float height = halfSize * (0.55f + (float)rng.NextDouble() * 0.6f) * (Mathf.Sin(a) > 0f ? 1.3f : 0.8f); // taller behind the mountain (north)
                float radius = height * (0.9f + (float)rng.NextDouble() * 0.4f);
                var basePos = new Vector3(Mathf.Cos(a) * dist, -2f, Mathf.Sin(a) * dist);
                const int seg = 7;
                float twist = (float)rng.NextDouble() * 6f;
                var rings = new[] { 0f, 0.45f, 0.78f, 1f };
                var ringPoints = new Vector3[rings.Length, seg];
                for (int r = 0; r < rings.Length; r++)
                    for (int k = 0; k < seg; k++)
                    {
                        float ang = twist + k * Mathf.PI * 2f / seg;
                        float rr = radius * (1f - rings[r]) * (0.8f + (float)rng.NextDouble() * 0.35f);
                        ringPoints[r, k] = basePos + new Vector3(Mathf.Cos(ang) * rr, height * rings[r], Mathf.Sin(ang) * rr);
                    }
                for (int r = 0; r < rings.Length - 1; r++)
                    for (int k = 0; k < seg; k++)
                    {
                        Vector3 p0 = ringPoints[r, k], p1 = ringPoints[r, (k + 1) % seg], p2 = ringPoints[r + 1, k], p3 = ringPoints[r + 1, (k + 1) % seg];
                        Vector3 outward = (p0 + p1) * 0.5f - (basePos + Vector3.up * height * rings[r]);
                        Vector2 uv = r == 0 ? GenPalette.UV(Ramp.PineGreen, 2) : r == 1 ? GenPalette.UV(Ramp.RockGrey, 3 + k % 2) : GenPalette.UV(Ramp.Petal, 4 + k % 2);
                        if (r < rings.Length - 2)
                        {
                            mb.Triangle(p0, p1, p2, outward, uv);
                            mb.Triangle(p2, p1, p3, outward, uv);
                        }
                        else
                        {
                            // Top ring collapses to the summit point (avoid degenerate triangles).
                            Vector3 apex = basePos + Vector3.up * height;
                            mb.Triangle(p0, p1, apex, outward, uv);
                        }
                    }
            }
            return mb.ToMesh(name);
        }
    }
}
