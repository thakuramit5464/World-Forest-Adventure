using Greenhaven.Quests;
using Greenhaven.World;
using UnityEngine;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Brief centred banner for milestones: quests ("New Quest", "Quest Complete!") and newly discovered
    /// places ("Discovered"). Text carries the meaning, so it does not rely on colour alone. Banners queue
    /// so two events in a row both show.
    /// </summary>
    public sealed class QuestToastView : MonoBehaviour
    {
        [SerializeField] QuestManager manager;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text headerText;
        [SerializeField] Text titleText;
        [SerializeField, Min(0.5f)] float holdSeconds = 2.5f;
        [SerializeField, Min(0.05f)] float fadeSeconds = 0.4f;

        readonly System.Collections.Generic.Queue<(string header, string title)> queue = new System.Collections.Generic.Queue<(string, string)>();
        float timer;

        void Awake()
        {
            if (group == null)
            {
                Debug.LogWarning($"{nameof(QuestToastView)} is missing its CanvasGroup.", this);
                enabled = false;
                return;
            }
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        void OnEnable()
        {
            if (manager != null)
            {
                manager.QuestStarted += OnStarted;
                manager.QuestCompleted += OnCompleted;
            }
            LocationMarker.Discovered += OnDiscovered;
        }

        void OnDisable()
        {
            if (manager != null)
            {
                manager.QuestStarted -= OnStarted;
                manager.QuestCompleted -= OnCompleted;
            }
            LocationMarker.Discovered -= OnDiscovered;
        }

        void OnStarted(QuestDefinition quest) => Show("New Quest", quest.Title);

        void OnCompleted(QuestDefinition quest) => Show("Quest Complete!", quest.Title);

        void OnDiscovered(LocationMarker location) => Show("Discovered", location.DisplayName);

        public void Show(string header, string title)
        {
            if (timer > 0f)
            {
                queue.Enqueue((header, title));
                return;
            }
            Present(header, title);
        }

        void Present(string header, string title)
        {
            if (headerText != null) headerText.text = header;
            if (titleText != null) titleText.text = title;
            timer = holdSeconds + fadeSeconds * 2f;
        }

        void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.unscaledDeltaTime;
            float total = holdSeconds + fadeSeconds * 2f;
            float elapsed = total - timer;
            // Fade in, hold, fade out.
            group.alpha = elapsed < fadeSeconds ? elapsed / fadeSeconds
                : timer < fadeSeconds ? Mathf.Max(0f, timer / fadeSeconds)
                : 1f;
            if (timer <= 0f && queue.Count > 0)
            {
                (string header, string title) = queue.Dequeue();
                Present(header, title);
            }
        }
    }
}
