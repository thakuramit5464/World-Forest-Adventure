using Greenhaven.Wind;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.WindTools
{
    /// <summary>Inspector with Play-mode presets, gust preview and live readouts for the WindManager.</summary>
    [CustomEditor(typeof(WindManager))]
    public sealed class WindManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            var wind = (WindManager)target;
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see vegetation move and to use the presets.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Strength {wind.Strength:0.00}   Gust {wind.Gust:0.00}   Direction {wind.Direction.x:0.00}, {wind.Direction.z:0.00}");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Calm")) wind.ApplyPreset(WindPreset.Calm);
                if (GUILayout.Button("Breeze")) wind.ApplyPreset(WindPreset.Breeze);
                if (GUILayout.Button("Strong")) wind.ApplyPreset(WindPreset.Strong);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Gust now")) wind.TriggerGust();
                if (GUILayout.Button(wind.WindEnabled ? "Disable wind" : "Enable wind")) wind.SetEnabled(!wind.WindEnabled);
            }
            Repaint();
        }
    }
}
