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
    /// 실제 집 맵(game/world/maps.json의 House, LDtk)에서 만든 가구 방 칸 — Host가 집에 들어설 때 넘기는 칸과 같다(<see cref="HouseRoomGrid"/>,
    /// Screens FurnitureRoom과 같은지는 Screens 테스트가 고정한다). 가구 칸 y는 위로 커진다(가구 칸 = (x, 행 수 - 1 - 맵 y)). 맵 크기는 맵 파일에서 온다.
    /// </summary>
    internal static class HouseRoom
    {
        public const string MapId = "House";
        public const string SurfaceId = "house";

        private static readonly Lazy<PlacementGrid> _grid = new Lazy<PlacementGrid>(() => HouseRoomGrid.From(Map()));

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
    }
}
