using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenhaven.Player
{
    /// <summary>
    /// Single source of player intent. Keyboard/mouse, gamepads and the on-screen
    /// touch controls (which emulate a virtual gamepad) all feed the same actions,
    /// so gameplay code never knows which device is in use.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Running")]
        [SerializeField, Range(0.5f, 1f), Tooltip("Pushing a stick (or the on-screen joystick) past this amount runs.")]
        float stickRunThreshold = 0.9f;

        [Header("Look")]
        [SerializeField, Tooltip("Degrees per pixel of mouse movement while the right mouse button is held.")]
        float mouseDegreesPerPixel = 0.15f;
        [SerializeField, Tooltip("Degrees per second at full right-stick deflection.")]
        float stickDegreesPerSecond = 160f;

        InputAction move;
        InputAction sprint;
        InputAction jump;
        InputAction interact;
        InputAction mouseLook;
        InputAction mouseLookHold;
        InputAction stickLook;

        Vector2 pendingLookDegrees;
        bool jumpQueued;

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
            jumpQueued = false;
            pendingLookDegrees = Vector2.zero;
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

        /// <summary>
        /// Returns look rotation (degrees: x = yaw, y = pitch) gathered since the last call.
        /// Call once per frame from the camera.
        /// </summary>
        public Vector2 ConsumeLookDegrees(float deltaTime)
        {
            Vector2 look = pendingLookDegrees;
            pendingLookDegrees = Vector2.zero;

            if (mouseLookHold.IsPressed())
                look += mouseLook.ReadValue<Vector2>() * mouseDegreesPerPixel;

            look += stickLook.ReadValue<Vector2>() * (stickDegreesPerSecond * deltaTime);
            return look;
        }
    }
}
