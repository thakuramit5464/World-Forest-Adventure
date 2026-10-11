using Greenhaven.Interaction;
using UnityEngine;

namespace Greenhaven.Player
{
    /// <summary>
    /// Expressive touches layered on top of the humanoid Animator, purely visual:
    /// eye blinks (eye-bone scale), head/eye look-at toward the current interaction target (Animator IK),
    /// and a damped spring sway on the backpack bone driven by the character's acceleration.
    /// Never moves the gameplay root. Requires "IK Pass" on the Animator's base layer for look-at.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class HeroSecondaryMotion : MonoBehaviour
    {
        [SerializeField] Transform leftEye;
        [SerializeField] Transform rightEye;
        [SerializeField] Transform backpack;

        [Header("Blink")]
        [SerializeField, Min(0.5f)] float minBlinkInterval = 2.5f;
        [SerializeField, Min(0.5f)] float maxBlinkInterval = 6f;
        [SerializeField, Min(0.05f)] float blinkDuration = 0.14f;

        [Header("Backpack sway")]
        [SerializeField, Min(1f)] float stiffness = 60f;
        [SerializeField, Min(0f)] float damping = 9f;
        [SerializeField, Tooltip("Degrees of sway per m/s² of acceleration.")] float response = 1.6f;
        [SerializeField, Min(0f)] float maxSwayDegrees = 16f;

        [Header("Look at")]
        [SerializeField, Range(0f, 1f)] float headWeight = 0.7f;
        [SerializeField, Range(0f, 1f)] float bodyWeight = 0.12f;
        [SerializeField, Range(0f, 1f)] float eyesWeight = 1f;
        [SerializeField, Tooltip("Height added to an interaction point so the hero looks at faces, not feet.")]
        float lookHeight = 1.1f;
        [SerializeField, Range(0f, 180f)] float maxLookAngle = 100f;
        [SerializeField, Min(0.1f)] float lookBlendSpeed = 3f;

        Animator animator;
        PlayerInteractor interactor;
        Quaternion backpackRest;
        Vector3 lastPosition, lastVelocity;
        Vector2 sway, swayVelocity;
        float nextBlink, blinkTime = -1f;
        float lookWeight;
        Vector3 lookPoint;

        void Awake()
        {
            animator = GetComponent<Animator>();
            interactor = GetComponentInParent<PlayerInteractor>(); // null for display copies: no look-at
            if (backpack != null) backpackRest = backpack.localRotation;
            lastPosition = transform.position;
            ScheduleBlink();
        }

        void OnDisable()
        {
            SetEyeScale(1f);
            if (backpack != null) backpack.localRotation = backpackRest;
        }

        void ScheduleBlink() => nextBlink = Time.time + Random.Range(minBlinkInterval, maxBlinkInterval);

        // LateUpdate runs after the Animator, so these offsets are not overwritten.
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            UpdateBlink(dt);
            UpdateBackpack(dt);
        }

        void UpdateBlink(float dt)
        {
            if (leftEye == null && rightEye == null) return;
            if (blinkTime < 0f && Time.time >= nextBlink) blinkTime = 0f;
            if (blinkTime < 0f) return;

            blinkTime += dt;
            float t = Mathf.Clamp01(blinkTime / blinkDuration);
            float closed = 1f - Mathf.Abs(t * 2f - 1f); // 0 → 1 → 0
            SetEyeScale(Mathf.Lerp(1f, 0.08f, closed));
            if (t >= 1f)
            {
                blinkTime = -1f;
                SetEyeScale(1f);
                ScheduleBlink();
            }
        }

        void SetEyeScale(float y)
        {
            var scale = new Vector3(1f, y, 1f);
            if (leftEye != null) leftEye.localScale = scale;
            if (rightEye != null) rightEye.localScale = scale;
        }

        void UpdateBackpack(float dt)
        {
            if (backpack == null) return;
            Vector3 velocity = (transform.position - lastPosition) / dt;
            Vector3 acceleration = (velocity - lastVelocity) / dt;
            lastPosition = transform.position;
            lastVelocity = velocity;

            // Accelerating forward tips the pack back; strafing rolls it; landing bounces it.
            Vector3 local = transform.InverseTransformDirection(acceleration);
            var target = new Vector2(
                Mathf.Clamp(-local.z * response - local.y * response * 0.3f, -maxSwayDegrees, maxSwayDegrees),
                Mathf.Clamp(local.x * response, -maxSwayDegrees, maxSwayDegrees));

            swayVelocity += (target - sway) * (stiffness * dt);
            swayVelocity *= Mathf.Exp(-damping * dt);
            sway += swayVelocity * dt;
            sway = Vector2.ClampMagnitude(sway, maxSwayDegrees);
            backpack.localRotation = backpackRest * Quaternion.Euler(sway.x, 0f, sway.y);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            bool want = false;
            if (interactor != null && interactor.CurrentTarget != null)
            {
                Vector3 point = interactor.CurrentTarget.InteractionPoint + Vector3.up * lookHeight;
                Vector3 toPoint = point - transform.position;
                toPoint.y = 0f;
                if (toPoint.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, toPoint) <= maxLookAngle)
                {
                    lookPoint = point;
                    want = true;
                }
            }
            lookWeight = Mathf.MoveTowards(lookWeight, want ? 1f : 0f, Time.deltaTime * lookBlendSpeed);
            if (lookWeight <= 0.001f) return;
            animator.SetLookAtWeight(lookWeight, bodyWeight, headWeight, eyesWeight, 0.6f);
            animator.SetLookAtPosition(lookPoint);
        }
    }
}
