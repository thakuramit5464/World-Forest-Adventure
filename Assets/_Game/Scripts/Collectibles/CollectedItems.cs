using System.Collections.Generic;

namespace Greenhaven.Collectibles
{
    /// <summary>
    /// Plain-data record of which collectibles have been picked up, by stable ID.
    /// No Unity dependencies, so it is unit-testable and easy to serialize for saving.
    /// </summary>
    public sealed class CollectedItems
    {
        readonly HashSet<string> collectedIds = new HashSet<string>();
        readonly Dictionary<string, int> countsByType = new Dictionary<string, int>();

        public IReadOnlyCollection<string> CollectedIds => collectedIds;

        public bool IsCollected(string collectibleId) =>
            !string.IsNullOrEmpty(collectibleId) && collectedIds.Contains(collectibleId);

        public int CountOf(string itemType) =>
            itemType != null && countsByType.TryGetValue(itemType, out int n) ? n : 0;

        /// <summary>Records a pickup. Returns false (and changes nothing) for empty or duplicate IDs.</summary>
        public bool TryCollect(string collectibleId, string itemType)
        {
            if (string.IsNullOrEmpty(collectibleId) || string.IsNullOrEmpty(itemType)) return false;
            if (!collectedIds.Add(collectibleId)) return false;

            countsByType.TryGetValue(itemType, out int n);
            countsByType[itemType] = n + 1;
            return true;
        }
    }
}
