using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Environment;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.River
{
    /// <summary>Inspector buttons (rebuild, banks, quality) and draggable control points with flow arrows in the Scene view.</summary>
    [CustomEditor(typeof(RiverFlowController))]
    public sealed class RiverFlowControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var river = (RiverFlowController)target;
            EditorGUILayout.HelpBox("Edit control points (drag them in the Scene view), width, depth, segments and foam, then press Rebuild River. " +
                                    "Water movement comes from the shader; nothing is rebuilt at runtime.", MessageType.None);
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild River", GUILayout.Height(26f))) RiverTools.Rebuild(river);
                if (GUILayout.Button("Regenerate Bank Details", GUILayout.Height(26f)))
                    Debug.Log("Greenhaven — " + RiverTools.AddBankDetails(river, GeneratorPrefs.Settings));
            }
            if (changed) river.ApplyQuality();
            EditorGUILayout.LabelField($"Length {river.Length:0.0} m, {river.controlPoints.Count} control points, {river.rocks.Count} in-water rocks.", EditorStyles.miniLabel);
        }

        void OnSceneGUI()
        {
            var river = (RiverFlowController)target;
            Transform t = river.transform;
            Handles.color = new Color(0.2f, 0.75f, 1f);
            for (int i = 0; i < river.controlPoints.Count; i++)
            {
                Vector3 world = t.TransformPoint(river.controlPoints[i]);
                float size = HandleUtility.GetHandleSize(world) * 0.08f;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(river, "Move river point");
                    river.controlPoints[i] = t.InverseTransformPoint(moved);
                }
                Handles.Label(world + Vector3.up * size * 3f, i.ToString());
                if (i > 0) Handles.DrawLine(t.TransformPoint(river.controlPoints[i - 1]), world);
            }
            // Downstream arrows along the baked centre line.
            for (int i = 6; i < river.PathCount - 1; i += 12)
            {
                Vector3 a = t.TransformPoint(river.PathPoint(i)), b = t.TransformPoint(river.PathPoint(i + 1));
                if ((b - a).sqrMagnitude < 1e-6f) continue;
                Handles.ArrowHandleCap(0, a + Vector3.up * 0.2f, Quaternion.LookRotation(b - a), HandleUtility.GetHandleSize(a) * 0.5f, EventType.Repaint);
            }
        }
    }
}
