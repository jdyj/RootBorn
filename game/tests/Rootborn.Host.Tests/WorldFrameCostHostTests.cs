using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using Rootborn.Host.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 월드 그리기의 프레임 비용과 그림 캐시(오래 플레이했을 때). 벽시계가 아니라 흔들리지 않는 값(할당 바이트, 캐시 바이트)을 본다.
    /// 할당 검사는 입력이 아니라 프레임 비용을 재는 것이라 월드의 한 프레임(Update·Draw)을 직접 돌린다. 맵 오가기는 장치 이벤트 주입(OS 입력 계층 미확인).
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class WorldFrameCostHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-frame-cost-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1280x720",
                "--evidence", Path.Combine(_root, "output", "host-frame-cost-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>
        /// SIM-003 재현: 가만히 선 프레임에서 월드 갱신(상호작용 후보·NPC 자리 다시 보기)과 월드 그리기(타일·NPC·플레이어 옷 레이어)는
        /// 새 객체를 만들지 않는다. 고치기 전: 갱신이 지역 함수의 클로저(40바이트), 그리기가 옷 레이어 열거자(40바이트)를 매 프레임 만들었다.
        /// </summary>
        [Test]
        public void WORLD_FRAME_001_StandingStill_WorldUpdateAndDraw_AllocateNothing()
        {
            using PlayerDriver player = Start();
            HostGame game = player.Game;
            WorldScreen world = game.World;
            using var batch = new SpriteBatch(game.GraphicsDevice);
            int w = game.Scene.Width, h = game.Scene.Height;
            game.GraphicsDevice.SetRenderTarget(game.Scene);
            const float dt = 1f / 60f;
            for (int i = 0; i < 30; i++) { world.Update(0, 0, dt); world.Draw(batch, w, h); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            const int Frames = 600;
            for (int i = 0; i < Frames; i++) { world.Update(0, 0, dt); world.Draw(batch, w, h); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            game.GraphicsDevice.SetRenderTarget(null);
            TestContext.Progress.WriteLine("WORLD_FRAME_001 " + world.Session.Map.Id + ": 가만히 선 " + Frames + "프레임 할당 " + bytes + "바이트");
            Assert.That(bytes, Is.EqualTo(0), "가만히 선 프레임마다 " + (bytes / (double)Frames).ToString("0.0") + "바이트를 새로 잡았다");
        }

        /// <summary>
        /// 그림 캐시는 한도 안이다: 한도를 넘으면 맵에 들어간 뒤 그 맵이 쓰지 않는 그림부터(오래 안 쓴 것부터) 푼다. 한도를 아주 작게 두고
        /// 집 → 마을 → 집으로 문을 걸어 지나면, 돌아온 집에서는 마을 그림이 풀려 캐시가 집에 처음 있었을 때와 같아진다. 다시 마을에 가면 다시 읽는다(빠진 그림 없음).
        /// </summary>
        [Test]
        public void WORLD_FRAME_002_OverTheTextureBudget_LeavingAMapReleasesItsPictures()
        {
            using PlayerDriver player = Start();
            WorldScreen world = player.Game.World;
            world.TextureBudgetBytes = 1; // 늘 넘는다: 지금 맵이 쓰는 것만 남는다
            player.Frames(2);
            long house = world.CachedTextureBytes;
            int houseCount = world.CachedTextureCount;
            Assert.That(house, Is.GreaterThan(0));

            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.Frames(2);
            long town = world.CachedTextureBytes;
            Assert.That(town, Is.GreaterThan(house), "마을 그림을 읽었다");
            player.Screenshot("frame-cost-01-town-small-budget");

            Assert.That(player.WalkThroughDoor("house"), Is.EqualTo("House"));
            player.Frames(2);
            TestContext.Progress.WriteLine($"WORLD_FRAME_002 캐시(MB, 추정): 집 {house / 1048576.0:0.0}({houseCount}장) → 마을 {town / 1048576.0:0.0} → 집 {world.CachedTextureBytes / 1048576.0:0.0}({world.CachedTextureCount}장)");
            Assert.That(world.CachedTextureBytes, Is.EqualTo(house), "마을 그림이 풀렸다");
            Assert.That(world.CachedTextureCount, Is.EqualTo(houseCount));

            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.Frames(2);
            Assert.That(world.CachedTextureBytes, Is.EqualTo(town), "다시 들어간 마을은 그림을 다시 읽는다");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            player.Screenshot("frame-cost-02-town-again");
        }

        /// <summary>기본 한도에서는 지금 data의 모든 맵 그림이 한도 안이다(맵을 오가도 다시 읽지 않는다). 한도는 맵을 새로 그리면 다시 본다.</summary>
        [Test]
        public void WORLD_FRAME_003_DefaultBudget_HoldsMoreThanTheTownAndHouse()
        {
            using PlayerDriver player = Start();
            WorldScreen world = player.Game.World;
            Assert.That(world.TextureBudgetBytes, Is.EqualTo(WorldScreen.DefaultTextureBudgetBytes));
            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.Frames(2);
            int count = world.CachedTextureCount;
            Assert.That(player.WalkThroughDoor("house"), Is.EqualTo("House"));
            player.Frames(2);
            Assert.That(world.CachedTextureCount, Is.EqualTo(count), "한도 안이면 풀지 않는다");
            Assert.That(world.CachedTextureBytes, Is.LessThanOrEqualTo(world.TextureBudgetBytes));
        }
    }
}
