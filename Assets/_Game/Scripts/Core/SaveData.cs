using System;
using System.Collections.Generic;
using Greenhaven.Collectibles;
using Greenhaven.Quests;
using UnityEngine;

namespace Greenhaven.Core
{
    /// <summary>Versioned save file contents. Add fields, never rename or remove them; bump <see cref="CurrentVersion"/> when the meaning changes.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        [Serializable]
        public struct Item
        {
            public string id, type;
        }

        [Serializable]
        public struct Quest
        {
            public string id;
            public int state, step, count;
        }

        public int version = CurrentVersion;
        public List<Item> items = new List<Item>();
        public List<Quest> quests = new List<Quest>();
        public List<string> flags = new List<string>();
        public string scene = "";
        public string arrival = "";
        public long savedAtTicks;
    }

    /// <summary>Pure conversion between the live progress objects and <see cref="SaveData"/> JSON (unit-tested).</summary>
    public static class SaveSerializer
    {
        public static SaveData Capture(CollectedItems items, QuestLog quests, IEnumerable<string> flags, string scene, string arrival)
        {
            var data = new SaveData { scene = scene ?? "", arrival = arrival ?? "", savedAtTicks = DateTime.UtcNow.Ticks };
            foreach (KeyValuePair<string, string> e in items.Entries) data.items.Add(new SaveData.Item { id = e.Key, type = e.Value });
            foreach (KeyValuePair<string, QuestProgress> e in quests.Entries)
                data.quests.Add(new SaveData.Quest { id = e.Key, state = (int)e.Value.State, step = e.Value.StepIndex, count = e.Value.Count });
            foreach (string f in flags) data.flags.Add(f);
            return data;
        }

        /// <summary>Applies saved data onto (cleared) progress objects. Unknown versions are read as far as their fields allow.</summary>
        public static void Apply(SaveData data, CollectedItems items, QuestLog quests, ICollection<string> flags)
        {
            items.Clear();
            quests.Clear();
            flags.Clear();
            if (data == null) return;
            foreach (SaveData.Item i in data.items ?? new List<SaveData.Item>())
                if (!string.IsNullOrEmpty(i.id) && !string.IsNullOrEmpty(i.type)) items.TryCollect(i.id, i.type);
            foreach (SaveData.Quest q in data.quests ?? new List<SaveData.Quest>())
                if (!string.IsNullOrEmpty(q.id) && q.state >= 0 && q.state <= (int)QuestState.Completed) quests.Restore(q.id, (QuestState)q.state, q.step, q.count);
            foreach (string f in data.flags ?? new List<string>())
                if (!string.IsNullOrEmpty(f)) flags.Add(f);
        }

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data, true);

        /// <summary>Parses JSON; returns null (and a reason) for anything unreadable, so a bad file never crashes the game.</summary>
        public static SaveData FromJson(string json, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                problem = "empty";
                return null;
            }
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null)
                {
                    problem = "not a save file";
                    return null;
                }
                if (data.version > SaveData.CurrentVersion) problem = $"newer version {data.version}; reading what is understood";
                return data;
            }
            catch (Exception e)
            {
                problem = e.Message;
                return null;
            }
        }
    }
}
