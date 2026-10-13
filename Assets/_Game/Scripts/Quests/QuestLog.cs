using System;
using System.Collections.Generic;

namespace Greenhaven.Quests
{
    public enum QuestState { NotStarted, Active, Completed }

    [Serializable]
    public sealed class QuestProgress
    {
        public QuestState State;
        public int StepIndex;
        public int Count;
    }

    /// <summary>
    /// Pure quest progress keyed by quest id (no Unity dependencies, unit-tested). Lives in
    /// <see cref="Greenhaven.Core.GameProgress"/> so it is shared by every scene and saved.
    /// </summary>
    public sealed class QuestLog
    {
        readonly Dictionary<string, QuestProgress> progress = new Dictionary<string, QuestProgress>();

        /// <summary>Every known quest's progress, for saving.</summary>
        public IEnumerable<KeyValuePair<string, QuestProgress>> Entries => progress;

        public QuestState StateOf(QuestDefinition quest) =>
            quest != null && progress.TryGetValue(quest.QuestId, out QuestProgress p) ? p.State : QuestState.NotStarted;

        public QuestProgress Get(QuestDefinition quest) =>
            quest != null && progress.TryGetValue(quest.QuestId, out QuestProgress p) ? p : null;

        /// <summary>Puts back saved progress (ignores empty ids).</summary>
        public void Restore(string questId, QuestState state, int stepIndex, int count)
        {
            if (string.IsNullOrEmpty(questId)) return;
            progress[questId] = new QuestProgress { State = state, StepIndex = Math.Max(0, stepIndex), Count = Math.Max(0, count) };
        }

        public void Clear() => progress.Clear();

        public bool Start(QuestDefinition quest)
        {
            if (quest == null || string.IsNullOrEmpty(quest.QuestId) || StateOf(quest) != QuestState.NotStarted) return false;
            var p = new QuestProgress { State = QuestState.Active };
            progress[quest.QuestId] = p;
            if (quest.StepCount == 0) p.State = QuestState.Completed;
            return true;
        }

        /// <summary>Reports a TalkTo/Inspect event. Returns true if it advanced the current step's count.</summary>
        public bool Report(QuestDefinition quest, ObjectiveType type, string targetId)
        {
            QuestProgress p = Get(quest);
            if (p == null || p.State != QuestState.Active || type == ObjectiveType.Collect) return false;
            if (p.StepIndex >= quest.StepCount) return false; // older save with a step that no longer exists
            QuestStep step = quest.GetStep(p.StepIndex);
            if (step.Type != type || step.TargetId != targetId) return false;

            p.Count++;
            if (p.Count >= step.RequiredCount) Advance(quest, p);
            return true;
        }

        /// <summary>Re-evaluates Collect steps against held counts (so items found before the quest still count).</summary>
        public bool Refresh(QuestDefinition quest, Func<string, int> countOf)
        {
            QuestProgress p = Get(quest);
            if (p == null || countOf == null) return false;

            bool changed = false;
            while (p.State == QuestState.Active && p.StepIndex < quest.StepCount)
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
                p.StepIndex = Math.Max(0, quest.StepCount - 1);
                p.State = QuestState.Completed;
            }
        }
    }
}
