using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(엔진 없음): 출처표 + 칸마다의 꾸밈 + 조각 시트 → 칸 그림(RoomTileCompositor), 그리고 짓는 집을 그릴 때 조합이 안 되면
    /// LDtk 층으로 그리는 길(ComposedMapTiles). 조각은 시험이 만든 버퍼다 — 조각마다 다른 값에 칸 안 자리를 넣은 것이라 그림 픽셀이 아니다.
    /// 값 = A 255 | 조각 번호 &lt;&lt; 10 | y &lt;&lt; 5 | x. 그래서 "어느 조각의 어느 자리에서 왔는가"를 픽셀 하나로 읽는다.
    /// </summary>
    public sealed class RoomTileCompositorTests
    {
        private const string Sheet = "test/finishes.png", BandId = "test.band";
        private const int Size = RoomTilePlan.CellSize;
        private static readonly uint[] Band = { 0xFF030201u, 0xFF060504u, 0xFF090807u, 0xFF0C0B0Au }; // o d f l (#010203 …의 PackedValue)

        private string _data;

        [SetUp]
        public void SetUp()
        {
            _data = Path.Combine(Path.GetTempPath(), "rootborn-compose-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_data);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_data, true); } catch (IOException) { }
        }

        /// <summary>두 방(왼쪽·오른쪽) 사이 세로 벽, 위 벽은 정면 2칸 + 윗면, 아래 벽에 문 하나(문지방).</summary>
        private static readonly string[] TwoRooms =
        {
            "#########",
            "#########",
            "#########",
            "#...#...#",
            "#...#...#",
            "#...#...#",
            "#...#...#",
            "#########",
        };
        private static readonly CellPos BottomDoor = new CellPos(2, 7);

        private static RoomTilePlan Plan(string[] rows, params CellPos[] doors)
        {
            var cells = new List<int>();
            foreach (string row in rows)
                foreach (char c in row) cells.Add(c == '#' ? RoomCells.Wall : c == '.' ? RoomCells.Floor : RoomCells.Out);
            return RoomTilePlan.Build(new RoomGrid(rows[0].Length, rows.Length, cells), 2, 16, doors);
        }

        private static RoomFinishDefinition Finish(string id, string slot, string art) => new RoomFinishDefinition(id, slot, id + ".name", Sheet, art, null, "");

        private static readonly RoomFinishDefinition WallA = Finish("finish.wall.a", RoomFinishSlots.Wall, "wa"), WallB = Finish("finish.wall.b", RoomFinishSlots.Wall, "wb");
        private static readonly RoomFinishDefinition FloorA = Finish("finish.floor.a", RoomFinishSlots.Floor, "fa"), FloorB = Finish("finish.floor.b", RoomFinishSlots.Floor, "fb");

        /// <summary>조각 시트(메모리)와 좌표표(임시 data 폴더): 출처표가 부르는 조각 전부를 넣되 skip은 뺀다.</summary>
        private sealed class TestSheet
        {
            public readonly Dictionary<string, int> Index = new Dictionary<string, int>(StringComparer.Ordinal);
            public PieceImage Image;

            public uint Value(string name, int x, int y) => 0xFF000000u | ((uint)(Index[name] + 1) << 10) | ((uint)y << 5) | (uint)x;
        }

        private TestSheet MakeSheet(RoomTilePlan plan, IEnumerable<RoomFinishDefinition> walls, IEnumerable<RoomFinishDefinition> floors, string skip = null, string atlasExtra = null, string bandsExtra = null)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (CellTiles c in plan.Cells)
                foreach (TilePart p in c.Parts)
                {
                    if (p.Source == TileSources.Face || p.Source == TileSources.Narrow) foreach (var w in walls) names.Add(w.Art + "/" + p.Piece);
                    if (p.Source == TileSources.Sill || p.Source == TileSources.Door) names.Add(RoomFinishArt.Common + "/" + p.Piece);
                }
            foreach (var f in floors) { names.Add(f.Art + "/floor"); names.Add(f.Art + "/floor:shade"); }
            names.Remove(skip ?? "");
            var sheet = new TestSheet();
            foreach (string n in names) sheet.Index[n] = sheet.Index.Count;
            const int columns = 16;
            int rows = (names.Count + columns - 1) / columns;
            var pixels = new uint[columns * Size * rows * Size];
            var json = new StringBuilder("{ \"image\": \"" + Sheet + "\", \"origin\": \"top-left\", \"band\": 16, \"sprites\": {");
            foreach (var (name, i) in sheet.Index)
            {
                int ox = i % columns * Size, oy = i / columns * Size;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++) pixels[(oy + y) * columns * Size + ox + x] = sheet.Value(name, x, y);
                json.Append(i == 0 ? "" : ",").Append($" \"{name}\": {{ \"x\": {ox}, \"y\": {oy}, \"w\": {Size}, \"h\": {Size} }}");
            }
            json.Append(atlasExtra ?? "");
            json.Append(" }, \"bands\": { \"" + BandId + "\": { \"o\": \"#010203\", \"d\": \"#040506\", \"f\": \"#070809\", \"l\": \"#0A0B0C\" }" + (bandsExtra ?? "") + " } }");
            string atlas = Path.Combine(_data, "atlases", Sheet + ".atlas.json");
            Directory.CreateDirectory(Path.GetDirectoryName(atlas));
            File.WriteAllText(atlas, json.ToString());
            sheet.Image = new PieceImage(columns * Size, rows * Size, pixels);
            return sheet;
        }

        private RoomTileCompositor Compositor(TestSheet sheet) => new RoomTileCompositor(s => s == Sheet ? sheet.Image : null, new RoomFinishArt(_data));

        private static uint At(ComposedRoom room, CellPos cell, int x, int y)
        {
            var (_, sx, sy) = room.Cells.Single(c => c.Cell.Equals(cell));
            return room.Pixels[(sy + y) * room.Width + sx + x];
        }

        private static (int Lo, int Hi)? ShadeRange(string ground)
        {
            if (ground == "floor") return null;
            if (ground == "floor:shade") return (0, Size);
            string[] r = ground.Substring("floor:shade:".Length).Split('-');
            return (int.Parse(r[0]), int.Parse(r[1]));
        }

        [Test]
        public void EveryPixel_ComesFromItsSource_BandFaceOwnersFloorShadeAndSill()
        {
            RoomTilePlan plan = Plan(TwoRooms, BottomDoor);
            RoomFinishDefinition WallAt(CellPos c) => c.X < 4 ? WallA : WallB;
            RoomFinishDefinition FloorAt(CellPos c) => c.X < 4 ? FloorA : FloorB;
            TestSheet sheet = MakeSheet(plan, new[] { WallA, WallB }, new[] { FloorA, FloorB });
            ComposedRoom room = Compositor(sheet).Compose(plan, WallAt, FloorAt, Sheet, BandId);
            Assert.That(room.Problems, Is.Empty, string.Join("\n", room.Problems));
            Assert.That(room.TooLarge, Is.False);
            Assert.That(room.Cells.Select(c => c.Cell), Is.EqualTo(plan.Cells.Select(c => c.Cell)), "밖이 아닌 칸마다 하나, 출처표 순서");
            Assert.That(room.Width, Is.EqualTo(ComposedRoom.AtlasWidth));

            var seen = new HashSet<string>();
            int shaded = 0, twoOwnerBands = 0;
            foreach (CellTiles cell in plan.Cells)
            {
                var covered = new bool[Size * Size];
                if (cell.Parts.Where(p => p.Source == TileSources.Floor).Select(p => p.Owner.X).Distinct().Count() == 2) twoOwnerBands++;
                foreach (TilePart part in cell.Parts)
                {
                    seen.Add(part.Source);
                    CellPos owner = new CellPos(cell.Cell.X + part.Owner.X, cell.Cell.Y + part.Owner.Y);
                    foreach (PixelRect r in part.Rects)
                        for (int y = r.Y0; y < r.Y1; y++)
                            for (int x = r.X0; x < r.X1; x++)
                            {
                                covered[y * Size + x] = true;
                                uint want;
                                if (part.Source == TileSources.Band) want = Band["odfl".IndexOf(part.Shade[0])];
                                else if (part.Source == TileSources.Face || part.Source == TileSources.Narrow) want = sheet.Value(WallAt(owner).Art + "/" + part.Piece, x, y);
                                else if (part.Source == TileSources.Floor)
                                {
                                    var range = ShadeRange(part.Shade);
                                    bool shade = range.HasValue && y < 2 && x >= range.Value.Lo && x < range.Value.Hi;
                                    if (shade) shaded++;
                                    want = sheet.Value(FloorAt(owner).Art + (shade ? "/floor:shade" : "/floor"), x, y);
                                }
                                else want = sheet.Value(RoomFinishArt.Common + "/" + part.Piece, x, y);
                                Assert.That(At(room, cell.Cell, x, y), Is.EqualTo(want), $"{cell.Cell} {part.Source} {part.Shade ?? part.Piece} 주인 {part.Owner} ({x}, {y})");
                            }
                }
                for (int i = 0; i < covered.Length; i++)
                    if (!covered[i]) Assert.That(At(room, cell.Cell, i % Size, i / Size) >> 24, Is.EqualTo(0u), cell.Cell + " 어느 부분에도 없는 픽셀은 투명");
            }
            Assert.That(seen, Is.SupersetOf(new[] { TileSources.Band, TileSources.Face, TileSources.Floor, TileSources.Sill }), "이 평면이 보는 출처 종류");
            Assert.That(shaded, Is.GreaterThan(0), "정면 아래 바닥 그림자(위 두 줄)를 본다");
            Assert.That(twoOwnerBands, Is.GreaterThan(0), "두 방 사이 세로 벽의 띠 칸(바닥 주인 둘)을 본다");
        }

        [Test]
        public void VerticalWallBand_LeftAndRightShowTheirOwnRoomsFloor()
        {
            // 두 방 사이 세로 벽(x = 4)의 띠 칸: 띠 왼쪽 픽셀은 왼쪽 방 바닥 조각, 오른쪽 픽셀은 오른쪽 방 바닥 조각에서 온다(출처로 확인).
            RoomTilePlan plan = Plan(TwoRooms);
            TestSheet sheet = MakeSheet(plan, new[] { WallA }, new[] { FloorA, FloorB });
            ComposedRoom room = Compositor(sheet).Compose(plan, _ => WallA, c => c.X < 4 ? FloorA : FloorB, Sheet, BandId);
            Assert.That(room.Problems, Is.Empty, string.Join("\n", room.Problems));
            var cell = new CellPos(4, 5);
            CellTiles tiles = plan.At(cell.X, cell.Y);
            var band = new HashSet<(int, int)>(tiles.Parts.Where(p => p.Source == TileSources.Band).SelectMany(p => p.Rects)
                .SelectMany(r => Enumerable.Range(r.X0, r.X1 - r.X0).SelectMany(x => Enumerable.Range(r.Y0, r.Y1 - r.Y0).Select(y => (x, y)))));
            int bandLeft = band.Min(p => p.Item1), bandRight = band.Max(p => p.Item1);
            Assert.That(bandLeft > 0 && bandRight < Size - 1, Is.True, "띠가 가운데에 있어 양옆으로 바닥이 비친다");
            int left = 0, right = 0;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (band.Contains((x, y))) continue;
                    uint got = At(room, cell, x, y);
                    if (x < bandLeft) { Assert.That(got, Is.EqualTo(sheet.Value("fa/floor", x, y)), $"왼쪽 ({x}, {y})"); left++; }
                    else { Assert.That(got, Is.EqualTo(sheet.Value("fb/floor", x, y)), $"오른쪽 ({x}, {y})"); right++; }
                }
            Assert.That(left > 0 && right > 0, Is.True, "띠 양옆 모두 본다");
        }

        [Test]
        public void SameCombination_IsComposedOnce()
        {
            RoomTilePlan plan = Plan(TwoRooms, BottomDoor);
            TestSheet sheet = MakeSheet(plan, new[] { WallA, WallB }, new[] { FloorA, FloorB });
            ComposedRoom room = Compositor(sheet).Compose(plan, c => c.X < 4 ? WallA : WallB, c => c.X < 4 ? FloorA : FloorB, Sheet, BandId);
            // 칸의 부분 목록(출처·값·픽셀)과 그 부분들이 쓰는 꾸밈이 같으면 같은 그림 — 한 칸만 만들고 같은 자리를 가리킨다.
            string Key(CellTiles c) => string.Join(";", c.Parts.Select(p =>
            {
                var o = new CellPos(c.Cell.X + p.Owner.X, c.Cell.Y + p.Owner.Y);
                string finish = !p.HasOwner ? "" : p.Source == TileSources.Floor ? (o.X < 4 ? FloorA.Id : FloorB.Id) : (o.X < 4 ? WallA.Id : WallB.Id);
                return p.Source + "|" + p.Shade + "|" + p.Piece + "|" + finish + "|" + p.Pixels;
            }));
            var keys = plan.Cells.Select(Key).ToList();
            Assert.That(room.UniqueTiles, Is.EqualTo(keys.Distinct().Count()));
            Assert.That(room.UniqueTiles, Is.LessThan(plan.Cells.Count));
            for (int i = 0; i < keys.Count; i++)
                for (int j = i + 1; j < keys.Count; j++)
                    Assert.That((room.Cells[i].SrcX, room.Cells[i].SrcY) == (room.Cells[j].SrcX, room.Cells[j].SrcY), Is.EqualTo(keys[i] == keys[j]),
                        plan.Cells[i].Cell + " / " + plan.Cells[j].Cell);
            Assert.That(room.Height, Is.EqualTo((room.UniqueTiles + ComposedRoom.AtlasWidth / Size - 1) / (ComposedRoom.AtlasWidth / Size) * Size));
        }

        [Test]
        public void MissingPiece_IsTransparent_AndAProblem()
        {
            RoomTilePlan plan = Plan(TwoRooms);
            TestSheet sheet = MakeSheet(plan, new[] { WallA }, new[] { FloorA }, skip: "fa/floor:shade");
            ComposedRoom room = Compositor(sheet).Compose(plan, _ => WallA, _ => FloorA, Sheet, BandId);
            Assert.That(room.Problems, Has.Some.Contains("fa/floor:shade").Or.Some.Contains("floor:shade"));
            Assert.That(room.Problems.Count, Is.LessThanOrEqualTo(RoomTileCompositor.MaxProblems), "문제는 같은 것을 한 번만, 개수에 끝이 있다");
            // 그림자 자리는 투명, 나머지 바닥은 그대로.
            CellTiles shaded = plan.Cells.First(c => c.Parts.Any(p => p.Source == TileSources.Floor && p.Shade == "floor:shade"));
            Assert.That(At(room, shaded.Cell, 3, 0) >> 24, Is.EqualTo(0u), "없는 조각의 픽셀은 투명");
            Assert.That(At(room, shaded.Cell, 3, 5), Is.EqualTo(sheet.Value("fa/floor", 3, 5)));
        }

        [Test]
        public void BrokenAtlasOrMissingSheetOrBand_IsAProblem_NotAnException()
        {
            RoomTilePlan plan = Plan(TwoRooms);
            // 좌표표가 깨졌다(조각이 32x32가 아님).
            TestSheet sheet = MakeSheet(plan, new[] { WallA }, new[] { FloorA }, atlasExtra: ", \"bad/x\": { \"x\": 0, \"y\": 0, \"w\": 16, \"h\": 16 }");
            ComposedRoom broken = Compositor(sheet).Compose(plan, _ => WallA, _ => FloorA, Sheet, BandId);
            Assert.That(broken.Problems, Has.Some.Contains("좌표표"));
            // 시트 그림이 없다.
            sheet = MakeSheet(plan, new[] { WallA }, new[] { FloorA });
            ComposedRoom noImage = new RoomTileCompositor(_ => null, new RoomFinishArt(_data)).Compose(plan, _ => WallA, _ => FloorA, Sheet, BandId);
            Assert.That(noImage.Problems, Has.Some.Contains(Sheet));
            // 띠 색 줄이 없다, 꾸밈이 없다.
            ComposedRoom noBand = Compositor(sheet).Compose(plan, _ => WallA, _ => FloorA, Sheet, "no.such.band");
            Assert.That(noBand.Problems, Has.Some.Contains("no.such.band"));
            ComposedRoom noFinish = Compositor(sheet).Compose(plan, _ => null, _ => FloorA, Sheet, BandId);
            Assert.That(noFinish.Problems, Is.Not.Empty);
            ComposedRoom wrongSlot = Compositor(sheet).Compose(plan, _ => FloorA, _ => FloorA, Sheet, BandId);
            Assert.That(wrongSlot.Problems, Has.Some.Contains(FloorA.Id));
        }

        [Test]
        public void MoreCellsThanTheLimit_IsTooLarge_WithNoPixels()
        {
            int side = 65; // 65 x 64 = 4,160칸 > 4,096
            var rows = Enumerable.Range(0, 64).Select(_ => new string('.', side)).ToArray();
            RoomTilePlan plan = Plan(rows);
            Assert.That(plan.Cells.Count, Is.GreaterThan(ComposedRoom.MaxCells));
            TestSheet sheet = MakeSheet(Plan(TwoRooms), new[] { WallA }, new[] { FloorA });
            ComposedRoom room = Compositor(sheet).Compose(plan, _ => WallA, _ => FloorA, Sheet, BandId);
            Assert.That(room.TooLarge, Is.True);
            Assert.That(room.Pixels, Is.Empty);
            Assert.That(room.Cells, Is.Empty);
            Assert.That(room.Problems, Has.Some.Contains(ComposedRoom.MaxCells.ToString()));

            // 64 x 64 = 4,096칸은 된다(상한 포함).
            Assert.That(Compositor(sheet).Compose(Plan(Enumerable.Range(0, 64).Select(_ => new string('.', 64)).ToArray()), _ => WallA, _ => FloorA, Sheet, BandId).TooLarge, Is.False);
        }

        // ---------- 칸마다 꾸밈이 다를 때: 벽지·바닥의 주인은 그 픽셀이 바라보는 바닥 칸 ----------

        /// <summary>
        /// 방마다 다른 꾸밈: 바닥 칸(상하좌우로 이어진 바닥 = 한 방)에서만 그 방의 꾸밈을 돌려준다. 벽 칸·밖 칸으로 부르면 null을 돌려주고 기록한다 —
        /// 조합기가 주인 칸(이 칸 + Owner) 대신 이 칸으로 부르면 드러난다(주인은 늘 바닥 칸이다).
        /// </summary>
        private sealed class RoomFinishes
        {
            public readonly Dictionary<CellPos, int> Room = new Dictionary<CellPos, int>();
            public readonly List<RoomFinishDefinition> Walls = new List<RoomFinishDefinition>(), Floors = new List<RoomFinishDefinition>();
            public readonly List<string> Misuse = new List<string>();

            public RoomFinishes(RoomGrid grid)
            {
                for (int y = 0; y < grid.Rows; y++)
                    for (int x = 0; x < grid.Columns; x++)
                    {
                        if (grid.At(x, y) != RoomCells.Floor || Room.ContainsKey(new CellPos(x, y))) continue;
                        int r = Walls.Count;
                        Walls.Add(Finish("finish.wall.r" + r, RoomFinishSlots.Wall, "w" + r));
                        Floors.Add(Finish("finish.floor.r" + r, RoomFinishSlots.Floor, "f" + r));
                        var stack = new Stack<CellPos>();
                        stack.Push(new CellPos(x, y));
                        Room[new CellPos(x, y)] = r;
                        while (stack.Count > 0)
                        {
                            CellPos c = stack.Pop();
                            foreach (var n in new[] { new CellPos(c.X + 1, c.Y), new CellPos(c.X - 1, c.Y), new CellPos(c.X, c.Y + 1), new CellPos(c.X, c.Y - 1) })
                                if (n.X >= 0 && n.Y >= 0 && n.X < grid.Columns && n.Y < grid.Rows && grid.At(n.X, n.Y) == RoomCells.Floor && !Room.ContainsKey(n))
                                {
                                    Room[n] = r;
                                    stack.Push(n);
                                }
                        }
                    }
            }

            public RoomFinishDefinition Wall(CellPos c)
            {
                if (Room.TryGetValue(c, out int r)) return Walls[r];
                Misuse.Add("벽 꾸밈을 바닥이 아닌 칸 " + c + "으로 불렀다");
                return null;
            }

            public RoomFinishDefinition Floor(CellPos c)
            {
                if (Room.TryGetValue(c, out int r)) return Floors[r];
                Misuse.Add("바닥 꾸밈을 바닥이 아닌 칸 " + c + "으로 불렀다");
                return null;
            }
        }

        /// <summary>칸마다 부분마다 그 픽셀이 주인(ownerOf가 주는 칸)의 꾸밈 조각 같은 자리에서 왔는지 본다. 다른 픽셀을 모아 돌려준다(처음 몇 개).</summary>
        private static List<string> WrongPixels(RoomTilePlan plan, ComposedRoom room, TestSheet sheet, RoomFinishes f, Func<CellTiles, TilePart, CellPos> ownerOf)
        {
            var wrong = new List<string>();
            foreach (CellTiles cell in plan.Cells)
                foreach (TilePart part in cell.Parts)
                {
                    CellPos owner = ownerOf(cell, part);
                    foreach (PixelRect r in part.Rects)
                        for (int y = r.Y0; y < r.Y1; y++)
                            for (int x = r.X0; x < r.X1; x++)
                            {
                                uint want;
                                if (part.Source == TileSources.Band) want = Band["odfl".IndexOf(part.Shade[0])];
                                else if (part.Source == TileSources.Face || part.Source == TileSources.Narrow) want = sheet.Value(f.Walls[f.Room[owner]].Art + "/" + part.Piece, x, y);
                                else if (part.Source == TileSources.Floor)
                                {
                                    var range = ShadeRange(part.Shade);
                                    bool shade = range.HasValue && y < 2 && x >= range.Value.Lo && x < range.Value.Hi;
                                    want = sheet.Value(f.Floors[f.Room[owner]].Art + (shade ? "/floor:shade" : "/floor"), x, y);
                                }
                                else want = sheet.Value(RoomFinishArt.Common + "/" + part.Piece, x, y);
                                uint got = At(room, cell.Cell, x, y);
                                if (got != want && wrong.Count < 12) wrong.Add($"{cell.Cell} {part.Source} {part.Shade ?? part.Piece} 주인 {owner} ({x}, {y})");
                            }
                }
            return wrong;
        }

        private static CellPos CoreOwner(CellTiles cell, TilePart part) => new CellPos(cell.Cell.X + part.Owner.X, cell.Cell.Y + part.Owner.Y);

        private static RoomGrid GridOf(string[] rows)
        {
            int columns = rows.Max(r => r.Length);
            var cells = new int[columns * rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    cells[y * columns + x] = rows[y][x] == '#' ? RoomCells.Wall : rows[y][x] == '.' ? RoomCells.Floor : RoomCells.Out;
            return new RoomGrid(columns, rows.Length, cells);
        }

        /// <summary>가로 벽 하나(윗면 + 정면 2칸)를 사이에 둔 북쪽 방과 남쪽 방.</summary>
        private static readonly string[] NorthSouth =
        {
            "#######",
            "#######",
            "#######",
            "#.....#",
            "#.....#",
            "#######",
            "#######",
            "#######",
            "#.....#",
            "#.....#",
            "#######",
        };

        [Test]
        public void HorizontalWallBetweenTwoRooms_ItsFaceIsTheSouthRoomsWallpaper()
        {
            RoomGrid grid = GridOf(NorthSouth);
            RoomTilePlan plan = RoomTilePlan.Build(grid, 2, 16, null);
            var f = new RoomFinishes(grid);
            Assert.That(f.Walls.Count, Is.EqualTo(2), "전제: 방 둘");
            int north = f.Room[new CellPos(1, 3)], south = f.Room[new CellPos(1, 8)];
            TestSheet sheet = MakeSheet(plan, f.Walls, f.Floors);
            ComposedRoom room = Compositor(sheet).Compose(plan, f.Wall, f.Floor, Sheet, BandId);
            Assert.That(f.Misuse, Is.Empty, "꾸밈은 주인 바닥 칸으로만 부른다:\n" + string.Join("\n", f.Misuse.Distinct().Take(10)));
            Assert.That(room.Problems, Is.Empty, string.Join("\n", room.Problems));

            // 사이 벽(줄 5~7)의 정면은 정면이 바라보는 앞(남쪽) 방의 벽지: 주인이 남쪽 방이고, 픽셀이 남쪽 방 벽지 조각에서 왔으며 북쪽 방 것이 아니다.
            int faces = 0;
            foreach (CellTiles cell in plan.Cells.Where(c => c.Cell.Y >= 5 && c.Cell.Y <= 7))
                foreach (TilePart part in cell.Parts.Where(p => p.Source == TileSources.Face))
                {
                    CellPos owner = CoreOwner(cell, part);
                    Assert.That(f.Room.TryGetValue(owner, out int r) ? r : -1, Is.EqualTo(south), cell.Cell + " 정면의 주인 " + owner);
                    PixelRect px = part.Rects[0];
                    uint got = At(room, cell.Cell, px.X0, px.Y0);
                    Assert.That(got, Is.EqualTo(sheet.Value(f.Walls[south].Art + "/" + part.Piece, px.X0, px.Y0)), cell.Cell + " 남쪽 방 벽지");
                    Assert.That(got, Is.Not.EqualTo(sheet.Value(f.Walls[north].Art + "/" + part.Piece, px.X0, px.Y0)), cell.Cell + " 북쪽 방 벽지가 아니다");
                    faces++;
                }
            Assert.That(faces, Is.GreaterThan(0), "사이 벽의 정면을 본다");
            Assert.That(WrongPixels(plan, room, sheet, f, CoreOwner), Is.Empty, "모든 픽셀이 주인의 꾸밈에서");
        }

        /// <summary>픽셀 키트 시험 표의 사례 하나(격자, 문, 칸마다 부분과 Python이 정한 주인 own).</summary>
        private sealed class VectorCase
        {
            public string Id;
            public string[] Rows;
            public int FaceHeight, Band;
            public List<CellPos> Doors = new List<CellPos>();
            /// <summary>(x, y, src, 값, px) → Python own(없으면 null).</summary>
            public Dictionary<string, CellPos?> Own = new Dictionary<string, CellPos?>(StringComparer.Ordinal);
            public int Parts;
            public override string ToString() => Id;
        }

        private static string PartKey(CellPos cell, string src, string value, string px) => cell.X + "," + cell.Y + "|" + src + "|" + value + "|" + px;

        private static List<VectorCase> VectorCases()
        {
            string path = Path.Combine(Repo.Root, "Tools", "pixel-kit", "tests", "fixtures", "room-vectors.json");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var cases = new List<VectorCase>();
            foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                var v = new VectorCase
                {
                    Id = c.GetProperty("id").GetString(),
                    Rows = c.GetProperty("grid").EnumerateArray().Select(r => r.GetString()).ToArray(),
                    FaceHeight = c.GetProperty("faceHeight").GetInt32(),
                    Band = c.GetProperty("band").GetInt32(),
                };
                foreach (var d in c.GetProperty("doors").EnumerateArray()) v.Doors.Add(new CellPos(d[0].GetInt32(), d[1].GetInt32()));
                foreach (var cell in c.GetProperty("cells").EnumerateArray())
                {
                    var at = new CellPos(cell.GetProperty("x").GetInt32(), cell.GetProperty("y").GetInt32());
                    foreach (var p in cell.GetProperty("parts").EnumerateArray())
                    {
                        string value = p.TryGetProperty("shade", out var s) ? s.GetString() : p.GetProperty("piece").GetString();
                        CellPos? own = p.TryGetProperty("own", out var o) ? new CellPos(o[0].GetInt32(), o[1].GetInt32()) : (CellPos?)null;
                        v.Own[PartKey(at, p.GetProperty("src").GetString(), value, p.GetProperty("px").GetString())] = own;
                        v.Parts++;
                    }
                }
                cases.Add(v);
            }
            return cases;
        }

        [Test]
        public void EveryVectorCase_EachRoomsOwnFinishes_EveryPixelFollowsThePythonOwner()
        {
            // 시험 표의 모든 사례(세로 벽 남쪽 끝의 좁은 정면 tee·wall-end, 두 방 사이 세로 벽, 문지방 …)에 방마다 다른 벽지·바닥을 주고 조합한다.
            // 기대 픽셀은 Python 규칙(시험 표의 own)으로 고른 주인 방의 조각에서 — Core 출처표의 주인이 그와 같다는 것도 본다.
            List<VectorCase> cases = VectorCases();
            Assert.That(cases.Count, Is.GreaterThan(10));
            int narrowParts = 0, threeRoomNarrowCells = 0, twoRoomFaceCells = 0;
            foreach (VectorCase v in cases)
            {
                RoomGrid grid = GridOf(v.Rows);
                RoomTilePlan plan = RoomTilePlan.Build(grid, v.FaceHeight, v.Band, v.Doors);
                var f = new RoomFinishes(grid);
                int parts = 0;
                var pythonOwner = new Dictionary<TilePart, CellPos>();
                foreach (CellTiles cell in plan.Cells)
                    foreach (TilePart part in cell.Parts)
                    {
                        parts++;
                        string key = PartKey(cell.Cell, part.Source, part.Shade ?? part.Piece, part.Pixels);
                        Assert.That(v.Own.TryGetValue(key, out CellPos? own), Is.True, v.Id + " 시험 표에 없는 부분 " + key);
                        Assert.That(part.HasOwner, Is.EqualTo(own.HasValue), v.Id + " " + key);
                        if (own.HasValue)
                        {
                            Assert.That(part.Owner, Is.EqualTo(own.Value), v.Id + " Core 주인 = Python own " + key);
                            pythonOwner[part] = new CellPos(cell.Cell.X + own.Value.X, cell.Cell.Y + own.Value.Y);
                        }
                    }
                Assert.That(parts, Is.EqualTo(v.Parts), v.Id + " 부분 수");

                TestSheet sheet = MakeSheet(plan, f.Walls, f.Floors);
                ComposedRoom room = Compositor(sheet).Compose(plan, f.Wall, f.Floor, Sheet, BandId);
                Assert.That(f.Misuse, Is.Empty, v.Id + ": 꾸밈은 주인 바닥 칸으로만 부른다:\n" + string.Join("\n", f.Misuse.Distinct().Take(10)));
                Assert.That(room.Problems, Is.Empty, v.Id + ":\n" + string.Join("\n", room.Problems));
                List<string> wrong = WrongPixels(plan, room, sheet, f, (cell, part) => part.HasOwner ? pythonOwner[part] : cell.Cell);
                Assert.That(wrong, Is.Empty, v.Id + ": 주인(Python own)의 꾸밈이 아닌 픽셀:\n" + string.Join("\n", wrong));

                foreach (CellTiles cell in plan.Cells)
                {
                    var narrow = cell.Parts.Where(p => p.Source == TileSources.Narrow).ToList();
                    narrowParts += narrow.Count;
                    var sideRooms = cell.Parts.Where(p => p.Source == TileSources.Floor).Select(p => f.Room[pythonOwner[p]]).ToList();
                    if (narrow.Any(n => sideRooms.Count == 2 && sideRooms.Distinct().Count() == 2 && !sideRooms.Contains(f.Room[pythonOwner[n]]))) threeRoomNarrowCells++;
                    if (cell.Parts.Where(p => p.Source == TileSources.Face).Select(p => f.Room[pythonOwner[p]]).Distinct().Count() == 2) twoRoomFaceCells++;
                }
            }
            Assert.That(narrowParts, Is.GreaterThan(0), "좁은 정면을 본다");
            Assert.That(threeRoomNarrowCells, Is.GreaterThan(0), "좁은 정면은 앞 방, 양옆 비침은 왼쪽·오른쪽 방 — 세 방의 꾸밈이 한 칸에 든 칸을 본다(tee)");
            Assert.That(twoRoomFaceCells, Is.GreaterThan(0), "세로 벽이 가로 벽 정면을 지나는 칸: 양옆 늘인 정면이 각각 다른 방의 벽지");
        }

        [Test]
        public void AFinishPartWithoutAnOwner_IsAProblem_NotPaintedWithThisCellsFinish()
        {
            // Core는 꾸밈이 필요한 종류(face·narrow·floor)를 주인 없이 내지 않는다(TilePart가 막는다). 손으로 만든 출처표(리플렉션 — 종류 표가
            // 어긋난 경우를 흉내)에서 정면 부분 하나를 주인 없는 것으로 바꾸면, 조합은 문제를 알리고(조합 실패 → LDtk 층 길) 그 픽셀은 이 칸의 꾸밈으로 칠하지 않는다.
            RoomTilePlan plan = Plan(TwoRooms);
            TestSheet sheet = MakeSheet(plan, new[] { WallA, WallB }, new[] { FloorA, FloorB });
            CellTiles cell = plan.Cells.First(c => c.Parts.Any(p => p.Source == TileSources.Face));
            TilePart face = cell.Parts.First(p => p.Source == TileSources.Face);
            const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            object kind = Activator.CreateInstance(typeof(TileSourceKind), Any, null, new object[] { TileSources.Face, TileSourceKind.PieceField, false }, null);
            var orphan = (TilePart)Activator.CreateInstance(typeof(TilePart), Any, null, new object[] { kind, face.Piece, false, new CellPos(0, 0), face.Rects }, null);
            Assert.That((orphan.Source, orphan.HasOwner), Is.EqualTo((TileSources.Face, false)));
            var parts = cell.Parts.Select(p => ReferenceEquals(p, face) ? orphan : p).ToList();
            typeof(CellTiles).GetField("<Parts>k__BackingField", Any).SetValue(cell, (IReadOnlyList<TilePart>)parts);
            Assert.That(plan.Cells.First(c => c.Cell.Equals(cell.Cell)).Parts, Has.Member(orphan), "전제: 출처표의 그 칸이 바뀌었다");

            ComposedRoom room = Compositor(sheet).Compose(plan, _ => WallA, _ => FloorA, Sheet, BandId);
            Assert.That(room.Problems, Has.Some.Contains("주인"), string.Join("\n", room.Problems));
            PixelRect r = face.Rects[0];
            Assert.That(At(room, cell.Cell, r.X0, r.Y0) >> 24, Is.EqualTo(0u), "주인 없는 꾸밈 부분은 투명(이 칸의 꾸밈으로 칠하지 않는다)");
        }

        // ---------- 짓는 집을 그릴 때: 조합이 안 되면 LDtk 층으로 ----------

        private static (LoadedMap Map, RoomFinishDefinition Wall, RoomFinishDefinition Floor) House()
        {
            WorldMapLoader loader = Repo.Loader();
            MapEntry entry = loader.Catalog.Maps.Single(m => m.Build != null);
            return (loader.LoadMap(entry.Id), Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultWall), Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultFloor));
        }

        private static RoomFinishDefinition Defined(string id) => Repo.Definitions.TryGet(id, out RoomFinishDefinition d) ? d : null;

        /// <summary>저장소 data의 좌표표 사본을 고친 임시 data 폴더(조각 시트 PNG는 저장소 것을 그대로 읽는다).</summary>
        private string CopiedAtlas(RoomFinishDefinition wall, Func<string, string> edit)
        {
            string from = Path.Combine(Repo.Root, "data", "atlases", wall.Sheet + ".atlas.json");
            string to = Path.Combine(_data, "atlases", wall.Sheet + ".atlas.json");
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.WriteAllText(to, edit(File.ReadAllText(from)));
            return _data;
        }

        private static void AssertDrawsTheLdtkLayers(LoadedMap map, ComposedMapTiles tiles, string why)
        {
            Assert.That(tiles.Ok, Is.False, why);
            Assert.That(tiles.Problems, Is.Not.Empty, why + ": 문제를 알린다");
            Assert.That(tiles.Draws, Is.Empty);
            MapPicture full = MapPicture.From(map.Level);
            Assert.That(tiles.FallbackUnder.Count, Is.EqualTo(full.Under.Count), why + ": LDtk의 Wall·Ground 층을 그대로 그린다");
            Assert.That(tiles.FallbackUnder.Select(s => s.Texture).Distinct(), Is.EquivalentTo(full.Under.Select(s => s.Texture).Distinct()));
        }

        [Test]
        public void TheHouseComposes_AndDrawsOneSpritePerCell()
        {
            var (map, wall, floor) = House();
            ComposedMapTiles tiles = ComposedMapTiles.Compose(map, new RoomTileCompositor(HouseMapTests.Sheets(), new RoomFinishArt(Path.Combine(Repo.Root, "data"))), Defined);
            Assert.That(tiles.Ok, Is.True, string.Join("\n", tiles.Problems));
            Assert.That(tiles.FallbackUnder, Is.Null);
            Assert.That(tiles.Draws.Count, Is.EqualTo(map.Tiles.Cells.Count));
            Assert.That(tiles.Draws.All(d => d.Texture == tiles.TextureKey && d.W == Size && d.H == Size), Is.True);
            Assert.That(tiles.TextureKey, Is.EqualTo(ComposedMapTiles.TexturePrefix + map.Entry.Id));
            CellTiles first = map.Tiles.Cells[0];
            Assert.That((tiles.Draws[0].X, tiles.Draws[0].Y), Is.EqualTo((first.Cell.X * (float)Size, first.Cell.Y * (float)Size)));
        }

        [Test]
        public void AMissingPiece_DrawsTheLdtkLayers()
        {
            var (map, wall, _) = House();
            string data = CopiedAtlas(wall, json => json.Replace("\"" + wall.Art + "/face:1\"", "\"" + wall.Art + "/face:1-renamed\""));
            ComposedMapTiles tiles = ComposedMapTiles.Compose(map, new RoomTileCompositor(HouseMapTests.Sheets(), new RoomFinishArt(data)), Defined);
            AssertDrawsTheLdtkLayers(map, tiles, "조각이 없다");
            Assert.That(tiles.Problems, Has.Some.Contains(wall.Art + "/face:1"));
        }

        [Test]
        public void ABrokenAtlas_DrawsTheLdtkLayers()
        {
            var (map, wall, _) = House();
            string data = CopiedAtlas(wall, json => json.Replace("\"top-left\"", "\"bottom-left\""));
            ComposedMapTiles tiles = ComposedMapTiles.Compose(map, new RoomTileCompositor(HouseMapTests.Sheets(), new RoomFinishArt(data)), Defined);
            AssertDrawsTheLdtkLayers(map, tiles, "좌표표가 깨졌다");
        }

        [Test]
        public void AMissingDefaultFinish_DrawsTheLdtkLayers()
        {
            var (map, _, _) = House();
            ComposedMapTiles tiles = ComposedMapTiles.Compose(map, new RoomTileCompositor(HouseMapTests.Sheets(), new RoomFinishArt(Path.Combine(Repo.Root, "data"))), _ => null);
            AssertDrawsTheLdtkLayers(map, tiles, "기본 꾸밈이 정의에 없다");
            Assert.That(tiles.Problems, Has.Some.Contains(map.Entry.Build.DefaultWall));
        }

        // ---------- 칸마다의 꾸밈(IHouseFinishLookup)으로 조합 ----------

        /// <summary>시험용 꾸밈 조회: 칸 → id 표(없으면 빈 글 = 기본), 띠 색 줄 이름.</summary>
        private sealed class TableLookup : IHouseFinishLookup
        {
            public readonly Dictionary<CellPos, string> Walls = new Dictionary<CellPos, string>(), Floors = new Dictionary<CellPos, string>();
            public string Band = "";
            public Func<CellPos, string> AllFloors;
            public string WallAt(CellPos floorCell) => Walls.TryGetValue(floorCell, out string id) ? id : "";
            public string FloorAt(CellPos floorCell) => AllFloors != null ? AllFloors(floorCell) : Floors.TryGetValue(floorCell, out string id) ? id : "";
            public string BandColors => Band;
        }

        private static RoomTileCompositor RepoCompositor() => new RoomTileCompositor(HouseMapTests.Sheets(), new RoomFinishArt(Path.Combine(Repo.Root, "data")));

        /// <summary>집의 왼쪽 위 방(행 순서로 첫 바닥 칸의 방, 문간 포함 — Core 방 채우기).</summary>
        private static IReadOnlyList<CellPos> TopLeftRoom(LoadedMap map)
        {
            RoomGrid grid = map.Tiles.Grid;
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (grid.At(x, y) == RoomCells.Floor) return RoomFill.Build(grid).RoomOf(new CellPos(x, y));
            throw new InvalidOperationException("바닥이 없다");
        }

        /// <summary>처음부터 열린, 그 자리의 기본이 아닌 꾸밈(데이터 순서의 첫 것).</summary>
        private static RoomFinishDefinition OtherFree(string slot, string notThis) =>
            Repo.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == slot && f.UnlockToken.Length == 0 && f.Id != notThis);

        private static uint[] CellPixels(ComposedRoom room, CellPos cell)
        {
            var (_, sx, sy) = room.Cells.Single(c => c.Cell.Equals(cell));
            var px = new uint[Size * Size];
            for (int y = 0; y < Size; y++) Array.Copy(room.Pixels, (sy + y) * room.Width + sx, px, y * Size, Size);
            return px;
        }

        [Test]
        public void Lookup_PerCellFloor_OnlyThatRoomChanges()
        {
            var (map, wall, floor) = House();
            RoomFinishDefinition other = OtherFree(RoomFinishSlots.Floor, floor.Id);
            IReadOnlyList<CellPos> roomCells = TopLeftRoom(map);
            var room = new HashSet<CellPos>(roomCells);
            var lookup = new TableLookup();
            foreach (CellPos c in roomCells) lookup.Floors[c] = other.Id;

            ComposedMapTiles plain = ComposedMapTiles.Compose(map, RepoCompositor(), Defined);
            ComposedMapTiles mixed = ComposedMapTiles.Compose(map, RepoCompositor(), Defined, lookup);
            Assert.That((plain.Ok, mixed.Ok), Is.EqualTo((true, true)), string.Join("\n", mixed.Problems));

            // 바닥 조각 자리(시트의 좌표표): 그 방의 바닥 칸에서 그림자 없는 바닥 부분은 다른 꾸밈의 floor 조각 픽셀 그대로다.
            var art = new RoomFinishArt(Path.Combine(Repo.Root, "data"));
            Assert.That(art.TryPiece(other, "floor", out FinishPiece piece), Is.True);
            PieceImage sheet = HouseMapTests.Sheets()(piece.Texture);
            int changedCells = 0, sameCells = 0, bandSides = 0, fromPiece = 0;
            foreach (CellTiles cell in map.Tiles.Cells)
            {
                bool touches = cell.Parts.Any(p => p.Source == TileSources.Floor && room.Contains(new CellPos(cell.Cell.X + p.Owner.X, cell.Cell.Y + p.Owner.Y)));
                uint[] a = CellPixels(plain.Room, cell.Cell), b = CellPixels(mixed.Room, cell.Cell);
                if (!touches)
                {
                    Assert.That(b, Is.EqualTo(a), cell.Cell + ": 다른 방(그 방 바닥이 비치지 않는 칸)은 기본 조합과 픽셀이 같다");
                    sameCells++;
                    continue;
                }
                if (!a.SequenceEqual(b)) changedCells++;
                if (!room.Contains(cell.Cell)) bandSides++;
                foreach (TilePart part in cell.Parts.Where(p => p.Source == TileSources.Floor && p.Shade == "floor"
                    && room.Contains(new CellPos(cell.Cell.X + p.Owner.X, cell.Cell.Y + p.Owner.Y))))
                    foreach (PixelRect r in part.Rects)
                        for (int y = r.Y0; y < r.Y1; y++)
                            for (int x = r.X0; x < r.X1; x++)
                            {
                                Assert.That(b[y * Size + x], Is.EqualTo(sheet.Pixels[(piece.Y + y) * sheet.Width + piece.X + x]), $"{cell.Cell} ({x}, {y}) 다른 꾸밈의 floor 조각에서");
                                fromPiece++;
                            }
            }
            TestContext.Out.WriteLine($"방 {room.Count}칸: 바뀐 칸 {changedCells}, 방 밖에서 그 방 바닥이 비치는 칸 {bandSides}, 그대로인 칸 {sameCells}");
            Assert.That(changedCells, Is.GreaterThanOrEqualTo(room.Count), "그 방의 칸은 다 바뀐다");
            Assert.That(bandSides, Is.GreaterThan(0), "벽 칸의 띠 옆으로 비치는 그 방 바닥도 바뀐다");
            Assert.That(sameCells, Is.GreaterThan(0));
            Assert.That(fromPiece, Is.GreaterThan(0));
        }

        [Test]
        public void Lookup_UnknownId_DrawsDefault_NoProblem()
        {
            var (map, _, _) = House();
            ComposedMapTiles plain = ComposedMapTiles.Compose(map, RepoCompositor(), Defined);
            var lookup = new TableLookup { AllFloors = _ => "finish.floor.no-such-finish" };
            foreach (CellTiles c in map.Tiles.Cells) lookup.Walls[c.Cell] = c.Cell.X % 2 == 0 ? "" : "finish.wall.no-such-finish";
            ComposedMapTiles unknown = ComposedMapTiles.Compose(map, RepoCompositor(), Defined, lookup);
            Assert.That(unknown.Ok, Is.True);
            Assert.That(unknown.Problems, Is.Empty, "지운 꾸밈은 기본으로 그린다 — 문제 줄 없음");
            Assert.That(unknown.Room.Pixels, Is.EqualTo(plain.Room.Pixels));
            // 다른 자리의 꾸밈(바닥 id를 벽으로)도 기본으로 그린다.
            var wrongSlot = new TableLookup();
            foreach (CellTiles c in map.Tiles.Cells) wrongSlot.Walls[c.Cell] = map.Entry.Build.DefaultFloor;
            ComposedMapTiles wrong = ComposedMapTiles.Compose(map, RepoCompositor(), Defined, wrongSlot);
            Assert.That((wrong.Ok, wrong.Problems.Count), Is.EqualTo((true, 0)));
            Assert.That(wrong.Room.Pixels, Is.EqualTo(plain.Room.Pixels));
            // 조회가 null이면 지금과 같다.
            Assert.That(ComposedMapTiles.Compose(map, RepoCompositor(), Defined, null).Room.Pixels, Is.EqualTo(plain.Room.Pixels));
        }

        [Test]
        public void Compose_BandOtherThanMapDefault_PaintsChosenBand_SillAndDoorUnchanged()
        {
            // 띠 색을 맵 기본이 아닌 띠 꾸밈의 줄로 바꾸면: 띠 부분의 픽셀은 고른 줄의 네 칠(o·d·f·l) 중 그 부분의 칠이고,
            // 문지방(common/sill:*)·문짝(common/door) 픽셀은 기본 띠 조합과 같다(그 조각에는 띠 색이 구워져 있지 않다). 정면·바닥 픽셀도 같다.
            var (map, wall, _) = House();
            RoomFinishDefinition band = Repo.Definitions.All<RoomFinishDefinition>()
                .First(f => f.Slot == RoomFinishSlots.Band && f.Art != map.Entry.Build.BandColors);
            Assert.That(band.Sheet, Is.EqualTo(wall.Sheet), "전제: 띠 꾸밈은 기본 벽 꾸밈과 같은 시트");
            var art = new RoomFinishArt(Path.Combine(Repo.Root, "data"));
            Assert.That(art.TryBand(wall.Sheet, band.Art, out BandColors chosen), Is.True);
            Assert.That(art.TryBand(wall.Sheet, map.Entry.Build.BandColors, out BandColors plainBand), Is.True);
            Assert.That(chosen, Is.Not.EqualTo(plainBand), "전제: 고른 줄은 기본 줄과 칠이 다르다");

            ComposedMapTiles plain = ComposedMapTiles.Compose(map, RepoCompositor(), Defined);
            ComposedMapTiles banded = ComposedMapTiles.Compose(map, RepoCompositor(), Defined, new TableLookup { Band = band.Art });
            Assert.That((plain.Ok, banded.Ok), Is.EqualTo((true, true)), string.Join("\n", banded.Problems));

            uint Paint(BandColors c, char shade) => shade == 'o' ? c.Outline : shade == 'd' ? c.Dark : shade == 'f' ? c.Face : c.Light;
            int bandPixels = 0, changedBand = 0, sillPixels = 0, doorPixels = 0, otherPixels = 0;
            foreach (CellTiles cell in map.Tiles.Cells)
            {
                uint[] a = CellPixels(plain.Room, cell.Cell), b = CellPixels(banded.Room, cell.Cell);
                var bandAt = new bool[Size * Size];
                foreach (TilePart part in cell.Parts)
                    foreach (PixelRect r in part.Rects)
                        for (int y = r.Y0; y < r.Y1; y++)
                            for (int x = r.X0; x < r.X1; x++)
                            {
                                int i = y * Size + x;
                                if (part.Source == TileSources.Band)
                                {
                                    bandAt[i] = true;
                                    Assert.That(b[i], Is.EqualTo(Paint(chosen, part.Shade[0])), $"{cell.Cell} 띠 {part.Shade} ({x}, {y}): 고른 줄의 그 칠");
                                    if (b[i] != a[i]) changedBand++;
                                    bandPixels++;
                                }
                                else if (part.Source == TileSources.Sill || part.Source == TileSources.Door)
                                {
                                    Assert.That(b[i], Is.EqualTo(a[i]), $"{cell.Cell} {part.Source} {part.Piece} ({x}, {y}): 띠 색과 무관");
                                    if (part.Source == TileSources.Sill) sillPixels++; else doorPixels++;
                                }
                            }
                for (int i = 0; i < bandAt.Length; i++)
                    if (!bandAt[i])
                    {
                        Assert.That(b[i], Is.EqualTo(a[i]), $"{cell.Cell} ({i % Size}, {i / Size}): 띠가 아닌 픽셀은 그대로");
                        otherPixels++;
                    }
            }
            TestContext.Out.WriteLine($"띠 {bandPixels}(바뀐 것 {changedBand}), 문지방 {sillPixels}, 문짝 {doorPixels}, 그 밖 {otherPixels}");
            Assert.That(bandPixels, Is.GreaterThan(0));
            Assert.That(changedBand, Is.GreaterThan(0), "띠가 실제로 다른 칠이 됐다");
            Assert.That(sillPixels, Is.GreaterThan(0), "문지방을 본다");
            Assert.That(doorPixels + sillPixels, Is.GreaterThan(0));

            // 지금 집에는 문짝(정면 위의 문)이 없다 — 시험 평면(정면 위 문 + 아래 벽 문지방)으로 문짝도 띠 색과 무관함을 본다.
            RoomTilePlan plan = Plan(TwoRooms, BottomDoor, new CellPos(2, 2));
            Assert.That(plan.Cells.SelectMany(c => c.Parts).Count(p => p.Source == TileSources.Door), Is.GreaterThan(0), "전제: 문짝이 있다");
            TestSheet sheet = MakeSheet(plan, new[] { WallA }, new[] { FloorA }, bandsExtra: ", \"test.band2\": { \"o\": \"#111111\", \"d\": \"#222222\", \"f\": \"#333333\", \"l\": \"#444444\" }");
            ComposedRoom one = Compositor(sheet).Compose(plan, _ => WallA, _ => FloorA, Sheet, BandId);
            ComposedRoom two = Compositor(sheet).Compose(plan, _ => WallA, _ => FloorA, Sheet, "test.band2");
            Assert.That(one.Problems.Concat(two.Problems), Is.Empty, string.Join("\n", two.Problems));
            int doors = 0, bands = 0;
            foreach (CellTiles cell in plan.Cells)
            {
                uint[] a = CellPixels(one, cell.Cell), b = CellPixels(two, cell.Cell);
                foreach (TilePart part in cell.Parts)
                    foreach (PixelRect r in part.Rects)
                        for (int y = r.Y0; y < r.Y1; y++)
                            for (int x = r.X0; x < r.X1; x++)
                            {
                                int i = y * Size + x;
                                if (part.Source == TileSources.Door || part.Source == TileSources.Sill) { Assert.That(b[i], Is.EqualTo(a[i]), $"{cell.Cell} {part.Source} ({x}, {y})"); doors++; }
                                else if (part.Source == TileSources.Band) { Assert.That(b[i], Is.Not.EqualTo(a[i]), $"{cell.Cell} 띠 ({x}, {y})"); bands++; }
                            }
            }
            Assert.That((doors > 0, bands > 0), Is.EqualTo((true, true)));
        }

        [Test]
        public void PreviewLookup_OverridesOnlyChangedCells()
        {
            var (map, wall, floor) = House();
            HouseBuildFrame frame = HouseBuildFrames.From(map);
            Assert.That(frame, Is.Not.Null, "짓는 집은 프레임이 있다");
            Assert.That((frame.FrameId, frame.DefaultWall, frame.DefaultFloor, frame.DefaultBand),
                Is.EqualTo((map.Entry.Build.FrameId, map.Entry.Build.DefaultWall, map.Entry.Build.DefaultFloor, map.Entry.Build.BandColors)));
            Assert.That(frame.Tiles, Is.SameAs(map.Tiles));

            var finishes = new HouseFinishes();
            finishes.Attach(frame, Defined);
            RoomFinishDefinition other = OtherFree(RoomFinishSlots.Floor, floor.Id);
            IReadOnlyList<CellPos> room = TopLeftRoom(map);
            HouseFinishPreview preview = finishes.Preview(other.Id, HouseFinishTools.Room, room[0], (Rootborn.Core.Placement.FurnitureUnlockProgress)null, _ => true);
            Assert.That(preview.CanApply, Is.True, preview.ReasonKey);

            var inner = new TableLookup { Band = "band.inner" };
            var outside = map.Tiles.Cells.Select(c => c.Cell).First(c => !room.Contains(c));
            inner.Floors[outside] = "inner.floor";
            inner.Walls[room[0]] = "inner.wall";
            var lookup = new HouseFinishPreviewLookup(inner, preview);
            var changed = new HashSet<CellPos>(preview.ChangedCells);
            Assert.That(changed.Count, Is.GreaterThan(0));
            foreach (CellPos c in changed) Assert.That(lookup.FloorAt(c), Is.EqualTo(other.Id), c + " 바뀌는 칸은 미리 보기 꾸밈");
            Assert.That(lookup.FloorAt(outside), Is.EqualTo("inner.floor"), "바뀌지 않는 칸은 안의 조회 그대로");
            Assert.That(lookup.WallAt(room[0]), Is.EqualTo("inner.wall"), "다른 자리(벽지)는 안의 조회 그대로");
            Assert.That(lookup.BandColors, Is.EqualTo("band.inner"));

            // 벽지 미리 보기는 벽지만 덮는다.
            RoomFinishDefinition otherWall = OtherFree(RoomFinishSlots.Wall, wall.Id);
            HouseFinishPreview wallPreview = finishes.Preview(otherWall.Id, HouseFinishTools.Room, room[0], (Rootborn.Core.Placement.FurnitureUnlockProgress)null, _ => true);
            var wallLookup = new HouseFinishPreviewLookup(inner, wallPreview);
            foreach (CellPos c in wallPreview.ChangedCells) Assert.That((wallLookup.WallAt(c), wallLookup.FloorAt(c)), Is.EqualTo((otherWall.Id, inner.FloorAt(c))));

            // 그 조회로 조합하면 그 방이 다른 바닥이다(Lookup_PerCellFloor와 같은 그림).
            var plainLookup = new TableLookup();
            foreach (CellPos c in changed) plainLookup.Floors[c] = other.Id;
            ComposedMapTiles viaPreview = ComposedMapTiles.Compose(map, RepoCompositor(), Defined, new HouseFinishPreviewLookup(new TableLookup(), preview));
            ComposedMapTiles viaTable = ComposedMapTiles.Compose(map, RepoCompositor(), Defined, plainLookup);
            Assert.That(viaPreview.Room.Pixels, Is.EqualTo(viaTable.Room.Pixels));
            // 안의 조회가 없으면 기본(빈 글)이다.
            Assert.That(new HouseFinishPreviewLookup(null, preview).FloorAt(outside), Is.EqualTo(""));
            Assert.That(HouseBuildFrames.From(Repo.Loader().LoadMap(Repo.Loader().Catalog.Maps.First(m => m.Build == null).Id)), Is.Null, "짓는 집이 아니면 null");
        }

        [Test]
        public void MoreCellsThanTheLimit_DrawsTheLdtkLayers()
        {
            var (house, _, _) = House();
            var rows = Enumerable.Range(0, 64).Select(_ => new string('.', 65)).ToArray();
            var big = new LoadedMap { Entry = house.Entry, Level = house.Level, World = house.World, Picture = house.Picture, Tiles = Plan(rows) };
            ComposedMapTiles tiles = ComposedMapTiles.Compose(big, new RoomTileCompositor(HouseMapTests.Sheets(), new RoomFinishArt(Path.Combine(Repo.Root, "data"))), Defined);
            AssertDrawsTheLdtkLayers(house, tiles, "칸 수가 상한을 넘었다");
            Assert.That(tiles.Room.TooLarge, Is.True);
        }
    }
}
