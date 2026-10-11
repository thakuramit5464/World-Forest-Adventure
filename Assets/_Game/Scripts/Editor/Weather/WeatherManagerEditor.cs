using Greenhaven.Weather;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.WeatherTools
{
    /// <summary>Inspector with Play-mode test buttons for the WeatherManager.</summary>
    [CustomEditor(typeof(WeatherManager))]
    public sealed class WeatherManagerEditor : UnityEditor.Editor
    {
        float testIntensity = 1f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            var manager = (WeatherManager)target;
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to test weather here or via Greenhaven ▸ Weather.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Current: {manager.CurrentState}  ({manager.TransitionProgress * 100f:0}% blended)");
            testIntensity = EditorGUILayout.Slider("Intensity", testIntensity, 0f, 1f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sunny")) manager.SetWeather(WeatherState.Sunny, testIntensity);
                if (GUILayout.Button("Rainy")) manager.SetWeather(WeatherState.Rainy, testIntensity);
                if (GUILayout.Button("Foggy")) manager.SetWeather(WeatherState.Foggy, testIntensity);
                if (GUILayout.Button("Cloudy*")) manager.SetWeather(WeatherState.Cloudy, testIntensity);
            }
            if (GUILayout.Button("Restore Original Lighting (instant)")) manager.RestoreOriginal();
            EditorGUILayout.HelpBox("* Cloudy is included in the API but not yet tuned or tested (Phase 2).", MessageType.None);
            Repaint(); // keep the blend percentage live
        }
    }
}
