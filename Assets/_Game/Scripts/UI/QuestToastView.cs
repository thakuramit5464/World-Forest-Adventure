using Greenhaven.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Brief centred banner for quest milestones ("New Quest", "Quest Complete").
    /// Text carries the meaning, so it does not rely on colour alone.
    /// </summary>
    public sealed class QuestToastView : MonoBehaviour
    {
        [SerializeField] QuestManager manager;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text headerText;
        [SerializeField] Text titleText;
        [SerializeField, Min(0.5f)] float holdSeconds = 2.5f;
        [SerializeField, Min(0.05f)] float fadeSeconds = 0.4f;

        float timer;

        void Awake()
        {
            if (manager == null || group == null)
            {
                Debug.LogWarning($"{nameof(QuestToastView)} is missing its manager or CanvasGroup.", this);
                enabled = false;
            }
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        void OnEnable()
        {
            if (manager == null) return;
            manager.QuestStarted += OnStarted;
            manager.QuestCompleted += OnCompleted;
        }

        void OnDisable()
        {
            if (manager == null) return;
            manager.QuestStarted -= OnStarted;
            manager.QuestCompleted -= OnCompleted;
        }

        void OnStarted(QuestDefinition quest) => Show("New Quest", quest.Title);

        void OnCompleted(QuestDefinition quest) => Show("Quest Complete!", quest.Title);

        void Show(string header, string title)
        {
            if (headerText != null) headerText.text = header;
            if (titleText != null) titleText.text = title;
            timer = holdSeconds + fadeSeconds * 2f;
        }

        void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            float total = holdSeconds + fadeSeconds * 2f;
            float elapsed = total - timer;
            // Fade in, hold, fade out.
            group.alpha = elapsed < fadeSeconds ? elapsed / fadeSeconds
                : timer < fadeSeconds ? Mathf.Max(0f, timer / fadeSeconds)
                : 1f;
        }
    }
}
