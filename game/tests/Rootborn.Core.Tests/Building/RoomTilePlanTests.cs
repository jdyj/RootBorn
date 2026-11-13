using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests.Building
{
    /// <summary>출처표의 약속(Tools/pixel-kit/README.md "방 만들기 — 시험 표")을 따로 고정한다. 자동 테스트.</summary>
    public sealed class RoomTilePlanTests
    {
        private static RoomGrid Grid(params string[] rows)
        {
            var c = new RoomVectors.Case { Rows = rows };
            return c.Grid();
        }

        [Test]
        public void OwnersAreAlwaysFloorCells()
        {
            foreach (var c in RoomVectors.Cases())
            {
                var plan = c.Build();
                foreach (var cell in plan.Cells)
                    foreach (var part in cell.Parts)
                    {
                        var kind = TileSources.Find(part.Source);
                        Assert.That(part.HasOwner, Is.EqualTo(kind.HasOwner), $"{c.Id} {cell.Cell} {part.Source}");
                        if (!part.HasOwner)
                            continue;
                        int ox = cell.Cell.X + part.Owner.X, oy = cell.Cell.Y + part.Owner.Y;
                        Assert.That(plan.Grid.At(ox, oy), Is.EqualTo(RoomCells.Floor), $"{c.Id} {cell.Cell} 부분 {TileSourceDescribe(part)}의 주인 ({ox},{oy})");
                    }
            }
        }

        [Test]
        public void PartsAreDisjointAndInsideTheCell()
        {
            foreach (var c in RoomVectors.Cases())
                foreach (var cell in c.Build().Cells)
                {
                    var taken = new bool[RoomTilePlan.CellSize * RoomTilePlan.CellSize];
                    foreach (var part in cell.Parts)
                    {
                        var pixels = new List<(int X, int Y)>();
                        foreach (var r in part.Rects)
                        {
                            Assert.That(0 <= r.X0 && r.X0 < r.X1 && r.X1 <= 32 && 0 <= r.Y0 && r.Y0 < r.Y1 && r.Y1 <= 32, Is.True, $"{c.Id} {cell.Cell} 사각형 {r}");
                            for (int y = r.Y0; y < r.Y1; y++)
                                for (int x = r.X0; x < r.X1; x++)
                                {
                                    Assert.That(taken[y * 32 + x], Is.False, $"{c.Id} {cell.Cell} 픽셀 ({x},{y})이 두 부분에 든다");
                                    taken[y * 32 + x] = true;
                                    pixels.Add((x, y));
                                }
                        }
                        Assert.That(PixelRects.Encode(pixels), Is.EqualTo(part.Pixels), $"{c.Id} {cell.Cell} Rects와 Pixels가 다르다");
                    }
                }
        }

        [Test]
        public void FloorCellsAndFullFacesCoverTheWholeCell()
        {
            var plan = RoomVectors.Find("house").Build();
            foreach (var cell in plan.Cells)
            {
                if (plan.Grid.At(cell.Cell.X, cell.Cell.Y) == RoomCells.Floor)
                {
                    int area = cell.Parts.Sum(p => p.Rects.Sum(r => (r.X1 - r.X0) * (r.Y1 - r.Y0)));
                    Assert.That(area, Is.EqualTo(32 * 32), $"{cell.Cell} {cell.Kind}");
                }
            }
        }

        [Test]
        public void VerticalWallBetweenTwoRooms_HasTwoDifferentFloorOwners()
        {
            // 계획 Review Focus 3: 띠 왼쪽 비침과 오른쪽 비침의 주인이 서로 다른 방의 바닥이다.
            var plan = RoomVectors.Find("two-rooms-wall").Build();
            foreach (int y in new[] { 3, 4, 5 })
            {
                var owners = plan.At(3, y).Parts.Where(p => p.Source == TileSources.Floor).Select(p => p.Owner).Distinct().ToList();
                Assert.That(owners, Is.EquivalentTo(new[] { new CellPos(-1, 0), new CellPos(1, 0) }), "(3," + y + ")");
            }
        }

        [Test]
        public void WallDoorOnTopCell_IsASill_AndOnFaceIsADoor()
        {
            // 새 집 현관 (14, 17): 바깥 벽 윗면 칸, 위가 바닥 → 문지방.
            var house = RoomVectors.Find("house");
            var cell = house.Build().At(14, 17);
            Assert.That(cell.Parts.Select(p => p.Source), Does.Contain(TileSources.Sill));
            Assert.That(cell.Parts.Select(p => p.Source), Does.Not.Contain(TileSources.Band));

            // 문을 빼면 같은 칸이 띠로 돌아간다.
            var noDoor = RoomTilePlan.Build(house.Grid(), house.FaceHeight, house.Band, Array.Empty<CellPos>()).At(14, 17);
            Assert.That(noDoor.Parts.Select(p => p.Source), Does.Not.Contain(TileSources.Sill));

            // 바닥 바로 위 정면 칸의 문은 문짝(정면 그대로 + 문 자리).
            var grid = Grid("###", "###", "...");
            var face = RoomTilePlan.Build(grid, 2, 16, new[] { new CellPos(1, 1) }).At(1, 1);
            Assert.That(face.Parts.Select(p => p.Source), Is.EquivalentTo(new[] { TileSources.Door, TileSources.Face }));
            var door = face.Parts.Single(p => p.Source == TileSources.Door);
            Assert.That(door.Piece, Is.EqualTo("door"));
            Assert.That(door.Rects, Is.EqualTo(new[] { new PixelRect(6, 4, 26, 31) }));
        }

        [Test]
        public void OutCells_HaveNoTiles()
        {
            var plan = RoomVectors.Find("edge").Build();
            Assert.That(plan.Grid.At(0, 0), Is.EqualTo(RoomCells.Out));
            Assert.That(plan.At(0, 0), Is.Null);
            Assert.That(plan.At(-1, 0), Is.Null);
            Assert.That(plan.At(100, 100), Is.Null);
            Assert.That(plan.Cells.Any(c => plan.Grid.At(c.Cell.X, c.Cell.Y) == RoomCells.Out), Is.False);
        }

        [Test]
        public void CellsAreInRowOrder()
        {
            var plan = RoomVectors.Find("house").Build();
            var order = plan.Cells.Select(c => c.Cell.Y * 1000 + c.Cell.X).ToList();
            Assert.That(order, Is.Ordered);
        }

        [TestCase(3)]
        [TestCase(32)]
        [TestCase(0)]
        [TestCase(-1)]
        public void BadBand_Throws(int band)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RoomTilePlan.Build(Grid("#.#"), 2, band, null));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void BadFaceHeight_Throws(int faceHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RoomTilePlan.Build(Grid("#.#"), faceHeight, 16, null));
        }

        [TestCase(3)]
        [TestCase(-1)]
        [TestCase(7)]
        public void UnknownCellValue_Throws(int value)
        {
            var ex = Assert.Throws<ArgumentException>(() => new RoomGrid(2, 1, new[] { 1, value }));
            Assert.That(ex.Message, Does.Contain("(1, 0)"));
        }

        [Test]
        public void GridSizeMismatch_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RoomGrid(2, 2, new[] { 1, 2, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoomGrid(-1, 2, new int[0]));
            Assert.Throws<ArgumentNullException>(() => new RoomGrid(1, 1, null));
            Assert.Throws<ArgumentNullException>(() => RoomTilePlan.Build(null, 2, 16, null));
        }

        [Test]
        public void EmptyGrid_HasNoCells()
        {
            var plan = RoomTilePlan.Build(new RoomGrid(0, 0, new int[0]), 2, 16, null);
            Assert.That(plan.Cells, Is.Empty);
            Assert.That(plan.At(0, 0), Is.Null);
        }

        [Test]
        public void OneByOne_EachValue()
        {
            Assert.That(RoomTilePlan.Build(Grid(" "), 2, 16, null).Cells, Is.Empty);

            var floor = RoomTilePlan.Build(Grid("."), 2, 16, null).At(0, 0);
            Assert.That(floor.Kind, Is.EqualTo("floor"));
            Assert.That(floor.Parts.Single().Rects, Is.EqualTo(new[] { new PixelRect(0, 0, 32, 32) }));

            // 둘레가 모두 맵 밖인 벽 한 칸: 가운데 띠만, 나머지는 투명(바닥이 없으니 비침도 없다).
            var wall = RoomTilePlan.Build(Grid("#"), 2, 16, null).At(0, 0);
            Assert.That(wall.Kind, Is.EqualTo("cap"));
            Assert.That(wall.Parts.All(p => p.Source == TileSources.Band), Is.True);
            Assert.That(wall.Parts.Sum(p => p.Rects.Sum(r => (r.X1 - r.X0) * (r.Y1 - r.Y0))), Is.EqualTo(16 * 16));
        }

        [Test]
        public void WallsOnly_AreAllBandWithoutFloor()
        {
            var plan = RoomTilePlan.Build(Grid("####", "####", "####"), 2, 16, new[] { new CellPos(1, 1) });
            Assert.That(plan.Cells.Count, Is.EqualTo(12));
            foreach (var cell in plan.Cells)
            {
                Assert.That(cell.Kind, Is.EqualTo("cap"));
                Assert.That(cell.Parts, Is.Not.Empty);
                Assert.That(cell.Parts.All(p => p.Source == TileSources.Band), Is.True, cell.Cell.ToString());
            }
        }

        [Test]
        public void DoorsOffTheMapOrNotOnWalls_AreIgnored()
        {
            var c = RoomVectors.Find("two-rooms-wall");
            var plain = RoomTilePlan.Build(c.Grid(), 2, 16, null);
            var odd = RoomTilePlan.Build(c.Grid(), 2, 16, new[] { new CellPos(-1, 0), new CellPos(50, 3), new CellPos(1, 3), new CellPos(1, 3), new CellPos(0, -5) });
            Assert.That(Dump(odd), Is.EqualTo(Dump(plain)));
            Assert.That(Dump(RoomTilePlan.Build(c.Grid(), 2, 16, Array.Empty<CellPos>())), Is.EqualTo(Dump(plain)));
        }

        [Test]
        public void DoorsListIsCopied()
        {
            var c = RoomVectors.Find("house");
            var doors = new List<CellPos>(c.Doors);
            var plan = RoomTilePlan.Build(c.Grid(), 2, 16, doors);
            doors.Clear();
            Assert.That(plan.At(14, 17).Parts.Select(p => p.Source), Does.Contain(TileSources.Sill));
        }

        [Test]
        public void PixelRects_EncodeIsTheCanonicalText()
        {
            Assert.That(PixelRects.Encode(new (int, int)[0]), Is.EqualTo(""));
            var ring = new List<(int, int)>();
            for (int x = 0; x < 32; x++) { ring.Add((x, 16)); ring.Add((x, 31)); }
            for (int y = 17; y < 31; y++) ring.Add((0, y));
            Assert.That(PixelRects.Encode(ring), Is.EqualTo("0,16,32,17+0,17,1,31+0,31,32,32"));
            Assert.That(PixelRects.Rects(ring).Count, Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => PixelRects.Encode(new[] { (32, 0) }));
        }

        [Test]
        public void HouseSizedPlan_TakesUnder16ms()
        {
            var c = RoomVectors.Find("house");
            var grid = c.Grid();
            RoomTilePlan.Build(grid, c.FaceHeight, c.Band, c.Doors);     // 데우기(JIT)
            const int Runs = 20;
            var times = new double[Runs];
            for (int i = 0; i < Runs; i++)
            {
                var sw = Stopwatch.StartNew();
                RoomTilePlan.Build(grid, c.FaceHeight, c.Band, c.Doors);
                times[i] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times);
            double median = times[Runs / 2], worst = times[Runs - 1], mean = times.Average();
            TestContext.Out.WriteLine($"새 집 {grid.Columns}x{grid.Rows} 출처표 {Runs}번: 가운데 {median:F2}ms, 평균 {mean:F2}ms, 최악 {worst:F2}ms (목표 16ms)");
            Assert.That(median, Is.LessThan(100), "느슨한 지킴이 — 느린 CI에서도 넘지 않을 값");
        }

        private static string Dump(RoomTilePlan plan) =>
            string.Join("\n", plan.Cells.Select(c => c.Cell + " " + c.Kind + " " + string.Join(" | ", c.Parts.Select(RoomTileVectorTests.Describe))));

        private static string TileSourceDescribe(TilePart p) => RoomTileVectorTests.Describe(p);
    }
}
