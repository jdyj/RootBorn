using System;
using UnityEngine;

namespace Rootborn.Game.WorldGeneration
{
    [Serializable]
    public struct TerrainReservedArea
    {
        public RectInt Area;
        public int SafeRadius;

        public TerrainReservedArea(RectInt area, int safeRadius)
        {
            Area = area;
            SafeRadius = Mathf.Max(0, safeRadius);
        }

        public bool Contains(Vector2Int cell)
        {
            var expanded = new RectInt(
                Area.xMin - SafeRadius,
                Area.yMin - SafeRadius,
                Area.width + SafeRadius * 2,
                Area.height + SafeRadius * 2);
            return expanded.Contains(cell);
        }
    }
}
