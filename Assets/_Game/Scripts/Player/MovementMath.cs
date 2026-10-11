using UnityEngine;

namespace Greenhaven.Player
{
    /// <summary>
    /// Pure movement helpers, kept free of scene state so they can be unit tested.
    /// </summary>
    public static class MovementMath
    {
        /// <summary>
        /// Converts 2D stick/keyboard input into a world-space planar direction relative
        /// to a camera yaw (degrees). The result magnitude is clamped to 1.
        /// </summary>
        public static Vector3 CameraRelative(Vector2 input, float cameraYawDegrees)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            Quaternion yaw = Quaternion.Euler(0f, cameraYawDegrees, 0f);
            return yaw * new Vector3(input.x, 0f, input.y);
        }

        /// <summary>
        /// Initial upward velocity needed to reach <paramref name="height"/> under
        /// <paramref name="gravity"/> (negative, m/s²).
        /// </summary>
        public static float JumpVelocity(float height, float gravity)
        {
            if (height <= 0f || gravity >= 0f) return 0f;
            return Mathf.Sqrt(-2f * gravity * height);
        }
    }
}
