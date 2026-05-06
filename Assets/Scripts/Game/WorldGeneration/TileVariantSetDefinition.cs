using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.WorldGeneration
{
    [CreateAssetMenu(fileName = "TileVariants_New", menuName = "Rootborn/World Generation/Tile Variant Set")]
    public sealed class TileVariantSetDefinition : ScriptableObject
    {
        [SerializeField] private WeightedTileVariant[] _variants = System.Array.Empty<WeightedTileVariant>();

        public WeightedTileVariant[] Variants => _variants;

        public TileBase Pick(int seed, int x, int y, int salt)
        {
            if (_variants == null || _variants.Length == 0)
            {
                return null;
            }

            int total = 0;
            for (int i = 0; i < _variants.Length; i++)
            {
                if (_variants[i].Tile != null && _variants[i].Weight > 0)
                {
                    total += _variants[i].Weight;
                }
            }

            if (total <= 0)
            {
                return null;
            }

            int roll = DeterministicHash(seed, x, y, salt) % total;
            int cursor = 0;
            for (int i = 0; i < _variants.Length; i++)
            {
                var variant = _variants[i];
                if (variant.Tile == null || variant.Weight <= 0)
                {
                    continue;
                }

                cursor += variant.Weight;
                if (roll < cursor)
                {
                    return variant.Tile;
                }
            }

            return null;
        }

        public void SetTestData(WeightedTileVariant[] variants)
        {
            _variants = variants ?? System.Array.Empty<WeightedTileVariant>();
        }

        private static int DeterministicHash(int seed, int x, int y, int salt)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ x;
                h = (h * 397) ^ y;
                h = (h * 397) ^ salt;
                return h & 0x7fffffff;
            }
        }
    }
}