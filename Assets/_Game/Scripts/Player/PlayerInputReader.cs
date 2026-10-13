using System;
using Greenhaven.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Greenhaven.Player
{
    public enum MouseLookMode
    {
        /// <summary>Standard desktop third-person: the cursor is locked and hidden while playing; Esc frees it, a click locks it again.</summary>
        FreeLook,
        /// <summary>The camera turns only while the right mouse button is held; the cursor stays free.</summary>
        HoldRightButton,
    }

    /// <summary>
    /// Single source of player intent. Keyboard/mouse, gamepads and the on-screen touch controls (which
    /// emulate a virtual gamepad) all feed the same actions, so gameplay code never knows which device is
    /// in use. Also owns the desktop cursor: locked for free mouse look, released with Esc or when another
    /// device takes over.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Running")]
        [SerializeField, Range(0.5f, 1f), Tooltip("Pushing a stick (or the on-screen joystick) past this amount runs.")]
        float stickRunThreshold = 0.9f;

        [Header("Look")]
        [SerializeField] MouseLookMode mouseLookMode = MouseLookMode.FreeLook;
        [SerializeField, Tooltip("Degrees per pixel of mouse movement.")]
        float mouseDegreesPerPixel = 0.12f;
        [SerializeField, Tooltip("Degrees per second at full right-stick deflection.")]
        float stickDegreesPerSecond = 160f;
        [SerializeField, Tooltip("Camera distance change per scroll notch (desktop).")] float zoomPerNotch = 0.6f;

        InputAction move;
        InputAction sprint;
        InputAction jump;
        InputAction interact;
        InputAction mouseLook;
        InputAction mouseLookHold;
        InputAction stickLook;
        InputAction zoom;

        Vector2 pendingLookDegrees;
        bool jumpQueued;
        bool cursorReleased;
        float pendingZoom;

        /// <summary>Raised once per interact press (E, gamepad West, on-screen Interact).</summary>
        public event Action InteractPressed;

        /// <summary>Current movement input, magnitude 0..1.</summary>
        public Vector2 Move => Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);

        /// <summary>True when the player asks to run: Shift/stick-click, or a near-full stick push.</summary>
        public bool RunRequested
        {
            get
            {
                if (sprint.IsPressed()) return true;
                InputControl control = move.activeControl;
                return control != null && control.device is Gamepad && Move.magnitude >= stickRunThreshold;
            }
        }

        /// <summary>True while the mouse is steering the camera (cursor locked, or right button held).</summary>
        public bool MouseLookActive =>
            Mouse.current != null && (mouseLookMode == MouseLookMode.FreeLook ? Cursor.lockState == CursorLockMode.Locked : mouseLookHold.IsPressed());

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            sprint = new InputAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonWest");

            mouseLook = new InputAction("MouseLook", InputActionType.Value, "<Mouse>/delta");
            mouseLookHold = new InputAction("MouseLookHold", InputActionType.Button, "<Mouse>/rightButton");
            stickLook = new InputAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick");
            zoom = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");

            jump.performed += OnJump;
            interact.performed += OnInteract;
        }

        void OnEnable()
        {
            move.Enable();
            sprint.Enable();
            jump.Enable();
            interact.Enable();
            mouseLook.Enable();
            mouseLookHold.Enable();
            stickLook.Enable();
            zoom.Enable();
        }

        void OnDisable()
        {
            move.Disable();
            sprint.Disable();
            jump.Disable();
            interact.Disable();
            mouseLook.Disable();
            mouseLookHold.Disable();
            stickLook.Disable();
            zoom.Disable();
            jumpQueued = false;
            pendingLookDegrees = Vector2.zero;
            SetCursorLocked(false);
        }

        void OnDestroy()
        {
            jump.performed -= OnJump;
            interact.performed -= OnInteract;
            move.Dispose();
            sprint.Dispose();
            jump.Dispose();
            interact.Dispose();
            mouseLook.Dispose();
            mouseLookHold.Dispose();
            stickLook.Dispose();
            zoom.Dispose();
        }

        void Update()
        {
            pendingZoom += zoom.ReadValue<float>() / 120f * zoomPerNotch; // Windows reports 120 per notch
            UpdateCursor();
        }

        void UpdateCursor()
        {
            ActiveInput.Refresh();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) cursorReleased = true;
            // A click on the world (not on UI) takes the mouse back.
            if (cursorReleased && mouse != null && mouse.leftButton.wasPressedThisFrame &&
                !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                cursorReleased = false;

            bool wantLock = mouseLookMode == MouseLookMode.FreeLook && mouse != null && !cursorReleased &&
                            Application.isFocused && ActiveInput.Current == InputKind.KeyboardMouse;
            SetCursorLocked(wantLock);
        }

        static void SetCursorLocked(bool locked)
        {
            CursorLockMode wanted = locked ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != wanted) Cursor.lockState = wanted;
            if (Cursor.visible == locked) Cursor.visible = !locked;
        }

        void OnJump(InputAction.CallbackContext _) => jumpQueued = true;

        void OnInteract(InputAction.CallbackContext _) => InteractPressed?.Invoke();

        /// <summary>Returns true once per jump press.</summary>
        public bool ConsumeJump()
        {
            bool queued = jumpQueued;
            jumpQueued = false;
            return queued;
        }

        /// <summary>Adds look rotation from a touch drag area (degrees: x = yaw, y = pitch).</summary>
        public void AddLookDegrees(Vector2 degrees) => pendingLookDegrees += degrees;

        /// <summary>Camera distance change requested since the last call (positive = zoom out).</summary>
        public float ConsumeZoom()
        {
            float z = -pendingZoom;
            pendingZoom = 0f;
            return z;
        }

        /// <summary>
        /// Returns look rotation (degrees: x = yaw, y = pitch) gathered since the last call, and whether the
        /// mouse provided it (mouse input wants little smoothing; sticks and touch want more). Call once per frame.
        /// </summary>
        public Vector2 ConsumeLookDegrees(float deltaTime, out bool fromMouse)
        {
            Vector2 look = pendingLookDegrees;
            pendingLookDegrees = Vector2.zero;
            fromMouse = false;

            if (MouseLookActive)
            {
                Vector2 mouseDelta = mouseLook.ReadValue<Vector2>() * mouseDegreesPerPixel;
                if (mouseDelta.sqrMagnitude > 0f)
                {
                    look += mouseDelta;
                    fromMouse = true;
                }
            }
            look += stickLook.ReadValue<Vector2>() * (stickDegreesPerSecond * deltaTime);
            look *= GameSettings.LookSensitivity;
            if (GameSettings.InvertY) look.y = -look.y;
            return look;
        }

        public Vector2 ConsumeLookDegrees(float deltaTime) => ConsumeLookDegrees(deltaTime, out _);

        /// <summary>Take the mouse back after a menu closes (free look only).</summary>
        public void RelockCursor() => cursorReleased = false;
    }
}
