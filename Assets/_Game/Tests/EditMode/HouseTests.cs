using System.Collections.Generic;
using System.Linq;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Editor.House;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>The rural courtyard house as data: modular, valid meshes, within budget, entrances not blocked by colliders.</summary>
    public class HouseTests
    {
        [Test]
        public void House_IsModular_WithValidMeshes_AndMobileBudget()
        {
            RuralCourtyardHouse.Layout layout = RuralCourtyardHouse.Build();
            string[] expected = { "Courtyard", "Section_Terracotta", "Staircase", "Section_Cream", "Section_Teal", "WaterBasin", "BoundaryWall", "Interior" };
            Assert.That(layout.Modules.Select(m => m.Name), Is.EquivalentTo(expected));
            Assert.That(layout.Fixtures.Count, Is.GreaterThanOrEqualTo(12), "doors, grilles and windows are separate objects");
            Assert.That(layout.Triangles, Is.LessThan(20000), "entry-level Android budget");
            foreach (RuralCourtyardHouse.Module module in layout.Modules)
            {
                Mesh mesh = module.Part.ToMesh(module.Name, out HouseMat[] materials);
                var problems = new List<string>();
                bool valid = MeshValidator.Validate(mesh, problems);
                int submeshes = mesh.subMeshCount;
                Object.DestroyImmediate(mesh);
                Assert.That(valid, Is.True, module.Name + ": " + string.Join("; ", problems));
                Assert.That(submeshes, Is.EqualTo(materials.Length), module.Name);
            }
        }

        [Test]
        public void Entrances_AreNotInsideAnyCollider()
        {
            RuralCourtyardHouse.Layout layout = RuralCourtyardHouse.Build();
            var boxes = layout.Modules.SelectMany(m => m.Part.Colliders).ToList();
            foreach (RuralCourtyardHouse.Fixture f in layout.Fixtures.Where(f => f.Collide))
                foreach (Bounds b in f.Part.Colliders)
                {
                    // Fixture colliders in house space (they are only ever placed at 0°, 90°, 180° or 270°).
                    Quaternion r = Quaternion.Euler(0f, f.Yaw, 0f);
                    var world = new Bounds(f.Position + r * b.center, Vector3.zero);
                    foreach (Vector3 corner in new[] { -1f, 1f }.SelectMany(x => new[] { -1f, 1f }.SelectMany(y => new[] { -1f, 1f }.Select(z => new Vector3(x, y, z)))))
                        world.Encapsulate(f.Position + r * (b.center + Vector3.Scale(b.extents, corner)));
                    boxes.Add(world);
                }
            foreach ((string name, Vector3 point) in RuralCourtyardHouse.Entrances)
            {
                // A player-sized column above the standing point must be free of solid boxes.
                var column = new Bounds(point + Vector3.up * 1.0f, new Vector3(0.6f, 1.6f, 0.6f));
                Bounds? hit = boxes.FirstOrDefault(b => b.Intersects(column) && b.max.y > point.y + 0.15f);
                Assert.That(boxes.Any(b => b.Intersects(column) && b.max.y > point.y + 0.15f), Is.False, $"{name} is blocked by a collider at {hit}");
            }
        }
    }
}
