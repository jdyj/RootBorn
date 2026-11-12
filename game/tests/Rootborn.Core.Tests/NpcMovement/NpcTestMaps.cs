using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests.NpcMovement
{
    /// <summary>NPC 이동 테스트가 함께 쓰는 메모리 맵 도우미. 칸은 32픽셀이다.</summary>
    internal static class NpcTestMaps
    {
        public const int Cell = 32;

        /// <summary>id → 맵. 없는 id는 null을 준다. 읽은 횟수를 센다.</summary>
        public sealed class Maps : IWorldMapSource
        {
            public readonly Dictionary<string, WorldMap> All = new Dictionary<string, WorldMap>();
            public int Loads;
            public WorldMap Load(string mapId) { Loads++; return All.TryGetValue(mapId, out var map) ? map : null; }
        }

        /// <summary>'#'는 막힌 칸.</summary>
        public static CollisionGrid Grid(params string[] rows)
        {
            int cols = rows[0].Length;
            var blocked = new bool[cols * rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < cols; x++)
                    blocked[y * cols + x] = rows[y][x] == '#';
            return new CollisionGrid(new MapGrid(cols, rows.Length, Cell), blocked);
        }

        public static WorldMap Map(string id, CollisionGrid grid, IReadOnlyList<DoorArea> doors = null, IDictionary<string, (float X, float Y)> spawns = null) =>
            new WorldMap(id, grid, doors ?? new DoorArea[0], spawns ?? new Dictionary<string, (float X, float Y)>());

        /// <summary>그 칸의 가운데 자리.</summary>
        public static NpcSpot Spot(string mapId, int cx, int cy)
        {
            var c = CellPos.Center(new CellPos(cx, cy), Cell);
            return new NpcSpot(mapId, c.X, c.Y);
        }

        public static (float X, float Y) Center(int cx, int cy) => CellPos.Center(new CellPos(cx, cy), Cell);

        /// <summary>두 자리가 모든 값에서 같다.</summary>
        public static void AssertSame(NpcPose expected, NpcPose actual, string message)
        {
            Assert.That(actual.Present, Is.EqualTo(expected.Present), message);
            Assert.That(actual.MapId, Is.EqualTo(expected.MapId), message);
            Assert.That(actual.X, Is.EqualTo(expected.X), message);
            Assert.That(actual.Y, Is.EqualTo(expected.Y), message);
            Assert.That(actual.Facing, Is.EqualTo(expected.Facing), message);
            Assert.That(actual.Moving, Is.EqualTo(expected.Moving), message);
        }
    }
}
