using UnityEngine;

namespace Greenhaven.Wildlife
{
    public enum WildlifeState { Idle, Wander, Graze, LookAtPlayer, Investigate, Flee, Return, Perched, Flying }

    /// <summary>
    /// Behaviour of one animal: idle, wander, graze/forage, look at the player, investigate, flee and
    /// return home; birds perch, peck and fly between perches. Decisions run at a distance-based rate
    /// chosen by <see cref="WildlifeManager"/> (which also calls <see cref="Step"/> every frame), so there
    /// is no Update per animal. Movement is kinematic over walkable ground (non-convex mesh colliders and
    /// terrain), validated with a few raycasts when a destination is picked, and kept out of the
    /// manager's blocked cells (trails). Animals have no colliders, so they never push the player.
    /// </summary>
    [RequireComponent(typeof(AnimalMotion)), DisallowMultipleComponent]
    public sealed class WildlifeAgent : MonoBehaviour
    {
        [SerializeField] WildlifeSpecies species;
        [SerializeField, Tooltip("Personality and timing seed (set by the placement tool).")] int seed = 1;
        [SerializeField, Tooltip("Birds: perch points (world space) to fly between.")] Vector3[] perches = new Vector3[0];

        public WildlifeSpecies Species => species;
        public WildlifeState State { get; private set; }
        public float Boldness { get; private set; }
        public int Seed => seed;
        public AnimalMotion Motion => motion;

        internal bool begun;
        internal float nextThink, lastThink;

        AnimalMotion motion;
        WildlifeManager manager;
        System.Random rng;
        Vector3 home, target, lastSafe;
        bool moving;
        float speed, stateTimer, reactTimer, reactionDelay, investigateCooldown, callTimer, groundY;
        Vector3 playerHead;
        bool playerValid;
        int perch;
        Vector3 flyFrom, flyControl, flyTo;
        float flyT, flyDuration;

        static readonly float[] FleeAngles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

        bool IsBird => species != null && species.kind == AnimalKind.Bird;

        /// <summary>Editor setup (placement tool).</summary>
        public void Configure(WildlifeSpecies s, int agentSeed, Vector3[] perchPoints)
        {
            species = s;
            seed = agentSeed;
            perches = perchPoints ?? new Vector3[0];
        }

        void Awake() => motion = GetComponent<AnimalMotion>();

        void OnEnable() => WildlifeManager.Register(this);

        void OnDisable() => WildlifeManager.Unregister(this);

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        float Range(Vector2 r) => Range(r.x, r.y);

        internal void Begin(WildlifeManager m)
        {
            begun = true;
            manager = m;
            rng = new System.Random(seed);
            home = lastSafe = transform.position;
            groundY = home.y;
            if (species == null)
            {
                Debug.LogWarning($"{name} has no WildlifeSpecies.", this);
                return;
            }
            Boldness = Range(species.boldnessRange);
            reactionDelay = Range(0.1f, 0.7f) * (1.2f - Boldness * 0.5f); // not everyone reacts on the same frame
            callTimer = Range(species.callInterval) * Range(0.3f, 1f);
            motion.Init(species, seed);
            if (IsBird)
            {
                if (perches == null || perches.Length == 0) perches = new[] { transform.position };
                State = WildlifeState.Perched;
            }
            else State = WildlifeState.Idle;
            stateTimer = Range(species.idleSeconds) * Range(0.2f, 1.2f);
        }

        // ───────────────────────────── Decisions (staggered) ─────────────────────────────

