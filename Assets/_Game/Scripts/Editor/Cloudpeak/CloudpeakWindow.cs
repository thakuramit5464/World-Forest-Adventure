using Greenhaven.Editor.AssetGenerator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Greenhaven.Editor.Cloudpeak
{
    /// <summary>
    /// Greenhaven ▸ Cloudpeak Generator: terrain and trail parameters, a data-only trail check, and the
    /// (confirmed) scene build. Nothing is generated automatically on launch.
    /// </summary>
    public sealed class CloudpeakWindow : EditorWindow
    {
        const string PrefsKey = "Greenhaven.Cloudpeak.Settings";

        CloudpeakSettings settings;
        string report = "";
        Vector2 scroll;

        public static CloudpeakSettings SavedSettings
        {
            get
            {
                string json = EditorPrefs.GetString(PrefsKey, "");
                if (string.IsNullOrEmpty(json)) return CloudpeakSettings.Default;
                try { return JsonUtility.FromJson<CloudpeakSettings>(json).Clamped(); }
                catch { return CloudpeakSettings.Default; }
            }
            set => EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(value));
        }

        [MenuItem("Greenhaven/Cloudpeak Generator…", priority = 46)]
        static void Open() => GetWindow<CloudpeakWindow>("Cloudpeak Generator").minSize = new Vector2(380f, 520f);

        [MenuItem("Greenhaven/Create Cloudpeak Adventure Scene", priority = 47)]
        static void CreateMenu() => Show("Create Cloudpeak Adventure Scene", CloudpeakSceneBuilder.Build(SavedSettings, GeneratorPrefs.Settings));

        [MenuItem("Greenhaven/Open Cloudpeak Adventure Scene", priority = 48)]
        static void OpenSceneMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Open Cloudpeak Scene", "Stop Play mode first.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CloudpeakSceneBuilder.ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Open Cloudpeak Scene", "The scene has not been created yet. Use Greenhaven ▸ Create Cloudpeak Adventure Scene.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(CloudpeakSceneBuilder.ScenePath);
        }

        static void Show(string title, string result)
        {
            Debug.Log($"Greenhaven — {title}:\n{result}");
            EditorUtility.DisplayDialog(title, result.Length > 1400 ? result.Substring(0, 1400) + "…\n(full report in the Console)" : result, "OK");
        }

        void OnEnable() => settings = SavedSettings;

        void OnDisable() => SavedSettings = settings;

        void OnGUI()
        {
            EditorGUILayout.LabelField("Terrain", EditorStyles.boldLabel);
            settings.Seed = EditorGUILayout.IntField(new GUIContent("Terrain seed", "Same settings and seed → the same terrain, trail and scatter."), settings.Seed);
            settings.HalfSize = EditorGUILayout.Slider(new GUIContent("Region half-size (m)", "100 = a 200 × 200 m playable region."), settings.HalfSize, 85f, 130f);
            settings.PeakHeight = EditorGUILayout.Slider("Summit height (m)", settings.PeakHeight, 20f, 70f);
            settings.MountainRadius = EditorGUILayout.Slider("Mountain radius (m)", settings.MountainRadius, 60f, settings.HalfSize * 0.8f);
            settings.ConeShare = EditorGUILayout.Slider(new GUIContent("Summit cone share", "How much of the height is in the steep cone (the rest is the foothill skirt)."), settings.ConeShare, 0.35f, 0.7f);
            settings.RidgeVariation = EditorGUILayout.Slider("Ridge variation", settings.RidgeVariation, 0f, 1f);
            settings.CellSize = EditorGUILayout.Slider(new GUIContent("Grid cell (m)", "Larger = fewer terrain triangles (better on low-end phones)."), settings.CellSize, 1.5f, 3f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Trail and vegetation", EditorStyles.boldLabel);
            settings.TrailWidth = EditorGUILayout.Slider("Trail width (m)", settings.TrailWidth, 2.4f, 4.5f);
            settings.MaxGrade = EditorGUILayout.Slider(new GUIContent("Max trail grade", "Rise over run. 0.25 ≈ 14°; the player can climb 45°."), settings.MaxGrade, 0.2f, 0.3f);
            settings.VegetationDensity = EditorGUILayout.Slider(new GUIContent("Vegetation density", "0.5 for low-end phones."), settings.VegetationDensity, 0f, 2f);
            EditorGUILayout.HelpBox($"Assets (trees, rocks, props) use the asset generator seed {GeneratorPrefs.Settings.Seed} from Greenhaven ▸ Generate Assets.", MessageType.None);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to defaults")) settings = CloudpeakSettings.Default;
                if (GUILayout.Button(new GUIContent("Check trail", "Generates the terrain and trail in memory only and reports grades, continuity and overlaps.")))
                {
                    CloudpeakWorld world = CloudpeakWorld.Generate(settings);
                    CloudpeakWorld.Report r = world.Validate();
                    report = (r.Passed ? "✔ Trail checks passed.\n" : "⚠ Trail checks failed.\n") + r;
                }
            }
            if (GUILayout.Button("Create / Rebuild Cloudpeak Adventure Scene", GUILayout.Height(30f)))
            {
                SavedSettings = settings;
                report = CloudpeakSceneBuilder.Build(settings, GeneratorPrefs.Settings);
                Debug.Log("Greenhaven — Cloudpeak:\n" + report);
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("Open Cloudpeak Adventure Scene")) OpenSceneMenu();

            if (!string.IsNullOrEmpty(report))
            {
                EditorGUILayout.Space();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.HelpBox(report, MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
