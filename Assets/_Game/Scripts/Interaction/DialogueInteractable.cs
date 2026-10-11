using Greenhaven.UI;
using UnityEngine;

namespace Greenhaven.Interaction
{
    /// <summary>
    /// Shows a few lines of dialogue when interacted with. Used for friendly NPCs and
    /// readable objects such as signposts. Interacting again advances the dialogue.
    /// </summary>
    public sealed class DialogueInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] string speakerName = "Villager";
        [SerializeField, Tooltip("e.g. \"Talk to\" for NPCs, \"Read\" for signs.")]
        string promptVerb = "Talk to";
        [SerializeField, TextArea(2, 4)] string[] lines = { "Hello, traveller!" };
        [SerializeField] DialogueView dialogueView;
        [SerializeField, Tooltip("Optional point used for range checks. Defaults to this transform.")]
        Transform interactionPoint;
        [SerializeField, Tooltip("NPCs turn to face the player when spoken to.")]
        bool faceInteractor = true;

        string startPrompt;
        const string ContinuePrompt = "Continue";

        public string SpeakerName => speakerName;

        public string Prompt => dialogueView != null && dialogueView.IsShowing(this) ? ContinuePrompt : startPrompt;

        public Vector3 InteractionPoint => interactionPoint != null ? interactionPoint.position : transform.position;

        void Awake()
        {
            startPrompt = $"{promptVerb} {speakerName}";
            if (dialogueView == null)
                Debug.LogWarning($"{nameof(DialogueInteractable)} '{name}' has no {nameof(DialogueView)} assigned; dialogue will not display.", this);
        }

        public bool CanInteract(GameObject interactor) =>
            isActiveAndEnabled && dialogueView != null && lines != null && lines.Length > 0;

        public void Interact(GameObject interactor)
        {
            if (dialogueView.IsShowing(this))
            {
                dialogueView.Advance();
                return;
            }

            if (faceInteractor && interactor != null)
            {
                Vector3 look = interactor.transform.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
            }
            dialogueView.Show(this, speakerName, lines);
        }

        void OnDisable()
        {
            if (dialogueView != null && dialogueView.IsShowing(this)) dialogueView.Hide();
        }
    }
}
