using System.Collections.Generic;

namespace Greenhaven.Collectibles
{
    /// <summary>
    /// Pure record of what has been picked up: stable IDs (never collected twice) and counts per item type.
    /// Lives in <see cref="Greenhaven.Core.GameProgress"/> so it survives scene changes and restarts.
    /// </summary>
    public sealed class CollectedItems
    {
        readonly Dictionary<string, string> typeById = new Dictionary<string, string>();
        readonly Dictionary<string, int> countsByType = new Dictionary<string, int>();

        public IReadOnlyCollection<string> CollectedIds => typeById.Keys;

        /// <summary>Every collected (id, item type) pair, for saving.</summary>
        public IEnumerable<KeyValuePair<string, string>> Entries => typeById;

        public bool IsCollected(string collectibleId) =>
            !string.IsNullOrEmpty(collectibleId) && typeById.ContainsKey(collectibleId);

        public int CountOf(string itemType) =>
            itemType != null && countsByType.TryGetValue(itemType, out int n) ? n : 0;

        public bool TryCollect(string collectibleId, string itemType)
        {
            if (string.IsNullOrEmpty(collectibleId) || string.IsNullOrEmpty(itemType)) return false;
            if (typeById.ContainsKey(collectibleId)) return false;

            typeById[collectibleId] = itemType;
            countsByType.TryGetValue(itemType, out int n);
            countsByType[itemType] = n + 1;
            return true;
        }

        public void Clear()
        {
            typeById.Clear();
            countsByType.Clear();
        }
    }
}
