using Greenhaven.Player;
using UnityEngine;

namespace Greenhaven.World
{
    /// <summary>
    /// A trigger at the edge of a region (e.g. the village's north gate). When the player walks in, the
    /// screen fades and <see cref="targetScene"/> loads; the player then appears at the
    /// <see cref="SceneArrival"/> with the matching id. The target scene must be in the build scene list
    /// (Greenhaven ▸ Link Village and Cloudpeak sets this up).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SceneGate : MonoBehaviour
    {
        [SerializeField, Tooltip("Scene name to load, e.g. \"CloudpeakAdventure\".")] string targetScene;
        [SerializeField, Tooltip("Arrival point id in the target scene, e.g. \"from_village\".")] string arrivalId;

        /// <summary>Arrival id requested by the last gate used; read once by the target scene.</summary>
        public static string PendingArrival { get; private set; }

        bool used;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => PendingArrival = null; // Enter Play Mode Options skip the domain reload

        /// <summary>Returns the pending arrival id (if any) and clears it.</summary>
        public static string ConsumeArrival()
        {
            string id = PendingArrival;
            PendingArrival = null;
            return id;
        }

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (used || other.GetComponentInParent<PlayerMotor>() == null) return;
            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                Debug.LogWarning($"{nameof(SceneGate)}: scene '{targetScene}' is not in the build scene list. Use Greenhaven ▸ Link Village and Cloudpeak.", this);
                return;
            }
            used = true;
            PendingArrival = arrivalId;
            Greenhaven.Core.GameProgress.Instance.RecordScene(targetScene, arrivalId);
            ScreenFader.LoadScene(targetScene);
        }
    }
}
