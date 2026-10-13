using System.Collections.Generic;
using Greenhaven.Quests;
using UnityEngine;

namespace Greenhaven.World
{
    /// <summary>
    /// Tags something a quest step can point at (a crate's item type, an NPC id, a landmark id), so the map
    /// can mark the current objective. Inactive objects (collected items) drop out automatically.
    /// </summary>
    public sealed class QuestTargetMarker : MonoBehaviour
    {
        [SerializeField] ObjectiveType type;
        [SerializeField, Tooltip("Item type (Collect), NPC id (TalkTo) or landmark id (Inspect).")] string targetId;

        public static readonly List<QuestTargetMarker> Active = new List<QuestTargetMarker>();

        public ObjectiveType Type => type;
        public string TargetId => targetId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);

        public bool Matches(QuestStep step) => step != null && step.Type == type && step.TargetId == targetId;
    }
}
