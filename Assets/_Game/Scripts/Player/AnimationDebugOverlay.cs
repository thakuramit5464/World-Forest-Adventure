using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenhaven.Player
{
    /// <summary>
    /// Development aid: press F3 to show the player's current animation state and the values driving it
    /// (MoveSpeed, Grounded, motor speed, gesture-layer weight). Only active in the Editor and
    /// development builds; does nothing in release builds.
    /// </summary>
    public sealed class AnimationDebugOverlay : MonoBehaviour
    {
        [SerializeField] bool visibleOnStart;

        bool visible;

        /// <summary>Show or hide the readout (also toggled with F3).</summary>
        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly string[] StateNames = { "Locomotion", "JumpStart", "Airborne", "Land", "PickUp", "Emote", "Interact", "Talk", "Empty" };
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");

        PlayerAnimator playerAnimator;
        PlayerMotor motor;
        GUIStyle style;

        void Awake()
        {
            TryGetComponent(out playerAnimator);
            TryGetComponent(out motor);
            visible = visibleOnStart;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;
        }

        static string StateName(AnimatorStateInfo info)
        {
            foreach (string n in StateNames)
                if (info.IsName(n)) return n;
            return "?";
        }

        void OnGUI()
        {
            if (!visible || playerAnimator == null || playerAnimator.Animator == null || motor == null) return;
            Animator a = playerAnimator.Animator;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 18 };
            int actions = a.GetLayerIndex("Actions");
            string text =
                $"Base: {StateName(a.GetCurrentAnimatorStateInfo(0))}\n" +
                (actions >= 0 ? $"Actions: {StateName(a.GetCurrentAnimatorStateInfo(actions))} (weight {a.GetLayerWeight(actions):0.00})\n" : "") +
                $"MoveSpeed: {a.GetFloat(MoveSpeedId):0.00}   motor: {motor.PlanarSpeed:0.00} m/s\n" +
                $"Grounded: {a.GetBool(GroundedId)}   running: {motor.IsRunning}\n" +
                "(F3 to hide)";
            GUI.Box(new Rect(Screen.width - 430, 10, 420, 130), text, style);
        }
#endif
    }
}
