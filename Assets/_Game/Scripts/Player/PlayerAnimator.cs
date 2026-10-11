using Greenhaven.Collectibles;
using Greenhaven.Interaction;
using UnityEngine;

namespace Greenhaven.Player
{
    /// <summary>
    /// Maps <see cref="PlayerMotor"/> and <see cref="PlayerInteractor"/> state to Animator parameters.
    /// Contains no movement logic. Parameters: MoveSpeed (float, m/s), Grounded (bool), Jump, Interact,
    /// PickUp, Emote (triggers), Talking (bool). Also fades the upper-body "Actions" layer in only while
    /// a gesture is playing, so an idle gesture layer never freezes the arms during locomotion.
    /// </summary>
    public sealed class PlayerAnimator : MonoBehaviour
    {
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int InteractId = Animator.StringToHash("Interact");
        static readonly int PickUpId = Animator.StringToHash("PickUp");
        static readonly int EmoteId = Animator.StringToHash("Emote");
        static readonly int TalkingId = Animator.StringToHash("Talking");

        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] Animator animator;
        [SerializeField, Min(0f), Tooltip("Ignore brief ungrounded moments (steps, slope seams) shorter than this.")]
        float airborneDelay = 0.15f;
        [SerializeField, Min(0f)] float speedDamping = 0.08f;

        [Header("Gestures")]
        [SerializeField, Min(0.1f), Tooltip("How long the upper-body layer stays in after an interact gesture.")]
        float gestureSeconds = 1.3f;
        [SerializeField, Min(0.1f)] float layerFadeSpeed = 6f;

        [Header("Idle emote")]
        [SerializeField, Tooltip("Seconds standing still before a little dance. 0 disables it.")]
        float idleEmoteDelay = 14f;

        float ungroundedTime;
        bool jumping;
        int actionsLayer = -1;
        float layerWeight;
        float gestureTimer;
        float idleTime;
        bool hasTalking, hasPickUp, hasEmote;

        public Animator Animator => animator;

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

            actionsLayer = animator.GetLayerIndex("Actions");
            // Older controllers may lack the newer parameters; only drive what exists.
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == TalkingId) hasTalking = true;
                else if (p.nameHash == PickUpId) hasPickUp = true;
                else if (p.nameHash == EmoteId) hasEmote = true;
            }
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
            idleTime = 0f;
            animator.SetTrigger(JumpId);
        }

        void OnInteracted(IInteractable target)
        {
            idleTime = 0f;
            // Picking something up off the ground is a full-body move; everything else is an upper-body gesture.
            if (hasPickUp && target is Collectible && motor.IsGrounded && motor.PlanarSpeed < 1f)
            {
                animator.SetTrigger(PickUpId);
                return;
            }
            animator.SetTrigger(InteractId);
            gestureTimer = gestureSeconds;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float speed = motor.PlanarSpeed;

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

            animator.SetFloat(MoveSpeedId, speed, speedDamping, dt);
            animator.SetBool(GroundedId, !airborne);

            bool talking = interactor != null && interactor.CurrentTarget is DialogueInteractable speaker && speaker.IsInConversation;
            if (hasTalking) animator.SetBool(TalkingId, talking);

            // Upper-body layer: in while gesturing or talking, otherwise out.
            gestureTimer = Mathf.Max(0f, gestureTimer - dt);
            if (actionsLayer >= 0)
            {
                float target = gestureTimer > 0f || (talking && hasTalking) ? 1f : 0f;
                layerWeight = Mathf.MoveTowards(layerWeight, target, layerFadeSpeed * dt);
                animator.SetLayerWeight(actionsLayer, layerWeight);
            }

            // Idle emote after standing still for a while.
            if (hasEmote && idleEmoteDelay > 0f)
            {
                bool still = speed < 0.05f && !airborne && !talking;
                idleTime = still ? idleTime + dt : 0f;
                if (idleTime >= idleEmoteDelay)
                {
                    animator.SetTrigger(EmoteId);
                    idleTime = 0f;
                }
            }
        }
    }
}
