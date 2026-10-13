using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Press feedback for an on-screen button (sits beside <c>OnScreenButton</c>): a quick squash and a brighter
    /// tint while held, and an optional gentle pulse when the button is worth pressing (e.g. Interact with a
    /// target in range). Purely visual; the input goes through OnScreenButton.
    /// </summary>
    public sealed class HudButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] Image background;
        [SerializeField, Range(0.5f, 1f)] float pressedScale = 0.9f;
        [SerializeField] Color pressedTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField, Min(0f)] float pulseAmount = 0.06f;

        Color restColor;
        float press, scaleNow = 1f;
        bool pressed;

        /// <summary>Pulse gently to draw the eye (set by the interaction prompt).</summary>
        public bool Attention { get; set; }

        void Awake()
        {
            if (background == null) background = GetComponent<Image>();
            if (background != null) restColor = background.color;
        }

        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnPointerExit(PointerEventData eventData) => pressed = false;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            press = Mathf.MoveTowards(press, pressed ? 1f : 0f, dt * 14f);
            float target = Mathf.Lerp(1f, pressedScale, press);
            if (Attention && !pressed) target *= 1f + pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
            scaleNow = Mathf.Lerp(scaleNow, target, 1f - Mathf.Exp(-18f * dt));
            transform.localScale = Vector3.one * scaleNow;
            if (background != null) background.color = Color.Lerp(restColor, pressedTint, press);
        }
    }
}
