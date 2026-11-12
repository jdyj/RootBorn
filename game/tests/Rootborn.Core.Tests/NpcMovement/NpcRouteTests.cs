using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests.NpcMovement
{
    public sealed class NpcRouteTests
    {
        private const int Cell = 32;

        private sealed class Maps : IWorldMapSource
        {
            public readonly Dictionary<string, WorldMap> All = new Dictionary<string, WorldMap>();
            public int Loads;
            public WorldMap Load(string mapId) { Loads++; return All.TryGetValue(mapId, out var map) ? map : null; }
        }

        /// <summary>'#'는 막힌 칸.</summary>
        private static CollisionGrid Grid(params string[] rows)
        {
            int cols = rows[0].Length;
            var blocked = new bool[cols * rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < cols; x++)
                    blocked[y * cols + x] = rows[y][x] == '#';
            return new CollisionGrid(new MapGrid(cols, rows.Length, Cell), blocked);
        }

        private static WorldMap Map(string id, CollisionGrid grid, IReadOnlyList<DoorArea> doors = null, IDictionary<string, (float X, float Y)> spawns = null) =>
            new WorldMap(id, grid, doors ?? new DoorArea[0], spawns ?? new Dictionary<string, (float X, float Y)>());

        private static NpcSpot Spot(string mapId, int cx, int cy)
        {
            var c = CellPos.Center(new CellPos(cx, cy), Cell);
            return new NpcSpot(mapId, c.X, c.Y);
        }

        private static (float X, float Y) Center(int cx, int cy) => CellPos.Center(new CellPos(cx, cy), Cell);

        /// <summary>A: 오른쪽 끝(4,1) 칸 위에 문 → B의 도착 지점 (1,1).</summary>
        private static Maps TwoMaps()
        {
            var maps = new Maps();
            var door = new DoorArea("b", new AreaRect(4 * Cell, 1 * Cell, Cell, Cell), new TravelTarget("B", "fromA"));
            maps.All["A"] = Map("A", Grid(
                "#####",
                "#....",
                "#####"), new[] { door });
            maps.All["B"] = Map("B", Grid(
                "#####",
                "#...#",
                "#...#",
                "#####"), spawns: new Dictionary<string, (float X, float Y)> { ["fromA"] = Center(1, 1) });
            return maps;
        }

        [Test]
        public void 같은_맵에서_막힌_칸을_돌아간다()
        {
            var maps = new Maps();
            CollisionGrid grid = Grid(
                ".....",
                "..#..",
                "..#..",
                "..#..");
            maps.All["A"] = Map("A", grid);
            NpcRoute route = new NpcRouteFinder(maps).Find(Spot("A", 0, 3), Spot("A", 4, 3));

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Legs.Count, Is.EqualTo(1));
            IReadOnlyList<CellPos> cells = route.Legs[0].Cells;
            Assert.That(cells[0], Is.EqualTo(new CellPos(0, 3)));
            Assert.That(cells[cells.Count - 1], Is.EqualTo(new CellPos(4, 3)));
            foreach (CellPos c in cells) Assert.That(grid.IsBlocked(c), Is.False, c.ToString());
            for (int i = 1; i < cells.Count; i++)
                Assert.That(System.Math.Abs(cells[i].X - cells[i - 1].X) + System.Math.Abs(cells[i].Y - cells[i - 1].Y), Is.EqualTo(1));
            Assert.That(route.TotalCells, Is.EqualTo(cells.Count - 1));
            Assert.That(route.TotalCells, Is.EqualTo(10f), "벽 위로 돌아가는 가장 짧은 길");
        }

        [Test]
        public void PoseAt은_칸_가운데를_곧게_잇는다()
        {
            var maps = new Maps();
            maps.All["A"] = Map("A", Grid("....."));
            NpcRoute route = new NpcRouteFinder(maps).Find(Spot("A", 0, 0), Spot("A", 3, 0));
            Assert.That(route.TotalCells, Is.EqualTo(3f));

            NpcPose start = route.PoseAt(0f);
            Assert.That(start.Present, Is.True);
            Assert.That(start.MapId, Is.EqualTo("A"));
            Assert.That((start.X, start.Y), Is.EqualTo(Center(0, 0)));

            NpcPose half = route.PoseAt(0.5f);
            Assert.That(half.X, Is.EqualTo((Center(0, 0).X + Center(1, 0).X) / 2f).Within(1e-4f));
            Assert.That(half.Y, Is.EqualTo(Center(0, 0).Y).Within(1e-4f));
            Assert.That(half.Moving, Is.True);
            Assert.That(half.Facing, Is.EqualTo(Facing.Right));

            NpcPose end = route.PoseAt(route.TotalCells);
            Assert.That((end.X, end.Y), Is.EqualTo(Center(3, 0)));
            Assert.That(end.Moving, Is.False);
            Assert.That(end.Facing, Is.EqualTo(Facing.Right), "마지막으로 움직인 방향");

            NpcPose past = route.PoseAt(route.TotalCells + 5f);
            Assert.That((past.X, past.Y), Is.EqualTo(Center(3, 0)), "TotalCells 너머는 끝으로 자른다");
            NpcPose before = route.PoseAt(-2f);
            Assert.That((before.X, before.Y), Is.EqualTo(Center(0, 0)), "0 아래는 처음으로 자른다");
        }

        [Test]
        public void 움직이지_않은_길은_아래를_본다()
        {
            var maps = new Maps();
            maps.All["A"] = Map("A", Grid("..."));
            NpcRoute route = new NpcRouteFinder(maps).Find(Spot("A", 1, 0), Spot("A", 1, 0));
            Assert.That(route.TotalCells, Is.EqualTo(0f));
            NpcPose pose = route.PoseAt(0f);
            Assert.That(pose.Facing, Is.EqualTo(Facing.Down));
            Assert.That(pose.Moving, Is.False);
            Assert.That(NpcPose.Absent.Present, Is.False);
        }

        [Test]
        public void 문을_지나_다른_맵으로_간다()
        {
            NpcRoute route = new NpcRouteFinder(TwoMaps()).Find(Spot("A", 1, 1), Spot("B", 3, 2));

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Legs.Count, Is.EqualTo(2));
            Assert.That(route.Legs[0].MapId, Is.EqualTo("A"));
            Assert.That(route.Legs[0].Cells[route.Legs[0].Cells.Count - 1], Is.EqualTo(new CellPos(3, 1)), "문 상자에 맞닿는 가장 가까운 칸");
            Assert.That(route.Legs[1].MapId, Is.EqualTo("B"));
            Assert.That(route.Legs[1].Cells[0], Is.EqualTo(new CellPos(1, 1)));
            Assert.That(route.Legs[1].Cells[route.Legs[1].Cells.Count - 1], Is.EqualTo(new CellPos(3, 2)));
            Assert.That(route.TotalCells, Is.EqualTo(2f + 3f), "문을 지나는 데는 0칸");
        }

        [Test]
        public void 문을_가장_적게_지나는_길을_고른다()
        {
            // A → B → C 와 A → C 둘 다 있다. A에서 C 쪽 문은 더 멀어도 문 하나짜리 길을 고른다.
            var maps = new Maps();
            maps.All["A"] = Map("A", Grid(
                "#########",
                "#.......#",
                "#########"), new[]
            {
                new DoorArea("b", new AreaRect(1 * Cell, 0, Cell, Cell), new TravelTarget("B", "in")),
                new DoorArea("c", new AreaRect(7 * Cell, 0, Cell, Cell), new TravelTarget("C", "in")),
            });
            maps.All["B"] = Map("B", Grid(
                "###",
                "#.#",
                "###"), new[] { new DoorArea("c", new AreaRect(1 * Cell, 0, Cell, Cell), new TravelTarget("C", "in")) },
                new Dictionary<string, (float X, float Y)> { ["in"] = Center(1, 1) });
            maps.All["C"] = Map("C", Grid(
                "###",
                "#.#",
                "###"), spawns: new Dictionary<string, (float X, float Y)> { ["in"] = Center(1, 1) });

            NpcRoute route = new NpcRouteFinder(maps).Find(Spot("A", 1, 1), Spot("C", 1, 1));
            Assert.That(route.Legs.Count, Is.EqualTo(2));
            Assert.That(route.Legs[1].MapId, Is.EqualTo("C"));
            Assert.That(route.TotalCells, Is.EqualTo(5f), "(1,1)에서 C 문 상자에 맞닿는 (6,1)까지 — B를 거치면 0칸이지만 문이 둘");
        }

        [Test]
        public void 다리_사이에서도_정확히_한_맵에_있다()
        {
            NpcRoute route = new NpcRouteFinder(TwoMaps()).Find(Spot("A", 1, 1), Spot("B", 3, 2));
            var seen = new List<string>();
            for (float t = 0f; t <= route.TotalCells + 1e-4f; t += 0.1f)
            {
                NpcPose pose = route.PoseAt(t);
                Assert.That(pose.Present, Is.True, "t=" + t);
                if (seen.Count == 0 || seen[seen.Count - 1] != pose.MapId) seen.Add(pose.MapId);
            }
            Assert.That(seen, Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void 길이_없으면_null이다()
        {
            var maps = new Maps();
            maps.All["A"] = Map("A", Grid(
                ".....",
                "...#.",
                "..#.#",
                "...#."));
            maps.All["Z"] = Map("Z", Grid("..."));
            var finder = new NpcRouteFinder(maps);

            Assert.That(finder.Find(Spot("A", 0, 0), Spot("A", 3, 2)), Is.Null, "벽으로 둘러싼 목적지");
            Assert.That(finder.Find(Spot("A", 0, 0), Spot("Z", 1, 0)), Is.Null, "문이 없는 다른 맵");
            Assert.That(finder.Find(Spot("A", 0, 0), Spot("없는맵", 1, 0)), Is.Null, "읽을 수 없는 맵");
        }

        [Test]
        public void 목적지_칸이_막혔으면_옆_칸으로_간다()
        {
            var maps = new Maps();
            CollisionGrid grid = Grid(
                "#####",
                "#..##",
                "#####");
            maps.All["A"] = Map("A", grid);
            var finder = new NpcRouteFinder(maps);

            NpcRoute route = finder.Find(Spot("A", 1, 1), Spot("A", 3, 1));
            Assert.That(route, Is.Not.Null);
            IReadOnlyList<CellPos> cells = route.Legs[route.Legs.Count - 1].Cells;
            Assert.That(cells[cells.Count - 1], Is.EqualTo(new CellPos(2, 1)));

            maps.All["B"] = Map("B", Grid(
                "#####",
                "#.###",
                "#####"));
            Assert.That(finder.Find(Spot("B", 1, 1), Spot("B", 3, 1)), Is.Null, "둘레 8칸 중 닿는 칸도 없다");
        }

        [Test]
        public void 같은_칸_사이의_길은_기억한_것을_돌려준다()
        {
            Maps maps = TwoMaps();
            var finder = new NpcRouteFinder(maps);
            NpcRoute first = finder.Find(Spot("A", 1, 1), Spot("B", 3, 2));
            int loads = maps.Loads;
            var c = Center(1, 1);
            NpcRoute again = finder.Find(new NpcSpot("A", c.X + 3f, c.Y - 5f), Spot("B", 3, 2));
            Assert.That(again, Is.SameAs(first), "칸이 같으면 같은 길");
            Assert.That(maps.Loads, Is.EqualTo(loads));
        }

        [Test]
        public void Forget_뒤에는_새_격자로_다시_찾는다()
        {
            var maps = new Maps();
            maps.All["A"] = Map("A", Grid(
                ".....",
                "....."));
            var finder = new NpcRouteFinder(maps);
            NpcRoute before = finder.Find(Spot("A", 0, 0), Spot("A", 4, 0));
            Assert.That(before.Legs[0].Cells, Has.Member(new CellPos(2, 0)));

            maps.All["A"] = Map("A", Grid(
                "..#..",
                "....."));
            Assert.That(finder.Find(Spot("A", 0, 0), Spot("A", 4, 0)), Is.SameAs(before), "Forget 전에는 기억한 길");

            finder.Forget("A");
            NpcRoute after = finder.Find(Spot("A", 0, 0), Spot("A", 4, 0));
            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after.Legs[0].Cells, Has.No.Member(new CellPos(2, 0)));
        }
    }
}
