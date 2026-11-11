using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// 가구 배치 화면을 창 없이: 실제 data/와 GameSession, 실제 등록 표. 방은 시험용 맵(벽으로 둘러싼 10x8칸, 아래쪽 가운데 도착 지점)이고,
    /// 월드 좌표 바꾸기는 칸 48픽셀짜리 시험용 보기다(Host가 카메라로 하는 일). 실제 집 맵·카메라·그림은 Host 시나리오가 본다.
    /// </summary>
    public sealed class FurniturePlacementScreenTests
    {
        private const string FreeDesk = "growth31.assembly.desk";

        /// <summary>화면 (OriginX, OriginY)에서 칸 하나가 Cell 픽셀인 보기.</summary>
        private sealed class TestView : IPlacementView
        {
            public const float OriginX = 500, OriginY = 200, Cell = 48;
            public TestView(FurnitureRoom room) { Room = room; }
            public FurnitureRoom Room { get; }

            public bool TryCellAt(float x, float y, out GridPoint cell)
            {
                int mx = (int)Math.Floor((x - OriginX) / Cell), my = (int)Math.Floor((y - OriginY) / Cell);
                cell = Room.ToPlacement(new CellPos(mx, my));
                return Room.Map.Grid.Contains(mx, my);
            }

            public UiRect CellRect(GridPoint cell)
            {
                CellPos c = Room.ToMap(cell);
                return new UiRect(OriginX + c.X * Cell, OriginY + c.Y * Cell, Cell, Cell);
            }

            /// <summary>그 칸 가운데의 화면 좌표.</summary>
            public (float X, float Y) Center(GridPoint cell)
            {
                UiRect r = CellRect(cell);
                return (r.CenterX, r.CenterY);
            }
        }

        /// <summary>10x8칸 방: 가장자리가 벽, 아래쪽 가운데(5,6)에 도착 지점.</summary>
        internal static WorldMap TestRoomMap()
        {
            var grid = new MapGrid(10, 8, 32);
            var blocked = new bool[grid.Count];
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    blocked[grid.Index(x, y)] = x == 0 || y == 0 || x == grid.Columns - 1 || y == grid.Rows - 1;
            var spawns = new Dictionary<string, (float X, float Y)> { ["entry"] = (5 * 32 + 16, 6 * 32 + 16) };
            return new WorldMap("TestRoom", new CollisionGrid(grid, blocked), new List<DoorArea>(), spawns);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly TestSession T = new TestSession();
            public readonly FurnitureRoom Room = new FurnitureRoom(TestRoomMap());
            public readonly TestView View;
            public readonly FurniturePlacementScreen Screen;

            public Fixture()
            {
                View = new TestView(Room);
                T.Session.EnterHouse(Room.Grid, "house");
                T.Stack.EnterMap("House");
                T.Stack.OpenHud();
                T.Click("hud.button.placement");
                Screen = T.Screen<FurniturePlacementScreen>(ScreenCatalog.FurniturePlacement);
                Screen.Attach(new FurnitureArt(TestData.DataDirectory), View);
                T.Emit();
            }

            public FurnitureDefinition Desk => T.Session.Definitions.Get<FurnitureDefinition>(FreeDesk);

            /// <summary>Host가 하는 일: 포인터가 UI 위인지 UiTree로 보고, 아니면 월드 포인터로 넘긴다.</summary>
            public void Pointer(float x, float y, bool down)
            {
                T.Stack.Pointer(x, y, down, 0);
                Screen.WorldPointer(x, y, down, T.Stack.Tree.HitTest(x, y) != null);
            }

            public void ClickCell(GridPoint cell)
            {
                (float x, float y) = View.Center(cell);
                Pointer(x, y, false);
                Pointer(x, y, true);
                Pointer(x, y, false);
            }

            public void HoverCell(GridPoint cell)
            {
                (float x, float y) = View.Center(cell);
                Pointer(x, y, false);
            }

            /// <summary>검색 칸을 눌러 글자를 친다(UiTree 글자 입력).</summary>
            public void Search(string text)
            {
                T.Click(FurniturePlacementScreen.SearchId);
                var field = (UiTextField)T.Node(FurniturePlacementScreen.SearchId);
                while (field.Text.Length > 0) T.Stack.TextBackspace();
                T.Stack.TextInput(text);
                T.Emit();
            }

            /// <summary>팔레트에서 그 가구를 찾아 그 칸을 누른다(검색 → 칸).</summary>
            public void PickFromPalette(string furnitureId)
            {
                Search(furnitureId);
                for (int i = 0; i < 20; i++)
                    if (Screen.Palette.At(i)?.StableId == furnitureId) { T.Click(FurniturePlacementScreen.SlotId(i)); return; }
                Assert.Fail("팔레트에 없다: " + furnitureId);
            }

            public void Dispose() => T.Dispose();
        }

        [Test]
        public void Palette_ListsEveryAvailablePiece_TwentyAPage_InTheGameOrder()
        {
            using var f = new Fixture();
            IReadOnlyList<FurnitureDefinition> available = f.T.Session.AvailableFurniture();
            Assert.That(available.Count, Is.GreaterThan(1000), "실제 data/의 가구");
            Assert.That(f.Screen.Palette.FilteredCount, Is.EqualTo(available.Count));
            Assert.That(f.Screen.Palette.PageCount, Is.EqualTo((available.Count + 19) / 20));
            for (int i = 0; i < 20; i++)
            {
                Assert.That(f.Screen.Palette.At(i), Is.SameAs(available[i]));
                Assert.That(f.T.Node(FurniturePlacementScreen.SlotId(i)).Visible, Is.True);
                Assert.That(((FittedImage)f.T.Node(FurniturePlacementScreen.SlotId(i) + ".icon")).Image, Is.Not.Null.And.Not.Empty, "가구 그림");
            }
            var page = (UiTextBlock)f.T.Node(FurniturePlacementScreen.PageTextId);
            Assert.That(page.Text, Is.EqualTo($"1 / {f.Screen.Palette.PageCount} 페이지\n가구 {available.Count}개"));
            Assert.That(f.T.Node(FurniturePlacementScreen.PreviousId).IsActive, Is.False, "첫 쪽에서 이전은 누를 수 없다");

            f.T.Click(FurniturePlacementScreen.NextId);
            Assert.That(f.Screen.Palette.Page, Is.EqualTo(1));
            Assert.That(f.Screen.Palette.At(0), Is.SameAs(available[20]));
            Assert.That(page.Text, Does.StartWith("2 / "));
            f.T.Click(FurniturePlacementScreen.PreviousId);
            Assert.That(f.Screen.Palette.Page, Is.EqualTo(0));
        }

        [Test]
        public void Search_FiltersByIdOrName_AndShowsWhenNothingMatches()
        {
            using var f = new Fixture();
            f.Search("growth31.assembly");
            int expected = f.T.Session.AvailableFurniture().Count(d => d.StableId.IndexOf("growth31.assembly", StringComparison.OrdinalIgnoreCase) >= 0
                || d.DisplayName.IndexOf("growth31.assembly", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(f.Screen.Palette.FilteredCount, Is.EqualTo(expected));
            Assert.That(f.Screen.Palette.Page, Is.EqualTo(0));

            FurnitureDefinition desk = f.Desk;
            f.Search(desk.DisplayName);
            Assert.That(Enumerable.Range(0, 20).Select(i => f.Screen.Palette.At(i)), Does.Contain(desk), "화면 이름으로도 찾는다");

            f.Search("이런 가구는 없다 zzz");
            Assert.That(f.Screen.Palette.IsEmpty, Is.True);
            Assert.That(((UiTextBlock)f.T.Node(FurniturePlacementScreen.PageTextId)).Text, Is.EqualTo("검색 결과가 없습니다"));
            Assert.That(f.T.Node(FurniturePlacementScreen.SlotId(0)).Visible, Is.False);
            Assert.That(f.T.Node(FurniturePlacementScreen.NextId).IsActive, Is.False);
            Assert.That(f.T.Stack.MovementBlocked, Is.True, "글자를 치는 동안은 걷지 않는다");
        }

        [Test]
        public void PickingAPiece_ShowsItsNameSizeAndDirection_RotateTurnsIt()
        {
            using var f = new Fixture();
            Assert.That(((UiLabel)f.T.Node(FurniturePlacementScreen.SelectedNameId)).Text, Is.EqualTo("가구를 선택하세요"));
            f.PickFromPalette(FreeDesk);
            FurnitureDefinition desk = f.Desk;
            Assert.That(f.Screen.Active, Is.SameAs(desk));
            Assert.That(((UiLabel)f.T.Node(FurniturePlacementScreen.SelectedNameId)).Text, Is.EqualTo(desk.DisplayName));
            int w = desk.FootprintCells.Max(c => c.X) - desk.FootprintCells.Min(c => c.X) + 1;
            int h = desk.FootprintCells.Max(c => c.Y) - desk.FootprintCells.Min(c => c.Y) + 1;
            Assert.That(((UiLabel)f.T.Node(FurniturePlacementScreen.SelectedSizeId)).Text, Is.EqualTo(w + "x" + h));
            Assert.That(((UiLabel)f.T.Node(FurniturePlacementScreen.SelectedDirectionId)).Text, Is.EqualTo("방향: 자동"));
            Assert.That(((FittedImage)f.T.Node(FurniturePlacementScreen.SelectedIconId)).Image, Does.Contain(desk.TileParts[0].Name));

            f.T.Click(FurniturePlacementScreen.RotateId);
            Assert.That(f.Screen.Direction, Is.EqualTo(FurniturePlacementDirection.East));
            Assert.That(((UiLabel)f.T.Node(FurniturePlacementScreen.SelectedDirectionId)).Text, Is.EqualTo("방향: 동쪽"));
            for (int i = 0; i < 3; i++) f.T.Click(FurniturePlacementScreen.RotateId);
            Assert.That(f.Screen.Direction, Is.EqualTo(FurniturePlacementDirection.North));
        }

        [Test]
        public void ClickingAFreeCell_PlacesTheChosenPiece_AsCoreAllows_AndSaves()
        {
            using var f = new Fixture();
            f.PickFromPalette(FreeDesk);
            var cell = new GridPoint(3, 3);
            f.HoverCell(cell);
            bool core = f.T.Session.House.CanPlace(f.Desk, cell, FurniturePlacementDirection.North);
            Assert.That(core, Is.True, "시험 칸");
            Assert.That(f.Screen.HoverValid, Is.EqualTo(core), "미리보기 색은 Core 판단");

            f.ClickCell(cell);
            Assert.That(f.Screen.LastAction, Is.EqualTo("placed"));
            PlacedFurniture placed = f.T.Session.House.Placed.Single();
            Assert.That(placed.Definition, Is.SameAs(f.Desk));
            Assert.That(placed.Anchor, Is.EqualTo(cell));
            Assert.That(f.Screen.SelectedInstanceId, Is.EqualTo(placed.InstanceId), "놓은 가구를 고른다(회전·삭제·이동 대상)");
            Assert.That(f.Screen.OverlayVisible, Is.False, "놓은 뒤 칸 색이 새 가구를 덮지 않는다");

        }

        [Test]
        public void ClickingAWallOrTheEntry_PlacesNothing_AndThePreviewIsRed()
        {
            using var f = new Fixture();
            f.PickFromPalette(FreeDesk);
            foreach (GridPoint blocked in new[] { f.Room.ToPlacement(new CellPos(0, 0)), f.Room.Grid.Spawn })
            {
                f.HoverCell(blocked);
                Assert.That(f.Screen.HoverValid, Is.False);
                f.ClickCell(blocked);
                Assert.That(f.Screen.LastAction, Is.EqualTo("rejected"));
                Assert.That(f.T.Session.House.Placed, Is.Empty, "아무것도 바뀌지 않는다");
                Assert.That(f.Screen.OverlayVisible, Is.True, "놓을 수 있는 칸(초록)과 없는 칸(빨강)을 다시 보여 준다");
            }

            var list = new DrawList();
            f.Screen.EmitWorldOverlay(list);
            Assert.That(f.T.Emit().Commands.Any(c => c.Kind == DrawKind.Fill && c.Color.Equals(FurniturePlacementScreen.Metrics.InvalidCell)), Is.False,
                "미리보기는 화면 층이 아니라 월드 층에서 그린다(HUD를 덮지 않게)");
            UiRect wall = f.View.CellRect(f.Room.ToPlacement(new CellPos(0, 0)));
            Assert.That(list.Commands.Any(c => c.Kind == DrawKind.Fill && c.Rect == wall && c.Color.Equals(FurniturePlacementScreen.Metrics.InvalidCell)), Is.True);
            Assert.That(f.Screen.ValidCellCount, Is.GreaterThan(0));
            Assert.That(f.Screen.ValidCellCount, Is.LessThan(f.Room.FloorCells.Count));
        }

        [Test]
        public void ClickOnPlacedPiece_SelectsIt_MoveThenClickMovesIt_DeleteRemovesIt()
        {
            using var f = new Fixture();
            f.PickFromPalette(FreeDesk);
            f.ClickCell(new GridPoint(3, 3));
            string id = f.Screen.SelectedInstanceId;
            f.HoverCell(new GridPoint(6, 4)); // 놓은 칸을 떠난다
            f.ClickCell(new GridPoint(3, 3));
            Assert.That(f.Screen.LastAction, Is.EqualTo("selected"));
            Assert.That(f.Screen.SelectedInstanceId, Is.EqualTo(id));

            f.T.Click(FurniturePlacementScreen.MoveId);
            Assert.That(f.Screen.Carrying, Is.True);
            var target = new GridPoint(6, 4);
            f.ClickCell(target);
            Assert.That(f.Screen.LastAction, Is.EqualTo("moved"));
            Assert.That(f.Screen.Carrying, Is.False);
            Assert.That(f.T.Session.House.Placed.Single().Anchor, Is.EqualTo(target));
            Assert.That(f.T.Session.House.Placed.Single().InstanceId, Is.EqualTo(id));

            f.T.Click(FurniturePlacementScreen.DeleteId);
            Assert.That(f.T.Session.House.Placed, Is.Empty);
            Assert.That(f.Screen.SelectedInstanceId, Is.Null);
        }

        [Test]
        public void PointerOverThePanels_DoesNotReachTheWorld()
        {
            using var f = new Fixture();
            f.PickFromPalette(FreeDesk);
            UiRect palette = f.T.Node(FurniturePlacementScreen.PaletteId).Rect;
            f.Pointer(palette.CenterX, palette.Bottom - 4, false);
            f.Pointer(palette.CenterX, palette.Bottom - 4, true);
            f.Pointer(palette.CenterX, palette.Bottom - 4, false);
            Assert.That(f.Screen.Hover, Is.Null);
            Assert.That(f.T.Session.House.Placed, Is.Empty);
        }

        [Test]
        public void OpeningPressDoesNotPlace_AndClosingDropsTheSelection()
        {
            using var f = new Fixture();
            f.PickFromPalette(FreeDesk);
            f.ClickCell(new GridPoint(3, 3));
            Assert.That(f.Screen.SelectedInstanceId, Is.Not.Null);
            f.T.Click("hud.button.placement"); // 끔
            Assert.That(f.T.Stack.IsOpen(ScreenCatalog.FurniturePlacement), Is.False);
            Assert.That(f.Screen.SelectedInstanceId, Is.Null);

            // 다시 켠 그 누름(포인터가 버튼 위에서 눌린 채)이 월드 클릭이 되지 않는다.
            f.T.Click("hud.button.placement");
            Assert.That(f.T.Stack.IsOpen(ScreenCatalog.FurniturePlacement), Is.True);
            Assert.That(f.T.Stack.MovementBlocked, Is.False, "배치 화면은 이동을 막지 않는다(Unity 판과 같음)");
            Assert.That(f.T.Session.House.Placed.Count, Is.EqualTo(1));
        }

        [Test]
        public void Ghost_UsesTheAtlasSizeAndPivot_NotACodedSize()
        {
            using var f = new Fixture();
            var art = new FurnitureArt(TestData.DataDirectory);
            FurnitureDefinition sink = f.T.Session.Definitions.Get<FurnitureDefinition>("comfy31.kitchen.sink.front");
            Assert.That(art.TryPrimary(sink, out _, out FurnitureSprite sprite), Is.True);
            // 좌표표: 96x144, PPU 48, 기준점(왼쪽 위 기준) 0.25, 0.8333 → 2x3칸, 기준점은 왼쪽 아래 칸 가운데.
            UiRect cell = new UiRect(100, 100, 48, 48);
            UiRect r = FurniturePlacementScreen.SpriteRect(cell, sprite);
            Assert.That(r.W, Is.EqualTo(sprite.W / sprite.PixelsPerUnit * 48).Within(0.01f));
            Assert.That(r.H, Is.EqualTo(sprite.H / sprite.PixelsPerUnit * 48).Within(0.01f));
            Assert.That(r.X + r.W * sprite.PivotX, Is.EqualTo(cell.CenterX).Within(0.01f));
            Assert.That(r.Y + r.H * sprite.PivotY, Is.EqualTo(cell.CenterY).Within(0.01f));
            Assert.That(r.Bottom, Is.EqualTo(cell.Bottom).Within(0.5f), "그림 아래가 기준 칸 아래에 닿는다(위로 솟는다)");
        }

        [Test]
        public void Texts_ExistInEveryLanguage_AndSwitchWithTheLanguage()
        {
            using var f = new Fixture();
            f.PickFromPalette(FreeDesk);
            f.T.Click(FurniturePlacementScreen.RotateId);
            f.T.Emit();
            f.T.Text.Language = "en";
            f.T.Emit();
            Assert.That(((UiButton)f.T.Node(FurniturePlacementScreen.DeleteId)).Text, Is.EqualTo("Delete"));
            Assert.That(((UiLabel)f.T.Node(FurniturePlacementScreen.SelectedDirectionId)).Text, Is.EqualTo("Facing: East"));
            f.Search("zzz-none");
            f.T.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(f.T.Text, f.Screen.TextKeys);
            foreach (string direction in new[] { "auto", "north", "east", "south", "west" })
                Assert.That(f.T.Text.Has("ui.furniture.direction." + direction, "ko") && f.T.Text.Has("ui.furniture.direction." + direction, "en"), Is.True);
        }

        [Test]
        public void OpeningAndPaging_With1500Pieces_IsCheap()
        {
            using var f = new Fixture();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++) { f.T.Click("hud.button.placement"); f.T.Click("hud.button.placement"); }
            double openMs = watch.Elapsed.TotalMilliseconds / 10;
            watch.Restart();
            for (int i = 0; i < 50; i++) f.T.Click(FurniturePlacementScreen.NextId);
            double pageMs = watch.Elapsed.TotalMilliseconds / 50;
            TestContext.Out.WriteLine($"열기+닫기 {openMs:F2} ms, 쪽 넘기기 {pageMs:F3} ms (가구 {f.Screen.Palette.TotalCount}개)");
            Assert.That(openMs, Is.LessThan(16.6), "한 프레임 안");
            Assert.That(pageMs, Is.LessThan(16.6));
            Assert.That(f.Screen.Root.Find(FurniturePlacementScreen.SlotId(20)), Is.Null, "칸은 한 쪽 20개만 만든다");
        }
    }
}
