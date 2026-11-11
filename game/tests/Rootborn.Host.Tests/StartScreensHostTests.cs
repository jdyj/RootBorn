using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Characters;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Screens;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 시작 화면 묶음: --slot 없이 세이브 폴더만 주고 띄우면 슬롯 선택부터 시작하고, 새 이야기 → 이름(한글) → 겉모습 → 시작하기로 집에서
    /// 게임이 시작되며, 다시 띄우면 그 슬롯에 이름이 보이고 이어서 시작으로 같은 자리에 선다. 실제 창과 게임 루프, 포인터·키·글자 이벤트만
    /// 쓴다(장치 이벤트 주입, OS 입력 계층 미확인). 세이브 폴더는 임시 폴더다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class StartScreensHostTests
    {
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-start-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Launch(int width = 1280, int height = 720)
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--size", width + "x" + height });
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-screens-evidence");
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static void AssertAt(PlayerDriver player, string mapId, string spawnId)
        {
            WorldSession world = player.World;
            Assert.That(world.Map.Id, Is.EqualTo(mapId));
            Assert.That(world.Map.TryGetSpawn(spawnId, out var spawn), Is.True);
            Assert.That((world.Walker.X, world.Walker.Y), Is.EqualTo((spawn.X, spawn.Y)));
        }

        [Test]
        public void START_PLAY_001_NewStoryWithAKoreanName_StartsInTheHouse_ThenContinuesWhereItLeft()
        {
            using (PlayerDriver player = Launch())
            {
                Assert.That(player.Game.World, Is.Null, "슬롯을 고르기 전에는 월드가 없다");
                Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
                Assert.That(((UiLabel)player.Node(SaveSlotScreen.CardTitleId(0))).Text, Is.EqualTo("새로운 이야기"));
                player.ScreenshotScreen(ScreenCatalog.SaveSlotSelect, "start-01-slots-empty");

                player.PressButton(SaveSlotScreen.NewGameId(0));
                player.WaitForScreen(ScreenCatalog.CharacterCreator);
                Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.False);
                player.ScreenshotScreen(ScreenCatalog.CharacterCreator, "start-02-creator");

                // 이름: 글자 칸을 눌러 입력기를 켜고, 조합 중 글자와 확정 글자를 차례로 넣는다.
                player.PressButton(CharacterCreatorScreen.NameFieldId);
                Assert.That(player.Game.TextInputActive, Is.True);
                player.Compose("ㅎ");
                player.Compose("하");
                player.TypeText("하");
                player.Compose("ㄴ");
                player.Compose("느");
                player.Compose("늘");
                player.Screenshot("start-03-name-composing");
                player.TypeText("늘");
                var creator = (CharacterCreatorScreen)player.Screens.Find(ScreenCatalog.CharacterCreator);
                Assert.That(creator.PlayerName, Is.EqualTo("하늘"));
                player.PressKey(TestKey.Escape); // 글자 칸에서 나온다
                Assert.That(player.Game.TextInputActive, Is.False);

                // 겉모습: 학교 다닐 때의 옷을 하나 넘긴다(사복 → 교복).
                player.PressButton(CharacterCreatorScreen.StageNextId(1));
                Assert.That(creator.Model.ChoiceAt(1).Id, Is.EqualTo("school_short_brown_school_uniform"));
                Assert.That(((UiLabel)player.Node(CharacterCreatorScreen.StageChoiceId(1))).Text, Is.EqualTo("교복"));
                var preview = (CharacterPreview)player.Node(CharacterCreatorScreen.CharacterId);
                Assert.That(preview.Frames.Count, Is.EqualTo(3), "몸·옷·머리");
                player.ScreenshotScreen(ScreenCatalog.CharacterCreator, "start-04-creator-uniform");

                player.PressButton(CharacterCreatorScreen.ConfirmId);
                player.Frames(3);
                Assert.That(player.Game.World, Is.Not.Null, "시작하기로 월드에 들어간다");
                AssertAt(player, "House", "house-entry");
                Assert.That(player.Screens.IsOpen(ScreenCatalog.HudClock), Is.True, "게임 화면 쌓기로 바뀌었다");
                Assert.That(player.Game.Session?.SaveSlot, Is.EqualTo("slot-0"));
                // 첫날은 놀이터 몸이고 그 단계의 옷은 하나뿐이다. 고른 학교 옷은 학교 몸으로 바뀔 때 입도록 옷 선택에 들어갔다.
                Assert.That(player.Game.World.PlayerLook.Body.Id, Is.EqualTo("body_playground"));
                Assert.That(player.Game.World.PlayerLook.Layers.Select(l => l.Id), Is.EqualTo(new[] { "outfit_playground_casual", "head_playground_short_brown" }));
                string uniform = player.Game.World.Content.Characters.LegacyAppearanceMap()["school_short_brown_school_uniform"];
                Assert.That(new FileSaveStore(_saves).TryRead("slot-0", SaveFileNames.Wearables(SaveFileNames.DefaultPlayerId), out WearableSaveData worn), Is.True);
                Assert.That(worn.entries.Select(e => e.wearableId), Does.Contain(uniform));
                Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "그림을 다 찾았다");
                player.Screenshot("start-05-house");

                Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
                AssertAt(player, "Town", "town-home-entry");
            }

            var store = new FileSaveStore(_saves);
            Assert.That(store.TryRead("slot-0", SaveFileNames.Metadata, out SaveSlotMetadata metadata), Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("하늘"));
            Assert.That(metadata.CharacterAppearanceSnapshot.selectedParts.Select(p => p.partId), Does.Contain("school_short_brown_school_uniform"));

            using (PlayerDriver again = Launch())
            {
                Assert.That(again.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "다시 띄워도 슬롯 선택부터");
                Assert.That(((UiLabel)again.Node(SaveSlotScreen.CardTitleId(0))).Text, Is.EqualTo("하늘"));
                Assert.That(again.Node(SaveSlotScreen.ContinueId(0)).IsActive, Is.True);
                Assert.That(((CharacterPreview)again.Node(SaveSlotScreen.CardPreviewId(0))).Frames.Count, Is.EqualTo(3));
                again.ScreenshotScreen(ScreenCatalog.SaveSlotSelect, "start-06-slots-saved");
                Assert.That(again.Game.Renderer.MissingImages, Is.Empty);

                again.PressButton(SaveSlotScreen.ContinueId(0));
                again.Frames(3);
                Assert.That(again.Game.World, Is.Not.Null);
                AssertAt(again, "Town", "town-home-entry");
                again.Screenshot("start-07-continue-in-town");
            }
        }

        [Test]
        public void START_PLAY_002_DeletingAStory_AsksFirst_ThenTheSlotIsEmpty()
        {
            var store = new FileSaveStore(_saves);
            SaveSlotMetadata story = SaveSlots.CreateMetadata("slot-1", "바다", null, DateTime.UtcNow.Ticks, 1);
            store.Write("slot-1", SaveFileNames.Metadata, story);
            using PlayerDriver player = Launch(1920, 1080);
            Assert.That(((UiLabel)player.Node(SaveSlotScreen.CardTitleId(1))).Text, Is.EqualTo("바다"));
            player.PressButton(SaveSlotScreen.DeleteId(1));
            player.WaitForScreen(ScreenCatalog.DeleteSlot);
            Assert.Throws<InvalidOperationException>(() => player.PressButton(SaveSlotScreen.ContinueId(1)), "확인 창이 아래 버튼을 가린다");
            player.ScreenshotScreen(ScreenCatalog.DeleteSlot, "start-08-delete-confirm");
            player.PressKey(TestKey.Enter); // 포커스는 취소에 있다
            player.WaitForScreenClosed(ScreenCatalog.DeleteSlot);
            Assert.That(Directory.Exists(Path.Combine(_saves, "slot-1")), Is.True, "취소는 지우지 않는다");

            player.PressButton(SaveSlotScreen.DeleteId(1));
            player.WaitForScreen(ScreenCatalog.DeleteSlot);
            player.PressButton(DeleteSlotScreen.ConfirmId);
            player.WaitForScreenClosed(ScreenCatalog.DeleteSlot);
            Assert.That(Directory.Exists(Path.Combine(_saves, "slot-1")), Is.False);
            Assert.That(player.Node(SaveSlotScreen.NewGameId(1)).IsActive, Is.True);
            player.ScreenshotScreen(ScreenCatalog.SaveSlotSelect, "start-09-deleted");
        }

        [Test]
        public void START_PLAY_003_NoArgumentsWithoutASaveFolder_IsRefusedWithAHint()
        {
            HostOptions options = HostOptions.Resolve(Array.Empty<string>(), name => name == HostOptions.SaveRootVariable ? null : Environment.GetEnvironmentVariable(name));
            Assert.That(options.StartMenu, Is.False, "세이브 폴더를 코드에 정해 두지 않는다");
            HostOptions withEnv = HostOptions.Resolve(Array.Empty<string>(), name => name == HostOptions.SaveRootVariable ? _saves : Environment.GetEnvironmentVariable(name));
            Assert.That(withEnv.StartMenu, Is.True);
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--hour", "9" }), "슬롯은 학생 시각을 따른다");
        }
    }
}
