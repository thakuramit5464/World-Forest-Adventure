using System.IO;
using Greenhaven.Core;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.Quests
{
    /// <summary>Save-file helpers for testing: delete it (new game) or open its folder.</summary>
    public static class ProgressMenu
    {
        [MenuItem("Greenhaven/Progress/Delete Save File (new game)", priority = 200)]
        static void Delete()
        {
            string path = GameProgress.FilePath;
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Save file", $"No save file at\n{path}", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Delete save file?", $"Delete {path}?\nAll quest progress and collected items are forgotten.", "Delete", "Cancel")) return;
            if (Application.isPlaying && GameProgress.Exists) GameProgress.Instance.ResetAll();
            else File.Delete(path);
            Debug.Log("Greenhaven: save file deleted.");
        }

        [MenuItem("Greenhaven/Progress/Show Save Folder", priority = 201)]
        static void Show() => EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
