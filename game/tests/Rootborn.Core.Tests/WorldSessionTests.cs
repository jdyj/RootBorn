// 옮겨 옴: Assets/Scripts/Game/World/WorldPortal.cs, WorldTravelRuntime.cs (문 → 목적지 맵의 도착 지점)
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    public sealed class WorldSessionTests
    {
        private const int Cell = 32;

        private sealed class Maps : IWorldMapSource
        {
            public readonly Dictionary<string, WorldMap> All = new Dictionary<string, WorldMap>();
            public int Loads;
            public WorldMap Load(string mapId) { Loads++; return All.TryGetValue(mapId, out var map) ? map : null; }
        }

        private static Maps TwoRooms()
        {
            var maps = new Maps();
            // 가게: 위쪽 가운데 문 → 마을의 가게 앞
            CollisionGrid shop = WorldMovementTests.Grid(
                "##.##",
                "#...#",
                "#...#",
                "#####");
            var shopDoor = DoorArea.FromEntity(2 * Cell, 0, Cell, Cell, "town", Cell, new TravelTarget("Town", "town-shop-entry"));
            maps.All["Shop"] = new WorldMap("Shop", shop, new[] { shopDoor }, new Dictionary<string, (float, float)> { { "shop-entry", (2.5f * Cell, 2.5f * Cell) } });
            CollisionGrid town = WorldMovementTests.Grid(
                "#######",
                "#.....#",
                "#.....#",
                "#######");
            var townDoor = DoorArea.FromEntity(5 * Cell, 1 * Cell, Cell, Cell, "shopping center", Cell, new TravelTarget("Shop", "shop-entry"));
            maps.All["Town"] = new WorldMap("Town", town, new[] { townDoor }, new Dictionary<string, (float, float)> { { "town-shop-entry", (2.5f * Cell, 1.5f * Cell) } });
            return maps;
        }

        [Test]
        public void WORLD_SESSION_001_WalkingIntoADoorMovesToTheDestinationSpawnAfterTheDoorOpens()
        {
            Maps maps = TwoRooms();
            var session = new WorldSession(maps, WalkerSettings.UnityPlayer);
            Assert.That(session.Enter("Shop", "shop-entry"), Is.True);
            bool travelled = false;
            for (int i = 0; i < 120 && !travelled; i++) travelled = session.Update(0, -1, 1f / 60f);
            Assert.That(travelled, Is.True);
            Assert.That(session.Map.Id, Is.EqualTo("Town"));
            Assert.That(session.Walker.X, Is.EqualTo(2.5f * Cell));
            Assert.That(session.Walker.Y, Is.EqualTo(1.5f * Cell));
            Assert.That(session.Walker.Facing, Is.EqualTo(Facing.Up), "보던 방향은 이어진다");
            Assert.That(session.Travels, Is.EqualTo(1));
        }

        [Test]
        public void WORLD_SESSION_002_UnknownMapOrSpawnIsRefusedAndKeepsTheCurrentPlace()
        {
            Maps maps = TwoRooms();
            var session = new WorldSession(maps, WalkerSettings.UnityPlayer);
            Assert.That(session.Enter("Nowhere", "x"), Is.False);
            Assert.That(session.LastProblem, Does.Contain("Nowhere"));
            Assert.That(session.Enter("Shop", "shop-entry"), Is.True);
            Assert.That(session.Enter("Town", "missing"), Is.False);
            Assert.That(session.Map.Id, Is.EqualTo("Shop"));
        }

        [Test]
        public void WORLD_SESSION_003_DoorToAMissingSpawnDoesNotMoveThePlayer()
        {
            Maps maps = TwoRooms();
            var broken = DoorArea.FromEntity(2 * Cell, 0, Cell, Cell, "town", Cell, new TravelTarget("Town", "no-such-spawn"));
            WorldMap shop = maps.All["Shop"];
            var shopSpawns = new Dictionary<string, (float, float)> { { "shop-entry", (2.5f * Cell, 2.5f * Cell) } };
            maps.All["Shop"] = new WorldMap("Shop", shop.Collision, new[] { broken }, shopSpawns);
            var session = new WorldSession(maps, WalkerSettings.UnityPlayer);
            session.Enter("Shop", "shop-entry");
            for (int i = 0; i < 120; i++) Assert.That(session.Update(0, -1, 1f / 60f), Is.False);
            Assert.That(session.Map.Id, Is.EqualTo("Shop"));
            Assert.That(session.LastProblem, Does.Contain("no-such-spawn"));
        }

        [Test]
        public void WORLD_SESSION_004_RoundTripShopTownShop()
        {
            Maps maps = TwoRooms();
            var session = new WorldSession(maps, WalkerSettings.UnityPlayer);
            session.Enter("Shop", "shop-entry");
            for (int i = 0; i < 120 && session.Map.Id == "Shop"; i++) session.Update(0, -1, 1f / 60f);
            Assert.That(session.Map.Id, Is.EqualTo("Town"));
            for (int i = 0; i < 240 && session.Map.Id == "Town"; i++) session.Update(1, 0, 1f / 60f);
            Assert.That(session.Map.Id, Is.EqualTo("Shop"));
            Assert.That(session.Travels, Is.EqualTo(2));
        }

        /// <summary>
        /// 몸 밑에 막는 칸이 생기면(같이 쓰는 집에서 남이 가구를 놓았다, 혼자서 자기 자리에 놓았다) 다음 프레임에 가장 가까운 빈 칸으로 비켜난다 —
        /// 그대로 두면 어느 쪽으로도 움직일 수 없어 갇힌다(시뮬레이션 방 판에서 찾음). 비켜난 뒤에는 벽·가구를 지나지 못한다.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        public void WORLD_SESSION_005_ABodyCaughtUnderNewFurniture_StepsAsideToTheNearestFreeCell(int furnitureSize)
        {
            var rows = new[]
            {
                "#######",
                "#.....#",
                "#.....#",
                "#.....#",
                "#######",
            };
            int cols = rows[0].Length;
            var blocked = new bool[cols * rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < cols; x++)
                    blocked[y * cols + x] = rows[y][x] == '#';
            var grid = new CollisionGrid(new MapGrid(cols, rows.Length, Cell), blocked);
            var maps = new Maps();
            maps.All["House"] = new WorldMap("House", grid, new DoorArea[0], new Dictionary<string, (float, float)> { { "bed", (2.5f * Cell, 2.5f * Cell) } });
            var session = new WorldSession(maps, WalkerSettings.UnityPlayer);
            Assert.That(session.Enter("House", "bed"), Is.True);

            // 가구가 몸이 선 칸(2,2)에(크기 2면 (2,2)~(3,3)) 놓인다: 막힌 칸 표가 바뀐다(FurnitureRoom.SetFurnitureBlocking과 같다).
            for (int y = 2; y < 2 + furnitureSize; y++)
                for (int x = 2; x < 2 + furnitureSize; x++)
                    blocked[y * cols + x] = true;
            Assert.That(grid.AnyBlocked(session.Walker.Box), Is.True, "준비: 몸이 가구 안에 있다");

            session.Update(0, 0, 1f / 60f);
            Assert.That(grid.AnyBlocked(session.Walker.Box), Is.False, "가장 가까운 빈 칸으로 비켜났다");
            float sx = session.Walker.X, sy = session.Walker.Y;
            Assert.That(System.Math.Abs(sx - 2.5f * Cell) + System.Math.Abs(sy - 2.5f * Cell), Is.LessThanOrEqualTo(Cell * 1.01f), "한 칸 옆으로");

            for (int i = 0; i < 120; i++)
            {
                session.Update(1, 0, 1f / 60f);
                Assert.That(grid.AnyBlocked(session.Walker.Box), Is.False, "비켜난 뒤에는 막힌 칸에 들어가지 않는다");
            }
            for (int i = 0; i < 120; i++) session.Update(0, 1, 1f / 60f);
            Assert.That(grid.AnyBlocked(session.Walker.Box), Is.False);
        }
    }
}
