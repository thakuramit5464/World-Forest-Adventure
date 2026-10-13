using System.Collections.Generic;
using Greenhaven.Editor.AssetGenerator;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>Adventurer v2 built in memory from the owner's OBJ: valid Humanoid, taller, with face and scarf bones.</summary>
    public class AdventurerV2Tests
    {
        [Test]
        public void V2_IsAValidTallerHumanoid_WithExpressionAndScarfBones()
        {
            string folder = AdventurerModelRig.SourceFolder();
            if (folder == null) Assert.Ignore("Greenhaven_Adventurer.obj is not in ArtSource/Characters/GreenhavenAdventurer.");
            HeroGenerator.Result v1 = AdventurerModelRig.BuildInMemory(folder, out _, out _);
            HeroGenerator.Result v2 = AdventurerModelRig.BuildInMemory(folder, out _, out _, true, out Dictionary<string, Transform> extras);
            try
            {
                var problems = new List<string>();
                Assert.That(MeshValidator.Validate(v2.Mesh, problems), Is.True, string.Join("\n", problems));
                Assert.That(v2.Avatar.isValid && v2.Avatar.isHuman, Is.True, "valid Humanoid avatar");
                Assert.That(v2.Mesh.bounds.max.y, Is.EqualTo(1.84f).Within(0.02f), "about 12 % taller");
                Assert.That(v2.Mesh.bounds.min.y, Is.EqualTo(0f).Within(0.03f), "feet on the ground");
                Assert.That(v2.Mesh.bounds.max.y / v1.Mesh.bounds.max.y, Is.InRange(1.08f, 1.16f));
                foreach (string bone in new[] { "BrowL", "BrowR", "Mouth", "ScarfTail0", "ScarfTail1" })
                    Assert.That(extras.ContainsKey(bone) && extras[bone] != null, Is.True, bone);
                Assert.That(v2.Mesh.boneWeights.Length, Is.EqualTo(v2.Mesh.vertexCount));
                Assert.That(v2.Renderer.bones.Length, Is.EqualTo(v2.Mesh.bindposes.Length));

                // Legs are a larger share of the height than in v1 (more natural proportions).
                float LegShare(HeroGenerator.Result r)
                {
                    Transform hips = r.Root.transform.Find("Armature/Hips");
                    return hips.position.y / r.Mesh.bounds.max.y;
                }
                Assert.That(LegShare(v2), Is.GreaterThan(LegShare(v1) + 0.02f), "longer legs relative to the body");
            }
            finally
            {
                foreach (HeroGenerator.Result r in new[] { v1, v2 })
                {
                    Object.DestroyImmediate(r.Root);
                    Object.DestroyImmediate(r.Mesh);
                    Object.DestroyImmediate(r.Avatar);
                }
            }
        }
    }
}
