using System;
using System.Collections.Generic;
using UnityEngine;

namespace Greenhaven.Collectibles
{
    /// <summary>
    /// Scene-level owner of collection state. Collectibles register here so the HUD
    /// knows totals (e.g. 1/3), and report pickups so duplicates are rejected.
    /// </summary>
    public sealed class CollectibleRegistry : MonoBehaviour
    {
        readonly CollectedItems collected = new CollectedItems();
        readonly Dictionary<string, int> totalsByType = new Dictionary<string, int>();
        readonly HashSet<string> registeredIds = new HashSet<string>();

        /// <summary>Raised after a successful pickup: (itemType, collectedCount, total).</summary>
        public event Action<string, int, int> Collected;

        /// <summary>Raised when totals change as collectibles register.</summary>
        public event Action<string> TotalsChanged;

        public CollectedItems State => collected;

        public int TotalOf(string itemType) =>
            itemType != null && totalsByType.TryGetValue(itemType, out int n) ? n : 0;

        public void Register(string collectibleId, string itemType, UnityEngine.Object context)
        {
            if (string.IsNullOrEmpty(collectibleId))
            {
                Debug.LogError("Collectible has no ID; it cannot be saved or counted.", context);
                return;
            }
            if (!registeredIds.Add(collectibleId))
            {
                Debug.LogError($"Duplicate collectible ID '{collectibleId}'. Each collectible needs a unique ID.", context);
                return;
            }
            totalsByType.TryGetValue(itemType, out int n);
            totalsByType[itemType] = n + 1;
            TotalsChanged?.Invoke(itemType);
        }

        public bool TryCollect(string collectibleId, string itemType)
        {
            if (!collected.TryCollect(collectibleId, itemType)) return false;
            Collected?.Invoke(itemType, collected.CountOf(itemType), TotalOf(itemType));
            return true;
        }
    }
}
