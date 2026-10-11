using UnityEngine;

namespace Greenhaven.Player
{
    /// <summary>
    /// Maps <see cref="PlayerMotor"/> state to Animator parameters. Contains no movement logic.
    /// Parameters: MoveSpeed (float, m/s), Grounded (bool), RunPlaybackSpeed (float).
    /// </summary>
    public sealed class PlayerAnimator : MonoBehaviour
    {
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int RunPlaybackSpeedId = Animator.StringToHash("RunPlaybackSpeed");

        [SerializeField] PlayerMotor motor;
        [SerializeField] Animator animator;
        [SerializeField, Min(0.1f), Tooltip("Ground speed (m/s) at which the run clip plays at normal speed.")]
        float runClipSpeed = 5f;
        [SerializeField, Range(0.1f, 1f), Tooltip("Slowest run-clip playback, used while walking.")]
        float minRunPlayback = 0.55f;
        [SerializeField, Min(0f), Tooltip("Ignore brief ungrounded moments (steps, slope seams) shorter than this.")]
        float airborneDelay = 0.12f;
        [SerializeField, Min(0f)] float speedDamping = 0.08f;

        float ungroundedTime;
        bool jumpedThisAirtime;

        void Awake()
        {
            if (motor == null) TryGetComponent(out motor);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (motor == null || animator == null)
            {
                Debug.LogWarning($"{nameof(PlayerAnimator)} on '{name}' is missing a motor or Animator; animation disabled.", this);
                enabled = false;
                return;
            }
            animator.applyRootMotion = false;
        }

        void OnEnable()
        {
            if (motor != null) motor.Jumped += OnJumped;
        }

        void OnDisable()
        {
            if (motor != null) motor.Jumped -= OnJumped;
        }

        void OnJumped() => jumpedThisAirtime = true;

        void Update()
        {
            float dt = Time.deltaTime;
            float speed = motor.PlanarSpeed;

            if (motor.IsGrounded)
            {
                ungroundedTime = 0f;
                jumpedThisAirtime = false;
            }
            else
            {
                ungroundedTime += dt;
            }
            bool showAirborne = jumpedThisAirtime || ungroundedTime > airborneDelay;

            animator.SetFloat(MoveSpeedId, speed, speedDamping, dt);
            animator.SetBool(GroundedId, !showAirborne);
            animator.SetFloat(RunPlaybackSpeedId, Mathf.Max(minRunPlayback, speed / runClipSpeed));
        }
    }
}
