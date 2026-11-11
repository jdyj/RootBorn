using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 실제 집 맵(game/world/maps.json의 House, LDtk)에서 만든 가구 방 칸. Host가 할 일을 테스트에서 흉내 낸다:
    /// 맵 안의 칸은 바닥, 막힌 칸은 고정 칸, 도착 지점이 있는 칸은 들어오는 칸과 문 앞이다. 맵 크기는 맵 파일에서 온다.
    /// </summary>
    internal static class HouseRoom
    {
        public const string MapId = "House";
        public const string SurfaceId = "house";

        private static readonly Lazy<PlacementGrid> _grid = new Lazy<PlacementGrid>(Build);

        public static PlacementGrid Grid => _grid.Value;

        /// <summary>그 가구를 놓을 수 있는 첫 칸(열 순서). skip개 만큼 건너뛴다. 없으면 예외.</summary>
        public static GridPoint FirstFreeAnchor(GameSession game, string furnitureId, int skip = 0)
        {
            var furniture = game.Definitions.Get<FurnitureDefinition>(furnitureId);
            var map = Map().Grid;
            for (int y = 0; y < map.Rows; y++)
                for (int x = 0; x < map.Columns; x++)
                {
                    var anchor = new GridPoint(x, y);
                    if (game.House.CanPlace(furniture, anchor, FurniturePlacementDirection.North) && skip-- <= 0) return anchor;
                }

            throw new InvalidOperationException(furnitureId + "를 놓을 칸이 없다");
        }

        private static WorldMap Map() => WorldMapLoader.FromRepo(TestWorld.RepoRoot).LoadMap(MapId).World;

        private static PlacementGrid Build()
        {
            var map = Map();
            var floor = new List<GridPoint>();
            var fixedCells = new List<GridPoint>();
            for (int y = 0; y < map.Grid.Rows; y++)
                for (int x = 0; x < map.Grid.Columns; x++)
                {
                    floor.Add(new GridPoint(x, y));
                    if (map.Collision.IsBlocked(x, y)) fixedCells.Add(new GridPoint(x, y));
                }

            var spawns = new List<GridPoint>();
            foreach (var id in map.SpawnIds)
            {
                map.TryGetSpawn(id, out var point);
                var cell = CellPos.Containing(point.X, point.Y, map.Grid.CellSize);
                spawns.Add(new GridPoint(cell.X, cell.Y));
            }

            if (spawns.Count == 0) throw new InvalidOperationException("집 맵에 도착 지점이 없다");
            return new PlacementGrid(floor, fixedCells, spawns[0], spawns);
        }
    }
}
