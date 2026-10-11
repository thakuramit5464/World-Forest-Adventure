using System.Collections.Generic;
using Greenhaven.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    public class QuestLogTests
    {
        QuestDefinition supplies;
        Dictionary<string, int> inventory;

        int CountOf(string itemType) => inventory.TryGetValue(itemType, out int n) ? n : 0;

        [SetUp]
        public void SetUp()
        {
            supplies = ScriptableObject.CreateInstance<QuestDefinition>();
            supplies.Configure("missing_supplies", "The Missing Supplies", "", null,
                new QuestStep("Find crates", ObjectiveType.Collect, "supply", 3),
                new QuestStep("Return to Mira", ObjectiveType.TalkTo, "mira"));
            inventory = new Dictionary<string, int>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(supplies);

        [Test]
        public void NewQuest_IsNotStarted_ThenActiveAfterStart()
        {
            var log = new QuestLog();
            Assert.That(log.StateOf(supplies), Is.EqualTo(QuestState.NotStarted));
            Assert.That(log.Start(supplies), Is.True);
            Assert.That(log.StateOf(supplies), Is.EqualTo(QuestState.Active));
        }

        [Test]
        public void Start_Twice_IsRejected()
        {
            var log = new QuestLog();
            log.Start(supplies);
            Assert.That(log.Start(supplies), Is.False);
        }

        [Test]
        public void Collect_TracksCount_AndAdvancesAtRequiredAmount()
        {
            var log = new QuestLog();
            log.Start(supplies);

            inventory["supply"] = 2;
            Assert.That(log.Refresh(supplies, CountOf), Is.True);
            Assert.That(log.Get(supplies).StepIndex, Is.EqualTo(0));
            Assert.That(log.Get(supplies).Count, Is.EqualTo(2));

            inventory["supply"] = 3;
            log.Refresh(supplies, CountOf);
            Assert.That(log.Get(supplies).StepIndex, Is.EqualTo(1));
            Assert.That(log.StateOf(supplies), Is.EqualTo(QuestState.Active));
        }

        [Test]
        public void ItemsCollectedBeforeStart_StillCount()
        {
            var log = new QuestLog();
            inventory["supply"] = 3;
            log.Start(supplies);
            log.Refresh(supplies, CountOf);
            Assert.That(log.Get(supplies).StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void TalkTo_OnlyCounts_WhenItIsTheCurrentStep()
        {
            var log = new QuestLog();
            log.Start(supplies);
            Assert.That(log.Report(supplies, ObjectiveType.TalkTo, "mira"), Is.False, "still collecting");

            inventory["supply"] = 3;
            log.Refresh(supplies, CountOf);
            Assert.That(log.Report(supplies, ObjectiveType.TalkTo, "rowan"), Is.False, "wrong NPC");
            Assert.That(log.Report(supplies, ObjectiveType.TalkTo, "mira"), Is.True);
            Assert.That(log.StateOf(supplies), Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void CompletedQuest_IgnoresFurtherReports()
        {
            var log = new QuestLog();
            inventory["supply"] = 3;
            log.Start(supplies);
            log.Refresh(supplies, CountOf);
            log.Report(supplies, ObjectiveType.TalkTo, "mira");

            Assert.That(log.Report(supplies, ObjectiveType.TalkTo, "mira"), Is.False);
            Assert.That(log.Refresh(supplies, CountOf), Is.False);
            Assert.That(log.StateOf(supplies), Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void NotStartedQuest_IgnoresProgress()
        {
            var log = new QuestLog();
            inventory["supply"] = 3;
            Assert.That(log.Refresh(supplies, CountOf), Is.False);
            Assert.That(log.Report(supplies, ObjectiveType.TalkTo, "mira"), Is.False);
            Assert.That(log.StateOf(supplies), Is.EqualTo(QuestState.NotStarted));
        }

        [Test]
        public void Prerequisite_IsExposedForTheManager()
        {
            var request = ScriptableObject.CreateInstance<QuestDefinition>();
            request.Configure("villagers_request", "A Villager's Request", "", supplies,
                new QuestStep("Find spectacles", ObjectiveType.Collect, "spectacles"));
            Assert.That(request.Prerequisite, Is.SameAs(supplies));
            Object.DestroyImmediate(request);
        }
    }
}
