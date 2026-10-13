using System.Collections.Generic;
using System.Linq;
using System.Text;
using Greenhaven.Editor.AssetGenerator;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.House
{
    /// <summary>
    /// The rural courtyard house from the owner's reference sheet ("Rural Courtyard House Reference Sheet.png"), built
    /// procedurally as editable modules: a terracotta section (two wooden doors, a barred window, two open doorways
    /// into a green room, a stone platform), a concrete staircase to the roof, a cream main section (overhanging flat
    /// slab, dark-red lattice grille window and double door with transom, small grille window, curtains, a cot), a
    /// weathered pale-teal block with an open water basin in front, a tiled courtyard with a kerb and a ramp, and a low
    /// boundary wall with a brick lattice panel. Flat concrete roofs with parapets; no tiles or chimneys.
    /// Units: metres. Pivot: ground level; the courtyard faces −Z (the front).
    /// </summary>
    public static class RuralCourtyardHouse
    {
        public const string Name = "RuralCourtyardHouse";
        public static string PrefabPath => $"{HouseAssets.PrefabFolder}/{Name}.prefab";

        // Key levels and the wall thickness.
        const float Courtyard = 0.2f, Floor = 0.4f, T = 0.25f;
        const float TerracottaTop = 3.0f, TerracottaRoof = 3.25f, CreamTop = 3.25f, CreamRoof = 3.5f;
        // Footprint lines.
        const float TerraX0 = -9f, TerraX1 = -1.6f, CreamX0 = -0.35f, CreamX1 = 6.4f, TealX1 = 9f;
        const float Front = 4.6f, Back = 9.5f, TealFront = 4.2f;
        // Stair: 12 steps from the courtyard to the terracotta roof, in the gap between the two sections.
        const int Steps = 12;
        const float StairX0 = TerraX1 + T * 0.5f, StairX1 = CreamX0 - T * 0.5f, StairZ0 = 3.4f, StairRun = 0.34f;

        /// <summary>Points the player must be able to stand at (for the test scene's checks): name and position.</summary>
        public static readonly (string name, Vector3 point)[] Entrances =
        {
            ("Cream main door (open leaf)", new Vector3(4.38f, Floor, Front)),
            ("Green room doorway", new Vector3(-3.65f, Floor, Front)),
            ("Green room door", new Vector3(-2.3f, Floor, Front)),
            ("Stair foot", new Vector3((StairX0 + StairX1) * 0.5f, Courtyard, StairZ0 - 0.4f)),
            ("Stair landing", new Vector3((StairX0 + StairX1) * 0.5f, TerracottaRoof, 8.6f)),
            ("Terracotta roof", new Vector3(-5.5f, TerracottaRoof, 7.2f)),
        };

        static readonly Paint TerracottaPaint = new Paint(HRow.Terracotta, 0.55f);
        static readonly Paint CreamPaint = new Paint(HRow.Cream, 0.45f);
        static readonly Paint TealPaint = new Paint(HRow.Teal, 1.1f);
        static readonly Paint ConcretePaint = new Paint(HRow.Concrete, 0.3f);
        static FaceStyle Terra => FaceStyle.Of(TerracottaPaint);
        static FaceStyle Cream => FaceStyle.Of(CreamPaint);
        static FaceStyle Teal => FaceStyle.Of(TealPaint);
        static FaceStyle Concrete => FaceStyle.Of(ConcretePaint);
        static FaceStyle Slab => FaceStyle.Flat(HouseMat.Concrete, HRow.Concrete, 4);
        static FaceStyle GreenRoom => new FaceStyle(HouseMat.Interior, (p, n) => HousePalette.UV(HRow.Green, 4 + (Mathf.PerlinNoise(p.x * 1.7f, p.y * 1.7f + p.z) > 0.65f ? -1 : 0)), 0.8f);
        static FaceStyle CreamRoom => FaceStyle.Flat(HouseMat.Interior, HRow.Cream, 5);
        static FaceStyle PlainRoom => FaceStyle.Flat(HouseMat.Interior, HRow.Concrete, 5);

        // ───────────────────────────── Modules ─────────────────────────────

        public sealed class Module
        {
            public string Name;
            public HousePart Part;
        }

        public sealed class Fixture
        {
            public string Name, MeshKey;
            public HousePart Part;           // built around its own pivot (hinge for doors)
            public Vector3 Position;
            public float Yaw;
            public bool Collide;
        }

        public sealed class Layout
        {
            public readonly List<Module> Modules = new List<Module>();
            public readonly List<Fixture> Fixtures = new List<Fixture>();
            public readonly List<(string name, Vector3 centre, Vector3 size, Quaternion rotation)> SlopedColliders = new List<(string, Vector3, Vector3, Quaternion)>();
            public int Triangles => Modules.Sum(m => m.Part.Triangles) + Fixtures.Sum(f => f.Part.Triangles);
        }

        static HousePart Add(Layout layout, string name)
        {
            var part = new HousePart();
            layout.Modules.Add(new Module { Name = name, Part = part });
            return part;
        }

        /// <summary>Builds the whole house as data (meshes not yet created).</summary>
        public static Layout Build()
        {
            var layout = new Layout();
            BuildCourtyard(Add(layout, "Courtyard"), layout);
            BuildTerracotta(Add(layout, "Section_Terracotta"));
            BuildStair(Add(layout, "Staircase"));
            BuildCream(Add(layout, "Section_Cream"));
            BuildTeal(Add(layout, "Section_Teal"));
            BuildBasin(Add(layout, "WaterBasin"));
            BuildBoundary(Add(layout, "BoundaryWall"));
            BuildInterior(Add(layout, "Interior"));
            BuildFixtures(layout);
            return layout;
        }

        static void BuildCourtyard(HousePart p, Layout layout)
        {
            const float x0 = -9.6f, x1 = 9.6f, z0 = -3.6f, z1 = Front;
            // Slab body (one collider) with a grout layer just under the tiles.
            p.Box(new Vector3(x0, 0f, z0), Vector3.right, Vector3.forward, Vector3.zero, new Vector3(x1 - x0, Courtyard - 0.005f, z1 - z0),
                Concrete, Concrete, FaceStyle.Flat(HouseMat.Paving, HRow.Grout, 4));
            // Square tiles, slightly inset so the grout lines read.
            const float tile = 1.2f, gap = 0.03f;
            for (float x = x0 + 0.3f; x < x1 - 0.3f - 0.01f; x += tile)
                for (float z = z0 + 0.3f; z < z1 - 0.01f; z += tile)
                {
                    float xb = Mathf.Min(x + tile, x1 - 0.3f), zb = Mathf.Min(z + tile, z1);
                    int shade = 5 + Mathf.RoundToInt((Mathf.PerlinNoise(x * 0.55f + 5f, z * 0.55f + 9f) - 0.5f) * 1.6f);
                    var style = FaceStyle.Flat(HouseMat.Paving, HRow.Paving, shade - 1);
                    float y = Courtyard;
                    p.Quad(new Vector3(x + gap, y, z + gap), new Vector3(x + gap, y, zb - gap), new Vector3(xb - gap, y, zb - gap), new Vector3(xb - gap, y, z + gap), Vector3.up, style);
                }
            // Raised kerb along the front and both sides.
            var kerb = FaceStyle.Of(new Paint(HRow.Concrete, 0.12f));
            p.Block(new Vector3(0f, Courtyard + 0.03f, z0 + 0.15f), new Vector3(x1 - x0, 0.06f, 0.3f), kerb, false);
            p.Block(new Vector3(x0 + 0.15f, Courtyard + 0.03f, (z0 + z1) * 0.5f), new Vector3(0.3f, 0.06f, z1 - z0), kerb, false);
            p.Block(new Vector3(x1 - 0.15f, Courtyard + 0.03f, (z0 + z1) * 0.5f), new Vector3(0.3f, 0.06f, z1 - z0), kerb, false);

            // Sloped ramp up to the cream section's main door (wedge, with a rotated collider).
            const float rx0 = 2.85f, rx1 = 4.95f, rz0 = 3.2f;
            Vector3 a = new Vector3(rx0, Courtyard, rz0), b = new Vector3(rx1, Courtyard, rz0), c = new Vector3(rx1, Floor, Front), d = new Vector3(rx0, Floor, Front);
            var ramp = FaceStyle.Flat(HouseMat.Paving, HRow.Concrete, 5);
            p.Quad(a, d, c, b, Vector3.up, ramp);
            p.Quad(a, new Vector3(rx0, Courtyard, Front), d, d, Vector3.left, ramp);
            p.Quad(b, c, new Vector3(rx1, Courtyard, Front), new Vector3(rx1, Courtyard, Front), Vector3.right, ramp);
            float run = Front - rz0, rise = Floor - Courtyard;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            layout.SlopedColliders.Add(("Ramp Collider", new Vector3((rx0 + rx1) * 0.5f, (Courtyard + Floor) * 0.5f - 0.05f, (rz0 + Front) * 0.5f),
                new Vector3(rx1 - rx0, 0.1f, Mathf.Sqrt(run * run + rise * rise)), Quaternion.Euler(-angle, 0f, 0f)));

            // A low stone step at each terracotta doorway, and the grey platform under the window.
            foreach (float x in new[] { -8.0f, -6.8f, -3.65f, -2.3f })
                p.Block(new Vector3(x, Courtyard + 0.05f, Front - 0.18f), new Vector3(1.05f, 0.1f, 0.36f), Concrete);
            p.Block(new Vector3(-5.55f, Courtyard + 0.27f, Front - 0.32f), new Vector3(1.1f, 0.54f, 0.64f), FaceStyle.Flat(HouseMat.Concrete, HRow.Stain, 4));
        }

        static void BuildTerracotta(HousePart p)
        {
            const float split = -4.7f; // partition between the closed rooms and the green room
            // Front wall (faces the courtyard): two wooden doors and a barred window, then two openings into the green room.
            p.Wall(new Vector3(TerraX0, 0f, Front), new Vector3(split, 0f, Front), Vector3.back, T, TerracottaTop, Terra, PlainRoom, Terra,
                new Opening(0.55f, 1.45f, Floor, Floor + 2.1f), new Opening(1.75f, 2.65f, Floor, Floor + 2.1f), new Opening(2.9f, 4.0f, 1.3f, 2.4f));
            p.Wall(new Vector3(split, 0f, Front), new Vector3(TerraX1, 0f, Front), Vector3.back, T, TerracottaTop, Terra, GreenRoom, Terra,
                new Opening(0.55f, 1.55f, Floor, Floor + 2.1f), new Opening(1.95f, 2.85f, Floor, Floor + 2.1f));
            // Sides, back (a back door and a barred window) and the partition.
            p.Wall(new Vector3(TerraX0, 0f, Front), new Vector3(TerraX0, 0f, Back), Vector3.left, T, TerracottaTop, Terra, PlainRoom, Terra);
            p.Wall(new Vector3(TerraX1, 0f, Front), new Vector3(TerraX1, 0f, Back), Vector3.right, T, TerracottaTop, Terra, GreenRoom, Terra);
            p.Wall(new Vector3(TerraX0, 0f, Back), new Vector3(split, 0f, Back), Vector3.forward, T, TerracottaTop, Terra, PlainRoom, Terra,
                new Opening(2.4f, 3.3f, Floor, Floor + 2.1f));
            p.Wall(new Vector3(split, 0f, Back), new Vector3(TerraX1, 0f, Back), Vector3.forward, T, TerracottaTop, Terra, GreenRoom, Terra,
                new Opening(1.1f, 2.1f, 1.3f, 2.3f));
            p.Wall(new Vector3(split, 0f, Front + T * 0.5f), new Vector3(split, 0f, Back - T * 0.5f), Vector3.right, 0.2f, TerracottaTop, GreenRoom, PlainRoom, PlainRoom);
            // Floor, a pier on the facade, the flat roof slab (deep front overhang) with a parapet, and a rooftop tank stand.
            p.Block(new Vector3((TerraX0 + TerraX1) * 0.5f, Floor * 0.5f, (Front + Back) * 0.5f), new Vector3(TerraX1 - TerraX0, Floor, Back - Front), FaceStyle.Flat(HouseMat.Paving, HRow.Paving, 3));
            p.Block(new Vector3(split, TerracottaTop * 0.5f, Front - 0.3f), new Vector3(0.5f, TerracottaTop, 0.35f), Terra);
            float sx0 = TerraX0 - T * 0.5f - 0.25f, sx1 = TerraX1 + T * 0.5f, sz0 = Front - 0.7f, sz1 = Back + T * 0.5f + 0.25f;
            p.Box(new Vector3(sx0, TerracottaTop, sz0), Vector3.right, Vector3.forward, Vector3.zero, new Vector3(sx1 - sx0, TerracottaRoof - TerracottaTop, sz1 - sz0), Terra, Terra, Terra);
            const float parapet = 0.35f, pt = 0.18f;
            p.Block(new Vector3((sx0 + sx1) * 0.5f, TerracottaRoof + parapet * 0.5f, sz0 + pt * 0.5f), new Vector3(sx1 - sx0, parapet, pt), Terra);
            p.Block(new Vector3((sx0 + sx1) * 0.5f, TerracottaRoof + parapet * 0.5f, sz1 - pt * 0.5f), new Vector3(sx1 - sx0, parapet, pt), Terra);
            p.Block(new Vector3(sx0 + pt * 0.5f, TerracottaRoof + parapet * 0.5f, (sz0 + sz1) * 0.5f), new Vector3(pt, parapet, sz1 - sz0), Terra);
            // Right edge: parapet only in front of the stair landing (the landing side stays open).
            p.Block(new Vector3(sx1 - pt * 0.5f, TerracottaRoof + parapet * 0.5f, (sz0 + 7.3f) * 0.5f), new Vector3(pt, parapet, 7.3f - sz0), Terra);
            p.Block(new Vector3(-7.6f, TerracottaRoof + 0.25f, 8.6f), new Vector3(1.0f, 0.5f, 0.7f), Terra);
        }

        static void BuildStair(HousePart p)
        {
            float rise = (TerracottaRoof - Courtyard) / Steps;
            float width = StairX1 - StairX0;
            for (int i = 0; i < Steps; i++)
            {
                float z = StairZ0 + i * StairRun;
                float top = Courtyard + rise * (i + 1);
                // Each step runs down to the ground: the solid, stepped concrete underside of the reference.
                p.Box(new Vector3(StairX0, 0f, z), Vector3.right, Vector3.forward, Vector3.zero, new Vector3(width, top, StairRun), Concrete, Concrete, Concrete);
            }
            float landing = StairZ0 + Steps * StairRun;
            p.Box(new Vector3(StairX0, 0f, landing), Vector3.right, Vector3.forward, Vector3.zero, new Vector3(width, TerracottaRoof, Back + T * 0.5f - landing), Concrete, Concrete, Concrete);
        }

        static void BuildCream(HousePart p)
        {
            // Front: large lattice grille window, the double grille door with transom, a small grille window.
            p.Wall(new Vector3(CreamX0, 0f, Front), new Vector3(CreamX1, 0f, Front), Vector3.back, T, CreamTop, Cream, CreamRoom, Cream,
                new Opening(0.6f, 2.9f, 0.55f, 2.75f), new Opening(3.35f, 5.15f, Floor, 3.0f), new Opening(5.7f, 6.5f, 1.0f, 2.6f));
            p.Wall(new Vector3(CreamX0, 0f, Front), new Vector3(CreamX0, 0f, Back), Vector3.left, T, CreamTop, Cream, CreamRoom, Cream);
            // The shared wall with the teal block (its far side is the teal room's interior).
            p.Wall(new Vector3(CreamX1, 0f, Front), new Vector3(CreamX1, 0f, Back), Vector3.right, T, CreamTop, PlainRoom, CreamRoom, Cream);
            // The rear is terracotta on the reference sheet.
            p.Wall(new Vector3(CreamX0, 0f, Back), new Vector3(CreamX1, 0f, Back), Vector3.forward, T, CreamTop, Terra, CreamRoom, Terra,
                new Opening(1.35f, 2.35f, 1.4f, 2.4f), new Opening(4.35f, 5.35f, 1.4f, 2.4f));
            p.Block(new Vector3((CreamX0 + CreamX1) * 0.5f, Floor * 0.5f, (Front + Back) * 0.5f), new Vector3(CreamX1 - CreamX0, Floor, Back - Front), FaceStyle.Flat(HouseMat.Paving, HRow.Paving, 5));
            // Overhanging flat slab (front and back) with a low rim; small dark fittings under the front edge.
            float sx0 = CreamX0 - T * 0.5f, sx1 = CreamX1 + T * 0.5f, sz0 = Front - 0.45f, sz1 = Back + T * 0.5f + 0.3f;
            p.Box(new Vector3(sx0, CreamTop, sz0), Vector3.right, Vector3.forward, Vector3.zero, new Vector3(sx1 - sx0, CreamRoof - CreamTop, sz1 - sz0), Cream, Cream, Cream);
            const float rim = 0.15f, rt = 0.15f;
            p.Block(new Vector3((sx0 + sx1) * 0.5f, CreamRoof + rim * 0.5f, sz0 + rt * 0.5f), new Vector3(sx1 - sx0, rim, rt), Cream);
            p.Block(new Vector3((sx0 + sx1) * 0.5f, CreamRoof + rim * 0.5f, sz1 - rt * 0.5f), new Vector3(sx1 - sx0, rim, rt), Cream);
            p.Block(new Vector3(sx0 + rt * 0.5f, CreamRoof + rim * 0.5f, (sz0 + sz1) * 0.5f), new Vector3(rt, rim, sz1 - sz0), Cream);
            p.Block(new Vector3(sx1 - rt * 0.5f, CreamRoof + rim * 0.5f, (sz0 + sz1) * 0.5f), new Vector3(rt, rim, sz1 - sz0), Cream);
            foreach (float x in new[] { 0.9f, 4.05f, 5.9f })
                p.Block(new Vector3(x, CreamTop - 0.12f, Front - 0.2f), new Vector3(0.22f, 0.18f, 0.12f), FaceStyle.Flat(HouseMat.Metal, HRow.Stain, 1), false);
            p.Block(new Vector3(2.3f, CreamRoof + 0.22f, 8.9f), new Vector3(0.9f, 0.45f, 0.6f), Cream);
        }

        static void BuildTeal(HousePart p)
        {
            p.Wall(new Vector3(CreamX1, 0f, TealFront), new Vector3(TealX1, 0f, TealFront), Vector3.back, T, CreamTop, Teal, PlainRoom, Teal);
            p.Wall(new Vector3(TealX1, 0f, TealFront), new Vector3(TealX1, 0f, Back), Vector3.right, T, CreamTop, Teal, PlainRoom, Teal,
                new Opening(2.4f, 3.3f, 1.4f, 2.3f));
            p.Wall(new Vector3(CreamX1, 0f, Back), new Vector3(TealX1, 0f, Back), Vector3.forward, T, CreamTop, Teal, PlainRoom, Teal);
            p.Wall(new Vector3(CreamX1, 0f, TealFront), new Vector3(CreamX1, 0f, Front), Vector3.left, T, CreamTop, Teal, Teal, Teal);
            p.Block(new Vector3((CreamX1 + TealX1) * 0.5f, Floor * 0.5f, (TealFront + Back) * 0.5f), new Vector3(TealX1 - CreamX1, Floor, Back - TealFront), Slab);
            p.Block(new Vector3((CreamX1 + TealX1) * 0.5f, 2.95f, (TealFront + Back) * 0.5f), new Vector3(TealX1 - CreamX1 - T, 0.15f, Back - TealFront - T), Slab);
        }

        static void BuildBasin(HousePart p)
        {
            const float x0 = 6.3f, x1 = 9.05f, z0 = 1.9f, top = 1.1f, t = 0.18f;
            var inside = FaceStyle.Flat(HouseMat.Concrete, HRow.Stain, 3);
            var tealOutside = FaceStyle.Of(new Paint(HRow.Teal, 0.9f));
            float h = top - Courtyard;
            p.Box(new Vector3(x0, Courtyard, z0), Vector3.right, Vector3.back, new Vector3(0f, 0f, -t), new Vector3(x1 - x0, h, 0f), tealOutside, inside, tealOutside);      // front
            p.Box(new Vector3(x0, Courtyard, z0), Vector3.forward, Vector3.left, new Vector3(0f, 0f, -t), new Vector3(TealFront - z0, h, 0f), tealOutside, inside, tealOutside); // left
            p.Box(new Vector3(x1, Courtyard, z0), Vector3.forward, Vector3.right, new Vector3(0f, 0f, -t), new Vector3(TealFront - z0, h, 0f), tealOutside, inside, tealOutside); // right
            var water = FaceStyle.Flat(HouseMat.Water, HRow.Water, 4);
            float y = 0.78f;
            p.Quad(new Vector3(x0 + t, y, z0 + t), new Vector3(x0 + t, y, TealFront), new Vector3(x1 - t, y, TealFront), new Vector3(x1 - t, y, z0 + t), Vector3.up, water);
        }

        static void BuildBoundary(HousePart p)
        {
            // Terracotta pillar and a low wall back to the house, with a brick lattice (jaali) panel.
            p.Block(new Vector3(-9.1f, Courtyard + 0.85f, 2.3f), new Vector3(0.5f, 1.7f, 0.5f), Terra);
            const float z0 = 2.55f, z1 = Front - T * 0.5f, height = 1.3f;
            var start = new Vector3(-9.1f, Courtyard, z0);
            p.Wall(start, new Vector3(-9.1f, Courtyard, z1), Vector3.left, 0.22f, height, Terra, Terra, Terra, new Opening(0.3f, 1.4f, 0.3f, 1.05f));
            // Lattice: staggered bricks with gaps (both sides visible).
            var brick = FaceStyle.Flat(HouseMat.Concrete, HRow.Brick, 4);
            int row = 0;
            for (float y = 0.32f; y < 1.03f; y += 0.105f, row++)
                for (float z = 0.3f + (row % 2) * 0.09f; z < 1.38f; z += 0.18f)
                    p.Block(new Vector3(-9.1f, Courtyard + y + 0.03f, z0 + z + 0.05f), new Vector3(0.16f, 0.06f, Mathf.Min(0.1f, 1.4f - z)), brick, false);
            p.Colliders.Add(new Bounds(new Vector3(-9.1f, Courtyard + 0.68f, z0 + 0.85f), new Vector3(0.22f, 0.75f, 1.1f)));
        }

        static void BuildInterior(HousePart p)
        {
            // Cot in the cream room (wooden frame, red fabric).
            p.Block(new Vector3(1.4f, Floor + 0.22f, 7.6f), new Vector3(0.95f, 0.44f, 1.95f), FaceStyle.Flat(HouseMat.Wood, HRow.Wood, 4));
            p.Block(new Vector3(1.4f, Floor + 0.47f, 7.6f), new Vector3(0.88f, 0.06f, 1.88f), FaceStyle.Flat(HouseMat.Interior, HRow.Fabric, 4), false);
            p.Block(new Vector3(1.4f, Floor + 0.55f, 8.35f), new Vector3(0.6f, 0.1f, 0.3f), FaceStyle.Flat(HouseMat.Interior, HRow.Curtain, 6), false);
            // A low table in the green room.
            p.Block(new Vector3(-3.2f, Floor + 0.25f, 7.8f), new Vector3(0.9f, 0.5f, 0.6f), FaceStyle.Flat(HouseMat.Wood, HRow.Wood, 3));
            // Curtains drawn to the sides behind the grille windows (folded strips).
            void Curtains(float x0, float x1, float y0, float y1)
            {
                float w = x1 - x0, z = Front + T * 0.5f + 0.05f;
                foreach ((float a, float b) in new[] { (x0, x0 + w * 0.32f), (x1 - w * 0.32f, x1) })
                {
                    int strips = 4;
                    for (int i = 0; i < strips; i++)
                    {
                        float s0 = Mathf.Lerp(a, b, i / (float)strips), s1 = Mathf.Lerp(a, b, (i + 1f) / strips);
                        float dz = i % 2 == 0 ? 0.03f : -0.02f;
                        var style = FaceStyle.Flat(HouseMat.Interior, i % 2 == 0 ? HRow.Curtain : HRow.Fabric, i % 2 == 0 ? 4 : 5);
                        Vector3 q0 = new Vector3(s0, y0, z + dz), q1 = new Vector3(s1, y0, z - dz), q2 = new Vector3(s1, y1, z - dz), q3 = new Vector3(s0, y1, z + dz);
                        p.Quad(q0, q1, q2, q3, Vector3.back, style);
                        p.Quad(q1, q0, q3, q2, Vector3.forward, style);
                    }
                }
            }
            Curtains(CreamX0 + 0.6f, CreamX0 + 2.9f, 0.6f, 2.7f);
            Curtains(CreamX0 + 5.7f, CreamX0 + 6.5f, 1.05f, 2.55f);
        }

        // ───────────────────────────── Doors, gates, windows ─────────────────────────────

        static void BuildFixtures(Layout layout)
        {
            void Fix(string name, string key, HousePart part, Vector3 at, float yaw, bool collide) =>
                layout.Fixtures.Add(new Fixture { Name = name, MeshKey = key, Part = part, Position = at, Yaw = yaw, Collide = collide });

            // Terracotta: two closed wooden doors, one door standing open into the green room (the other doorway is open).
            HousePart door = WoodDoor(0.9f, 2.1f);
            Fix("Door_Terracotta_1", "WoodDoor", door, new Vector3(TerraX0 + 0.55f, Floor, Front), 0f, true);
            Fix("Door_Terracotta_2", "WoodDoor", door, new Vector3(TerraX0 + 1.75f, Floor, Front), 0f, true);
            Fix("Door_GreenRoom_Open", "WoodDoor", door, new Vector3(-4.7f + 1.95f, Floor, Front + T * 0.5f), -100f, false);
            Fix("Door_Back", "WoodDoor", door, new Vector3(TerraX0 + 2.4f, Floor, Back), 0f, true);
            HousePart barred = BarredWindow(1.1f, 1.1f);
            Fix("Window_Terracotta_Front", "BarredWindow_110", barred, new Vector3(TerraX0 + 2.9f, 1.3f, Front), 0f, true);
            HousePart barredSmall = BarredWindow(1.0f, 1.0f);
            Fix("Window_Terracotta_Back", "BarredWindow_100", barredSmall, new Vector3(-4.7f + 1.1f, 1.3f, Back), 0f, true);
            Fix("Window_Cream_Back_1", "BarredWindow_100", barredSmall, new Vector3(CreamX0 + 1.35f, 1.4f, Back), 0f, true);
            Fix("Window_Cream_Back_2", "BarredWindow_100", barredSmall, new Vector3(CreamX0 + 4.35f, 1.4f, Back), 0f, true);
            HousePart barredSide = BarredWindow(0.9f, 0.9f);
            Fix("Window_Teal_Side", "BarredWindow_90", barredSide, new Vector3(TealX1, 1.4f, TealFront + 3.3f), 90f, true);

            // Cream front: lattice grille window (two fixed panels), double door (left leaf closed, right leaf open), transom, side window.
            HousePart bigPanel = Grille(1.15f, 2.2f, true);
            Fix("Grille_Window_Left", "Grille_115x220", bigPanel, new Vector3(CreamX0 + 0.6f, 0.55f, Front), 0f, true);
            Fix("Grille_Window_Right", "Grille_115x220", bigPanel, new Vector3(CreamX0 + 1.75f, 0.55f, Front), 0f, true);
            HousePart leaf = Grille(0.9f, 2.2f, true);
            Fix("Grille_Door_Left", "Grille_090x220", leaf, new Vector3(CreamX0 + 3.35f, Floor, Front), 0f, true);
            Fix("Grille_Door_Right_Open", "Grille_090x220", leaf, new Vector3(CreamX0 + 5.15f, Floor, Front + T * 0.5f), 180f + 100f, false);
            Fix("Grille_Transom", "Grille_180x040", Grille(1.8f, 0.4f, false), new Vector3(CreamX0 + 3.35f, Floor + 2.2f, Front), 0f, true);
            Fix("Grille_Window_Side", "Grille_080x160", Grille(0.8f, 1.6f, true), new Vector3(CreamX0 + 5.7f, 1.0f, Front), 0f, true);
        }

        /// <summary>Wooden door, pivot at the hinge (bottom-left seen from outside), closing across +X.</summary>
        static HousePart WoodDoor(float width, float height)
        {
            var p = new HousePart();
            var wood = FaceStyle.Flat(HouseMat.Wood, HRow.Wood, 4);
            p.Block(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width - 0.02f, height, 0.05f), wood);
            foreach (float y in new[] { 0.3f, 0.72f })
                p.Block(new Vector3(width * 0.5f, height * y, -0.03f), new Vector3(width - 0.22f, height * 0.36f, 0.015f), FaceStyle.Flat(HouseMat.Wood, HRow.Wood, 3), false);
            p.Block(new Vector3(width - 0.12f, height * 0.48f, -0.05f), new Vector3(0.04f, 0.14f, 0.04f), FaceStyle.Flat(HouseMat.Metal, HRow.Concrete, 6), false);
            return p;
        }

        /// <summary>Dark-red metal gate or window panel: frame, optional mid rail and a diagonal lattice. Pivot bottom-left.</summary>
        static HousePart Grille(float width, float height, bool midRail)
        {
            var p = new HousePart();
            var metal = FaceStyle.Flat(HouseMat.Metal, HRow.Metal, 4);
            const float frame = 0.055f, bar = 0.018f, spacing = 0.27f;
            p.Block(new Vector3(width * 0.5f, frame * 0.5f, 0f), new Vector3(width, frame, frame), metal);
            p.Block(new Vector3(width * 0.5f, height - frame * 0.5f, 0f), new Vector3(width, frame, frame), metal);
            p.Block(new Vector3(frame * 0.5f, height * 0.5f, 0f), new Vector3(frame, height, frame), metal);
            p.Block(new Vector3(width - frame * 0.5f, height * 0.5f, 0f), new Vector3(frame, height, frame), metal);
            if (midRail) p.Block(new Vector3(width * 0.5f, height * 0.45f, 0f), new Vector3(width, frame * 0.8f, frame * 0.8f), metal);
            // Diagonal lattice clipped to the inner rectangle.
            float ix0 = frame, ix1 = width - frame, iy0 = frame, iy1 = height - frame;
            foreach (float dir in new[] { 1f, -1f })
                for (float c = -(ix1 - ix0) - (iy1 - iy0); c < (ix1 - ix0) + (iy1 - iy0); c += spacing)
                {
                    // Line: y = dir * (x - ix0) + c + iy0 (dir = +1 rising, −1 falling), clipped to the box.
                    var points = new List<Vector2>();
                    foreach (float x in new[] { ix0, ix1 })
                    {
                        float y = dir * (x - ix0) + c + iy0;
                        if (y >= iy0 - 1e-4f && y <= iy1 + 1e-4f) points.Add(new Vector2(x, y));
                    }
                    foreach (float y in new[] { iy0, iy1 })
                    {
                        float x = (y - iy0 - c) * dir + ix0;
                        if (x > ix0 + 1e-4f && x < ix1 - 1e-4f) points.Add(new Vector2(x, y));
                    }
                    if (points.Count < 2) continue;
                    Vector2 a = points[0], b = points[1];
                    float len = Vector2.Distance(a, b);
                    if (len < 0.05f) continue;
                    Vector2 mid = (a + b) * 0.5f;
                    float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                    var rot = Quaternion.Euler(0f, 0f, ang);
                    Shapes.Box(p[HouseMat.Metal], new Vector3(mid.x, mid.y, 0f), new Vector3(len, bar, bar), rot, n => HousePalette.UV(HRow.Metal, 4 + (n.y > 0.5f ? 1 : 0)));
                }
            p.Colliders.Clear();
            p.Colliders.Add(new Bounds(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width, height, 0.08f)));
            return p;
        }

        /// <summary>Window with a wooden frame and vertical iron bars. Pivot bottom-left.</summary>
        static HousePart BarredWindow(float width, float height)
        {
            var p = new HousePart();
            var wood = FaceStyle.Flat(HouseMat.Wood, HRow.Wood, 5);
            var metal = FaceStyle.Flat(HouseMat.Metal, HRow.Stain, 2);
            const float f = 0.07f;
            p.Block(new Vector3(width * 0.5f, f * 0.5f, 0f), new Vector3(width, f, 0.1f), wood, false);
            p.Block(new Vector3(width * 0.5f, height - f * 0.5f, 0f), new Vector3(width, f, 0.1f), wood, false);
            p.Block(new Vector3(f * 0.5f, height * 0.5f, 0f), new Vector3(f, height, 0.1f), wood, false);
            p.Block(new Vector3(width - f * 0.5f, height * 0.5f, 0f), new Vector3(f, height, 0.1f), wood, false);
            for (float x = f + 0.1f; x < width - f - 0.05f; x += 0.12f)
                p.Block(new Vector3(x, height * 0.5f, 0f), new Vector3(0.02f, height - 2f * f, 0.02f), metal, false);
            p.Block(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width - 2f * f, 0.02f, 0.02f), metal, false);
            // Dark glass behind the bars.
            p.Block(new Vector3(width * 0.5f, height * 0.5f, 0.06f), new Vector3(width - f, height - f, 0.01f), FaceStyle.Flat(HouseMat.Interior, HRow.Water, 2), false);
            p.Colliders.Add(new Bounds(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width, height, 0.1f)));
            return p;
        }

        // ───────────────────────────── Prefab ─────────────────────────────

        /// <summary>Builds the house into GameObjects (meshes saved as assets) under a root at the origin.</summary>
        public static GameObject Instantiate(Layout layout, Dictionary<HouseMat, Material> materials, bool saveMeshes)
        {
            var root = new GameObject(Name);
            Mesh MakeMesh(HousePart part, string key, out Material[] mats)
            {
                Mesh mesh = part.ToMesh($"Gen_House_{key}", out HouseMat[] used);
                mats = used.Select(u => materials[u]).ToArray();
                return saveMeshes ? GeneratedAssetStore.SaveMesh(mesh, $"{HouseAssets.MeshFolder}/Gen_House_{key}.asset") : mesh;
            }
            foreach (Module module in layout.Modules)
            {
                var go = new GameObject(module.Name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = MakeMesh(module.Part, module.Name, out Material[] mats);
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                foreach (Bounds b in module.Part.Colliders)
                {
                    BoxCollider box = go.AddComponent<BoxCollider>();
                    box.center = b.center;
                    box.size = b.size;
                }
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            var fixtures = new GameObject("Doors, Gates and Windows").transform;
            fixtures.SetParent(root.transform, false);
            var shared = new Dictionary<string, (Mesh mesh, Material[] mats)>();
            foreach (Fixture f in layout.Fixtures)
            {
                if (!shared.TryGetValue(f.MeshKey, out var entry))
                {
                    Mesh mesh = MakeMesh(f.Part, f.MeshKey, out Material[] mats);
                    shared[f.MeshKey] = entry = (mesh, mats); // one mesh per door/gate type, reused
                }
                var go = new GameObject(f.Name);
                go.transform.SetParent(fixtures, false);
                go.transform.SetLocalPositionAndRotation(f.Position, Quaternion.Euler(0f, f.Yaw, 0f));
                go.AddComponent<MeshFilter>().sharedMesh = entry.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = entry.mats;
                if (f.Collide)
                    foreach (Bounds b in f.Part.Colliders)
                    {
                        BoxCollider box = go.AddComponent<BoxCollider>();
                        box.center = b.center;
                        box.size = b.size;
                    }
            }
            foreach (var (name, centre, size, rotation) in layout.SlopedColliders)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.SetLocalPositionAndRotation(centre, rotation);
                go.AddComponent<BoxCollider>().size = size;
            }
            return root;
        }

        [MenuItem("Greenhaven/House/Generate Rural Courtyard House", priority = 110)]
        static void GenerateMenu()
        {
            string report = Generate();
            Debug.Log("Greenhaven — Rural courtyard house:\n" + report);
            EditorUtility.DisplayDialog("Rural Courtyard House", report, "OK");
        }

        /// <summary>Saves meshes, materials and the prefab (confirms before replacing a generated prefab; refuses foreign files).</summary>
        public static string Generate()
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";
            HouseAssets.EnsureFolders();
            Object existing = AssetDatabase.LoadMainAssetAtPath(PrefabPath);
            if (existing != null && !GeneratedAssetStore.IsGenerated(existing)) return $"Stopped: {PrefabPath} exists but was not made by this tool.";
            if (existing != null && !EditorUtility.DisplayDialog("Replace house prefab?", $"{PrefabPath} will be rebuilt in place (scenes using it update).", "Replace", "Cancel"))
                return "Cancelled.";
            Layout layout = Build();
            Dictionary<HouseMat, Material> materials = HouseAssets.EnsureMaterials();
            GameObject root = Instantiate(layout, materials, true);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok || prefab == null) return $"Could not save {PrefabPath}.";
                AssetDatabase.SetLabels(prefab, new[] { GeneratedAssetStore.Label });
                AssetDatabase.SaveAssets();
                var sb = new StringBuilder();
                sb.AppendLine($"Saved {PrefabPath}: {layout.Modules.Count} modules, {layout.Fixtures.Count} doors/gates/windows, {layout.Triangles} triangles, {materials.Count} materials.");
                sb.Append("Meshes in Generated/House/Meshes, materials in Generated/House/Materials.");
                return sb.ToString();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
