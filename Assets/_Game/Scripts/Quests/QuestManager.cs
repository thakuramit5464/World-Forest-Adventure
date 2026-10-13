using System;
using Greenhaven.Collectibles;
using Greenhaven.Core;
using UnityEngine;

namespace Greenhaven.Quests
{
    /// <summary>
    /// Scene owner of quest logic: starts quests, routes TalkTo/Inspect events and item pickups to the shared
    /// <see cref="QuestLog"/> in <see cref="GameProgress"/> (so progress carries between scenes and restarts),
    /// and raises UI events. The quest list is per scene: the tracker shows the first active one.
    /// </summary>
    public sealed class QuestManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Quests relevant in this scene, in tracker priority order.")]
        QuestDefinition[] quests = Array.Empty<QuestDefinition>();
        [SerializeField] CollectibleRegistry registry;

        QuestLog log;

        public event Action<QuestDefinition> QuestStarted;
        public event Action<QuestDefinition> QuestProgressed;
        public event Action<QuestDefinition> QuestCompleted;

        public QuestLog Log => log ??= GameProgress.Instance.Quests;

        void Awake()
        {
            if (registry == null)
                Debug.LogWarning($"{nameof(QuestManager)} has no {nameof(CollectibleRegistry)}; Collect objectives cannot progress.", this);
        }

        void OnEnable()
        {
            if (registry != null) registry.Collected += OnCollected;
        }

        void Start()
        {
            // Items picked up in another scene (or before a restart) may already satisfy a step.
            foreach (QuestDefinition quest in quests)
                if (quest != null && Log.StateOf(quest) == QuestState.Active && Log.Refresh(quest, CountOf)) Notify(quest);
        }

        void OnDisable()
        {
            if (registry != null) registry.Collected -= OnCollected;
        }

        public QuestState StateOf(QuestDefinition quest) => Log.StateOf(quest);

        public bool CanStart(QuestDefinition quest) =>
            quest != null && Log.StateOf(quest) == QuestState.NotStarted &&
            (quest.Prerequisite == null || Log.StateOf(quest.Prerequisite) == QuestState.Completed);

        public bool TryStart(QuestDefinition quest)
        {
            if (!CanStart(quest) || !Log.Start(quest)) return false;
            QuestStarted?.Invoke(quest);
            // Items picked up before the quest began still count.
            if (Log.Refresh(quest, CountOf)) Notify(quest);
            GameProgress.Instance.Save();
            return true;
        }

        public void ReportTalk(string npcId) => ReportEvent(ObjectiveType.TalkTo, npcId);

        public void ReportInspect(string landmarkId) => ReportEvent(ObjectiveType.Inspect, landmarkId);

        void ReportEvent(ObjectiveType type, string targetId)
        {
            foreach (QuestDefinition quest in quests)
            {
                if (quest == null || Log.StateOf(quest) != QuestState.Active) continue;
                if (!Log.Report(quest, type, targetId)) continue;
                Log.Refresh(quest, CountOf); // the next step may already be satisfied
                Notify(quest);
            }
        }

        void OnCollected(string itemType, int count, int total)
        {
            foreach (QuestDefinition quest in quests)
            {
                if (quest != null && Log.StateOf(quest) == QuestState.Active && Log.Refresh(quest, CountOf))
                    Notify(quest);
            }
        }

        int CountOf(string itemType) => registry != null ? registry.State.CountOf(itemType) : GameProgress.Instance.Items.CountOf(itemType);

        void Notify(QuestDefinition quest)
        {
            if (Log.StateOf(quest) == QuestState.Completed) QuestCompleted?.Invoke(quest);
            else QuestProgressed?.Invoke(quest);
            GameProgress.Instance.Save();
        }

        public QuestDefinition TrackedQuest
        {
            get
            {
                foreach (QuestDefinition quest in quests)
                    if (quest != null && Log.StateOf(quest) == QuestState.Active) return quest;
                return null;
            }
        }

        public string CurrentObjectiveText(QuestDefinition quest)
        {
            QuestProgress p = Log.Get(quest);
            if (p == null || p.State != QuestState.Active || p.StepIndex >= quest.StepCount) return string.Empty;
            QuestStep step = quest.GetStep(p.StepIndex);
            return step.RequiredCount > 1 ? $"{step.Description} ({p.Count}/{step.RequiredCount})" : step.Description;
        }
    }
}
