using System;
using UnityEngine;

namespace Greenhaven.Weather
{
    /// <summary>
    /// Drives the scene's sun, ambient light, linear fog, sky colour and rain toward a weather state with
    /// smooth transitions. Sunny = the scene's own original settings (captured on enable and restored on
    /// disable, so weather never permanently changes the scene). Lightweight: no post-processing, no
    /// per-frame allocations, one reused rain particle system.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class WeatherManager : MonoBehaviour
    {
        static readonly int WindId = Shader.PropertyToID("_GreenhavenWind");

        [SerializeField, Tooltip("Scene-specific looks for Cloudy/Rainy/Foggy.")] WeatherProfile profile;
        [SerializeField, Tooltip("Defaults to RenderSettings.sun or the first directional light.")] Light sun;
        [SerializeField, Tooltip("Its background is tinted if it clears to a solid colour. Defaults to the main camera.")] Camera skyCamera;
        [SerializeField] RainController rain;

        [Header("State")]
        [SerializeField] WeatherState startWeather = WeatherState.Sunny;
        [SerializeField, Range(0f, 1f)] float intensity = 1f;
        [SerializeField, Min(0f)] float transitionSeconds = 6f;

        [Header("Automatic changes (optional)")]
        [SerializeField] bool automatic;
        [SerializeField, Min(5f)] float minSecondsBetweenChanges = 90f;
        [SerializeField, Min(5f)] float maxSecondsBetweenChanges = 240f;
        [SerializeField] WeatherState[] automaticStates = { WeatherState.Sunny, WeatherState.Rainy, WeatherState.Foggy };

        WeatherSnapshot original, from, to, current;
        FogMode originalFogMode;
        bool captured;
        float transitionTime, transitionLength;
        float nextAutomaticChange;

        public WeatherState CurrentState { get; private set; }
        public float Intensity => intensity;
        /// <summary>0..1 progress of the current transition (1 when settled).</summary>
        public float TransitionProgress => transitionLength <= 0f ? 1f : Mathf.Clamp01(transitionTime / transitionLength);
        public bool IsTransitioning => TransitionProgress < 1f;

        /// <summary>Current (blended) wind level 0..1 from the weather — read by WindManager.</summary>
        public float Wind => current.Wind;

        /// <summary>Raised when a new weather state is requested: (previous, next).</summary>
        public event Action<WeatherState, WeatherState> WeatherChanged;

        void OnEnable()
        {
            if (sun == null) sun = RenderSettings.sun != null ? RenderSettings.sun : FindDirectionalLight();
            if (skyCamera == null) skyCamera = Camera.main;
            original = Capture();
            originalFogMode = RenderSettings.fogMode;
            captured = true;
            current = from = to = original;
            transitionLength = 0f;
            CurrentState = WeatherState.Sunny;
            if (startWeather != WeatherState.Sunny) SetWeather(startWeather, intensity, 0f);
            ScheduleAutomatic();
        }

        void OnDisable()
        {
            RestoreOriginal();
        }

        /// <summary>Change the weather. Intensity 0..1 (default: inspector value); duration in seconds (default: inspector value).</summary>
        public void SetWeather(WeatherState state, float? newIntensity = null, float? seconds = null)
        {
            if (!captured) return;
            if (newIntensity.HasValue) intensity = Mathf.Clamp01(newIntensity.Value);
            WeatherState previous = CurrentState;
            CurrentState = state;

            from = current;
            to = TargetFor(state);
            transitionLength = Mathf.Max(0f, seconds ?? transitionSeconds);
            transitionTime = 0f;
            if (transitionLength <= 0f) Apply(to);
            WeatherChanged?.Invoke(previous, state);
        }

        /// <summary>Immediately puts every setting back exactly as the scene had it.</summary>
        public void RestoreOriginal()
        {
            if (!captured) return;
            Apply(original);
            RenderSettings.fogMode = originalFogMode;
            current = from = to = original;
            transitionLength = 0f;
            CurrentState = WeatherState.Sunny;
        }

        WeatherSnapshot TargetFor(WeatherState state)
        {
            if (state == WeatherState.Sunny) return original;
            if (profile != null && profile.TryGet(state, out WeatherPreset preset)) return preset.Resolve(original, intensity);
            Debug.LogWarning($"{nameof(WeatherManager)}: no preset for {state} in the weather profile; staying sunny.", this);
            return original;
        }

        void Update()
        {
            if (transitionLength > 0f && transitionTime < transitionLength)
            {
                transitionTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, TransitionProgress);
                Apply(WeatherSnapshot.Lerp(from, to, t));
                if (!IsTransitioning && CurrentState == WeatherState.Sunny) RenderSettings.fogMode = originalFogMode;
            }

            if (automatic && Time.time >= nextAutomaticChange && !IsTransitioning && automaticStates.Length > 0)
            {
                WeatherState next = automaticStates[UnityEngine.Random.Range(0, automaticStates.Length)];
                if (next != CurrentState || automaticStates.Length == 1) SetWeather(next);
                ScheduleAutomatic();
            }
        }

        void ScheduleAutomatic() =>
            nextAutomaticChange = Time.time + UnityEngine.Random.Range(minSecondsBetweenChanges, Mathf.Max(minSecondsBetweenChanges, maxSecondsBetweenChanges));

        WeatherSnapshot Capture()
        {
            return new WeatherSnapshot
            {
                SunIntensity = sun != null ? sun.intensity : 1f,
                SunColor = sun != null ? sun.color : Color.white,
                AmbientSky = RenderSettings.ambientSkyColor,
                AmbientEquator = RenderSettings.ambientEquatorColor,
                AmbientGround = RenderSettings.ambientGroundColor,
                FogEnabled = RenderSettings.fog,
                FogColor = RenderSettings.fogColor,
                FogStart = RenderSettings.fogStartDistance,
                FogEnd = RenderSettings.fogEndDistance,
                Background = skyCamera != null ? skyCamera.backgroundColor : RenderSettings.fogColor,
                Rain = 0f,
                Wind = 0f,
            };
        }

        void Apply(WeatherSnapshot s)
        {
            current = s;
            if (sun != null)
            {
                sun.intensity = s.SunIntensity;
                sun.color = s.SunColor;
            }
            RenderSettings.ambientSkyColor = s.AmbientSky;
            RenderSettings.ambientEquatorColor = s.AmbientEquator;
            RenderSettings.ambientGroundColor = s.AmbientGround;
            RenderSettings.fog = s.FogEnabled;
            if (s.FogEnabled) RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = s.FogColor;
            RenderSettings.fogStartDistance = s.FogStart;
            RenderSettings.fogEndDistance = s.FogEnd;
            if (skyCamera != null && skyCamera.clearFlags == CameraClearFlags.SolidColor) skyCamera.backgroundColor = s.Background;
            if (rain != null) rain.SetAmount(s.Rain);
            Shader.SetGlobalFloat(WindId, s.Wind);
        }

        static Light FindDirectionalLight()
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }
    }
}
