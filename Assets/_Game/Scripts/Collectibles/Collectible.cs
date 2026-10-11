using Greenhaven.Interaction;
using UnityEngine;

namespace Greenhaven.Collectibles
{
    /// <summary>
    /// A world pickup with a stable ID. Collected via the normal Interact action;
    /// hides itself once collected and stays hidden if already collected (e.g. after loading).
    /// </summary>
    public sealed class Collectible : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("Unique and stable across builds, e.g. \"supply_01\". Used by saves.")]
        string collectibleId;
        [SerializeField, Tooltip("Groups collectibles for counting, e.g. \"supply\".")]
        string itemType = "supply";
        [SerializeField] string displayName = "Supply Crate";
        [SerializeField] CollectibleRegistry registry;

        [Header("Idle motion")]
        [SerializeField, Tooltip("Optional child that bobs and spins to catch the eye.")]
        Transform visual;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float spinDegreesPerSecond = 45f;

        string prompt;
        Vector3 visualRestPosition;

        public string CollectibleId => collectibleId;

        public string Prompt => prompt;

        public Vector3 InteractionPoint => transform.position;

        void Awake()
        {
            prompt = $"Pick up {displayName}";
            if (visual != null) visualRestPosition = visual.localPosition;
            if (registry == null)
                Debug.LogWarning($"{nameof(Collectible)} '{name}' has no {nameof(CollectibleRegistry)}; it cannot be collected.", this);
        }

        void Start()
        {
            if (registry == null) return;
            registry.Register(collectibleId, itemType, this);
            if (registry.State.IsCollected(collectibleId)) gameObject.SetActive(false);
        }

        void Update()
        {
            if (visual == null) return;
            // Phase offset by position so neighbouring pickups don't bob in sync.
            float t = Time.time + transform.position.x * 0.37f;
            visual.localPosition = visualRestPosition + Vector3.up * (Mathf.Sin(t * 2f) * bobHeight);
            visual.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        public bool CanInteract(GameObject interactor) =>
            isActiveAndEnabled && registry != null && !registry.State.IsCollected(collectibleId);

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            if (registry.TryCollect(collectibleId, itemType)) gameObject.SetActive(false);
        }
    }
}
