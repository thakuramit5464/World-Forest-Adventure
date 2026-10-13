using UnityEngine;

namespace Greenhaven.UI
{
    /// <summary>
    /// Fades the on-screen controls in when the player touches the screen and out when a keyboard, mouse or
    /// gamepad takes over. They start visible everywhere (so nothing looks broken), and hide on the first
    /// non-touch input. Tick Always Show to keep them for testing.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TouchControlsVisibility : MonoBehaviour
    {
        [SerializeField] bool alwaysShow;
        [SerializeField, Min(0.5f)] float fadeSpeed = 5f;

        CanvasGroup group;

        public bool Visible { get; private set; } = true;

        void Awake() => group = GetComponent<CanvasGroup>();

        void Update()
        {
            ActiveInput.Refresh();
            bool show = alwaysShow || ActiveInput.Current == InputKind.Touch || !ActiveInput.HasSeenActivity;
            if (show != Visible)
            {
                Visible = show;
                group.blocksRaycasts = show;
                group.interactable = show;
            }
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
