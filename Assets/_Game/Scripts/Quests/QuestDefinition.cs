using System;
using UnityEngine;

namespace Greenhaven.Quests
{
    public enum ObjectiveType
    {
        /// <summary>Have <see cref="QuestStep.RequiredCount"/> collectibles of item type <see cref="QuestStep.TargetId"/>.</summary>
        Collect,
        /// <summary>Talk to the NPC whose id is <see cref="QuestStep.TargetId"/>.</summary>
        TalkTo,
        /// <summary>Inspect the landmark whose id is <see cref="QuestStep.TargetId"/>.</summary>
        Inspect,
    }

    [Serializable]
    public sealed class QuestStep
    {
        [SerializeField, Tooltip("Shown in the objective tracker, e.g. \"Find the missing supply crates\".")]
        string description;
        [SerializeField] ObjectiveType type;
        [SerializeField, Tooltip("Item type (Collect), NPC id (TalkTo) or landmark id (Inspect).")]
        string targetId;
        [SerializeField, Min(1)] int requiredCount = 1;

        public string Description => description;
        public ObjectiveType Type => type;
        public string TargetId => targetId;
        public int RequiredCount => Mathf.Max(1, requiredCount);

        public QuestStep(string description, ObjectiveType type, string targetId, int requiredCount = 1)
        {
            this.description = description;
            this.type = type;
            this.targetId = targetId;
            this.requiredCount = requiredCount;
        }
    }

    /// <summary>
    /// Static quest data: a stable id, display text, and steps completed in order.
    /// Mutable progress lives in <see cref="QuestLog"/>, never here.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenhaven/Quest Definition", fileName = "Quest_")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable id used by saves. Never change it after release.")]
        string questId;
        [SerializeField] string title;
        [SerializeField, TextArea(2, 4)] string summary;
        [SerializeField, Tooltip("Optional quest that must be completed before this one can start.")]
        QuestDefinition prerequisite;
        [SerializeField] QuestStep[] steps = Array.Empty<QuestStep>();

        public string QuestId => questId;
        public string Title => title;
        public string Summary => summary;
        public QuestDefinition Prerequisite => prerequisite;
        public int StepCount => steps?.Length ?? 0;
        public QuestStep GetStep(int index) => steps[index];

        /// <summary>Editor/test helper to fill a definition from code.</summary>
        public void Configure(string id, string questTitle, string questSummary, QuestDefinition requires, params QuestStep[] questSteps)
        {
            questId = id;
            title = questTitle;
            summary = questSummary;
            prerequisite = requires;
            steps = questSteps ?? Array.Empty<QuestStep>();
        }
    }
}
