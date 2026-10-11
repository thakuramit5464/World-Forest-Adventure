using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>
    /// Greenhaven ▸ Generate Assets. Nothing runs automatically: assets are only created or
    /// replaced when a Generate button is pressed (and replacing asks for confirmation).
    /// </summary>
    public sealed class AssetGeneratorWindow : EditorWindow
    {
        const string PrefsKey = "Greenhaven.AssetGenerator.";
        static readonly int[] StarterPackCounts = { 3, 3, 6 }; // pine, broadleaf, rock

        GeneratorSettings settings = GeneratorSettings.Default;
        readonly bool[] enabled = { true, true, true };
        readonly int[] counts = { 3, 3, 5 };
        string report = "Choose categories and press Generate.";
        Vector2 scroll;

        [MenuItem("Greenhaven/Generate Assets", priority = 20)]
        static void Open()
        {
            var window = GetWindow<AssetGeneratorWindow>("Greenhaven Assets");
            window.minSize = new Vector2(360f, 520f);
        }

        void OnEnable()
        {
            settings.Seed = EditorPrefs.GetInt(PrefsKey + "Seed", settings.Seed);
            settings.Size = EditorPrefs.GetFloat(PrefsKey + "Size", settings.Size);
            settings.ColorVariation = EditorPrefs.GetFloat(PrefsKey + "ColorVariation", settings.ColorVariation);
            for (int i = 0; i < Generators.All.Length; i++)
            {
                enabled[i] = EditorPrefs.GetBool(PrefsKey + "Enabled" + i, enabled[i]);
                counts[i] = EditorPrefs.GetInt(PrefsKey + "Count" + i, counts[i]);
            }
        }

        void OnDisable()
        {
            EditorPrefs.SetInt(PrefsKey + "Seed", settings.Seed);
            EditorPrefs.SetFloat(PrefsKey + "Size", settings.Size);
            EditorPrefs.SetFloat(PrefsKey + "ColorVariation", settings.ColorVariation);
            for (int i = 0; i < Generators.All.Length; i++)
            {
                EditorPrefs.SetBool(PrefsKey + "Enabled" + i, enabled[i]);
                EditorPrefs.SetInt(PrefsKey + "Count" + i, counts[i]);
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Procedural Asset Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Saves meshes, one shared palette material and prefabs to {GeneratedAssetStore.Root}. " +
                                    "The same seed and settings always produce the same assets.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                settings.Seed = EditorGUILayout.IntField(new GUIContent("Seed", "Same seed + settings = identical assets and names."), settings.Seed);
                if (GUILayout.Button("Randomize", GUILayout.Width(90f))) settings.Seed = Random.Range(1, 100000);
            }
            settings.Size = EditorGUILayout.Slider(new GUIContent("Size", "Overall scale multiplier."), settings.Size, 0.5f, 2f);
            settings.ColorVariation = EditorGUILayout.Slider(new GUIContent("Color Variation", "Per-asset and per-face colour spread."), settings.ColorVariation, 0f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Categories (variations)", EditorStyles.boldLabel);
            for (int i = 0; i < Generators.All.Length; i++)
            {
                IAssetGenerator gen = Generators.All[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    enabled[i] = EditorGUILayout.ToggleLeft(gen.DisplayName, enabled[i], GUILayout.Width(140f));
                    using (new EditorGUI.DisabledScope(!enabled[i]))
                        counts[i] = EditorGUILayout.IntSlider(counts[i], 1, 12);
                }
            }
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ToggleLeft("Bushes & Flowers (stage 2)", false);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Selected", GUILayout.Height(30f))) Run(selectedOnly: true);
                if (GUILayout.Button(new GUIContent("Generate Starter Pack", "All categories: 3 pines, 3 broadleaf, 6 rocks."), GUILayout.Height(30f))) Run(selectedOnly: false);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Preview Scene")) report = PreviewSceneBuilder.Build();
                if (GUILayout.Button("Validate")) report = GeneratedAssetStore.ValidateAll();
                if (GUILayout.Button("Show in Project")) ShowInProject();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last result", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void Run(bool selectedOnly)
        {
            var requests = new List<GeneratedAssetStore.Request>();
            for (int i = 0; i < Generators.All.Length; i++)
            {
                if (selectedOnly && !enabled[i]) continue;
                requests.Add(new GeneratedAssetStore.Request
                {
                    Generator = Generators.All[i],
                    Count = selectedOnly ? counts[i] : StarterPackCounts[i],
                });
            }
            report = GeneratedAssetStore.Generate(requests, settings);
            Debug.Log("Greenhaven generator: " + report);
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
