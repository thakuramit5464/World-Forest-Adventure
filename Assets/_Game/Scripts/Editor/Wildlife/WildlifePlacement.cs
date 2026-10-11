using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Greenhaven.Editor.Cloudpeak;
using Greenhaven.Player;
using Greenhaven.Wildlife;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.Wildlife
{
    /// <summary>
    /// Places generated animals in the open scene (Greenhaven ▸ Wildlife ▸ Populate Current Scene) under one
    /// "Generated Wildlife" root, which Clear removes without touching anything else. Deterministic for a
    /// given seed and scene. Paths and trails (meshes named Trail / PathsAndSquare / Path) become blocked
    /// cells that animals keep out of at runtime and never spawn near. In CloudpeakAdventure the habitats
    /// come from the generated world (glade deer, forest rabbits and foxes, ledge goats, perching birds);
    /// in other scenes they are inferred from the generated bushes, trees, rocks and props.
    /// </summary>
    public static class WildlifePlacement
    {
        public const string RootName = "Generated Wildlife";
        const float CellSize = 1.5f;
        static readonly string[] PathMeshNames = { "Trail", "PathsAndSquare", "Path" };

        [Serializable]
        public struct Counts
        {
            public int Foxes, Deer, Rabbits, Goats, Birds;
            public static Counts Default => new Counts { Foxes = 2, Deer = 4, Rabbits = 5, Goats = 3, Birds = 6 };
            public Counts Scaled(float d) => new Counts
            {
                Foxes = Mathf.RoundToInt(Foxes * d), Deer = Mathf.RoundToInt(Deer * d), Rabbits = Mathf.RoundToInt(Rabbits * d),
                Goats = Mathf.RoundToInt(Goats * d), Birds = Mathf.RoundToInt(Birds * d),
            };
        }

        /// <summary>State shared while placing one scene.</summary>
        public sealed class Context
        {
            public System.Random Rng;
            public int Seed, Index;
            public Transform Root;
            public Vector3? Player;
            public readonly HashSet<long> SpawnBlocked = new HashSet<long>();
            public readonly List<Vector2Int> RuntimeBlocked = new List<Vector2Int>();
            public readonly List<Vector3> Placed = new List<Vector3>();
            public readonly List<Vector3> Bushes = new List<Vector3>(), Trees = new List<Vector3>(), Rocks = new List<Vector3>();
            public readonly List<Vector3> Perches = new List<Vector3>(), Canopies = new List<Vector3>();
            public readonly Dictionary<AnimalKind, int> Spawned = new Dictionary<AnimalKind, int>();
            public float Range(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
        }

        // ───────────────────────────── Menus ─────────────────────────────

        [MenuItem("Greenhaven/Wildlife/Populate Current Scene", priority = 80)]
        static void PopulateMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Populate wildlife", "Stop Play mode first.", "OK");
                return;
            }
            if (WildlifeGenerator.Kinds.All(k => WildlifeGenerator.VariantsOnDisk(k) == 0))
            {
                EditorUtility.DisplayDialog("Populate wildlife", "No wildlife prefabs yet. Run Greenhaven ▸ Wildlife ▸ Generate Wildlife first.", "OK");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            bool hasOld = scene.GetRootGameObjects().Any(g => g.name == RootName);
            if (!EditorUtility.DisplayDialog("Populate wildlife",
                    $"Place animals in '{scene.name}'? They go under a '{RootName}' object{(hasOld ? " (the existing one is replaced)" : "")}; nothing else changes. " +
                    "Undo (Ctrl+Z) removes them again, so you can preview the placement.", "Populate", "Cancel"))
                return;
            CloudpeakWorld world = scene.name == "CloudpeakAdventure" ? CloudpeakWorld.Generate(CloudpeakWindow.SavedSettings) : null;
            string report = Populate(Counts.Default, 1f, WildlifeGeneratorSettings.LoadOrCreate().seed, world);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Greenhaven — Populate wildlife:\n" + report);
            EditorUtility.DisplayDialog("Populate wildlife", report + "\n\nSave the scene to keep them.", "OK");
        }

        [MenuItem("Greenhaven/Wildlife/Clear Generated Wildlife", priority = 81)]
        static void ClearMenu()
        {
            int removed = Clear(true);
            EditorUtility.DisplayDialog("Clear wildlife", removed > 0 ? $"Removed '{RootName}' ({removed} animals). Undo restores it." : $"No '{RootName}' object in this scene.", "OK");
        }

        /// <summary>Removes only the generated wildlife root. Returns the number of animals removed.</summary>
        public static int Clear(bool undoable)
        {
            int removed = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == RootName).ToArray())
            {
                removed += root.GetComponentsInChildren<WildlifeAgent>(true).Length;
                if (undoable) Undo.DestroyObjectImmediate(root);
                else Object.DestroyImmediate(root);
            }
            if (removed > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return removed;
        }

        // ───────────────────────────── Populate ─────────────────────────────

        /// <summary>Places animals in the active scene. <paramref name="world"/> enables Cloudpeak habitats.</summary>
        public static string Populate(Counts counts, float density, int seed, CloudpeakWorld world)
        {
            Clear(true);
            Physics.SyncTransforms();
            Context ctx = Begin(seed);
            counts = counts.Scaled(Mathf.Clamp(density, 0f, 2f));
            if (world != null) PopulateCloudpeak(ctx, counts, world);
            else PopulateGeneric(ctx, counts);
            return Finish(ctx);
        }

        public static Context Begin(int seed)
        {
            var ctx = new Context { Rng = new System.Random(seed ^ 0x5A11D), Seed = seed };
            var rootGo = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootGo, "Populate wildlife");
            ctx.Root = rootGo.transform;
            PlayerMotor player = Object.FindFirstObjectByType<PlayerMotor>();
            if (player != null) ctx.Player = player.transform.position;
            BakeBlockedCells(ctx);
            CollectAnchors(ctx);
            return ctx;
        }

        public static string Finish(Context ctx)
        {
            // One manager per scene: reuse an existing one, otherwise put it on the wildlife root.
            WildlifeManager manager = Object.FindObjectsByType<WildlifeManager>(FindObjectsSortMode.None).FirstOrDefault();
            if (manager == null) manager = ctx.Root.gameObject.AddComponent<WildlifeManager>();
            var so = new SerializedObject(manager);
            SerializedProperty cells = so.FindProperty("blockedCells");
            cells.arraySize = ctx.RuntimeBlocked.Count;
            for (int i = 0; i < ctx.RuntimeBlocked.Count; i++) cells.GetArrayElementAtIndex(i).vector2IntValue = ctx.RuntimeBlocked[i];
            so.FindProperty("cellSize").floatValue = CellSize;
            so.ApplyModifiedPropertiesWithoutUndo();

            var sb = new StringBuilder();
            int total = ctx.Spawned.Values.Sum();
            sb.AppendLine($"Placed {total} animals under '{RootName}': " +
                          string.Join(", ", WildlifeGenerator.Kinds.Select(k => $"{(ctx.Spawned.TryGetValue(k, out int n) ? n : 0)} {k.ToString().ToLower()}")) + ".");
            sb.AppendLine($"Trails/paths kept clear: {ctx.RuntimeBlocked.Count} blocked cells. Perches found: {ctx.Perches.Count + ctx.Canopies.Count}.");
            sb.Append("Animals have no colliders (they cannot push the player). Tune counts with the WildlifeManager's Density, species in Generated/Wildlife/Species.");
            return sb.ToString();
        }

        static void PopulateCloudpeak(Context ctx, Counts c, CloudpeakWorld w)
        {
            float R = w.Settings.MountainRadius;
            float T(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), w.Mountain) / R;
            Vector3 Around(Vector2 centre, float min, float max)
            {
                float a = ctx.Range(0f, Mathf.PI * 2f), r = ctx.Range(min, max);
                return new Vector3(centre.x + Mathf.Cos(a) * r, 0f, centre.y + Mathf.Sin(a) * r);
            }
            bool OffTrail(Vector3 p, float m) => w.DistanceToTrailEdge(new Vector2(p.x, p.z), m + 2f) >= m;

            // Deer: a small herd around the Whispering Glade, the rest in the open foothills.
            int herd = Mathf.Min(c.Deer, Mathf.Max(1, c.Deer - 1));
            SpawnGroup(ctx, AnimalKind.Deer, herd, () => Around(w.Glade, 8f, 16f), p => OffTrail(p, 4f), 2.5f);
            SpawnGroup(ctx, AnimalKind.Deer, c.Deer - herd, () => Around(w.FoothillRest, 10f, 22f), p => OffTrail(p, 4f) && T(p) > 0.8f, 2.5f);
            // Rabbits: beside forest bushes.
            var forestBushes = ctx.Bushes.Where(b => T(b) > 1.0f && OffTrail(b, 2f)).ToList();
            SpawnGroup(ctx, AnimalKind.Rabbit, c.Rabbits, () => Near(ctx, forestBushes, 0.8f, 2.5f), p => OffTrail(p, 2f), 2f);
            // Foxes: quiet spots along the forest trail, a few metres off it.
            List<TrailPoint> forestTrail = w.Trail.Where(t => t.Section == TrailSection.Forest).ToList();
            SpawnGroup(ctx, AnimalKind.Fox, c.Foxes, () =>
            {
                TrailPoint t = forestTrail[ctx.Rng.Next(forestTrail.Count)];
                return Around(t.Position, t.HalfWidth + 4f, t.HalfWidth + 10f);
            }, p => OffTrail(p, 4f) && Vector3.Distance(p, new Vector3(w.Spawn.x, p.y, w.Spawn.y)) > 15f, 8f);
            // Goats: the shelf around the Eagle Ledge and rocky slopes of the upper mountain.
            SpawnGroup(ctx, AnimalKind.Goat, c.Goats, () => ctx.Rng.NextDouble() < 0.5 ? Around(w.Ledge, 5f, 14f) : Around(w.Mountain, R * 0.2f, R * 0.5f),
                p => OffTrail(p, 2.5f) && T(p) < 0.62f, 3f);
            // Birds: props and rocks near the points of interest, and tree tops near the trail.
            var perchPool = ctx.Perches.Concat(ctx.Canopies.Where(p => w.DistanceToTrailEdge(new Vector2(p.x, p.z), 14f) < 12f)).ToList();
            SpawnBirds(ctx, c.Birds, perchPool);
        }

        static void PopulateGeneric(Context ctx, Counts c)
        {
            Vector3 centre = ctx.Player ?? Vector3.zero;
            Vector3 Anywhere(float radius)
            {
                float a = ctx.Range(0f, Mathf.PI * 2f), r = radius * Mathf.Sqrt((float)ctx.Rng.NextDouble());
                return centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            float reach = 60f;
            SpawnGroup(ctx, AnimalKind.Deer, c.Deer, () => ctx.Trees.Count > 0 ? Near(ctx, ctx.Trees, 4f, 9f) : Anywhere(reach), _ => true, 2.5f);
            SpawnGroup(ctx, AnimalKind.Rabbit, c.Rabbits, () => ctx.Bushes.Count > 0 ? Near(ctx, ctx.Bushes, 0.8f, 2.5f) : Anywhere(reach), _ => true, 2f);
            SpawnGroup(ctx, AnimalKind.Fox, c.Foxes, () => ctx.Trees.Count > 0 ? Near(ctx, ctx.Trees, 2f, 6f) : Anywhere(reach), _ => true, 8f);
            // Goats on the highest walkable ground around (or near rocks).
            float highest = float.MinValue;
            var high = new List<Vector3>();
            for (int i = 0; i < 200; i++)
            {
                Vector3 p = Anywhere(reach);
                if (Ground(p, out RaycastHit hit)) high.Add(hit.point);
            }
            if (high.Count > 0) highest = high.Max(h => h.y);
            var top = high.Where(h => h.y > highest - 3f).ToList();
            SpawnGroup(ctx, AnimalKind.Goat, c.Goats, () => top.Count > 0 && ctx.Rng.NextDouble() < 0.6 ? top[ctx.Rng.Next(top.Count)] : ctx.Rocks.Count > 0 ? Near(ctx, ctx.Rocks, 1.5f, 4f) : Anywhere(reach), _ => true, 3f);
            SpawnBirds(ctx, c.Birds, ctx.Perches.Concat(ctx.Canopies).Where(p => (p - centre).sqrMagnitude < 80f * 80f).ToList());
        }

        static Vector3 Near(Context ctx, List<Vector3> anchors, float min, float max)
        {
            if (anchors.Count == 0) return new Vector3(float.MaxValue, 0f, 0f);
            Vector3 a = anchors[ctx.Rng.Next(anchors.Count)];
            float ang = ctx.Range(0f, Mathf.PI * 2f), r = ctx.Range(min, max);
            return a + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        }

        // ───────────────────────────── Spawning ─────────────────────────────

        /// <summary>Spawns up to <paramref name="count"/> animals at valid samples. Returns how many were placed.</summary>
        public static int SpawnGroup(Context ctx, AnimalKind kind, int count, Func<Vector3> sample, Func<Vector3, bool> accept, float spacing)
        {
            if (count <= 0 || WildlifeGenerator.VariantsOnDisk(kind) == 0) return 0;
            WildlifeSpecies species = WildlifeGenerator.EnsureSpecies(kind);
            int placed = 0;
            for (int attempt = 0; attempt < count * 60 && placed < count; attempt++)
            {
                Vector3 p = sample();
                if (float.IsInfinity(p.x) || p.x > 1e6f || !accept(p)) continue;
                if (!Valid(ctx, p, species, spacing, out Vector3 ground)) continue;
                Spawn(ctx, kind, ground, ctx.Range(0f, 360f), null);
                placed++;
            }
            return placed;
        }

        static void SpawnBirds(Context ctx, int count, List<Vector3> pool)
        {
            if (count <= 0 || pool.Count == 0 || WildlifeGenerator.VariantsOnDisk(AnimalKind.Bird) == 0) return;
            var used = new HashSet<int>();
            for (int i = 0; i < count && used.Count < pool.Count; i++)
            {
                int start = ctx.Rng.Next(pool.Count);
                int pick = -1;
                for (int k = 0; k < pool.Count; k++)
                {
                    int j = (start + k) % pool.Count;
                    if (!used.Contains(j) && !IsSpawnBlocked(ctx, pool[j]) && (ctx.Player == null || (pool[j] - ctx.Player.Value).sqrMagnitude > 25f)) { pick = j; break; }
                }
                if (pick < 0) break;
                used.Add(pick);
                // Two or three other perches within reach to flutter between.
                Vector3 home = pool[pick];
                Vector3[] perches = new[] { home }.Concat(pool.Where((p, j) => j != pick && (p - home).sqrMagnitude < 25f * 25f)
                    .OrderBy(_ => ctx.Rng.Next()).Take(3)).ToArray();
                Spawn(ctx, AnimalKind.Bird, home, ctx.Range(0f, 360f), perches);
            }
        }

        static bool Ground(Vector3 p, out RaycastHit hit) =>
            Physics.Raycast(new Vector3(p.x, p.y + 200f, p.z), Vector3.down, out hit, 400f, ~(1 << 2), QueryTriggerInteraction.Ignore) && WildlifeManager.IsWalkable(hit.collider);

        static bool Valid(Context ctx, Vector3 p, WildlifeSpecies species, float spacing, out Vector3 ground)
        {
            ground = p;
            if (!Ground(p, out RaycastHit hit) || Vector3.Angle(hit.normal, Vector3.up) > species.maxSlope) return false;
            ground = hit.point;
            if (IsSpawnBlocked(ctx, ground)) return false;
            if (ctx.Player.HasValue && (ground - ctx.Player.Value).sqrMagnitude < 8f * 8f) return false;
            foreach (Vector3 q in ctx.Placed)
                if ((q - ground).sqrMagnitude < spacing * spacing) return false;
            // Nothing solid at the spot (rocks, trunks, props, buildings) with some room to move.
            float r = species.bodyRadius * 1.6f + 0.2f;
            foreach (Collider c in Physics.OverlapSphere(ground + Vector3.up * (r + 0.15f), r, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (!WildlifeManager.IsWalkable(c)) return false;
            return true;
        }

        public static GameObject Spawn(Context ctx, AnimalKind kind, Vector3 position, float yaw, Vector3[] perches)
        {
            int variants = WildlifeGenerator.VariantsOnDisk(kind);
            GameObject prefab = WildlifeGenerator.LoadPrefab(kind, ctx.Rng.Next(variants));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ctx.Root);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.name = $"{prefab.name} #{++ctx.Index}";
            WildlifeAgent agent = go.GetComponent<WildlifeAgent>();
            agent.Configure(WildlifeGenerator.EnsureSpecies(kind), ctx.Seed * 31 + ctx.Index * 7919, perches);
            EditorUtility.SetDirty(agent);
            ctx.Placed.Add(position);
            ctx.Spawned[kind] = (ctx.Spawned.TryGetValue(kind, out int n) ? n : 0) + 1;
            return go;
        }

        // ───────────────────────────── Scene analysis ─────────────────────────────

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        static bool IsSpawnBlocked(Context ctx, Vector3 p) => ctx.SpawnBlocked.Contains(Key(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize)));

        /// <summary>Rasterises the path/trail meshes into cells: runtime cells (path + 1 cell) and spawn cells (path + 3 cells).</summary>
        static void BakeBlockedCells(Context ctx)
        {
            var path = new HashSet<long>();
            var pathCells = new List<Vector2Int>();
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (!PathMeshNames.Contains(mf.gameObject.name) || mf.sharedMesh == null) continue;
                Vector3[] v = mf.sharedMesh.vertices;
                int[] tris = mf.sharedMesh.triangles;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(v[tris[t]]), b = m.MultiplyPoint3x4(v[tris[t + 1]]), c = m.MultiplyPoint3x4(v[tris[t + 2]]);
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / CellSize), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / CellSize);
                    int z0 = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / CellSize), z1 = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / CellSize);
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                            if (path.Add(Key(x, z))) pathCells.Add(new Vector2Int(x, z));
                }
            }
            var runtime = new HashSet<long>();
            foreach (Vector2Int c in pathCells)
                for (int dx = -3; dx <= 3; dx++)
                    for (int dz = -3; dz <= 3; dz++)
                    {
                        long k = Key(c.x + dx, c.y + dz);
                        ctx.SpawnBlocked.Add(k);
                        if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1 && runtime.Add(k)) ctx.RuntimeBlocked.Add(new Vector2Int(c.x + dx, c.y + dz));
                    }
        }

        /// <summary>Finds generated bushes, trees and rocks, plus perch points on rocks, stumps, logs, fences, benches, signs and tree tops.</summary>
        static void CollectAnchors(Context ctx)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source == null) continue;
                string n = source.name;
                if (n.StartsWith("Gen_Bush")) ctx.Bushes.Add(t.position);
                else if (n.StartsWith("Gen_Broadleaf") || n.StartsWith("Gen_Pine"))
                {
                    ctx.Trees.Add(t.position);
                    Renderer r = t.GetComponentInChildren<Renderer>();
                    if (r != null && n.StartsWith("Gen_Broadleaf") && ctx.Canopies.Count < 400)
                    {
                        Bounds b = r.bounds;
                        ctx.Canopies.Add(new Vector3(b.center.x + ctx.Range(-0.2f, 0.2f) * b.extents.x, b.max.y - b.size.y * 0.12f, b.center.z + ctx.Range(-0.2f, 0.2f) * b.extents.z));
                    }
                }
                if (n.StartsWith("Gen_Rock")) ctx.Rocks.Add(t.position);
                if (n.StartsWith("Gen_Rock") || n.StartsWith("Gen_Stump") || n.StartsWith("Gen_Log") || n.StartsWith("Gen_Fence") || n.StartsWith("Gen_Bench") || n.StartsWith("Gen_Signpost"))
                {
                    Collider c = t.GetComponentInChildren<Collider>();
                    if (c == null || c.isTrigger) continue;
                    Bounds b = c.bounds;
                    if (b.size.y > 3f) continue;
                    var top = new Vector3(b.center.x, b.max.y + 0.5f, b.center.z);
                    if (c.Raycast(new Ray(top, Vector3.down), out RaycastHit hit, 2f)) ctx.Perches.Add(hit.point);
                }
            }
        }
    }
}
