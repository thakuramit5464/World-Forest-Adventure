using Greenhaven.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Simple dialogue panel: speaker name plus one line at a time. Advances on
    /// Interact or a tap on the panel; closes after the last line or when the
    /// player walks away from the speaker.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;
        [SerializeField, Tooltip("Optional. Dialogue closes when the player's interaction target changes.")]
        PlayerInteractor interactor;

        object owner;
        string[] lines;
        int index;

        public bool IsOpen => owner != null;

        void Awake()
        {
            if (panel == null || bodyText == null)
                Debug.LogWarning($"{nameof(DialogueView)} is missing panel/body references.", this);
            if (panel != null) panel.SetActive(false);
        }

        void OnEnable()
        {
            if (interactor != null) interactor.TargetChanged += OnTargetChanged;
        }

        void OnDisable()
        {
            if (interactor != null) interactor.TargetChanged -= OnTargetChanged;
        }

        public bool IsShowing(object speaker) => owner != null && ReferenceEquals(owner, speaker);

        public void Show(object speaker, string speakerName, string[] dialogueLines)
        {
            if (dialogueLines == null || dialogueLines.Length == 0) return;
            owner = speaker;
            lines = dialogueLines;
            index = 0;
            if (speakerText != null) speakerText.text = speakerName;
            if (panel != null) panel.SetActive(true);
            Refresh();
        }

        public void Advance()
        {
            if (!IsOpen) return;
            index++;
            if (index >= lines.Length) Hide();
            else Refresh();
        }

        public void Hide()
        {
            owner = null;
            lines = null;
            if (panel != null) panel.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData) => Advance();

        void Refresh()
        {
            if (bodyText != null) bodyText.text = lines[index];
        }

        void OnTargetChanged(IInteractable target)
        {
            if (IsOpen && !ReferenceEquals(target, owner)) Hide();
        }
    }
}
