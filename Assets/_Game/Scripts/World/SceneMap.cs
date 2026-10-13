using UnityEngine;

namespace Greenhaven.World
{
    /// <summary>The scene's map picture (captured top-down by the scene builder) and the world area it covers.</summary>
    public sealed class SceneMap : MonoBehaviour
    {
        [SerializeField] string title = "Greenhaven";
        [SerializeField] Sprite map;
        [SerializeField, Tooltip("World X/Z area the picture covers (x = min X, y = min Z).")] Rect worldRect = new Rect(-50f, -50f, 100f, 100f);

        public static SceneMap Current { get; private set; }

        public string Title => title;
        public Sprite Map => map;
        public Rect WorldRect => worldRect;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>World position → 0..1 across the map picture.</summary>
        public Vector2 ToMap(Vector3 world) =>
            new Vector2(Mathf.InverseLerp(worldRect.xMin, worldRect.xMax, world.x), Mathf.InverseLerp(worldRect.yMin, worldRect.yMax, world.z));
    }
}
