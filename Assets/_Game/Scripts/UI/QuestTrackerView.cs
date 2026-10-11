using Greenhaven.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Top-left objective tracker: the tracked quest's title and current step,
    /// e.g. "The Missing Supplies / Find the missing supply crates (1/3)". Hidden when no quest is active.
    /// </summary>
    public sealed class QuestTrackerView : MonoBehaviour
    {
        [SerializeField] QuestManager manager;
        [SerializeField] GameObject root;
        [SerializeField] Text titleText;
        [SerializeField] Text objectiveText;
        [SerializeField, Min(1f)] float popScale = 1.12f;
        [SerializeField, Min(0.01f)] float popDuration = 0.25f;

        float popTimer;

        void Awake()
        {
            if (manager == null || objectiveText == null)
            {
                Debug.LogWarning($"{nameof(QuestTrackerView)} is missing its manager or text.", this);
                enabled = false;
            }
            if (root != null) root.SetActive(false);
        }

        void OnEnable()
        {
            if (manager == null) return;
            manager.QuestStarted += OnQuestChanged;
            manager.QuestProgressed += OnQuestChanged;
            manager.QuestCompleted += OnQuestChanged;
            Refresh();
        }

        void OnDisable()
        {
            if (manager == null) return;
            manager.QuestStarted -= OnQuestChanged;
            manager.QuestProgressed -= OnQuestChanged;
            manager.QuestCompleted -= OnQuestChanged;
        }

        void Update()
        {
            if (popTimer <= 0f || root == null) return;
            popTimer = Mathf.Max(0f, popTimer - Time.deltaTime);
            root.transform.localScale = Vector3.one * Mathf.Lerp(1f, popScale, popTimer / popDuration);
        }

        void OnQuestChanged(QuestDefinition _)
        {
            Refresh();
            popTimer = popDuration;
        }

        void Refresh()
        {
            QuestDefinition tracked = manager.TrackedQuest;
            if (root != null) root.SetActive(tracked != null);
            if (tracked == null) return;
            if (titleText != null) titleText.text = tracked.Title;
            objectiveText.text = manager.CurrentObjectiveText(tracked);
        }
    }
}
