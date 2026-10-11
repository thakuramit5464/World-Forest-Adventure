using UnityEngine;

namespace Greenhaven.Interaction
{
    /// <summary>
    /// Ranks candidate interactables. Lower is better; prefers things that are close
    /// and roughly in front of the player. Pure so it can be unit tested.
    /// </summary>
    public static class InteractionScoring
    {
        /// <summary>Returns a score, or float.PositiveInfinity when out of range.</summary>
        public static float Score(Vector3 origin, Vector3 forward, Vector3 point, float range)
        {
            Vector3 toPoint = point - origin;
            toPoint.y = 0f;
            float dist = toPoint.magnitude;
            if (dist > range) return float.PositiveInfinity;
            if (dist < 0.0001f) return 0f;

            forward.y = 0f;
            float facing = forward.sqrMagnitude > 0.0001f
                ? Vector3.Dot(forward.normalized, toPoint / dist) // 1 ahead, -1 behind
                : 0f;

            // Things behind the player still count (touch players often overshoot), but cost more.
            return dist / range + (1f - facing) * 0.5f;
        }
    }
}
