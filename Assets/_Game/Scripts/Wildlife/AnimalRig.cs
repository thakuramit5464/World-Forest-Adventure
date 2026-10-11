using UnityEngine;

namespace Greenhaven.Wildlife
{
    /// <summary>
    /// Bone references of a generated animal (filled in by the wildlife generator). Every bone rests at
    /// identity rotation in the bind pose, so procedural motion simply rotates them away from rest.
    /// Missing parts (a rabbit has no neck, a bird has two legs) are left empty.
    /// </summary>
    public sealed class AnimalRig : MonoBehaviour
    {
        public SkinnedMeshRenderer meshRenderer;
        public Transform body;
        public Transform neck;
        public Transform head;
        public Transform[] ears = new Transform[0];
        [Tooltip("Base to tip.")] public Transform[] tail = new Transform[0];
        [Tooltip("Quadrupeds: front-left, front-right, hind-left, hind-right. Birds: left, right.")]
        public Transform[] legs = new Transform[0];
        [Tooltip("Birds: left, right.")] public Transform[] wings = new Transform[0];

        public bool IsVisible => meshRenderer != null && meshRenderer.isVisible;
    }
}
