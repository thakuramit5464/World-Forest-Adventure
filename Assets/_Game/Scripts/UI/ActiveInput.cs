using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Greenhaven.UI
{
    public enum InputKind { Touch, KeyboardMouse, Gamepad }

    /// <summary>
    /// Which kind of device the player used last, so the HUD can show touch controls only when they are
    /// wanted and pick the right key hints. Polled once per frame by whoever calls <see cref="Refresh"/>.
    /// Mouse clicks on UI are ignored (they may be driving the on-screen controls in the Editor), and the
    /// virtual gamepad created by the on-screen controls is never counted as a real gamepad.
    /// </summary>
    public static class ActiveInput
    {
        public static InputKind Current { get; private set; }
        /// <summary>False until any device activity has been seen this session.</summary>
        public static bool HasSeenActivity { get; private set; }
        public static event Action<InputKind> Changed;

        static int frame = -1;
        static bool initialised;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            frame = -1;
            initialised = false;
            HasSeenActivity = false;
            Changed = null;
        }

        public static void Refresh()
        {
            if (Time.frameCount == frame) return;
            frame = Time.frameCount;
            if (!initialised)
            {
                initialised = true;
                Current = Application.isMobilePlatform ? InputKind.Touch : InputKind.KeyboardMouse;
            }

            InputKind? seen = null;
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) seen = InputKind.Touch;

            if (seen == null)
            {
                Keyboard keyboard = Keyboard.current;
                Mouse mouse = Mouse.current;
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (keyboard != null && keyboard.anyKey.isPressed) seen = InputKind.KeyboardMouse;
                else if (mouse != null && !overUi && (mouse.rightButton.isPressed || mouse.leftButton.wasPressedThisFrame || mouse.scroll.ReadValue().sqrMagnitude > 0.01f))
                    seen = InputKind.KeyboardMouse;
            }

            if (seen == null)
            {
                Gamepad pad = Gamepad.current;
                // On-screen controls add a virtual gamepad with no interface name; only real pads count.
                if (pad != null && !string.IsNullOrEmpty(pad.description.interfaceName))
                {
                    bool active = pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.rightStick.ReadValue().sqrMagnitude > 0.04f;
                    if (!active)
                        foreach (InputControl control in pad.allControls)
                            if (control is ButtonControl button && button.isPressed) { active = true; break; }
                    if (active) seen = InputKind.Gamepad;
                }
            }

            if (seen == null) return;
            HasSeenActivity = true;
            if (seen.Value == Current) return;
            Current = seen.Value;
            Changed?.Invoke(Current);
        }
    }
}
