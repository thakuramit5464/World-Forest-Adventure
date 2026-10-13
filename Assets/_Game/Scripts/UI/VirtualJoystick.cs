using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Floating thumb-stick for touch. Put it on a large invisible zone (the left part of the screen): the
    /// stick appears where the thumb lands, so the player never has to find it, and drifts home when
    /// released. Feeds the virtual gamepad's left stick like <c>OnScreenStick</c>, so gameplay code is
    /// unchanged. Pushing past the run threshold lights the outer ring (that is what makes the hero run).
    /// </summary>
    public sealed class VirtualJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [InputControl(layout = "Vector2"), SerializeField] string m_ControlPath;

        [Header("Parts")]
        [SerializeField] RectTransform stickBase;
        [SerializeField] RectTransform knob;
        [SerializeField] Image runRing;

        [Header("Feel")]
        [SerializeField, Min(10f), Tooltip("Pixels (canvas units) the knob travels for full input.")] float movementRange = 110f;
        [SerializeField, Range(0f, 0.5f)] float deadZone = 0.12f;
        [SerializeField, Range(0.5f, 1f), Tooltip("Pushing past this fraction of the range runs (matches PlayerInputReader).")] float runThreshold = 0.9f;
        [SerializeField, Tooltip("The stick appears where the thumb lands instead of at a fixed spot.")] bool floating = true;
        [SerializeField, Min(1f)] float returnSpeed = 14f;
        [SerializeField] Color ringIdle = new Color(1f, 1f, 1f, 0f);
        [SerializeField] Color ringRun = new Color(0.5f, 0.79f, 0.65f, 0.9f);

        protected override string controlPathInternal
        {
            get => m_ControlPath;
            set => m_ControlPath = value;
        }

        RectTransform zone;
        Vector2 home;
        int pointerId = -1;
        bool running;
        float ringBlend;

        public bool Active => pointerId != -1;
        public bool Running => running;

        void Awake()
        {
            zone = (RectTransform)transform;
            if (stickBase != null) home = stickBase.anchoredPosition;
            if (runRing != null) runRing.color = ringIdle;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            pointerId = -1;
            running = false;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (stickBase != null) stickBase.anchoredPosition = home;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != -1 || stickBase == null) return;
            pointerId = eventData.pointerId;
            if (floating && RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                // Keep the whole base inside the zone so it never hides under the screen edge.
                Vector2 half = zone.rect.size * 0.5f, margin = stickBase.rect.size * 0.5f;
                local.x = Mathf.Clamp(local.x, -half.x + margin.x, half.x - margin.x);
                local.y = Mathf.Clamp(local.y, -half.y + margin.y, half.y - margin.y);
                stickBase.anchoredPosition = local; // the base is anchored at the zone's centre, so local == anchored
            }
            knob.anchoredPosition = Vector2.zero;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stickBase, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            Vector2 v = local / movementRange;
            float magnitude = v.magnitude;
            if (magnitude > 1f) v /= magnitude;
            knob.anchoredPosition = v * movementRange;

            Vector2 output = magnitude < deadZone ? Vector2.zero : v.normalized * Mathf.Clamp01((Mathf.Min(magnitude, 1f) - deadZone) / (1f - deadZone));
            running = output.magnitude >= runThreshold;
            SendValueToControl(output);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            pointerId = -1;
            running = false;
            SendValueToControl(Vector2.zero);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (pointerId == -1)
            {
                float k = 1f - Mathf.Exp(-returnSpeed * dt);
                knob.anchoredPosition = Vector2.Lerp(knob.anchoredPosition, Vector2.zero, k);
                if (floating) stickBase.anchoredPosition = Vector2.Lerp(stickBase.anchoredPosition, home, k * 0.6f);
            }
            if (runRing != null)
            {
                ringBlend = Mathf.MoveTowards(ringBlend, running ? 1f : 0f, dt * 8f);
                runRing.color = Color.Lerp(ringIdle, ringRun, ringBlend);
                runRing.rectTransform.localScale = Vector3.one * (1f + 0.04f * ringBlend * Mathf.Sin(Time.unscaledTime * 9f));
            }
        }
    }
}
