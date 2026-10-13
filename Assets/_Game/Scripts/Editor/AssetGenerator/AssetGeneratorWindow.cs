using System.Collections.Generic;
using System.Linq;
using Greenhaven.Editor.VillageScene;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>Per-user generator settings shared by the window and the Greenhaven menu commands.</summary>
    public static class GeneratorPrefs
    {
        const string Key = "Greenhaven.AssetGenerator.";

        public static GeneratorSettings Settings
        {
            get
            {
                GeneratorSettings d = GeneratorSettings.Default;
                return new GeneratorSettings
                {
                    Seed = EditorPrefs.GetInt(Key + "Seed", d.Seed),
                    Size = EditorPrefs.GetFloat(Key + "Size", d.Size),
                    ColorVariation = EditorPrefs.GetFloat(Key + "ColorVariation", d.ColorVariation),
                    FoliageDetail = EditorPrefs.GetFloat(Key + "FoliageDetail", d.FoliageDetail),
                };
            }
            set
            {
                EditorPrefs.SetInt(Key + "Seed", value.Seed);
                EditorPrefs.SetFloat(Key + "Size", value.Size);
                EditorPrefs.SetFloat(Key + "ColorVariation", value.ColorVariation);
                EditorPrefs.SetFloat(Key + "FoliageDetail", value.FoliageDetail);
            }
        }

        public static float TreeDensity
        {
            get => EditorPrefs.GetFloat(Key + "TreeDensity", 1f);
            set => EditorPrefs.SetFloat(Key + "TreeDensity", value);
        }

        public static bool GetEnabled(IAssetGenerator g) => EditorPrefs.GetBool(Key + "Enabled." + g.Prefix, true);
        public static void SetEnabled(IAssetGenerator g, bool on) => EditorPrefs.SetBool(Key + "Enabled." + g.Prefix, on);
        public static int GetCount(IAssetGenerator g) => EditorPrefs.GetInt(Key + "Count." + g.Prefix, g.DefaultCount);
        public static void SetCount(IAssetGenerator g, int count) => EditorPrefs.SetInt(Key + "Count." + g.Prefix, count);
    }

    /// <summary>
    /// Greenhaven ▸ Generate Assets. Nothing runs automatically: assets are only created or replaced
    /// when a button or menu command is used, and replacing always asks first.
    /// </summary>
    public sealed class AssetGeneratorWindow : EditorWindow
    {
        GeneratorSettings settings;
        float treeDensity;
        string report = "Choose categories and press Generate.";
        Vector2 scroll;
        Vector2 reportScroll;

        [MenuItem("Greenhaven/Generate Assets", priority = 20)]
        static void Open()
        {
            var window = GetWindow<AssetGeneratorWindow>("Greenhaven Assets");
            window.minSize = new Vector2(380f, 600f);
        }

        [MenuItem("Greenhaven/Rig Greenhaven Adventurer Model", priority = 19)]
        static void RigAdventurerMenu() => Report("Rig Greenhaven Adventurer Model", AdventurerModelRig.Rig());

        [MenuItem("Greenhaven/Build Adventurer v2 (taller, expressive)", priority = 20)]
        static void RigAdventurerV2Menu() => Report("Build Adventurer v2", AdventurerModelRig.Rig(true));

        [MenuItem("Greenhaven/Generate Hero", priority = 21)]
        static void GenerateHeroMenu() => Report("Generate Hero", HeroGenerator.Generate(GeneratorPrefs.Settings.Seed));

        [MenuItem("Greenhaven/Create Character Test Scene", priority = 23)]
        static void CharacterTestMenu() =>
            Report("Create Character Test Scene", Greenhaven.Editor.CharacterTest.CharacterTestSceneBuilder.Build(GeneratorPrefs.Settings.Seed));

        [MenuItem("Greenhaven/Generate Village Assets", priority = 22)]
        static void GenerateVillageMenu() => Report("Generate Village Assets", GenerateDefaults(GeneratorPrefs.Settings));

        [MenuItem("Greenhaven/Create Village Scene", priority = 40)]
        static void CreateVillageMenu() => Report("Create Village Scene", VillageSceneBuilder.Build(GeneratorPrefs.Settings, GeneratorPrefs.TreeDensity));

        [MenuItem("Greenhaven/Open Village Scene", priority = 41)]
        static void OpenVillageMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Open Village Scene", "Stop Play mode first.", "OK");
                return;
            }
            if (!System.IO.File.Exists(VillageSceneBuilder.ScenePath))
            {
                EditorUtility.DisplayDialog("Open Village Scene", "The village scene has not been created yet. Use Greenhaven ▸ Create Village Scene.", "OK");
                return;
            }
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(VillageSceneBuilder.ScenePath);
        }

        [MenuItem("Greenhaven/Make Village the Build Start Scene", priority = 42)]
        static void VillageAsStartSceneMenu()
        {
            if (!System.IO.File.Exists(VillageSceneBuilder.ScenePath))
            {
                EditorUtility.DisplayDialog("Build start scene", "Create the village scene first (Greenhaven ▸ Create Village Scene).", "OK");
                return;
            }
            // Village first (it launches), every other listed scene kept; the Unity template scene is unticked.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(VillageSceneBuilder.ScenePath, true) };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.path == VillageSceneBuilder.ScenePath) continue;
                scenes.Add(new EditorBuildSettingsScene(s.path, s.enabled && !s.path.EndsWith("Scenes/SampleScene.unity")));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            string list = string.Join("\n", scenes.Select((scene, i) => $"{i}. {(scene.enabled ? "✔" : "✘")} {scene.path}"));
            Report("Build start scene", $"Build scene list:\n{list}\n\nBuild with File ▸ Build Profiles ▸ Windows ▸ Build.");
        }

        [MenuItem("Greenhaven/Validate Generated Assets", priority = 60)]
        static void ValidateMenu() => Report("Validate Generated Assets", GeneratedAssetStore.ValidateAll());

        static void Report(string title, string text)
        {
            Debug.Log($"Greenhaven — {title}:\n{text}");
            string shown = text.Length > 1200 ? text.Substring(0, 1200) + "\n…(full report in the Console)" : text;
            EditorUtility.DisplayDialog(title, shown, "OK");
        }

        /// <summary>Every category at its default count: the full village starter set.</summary>
        public static string GenerateDefaults(GeneratorSettings s) =>
            GeneratedAssetStore.Generate(Generators.All.Select(g => new GeneratedAssetStore.Request { Generator = g, Count = g.DefaultCount }).ToList(), s);

        void OnEnable()
        {
            settings = GeneratorPrefs.Settings;
            treeDensity = GeneratorPrefs.TreeDensity;
        }

        void OnDisable()
        {
            GeneratorPrefs.Settings = settings;
            GeneratorPrefs.TreeDensity = treeDensity;
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Procedural Asset Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Saves meshes, shared palette materials and prefabs to {GeneratedAssetStore.Root}. " +
                                    "The same seed and settings always produce the same assets.", MessageType.None);

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                settings.Seed = EditorGUILayout.IntField(new GUIContent("Seed", "Same seed + settings = identical assets and names."), settings.Seed);
                if (GUILayout.Button("Randomize", GUILayout.Width(90f))) settings.Seed = Random.Range(1, 100000);
            }
            settings.Size = EditorGUILayout.Slider(new GUIContent("Size", "Overall scale multiplier."), settings.Size, 0.5f, 2f);
            settings.ColorVariation = EditorGUILayout.Slider(new GUIContent("Color Variation", "Per-asset and per-face colour spread."), settings.ColorVariation, 0f, 1f);
            settings.FoliageDetail = EditorGUILayout.Slider(new GUIContent("Foliage Detail",
                "Tree canopy cluster count and smoothness. ~0.5 for entry-level phones, 0.8 default, 1 = full."), settings.Detail, 0.3f, 1f);

            foreach (IGrouping<string, IAssetGenerator> group in Generators.All.GroupBy(g => g.Group))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(group.Key + " (variations)", EditorStyles.boldLabel);
                foreach (IAssetGenerator gen in group)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool on = EditorGUILayout.ToggleLeft(gen.DisplayName, GeneratorPrefs.GetEnabled(gen), GUILayout.Width(160f));
                        GeneratorPrefs.SetEnabled(gen, on);
                        using (new EditorGUI.DisabledScope(!on))
                            GeneratorPrefs.SetCount(gen, EditorGUILayout.IntSlider(GeneratorPrefs.GetCount(gen), 1, 12));
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Selected", GUILayout.Height(30f))) report = GenerateSelected();
                if (GUILayout.Button(new GUIContent("Generate All (defaults)", "Every category at its default count."), GUILayout.Height(30f)))
                    report = GenerateDefaults(settings);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hero", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate Hero (uses Seed)")) report = HeroGenerator.Generate(settings.Seed);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Village scene", EditorStyles.boldLabel);
            treeDensity = EditorGUILayout.Slider(new GUIContent("Tree Density", "Multiplier for trees, bushes and rocks around the village."), treeDensity, 0.3f, 1.5f);
            if (GUILayout.Button("Create Village Scene (uses Seed)"))
            {
                GeneratorPrefs.Settings = settings;
                report = VillageSceneBuilder.Build(settings, treeDensity);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Preview Scene")) report = PreviewSceneBuilder.Build();
                if (GUILayout.Button("Validate")) report = GeneratedAssetStore.ValidateAll();
                if (GUILayout.Button("Show in Project")) ShowInProject();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last result", EditorStyles.boldLabel);
            reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.MinHeight(120f));
            EditorGUILayout.HelpBox(report, MessageType.None);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndScrollView();
        }

        string GenerateSelected()
        {
            var requests = new List<GeneratedAssetStore.Request>();
            foreach (IAssetGenerator gen in Generators.All)
                if (GeneratorPrefs.GetEnabled(gen))
                    requests.Add(new GeneratedAssetStore.Request { Generator = gen, Count = GeneratorPrefs.GetCount(gen) });
            string result = GeneratedAssetStore.Generate(requests, settings);
            Debug.Log("Greenhaven generator: " + result);
            return result;
        }

        static void ShowInProject()
        {
            GeneratedAssetStore.EnsureFolders();
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(GeneratedAssetStore.PrefabFolder);
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }
}
