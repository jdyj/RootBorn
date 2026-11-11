using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.AdultFacility;
using Rootborn.Screens.Career;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Minigames;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 실제 맵 데이터(game/world/interactions.json, 시험용 --interactions 파일 없이)의 화면을 여는 지점에 새 게임에서 걸어가 E를 누른다.
    /// 종류마다 대표 하나: 탐색(exploration), 진로 연습(career-practice → 미니게임), 성인 시설(career-facility).
    /// 집에서 문을 지나 마을 북쪽 광장의 임시 지점까지 방향 키로 걷는다(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 스크린샷: output/host-screens-evidence/world-entry-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class WorldEntryPointsHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-world-entry-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        /// <summary>새 게임(새 슬롯): 집 안 시작 자리에서 시작한다. --interactions를 주지 않는다 — 실제 파일을 쓴다.</summary>
        private PlayerDriver StartNewGame()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            Assert.That(options.InteractionsPath ?? string.Empty, Does.Not.Contain(_saves), "시험용 상호작용 파일을 쓰지 않는다");
            var player = new PlayerDriver(options);
            player.Frames(3);
            Assert.That(player.World.Map.Id, Is.EqualTo("House"), "새 게임은 집에서 시작한다");
            return player;
        }

        /// <summary>집 문 'town'을 지나 마을로 걸어 나간다. 저절로 뜬 하루 이벤트가 있으면 닫기로 닫는다.</summary>
        private static void WalkOutToTown(PlayerDriver player)
        {
            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.Frames(2);
            if (player.Screens.IsOpen(ScreenCatalog.DailyEvent))
            {
                player.PressButton(DailyEventScreen.CloseId);
                player.WaitForScreenClosed(ScreenCatalog.DailyEvent);
            }
        }

        /// <summary>지금 맵의 그 종류 지점(실제 데이터에서 찾는다)까지 걸어가 그것이 E의 대상인지 본다.</summary>
        private static InteractionThing WalkToPoint(PlayerDriver player, string kind)
        {
            InteractionThing thing = player.Game.World.HereData.Things.FirstOrDefault(t => t.Kind == kind);
            Assert.That(thing, Is.Not.Null, player.World.Map.Id + "에 " + kind + " 지점이 없다");
            player.WalkNear(thing.X, thing.Y, player.World.Map.Grid.CellSize * 1.25f);
            var target = player.Game.World.Interactions.Target;
            Assert.That((target?.Kind, target?.Id), Is.EqualTo((thing.Kind, thing.Id)), "걸어간 자리에서 E의 대상");
            return thing;
        }

        private static void AssertClean(PlayerDriver player)
        {
            Assert.That(player.CollisionViolations, Is.EqualTo(0), "걷는 동안 막힌 칸에 들어가지 않는다");
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void WORLD_ENTRY_001_NewGame_WalkToTheTownExplorationPoint_E_ExplorationOpens()
        {
            using PlayerDriver player = StartNewGame();
            WalkOutToTown(player);
            InteractionThing point = WalkToPoint(player, "exploration");
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Exploration);
            var screen = (ExplorationScreen)player.Screens.Find(ScreenCatalog.Exploration);
            Assert.That(screen.Model.InteractionId, Is.EqualTo(point.Id));
            Assert.That(screen.Model.Choices, Is.Not.Empty, "탐색 선택지가 보인다");
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.Exploration, "world-entry-01-exploration");

            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Exploration);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            AssertClean(player);
        }

        [Test]
        public void WORLD_ENTRY_002_NewGame_WalkToTheTownPracticePoint_E_MinigameOpens()
        {
            using PlayerDriver player = StartNewGame();
            WalkOutToTown(player);
            WalkToPoint(player, "career-practice");
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Minigame);
            Assert.That(player.Game.Session.Student.HasPendingMinigame, Is.True, "E로 연습을 예약했다");
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Minigame, "world-entry-02-practice-minigame");

            // 닫으면 끝나지 않은 판은 포기로 정산된다.
            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Minigame);
            Assert.That(player.Game.Session.Student.HasPendingMinigame, Is.False);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            AssertClean(player);
        }

        [Test]
        public void WORLD_ENTRY_003_NewGame_WalkToTheTownFacilityPoint_E_FacilityOpens()
        {
            using PlayerDriver player = StartNewGame();
            WalkOutToTown(player);
            InteractionThing point = WalkToPoint(player, FacilityScreen.InteractionKind);
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Facility);
            var screen = (FacilityScreen)player.Screens.Find(ScreenCatalog.Facility);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            Assert.That(screen.Model.Works, Is.Not.Empty, "시설의 일 목록이 보인다 (" + point.Id + ")");
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.Facility, "world-entry-03-facility");

            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Facility);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            AssertClean(player);
        }
    }
}
