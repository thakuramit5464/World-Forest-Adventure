using Greenhaven.Player;
using UnityEngine;

namespace Greenhaven.CameraRig
{
    /// <summary>
    /// Lightweight orbiting follow camera with sphere-cast obstacle avoidance. Look input is smoothed, the
    /// camera drifts back behind the hero while they walk without the player steering the view (so touch and
    /// gamepad players rarely need to touch the camera), and the field of view widens slightly at a run.
    /// Chosen over Cinemachine to avoid a package dependency; see plan.md "Camera decision".
    /// </summary>
    [RequireComponent(typeof(Camera))]
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

        [Header("Look feel")]
        [SerializeField, Min(0f), Tooltip("Seconds of smoothing on look input (0 = raw).")] float lookSmoothTime = 0.035f;
        [SerializeField, Tooltip("Drift back behind the hero while walking without look input.")] bool autoRecentre = true;
        [SerializeField, Min(0f)] float recentreDelay = 1.2f;
        [SerializeField, Min(0f), Tooltip("Fraction of the remaining angle closed per second.")] float recentreRate = 1.4f;
        [SerializeField, Min(0f), Tooltip("Extra field of view while running.")] float runFovBoost = 5f;

        [Header("Collision")]
        [SerializeField] LayerMask obstacleMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] float collisionRadius = 0.25f;
        [SerializeField, Min(0.1f)] float minDistance = 0.8f;
        [SerializeField, Min(0.1f), Tooltip("Metres per second the camera eases back out after an obstacle clears.")]
        float recoverSpeed = 6f;

        Camera cam;
        PlayerMotor motor;
        float yaw, pitch, currentDistance, baseFov, fovVelocity;
        float lastLookTime = float.NegativeInfinity;
        Vector2 lookSmoothed, lookVelocity;
        Vector3 pivot, pivotVelocity;

        void Start()
        {
            cam = GetComponent<Camera>();
            baseFov = cam.fieldOfView;
            if (target == null)
            {
                Debug.LogError($"{nameof(ThirdPersonCameraRig)} needs a target. Disabling.", this);
                enabled = false;
                return;
            }
            if (input == null) input = target.GetComponent<PlayerInputReader>();
            motor = target.GetComponent<PlayerMotor>();

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
                if (look.sqrMagnitude > 1e-6f) lastLookTime = Time.time;
                // Smooth the per-frame look so touch and stick input never stutter.
                Vector2 perSecond = dt > 0f ? look / dt : Vector2.zero;
                lookSmoothed = lookSmoothTime > 0f ? Vector2.SmoothDamp(lookSmoothed, perSecond, ref lookVelocity, lookSmoothTime) : perSecond;
                Vector2 applied = lookSmoothed * dt;
                yaw += applied.x;
                pitch = Mathf.Clamp(pitch - applied.y, minPitch, maxPitch);
            }

            if (autoRecentre && motor != null && Time.time - lastLookTime > recentreDelay && motor.IsGrounded && motor.PlanarSpeed > 1f)
            {
                // Follow round behind the hero, but not when steering sideways (that would spiral) or when they walk toward the camera.
                Vector2 move = input != null ? input.Move : Vector2.zero;
                float delta = Mathf.DeltaAngle(yaw, target.eulerAngles.y);
                if (Mathf.Abs(move.x) < 0.6f && Mathf.Abs(delta) < 140f)
                    yaw += delta * (1f - Mathf.Exp(-recentreRate * dt));
            }

            pivot = Vector3.SmoothDamp(pivot, target.position + pivotOffset, ref pivotVelocity, followSmoothTime);
            float wantedFov = baseFov + (motor != null && motor.IsRunning ? runFovBoost : 0f);
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, wantedFov, ref fovVelocity, 0.25f);
            Apply(dt);
        }

        void Apply(float dt)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float wanted = distance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(minDistance, hit.distance);

            // Snap in immediately so the player is never hidden; ease back out to avoid popping.
            currentDistance = wanted < currentDistance || dt <= 0f ? wanted : Mathf.MoveTowards(currentDistance, wanted, recoverSpeed * dt);
            transform.SetPositionAndRotation(pivot + back * currentDistance, rotation);
        }
    }
}
