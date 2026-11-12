using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests.NpcMovement
{
    public sealed class NpcStayTests
    {
        private const int Cell = 32;
        private const double SecondsPerMinute = 1.0;
        private const double CellsPerMinute = 2.5 * SecondsPerMinute; // 초당 2.5칸

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

        /// <summary>11x11, 자리 (5,5) 둘레에 기둥 몇 개.</summary>
        private static CollisionGrid Yard() => Grid(
            "...........",
            "...........",
            "...........",
            "....#......",
            "...#...#...",
            ".....#.....",
            "..#........",
            "......##...",
            "...........",
            "...........",
            "...........");

        private static NpcStayContext Context(CollisionGrid grid, int cx, int cy, uint seed)
        {
            var c = CellPos.Center(new CellPos(cx, cy), Cell);
            return new NpcStayContext(new NpcSpot("map.yard", c.X, c.Y), grid, seed, CellsPerMinute, SecondsPerMinute);
        }

        private static NpcWanderStay Wander() => new NpcWanderStay(3f, 2f, 6f);

        private static NpcPose Fresh(INpcStayBehavior stay, in NpcStayContext context, double minutes)
        {
            object state = null;
            return stay.PoseAt(in context, minutes, ref state);
        }

        private static void AssertSame(NpcPose expected, NpcPose actual, string message)
        {
            Assert.That(actual.Present, Is.EqualTo(expected.Present), message);
            Assert.That(actual.MapId, Is.EqualTo(expected.MapId), message);
            Assert.That(actual.X, Is.EqualTo(expected.X), message);
            Assert.That(actual.Y, Is.EqualTo(expected.Y), message);
            Assert.That(actual.Facing, Is.EqualTo(expected.Facing), message);
            Assert.That(actual.Moving, Is.EqualTo(expected.Moving), message);
        }

        [Test]
        public void StableHash는_실행과_무관하다()
        {
            // FNV-1a 32비트: 2166136261에서 시작해 글자마다 xor 뒤 16777619를 곱한다. 손으로(별도 계산기로) 구한 값.
            Assert.That(StableHash.Of("npc.teacher"), Is.EqualTo(1694637116u));
            Assert.That(StableHash.Of(string.Empty), Is.EqualTo(2166136261u));
        }

        [Test]
        public void 서_있기는_자리에_그대로다()
        {
            var context = new NpcStayContext(new NpcSpot("map.yard", 100.5f, 77.25f), Yard(), 7u, CellsPerMinute, SecondsPerMinute);
            object state = null;
            foreach (double t in new[] { 0.0, 1.5, 60.0, 599.75 })
            {
                NpcPose pose = NpcStandStay.Instance.PoseAt(in context, t, ref state);
                Assert.That(pose.Present, Is.True);
                Assert.That(pose.MapId, Is.EqualTo("map.yard"));
                Assert.That(pose.X, Is.EqualTo(100.5f));
                Assert.That(pose.Y, Is.EqualTo(77.25f));
                Assert.That(pose.Facing, Is.EqualTo(Facing.Down));
                Assert.That(pose.Moving, Is.False);
            }
        }

        [Test]
        public void 어슬렁거림은_반지름_안의_열린_칸에만_있다()
        {
            CollisionGrid grid = Yard();
            NpcStayContext context = Context(grid, 5, 5 + 1, 12345u); // 자리 (5,6)
            var stay = Wander();
            object state = null;
            var visited = new HashSet<CellPos>();
            bool sawMoving = false;
            for (int i = 0; i <= 2400; i++)
            {
                double t = i / 4.0;
                NpcPose pose = stay.PoseAt(in context, t, ref state);
                Assert.That(pose.Present, Is.True);
                Assert.That(pose.MapId, Is.EqualTo("map.yard"));
                CellPos cell = CellPos.Containing(pose.X, pose.Y, Cell);
                Assert.That(grid.IsBlocked(cell), Is.False, "t=" + t + " " + cell);
                Assert.That(Math.Abs(cell.X - 5), Is.LessThanOrEqualTo(3), "t=" + t + " " + cell);
                Assert.That(Math.Abs(cell.Y - 6), Is.LessThanOrEqualTo(3), "t=" + t + " " + cell);
                visited.Add(cell);
                sawMoving |= pose.Moving;
            }
            Assert.That(sawMoving, Is.True, "어슬렁거리는 동안 걷는 때가 있다");
            Assert.That(visited.Count, Is.GreaterThan(3), "여러 칸을 다닌다");
        }

        [Test]
        public void 같은_씨앗이면_같은_걸음이다()
        {
            NpcStayContext context = Context(Yard(), 5, 6, 99u);
            var stay = Wander();
            AssertSame(Fresh(stay, in context, 137.5), Fresh(stay, in context, 137.5), "같은 씨앗");
            AssertSame(Fresh(stay, in context, 137.5), Fresh(Wander(), in context, 137.5), "같은 값의 다른 행동 객체");
        }

        [Test]
        public void 이어_간_것과_처음부터_계산한_것이_같다()
        {
            NpcStayContext context = Context(Yard(), 5, 6, 4242u);
            var stay = Wander();
            object state = null;
            NpcPose last = default;
            for (int i = 0; i <= 1500; i++) last = stay.PoseAt(in context, i / 5.0, ref state);
            AssertSame(Fresh(stay, in context, 300.0), last, "0→300분을 0.2분씩");
        }

        [Test]
        public void 시각이_뒤로_가도_맞는_자리다()
        {
            NpcStayContext context = Context(Yard(), 5, 6, 777u);
            var stay = Wander();
            object state = null;
            stay.PoseAt(in context, 300.0, ref state);
            NpcPose back = stay.PoseAt(in context, 40.0, ref state);
            AssertSame(Fresh(stay, in context, 40.0), back, "300분 뒤 40분");
        }

        [Test]
        public void 갈_칸이_없으면_서_있는다()
        {
            CollisionGrid grid = Grid(
                "#####",
                "##.##",
                "#####");
            var context = new NpcStayContext(new NpcSpot("map.box", 2 * Cell + 3f, Cell + 20f), grid, 5u, CellsPerMinute, SecondsPerMinute);
            var stay = Wander();
            object state = null;
            foreach (double t in new[] { 0.0, 3.0, 10.0, 123.4, 599.0 })
            {
                NpcPose pose = stay.PoseAt(in context, t, ref state);
                Assert.That(pose.Present, Is.True);
                Assert.That(pose.MapId, Is.EqualTo("map.box"));
                Assert.That(pose.X, Is.EqualTo(2 * Cell + 3f));
                Assert.That(pose.Y, Is.EqualTo(Cell + 20f));
                Assert.That(pose.Facing, Is.EqualTo(Facing.Down));
                Assert.That(pose.Moving, Is.False);
            }
        }

        [Test]
        public void 자리_칸이_막혀_있으면_서_있는다()
        {
            CollisionGrid grid = Grid(
                ".....",
                "..#..",
                ".....");
            var context = new NpcStayContext(new NpcSpot("map.room", 2 * Cell + 10f, Cell + 10f), grid, 5u, CellsPerMinute, SecondsPerMinute);
            object state = null;
            foreach (double t in new[] { 0.0, 30.0, 300.0 })
            {
                NpcPose pose = Wander().PoseAt(in context, t, ref state);
                Assert.That(pose.X, Is.EqualTo(2 * Cell + 10f));
                Assert.That(pose.Y, Is.EqualTo(Cell + 10f));
                Assert.That(pose.Facing, Is.EqualTo(Facing.Down));
                Assert.That(pose.Moving, Is.False);
            }
        }

        [Test]
        public void 씨앗이_다르면_걸음이_다르다()
        {
            CollisionGrid grid = Yard();
            NpcStayContext a = Context(grid, 5, 6, StableHash.Of("npc.teacher"));
            NpcStayContext b = Context(grid, 5, 6, StableHash.Of("npc.baker"));
            var stay = Wander();
            int differ = 0;
            for (int i = 1; i <= 60; i++)
            {
                NpcPose pa = Fresh(stay, in a, i * 5.0), pb = Fresh(stay, in b, i * 5.0);
                if (pa.X != pb.X || pa.Y != pb.Y) differ++;
            }
            Assert.That(differ, Is.GreaterThan(0));
        }

        [Test]
        public void 시각_0에는_자리_칸에서_쉰다()
        {
            NpcPose pose = Fresh(Wander(), Context(Yard(), 5, 6, 1u), 0.0);
            var center = CellPos.Center(new CellPos(5, 6), Cell);
            Assert.That(pose.X, Is.EqualTo(center.X));
            Assert.That(pose.Y, Is.EqualTo(center.Y));
            Assert.That(pose.Facing, Is.EqualTo(Facing.Down));
            Assert.That(pose.Moving, Is.False);
        }

        [Test]
        public void 어슬렁거림_값은_음수를_0으로_max를_min_이상으로()
        {
            var w = new NpcWanderStay(-1f, -2f, -3f);
            Assert.That(w.RadiusCells, Is.EqualTo(0f));
            Assert.That(w.PauseMinSeconds, Is.EqualTo(0f));
            Assert.That(w.PauseMaxSeconds, Is.EqualTo(0f));
            var v = new NpcWanderStay(3f, 6f, 2f);
            Assert.That(v.PauseMinSeconds, Is.EqualTo(6f));
            Assert.That(v.PauseMaxSeconds, Is.EqualTo(6f));
        }

        [Test]
        public void 일정_줄의_머무는_행동은_없으면_서_있기다()
        {
            var plain = new NpcScheduleEntry("location.a", null, null, null, null, 0, null);
            Assert.That(plain.Stay, Is.SameAs(NpcStandStay.Instance));
            var wander = Wander();
            var entry = new NpcScheduleEntry("location.a", null, null, null, null, 0, null, wander);
            Assert.That(entry.Stay, Is.SameAs(wander));
        }

        [Test]
        public void NpcRandom은_같은_씨앗이면_같은_수열이고_0은_1과_같다()
        {
            var a = new NpcRandom(0u);
            var b = new NpcRandom(1u);
            for (int i = 0; i < 20; i++)
            {
                int n = a.Next(10);
                Assert.That(n, Is.EqualTo(b.Next(10)));
                Assert.That(n, Is.InRange(0, 9));
                float r = a.Range(2f, 6f);
                Assert.That(r, Is.EqualTo(b.Range(2f, 6f)));
                Assert.That(r, Is.GreaterThanOrEqualTo(2f).And.LessThanOrEqualTo(6f));
            }
        }
    }
}
