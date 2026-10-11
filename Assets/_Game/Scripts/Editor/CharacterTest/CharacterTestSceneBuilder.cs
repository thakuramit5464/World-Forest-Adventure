using System.IO;
using Greenhaven.Collectibles;
using Greenhaven.Editor.AssetGenerator;
using Greenhaven.Interaction;
using Greenhaven.Quests;
using Greenhaven.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greenhaven.Editor.CharacterTest
{
    /// <summary>
    /// Builds Assets/_Game/Scenes/CharacterTest.unity: a small flat stage to inspect and play-test the
    /// latest generated hero before it is used in the main scenes. Uses its own player prefab
    /// (Player_CharacterTest) so the shared Player prefab and other scenes are not changed.
    /// </summary>
    public static class CharacterTestSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/CharacterTest.unity";

        // Display copies, each looping one clip from the Universal Animation Library.
        static readonly (string label, string clip)[] Showcase =
        {
            ("Idle", "Idle_Loop"), ("Walk", "Walk_Loop"), ("Jog", "Jog_Fwd_Loop"), ("Sprint", "Sprint_Loop"), ("Airborne", "Jump_Loop"),
        };

        public static string Build(int seed)
        {
            if (EditorApplication.isPlaying) return "Stop Play mode before creating the character test scene.";
            if (!GreenhavenArt.AnimationLibraryAvailable) return "The Universal Animation Library is missing; the hero cannot be animated.";

            // Prefer the rigged Greenhaven Adventurer model; fall back to the generated hero.
            string heroPath = GeneratedAssetStore.PrefabPath(HeroGenerator.AssetName(seed));
            GameObject hero = AdventurerModelRig.FindPrefab();
            if (hero == null) hero = AssetDatabase.LoadAssetAtPath<GameObject>(heroPath);
            string heroReport = null;
            if (hero == null)
            {
                if (!EditorUtility.DisplayDialog("Generate hero?", $"No version-2 hero for seed {seed}. Generate it now?", "Generate", "Cancel"))
                    return "Cancelled: no hero to test.";
                heroReport = HeroGenerator.Generate(seed);
                hero = AssetDatabase.LoadAssetAtPath<GameObject>(heroPath);
                if (hero == null) return heroReport;
            }

            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild character test scene?",
                    $"{ScenePath} will be rebuilt. No other scene is changed.", "Rebuild", "Cancel"))
                return "Cancelled. Character test scene unchanged.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled (current scene not saved).";

            Phase1SceneBuilder.CreateMaterials();
            GreenhavenArt.PrepareImports();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Phase1SceneBuilder.BuildLighting();

            var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_Grass.mat");
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_StoneWarm.mat");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = grass;

            // Jump tests: two blocks (0.5 m needs a hop, 1.0 m needs a full jump) and a walkable ramp.
            Block("Step 0.5m", new Vector3(5f, 0.25f, 0f), new Vector3(1.5f, 0.5f, 1.5f), Quaternion.identity, stone);
            Block("Step 1.0m", new Vector3(7f, 0.5f, 0f), new Vector3(1.5f, 1f, 1.5f), Quaternion.identity, stone);
            Block("Ramp", new Vector3(-6f, 0.55f, 0f), new Vector3(2.5f, 0.2f, 6f), Quaternion.Euler(-12f, 0f, 0f), stone);

            // Gameplay: player (own prefab), camera, HUD.
            GameObject player = Phase1SceneBuilder.BuildPlayer(new Vector3(0f, 0.05f, -4f), "Player_CharacterTest", hero);
            Phase1SceneBuilder.BuildCamera(player);
            WeatherTools.WeatherSetup.AddToScene();
            WindTools.WindSetup.AddToScene();
            var overlay = new SerializedObject(player.GetComponent<Greenhaven.Player.AnimationDebugOverlay>());
            overlay.FindProperty("visibleOnStart").boolValue = true; // show animation state by default here
            overlay.ApplyModifiedPropertiesWithoutUndo();
            var registry = new GameObject("CollectibleRegistry").AddComponent<CollectibleRegistry>();
            var quests = new GameObject("QuestManager").AddComponent<QuestManager>();
            Phase1SceneBuilder.SetRef(quests, "registry", registry);
            DialogueView dialogue = Phase1SceneBuilder.BuildHud(player, quests);

            // Showcase row facing the start position.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < Showcase.Length; i++)
            {
                var (label, clip) = Showcase[i];
                float x = (i - (Showcase.Length - 1) * 0.5f) * 1.8f;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(hero);
                copy.name = $"Showcase_{label}";
                copy.transform.SetPositionAndRotation(new Vector3(x, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));
                copy.GetComponent<Animator>().runtimeAnimatorController = GreenhavenArt.NpcController("HeroPreview_" + label, clip, clip);

                var text = new GameObject($"Label_{label}");
                text.transform.SetPositionAndRotation(new Vector3(x, 2f, 4f), Quaternion.Euler(0f, 180f, 0f));
                TextMesh tm = text.AddComponent<TextMesh>();
                tm.text = label;
                tm.font = font;
                tm.fontSize = 48;
                tm.characterSize = 0.05f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.white;
                text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            // A "twin" to talk to: tests interact gesture, turn-to-target and head look-at.
            var twin = (GameObject)PrefabUtility.InstantiatePrefab(hero);
            twin.name = "Twin (talk to me)";
            twin.transform.SetPositionAndRotation(new Vector3(-2.5f, 0f, -1f), Quaternion.Euler(0f, 120f, 0f));
            twin.GetComponent<Animator>().runtimeAnimatorController = GreenhavenArt.NpcController("HeroPreview_Twin", "Idle_Loop", "Idle_Talking_Loop");
            CapsuleCollider capsule = twin.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.85f, 0f);
            capsule.height = 1.7f;
            capsule.radius = 0.35f;
            DialogueInteractable talk = twin.AddComponent<DialogueInteractable>();
            var so = new SerializedObject(talk);
            so.FindProperty("speakerName").stringValue = "Twin";
            so.FindProperty("promptVerb").stringValue = "Talk to";
            Phase1SceneBuilder.SetStrings(so.FindProperty("lines"),
                "Hey, it's you! Well... it's me.",
                "Watch my hands while we talk, and try walking around me — my eyes should follow you.");
            so.FindProperty("dialogueView").objectReferenceValue = dialogue;
            so.FindProperty("talkAnimator").objectReferenceValue = twin.GetComponent<Animator>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // A readable sign (generated signpost if available).
            var signPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssetStore.PrefabPath(
                GeneratedAssetStore.AssetName(Generators.For(AssetCategory.Signpost), seed, 0)));
            GameObject sign = signPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(signPrefab)
                : Block("Sign", new Vector3(2.5f, 0.75f, -1f), new Vector3(0.3f, 1.5f, 0.3f), Quaternion.identity, stone);
            sign.transform.position = new Vector3(2.5f, 0f, -1f);
            DialogueInteractable read = sign.AddComponent<DialogueInteractable>();
            var rso = new SerializedObject(read);
            rso.FindProperty("speakerName").stringValue = "Signpost";
            rso.FindProperty("promptVerb").stringValue = "Read";
            rso.FindProperty("faceInteractor").boolValue = false;
            Phase1SceneBuilder.SetStrings(rso.FindProperty("lines"),
                "Character test: walk (stick or WASD), run (push fully or Shift), jump (Space), interact (E).",
                "The row behind me loops each animation so you can inspect it up close.");
            rso.FindProperty("dialogueView").objectReferenceValue = dialogue;
            rso.ApplyModifiedPropertiesWithoutUndo();

            int badRefs = Phase1SceneBuilder.ValidateSceneReferences(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            string result = $"Character test scene saved to {ScenePath} using {hero.name}. " +
                            (badRefs == 0 ? "Scene references OK." : $"{badRefs} scene reference problem(s); see Console.");
            return heroReport != null ? heroReport + "\n" + result : result;
        }

        static GameObject Block(string name, Vector3 position, Vector3 size, Quaternion rotation, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
