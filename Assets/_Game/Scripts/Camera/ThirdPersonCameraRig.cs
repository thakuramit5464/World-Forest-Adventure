using Greenhaven.Player;
using Greenhaven.UI;
using UnityEngine;

namespace Greenhaven.CameraRig
{
    /// <summary>
    /// Orbiting follow camera. Mouse input is applied almost raw (responsive); stick and touch input is
    /// smoothed. The pivot follows the hero with soft vertical damping (jumps and landings do not bounce the
    /// view) and a little look-ahead in the direction of travel. On touch and gamepad the camera drifts back
    /// behind the hero while walking; mouse players keep full control. Scroll zooms. A sphere cast keeps the
    /// camera out of walls and ground, snapping in and easing back out. No Cinemachine dependency.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerInputReader input;

        [Header("Framing")]
        [SerializeField] Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField, Min(0.5f)] float distance = 5.5f;
        [SerializeField, Min(1f)] float minZoom = 2.5f;
        [SerializeField, Min(2f)] float maxZoom = 8.5f;
        [SerializeField] float startPitch = 15f;
        [SerializeField] float minPitch = -25f;
        [SerializeField] float maxPitch = 65f;
        [SerializeField, Min(0f)] float followSmoothTime = 0.06f;
        [SerializeField, Min(0f), Tooltip("Slower vertical follow so steps, jumps and landings stay calm.")] float verticalSmoothTime = 0.18f;
        [SerializeField, Min(0f), Tooltip("Metres the pivot leads the hero at full run.")] float lookAhead = 0.35f;

        [Header("Look feel")]
        [SerializeField, Min(0f), Tooltip("Smoothing for stick and touch input (seconds).")] float stickSmoothTime = 0.05f;
        [SerializeField, Min(0f), Tooltip("Smoothing for the mouse (seconds; keep tiny).")] float mouseSmoothTime = 0.012f;
        [SerializeField, Tooltip("Touch and gamepad only: drift back behind the hero while walking without look input.")] bool autoRecentre = true;
        [SerializeField, Min(0f)] float recentreDelay = 1.2f;
        [SerializeField, Min(0f), Tooltip("Fraction of the remaining angle closed per second.")] float recentreRate = 1.4f;
        [SerializeField, Min(0f), Tooltip("Extra field of view while running.")] float runFovBoost = 5f;

        [Header("Collision")]
        [SerializeField] LayerMask obstacleMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] float collisionRadius = 0.3f;
        [SerializeField, Min(0.1f)] float minDistance = 0.9f;
        [SerializeField, Min(0.1f), Tooltip("Metres per second the camera eases back out after an obstacle clears.")]
        float recoverSpeed = 5f;

        Camera cam;
        PlayerMotor motor;
        float yaw, pitch, currentDistance, wantedDistance, baseFov, fovVelocity;
        float lastLookTime = float.NegativeInfinity;
        Vector2 lookSmoothed, lookVelocity;
        Vector3 pivot;
        Vector2 pivotVelocityXZ;
        float pivotVelocityY;
        Vector3 lastTargetPosition;

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
            wantedDistance = currentDistance = Mathf.Clamp(distance, minZoom, maxZoom);
            pivot = target.position + pivotOffset;
            lastTargetPosition = target.position;
            Apply(0f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (input != null)
            {
                Vector2 look = input.ConsumeLookDegrees(dt, out bool fromMouse);
                if (look.sqrMagnitude > 1e-6f) lastLookTime = Time.time;
                float smoothTime = fromMouse ? mouseSmoothTime : stickSmoothTime;
                Vector2 perSecond = look / dt;
                lookSmoothed = smoothTime > 0f ? Vector2.SmoothDamp(lookSmoothed, perSecond, ref lookVelocity, smoothTime) : perSecond;
                Vector2 applied = lookSmoothed * dt;
                yaw += applied.x;
                pitch = Mathf.Clamp(pitch - applied.y, minPitch, maxPitch);
                wantedDistance = Mathf.Clamp(wantedDistance + input.ConsumeZoom(), minZoom, maxZoom);
            }

            bool handsFreeDevice = ActiveInput.Current != InputKind.KeyboardMouse;
            if (autoRecentre && GameSettings.AutoRecentre && handsFreeDevice && motor != null && Time.time - lastLookTime > recentreDelay && motor.IsGrounded && motor.PlanarSpeed > 1f)
            {
                // Follow round behind the hero, but not when steering sideways (that would spiral) or walking toward the camera.
                Vector2 move = input != null ? input.Move : Vector2.zero;
                float delta = Mathf.DeltaAngle(yaw, target.eulerAngles.y);
                if (Mathf.Abs(move.x) < 0.6f && Mathf.Abs(delta) < 140f)
                    yaw += delta * (1f - Mathf.Exp(-recentreRate * dt));
            }

            // Pivot: quick on the ground plane, calm vertically, leading the hero slightly at speed.
            Vector3 velocity = (target.position - lastTargetPosition) / dt;
            lastTargetPosition = target.position;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            float lead = motor != null ? Mathf.Clamp01(planar.magnitude / 5.5f) * lookAhead : 0f;
            Vector3 wantedPivot = target.position + pivotOffset + (planar.sqrMagnitude > 0.01f ? planar.normalized * lead : Vector3.zero);
            Vector2 xz = Vector2.SmoothDamp(new Vector2(pivot.x, pivot.z), new Vector2(wantedPivot.x, wantedPivot.z), ref pivotVelocityXZ, followSmoothTime);
            float y = Mathf.SmoothDamp(pivot.y, wantedPivot.y, ref pivotVelocityY, motor != null && !motor.IsGrounded ? verticalSmoothTime : verticalSmoothTime * 0.5f);
            pivot = new Vector3(xz.x, y, xz.y);

            float wantedFov = baseFov + (motor != null && motor.IsRunning ? runFovBoost : 0f);
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, wantedFov, ref fovVelocity, 0.25f);
            Apply(dt);
        }

        void Apply(float dt)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float wanted = wantedDistance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, wantedDistance, obstacleMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(minDistance, hit.distance - 0.05f);

            // Snap in immediately so the hero is never hidden; ease back out to avoid popping.
            currentDistance = wanted < currentDistance || dt <= 0f ? wanted : Mathf.MoveTowards(currentDistance, wanted, recoverSpeed * dt);
            transform.SetPositionAndRotation(pivot + back * currentDistance, rotation);
        }
    }
}
