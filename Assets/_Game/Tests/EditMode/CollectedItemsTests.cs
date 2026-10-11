using Greenhaven.Collectibles;
using NUnit.Framework;

namespace Greenhaven.Tests
{
    public class CollectedItemsTests
    {
        [Test]
        public void FirstCollect_Succeeds_AndCounts()
        {
            var items = new CollectedItems();
            Assert.That(items.TryCollect("supply_01", "supply"), Is.True);
            Assert.That(items.IsCollected("supply_01"), Is.True);
            Assert.That(items.CountOf("supply"), Is.EqualTo(1));
        }

        [Test]
        public void DuplicateCollect_IsRejected_AndDoesNotDoubleCount()
        {
            var items = new CollectedItems();
            items.TryCollect("supply_01", "supply");
            Assert.That(items.TryCollect("supply_01", "supply"), Is.False);
            Assert.That(items.CountOf("supply"), Is.EqualTo(1));
        }

        [Test]
        public void Counts_AreTrackedPerItemType()
        {
            var items = new CollectedItems();
            items.TryCollect("supply_01", "supply");
            items.TryCollect("supply_02", "supply");
            items.TryCollect("flower_01", "flower");
            Assert.That(items.CountOf("supply"), Is.EqualTo(2));
            Assert.That(items.CountOf("flower"), Is.EqualTo(1));
            Assert.That(items.CountOf("unknown"), Is.EqualTo(0));
        }

        [TestCase(null, "supply")]
        [TestCase("", "supply")]
        [TestCase("supply_01", null)]
        [TestCase("supply_01", "")]
        public void InvalidInput_IsRejected(string id, string type)
        {
            var items = new CollectedItems();
            Assert.That(items.TryCollect(id, type), Is.False);
            Assert.That(items.CollectedIds.Count, Is.EqualTo(0));
        }

        [Test]
        public void IsCollected_IsFalse_ForUnknownOrEmptyIds()
        {
            var items = new CollectedItems();
            Assert.That(items.IsCollected("nope"), Is.False);
            Assert.That(items.IsCollected(null), Is.False);
        }
    }
}
