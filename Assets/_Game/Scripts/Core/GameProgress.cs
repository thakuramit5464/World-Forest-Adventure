using System;
using System.Collections.Generic;
using System.IO;
using Greenhaven.Collectibles;
using Greenhaven.Quests;
using UnityEngine;

namespace Greenhaven.Core
{
    /// <summary>
    /// The player's progress, shared by every scene and saved to disk: collected items, quest progress,
    /// puzzle flags and the last scene entered. Created on first use, survives scene loads, loads the save
    /// file once, and writes it whenever something changes (small JSON, written atomically). Works offline;
    /// a missing or broken file simply starts a fresh game.
    /// </summary>
    public sealed class GameProgress : MonoBehaviour
    {
        public const string FileName = "greenhaven_save.json";

        static GameProgress instance;

        public static GameProgress Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GameProgress");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GameProgress>();
                    instance.Load();
                }
                return instance;
            }
        }

        public static bool Exists => instance != null;
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public CollectedItems Items { get; } = new CollectedItems();
        public QuestLog Quests { get; } = new QuestLog();
        readonly HashSet<string> flags = new HashSet<string>();

        public string LastScene { get; private set; } = "";
        public string LastArrival { get; private set; } = "";
        public bool HasSaveFile => File.Exists(FilePath);

        public event Action Saved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return;
            Save();
        }

        /// <summary>Remembers where the player is heading, so a later "Continue" can return there.</summary>
        public void RecordScene(string scene, string arrival)
        {
            LastScene = scene ?? "";
            LastArrival = arrival ?? "";
            Save();
        }

        public void Save()
        {
            try
            {
                SaveData data = SaveSerializer.Capture(Items, Quests, flags, LastScene, LastArrival);
                string path = FilePath, temp = path + ".tmp";
                File.WriteAllText(temp, SaveSerializer.ToJson(data));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                Saved?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameProgress: could not save ({e.Message}).");
            }
        }

        public void Load()
        {
            if (!File.Exists(FilePath)) return;
            string json;
            try { json = File.ReadAllText(FilePath); }
            catch (Exception e)
            {
                Debug.LogWarning($"GameProgress: could not read the save file ({e.Message}); starting fresh.");
                return;
            }
            SaveData data = SaveSerializer.FromJson(json, out string problem);
            if (data == null)
            {
                Debug.LogWarning($"GameProgress: save file ignored ({problem}); starting fresh.");
                return;
            }
            if (problem != null) Debug.LogWarning($"GameProgress: {problem}.");
            SaveSerializer.Apply(data, Items, Quests, flags);
            LastScene = data.scene ?? "";
            LastArrival = data.arrival ?? "";
        }

        /// <summary>New game: forgets everything in memory and deletes the file.</summary>
        public void ResetAll()
        {
            Items.Clear();
            Quests.Clear();
            flags.Clear();
            LastScene = LastArrival = "";
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning($"GameProgress: could not delete the save file ({e.Message})."); }
        }
    }
}