        internal void Think(float dt, Vector3 playerPosition, bool playerRunning, bool hasPlayer)
        {
            if (species == null) return;
            dt = Mathf.Min(dt, 1f);
            playerValid = hasPlayer;
            playerHead = playerPosition + Vector3.up * 1.5f;
            Vector3 pos = transform.position;
            float d = hasPlayer ? Flat(playerPosition - pos).magnitude : float.PositiveInfinity;
            float notice = species.detectDistance * (playerRunning ? species.runningPlayerMultiplier : 1f);
            float fleeAt = Mathf.Max(species.personalSpace, species.fleeDistance * (1.25f - Boldness * 0.6f)) * (playerRunning ? 1.4f : 1f);

            callTimer -= dt;
            if (callTimer <= 0f)
            {
                manager.RequestCall(this);
                callTimer = Range(species.callInterval);
            }
            investigateCooldown -= dt;

            if (IsBird)
            {
                ThinkBird(dt, d, fleeAt, playerPosition);
                return;
            }

            // Threat first (after a personal reaction delay).
            if (State != WildlifeState.Flee && d < fleeAt)
            {
                reactTimer += dt;
                if (reactTimer >= reactionDelay) StartFlee(playerPosition);
            }
            else if (d > fleeAt * 1.2f) reactTimer = 0f;

            switch (State)
            {
                case WildlifeState.Flee:
                    if (d > species.calmDistance)
                    {
                        Set(WildlifeState.Return, 0f);
                        if (!PickSpot(home, 0f, species.wanderRadius * 0.4f, out target)) target = home;
                        moving = true;
                    }
                    else if (!moving) StartFlee(playerPosition);
                    break;

                case WildlifeState.Idle:
                case WildlifeState.Wander:
                case WildlifeState.Graze:
                case WildlifeState.Return:
                    if (d < notice && d >= fleeAt)
                    {
                        bool investigate = investigateCooldown <= 0f && !playerRunning && rng.NextDouble() < species.curiosity * (0.5f + Boldness);
                        Set(investigate ? WildlifeState.Investigate : WildlifeState.LookAtPlayer, Range(2.5f, 6f));
                        moving = false;
                        break;
                    }
                    stateTimer -= dt;
                    if (State == WildlifeState.Wander || State == WildlifeState.Return)
                    {
                        if (!moving) Set(WildlifeState.Idle, Range(species.idleSeconds));
                    }
                    else if (stateTimer <= 0f) ChooseCalmActivity();
                    break;

                case WildlifeState.LookAtPlayer:
                    stateTimer -= dt;
                    if (d > notice * 1.15f || stateTimer <= 0f)
                    {
                        investigateCooldown = Range(8f, 20f);
                        Set(WildlifeState.Idle, Range(species.idleSeconds));
                    }
                    break;

                case WildlifeState.Investigate:
                    stateTimer -= dt;
                    float stop = species.personalSpace * 1.8f + 0.5f;
                    if (d <= stop + 0.3f || stateTimer <= 0f)
                    {
                        investigateCooldown = Range(15f, 30f);
                        moving = false;
                        Set(WildlifeState.LookAtPlayer, Range(2f, 4f));
                    }
                    else
                    {
                        Vector3 approach = playerPosition + Flat(pos - playerPosition).normalized * stop;
                        if (Usable(approach, out Vector3 ground)) { target = ground; moving = true; }
                    }
                    break;
            }

            if (moving) CheckAhead();
            SampleGround();
        }

        void ChooseCalmActivity()
        {
            double roll = rng.NextDouble();
            if (roll < species.wanderChance && PickSpot(home, 1.5f, species.wanderRadius, out target))
            {
                moving = true;
                Set(WildlifeState.Wander, 0f);
            }
            else if (roll < species.wanderChance + species.grazeChance) Set(WildlifeState.Graze, Range(species.grazeSeconds));
            else Set(WildlifeState.Idle, Range(species.idleSeconds));
        }

        void StartFlee(Vector3 from)
        {
            Vector3 pos = transform.position;
            Vector3 away = Flat(pos - from);
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();
            float distance = species.fleeDistance * 1.8f + 3f;
            // Straight away first, then fanning out; the first usable spot with a clear run wins.
            foreach (float angle in FleeAngles)
            {
                Vector3 dir = Quaternion.Euler(0f, angle + Range(-10f, 10f), 0f) * away;
                if (Usable(pos + dir * distance, out Vector3 spot) && ClearRun(pos, spot))
                {
                    target = spot;
                    moving = true;
                    Set(WildlifeState.Flee, 0f);
                    reactTimer = 0f;
                    return;
                }
            }
            // Cornered: stay alert rather than running into a wall.
            moving = false;
            Set(WildlifeState.LookAtPlayer, 1.5f);
        }

        void Set(WildlifeState state, float seconds)
        {
            State = state;
            stateTimer = seconds;
        }

        // ───────────────────────────── Birds ─────────────────────────────

