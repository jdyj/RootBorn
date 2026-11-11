using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Characters;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.Screens.Records;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 묶음 10(도감·옷장·마을 기록)을 실제 창과 게임 루프로: 새 슬롯 → HUD 버튼을 포인터로 눌러 화면을 열고, 휠로 스크롤하고, 닫기.
    /// 옷장은 옷 카드를 포인터로 눌러 캐릭터 겉모습이 바뀌고 슬롯에 저장되어 다시 시작해도 유지되는지 본다.
    /// 조작은 포인터·키뿐(장치 이벤트 주입, OS 입력 계층 미확인). 새 게임은 첫 성장 단계라 고를 옷이 하나뿐이어서,
    /// 옷을 바꾸는 시험은 게임 한 판을 다음 날로 넘겨 둔 슬롯(준비 단계: GameSession 명령으로 하루 넘기기)에서 한다.
    /// 스크린샷: output/host-screens-evidence/records-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class RecordsHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-records-" + Guid.NewGuid().ToString("N"));
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
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static T Screen<T>(PlayerDriver player, string id) where T : GameScreen => (T)player.Screens.Find(id);

        [Test]
        public void RECORDS_001_NewSlot_EncyclopediaButton_Open_Scroll_Close_ThenWardrobeButton_ThenEscape()
        {
            using PlayerDriver player = Start();

            // 도감: HUD 버튼을 포인터로 누른다.
            player.PressButton("hud.button.encyclopedia");
            player.WaitForScreen(ScreenCatalog.Encyclopedia);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Encyclopedia, "records-01-encyclopedia-open");
            var screen = Screen<EncyclopediaScreen>(player, ScreenCatalog.Encyclopedia);
            Assert.That(screen.Model.Categories.Count, Is.GreaterThan(0));
            Assert.That(((UiLabel)player.Node(EncyclopediaScreen.TitleId)).Text, Is.EqualTo(player.Game.Text.Get("ui.encyclopedia.open")));

            // 분류를 눌러 바꾸고, 목록에 휠을 굴린다(항목이 적어 스크롤은 움직이지 않을 수 있다 — 입력이 오류 없이 들어가는지).
            player.PressButton(EncyclopediaScreen.CategoryId(Math.Min(1, screen.Model.Categories.Count - 1)));
            player.MovePointerTo(EncyclopediaScreen.ListId);
            player.Wheel(-3);
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Encyclopedia, "records-02-encyclopedia-category");

            // 닫기 버튼을 포인터로.
            player.PressButton(EncyclopediaScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.Encyclopedia);
            Assert.That(player.Screens.MovementBlocked, Is.False);

            // 옷장: HUD 버튼을 눌러 열고 Esc로 닫는다.
            player.PressButton("hud.button.wardrobe");
            player.WaitForScreen(ScreenCatalog.Wardrobe);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Wardrobe, "records-03-wardrobe-new-game");
            Assert.That(Screen<WardrobeScreen>(player, ScreenCatalog.Wardrobe).VisibleCardCount, Is.GreaterThan(0), "새 게임에서도 옷장이 비지 않는다");
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.Wardrobe);

            // 마을 기록: HUD 버튼을 눌러 열면 새 게임에는 기록된 변화가 없다는 안내가 보인다. 닫기 버튼을 포인터로.
            player.PressButton("hud.button.world-log");
            player.WaitForScreen(ScreenCatalog.WorldStateLog);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            Assert.That(player.Node(WorldStateLogScreen.EmptyId).Visible, Is.True);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.WorldStateLog, "records-07-world-log-empty");
            player.PressButton(WorldStateLogScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.WorldStateLog);

            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void RECORDS_003_WorldLog_WithChanges_ShowsCards_AndScrolls()
        {
            using PlayerDriver player = Start();
            // 준비(실제 플레이 아님): 월드 상태 변화를 켜 둔다. 새 게임에서 이 변화를 켜는 퀘스트 흐름은 이 시험이 걷지 않는다.
            var session = player.Game.Session;
            int n = 0;
            foreach (Rootborn.Core.WorldState.WorldStateFlag flag in session.Definitions.All<Rootborn.Core.WorldState.WorldStateFlag>())
                session.World.TryActivate(flag, new Rootborn.Core.WorldState.WorldStateActivationSource("chain", "step", "fixture-" + n++, 1));
            player.PressButton("hud.button.world-log");
            player.WaitForScreen(ScreenCatalog.WorldStateLog);
            var screen = Screen<WorldStateLogScreen>(player, ScreenCatalog.WorldStateLog);
            Assert.That(screen.Model.Cards.Count, Is.GreaterThan(0));
            Assert.That(player.Node(WorldStateLogScreen.EmptyId).Visible, Is.False);
            player.MovePointerTo(WorldStateLogScreen.ListId);
            player.Wheel(-3);
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.WorldStateLog, "records-08-world-log-cards");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.WorldStateLog);
        }

        /// <summary>
        /// 장소 기록은 HUD 버튼이 없어(Unity 버튼은 가려져 있었다) 사용자가 여는 길이 아직 없다. 그래서 이 시험은 화면 쌓기에 직접 열라고 해서
        /// 그려진 모습의 결함만 본다(실제 플레이 경로 아님). 장소에 들어간 기록은 GameSession 명령으로 만든다(준비).
        /// </summary>
        [Test]
        public void RECORDS_004_LocationLog_DirectOpen_ScreenshotOnly()
        {
            using PlayerDriver player = Start();
            player.Game.Session.VisitLocation("location.library");
            player.Screens.Open(ScreenCatalog.LocationStateLog);
            player.Frames(3);
            player.ScreenshotScreen(ScreenCatalog.LocationStateLog, "records-09-location-log");
            Assert.That(Screen<LocationStateLogScreen>(player, ScreenCatalog.LocationStateLog).Model.Rows.Count, Is.GreaterThan(0));
            Assert.That(player.Screens.MovementBlocked, Is.False);
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.LocationStateLog);
        }

        /// <summary>준비: 새 슬롯을 한 번 띄워 슬롯 파일을 만들고, 그 슬롯의 게임 한 판을 다음 날(학교 단계)로 넘겨 둔다.</summary>
        private void PrepareSlotAtSchoolStage()
        {
            using (PlayerDriver first = Start()) first.Frames(2);
            string data = Path.Combine(_root, "data");
            var session = GameSession.Start(Rootborn.Host.World.GameContent.Load(_root).Definitions, GameStartSettings.FromRegistry(data),
                new FileSaveStore(_saves), "slot-1", SaveFileNames.DefaultPlayerId);
            Assert.That(session.EndDay().Ended, Is.True);
            Assert.That(session.StartNextDay(), Is.True);
            Assert.That(session.Student.CurrentDay, Is.EqualTo(2));
        }

        [Test]
        public void RECORDS_002_SchoolStage_WardrobeCard_Click_ChangesLook_IsSaved_AndSurvivesRestart()
        {
            PrepareSlotAtSchoolStage();
            string chosenCode;
            string chosenId;
            using (PlayerDriver player = Start())
            {
                string before = player.Game.World.PlayerLook.Code;
                Assert.That(before, Does.Contain("school"), "이어 한 판은 진행에 맞는 성장 단계로 입는다");
                player.PressButton("hud.button.wardrobe");
                player.WaitForScreen(ScreenCatalog.Wardrobe);
                player.MovePointer(-50, -50);
                player.Frames(2);
                var wardrobe = Screen<WardrobeScreen>(player, ScreenCatalog.Wardrobe);
                WardrobeSectionModel outfits = wardrobe.Sections.First(s => s.Cards.Count >= 2);
                player.ScreenshotScreen(ScreenCatalog.Wardrobe, "records-04-wardrobe-school-before");
                WardrobeCardModel target = outfits.Cards.First(c => !c.Selected);
                player.PressButton(wardrobe.CardIdOf(target.WearableId));
                player.MovePointer(-50, -50);
                player.Frames(2);
                chosenId = target.WearableId;
                chosenCode = player.Game.World.PlayerLook.Code;
                Assert.That(chosenCode, Is.Not.EqualTo(before), "옷을 고르면 캐릭터 겉모습이 바뀐다");
                Assert.That(chosenCode, Does.Contain(chosenId));
                Assert.That(wardrobe.Sections.SelectMany(s => s.Cards).Single(c => c.WearableId == chosenId).Selected, Is.True);
                player.ScreenshotScreen(ScreenCatalog.Wardrobe, "records-05-wardrobe-school-after");
                player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.Wardrobe);
                player.Screenshot("records-06-world-after-wardrobe");
                Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            }

            // 다시 시작해도 같은 옷.
            using (PlayerDriver again = Start())
            {
                Assert.That(again.Game.World.PlayerLook.Code, Is.EqualTo(chosenCode));
                Assert.That(again.Game.World.Wearing.Get("slot.outfit", "stage.school"), Is.EqualTo(chosenId).Or.Not.Empty);
            }
        }
    }
}
