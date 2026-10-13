using Greenhaven.Collectibles;
using Greenhaven.Interaction;
using Greenhaven.Quests;
using Greenhaven.Wind;
using UnityEngine;

namespace Greenhaven.Player
{
    /// <summary>
    /// Expressive touches layered on top of the humanoid Animator, purely visual (never moves the gameplay root):
    /// randomised eye blinks (eye-bone scale; occasional double blinks), head/eye look-at toward the current
    /// interaction target and, while idle, curious glances around (Animator IK), small eye movements, a
    /// damped spring sway on the backpack and scarf, and — when the brow/mouth bones exist (adventurer v2) —
    /// a friendly resting expression, a brief happy face after interacting, collecting or finishing a quest,
    /// a raised brow while looking around, a surprised lift on jumps, and mouth movement while talking.
    /// Requires "IK Pass" on the Animator's base layer for look-at.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class HeroSecondaryMotion : MonoBehaviour
    {
        [SerializeField] Transform leftEye;
        [SerializeField] Transform rightEye;
        [SerializeField] Transform backpack;

        [Header("Face (optional, adventurer v2)")]
        [SerializeField] Transform leftBrow;
        [SerializeField] Transform rightBrow;
        [SerializeField] Transform mouth;
        [SerializeField, Tooltip("Metres a brow moves at full raise.")] float browTravel = 0.012f;
        [SerializeField, Range(0f, 1f), Tooltip("Resting brow lift (friendly, open look).")] float restingBrow = 0.2f;
        [SerializeField, Min(0.5f)] float happySeconds = 1.8f;

        [Header("Scarf (optional, base to tip)")]
        [SerializeField] Transform[] scarf = new Transform[0];
        [SerializeField, Min(0f)] float maxScarfDegrees = 28f;

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
        [SerializeField, Tooltip("Glance around while standing still.")] bool idleLookAround = true;

        Animator animator;
        PlayerInteractor interactor;
        PlayerMotor motor;
        CollectibleRegistry registry;
        QuestManager quests;
        Quaternion backpackRest, leftEyeRest, rightEyeRest;
        Quaternion[] scarfRest = new Quaternion[0];
        Vector3 leftBrowRest, rightBrowRest, mouthRestScale;
        Quaternion leftBrowRot, rightBrowRot;
        Vector3 lastPosition, lastVelocity, acceleration;
        Vector2 sway, swayVelocity, scarfSway, scarfVelocity;
        float nextBlink, blinkTime = -1f, blinkSquint = 1f;
        bool doubleBlink;
        float lookWeight;
        Vector3 lookPoint;
        float idleTimer, glanceTimer, glanceYaw, glancePitch;
        bool glancing;
        Vector2 saccade, saccadeTarget;
        float saccadeTimer;
        float happy, surprise, curious;
        static readonly int TalkingId = Animator.StringToHash("Talking");

        void Awake()
        {
            animator = GetComponent<Animator>();
            interactor = GetComponentInParent<PlayerInteractor>(); // null for display copies: no look-at or reactions
            motor = GetComponentInParent<PlayerMotor>();
            if (backpack != null) backpackRest = backpack.localRotation;
            if (leftEye != null) leftEyeRest = leftEye.localRotation;
            if (rightEye != null) rightEyeRest = rightEye.localRotation;
            if (leftBrow != null) { leftBrowRest = leftBrow.localPosition; leftBrowRot = leftBrow.localRotation; }
            if (rightBrow != null) { rightBrowRest = rightBrow.localPosition; rightBrowRot = rightBrow.localRotation; }
            if (mouth != null) mouthRestScale = mouth.localScale;
            scarfRest = new Quaternion[scarf.Length];
            for (int i = 0; i < scarf.Length; i++) scarfRest[i] = scarf[i] != null ? scarf[i].localRotation : Quaternion.identity;
            lastPosition = transform.position;
            ScheduleBlink();
            glanceTimer = Random.Range(2f, 5f);
        }

        void Start()
        {
            if (interactor == null) return;
            interactor.Interacted += OnInteracted;
            if (motor != null) motor.Jumped += OnJumped;
            registry = FindFirstObjectByType<CollectibleRegistry>();
            if (registry != null) registry.Collected += OnCollected;
            quests = FindFirstObjectByType<QuestManager>();
            if (quests != null) quests.QuestCompleted += OnQuestCompleted;
        }

        void OnDestroy()
        {
            if (interactor != null) interactor.Interacted -= OnInteracted;
            if (motor != null) motor.Jumped -= OnJumped;
            if (registry != null) registry.Collected -= OnCollected;
            if (quests != null) quests.QuestCompleted -= OnQuestCompleted;
        }

        void OnInteracted(IInteractable _) => happy = Mathf.Max(happy, happySeconds);
        void OnCollected(string id, int count, int total) => happy = Mathf.Max(happy, happySeconds * 1.2f);
        void OnQuestCompleted(QuestDefinition _) => happy = Mathf.Max(happy, happySeconds * 1.8f);
        void OnJumped() => surprise = 0.35f;

        /// <summary>Show the happy expression now (e.g. on reaching a landmark).</summary>
        public void Celebrate(float seconds = 2f) => happy = Mathf.Max(happy, seconds);

        void OnDisable()
        {
            SetEyeScale(1f);
            if (backpack != null) backpack.localRotation = backpackRest;
        }

        void ScheduleBlink() => nextBlink = Time.time + Random.Range(minBlinkInterval, maxBlinkInterval);

        bool IsIdle => motor == null || (motor.IsGrounded && motor.PlanarSpeed < 0.25f);

        // LateUpdate runs after the Animator, so these offsets are not overwritten.
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 velocity = (transform.position - lastPosition) / dt;
            acceleration = (velocity - lastVelocity) / dt;
            lastPosition = transform.position;
            lastVelocity = velocity;

            happy = Mathf.Max(0f, happy - dt);
            surprise = Mathf.Max(0f, surprise - dt);
            curious = Mathf.MoveTowards(curious, glancing ? 1f : 0f, dt * 3f);
            float happyWeight = Mathf.Clamp01(happy / 0.35f) * Mathf.Clamp01(happy > 0f ? 1f : 0f);
            blinkSquint = Mathf.Lerp(1f, 0.72f, happyWeight); // smiling eyes

            UpdateBlink(dt);
            UpdateEyes(dt);
            UpdateFace(happyWeight);
            UpdateBackpack(dt);
            UpdateScarf(dt, velocity);
        }

