using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 세이브 슬롯으로 월드를 시작한다(--save-root, --slot). 세이브 폴더는 테스트마다 새로 만든 임시 폴더다(사용자 세이브를 쓰지 않는다).
    /// 장치 이벤트 주입, OS 입력 계층 미확인.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class SaveSlotScreenTests
    {
        private const int Width = 1280, Height = 720;
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-slot-screen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(string slot)
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--slot", slot, "--size", Width + "x" + Height });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static void AssertAt(PlayerDriver player, string mapId, string spawnId)
        {
            WorldSession world = player.World;
            Assert.That(world.Map.Id, Is.EqualTo(mapId));
            Assert.That(world.Map.TryGetSpawn(spawnId, out var spawn), Is.True);
            Assert.That(world.Walker.X, Is.EqualTo(spawn.X).Within(0.01f));
            Assert.That(world.Walker.Y, Is.EqualTo(spawn.Y).Within(0.01f));
        }

        [Test]
        public void SLOT_PLAY_001_NewSlotStartsInTheHouseAndContinuesWhereTheLastDoorLed()
        {
            var store = new FileSaveStore(_saves);
            using (PlayerDriver player = Start("slot-0"))
            {
                Assert.That(player.Game.World.SlotStart.NewGame, Is.True);
                AssertAt(player, "House", "house-entry");
                Assert.That(store.TryRead("slot-0", SaveFileNames.Metadata, out SaveSlotMetadata metadata), Is.True, "새 슬롯이면 슬롯 정보를 쓴다");
                Assert.That(metadata.SlotId, Is.EqualTo("slot-0"));
                Assert.That(store.TryRead("slot-0", SaveFileNames.WorldProgress, out WorldProgressSaveData entered), Is.True, "Unity 판처럼 처음 들어갈 때도 쓴다");
                Assert.That(entered.CurrentScene, Is.EqualTo("House"));

                Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
                AssertAt(player, "Town", "town-home-entry");
                Assert.That(store.TryRead("slot-0", SaveFileNames.WorldProgress, out WorldProgressSaveData left), Is.True);
                Assert.That(left.Version, Is.EqualTo(1));
                Assert.That(left.SaveSlot, Is.EqualTo("slot-0"));
                Assert.That(left.CurrentScene, Is.EqualTo("Town"));
                Assert.That(left.CurrentEntryPointId, Is.EqualTo("town-home-entry"));
                Assert.That(left.LastPortalDisplayName, Is.EqualTo("town"), "지난 문의 목적지 값");
            }

            using (PlayerDriver again = Start("slot-0"))
            {
                Assert.That(again.Game.World.SlotStart.NewGame, Is.False);
                AssertAt(again, "Town", "town-home-entry");
                again.Screenshot("slot-01-continue-in-town");
            }
        }

        [Test]
        public void SLOT_PLAY_002_SlotWithoutSaveRootIsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--slot", "slot-0" }, name => null));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--slot", "slot-0", "--save-root", _saves, "--world", "Shop" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--slot", "slot-0", "--save-root", _saves, "--hour", "12" }), "슬롯은 학생 시각을 따른다");
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--slot", "slot-0", "--save-root", _saves, "--time-scale", "20" }));
        }
    }
}
