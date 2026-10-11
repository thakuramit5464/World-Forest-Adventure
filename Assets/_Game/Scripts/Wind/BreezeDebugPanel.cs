using Greenhaven.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenhaven.Wind
{
    /// <summary>
    /// Development aid for the breeze demo: press F2 for buttons to switch wind presets, trigger a gust,
    /// toggle wind / quality / leaves / ambience, and show the character animation readout (F3 overlay).
    /// Editor and development builds only.
    /// </summary>
    public sealed class BreezeDebugPanel : MonoBehaviour
    {
        [SerializeField] WindManager wind;
        [SerializeField] LeafDrift leaves;
        [SerializeField] AmbienceController ambience;
        [SerializeField] AnimationDebugOverlay animationOverlay;
        [SerializeField] bool visibleOnStart = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool visible;

        void Awake() => visible = visibleOnStart;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(10, 10, 270, 330), GUI.skin.box);
            GUILayout.Label("Breeze demo (F2 to hide)");
            if (wind != null)
            {
                GUILayout.Label($"Strength {wind.Strength:0.00}  Gust {wind.Gust:0.00}  {wind.Quality}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Calm")) wind.ApplyPreset(WindPreset.Calm);
                if (GUILayout.Button("Breeze")) wind.ApplyPreset(WindPreset.Breeze);
                if (GUILayout.Button("Strong")) wind.ApplyPreset(WindPreset.Strong);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Gust now")) wind.TriggerGust();
                if (GUILayout.Button(wind.WindEnabled ? "Wind: on" : "Wind: off")) wind.SetEnabled(!wind.WindEnabled);
                GUILayout.EndHorizontal();
                if (GUILayout.Button($"Quality: {wind.Quality} (tap to cycle)"))
                    wind.SetQuality(wind.Quality == WindQuality.High ? WindQuality.Low : wind.Quality == WindQuality.Low ? WindQuality.Off : WindQuality.High);
            }
            if (leaves != null && GUILayout.Button(leaves.LeavesEnabled ? "Leaves: on" : "Leaves: off")) leaves.SetEnabled(!leaves.LeavesEnabled);
            if (ambience != null)
            {
                string label = ambience.HasAudio ? (ambience.AmbienceEnabled ? "Ambience: on" : "Ambience: off") : "Ambience: no clips assigned";
                if (GUILayout.Button(label)) ambience.SetEnabled(!ambience.AmbienceEnabled);
            }
            if (animationOverlay != null && GUILayout.Button(animationOverlay.Visible ? "Animation readout: on" : "Animation readout: off"))
                animationOverlay.Visible = !animationOverlay.Visible;
            GUILayout.EndArea();
        }
#endif
    }
}
