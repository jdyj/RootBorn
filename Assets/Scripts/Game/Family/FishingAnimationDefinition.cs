using UnityEngine;

namespace Rootborn.Game.Family
{
    [CreateAssetMenu(fileName = "FishingAnimation", menuName = "Rootborn/Family/Fishing Animation")]
    public sealed class FishingAnimationDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private CharacterPartAnimationClipDefinition _throwHookClip;
        [SerializeField] private CharacterPartAnimationClipDefinition _waitingIdleClip;
        [SerializeField] private CharacterPartAnimationClipDefinition _pullHookClip;
        [SerializeField] private CharacterPartAnimationClipDefinition _caughtClip;

        public string Id => _id;
        public CharacterPartAnimationClipDefinition ThrowHookClip => _throwHookClip;
        public CharacterPartAnimationClipDefinition WaitingIdleClip => _waitingIdleClip;
        public CharacterPartAnimationClipDefinition PullHookClip => _pullHookClip;
        public CharacterPartAnimationClipDefinition CaughtClip => _caughtClip;

        public bool IsValid(out string error)
        {
            error = string.Empty;
            AppendIfInvalid(string.IsNullOrWhiteSpace(_id), "id", ref error);
            AppendIfInvalid(_throwHookClip == null, "throwHookClip", ref error);
            AppendIfInvalid(_waitingIdleClip == null, "waitingIdleClip", ref error);
            AppendIfInvalid(_pullHookClip == null, "pullHookClip", ref error);
            AppendIfInvalid(_caughtClip == null, "caughtClip", ref error);
            return string.IsNullOrEmpty(error);
        }

        private static void AppendIfInvalid(bool invalid, string fieldName, ref string error)
        {
            if (!invalid)
            {
                return;
            }

            error = string.IsNullOrEmpty(error) ? fieldName : error + ", " + fieldName;
        }
    }
}
