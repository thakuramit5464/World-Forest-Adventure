using UnityEngine;

namespace Greenhaven.Wildlife
{
    public enum AnimalKind { Fox, Deer, Rabbit, Goat, Bird }

    /// <summary>
    /// Tunable behaviour and animation settings shared by every animal of one species. Created once by
    /// Greenhaven ▸ Wildlife ▸ Generate Wildlife and never overwritten afterwards, so Inspector tweaks stick.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenhaven/Wildlife Species", fileName = "WildlifeSpecies")]
    public sealed class WildlifeSpecies : ScriptableObject
    {
        public AnimalKind kind;

        [Header("Movement")]
        [Min(0f)] public float walkSpeed = 1f;
        [Min(0f)] public float trotSpeed = 2.5f;
        [Min(0f)] public float fleeSpeed = 6f;
        [Min(1f), Tooltip("Degrees per second.")] public float turnSpeed = 220f;
        [Min(0.1f)] public float acceleration = 5f;
        [Min(1f), Tooltip("How far from its home spot the animal wanders.")] public float wanderRadius = 8f;
        [Range(5f, 60f)] public float maxSlope = 32f;
        [Min(0.05f), Tooltip("Radius used for obstacle checks.")] public float bodyRadius = 0.25f;

        [Header("Awareness")]
        [Min(0f)] public float detectDistance = 14f;
        [Min(0f), Tooltip("Flees when the player comes this close (scaled by boldness).")] public float fleeDistance = 6f;
        [Min(0f), Tooltip("Never lets the player closer than this without fleeing.")] public float personalSpace = 2.5f;
        [Min(0f), Tooltip("Stops fleeing once the player is this far away.")] public float calmDistance = 18f;
        [Min(1f), Tooltip("A running player is noticed this much further away.")] public float runningPlayerMultiplier = 1.5f;

        [Header("Behaviour")]
        [Range(0f, 1f)] public float wanderChance = 0.45f;
        [Range(0f, 1f)] public float grazeChance = 0.35f;
        public Vector2 idleSeconds = new Vector2(2f, 5f);
        public Vector2 grazeSeconds = new Vector2(4f, 9f);
        [Range(0f, 1f), Tooltip("Chance to approach and investigate a calm player.")] public float curiosity = 0.25f;
        [Tooltip("Each animal gets a boldness in this range (bold animals let the player closer).")] public Vector2 boldnessRange = new Vector2(0.2f, 0.6f);

        [Header("Animation")]
        [Min(0.05f), Tooltip("Metres covered by one walk cycle (keeps feet from sliding).")] public float strideLength = 0.6f;
        [Range(0f, 60f)] public float legSwingDegrees = 26f;
        [Range(0f, 0.2f)] public float bodyBob = 0.015f;
        [Range(0f, 120f), Tooltip("Total head-down angle when grazing (neck + head).")] public float grazeHeadPitch = 50f;
        [Range(0f, 0.5f), Tooltip("Rabbits: height of each hop.")] public float hopHeight = 0f;
        [Range(1f, 30f), Tooltip("Birds: wing beats per second.")] public float flapRate = 12f;

        [Header("Birds")]
        [Min(0.5f)] public float flightSpeed = 5f;
        [Min(0.5f)] public float flightHeight = 3f;

        [Header("Audio (optional; leave empty for silence)")]
        public AudioClip[] calls;
        [Range(0f, 1f)] public float callVolume = 0.35f;
        public Vector2 callInterval = new Vector2(14f, 35f);

        /// <summary>Sensible starting values for each species.</summary>
        public void ApplyDefaults(AnimalKind k)
        {
            kind = k;
            switch (k)
            {
                case AnimalKind.Fox:
                    Set(walk: 1.1f, trot: 2.6f, flee: 6.5f, wander: 12f, radius: 0.22f, detect: 15f, flee2: 6f, space: 3f, calm: 20f,
                        stride: 0.62f, swing: 28f, bob: 0.012f, graze: 80f);
                    wanderChance = 0.55f; grazeChance = 0.25f; curiosity = 0.35f; boldnessRange = new Vector2(0.25f, 0.65f);
                    break;
                case AnimalKind.Deer:
                    Set(walk: 1.0f, trot: 2.8f, flee: 7.5f, wander: 10f, radius: 0.38f, detect: 18f, flee2: 8f, space: 4f, calm: 24f,
                        stride: 0.95f, swing: 24f, bob: 0.018f, graze: 105f);
                    wanderChance = 0.35f; grazeChance = 0.5f; curiosity = 0.15f; boldnessRange = new Vector2(0.1f, 0.45f);
                    break;
                case AnimalKind.Rabbit:
                    Set(walk: 0.9f, trot: 1.8f, flee: 5.5f, wander: 5f, radius: 0.14f, detect: 9f, flee2: 4f, space: 2f, calm: 12f,
                        stride: 0.45f, swing: 32f, bob: 0f, graze: 25f);
                    hopHeight = 0.12f; wanderChance = 0.45f; grazeChance = 0.4f; curiosity = 0.1f; boldnessRange = new Vector2(0.05f, 0.4f);
                    break;
                case AnimalKind.Goat:
                    Set(walk: 0.8f, trot: 2.0f, flee: 4.5f, wander: 7f, radius: 0.32f, detect: 12f, flee2: 4f, space: 2.5f, calm: 14f,
                        stride: 0.65f, swing: 22f, bob: 0.014f, graze: 100f);
                    maxSlope = 42f; wanderChance = 0.35f; grazeChance = 0.55f; curiosity = 0.45f; boldnessRange = new Vector2(0.4f, 0.85f);
                    break;
                default:
                    Set(walk: 0.5f, trot: 0.8f, flee: 0.8f, wander: 3f, radius: 0.08f, detect: 7f, flee2: 5f, space: 3f, calm: 10f,
                        stride: 0.12f, swing: 10f, bob: 0f, graze: 60f);
                    flapRate = 12f; flightSpeed = 5f; flightHeight = 3f; wanderChance = 0.3f; grazeChance = 0.45f; curiosity = 0f;
                    boldnessRange = new Vector2(0.1f, 0.5f); callInterval = new Vector2(8f, 20f);
                    break;
            }
        }

        void Set(float walk, float trot, float flee, float wander, float radius, float detect, float flee2, float space, float calm,
            float stride, float swing, float bob, float graze)
        {
            walkSpeed = walk; trotSpeed = trot; fleeSpeed = flee; wanderRadius = wander; bodyRadius = radius;
            detectDistance = detect; fleeDistance = flee2; personalSpace = space; calmDistance = calm;
            strideLength = stride; legSwingDegrees = swing; bodyBob = bob; grazeHeadPitch = graze;
        }
    }
}
