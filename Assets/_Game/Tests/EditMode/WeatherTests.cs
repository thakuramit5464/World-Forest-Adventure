using Greenhaven.Weather;
using NUnit.Framework;
using UnityEngine;

namespace Greenhaven.Tests
{
    public class WeatherTests
    {
        static WeatherSnapshot Sunny => new WeatherSnapshot
        {
            SunIntensity = 1.25f, SunColor = new Color(1f, 0.95f, 0.85f),
            AmbientSky = new Color(0.66f, 0.78f, 0.95f), AmbientEquator = new Color(0.66f, 0.74f, 0.62f), AmbientGround = new Color(0.4f, 0.34f, 0.28f),
            FogEnabled = true, FogColor = new Color(0.72f, 0.85f, 0.96f), FogStart = 45f, FogEnd = 170f,
            Background = new Color(0.72f, 0.85f, 0.96f), Rain = 0f, Wind = 0f,
        };

        static WeatherPreset Rainy
        {
            get
            {
                WeatherProfile profile = ScriptableObject.CreateInstance<WeatherProfile>();
                profile.ResetToDefaults();
                profile.TryGet(WeatherState.Rainy, out WeatherPreset preset);
                Object.DestroyImmediate(profile);
                return preset;
            }
        }

        [Test]
        public void ZeroIntensity_LeavesTheSceneUnchanged()
        {
            WeatherSnapshot s = Rainy.Resolve(Sunny, 0f);
            Assert.That(s.SunIntensity, Is.EqualTo(Sunny.SunIntensity).Within(1e-5f));
            Assert.That(s.FogEnd, Is.EqualTo(Sunny.FogEnd).Within(1e-4f));
            Assert.That(s.Rain, Is.EqualTo(0f));
        }

        [Test]
        public void FullRain_DimsTheSun_PullsInFog_AndRains()
        {
            WeatherSnapshot s = Rainy.Resolve(Sunny, 1f);
            Assert.That(s.SunIntensity, Is.LessThan(Sunny.SunIntensity * 0.5f));
            Assert.That(s.FogEnd, Is.LessThan(Sunny.FogEnd));
            Assert.That(s.Rain, Is.EqualTo(1f));
            Assert.That(Vector4.Distance(s.Background, s.FogColor), Is.LessThan(1e-4f), "sky matches fog so the horizon blends");
        }

        [Test]
        public void Lerp_HitsBothEndpoints_AndKeepsFogOnMidBlend()
        {
            WeatherSnapshot clearSky = Sunny;
            clearSky.FogEnabled = false;
            WeatherSnapshot rain = Rainy.Resolve(Sunny, 1f);

            Assert.That(WeatherSnapshot.Lerp(clearSky, rain, 0f).SunIntensity, Is.EqualTo(clearSky.SunIntensity).Within(1e-5f));
            Assert.That(WeatherSnapshot.Lerp(clearSky, rain, 1f).SunIntensity, Is.EqualTo(rain.SunIntensity).Within(1e-5f));
            Assert.That(WeatherSnapshot.Lerp(clearSky, rain, 0.5f).FogEnabled, Is.True);
            Assert.That(WeatherSnapshot.Lerp(rain, clearSky, 1f).FogEnabled, Is.False, "returns to the original fog setting at the end");
        }

        [Test]
        public void DefaultProfile_DefinesPhaseOneStates_WithValidFog()
        {
            WeatherProfile profile = ScriptableObject.CreateInstance<WeatherProfile>();
            profile.ResetToDefaults();
            foreach (WeatherState state in new[] { WeatherState.Rainy, WeatherState.Foggy })
            {
                Assert.That(profile.TryGet(state, out WeatherPreset p), Is.True, state.ToString());
                Assert.That(p.Resolve(Sunny, 1f).FogEnd, Is.GreaterThan(p.Resolve(Sunny, 1f).FogStart), state.ToString());
            }
            Assert.That(profile.TryGet(WeatherState.Sunny, out _), Is.False, "Sunny always uses the scene's own lighting");
            Object.DestroyImmediate(profile);
        }
    }
}
