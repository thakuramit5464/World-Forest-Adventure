using Greenhaven.Collectibles;
using UnityEngine;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Shows "Label n/total" for one item type and pops briefly on each pickup.
    /// Hidden until at least one collectible of that type exists in the scene.
    /// </summary>
    public sealed class CollectibleCounterView : MonoBehaviour
    {
        [SerializeField] CollectibleRegistry registry;
        [SerializeField] string itemType = "supply";
        [SerializeField] string label = "Supplies";
        [SerializeField] GameObject root;
        [SerializeField] Text counterText;
        [SerializeField, Min(1f)] float popScale = 1.25f;
        [SerializeField, Min(0.01f)] float popDuration = 0.25f;

        float popTimer;

        void Awake()
        {
            if (registry == null || counterText == null)
            {
                Debug.LogWarning($"{nameof(CollectibleCounterView)} is missing its registry or text.", this);
                enabled = false;
                return;
            }
            if (root != null) root.SetActive(false);
        }

        void OnEnable()
        {
            if (registry == null) return;
            registry.Collected += OnCollected;
            registry.TotalsChanged += OnTotalsChanged;
        }

        void OnDisable()
        {
            if (registry == null) return;
            registry.Collected -= OnCollected;
            registry.TotalsChanged -= OnTotalsChanged;
        }

        void Update()
        {
            if (popTimer <= 0f || root == null) return;
            popTimer = Mathf.Max(0f, popTimer - Time.deltaTime);
            float k = popTimer / popDuration; // 1 → 0
            root.transform.localScale = Vector3.one * Mathf.Lerp(1f, popScale, k);
        }

        void OnTotalsChanged(string type)
        {
            if (type == itemType) Refresh();
        }

        void OnCollected(string type, int count, int total)
        {
            if (type != itemType) return;
            Refresh();
            popTimer = popDuration;
        }

        void Refresh()
        {
            int total = registry.TotalOf(itemType);
            if (root != null) root.SetActive(total > 0);
            counterText.text = $"{label} {registry.State.CountOf(itemType)}/{total}";
        }
    }
}
