using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 묶음 7(가구 배치)을 실제 창과 게임 루프로: 새 슬롯(집) → HUD 버튼으로 배치 켬(배치 카메라 전체 보기) → 검색 칸에 글자를 쳐
    /// 팔레트에서 가구를 고름 → 월드를 포인터로 눌러 놓음 → 벽 칸에는 놓이지 않음 → 놓은 가구를 눌러 고르고 이동 → 카메라(+, 방향 키, F, 휠)
    /// → 배치 끔 → 캐릭터가 그 가구에 막힘 → 끄고 다시 시작해도 가구가 그 자리에 있음. 조작은 키·글자·포인터뿐(장치 이벤트 주입,
    /// OS 입력 계층 미확인). 순간이동·내부 명령 호출·상태 강제 설정 없음. 칸을 고를 때만 Core 판정(House.CanPlace)과 화면 좌표 바꾸기를
    /// 읽는다(어디를 누를지 정하는 데만 쓴다). 스크린샷: output/host-screens-evidence/furniture-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class FurniturePlacementHostTests
    {
        private const string Desk = "growth31.assembly.desk";
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-furniture-" + Guid.NewGuid().ToString("N"));
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

        private static FurniturePlacementScreen Placement(PlayerDriver p) => (FurniturePlacementScreen)p.Screens.Find(ScreenCatalog.FurniturePlacement);

        private static HostPlacementView View(PlayerDriver p) => new HostPlacementView(p.Game.World, () => p.Game.Fit.Scale);

        /// <summary>화면에 보이고 UI가 덮지 않은 칸 중 조건에 맞는 것(맵 위 줄부터). 어디를 누를지 정하는 데만 쓴다.</summary>
        private static GridPoint FindCell(PlayerDriver p, Func<GridPoint, bool> want, string what)
        {
            HostPlacementView view = View(p);
            foreach (GridPoint cell in p.Game.World.Room.FloorCells)
            {
                UiRect r = view.CellRect(cell);
                if (r.X < 0 || r.Y < 0 || r.Right > 1920 || r.Bottom > 1080) continue;
                if (p.Game.Ui.HitTest(r.CenterX, r.CenterY) != null) continue;
                if (want(cell)) return cell;
            }
            throw new InvalidOperationException("누를 칸이 없다: " + what);
        }

        private static (float X, float Y) CenterOf(PlayerDriver p, GridPoint cell)
        {
            UiRect r = View(p).CellRect(cell);
            return (r.CenterX, r.CenterY);
        }

        private static bool Near(GridPoint a, GridPoint b, int distance) => Math.Abs(a.X - b.X) <= distance && Math.Abs(a.Y - b.Y) <= distance;

        /// <summary>
        /// 키보드로 놓기: P 키로 가구 배치를 열고(가구 고르기는 검색 칸에 글자 + 칸 누르기), 놓을 칸 아래까지 걸어가 위를 보고 E를 누르면
        /// 바라보는 칸에 놓인다(포인터로 누른 것과 같은 GameSession 명령). 장치 이벤트 주입, OS 입력 계층 미확인.
        /// </summary>
        [Test]
        public void FURNITURE_007_PKeyOpensPlacement_EPlacesOnTheCellTheCharacterFaces()
        {
            using PlayerDriver player = Start();
            var session = player.Game.Session;
            FurnitureDefinition desk = session.Definitions.Get<FurnitureDefinition>(Desk);
            FurnitureRoom room = player.Game.World.Room;
            var house = session.House;
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            FurniturePlacementScreen screen = Placement(player);
            player.PressButton(FurniturePlacementScreen.SearchId);
            player.TypeText(Desk);
            int slot = Enumerable.Range(0, 20).First(i => screen.Palette.At(i)?.StableId == Desk);
            player.PressButton(FurniturePlacementScreen.SlotId(slot));
            Assert.That(screen.Active, Is.SameAs(desk));

            // 놓을 칸: 그 아래(맵에서 한 줄 아래) 칸이 걸을 수 있는 바닥. 거기까지 걸어가 위를 본다(위 키를 한 프레임).
            GridPoint spawn = room.Grid.Spawn;
            GridPoint target = room.FloorCells.First(c => house.CanPlace(desk, c, FurniturePlacementDirection.North) && !Near(c, spawn, 1)
                && !player.World.Map.Collision.IsBlocked(new CellPos(room.ToMap(c).X, room.ToMap(c).Y + 1)));
            CellPos below = new CellPos(room.ToMap(target).X, room.ToMap(target).Y + 1);
            player.WalkTo(below);
            player.HoldKey(TestKey.Up, 1);
            Assert.That(View(player).TryFacingCell(out GridPoint facing) && facing.Equals(target), Is.True, "준비: 놓을 칸을 바라본다");
            player.Frames(2);
            player.Screenshot("furniture-12-keyboard-before");

            player.PressKey(TestKey.E);
            player.Frames(2);
            Assert.That(screen.LastAction, Is.EqualTo("placed"));
            Assert.That(house.Placed.Single().Anchor, Is.EqualTo(target), "바라보는 칸에 놓였다");
            player.Screenshot("furniture-13-keyboard-placed");
        }

        [Test]
        public void FURNITURE_001_NewSlot_PlacementOn_PickFromPalette_PlaceMoveRefuse_Off_BlocksWalking_SurvivesRestart()
        {
            GridPoint movedTo;
            FurnitureDefinition desk;
            string timings;
            using (PlayerDriver player = Start())
            {
                var session = player.Game.Session;
                Assert.That(player.World.Map.Id, Is.EqualTo("House"), "새 슬롯은 집에서 시작한다");
                Assert.That(session.House, Is.Not.Null, "집에 들어오면 방 칸을 넘긴다(EnterHouse)");
                Assert.That(session.House.Placed, Is.Empty);
                desk = session.Definitions.Get<FurnitureDefinition>(Desk);
                FurnitureRoom room = player.Game.World.Room;
                Assert.That(room, Is.Not.Null);

                // 배치 켬: HUD 버튼. 열기 시간을 잰다(누르고 떼는 6프레임, 그리고 열린 뒤 한 프레임).
                var watch = Stopwatch.StartNew();
                player.PressButton("hud.button.placement");
                player.WaitForScreen(ScreenCatalog.FurniturePlacement);
                double openMs = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                player.Frames(1);
                double frameMs = watch.Elapsed.TotalMilliseconds;
                FurniturePlacementScreen screen = Placement(player);
                Assert.That(player.Game.World.PlacementCamera.Active, Is.True, "배치 카메라");
                Assert.That(player.Screens.MovementBlocked, Is.False, "이동을 막지 않는다(Unity 판과 같음)");
                Assert.That(screen.Palette.TotalCount, Is.EqualTo(session.AvailableFurniture().Count));
                Assert.That(((UiButton)player.Node("hud.button.placement")).Text, Is.EqualTo("가구 배치 켜짐"));
                player.Frames(2);
                player.Screenshot("furniture-01-open");

                // 쪽 넘기기 시간: 다음 20번(버튼마다 6프레임).
                watch.Restart();
                for (int i = 0; i < 20; i++) player.PressButton(FurniturePlacementScreen.NextId);
                double pageMs = watch.Elapsed.TotalMilliseconds / 20;
                Assert.That(screen.Palette.Page, Is.EqualTo(20));
                player.Frames(1);
                player.Screenshot("furniture-02-page-21");

                // 검색 칸을 눌러 id를 친다 → 그 가구 칸을 누른다.
                player.PressButton(FurniturePlacementScreen.SearchId);
                Assert.That(player.Game.TextInputActive, Is.True);
                player.TypeText(Desk);
                Assert.That(screen.Palette.Query, Is.EqualTo(Desk));
                int slot = Enumerable.Range(0, 20).First(i => screen.Palette.At(i)?.StableId == Desk);
                player.Frames(1);
                player.Screenshot("furniture-03-search");
                player.PressButton(FurniturePlacementScreen.SlotId(slot));
                Assert.That(screen.Active, Is.SameAs(desk));
                Assert.That(player.Game.TextInputActive, Is.False, "칸을 누르면 글자 입력이 끝난다");

                // 놓을 수 있는 칸에 포인터를 올리면 초록 미리보기, 눌러 놓는다.
                var house = session.House;
                GridPoint spawn = room.Grid.Spawn;
                GridPoint target = FindCell(player, c => house.CanPlace(desk, c, FurniturePlacementDirection.North) && !Near(c, spawn, 2)
                    && house.CanPlace(desk, new GridPoint(c.X, c.Y - 2), FurniturePlacementDirection.North), "책상 놓을 칸");
                (float tx, float ty) = CenterOf(player, target);
                player.MovePointer(tx, ty);
                Assert.That(screen.Hover, Is.EqualTo(target));
                Assert.That(screen.HoverValid, Is.True);
                player.Screenshot("furniture-04-preview-valid");
                player.ClickAt(tx, ty);
                Assert.That(screen.LastAction, Is.EqualTo("placed"));
                PlacedFurniture placed = house.Placed.Single();
                Assert.That(placed.Anchor, Is.EqualTo(target));
                Assert.That(player.Game.World.FurnitureDrawCount, Is.EqualTo(1), "월드가 놓인 가구를 그린다");
                player.MovePointer(tx, ty + 200);
                player.Screenshot("furniture-05-placed");

                // 벽(고정 칸)에는 놓이지 않는다. 미리보기는 빨강.
                GridPoint wall = FindCell(player, c => room.Grid.IsFixed(c), "벽 칸");
                (float wx, float wy) = CenterOf(player, wall);
                player.MovePointer(wx, wy);
                Assert.That(screen.HoverValid, Is.False);
                player.Screenshot("furniture-06-preview-blocked");
                player.ClickAt(wx, wy);
                Assert.That(screen.LastAction, Is.EqualTo("rejected"));
                Assert.That(house.Placed.Count, Is.EqualTo(1), "아무것도 바뀌지 않는다");

                // 놓은 가구를 눌러 고르고, 이동을 누르고, 두 줄 아래 칸을 누른다.
                player.ClickAt(tx, ty);
                Assert.That(screen.SelectedInstanceId, Is.EqualTo(placed.InstanceId));
                player.PressButton(FurniturePlacementScreen.MoveId);
                Assert.That(screen.Carrying, Is.True);
                movedTo = new GridPoint(target.X, target.Y - 2);
                (float mx, float my) = CenterOf(player, movedTo);
                player.ClickAt(mx, my);
                Assert.That(screen.LastAction, Is.EqualTo("moved"));
                Assert.That(house.Placed.Single().Anchor, Is.EqualTo(movedTo));
                player.MovePointer(mx, my + 200);
                player.Screenshot("furniture-07-moved");

                // 배치 카메라: = 를 누르고 있으면 가까이, 방향 키로 옮기고, F로 전체 보기, 휠로 확대.
                PlacementCamera camera = player.Game.World.PlacementCamera;
                float full = camera.HalfHeightCells;
                player.HoldKey(TestKey.Equals, 30);
                Assert.That(camera.HalfHeightCells, Is.LessThan(full));
                float x0 = camera.CenterX;
                player.HoldKey(TestKey.Right, 20);
                player.HoldKey(TestKey.Left, 5);
                Assert.That(camera.CenterX, Is.Not.EqualTo(x0), "방향 키가 카메라를 옮긴다");
                player.Frames(1);
                player.Screenshot("furniture-08-camera-zoomed");
                player.PressKey(TestKey.F);
                Assert.That(camera.HalfHeightCells, Is.EqualTo(full).Within(0.001f), "F는 전체 보기");
                player.MovePointer(960, 560);
                player.Wheel(1);
                Assert.That(camera.HalfHeightCells, Is.EqualTo(Math.Max(PlacementCamera.Settings.MinHalfHeightCells, full - 1f)).Within(0.001f));
                player.PressKey(TestKey.F);

                // 배치 끔.
                player.PressButton("hud.button.placement");
                player.WaitForScreenClosed(ScreenCatalog.FurniturePlacement);
                Assert.That(camera.Active, Is.False);
                player.MovePointer(-50, -50);
                player.Frames(2);
                player.Screenshot("furniture-09-off");

                // 캐릭터가 그 가구에 막힌다: 가구 바로 아래(맵에서) 칸까지 걸어가 위로 밀어 본다.
                CellPos deskCell = room.ToMap(movedTo);
                Assert.That(player.World.Map.Collision.IsBlocked(deskCell), Is.True, "가구가 막는 칸");
                var below = new CellPos(deskCell.X, deskCell.Y + 1);
                player.WalkTo(below);
                player.HoldKey(TestKey.Up, 45);
                Assert.That(player.PlayerCell, Is.EqualTo(below), "가구 칸으로 들어가지 못한다");
                Assert.That(player.CollisionViolations, Is.EqualTo(0));
                player.Screenshot("furniture-10-blocked-by-desk");

                timings = $"배치 켬(누르기 6프레임 포함) {openMs:F1} ms, 열린 뒤 한 프레임 {frameMs:F1} ms, 쪽 넘기기(누르기 6프레임) 평균 {pageMs:F1} ms, 가구 {screen.Palette.TotalCount}개";
                TestContext.Out.WriteLine(timings);
                Assert.That(frameMs, Is.LessThan(100), "열린 뒤 한 프레임");
                Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, screen.TextKeys);
                Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            }

            // 끄고 다시 시작해도 가구가 그 자리에 있고, 캐릭터를 막는다.
            using (PlayerDriver again = Start())
            {
                Assert.That(again.World.Map.Id, Is.EqualTo("House"));
                PlacedFurniture restored = again.Game.Session.House.Placed.Single();
                Assert.That(restored.Definition.StableId, Is.EqualTo(Desk));
                Assert.That(restored.Anchor, Is.EqualTo(movedTo));
                Assert.That(again.Game.World.FurnitureDrawCount, Is.EqualTo(1));
                Assert.That(again.World.Map.Collision.IsBlocked(again.Game.World.Room.ToMap(movedTo)), Is.True);
                again.Frames(2);
                again.Screenshot("furniture-11-after-restart");
            }
            TestContext.Out.WriteLine(timings);
        }
    }
}
