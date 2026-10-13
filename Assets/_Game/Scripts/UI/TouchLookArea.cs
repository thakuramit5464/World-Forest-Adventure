using Greenhaven.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Greenhaven.UI
{
    /// <summary>
    /// Invisible screen region (the right part) that turns the camera when dragged. One finger at a time;
    /// a flick keeps the camera turning briefly after the finger lifts (inertia), and quick swipes get a little
    /// extra speed so looking around feels responsive without being twitchy. Needs a raycast-target Graphic.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField, Tooltip("Camera rotation in degrees for a drag across the full screen height.")] float degreesPerScreenHeight = 210f;
        [SerializeField, Range(0f, 1f), Tooltip("Extra speed for fast swipes (0 = linear).")] float swipeBoost = 0.35f;
        [SerializeField, Min(0f), Tooltip("How quickly the after-flick turn dies away (per second).")] float inertiaDecay = 7f;
        [SerializeField] bool invertY;

        int pointerId = -1;
        Vector2 velocity, inertia; // degrees per second
        float lastDragTime;

        void Awake()
        {
            if (input == null)
            {
                Debug.LogWarning($"{nameof(TouchLookArea)} has no {nameof(PlayerInputReader)}; touch look disabled.", this);
                enabled = false;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != -1) return;
            pointerId = eventData.pointerId;
            velocity = inertia = Vector2.zero;
            lastDragTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!enabled || eventData.pointerId != pointerId) return;
            float dt = Mathf.Max(0.004f, Time.unscaledTime - lastDragTime);
            lastDragTime = Time.unscaledTime;
            // Normalise by screen height so the feel is the same on any resolution or DPI.
            Vector2 degrees = eventData.delta * (degreesPerScreenHeight / Mathf.Max(1, Screen.height));
            float speed = degrees.magnitude / dt; // deg/s
            degrees *= 1f + swipeBoost * Mathf.Clamp01((speed - 200f) / 800f);
            if (invertY) degrees.y = -degrees.y;
            input.AddLookDegrees(degrees);
            velocity = Vector2.Lerp(velocity, degrees / dt, 0.5f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            pointerId = -1;
            // Only a real flick carries on; a slow drag that stops just stops.
            inertia = Time.unscaledTime - lastDragTime < 0.08f && velocity.magnitude > 60f ? velocity * 0.6f : Vector2.zero;
        }

        void Update()
        {
            if (pointerId != -1 || inertia.sqrMagnitude < 1f) return;
            float dt = Time.unscaledDeltaTime;
            input.AddLookDegrees(inertia * dt);
            inertia *= Mathf.Exp(-inertiaDecay * dt);
        }
    }
}
