using UnityEngine;

namespace Rootborn.Game.WorldGeneration
{
    [CreateAssetMenu(fileName = "Pattern_New", menuName = "Rootborn/World Generation/Tile Pattern")]
    public sealed class TilePatternDefinition : ScriptableObject
    {
        [SerializeField] private int _width = 1;
        [SerializeField] private int _height = 1;
        [SerializeField] private TileVariantSetDefinition[] _cells = System.Array.Empty<TileVariantSetDefinition>();

        public int Width => Mathf.Max(1, _width);
        public int Height => Mathf.Max(1, _height);
        public TileVariantSetDefinition[] Cells => _cells;

        public TileVariantSetDefinition GetVariantSet(int x, int y)
        {
            if (_cells == null || _cells.Length == 0)
            {
                return null;
            }

            int px = PositiveModulo(x, Width);
            int py = PositiveModulo(y, Height);
            int index = py * Width + px;
            if (index < 0 || index >= _cells.Length)
            {
                return null;
            }

            return _cells[index];
        }

        public void SetTestData(int width, int height, TileVariantSetDefinition[] cells)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _cells = cells ?? System.Array.Empty<TileVariantSetDefinition>();
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}