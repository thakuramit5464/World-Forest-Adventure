using Greenhaven.Editor.Cloudpeak;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>Cloudpeak terrain and trail as data: deterministic, continuous, gentle enough to walk, reaches the summit.</summary>
    public class CloudpeakTests
    {
        static CloudpeakWorld Default() => CloudpeakWorld.Generate(CloudpeakSettings.Default);

        [Test]
        public void SameSettings_GiveTheSameWorld()
        {
            CloudpeakWorld a = Default(), b = Default();
            Assert.That(a.Trail.Count, Is.EqualTo(b.Trail.Count));
            for (int i = 0; i < a.Trail.Count; i += 37)
            {
                Assert.That(a.Trail[i].Position, Is.EqualTo(b.Trail[i].Position));
                Assert.That(a.Trail[i].Height, Is.EqualTo(b.Trail[i].Height));
            }
            Assert.That(a.Surface(10f, -40f), Is.EqualTo(b.Surface(10f, -40f)));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentTerrain()
        {
            CloudpeakSettings other = CloudpeakSettings.Default;
            other.Seed = 4321;
            Assert.That(CloudpeakWorld.Generate(other).NaturalHeight(-40f, -50f), Is.Not.EqualTo(Default().NaturalHeight(-40f, -50f)));
        }

        [TestCase(1234)]
        [TestCase(7)]
        [TestCase(2026)]
        public void Trail_PassesAllChecks(int seed)
        {
            CloudpeakSettings s = CloudpeakSettings.Default;
            s.Seed = seed;
            CloudpeakWorld.Report report = CloudpeakWorld.Generate(s).Validate();
            Assert.That(report.Passed, Is.True, report.ToString());
        }

        [Test]
        public void Trail_RunsFromTheEntranceToTheSummit_WithSwitchbacks()
        {
            CloudpeakWorld w = Default();
            Assert.That(Vector2.Distance(w.Trail[0].Position, w.Entrance), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(w.Trail[w.Trail.Count - 1].Position, w.Mountain), Is.LessThan(0.5f));
            Assert.That(w.Hairpins.Count, Is.GreaterThanOrEqualTo(1), "switchbacks on the climb");
            Assert.That(w.Platforms.Count, Is.GreaterThanOrEqualTo(4), "glade, rest stop, viewpoint, summit");
        }

        [Test]
        public void TrailSurface_IsFlatAcross_AndTerrainMatchesTheTrailHeight()
        {
            CloudpeakWorld w = Default();
            foreach (TrailPoint p in w.Trail)
            {
                if (Vector2.Distance(p.Position, w.BridgeCentre) < 4.7f) continue; // the deck spans the brook here
                Assert.That(Mathf.Abs(w.Surface(p.Position.x, p.Position.y) - p.Height), Is.LessThan(0.35f),
                    $"terrain at {p.Distance:0} m follows the trail (no floating path or clipping)");
            }
        }

        [Test]
        public void Settings_AreClampedToSafeRanges()
        {
            var silly = new CloudpeakSettings { HalfSize = 1000f, PeakHeight = 500f, MaxGrade = 2f, CellSize = 0.1f, TrailWidth = 0.5f };
            CloudpeakSettings s = silly.Clamped();
            Assert.That(s.HalfSize, Is.LessThanOrEqualTo(130f));
            Assert.That(s.PeakHeight, Is.LessThanOrEqualTo(70f));
            Assert.That(s.MaxGrade, Is.LessThanOrEqualTo(0.3f));
            Assert.That(s.CellSize, Is.GreaterThanOrEqualTo(1.5f));
            Assert.That(s.TrailWidth, Is.GreaterThanOrEqualTo(2.4f));
        }
    }
}
