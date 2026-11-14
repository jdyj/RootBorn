using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Hud;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// Unity 판에 있던 조작감·연출을 실제 창과 게임 루프로(장치 이벤트 주입, OS 입력 계층 미확인):
    /// 상호작용 실패 안내(E와 마우스), 마우스 왼클릭(누른 쪽 바라보기·행동 칸·E와 같은 상호작용, UI 위 클릭은 월드로 가지 않음),
    /// 카메라 부드러운 추적과 맵 가장자리, 자동문 열림 덮개와 이동 구역 페이드. 조작은 방향 키·E·F1·포인터뿐이다.
    /// 준비 단계의 상태 강제 설정은 실패 안내 테스트의 "오늘 시간을 다 씀" 하나뿐이다(그 테스트에 적었다).
    /// 스크린샷: output/runtime-feel-evidence/*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class WorldFeelHostTests
    {
        private const float Frame = 1f / 60f;
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-feel-host-" + Guid.NewGuid().ToString("N"));
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
                "--evidence", Path.Combine(_root, "output", "runtime-feel-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            Assert.That(player.World.Map.Id, Is.EqualTo("House"), "새 게임은 집에서 시작한다");
            return player;
        }

        private static WorldFeel Feel(PlayerDriver p) => p.Game.World.Feel;

        private static void CloseDailyEventIfOpen(PlayerDriver player)
        {
            if (!player.Screens.IsOpen(ScreenCatalog.DailyEvent)) return;
            player.PressButton(DailyEventScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.DailyEvent);
        }

        private static void WalkOutToTown(PlayerDriver player)
        {
            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.Frames(2);
            CloseDailyEventIfOpen(player);
        }

        /// <summary>맵 픽셀 → 화면 논리 좌표(1920x1080 기준, 지난 프레임의 카메라).</summary>
        private static (float X, float Y) OnScreen(PlayerDriver p, float x, float y)
        {
            WorldScreen w = p.Game.World;
            float k = w.Scale / p.Game.Fit.Scale;
            return ((x - w.CameraOffset.X) * k, (y - w.CameraOffset.Y) * k);
        }

        private static void ClickWorld(PlayerDriver p, float mapX, float mapY)
        {
            (float x, float y) = OnScreen(p, mapX, mapY);
            p.ClickAt(x, y);
        }

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

        // ---------- 상호작용 실패 안내 ----------

        [Test]
        public void FEEL_REFUSAL_001_WorkPointWhileExhausted_EAndMouseClick_ShowTheReason_ThenItGoes()
        {
            using PlayerDriver player = StartNewGame();
            WalkOutToTown(player);
            // 준비(GameSession 명령, 플레이어 경로 아님): 지치게 한다(이웃 돕기 되풀이 — 체력 0). 흐르는 시간에서는 시간 포인트로 막지 않고
            // 지침이 활동을 막는다(설계 2026-11-12-flowing-time-design.md 3.4). 실제로 지치려면 하루를 다 보내야 해서 여기만 줄였다.
            var student = player.Game.Session.Student;
            for (int n = 0; !player.Game.Session.IsExhausted && n < 40; n++) player.Game.Session.PerformOutsideSchoolActivity("outside.help-neighbor", "feel-exhaust:" + n);
            Assert.That(player.Game.Session.IsExhausted, Is.True);

            InteractionThing work = WalkToPoint(player, "part-time-work", "work.clinic-helper");
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.InteractionRefusal);
            var notice = (InteractionRefusalNoticeScreen)player.Screens.Find(ScreenCatalog.InteractionRefusal);
            Assert.That(notice.MessageKey, Is.EqualTo("ui.prompt.exhausted"));
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Minigame), Is.False, "거절되면 미니게임을 열지 않는다");
            Assert.That(student.HasPendingMinigame, Is.False);
            Assert.That(player.Screens.MovementBlocked, Is.False, "알림이 떠 있어도 걷는다");
            player.MovePointer(-50, -50);
            player.Frames(3);
            string shot = player.ScreenshotScreen(ScreenCatalog.InteractionRefusal, "refusal-01-exhausted");
            Assert.That(File.Exists(shot));

            int frames = player.WaitForScreenClosed(ScreenCatalog.InteractionRefusal, 240);
            Assert.That(frames * Frame, Is.EqualTo(InteractionRefusalNoticeScreen.ShowSeconds).Within(0.2f), "2.5초 뒤 닫힌다");

            // 마우스로 같은 지점을 눌러도 E와 같다.
            ClickWorld(player, work.X, work.Y);
            player.WaitForScreen(ScreenCatalog.InteractionRefusal);
            Assert.That(Feel(player).WorldClicks, Is.EqualTo(1));
            AssertClean(player);
        }

        // ---------- 마우스 왼클릭 ----------

        [Test]
        public void FEEL_CLICK_001_LeftClick_FacesThatWay_PlaysTheAction_AndInteractsLikeE_ButNotThroughUi()
        {
            using PlayerDriver player = StartNewGame();
            WorldFeel feel = Feel(player);
            int cell = player.World.Map.Grid.CellSize;
            Walker w = player.World.Walker;

            // 오른쪽을 누른다: 오른쪽을 보고 행동 칸(3·4열)을 0.3초 보인다.
            ClickWorld(player, w.X + cell * 3, w.Y);
            Assert.That(feel.WorldClicks, Is.EqualTo(1));
            Assert.That(player.World.Walker.Facing, Is.EqualTo(Facing.Right));
            Assert.That(feel.Action.Active, Is.True);
            Assert.That(feel.Action.ColumnOr(1), Is.EqualTo(3).Or.EqualTo(4));
            player.Screenshot("click-01-action-right");
            player.Frames(20);
            Assert.That(feel.Action.Active, Is.False, "0.3초 뒤 끝난다");
            Assert.That(player.Game.World.PlayerAnimator.Column, Is.EqualTo(1), "서 있는 칸으로 돌아온다");

            // 위쪽을 누른다.
            ClickWorld(player, w.X, w.Y - cell * 3);
            Assert.That(player.World.Walker.Facing, Is.EqualTo(Facing.Up));

            // 하루 계획판 앞에서 판을 누르면 E처럼 계획 화면이 열린다.
            InteractionThing board = WalkToPoint(player, DailyPlanScreen.InteractionKind, "house.daily-plan-board");
            ClickWorld(player, board.X, board.Y);
            player.WaitForScreen(ScreenCatalog.DailyPlan);
            int clicks = feel.WorldClicks;
            int played = feel.Action.Played;

            // 열린 패널의 버튼(UI)을 누르면 월드로 가지 않는다.
            player.PressButton(DailyPlanScreen.CloseBottomId);
            player.WaitForScreenClosed(ScreenCatalog.DailyPlan);
            Assert.That(feel.WorldClicks, Is.EqualTo(clicks), "UI 위의 클릭은 월드 클릭이 아니다");
            Assert.That(feel.Action.Played, Is.EqualTo(played));

            // 모달(단축키 안내)이 떠 있으면 월드를 눌러도 반응하지 않는다(막는 판이 받는다).
            player.OpenScreenWithKey(TestKey.F1, ScreenCatalog.KeyGuide);
            ClickWorld(player, w.X - cell * 3, w.Y);
            Assert.That(feel.WorldClicks, Is.EqualTo(clicks), "모달 아래의 월드는 포인터를 받지 않는다");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.DailyPlan), Is.False);
            player.CloseScreenWithKey(TestKey.F1, ScreenCatalog.KeyGuide);
            AssertClean(player);
        }

        // ---------- 카메라 ----------

        [Test]
        public void FEEL_CAMERA_001_CameraFollowsSmoothly_ThenStopsAtTheMapEdge()
        {
            using PlayerDriver player = StartNewGame();
            WalkOutToTown(player);
            WorldScreen world = player.Game.World;
            // 지금 맵에는 cameraBounds가 없다(맵 크기가 경계). 바로 맞춘 자리 = CameraFrame.View(캐릭터).
            AreaRect Desired() => CameraFrame.View(player.World.Walker.X, player.World.Walker.Y,
                CameraFrame.Bounds(player.World.Map.Grid.PixelWidth, player.World.Map.Grid.PixelHeight, null), world.CameraView.W, world.CameraView.H);
            Assert.That(world.CameraView.X, Is.EqualTo(Desired().X).Within(0.5f), "들어선 순간 바로 맞춘다");

            // 오른쪽으로 걸으면 카메라가 조금 늦게 따라온다(Unity SmoothDamp 0.15초).
            float lagMax = 0f;
            player.KeyDown(TestKey.Right);
            for (int i = 0; i < 30; i++)
            {
                player.Frames(1);
                lagMax = Math.Max(lagMax, Math.Abs(Desired().X - world.CameraView.X));
            }
            player.KeyUp(TestKey.Right);
            Assert.That(lagMax, Is.GreaterThan(2f), "걷는 동안 카메라가 바로 붙지 않고 뒤따른다");
            player.Frames(60);
            Assert.That(world.CameraView.X, Is.EqualTo(Desired().X).Within(0.5f), "멈추면 따라잡는다");

            // 맵 왼쪽 끝까지 걸어간다: 카메라는 맵 안에 머물고 가장자리에서 멈춘다.
            CollisionGrid collision = player.World.Map.Collision;
            List<CellPos> path = GridPath.Find(collision, player.PlayerCell, c => c.X <= 1);
            Assert.That(path, Is.Not.Null.And.Not.Empty, "맵 왼쪽 끝까지 가는 길");
            player.WalkTo(path[path.Count - 1]);
            player.Frames(60);
            AreaRect view = world.CameraView;
            Assert.That(view.X, Is.EqualTo(0f).Within(0.5f), "왼쪽 가장자리에서 멈춘다");
            Assert.That(view.Y, Is.GreaterThanOrEqualTo(-0.5f));
            Assert.That(view.Bottom, Is.LessThanOrEqualTo(player.World.Map.Grid.PixelHeight + 0.5f));
            player.Screenshot("camera-01-left-edge");
            AssertClean(player);
        }

        // ---------- 문 열림·구역 페이드 ----------

        /// <summary>문 사각형에 맞닿은(문 칸이 아닌) 칸까지 걸어간 뒤 문 쪽 키를 누르고 프레임마다 연출을 본다. 맵이 바뀌면 뗀다.</summary>
        private static (int OpeningFrames, bool SawHalf) WalkIntoDoorWatching(PlayerDriver player, string target, string shot)
        {
            WorldSession session = player.World;
            DoorArea door = session.Map.Doors.First(d => d.Destination.HasValue && DoorRoutes.Normalize(d.Target) == DoorRoutes.Normalize(target));
            int size = session.Map.Grid.CellSize;
            // 출발 칸: 그 칸에 선 몸은 문 상자에 닿지 않고(Core DoorWatch와 같은 접촉 여유), 이웃 칸에 선 몸은 닿는 칸.
            // 바깥 벽 칸의 문(막힌 칸)은 문 칸 바로 앞에 서기만 해도 닿으므로, 그 칸에서 출발하면 연출이 걷는 동안 시작돼 처음부터 셀 수 없다.
            float half = session.Walker.Box.W / 2f, skin = DoorWatch.UnityContactSkinCells * size;
            bool Touches(CellPos c)
            {
                var center = CellPos.Center(c, size);
                return door.Area.Overlaps(new AreaRect(center.X - half - skin, center.Y - half - skin, 2 * (half + skin), 2 * (half + skin)));
            }
            CellPos[] Around(CellPos c) => new[] { new CellPos(c.X + 1, c.Y), new CellPos(c.X - 1, c.Y), new CellPos(c.X, c.Y + 1), new CellPos(c.X, c.Y - 1) };
            bool Next(CellPos c) => !Touches(c) && Around(c).Any(Touches);
            List<CellPos> path = GridPath.Find(session.Map.Collision, player.PlayerCell, Next);
            Assert.That(path, Is.Not.Null, target + " 문 앞까지 갈 길");
            if (path.Count > 0) player.WalkTo(path[path.Count - 1]);
            Assert.That(session.OpeningDoor, Is.Null, target + " 문 앞까지 걷는 동안 문이 열리기 시작하지 않았다(연출을 처음부터 센다)");
            CellPos at = player.PlayerCell;
            CellPos into = Around(at).First(Touches);
            TestKey key = into.X > at.X ? TestKey.Right : into.X < at.X ? TestKey.Left : into.Y > at.Y ? TestKey.Down : TestKey.Up;

            string from = session.Map.Id;
            WorldTransition t = Feel(player).Transition;
            int opening = 0;
            bool sawHalf = false;
            float last = -1f;
            player.KeyDown(key);
            for (int i = 0; i < 240 && player.World.Map.Id == from; i++)
            {
                player.Frames(1);
                if (player.World.Map.Id != from || t.Kind == WorldTransitionKind.None) continue;
                opening++;
                Assert.That(t.Progress, Is.GreaterThanOrEqualTo(last), "진행도는 줄지 않는다");
                last = t.Progress;
                Assert.That(t.BlocksInput, Is.False, "Unity 판처럼 연출 동안 입력을 막지 않는다");
                Assert.That(player.Screens.MovementBlocked, Is.False);
                if (!sawHalf && t.Progress >= 0.45f)
                {
                    sawHalf = true;
                    player.KeyUp(key); // 멈춰도 열리던 문은 끝까지 열린다(Core DoorWatch)
                    player.Screenshot(shot);
                }
            }
            player.KeyUp(key);
            for (int i = 0; i < 60 && player.World.Map.Id == from; i++) player.Frames(1);
            Assert.That(player.World.Map.Id, Is.Not.EqualTo(from), target + " 문을 지났다");
            player.Frames(2);
            return (opening, sawHalf);
        }

        [Test]
        public void FEEL_DOOR_001_AutoDoor_ShowsTheWideningCover_ForItsOpenTime_WithoutBlockingInput()
        {
            using PlayerDriver player = StartNewGame();
            WorldTransition t = Feel(player).Transition;
            (int frames, bool half) = WalkIntoDoorWatching(player, "town", "door-01-opening-half");
            Assert.That(half, Is.True, "열리는 중간을 보았다");
            Assert.That(frames * Frame, Is.EqualTo(DoorWatch.UnityOpeningSeconds).Within(3 * Frame), "열리는 시간은 Unity 판 0.35초 그대로");
            Assert.That(player.World.Map.Id, Is.EqualTo("Town"));
            Assert.That(t.Kind, Is.EqualTo(WorldTransitionKind.None), "지난 뒤에는 연출이 없다");
            Assert.That(t.Completed, Is.EqualTo(1));
            CloseDailyEventIfOpen(player);
            AssertClean(player);
        }

        [Test]
        public void FEEL_AREA_001_TownToBeach_FadesTheScreen_ThenArrives()
        {
            using PlayerDriver player = StartNewGame();
            WalkOutToTown(player);
            WorldTransition t = Feel(player).Transition;
            int before = t.Completed;
            (int frames, bool half) = WalkIntoDoorWatching(player, "beach", "area-01-fade-half");
            Assert.That(half, Is.True);
            Assert.That(frames * Frame, Is.EqualTo(0.25f).Within(3 * Frame), "maps.json openSeconds 0.25");
            Assert.That(player.World.Map.Id, Is.EqualTo("Beach"));
            Assert.That(t.Completed, Is.EqualTo(before + 1));
            AssertClean(player);
        }
    }
}
