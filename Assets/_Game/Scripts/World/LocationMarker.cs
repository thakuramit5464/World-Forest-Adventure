using System;
using System.Collections.Generic;
using Greenhaven.Core;
using Greenhaven.Player;
using UnityEngine;

namespace Greenhaven.World
{
    public enum LocationKind { Village, House, Bridge, Gate, Clearing, Rest, Viewpoint, Summit, Landmark }

    /// <summary>
    /// A named place. Walking into its trigger the first time announces it ("Discovered — Whispering Glade")
    /// and remembers it in the save, after which it shows on the map. Undiscovered places stay hidden on the
    /// map unless <see cref="alwaysOnMap"/> is set (used for the village square and gates).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class LocationMarker : MonoBehaviour
    {
        [SerializeField] string locationId;
        [SerializeField] string displayName = "Somewhere";
        [SerializeField, TextArea(1, 3)] string description;
        [SerializeField] LocationKind kind = LocationKind.Landmark;
        [SerializeField, Tooltip("Shown on the map before it is discovered.")] bool alwaysOnMap;

        public static readonly List<LocationMarker> Active = new List<LocationMarker>();
        public static event Action<LocationMarker> Discovered;

        public string Id => locationId;
        public string DisplayName => displayName;
        public string Description => description;
        public LocationKind Kind => kind;
        public bool AlwaysOnMap => alwaysOnMap;
        public bool IsDiscovered => GameProgress.Instance.HasFlag(Flag);
        string Flag => "discovered:" + locationId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
            Discovered = null;
        }

        void Reset() => GetComponent<SphereCollider>().isTrigger = true;

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() == null || string.IsNullOrEmpty(locationId)) return;
            if (GameProgress.Instance.HasFlag(Flag)) return;
            GameProgress.Instance.SetFlag(Flag);
            Discovered?.Invoke(this);
        }
    }
}
