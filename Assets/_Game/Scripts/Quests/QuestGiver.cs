using UnityEngine;

namespace Greenhaven.Quests
{
    /// <summary>
    /// Lets an NPC offer, track and complete one quest through conversation. Chooses which
    /// lines the NPC says based on quest state, and reports "talked to" for this NPC.
    /// Used by <see cref="Interaction.DialogueInteractable"/> on the same GameObject.
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

        void Awake()
        {
            if (manager == null || quest == null)
                Debug.LogWarning($"{nameof(QuestGiver)} on '{name}' needs a {nameof(QuestManager)} and a quest.", this);
        }

        /// <summary>
        /// Called when a conversation starts. Returns the lines to say, or null to use the
        /// NPC's normal lines (e.g. when this quest is not yet available).
        /// </summary>
        public string[] BeginConversation()
        {
            if (manager == null || quest == null) return null;

            QuestState before = manager.StateOf(quest);
            // Report first: talking to this NPC may complete this quest's "return to" step (or another quest's).
            manager.ReportTalk(npcId);

            switch (before)
            {
                case QuestState.NotStarted:
                    return manager.TryStart(quest) ? NonEmpty(offerLines) : null;
                case QuestState.Active:
                    return manager.StateOf(quest) == QuestState.Completed ? NonEmpty(completeLines) : NonEmpty(activeLines);
                default:
                    return NonEmpty(doneLines);
            }
        }

        static string[] NonEmpty(string[] lines) => lines != null && lines.Length > 0 ? lines : null;
    }
}
