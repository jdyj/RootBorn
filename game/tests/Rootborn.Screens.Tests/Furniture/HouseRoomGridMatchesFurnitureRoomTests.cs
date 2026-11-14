using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Game.Tests;
using Rootborn.Screens.Furniture;
using Rootborn.World;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// Game 테스트의 시험용 집 칸(<see cref="HouseRoomGrid"/>, Game 테스트 파일을 링크해 쓴다)이 Host가 실제로 넘기는 가구 방 칸
    /// (<see cref="FurnitureRoom.Grid"/>)과 같다 — 바닥 칸, 고정 칸, 들어오는 칸, 지나갈 칸(순서까지). Game 테스트는 Screens를 참조하지 않아
    /// 변환을 옮겨 두었으므로, 둘이 어긋나면 여기서 걸린다. game/world/maps.json의 모든 맵(도착 지점이 있는 것)과 시험용 방으로 본다(자동 테스트).
    /// </summary>
    public sealed class HouseRoomGridMatchesFurnitureRoomTests
    {
        private static void AssertSame(WorldMap map)
        {
            PlacementGrid real = new FurnitureRoom(map).Grid;
            PlacementGrid copy = HouseRoomGrid.From(map);
            Assert.That(copy.FloorCells, Is.EquivalentTo(real.FloorCells), map.Id + ": 바닥 칸");
            Assert.That(copy.FloorCells.Where(copy.IsFixed), Is.EquivalentTo(real.FloorCells.Where(real.IsFixed)), map.Id + ": 고정 칸");
            Assert.That(copy.Spawn, Is.EqualTo(real.Spawn), map.Id + ": 들어오는 칸");
            Assert.That(copy.Approaches, Is.EqualTo(real.Approaches), map.Id + ": 지나갈 칸(순서까지)");
        }

        [Test]
        public void EveryMapWithSpawns_GivesTheSameGrid()
        {
            var loader = WorldMapLoader.FromRepo(TestData.RepoRoot);
            var compared = new List<string>();
            foreach (var entry in loader.Catalog.Maps)
            {
                var map = loader.LoadMap(entry.Id).World;
                if (!map.SpawnIds.Any()) continue; // FurnitureRoom은 도착 지점이 없는 맵을 받지 않는다(둘 다 던진다)
                AssertSame(map);
                compared.Add(map.Id);
            }

            Assert.That(compared, Does.Contain("House"), "전제: 실제 집 맵을 견줬다");
            TestContext.Out.WriteLine("견준 맵: " + string.Join(", ", compared));
        }

        [Test]
        public void TwoSpawnsInOneCell_AreOneApproach_AndTheSpawnIsTheFirstIdInOrder()
        {
            // 같은 칸의 도착 지점은 한 번만 넣고, 들어오는 칸은 id 문자열 순의 첫 것이다(맵 파일의 순서가 아니다).
            var grid = new MapGrid(6, 5, 32);
            var blocked = new bool[grid.Count];
            var spawns = new Dictionary<string, (float X, float Y)>
            {
                ["z-door"] = (4 * 32 + 16, 1 * 32 + 16),
                ["a-entry"] = (2 * 32 + 16, 3 * 32 + 16),
                ["b-same-cell"] = (2 * 32 + 4, 3 * 32 + 28),
            };
            var map = new WorldMap("TwoSpawns", new CollisionGrid(grid, blocked), new List<DoorArea>(), spawns);
            AssertSame(map);
            Assert.That(HouseRoomGrid.From(map).Approaches.Count, Is.EqualTo(2), "같은 칸은 한 번");
            Assert.That(HouseRoomGrid.From(map).Spawn, Is.EqualTo(new GridPoint(2, grid.Rows - 1 - 3)), "a-entry 칸(가구 칸은 y를 뒤집는다)");
        }

        [Test]
        public void TheWalledTestRoom_GivesTheSameGrid() => AssertSame(FurniturePlacementScreenTests.TestRoomMap(9, 7));
    }
}
