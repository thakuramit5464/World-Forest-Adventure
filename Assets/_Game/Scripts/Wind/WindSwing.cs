using UnityEngine;

namespace Greenhaven.Wind
{
    /// <summary>
    /// Swings a hanging object (sign board, banner, lantern) about its own pivot, driven by the shared
    /// <see cref="WindManager"/>. Put it on the object whose pivot is the hinge. For a handful of props
    /// only — vegetation is animated by the Greenhaven/WindLit shader instead.
    /// </summary>
    public sealed class WindSwing : MonoBehaviour
    {
        [SerializeField, Tooltip("Local axis the object swings around (the hinge).")] Vector3 hingeAxis = Vector3.right;
        [SerializeField, Range(0f, 45f)] float amplitudeDegrees = 14f;
        [SerializeField, Range(0.1f, 4f)] float frequency = 1.3f;
        [SerializeField, Range(0f, 1f), Tooltip("Small extra twist about the vertical axis.")] float twist = 0.25f;

        Quaternion rest;

        void Awake() => rest = transform.localRotation;

        void OnDisable() => transform.localRotation = rest;

        void Update()
        {
            WindManager wind = WindManager.Current;
            if (wind == null)
            {
                transform.localRotation = rest;
                return;
            }
            // Same sway curve as the vegetation shader, so signs move with the trees around them. The swing
            // direction follows how the wind meets the hinge.
            float sway = wind.Sway(transform.position, frequency);
            // Hinge direction from the rest pose (not the swung pose) so the swing doesn't feed back on itself.
            Quaternion parentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 hingeWorld = parentRotation * rest * hingeAxis;
            float facing = Vector3.Dot(Vector3.Cross(hingeWorld, Vector3.up), wind.Direction);
            float angle = amplitudeDegrees * sway * facing;
            // A slower, offset sample adds a small twist so the board doesn't swing like a metronome.
            float yaw = amplitudeDegrees * twist * 0.3f * (wind.Sway(transform.position + Vector3.right * 3f, frequency * 0.7f) - 0.4f);
            transform.localRotation = rest * Quaternion.AngleAxis(angle, hingeAxis) * Quaternion.AngleAxis(yaw, Vector3.up);
        }
    }
}
