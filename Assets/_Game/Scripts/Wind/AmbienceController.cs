using UnityEngine;

namespace Greenhaven.Wind
{
    /// <summary>
    /// Optional ambient audio tied to the shared wind: a wind loop that swells with strength and birdsong
    /// that quietens in strong wind. Both clips are optional (the project ships none); with no clips this
    /// does nothing. Enabling/disabling fades smoothly instead of cutting.
    /// </summary>
    public sealed class AmbienceController : MonoBehaviour
    {
        [SerializeField, Tooltip("Looping wind clip (optional).")] AudioSource windSource;
        [SerializeField, Tooltip("Looping birdsong clip (optional).")] AudioSource birdSource;
        [SerializeField, Range(0f, 1f)] float windMinVolume = 0.08f;
        [SerializeField, Range(0f, 1f)] float windMaxVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float birdVolume = 0.35f;
        [SerializeField, Min(0.1f)] float fadeSeconds = 1.5f;
        [SerializeField] bool ambienceEnabled = true;

        float master; // 0..1 fade

        public bool AmbienceEnabled => ambienceEnabled;
        public bool HasAudio => (windSource != null && windSource.clip != null) || (birdSource != null && birdSource.clip != null);

        public void SetEnabled(bool on) => ambienceEnabled = on;

        void Awake()
        {
            foreach (AudioSource s in new[] { windSource, birdSource })
            {
                if (s == null) continue;
                s.loop = true;
                s.playOnAwake = false;
                s.volume = 0f;
            }
            if (!HasAudio) Debug.Log("Greenhaven ambience: no wind/birdsong clips assigned; ambience is silent (optional).", this);
        }

        void Update()
        {
            master = Mathf.MoveTowards(master, ambienceEnabled ? 1f : 0f, Time.deltaTime / fadeSeconds);
            WindManager wind = WindManager.Current;
            float strength = wind != null && wind.WindEnabled ? wind.Strength : 0f;
            Drive(windSource, master * Mathf.Lerp(windMinVolume, windMaxVolume, strength));
            Drive(birdSource, master * birdVolume * (1f - 0.7f * strength));
        }

        static void Drive(AudioSource source, float volume)
        {
            if (source == null || source.clip == null) return;
            source.volume = volume;
            if (volume > 0.001f && !source.isPlaying) source.Play();
            else if (volume <= 0.001f && source.isPlaying) source.Stop();
        }
    }
}
