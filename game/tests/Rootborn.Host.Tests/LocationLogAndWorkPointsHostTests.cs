using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Minigames;
using Rootborn.Screens.Records;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 장소 기록을 여는 길과 나머지 알바 지점(사용자 결정 2026-11-12)을 실제 창과 게임 루프로, 실제 맵 데이터(game/world/interactions.json)에서:
    /// 1) 새 게임 → 집 문 → 마을 북쪽 광장의 도서관 지점까지 걸어가 E(장소 방문) → 닫기 → L 키로 장소 기록을 열어 카드를 보고 L로 닫는다 →
    ///    HUD의 장소 기록 버튼을 포인터로 눌러 다시 연다.
    /// 2) 새 게임 → 마을 → 가게가 아닌 알바(병원 도우미) 지점까지 걸어가 E → 미니게임이 열린다.
    /// 조작은 방향 키·E·L·Esc·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인). 준비 단계의 상태 강제 설정은 없다.
    /// 스크린샷: output/host-screens-evidence/location-log-*.png, work-point-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class LocationLogAndWorkPointsHostTests
    {
        private const string LocationLogButton = "hud.button.location-log";
        private const string RepresentativeWork = "work.clinic-helper";

        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-location-log-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver StartNewGame()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            Assert.That(player.World.Map.Id, Is.EqualTo("House"), "새 게임은 집에서 시작한다");
            return player;
        }

        private static void WalkOutToTown(PlayerDriver player)
        {
            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.Frames(2);
            CloseDailyEventIfOpen(player);
        }

        private static void CloseDailyEventIfOpen(PlayerDriver player)
        {
            if (!player.Screens.IsOpen(ScreenCatalog.DailyEvent)) return;
            player.PressButton(DailyEventScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.DailyEvent);
        }

        /// <summary>지금 맵의 그 지점(실제 데이터에서 찾는다)까지 걸어가 그것이 E의 대상인지 본다.</summary>
        private static InteractionThing WalkToPoint(PlayerDriver player, string kind, string id)
        {
            InteractionThing thing = player.Game.World.HereData.Things.FirstOrDefault(t => t.Kind == kind && t.Id == id);
            Assert.That(thing, Is.Not.Null, player.World.Map.Id + "에 " + kind + "/" + id + " 지점이 없다");
            player.WalkNear(thing.X, thing.Y, player.World.Map.Grid.CellSize * 0.5f);
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
        public void LOCATION_LOG_001_NewGame_WalkToTheLibrary_E_ThenLKey_ShowsTheCard_LCloses_HudButtonReopens()
        {
            using PlayerDriver player = StartNewGame();
            WalkOutToTown(player);

            // 걷기 전에는 비어 있다(L로 열어 빈 안내를 보고 닫는다).
            player.OpenScreenWithKey(TestKey.L, ScreenCatalog.LocationStateLog);
            Assert.That(((LocationStateLogScreen)player.Screens.Find(ScreenCatalog.LocationStateLog)).Model.Rows, Is.Empty);
            Assert.That(player.Node(LocationStateLogScreen.EmptyId).Visible, Is.True);
            player.CloseScreenWithKey(TestKey.L, ScreenCatalog.LocationStateLog);

            // 도서관 지점까지 걸어가 E: 장소 화면이 열리며 방문이 기록된다. 닫는다(뒤에 하루 이벤트가 뜨면 그것도 닫는다).
            WalkToPoint(player, LocationScreen.InteractionKind, "location.library");
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Location);
            player.PressButton(LocationScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.Location);
            player.Frames(2);
            CloseDailyEventIfOpen(player);

            // L: 오늘 본 장소 상태 카드가 보인다. 이동을 막지 않는다.
            player.OpenScreenWithKey(TestKey.L, ScreenCatalog.LocationStateLog);
            var log = (LocationStateLogScreen)player.Screens.Find(ScreenCatalog.LocationStateLog);
            Assert.That(log.Model.Rows, Is.Not.Empty, "도서관에 들른 기록이 카드로 보인다");
            Assert.That(log.Model.Rows.Select(r => r.StateId), Is.EqualTo(player.Game.Session.LocationStates.TodaySummary.Select(s => s.StateId)));
            Assert.That(player.Node(LocationStateLogScreen.EmptyId).Visible, Is.False);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            player.MovePointer(-50, -50);
            player.Frames(3);
            player.Screenshot("location-log-01-key");
            player.CloseScreenWithKey(TestKey.L, ScreenCatalog.LocationStateLog);

            // HUD 버튼을 포인터로 눌러 다시 연다(같은 켬/끔).
            player.PressButton(LocationLogButton);
            player.WaitForScreen(ScreenCatalog.LocationStateLog);
            Assert.That(((LocationStateLogScreen)player.Screens.Find(ScreenCatalog.LocationStateLog)).Model.Rows, Is.Not.Empty);
            player.MovePointer(-50, -50);
            player.Frames(3);
            player.Screenshot("location-log-02-hud-button");
            player.PressButton(LocationLogButton);
            player.WaitForScreenClosed(ScreenCatalog.LocationStateLog);
            AssertClean(player);
        }

        [Test]
        public void WORK_POINT_001_NewGame_WalkToTheClinicHelperPoint_E_MinigameOpens()
        {
            using PlayerDriver player = StartNewGame();
            WalkOutToTown(player);
            WalkToPoint(player, "part-time-work", RepresentativeWork);
            player.Frames(2);
            player.Screenshot("work-point-01-target");
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Minigame);
            Assert.That(player.Game.Session.Student.HasPendingMinigame, Is.True, "E로 알바를 예약했다");
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Minigame, "work-point-02-minigame");

            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Minigame);
            Assert.That(player.Game.Session.Student.HasPendingMinigame, Is.False);
            AssertClean(player);
        }
    }
}
