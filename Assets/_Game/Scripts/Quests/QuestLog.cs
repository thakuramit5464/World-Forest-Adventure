using System;
using System.Collections.Generic;

namespace Greenhaven.Quests
{
    public enum QuestState { NotStarted, Active, Completed }

    /// <summary>Plain-data progress for one quest (serializable for saving later).</summary>
    [Serializable]
    public sealed class QuestProgress
    {
        public QuestState State;
        public int StepIndex;
        public int Count;
    }

    /// <summary>
    /// Mutable quest progress keyed by quest id. Pure logic with no scene dependencies,
    /// so it is unit-testable and maps directly onto save data.
    /// </summary>
    public sealed class QuestLog
    {
        readonly Dictionary<string, QuestProgress> progress = new Dictionary<string, QuestProgress>();

        public QuestState StateOf(QuestDefinition quest) =>
            quest != null && progress.TryGetValue(quest.QuestId, out QuestProgress p) ? p.State : QuestState.NotStarted;

        /// <summary>Progress for an active or completed quest, or null if not started.</summary>
        public QuestProgress Get(QuestDefinition quest) =>
            quest != null && progress.TryGetValue(quest.QuestId, out QuestProgress p) ? p : null;

        /// <summary>Starts a quest. Returns false if it is invalid or already started/completed.</summary>
        public bool Start(QuestDefinition quest)
        {
            if (quest == null || string.IsNullOrEmpty(quest.QuestId) || StateOf(quest) != QuestState.NotStarted) return false;
            var p = new QuestProgress { State = QuestState.Active };
            progress[quest.QuestId] = p;
            if (quest.StepCount == 0) p.State = QuestState.Completed;
            return true;
        }

        /// <summary>
        /// Reports an event objective (TalkTo/Inspect). Advances only if it matches the CURRENT step.
        /// Returns true if progress changed.
        /// </summary>
        public bool Report(QuestDefinition quest, ObjectiveType type, string targetId)
        {
            QuestProgress p = Get(quest);
            if (p == null || p.State != QuestState.Active || type == ObjectiveType.Collect) return false;
            QuestStep step = quest.GetStep(p.StepIndex);
            if (step.Type != type || step.TargetId != targetId) return false;

            p.Count++;
            if (p.Count >= step.RequiredCount) Advance(quest, p);
            return true;
        }

        /// <summary>
        /// Re-evaluates Collect steps against how many of each item the player has
        /// (so items picked up before the quest started still count). Returns true if progress changed.
        /// </summary>
        public bool Refresh(QuestDefinition quest, Func<string, int> countOf)
        {
            QuestProgress p = Get(quest);
            if (p == null || countOf == null) return false;

            bool changed = false;
            while (p.State == QuestState.Active)
            {
                QuestStep step = quest.GetStep(p.StepIndex);
                if (step.Type != ObjectiveType.Collect) break;

                int have = Math.Min(countOf(step.TargetId), step.RequiredCount);
                if (have != p.Count)
                {
                    p.Count = have;
                    changed = true;
                }
                if (p.Count < step.RequiredCount) break;
                Advance(quest, p);
                changed = true;
            }
            return changed;
        }

        static void Advance(QuestDefinition quest, QuestProgress p)
        {
            p.StepIndex++;
            p.Count = 0;
            if (p.StepIndex >= quest.StepCount)
            {
                p.StepIndex = quest.StepCount - 1;
                p.State = QuestState.Completed;
            }
        }
    }
}
