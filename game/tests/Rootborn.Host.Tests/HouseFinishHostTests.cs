using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.World;
using Rootborn.Game;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Start;
using UiRect = Rootborn.Ui.UiRect;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 짓는 집을 칸마다 고른 꾸밈으로 다시 조합해 그린다(1단계 (나) 작업 3). 실제 창과 게임 루프(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 새 게임·걷기·이어 하기는 PlayerDriver의 동사(버튼, 글자, 걸어가기). 꾸미는 화면은 뒤 작업이라 꾸밈 적용과 돈 주기는 GameSession 명령을 직접 부른다
    /// — 준비 단계이고 실제 플레이가 아니다. 칸 좌표를 적지 않는다: 방은 게임이 읽은 출처표를 Core 방 채우기로 나눠 찾는다.
    /// 그림 색은 단언하지 않는다 — GPU에 올린 조합 텍스처가 같은 조회로 엔진 없이 조합한 것과 같은지, 바뀐 방의 바닥이 그 꾸밈의 조각에서 왔는지만 본다.
    /// 스크린샷은 직접 연다: output/house-building/stage1b/house-finish-NN-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HouseFinishHostTests
    {
        private const int Size = RoomTilePlan.CellSize;
        /// <summary>바꿔 볼 바닥 꾸밈(실제 data의 id — 테스트는 실제 데이터 id로 확인해도 된다).</summary>
        private const string Carpet = "finish.floor.carpet";
        private string _root, _saves, _evidence;

        [SetUp]
        public void SetUp()
        {
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-house-finish-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _evidence = Path.Combine(_root, "output", "house-building", "stage1b");
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
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "실행하면 슬롯 선택부터");
            player.PressButton(SaveSlotScreen.NewGameId(0));
            player.WaitForScreen(ScreenCatalog.CharacterCreator);
            player.PressButton(CharacterCreatorScreen.NameFieldId);
            player.TypeText("하늘");
            player.PressKey(TestKey.Escape);
            player.PressButton(CharacterCreatorScreen.ConfirmId);
            player.Frames(3);
            Assert.That(player.Game.World, Is.Not.Null, "시작하기로 월드에 들어간다");
            Assert.That(player.Game.World.Current.Entry.Build, Is.Not.Null, "새 게임은 짓는 집에서 시작한다");
        }

        private static void Continue(PlayerDriver player)
        {
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "실행하면 슬롯 선택부터");
            player.PressButton(SaveSlotScreen.ContinueId(0));
            player.Frames(3);
            Assert.That(player.Game.World, Is.Not.Null, "이어서 시작으로 월드에 들어간다");
        }

        /// <summary>집의 왼쪽 위 방(행 순서로 첫 바닥 칸의 방, 문간 포함 — Core 방 채우기)과 그 방의 가운데에 가까운 문간 아닌 바닥 칸.</summary>
        private static (IReadOnlyList<CellPos> Room, CellPos Middle) TopLeftRoom(LoadedMap map)
        {
            RoomGrid grid = map.Tiles.Grid;
            RoomFill fill = RoomFill.Build(grid);
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (grid.At(x, y) == RoomCells.Floor)
                    {
                        IReadOnlyList<CellPos> room = fill.RoomOf(new CellPos(x, y));
                        double cx = room.Average(c => c.X), cy = room.Average(c => c.Y);
                        CellPos middle = room.Where(c => !fill.IsDoorway(c)).OrderBy(c => (c.X - cx) * (c.X - cx) + (c.Y - cy) * (c.Y - cy)).First();
                        return (room, middle);
                    }
            throw new InvalidOperationException("바닥이 없다");
        }

        /// <summary>처음부터 열린, 그 자리의 기본이 아닌 꾸밈(데이터 순서의 첫 것).</summary>
        private static RoomFinishDefinition OtherFree(WorldScreen world, string slot, string notThis) =>
            world.Content.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == slot && f.UnlockToken.Length == 0 && f.Id != notThis);

        /// <summary>준비 단계(실제 플레이 아님): 미리 보기의 비용만큼 돈을 주고 GameSession 명령으로 꾸밈을 적용한다.</summary>
        private static void ApplyAsSetup(GameSession session, string finishId, CellPos target)
        {
            HouseFinishPreview preview = session.PreviewHouseFinish(finishId, HouseFinishTools.Room, target);
            foreach (var cost in preview.Cost) session.Inventory.Add(session.Item(cost.Item), cost.Count);
            preview = session.PreviewHouseFinish(finishId, HouseFinishTools.Room, target);
            Assert.That(preview.CanApply, Is.True, preview.ReasonKey);
            Assert.That(session.ApplyHouseFinish(finishId, HouseFinishTools.Room, target), Is.True);
        }

        private static uint[] Pixels(Texture2D texture)
        {
            var pixels = new uint[texture.Width * texture.Height];
            texture.GetData(pixels);
            return pixels;
        }

        private sealed class TableLookup : IHouseFinishLookup
        {
            public readonly Dictionary<CellPos, string> Floors = new Dictionary<CellPos, string>();
            /// <summary>벽지(비면 모든 칸이 기본).</summary>
            public Func<CellPos, string> Walls;
            public string WallAt(CellPos floorCell) => Walls?.Invoke(floorCell) ?? "";
            public string FloorAt(CellPos floorCell) => Floors.TryGetValue(floorCell, out string id) ? id : "";
            public string BandColors => "";
        }

        /// <summary>GPU 조합 텍스처 = 같은 조회(그 방만 다른 바닥)로 엔진 없이 조합한 것(칸마다 견준다). 바뀐 방의 그림자 없는 바닥 부분은 그 꾸밈의 floor 조각 그대로.</summary>
        /// <param name="walls">벽지 조회(비면 모든 칸이 기본 벽지 — 바닥만 바꾼 판).</param>
        private void AssertDrawnWith(PlayerDriver player, IReadOnlyList<CellPos> room, RoomFinishDefinition floor, string why, Func<CellPos, string> walls = null)
        {
            WorldScreen world = player.Game.World;
            LoadedMap map = world.Current;
            using var sheets = new ComposedRoomTextures(player.Game.GraphicsDevice, _root);
            var art = new RoomFinishArt(Path.Combine(_root, "data"));
            var lookup = new TableLookup { Walls = walls };
            foreach (CellPos c in room) lookup.Floors[c] = floor.Id;
            RoomFinishDefinition Find(string id) => world.Content.Definitions.TryGet(id ?? "", out RoomFinishDefinition d) ? d : null;
            ComposedMapTiles want = ComposedMapTiles.Compose(map, new RoomTileCompositor(sheets.Sheet, art), Find, lookup);
            ComposedMapTiles got = world.ComposedTiles;
            Assert.That((want.Ok, got?.Ok), Is.EqualTo((true, (bool?)true)), why);
            uint[] gpu = Pixels(world.ComposedTexture);
            int width = world.ComposedTexture.Width;
            var different = new List<string>();
            for (int i = 0; i < want.Room.Cells.Count; i++)
            {
                var (cell, wx, wy) = want.Room.Cells[i];
                var (gotCell, gx, gy) = got.Room.Cells[i];
                Assert.That(gotCell, Is.EqualTo(cell));
                for (int p = 0; p < Size * Size && different.Count < 20; p++)
                {
                    uint a = want.Room.Pixels[(wy + p / Size) * want.Room.Width + wx + p % Size];
                    uint b = gpu[(gy + p / Size) * width + gx + p % Size];
                    if ((a >> 24) == 0) a = 0;
                    if ((b >> 24) == 0) b = 0;
                    if (a != b) { different.Add(cell + " (" + p % Size + ", " + p / Size + ")"); break; }
                }
            }
            Assert.That(different, Is.Empty, why + ": GPU 조합 = 엔진 없이 같은 조회로 조합:\n" + string.Join("\n", different));

            Assert.That(art.TryPiece(floor, "floor", out FinishPiece piece), Is.True);
            PieceImage sheet = sheets.Sheet(piece.Texture);
            var inRoom = new HashSet<CellPos>(room);
            int fromPiece = 0;
            foreach (CellTiles cell in map.Tiles.Cells.Where(c => inRoom.Contains(c.Cell)))
            {
                var (_, gx, gy) = got.Room.Cells.First(c => c.Cell.Equals(cell.Cell));
                foreach (TilePart part in cell.Parts.Where(p => p.Source == TileSources.Floor && p.Shade == "floor"))
                    foreach (PixelRect r in part.Rects)
                        for (int y = r.Y0; y < r.Y1; y++)
                            for (int x = r.X0; x < r.X1; x++)
                            {
                                Assert.That(gpu[(gy + y) * width + gx + x], Is.EqualTo(sheet.Pixels[(piece.Y + y) * sheet.Width + piece.X + x]),
                                    $"{why}: {cell.Cell} ({x}, {y}) {floor.Id}의 floor 조각에서");
                                fromPiece++;
                            }
            }
            Assert.That(fromPiece, Is.GreaterThan(0), why + ": 바뀐 방의 바닥 픽셀을 본다");
        }

        [Test]
        public void HOUSE_BUILD_001_ChangedFloor_IsRedrawnOnce_AndAfterContinue()
        {
            int shot = 1;
            IReadOnlyList<CellPos> room;
            RoomFinishDefinition carpet;
            using (PlayerDriver player = Open())
            {
                string Shot(string name) => player.Screenshot($"house-finish-{shot++:D2}-{name}");
                NewGame(player);
                WorldScreen world = player.Game.World;
                GameSession session = player.Game.Session;
                LoadedMap map = world.Current;
                CellPos middle;
                (room, middle) = TopLeftRoom(map);
                carpet = world.Content.Definitions.Get<RoomFinishDefinition>(Carpet);
                Assert.That((carpet.Slot, carpet.UnlockToken, carpet.Id == map.Entry.Build.DefaultFloor), Is.EqualTo((RoomFinishSlots.Floor, "", false)),
                    "전제: 처음부터 열린, 기본이 아닌 바닥 꾸밈");
                TestContext.Progress.WriteLine($"HOUSE_BUILD_001 {map.Entry.Id}: 왼쪽 위 방 {room.Count}칸, 꾸밈 {carpet.Id}");
                Assert.That(session.HouseBuildOpen, Is.True, "짓는 집에 들어서면 꾸밀 수 있다(Host가 프레임을 붙였다)");
                player.WalkTo(middle);
                Assert.That(player.PlayerCell, Is.EqualTo(middle), "그 방 가운데에 걸어서 닿았다");
                int before = world.ComposeCount;
                Assert.That(before, Is.EqualTo(1), "들어선 뒤 걸어 다녀도 조합은 한 번");
                Shot("before");

                ApplyAsSetup(session, carpet.Id, middle); // 준비 단계(실제 플레이 아님)
                player.Frames(1);
                Assert.That(world.ComposeCount, Is.EqualTo(before + 1), "꾸밈 알림 다음 그리기에서 한 번 다시 조합");
                TestContext.Progress.WriteLine($"HOUSE_BUILD_001 꾸밈 뒤 다시 조합(조합 + SetData) {world.LastComposeMilliseconds:0.00}ms, 텍스처 {world.ComposedTextureBytes}바이트");
                player.Frames(30);
                Assert.That(world.ComposeCount, Is.EqualTo(before + 1), "그 뒤로는 다시 조합하지 않는다");
                AssertDrawnWith(player, room, carpet, "적용 뒤");
                Shot("after-room-floor");

                // 띠 양옆으로 두 바닥이 비치는 세로 벽(그 방과 옆방 사이), 그 방의 문간.
                var inRoom = new HashSet<CellPos>(room);
                RoomFill fill = RoomFill.Build(map.Tiles.Grid);
                CellTiles between = map.Tiles.Cells.FirstOrDefault(c => c.Parts.Any(p => p.Source == TileSources.Band)
                    && c.Parts.Where(p => p.Source == TileSources.Floor).Select(p => inRoom.Contains(new CellPos(c.Cell.X + p.Owner.X, c.Cell.Y + p.Owner.Y))).Distinct().Count() == 2);
                if (between != null)
                {
                    CellPos stand = new[] { new CellPos(between.Cell.X - 1, between.Cell.Y), new CellPos(between.Cell.X + 1, between.Cell.Y) }
                        .First(c => inRoom.Contains(c) && !fill.IsDoorway(c));
                    player.WalkTo(stand);
                    Shot("band-two-floors");
                }
                CellPos doorway = room.FirstOrDefault(fill.IsDoorway);
                if (room.Any(fill.IsDoorway))
                {
                    player.WalkTo(doorway);
                    Shot("doorway");
                }
                Assert.That(world.ComposeCount, Is.EqualTo(before + 1));
                Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
                Assert.That(player.CollisionViolations, Is.EqualTo(0));
            }

            // 끄고 이어 하기(입력): 저장된 꾸밈으로 같은 그림.
            using (PlayerDriver again = Open())
            {
                Continue(again);
                WorldScreen world = again.Game.World;
                Assert.That(world.Current.Entry.Build, Is.Not.Null, "이어 하기도 집에서");
                again.Frames(3);
                Assert.That(world.ComposeCount, Is.EqualTo(1), "이어 하면 들어선 그리기에서 한 번");
                AssertDrawnWith(again, room, carpet, "이어 하기 뒤");
                again.Screenshot($"house-finish-{shot++:D2}-after-continue");
                Assert.That(again.Game.Renderer.MissingImages, Is.Empty);
            }
        }

        // ---------- 작업 6: 가구 배치 화면의 꾸미기 분류 ----------

        private const string Sprig = "finish.wall.wallpaper-sprig";

        private PlayerDriver Open(string language)
        {
            var args = new List<string> { "--save-root", _saves, "--size", "1920x1080", "--evidence", _evidence };
            if (language != null) { args.Add("--lang"); args.Add(language); }
            HostOptions options = HostOptions.Resolve(args.ToArray());
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static FurniturePlacementScreen Placement(PlayerDriver p) => (FurniturePlacementScreen)p.Screens.Find(ScreenCatalog.FurniturePlacement);

        /// <summary>그 맵 칸의 화면 가운데(논리 좌표). 화면 안이고 UI가 덮지 않으면 true. 어디를 가리킬지 정하는 데만 쓴다.</summary>
        private static bool OnScreen(PlayerDriver p, CellPos cell, out float x, out float y)
        {
            HostPlacementView view = FurniturePlacementHostTests.View(p);
            UiRect r = view.CellRect(p.Game.World.Room.ToPlacement(cell));
            x = r.CenterX; y = r.CenterY;
            return r.X >= 0 && r.Y >= 0 && r.Right <= 1920 && r.Bottom <= 1080 && p.Game.Ui.HitTest(x, y) == null;
        }

        private static (float X, float Y) Visible(PlayerDriver p, IEnumerable<CellPos> cells, string what)
        {
            foreach (CellPos c in cells)
                if (OnScreen(p, c, out float x, out float y)) return (x, y);
            throw new InvalidOperationException("가리킬 칸이 화면에 없다: " + what);
        }

        private static void PickCard(PlayerDriver p, string finishId)
        {
            FurniturePlacementScreen screen = Placement(p);
            int card = Enumerable.Range(0, FurniturePlacementScreen.Metrics.PageSize).First(i => screen.Finishes.At(i)?.Definition.Id == finishId);
            p.PressButton(FurniturePlacementScreen.FinishCardId(card));
            Assert.That(screen.PickedFinish?.Id, Is.EqualTo(finishId));
        }

        /// <summary>준비 단계(실제 플레이 아님): 그 꾸밈으로 그 칸을 바꿀 돈을 가방 명령으로 준다.</summary>
        private static void GiveMoneyFor(GameSession session, string finishId, string tool, CellPos target)
        {
            foreach (var cost in session.PreviewHouseFinish(finishId, tool, target).Cost) session.Inventory.Add(session.Item(cost.Item), cost.Count);
        }

        /// <summary>
        /// HOUSE-001 화면: 새 게임(입력) → 집 → [준비: 돈] → P → 꾸미기 단추 → 바닥 탭 → 카펫 카드 → 왼쪽 위 방 칸을 가리킴(미리 보기) → 누름
        /// (그 업데이트에서 미리 보기를 지우고 다음 그리기에서 집을 한 번 다시 조합) → 벽지 탭·잔무늬 카드 → 북쪽 벽 정면을 누름 → 띠 칸을 가리킨 미리 보기
        /// → 끄고 이어 하기 → 같은 꾸밈. 조작은 키·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인). 돈 주기만 준비 단계.
        /// 스크린샷: output/house-building/stage1b/house-decorate-NN-*.png(직접 연다).
        /// </summary>
        [Test]
        public void HOUSE_BUILD_010_DecorateFromPlacementScreen()
        {
            int shot = 1;
            IReadOnlyList<CellPos> room;
            CellPos wallFace;
            RoomFinishDefinition carpet, sprig;
            using (PlayerDriver player = Open(null))
            {
                string Shot(string name) => player.Screenshot($"house-decorate-{shot++:D2}-{name}");
                NewGame(player);
                WorldScreen world = player.Game.World;
                GameSession session = player.Game.Session;
                LoadedMap map = world.Current;
                CellPos middle;
                (room, middle) = TopLeftRoom(map);
                carpet = world.Content.Definitions.Get<RoomFinishDefinition>(Carpet);
                sprig = world.Content.Definitions.Get<RoomFinishDefinition>(Sprig);
                Assert.That((carpet.UnlockToken, sprig.UnlockToken), Is.EqualTo(("", "")), "전제: 처음부터 열린 꾸밈");
                var inRoom = new HashSet<CellPos>(room);
                RoomFill fill = RoomFill.Build(map.Tiles.Grid);
                // 북쪽 벽 정면: 그 방이 주인인 정면 부분이 있고 주인보다 위에 있는 벽 칸.
                var faces = map.Tiles.Cells.Where(c => map.Tiles.Grid.At(c.Cell.X, c.Cell.Y) == RoomCells.Wall && c.Parts.Any(p => p.HasOwner && p.Source == TileSources.Face
                    && p.Owner.Y > 0 && inRoom.Contains(new CellPos(c.Cell.X + p.Owner.X, c.Cell.Y + p.Owner.Y)))).Select(c => c.Cell).ToList();
                Assert.That(faces, Is.Not.Empty, "전제: 왼쪽 위 방에 북쪽 벽 정면이 있다");
                GiveMoneyFor(session, carpet.Id, HouseFinishTools.Room, middle);   // 준비 단계(실제 플레이 아님)
                GiveMoneyFor(session, sprig.Id, HouseFinishTools.Room, middle);
                int money = session.Inventory.CountOf(session.Item("item.allowance"));

                player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
                FurniturePlacementScreen screen = Placement(player);
                player.PressButton(FurniturePlacementScreen.CategoryFinishId);
                Assert.That(screen.FinishShown, Is.True);
                Assert.That(player.Node(FurniturePlacementScreen.ToolbarId).IsActive || player.Node(FurniturePlacementScreen.BottomBarId).IsActive, Is.False,
                    "꾸미기에서는 가구 단추가 숨는다");
                player.PressButton(FurniturePlacementScreen.FinishTabFloorId);
                PickCard(player, carpet.Id);
                Shot("palette-floor");

                (float x, float y) = Visible(player, new[] { middle }.Concat(room.Where(c => !fill.IsDoorway(c))), "왼쪽 위 방");
                player.MovePointer(x, y);
                player.Frames(1);
                HouseFinishPreview preview = screen.FinishPreview;
                Assert.That(preview?.CanApply, Is.True, preview?.ReasonKey);
                Assert.That(preview.ChangedCells.Count, Is.EqualTo(room.Count), "방 채우기: 방 + 맞닿은 문간");
                int composes = world.ComposeCount, previews = world.PreviewComposeCount;
                Assert.That(previews, Is.GreaterThanOrEqualTo(1), "가리키면 미리 보기를 조합한다");
                Assert.That(world.PreviewTextureBytes, Is.GreaterThan(0));
                int queries = screen.FinishPreviewQueries;
                player.Frames(20);
                Assert.That((screen.FinishPreviewQueries, world.PreviewComposeCount), Is.EqualTo((queries, previews)), "가만히 있으면 묻지도 조합하지도 않는다");
                Shot("preview-floor");

                player.SetPointerDown(true);
                player.Frames(1);
                Assert.That(screen.FinishLastAction, Is.EqualTo("applied"));
                Assert.That(world.PreviewTextureBytes, Is.EqualTo(0), "누른 그 업데이트에서 미리 보기를 지웠다(첫 그리기에 옛 미리 보기가 없다)");
                Assert.That(world.PreviewComposeCount, Is.EqualTo(previews), "미리 보기를 다시 조합하지 않는다");
                Assert.That(world.ComposeCount, Is.EqualTo(composes + 1), "집을 한 번 다시 조합");
                player.SetPointerDown(false);
                player.Frames(10);
                Assert.That(world.ComposeCount, Is.EqualTo(composes + 1));
                Assert.That(room.All(c => session.HouseFloorAt(c) == carpet.Id), Is.True);
                Assert.That(screen.FinishPreview.ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
                AssertDrawnWith(player, room, carpet, "화면으로 적용한 뒤");
                Shot("after-floor");

                player.PressButton(FurniturePlacementScreen.FinishTabWallId);
                PickCard(player, sprig.Id);
                wallFace = faces.First(c => OnScreen(player, c, out _, out _));
                OnScreen(player, wallFace, out x, out y);
                player.MovePointer(x, y);
                player.Frames(1);
                Assert.That(screen.FinishPreview?.CanApply, Is.True, screen.FinishPreview?.ReasonKey);
                Assert.That(inRoom.Contains(screen.FinishPreview.Target), Is.True, "벽 정면을 가리키면 그 정면 주인 방");
                Shot("preview-wall");
                player.PressPointer();
                player.ReleasePointer();
                Assert.That(screen.FinishLastAction, Is.EqualTo("applied"));
                Assert.That(session.HouseWallAt(screen.FinishPreview.Target), Is.EqualTo(sprig.Id));
                Assert.That(session.Inventory.CountOf(session.Item("item.allowance")), Is.LessThan(money));
                Shot("after-wall");

                // 넘긴 것 4: 두 방 사이 세로 벽의 띠 칸을 가리키면(Core: 정면·좁은 부분의 주인 방이 대상) 어느 방이 대상인지 — 고치지 않고 보인다.
                // 띠 칸 중 정면·좁은 부분이 있는 것(Core가 대상을 잡는 칸)을 먼저, 없으면 띠만 있는 칸.
                var bandCells = map.Tiles.Cells.Where(c => c.Parts.Any(p => p.Source == TileSources.Band)
                    && c.Parts.Where(p => p.Source == TileSources.Floor).Select(p => inRoom.Contains(new CellPos(c.Cell.X + p.Owner.X, c.Cell.Y + p.Owner.Y))).Distinct().Count() == 2
                    && OnScreen(player, c.Cell, out _, out _)).ToList();
                foreach (CellTiles c in bandCells)
                    TestContext.Progress.WriteLine($"HOUSE_BUILD_010 띠 칸 후보 {c.Cell}: " + string.Join(", ", c.Parts.Select(p => p.Source + (p.HasOwner ? "@" + p.Owner : ""))));
                CellTiles between = bandCells.FirstOrDefault(c => c.Parts.Any(p => p.HasOwner && (p.Source == TileSources.Face || p.Source == TileSources.Narrow)))
                    ?? map.Tiles.Cells.FirstOrDefault(c => c.Parts.Any(p => p.Source == TileSources.Band) && c.Parts.Any(p => p.HasOwner && p.Source == TileSources.Narrow)
                        && OnScreen(player, c.Cell, out _, out _))
                    ?? bandCells.FirstOrDefault();
                Assert.That(between, Is.Not.Null, "전제: 화면에 보이는 두 방 사이 띠 칸이 있다");
                if (between != null)
                {
                    player.PressButton(FurniturePlacementScreen.FinishTabFloorId);
                    RoomFinishDefinition checker = world.Content.Definitions.All<RoomFinishDefinition>()
                        .First(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length == 0 && f.Id != carpet.Id && f.Id != map.Entry.Build.DefaultFloor);
                    PickCard(player, checker.Id);
                    GiveMoneyFor(session, checker.Id, HouseFinishTools.Room, between.Cell); // 준비 단계 — 미리 보기가 월드에 그려지게
                    OnScreen(player, between.Cell, out float bx, out float by);
                    player.MovePointer(bx, by);
                    player.Frames(1);
                    HouseFinishPreview band = screen.FinishPreview;
                    TestContext.Progress.WriteLine($"HOUSE_BUILD_010 띠 칸 {between.Cell}: 대상 {(band.HasTarget ? band.Target.ToString() : "없음")} "
                        + $"({(band.HasTarget && inRoom.Contains(band.Target) ? "왼쪽 위 방" : "다른 방")}), 이유 {band.ReasonKey ?? "없음"}, 바뀌는 칸 {band.ChangedCells.Count}");
                    Shot("band-cell-preview");
                    UiRect palette = player.Node(FurniturePlacementScreen.PaletteId).Rect;
                    player.MovePointer(palette.CenterX, palette.Bottom - 8); // UI 위: 미리 보기를 지운다
                    player.Frames(1);
                    Assert.That(world.PreviewTextureBytes, Is.EqualTo(0), "UI 위로 가면 미리 보기를 지운다");
                }
                player.PressButton(FurniturePlacementScreen.CategoryFurnitureId); // 가구 분류로 돌아가면 가구 단추가 다시 보인다
                Assert.That(player.Node(FurniturePlacementScreen.ToolbarId).IsActive && player.Node(FurniturePlacementScreen.BottomBarId).IsActive, Is.True);
                Shot("back-to-furniture");

                Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "카드 그림이 모두 풀린다");
                Assert.That(player.CollisionViolations, Is.EqualTo(0));
            }

            // 끄고 이어 하기(입력): 저장된 꾸밈으로 같은 그림.
            using (PlayerDriver again = Open(null))
            {
                Continue(again);
                again.Frames(3);
                GameSession back = again.Game.Session;
                AssertDrawnWith(again, room, carpet, "이어 하기 뒤", back.HouseWallAt);
                Assert.That(again.Game.Session.HouseWallAt(again.Game.Session.PreviewHouseFinish(sprig.Id, HouseFinishTools.Room, wallFace).Target), Is.EqualTo(sprig.Id),
                    "벽지도 남았다");
                again.Screenshot($"house-decorate-{shot++:D2}-after-continue");
                again.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement); // 배치 카메라의 집 전체 보기 — 벽지까지 한 장에
                again.Screenshot($"house-decorate-{shot++:D2}-after-continue-whole");
                Assert.That(again.Game.Renderer.MissingImages, Is.Empty);
            }
        }

        /// <summary>영어로: 꾸미기 분류의 글자(분류 셋, 탭, 도구, 오른쪽 패널)가 넘치지 않는지 직접 본다(스크린샷). 돈은 주지 않는다 — 이유 줄이 찬다.</summary>
        [Test]
        public void HOUSE_BUILD_011_DecorateInEnglish()
        {
            using PlayerDriver player = Open("en");
            NewGame(player);
            WorldScreen world = player.Game.World;
            var (room, middle) = TopLeftRoom(world.Current);
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            FurniturePlacementScreen screen = Placement(player);
            player.PressButton(FurniturePlacementScreen.CategoryFinishId);
            player.PressButton(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition locked = world.Content.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length > 0);
            PickCard(player, locked.Id);
            (float x, float y) = Visible(player, new[] { middle }.Concat(room), "왼쪽 위 방");
            player.MovePointer(x, y);
            player.Frames(1);
            Assert.That(screen.FinishPreview?.ReasonKey, Is.EqualTo(HouseFinishReasons.Locked));
            Assert.That(world.PreviewTextureBytes, Is.EqualTo(0), "거절된 미리 보기는 월드 그림을 덮지 않는다");
            player.Screenshot("house-decorate-20-english-locked");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void HOUSE_BUILD_002_FirstEntryUnder5ms_AndTextureCounted()
        {
            using PlayerDriver player = Open();
            NewGame(player);
            player.Frames(2);
            WorldScreen world = player.Game.World;
            TestContext.Progress.WriteLine($"HOUSE_BUILD_002 첫 입장 조합(꾸밈 찾기 + 조합 + 텍스처 올리기) {world.LastComposeMilliseconds:0.00}ms, "
                + $"조합 텍스처 {world.ComposedTextureBytes}바이트({world.ComposedTextureBytes / 1048576.0:0.00}MB), 파일 그림 캐시 {world.CachedTextureBytes / 1048576.0:0.0}MB");
            Assert.That(world.ComposeCount, Is.EqualTo(1));
            Assert.That(world.LastComposeMilliseconds, Is.LessThan(50), "느슨한 지킴이(목표 5ms — 시트는 시작할 때 미리 읽었다)");
            Assert.That(world.ComposedTextureBytes, Is.GreaterThan(0));
            Assert.That(world.BudgetedTextureBytes, Is.EqualTo(world.CachedTextureBytes + world.ComposedTextureBytes + world.PreviewTextureBytes),
                "그림 캐시 한도 계산에 조합·미리 보기 텍스처가 든다");
            world.TextureBudgetBytes = world.CachedTextureBytes; // 파일 그림만으로는 한도 안
            Assert.That(world.OverTextureBudget, Is.True, "조합 텍스처까지 세면 한도를 넘는다");
            world.TextureBudgetBytes = WorldScreen.DefaultTextureBudgetBytes;
            Assert.That(world.OverTextureBudget, Is.False);
        }

        [Test]
        public void HOUSE_BUILD_003_Preview_ComposesOnlyWhenItChanges()
        {
            using PlayerDriver player = Open();
            NewGame(player);
            WorldScreen world = player.Game.World;
            GameSession session = player.Game.Session;
            LoadedMap map = world.Current;
            var (room, middle) = TopLeftRoom(map);
            player.WalkTo(middle);
            RoomFinishDefinition carpet = OtherFree(world, RoomFinishSlots.Floor, map.Entry.Build.DefaultFloor);
            int composes = world.ComposeCount;

            // 미리 보기는 화면(뒤 작업)이 내는 것을 흉내 낸다: GameSession 조회를 직접 부른다(준비 단계, 실제 플레이 아님).
            HouseFinishPreview preview = session.PreviewHouseFinish(carpet.Id, HouseFinishTools.Room, middle);
            Assert.That(preview.ChangedCells.Count, Is.EqualTo(room.Count));
            world.ShowFinishPreview(preview);
            player.Frames(2);
            Assert.That(world.PreviewComposeCount, Is.EqualTo(1), "미리 보기가 바뀌면 다음 그리기에서 한 번");
            Assert.That(world.PreviewTextureBytes, Is.GreaterThan(0));
            Assert.That(world.ComposeCount, Is.EqualTo(composes), "집 조합은 그대로");
            player.Screenshot("house-finish-20-preview");

            world.ShowFinishPreview(session.PreviewHouseFinish(carpet.Id, HouseFinishTools.Room, room.First(c => !c.Equals(middle))));
            player.Frames(10);
            Assert.That(world.PreviewComposeCount, Is.EqualTo(1), "같은 방 안에서 가리킨 칸만 움직이면(SameAs) 다시 조합하지 않는다");

            RoomFinishDefinition other = world.Content.Definitions.All<RoomFinishDefinition>()
                .First(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length == 0 && f.Id != carpet.Id && f.Id != map.Entry.Build.DefaultFloor);
            world.ShowFinishPreview(session.PreviewHouseFinish(other.Id, HouseFinishTools.Room, middle));
            player.Frames(2);
            Assert.That(world.PreviewComposeCount, Is.EqualTo(2), "다른 꾸밈이면 다시 조합");

            world.ShowFinishPreview(null);
            player.Frames(2);
            Assert.That(world.PreviewTextureBytes, Is.EqualTo(0), "null이면 미리 보기를 버린다");
            Assert.That(world.PreviewComposeCount, Is.EqualTo(2));
            Assert.That(world.ComposeCount, Is.EqualTo(composes));
            player.Screenshot("house-finish-21-preview-cleared");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }
    }
}
