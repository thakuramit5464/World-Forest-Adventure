using Greenhaven.Player;
using UnityEngine;

namespace Greenhaven.CameraRig
{
    /// <summary>
    /// Lightweight orbiting follow camera with sphere-cast obstacle avoidance.
    /// Place on the camera. Chosen over Cinemachine for Phase 1 to avoid a package
    /// dependency; see plan.md "Camera decision".
    /// </summary>
    public sealed class ThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerInputReader input;

        [Header("Framing")]
        [SerializeField] Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField, Min(0.5f)] float distance = 5.5f;
        [SerializeField] float startPitch = 15f;
        [SerializeField] float minPitch = -25f;
        [SerializeField] float maxPitch = 65f;
        [SerializeField, Min(0f)] float followSmoothTime = 0.08f;

        [Header("Collision")]
        [SerializeField] LayerMask obstacleMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] float collisionRadius = 0.25f;
        [SerializeField, Min(0.1f)] float minDistance = 0.8f;
        [SerializeField, Min(0.1f), Tooltip("Metres per second the camera eases back out after an obstacle clears.")]
        float recoverSpeed = 6f;

        float yaw;
        float pitch;
        float currentDistance;
        Vector3 pivot;
        Vector3 pivotVelocity;

        void Start()
        {
            if (target == null)
            {
                Debug.LogError($"{nameof(ThirdPersonCameraRig)} needs a target. Disabling.", this);
                enabled = false;
                return;
            }
            if (input == null) input = target.GetComponent<PlayerInputReader>();

            yaw = target.eulerAngles.y;
            pitch = startPitch;
            currentDistance = distance;
            pivot = target.position + pivotOffset;
            Apply(0f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (input != null)
            {
                Vector2 look = input.ConsumeLookDegrees(dt);
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
            }

            pivot = Vector3.SmoothDamp(pivot, target.position + pivotOffset, ref pivotVelocity, followSmoothTime);
            Apply(dt);
        }

        void Apply(float dt)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float wanted = distance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance,
                    obstacleMask, QueryTriggerInteraction.Ignore))
            {
                wanted = Mathf.Max(minDistance, hit.distance);
            }

            // Snap in immediately so the player is never hidden; ease back out to avoid popping.
            currentDistance = wanted < currentDistance || dt <= 0f
                ? wanted
                : Mathf.MoveTowards(currentDistance, wanted, recoverSpeed * dt);

            transform.SetPositionAndRotation(pivot + back * currentDistance, rotation);
        }
    }
}
