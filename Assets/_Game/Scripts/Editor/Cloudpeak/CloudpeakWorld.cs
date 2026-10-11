using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Greenhaven.Editor.Cloudpeak
{
    /// <summary>Parameters for the Cloudpeak terrain and trail. Same settings → same world.</summary>
    [Serializable]
    public struct CloudpeakSettings
    {
        public int Seed;
        [Tooltip("Half the side of the square playable region in metres (100 = 200 × 200 m).")]
        public float HalfSize;
        [Tooltip("Summit height above the forest floor in metres.")]
        public float PeakHeight;
        [Tooltip("Radius of the mountain's foot in metres.")]
        public float MountainRadius;
        [Tooltip("Share of the height in the steep summit cone (the rest is the foothill skirt).")]
        public float ConeShare;
        [Tooltip("0 = smooth slopes, 1 = strong rocky ridges and spurs.")]
        public float RidgeVariation;
        [Tooltip("Terrain grid spacing in metres (larger = fewer triangles).")]
        public float CellSize;
        [Tooltip("Walkable trail width in metres.")]
        public float TrailWidth;
        [Tooltip("Steepest allowed trail grade (rise over run; 0.25 ≈ 14°).")]
        public float MaxGrade;
        [Tooltip("Vegetation amount (0.5 = low-end phones, 1 = default).")]
        public float VegetationDensity;

        public static CloudpeakSettings Default => new CloudpeakSettings
        {
            Seed = 1234, HalfSize = 100f, PeakHeight = 44f, MountainRadius = 70f, ConeShare = 0.6f, RidgeVariation = 0.5f,
            CellSize = 2f, TrailWidth = 3f, MaxGrade = 0.25f, VegetationDensity = 1f,
        };

        public CloudpeakSettings Clamped()
        {
            CloudpeakSettings s = this;
            s.HalfSize = Mathf.Clamp(s.HalfSize <= 0f ? 100f : s.HalfSize, 85f, 130f);
            s.MountainRadius = Mathf.Clamp(s.MountainRadius <= 0f ? 70f : s.MountainRadius, 60f, s.HalfSize * 0.8f);
            s.PeakHeight = Mathf.Clamp(s.PeakHeight <= 0f ? 44f : s.PeakHeight, 20f, Mathf.Min(70f, s.MountainRadius * 0.75f));
            s.ConeShare = Mathf.Clamp(s.ConeShare <= 0f ? 0.6f : s.ConeShare, 0.35f, 0.7f);
            s.RidgeVariation = Mathf.Clamp01(s.RidgeVariation);
            s.CellSize = Mathf.Clamp(s.CellSize <= 0f ? 2f : s.CellSize, 1.5f, 3f);
            s.TrailWidth = Mathf.Clamp(s.TrailWidth <= 0f ? 3f : s.TrailWidth, 2.4f, 4.5f);
            s.MaxGrade = Mathf.Clamp(s.MaxGrade <= 0f ? 0.25f : s.MaxGrade, 0.2f, 0.3f);
            s.VegetationDensity = Mathf.Clamp(s.VegetationDensity, 0f, 2f);
            return s;
        }
    }

    public enum TrailSurface { Dirt, Stone, Bridge }

    public enum TrailSection { Forest, Switchbacks, Shelf, Summit }

    public struct TrailPoint
    {
        public Vector2 Position;
        public float Height;
        public float HalfWidth;
        /// <summary>Extra level ground beside the trail edge (wider on steep slopes).</summary>
        public float Margin;
        public TrailSurface Surface;
        public TrailSection Section;
        /// <summary>Distance along the trail from the entrance, in metres.</summary>
        public float Distance;
    }

    /// <summary>A flattened, widened area (clearing, rest stop, viewpoint, summit).</summary>
    public struct Platform
    {
        public string Name;
        public Vector2 Centre;
        public float Radius;
        public float Height;
    }

    /// <summary>
    /// The Cloudpeak region as data: natural height field (rolling forest, two-tier mountain with a
    /// foothill skirt, a viewpoint shelf and a steep summit cone, rocky spurs, boundary hills, a brook),
    /// a continuous grade-limited trail (forest S-curve → bridge → switchbacks → shelf → summit spiral),
    /// and the carved terrain grid. No Unity objects are created here, so it is unit-testable.
    /// </summary>
    public sealed class CloudpeakWorld
    {
        // Mountain tiers as fractions of the mountain radius: plateau | cone | shelf | skirt.
        const float TierPlateau = 0.09f, TierCone = 0.46f, TierShelf = 0.56f;
        const float ShelfShare = 0.05f;
        const float FlatMargin = 1.4f, Shoulder = 3f;
        const float Step = 0.5f, Spacing = 0.75f;
        const float HairpinRadius = 5f;
        const float BridgeHalfSpan = 3.2f;

        public readonly CloudpeakSettings Settings;
        public readonly Vector2 Mountain;
        public readonly Vector2 Entrance, Spawn;
        public readonly float PlateauRadius, ShelfRadius;
        public Vector2 Glade { get; private set; }
        public Vector2 FoothillRest { get; private set; }
        public Vector2 Ledge { get; private set; }
        public Vector2 LedgeOutward { get; private set; }
        public Vector2 BridgeCentre { get; private set; }
        public Vector2 BridgeDirection { get; private set; }
        public float BridgeDeckHeight { get; private set; }
        public Vector2 SummitApproach { get; private set; }
        public readonly List<Vector2> Hairpins = new List<Vector2>();
        public readonly List<TrailPoint> Trail = new List<TrailPoint>();
        public readonly List<Platform> Platforms = new List<Platform>();

        // Carved terrain grid.
        public readonly int Cells;
        public readonly float Cell;
        public float Origin => -Settings.HalfSize;
        float[] heights;

        readonly float ox, oz, ridgePhase, brookPhase;
        readonly float wCone, wSkirt;
        readonly Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>();
        const float BucketSize = 8f;

        public int Vertices => Cells + 1;
        public float HeightAt(int i, int j) => heights[i + j * Vertices];

        CloudpeakWorld(CloudpeakSettings s)
        {
            Settings = s;
            float k = s.HalfSize / 100f;
            Mountain = new Vector2(12f * k, 30f * k);
            Entrance = new Vector2(0f, -s.HalfSize + 5f);
            Spawn = new Vector2(0f, -s.HalfSize + 9f);
            PlateauRadius = TierPlateau * s.MountainRadius;
            ShelfRadius = (TierCone + TierShelf) * 0.5f * s.MountainRadius;
            var rng = new System.Random(s.Seed ^ 0xC10D);
            ox = (float)rng.NextDouble() * 500f + 50f;
            oz = (float)rng.NextDouble() * 500f + 50f;
            ridgePhase = (float)rng.NextDouble() * Mathf.PI * 2f;
            brookPhase = (float)rng.NextDouble() * Mathf.PI * 2f;
            wCone = s.ConeShare;
            wSkirt = 1f - s.ConeShare - ShelfShare;
            Cell = s.CellSize;
            Cells = Mathf.RoundToInt(s.HalfSize * 2f / Cell);
            Cell = s.HalfSize * 2f / Cells;
        }

        public static CloudpeakWorld Generate(CloudpeakSettings settings)
        {
            var world = new CloudpeakWorld(settings.Clamped());
            world.BuildTrail();
            world.BuildGrid();
            return world;
        }

        // ───────────────────────────── Natural height ─────────────────────────────

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Radial mountain profile (no noise): plateau, steep cone, gentle shelf, foothill skirt.</summary>
        public float Profile(float d)
        {
            float t = d / Settings.MountainRadius;
            float cone = 1f - SmoothStep(TierPlateau, TierCone, t);
            float shelf = 1f - SmoothStep(TierCone, TierShelf, t);
            float skirt = 1f - SmoothStep(TierShelf, 1f, t);
            return Settings.PeakHeight * (wCone * cone + ShelfShare * shelf + wSkirt * skirt);
        }

        public float ProfileSlope(float d) => Mathf.Max(0f, (Profile(d - 0.25f) - Profile(d + 0.25f)) / 0.5f);

        float Perlin(float x, float z, float frequency, float offset) => Mathf.PerlinNoise(x * frequency + ox + offset, z * frequency + oz - offset);

        /// <summary>Centre line of the Whispering Brook across the forest.</summary>
        public float BrookZ(float x) => -0.6f * Settings.HalfSize + 5f * Mathf.Sin(x / 15f + brookPhase);

        float MountainT(float x, float z) => Vector2.Distance(new Vector2(x, z), Mountain) / Settings.MountainRadius;

        /// <summary>Terrain before the trail is carved. <paramref name="brook"/> = false ignores the brook bed.</summary>
        public float NaturalHeight(float x, float z, bool brook = true)
        {
            float H = Settings.HalfSize;
            float t = MountainT(x, z);
            float d = t * Settings.MountainRadius;

            // Rolling forest floor.
            float floor = 2.2f * Perlin(x, z, 0.022f, 0f) + 0.8f * Perlin(x, z, 0.07f, 13f) - 1.4f;
            float mountainMask = 1f - SmoothStep(0.75f, 1.1f, t);
            float h = floor * (1f - 0.75f * mountainMask) + Profile(d);

            // Rocky spurs radiating from the summit, plus broad lumps; none on the plateau or far out.
            float bell = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.1f) / 0.95f));
            if (bell > 0f)
            {
                float angle = Mathf.Atan2(z - Mountain.y, x - Mountain.x);
                float wobble = Perlin(x, z, 0.04f, 31f);
                float crest = 1f - Mathf.Abs(Mathf.Sin(3f * angle + ridgePhase + 3f * t + 1.6f * wobble));
                float spur = crest * crest * crest;
                float lumps = Perlin(x, z, 0.05f, 57f) - 0.45f;
                h += Settings.RidgeVariation * bell * (0.1f * Settings.PeakHeight * spur + 3f * lumps);
            }

            // Boundary hills around the edge (a natural barrier), opening at the entrance from the village.
            float edge = H - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float rim = (1f - SmoothStep(3f, 24f, edge)) * (9f + 8f * Perlin(x, z, 0.035f, 77f));
            float notch = 1f - (1f - SmoothStep(8f, 18f, Mathf.Abs(x))) * (1f - SmoothStep(18f, 30f, z + H));
            h += rim * notch;

            if (brook)
            {
                float bed = 1f - SmoothStep(1.0f, 2.1f, Mathf.Abs(z - BrookZ(x)));
                h -= 1.1f * bed * (1f - mountainMask);
            }
            return h;
        }

        // ───────────────────────────── Trail ─────────────────────────────

        Vector2 Polar(float r, float angle) => Mountain + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;

        void BuildTrail()
        {
            CloudpeakSettings s = Settings;
            float k = s.HalfSize / 100f;
            float R = s.MountainRadius;
            float hw = s.TrailWidth * 0.5f;
            var raw = new List<(Vector2 p, float hw, TrailSection section)>();

            // Trailhead at the foot of the foothill skirt, south-west of the mountain.
            float startAngle = -128f * Mathf.Deg2Rad;
            float startRadius = 0.97f * R;
            FoothillRest = Polar(startRadius, startAngle);

            // A. Forest: S-curve through the Whispering Glade, straight across the brook bridge, on to the trailhead.
            Glade = new Vector2(-24f * k, -80f * k);
            float bx = -12f * k;
            BridgeCentre = new Vector2(bx, BrookZ(bx));
            float slope = (BrookZ(bx + 0.5f) - BrookZ(bx - 0.5f)) / 1f;
            BridgeDirection = new Vector2(-slope, 1f).normalized;
            Vector2 n = BridgeDirection;
            Vector2 afterBridge = BridgeCentre + n * 6f;
            Vector2 toRest = FoothillRest - afterBridge;
            Vector2 mid = afterBridge + toRest * 0.5f + new Vector2(-toRest.y, toRest.x).normalized * 6f;
            var forest = new[]
            {
                Entrance, Entrance + new Vector2(-3f, 6f) * k, new Vector2(-10f * k, -86f * k), Glade,
                BridgeCentre - n * 6f, BridgeCentre - n * 3f, BridgeCentre + n * 3f, afterBridge, mid, FoothillRest,
            };
            foreach (Vector2 p in CatmullRom(forest, Step)) raw.Add((p, hw * 0.95f, TrailSection.Forest));

            // B. Switchbacks up the skirt: constant-grade traverses with semicircular hairpins.
            float r = startRadius, angle = startAngle, dir = 1f;
            float angleMin = startAngle - 0.05f, angleMax = startAngle + 0.75f;
            float skirtGrade = s.MaxGrade * 0.38f;
            float skirtTop = TierShelf * R + 0.5f;
            for (int guard = 0; r > skirtTop && guard < 4000; guard++)
            {
                float sinB = SinClimb(r, skirtGrade);
                r -= sinB * Step;
                angle += dir * Mathf.Sqrt(1f - sinB * sinB) * Step / r;
                raw.Add((Polar(r, angle), hw, TrailSection.Switchbacks));
                if ((dir > 0f && angle > angleMax) || (dir < 0f && angle < angleMin))
                {
                    if (r - 2f * HairpinRadius < skirtTop) break; // the shelf is close enough: no more turns needed
                    Vector2 p = Polar(r, angle);
                    Vector2 inward = (Mountain - p).normalized;
                    Vector2 tangent = new Vector2(-inward.y, inward.x) * -dir; // direction of travel around the mountain
                    if (Vector2.Dot(tangent, Polar(r, angle + dir * 0.01f) - p) < 0f) tangent = -tangent;
                    Vector2 centre = p + inward * HairpinRadius;
                    Hairpins.Add(centre);
                    int steps = Mathf.CeilToInt(Mathf.PI * HairpinRadius / Step);
                    for (int i = 1; i <= steps; i++)
                    {
                        float phi = Mathf.PI * i / steps;
                        Vector2 q = centre + (-inward * Mathf.Cos(phi) + tangent * Mathf.Sin(phi)) * HairpinRadius;
                        raw.Add((q, hw * 1.35f, TrailSection.Switchbacks));
                    }
                    Vector2 end = raw[raw.Count - 1].p - Mountain;
                    r = end.magnitude;
                    angle = Mathf.Atan2(end.y, end.x);
                    // Keep the angle continuous with the limits.
                    while (angle - angleMax > Mathf.PI) angle -= Mathf.PI * 2f;
                    while (angleMin - angle > Mathf.PI) angle += Mathf.PI * 2f;
                    dir = -dir;
                }
            }

            // C. Shelf: an almost level traverse along the mountain's waist past the Eagle Ledge viewpoint.
            float shelfStartRadius = r;
            float shelfSpan = 1.15f;
            int shelfSteps = Mathf.CeilToInt(shelfSpan * ShelfRadius / Step);
            for (int i = 1; i <= shelfSteps; i++)
            {
                float f = i / (float)shelfSteps;
                float rr = Mathf.Lerp(shelfStartRadius, ShelfRadius, SmoothStep(0f, 0.35f, f));
                float a = angle + dir * shelfSpan * f;
                raw.Add((Polar(rr, a), hw * (Mathf.Abs(f - 0.5f) < 0.12f ? 1.3f : 1f), TrailSection.Shelf));
            }
            float ledgeAngle = angle + dir * shelfSpan * 0.5f;
            LedgeOutward = new Vector2(Mathf.Cos(ledgeAngle), Mathf.Sin(ledgeAngle));
            Ledge = Polar(ShelfRadius + 4.5f, ledgeAngle);
            angle += dir * shelfSpan;
            r = ShelfRadius;

            // D. Summit spiral: a ledge cut around the steep cone at a steady grade.
            float coneGrade = s.MaxGrade * 0.8f;
            for (int guard = 0; r > PlateauRadius + 1f && guard < 6000; guard++)
            {
                float sinB = SinClimb(r, coneGrade);
                r -= sinB * Step;
                angle += dir * Mathf.Sqrt(1f - sinB * sinB) * Step / Mathf.Max(r, 1f);
                raw.Add((Polar(r, angle), hw * 0.95f, TrailSection.Summit));
            }

            // E. Final approach: straight to the summit centre, through the landmark arch.
            Vector2 last = raw[raw.Count - 1].p;
            SummitApproach = (Mountain - last).normalized;
            int finalSteps = Mathf.CeilToInt(Vector2.Distance(last, Mountain) / Step);
            for (int i = 1; i <= finalSteps; i++)
                raw.Add((Vector2.Lerp(last, Mountain, i / (float)finalSteps), hw * 1.15f, TrailSection.Summit));

            Resample(raw);
            AssignHeights();
            BuildPlatforms();
            PinPlatforms();
            BuildBuckets();
        }

        /// <summary>Sine of the angle off the contour line that climbs at <paramref name="grade"/> on the profile.</summary>
        float SinClimb(float r, float grade)
        {
            float slope = ProfileSlope(r);
            return slope <= grade ? 1f : grade / slope;
        }

        static List<Vector2> CatmullRom(IReadOnlyList<Vector2> points, float step)
        {
            var result = new List<Vector2> { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p0 = points[Mathf.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
                for (int k = 1; k <= steps; k++)
                {
                    float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            return result;
        }

        void Resample(List<(Vector2 p, float hw, TrailSection section)> raw)
        {
            Trail.Clear();
            float travelled = 0f, nextAt = 0f;
            for (int i = 0; i < raw.Count - 1; i++)
            {
                Vector2 a = raw[i].p, b = raw[i + 1].p;
                float len = Vector2.Distance(a, b);
                while (nextAt <= travelled + len)
                {
                    float f = len > 1e-5f ? (nextAt - travelled) / len : 0f;
                    Vector2 p = Vector2.Lerp(a, b, f);
                    bool onBridge = Mathf.Abs(Vector2.Dot(p - BridgeCentre, BridgeDirection)) <= BridgeHalfSpan &&
                                    Vector2.Distance(p, BridgeCentre) < BridgeHalfSpan + 0.5f;
                    TrailSection section = raw[i].section;
                    Trail.Add(new TrailPoint
                    {
                        Position = p,
                        HalfWidth = Mathf.Lerp(raw[i].hw, raw[i + 1].hw, f),
                        Section = section,
                        Surface = onBridge ? TrailSurface.Bridge : section == TrailSection.Shelf || section == TrailSection.Summit ? TrailSurface.Stone : TrailSurface.Dirt,
                        Distance = nextAt,
                    });
                    nextAt += Spacing;
                }
                travelled += len;
            }
            Vector2 end = raw[raw.Count - 1].p;
            if (Vector2.Distance(Trail[Trail.Count - 1].Position, end) > 0.05f)
                Trail.Add(new TrailPoint { Position = end, HalfWidth = raw[raw.Count - 1].hw, Section = TrailSection.Summit, Surface = TrailSurface.Stone, Distance = travelled });
        }

        void AssignHeights()
        {
            int count = Trail.Count;
            var h = new float[count];
            for (int i = 0; i < count; i++) h[i] = NaturalHeight(Trail[i].Position.x, Trail[i].Position.y, brook: false);

            // Smooth bumps, then limit the grade both ways (a few passes converge).
            var smooth = new float[count];
            const int window = 5;
            for (int i = 0; i < count; i++)
            {
                float sum = 0f;
                int n = 0;
                for (int j = Mathf.Max(0, i - window); j <= Mathf.Min(count - 1, i + window); j++, n++) sum += h[j];
                smooth[i] = sum / n;
            }
            smooth[count - 1] = NaturalHeight(Mountain.x, Mountain.y);
            float rise = Settings.MaxGrade * 0.92f * Spacing;
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 1; i < count; i++) smooth[i] = Mathf.Clamp(smooth[i], smooth[i - 1] - rise, smooth[i - 1] + rise);
                for (int i = count - 2; i >= 0; i--) smooth[i] = Mathf.Clamp(smooth[i], smooth[i + 1] - rise, smooth[i + 1] + rise);
            }

            // The bridge deck is level with both banks.
            float deck = 0f;
            int deckPoints = 0;
            for (int i = 0; i < count; i++)
                if (Trail[i].Surface == TrailSurface.Bridge) { deck += smooth[i]; deckPoints++; }
            if (deckPoints > 0)
            {
                deck /= deckPoints;
                for (int i = 0; i < count; i++)
                    if (Trail[i].Surface == TrailSurface.Bridge) smooth[i] = deck;
                BridgeDeckHeight = deck;
            }

            for (int i = 0; i < count; i++)
            {
                TrailPoint p = Trail[i];
                p.Height = smooth[i];
                Vector2 q = p.Position;
                float gx = NaturalHeight(q.x + 1f, q.y, false) - NaturalHeight(q.x - 1f, q.y, false);
                float gz = NaturalHeight(q.x, q.y + 1f, false) - NaturalHeight(q.x, q.y - 1f, false);
                float slope = 0.5f * Mathf.Sqrt(gx * gx + gz * gz);
                p.Margin = FlatMargin + Mathf.Clamp(slope, 0f, 1.6f) * 1.6f;
                Trail[i] = p;
            }
        }

        void BuildPlatforms()
        {
            Platforms.Clear();
            Platforms.Add(new Platform { Name = "Whispering Glade", Centre = Glade, Radius = 7f, Height = TrailHeightNear(Glade) });
            Vector2 restOutward = (FoothillRest - Mountain).normalized;
            Vector2 restCentre = FoothillRest + restOutward * 3.5f;
            Platforms.Add(new Platform { Name = "Foothill Rest", Centre = restCentre, Radius = 4.5f, Height = TrailHeightNear(FoothillRest) });
            Platforms.Add(new Platform { Name = "Eagle Ledge", Centre = Ledge, Radius = 4.5f, Height = TrailHeightNear(Ledge) });
            Platforms.Add(new Platform { Name = "Cloudpeak Summit", Centre = Mountain, Radius = PlateauRadius + 1.5f, Height = Trail[Trail.Count - 1].Height });
        }

        /// <summary>
        /// The trail is level where it crosses a platform (so the flattened area and the trail agree), and
        /// grade-limited back to its own profile on either side.
        /// </summary>
        void PinPlatforms()
        {
            int count = Trail.Count;
            var h = new float[count];
            var pinned = new bool[count];
            for (int i = 0; i < count; i++) h[i] = Trail[i].Height;
            foreach (Platform pl in Platforms)
                for (int i = 0; i < count; i++)
                    if (Trail[i].Surface != TrailSurface.Bridge && Vector2.Distance(Trail[i].Position, pl.Centre) <= pl.Radius + 0.5f)
                    {
                        h[i] = pl.Height;
                        pinned[i] = true;
                    }
            float rise = Settings.MaxGrade * 0.92f * Spacing;
            for (int pass = 0; pass < 6; pass++)
            {
                for (int i = 1; i < count; i++)
                    if (!pinned[i]) h[i] = Mathf.Clamp(h[i], h[i - 1] - rise, h[i - 1] + rise);
                for (int i = count - 2; i >= 0; i--)
                    if (!pinned[i]) h[i] = Mathf.Clamp(h[i], h[i + 1] - rise, h[i + 1] + rise);
            }
            for (int i = 0; i < count; i++)
            {
                TrailPoint p = Trail[i];
                p.Height = h[i];
                Trail[i] = p;
            }
        }

        public float TrailHeightNear(Vector2 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < Trail.Count; i++)
            {
                float d = (Trail[i].Position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return Trail[best].Height;
        }

        // ───────────────────────────── Spatial lookup ─────────────────────────────

        static long Key(int bx, int bz) => ((long)bx << 32) ^ (uint)bz;

        void BuildBuckets()
        {
            buckets.Clear();
            for (int i = 0; i < Trail.Count - 1; i++)
            {
                Vector2 a = Trail[i].Position, b = Trail[i + 1].Position;
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / BucketSize), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / BucketSize);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / BucketSize), z1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / BucketSize);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!buckets.TryGetValue(Key(x, z), out List<int> list)) buckets[Key(x, z)] = list = new List<int>();
                        list.Add(i);
                    }
            }
        }

        /// <summary>Nearest trail segment within <paramref name="maxDistance"/>; false if none.</summary>
        public bool NearestTrail(Vector2 p, float maxDistance, out int segment, out float t, out float distance)
        {
            segment = -1;
            t = 0f;
            distance = float.MaxValue;
            int x0 = Mathf.FloorToInt((p.x - maxDistance) / BucketSize), x1 = Mathf.FloorToInt((p.x + maxDistance) / BucketSize);
            int z0 = Mathf.FloorToInt((p.y - maxDistance) / BucketSize), z1 = Mathf.FloorToInt((p.y + maxDistance) / BucketSize);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    if (!buckets.TryGetValue(Key(x, z), out List<int> list)) continue;
                    foreach (int i in list)
                    {
                        Vector2 a = Trail[i].Position, ab = Trail[i + 1].Position - a;
                        float f = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                        float d = Vector2.Distance(p, a + ab * f);
                        if (d < distance) { distance = d; segment = i; t = f; }
                    }
                }
            return segment >= 0 && distance <= maxDistance;
        }

        /// <summary>Distance from <paramref name="p"/> to the nearest trail edge (negative = on the trail); +∞ beyond <paramref name="maxDistance"/>.</summary>
        public float DistanceToTrailEdge(Vector2 p, float maxDistance = 12f)
        {
            if (!NearestTrail(p, maxDistance + 3f, out int seg, out float t, out float d)) return float.PositiveInfinity;
            return d - Mathf.Lerp(Trail[seg].HalfWidth, Trail[seg + 1].HalfWidth, t);
        }

        // ───────────────────────────── Carved grid ─────────────────────────────

        void BuildGrid()
        {
            int nv = Vertices;
            heights = new float[nv * nv];
            float reach = 2.5f + FlatMargin + 2.6f + Shoulder;
            for (int j = 0; j < nv; j++)
            {
                for (int i = 0; i < nv; i++)
                {
                    float x = Origin + i * Cell, z = Origin + j * Cell;
                    var p = new Vector2(x, z);
                    float natural = NaturalHeight(x, z);
                    float weight = 0f, target = natural;

                    if (NearestTrail(p, reach * 1.3f, out int seg, out float t, out float d))
                    {
                        TrailPoint a = Trail[seg], b = Trail[seg + 1];
                        float inner = Mathf.Lerp(a.HalfWidth + a.Margin, b.HalfWidth + b.Margin, t);
                        float w = 1f - SmoothStep(inner, inner + Shoulder, d);
                        // Under the bridge the brook bed stays open.
                        if (a.Surface == TrailSurface.Bridge || b.Surface == TrailSurface.Bridge)
                            w *= SmoothStep(0.8f, 1.9f, Mathf.Abs(z - BrookZ(x)));
                        if (w > 0f)
                        {
                            weight = w;
                            target = Mathf.Lerp(a.Height, b.Height, t);
                        }
                    }
                    foreach (Platform pl in Platforms)
                    {
                        float w = 1f - SmoothStep(pl.Radius, pl.Radius + Shoulder, Vector2.Distance(p, pl.Centre));
                        if (w > weight + 0.001f)
                        {
                            weight = w;
                            target = pl.Height;
                        }
                    }
                    heights[i + j * nv] = Mathf.Lerp(natural, target, weight);
                }
            }
        }

        /// <summary>
        /// Height of the carved terrain surface, matching the mesh triangulation exactly
        /// (cell quads split along the (i+1, j)–(i, j+1) diagonal).
        /// </summary>
        public float Surface(float x, float z)
        {
            float fx = Mathf.Clamp((x - Origin) / Cell, 0f, Cells - 1e-4f);
            float fz = Mathf.Clamp((z - Origin) / Cell, 0f, Cells - 1e-4f);
            int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
            float u = fx - i, v = fz - j;
            float a = HeightAt(i, j), b = HeightAt(i + 1, j), c = HeightAt(i, j + 1), d = HeightAt(i + 1, j + 1);
            return u + v <= 1f ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1f - u) + (b - d) * (1f - v);
        }

        public Vector3 OnSurface(Vector2 p, float lift = 0f) => new Vector3(p.x, Surface(p.x, p.y) + lift, p.y);

        /// <summary>Upward unit normal of the carved surface (finite differences).</summary>
        public Vector3 SurfaceNormal(Vector2 p, float e = 0.6f)
        {
            float hx = Surface(p.x + e, p.y) - Surface(p.x - e, p.y);
            float hz = Surface(p.x, p.y + e) - Surface(p.x, p.y - e);
            return new Vector3(-hx, 2f * e, -hz).normalized;
        }

        // ───────────────────────────── Validation ─────────────────────────────

        public struct Report
        {
            public float Length, MaxGap, MaxGrade, MaxCrossSlopeDeg, MaxCorridorSlopeDeg, MaxCut, MaxFill, StartHeight, EndHeight, SummitHeight;
            public int SelfOverlaps;
            public string OverlapAt, GradeAt, CrossAt;
            public bool SummitReached;
            public float GradeLimit;

            public bool Passed => MaxGap <= 1.0f && MaxGrade <= GradeLimit + 0.03f && MaxCrossSlopeDeg <= 15f && MaxCorridorSlopeDeg <= 35f &&
                                  SelfOverlaps == 0 && SummitReached;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Trail: {Length:0} m from {StartHeight:0.0} m to {EndHeight:0.0} m (summit {SummitHeight:0.0} m) — {(SummitReached ? "reaches the summit" : "DOES NOT reach the summit")}.");
                sb.AppendLine($"Continuity: largest gap {MaxGap:0.00} m. Steepest grade {MaxGrade:0.000} at {GradeAt} (limit {GradeLimit:0.00}, {Mathf.Atan(MaxGrade) * Mathf.Rad2Deg:0.0}°).");
                sb.AppendLine($"Cross-slope ≤ {MaxCrossSlopeDeg:0.0}° (at {CrossAt}), terrain within the trail ≤ {MaxCorridorSlopeDeg:0.0}° (controller limit 45°). Cut {MaxCut:0.0} m / fill {MaxFill:0.0} m.");
                sb.Append(SelfOverlaps == 0 ? "No trail section overlaps another." : $"⚠ {SelfOverlaps} places where trail sections overlap ({OverlapAt}).");
                return sb.ToString();
            }
        }

        public Report Validate()
        {
            var r = new Report { GradeLimit = Settings.MaxGrade };
            r.Length = Trail[Trail.Count - 1].Distance;
            r.StartHeight = Surface(Trail[0].Position.x, Trail[0].Position.y);
            r.EndHeight = Surface(Trail[Trail.Count - 1].Position.x, Trail[Trail.Count - 1].Position.y);
            r.SummitHeight = NaturalHeight(Mountain.x, Mountain.y);
            r.SummitReached = r.EndHeight >= r.SummitHeight - 1f && Vector2.Distance(Trail[Trail.Count - 1].Position, Mountain) < 1f;

            for (int i = 1; i < Trail.Count; i++)
                r.MaxGap = Mathf.Max(r.MaxGap, Vector2.Distance(Trail[i].Position, Trail[i - 1].Position));

            // Grade the player actually walks: carved surface over ~3 m windows (bridge deck excluded; it has its own gentle arch).
            const int span = 4;
            for (int i = span; i < Trail.Count; i++)
            {
                TrailPoint a = Trail[i - span], b = Trail[i];
                if (Vector2.Distance(a.Position, BridgeCentre) < BridgeHalfSpan + 1.5f || Vector2.Distance(b.Position, BridgeCentre) < BridgeHalfSpan + 1.5f) continue;
                float run = b.Distance - a.Distance;
                float rise = Mathf.Abs(Surface(b.Position.x, b.Position.y) - Surface(a.Position.x, a.Position.y));
                if (rise / run > r.MaxGrade)
                {
                    r.MaxGrade = rise / run;
                    r.GradeAt = $"{b.Distance:0} m ({b.Section})";
                }
            }

            for (int i = 1; i < Trail.Count - 1; i++)
            {
                TrailPoint p = Trail[i];
                if (p.Surface == TrailSurface.Bridge) continue;
                Vector2 dir = (Trail[i + 1].Position - Trail[i - 1].Position).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x) * (p.HalfWidth * 0.85f);
                float centre = Surface(p.Position.x, p.Position.y);
                float left = Surface(p.Position.x - side.x, p.Position.y - side.y), right = Surface(p.Position.x + side.x, p.Position.y + side.y);
                float cross = Mathf.Max(Mathf.Abs(left - centre), Mathf.Abs(right - centre)) / side.magnitude;
                if (Mathf.Atan(cross) * Mathf.Rad2Deg > r.MaxCrossSlopeDeg)
                {
                    r.MaxCrossSlopeDeg = Mathf.Atan(cross) * Mathf.Rad2Deg;
                    r.CrossAt = $"{p.Distance:0} m ({p.Section})";
                }
                foreach (Vector2 q in new[] { p.Position, p.Position - side, p.Position + side })
                    r.MaxCorridorSlopeDeg = Mathf.Max(r.MaxCorridorSlopeDeg, Vector3.Angle(SurfaceNormal(q, 0.5f), Vector3.up));
                float natural = NaturalHeight(p.Position.x, p.Position.y, brook: false);
                r.MaxCut = Mathf.Max(r.MaxCut, natural - p.Height);
                r.MaxFill = Mathf.Max(r.MaxFill, p.Height - natural);
            }

            // Sections far apart along the trail must not be side by side (their carving would fight).
            for (int i = 0; i < Trail.Count; i += 2)
            {
                if (!NearestTrailExcluding(Trail[i], 25f, out float d, out int j)) continue;
                if (d < Trail[i].HalfWidth + Trail[j].HalfWidth + Trail[i].Margin + Trail[j].Margin + 0.5f)
                {
                    if (r.SelfOverlaps < 3) r.OverlapAt += $"{(r.OverlapAt == null ? "" : "; ")}{Trail[i].Distance:0} m ({Trail[i].Section}) beside {Trail[j].Distance:0} m ({Trail[j].Section}), {d:0.0} m apart";
                    r.SelfOverlaps++;
                }
            }
            return r;
        }

        bool NearestTrailExcluding(TrailPoint p, float minAlong, out float distance, out int index)
        {
            distance = float.MaxValue;
            index = -1;
            for (int j = 0; j < Trail.Count; j++)
            {
                if (Mathf.Abs(Trail[j].Distance - p.Distance) < minAlong) continue;
                float d = Vector2.Distance(Trail[j].Position, p.Position);
                if (d < distance) { distance = d; index = j; }
            }
            return index >= 0;
        }
    }
}
