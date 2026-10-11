using System;
using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    public enum AssetCategory
    {
        PineTree = 1,
        BroadleafTree = 2,
        Rock = 3,
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

        public static GeneratorSettings Default => new GeneratorSettings { Seed = 1234, Size = 1f, ColorVariation = 0.5f };
    }

    /// <summary>How the prefab should collide.</summary>
    public enum ColliderKind { TrunkCapsule, ConvexMesh }

    public struct GeneratedMesh
    {
        public Mesh Mesh;
        public ColliderKind Collider;
        /// <summary>For <see cref="ColliderKind.TrunkCapsule"/>: trunk radius and height in metres.</summary>
        public float TrunkRadius;
        public float TrunkHeight;
    }

    public interface IAssetGenerator
    {
        AssetCategory Category { get; }
        /// <summary>Short name used in asset names, e.g. "Pine".</summary>
        string Prefix { get; }
        string DisplayName { get; }
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
        };

        public static IAssetGenerator ForPrefix(string prefix)
        {
            foreach (IAssetGenerator g in All)
                if (g.Prefix == prefix) return g;
            return null;
        }
    }
}
