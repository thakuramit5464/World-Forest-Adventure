using System.Collections.Generic;
using System.Text;
using Greenhaven.Player;
using Greenhaven.Wind;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenhaven.Wildlife
{
    public enum WildlifeQuality { Low, High }

    /// <summary>
    /// The one wildlife coordinator in a scene. It applies density, runs every animal's decisions at a
    /// distance-based, staggered rate, moves and animates nearby animals once per frame (visible ones only),
    /// pauses far ones, keeps animals off trails (blocked cells baked by the placement tool), and plays
    /// optional calls through two pooled audio sources. F4 shows a debug readout in the Editor and
    /// development builds.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class WildlifeManager : MonoBehaviour
    {
        [SerializeField] bool wildlifeEnabled = true;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of placed animals that stay active (applied at start).")] float density = 1f;
        [SerializeField] WildlifeQuality quality = WildlifeQuality.High;
        [SerializeField, Min(10f), Tooltip("Animals further than this from the player pause completely.")] float activeDistance = 70f;
        [SerializeField, Min(5f), Tooltip("Within this distance animals think 10 times a second (High).")] float nearDistance = 25f;
        [SerializeField] LayerMask groundMask = ~(1 << 2);
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);
        [SerializeField, Tooltip("Size of the blocked cells in metres.")] float cellSize = 1.5f;
        [SerializeField, Tooltip("Cells animals keep out of (trails, paths). Filled by Greenhaven ▸ Wildlife ▸ Populate Current Scene.")]
        List<Vector2Int> blockedCells = new List<Vector2Int>();
        [SerializeField, Range(0f, 1f)] float callVolume = 1f;

        public static WildlifeManager Current { get; private set; }
        static readonly List<WildlifeAgent> agents = new List<WildlifeAgent>();

        public LayerMask GroundMask => groundMask;
        public LayerMask ObstacleMask => obstacleMask;
        public IReadOnlyList<WildlifeAgent> Agents => agents;
        public bool WildlifeEnabled => wildlifeEnabled;

        readonly HashSet<long> blocked = new HashSet<long>();
        readonly Collider[] overlap = new Collider[8];
        Transform player;
        PlayerMotor motor;
        AudioSource[] voices;
        float nextCall;
        bool densityApplied, hidden, showDebug;
        GUIStyle debugStyle;
        readonly StringBuilder debugText = new StringBuilder(512);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            agents.Clear();
            Current = null;
        }

        public static void Register(WildlifeAgent agent)
        {
            if (!agents.Contains(agent)) agents.Add(agent);
            if (Current == null && Application.isPlaying)
            {
                // A scene with animals but no manager still works: create exactly one.
                WildlifeManager existing = FindFirstObjectByType<WildlifeManager>();
                if (existing == null) new GameObject("WildlifeManager (auto)").AddComponent<WildlifeManager>();
            }
        }

        public static void Unregister(WildlifeAgent agent) => agents.Remove(agent);

        /// <summary>Walkable ground: terrain and non-convex mesh colliders (rocks use convex colliders, so they are obstacles).</summary>
        public static bool IsWalkable(Collider c) => c is TerrainCollider || (c is MeshCollider mc && !mc.convex);

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("Only one WildlifeManager is used per scene; this one is disabled.", this);
                enabled = false;
                return;
            }
            Current = this;
            blocked.Clear();
            foreach (Vector2Int c in blockedCells) blocked.Add(Key(c.x, c.y));
            voices = new AudioSource[2];
            for (int i = 0; i < voices.Length; i++)
            {
                var go = new GameObject($"Wildlife Voice {i}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2f;
                source.maxDistance = 28f;
                voices[i] = source;
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            motor = FindFirstObjectByType<PlayerMotor>();
            player = motor != null ? motor.transform : null;
        }

        /// <summary>Turns all wildlife on or off at runtime (also available as the Inspector toggle).</summary>
        public void SetEnabled(bool on) => wildlifeEnabled = on;

        public void SetQuality(WildlifeQuality value) => quality = value;

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        public bool IsBlocked(Vector3 p) => blocked.Count > 0 && blocked.Contains(Key(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize)));

        /// <summary>True if anything solid other than walkable ground overlaps a body-sized sphere standing at <paramref name="ground"/>.</summary>
        public bool Obstructed(Vector3 ground, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(ground + Vector3.up * (radius + 0.15f), radius, overlap, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!IsWalkable(overlap[i]) && overlap[i].GetComponentInParent<PlayerMotor>() == null) return true;
            return false;
        }

        void ApplyDensity()
        {
            densityApplied = true;
            if (density >= 0.999f) return;
            var list = new List<WildlifeAgent>(agents);
            list.Sort((a, b) => Hash(a.Seed).CompareTo(Hash(b.Seed)));
            int keep = Mathf.RoundToInt(list.Count * density);
            for (int i = keep; i < list.Count; i++) list[i].gameObject.SetActive(false);
        }

        static uint Hash(int seed) => unchecked((uint)seed * 2654435761u);

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame) showDebug = !showDebug;
#endif
            if (!densityApplied) ApplyDensity();
            if (!wildlifeEnabled)
            {
                if (!hidden) SetRenderers(false);
                return;
            }
            if (hidden) SetRenderers(true);

            float dt = Time.deltaTime, now = Time.time;
            Vector3 p = player != null ? player.position : new Vector3(0f, -10000f, 0f);
            bool running = motor != null && motor.IsRunning;
            WindManager wind = WindManager.Current;
            float windStrength = wind != null && wind.WindEnabled ? wind.Strength : 0f;
            bool high = quality == WildlifeQuality.High;
            float near2 = nearDistance * nearDistance, active2 = activeDistance * activeDistance;

            for (int i = agents.Count - 1; i >= 0; i--)
            {
                WildlifeAgent a = agents[i];
                if (!a.begun)
                {
                    a.Begin(this);
                    a.nextThink = now + (Hash(a.Seed) % 1000) * 0.0003f; // stagger the first decisions
                }
                Vector3 offset = a.transform.position - p;
                float d2 = offset.x * offset.x + offset.z * offset.z;
                if (player != null && d2 > active2) continue; // far away: paused

                if (now >= a.nextThink)
                {
                    float interval = d2 < near2 ? (high ? 0.1f : 0.2f) : d2 < near2 * 4f ? (high ? 0.3f : 0.5f) : (high ? 0.8f : 1.2f);
                    a.Think(now - a.lastThink, p, running, player != null);
                    a.lastThink = now;
                    a.nextThink = now + interval * (0.85f + (Hash(a.Seed) % 100) * 0.003f);
                }
                a.Step(dt);
                if (a.Motion.Rig != null && a.Motion.Rig.IsVisible) a.Motion.Animate(dt, now, windStrength, high);
            }
        }

        void SetRenderers(bool on)
        {
            hidden = !on;
            foreach (WildlifeAgent a in agents)
                if (a.Motion != null && a.Motion.Rig != null && a.Motion.Rig.meshRenderer != null) a.Motion.Rig.meshRenderer.enabled = on;
        }

        /// <summary>An animal would like to call; plays only if it has clips, is near the player and no other call played recently.</summary>
        public void RequestCall(WildlifeAgent agent)
        {
            WildlifeSpecies s = agent.Species;
            if (s == null || s.calls == null || s.calls.Length == 0 || Time.time < nextCall || player == null) return;
            if ((agent.transform.position - player.position).sqrMagnitude > 28f * 28f) return;
            AudioClip clip = s.calls[Random.Range(0, s.calls.Length)];
            if (clip == null) return;
            AudioSource voice = voices[0].isPlaying ? voices[1] : voices[0];
            if (voice.isPlaying) return; // never overlap more than two calls
            voice.transform.position = agent.transform.position;
            voice.pitch = Random.Range(0.92f, 1.08f);
            voice.PlayOneShot(clip, s.callVolume * callVolume);
            nextCall = Time.time + 3f;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnGUI()
        {
            if (!showDebug) return;
            debugStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16 };
            debugText.Clear();
            debugText.Append($"Wildlife: {agents.Count} animals, quality {quality}, density {density:0.00}{(wildlifeEnabled ? "" : " (disabled)")}\n");
            Vector3 p = player != null ? player.position : Vector3.zero;
            int shown = 0;
            foreach (WildlifeAgent a in agents)
            {
                float d = Vector3.Distance(a.transform.position, p);
                if (d > nearDistance * 1.5f || shown >= 10) continue;
                debugText.Append($"{a.name}: {a.State}  {d:0.0} m  bold {a.Boldness:0.00}\n");
                shown++;
            }
            debugText.Append("(F4 to hide)");
            GUI.Box(new Rect(10, Screen.height - 280, 460, 270), debugText.ToString(), debugStyle);
        }
#endif
    }
}
