using System;
using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>Stable ids: they feed the per-category random stream, so never renumber existing values.</summary>
    public enum AssetCategory
    {
        PineTree = 1,
        BroadleafTree = 2,
        Rock = 3,
        Bush = 4,
        Flower = 5,
        Grass = 6,

        Cottage = 10,
        House = 11,
        Shop = 12,

        Well = 20,
        Bridge = 21,
        Fence = 22,
        Gate = 23,
        Bench = 24,
        Lantern = 25,
        Signpost = 26,
        Barrel = 27,
        Crate = 28,
        Planter = 29,
    }

    /// <summary>User-facing generation parameters. Stored with each mesh so validation can regenerate it.</summary>
    [Serializable]
    public struct GeneratorSettings
    {
        public int Seed;
        [Tooltip("Overall scale multiplier (1 = default storybook proportions).")]
        public float Size;
        [Tooltip("0 = uniform colours, 1 = strong per-asset and per-face variation.")]
        public float ColorVariation;
        [Tooltip("Foliage cluster count and smoothness (0.3 = low-end phones, 1 = full). 0 in older saved settings means full.")]
        public float FoliageDetail;

        /// <summary>Effective foliage detail, 0.3..1 (older settings without the field count as full detail).</summary>
        public float Detail => FoliageDetail <= 0f ? 1f : Mathf.Clamp(FoliageDetail, 0.3f, 1f);

        public static GeneratorSettings Default => new GeneratorSettings { Seed = 1234, Size = 1f, ColorVariation = 0.5f, FoliageDetail = 0.8f };
    }

    /// <summary>How the prefab should collide.</summary>
    public enum ColliderKind
    {
        /// <summary>Walk-through decoration (flowers, bushes).</summary>
        None,
        /// <summary>Capsule around the trunk only; foliage stays walk-through.</summary>
        TrunkCapsule,
        /// <summary>Convex MeshCollider from the render mesh (rocks; must stay ≤ 255 triangles).</summary>
        ConvexMesh,
        /// <summary>A few BoxColliders (buildings, fences, props).</summary>
        Boxes,
        /// <summary>Non-convex MeshCollider for static walkable shapes (bridge deck with rails).</summary>
        Mesh,
    }

    public struct GeneratedMesh
    {
        public Mesh Mesh;
        public ColliderKind Collider;
        /// <summary>For <see cref="ColliderKind.TrunkCapsule"/>: trunk radius and height in metres.</summary>
        public float TrunkRadius;
        public float TrunkHeight;
        /// <summary>For <see cref="ColliderKind.Boxes"/>: boxes in the prefab's local space.</summary>
        public Bounds[] Boxes;
    }

    public interface IAssetGenerator
    {
        AssetCategory Category { get; }
        /// <summary>Short name used in asset names, e.g. "Pine".</summary>
        string Prefix { get; }
        string DisplayName { get; }
        /// <summary>Window section: "Nature", "Buildings" or "Props".</summary>
        string Group { get; }
        int DefaultCount { get; }
        Palette Palette { get; }
        /// <summary>Style label for a variant, e.g. "Slender". Variants cycle through styles.</summary>
        string StyleName(int variant);
        GeneratedMesh Build(GeneratorSettings settings, int variant, string meshName);
    }

    public static class GenRandom
    {
        /// <summary>
        /// Independent, deterministic stream per (seed, category, variant): variant 2 is identical
        /// whether you generate 3 or 10 variants.
        /// </summary>
        public static System.Random For(int seed, AssetCategory category, int variant)
        {
            unchecked
            {
                int hash = seed * 73856093 ^ (int)category * 19349663 ^ (variant + 1) * 83492791;
                return new System.Random(hash);
            }
        }

        public static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }

    public static class Generators
    {
        public static readonly IAssetGenerator[] All =
        {
            new PineTreeGenerator(),
            new BroadleafTreeGenerator(),
            new RockGenerator(),
            new BushGenerator(),
            new FlowerGenerator(),
            new GrassGenerator(),
            new BuildingGenerator(AssetCategory.Cottage),
            new BuildingGenerator(AssetCategory.House),
            new BuildingGenerator(AssetCategory.Shop),
            new PropGenerator(AssetCategory.Well),
            new PropGenerator(AssetCategory.Bridge),
            new PropGenerator(AssetCategory.Fence),
            new PropGenerator(AssetCategory.Gate),
            new PropGenerator(AssetCategory.Bench),
            new PropGenerator(AssetCategory.Lantern),
            new PropGenerator(AssetCategory.Signpost),
            new PropGenerator(AssetCategory.Barrel),
            new PropGenerator(AssetCategory.Crate),
            new PropGenerator(AssetCategory.Planter),
        };

        public static IAssetGenerator ForPrefix(string prefix)
        {
            foreach (IAssetGenerator g in All)
                if (g.Prefix == prefix) return g;
            return null;
        }

        public static IAssetGenerator For(AssetCategory category)
        {
            foreach (IAssetGenerator g in All)
                if (g.Category == category) return g;
            return null;
        }
    }

    /// <summary>Small helpers shared by the generators.</summary>
    public static class GenUtil
    {
        public static Func<Vector3, Vector2> Village(VRamp ramp, int shade) =>
            n => VillagePalette.UV(ramp, shade + Palette.SlopeShade(n));

        public static Func<Vector3, Vector2> VillageFlat(VRamp ramp, int shade) => _ => VillagePalette.UV(ramp, shade);

        public static Bounds Box(Vector3 centre, Vector3 size) => new Bounds(centre, size);
    }
}
