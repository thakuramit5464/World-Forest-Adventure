using Greenhaven.Interaction;
using UnityEngine;

namespace Greenhaven.Player
{
    /// <summary>
    /// Maps <see cref="PlayerMotor"/> and <see cref="PlayerInteractor"/> events to Animator parameters.
    /// Contains no movement logic. Parameters: MoveSpeed (float, m/s), Grounded (bool),
    /// Jump (trigger), Interact (trigger).
    /// </summary>
    public sealed class PlayerAnimator : MonoBehaviour
    {
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int InteractId = Animator.StringToHash("Interact");

        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] Animator animator;
        [SerializeField, Min(0f), Tooltip("Ignore brief ungrounded moments (steps, slope seams) shorter than this.")]
        float airborneDelay = 0.15f;
        [SerializeField, Min(0f)] float speedDamping = 0.08f;

        float ungroundedTime;
        bool jumping;

        void Awake()
        {
            if (motor == null) TryGetComponent(out motor);
            if (interactor == null) TryGetComponent(out interactor);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (motor == null || animator == null)
            {
                Debug.LogWarning($"{nameof(PlayerAnimator)} on '{name}' is missing a motor or Animator; animation disabled.", this);
                enabled = false;
                return;
            }
            animator.applyRootMotion = false;
            if (animator.avatar == null || !animator.avatar.isValid)
                Debug.LogWarning($"{nameof(PlayerAnimator)}: the character avatar is missing or invalid; humanoid clips will not play.", animator);
        }

        void OnEnable()
        {
            if (motor != null) motor.Jumped += OnJumped;
            if (interactor != null) interactor.Interacted += OnInteracted;
        }

        void OnDisable()
        {
            if (motor != null) motor.Jumped -= OnJumped;
            if (interactor != null) interactor.Interacted -= OnInteracted;
        }

        void OnJumped()
        {
            jumping = true;
            animator.SetTrigger(JumpId);
        }

        void OnInteracted(IInteractable _) => animator.SetTrigger(InteractId);

        void Update()
        {
            float dt = Time.deltaTime;

            // Only clear the jump once we are grounded AND no longer rising; on the jump frame
            // the motor still reports grounded from before the jump.
            if (motor.IsGrounded && motor.Velocity.y <= 0f)
            {
                ungroundedTime = 0f;
                jumping = false;
            }
            else if (!motor.IsGrounded)
            {
                ungroundedTime += dt;
            }
            bool airborne = jumping || ungroundedTime > airborneDelay;

            animator.SetFloat(MoveSpeedId, motor.PlanarSpeed, speedDamping, dt);
            animator.SetBool(GroundedId, !airborne);
        }
    }
}
