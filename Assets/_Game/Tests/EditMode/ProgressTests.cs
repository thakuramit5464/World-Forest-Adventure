using System.Collections.Generic;
using Greenhaven.Collectibles;
using Greenhaven.Core;
using Greenhaven.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    /// <summary>Save data round-trips and survives bad input; restored quest progress keeps working.</summary>
    public class ProgressTests
    {
        static QuestDefinition Quest(string id, params QuestStep[] steps)
        {
            var q = ScriptableObject.CreateInstance<QuestDefinition>();
            q.Configure(id, id, "", null, steps);
            return q;
        }

        [Test]
        public void SaveData_RoundTrips_ItemsQuestsAndFlags()
        {
            var items = new CollectedItems();
            items.TryCollect("supply_01", "supply");
            items.TryCollect("spectacles_01", "spectacles");
            var quests = new QuestLog();
            QuestDefinition q = Quest("missing_supplies", new QuestStep("find", ObjectiveType.Collect, "supply", 3), new QuestStep("return", ObjectiveType.TalkTo, "mira"));
            quests.Start(q);
            quests.Refresh(q, items.CountOf);
            var flags = new HashSet<string> { "puzzle:cairns" };

            string json = SaveSerializer.ToJson(SaveSerializer.Capture(items, quests, flags, "GreenhavenVillage", "from_cloudpeak"));
            SaveData back = SaveSerializer.FromJson(json, out string problem);
            Assert.That(problem, Is.Null);

            var items2 = new CollectedItems();
            var quests2 = new QuestLog();
            var flags2 = new HashSet<string>();
            SaveSerializer.Apply(back, items2, quests2, flags2);
            Assert.That(items2.IsCollected("supply_01") && items2.IsCollected("spectacles_01"));
            Assert.That(items2.CountOf("supply"), Is.EqualTo(1));
            Assert.That(quests2.StateOf(q), Is.EqualTo(QuestState.Active));
            Assert.That(quests2.Get(q).Count, Is.EqualTo(1));
            Assert.That(flags2, Does.Contain("puzzle:cairns"));
            Assert.That(back.scene, Is.EqualTo("GreenhavenVillage"));
            Object.DestroyImmediate(q);
        }

        [Test]
        public void RestoredQuest_ContinuesFromItsStep()
        {
            QuestDefinition q = Quest("mountain_clue",
                new QuestStep("shrine", ObjectiveType.Inspect, "summit_shrine"), new QuestStep("cairns", ObjectiveType.Inspect, "cairn_puzzle"),
                new QuestStep("rowan", ObjectiveType.TalkTo, "rowan"));
            var log = new QuestLog();
            log.Restore("mountain_clue", QuestState.Active, 1, 0);
            Assert.That(log.Report(q, ObjectiveType.Inspect, "summit_shrine"), Is.False, "step 0 is already done");
            Assert.That(log.Report(q, ObjectiveType.Inspect, "cairn_puzzle"), Is.True);
            Assert.That(log.Report(q, ObjectiveType.TalkTo, "rowan"), Is.True);
            Assert.That(log.StateOf(q), Is.EqualTo(QuestState.Completed));
            Object.DestroyImmediate(q);
        }

        [Test]
        public void BadOrOldSaveData_NeverThrows()
        {
            Assert.That(SaveSerializer.FromJson("", out string p1), Is.Null);
            Assert.That(p1, Is.Not.Null);
            Assert.That(SaveSerializer.FromJson("{ not json", out string p2), Is.Null);
            Assert.That(p2, Is.Not.Null);

            // A step index beyond the quest's steps (quest shortened in an update) must not crash.
            QuestDefinition q = Quest("short", new QuestStep("only", ObjectiveType.TalkTo, "x"));
            var log = new QuestLog();
            log.Restore("short", QuestState.Active, 5, 2);
            Assert.That(log.Report(q, ObjectiveType.TalkTo, "x"), Is.False);
            Assert.That(log.Refresh(q, _ => 0), Is.False);

            var items = new CollectedItems();
            var quests = new QuestLog();
            var flags = new HashSet<string>();
            SaveSerializer.Apply(new SaveData { items = null, quests = null, flags = null }, items, quests, flags);
            SaveSerializer.Apply(null, items, quests, flags);
            Object.DestroyImmediate(q);
        }
    }
}
