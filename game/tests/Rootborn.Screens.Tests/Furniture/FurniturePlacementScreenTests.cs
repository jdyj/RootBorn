using System;
using System.Collections.Generic;
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
        internal sealed class TestView : IPlacementView
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
            public readonly TestSession T;
            public readonly FurnitureRoom Room = new FurnitureRoom(TestRoomMap());
            public readonly TestView View;
            public readonly FurniturePlacementScreen Screen;

            /// <param name="shares">같이 하는 방의 조각 묶음(같이 쓰는 집 시험). 없으면 혼자 하기.</param>
            public Fixture(Rootborn.Core.Room.RoomShares shares = null)
            {
                T = new TestSession(playerId: shares == null ? null : "me", shares: shares);
                View = new TestView(Room);
                T.Session.EnterHouse(Room.Grid, "house");
                T.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
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
                Assert.That(((UiFittedImage)f.T.Node(FurniturePlacementScreen.SlotId(i) + ".icon")).Image, Is.Not.Null.And.Not.Empty, "가구 그림");
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
            Assert.That(((UiFittedImage)f.T.Node(FurniturePlacementScreen.SelectedIconId)).Image, Does.Contain(desk.TileParts[0].Name));

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

        // ---------- 같이 쓰는 집 ----------

        /// <summary>같이 쓰는 집의 방 쪽(Core RoomPieceSet, 시험용 방 칸으로 판정). 보낼 요청을 받아 답과 상태를 돌려준다(서버가 할 일).</summary>
        private sealed class HouseRoom
        {
            private readonly List<(string Player, Rootborn.Core.Room.RoomShares Shares)> _players = new List<(string, Rootborn.Core.Room.RoomShares)>();
            private int _ids;
            public readonly Rootborn.Core.Room.RoomPieceSet Pieces;

            public HouseRoom(GameDefinitionsSource definitions)
            {
                var grid = new FurnitureRoom(TestRoomMap()).Grid;
                var rules = new Rootborn.Core.Room.RoomHouseRules(definitions(), s => s == "house" ? grid : null);
                Pieces = new Rootborn.Core.Room.RoomPieceSet(new Rootborn.Core.Room.RoomShareSettings(new[] { Rootborn.Core.Room.RoomHouse.PieceName }),
                    Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(rules));
            }

            public Rootborn.Core.Room.RoomShares Join(string player)
            {
                var shares = new Rootborn.Core.Room.RoomShares(Pieces.Settings, Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(), () => player + "-r" + (++_ids));
                foreach (var piece in Pieces.Pieces) shares.Receive(piece.State());
                _players.Add((player, shares));
                return shares;
            }

            public void Deliver(Rootborn.Core.Room.RoomShares from)
            {
                var (player, _) = _players.First(p => p.Shares == from);
                var answers = new List<Rootborn.Core.Room.RoomAnswer>();
                var changed = new List<Rootborn.Core.Room.IRoomPiece>();
                Pieces.Apply(player, from.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
                foreach (var a in answers) from.Receive(a);
                foreach (var piece in changed)
                    foreach (var p in _players) p.Shares.Receive(piece.State());
            }
        }

        private delegate IEnumerable<FurnitureDefinition> GameDefinitionsSource();

        private static IEnumerable<FurnitureDefinition> AllFurniture()
        {
            using var t = new TestSession();
            return t.Session.Definitions.All<FurnitureDefinition>().ToList();
        }

        private static string RoomStatus(Fixture f) => ((UiTextBlock)f.T.Node(FurniturePlacementScreen.RoomStatusId)).Text;

        [Test]
        public void SharedHouse_ClickingACell_WaitsForTheRoom_ThenThePieceAppears()
        {
            var room = new HouseRoom(AllFurniture);
            var mine = room.Join("me");
            using var f = new Fixture(mine);
            Assert.That(f.T.Session.SharesHouse, Is.True);
            f.PickFromPalette(FreeDesk);
            var cell = new GridPoint(3, 3);
            f.ClickCell(cell);
            f.T.Emit();
            Assert.That(f.Screen.LastAction, Is.EqualTo("waiting"), "방에 물었다");
            Assert.That(RoomStatus(f), Is.EqualTo(f.T.Text.Get("ui.furniture.room-waiting")));
            Assert.That(f.T.Session.House.Placed, Is.Empty, "허락 전에는 바뀌지 않는다");
            f.ClickCell(new GridPoint(5, 3));
            Assert.That(mine.Outbox.Requests.Count, Is.EqualTo(1), "기다리는 동안 다시 누를 수 없다");

            room.Deliver(mine);
            f.T.Emit();
            Assert.That(f.Screen.LastAction, Is.EqualTo("placed"));
            Assert.That(RoomStatus(f), Is.Empty);
            Assert.That(f.T.Session.House.Placed.Single().Anchor, Is.EqualTo(cell));
            KnownMissingKeys.AssertOnlyKnownMissing(f.T.Text, f.T.Stack.Find(ScreenCatalog.FurniturePlacement).TextKeys);
        }

        [Test]
        public void SharedHouse_SomeoneElseTookTheCellFirst_SaysSo_AndNothingIsPlacedForMe()
        {
            var room = new HouseRoom(AllFurniture);
            var mine = room.Join("me");
            var theirs = room.Join("friend");
            using var f = new Fixture(mine);
            f.PickFromPalette(FreeDesk);
            var cell = new GridPoint(3, 3);
            f.ClickCell(cell);
            // 친구가 같은 칸에 먼저 놓았다(준비 — 친구의 요청을 방에 직접 적는다).
            theirs.Find<Rootborn.Core.Room.RoomHouseClient>().RequestPlace("house", FreeDesk, cell, FurniturePlacementDirection.North);
            room.Deliver(theirs);
            room.Deliver(mine);
            f.T.Emit();
            Assert.That(f.Screen.LastAction, Is.EqualTo("refused"));
            Assert.That(RoomStatus(f), Is.EqualTo(f.T.Text.Get("ui.furniture.room-refused")));
            Assert.That(f.T.Session.House.Placed.Single().Anchor, Is.EqualTo(cell), "친구가 놓은 가구가 보인다");
            Assert.That(room.Pieces.TryGet(Rootborn.Core.Room.RoomHouse.PieceName, out var house)
                        && ((Rootborn.Core.Room.RoomHouse)house).Placed.Single().PlacedBy == "friend", Is.True);
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
        public void DeletingAChestWithItems_IsRefused_WithAReason_AndAnEmptyChestIsDeleted()
        {
            using var f = new Fixture();
            string crate = f.T.Session.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;
            f.PickFromPalette(crate);
            f.ClickCell(new GridPoint(3, 3));
            string id = f.Screen.SelectedInstanceId;
            Assert.That(id, Is.Not.Null);
            f.T.Session.Inventory.Add(f.T.Session.Item("Wood"), 2);
            Assert.That(f.T.Session.PutIntoStorage(id, "Wood", 2), Is.EqualTo(StorageTransferResult.Moved)); // 준비

            f.T.Click(FurniturePlacementScreen.DeleteId);
            Assert.That(f.T.Session.House.Placed.Single().InstanceId, Is.EqualTo(id), "물건이 든 상자는 남는다");
            Assert.That(f.Screen.LastAction, Is.EqualTo("storage-not-empty"));
            Assert.That(f.T.Texts(f.T.Emit()), Does.Contain(f.T.Text.Get("ui.furniture.storage-not-empty")));

            Assert.That(f.T.Session.TakeFromStorage(id, "Wood", 2), Is.EqualTo(StorageTransferResult.Moved));
            f.T.Click(FurniturePlacementScreen.DeleteId);
            Assert.That(f.T.Session.House.Placed, Is.Empty, "비우면 치운다");
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

        // ---------- 일터(요리사 식당) — 점수 줄, 한도, 주인 ----------

        private static string Text(UiNode node) => node is UiLabel l ? l.Text : node is UiTextBlock b ? b.Text : ((UiButton)node).Text;

        [Test]
        public void Placement_InWorkspace_ShowsQualityRowAndCapacity()
        {
            using var w = new Workspaces.WorkspaceFixture();
            var status = w.Session.ActiveWorkspaceStatus;
            Assert.That(status, Is.Not.Null, "준비: 일터가 열렸다");
            Assert.That(w.Node(FurniturePlacementScreen.WorkspaceRowId).Visible, Is.True);
            var qualities = w.Definitions.All<Rootborn.Core.Workspaces.SpaceQuality>().ToList();
            Assert.That(qualities.Count, Is.GreaterThan(0));
            foreach (var quality in qualities)
            {
                Assert.That(Text(w.Node(FurniturePlacementScreen.QualityId(quality.Id))), Is.EqualTo(status.Scores.Get(quality.Id).ToString()), quality.Id);
                Assert.That(((UiImage)w.Node(FurniturePlacementScreen.QualityId(quality.Id) + ".icon")).Image, Is.EqualTo("icon:" + quality.IconKey), "아이콘 키는 데이터에서");
            }
            Assert.That(Text(w.Node(FurniturePlacementScreen.CapacityId)), Is.EqualTo(w.Text.Format("ui.workspace.capacity", status.Placed, status.Capacity)));
            Assert.That(status.Placed, Is.EqualTo(w.Session.House.Placed.Count));
            Assert.That(w.Node(FurniturePlacementScreen.TemplatesId).IsActive, Is.True, "자기 일터면 템플릿 단추를 누를 수 있다");
            Assert.That(w.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.True, "한도 안이면 카드를 누를 수 있다");
            Assert.That(Text(w.Node(FurniturePlacementScreen.WorkspaceReasonId)), Is.Empty);
            Assert.That(Text(w.Node(FurniturePlacementScreen.RoomStatusId)), Is.Empty);

            // 줄은 다른 패널과 겹치지 않는다(위 도구 줄 바로 아래).
            UiRect row = w.Node(FurniturePlacementScreen.WorkspaceRowId).Rect;
            foreach (string other in new[] { FurniturePlacementScreen.PaletteId, "furniture.settings", "furniture.toolbar", "furniture.bottom" })
            {
                UiRect r = w.Node(other).Rect;
                Assert.That(row.X >= r.Right || r.X >= row.Right || row.Y >= r.Bottom || r.Y >= row.Bottom, Is.True, other + " " + r + " / 줄 " + row);
            }
            KnownMissingKeys.AssertOnlyKnownMissing(w.Text, w.Placement.TextKeys);
        }

        [Test]
        public void Placement_InHouse_HidesWorkspaceRow()
        {
            using var f = new Fixture();
            Assert.That(f.T.Session.ActiveWorkspace, Is.Null);
            Assert.That(f.T.Node(FurniturePlacementScreen.WorkspaceRowId).Visible, Is.False, "집에서는 일터 줄이 없다");
            Assert.That(f.T.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.True, "집은 한도가 없다");
        }

        [Test]
        public void Placement_AtCapacity_CardsDisabledWithReason()
        {
            using var w = new Workspaces.WorkspaceFixture();
            w.FillToCapacity(); // 준비(GameSession 명령)
            w.Emit();
            Assert.That(w.Session.CanAddToActiveWorkspace(), Is.EqualTo(Rootborn.Core.Workspaces.WorkspaceResultKind.OverCapacity));
            var status = w.Session.ActiveWorkspaceStatus;
            Assert.That(Text(w.Node(FurniturePlacementScreen.CapacityId)), Is.EqualTo(w.Text.Format("ui.workspace.capacity", status.Capacity, status.Capacity)));
            for (int i = 0; i < FurniturePlacementScreen.Metrics.PageSize; i++)
                Assert.That(w.Node(FurniturePlacementScreen.SlotId(i)).IsActive, Is.False, "한도에 닿으면 카드를 누를 수 없다: " + i);
            Assert.That(Text(w.Node(FurniturePlacementScreen.WorkspaceReasonId)), Is.EqualTo(w.Text.Get("ui.workspace.full")));
            Assert.That(w.Node(FurniturePlacementScreen.TemplatesId).IsActive, Is.True, "템플릿은 한도와 따로 Core가 판단한다");

            // 하나 치우면 다시 누를 수 있다(삭제 단추로). 패널 밑이 아닌 칸의 가구를 고른다(시험용 보기는 화면 일부를 패널이 덮는다).
            PlacedFurniture target = w.Session.House.Placed.First(p =>
            {
                (float x, float y) = w.View.Center(p.Anchor);
                return w.Stack.Tree.HitTest(x, y) == null;
            });
            w.ClickCell(target.Anchor);
            Assert.That(w.Placement.LastAction, Is.EqualTo("selected"));
            w.Click(FurniturePlacementScreen.DeleteId);
            w.Emit();
            Assert.That(w.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.True, "한도 밑으로 내려가면 카드를 다시 누를 수 있다");
            Assert.That(Text(w.Node(FurniturePlacementScreen.WorkspaceReasonId)), Is.Empty);
        }

        [Test]
        public void Placement_NotOwner_Disabled()
        {
            using var w = new Workspaces.WorkspaceFixture(adult: false);
            Assert.That(w.Session.ActiveWorkspace, Is.Not.Null, "준비: 학생이 요리사 일터에 들어왔다");
            Assert.That(w.Session.OwnsActiveWorkspace, Is.False);
            Assert.That(w.Node(FurniturePlacementScreen.WorkspaceRowId).Visible, Is.True);
            Assert.That(w.Node(FurniturePlacementScreen.TemplatesId).IsActive, Is.False, "남의 일터는 템플릿 단추가 꺼진다");
            Assert.That(w.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.False, "남의 일터는 카드가 꺼진다");
            Assert.That(Text(w.Node(FurniturePlacementScreen.WorkspaceReasonId)), Is.EqualTo(w.Text.Get("ui.workspace.not-owner")));
        }

        [Test]
        public void PlacingFurniture_UpdatesQualityScore()
        {
            using var w = new Workspaces.WorkspaceFixture();
            string cozy = w.FurnitureGiving(Workspaces.WorkspaceFixture.Cozy).First(id => w.Session.House.Placed.All(p => p.Definition.StableId != id));
            var label = (UiLabel)w.Node(FurniturePlacementScreen.QualityId(Workspaces.WorkspaceFixture.Cozy));
            string before = label.Text;
            int placedBefore = w.Session.House.Placed.Count;

            w.PickFromPalette(cozy);
            w.ClickCell(w.FreeCell(cozy));
            w.Emit();
            Assert.That(w.Placement.LastAction, Is.EqualTo("placed"));
            Assert.That(w.Session.House.Placed.Count, Is.EqualTo(placedBefore + 1));
            Assert.That(label.Text, Is.Not.EqualTo(before), "놓은 뒤 아늑함 점수 글자가 바뀐다");
            Assert.That(label.Text, Is.EqualTo(w.Session.ActiveWorkspaceStatus.Scores.Get(Workspaces.WorkspaceFixture.Cozy).ToString()));
            Assert.That(Text(w.Node(FurniturePlacementScreen.CapacityId)), Does.StartWith((placedBefore + 1) + "/"));
        }

        [Test]
        public void SharedHouseRoom_TheWorkspaceIsNotShownAsShared()
        {
            var room = new HouseRoom(AllFurniture);
            var mine = room.Join("me");
            using var w = new Workspaces.WorkspaceFixture(shares: mine, playerId: "me");
            Assert.That(w.Session.SharesHouse, Is.True, "준비: 집을 같이 쓰는 방");
            string cozy = w.FurnitureGiving(Workspaces.WorkspaceFixture.Cozy).First(id => w.Session.House.Placed.All(p => p.Definition.StableId != id));
            w.PickFromPalette(cozy);
            w.ClickCell(w.FreeCell(cozy));
            w.Emit();
            Assert.That(w.Placement.LastAction, Is.EqualTo("placed"), "일터는 방에 묻지 않고 바로 놓인다");
            Assert.That(Text(w.Node(FurniturePlacementScreen.RoomStatusId)), Is.Empty, "공유 중·기다림 문구가 없다");
        }

        [Test]
        public void WorkspaceRow_LongText_StaysInsideItsPanel()
        {
            using var w = new Workspaces.WorkspaceFixture(adult: false, longText: true);
            var measurer = new WideMeasurer();
            for (int i = 0; i < 2; i++) { w.Stack.Layout(1920, 1080); w.Stack.Emit(new DrawList(), measurer); }
            w.Stack.Layout(1920, 1080);
            UiNode row = w.Node(FurniturePlacementScreen.WorkspaceRowId);
            Assert.That(row.Visible, Is.True);
            foreach (UiNode node in new[] { row }.Concat(row.Children))
            {
                var list = new DrawList();
                node.Emit(list, measurer);
                foreach (DrawCommand text in list.Commands.Where(c => c.Kind == DrawKind.Text))
                {
                    float right = text.Rect.X + measurer.Width(text.Text, text.FontSize), bottom = text.Rect.Y + measurer.LineHeight(text.FontSize);
                    Assert.That(text.Rect.X >= row.Rect.X - 0.5f && right <= row.Rect.Right + 0.5f && text.Rect.Y >= row.Rect.Y - 0.5f && bottom <= row.Rect.Bottom + 0.5f,
                        Is.True, (node.Id ?? node.GetType().Name) + ": " + text.Text);
                }
            }
        }

        /// <summary>
        /// 가구 1,500여 개에서 열기·닫기와 쪽 넘기기가 가구 수에 따라 커지지 않는지. 벽시계는 기계 부하로 흔들려 쓰지 않고
        /// 흔들리지 않는 기준을 본다: 만든 노드 수(열고 닫고 넘겨도 늘지 않는다), 그린 명령 수(한 쪽 20칸만 그린다 — 쪽마다 같다),
        /// 쪽 넘기기 한 번의 할당 바이트(쪽 크기만큼만, 전체 목록을 훑어 새로 만들지 않는다. 그림 좌표표를 읽은 뒤 — 2026-11-12 잰 값 약 9KB).
        /// </summary>
        [Test]
        public void OpeningAndPaging_With1500Pieces_IsCheap()
        {
            using var f = new Fixture();
            Assume.That(f.Screen.Palette.TotalCount, Is.GreaterThan(1000), "실제 data/의 가구가 많다");
            int nodes = CountNodes(f.Screen.Root);
            int drawn = f.T.Emit().Commands.Count;

            for (int i = 0; i < 10; i++) { f.T.Click("hud.button.placement"); f.T.Click("hud.button.placement"); }
            Assert.That(CountNodes(f.Screen.Root), Is.EqualTo(nodes), "열고 닫아도 노드를 새로 만들지 않는다");
            Assert.That(f.Screen.Root.Find(FurniturePlacementScreen.SlotId(20)), Is.Null, "칸은 한 쪽 20개만 만든다");

            // 가구 그림 좌표표는 그 쪽을 처음 볼 때 한 번 읽는다(FurnitureArt 캐시). 한 바퀴 돌아 읽힌 뒤 두 번째 바퀴를 잰다.
            const int pages = 50;
            for (int i = 0; i < pages; i++) f.T.Click(FurniturePlacementScreen.NextId);
            while (f.T.Node(FurniturePlacementScreen.PreviousId).IsActive) f.T.Click(FurniturePlacementScreen.PreviousId);
            f.T.Emit();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < pages; i++) f.T.Click(FurniturePlacementScreen.NextId); // 포인터로 누르기(판정 포함)
            long perPage = (GC.GetAllocatedBytesForCurrentThread() - before) / pages;
            TestContext.Out.WriteLine($"노드 {nodes}개, 그린 명령 {drawn}개, 쪽 넘기기 한 번 할당 {perPage} 바이트 (가구 {f.Screen.Palette.TotalCount}개)");
            Assert.That(CountNodes(f.Screen.Root), Is.EqualTo(nodes), "쪽을 넘겨도 노드를 새로 만들지 않는다");
            Assert.That(perPage, Is.LessThan(PagingAllocationBudget), "쪽 넘기기는 한 쪽 분량만 다룬다(가구 수에 비례해 할당하지 않는다)");
            for (int i = 0; i < 3; i++)
            {
                f.T.Click(FurniturePlacementScreen.NextId);
                Assert.That(f.T.Emit().Commands.Count, Is.LessThanOrEqualTo(drawn + DrawSlack), "그린 명령 수가 쪽마다 거의 같다(보이는 칸만 그린다)");
            }
        }

        /// <summary>쪽 넘기기 한 번에 허용하는 할당. 가구 정의 하나를 훑을 때마다 작은 객체 하나만 만들어도 1,500개면 이를 넘는다.</summary>
        private const long PagingAllocationBudget = 32 * 1024;

        /// <summary>쪽마다 이름 길이가 달라 줄바꿈 글자 명령 수가 조금 다를 수 있다.</summary>
        private const int DrawSlack = 40;

        private static int CountNodes(UiNode node)
        {
            int count = 1;
            foreach (UiNode child in node.Children) count += CountNodes(child);
            return count;
        }
    }
}
