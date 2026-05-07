using UnityEngine;

namespace Rootborn.Game.Family
{
    [CreateAssetMenu(fileName = "CharacterPartAnimationClip", menuName = "Rootborn/Family/Character Part Animation Clip")]
    public sealed class CharacterPartAnimationClipDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private int _row;
        [SerializeField] private int _frameCount = 1;
        [SerializeField] private float _framesPerSecond = 8f;
        [SerializeField] private bool _loop;

        public string Id => _id;
        public int Row => Mathf.Max(0, _row);
        public int FrameCount => Mathf.Max(1, _frameCount);
        public float FramesPerSecond => Mathf.Max(0.01f, _framesPerSecond);
        public bool Loop => _loop;

        public bool IsValid(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(_id))
            {
                error = "id";
            }
            if (_frameCount < 1)
            {
                error = string.IsNullOrEmpty(error) ? "frameCount" : error + ", frameCount";
            }
            if (_framesPerSecond <= 0f)
            {
                error = string.IsNullOrEmpty(error) ? "framesPerSecond" : error + ", framesPerSecond";
            }
            return string.IsNullOrEmpty(error);
        }
    }
}
