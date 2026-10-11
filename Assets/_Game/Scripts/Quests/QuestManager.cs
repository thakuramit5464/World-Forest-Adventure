using System;
using Greenhaven.Collectibles;
using UnityEngine;

namespace Greenhaven.Quests
{
    /// <summary>
    /// Scene owner of quest progress. Starts quests (respecting prerequisites), listens for
    /// pickups, accepts talk/inspect reports, and raises events for the HUD.
    /// </summary>
    public sealed class QuestManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Quests in this region, in tracker priority order.")]
        QuestDefinition[] quests = Array.Empty<QuestDefinition>();
        [SerializeField] CollectibleRegistry registry;

        readonly QuestLog log = new QuestLog();

        public event Action<QuestDefinition> QuestStarted;
        public event Action<QuestDefinition> QuestProgressed;
        public event Action<QuestDefinition> QuestCompleted;

        public QuestLog Log => log;

        void Awake()
        {
            if (registry == null)
                Debug.LogWarning($"{nameof(QuestManager)} has no {nameof(CollectibleRegistry)}; Collect objectives cannot progress.", this);
        }

        void OnEnable()
        {
            if (registry != null) registry.Collected += OnCollected;
        }

        void OnDisable()
        {
            if (registry != null) registry.Collected -= OnCollected;
        }

        public QuestState StateOf(QuestDefinition quest) => log.StateOf(quest);

        public bool CanStart(QuestDefinition quest) =>
            quest != null && log.StateOf(quest) == QuestState.NotStarted &&
            (quest.Prerequisite == null || log.StateOf(quest.Prerequisite) == QuestState.Completed);

        public bool TryStart(QuestDefinition quest)
        {
            if (!CanStart(quest) || !log.Start(quest)) return false;
            QuestStarted?.Invoke(quest);
            // Items picked up before the quest began still count.
            if (log.Refresh(quest, CountOf)) Notify(quest);
            return true;
        }

        public void ReportTalk(string npcId) => ReportEvent(ObjectiveType.TalkTo, npcId);

        public void ReportInspect(string landmarkId) => ReportEvent(ObjectiveType.Inspect, landmarkId);

        void ReportEvent(ObjectiveType type, string targetId)
        {
            foreach (QuestDefinition quest in quests)
            {
                if (quest == null || log.StateOf(quest) != QuestState.Active) continue;
                if (!log.Report(quest, type, targetId)) continue;
                log.Refresh(quest, CountOf); // the next step may already be satisfied
                Notify(quest);
            }
        }

        void OnCollected(string itemType, int count, int total)
        {
            foreach (QuestDefinition quest in quests)
            {
                if (quest != null && log.StateOf(quest) == QuestState.Active && log.Refresh(quest, CountOf))
                    Notify(quest);
            }
        }

        int CountOf(string itemType) => registry != null ? registry.State.CountOf(itemType) : 0;

        void Notify(QuestDefinition quest)
        {
            if (log.StateOf(quest) == QuestState.Completed) QuestCompleted?.Invoke(quest);
            else QuestProgressed?.Invoke(quest);
        }

        /// <summary>The first active quest in priority order, or null.</summary>
        public QuestDefinition TrackedQuest
        {
            get
            {
                foreach (QuestDefinition quest in quests)
                    if (quest != null && log.StateOf(quest) == QuestState.Active) return quest;
                return null;
            }
        }

        /// <summary>Tracker text for the quest's current step, e.g. "Find the supply crates (1/3)".</summary>
        public string CurrentObjectiveText(QuestDefinition quest)
        {
            QuestProgress p = log.Get(quest);
            if (p == null || p.State != QuestState.Active) return string.Empty;
            QuestStep step = quest.GetStep(p.StepIndex);
            return step.RequiredCount > 1 ? $"{step.Description} ({p.Count}/{step.RequiredCount})" : step.Description;
        }
    }
}
