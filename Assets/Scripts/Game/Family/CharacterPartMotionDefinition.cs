using UnityEngine;

namespace Rootborn.Game.Family
{
    [CreateAssetMenu(fileName = "CharacterPartMotion", menuName = "Rootborn/Family/Character Part Motion")]
    public sealed class CharacterPartMotionDefinition : ScriptableObject
    {
        [Header("Frame layout")]
        [SerializeField, Min(0)] private int _idleRow = 0;
        [SerializeField, Min(0)] private int _walkRow = 1;
        [SerializeField, Min(1)] private int _framesPerDirection = 1;
        [SerializeField, Min(0.01f)] private float _framesPerSecond = 8f;

        [Header("Idle columns")]
        [SerializeField, Min(0)] private int _idleRightColumn = 0;
        [SerializeField, Min(0)] private int _idleUpColumn = 0;
        [SerializeField, Min(0)] private int _idleLeftColumn = 0;
        [SerializeField, Min(0)] private int _idleDownColumn = 0;

        [Header("Walk start columns")]
        [SerializeField, Min(0)] private int _walkRightStartColumn = 0;
        [SerializeField, Min(0)] private int _walkUpStartColumn = 0;
        [SerializeField, Min(0)] private int _walkLeftStartColumn = 0;
        [SerializeField, Min(0)] private int _walkDownStartColumn = 0;

        [Tooltip("Disable when the sheet contains distinct left- and right-facing frames.")]
        [SerializeField] private bool _mirrorHorizontal = true;

        public int IdleRow => Mathf.Max(0, _idleRow);
        public int WalkRow => Mathf.Max(0, _walkRow);
        public int FramesPerDirection => Mathf.Max(1, _framesPerDirection);
        public float FramesPerSecond => Mathf.Max(0.01f, _framesPerSecond);
        public bool MirrorHorizontal => _mirrorHorizontal;

        public int GetIdleColumn(Vector2 facing)
        {
            switch (GetDirectionIndex(facing))
            {
                case 0: return Mathf.Max(0, _idleRightColumn);
                case 1: return Mathf.Max(0, _idleUpColumn);
                case 2: return Mathf.Max(0, _idleLeftColumn);
                default: return Mathf.Max(0, _idleDownColumn);
            }
        }

        public int GetWalkStartColumn(Vector2 facing)
        {
            switch (GetDirectionIndex(facing))
            {
                case 0: return Mathf.Max(0, _walkRightStartColumn);
                case 1: return Mathf.Max(0, _walkUpStartColumn);
                case 2: return Mathf.Max(0, _walkLeftStartColumn);
                default: return Mathf.Max(0, _walkDownStartColumn);
            }
        }

        internal static int GetDirectionIndex(Vector2 facing)
        {
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
            {
                return facing.x > 0f ? 0 : 2;
            }

            return facing.y > 0f ? 1 : 3;
        }
    }
}
