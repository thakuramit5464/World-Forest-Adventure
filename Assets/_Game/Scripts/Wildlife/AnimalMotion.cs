using UnityEngine;

namespace Greenhaven.Wildlife
{
    /// <summary>
    /// Procedural animation for generated animals: gait-blended leg swing (walk/trot/gallop) whose cycle
    /// rate follows the actual ground speed (so feet do not slide), body bob and breathing, look-around
    /// and look-at head turns, grazing, ear flicks, tail sway, rabbit hops, and bird flapping, perching
    /// and pecking. Only bones below the root move; the root transform (position, collisions) belongs to
    /// <see cref="WildlifeAgent"/>. Driven once per frame by <see cref="WildlifeManager"/> while visible;
    /// no Update of its own and no per-frame allocations.
    /// </summary>
    [RequireComponent(typeof(AnimalRig))]
    public sealed class AnimalMotion : MonoBehaviour
    {
        // Inputs, set by the agent every frame.
        [System.NonSerialized] public float Speed;
        [System.NonSerialized] public bool Grazing, Alert, Fleeing, Flying;
        [System.NonSerialized] public bool HasLookTarget;
        [System.NonSerialized] public Vector3 LookTarget;

        // Leg phase offsets per gait: front-left, front-right, hind-left, hind-right.
        static readonly float[] WalkOffsets = { 0.25f, 0.75f, 0f, 0.5f };
        static readonly float[] TrotOffsets = { 0f, 0.5f, 0.5f, 0f };
        static readonly float[] RunOffsets = { 0f, 0.12f, 0.5f, 0.62f };
        const float Tau = Mathf.PI * 2f;

        AnimalRig rig;
        WildlifeSpecies species;
        System.Random rng;
        bool ready;

        Vector3 bodyRestPosition;
        Quaternion bodyRest, neckRest, headRest;
        Quaternion[] earRest, tailRest, legRest, wingRest;

        float phase, walkW = 1f, trotW, runW, moveW, grazeW, alertW, fleeW, flyW;
        float yaw, pitch, yawVelocity, pitchVelocity, idleYaw, idleTimer;
        float flickTimer, flick, flutterTimer, flutter, timeOffset;
        int flickEar;

        public AnimalRig Rig => rig;

        public void Init(WildlifeSpecies s, int seed)
        {
            rig = GetComponent<AnimalRig>();
            species = s;
            rng = new System.Random(seed * 7919 + 17);
            timeOffset = (float)rng.NextDouble() * 20f;
            idleTimer = Range(0.5f, 3f);
            flickTimer = Range(2f, 6f);
            flutterTimer = Range(3f, 9f);
            if (rig.body != null)
            {
                bodyRestPosition = rig.body.localPosition;
                bodyRest = rig.body.localRotation;
            }
            if (rig.neck != null) neckRest = rig.neck.localRotation;
            if (rig.head != null) headRest = rig.head.localRotation;
            earRest = Capture(rig.ears);
            tailRest = Capture(rig.tail);
            legRest = Capture(rig.legs);
            wingRest = Capture(rig.wings);
            ready = rig.body != null && species != null;
        }

