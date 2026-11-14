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
using Rootborn.Screens.Start;
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
            public string WallAt(CellPos floorCell) => "";
            public string FloorAt(CellPos floorCell) => Floors.TryGetValue(floorCell, out string id) ? id : "";
            public string BandColors => "";
        }

        /// <summary>GPU 조합 텍스처 = 같은 조회(그 방만 다른 바닥)로 엔진 없이 조합한 것(칸마다 견준다). 바뀐 방의 그림자 없는 바닥 부분은 그 꾸밈의 floor 조각 그대로.</summary>
        private void AssertDrawnWith(PlayerDriver player, IReadOnlyList<CellPos> room, RoomFinishDefinition floor, string why)
        {
            WorldScreen world = player.Game.World;
            LoadedMap map = world.Current;
            using var sheets = new ComposedRoomTextures(player.Game.GraphicsDevice, _root);
            var art = new RoomFinishArt(Path.Combine(_root, "data"));
            var lookup = new TableLookup();
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
