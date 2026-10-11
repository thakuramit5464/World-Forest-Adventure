using System.Collections.Generic;
using UnityEngine;
using static Greenhaven.Editor.AssetGenerator.GenRandom;
using static Greenhaven.Editor.AssetGenerator.GenUtil;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Village props as single meshes on the village palette: well, bridge, fence, gate, bench,
    /// lantern, signpost, barrel, crate and planter. Pivot at the base centre; "front" is +Z
    /// (bench seat side, gate opening axis, bridge span along Z).
    /// </summary>
    public sealed class PropGenerator : IAssetGenerator
    {
        readonly AssetCategory kind;

        public PropGenerator(AssetCategory kind) => this.kind = kind;

        public AssetCategory Category => kind;
        public string Prefix => kind.ToString();
        public string DisplayName => kind switch
        {
            AssetCategory.Well => "Stone Well",
            AssetCategory.Bridge => "Wooden Bridge",
            AssetCategory.Fence => "Fence Sections",
            AssetCategory.Gate => "Gates",
            AssetCategory.Bench => "Benches",
            AssetCategory.Lantern => "Lanterns",
            AssetCategory.Signpost => "Signposts",
            AssetCategory.Barrel => "Barrels",
            AssetCategory.Crate => "Crates",
            _ => "Flower Planters",
        };
        public string Group => "Props";
        public int DefaultCount => kind == AssetCategory.Well || kind == AssetCategory.Bridge || kind == AssetCategory.Gate ? 1 : 2;
        public Palette Palette => VillagePalette.Palette;

        public string StyleName(int variant) => kind switch
        {
            AssetCategory.Fence => variant % 2 == 0 ? "Rail" : "Picket",
            AssetCategory.Lantern => variant % 2 == 0 ? "Arm" : "Post-top",
            AssetCategory.Signpost => variant % 2 == 0 ? "Two arrows" : "Three arrows",
            _ => $"Variant {variant}",
        };

        /// <summary>Nominal bridge span along Z (used by the village layout).</summary>
        public const float BridgeLength = 6f;
        /// <summary>Nominal fence section length along X.</summary>
        public const float FenceLength = 2f;

        public GeneratedMesh Build(GeneratorSettings s, int variant, string meshName)
        {
            System.Random rng = For(s.Seed, Category, variant);
            var mb = new MeshBuilder();
            var boxes = new List<Bounds>();
            ColliderKind collider = ColliderKind.Boxes;

            switch (kind)
            {
                case AssetCategory.Well: Well(mb, rng, boxes); break;
                case AssetCategory.Bridge: Bridge(mb, rng); collider = ColliderKind.Mesh; break;
                case AssetCategory.Fence: Fence(mb, variant, boxes); break;
                case AssetCategory.Gate: Gate(mb, boxes); break;
                case AssetCategory.Bench: Bench(mb, boxes); break;
                case AssetCategory.Lantern: Lantern(mb, variant, boxes); break;
                case AssetCategory.Signpost: Signpost(mb, rng, variant, boxes); break;
                case AssetCategory.Barrel: Barrel(mb, rng, boxes); break;
                case AssetCategory.Crate: Crate(mb, rng, boxes); break;
                default: Planter(mb, rng, boxes); break;
            }

            return new GeneratedMesh { Mesh = mb.ToMesh(meshName), Collider = collider, Boxes = boxes.ToArray() };
        }

        // ───────────────────────────── Props ─────────────────────────────

        static void Well(MeshBuilder mb, System.Random rng, List<Bounds> boxes)
        {
            const float outer = 0.95f, inner = 0.72f, wallTop = 0.8f;
            const int seg = 10;
            var stone = Village(VRamp.Stone, 4);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(Vector3.zero, outer, outer, stone),
                new Shapes.Ring(new Vector3(0f, wallTop, 0f), outer, outer, stone),
            }, seg, false, false);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 0.45f, 0f), inner, inner, Village(VRamp.Stone, 2)),
                new Shapes.Ring(new Vector3(0f, wallTop, 0f), inner, inner, Village(VRamp.Stone, 2)),
            }, seg, false, false, inward: true);
            // Rim (annulus) and water surface.
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                Vector3 o0 = new Vector3(Mathf.Cos(a0) * outer, wallTop, Mathf.Sin(a0) * outer);
                Vector3 o1 = new Vector3(Mathf.Cos(a1) * outer, wallTop, Mathf.Sin(a1) * outer);
                Vector3 i0 = new Vector3(Mathf.Cos(a0) * inner, wallTop, Mathf.Sin(a0) * inner);
                Vector3 i1 = new Vector3(Mathf.Cos(a1) * inner, wallTop, Mathf.Sin(a1) * inner);
                Vector2 rim = VillagePalette.UV(VRamp.Stone, 5 + Palette.FaceJitter(rng, 0.5f));
                mb.Triangle(o0, o1, i0, Vector3.up, rim);
                mb.Triangle(i0, o1, i1, Vector3.up, rim);
                mb.Triangle(i0 + Vector3.down * 0.3f, i1 + Vector3.down * 0.3f, new Vector3(0f, wallTop - 0.3f, 0f), Vector3.up,
                    VillagePalette.UV(VRamp.Water, 4));
            }
            // Posts, crossbeam, little roof, bucket and rope.
            var wood = Village(VRamp.Wood, 3);
            foreach (float x in new[] { -0.85f, 0.85f })
                Shapes.Box(mb, new Vector3(x, 1.05f, 0f), new Vector3(0.14f, 1.9f, 0.14f), wood);
            Shapes.Box(mb, new Vector3(0f, 1.85f, 0f), new Vector3(1.9f, 0.12f, 0.12f), wood);
            BuildingParts.GableRoof(mb, Vector3.zero, 1.9f, 1.3f, 1.95f, 0.45f, 0.2f, 0.15f, 0.08f, VRamp.RoofRed, 4);
            Shapes.Box(mb, new Vector3(0f, 1.5f, 0f), new Vector3(0.02f, 0.65f, 0.02f), VillageFlat(VRamp.Dark, 3));
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(new Vector3(0f, 1.0f, 0f), 0.11f, 0.11f, Village(VRamp.Wood, 4)),
                new Shapes.Ring(new Vector3(0f, 1.2f, 0f), 0.13f, 0.13f, Village(VRamp.Wood, 4)),
            }, 8, true, true);

            boxes.Add(Box(new Vector3(0f, 0.45f, 0f), new Vector3(2f * outer, 0.9f, 2f * outer)));
            boxes.Add(Box(new Vector3(-0.85f, 1.05f, 0f), new Vector3(0.2f, 2.1f, 0.2f)));
            boxes.Add(Box(new Vector3(0.85f, 1.05f, 0f), new Vector3(0.2f, 2.1f, 0.2f)));
        }

        static void Bridge(MeshBuilder mb, System.Random rng)
        {
            const float length = BridgeLength, width = 2.4f, arch = 0.5f, baseY = 0.2f;
            const int planks = 14;
            float DeckY(float t) => baseY + arch * Mathf.Sin(Mathf.PI * t);
            float Slope(float t) => Mathf.Atan(arch * Mathf.PI * Mathf.Cos(Mathf.PI * t) / length) * Mathf.Rad2Deg;
            float segment = length / planks;

            for (int i = 0; i < planks; i++)
            {
                float t = (i + 0.5f) / planks;
                float z = -length * 0.5f + t * length;
                Quaternion tilt = Quaternion.Euler(-Slope(t), 0f, 0f);
                int shade = 4 + (i % 2) + Palette.FaceJitter(rng, 0.4f);
                Shapes.Box(mb, new Vector3(0f, DeckY(t) - 0.04f, z), new Vector3(width, 0.08f, segment * 0.92f), tilt, Village(VRamp.Wood, shade));
                foreach (float x in new[] { -width * 0.5f - 0.02f, width * 0.5f + 0.02f })
                    Shapes.Box(mb, new Vector3(x, DeckY(t) - 0.12f, z), new Vector3(0.14f, 0.16f, segment * 1.02f), tilt, VillageFlat(VRamp.TimberDark, 3));
            }

            // Railings: posts and sloping handrails along both sides.
            float[] postT = { 0.02f, 0.26f, 0.5f, 0.74f, 0.98f };
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3? previousTop = null;
                foreach (float t in postT)
                {
                    float z = -length * 0.5f + t * length;
                    float y = DeckY(t);
                    var post = new Vector3(side * width * 0.5f, y + 0.42f, z);
                    Shapes.Box(mb, post, new Vector3(0.1f, 0.85f, 0.1f), Village(VRamp.Wood, 3));
                    Vector3 topPoint = new Vector3(post.x, y + 0.82f, z);
                    if (previousTop.HasValue)
                    {
                        Vector3 a = previousTop.Value, b = topPoint;
                        Shapes.Box(mb, (a + b) * 0.5f, new Vector3(0.08f, 0.08f, Vector3.Distance(a, b) + 0.06f),
                            Quaternion.LookRotation(b - a), Village(VRamp.Wood, 5));
                    }
                    previousTop = topPoint;
                }
            }
        }

        static void Fence(MeshBuilder mb, int variant, List<Bounds> boxes)
        {
            const float half = FenceLength * 0.5f;
            var post = Village(VRamp.Wood, 3);
            foreach (float x in new[] { -half, half })
            {
                Shapes.Box(mb, new Vector3(x, 0.5f, 0f), new Vector3(0.12f, 1f, 0.12f), post);
                Shapes.Box(mb, new Vector3(x, 1.02f, 0f), new Vector3(0.1f, 0.1f, 0.1f), Quaternion.Euler(45f, 0f, 45f), post);
            }
            var rail = Village(VRamp.Wood, 4);
            Shapes.Box(mb, new Vector3(0f, 0.35f, 0f), new Vector3(FenceLength, 0.08f, 0.05f), rail);
            Shapes.Box(mb, new Vector3(0f, 0.75f, 0f), new Vector3(FenceLength, 0.08f, 0.05f), rail);
            if (variant % 2 == 1)
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = -0.75f + i * 0.25f;
                    Shapes.Box(mb, new Vector3(x, 0.45f, 0.045f), new Vector3(0.09f, 0.85f, 0.03f), Village(VRamp.Plaster, 5));
                    Shapes.Box(mb, new Vector3(x, 0.88f, 0.045f), new Vector3(0.065f, 0.065f, 0.03f), Quaternion.Euler(0f, 0f, 45f), Village(VRamp.Plaster, 5));
                }
            }
            boxes.Add(Box(new Vector3(0f, 0.5f, 0f), new Vector3(FenceLength, 1f, 0.15f)));
        }

        static void Gate(MeshBuilder mb, List<Bounds> boxes)
        {
            // Two tall posts with a crossbeam; both leaves swung open so the path passes through (along Z).
            var wood = Village(VRamp.Wood, 3);
            foreach (float x in new[] { -1f, 1f })
            {
                Shapes.Box(mb, new Vector3(x, 1f, 0f), new Vector3(0.18f, 2f, 0.18f), wood);
                boxes.Add(Box(new Vector3(x, 1f, 0f), new Vector3(0.22f, 2f, 0.22f)));
            }
            Shapes.Box(mb, new Vector3(0f, 1.95f, 0f), new Vector3(2.3f, 0.14f, 0.16f), Village(VRamp.Wood, 4));
            Shapes.Box(mb, new Vector3(0f, 2.06f, 0f), new Vector3(2.5f, 0.08f, 0.24f), VillageFlat(VRamp.TimberDark, 3));

            foreach (float side in new[] { -1f, 1f })
            {
                var hinge = new Vector3(side * 0.9f, 0f, 0f);
                Quaternion swing = Quaternion.Euler(0f, side * -100f, 0f); // swung fully open, clear of the opening
                Vector3 P(float x, float y) => hinge + swing * new Vector3(-side * x, y, 0f);
                var slat = Village(VRamp.Wood, 5);
                Shapes.Box(mb, P(0.42f, 0.3f), new Vector3(0.85f, 0.07f, 0.05f), swing, slat);
                Shapes.Box(mb, P(0.42f, 0.85f), new Vector3(0.85f, 0.07f, 0.05f), swing, slat);
                for (int i = 0; i < 4; i++)
                    Shapes.Box(mb, P(0.12f + i * 0.2f, 0.58f), new Vector3(0.08f, 0.75f, 0.03f), swing, Village(VRamp.Wood, 4));
                Shapes.Box(mb, P(0.42f, 0.58f), new Vector3(0.06f, 0.95f, 0.035f), swing * Quaternion.Euler(0f, 0f, side * 50f), VillageFlat(VRamp.TimberDark, 3));
            }
        }

        static void Bench(MeshBuilder mb, List<Bounds> boxes)
        {
            var plank = Village(VRamp.Wood, 4);
            foreach (float z in new[] { -0.13f, 0f, 0.13f })
                Shapes.Box(mb, new Vector3(0f, 0.45f, z), new Vector3(1.6f, 0.05f, 0.12f), plank);
            Quaternion lean = Quaternion.Euler(-10f, 0f, 0f);
            foreach (float y in new[] { 0.64f, 0.82f })
                Shapes.Box(mb, new Vector3(0f, y, -0.25f - (y - 0.64f) * 0.18f), new Vector3(1.6f, 0.1f, 0.04f), lean, plank);
            var iron = VillageFlat(VRamp.Dark, 2);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                Shapes.Box(mb, new Vector3(x, 0.225f, 0f), new Vector3(0.07f, 0.45f, 0.42f), iron);
                Shapes.Box(mb, new Vector3(x, 0.66f, -0.27f), new Vector3(0.06f, 0.48f, 0.05f), lean, iron);
                Shapes.Box(mb, new Vector3(x * 1.07f, 0.62f, -0.02f), new Vector3(0.06f, 0.06f, 0.45f), iron);
            }
            boxes.Add(Box(new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.5f, 0.5f)));
        }

        static void Lantern(MeshBuilder mb, int variant, List<Bounds> boxes)
        {
            var iron = Village(VRamp.Dark, 2);
            Shapes.Box(mb, new Vector3(0f, 0.075f, 0f), new Vector3(0.26f, 0.15f, 0.26f), Village(VRamp.Stone, 3));
            Shapes.Cylinder(mb, Vector3.zero, new Vector3(0f, 2f, 0f), 0.055f, 8, iron);

            Vector3 lamp;
            if (variant % 2 == 0)
            {
                Shapes.Box(mb, new Vector3(0f, 1.95f, 0.22f), new Vector3(0.05f, 0.05f, 0.48f), iron);
                Shapes.Box(mb, new Vector3(0f, 1.86f, 0.42f), new Vector3(0.02f, 0.14f, 0.02f), iron);
                lamp = new Vector3(0f, 1.62f, 0.42f);
            }
            else
            {
                lamp = new Vector3(0f, 2.17f, 0f);
            }

            // Lamp: glass core, corner bars, plates, pyramid cap.
            Shapes.Box(mb, lamp, new Vector3(0.19f, 0.26f, 0.19f), VillageFlat(VRamp.Window, 7));
            foreach (float x in new[] { -0.105f, 0.105f })
                foreach (float z in new[] { -0.105f, 0.105f })
                    Shapes.Box(mb, lamp + new Vector3(x, 0f, z), new Vector3(0.03f, 0.3f, 0.03f), iron);
            Shapes.Box(mb, lamp + new Vector3(0f, -0.15f, 0f), new Vector3(0.25f, 0.03f, 0.25f), iron);
            Shapes.Box(mb, lamp + new Vector3(0f, 0.15f, 0f), new Vector3(0.25f, 0.03f, 0.25f), iron);
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(lamp + new Vector3(0f, 0.16f, 0f), 0.2f, 0.2f, iron),
                new Shapes.Ring(lamp + new Vector3(0f, 0.32f, 0f), 0.01f, 0.01f, iron),
            }, 4, true, false, Mathf.PI * 0.25f);

            boxes.Add(Box(new Vector3(0f, 1.05f, 0f), new Vector3(0.26f, 2.1f, 0.26f)));
        }

        static void Signpost(MeshBuilder mb, System.Random rng, int variant, List<Bounds> boxes)
        {
            Shapes.Box(mb, new Vector3(0f, 0.95f, 0f), new Vector3(0.12f, 1.9f, 0.12f), Village(VRamp.Wood, 3));
            Shapes.Box(mb, new Vector3(0f, 1.93f, 0f), new Vector3(0.16f, 0.06f, 0.16f), Village(VRamp.Wood, 2));
            int arrows = variant % 2 == 0 ? 2 : 3;
            for (int i = 0; i < arrows; i++)
            {
                float yaw = (i * 137f + Range(rng, -15f, 15f)) % 360f;
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                float y = 1.65f - i * 0.26f;
                int shade = 4 + (i % 2);
                Shapes.Box(mb, new Vector3(0f, y, 0f) + rot * new Vector3(0.42f, 0f, 0f), new Vector3(0.7f, 0.18f, 0.04f), rot, Village(VRamp.Wood, shade));
                // Arrow tip: a square turned 45 degrees.
                Shapes.Box(mb, new Vector3(0f, y, 0f) + rot * new Vector3(0.77f, 0f, 0f), new Vector3(0.13f, 0.13f, 0.04f),
                    rot * Quaternion.Euler(0f, 0f, 45f), Village(VRamp.Wood, shade));
            }
            boxes.Add(Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.16f, 1.9f, 0.16f)));
        }

        static void Barrel(MeshBuilder mb, System.Random rng, List<Bounds> boxes)
        {
            float s = Range(rng, 0.9f, 1.1f);
            System.Func<Vector3, Vector2> stave = n => VillagePalette.UV(VRamp.Wood, 4 + Palette.SlopeShade(n) + Palette.FaceJitter(rng, 0.8f));
            Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
            {
                new Shapes.Ring(Vector3.zero, 0.3f * s, 0.3f * s, stave),
                new Shapes.Ring(new Vector3(0f, 0.18f * s, 0f), 0.345f * s, 0.345f * s, stave),
                new Shapes.Ring(new Vector3(0f, 0.45f * s, 0f), 0.37f * s, 0.37f * s, stave),
                new Shapes.Ring(new Vector3(0f, 0.72f * s, 0f), 0.345f * s, 0.345f * s, stave),
                new Shapes.Ring(new Vector3(0f, 0.9f * s, 0f), 0.3f * s, 0.3f * s, Village(VRamp.Wood, 3)),
            }, 10, false, true);
            foreach (float y in new[] { 0.15f, 0.75f })
                Shapes.Loft(mb, Vector3.right, Vector3.forward, new[]
                {
                    new Shapes.Ring(new Vector3(0f, (y - 0.03f) * s, 0f), 0.352f * s, 0.352f * s, VillageFlat(VRamp.Dark, 3)),
                    new Shapes.Ring(new Vector3(0f, (y + 0.03f) * s, 0f), 0.352f * s, 0.352f * s, VillageFlat(VRamp.Dark, 3)),
                }, 10, false, false);
            boxes.Add(Box(new Vector3(0f, 0.45f * s, 0f), new Vector3(0.7f * s, 0.9f * s, 0.7f * s)));
        }

        static void Crate(MeshBuilder mb, System.Random rng, List<Bounds> boxes)
        {
            float s = 0.7f * Range(rng, 0.85f, 1.15f);
            float h = s * 0.5f;
            Shapes.Box(mb, new Vector3(0f, h, 0f), Vector3.one * s, Village(VRamp.Wood, 5));
            var edge = VillageFlat(VRamp.TimberDark, 4);
            const float t = 0.07f;
            foreach (float a in new[] { -1f, 1f })
                foreach (float b in new[] { -1f, 1f })
                {
                    Shapes.Box(mb, new Vector3(0f, h + a * (h - t * 0.5f), b * (h - t * 0.5f)), new Vector3(s + 0.01f, t, t), edge);
                    Shapes.Box(mb, new Vector3(a * (h - t * 0.5f), h + b * (h - t * 0.5f), 0f), new Vector3(t, t, s + 0.01f), edge);
                    Shapes.Box(mb, new Vector3(a * (h - t * 0.5f), h, b * (h - t * 0.5f)), new Vector3(t, s + 0.01f, t), edge);
                }
            // Diagonal braces on the front and one side.
            Shapes.Box(mb, new Vector3(0f, h, h + 0.005f), new Vector3(t, s * 1.3f, 0.02f), Quaternion.Euler(0f, 0f, 45f), edge);
            Shapes.Box(mb, new Vector3(h + 0.005f, h, 0f), new Vector3(0.02f, s * 1.3f, t), Quaternion.Euler(45f, 0f, 0f), edge);
            boxes.Add(Box(new Vector3(0f, h, 0f), Vector3.one * s));
        }

        static void Planter(MeshBuilder mb, System.Random rng, List<Bounds> boxes)
        {
            Shapes.Box(mb, new Vector3(0f, 0.2f, 0f), new Vector3(1.2f, 0.4f, 0.5f), Village(VRamp.Wood, 4));
            Shapes.Box(mb, new Vector3(0f, 0.41f, 0f), new Vector3(1.28f, 0.05f, 0.58f), Village(VRamp.Wood, 2));
            Shapes.Box(mb, new Vector3(0f, 0.43f, 0f), new Vector3(1.1f, 0.04f, 0.4f), VillageFlat(VRamp.Leather, 1));
            for (int i = 0; i < 3; i++)
                Shapes.Blob(mb, rng, new Vector3(-0.38f + i * 0.38f, 0.5f, 0f), 0.17f, new Vector3(1.1f, 0.75f, 0.9f), 0.15f, 0,
                    n => VillagePalette.UV(VRamp.Tunic, 3 + Palette.SlopeShade(n)));
            VRamp[] colours = { VRamp.Accent, VRamp.Window, VRamp.Plaster };
            for (int i = 0; i < 7; i++)
            {
                VRamp c = colours[i % colours.Length];
                var p = new Vector3(Range(rng, -0.5f, 0.5f), Range(rng, 0.6f, 0.7f), Range(rng, -0.15f, 0.15f));
                Shapes.Blob(mb, rng, p, 0.065f, Vector3.one, 0.1f, 0, _ => VillagePalette.UV(c, c == VRamp.Plaster ? 7 : 5));
            }
            boxes.Add(Box(new Vector3(0f, 0.225f, 0f), new Vector3(1.28f, 0.45f, 0.58f)));
        }
    }
}
