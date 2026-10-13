using Greenhaven.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Shows the current target's prompt (e.g. "Talk to Mira") with a key hint that follows the device in
    /// use (E on keyboard, X on a gamepad, nothing on touch where the button itself is the hint), fades it
    /// in and out, dims the on-screen Interact button when nothing is in range and pulses it when something is.
    /// </summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] GameObject promptRoot;
        [SerializeField] Text promptText;
        [SerializeField, Tooltip("Optional key cap shown before the prompt on keyboard and gamepad.")] GameObject keyCap;
        [SerializeField] Text keyText;
        [SerializeField, Tooltip("Optional: the on-screen Interact button, dimmed when there is no target.")] CanvasGroup interactButton;
        [SerializeField] HudButton interactHudButton;
        [SerializeField, Range(0f, 1f)] float idleButtonAlpha = 0.45f;
        [SerializeField, Min(1f)] float fadeSpeed = 10f;

        CanvasGroup promptGroup;
        string shownPrompt = string.Empty; // non-null sentinel so the first Show(null) hides the prompt
        bool visible;

        void Awake()
        {
            if (interactor == null)
            {
                Debug.LogWarning($"{nameof(InteractionPromptView)} has no {nameof(PlayerInteractor)}; prompt disabled.", this);
                enabled = false;
            }
            if (promptRoot != null && !promptRoot.TryGetComponent(out promptGroup)) promptGroup = promptRoot.AddComponent<CanvasGroup>();
            if (promptGroup != null)
            {
                promptGroup.alpha = 0f;
                promptGroup.blocksRaycasts = false;
                promptGroup.interactable = false;
            }
            Show(null);
        }

        void Update()
        {
            ActiveInput.Refresh();
            IInteractable target = interactor.CurrentTarget;
            Show(target != null ? target.Prompt : null);
            if (promptGroup != null) promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, visible ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            if (keyCap != null)
            {
                bool showKey = ActiveInput.Current != InputKind.Touch;
                if (keyCap.activeSelf != showKey) keyCap.SetActive(showKey);
                if (keyText != null) keyText.text = ActiveInput.Current == InputKind.Gamepad ? "X" : "E";
            }
        }

        void Show(string prompt)
        {
            if (ReferenceEquals(prompt, shownPrompt)) return;
            shownPrompt = prompt;
            visible = !string.IsNullOrEmpty(prompt);
            if (promptRoot != null && promptGroup == null) promptRoot.SetActive(visible);
            if (promptText != null && visible) promptText.text = prompt;
            if (interactButton != null) interactButton.alpha = visible ? 1f : idleButtonAlpha;
            if (interactHudButton != null) interactHudButton.Attention = visible;
        }
    }
}
