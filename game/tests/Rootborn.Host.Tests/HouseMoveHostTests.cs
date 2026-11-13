using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// HOUSE-002를 실제 창과 게임 루프에서(장치 이벤트 주입, OS 입력 계층 미확인): 집 맵이 새 집으로 바뀌기 전의 세이브(벽이 된 칸에 책상과 깔개가
    /// 놓인 배치 파일)로 슬롯 선택에서 이어 하기 → 집에 들어서면 "옮겼고 보관했다" 알림 → P → 보관 분류 → 줄 고르기 → 바닥 칸 누르기 →
    /// 저장 → 다시 이어 하기 → 그대로. 준비 단계에서만 배치 파일을 임시 폴더의 슬롯에 쓴다(사용자 세이브 폴더는 쓰지 않는다). 조작은
    /// PlayerDriver의 동사(버튼 누르기, 키, 포인터)뿐이고 그림 픽셀은 단언하지 않는다. 스크린샷: output/house-building/stage1/house-move-NN-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HouseMoveHostTests
    {
        private const string Desk = "growth31.assembly.desk";
        private string _saves;
        private string _root;
        private string _evidence;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-house-move-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            _evidence = Path.Combine(_root, "output", "house-building", "stage1");
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Open()
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--size", "1920x1080", "--evidence", _evidence });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static void NewGame(PlayerDriver player)
        {
            player.PressButton(SaveSlotScreen.NewGameId(0));
            player.WaitForScreen(ScreenCatalog.CharacterCreator);
            player.PressButton(CharacterCreatorScreen.NameFieldId);
            player.TypeText("하늘");
            player.PressKey(TestKey.Escape);
            player.PressButton(CharacterCreatorScreen.ConfirmId);
            player.Frames(3);
        }

        private static void Continue(PlayerDriver player)
        {
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "실행하면 슬롯 선택부터");
            player.PressButton(SaveSlotScreen.ContinueId(0));
            player.Frames(3);
            Assert.That(player.Game.World, Is.Not.Null, "이어서 시작으로 월드에 들어간다");
        }

        private static FurniturePlacementScreen Placement(PlayerDriver p) => (FurniturePlacementScreen)p.Screens.Find(ScreenCatalog.FurniturePlacement);

        private static UiScrollList StoredList(PlayerDriver p) => (UiScrollList)p.Node(FurniturePlacementScreen.StoredListId);

        private static void ClickRow(PlayerDriver p, int row)
        {
            UiScrollList list = StoredList(p);
            Assert.That(list.TryGetRowRect(row, out UiRect r), Is.True, "줄이 보인다");
            Assert.That(p.Game.Ui.HitTest(r.CenterX, r.CenterY), Is.SameAs(list));
            p.ClickAt(r.CenterX, r.CenterY);
        }

        [Test]
        public void HOUSE_MOVE_001_OldFurnitureIsMoved_StoredOneIsPlacedFromTheStoredTab()
        {
            // 준비 1: 새 이야기를 시작해 슬롯을 만들고(입력), 끈다. 그 집(옛 세이브가 읽을 새 집)의 안쪽 벽 칸과, 한 칸 깔개가 들어갈 칸 수를 잰다.
            string slot, rug;
            int rugRoom;
            var wallCells = new List<GridPoint>();
            using (PlayerDriver first = Open())
            {
                NewGame(first);
                var session = first.Game.Session;
                var room = first.Game.World.Room;
                FurnitureDefinition rugDefinition = session.AvailableFurniture().First(f => !f.BlocksMovement && f.AllowsSurface("house")
                    && f.GetFootprintCells(FurniturePlacementDirection.North).Count == 1 && f.GetTileParts(FurniturePlacementDirection.North).Count == 1);
                rug = rugDefinition.StableId;
                rugRoom = room.FloorCells.Count(c => session.House.CanPlace(rugDefinition, c, FurniturePlacementDirection.North));
                var map = first.World.Map;
                for (int y = 1; y < map.Grid.Rows - 1; y++)
                    for (int x = 1; x < map.Grid.Columns - 1; x++)
                        if (map.Collision.IsBlocked(new Rootborn.Core.World.CellPos(x, y))) wallCells.Add(room.ToPlacement(new Rootborn.Core.World.CellPos(x, y)));
            }
            slot = Directory.GetDirectories(_saves).Select(Path.GetFileName).Single();
            Assert.That(wallCells.Count, Is.GreaterThan(10), "준비: 새 집 안쪽의 벽 칸");

            // 준비 2: 옛 세이브처럼 쓴다. 책상 하나를 벽 칸에, 깔개를 집이 담을 수 있는 수보다 둘 더 벽 칸에(앞 기록부터 자리를 얻고 뒤는 보관된다).
            // 집이 가득 차는 것은 보관이 생기게 하려는 시험 조건이다 — 가구는 어느 것이나 집 안 백 곳 넘게 들어가서 적은 수로는 보관이 생기지 않는다.
            GridPoint deskWall = wallCells[wallCells.Count / 3];
            var records = new List<FurniturePlacementSaveData> { Record(Desk, deskWall) };
            for (int i = 0; i < rugRoom + 2; i++) records.Add(Record(rug, wallCells[(wallCells.Count / 2 + i) % wallCells.Count]));
            new FileSaveStore(_saves).Write(slot, SaveFileNames.HouseFurnitureLayout(0),
                new FurniturePlacementLayoutSaveData { Version = FurniturePlacementLayoutSaveData.KnownVersion, Items = records.ToArray() });

            int shot = 1;
            int storedAtStart, placedAtStart, deleted = 0;
            GridPoint deskMoved;
            using (PlayerDriver player = Open())
            {
                string Shot(string name) => player.Screenshot($"house-move-{shot++:D2}-{name}");
                Continue(player);
                var session = player.Game.Session;
                Assert.That(player.World.Map.Id, Is.EqualTo("House"));

                // 알림: 옮긴 수와 보관한 수.
                Assert.That(player.Screens.IsOpen(ScreenCatalog.HouseMovedNotice), Is.True, "집에 들어서면 알림");
                storedAtStart = session.StoredFurniture().Count;
                placedAtStart = session.House.Placed.Count;
                Assert.That(storedAtStart, Is.GreaterThanOrEqualTo(2), "둘 이상 보관됐다");
                Assert.That(placedAtStart + storedAtStart, Is.EqualTo(records.Count), "하나도 잃지 않았다");
                var message = (UiTextBlock)player.Node(HouseMovedNoticeScreen.MessageId);
                Assert.That(Enumerable.Range(0, placedAtStart + 1).Any(moved => message.Text == player.Game.Text.Format(HouseMovedNoticeScreen.KeyFor(moved, storedAtStart), moved, storedAtStart)), Is.True,
                    "알림 글: 옮긴 수와 보관한 수(" + storedAtStart + ")");
                TestContext.Out.WriteLine("알림: " + message.Text);
                Assert.That(player.Screens.MovementBlocked, Is.False, "알림은 걷기를 막지 않는다");
                Shot("notice");
                PlacedFurniture desk = session.House.Placed.Single(p => p.Definition.StableId == Desk);
                deskMoved = desk.Anchor;
                TestContext.Out.WriteLine($"책상: 옛 세이브의 벽 칸 ({deskWall.X},{deskWall.Y}) → 새 집의 바닥 ({deskMoved.X},{deskMoved.Y}) (가구 칸 좌표, y 위로)");
                player.Frames(60 * 6); // 알림이 저절로 닫힌다(5초)
                Assert.That(player.Screens.IsOpen(ScreenCatalog.HouseMovedNotice), Is.False, "5초 뒤 닫힘");
                player.Frames(2);
                Shot("moved-furniture-in-the-house");

                // P → 가구 분류(기본) → 보관 분류.
                player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
                FurniturePlacementScreen screen = Placement(player);
                Assert.That(screen.StoredShown, Is.False);
                Shot("furniture-tab");
                player.PressButton(FurniturePlacementScreen.CategoryStoredId);
                player.MovePointer(960, 20);
                player.Frames(2);
                Assert.That(screen.StoredShown, Is.True);
                Assert.That(StoredList(player).RowCount, Is.EqualTo(storedAtStart));
                Shot("stored-tab");

                // 자리가 없다: 놓인 깔개를 하나씩 골라 치워(삭제 단추) 자리를 만들고, 보관한 가구가 들어가는 칸이 생기면 거기에 놓는다.
                GridPoint target = default;
                bool found = false;
                var view = FurniturePlacementHostTests.View(player);
                foreach (PlacedFurniture placedRug in session.House.Placed.Where(p => p.Definition.StableId == rug && Visible(player, p.Anchor)).Take(6).ToList())
                {
                    GridPoint blocker = placedRug.Anchor;
                    player.PressButton(FurniturePlacementScreen.CategoryFurnitureId);
                    UiRect blockerRect = view.CellRect(blocker);
                    player.ClickAt(blockerRect.CenterX, blockerRect.CenterY);
                    Assert.That(screen.SelectedInstanceId, Is.Not.Null, "놓인 깔개를 골랐다");
                    player.PressButton(FurniturePlacementScreen.DeleteId);
                    deleted++;
                    player.PressButton(FurniturePlacementScreen.CategoryStoredId);
                    ClickRow(player, 0);
                    Assert.That(screen.PickedStoredIndex, Is.GreaterThanOrEqualTo(0), "줄을 골랐다");
                    foreach (GridPoint floor in player.Game.World.Room.FloorCells)
                    {
                        if (!Visible(player, floor)) continue;
                        if (session.CanPlaceStoredFurniture(screen.PickedStoredIndex, floor, screen.Direction)) { target = floor; found = true; break; }
                    }
                    if (found) break;
                }
                Assert.That(found, Is.True, "깔개를 치우자 보관한 가구가 들어갈 칸이 생겼다");
                UiRect rect = view.CellRect(target);
                player.MovePointer(rect.CenterX, rect.CenterY);
                player.Frames(2);
                Shot("stored-row-picked-hover");
                player.ClickAt(rect.CenterX, rect.CenterY);
                Assert.That(screen.LastAction, Is.EqualTo("placed"));
                Assert.That(session.StoredFurniture().Count, Is.EqualTo(storedAtStart - 1), "놓은 줄은 보관에서 빠진다");
                Assert.That(StoredList(player).RowCount, Is.EqualTo(storedAtStart - 1));
                player.MovePointer(960, 20);
                player.Frames(2);
                Shot("stored-after-place");
                Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, screen.TextKeys);
                Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            }

            // 다시 이어 하기: 놓은 것은 놓인 채, 보관은 하나 줄어든 채, 책상은 같은 자리.
            using (PlayerDriver again = Open())
            {
                Continue(again);
                var session = again.Game.Session;
                Assert.That(session.StoredFurniture().Count, Is.EqualTo(storedAtStart - 1));
                Assert.That(session.House.Placed.Count, Is.EqualTo(placedAtStart - deleted + 1), "치운 깔개를 빼고 보관에서 놓은 하나를 더한 수");
                Assert.That(session.House.Placed.Single(p => p.Definition.StableId == Desk).Anchor, Is.EqualTo(deskMoved), "옮겨진 책상은 같은 자리");
                again.Frames(2);
                again.Screenshot($"house-move-{shot++:D2}-after-continue");
            }
        }

        [Test]
        public void HOUSE_MOVE_002_NothingStored_TheStoredTabSaysSo()
        {
            using PlayerDriver player = Open();
            NewGame(player);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.HouseMovedNotice), Is.False, "옮긴 것도 보관한 것도 없으면 알림이 없다");
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            player.PressButton(FurniturePlacementScreen.CategoryStoredId);
            player.MovePointer(960, 20);
            player.Frames(2);
            var empty = (UiTextBlock)player.Node(FurniturePlacementScreen.StoredEmptyId);
            Assert.That(empty.IsActive, Is.True);
            Assert.That(empty.Text, Is.EqualTo(player.Game.Text.Get("ui.furniture.stored.empty")));
            player.Screenshot("house-move-20-stored-empty");
        }

        private static bool Visible(PlayerDriver p, GridPoint cell)
        {
            UiRect r = FurniturePlacementHostTests.View(p).CellRect(cell);
            return r.X >= 0 && r.Y >= 0 && r.Right <= 1920 && r.Bottom <= 1080 && p.Game.Ui.HitTest(r.CenterX, r.CenterY) == null;
        }

        private static FurniturePlacementSaveData Record(string id, GridPoint at) => new FurniturePlacementSaveData
        {
            FurnitureId = id, SurfaceId = "house", AnchorX = at.X, AnchorY = at.Y, Direction = FurniturePlacementDirection.North, StateJson = "{}",
        };
    }
}
