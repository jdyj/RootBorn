using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 프로젝트 3 끝 기준: 새 게임 → 집 → 마을 → 시설 실내까지 걸어 다님. 실제 창과 게임 루프, 방향 키와 상호작용 키만 쓴다
    /// (장치 이벤트 주입, OS 입력 계층 미확인). 세이브 폴더는 임시 폴더다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class NewGameJourneyTests
    {
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-journey-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start()
        {
            // 슬롯은 저장된 학생 시각에서 이어지고 시계는 흐른다(배속 1). --hour는 슬롯과 함께 줄 수 없다.
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--slot", "slot-1", "--size", "1280x720" });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static double Brightness(PlayerDriver player)
        {
            RenderTarget2D scene = player.Game.Scene;
            var pixels = new Microsoft.Xna.Framework.Color[scene.Width * scene.Height];
            scene.GetData(pixels);
            double sum = 0;
            for (int i = 0; i < pixels.Length; i += 7) sum += pixels[i].B;
            return sum / ((pixels.Length + 6) / 7);
        }

        private static void AssertAt(PlayerDriver player, string mapId, string spawnId)
        {
            Assert.That(player.World.Map.Id, Is.EqualTo(mapId));
            Assert.That(player.World.Map.TryGetSpawn(spawnId, out var spawn), Is.True);
            Assert.That((player.World.Walker.X, player.World.Walker.Y), Is.EqualTo((spawn.X, spawn.Y)));
        }

        [Test]
        public void JOURNEY_001_NewSlotHouseNpcTownShopThenContinueInTheShop()
        {
            using (PlayerDriver player = Start())
            {
                // 새 게임: 집 입구, 8시(오전), 첫 안내 NPC가 집에 있다.
                Assert.That(player.Game.World.SlotStart.NewGame, Is.True);
                AssertAt(player, "House", "house-entry");
                player.Screenshot("journey-01-new-game-house");

                NpcOnMap guide = player.Game.World.Npcs[0];
                Assert.That(guide.NpcId, Is.EqualTo("npc.first-guide"));
                player.WalkNear(guide.Pose.X, guide.Pose.Y + 32f, 20f);
                Assert.That(player.Game.World.Interactions.Target?.Id, Is.EqualTo("npc.first-guide"));
                player.PressKey(TestKey.E);
                Assert.That(player.Game.World.InteractionLog, Is.EqualTo(new[] { new InteractionEvent("House", "npc", "npc.first-guide") }));
                player.Screenshot("journey-02-house-npc");

                Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
                AssertAt(player, "Town", "town-home-entry");
                LightColor arrived = player.Game.World.Tint;
                double arrivedBlue = Brightness(player);
                float arrivedHour = player.Game.World.CurrentHour;
                Assert.That(arrivedHour, Is.EqualTo(player.Game.Session.StudentHour), "마을 낮밤은 학생 시각을 따른다(Unity 판과 같음)");
                player.Screenshot("journey-03-town-arrived");

                player.Frames(450); // 키를 누르지 않고 프레임만 흐른다 — 학생 시각은 활동으로만 흐르므로 빛도 그대로다
                Assert.That(player.Game.World.CurrentHour, Is.EqualTo(arrivedHour));
                Assert.That(player.Game.World.Tint, Is.EqualTo(arrived));
                Assert.That(Math.Abs(Brightness(player) - arrivedBlue), Is.LessThan(1.0), "화면 색이 그대로다");
                player.Screenshot("journey-04-town-later");

                Assert.That(player.WalkThroughDoor("shopping center"), Is.EqualTo("ShopInterior"));
                AssertAt(player, "ShopInterior", "shop-entry");
                Assert.That(player.Game.World.Tint.R, Is.EqualTo(1f), "가게 안은 낮밤이 없다");
                Assert.That(player.CollisionViolations, Is.EqualTo(0));
                player.Screenshot("journey-05-shop");
            }

            Assert.That(new FileSaveStore(_saves).TryRead("slot-1", SaveFileNames.WorldProgress, out WorldProgressSaveData saved), Is.True);
            Assert.That((saved.CurrentScene, saved.CurrentEntryPointId, saved.LastPortalDisplayName), Is.EqualTo(("ShopInterior", "shop-entry", "shopping center")));

            using (PlayerDriver again = Start())
            {
                Assert.That(again.Game.World.SlotStart.NewGame, Is.False);
                AssertAt(again, "ShopInterior", "shop-entry");
                again.Screenshot("journey-06-continue-in-shop");
            }
        }
    }
}