        void UpdateBlink(float dt)
        {
            if (leftEye == null && rightEye == null) return;
            if (blinkTime < 0f && Time.time >= nextBlink) blinkTime = 0f;
            if (blinkTime < 0f)
            {
                SetEyeScale(blinkSquint);
                return;
            }
            blinkTime += dt;
            float t = Mathf.Clamp01(blinkTime / blinkDuration);
            float closed = 1f - Mathf.Abs(t * 2f - 1f); // 0 → 1 → 0
            SetEyeScale(Mathf.Lerp(blinkSquint, 0.08f, closed));
            if (t >= 1f)
            {
                blinkTime = -1f;
                if (!doubleBlink && Random.value < 0.2f)
                {
                    doubleBlink = true;
                    nextBlink = Time.time + 0.12f;
                }
                else
                {
                    doubleBlink = false;
                    ScheduleBlink();
                }
            }
        }

        void SetEyeScale(float y)
        {
            var scale = new Vector3(1f, y, 1f);
            if (leftEye != null) leftEye.localScale = scale;
            if (rightEye != null) rightEye.localScale = scale;
        }

        /// <summary>Small, quick eye movements while nothing is being looked at.</summary>
        void UpdateEyes(float dt)
        {
            if (leftEye == null || rightEye == null) return;
            saccadeTimer -= dt;
            if (saccadeTimer <= 0f)
            {
                saccadeTarget = lookWeight > 0.3f ? Vector2.zero : new Vector2(Random.Range(-6f, 6f), Random.Range(-3f, 3f));
                saccadeTimer = Random.Range(0.8f, 2.6f);
            }
            saccade = Vector2.Lerp(saccade, saccadeTarget, 1f - Mathf.Exp(-25f * dt));
            Quaternion offset = Quaternion.Euler(saccade.y, saccade.x, 0f);
            leftEye.localRotation *= offset;
            rightEye.localRotation *= offset;
        }

