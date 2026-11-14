using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 맵 하나로 만든 가구 방 칸 — Host가 실제로 GameSession.EnterPlacementSurface에 넘기는 칸(Screens <c>FurnitureRoom.Grid</c>)과 같아야 한다.
    /// Game 테스트는 Screens를 참조하지 않으므로(참조 방향을 새로 더하지 않는다) 그 변환을 여기 옮겨 두고, "이것 = FurnitureRoom.Grid"는
    /// Screens 테스트 <c>HouseRoomGridMatchesFurnitureRoomTests</c>가 모든 맵에서 고정한다(이 파일을 Screens 테스트에 링크해 견준다).
    /// FurnitureRoom을 고치면 여기도 같이 고친다 — 다르면 그 테스트가 실패한다.
    ///
    /// 변환: 가구 칸 y는 위로 커진다(가구 칸 = (x, 행 수 - 1 - 맵 y)). 맵 안의 칸은 모두 바닥, 막힌 칸은 고정 칸,
    /// 도착 지점 칸은 늘 지나갈 수 있어야 하는 칸이고(같은 칸은 한 번만), 들어오는 칸은 도착 지점 id 순서(문자열 순)의 첫 칸이다.
    /// Core·World 형식만 쓴다(링크해 쓰는 쪽이 따로 참조할 것이 없게).
    /// </summary>
    internal static class HouseRoomGrid
    {
        public static PlacementGrid From(WorldMap map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            MapGrid grid = map.Grid;
            GridPoint Cell(int x, int y) => new GridPoint(x, grid.Rows - 1 - y);

            var floor = new List<GridPoint>(grid.Columns * grid.Rows);
            var fixedCells = new List<GridPoint>();
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                {
                    floor.Add(Cell(x, y));
                    if (map.Collision.IsBlocked(x, y)) fixedCells.Add(Cell(x, y));
                }

            var ids = new List<string>(map.SpawnIds);
            ids.Sort(StringComparer.Ordinal);
            var spawns = new List<GridPoint>(ids.Count);
            foreach (string id in ids)
            {
                map.TryGetSpawn(id, out var point);
                CellPos cell = CellPos.Containing(point.X, point.Y, grid.CellSize);
                GridPoint at = Cell(cell.X, cell.Y);
                if (!spawns.Contains(at)) spawns.Add(at);
            }

            if (spawns.Count == 0) throw new InvalidOperationException(map.Id + ": 방에 도착 지점이 없다");
            return new PlacementGrid(floor, fixedCells, spawns[0], spawns);
        }
    }
}
