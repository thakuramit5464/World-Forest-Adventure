using UnityEngine;

namespace Greenhaven.Interaction
{
    /// <summary>
    /// Anything the player can approach and interact with (NPCs, signs, collectibles, landmarks).
    /// Implemented by MonoBehaviours that sit on, or above, a collider.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short prompt shown when this is the selected target, e.g. "Talk to Mira".</summary>
        string Prompt { get; }

        /// <summary>World position used for range and facing checks.</summary>
        Vector3 InteractionPoint { get; }

        /// <summary>Whether interaction is currently allowed for this interactor.</summary>
        bool CanInteract(GameObject interactor);

        void Interact(GameObject interactor);
    }
}
