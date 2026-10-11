using System.Linq;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Interaction;
using Greenhaven.Player;
using Greenhaven.UI;
using Greenhaven.Wind;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenhaven.Editor.WindTools
{
    /// <summary>
    /// Shared scene setup for environmental motion (used by the breeze demo and the village):
    /// drifting leaves, optional ambience, a wind-swung hanging sign, the F2 breeze debug panel, and a
    /// check that vegetation prefabs are the wind-ready versions.
    /// </summary>
    public static class AmbientMotionSetup
    {
        const string AssetFolder = GeneratedAssetStore.Root + "/BreezeDemo";

        /// <summary>Plant types animated by the wind shader.</summary>
        public static readonly AssetCategory[] WindTypes =
            { AssetCategory.BroadleafTree, AssetCategory.PineTree, AssetCategory.Grass, AssetCategory.Bush, AssetCategory.Flower };

        static void EnsureFolder()
        {
            GeneratedAssetStore.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder(GeneratedAssetStore.Root, "BreezeDemo");
        }

        public static GameObject Prefab(AssetCategory type, int seed, int variant) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(GeneratedAssetStore.AssetName(Generators.For(type), seed, variant)));

        public static bool UsesWind(GameObject prefab)
        {
            var r = prefab != null ? prefab.GetComponent<MeshRenderer>() : null;
            return r != null && r.sharedMaterial != null && r.sharedMaterial.shader.name == "Greenhaven/WindLit";
        }

        /// <summary>Wind plant types whose prefabs for this seed are missing or still use the static material.</summary>
        public static AssetCategory[] NotWindReady(int seed) =>
            WindTypes.Where(t => !Enumerable.Range(0, Generators.For(t).DefaultCount).All(v => UsesWind(Prefab(t, seed, v)))).ToArray();

        // ───────────────────────────── Leaves ─────────────────────────────

        /// <summary>The shared leaf particle mesh and material (created once).</summary>
        public static void EnsureLeafAssets(out Mesh leafMesh, out Material material)
        {
            EnsureFolder();
            leafMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{AssetFolder}/Gen_LeafParticle.asset");
            if (leafMesh == null)
            {
                var leafMb = new MeshBuilder();
                leafMb.Triangle(new Vector3(0f, -0.5f, 0f), new Vector3(0.35f, 0f, 0f), new Vector3(0f, 0.5f, 0f), Vector3.back, Vector2.zero);
                leafMb.Triangle(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), new Vector3(-0.35f, 0f, 0f), Vector3.back, Vector2.zero);
                leafMesh = GeneratedAssetStore.SaveMesh(leafMb.ToMesh("Gen_LeafParticle"), $"{AssetFolder}/Gen_LeafParticle.asset");
            }
            string materialPath = $"{AssetFolder}/M_LeafParticle.mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null)
                {
                    material = new Material(shader) { name = "M_LeafParticle" };
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_Cull", (float)CullMode.Off);
                    AssetDatabase.CreateAsset(material, materialPath);
                    AssetDatabase.SetLabels(material, new[] { GeneratedAssetStore.Label });
                }
            }
        }

        public static LeafDrift AddLeafDrift()
        {
            EnsureLeafAssets(out Mesh leafMesh, out Material material);
            var go = new GameObject("LeafDrift");
            var leaves = go.AddComponent<LeafDrift>();
            var so = new SerializedObject(leaves);
            so.FindProperty("leafMesh").objectReferenceValue = leafMesh;
            so.FindProperty("leafMaterial").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
            var ps = new GameObject("Leaves").AddComponent<ParticleSystem>();
            ps.transform.SetParent(go.transform, false);
            leaves.Configure(ps); // stored ready and stopped; reused at runtime
            return leaves;
        }

        // ───────────────────────────── Ambience ─────────────────────────────

        public static AmbienceController AddAmbience()
        {
            var go = new GameObject("Ambience (assign optional clips)");
            var ambience = go.AddComponent<AmbienceController>();
            AudioSource Source(string name)
            {
                var child = new GameObject(name);
                child.transform.SetParent(go.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.spatialBlend = 0f;
                source.loop = true;
                source.playOnAwake = false;
                return source;
            }
            Phase1SceneBuilder.SetRef(ambience, "windSource", Source("Wind Loop"));
            Phase1SceneBuilder.SetRef(ambience, "birdSource", Source("Birdsong Loop"));
            return ambience;
        }

        // ───────────────────────────── Debug panel ─────────────────────────────

        public static BreezeDebugPanel AddDebugPanel(WindManager wind, LeafDrift leaves, AmbienceController ambience, GameObject player, bool visibleOnStart)
        {
            var debug = new GameObject("BreezeDebug").AddComponent<BreezeDebugPanel>();
            Phase1SceneBuilder.SetRef(debug, "wind", wind);
            Phase1SceneBuilder.SetRef(debug, "leaves", leaves);
            Phase1SceneBuilder.SetRef(debug, "ambience", ambience);
            Phase1SceneBuilder.SetRef(debug, "animationOverlay", player != null ? player.GetComponent<AnimationDebugOverlay>() : null);
            var so = new SerializedObject(debug);
            so.FindProperty("visibleOnStart").boolValue = visibleOnStart;
            so.ApplyModifiedPropertiesWithoutUndo();
            return debug;
        }

        // ───────────────────────────── Signs ─────────────────────────────

        public static void Readable(GameObject target, DialogueView dialogue, params string[] lines)
        {
            DialogueInteractable read = target.AddComponent<DialogueInteractable>();
            var so = new SerializedObject(read);
            so.FindProperty("speakerName").stringValue = "Signpost";
            so.FindProperty("promptVerb").stringValue = "Read";
            so.FindProperty("faceInteractor").boolValue = false;
            Phase1SceneBuilder.SetStrings(so.FindProperty("lines"), lines);
            so.FindProperty("dialogueView").objectReferenceValue = dialogue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Post + arm (static) and a board hanging from a hinge that swings with the wind.</summary>
        public static GameObject AddHangingSign(Transform parent, Vector3 position, float yaw, int seed, DialogueView dialogue, params string[] lines)
        {
            EnsureFolder();
            Material village = GeneratedAssetStore.EnsurePaletteMaterial(VillagePalette.Palette);
            var frameMb = new MeshBuilder();
            Shapes.Box(frameMb, new Vector3(0f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.14f), GenUtil.Village(VRamp.Wood, 3));
            Shapes.Box(frameMb, new Vector3(0.45f, 2.2f, 0f), new Vector3(0.9f, 0.1f, 0.1f), GenUtil.Village(VRamp.Wood, 4));
            Shapes.Box(frameMb, new Vector3(0.22f, 2.0f, 0f), new Vector3(0.06f, 0.4f, 0.06f), Quaternion.Euler(0f, 0f, -45f), GenUtil.Village(VRamp.Wood, 3));
            var boardMb = new MeshBuilder();
            foreach (float x in new[] { -0.28f, 0.28f })
                Shapes.Box(boardMb, new Vector3(x, -0.06f, 0f), new Vector3(0.02f, 0.12f, 0.02f), GenUtil.VillageFlat(VRamp.Dark, 3));
            Shapes.Box(boardMb, new Vector3(0f, -0.33f, 0f), new Vector3(0.75f, 0.42f, 0.05f), GenUtil.Village(VRamp.Wood, 5));
            Shapes.Box(boardMb, new Vector3(0f, -0.33f, 0f), new Vector3(0.28f, 0.2f, 0.07f), GenUtil.VillageFlat(VRamp.Tunic, 4));

            string suffix = $"_s{seed}";
            Mesh frame = GeneratedAssetStore.SaveMesh(frameMb.ToMesh("Gen_HangingSignFrame" + suffix), $"{AssetFolder}/Gen_HangingSignFrame{suffix}.asset");
            Mesh board = GeneratedAssetStore.SaveMesh(boardMb.ToMesh("Gen_HangingSignBoard" + suffix), $"{AssetFolder}/Gen_HangingSignBoard{suffix}.asset");

            var root = new GameObject("Hanging Sign");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            root.AddComponent<MeshFilter>().sharedMesh = frame;
            root.AddComponent<MeshRenderer>().sharedMaterial = village;
            BoxCollider post = root.AddComponent<BoxCollider>();
            post.center = new Vector3(0f, 1.15f, 0f);
            post.size = new Vector3(0.2f, 2.3f, 0.2f);

            var hinge = new GameObject("Board (swings)");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(0.7f, 2.15f, 0f); // pivot = hinge under the arm
            hinge.AddComponent<MeshFilter>().sharedMesh = board;
            hinge.AddComponent<MeshRenderer>().sharedMaterial = village;
            hinge.AddComponent<WindSwing>(); // hinge axis = local X (along the arm)

            if (lines != null && lines.Length > 0) Readable(root, dialogue, lines);
            return root;
        }
    }
}
