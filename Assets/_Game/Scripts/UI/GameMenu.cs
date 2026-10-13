using Greenhaven.Core;
using Greenhaven.Player;
using Greenhaven.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// Title screen (Continue / New Game / Settings / Quit), pause menu (Esc, gamepad Start or the HUD button)
    /// and settings panel. Pausing stops time, disables player input and frees the cursor; resuming restores
    /// them. The title shows once per session, in the scene the game starts in. Continue returns to the last
    /// scene the save recorded. New Game asks twice before wiping the save.
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] CanvasGroup overlay;
        [SerializeField] GameObject titlePanel, pausePanel, settingsPanel;
        [SerializeField] Button continueButton, newGameButton, titleSettingsButton, quitButton;
        [SerializeField] Button resumeButton, pauseSettingsButton, toTitleButton, settingsBackButton, pauseButton;
        [SerializeField] Text newGameLabel;
        [SerializeField] Slider sensitivitySlider, volumeSlider;
        [SerializeField] Toggle invertToggle, touchControlsToggle, recentreToggle;
        [SerializeField] Text sensitivityValue, volumeValue;
        [SerializeField] GameObject mapPanel;
        [SerializeField] MapView mapView;
        [SerializeField] Button mapButton, pauseMapButton, mapBackButton;
        [SerializeField, Tooltip("Scene a new game starts in.")] string startScene = "GreenhavenVillage";
        [SerializeField, Tooltip("Show the title screen when this scene starts (first scene of the session only).")] bool showTitleOnStart = true;
        [SerializeField, Min(1f)] float fadeSpeed = 8f;

        enum Page { None, Title, Pause, Settings, Map }

        static bool sessionStarted;
        Page page = Page.None, settingsReturn = Page.Pause;
        bool newGameArmed;
        float newGameArmedUntil;

        public bool IsOpen => page != Page.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => sessionStarted = false;

        void Start()
        {
            Hook(continueButton, Continue);
            Hook(newGameButton, NewGame);
            Hook(titleSettingsButton, () => OpenSettings(Page.Title));
            Hook(quitButton, Quit);
            Hook(resumeButton, Resume);
            Hook(pauseSettingsButton, () => OpenSettings(Page.Pause));
            Hook(toTitleButton, () => Show(Page.Title));
            Hook(settingsBackButton, () => Show(settingsReturn));
            Hook(pauseButton, TogglePause);
            Hook(mapButton, ToggleMap);
            Hook(pauseMapButton, () => Show(Page.Map));
            Hook(mapBackButton, () => Show(Page.Pause));
            if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(v => { GameSettings.LookSensitivity = v; RefreshValues(); });
            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(v => { GameSettings.MasterVolume = v; RefreshValues(); });
            if (invertToggle != null) invertToggle.onValueChanged.AddListener(v => GameSettings.InvertY = v);
            if (touchControlsToggle != null) touchControlsToggle.onValueChanged.AddListener(v => GameSettings.AlwaysShowTouchControls = v);
            if (recentreToggle != null) recentreToggle.onValueChanged.AddListener(v => GameSettings.AutoRecentre = v);
            LoadWidgets();
            GameSettings.Apply();

            if (showTitleOnStart && !sessionStarted) Show(Page.Title);
            else Resume();
        }

        static void Hook(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        void LoadWidgets()
        {
            if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(GameSettings.LookSensitivity);
            if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            if (invertToggle != null) invertToggle.SetIsOnWithoutNotify(GameSettings.InvertY);
            if (touchControlsToggle != null) touchControlsToggle.SetIsOnWithoutNotify(GameSettings.AlwaysShowTouchControls);
            if (recentreToggle != null) recentreToggle.SetIsOnWithoutNotify(GameSettings.AutoRecentre);
            RefreshValues();
        }

        void RefreshValues()
        {
            if (sensitivityValue != null) sensitivityValue.text = $"{GameSettings.LookSensitivity:0.0}×";
            if (volumeValue != null) volumeValue.text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%";
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool pausePressed = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (pausePressed)
            {
                if (page == Page.None) Show(Page.Pause);
                else if (page == Page.Pause || page == Page.Map) Resume();
                else if (page == Page.Settings) Show(settingsReturn);
            }
            bool mapPressed = (keyboard != null && keyboard.mKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
            if (mapPressed) ToggleMap();
            if (overlay != null)
                overlay.alpha = Mathf.MoveTowards(overlay.alpha, IsOpen ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            if (newGameArmed && Time.unscaledTime > newGameArmedUntil) DisarmNewGame();
        }

        public void TogglePause()
        {
            if (page == Page.None) Show(Page.Pause);
            else if (page == Page.Pause) Resume();
        }

        public void ToggleMap()
        {
            if (page == Page.None || page == Page.Pause) Show(Page.Map);
            else if (page == Page.Map) Resume();
        }

        void Show(Page next)
        {
            page = next;
            if (titlePanel != null) titlePanel.SetActive(next == Page.Title);
            if (pausePanel != null) pausePanel.SetActive(next == Page.Pause);
            if (settingsPanel != null) settingsPanel.SetActive(next == Page.Settings);
            if (mapPanel != null) mapPanel.SetActive(next == Page.Map);
            if (next == Page.Map && mapView != null) mapView.Open();
            if (overlay != null)
            {
                overlay.blocksRaycasts = true;
                overlay.interactable = true;
            }
            Time.timeScale = 0f;
            if (input != null) input.enabled = false; // also frees the cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (next == Page.Title)
            {
                bool canContinue = GameProgress.Instance.HasSaveFile || GameProgress.Instance.LastScene.Length > 0 || sessionStarted;
                if (continueButton != null) continueButton.gameObject.SetActive(canContinue);
                DisarmNewGame();
            }
            if (next == Page.Settings) LoadWidgets();
            Select(next == Page.Title ? (continueButton != null && continueButton.gameObject.activeSelf ? continueButton : newGameButton)
                : next == Page.Pause ? resumeButton : next == Page.Map ? mapBackButton : settingsBackButton);
        }

        void OpenSettings(Page returnTo)
        {
            settingsReturn = returnTo;
            Show(Page.Settings);
        }

        static void Select(Selectable s)
        {
            if (s != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        public void Resume()
        {
            page = Page.None;
            sessionStarted = true;
            if (titlePanel != null) titlePanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (mapPanel != null) mapPanel.SetActive(false);
            if (overlay != null)
            {
                overlay.blocksRaycasts = false;
                overlay.interactable = false;
            }
            Time.timeScale = 1f;
            if (input != null)
            {
                input.enabled = true;
                input.RelockCursor();
            }
        }

        void Continue()
        {
            GameProgress progress = GameProgress.Instance;
            string active = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(progress.LastScene) && progress.LastScene != active && Application.CanStreamedLevelBeLoaded(progress.LastScene))
            {
                SceneGate.RequestArrival(progress.LastArrival);
                Resume();
                ScreenFader.LoadScene(progress.LastScene);
                return;
            }
            Resume();
        }

        void NewGame()
        {
            bool hasProgress = GameProgress.Instance.HasSaveFile;
            if (hasProgress && !newGameArmed)
            {
                newGameArmed = true;
                newGameArmedUntil = Time.unscaledTime + 4f;
                if (newGameLabel != null) newGameLabel.text = "Erase progress and start over?";
                return;
            }
            GameProgress.Instance.ResetAll();
            Resume();
            string scene = Application.CanStreamedLevelBeLoaded(startScene) ? startScene : SceneManager.GetActiveScene().name;
            ScreenFader.LoadScene(scene);
        }

        void DisarmNewGame()
        {
            newGameArmed = false;
            if (newGameLabel != null) newGameLabel.text = "New Game";
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            if (IsOpen) Time.timeScale = 1f;
        }
    }
}
