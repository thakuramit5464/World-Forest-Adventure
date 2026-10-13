using Greenhaven.Interaction;
using UnityEngine;

namespace Greenhaven.World
{
    /// <summary>
    /// A villager who strolls between waypoints (along the village paths), pauses now and then, and stops
    /// to chat when spoken to. Kinematic: follows the ground with a raycast; the capsule collider on the NPC
    /// keeps the player from walking through. Drives the Animator's MoveSpeed.
    /// </summary>
    public sealed class VillagerWalker : MonoBehaviour
    {
        [SerializeField] Vector3[] waypoints = new Vector3[0];
        [SerializeField, Min(0.1f)] float speed = 1.0f;
        [SerializeField, Min(1f)] float turnSpeed = 240f;
        [SerializeField] Vector2 pauseSeconds = new Vector2(2f, 6f);
        [SerializeField, Range(0f, 1f), Tooltip("Chance to pause at a waypoint.")] float pauseChance = 0.6f;
        [SerializeField] LayerMask groundMask = ~(1 << 2);
        [SerializeField] Animator animator;
        [SerializeField] DialogueInteractable talk;

        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");

        int index, direction = 1;
        float pauseTimer, currentSpeed;
        Quaternion restRotation;

        public void Configure(Vector3[] route, float walkSpeed)
        {
            waypoints = route;
            speed = walkSpeed;
        }

        void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (talk == null) TryGetComponent(out talk);
            pauseTimer = Random.Range(0f, pauseSeconds.y);
            if (waypoints.Length > 0) index = Nearest();
        }

        int Nearest()
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < waypoints.Length; i++)
            {
                float d = (waypoints[i] - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool talking = talk != null && talk.IsInConversation;
            bool moving = false;
            if (!talking && waypoints.Length > 1)
            {
                if (pauseTimer > 0f) pauseTimer -= dt;
                else
                {
                    Vector3 target = waypoints[index];
                    Vector3 to = target - transform.position;
                    to.y = 0f;
                    if (to.magnitude < 0.3f)
                    {
                        // Walk the route out and back (so it never needs to be a loop).
                        if (index + direction >= waypoints.Length || index + direction < 0) direction = -direction;
                        index += direction;
                        if (Random.value < pauseChance) pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
                    }
                    else
                    {
                        moving = true;
                        Quaternion face = Quaternion.LookRotation(to.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * dt);
                        float angle = Quaternion.Angle(transform.rotation, face);
                        float wanted = angle > 60f ? speed * 0.3f : speed;
                        currentSpeed = Mathf.MoveTowards(currentSpeed, wanted, 3f * dt);
                        Vector3 step = transform.forward * (currentSpeed * dt);
                        Vector3 next = transform.position + step;
                        if (Physics.Raycast(next + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
                            next.y = hit.point.y;
                        transform.position = next;
                    }
                }
            }
            if (!moving) currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, 4f * dt);
            if (animator != null) animator.SetFloat(MoveSpeedId, currentSpeed, 0.1f, dt);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f);
            for (int i = 0; i < waypoints.Length - 1; i++) Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
        }
    }
}
