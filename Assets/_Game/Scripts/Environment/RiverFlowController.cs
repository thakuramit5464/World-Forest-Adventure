using System;
using System.Collections.Generic;
using UnityEngine;

namespace Greenhaven.Environment
{
    public enum RiverQuality { Low, Standard }

    /// <summary>A stretch of river starting at a control point, with its own flow speed (negative = reversed).</summary>
    [Serializable]
    public struct RiverSegment
    {
        [Tooltip("Control point where this segment starts (later segments must start further down).")] public int fromPoint;
        [Range(-3f, 3f), Tooltip("Flow speed multiplier; negative flows the other way.")] public float speed;
    }

    /// <summary>
    /// A generated flowing river: its shape (control points, width, depth), per-segment flow, quality, and
    /// lightweight life. The water movement itself is in the Greenhaven/RiverWater shader (driven by time,
    /// so the mesh is never rebuilt at runtime). At runtime this component only swaps the Low/Standard
    /// material, moves a small pool of floating leaves along the river, emits a few splashes at rocks,
    /// and keeps an optional ambience source near the listener. All of it pauses when the camera is far.
    /// Edit the shape in the Inspector or Scene view and press Rebuild (editor tool).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RiverFlowController : MonoBehaviour
    {
        [Header("Shape (local space — press Rebuild River after editing)")]
        public List<Vector3> controlPoints = new List<Vector3>();
        [Min(0.5f)] public float width = 3f;
        [Min(0.05f), Tooltip("Visual depth of the centre (colour and opacity).")] public float depth = 0.6f;
        [Range(3, 11)] public int acrossVertices = 7;
        [Range(0.3f, 3f), Tooltip("Metres between cross-sections.")] public float spacing = 0.75f;
        public int seed = 1;

        [Header("Flow")]
        public List<RiverSegment> segments = new List<RiverSegment> { new RiverSegment { fromPoint = 0, speed = 1f } };
        [Range(0f, 1f)] public float edgeFoam = 0.3f;
        [Range(0f, 1f)] public float bendFoam = 0.55f;
        [Range(0f, 1f)] public float rockFoam = 0.9f;
        [Range(0f, 1f), Tooltip("Extra foam where segments flow faster than 1.")] public float fastFoam = 0.5f;
        [Tooltip("In-water rocks (local space): foam, ripples and splashes form around them.")] public List<Vector3> rocks = new List<Vector3>();

        [Header("Look")]
        public RiverQuality quality = RiverQuality.Standard;
        public Material standardMaterial;
        public Material lowMaterial;
        public MeshRenderer surface;

        [Header("River life")]
        public bool floatingLeaves = true;
        [Range(0, 24)] public int leafCount = 10;
        public bool splashes = true;
        [Min(10f), Tooltip("Leaves, splashes and audio pause beyond this distance from the camera.")] public float activeDistance = 60f;
        public Mesh leafMesh;
        public Material leafMaterial;
        public Material splashMaterial;

        [Header("Audio (optional — assign a looping clip)")]
        public AudioSource ambience;

        // Baked by the editor tool: centre line at even spacing (local), signed speed factor and half width.
        [SerializeField, HideInInspector] Vector3[] path = new Vector3[0];
        [SerializeField, HideInInspector] float[] pathSpeed = new float[0];
        [SerializeField, HideInInspector] float pathSpacing = 0.75f;
        [SerializeField, HideInInspector] float baseFlowSpeed = 0.9f;

        struct Leaf
        {
            public float s, lateral, spin, spinSpeed, phase;
        }

        ParticleSystem leafSystem, splashSystem;
        ParticleSystem.Particle[] leafParticles;
        Leaf[] leaves;
        float[] rockTimers;
        System.Random rng;
        Camera cam;
        float camSearch, audioTimer;
        bool wasActive;

        public int PathCount => path.Length;
        /// <summary>Baked centre line point <paramref name="i"/> in local space.</summary>
        public Vector3 PathPoint(int i) => path[i];

        public float Length => path.Length > 1 ? (path.Length - 1) * pathSpacing : 0f;

        /// <summary>Editor: stores the evenly spaced centre line used by leaves and audio.</summary>
        public void SetBakedPath(Vector3[] localPath, float[] speedFactors, float spacingMetres, float flowSpeed)
        {
            path = localPath;
            pathSpeed = speedFactors;
            pathSpacing = spacingMetres;
            baseFlowSpeed = flowSpeed;
        }

        public void ApplyQuality()
        {
            Material m = quality == RiverQuality.Low && lowMaterial != null ? lowMaterial : standardMaterial;
            if (surface != null && m != null && surface.sharedMaterial != m) surface.sharedMaterial = m;
        }

        void OnValidate() => ApplyQuality();

        void Start()
        {
            ApplyQuality();
            rng = new System.Random(seed * 977 + 13);
            if (path.Length < 2) return;
            if (floatingLeaves && leafCount > 0 && leafMesh != null && leafMaterial != null)
            {
                leafSystem = CreateSystem("Floating Leaves", leafCount, leafMaterial, leafMesh);
                leafParticles = new ParticleSystem.Particle[leafCount];
                leaves = new Leaf[leafCount];
                for (int i = 0; i < leafCount; i++) leaves[i] = NewLeaf(Range(0f, Length));
            }
            if (splashes && rocks.Count > 0 && splashMaterial != null)
            {
                splashSystem = CreateSystem("Rock Splashes", Mathf.Min(48, rocks.Count * 6), splashMaterial, null);
                ParticleSystem.MainModule main = splashSystem.main;
                main.gravityModifier = 1.2f;
                rockTimers = new float[rocks.Count];
                for (int i = 0; i < rockTimers.Length; i++) rockTimers[i] = Range(0f, 1f);
            }
        }

        ParticleSystem CreateSystem(string name, int max, Material material, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.6f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (mesh != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = mesh;
                renderer.alignment = ParticleSystemRenderSpace.World;
                main.startRotation3D = true;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        Leaf NewLeaf(float s) => new Leaf
        {
            s = s, lateral = Range(-0.55f, 0.55f), spin = Range(0f, 360f), spinSpeed = Range(-40f, 40f), phase = Range(0f, 10f),
        };

        void Update()
        {
            if (path.Length < 2) return;
            if (cam == null)
            {
                camSearch -= Time.deltaTime;
                if (camSearch > 0f) return;
                camSearch = 2f;
                cam = Camera.main;
                if (cam == null) return;
            }
            bool active = surface == null || surface.bounds.SqrDistance(cam.transform.position) < activeDistance * activeDistance;
            if (!active)
            {
                if (wasActive)
                {
                    if (leafSystem != null) leafSystem.Clear();
                    if (ambience != null && ambience.isPlaying) ambience.Pause();
                }
                wasActive = false;
                return;
            }
            wasActive = true;
            float dt = Time.deltaTime;
            if (rng == null) rng = new System.Random(seed * 977 + 13); // caches are lost after a script reload in Play mode
            if (leafSystem != null && leaves != null && leafParticles != null) UpdateLeaves(dt);
            if (splashSystem != null && rockTimers != null) UpdateSplashes(dt);
            if (ambience != null && ambience.clip != null) UpdateAudio(dt);
        }

        /// <summary>Local point on the centre line at distance <paramref name="s"/>, with its tangent and signed speed factor.</summary>
        void Sample(float s, out Vector3 point, out Vector3 tangent, out float speed)
        {
            float f = Mathf.Clamp(s / pathSpacing, 0f, path.Length - 1.001f);
            int i = (int)f;
            float t = f - i;
            point = Vector3.LerpUnclamped(path[i], path[i + 1], t);
            tangent = (path[i + 1] - path[i]).normalized;
            speed = pathSpeed.Length == path.Length ? Mathf.Lerp(pathSpeed[i], pathSpeed[i + 1], t) : 1f;
        }

        void UpdateLeaves(float dt)
        {
            float length = Length, time = Time.time;
            float halfWidth = width * 0.5f;
            for (int i = 0; i < leaves.Length; i++)
            {
                ref Leaf leaf = ref leaves[i];
                Sample(leaf.s, out _, out _, out float speed);
                leaf.s += speed * baseFlowSpeed * 0.85f * dt;
                if (leaf.s > length) leaf = NewLeaf(Range(0f, 1.5f));
                else if (leaf.s < 0f) leaf = NewLeaf(length - Range(0f, 1.5f));
                leaf.spin += leaf.spinSpeed * dt;
                Sample(leaf.s, out Vector3 p, out Vector3 tangent, out _);
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 local = p + side * (leaf.lateral + 0.06f * Mathf.Sin(time * 0.7f + leaf.phase)) * halfWidth + Vector3.up * 0.015f;
                ref ParticleSystem.Particle particle = ref leafParticles[i];
                particle.position = transform.TransformPoint(local);
                particle.rotation3D = new Vector3(90f + 6f * Mathf.Sin(time * 1.3f + leaf.phase), leaf.spin, 0f);
                particle.startSize = 0.14f;
                particle.startColor = Color.Lerp(new Color(0.55f, 0.72f, 0.28f), new Color(0.92f, 0.68f, 0.3f), Mathf.Repeat(leaf.phase * 0.37f, 1f));
                particle.startLifetime = particle.remainingLifetime = 1000f;
            }
            leafSystem.SetParticles(leafParticles, leafParticles.Length);
        }

        void UpdateSplashes(float dt)
        {
            for (int i = 0; i < rockTimers.Length; i++)
            {
                rockTimers[i] -= dt;
                if (rockTimers[i] > 0f) continue;
                rockTimers[i] = Range(0.35f, 0.9f);
                Vector3 world = transform.TransformPoint(rocks[i]);
                Vector3 downstream = transform.TransformDirection(NearestTangent(rocks[i]));
                var emit = new ParticleSystem.EmitParams
                {
                    position = world + Vector3.up * 0.06f,
                    velocity = Vector3.up * Range(0.5f, 1.0f) + downstream * Range(0.15f, 0.45f) + new Vector3(Range(-0.2f, 0.2f), 0f, Range(-0.2f, 0.2f)),
                    startSize = Range(0.04f, 0.08f),
                    startLifetime = Range(0.35f, 0.6f),
                    startColor = new Color(0.92f, 0.97f, 1f, 0.85f),
                    applyShapeToPosition = false,
                };
                splashSystem.Emit(emit, 1);
            }
        }

        Vector3 NearestTangent(Vector3 local)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < path.Length; i += 2)
            {
                float d = (path[i] - local).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            int next = Mathf.Min(best + 1, path.Length - 1), prev = Mathf.Max(best - 1, 0);
            float sign = pathSpeed.Length == path.Length && pathSpeed[best] < 0f ? -1f : 1f;
            return (path[next] - path[prev]).normalized * sign;
        }

        void UpdateAudio(float dt)
        {
            audioTimer -= dt;
            if (audioTimer > 0f) return;
            audioTimer = 0.25f;
            // One looping source glides to the nearest point of the river, so the whole river sounds right.
            Vector3 listener = transform.InverseTransformPoint(cam.transform.position);
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < path.Length; i += 2)
            {
                float d = (path[i] - listener).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            ambience.transform.position = Vector3.Lerp(ambience.transform.position, transform.TransformPoint(path[best]), 0.5f);
            if (!ambience.isPlaying) ambience.Play();
        }

        void OnDrawGizmosSelected()
        {
            if (controlPoints.Count < 2) return;
            Gizmos.color = new Color(0.2f, 0.7f, 1f);
            for (int i = 0; i < controlPoints.Count - 1; i++)
                Gizmos.DrawLine(transform.TransformPoint(controlPoints[i]), transform.TransformPoint(controlPoints[i + 1]));
        }
    }
}
