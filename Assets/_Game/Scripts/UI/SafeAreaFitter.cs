using UnityEngine;

namespace Greenhaven.UI
{
    /// <summary>
    /// Fits this RectTransform to <see cref="Screen.safeArea"/> so controls avoid
    /// notches and rounded corners. Use on a full-screen child of the canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect appliedArea;
        Vector2Int appliedSize;

        void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            // Cheap comparison; handles rotation and resolution changes.
            if (Screen.safeArea != appliedArea || Screen.width != appliedSize.x || Screen.height != appliedSize.y)
                Apply();
        }

        void Apply()
        {
            appliedArea = Screen.safeArea;
            appliedSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = appliedArea.position;
            Vector2 max = appliedArea.position + appliedArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
