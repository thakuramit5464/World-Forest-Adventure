using UnityEngine;

namespace Greenhaven.Quests
{
    /// <summary>
    /// Starts a quest as soon as the scene begins (for exploration goals that have no quest giver, such as
    /// collecting the Cloud Feathers on the Cloudpeak Trail).
    /// </summary>
    public sealed class QuestAutoStart : MonoBehaviour
    {
        [SerializeField] QuestManager manager;
        [SerializeField] QuestDefinition quest;

        void Start()
        {
            if (manager != null && quest != null) manager.TryStart(quest);
        }
    }
}
