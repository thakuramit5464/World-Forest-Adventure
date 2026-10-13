using System.Collections.Generic;
using Greenhaven.Core;
using Greenhaven.Quests;
using Greenhaven.UI;
using UnityEngine;

namespace Greenhaven.Puzzles
{
    /// <summary>
    /// The Mountain Clue puzzle: light the cairns in the order the shrine describes. A wrong cairn snuffs
    /// them all with a hint; the right order keeps them lit, reports the Inspect objective and sets a
    /// progress flag, so the puzzle stays solved after leaving the scene or restarting.
    /// </summary>
    public sealed class CairnPuzzle : MonoBehaviour
    {
        [SerializeField, Tooltip("Cairns in the correct lighting order.")] PuzzleCairn[] order = new PuzzleCairn[0];
        [SerializeField, Tooltip("Reported as an Inspect objective when solved, e.g. \"cairn_puzzle\".")] string landmarkId = "cairn_puzzle";
        [SerializeField] string solvedFlag = "puzzle:cairns";
        [SerializeField] QuestManager questManager;
        [SerializeField] DialogueView dialogueView;
        [SerializeField, TextArea(2, 4)] string[] wrongOrderLines = { "The flame gutters and dies. That was not the order the carving shows." };
        [SerializeField, TextArea(2, 4)] string[] solvedLines = { "All three cairns burn steadily. The light picks out faint marks on the rock: an old trail, heading beyond the summit." };
        [SerializeField, Tooltip("Optional object revealed when solved (e.g. a carved trail marker).")] GameObject reveal;

        readonly List<PuzzleCairn> lit = new List<PuzzleCairn>();

        public bool Solved { get; private set; }

        void Start()
        {
            if (reveal != null) reveal.SetActive(false);
            if (GameProgress.Instance.HasFlag(solvedFlag)) SetSolved(false);
        }

        /// <summary>Called by a cairn when the player lights it. Returns false if it should stay unlit.</summary>
        public bool TryLight(PuzzleCairn cairn)
        {
            if (Solved) return true;
            if (lit.Contains(cairn)) return true;
            int index = lit.Count;
            if (index >= order.Length || order[index] != cairn)
            {
                foreach (PuzzleCairn c in lit) c.SetLit(false);
                lit.Clear();
                Say(wrongOrderLines);
                return false;
            }
            lit.Add(cairn);
            if (lit.Count == order.Length) SetSolved(true);
            return true;
        }

        void SetSolved(bool announce)
        {
            Solved = true;
            foreach (PuzzleCairn c in order) if (c != null) c.SetLit(true);
            if (reveal != null) reveal.SetActive(true);
            if (!announce) return;
            if (questManager == null) questManager = FindFirstObjectByType<QuestManager>();
            if (questManager != null) questManager.ReportInspect(landmarkId);
            GameProgress.Instance.SetFlag(solvedFlag);
            Say(solvedLines);
        }

        void Say(string[] lines)
        {
            if (dialogueView == null) dialogueView = FindFirstObjectByType<DialogueView>();
            if (dialogueView != null && lines != null && lines.Length > 0) dialogueView.Show(this, "Cairns", lines);
        }
    }
}
