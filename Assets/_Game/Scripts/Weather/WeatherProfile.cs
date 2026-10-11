using System;
using UnityEngine;

namespace Greenhaven.Weather
{
    /// <summary>
    /// Per-scene weather configuration: how each non-sunny state looks. Assign a different profile to a
    /// scene's WeatherManager for scene-specific weather (e.g. a mistier forest).
    /// </summary>
    [CreateAssetMenu(menuName = "Greenhaven/Weather Profile", fileName = "WeatherProfile")]
    public sealed class WeatherProfile : ScriptableObject
    {
        [SerializeField] WeatherPreset[] presets = Array.Empty<WeatherPreset>();

        public bool TryGet(WeatherState state, out WeatherPreset preset)
        {
            foreach (WeatherPreset p in presets)
            {
                if (p != null && p.State == state)
                {
                    preset = p;
                    return true;
                }
            }
            preset = null;
            return false;
        }

        /// <summary>Storybook defaults for Cloudy, Rainy and Foggy (Sunny uses the scene's own lighting).</summary>
        public void ResetToDefaults()
        {
            presets = new[]
            {
                new WeatherPreset
                {
                    State = WeatherState.Rainy, SunIntensityScale = 0.35f, SunTint = new Color(0.75f, 0.8f, 0.9f),
                    AmbientSky = new Color(0.38f, 0.43f, 0.52f), AmbientEquator = new Color(0.36f, 0.4f, 0.42f), AmbientGround = new Color(0.22f, 0.22f, 0.22f),
                    FogColor = new Color(0.55f, 0.6f, 0.66f), FogStart = 12f, FogEnd = 65f, Rain = 1f, Wind = 0.8f,
                },
                new WeatherPreset
                {
                    State = WeatherState.Foggy, SunIntensityScale = 0.55f, SunTint = new Color(0.95f, 0.95f, 1f),
                    AmbientSky = new Color(0.6f, 0.64f, 0.7f), AmbientEquator = new Color(0.58f, 0.62f, 0.64f), AmbientGround = new Color(0.35f, 0.35f, 0.34f),
                    FogColor = new Color(0.78f, 0.82f, 0.86f), FogStart = 1.5f, FogEnd = 26f, Rain = 0f, Wind = 0.1f,
                },
                new WeatherPreset
                {
                    State = WeatherState.Cloudy, SunIntensityScale = 0.6f, SunTint = new Color(0.9f, 0.92f, 1f),
                    AmbientSky = new Color(0.55f, 0.62f, 0.74f), AmbientEquator = new Color(0.55f, 0.6f, 0.58f), AmbientGround = new Color(0.33f, 0.3f, 0.27f),
                    FogColor = new Color(0.68f, 0.75f, 0.82f), FogStart = 35f, FogEnd = 130f, Rain = 0f, Wind = 0.4f,
                },
            };
        }

        void Reset() => ResetToDefaults();
    }
}
