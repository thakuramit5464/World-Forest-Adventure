using Greenhaven.Interaction;
using Greenhaven.Player;
using Greenhaven.Quests;
using Greenhaven.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Greenhaven.Editor.UI
{
    /// <summary>
    /// Builds the themed HUD used by every scene builder: parchment-and-wood panels, a floating touch
    /// joystick with a run ring, icon buttons with press feedback, a drag-to-look area with inertia, a key
    /// hint on the interaction prompt, the quest tracker and milestone banner, and the dialogue panel. Touch
    /// controls fade out when a keyboard, mouse or gamepad is used and come back on touch.
    /// </summary>
    public static class HudBuilder
    {
        static Font font;
        static HudSprites.Set sprites;

        public static DialogueView Build(GameObject player, QuestManager quests)
        {
            PlayerInputReader input = player.GetComponent<PlayerInputReader>();
            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            sprites = HudSprites.Ensure();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>(); // default UI actions when no asset is assigned

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // landscape: scale by height so buttons keep their physical size
            canvasGo.AddComponent<GraphicRaycaster>();

            RectTransform safe = Rect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ── Touch controls (fade as a group) ──
            RectTransform touch = Rect("TouchControls", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            touch.gameObject.AddComponent<CanvasGroup>();
            touch.gameObject.AddComponent<TouchControlsVisibility>();

            // Look area first so it sits behind every control.
            RectTransform look = Rect("TouchLookArea", touch, new Vector2(0.42f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            Invisible(look);
            Phase1SceneBuilder.SetRef(look.gameObject.AddComponent<TouchLookArea>(), "input", input);

            // Floating joystick zone (left part of the screen, below the quest tracker).
            RectTransform zone = Rect("JoystickZone", touch, Vector2.zero, new Vector2(0.42f, 0.82f), Vector2.zero, Vector2.zero);
            Invisible(zone);
            RectTransform stickBase = Rect("JoystickBase", zone, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
            Image baseImg = stickBase.gameObject.AddComponent<Image>();
            baseImg.sprite = sprites.Circle;
            baseImg.color = new Color(0.16f, 0.11f, 0.07f, 0.42f);
            baseImg.raycastTarget = false;
            RectTransform baseRing = Rect("Rim", stickBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
            Image rimImg = baseRing.gameObject.AddComponent<Image>();
            rimImg.sprite = sprites.Ring;
            rimImg.color = new Color(HudSprites.Parchment.r, HudSprites.Parchment.g, HudSprites.Parchment.b, 0.55f);
            rimImg.raycastTarget = false;
            RectTransform runRing = Rect("RunRing", stickBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 340f));
            Image runImg = runRing.gameObject.AddComponent<Image>();
            runImg.sprite = sprites.Ring;
            runImg.color = new Color(HudSprites.TealLight.r, HudSprites.TealLight.g, HudSprites.TealLight.b, 0f);
            runImg.raycastTarget = false;
            RectTransform knob = Rect("Knob", stickBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124f, 124f));
            Image knobImg = knob.gameObject.AddComponent<Image>();
            knobImg.sprite = sprites.Circle;
            knobImg.color = HudSprites.Parchment;
            knobImg.raycastTarget = false;
            RectTransform knobDot = Rect("Dot", knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            Image dotImg = knobDot.gameObject.AddComponent<Image>();
            dotImg.sprite = sprites.Circle;
            dotImg.color = HudSprites.Wood;
            dotImg.raycastTarget = false;
            VirtualJoystick stick = zone.gameObject.AddComponent<VirtualJoystick>();
            SetPath(stick, "<Gamepad>/leftStick");
            var stickSo = new SerializedObject(stick);
            stickSo.FindProperty("stickBase").objectReferenceValue = stickBase;
            stickSo.FindProperty("knob").objectReferenceValue = knob;
            stickSo.FindProperty("runRing").objectReferenceValue = runImg;
            stickSo.FindProperty("ringIdle").colorValue = runImg.color;
            stickSo.FindProperty("ringRun").colorValue = new Color(HudSprites.TealLight.r, HudSprites.TealLight.g, HudSprites.TealLight.b, 0.9f);
            stickSo.ApplyModifiedPropertiesWithoutUndo();
            // Resting spot (lower-left of the zone), where the stick drifts back to after release.
            stickBase.anchoredPosition = new Vector2(-150f, -170f);

            // Buttons: Jump (large, bottom-right) and Interact (above-left of it).
            RectTransform jump = IconButton("JumpButton", touch, new Vector2(-210f, 200f), 200f, HudSprites.Teal, sprites.IconJump, "JUMP");
            SetPath(jump.gameObject.AddComponent<OnScreenButton>(), "<Gamepad>/buttonSouth");
            RectTransform interact = IconButton("InteractButton", touch, new Vector2(-430f, 330f), 160f, HudSprites.Gold, sprites.IconInteract, "TALK");
            SetPath(interact.gameObject.AddComponent<OnScreenButton>(), "<Gamepad>/buttonWest");
            CanvasGroup interactGroup = interact.gameObject.AddComponent<CanvasGroup>();
            HudButton interactHud = interact.GetComponent<HudButton>();

            // ── Interaction prompt (centre-bottom): key cap + text on a parchment pill ──
            RectTransform prompt = Rect("InteractionPrompt", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 380f), new Vector2(560f, 82f));
            Panel(prompt, 1f);
            RectTransform cap = Rect("KeyCap", prompt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(54f, 54f));
            Image capImg = cap.gameObject.AddComponent<Image>();
            capImg.sprite = sprites.KeyCap;
            capImg.type = Image.Type.Sliced;
            capImg.raycastTarget = false;
            Text capText = Label("Key", cap, 30, TextAnchor.MiddleCenter, HudSprites.Ink, bold: true);
            capText.text = "E";
            Text promptText = Label("Text", prompt, 36, TextAnchor.MiddleCenter, HudSprites.Ink);
            promptText.rectTransform.offsetMin = new Vector2(96f, 0f);
            promptText.rectTransform.offsetMax = new Vector2(-30f, 0f);

            // ── Quest tracker (top-left) ──
            RectTransform tracker = Rect("QuestTracker", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(290f, -105f), new Vector2(520f, 150f));
            Panel(tracker, 0.92f);
            RectTransform trackerBar = Rect("Bar", tracker, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            trackerBar.offsetMin = new Vector2(10f, 12f);
            trackerBar.offsetMax = new Vector2(16f, -12f);
            Image barImg = trackerBar.gameObject.AddComponent<Image>();
            barImg.sprite = sprites.Circle;
            barImg.type = Image.Type.Sliced;
            barImg.color = HudSprites.Gold;
            barImg.raycastTarget = false;
            Text trackerTitle = Label("Title", tracker, 30, TextAnchor.UpperLeft, HudSprites.Accent, bold: true);
            trackerTitle.rectTransform.offsetMin = new Vector2(34f, 0f);
            trackerTitle.rectTransform.offsetMax = new Vector2(-20f, -16f);
            Text trackerObjective = Label("Objective", tracker, 27, TextAnchor.LowerLeft, HudSprites.Ink);
            trackerObjective.rectTransform.offsetMin = new Vector2(34f, 18f);
            trackerObjective.rectTransform.offsetMax = new Vector2(-20f, 0f);
            QuestTrackerView trackerView = canvasGo.AddComponent<QuestTrackerView>();
            Phase1SceneBuilder.SetRef(trackerView, "manager", quests);
            Phase1SceneBuilder.SetRef(trackerView, "root", tracker.gameObject);
            Phase1SceneBuilder.SetRef(trackerView, "titleText", trackerTitle);
            Phase1SceneBuilder.SetRef(trackerView, "objectiveText", trackerObjective);

            // ── Quest milestone banner (top-centre) ──
            RectTransform toast = Rect("QuestToast", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(740f, 160f));
            Panel(toast, 0.97f);
            CanvasGroup toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            Text toastHeader = Label("Header", toast, 32, TextAnchor.UpperCenter, HudSprites.Accent, bold: true);
            toastHeader.rectTransform.offsetMax = new Vector2(0f, -22f);
            Text toastTitle = Label("Title", toast, 50, TextAnchor.LowerCenter, HudSprites.Ink, bold: true);
            toastTitle.rectTransform.offsetMin = new Vector2(0f, 26f);
            QuestToastView toastView = canvasGo.AddComponent<QuestToastView>();
            Phase1SceneBuilder.SetRef(toastView, "manager", quests);
            Phase1SceneBuilder.SetRef(toastView, "group", toastGroup);
            Phase1SceneBuilder.SetRef(toastView, "headerText", toastHeader);
            Phase1SceneBuilder.SetRef(toastView, "titleText", toastTitle);

            InteractionPromptView promptView = canvasGo.AddComponent<InteractionPromptView>();
            Phase1SceneBuilder.SetRef(promptView, "interactor", interactor);
            Phase1SceneBuilder.SetRef(promptView, "promptRoot", prompt.gameObject);
            Phase1SceneBuilder.SetRef(promptView, "promptText", promptText);
            Phase1SceneBuilder.SetRef(promptView, "keyCap", cap.gameObject);
            Phase1SceneBuilder.SetRef(promptView, "keyText", capText);
            Phase1SceneBuilder.SetRef(promptView, "interactButton", interactGroup);
            Phase1SceneBuilder.SetRef(promptView, "interactHudButton", interactHud);

            // ── Dialogue panel (bottom centre, between the controls) ──
            RectTransform dialogueRoot = Rect("Dialogue", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform panel = Rect("Panel", dialogueRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1000f, 236f));
            Panel(panel, 0.97f).raycastTarget = true; // taps on the panel advance the dialogue
            RectTransform speakerTag = Rect("SpeakerTag", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, 6f), new Vector2(260f, 52f));
            Image tagImg = speakerTag.gameObject.AddComponent<Image>();
            tagImg.sprite = sprites.Panel;
            tagImg.type = Image.Type.Sliced;
            tagImg.color = HudSprites.Accent;
            tagImg.raycastTarget = false;
            Text speaker = Label("Speaker", speakerTag, 30, TextAnchor.MiddleCenter, HudSprites.Parchment, bold: true);
            Text body = Label("Body", panel, 34, TextAnchor.UpperLeft, HudSprites.Ink);
            body.rectTransform.offsetMin = new Vector2(40f, 24f);
            body.rectTransform.offsetMax = new Vector2(-40f, -48f);
            Text hint = Label("Hint", panel, 26, TextAnchor.LowerRight, HudSprites.WoodLight);
            hint.text = "▼";
            hint.rectTransform.offsetMin = new Vector2(40f, 14f);
            hint.rectTransform.offsetMax = new Vector2(-32f, 0f);
            DialogueView dialogue = dialogueRoot.gameObject.AddComponent<DialogueView>();
            Phase1SceneBuilder.SetRef(dialogue, "panel", panel.gameObject);
            Phase1SceneBuilder.SetRef(dialogue, "speakerText", speaker);
            Phase1SceneBuilder.SetRef(dialogue, "bodyText", body);
            Phase1SceneBuilder.SetRef(dialogue, "interactor", interactor);
            Phase1SceneBuilder.SetRef(dialogue, "continueHint", hint);
            panel.gameObject.SetActive(false);

            BuildMenu(safe, input);
            return dialogue;
        }

        // ───────────────────────────── Title, pause and settings ─────────────────────────────

        static void BuildMenu(RectTransform safe, PlayerInputReader input)
        {
            // Pause button (top-right, always available; the only HUD button mouse players need).
            RectTransform pauseRt = Rect("PauseButton", safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(84f, 84f));
            Image pauseBg = pauseRt.gameObject.AddComponent<Image>();
            pauseBg.sprite = sprites.Circle;
            pauseBg.color = new Color(0.16f, 0.11f, 0.07f, 0.55f);
            foreach (float x in new[] { -9f, 9f })
            {
                Image bar = Rect("Bar", pauseRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(10f, 30f)).gameObject.AddComponent<Image>();
                bar.sprite = sprites.Panel;
                bar.type = Image.Type.Sliced;
                bar.color = HudSprites.Parchment;
                bar.raycastTarget = false;
            }
            Button pauseButton = pauseRt.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pauseBg;

            // Overlay: dims the game and blocks clicks while a page is open.
            RectTransform overlay = Rect("MenuOverlay", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.04f, 0.03f, 0.55f);
            CanvasGroup overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable = false;

            // Title page.
            RectTransform title = Rect("TitlePage", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
            Panel(title, 0.97f);
            Text heading = Label("Heading", title, 74, TextAnchor.UpperCenter, HudSprites.Accent, bold: true);
            heading.text = "Trails of Greenhaven";
            heading.rectTransform.offsetMax = new Vector2(0f, -36f);
            Text tagline = Label("Tagline", title, 30, TextAnchor.UpperCenter, HudSprites.WoodLight);
            tagline.text = "A storybook adventure in the forests and mountains";
            tagline.rectTransform.offsetMax = new Vector2(0f, -132f);
            Button continueButton = MenuButton(title, "Continue", 110f, HudSprites.Teal, HudSprites.Parchment, out _);
            Button newGame = MenuButton(title, "New Game", 10f, HudSprites.Parchment, HudSprites.Ink, out Text newGameLabel);
            Button titleSettings = MenuButton(title, "Settings", -90f, HudSprites.Parchment, HudSprites.Ink, out _);
            Button quit = MenuButton(title, "Quit", -190f, HudSprites.Parchment, HudSprites.Ink, out _);
            Text version = Label("Version", title, 22, TextAnchor.LowerCenter, HudSprites.WoodLight);
            version.text = "Prototype — progress saves automatically";
            version.rectTransform.offsetMin = new Vector2(0f, 20f);
            title.gameObject.SetActive(false);

            // Pause page.
            RectTransform pause = Rect("PausePage", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 470f));
            Panel(pause, 0.97f);
            Text paused = Label("Heading", pause, 56, TextAnchor.UpperCenter, HudSprites.Accent, bold: true);
            paused.text = "Paused";
            paused.rectTransform.offsetMax = new Vector2(0f, -30f);
            Button resume = MenuButton(pause, "Resume", 50f, HudSprites.Teal, HudSprites.Parchment, out _);
            Button pauseSettings = MenuButton(pause, "Settings", -50f, HudSprites.Parchment, HudSprites.Ink, out _);
            Button toTitle = MenuButton(pause, "Title Screen", -150f, HudSprites.Parchment, HudSprites.Ink, out _);
            pause.gameObject.SetActive(false);

            // Settings page.
            RectTransform settings = Rect("SettingsPage", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 660f));
            Panel(settings, 0.97f);
            Text settingsHeading = Label("Heading", settings, 52, TextAnchor.UpperCenter, HudSprites.Accent, bold: true);
            settingsHeading.text = "Settings";
            settingsHeading.rectTransform.offsetMax = new Vector2(0f, -28f);
            Slider sensitivity = SliderRow(settings, "Look sensitivity", 160f, 0.3f, 2.5f, out Text sensitivityValue);
            Toggle invert = ToggleRow(settings, "Invert look up/down", 70f);
            Toggle recentre = ToggleRow(settings, "Camera follows behind (touch/gamepad)", -10f);
            Toggle touchControls = ToggleRow(settings, "Always show touch controls", -90f);
            Slider volume = SliderRow(settings, "Volume", -180f, 0f, 1f, out Text volumeValue);
            Button back = MenuButton(settings, "Back", -270f, HudSprites.Teal, HudSprites.Parchment, out _);
            settings.gameObject.SetActive(false);

            GameMenu menu = overlay.gameObject.AddComponent<GameMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("overlay").objectReferenceValue = overlayGroup;
            so.FindProperty("titlePanel").objectReferenceValue = title.gameObject;
            so.FindProperty("pausePanel").objectReferenceValue = pause.gameObject;
            so.FindProperty("settingsPanel").objectReferenceValue = settings.gameObject;
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.FindProperty("newGameButton").objectReferenceValue = newGame;
            so.FindProperty("titleSettingsButton").objectReferenceValue = titleSettings;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("pauseSettingsButton").objectReferenceValue = pauseSettings;
            so.FindProperty("toTitleButton").objectReferenceValue = toTitle;
            so.FindProperty("settingsBackButton").objectReferenceValue = back;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("newGameLabel").objectReferenceValue = newGameLabel;
            so.FindProperty("sensitivitySlider").objectReferenceValue = sensitivity;
            so.FindProperty("volumeSlider").objectReferenceValue = volume;
            so.FindProperty("invertToggle").objectReferenceValue = invert;
            so.FindProperty("touchControlsToggle").objectReferenceValue = touchControls;
            so.FindProperty("recentreToggle").objectReferenceValue = recentre;
            so.FindProperty("sensitivityValue").objectReferenceValue = sensitivityValue;
            so.FindProperty("volumeValue").objectReferenceValue = volumeValue;
            so.FindProperty("showTitleOnStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Button MenuButton(RectTransform parent, string label, float y, Color fill, Color textColor, out Text text)
        {
            RectTransform rt = Rect(label.Replace(" ", "") + "Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(440f, 84f));
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprites.Panel;
            img.type = Image.Type.Sliced;
            img.color = fill;
            text = Label("Label", rt, 34, TextAnchor.MiddleCenter, textColor, bold: true);
            text.text = label;
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = new Color(0.95f, 0.95f, 0.95f);
            button.colors = colors;
            return button;
        }

        static Text RowLabel(RectTransform parent, string label, float y)
        {
            Text t = Label(label.Replace(" ", "") + "Label", parent, 30, TextAnchor.MiddleLeft, HudSprites.Ink);
            t.text = label;
            t.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            t.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(300f, y);
            t.rectTransform.sizeDelta = new Vector2(580f, 60f);
            return t;
        }

        static Slider SliderRow(RectTransform parent, string label, float y, float min, float max, out Text valueText)
        {
            RowLabel(parent, label, y);
            RectTransform rt = Rect(label.Replace(" ", "") + "Slider", parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-250f, y), new Vector2(300f, 44f));
            Image bg = Rect("Background", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            bg.rectTransform.offsetMin = new Vector2(0f, 14f);
            bg.rectTransform.offsetMax = new Vector2(0f, -14f);
            bg.sprite = sprites.Bar;
            bg.type = Image.Type.Sliced;
            bg.color = HudSprites.WoodLight;
            RectTransform fillArea = Rect("Fill Area", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fillArea.offsetMin = new Vector2(10f, 14f);
            fillArea.offsetMax = new Vector2(-10f, -14f);
            Image fill = Rect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            fill.rectTransform.offsetMin = new Vector2(-10f, 0f);
            fill.rectTransform.offsetMax = new Vector2(10f, 0f);
            fill.sprite = sprites.Bar;
            fill.type = Image.Type.Sliced;
            fill.color = HudSprites.Teal;
            RectTransform handleArea = Rect("Handle Slide Area", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            handleArea.offsetMin = new Vector2(20f, 0f);
            handleArea.offsetMax = new Vector2(-20f, 0f);
            Image handle = Rect("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 44f)).gameObject.AddComponent<Image>();
            handle.rectTransform.sizeDelta = new Vector2(44f, 0f); // the Slider stretches its handle to the area height
            handle.sprite = sprites.Circle;
            handle.color = HudSprites.Parchment;
            Slider slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            valueText = Label("Value", parent, 28, TextAnchor.MiddleRight, HudSprites.Ink, bold: true);
            valueText.rectTransform.anchorMin = valueText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            valueText.rectTransform.anchoredPosition = new Vector2(-440f, y);
            valueText.rectTransform.sizeDelta = new Vector2(90f, 60f);
            return slider;
        }

        static Toggle ToggleRow(RectTransform parent, string label, float y)
        {
            RowLabel(parent, label, y);
            RectTransform rt = Rect(label.Replace(" ", "") + "Toggle", parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-128f, y), new Vector2(56f, 56f));
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = sprites.KeyCap;
            bg.type = Image.Type.Sliced;
            Image check = Rect("Checkmark", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f)).gameObject.AddComponent<Image>();
            check.sprite = sprites.Circle;
            check.color = HudSprites.Teal;
            check.raycastTarget = false;
            Toggle toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = check;
            return toggle;
        }

        // ───────────────────────────── Pieces ─────────────────────────────

        static RectTransform IconButton(string name, Transform parent, Vector2 offsetFromBottomRight, float size, Color fill, Sprite icon, string label)
        {
            RectTransform rt = Rect(name, parent, new Vector2(1f, 0f), new Vector2(1f, 0f), offsetFromBottomRight, new Vector2(size, size));
            Image shadow = Rect("Shadow", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(size, size)).gameObject.AddComponent<Image>();
            shadow.sprite = sprites.Circle;
            shadow.color = new Color(0f, 0f, 0f, 0.28f);
            shadow.raycastTarget = false;
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprites.Circle;
            img.color = new Color(fill.r, fill.g, fill.b, 0.92f);
            Image rim = Rect("Rim", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size)).gameObject.AddComponent<Image>();
            rim.sprite = sprites.Ring;
            rim.color = new Color(HudSprites.Parchment.r, HudSprites.Parchment.g, HudSprites.Parchment.b, 0.8f);
            rim.raycastTarget = false;
            Image ic = Rect("Icon", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.1f), new Vector2(size * 0.46f, size * 0.46f)).gameObject.AddComponent<Image>();
            ic.sprite = icon;
            ic.color = HudSprites.Parchment;
            ic.raycastTarget = false;
            Text t = Label("Label", rt, Mathf.RoundToInt(size * 0.11f), TextAnchor.LowerCenter, HudSprites.Parchment, bold: true);
            t.text = label;
            t.rectTransform.offsetMin = new Vector2(0f, size * 0.12f);
            var hud = rt.gameObject.AddComponent<HudButton>();
            var so = new SerializedObject(hud);
            so.FindProperty("background").objectReferenceValue = img;
            so.FindProperty("pressedTint").colorValue = Color.Lerp(img.color, Color.white, 0.3f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return rt;
        }

        static Image Panel(RectTransform rt, float alpha)
        {
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprites.Panel;
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
            Shadow drop = rt.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.25f);
            drop.effectDistance = new Vector2(0f, -4f);
            return img;
        }

        static void Invisible(RectTransform rt)
        {
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // invisible but still receives raycasts
        }

        static void SetPath(Object onScreenControl, string path)
        {
            var so = new SerializedObject(onScreenControl);
            so.FindProperty("m_ControlPath").stringValue = path;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            if (anchorMin == anchorMax) rt.sizeDelta = size;
            else
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            return rt;
        }

        static Text Label(string name, Transform parent, int size, TextAnchor anchor, Color color, bool bold = false)
        {
            RectTransform rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
