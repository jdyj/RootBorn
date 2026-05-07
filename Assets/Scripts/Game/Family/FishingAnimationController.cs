using UnityEngine;

namespace Rootborn.Game.Family
{
    public sealed class FishingAnimationController : MonoBehaviour
    {
        [SerializeField] private CharacterPartAnimator _animator;
        [SerializeField] private FishingAnimationDefinition _definition;

        public FishingAnimationDefinition Definition => _definition;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponent<CharacterPartAnimator>();
            }
        }

        public void Configure(CharacterPartAnimator animator, FishingAnimationDefinition definition)
        {
            _animator = animator != null ? animator : GetComponent<CharacterPartAnimator>();
            _definition = definition;
        }

        public void PlayThrowHook()
        {
            Play(_definition != null ? _definition.ThrowHookClip : null);
        }

        public void PlayWaitingIdle()
        {
            Play(_definition != null ? _definition.WaitingIdleClip : null);
        }

        public void PlayPullHook(bool caughtFish)
        {
            if (_definition == null)
            {
                return;
            }

            Play(caughtFish ? _definition.CaughtClip : _definition.PullHookClip);
        }

        private void Play(CharacterPartAnimationClipDefinition clip)
        {
            if (_animator == null || clip == null)
            {
                return;
            }

            _animator.PlayClip(clip);
        }
    }
}