        void ThinkBird(float dt, float d, float fleeAt, Vector3 playerPosition)
        {
            if (State == WildlifeState.Flying) return;
            stateTimer -= dt;
            if (d < fleeAt * 1.2f)
            {
                reactTimer += dt;
                if (reactTimer >= reactionDelay) TakeOff(playerPosition, true);
                return;
            }
            reactTimer = 0f;
            if (stateTimer > 0f) return;
            double roll = rng.NextDouble();
            if (roll < 0.15 && perches.Length > 1) TakeOff(playerPosition, false);
            else
            {
                State = WildlifeState.Perched;
                motion.Grazing = roll < 0.15 + species.grazeChance; // peck
                stateTimer = Range(1.2f, 3.5f);
            }
        }

        void TakeOff(Vector3 playerPosition, bool scared)
        {
            int next = perch;
            if (perches.Length > 1)
            {
                float best = -1f;
                for (int i = 0; i < perches.Length; i++)
                {
                    if (i == perch) continue;
                    float score = (scared && playerValid ? Flat(perches[i] - playerPosition).magnitude : Range(0f, 10f)) + Range(0f, 2f);
                    if (score > best) { best = score; next = i; }
                }
            }
            flyFrom = transform.position;
            flyTo = perches[next];
            Vector3 mid = (flyFrom + flyTo) * 0.5f;
            float span = Flat(flyTo - flyFrom).magnitude;
            Vector3 side = next == perch ? Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * 4f : Vector3.zero; // loop back to the same perch
            flyControl = mid + side + Vector3.up * (species.flightHeight + span * 0.15f);
            flyDuration = Mathf.Max(1.2f, (span + species.flightHeight * 2f) / species.flightSpeed);
            flyT = 0f;
            perch = next;
            State = WildlifeState.Flying;
            motion.Grazing = false;
            reactTimer = 0f;
        }

        // ───────────────────────────── Movement (every frame) ─────────────────────────────

        internal void Step(float dt)
        {
            if (species == null) return;
            if (IsBird)
            {
                StepBird(dt);
                return;
            }

            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            float desired = 0f;
            if (moving)
            {
                Vector3 to = Flat(target - pos);
                float dist = to.magnitude;
                if (dist < 0.35f) moving = false;
                else
                {
                    desired = State == WildlifeState.Flee ? species.fleeSpeed
                        : State == WildlifeState.Investigate ? species.walkSpeed * 0.7f
                        : State == WildlifeState.Return && dist > 6f ? species.trotSpeed
                        : species.walkSpeed;
                    Quaternion face = Quaternion.LookRotation(to / dist, Vector3.up);
                    float angle = Quaternion.Angle(rot, face);
                    rot = Quaternion.RotateTowards(rot, face, species.turnSpeed * dt);
                    if (angle > 70f) desired *= 0.25f; // turn mostly in place
                    desired *= Mathf.Clamp(dist / 1.2f, 0.3f, 1f); // ease into the stop
                }
            }
            else if (playerValid && (State == WildlifeState.LookAtPlayer || State == WildlifeState.Investigate))
            {
                // Turn the body partly toward the player; the head does the rest.
                Vector3 to = Flat(playerHead - pos);
                if (to.sqrMagnitude > 0.01f)
                {
                    Quaternion face = Quaternion.LookRotation(to, Vector3.up);
                    if (Quaternion.Angle(rot, face) > 60f) rot = Quaternion.RotateTowards(rot, face, species.turnSpeed * 0.4f * dt);
                }
            }

            speed = Mathf.MoveTowards(speed, desired, species.acceleration * dt * (desired < speed ? 1.5f : 1f));
            pos += rot * Vector3.forward * (speed * dt);
            pos.y = Mathf.Lerp(pos.y, groundY, 1f - Mathf.Exp(-12f * dt));
            transform.SetPositionAndRotation(pos, rot);

            motion.Speed = speed;
            motion.Grazing = State == WildlifeState.Graze;
            motion.Fleeing = State == WildlifeState.Flee;
            motion.Alert = State == WildlifeState.LookAtPlayer || State == WildlifeState.Investigate || State == WildlifeState.Flee;
            motion.HasLookTarget = playerValid && (State == WildlifeState.LookAtPlayer || State == WildlifeState.Investigate);
            motion.LookTarget = playerHead;
            motion.Flying = false;
        }

