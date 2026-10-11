using Greenhaven.Player;
using UnityEngine;

namespace Greenhaven.World
{
    /// <summary>
    /// Where the player appears when entering this scene through a <see cref="SceneGate"/> with the same
    /// id. Runs before the player's own Awake, so the motor records it as the respawn point and the
    /// camera starts behind the player. Without a matching gate the scene's normal spawn is used.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class SceneArrival : MonoBehaviour
    {
        [SerializeField, Tooltip("Matches the arrival id on the gate in the other scene, e.g. \"from_village\".")] string arrivalId;

        void Awake()
        {
            // Every arrival point checks; only the one matching the pending id moves the player.
            string pending = SceneGate.PendingArrival;
            if (string.IsNullOrEmpty(pending) || pending != arrivalId) return;
            SceneGate.ConsumeArrival();

            PlayerMotor player = FindFirstObjectByType<PlayerMotor>();
            if (player == null) return;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(transform.position, transform.rotation);
            if (controller != null) controller.enabled = true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward * 1.5f);
        }
    }
}
