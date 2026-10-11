using System.Collections.Generic;
using System.Linq;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Editor.Wildlife;
using Greenhaven.Wildlife;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>Generated animals in memory: valid skinned meshes, deterministic, standing on the ground, within budget.</summary>
    public class WildlifeTests
    {
        static IEnumerable<AnimalKind> Kinds => WildlifeGenerator.Kinds;

        static WildlifeGeneratorSettings Settings() => ScriptableObject.CreateInstance<WildlifeGeneratorSettings>();

        static Mesh Build(AnimalKind kind, int variant, WildlifeGeneratorSettings s, out AnimalBuilder builder)
        {
            builder = AnimalModels.Build(kind, variant, s, s.ToPalette());
            return builder.ToMesh($"Test_{kind}_{variant}");
        }

        [Test]
        public void EveryAnimal_IsAValidRigidlySkinnedMesh([ValueSource(nameof(Kinds))] AnimalKind kind)
        {
            WildlifeGeneratorSettings s = Settings();
            for (int v = 0; v < 3; v++)
            {
                Mesh mesh = Build(kind, v, s, out AnimalBuilder builder);
                var problems = new List<string>();
                bool valid = MeshValidator.Validate(mesh, problems);
                BoneWeight[] weights = mesh.boneWeights;
                int bindposes = mesh.bindposes.Length, vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3;
                Object.DestroyImmediate(mesh);
                Assert.That(valid, Is.True, string.Join("\n", problems));
                Assert.That(weights.Length, Is.EqualTo(vertices), "one bone weight per vertex");
                Assert.That(bindposes, Is.EqualTo(builder.BoneCount));
                Assert.That(weights.All(w => w.boneIndex0 >= 0 && w.boneIndex0 < bindposes && Mathf.Approximately(w.weight0, 1f)), "rigid weights on real bones");
                Assert.That(triangles, Is.LessThanOrEqualTo(1600), $"{kind} v{v}: mobile budget");
            }
            Object.DestroyImmediate(s);
        }

        [Test]
        public void Animals_StandOnTheGround_FacingForward([ValueSource(nameof(Kinds))] AnimalKind kind)
        {
            WildlifeGeneratorSettings s = Settings();
            Mesh mesh = Build(kind, 0, s, out _);
            Bounds b = mesh.bounds;
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(s);
            Assert.That(b.min.y, Is.EqualTo(0f).Within(0.02f), "feet on the ground");
            Assert.That(Mathf.Abs(b.center.x), Is.LessThan(0.05f), "left/right symmetric about the pivot");
            if (kind != AnimalKind.Bird) Assert.That(b.size.z, Is.GreaterThan(b.size.x), "longer than wide (faces +Z)");
        }

        [Test]
        public void SameSettings_GiveIdenticalAnimals_AndSeedsChangeThem([ValueSource(nameof(Kinds))] AnimalKind kind)
        {
            WildlifeGeneratorSettings s = Settings();
            Mesh a = Build(kind, 1, s, out _), b = Build(kind, 1, s, out _);
            s.seed = 999;
            s.sizeVariation = 0.2f;
            Mesh c = Build(kind, 1, s, out _);
            bool same = GeneratedAssetStore.SameVertices(a, b), changed = !GeneratedAssetStore.SameVertices(a, c);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
            Object.DestroyImmediate(s);
            Assert.That(same, Is.True, "deterministic");
            Assert.That(changed, Is.True, "seed/variation matter");
        }

        [Test]
        public void Bird_HasWings_AndQuadrupedsHaveFourLegs()
        {
            WildlifeGeneratorSettings s = Settings();
            var root = new GameObject("BirdTest");
            AnimalBuilder bird = AnimalModels.Build(AnimalKind.Bird, 0, s, s.ToPalette());
            Transform[] bones = bird.CreateBones(root.transform);
            Assert.That(bird.Find(bones, AnimalModels.WingL), Is.Not.Null);
            Assert.That(bird.Find(bones, AnimalModels.WingR), Is.Not.Null);
            Object.DestroyImmediate(root);

            var root2 = new GameObject("FoxTest");
            AnimalBuilder fox = AnimalModels.Build(AnimalKind.Fox, 0, s, s.ToPalette());
            Transform[] foxBones = fox.CreateBones(root2.transform);
            foreach (string leg in new[] { AnimalModels.LegFL, AnimalModels.LegFR, AnimalModels.LegHL, AnimalModels.LegHR })
                Assert.That(fox.Find(foxBones, leg), Is.Not.Null, leg);
            Assert.That(fox.Find(foxBones, AnimalModels.Tail1), Is.Not.Null, "two-bone bushy tail");
            Object.DestroyImmediate(root2);
            Object.DestroyImmediate(s);
        }

        [Test]
        public void SpeciesDefaults_AreSensible([ValueSource(nameof(Kinds))] AnimalKind kind)
        {
            var species = ScriptableObject.CreateInstance<WildlifeSpecies>();
            species.ApplyDefaults(kind);
            Assert.That(species.kind, Is.EqualTo(kind));
            Assert.That(species.fleeDistance, Is.LessThan(species.calmDistance), "stops fleeing further out than it starts");
            Assert.That(species.personalSpace, Is.LessThanOrEqualTo(species.detectDistance));
            Assert.That(species.walkSpeed, Is.LessThanOrEqualTo(species.fleeSpeed));
            Object.DestroyImmediate(species);
        }
    }
}
