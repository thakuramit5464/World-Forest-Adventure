using System;
using UnityEngine;

namespace Greenhaven.Player
{
    /// <summary>
    /// Camera-relative walking, running, jumping and gravity on a CharacterController.
    /// Reads intent from <see cref="PlayerInputReader"/>; knows nothing about UI.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField, Tooltip("Movement is relative to this transform's yaw. Defaults to the main camera.")]
        Transform cameraTransform;

        [Header("Ground movement")]
        [SerializeField, Min(0f)] float walkSpeed = 2.0f;
        [SerializeField, Min(0f)] float runSpeed = 5.5f;
        [SerializeField, Min(0f)] float acceleration = 25f;
        [SerializeField, Range(0f, 1f)] float airControl = 0.5f;
        [SerializeField, Min(0f), Tooltip("Degrees per second.")] float turnSpeed = 720f;

        [Header("Jump and gravity")]
        [SerializeField, Min(0f)] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -22f;
        [SerializeField] float terminalVelocity = -30f;
        [SerializeField, Tooltip("Small downward speed that keeps the controller snapped to slopes.")]
        float groundedVelocity = -2f;
        [SerializeField, Min(0f), Tooltip("Jump still allowed this long after walking off a ledge.")]
        float coyoteTime = 0.12f;
        [SerializeField, Min(0f), Tooltip("A jump pressed this long before landing still fires.")]
        float jumpBufferTime = 0.12f;

        [Header("Safety")]
        [SerializeField, Tooltip("Respawn at the start position if the player falls below this height.")]
        float respawnBelowY = -25f;

        CharacterController controller;
        Vector3 planarVelocity;
        float verticalVelocity;
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressedTime = float.NegativeInfinity;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public bool IsGrounded { get; private set; }
        public bool IsRunning { get; private set; }
        public float PlanarSpeed => planarVelocity.magnitude;
        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;

        public event Action Jumped;
        public event Action Landed;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (input == null) TryGetComponent(out input);
            if (input == null)
            {
                Debug.LogError($"{nameof(PlayerMotor)} on '{name}' needs a {nameof(PlayerInputReader)} reference. Disabling.", this);
                enabled = false;
                return;
            }

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform == null)
                Debug.LogWarning($"{nameof(PlayerMotor)}: no camera assigned; movement will use world axes.", this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float now = Time.time;

            bool wasGrounded = IsGrounded;
            IsGrounded = controller.isGrounded;
            if (IsGrounded) lastGroundedTime = now;
            if (input.ConsumeJump()) lastJumpPressedTime = now;

            UpdatePlanarVelocity(dt);
            UpdateVerticalVelocity(dt, now);

            CollisionFlags flags = controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            if (!wasGrounded && controller.isGrounded) Landed?.Invoke();
            if (transform.position.y < respawnBelowY) Respawn();
        }

        void UpdatePlanarVelocity(float dt)
        {
            float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
            Vector3 direction = MovementMath.CameraRelative(input.Move, yaw);
            float amount = direction.magnitude;

            IsRunning = input.RunRequested && amount > 0.01f;
            Vector3 target = amount > 0.01f
                ? direction / amount * ((IsRunning ? runSpeed : walkSpeed) * amount)
                : Vector3.zero;

            float accel = IsGrounded ? acceleration : acceleration * airControl;
            planarVelocity = Vector3.MoveTowards(planarVelocity, target, accel * dt);

            if (amount > 0.01f)
            {
                Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * dt);
            }
        }

        void UpdateVerticalVelocity(float dt, float now)
        {
            bool jumpBuffered = now - lastJumpPressedTime <= jumpBufferTime;
            bool withinCoyote = now - lastGroundedTime <= coyoteTime;

            if (jumpBuffered && withinCoyote && verticalVelocity <= 0f)
            {
                verticalVelocity = MovementMath.JumpVelocity(jumpHeight, gravity);
                // Clear both timers so a held/repeated press cannot jump again mid-air.
                lastJumpPressedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                Jumped?.Invoke();
            }
            else if (IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedVelocity;
            }
            else
            {
                verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, terminalVelocity);
            }
        }

        /// <summary>Moves the player back to the start point (used when falling out of the world).</summary>
        public void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
}
