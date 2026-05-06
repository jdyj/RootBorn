using System;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.WorldGeneration
{
    [Serializable]
    public struct WeightedTileVariant
    {
        public TileBase Tile;
        public int Weight;

        public WeightedTileVariant(TileBase tile, int weight)
        {
            Tile = tile;
            Weight = weight;
        }
    }
}