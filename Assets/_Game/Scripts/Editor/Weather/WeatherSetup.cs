using Greenhaven.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenhaven.Editor.WeatherTools
{
    /// <summary>
    /// Editor workflow for the weather system: add it to the open scene (with a default profile and rain
    /// material), and switch weather from the Greenhaven ▸ Weather menu while in Play mode.
    /// </summary>
    public static class WeatherSetup
    {
        const string Folder = "Assets/_Game/Settings/Weather";
        const string ProfilePath = Folder + "/WeatherProfile_Default.asset";
        const string RainMaterialPath = Folder + "/M_RainDrop.mat";

        [MenuItem("Greenhaven/Weather/Add Weather To Open Scene", priority = 200)]
        static void AddMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Weather", "Stop Play mode first.", "OK");
                return;
            }
            bool existed = Object.FindAnyObjectByType<WeatherManager>() != null;
            WeatherManager manager = AddToScene();
            Selection.activeObject = manager;
            EditorUtility.DisplayDialog("Weather", existed
                ? "This scene already has a WeatherManager (selected)."
                : "Weather added to the open scene (selected). Save the scene to keep it, then press Play and use Greenhaven ▸ Weather.", "OK");
        }

        /// <summary>Adds (or returns the existing) WeatherManager + rain to the open scene.</summary>
        public static WeatherManager AddToScene()
        {
            WeatherManager existing = Object.FindAnyObjectByType<WeatherManager>();
            if (existing != null) return existing;

            WeatherProfile profile = EnsureProfile();
            Material rainMaterial = EnsureRainMaterial();

            var root = new GameObject("Weather");
            var manager = root.AddComponent<WeatherManager>();
            var rainGo = new GameObject("Rain");
            rainGo.transform.SetParent(root.transform, false);
            var rain = rainGo.AddComponent<RainController>();
            var particlesGo = new GameObject("RainParticles");
            particlesGo.transform.SetParent(rainGo.transform, false);
            var particles = particlesGo.AddComponent<ParticleSystem>();

            var rainSo = new SerializedObject(rain);
            rainSo.FindProperty("dropMaterial").objectReferenceValue = rainMaterial;
            rainSo.ApplyModifiedPropertiesWithoutUndo();
            rain.Configure(particles); // the scene stores a ready, stopped particle system (reused at runtime)

            Light sun = RenderSettings.sun;
            if (sun == null)
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }

            var so = new SerializedObject(manager);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("rain").objectReferenceValue = rain;
            so.FindProperty("sun").objectReferenceValue = sun;
            so.FindProperty("skyCamera").objectReferenceValue = Camera.main;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Add Weather");
            EditorSceneManager.MarkSceneDirty(root.scene);
            return manager;
        }

        static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Settings")) AssetDatabase.CreateFolder("Assets/_Game", "Settings");
            AssetDatabase.CreateFolder("Assets/_Game/Settings", "Weather");
        }

        static WeatherProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(ProfilePath);
            if (profile != null) return profile; // keep any tuning the user has done
            EnsureFolder();
            profile = ScriptableObject.CreateInstance<WeatherProfile>();
            profile.ResetToDefaults();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Transparent URP Particles/Unlit material using Unity's soft default particle texture.</summary>
        static Material EnsureRainMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RainMaterialPath);
            if (mat != null) return mat;
            EnsureFolder();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("Greenhaven weather: URP Particles/Unlit shader not found; rain will use the default particle material.");
                return null;
            }
            mat = new Material(shader) { name = "M_RainDrop" };
            mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);                                 // transparent
            mat.SetFloat("_Blend", 0f);                                   // alpha blend
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, RainMaterialPath);
            return mat;
        }

        // ───────────────────────────── Play-mode test menu ─────────────────────────────

        static void Set(WeatherState state)
        {
            var manager = Object.FindAnyObjectByType<WeatherManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Weather", "No WeatherManager in this scene. Stop Play mode and use Greenhaven ▸ Weather ▸ Add Weather To Open Scene.", "OK");
                return;
            }
            manager.SetWeather(state);
        }

        static bool PlayingWithWeather() => EditorApplication.isPlaying && Object.FindAnyObjectByType<WeatherManager>() != null;

        [MenuItem("Greenhaven/Weather/Sunny", priority = 220)] static void Sunny() => Set(WeatherState.Sunny);
        [MenuItem("Greenhaven/Weather/Rainy", priority = 221)] static void Rainy() => Set(WeatherState.Rainy);
        [MenuItem("Greenhaven/Weather/Foggy", priority = 222)] static void Foggy() => Set(WeatherState.Foggy);
        [MenuItem("Greenhaven/Weather/Cloudy (not yet tuned)", priority = 223)] static void Cloudy() => Set(WeatherState.Cloudy);

        [MenuItem("Greenhaven/Weather/Restore Original Lighting", priority = 240)]
        static void Restore() => Object.FindAnyObjectByType<WeatherManager>()?.RestoreOriginal();

        [MenuItem("Greenhaven/Weather/Sunny", true)]
        [MenuItem("Greenhaven/Weather/Rainy", true)]
        [MenuItem("Greenhaven/Weather/Foggy", true)]
        [MenuItem("Greenhaven/Weather/Cloudy (not yet tuned)", true)]
        [MenuItem("Greenhaven/Weather/Restore Original Lighting", true)]
        static bool ValidatePlayMenu() => PlayingWithWeather();
    }
}
