using System;
using System.Collections.Generic;
using Greenhaven.Core;
using UnityEngine;

namespace Greenhaven.Collectibles
{
    /// <summary>
    /// Scene-side view of the player's collected items. The record itself lives in
    /// <see cref="GameProgress"/> (shared across scenes and saved); this component registers the scene's
    /// collectibles (for totals and duplicate-ID checks) and raises events when something is picked up.
    /// </summary>
    public sealed class CollectibleRegistry : MonoBehaviour
    {
        readonly Dictionary<string, int> totalsByType = new Dictionary<string, int>();
        readonly HashSet<string> registeredIds = new HashSet<string>();
        CollectedItems collected;

        /// <summary>(item type, held count, total of that type in this scene).</summary>
        public event Action<string, int, int> Collected;

        public event Action<string> TotalsChanged;

        public CollectedItems State => collected ??= GameProgress.Instance.Items;

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
            if (!State.TryCollect(collectibleId, itemType)) return false;
            GameProgress.Instance.Save();
            Collected?.Invoke(itemType, State.CountOf(itemType), TotalOf(itemType));
            return true;
        }
    }
}
