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

        private TestSheet MakeSheet(RoomTilePlan plan, IEnumerable<RoomFinishDefinition> walls, IEnumerable<RoomFinishDefinition> floors, string skip = null, string atlasExtra = null)
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
            json.Append(" }, \"bands\": { \"" + BandId + "\": { \"o\": \"#010203\", \"d\": \"#040506\", \"f\": \"#070809\", \"l\": \"#0A0B0C\" } } }");
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
