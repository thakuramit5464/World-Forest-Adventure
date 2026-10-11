using System.Collections.Generic;
using System.Linq;
using System.Text;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Editor.WindTools;
using Greenhaven.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Greenhaven.Editor.River
{
    /// <summary>
    /// Builds flowing rivers: a smooth strip along a Catmull-Rom spline through the control points, with
    /// lengthwise UVs in metres (so flow follows every bend without seams) and per-vertex foam / speed /
    /// depth for the Greenhaven/RiverWater shader. Also upgrades existing flat water (the village stream,
    /// the Cloudpeak brook) in place, and dresses banks with pebbles, plants and in-water rocks.
    /// Generated meshes live in Generated/River and are replaced in place (same GUID).
    /// </summary>
    public static class RiverTools
    {
        public const string Root = GeneratedAssetStore.Root + "/River";
        const string MeshFolder = Root + "/Meshes";
        public const string StandardMaterialPath = Root + "/M_RiverWater.mat", LowMaterialPath = Root + "/M_RiverWater_Low.mat", SplashMaterialPath = Root + "/M_RiverSplash.mat";
        const string DetailsName = "Bank Details (generated)";
        static readonly string[] PathMeshNames = { "Trail", "PathsAndSquare", "Path" };

        // ───────────────────────────── Assets ─────────────────────────────

        static void EnsureFolders()
        {
            GeneratedAssetStore.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder(GeneratedAssetStore.Root, "River");
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(Root, "Meshes");
        }

        /// <summary>Shared water materials (created once; later tweaks in the Inspector are kept).</summary>
        public static void EnsureMaterials(out Material standard, out Material low, out Material splash)
        {
            EnsureFolders();
            Shader water = Shader.Find("Greenhaven/RiverWater");
            standard = AssetDatabase.LoadAssetAtPath<Material>(StandardMaterialPath);
            if (standard == null)
            {
                standard = new Material(water) { name = "M_RiverWater" };
                AssetDatabase.CreateAsset(standard, StandardMaterialPath);
                AssetDatabase.SetLabels(standard, new[] { GeneratedAssetStore.Label });
            }
            low = AssetDatabase.LoadAssetAtPath<Material>(LowMaterialPath);
            if (low == null)
            {
                // Low: opaque (no blending or overdraw), one ripple layer, no specular.
                low = new Material(water) { name = "M_RiverWater_Low" };
                low.SetFloat("_LowQuality", 1f);
                low.EnableKeyword("_RIVER_LOW");
                low.SetFloat("_SrcBlend", (float)BlendMode.One);
                low.SetFloat("_DstBlend", (float)BlendMode.Zero);
                low.SetFloat("_ZWrite", 1f);
                low.SetOverrideTag("RenderType", "Opaque");
                low.renderQueue = (int)RenderQueue.AlphaTest - 1;
                AssetDatabase.CreateAsset(low, LowMaterialPath);
                AssetDatabase.SetLabels(low, new[] { GeneratedAssetStore.Label });
            }
            splash = AssetDatabase.LoadAssetAtPath<Material>(SplashMaterialPath);
            if (splash == null)
            {
                Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                splash = new Material(particles) { name = "M_RiverSplash" };
                splash.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(splash, SplashMaterialPath);
                AssetDatabase.SetLabels(splash, new[] { GeneratedAssetStore.Label });
            }
        }

        // ───────────────────────────── Creating rivers ─────────────────────────────

        /// <summary>Creates a river through <paramref name="worldPoints"/> (water surface heights) and builds its mesh.</summary>
        public static RiverFlowController CreateRiver(string name, string meshKey, Transform parent, IList<Vector3> worldPoints, float width, float depth, int seed)
        {
            EnsureMaterials(out Material standard, out Material low, out Material splash);
            AmbientMotionSetup.EnsureLeafAssets(out Mesh leafMesh, out Material leafMaterial);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create river");
            go.transform.SetParent(parent, false);
            Vector3 origin = worldPoints[0];
            go.transform.SetPositionAndRotation(origin, Quaternion.identity);

            var surfaceGo = new GameObject("Water Surface");
            surfaceGo.transform.SetParent(go.transform, false);
            surfaceGo.AddComponent<MeshFilter>();
            MeshRenderer renderer = surfaceGo.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = standard;

            var audioGo = new GameObject("River Ambience (assign a looping clip)");
            audioGo.transform.SetParent(go.transform, false);
            AudioSource audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 3f;
            audio.maxDistance = 22f;
            audio.volume = 0.6f;

            RiverFlowController river = go.AddComponent<RiverFlowController>();
            river.controlPoints = worldPoints.Select(p => p - origin).ToList();
            river.width = width;
            river.depth = depth;
            river.seed = seed;
            river.surface = renderer;
            river.standardMaterial = standard;
            river.lowMaterial = low;
            river.splashMaterial = splash;
            river.leafMesh = leafMesh;
            river.leafMaterial = leafMaterial;
            river.ambience = audio;
            Rebuild(river, meshKey);
            return river;
        }

        static string MeshKey(RiverFlowController river)
        {
            string scene = river.gameObject.scene.IsValid() && !string.IsNullOrEmpty(river.gameObject.scene.name) ? river.gameObject.scene.name : "Scene";
            string clean = new string(river.name.Where(char.IsLetterOrDigit).ToArray());
            return $"{scene}_{clean}";
        }

        /// <summary>Regenerates the river mesh from the controller's settings (in place, same asset GUID).</summary>
        public static void Rebuild(RiverFlowController river, string meshKey = null)
        {
            EnsureFolders();
            MeshFilter filter = river.surface != null ? river.surface.GetComponent<MeshFilter>() : null;
            if (filter == null || river.controlPoints.Count < 2) return;
            string path = filter.sharedMesh != null && AssetDatabase.Contains(filter.sharedMesh)
                ? AssetDatabase.GetAssetPath(filter.sharedMesh)
                : $"{MeshFolder}/Gen_River_{meshKey ?? MeshKey(river)}.asset";
            Mesh mesh = BuildMesh(river, out Vector3[] centre, out float[] speeds, out float flowSpeed);
            filter.sharedMesh = GeneratedAssetStore.SaveMesh(mesh, path);
            river.SetBakedPath(centre, speeds, river.spacing, flowSpeed);
            river.ApplyQuality();
            EditorUtility.SetDirty(river);
            EditorUtility.SetDirty(filter);
        }

        // ───────────────────────────── Mesh ─────────────────────────────

        static List<Vector3> Spline(IList<Vector3> points, float step, List<float> controlDistances)
        {
            var result = new List<Vector3> { points[0] };
            float travelled = 0f;
            controlDistances.Add(0f);
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = points[Mathf.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int k = 1; k <= steps; k++)
                {
                    float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
                    Vector3 q = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                    travelled += Vector3.Distance(Flat(result[result.Count - 1]), Flat(q));
                    result.Add(q);
                }
                controlDistances.Add(travelled);
            }
            return result;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static List<Vector3> Resample(List<Vector3> dense, float spacing)
        {
            var result = new List<Vector3> { dense[0] };
            float carried = 0f;
            for (int i = 0; i < dense.Count - 1; i++)
            {
                Vector3 a = dense[i], b = dense[i + 1];
                float len = Vector3.Distance(Flat(a), Flat(b));
                float s = spacing - carried;
                while (s <= len)
                {
                    result.Add(Vector3.Lerp(a, b, s / len));
                    s += spacing;
                }
                carried = len - (s - spacing);
            }
            if (Vector3.Distance(Flat(result[result.Count - 1]), Flat(dense[dense.Count - 1])) > spacing * 0.3f) result.Add(dense[dense.Count - 1]);
            return result;
        }

        public static Mesh BuildMesh(RiverFlowController river, out Vector3[] centre, out float[] speeds, out float flowSpeed)
        {
            var controlDistances = new List<float>();
            List<Vector3> dense = Spline(river.controlPoints, 0.2f, controlDistances);
            List<Vector3> c = Resample(dense, river.spacing);
            int n = c.Count, across = Mathf.Clamp(river.acrossVertices, 3, 11);
            float halfWidth = river.width * 0.5f;
            Material material = river.standardMaterial;
            flowSpeed = material != null && material.HasProperty("_FlowSpeed") ? material.GetFloat("_FlowSpeed") : 0.9f;

            // Segment speed per sample, blended over 4 m at each boundary so the flow never jumps.
            var segments = river.segments.Count > 0 ? river.segments.OrderBy(s => s.fromPoint).ToList() : new List<RiverSegment> { new RiverSegment { speed = 1f } };
            float SpeedAt(float d)
            {
                float speed = segments[0].speed;
                for (int i = 1; i < segments.Count; i++)
                {
                    float start = controlDistances[Mathf.Clamp(segments[i].fromPoint, 0, controlDistances.Count - 1)];
                    speed = Mathf.Lerp(speed, segments[i].speed, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start - 2f, start + 2f, d)));
                }
                return speed;
            }

            var tangents = new Vector3[n];
            for (int i = 0; i < n; i++) tangents[i] = Flat(c[Mathf.Min(i + 1, n - 1)] - c[Mathf.Max(i - 1, 0)]).normalized;
            var vertices = new List<Vector3>(n * across);
            var normals = new List<Vector3>(n * across);
            var uv0 = new List<Vector2>(n * across);
            var uv2 = new List<Vector4>(n * across);
            speeds = new float[n];
            float seedOffset = (river.seed % 997) * 1.37f;
            for (int i = 0; i < n; i++)
            {
                float d = i * river.spacing;
                Vector3 side = Vector3.Cross(Vector3.up, tangents[i]).normalized;
                // Signed turn per metre (positive = turning left), smoothed over neighbours.
                float turn = 0f;
                for (int j = Mathf.Max(1, i - 2); j <= Mathf.Min(n - 2, i + 2); j++) turn += -Vector3.Cross(tangents[j - 1], tangents[j + 1]).y / (2f * river.spacing);
                turn /= 5f;
                float speed = SpeedAt(d);
                speeds[i] = speed;
                float localHalf = halfWidth * (1f + 0.08f * (Mathf.PerlinNoise(d * 0.12f + seedOffset, 0.5f) - 0.5f) * 2f);
                for (int k = 0; k < across; k++)
                {
                    float u = Mathf.Lerp(-1f, 1f, k / (across - 1f));
                    Vector3 p = c[i] + side * (u * localHalf);
                    vertices.Add(p);
                    normals.Add(Vector3.up);
                    uv0.Add(new Vector2(u * localHalf, d));
                    float foam = river.edgeFoam * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, Mathf.Abs(u)));
                    float outer = turn > 0f ? u : -u; // the outside of a bend is opposite the turn
                    foam += river.bendFoam * Mathf.Clamp01(Mathf.Abs(turn) * 6f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, outer));
                    foam += river.fastFoam * Mathf.Clamp01(Mathf.Abs(speed) - 1f) * 0.5f;
                    foreach (Vector3 rock in river.rocks)
                    {
                        float rd = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(rock.x, rock.z));
                        // Foam hugs the rock and trails off downstream.
                        Vector3 rel = p - rock;
                        float downstream = Vector3.Dot(rel, tangents[i]) * Mathf.Sign(speed == 0f ? 1f : speed);
                        float reach = 0.7f + Mathf.Clamp(downstream, 0f, 2.5f) * 0.5f;
                        foam = Mathf.Max(foam, river.rockFoam * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, reach, rd))));
                    }
                    uv2.Add(new Vector4(Mathf.Clamp01(foam), speed, 1f - u * u, 0f));
                }
            }
            var triangles = new List<int>((n - 1) * (across - 1) * 6);
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < across - 1; k++)
                {
                    int a = i * across + k, b = a + 1, cIdx = a + across, dIdx = cIdx + 1;
                    triangles.Add(a); triangles.Add(cIdx); triangles.Add(b);
                    triangles.Add(b); triangles.Add(cIdx); triangles.Add(dIdx);
                }
            var mesh = new Mesh { name = "River" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(2, uv2);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            centre = c.ToArray();
            return mesh;
        }

        // ───────────────────────────── Upgrading existing water ─────────────────────────────

        static bool LooksLikeWater(MeshRenderer r)
        {
            if (r.GetComponentInParent<RiverFlowController>() != null) return false;
            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("replaced by flowing river")) return false;
            return n == "stream" || n == "whispering brook" || n.Contains("river") || n.Contains("brook") || n.Contains("stream") || n.Contains("water");
        }

        /// <summary>Centre line, surface level and width of a long water strip (principal axis, binned).</summary>
        static bool DeriveCentreLine(MeshFilter filter, out List<Vector3> points, out float width)
        {
            points = new List<Vector3>();
            width = 0f;
            if (filter.sharedMesh == null) return false;
            Vector3[] v = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
            if (v.Length < 6) return false;
            Vector2 mean = new Vector2(v.Average(p => p.x), v.Average(p => p.z));
            float sxx = 0f, szz = 0f, sxz = 0f;
            foreach (Vector3 p in v)
            {
                float dx = p.x - mean.x, dz = p.z - mean.y;
                sxx += dx * dx; szz += dz * dz; sxz += dx * dz;
            }
            float angle = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
            var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var perp = new Vector2(-axis.y, axis.x);
            const float bin = 3f;
            var bins = new SortedDictionary<int, List<Vector3>>();
            foreach (Vector3 p in v)
            {
                int key = Mathf.FloorToInt(Vector2.Dot(new Vector2(p.x, p.z) - mean, axis) / bin);
                if (!bins.TryGetValue(key, out List<Vector3> list)) bins[key] = list = new List<Vector3>();
                list.Add(p);
            }
            var widths = new List<float>();
            foreach (List<Vector3> list in bins.Values)
            {
                if (list.Count < 2) continue;
                points.Add(new Vector3(list.Average(p => p.x), list.Average(p => p.y), list.Average(p => p.z)));
                float min = list.Min(p => Vector2.Dot(new Vector2(p.x, p.z), perp)), max = list.Max(p => Vector2.Dot(new Vector2(p.x, p.z), perp));
                widths.Add(max - min);
            }
            if (points.Count < 2) return false;
            widths.Sort();
            width = Mathf.Max(1f, widths[widths.Count / 2]);
            return true;
        }

        [MenuItem("Greenhaven/Environment/Generate Flowing River", priority = 100)]
        static void GenerateMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Flowing river", "Stop Play mode first.", "OK");
                return;
            }
            var candidates = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.enabled && LooksLikeWater(r)).ToList();
            var existing = Object.FindObjectsByType<RiverFlowController>(FindObjectsSortMode.None);
            if (candidates.Count == 0)
            {
                string msg = existing.Length > 0
                    ? $"This scene already has {existing.Length} flowing river(s). Select one to edit its points, width, flow and quality, then press Rebuild River."
                    : "No water found in this scene. Use Greenhaven ▸ Environment ▸ Create River Demo Scene for a winding test river, or add a RiverFlowController to an empty object and set its control points.";
                EditorUtility.DisplayDialog("Flowing river", msg, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Flowing river",
                    $"Upgrade {string.Join(", ", candidates.Select(c => $"'{c.name}'"))} to flowing water?\n\nA new 'Flowing River (generated)' object follows the same course and level. " +
                    "The old water mesh is only hidden (kept), and pebbles, plants and a few in-water rocks are added along the banks, away from trails and bridges. Undo reverts it.",
                    "Upgrade", "Cancel"))
                return;
            var report = new StringBuilder();
            foreach (MeshRenderer old in candidates)
            {
                if (!DeriveCentreLine(old.GetComponent<MeshFilter>(), out List<Vector3> points, out float width)) continue;
                // Control points every ~6 m are plenty for the spline.
                var control = points.Where((p, i) => i % 2 == 0 || i == points.Count - 1).ToList();
                RiverFlowController river = CreateRiver($"Flowing River (generated) — {old.name}", null, old.transform.parent, control, width + 1f, 0.5f, old.name.Sum(ch => ch) % 1000); // a little wider so the edges tuck under the banks
                Undo.RecordObject(old, "Hide old water");
                Undo.RecordObject(old.gameObject, "Rename old water");
                old.enabled = false;
                old.gameObject.name = $"{old.gameObject.name} (replaced by flowing river)";
                string details = AddBankDetails(river, GeneratorPrefs.Settings);
                report.AppendLine($"'{river.name}': {river.Length:0} m, {width:0.0} m wide. {details}");
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Greenhaven — Flowing river:\n" + report);
            EditorUtility.DisplayDialog("Flowing river", report + "\nSave the scene to keep it. Select the river to tune flow, foam, segments and quality.", "OK");
        }

        [MenuItem("Greenhaven/Environment/Rebuild Selected River", priority = 101)]
        static void RebuildSelectedMenu()
        {
            RiverFlowController river = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<RiverFlowController>() : null;
            if (river == null)
            {
                EditorUtility.DisplayDialog("Rebuild river", "Select a flowing river first.", "OK");
                return;
            }
            Rebuild(river);
        }

        // ───────────────────────────── Banks ─────────────────────────────

        sealed class PathMask
        {
            readonly List<(Vector2 a, Vector2 b, Vector2 c, Rect box)> tris = new List<(Vector2, Vector2, Vector2, Rect)>();

            public PathMask()
            {
                foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (!PathMeshNames.Contains(mf.gameObject.name) || mf.sharedMesh == null) continue;
                    Vector3[] v = mf.sharedMesh.vertices;
                    int[] t = mf.sharedMesh.triangles;
                    Matrix4x4 m = mf.transform.localToWorldMatrix;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]);
                        var a2 = new Vector2(a.x, a.z); var b2 = new Vector2(b.x, b.z); var c2 = new Vector2(c.x, c.z);
                        Rect box = Rect.MinMaxRect(Mathf.Min(a2.x, b2.x, c2.x), Mathf.Min(a2.y, b2.y, c2.y), Mathf.Max(a2.x, b2.x, c2.x), Mathf.Max(a2.y, b2.y, c2.y));
                        tris.Add((a2, b2, c2, box));
                    }
                }
            }

            public bool Near(Vector3 p, float margin)
            {
                var q = new Vector2(p.x, p.z);
                foreach (var (a, b, c, box) in tris)
                {
                    if (q.x < box.xMin - margin || q.x > box.xMax + margin || q.y < box.yMin - margin || q.y > box.yMax + margin) continue;
                    return true; // within the (expanded) bounds of a path triangle: close enough to keep clear
                }
                return false;
            }
        }

        static bool IsGround(Collider c) => c is TerrainCollider || (c is MeshCollider mc && !mc.convex);

        /// <summary>Pebbles, grass, flowers and mossy rocks along both banks, plus a few rocks in the shallows (foam and splashes form around them).</summary>
        public static string AddBankDetails(RiverFlowController river, GeneratorSettings assets)
        {
            Transform old = river.transform.Find(DetailsName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var parentGo = new GameObject(DetailsName);
            Undo.RegisterCreatedObjectUndo(parentGo, "River bank details");
            Transform parent = parentGo.transform;
            parent.SetParent(river.transform, false);
            Physics.SyncTransforms();

            GameObject Prefab(AssetCategory c, int v) =>
                AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(GeneratedAssetStore.AssetName(Generators.For(c), assets.Seed, v)));
            var rng = new System.Random(river.seed * 7919 + 3);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var mask = new PathMask();
            List<Vector3> centre = Enumerable.Range(0, river.PathCount).Select(i => river.transform.TransformPoint(river.PathPoint(i))).ToList();
            if (centre.Count < 2) return "No banks: rebuild the river first.";
            float half = river.width * 0.5f;
            int pebbles = 0, plants = 0, inWater = 0;
            river.rocks.Clear();

            bool Clear(Vector3 at, Collider ground, float radius)
            {
                foreach (Collider c in Physics.OverlapSphere(at + Vector3.up * (radius + 0.2f), radius, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    if (c != ground && !(c.transform.IsChildOf(parent))) return false;
                return true;
            }

            GameObject Place(GameObject prefab, Vector3 at, float scale, float cull)
            {
                if (prefab == null) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, R(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * scale;
                if (cull > 0f)
                {
                    LODGroup lod = go.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(cull, go.GetComponentsInChildren<Renderer>()) });
                    lod.RecalculateBounds();
                }
                return go;
            }

            for (int i = 1; i < centre.Count - 1; i += 2)
            {
                Vector3 c = centre[i];
                Vector3 tangent = Flat(centre[i + 1] - centre[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                foreach (float s in new[] { -1f, 1f })
                {
                    if (rng.NextDouble() > 0.75) continue;
                    double roll = rng.NextDouble();
                    float lateral = roll < 0.45 ? R(0.05f, 0.6f) : R(0.3f, 1.6f);
                    Vector3 probe = c + side * s * (half + lateral);
                    if (mask.Near(probe, 1.2f)) continue;
                    if (!Physics.Raycast(probe + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 12f, ~(1 << 2), QueryTriggerInteraction.Ignore) || !IsGround(hit.collider)) continue;
                    if (hit.point.y < c.y + 0.02f || hit.point.y > c.y + 2.5f) continue; // in the water, or up a cliff
                    if (!Clear(hit.point, hit.collider, 0.3f)) continue;
                    if (roll < 0.45)
                    {
                        Place(Prefab(AssetCategory.Rock, rng.Next(6)), hit.point + Vector3.down * 0.05f, R(0.12f, 0.24f), 0.012f);
                        pebbles++;
                    }
                    else if (roll < 0.88)
                    {
                        AssetCategory type = roll < 0.78 ? AssetCategory.Grass : AssetCategory.Flower;
                        Place(Prefab(type, rng.Next(Generators.For(type).DefaultCount)), hit.point, R(0.8f, 1.2f), 0.025f);
                        plants++;
                    }
                    else
                    {
                        Place(Prefab(AssetCategory.Rock, rng.Next(6)), hit.point + Vector3.down * 0.12f, R(0.4f, 0.7f), 0.01f);
                        pebbles++;
                    }
                }
            }

            // A few rocks breaking the surface in the shallows.
            int wanted = Mathf.Clamp(Mathf.RoundToInt(river.Length / 14f), 2, 10);
            for (int attempt = 0; attempt < wanted * 12 && inWater < wanted; attempt++)
            {
                int i = rng.Next(2, centre.Count - 2);
                Vector3 c = centre[i];
                Vector3 side = Vector3.Cross(Vector3.up, Flat(centre[i + 1] - centre[i - 1]).normalized).normalized;
                Vector3 probe = c + side * (rng.NextDouble() < 0.5 ? -1f : 1f) * half * R(0.25f, 0.7f);
                if (mask.Near(probe, 2f) || river.rocks.Any(r => (river.transform.TransformPoint(r) - probe).sqrMagnitude < 9f)) continue;
                if (!Physics.Raycast(probe + Vector3.up * 3f, Vector3.down, out RaycastHit bed, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore) || !IsGround(bed.collider)) continue;
                if (bed.point.y > c.y - 0.05f || !Clear(bed.point, bed.collider, 0.45f)) continue;
                GameObject rock = Place(Prefab(AssetCategory.Rock, rng.Next(6)), bed.point, R(0.35f, 0.55f), 0.008f);
                if (rock == null) break;
                // Sit it so the top just breaks the surface.
                Renderer rr = rock.GetComponentInChildren<Renderer>();
                float top = rr != null ? rr.bounds.max.y : bed.point.y;
                rock.transform.position += Vector3.up * (c.y + 0.1f - top);
                river.rocks.Add(river.transform.InverseTransformPoint(new Vector3(probe.x, c.y, probe.z)));
                inWater++;
            }
            Rebuild(river);
            return $"Banks: {pebbles} pebbles and rocks, {plants} plants; {inWater} rocks in the water.";
        }

    }
}
