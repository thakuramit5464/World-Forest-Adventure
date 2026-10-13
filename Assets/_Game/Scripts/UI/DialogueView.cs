using Greenhaven.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Dialogue panel: speaker name plus one line at a time, revealed with a typewriter effect. The first
    /// Interact press or tap finishes the line; the next one advances. Fades in and out, and closes after the
    /// last line or when the player walks away from the speaker. The continue hint pulses once a line is done.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;
        [SerializeField, Tooltip("Optional. Dialogue closes when the player's interaction target changes.")] PlayerInteractor interactor;
        [SerializeField, Tooltip("Optional continue hint, pulsed when the line is fully shown.")] Graphic continueHint;
        [SerializeField, Min(1f)] float charactersPerSecond = 45f;
        [SerializeField, Min(1f)] float fadeSpeed = 9f;

        object owner;
        string[] lines;
        int index;
        float revealed;
        bool typing;
        CanvasGroup group;
        Color hintColor;

        public bool IsOpen => owner != null;

        void Awake()
        {
            if (panel == null || bodyText == null)
                Debug.LogWarning($"{nameof(DialogueView)} is missing panel/body references.", this);
            if (panel != null)
            {
                if (!panel.TryGetComponent(out group)) group = panel.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                panel.SetActive(false);
            }
            if (continueHint != null) hintColor = continueHint.color;
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

        /// <summary>Finishes the current line if it is still being typed, otherwise moves to the next (or closes).</summary>
        public void Advance()
        {
            if (!IsOpen) return;
            if (typing)
            {
                revealed = lines[index].Length;
                typing = false;
                if (bodyText != null) bodyText.text = lines[index];
                return;
            }
            index++;
            if (index >= lines.Length) Hide();
            else Refresh();
        }

        public void Hide()
        {
            owner = null;
            lines = null;
            typing = false;
        }

        public void OnPointerClick(PointerEventData eventData) => Advance();

        void Refresh()
        {
            revealed = 0f;
            typing = true;
            if (bodyText != null) bodyText.text = string.Empty;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (typing && lines != null)
            {
                revealed = Mathf.Min(lines[index].Length, revealed + charactersPerSecond * dt);
                int count = Mathf.FloorToInt(revealed);
                if (bodyText != null && bodyText.text.Length != count) bodyText.text = lines[index].Substring(0, count);
                if (count >= lines[index].Length) typing = false;
            }
            if (continueHint != null)
            {
                Color c = hintColor;
                c.a = typing ? 0f : hintColor.a * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f));
                continueHint.color = c;
            }
            if (group != null && panel != null && panel.activeSelf)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, IsOpen ? 1f : 0f, fadeSpeed * dt);
                group.blocksRaycasts = IsOpen;
                if (!IsOpen && group.alpha <= 0f) panel.SetActive(false);
            }
        }

        void OnTargetChanged(IInteractable target)
        {
            if (IsOpen && !ReferenceEquals(target, owner)) Hide();
        }
    }
}
