using System;
using Greenhaven.Player;
using UnityEngine;

namespace Greenhaven.Interaction
{
    /// <summary>
    /// Finds the best nearby <see cref="IInteractable"/> with a bounded, non-allocating
    /// overlap and triggers it when the player presses Interact.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        const int MaxCandidates = 16;

        [SerializeField] PlayerInputReader input;
        [SerializeField, Min(0.1f)] float range = 2.2f;
        [SerializeField] LayerMask interactableMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.02f), Tooltip("Seconds between target searches.")]
        float scanInterval = 0.1f;

        readonly Collider[] candidates = new Collider[MaxCandidates];
        PlayerMotor motor;
        float nextScanTime;

        public IInteractable CurrentTarget { get; private set; }

        /// <summary>Raised when the selected target changes (argument may be null).</summary>
        public event Action<IInteractable> TargetChanged;

        /// <summary>Raised after the player successfully interacts with a target.</summary>
        public event Action<IInteractable> Interacted;

        void Awake()
        {
            if (input == null) TryGetComponent(out input);
            TryGetComponent(out motor); // optional: turns the hero toward what it interacts with
            if (input == null)
                Debug.LogWarning($"{nameof(PlayerInteractor)} on '{name}' has no {nameof(PlayerInputReader)}; Interact will not fire.", this);
        }

        void OnEnable()
        {
            if (input != null) input.InteractPressed += TryInteract;
        }

        void OnDisable()
        {
            if (input != null) input.InteractPressed -= TryInteract;
            SetTarget(null);
        }

        void Update()
        {
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + scanInterval;
            SetTarget(FindBestTarget());
        }

        IInteractable FindBestTarget()
        {
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, range, candidates, interactableMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                IInteractable candidate = candidates[i].GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(gameObject)) continue;

                float score = InteractionScoring.Score(origin, transform.forward, candidate.InteractionPoint, range);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        void SetTarget(IInteractable target)
        {
            if (ReferenceEquals(target, CurrentTarget)) return;
            CurrentTarget = target;
            TargetChanged?.Invoke(target);
        }

        void TryInteract()
        {
            IInteractable target = CurrentTarget;
            if (target == null || (target is UnityEngine.Object obj && obj == null)) return;

            // Revalidate: the target may have moved, been disabled, or changed state since the last scan.
            bool inRange = InteractionScoring.Score(transform.position, transform.forward, target.InteractionPoint, range * 1.25f)
                           < float.PositiveInfinity;
            if (!inRange || !target.CanInteract(gameObject)) return;
            target.Interact(gameObject);
            Interacted?.Invoke(target);
            if (motor != null) motor.FaceTowards(target.InteractionPoint);
            nextScanTime = 0f; // rescan next frame: the target may now be collected or changed
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
