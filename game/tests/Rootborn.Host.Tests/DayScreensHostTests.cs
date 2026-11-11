using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Dialogue;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 묶음 4(하루)를 실제 창과 게임 루프로: 새 슬롯 → 집의 계획판까지 걸어가 E → 일과 카드를 포인터로 고르고 닫기 → 장소 지점까지 걸어가 E
    /// → 활동 하나 → 닫기(그 장소의 하루 이벤트가 뜨면 닫기) → 하루 마감 지점까지 걸어가 E → 결산을 휠로 내려 보고 다음 날 → 2일째.
    /// 조작은 방향 키·E·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인). 순간이동·내부 호출·상태 강제 설정 없음.
    /// 마을의 장소 지점은 아직 맵에 없어서(북쪽 구역), 시험용 상호작용 파일에 집 안 도서관 지점 하나를 더한다(HUD 묶음과 같은 방식).
    /// 스크린샷: output/host-screens-evidence/day-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class DayScreensHostTests
    {
        private const float LibraryX = 624f, LibraryY = 560f;
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-day-" + Guid.NewGuid().ToString("N"));
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
                "--interactions", InteractionsWithLibraryPoint(),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private string InteractionsWithLibraryPoint()
        {
            string text = File.ReadAllText(Path.Combine(_root, InteractionData.DefaultRelativePath));
            const string houseThings = "\"things\": [";
            int house = text.IndexOf("\"House\"", StringComparison.Ordinal);
            int things = text.IndexOf(houseThings, house, StringComparison.Ordinal);
            string point = "\n        { \"kind\": \"location-activity\", \"id\": \"location.library\", \"at\": [ "
                + LibraryX.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + LibraryY.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ], \"priority\": 45 },";
            string path = Path.Combine(_saves, "interactions-day.json");
            File.WriteAllText(path, text.Insert(things + houseThings.Length, point));
            return path;
        }

        private static InteractionThing Thing(string kind, string root) =>
            InteractionData.Load(Path.Combine(root, InteractionData.DefaultRelativePath)).Map("House").Things.First(t => t.Kind == kind);

        private static T Screen<T>(PlayerDriver player, string id) where T : GameScreen => (T)player.Screens.Find(id);

        /// <summary>그 자리의 맨 위 요소가 expectId인지 본 뒤 포인터로 누른다(카드 목록의 칸은 요소가 아니라 자리로 누른다).</summary>
        private static void ClickInside(PlayerDriver player, UiRect rect, string expectId)
        {
            Assert.That(player.Game.Ui.HitTest(rect.CenterX, rect.CenterY)?.Id, Is.EqualTo(expectId));
            player.ClickAt(rect.CenterX, rect.CenterY);
        }

        [Test]
        public void DAY_001_PlanBoard_Location_DayEnd_NextDay_ByWalkingAndPressing()
        {
            using PlayerDriver player = Start();
            Assert.That(player.World.Map.Id, Is.EqualTo("House"));
            Assert.That(player.Game.Session.House, Is.Not.Null, "집 맵에 들어오면 방 칸으로 EnterHouse를 불렀다");
            Assert.That(player.Game.World.GrowthStage, Is.EqualTo(player.Game.Session.GrowthStageId()));
            Assert.That(player.Game.World.CurrentHour, Is.EqualTo(player.Game.Session.StudentHour));

            // 1. 계획판까지 걸어가 E
            InteractionThing board = Thing(DailyPlanScreen.InteractionKind, _root);
            player.WalkNear(board.X, board.Y, 24f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo(DailyPlanScreen.InteractionKind));
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.DailyPlan);
            Assert.That(player.Screens.MovementBlocked, Is.False, "일과는 이동을 막지 않는다");
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.DailyPlan, "day-01-plan-open");

            var plan = Screen<DailyPlanScreen>(player, ScreenCatalog.DailyPlan);
            string route = plan.Model.Routes[0].RouteId;
            ClickInside(player, plan.RouteCardRect(0), DailyPlanScreen.RouteGridId);
            Assert.That(player.Game.Session.DailyPlan.Slots[0].RouteId, Is.EqualTo(route), "포인터로 고른 일과 길이 첫 칸에 들어갔다");
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.DailyPlan, "day-02-plan-chosen");
            player.PressButton(DailyPlanScreen.CloseTopId);
            player.WaitForScreenClosed(ScreenCatalog.DailyPlan);

            // 2. 장소 지점까지 걸어가 E → 활동 하나
            player.WalkNear(LibraryX, LibraryY, 40f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo(LocationScreen.InteractionKind));
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Location);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            Assert.That(new LocationVisitProgress(player.Game.Session.Student).HasVisited("location.library"), Is.True);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Location, "day-03-location-open");

            var location = Screen<LocationScreen>(player, ScreenCatalog.Location);
            int row = location.Model.Rows.ToList().FindIndex(r => r.Enabled);
            Assert.That(row, Is.GreaterThanOrEqualTo(0), "도서관에서 할 수 있는 활동이 있다");
            int logs = player.Game.Session.Student.GetActivityLogIds().Length;
            ClickInside(player, location.RowRect(row), LocationScreen.RowsId);
            Assert.That(player.Game.Session.Student.GetActivityLogIds().Length, Is.EqualTo(logs + 1), "활동을 했다");
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Location, "day-04-location-done");
            player.PressButton(LocationScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.Location);
            if (player.Screens.IsOpen(ScreenCatalog.DailyEvent))
            {
                player.PressButton(DailyEventScreen.CloseId); // 그 장소의 하루 이벤트(장소 화면 뒤에 뜬다)
                player.WaitForScreenClosed(ScreenCatalog.DailyEvent);
            }

            // 3. 하루 마감 지점까지 걸어가 E → 결산 → 다음 날
            InteractionThing end = Thing(DayResultScreen.InteractionKind, _root);
            player.WalkNear(end.X, end.Y, 24f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo(DayResultScreen.InteractionKind));
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.DayResult);
            Assert.That(player.Game.Session.Student.DayState, Is.EqualTo(StudentDayState.ResultReady));
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.DayResult, "day-05-result-top");

            var result = Screen<DayResultScreen>(player, ScreenCatalog.DayResult);
            Assert.That(result.Model.Summary.CompletedActivityIds, Does.Contain(location.Model.Rows[row].Id));
            if (result.Body.MaxScroll > 0)
            {
                player.MovePointerTo(DayResultScreen.BodyId);
                player.Wheel(-6f);
                Assert.That(result.Body.ScrollOffset, Is.GreaterThan(0f), "휠로 결산을 내려 본다");
                player.MovePointer(-50, -50);
                player.ScreenshotScreen(ScreenCatalog.DayResult, "day-06-result-scrolled");
            }

            player.PressButton(DayResultScreen.NextDayId);
            player.WaitForScreenClosed(ScreenCatalog.DayResult);
            Assert.That(player.Game.Session.StudentDay, Is.EqualTo(2), "다음 날");
            Assert.That(player.Game.Session.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(player.Screens.MovementBlocked, Is.False);
            Assert.That(player.Game.World.GrowthStage, Is.EqualTo(player.Game.Session.GrowthStageId()));
            player.WalkAway(1);
            player.Screenshot("day-07-day-two");

            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }
    }
}
