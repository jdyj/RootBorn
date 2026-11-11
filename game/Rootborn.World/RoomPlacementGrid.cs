using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>
    /// 맵에서 가구 방 칸을 만든다(Unity AuthoredHousePlacementBinding이 씬의 방에서 하던 일). 맵 안의 칸은 모두 바닥, 막힌 칸은 고정 칸,
    /// 도착 지점이 있는 칸은 들어오는 칸과 늘 지나갈 수 있어야 하는 칸이다. 맵 크기는 맵(LDtk 레벨)에서 온다 — 코드에 적지 않는다.
    /// 들어오는 칸은 도착 지점 id 순서(문자열 순)의 첫 칸이다.
    /// </summary>
    public static class RoomPlacementGrid
    {
        public static PlacementGrid FromMap(WorldMap map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            var floor = new List<GridPoint>(map.Grid.Columns * map.Grid.Rows);
            var fixedCells = new List<GridPoint>();
            for (int y = 0; y < map.Grid.Rows; y++)
                for (int x = 0; x < map.Grid.Columns; x++)
                {
                    floor.Add(new GridPoint(x, y));
                    if (map.Collision.IsBlocked(x, y)) fixedCells.Add(new GridPoint(x, y));
                }

            var ids = new List<string>(map.SpawnIds);
            ids.Sort(StringComparer.Ordinal);
            var spawns = new List<GridPoint>(ids.Count);
            foreach (string id in ids)
            {
                map.TryGetSpawn(id, out var point);
                CellPos cell = CellPos.Containing(point.X, point.Y, map.Grid.CellSize);
                var p = new GridPoint(cell.X, cell.Y);
                if (!spawns.Contains(p)) spawns.Add(p);
            }
            if (spawns.Count == 0) throw new InvalidOperationException(map.Id + ": 방에 도착 지점이 없다");
            return new PlacementGrid(floor, fixedCells, spawns[0], spawns);
        }
    }
}
