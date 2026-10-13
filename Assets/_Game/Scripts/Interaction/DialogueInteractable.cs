using System.Collections.Generic;
using Greenhaven.Core;
using Greenhaven.Quests;
using Greenhaven.UI;
using UnityEngine;

namespace Greenhaven.Interaction
{
    /// <summary>
    /// Something the player can talk to or read: NPCs, signs, landmarks. Shows its lines in the shared
    /// <see cref="DialogueView"/>. Quest givers on the same object supply quest-specific lines (several
    /// quests per NPC, in order). A landmark id makes the first look count as an Inspect objective.
    /// </summary>
    public sealed class DialogueInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] string speakerName = "Villager";
        [SerializeField, Tooltip("e.g. \"Talk to\" for NPCs, \"Read\" for signs, \"Inspect\" for landmarks.")]
        string promptVerb = "Talk to";
        [SerializeField, TextArea(2, 4)] string[] lines = { "Hello, traveller!" };
        [SerializeField] DialogueView dialogueView;
        [SerializeField, Tooltip("Optional point used for range checks. Defaults to this transform.")]
        Transform interactionPoint;
        [SerializeField, Tooltip("NPCs turn to face the player when spoken to.")]
        bool faceInteractor = true;
        [SerializeField, Tooltip("Optional. Its \"Talking\" bool is true while this dialogue is open.")]
        Animator talkAnimator;
        [SerializeField, Tooltip("Optional. Picks quest-specific lines (offer/progress/complete) instead of the lines above.")]
        QuestGiver questGiver;

        [Header("Landmark (optional)")]
        [SerializeField, Tooltip("Reported as an Inspect objective when the player looks at this, e.g. \"summit_shrine\".")]
        string landmarkId;
        [SerializeField] QuestManager questManager;

        static readonly int TalkingId = Animator.StringToHash("Talking");
        bool wasTalking;
        QuestGiver[] givers;

        string startPrompt;
        const string ContinuePrompt = "Continue";

        public string SpeakerName => speakerName;

        public bool IsInConversation => dialogueView != null && dialogueView.IsShowing(this);

        public string Prompt => dialogueView != null && dialogueView.IsShowing(this) ? ContinuePrompt : startPrompt;

        public Vector3 InteractionPoint => interactionPoint != null ? interactionPoint.position : transform.position;

        void Awake()
        {
            startPrompt = $"{promptVerb} {speakerName}";
            givers = GetComponents<QuestGiver>();
            if (givers.Length == 0 && questGiver != null) givers = new[] { questGiver };
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
            dialogueView.Show(this, speakerName, QuestLinesOrDefault());

            if (!string.IsNullOrEmpty(landmarkId))
            {
                if (questManager == null) questManager = FindFirstObjectByType<QuestManager>();
                if (questManager != null) questManager.ReportInspect(landmarkId);
                GameProgress.Instance.SetFlag("inspected:" + landmarkId);
            }
        }

        /// <summary>Quest lines from the giver that needs attention (active or startable), else a finished one's lines, else the default.</summary>
        string[] QuestLinesOrDefault()
        {
            if (givers == null || givers.Length == 0) return lines;
            // Snapshot states first: reporting the talk may complete a "return to" step, and the lines must reflect the state before.
            var before = new List<QuestState>(givers.Length);
            foreach (QuestGiver g in givers) before.Add(g.StateNow);
            givers[0].ReportTalk(); // one report per conversation (all givers share the NPC id)

            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < givers.Length; i++)
                {
                    bool current = before[i] == QuestState.Active || (before[i] == QuestState.NotStarted && givers[i].IsCurrent);
                    if ((pass == 0) != current) continue;
                    string[] said = givers[i].LinesFor(before[i]);
                    if (said != null) return said;
                }
            return lines;
        }

        void Update()
        {
            if (talkAnimator == null || dialogueView == null) return;
            bool talking = dialogueView.IsShowing(this);
            if (talking == wasTalking) return;
            wasTalking = talking;
            talkAnimator.SetBool(TalkingId, talking);
        }

        void OnDisable()
        {
            if (dialogueView != null && dialogueView.IsShowing(this)) dialogueView.Hide();
        }
    }
}
