using UnityEngine;

namespace Greenhaven.Wind
{
    /// <summary>
    /// A few drifting leaves around the camera, carried by the shared wind: emission rises with gusts and
    /// they travel along the wind direction. One reused, world-space ParticleSystem; settings are applied
    /// once and only velocity/emission values change per frame (no allocations).
    /// </summary>
    public sealed class LeafDrift : MonoBehaviour
    {
        [SerializeField, Tooltip("Defaults to the main camera.")] Transform target;
        [SerializeField, Min(2f)] float radius = 12f;
        [SerializeField, Min(10)] int maxParticles = 40;
        [SerializeField, Min(0f), Tooltip("Leaves per second in a gentle breeze.")] float baseEmission = 1.5f;
        [SerializeField, Min(0f), Tooltip("Extra leaves per second at a full gust.")] float gustEmission = 6f;
        [SerializeField, Min(0.1f)] float leafSize = 0.16f;
        [SerializeField] Color colourA = new Color(0.45f, 0.7f, 0.25f);
        [SerializeField] Color colourB = new Color(0.95f, 0.75f, 0.3f);
        [SerializeField, Tooltip("Small leaf mesh (created by the Breeze Demo builder). Billboards are used if empty.")] Mesh leafMesh;
        [SerializeField, Tooltip("URP Particles/Unlit material (created by the Breeze Demo builder).")] Material leafMaterial;
        [SerializeField] bool leavesEnabled = true;

        ParticleSystem leaves;
        ParticleSystem.EmissionModule emission;
        ParticleSystem.VelocityOverLifetimeModule velocity;

        public bool LeavesEnabled => leavesEnabled;

        public void SetEnabled(bool on)
        {
            leavesEnabled = on;
            if (!on && leaves != null) leaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void Awake()
        {
            if (target == null && Camera.main != null) target = Camera.main.transform;
            leaves = GetComponentInChildren<ParticleSystem>();
            if (leaves == null) leaves = new GameObject("Leaves").AddComponent<ParticleSystem>();
            leaves.transform.SetParent(transform, false);
            Configure(leaves);
            emission = leaves.emission;
            velocity = leaves.velocityOverLifetime;
        }

        /// <summary>Applies the inspector settings (also used by the editor scene builder).</summary>
        public void Configure(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(leafSize * 0.7f, leafSize);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 2f, 4f, radius * 2f);
            shape.position = new Vector3(0f, 4f, 0f);

            ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.y = new ParticleSystem.MinMaxCurve(-0.7f, -0.4f);
            v.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Tumbling.
            ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
            spin.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
            spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

            // Gentle side-to-side flutter.
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = leafMesh != null ? ParticleSystemRenderMode.Mesh : ParticleSystemRenderMode.Billboard;
            if (leafMesh != null) renderer.mesh = leafMesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (leafMaterial != null) renderer.sharedMaterial = leafMaterial;

            ParticleSystem.EmissionModule e = ps.emission;
            e.enabled = true;
            e.rateOverTime = 0f;
        }

        void LateUpdate()
        {
            WindManager wind = WindManager.Current;
            bool active = leavesEnabled && wind != null && wind.WindEnabled && wind.Quality != WindQuality.Off;
            if (!active)
            {
                if (leaves.isPlaying) leaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            if (!leaves.isPlaying) leaves.Play();
            if (target != null) transform.position = target.position;

            float strength = wind.Strength;
            emission.rateOverTime = (baseEmission + gustEmission * wind.Gust) * (wind.Quality == WindQuality.Low ? 0.5f : 1f);
            Vector3 carry = wind.Direction * (0.6f + strength * 3.5f);
            velocity.x = new ParticleSystem.MinMaxCurve(carry.x * 0.7f, carry.x * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(carry.z * 0.7f, carry.z * 1.2f);
        }
    }
}
