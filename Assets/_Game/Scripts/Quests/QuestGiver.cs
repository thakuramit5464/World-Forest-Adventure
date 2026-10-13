using UnityEngine;

namespace Greenhaven.Quests
{
    /// <summary>
    /// Picks an NPC's lines for one quest by its state: offer (and start it), progress, completion, or
    /// afterwards. An NPC can carry several of these (one per quest); <see cref="Greenhaven.Interaction.DialogueInteractable"/>
    /// asks the one whose quest is active or ready to start first, then the finished ones.
    /// </summary>
    public sealed class QuestGiver : MonoBehaviour
    {
        [SerializeField] QuestManager manager;
        [SerializeField] QuestDefinition quest;
        [SerializeField, Tooltip("Matches TalkTo objective target ids, e.g. \"mira\".")]
        string npcId;
        [SerializeField, TextArea(2, 4)] string[] offerLines;
        [SerializeField, TextArea(2, 4)] string[] activeLines;
        [SerializeField, TextArea(2, 4)] string[] completeLines;
        [SerializeField, TextArea(2, 4), Tooltip("After completion. Empty = the NPC's normal lines.")]
        string[] doneLines;

        public string NpcId => npcId;
        public QuestDefinition Quest => quest;

        /// <summary>True while this quest wants attention: it is running, or it can be offered now.</summary>
        public bool IsCurrent => manager != null && quest != null && (manager.StateOf(quest) == QuestState.Active || manager.CanStart(quest));

        void Awake()
        {
            if (manager == null || quest == null)
                Debug.LogWarning($"{nameof(QuestGiver)} on '{name}' needs a {nameof(QuestManager)} and a quest.", this);
        }

        /// <summary>Tells the quest system the player talked to this NPC (call once per conversation).</summary>
        public void ReportTalk()
        {
            if (manager != null) manager.ReportTalk(npcId);
        }

        /// <summary>Lines for this quest given <paramref name="stateBeforeTalk"/>; null when it has nothing to say.</summary>
        public string[] LinesFor(QuestState stateBeforeTalk)
        {
            if (manager == null || quest == null) return null;
            switch (stateBeforeTalk)
            {
                case QuestState.NotStarted:
                    return manager.TryStart(quest) ? NonEmpty(offerLines) : null;
                case QuestState.Active:
                    return manager.StateOf(quest) == QuestState.Completed ? NonEmpty(completeLines) : NonEmpty(activeLines);
                default:
                    return NonEmpty(doneLines);
            }
        }

        public QuestState StateNow => manager != null ? manager.StateOf(quest) : QuestState.NotStarted;

        /// <summary>Single-quest convenience (report, then pick lines).</summary>
        public string[] BeginConversation()
        {
            QuestState before = StateNow;
            ReportTalk();
            return LinesFor(before);
        }

        static string[] NonEmpty(string[] lines) => lines != null && lines.Length > 0 ? lines : null;
    }
}
