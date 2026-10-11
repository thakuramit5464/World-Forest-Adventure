using System;
using UnityEngine;

namespace Greenhaven.Weather
{
    public enum WeatherState
    {
        Sunny,
        Cloudy,
        Rainy,
        Foggy,
    }

    /// <summary>
    /// Everything weather changes, as plain values: sun, ambient light, fog, sky (camera background),
    /// rain amount and wind. Lerping two snapshots gives a smooth transition with no allocations.
    /// </summary>
    [Serializable]
    public struct WeatherSnapshot
    {
        public float SunIntensity;
        public Color SunColor;
        public Color AmbientSky, AmbientEquator, AmbientGround;
        public bool FogEnabled;
        public Color FogColor;
        public float FogStart, FogEnd;
        public Color Background;
        /// <summary>0 = dry, 1 = full rain (scaled by the rain controller's maximum emission).</summary>
        public float Rain;
        /// <summary>0..1, published as the global shader float "_GreenhavenWind" for future wind shaders.</summary>
        public float Wind;

        public static WeatherSnapshot Lerp(WeatherSnapshot a, WeatherSnapshot b, float t)
        {
            t = Mathf.Clamp01(t);
            return new WeatherSnapshot
            {
                SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
                SunColor = Color.Lerp(a.SunColor, b.SunColor, t),
                AmbientSky = Color.Lerp(a.AmbientSky, b.AmbientSky, t),
                AmbientEquator = Color.Lerp(a.AmbientEquator, b.AmbientEquator, t),
                AmbientGround = Color.Lerp(a.AmbientGround, b.AmbientGround, t),
                // Fog must be on for the whole blend if either end uses it.
                FogEnabled = t < 1f ? a.FogEnabled || b.FogEnabled : b.FogEnabled,
                FogColor = Color.Lerp(a.FogColor, b.FogColor, t),
                FogStart = Mathf.Lerp(a.FogStart, b.FogStart, t),
                FogEnd = Mathf.Lerp(a.FogEnd, b.FogEnd, t),
                Background = Color.Lerp(a.Background, b.Background, t),
                Rain = Mathf.Lerp(a.Rain, b.Rain, t),
                Wind = Mathf.Lerp(a.Wind, b.Wind, t),
            };
        }
    }

    /// <summary>
    /// Look of one weather state. Sunny is always the scene's own original lighting, so presets are
    /// only needed for the other states; intensity blends from the original toward the preset.
    /// </summary>
    [Serializable]
    public sealed class WeatherPreset
    {
        public WeatherState State;
        [Tooltip("Multiplier on the scene's original sun intensity.")]
        [Range(0f, 1.5f)] public float SunIntensityScale = 1f;
        [Tooltip("Multiplied with the scene's original sun colour.")]
        public Color SunTint = Color.white;
        public Color AmbientSky = Color.grey, AmbientEquator = Color.grey, AmbientGround = Color.grey;
        public Color FogColor = Color.grey;
        [Min(0f)] public float FogStart = 20f;
        [Min(0.1f)] public float FogEnd = 100f;
        [Range(0f, 1f)] public float Rain;
        [Range(0f, 1f)] public float Wind;

        /// <summary>Target snapshot: <paramref name="original"/> blended toward this preset by <paramref name="intensity"/>.</summary>
        public WeatherSnapshot Resolve(WeatherSnapshot original, float intensity)
        {
            var full = new WeatherSnapshot
            {
                SunIntensity = original.SunIntensity * SunIntensityScale,
                SunColor = original.SunColor * SunTint,
                AmbientSky = AmbientSky,
                AmbientEquator = AmbientEquator,
                AmbientGround = AmbientGround,
                FogEnabled = true,
                FogColor = FogColor,
                FogStart = FogStart,
                FogEnd = Mathf.Max(FogStart + 0.1f, FogEnd),
                Background = FogColor, // sky matches the fog so the horizon blends
                Rain = Rain,
                Wind = Wind,
            };
            return WeatherSnapshot.Lerp(original, full, intensity);
        }
    }
}
