using UnityEngine;

namespace Rootborn.Game.Characters.Growing
{
    /// <summary>One layer's frames: rows down, left, right, up; columns walk x3, action x2, hurt.</summary>
    [CreateAssetMenu(fileName = "GrowingSheet", menuName = "Rootborn/Characters/Growing Sheet")]
    public sealed class GrowingCharacterSheetDefinition : ScriptableObject
    {
        public const int Rows = 4;
        public const int Columns = 6;
        public const int FrameCount = Rows * Columns;

        [SerializeField] private string _id;
        [SerializeField] private Sprite[] _frames = new Sprite[0];

        public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id;
        public bool IsValid => _frames != null && _frames.Length == FrameCount;

        public Sprite GetFrame(int row, int column)
        {
            if (!IsValid || row < 0 || row >= Rows || column < 0 || column >= Columns) return null;
            return _frames[row * Columns + column];
        }

        public void ConfigureForTests(string id, Sprite[] frames)
        {
            _id = id;
            _frames = frames;
        }
    }
}
