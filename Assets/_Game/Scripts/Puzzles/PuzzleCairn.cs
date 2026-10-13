using Greenhaven.Interaction;
using UnityEngine;

namespace Greenhaven.Puzzles
{
    /// <summary>One cairn of the summit puzzle: interact to light it (the puzzle decides whether it stays lit). The flame flickers by scale.</summary>
    public sealed class PuzzleCairn : MonoBehaviour, IInteractable
    {
        [SerializeField] string cairnName = "Cairn";
        [SerializeField] CairnPuzzle puzzle;
        [SerializeField, Tooltip("Flame visual, shown while lit.")] Transform flame;

        bool isLit;
        Vector3 flameScale;

        public bool IsLit => isLit;
        public string Prompt => isLit ? $"{cairnName} (lit)" : $"Light the {cairnName}";
        public Vector3 InteractionPoint => transform.position;

        void Awake()
        {
            if (flame != null)
            {
                flameScale = flame.localScale;
                flame.gameObject.SetActive(false);
            }
        }

        public bool CanInteract(GameObject interactor) => isActiveAndEnabled && !isLit && puzzle != null;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            SetLit(true);
            if (!puzzle.TryLight(this)) SetLit(false);
        }

        public void SetLit(bool lit)
        {
            isLit = lit;
            if (flame != null) flame.gameObject.SetActive(lit);
        }

        void Update()
        {
            if (!isLit || flame == null) return;
            float t = Time.time * 9f + transform.position.x;
            float flicker = 1f + 0.12f * Mathf.Sin(t) + 0.06f * Mathf.Sin(t * 2.7f);
            flame.localScale = new Vector3(flameScale.x * (2f - flicker), flameScale.y * flicker, flameScale.z * (2f - flicker));
        }
    }
}
