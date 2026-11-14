using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
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

        private static readonly Lazy<HouseBuildFrame> _frame = new Lazy<HouseBuildFrame>(BuildFrame);

        /// <summary>짓는 집의 틀(Host가 집에 들어설 때 EnterHouseBuild로 넘기는 것과 같은 값). 맵의 build와 World 로더의 출처표에서 만든다.</summary>
        public static HouseBuildFrame Frame => _frame.Value;

        /// <summary>새 집 왼쪽 위 방 안의 바닥 칸(Core RoomFillTests와 같은 평면 좌표).</summary>
        public static readonly CellPos TopLeftRoom = new CellPos(3, 4);

        private static HouseBuildFrame BuildFrame()
        {
            var map = WorldMapLoader.FromRepo(TestWorld.RepoRoot).LoadMap(MapId);
            var build = map.Entry.Build ?? throw new InvalidOperationException("집 맵은 짓는 집이어야 한다");
            if (map.Tiles == null) throw new InvalidOperationException("짓는 집은 출처표가 있어야 한다");
            return new HouseBuildFrame(build.FrameId, map.Tiles, build.DefaultWall, build.DefaultFloor, build.BandColors);
        }

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

        /// <summary>
        /// 준비: 이 슬롯의 집을 처음 배치 가구 없이 빈 집으로 둔다(빈 배치 파일 — 처음 가구를 모두 치운 집과 같다). 새 게임이 받는 처음 배치(HouseStarterTests)와
        /// 상관없이 "가구가 하나도 없는 집"에서 놓기·옮기기를 보는 테스트가 시작 전에 부른다.
        /// </summary>
        public static void EmptyHouse(Rootborn.Core.Save.ISaveStore store, string slot = TestWorld.Slot)
            => FurnitureSaveFiles.SaveHouseLayout(store, slot, 0, Array.Empty<FurniturePlacementSaveData>());

        private static readonly Lazy<PlacementGrid> _hostGrid = new Lazy<PlacementGrid>(() => Build(true));

        /// <summary>
        /// Host가 실제로 넘기는 가구 방 칸(Screens FurnitureRoom과 같다: 가구 칸 y는 위로 커진다, 가구 칸 = (x, 행 수 - 1 - 맵 y)).
        /// <see cref="Grid"/>는 맵 y를 그대로 쓰는 옛 시험 칸이라, 데이터에 적은 가구 칸(처음 배치)이 실제 집에 맞는지는 이것으로 본다.
        /// </summary>
        public static PlacementGrid HostGrid => _hostGrid.Value;

        private static PlacementGrid Build() => Build(false);

        private static PlacementGrid Build(bool flipY)
        {
            var map = Map();
            GridPoint Cell(int x, int y) => new GridPoint(x, flipY ? map.Grid.Rows - 1 - y : y);
            var floor = new List<GridPoint>();
            var fixedCells = new List<GridPoint>();
            for (int y = 0; y < map.Grid.Rows; y++)
                for (int x = 0; x < map.Grid.Columns; x++)
                {
                    floor.Add(Cell(x, y));
                    if (map.Collision.IsBlocked(x, y)) fixedCells.Add(Cell(x, y));
                }

            var spawns = new List<GridPoint>();
            var ids = new List<string>(map.SpawnIds);
            if (flipY) ids.Sort(StringComparer.Ordinal); // Host(FurnitureRoom)는 도착 지점 id 순서의 첫 칸이 들어오는 칸이다
            foreach (var id in ids)
            {
                map.TryGetSpawn(id, out var point);
                var cell = CellPos.Containing(point.X, point.Y, map.Grid.CellSize);
                spawns.Add(Cell(cell.X, cell.Y));
            }

            if (spawns.Count == 0) throw new InvalidOperationException("집 맵에 도착 지점이 없다");
            return new PlacementGrid(floor, fixedCells, spawns[0], spawns);
        }
    }
}
