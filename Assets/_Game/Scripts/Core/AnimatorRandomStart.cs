using UnityEngine;

namespace Greenhaven.Core
{
    /// <summary>
    /// Starts the Animator's default state at a random point (and slightly varied speed) so
    /// groups of NPCs or animals don't idle in perfect sync.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class AnimatorRandomStart : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.5f)] float speedVariation = 0.1f;

        void Start()
        {
            var animator = GetComponent<Animator>();
            if (animator.runtimeAnimatorController == null) return;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            animator.Play(state.fullPathHash, 0, Random.value);
            animator.speed = 1f + Random.Range(-speedVariation, speedVariation);
        }
    }
}
