using Greenhaven.Weather;
using UnityEngine;

namespace Greenhaven.Wind
{
    public enum WindPreset { Calm, Breeze, Strong }

    /// <summary>Visual cost of wind: High = all motion, Low = no leaf flutter and less cross-wind motion, Off = still.</summary>
    public enum WindQuality { Off, Low, High }

    /// <summary>
    /// The one place wind is simulated. Each frame it smooths strength toward base + gusts (seeded Perlin
    /// noise), advances a phase, lets the direction wander slightly, and publishes everything as global
    /// shader values for Greenhaven/WindLit. Vegetation needs no scripts of its own; lightweight C#
    /// components can read <see cref="Current"/> or call <see cref="Sway"/>.
    /// If a <see cref="WeatherManager"/> is present, its wind level raises the base strength smoothly.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class WindManager : MonoBehaviour
    {
        static readonly int DirectionId = Shader.PropertyToID("_GH_WindDirection");
        static readonly int ParamsId = Shader.PropertyToID("_GH_WindParams");
        static readonly int QualityId = Shader.PropertyToID("_GH_WindQuality");

        public static WindManager Current { get; private set; }

        [SerializeField] bool windEnabled = true;
        [SerializeField, Range(0f, 360f), Tooltip("Direction the wind blows towards (degrees, 0 = +Z).")] float directionDegrees = 60f;
        [SerializeField, Range(0f, 1f)] float baseStrength = 0.3f;
        [SerializeField, Range(0.1f, 4f), Tooltip("How fast plants sway.")] float speed = 1f;

        [Header("Gusts")]
        [SerializeField, Range(0f, 1f)] float gustStrength = 0.35f;
        [SerializeField, Range(0.01f, 1f), Tooltip("Gust variation rate (higher = more frequent gusts).")] float gustFrequency = 0.15f;
        [SerializeField, Range(0.2f, 8f), Tooltip("Seconds a gust takes to build and fade (smooth bend and recovery).")] float gustDuration = 2.5f;
        [SerializeField, Range(0f, 45f)] float directionWander = 12f;
        [SerializeField] int seed = 1234;
        [SerializeField, Min(0.05f), Tooltip("Seconds to settle when strength changes.")] float responseSeconds = 1.5f;

        [Header("Quality")]
        [SerializeField, Tooltip("Low suits entry-level phones (no leaf flutter, less cross-wind motion).")] WindQuality quality = WindQuality.High;
        [SerializeField, Range(0f, 1f), Tooltip("Scales visible wind motion. 0 = still vegetation (cheapest test of the effect).")]
        float effectScale = 1f;

        [Header("Weather (optional)")]
        [SerializeField, Tooltip("Defaults to the scene's WeatherManager, if any.")] WeatherManager weather;
        [SerializeField] bool followWeather = true;

        float phase, strength, gust, seedOffset, manualGustTime;

        public bool WindEnabled => windEnabled;
        public Vector3 Direction { get; private set; } = Vector3.forward;
        /// <summary>Current strength 0..1 including gusts (before <see cref="effectScale"/>).</summary>
        public float Strength => strength;
        public float Gust => gust;
        public float Phase => phase;
        public float BaseStrength => baseStrength;
        public float DirectionDegrees => directionDegrees;

        void OnEnable()
        {
            Current = this;
            seedOffset = (Mathf.Abs(seed) % 1000) * 1.37f;
            if (weather == null) weather = FindAnyObjectByType<WeatherManager>();
            strength = baseStrength;
            UpdateDirection(Time.time);
            Publish();
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            Shader.SetGlobalVector(ParamsId, Vector4.zero); // vegetation returns to its rest pose
            Shader.SetGlobalVector(QualityId, Vector4.zero);
        }

        public void SetEnabled(bool on) => windEnabled = on;
        public void SetDirection(float degrees) => directionDegrees = Mathf.Repeat(degrees, 360f);
        public void SetBaseStrength(float value) => baseStrength = Mathf.Clamp01(value);
        public void SetEffectScale(float value) => effectScale = Mathf.Clamp01(value);
        public void SetQuality(WindQuality value) => quality = value;
        public WindQuality Quality => quality;

        public void ApplyPreset(WindPreset preset)
        {
            switch (preset)
            {
                case WindPreset.Calm: baseStrength = 0.05f; gustStrength = 0.05f; speed = 0.7f; break;
                case WindPreset.Breeze: baseStrength = 0.3f; gustStrength = 0.35f; speed = 1f; break;
                case WindPreset.Strong: baseStrength = 0.7f; gustStrength = 0.3f; speed = 1.6f; break;
            }
        }

        /// <summary>Forces a full gust for <paramref name="seconds"/> (testing, story moments).</summary>
        public void TriggerGust(float seconds = 2.5f) => manualGustTime = Mathf.Max(manualGustTime, seconds);

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            // Occasional gusts from seeded noise, plus any manual gust.
            float noise = Mathf.PerlinNoise(t * gustFrequency + seedOffset, seedOffset * 0.5f);
            float targetGust = Mathf.Clamp01((noise - 0.45f) / 0.55f);
            targetGust *= targetGust;
            if (manualGustTime > 0f)
            {
                manualGustTime -= dt;
                targetGust = 1f;
            }

            float targetBase = baseStrength;
            if (followWeather && weather != null && weather.isActiveAndEnabled) targetBase = Mathf.Max(baseStrength, weather.Wind);

            // Base strength follows responseSeconds; gusts build and fade over gustDuration, so trees bend and
            // recover smoothly instead of snapping.
            float k = 1f - Mathf.Exp(-dt / responseSeconds);
            float kGust = 1f - Mathf.Exp(-dt / (gustDuration * 0.5f));
            gust += (targetGust - gust) * kGust;
            float smoothedTarget = windEnabled ? Mathf.Clamp01(targetBase + gust * gustStrength) : 0f;
            strength += (smoothedTarget - strength) * k;
            phase += dt * speed * (0.6f + strength); // stronger wind sways a little faster, with no phase jumps
            UpdateDirection(t);
            Publish();
        }

        void UpdateDirection(float t)
        {
            float wander = (Mathf.PerlinNoise(seedOffset, t * 0.05f) - 0.5f) * 2f * directionWander;
            float yaw = (directionDegrees + wander) * Mathf.Deg2Rad;
            Direction = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        }

        void Publish()
        {
            float scale = quality == WindQuality.Off ? 0f : quality == WindQuality.Low ? effectScale * 0.7f : effectScale;
            Shader.SetGlobalVector(DirectionId, Direction);
            Shader.SetGlobalVector(ParamsId, new Vector4(strength * scale, phase, gust, windEnabled && quality != WindQuality.Off ? 1f : 0f));
            // x = leaf flutter scale, y = cross-wind (secondary) motion scale.
            Shader.SetGlobalVector(QualityId, quality == WindQuality.High ? new Vector4(1f, 1f, 0f, 0f) : new Vector4(0f, 0.5f, 0f, 0f));
        }

        /// <summary>
        /// The same sway curve the shader uses, for lightweight C# components (signs, banners): returns a
        /// signed sway value (roughly −0.1..1.2) at a world position, already scaled by current strength.
        /// </summary>
        public float Sway(Vector3 worldPosition, float frequency = 1f)
        {
            if (!windEnabled) return 0f;
            float p = phase * frequency + worldPosition.x * 0.13f + worldPosition.z * 0.09f;
            float sway = Mathf.Sin(p) * 0.55f + Mathf.Sin(p * 2.3f + 1.7f) * 0.2f + 0.45f;
            return sway * strength * effectScale;
        }
    }
}