        void StepBird(float dt)
        {
            if (State == WildlifeState.Flying)
            {
                flyT = Mathf.Min(1f, flyT + dt / flyDuration);
                float s = Mathf.SmoothStep(0f, 1f, flyT);
                Vector3 p = Bezier(s);
                Vector3 ahead = Bezier(Mathf.Min(1f, s + 0.05f)) - p;
                Quaternion rot = transform.rotation;
                if (Flat(ahead).sqrMagnitude > 1e-5f)
                    rot = Quaternion.Slerp(rot, Quaternion.LookRotation(Flat(ahead), Vector3.up), 1f - Mathf.Exp(-8f * dt));
                transform.SetPositionAndRotation(p, rot);
                motion.Flying = flyT < 0.93f;
                if (flyT >= 1f)
                {
                    State = WildlifeState.Perched;
                    stateTimer = Range(2f, 5f);
                }
            }
            motion.Speed = 0f;
            motion.Alert = reactTimer > 0f;
            motion.HasLookTarget = playerValid && reactTimer > 0f;
            motion.LookTarget = playerHead;
        }

        Vector3 Bezier(float s) => (1f - s) * (1f - s) * flyFrom + 2f * (1f - s) * s * flyControl + s * s * flyTo;

        // ───────────────────────────── Ground and obstacles ─────────────────────────────

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Ground under <paramref name="p"/> if it is walkable, not too steep, not blocked and free of obstacles.</summary>
        bool Usable(Vector3 p, out Vector3 ground)
        {
            ground = p;
            if (manager.IsBlocked(p)) return false;
            if (!Physics.Raycast(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, out RaycastHit hit, 14f, manager.GroundMask, QueryTriggerInteraction.Ignore)) return false;
            if (!WildlifeManager.IsWalkable(hit.collider) || Vector3.Angle(hit.normal, Vector3.up) > species.maxSlope) return false;
            ground = hit.point;
            return !manager.Obstructed(ground, species.bodyRadius);
        }

        bool ClearRun(Vector3 from, Vector3 to)
        {
            float r = species.bodyRadius;
            Vector3 a = from + Vector3.up * (r + 0.2f), b = to + Vector3.up * (r + 0.2f);
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return true;
            return !Physics.SphereCast(a, r, dir / len, out RaycastHit hit, len, manager.ObstacleMask, QueryTriggerInteraction.Ignore) || WildlifeManager.IsWalkable(hit.collider);
        }

        bool PickSpot(Vector3 around, float minR, float maxR, out Vector3 spot)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = Range(0f, Mathf.PI * 2f), r = Range(minR, maxR);
                Vector3 p = around + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (Usable(p, out spot) && ClearRun(transform.position, spot)) return true;
            }
            spot = around;
            return false;
        }

        void CheckAhead()
        {
            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;
            float r = species.bodyRadius;
            float look = Mathf.Max(0.6f, speed * 0.45f + r);
            bool blocked = Physics.SphereCast(pos + Vector3.up * (r + 0.2f), r * 0.9f, fwd, out RaycastHit hit, look, manager.ObstacleMask, QueryTriggerInteraction.Ignore)
                           && !WildlifeManager.IsWalkable(hit.collider);
            Vector3 ahead = pos + fwd * look;
            if (!blocked && manager.IsBlocked(ahead) && State != WildlifeState.Flee) blocked = true; // keep off trails unless fleeing
            if (!blocked) return;
            moving = false;
            speed *= 0.3f;
            if (State == WildlifeState.Flee) StartFlee(playerHead);
            else Set(WildlifeState.Idle, Range(species.idleSeconds) * 0.5f);
        }

        void SampleGround()
        {
            Vector3 pos = transform.position;
            Vector3 probe = pos + transform.forward * Mathf.Min(0.5f, speed * 0.1f);
            if (Physics.Raycast(new Vector3(probe.x, pos.y + 2f, probe.z), Vector3.down, out RaycastHit hit, 6f, manager.GroundMask, QueryTriggerInteraction.Ignore)
                && WildlifeManager.IsWalkable(hit.collider) && Vector3.Angle(hit.normal, Vector3.up) <= species.maxSlope + 8f)
            {
                groundY = hit.point.y;
                lastSafe = pos;
            }
            else if (moving)
            {
                // Edge of walkable ground (cliff, water, rock): back off to the last safe spot.
                target = lastSafe;
                if (State != WildlifeState.Flee) Set(WildlifeState.Return, 0f);
            }
        }
    }
}
