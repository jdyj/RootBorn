using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests.Building
{
    /// <summary>방 채우기 규칙(좁은 칸, 방 바탕, 문간, 채우기 범위)을 실제 평면으로 고정한다. 자동 테스트.
    /// 평면 글은 파일을 읽지 않고 복사해 둔다(Core 테스트는 파일과 무관하다): 새 집은 art/kit/rooms/house.json의 plan,
    /// 시험 방은 커밋된 art/kit/rooms/test.two-rooms.json의 plan. '#' 벽, '.' 바닥, 'D'(벽 문) 벽, 그 밖은 밖.</summary>
    public sealed class RoomFillTests
    {
        private static readonly string[] NewHouse =
        {
            "############################",
            "############################",
            "############################",
            "#............#.............#",
            "#............#.............#",
            "#............#.............#",
            "#............#.............#",
            "#............#######.#######",
            "#............#######.#######",
            "#............#######.#######",
            "######.#######.............#",
            "######.#######.............#",
            "######.#######.............#",
            "#............#.............#",
            "#..........................#",
            "#..........................#",
            "#............#.............#",
            "##############D#############",
        };

        private static readonly string[] TwoRooms =
        {
            "####################",
            "####################",
            "####################",
            "#........#.........#",
            "#........#.........#",
            "#........#.........#",
            "#..................#",
            "#........#.........#",
            "#........#.........#",
            "######...######....#",
            "######...######....#",
            "######...######....#",
            "#........#.........#",
            "####################",
        };

        internal static RoomGrid Grid(params string[] rows)
        {
            int columns = rows.Max(r => r.Length);
            var cells = new int[columns * rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char ch = rows[y][x];
                    cells[y * columns + x] = ch == '.' ? RoomCells.Floor : ch == '#' || ch == 'D' ? RoomCells.Wall : RoomCells.Out;
                }
            return new RoomGrid(columns, rows.Length, cells);
        }

        private static IEnumerable<CellPos> Column(int x, int y0, int y1) => Enumerable.Range(y0, y1 - y0 + 1).Select(y => new CellPos(x, y));

        private static readonly CellPos[] NewHouseDoorways =
            Column(6, 10, 12).Concat(Column(20, 7, 9)).Concat(Column(13, 14, 15)).ToArray();

        private static int FloorCount(RoomGrid g)
        {
            int n = 0;
            for (int y = 0; y < g.Rows; y++)
                for (int x = 0; x < g.Columns; x++)
                    if (g.At(x, y) == RoomCells.Floor) n++;
            return n;
        }

        [Test]
        public void NewHouse_HasFourRoomsAndThreeDoorways()
        {
            var grid = Grid(NewHouse);
            Assert.That(FloorCount(grid), Is.EqualTo(283));
            var fill = RoomFill.Build(grid);
            Assert.That(fill.RoomCount, Is.EqualTo(4));

            var doorways = new List<CellPos>();
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (fill.IsDoorway(new CellPos(x, y))) doorways.Add(new CellPos(x, y));
            Assert.That(doorways, Is.EquivalentTo(NewHouseDoorways));

            // 방 바탕 = 방 채우기에서 문간을 뺀 것(이 평면에는 방에 드는 좁은 칸이 없다)
            var bases = new[] { new CellPos(3, 4), new CellPos(20, 4), new CellPos(20, 12), new CellPos(3, 15) }
                .Select(c => fill.RoomOf(c).Count(x => !fill.IsDoorway(x))).ToArray();
            Assert.That(bases, Is.EqualTo(new[] { 84, 52, 91, 48 }));
        }

        [Test]
        public void NewHouse_FillFromEachRoom_StopsAtDoorways()
        {
            var fill = RoomFill.Build(Grid(NewHouse));
            var starts = new[] { new CellPos(3, 4), new CellPos(20, 4), new CellPos(20, 12), new CellPos(3, 15) };
            var rooms = starts.Select(fill.RoomOf).ToArray();
            Assert.That(rooms.Select(r => r.Count), Is.EqualTo(new[] { 87, 55, 96, 53 }));

            // 다른 방의 바탕 칸은 없다 — 두 방이 함께 가지는 칸은 문간뿐
            for (int i = 0; i < rooms.Length; i++)
                for (int j = i + 1; j < rooms.Length; j++)
                    Assert.That(rooms[i].Intersect(rooms[j]).All(fill.IsDoorway), Is.True, $"방 {i}와 {j}");

            Assert.That(rooms[0].Where(fill.IsDoorway), Is.EquivalentTo(Column(6, 10, 12)));
            Assert.That(rooms[1].Where(fill.IsDoorway), Is.EquivalentTo(Column(20, 7, 9)));
            Assert.That(rooms[2].Where(fill.IsDoorway), Is.EquivalentTo(Column(20, 7, 9).Concat(Column(13, 14, 15))));
            Assert.That(rooms[3].Where(fill.IsDoorway), Is.EquivalentTo(Column(6, 10, 12).Concat(Column(13, 14, 15))));
        }

        [Test]
        public void FillFromDoorway_IsThatDoorwayOnly()
        {
            var fill = RoomFill.Build(Grid(NewHouse));
            Assert.That(fill.RoomOf(new CellPos(6, 11)), Is.EqualTo(Column(6, 10, 12).ToArray()));
            Assert.That(fill.RoomOf(new CellPos(20, 7)), Is.EqualTo(Column(20, 7, 9).ToArray()));
            Assert.That(fill.RoomOf(new CellPos(13, 15)), Is.EqualTo(Column(13, 14, 15).ToArray()));
        }

        [Test]
        public void TwoRooms_StripUnderThickWall_JoinsItsRoom()
        {
            var grid = Grid(TwoRooms);
            Assert.That(FloorCount(grid), Is.EqualTo(141));
            var fill = RoomFill.Build(grid);
            Assert.That(fill.RoomCount, Is.EqualTo(2));
            Assert.That(fill.IsDoorway(new CellPos(9, 6)), Is.True);

            var left = fill.RoomOf(new CellPos(3, 4));
            var right = fill.RoomOf(new CellPos(14, 4));
            // 방 둘 65·75 + 문간 (9,6) = 141. 채우기는 맞닿은 문간을 함께 든다.
            Assert.That(left.Count(c => !fill.IsDoorway(c)), Is.EqualTo(65));
            Assert.That(right.Count(c => !fill.IsDoorway(c)), Is.EqualTo(75));
            Assert.That(left.Count, Is.EqualTo(66));
            Assert.That(right.Count, Is.EqualTo(76));

            // 두꺼운 벽 밑 한 줄(세로 줄 길이 1)은 좁지만 한 방에만 닿아 그 방에 든다
            Assert.That(fill.IsDoorway(new CellPos(1, 12)), Is.False);
            Assert.That(fill.RoomOf(new CellPos(1, 12)), Is.SameAs(left));
            Assert.That(fill.RoomOf(new CellPos(10, 12)), Is.SameAs(right));
        }

        [Test]
        public void GapOfThree_IsOneRoom()
        {
            // 폭 3 틈은 좁지 않다 — 양쪽이 한 방이 된다
            var fill = RoomFill.Build(Grid(
                "#########",
                "#...#...#",
                "#...#...#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#...#...#",
                "#########"));
            Assert.That(fill.RoomCount, Is.EqualTo(1));
            Assert.That(fill.RoomOf(new CellPos(1, 1)).Count, Is.EqualTo(39));
            Assert.That(fill.IsDoorway(new CellPos(4, 4)), Is.False);
        }

        [Test]
        public void NarrowDeadEnd_JoinsItsRoom()
        {
            // 방 오른쪽에서 튀어나온 폭 1 막다른 길: 좁지만 맞닿은 방이 하나라 그 방에 든다
            var fill = RoomFill.Build(Grid(
                "#########",
                "#...#####",
                "#.......#",
                "#...#####",
                "#########"));
            Assert.That(fill.RoomCount, Is.EqualTo(1));
            Assert.That(fill.IsDoorway(new CellPos(6, 2)), Is.False);
            Assert.That(fill.RoomOf(new CellPos(7, 2)), Is.SameAs(fill.RoomOf(new CellPos(1, 1))));
            Assert.That(fill.RoomOf(new CellPos(1, 1)).Count, Is.EqualTo(13));
        }

        [Test]
        public void LoneNarrowPassage_IsItsOwnRoom()
        {
            var fill = RoomFill.Build(Grid(
                "#####",
                "#..##",
                "#####"));
            Assert.That(fill.RoomCount, Is.EqualTo(1));
            Assert.That(fill.IsDoorway(new CellPos(1, 1)), Is.False);
            Assert.That(fill.RoomOf(new CellPos(2, 1)), Is.EqualTo(new[] { new CellPos(1, 1), new CellPos(2, 1) }));
        }

        [Test]
        public void SameRoom_SameListInstance()
        {
            var fill = RoomFill.Build(Grid(NewHouse));
            var a = fill.RoomOf(new CellPos(1, 3));
            Assert.That(fill.RoomOf(new CellPos(12, 9)), Is.SameAs(a));
            Assert.That(fill.RoomOf(new CellPos(1, 3)), Is.SameAs(a));
            Assert.That(fill.RoomOf(new CellPos(6, 10)), Is.SameAs(fill.RoomOf(new CellPos(6, 12))));
            Assert.That(fill.RoomOf(new CellPos(20, 4)), Is.Not.SameAs(a));

            // y 다음 x 순
            var sorted = a.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
            Assert.That(a, Is.EqualTo(sorted));
        }

        [Test]
        public void NotFloor_Empty()
        {
            var fill = RoomFill.Build(Grid(NewHouse));
            Assert.That(fill.RoomOf(new CellPos(0, 0)), Is.Empty);           // 벽
            Assert.That(fill.RoomOf(new CellPos(14, 17)), Is.Empty);         // 벽 문
            Assert.That(fill.RoomOf(new CellPos(-1, 5)), Is.Empty);          // 격자 밖
            Assert.That(fill.RoomOf(new CellPos(100, 100)), Is.Empty);
            Assert.That(fill.IsDoorway(new CellPos(0, 0)), Is.False);
            Assert.That(fill.IsDoorway(new CellPos(-1, -1)), Is.False);
            Assert.That(RoomFill.NarrowRun, Is.EqualTo(2));
        }
    }
}
