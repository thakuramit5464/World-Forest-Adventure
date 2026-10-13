using System;
using UnityEngine;

namespace Greenhaven.UI
{
    /// <summary>Player options, stored in PlayerPrefs and applied immediately. Read by the input reader, camera and HUD.</summary>
    public static class GameSettings
    {
        const string Prefix = "Greenhaven.";
        static bool loaded;
        static float lookSensitivity = 1f, masterVolume = 0.8f;
        static bool invertY, alwaysShowTouchControls, autoRecentre = true;

        public static event Action Changed;

        public static float LookSensitivity { get { Load(); return lookSensitivity; } set { lookSensitivity = Mathf.Clamp(value, 0.3f, 2.5f); Save(); } }
        public static bool InvertY { get { Load(); return invertY; } set { invertY = value; Save(); } }
        public static bool AlwaysShowTouchControls { get { Load(); return alwaysShowTouchControls; } set { alwaysShowTouchControls = value; Save(); } }
        public static bool AutoRecentre { get { Load(); return autoRecentre; } set { autoRecentre = value; Save(); } }
        public static float MasterVolume { get { Load(); return masterVolume; } set { masterVolume = Mathf.Clamp01(value); Save(); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            lookSensitivity = PlayerPrefs.GetFloat(Prefix + "LookSensitivity", 1f);
            invertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) == 1;
            alwaysShowTouchControls = PlayerPrefs.GetInt(Prefix + "AlwaysShowTouch", 0) == 1;
            autoRecentre = PlayerPrefs.GetInt(Prefix + "AutoRecentre", 1) == 1;
            masterVolume = PlayerPrefs.GetFloat(Prefix + "MasterVolume", 0.8f);
            Apply();
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "LookSensitivity", lookSensitivity);
            PlayerPrefs.SetInt(Prefix + "InvertY", invertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "AlwaysShowTouch", alwaysShowTouchControls ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "AutoRecentre", autoRecentre ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "MasterVolume", masterVolume);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Pushes values that live outside the settings (currently the master volume).</summary>
        public static void Apply() => AudioListener.volume = masterVolume;
    }
}
