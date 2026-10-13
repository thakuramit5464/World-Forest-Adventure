using System.IO;
using Greenhaven.Interaction;
using Greenhaven.Player;
using Greenhaven.Quests;
using Greenhaven.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Greenhaven.Editor.UI
{
    /// <summary>
    /// Greenhaven ▸ Capture HUD Preview: builds the HUD in a temporary preview scene, poses it as it looks
    /// in play (prompt and dialogue shown), renders it at 1920 × 1080 over a flat backdrop and saves the PNG.
    /// Nothing is added to the open scene.
    /// </summary>
    [InitializeOnLoad]
    public static class HudPreview
    {
        const string DevFlag = "Temp/hud_preview.flag";

        static HudPreview()
        {
            if (File.Exists(DevFlag)) EditorApplication.delayCall += () =>
            {
                File.Delete(DevFlag);
                File.WriteAllText("Temp/hud_preview.txt", Render("Temp/hud_preview.png"));
            };
        }

        [MenuItem("Greenhaven/Capture HUD Preview", priority = 61)]
        static void Menu()
        {
            string path = HudSprites.Folder + "/HUD_Preview.png";
            string result = Render(path);
            AssetDatabase.ImportAsset(path);
            EditorUtility.DisplayDialog("HUD preview", result, "OK");
        }

        public static string Render(string path)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            Texture2D shot = null;
            try
            {
                var player = new GameObject("PreviewPlayer");
                player.AddComponent<PlayerInputReader>();
                player.AddComponent<PlayerInteractor>();
                var quests = new GameObject("QuestManager").AddComponent<QuestManager>();
                SceneManager.MoveGameObjectToScene(player, preview);
                SceneManager.MoveGameObjectToScene(quests.gameObject, preview);
                DialogueView dialogue = HudBuilder.Build(player, quests);
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == "HUD" || root.name == "EventSystem") SceneManager.MoveGameObjectToScene(root, preview);

                // Pose: prompt visible with a key cap, dialogue open, tracker filled in.
                GameObject hud = null;
                foreach (GameObject root in preview.GetRootGameObjects()) if (root.name == "HUD") hud = root;
                if (hud == null) return "HUD not found.";
                Transform safe = hud.transform.Find("SafeArea");
                SetText(safe, "InteractionPrompt/Text", "Talk to Mira");
                SetText(safe, "QuestTracker/Title", "The Missing Supplies");
                SetText(safe, "QuestTracker/Objective", "Find the missing supply crates (1/3)");
                Transform panel = safe.Find("Dialogue/Panel");
                panel.gameObject.SetActive(true);
                SetText(safe, "Dialogue/Panel/SpeakerTag/Speaker", "Mira");
                SetText(safe, "Dialogue/Panel/Body", "Oh! A new face in Greenhaven. Welcome, traveller! Could you help me find three supply crates that fell off my cart?");
                Transform toast = safe.Find("QuestToast");
                toast.GetComponent<CanvasGroup>().alpha = 1f;
                SetText(safe, "QuestToast/Header", "New Quest");
                SetText(safe, "QuestToast/Title", "The Missing Supplies");

                // Render through a camera (overlay canvases cannot be captured directly).
                var camGo = new GameObject("PreviewCamera");
                SceneManager.MoveGameObjectToScene(camGo, preview);
                Camera cam = camGo.AddComponent<Camera>();
                cam.scene = preview;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.42f, 0.58f, 0.38f);
                cam.orthographic = true;
                cam.cullingMask = 1 << 5;
                rt = new RenderTexture(1920, 1080, 24);
                cam.targetTexture = rt;
                Canvas canvas = hud.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                foreach (Transform t in hud.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
                shot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                void Capture(string file)
                {
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    RenderTexture.active = rt;
                    shot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    shot.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(file, shot.EncodeToPNG());
                }
                Capture(path);
                // Menu pages: title, then settings (same file name with a suffix).
                Transform overlay = safe.Find("MenuOverlay");
                if (overlay != null)
                {
                    overlay.GetComponent<CanvasGroup>().alpha = 1f;
                    string stem = path.Substring(0, path.Length - 4);
                    overlay.Find("TitlePage").gameObject.SetActive(true);
                    Capture(stem + "_title.png");
                    overlay.Find("TitlePage").gameObject.SetActive(false);
                    overlay.Find("SettingsPage").gameObject.SetActive(true);
                    Capture(stem + "_settings.png");
                    overlay.Find("SettingsPage").gameObject.SetActive(false);
                    overlay.Find("MapPage").gameObject.SetActive(true);
                    Capture(stem + "_map.png");
                }
                return $"Saved {path} (HUD with a prompt, open dialogue, tracker and banner), plus _title and _settings pages.";
            }
            finally
            {
                if (shot != null) Object.DestroyImmediate(shot);
                if (rt != null) rt.Release();
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void SetText(Transform root, string path, string value)
        {
            Transform t = root.Find(path);
            if (t != null && t.TryGetComponent(out Text text)) text.text = value;
        }
    }
}
