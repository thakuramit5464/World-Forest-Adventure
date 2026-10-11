using UnityEngine;

namespace Greenhaven.Weather
{
    /// <summary>
    /// One reusable rain ParticleSystem that follows the camera (or another target). Drops spawn in a
    /// box above the target and fall in world space, so the shower stays around the player without
    /// sliding with the camera. Emission and optional audio scale with <see cref="Amount"/> (0..1).
    /// Created once; never destroyed or re-created while weather changes.
    /// </summary>
    public sealed class RainController : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField, Tooltip("Defaults to the main camera.")] Transform target;
        [SerializeField, Min(1f), Tooltip("Half-width of the square rain area around the target (m).")] float radius = 10f;
        [SerializeField, Min(1f), Tooltip("Height above the target where drops spawn (m).")] float spawnHeight = 8f;

        [Header("Drops (conservative defaults for entry-level phones)")]
        [SerializeField, Min(10)] int maxParticles = 600;
        [SerializeField, Min(1f), Tooltip("Drops per second at full rain.")] float maxEmission = 450f;
        [SerializeField, Min(1f)] float fallSpeed = 14f;
        [SerializeField, Range(0f, 5f), Tooltip("Sideways drift (m/s) for a slight slant.")] float drift = 1.2f;
        [SerializeField, Min(0.005f)] float dropWidth = 0.025f;
        [SerializeField] Color dropColor = new Color(0.78f, 0.84f, 0.95f, 0.45f);
        [SerializeField, Tooltip("URP Particles/Unlit material (created by Greenhaven ▸ Weather ▸ Add Weather To Open Scene).")]
        Material dropMaterial;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Optional looping rain clip. Leave empty for silent rain.")] AudioSource rainAudio;
        [SerializeField, Range(0f, 1f)] float maxVolume = 0.6f;

        ParticleSystem rain;
        ParticleSystem.EmissionModule emission;
        float amount;

        public float Amount => amount;

        void Awake()
        {
            if (target == null && Camera.main != null) target = Camera.main.transform;
            rain = GetComponentInChildren<ParticleSystem>();
            if (rain == null) rain = new GameObject("RainParticles").AddComponent<ParticleSystem>();
            rain.transform.SetParent(transform, false);
            Configure(rain);
            emission = rain.emission;
            if (rainAudio != null)
            {
                rainAudio.loop = true;
                rainAudio.playOnAwake = false;
                rainAudio.volume = 0f;
            }
            SetAmount(amount); // keep any amount requested before Awake (the manager may run first)
        }

        /// <summary>Applies the inspector settings to the particle system (also used by editor setup).</summary>
        public void Configure(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = maxParticles;
            main.startLifetime = (spawnHeight + 2f) / fallSpeed; // long enough to reach just below the player
            main.startSpeed = 0f;
            main.startSize = dropWidth;
            main.startColor = dropColor;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 2f, 0.1f, radius * 2f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(drift * 0.6f, drift);
            velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed * 1.1f, -fallSpeed * 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f; // ~0.5 m streaks at full speed
            r.lengthScale = 0f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.05f; // never let a drop fill the screen
            if (dropMaterial != null) r.sharedMaterial = dropMaterial;
            else Debug.LogWarning($"{nameof(RainController)}: no drop material assigned; rain may render pink. Run Greenhaven ▸ Weather ▸ Add Weather To Open Scene.", this);

            ParticleSystem.EmissionModule e = ps.emission;
            e.enabled = true;
            e.rateOverTime = 0f;
        }

        /// <summary>Sets rain strength (0..1): emission and audio volume follow.</summary>
        public void SetAmount(float value)
        {
            amount = Mathf.Clamp01(value);
            if (rain == null) return;
            emission.rateOverTime = maxEmission * amount;
            if (amount > 0.001f)
            {
                if (!rain.isPlaying) rain.Play();
            }
            else if (rain.isPlaying)
            {
                rain.Stop(true, ParticleSystemStopBehavior.StopEmitting); // let falling drops finish
            }

            if (rainAudio != null && rainAudio.clip != null)
            {
                rainAudio.volume = maxVolume * amount;
                if (amount > 0.001f && !rainAudio.isPlaying) rainAudio.Play();
                else if (amount <= 0.001f && rainAudio.isPlaying) rainAudio.Stop();
            }
        }

        void LateUpdate()
        {
            if (target == null || amount <= 0.001f) return;
            // Follow horizontally and stay above; drops already in the air keep falling in world space.
            Vector3 p = target.position;
            transform.position = new Vector3(p.x, p.y + spawnHeight, p.z);
        }
    }
}