        void UpdateFace(float happyWeight)
        {
            float lift = restingBrow + happyWeight * 0.6f + surprise * 1.6f;
            if (leftBrow != null)
            {
                leftBrow.localPosition = leftBrowRest + Vector3.up * ((lift + curious * 0.35f) * browTravel);
                leftBrow.localRotation = leftBrowRot * Quaternion.Euler(0f, 0f, -6f * happyWeight);
            }
            if (rightBrow != null)
            {
                rightBrow.localPosition = rightBrowRest + Vector3.up * (lift * browTravel);
                rightBrow.localRotation = rightBrowRot * Quaternion.Euler(0f, 0f, 6f * happyWeight);
            }
            if (mouth != null)
            {
                bool talking = animator != null && animator.runtimeAnimatorController != null && animator.GetBool(TalkingId);
                float speak = talking ? 0.5f + 0.5f * Mathf.Sin(Time.time * 15f) * Mathf.Sin(Time.time * 4.3f) : 0f;
                mouth.localScale = Vector3.Scale(mouthRestScale, new Vector3(1f + 0.25f * happyWeight, 1f + 0.2f * happyWeight + 0.45f * speak, 1f));
            }
        }

        void UpdateBackpack(float dt)
        {
            if (backpack == null) return;
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

        /// <summary>The scarf tail trails behind movement, bounces on landing and flutters a little in the wind.</summary>
        void UpdateScarf(float dt, Vector3 velocity)
        {
            if (scarf.Length == 0) return;
            Vector3 localAccel = transform.InverseTransformDirection(acceleration);
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            WindManager wind = WindManager.Current;
            float windStrength = wind != null && wind.WindEnabled ? wind.Strength : 0f;
            float flutter = (speed * 1.2f + windStrength * 6f) * Mathf.Sin(Time.time * (7f + speed));
            var target = new Vector2(
                Mathf.Clamp(-localAccel.z * 2.2f - localAccel.y * 0.8f - speed * 3.5f, -maxScarfDegrees, maxScarfDegrees) + flutter,
                Mathf.Clamp(localAccel.x * 2.2f, -maxScarfDegrees, maxScarfDegrees) + flutter * 0.4f);
            scarfVelocity += (target - scarfSway) * (45f * dt);
            scarfVelocity *= Mathf.Exp(-6f * dt);
            scarfSway += scarfVelocity * dt;
            scarfSway = Vector2.ClampMagnitude(scarfSway, maxScarfDegrees);
            for (int i = 0; i < scarf.Length; i++)
            {
                if (scarf[i] == null) continue;
                float share = i == 0 ? 0.6f : 0.5f; // the tip follows the base and adds its own swing
                scarf[i].localRotation = scarfRest[i] * Quaternion.Euler(scarfSway.x * share, 0f, scarfSway.y * share);
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            bool want = false;
            glancing = false;
            float targetWeight = 1f;
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
            else if (idleLookAround && interactor != null && IsIdle)
            {
                // Curious glances: pick a direction every few seconds, hold it, then look ahead again.
                idleTimer += Time.deltaTime;
                glanceTimer -= Time.deltaTime;
                if (glanceTimer <= 0f)
                {
                    bool start = !glancing && idleTimer > 1.5f && Random.value < 0.7f;
                    glanceYaw = start ? Random.Range(-55f, 55f) : 0f;
                    glancePitch = start ? Random.Range(-8f, 12f) : 0f;
                    glanceTimer = start ? Random.Range(1.4f, 2.8f) : Random.Range(2.5f, 6f);
                }
                if (Mathf.Abs(glanceYaw) > 1f)
                {
                    glancing = true;
                    Vector3 dir = Quaternion.Euler(-glancePitch, glanceYaw, 0f) * Vector3.forward;
                    lookPoint = transform.position + Vector3.up * 1.55f + transform.rotation * dir * 5f;
                    want = true;
                    targetWeight = 0.75f;
                }
            }
            else
            {
                idleTimer = 0f;
                glanceYaw = 0f;
            }
            lookWeight = Mathf.MoveTowards(lookWeight, want ? targetWeight : 0f, Time.deltaTime * lookBlendSpeed);
            if (lookWeight <= 0.001f) return;
            animator.SetLookAtWeight(lookWeight, bodyWeight, headWeight, eyesWeight, 0.6f);
            animator.SetLookAtPosition(lookPoint);
        }
    }
}