        static Quaternion[] Capture(Transform[] bones)
        {
            var rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) rest[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
            return rest;
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static float Approach(float value, float target, float rate) => Mathf.MoveTowards(value, target, rate);

        /// <param name="windStrength">0..1 from the shared WindManager (0 when wind is off).</param>
        /// <param name="secondary">False on the Low wildlife quality: skips ears, tail and wind detail.</param>
        public void Animate(float dt, float time, float windStrength, bool secondary)
        {
            if (!ready || dt <= 0f) return;
            float t = time + timeOffset;

            // Smooth state weights so every change blends instead of popping.
            int gait = Speed > (species.trotSpeed + species.fleeSpeed) * 0.5f ? 2 : Speed > (species.walkSpeed + species.trotSpeed) * 0.5f ? 1 : 0;
            walkW = Approach(walkW, gait == 0 ? 1f : 0f, dt * 3f);
            trotW = Approach(trotW, gait == 1 ? 1f : 0f, dt * 3f);
            runW = Approach(runW, gait == 2 ? 1f : 0f, dt * 3f);
            float sum = Mathf.Max(0.001f, walkW + trotW + runW);
            float w0 = walkW / sum, w1 = trotW / sum, w2 = runW / sum;
            moveW = Approach(moveW, Speed > 0.05f ? 1f : 0f, dt * 4f);
            grazeW = Approach(grazeW, Grazing ? 1f : 0f, dt * 1.6f);
            alertW = Approach(alertW, Alert ? 1f : 0f, dt * 4f);
            fleeW = Approach(fleeW, Fleeing ? 1f : 0f, dt * 3f);
            flyW = Approach(flyW, Flying ? 1f : 0f, dt * 5f);

            float stride = species.strideLength * (1f + 0.5f * w1 + 1.2f * w2);
            phase = Mathf.Repeat(phase + Speed / stride * dt, 1f);

            switch (species.kind)
            {
                case AnimalKind.Bird: Bird(dt, t); break;
                case AnimalKind.Rabbit: Rabbit(t); break;
                default: Quadruped(t, w0, w1, w2); break;
            }
            Head(dt, t);
            if (secondary)
            {
                Ears(dt, t, windStrength);
                Tail(t, windStrength);
            }
        }

        void Quadruped(float t, float w0, float w1, float w2)
        {
            float amp = species.legSwingDegrees * Mathf.Clamp01(Speed / Mathf.Max(0.05f, species.walkSpeed * 0.7f)) * (1f + 0.35f * w2);
            for (int i = 0; i < rig.legs.Length && i < 4; i++)
            {
                if (rig.legs[i] == null) continue;
                float swing = w0 * Mathf.Sin(Tau * (phase + WalkOffsets[i])) + w1 * Mathf.Sin(Tau * (phase + TrotOffsets[i])) + w2 * Mathf.Sin(Tau * (phase + RunOffsets[i]));
                rig.legs[i].localRotation = legRest[i] * Quaternion.Euler(swing * amp, 0f, 0f);
            }
            float bob = species.bodyBob * moveW * (0.5f + 0.5f * Mathf.Cos(Tau * phase * (w2 > 0.5f ? 1f : 2f))) * (1f + w2);
            float breath = species.bodyRadius * 0.025f * Mathf.Sin(t * 2.1f) * (1f - moveW);
            rig.body.localPosition = bodyRestPosition + Vector3.up * (bob + breath - grazeW * species.bodyRadius * 0.08f);
            float pitchBody = w2 * moveW * 4f * Mathf.Sin(Tau * phase) + grazeW * 4f;
            rig.body.localRotation = bodyRest * Quaternion.Euler(pitchBody, 0f, 0f);
        }

        void Rabbit(float t)
        {
            float c = phase;
            float air = c < 0.6f ? Mathf.Sin(Mathf.PI * c / 0.6f) : 0f;
            float push = Mathf.Sin(Mathf.PI * Mathf.Clamp01(c / 0.35f));
            for (int i = 0; i < rig.legs.Length && i < 4; i++)
            {
                if (rig.legs[i] == null) continue;
                float angle = i < 2 ? -35f * air : 45f * push;
                rig.legs[i].localRotation = legRest[i] * Quaternion.Euler(angle * moveW, 0f, 0f);
            }
            float breath = 0.003f * Mathf.Sin(t * 5f) * (1f - moveW);
            rig.body.localPosition = bodyRestPosition + Vector3.up * (species.hopHeight * air * moveW + breath);
            rig.body.localRotation = bodyRest * Quaternion.Euler(-12f * Mathf.Sin(Tau * c) * moveW, 0f, 0f);
        }

        void Bird(float dt, float t)
        {
            // Occasional wing flutter while perched.
            flutterTimer -= dt;
            if (flutterTimer <= 0f)
            {
                flutter = 1f;
                flutterTimer = Range(4f, 10f);
            }
            flutter = Mathf.MoveTowards(flutter, 0f, dt * 2.5f);
            float beat = Mathf.Sin(Tau * species.flapRate * t);
            float open = flyW * (50f * beat + 18f) + (1f - flyW) * flutter * (25f + 20f * Mathf.Sin(Tau * 14f * t));
            for (int i = 0; i < rig.wings.Length; i++)
                if (rig.wings[i] != null) rig.wings[i].localRotation = wingRest[i] * Quaternion.Euler(0f, 0f, i == 0 ? -open : open);
            for (int i = 0; i < rig.legs.Length; i++)
                if (rig.legs[i] != null) rig.legs[i].localRotation = legRest[i] * Quaternion.Euler(65f * flyW, 0f, 0f);
            float hop = (1f - flyW) * moveW * 0.025f * Mathf.Abs(Mathf.Sin(Mathf.PI * phase));
            rig.body.localPosition = bodyRestPosition + Vector3.up * (hop + flyW * 0.01f * beat);
            rig.body.localRotation = bodyRest * Quaternion.Euler(-8f * flyW, 0f, 0f);
        }

        void Head(float dt, float t)
        {
            if (rig.head == null) return;
            bool bird = species.kind == AnimalKind.Bird;
            float targetYaw = 0f, targetPitch = 0f;
            if (HasLookTarget)
            {
                Vector3 d = transform.InverseTransformPoint(LookTarget) - transform.InverseTransformPoint(rig.head.position);
                targetYaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -75f, 75f);
                targetPitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -30f, 35f);
            }
            else if (moveW < 0.5f || bird)
            {
                idleTimer -= dt;
                if (idleTimer <= 0f)
                {
                    idleYaw = rng.NextDouble() < 0.3 ? 0f : Range(-45f, 45f);
                    idleTimer = bird ? Range(0.4f, 1.6f) : Range(1.5f, 4.5f);
                }
                targetYaw = idleYaw * (1f - moveW);
                targetPitch = 4f * Mathf.Sin(t * 0.7f);
            }
            float lookYaw = targetYaw * (1f - grazeW * 0.7f);
            float lookPitch = targetPitch + runW * moveW * 8f;
            if (species.kind == AnimalKind.Rabbit && moveW < 0.5f && Mathf.Repeat(t * 0.6f, 3f) < 1f) lookPitch += 2f * Mathf.Sin(t * 30f); // nose twitch
            float graze = grazeW * (bird ? species.grazeHeadPitch * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 7f)), 4f) : species.grazeHeadPitch + 3f * Mathf.Sin(t * 6f));

            float smooth = bird ? 0.06f : 0.28f;
            yaw = Mathf.SmoothDampAngle(yaw, lookYaw, ref yawVelocity, smooth, Mathf.Infinity, dt);
            pitch = Mathf.SmoothDampAngle(pitch, lookPitch, ref pitchVelocity, smooth, Mathf.Infinity, dt);
            if (rig.neck != null)
            {
                rig.neck.localRotation = neckRest * Quaternion.Euler(pitch * 0.5f + graze * 0.65f, yaw * 0.5f, 0f);
                rig.head.localRotation = headRest * Quaternion.Euler(pitch * 0.5f + graze * 0.35f, yaw * 0.5f, 0f);
            }
            else rig.head.localRotation = headRest * Quaternion.Euler(pitch + graze, yaw, 0f);
        }

        void Ears(float dt, float t, float wind)
        {
            if (rig.ears.Length == 0) return;
            flickTimer -= dt;
            if (flickTimer <= 0f)
            {
                flick = -28f;
                flickEar = rng.Next(rig.ears.Length);
                flickTimer = Range(2.5f, 8f);
            }
            flick = Mathf.MoveTowards(flick, 0f, dt * 140f);
            for (int i = 0; i < rig.ears.Length; i++)
            {
                if (rig.ears[i] == null) continue;
                float side = i == 0 ? -1f : 1f;
                float tilt = alertW * 8f - (1f - alertW) * 10f - fleeW * 30f + (i == flickEar ? flick : 0f) + wind * 3f * Mathf.Sin(t * 9f + i * 1.7f);
                rig.ears[i].localRotation = earRest[i] * Quaternion.Euler(tilt, side * alertW * -6f, side * fleeW * 12f);
            }
        }

        void Tail(float t, float wind)
        {
            int n = rig.tail.Length;
            if (n == 0) return;
            float lift = species.kind == AnimalKind.Deer ? fleeW * 75f : runW * moveW * 12f + fleeW * 10f;
            for (int k = 0; k < n; k++)
            {
                if (rig.tail[k] == null) continue;
                float lag = k * 0.6f;
                float sway = moveW * (7f + 9f * runW) * Mathf.Sin(Tau * phase + lag) + (1f - moveW) * 6f * Mathf.Sin(t * 1.3f - lag)
                             + wind * 4f * Mathf.Sin(t * 3f + k);
                float raise = (k == 0 ? lift : lift * 0.2f) + (species.kind == AnimalKind.Bird ? (1f - flyW) * 8f * Mathf.Max(0f, Mathf.Sin(t * 5f)) : 0f);
                rig.tail[k].localRotation = tailRest[k] * Quaternion.Euler(raise, sway, 0f);
            }
        }
    }
}
