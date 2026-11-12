using System.Collections.Generic;

namespace Rootborn.Core.World
{
    /// <summary>한 칸에서 걸어서 닿는 칸과, 닿은 칸이 상자(문 등)에 맞닿는지. 맵 검사와 NPC 길 찾기가 같은 기준을 쓴다.</summary>
    public static class MapReach
    {
        /// <summary>from에서 걸어서 닿는 칸 전부(from 포함). from이 막혔으면 빈 집합.</summary>
        public static HashSet<CellPos> From(CollisionGrid grid, CellPos from)
        {
            var reached = new HashSet<CellPos>();
            if (grid.IsBlocked(from)) return reached;
            reached.Add(from);
            GridPath.Find(grid, from, c => { reached.Add(c); return false; });
            return reached;
        }

        /// <summary>닿은 칸 중 하나가 상자에 겹치거나 맞닿는가(Unity 판 물리의 접촉 여유처럼 1픽셀 넓혀 본다).</summary>
        public static bool Touches(HashSet<CellPos> reached, AreaRect area, int cellSize)
        {
            AreaRect grown = Grown(area);
            foreach (CellPos c in reached)
                if (CellTouches(c, grown, cellSize)) return true;
            return false;
        }

        /// <summary>칸 하나가 상자에 겹치거나 맞닿는가. Touches와 같은 기준.</summary>
        internal static bool Touches(CellPos cell, AreaRect area, int cellSize) => CellTouches(cell, Grown(area), cellSize);

        private static AreaRect Grown(AreaRect area) => new AreaRect(area.X - 1, area.Y - 1, area.W + 2, area.H + 2);

        private static bool CellTouches(CellPos c, AreaRect grown, int cellSize) =>
            new AreaRect(c.X * cellSize, c.Y * cellSize, cellSize, cellSize).Overlaps(grown);
    }
}
