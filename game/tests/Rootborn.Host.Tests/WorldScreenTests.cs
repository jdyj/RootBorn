using System.IO;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 월드 화면을 실제 창과 게임 루프로 띄워 방향 키로만 걷는다(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 창 하나로 가게 → 마을 → 집 → 마을 순서로 이어 걷는다(앞 테스트가 실패하면 뒤 테스트의 시작 맵도 어긋난다).
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class WorldScreenTests
    {
        private const int Width = 1280, Height = 720;
        private PlayerDriver _player;
        private string _evidence;

        [OneTimeSetUp]
        public void StartGame()
        {
            HostOptions options = HostOptions.Resolve(new[] { "--world", "Shop", "--size", Width + "x" + Height });
            options.TestMode = true;
            _evidence = options.EvidenceDir;
            _player = new PlayerDriver(options);
            _player.Frames(3);
        }

        [OneTimeTearDown]
        public void StopGame()
        {
            _player?.Dispose(); // 창을 닫는다
            _player = null;
        }

        private WorldSession World => _player.World;

        private void AssertAt(string mapId, string spawnId)
        {
            Assert.That(World.Map.Id, Is.EqualTo(mapId));
            Assert.That(World.Map.TryGetSpawn(spawnId, out var spawn), Is.True);
            Assert.That(World.Walker.X, Is.EqualTo(spawn.X).Within(0.01f));
            Assert.That(World.Walker.Y, Is.EqualTo(spawn.Y).Within(0.01f));
        }

        [Test, Order(1)]
        public void WORLD_PLAY_001_ShopStartsAtTheShopEntryWithTheCameraInsideTheMap()
        {
            AssertAt("ShopInterior", "shop-entry");
            Assert.That(_player.Game.World.Zoom, Is.EqualTo(2), "720 / (13칸 × 32) → 가장 가까운 정수 2");
            string shot = _player.Screenshot("01-shop-start");
            Assert.That(File.Exists(shot), Is.True);
            Microsoft.Xna.Framework.Vector2 camera = _player.Game.World.CameraOffset;
            MapGrid grid = World.Map.Grid;
            Assert.That(camera.X, Is.GreaterThanOrEqualTo(0f));
            Assert.That(camera.Y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(camera.X + Width / 2f, Is.LessThanOrEqualTo(grid.PixelWidth));
            Assert.That(camera.Y + Height / 2f, Is.LessThanOrEqualTo(grid.PixelHeight));
        }

        [Test, Order(2)]
        public void WORLD_PLAY_002_WalkingIntoAWallStopsAtItsEdge()
        {
            // 가게 도착 지점에서 오른쪽(진열대 쪽)으로 키를 누르고 있어도 막힌 칸에는 들어가지 않는다.
            CollisionGrid grid = World.Map.Collision;
            int size = grid.Grid.CellSize;
            CellPos start = _player.PlayerCell;
            int wall = start.X + 1;
            while (!grid.IsBlocked(wall, start.Y)) wall++;
            _player.HoldKey(TestKey.Right, 120);
            Assert.That(World.Walker.Box.Right, Is.LessThanOrEqualTo(wall * size + 0.001f), "막힌 칸 " + new CellPos(wall, start.Y) + " 앞에서 멈춘다");
            Assert.That(World.Walker.Box.Right, Is.GreaterThan(wall * size - 3f), "벽 바로 앞까지는 간다");
            Assert.That(_player.CollisionViolations, Is.EqualTo(0));
            _player.Screenshot("02-shop-against-shelf");
        }

        [Test, Order(3)]
        public void WORLD_PLAY_003_WalkFromTheShopToItsDoorAndComeOutInFrontOfTheShopInTown()
        {
            Assert.That(World.Map.Id, Is.EqualTo("ShopInterior"));
            _player.WalkTo(new CellPos(7, 4)); // 문 왼쪽 아래. 문(그림 기준 2~3행, 9~10열)에 닿지 않는 칸
            _player.Screenshot("03-shop-near-door");
            Assert.That(_player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            AssertAt("Town", "town-shop-entry");
            Assert.That(_player.Game.World.Zoom, Is.EqualTo(1), "720 / (18칸 × 32) = 1.25 → 1");
            Assert.That(_player.CollisionViolations, Is.EqualTo(0));
            _player.Screenshot("04-town-out-of-shop");
        }

        [Test, Order(4)]
        public void WORLD_PLAY_004_WalkAcrossTownIntoTheHouse()
        {
            Assert.That(World.Map.Id, Is.EqualTo("Town"));
            Assert.That(_player.WalkThroughDoor("house"), Is.EqualTo("House"));
            AssertAt("House", "house-entry");
            Assert.That(_player.CollisionViolations, Is.EqualTo(0));
            _player.Screenshot("05-house-entry");
        }

        [Test, Order(5)]
        public void WORLD_PLAY_005_WalkBetweenHouseFurnitureAndLeaveThroughTheHouseDoor()
        {
            Assert.That(World.Map.Id, Is.EqualTo("House"));
            // 왼쪽 위 방(가구가 있는 방)까지 걸어가 가구 아래에 선다.
            _player.WalkTo(new CellPos(17, 3)); // 책상(17,4) 바로 위: 책상이 캐릭터 앞에 그려져야 한다
            _player.Screenshot("06-house-by-furniture");
            Assert.That(_player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            AssertAt("Town", "town-home-entry");
            Assert.That(_player.CollisionViolations, Is.EqualTo(0));
            _player.Screenshot("07-town-out-of-house");
        }
    }
}
