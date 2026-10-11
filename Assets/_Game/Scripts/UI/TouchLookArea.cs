using Greenhaven.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Greenhaven.UI
{
    /// <summary>
    /// Invisible screen region (normally the right half) that rotates the camera
    /// when dragged. Needs a raycast-target Graphic on the same object.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IDragHandler
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField, Tooltip("Camera rotation in degrees for a drag across the full screen height.")]
        float degreesPerScreenHeight = 200f;
        [SerializeField] bool invertY;

        void Awake()
        {
            if (input == null)
            {
                Debug.LogWarning($"{nameof(TouchLookArea)} has no {nameof(PlayerInputReader)}; touch look disabled.", this);
                enabled = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!enabled) return;
            // Normalise by screen height so the feel is the same on any resolution/DPI.
            Vector2 degrees = eventData.delta * (degreesPerScreenHeight / Mathf.Max(1, Screen.height));
            if (invertY) degrees.y = -degrees.y;
            input.AddLookDegrees(degrees);
        }
    }
}
