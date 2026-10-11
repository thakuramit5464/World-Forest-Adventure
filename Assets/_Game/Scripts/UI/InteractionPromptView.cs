using Greenhaven.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Shows the current target's prompt (e.g. "Talk to Mira") and dims the
    /// on-screen Interact button when nothing is in range.
    /// </summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] GameObject promptRoot;
        [SerializeField] Text promptText;
        [SerializeField, Tooltip("Optional: the on-screen Interact button, dimmed when there is no target.")]
        CanvasGroup interactButton;
        [SerializeField, Range(0f, 1f)] float idleButtonAlpha = 0.45f;

        // Non-null sentinel so the first Show(null) in Awake actually hides the prompt.
        string shownPrompt = string.Empty;

        void Awake()
        {
            if (interactor == null)
            {
                Debug.LogWarning($"{nameof(InteractionPromptView)} has no {nameof(PlayerInteractor)}; prompt disabled.", this);
                enabled = false;
            }
            Show(null);
        }

        void Update()
        {
            IInteractable target = interactor.CurrentTarget;
            Show(target != null ? target.Prompt : null);
        }

        void Show(string prompt)
        {
            if (ReferenceEquals(prompt, shownPrompt)) return;
            shownPrompt = prompt;

            bool visible = !string.IsNullOrEmpty(prompt);
            if (promptRoot != null) promptRoot.SetActive(visible);
            if (promptText != null && visible) promptText.text = prompt;
            if (interactButton != null) interactButton.alpha = visible ? 1f : idleButtonAlpha;
        }
    }
}
