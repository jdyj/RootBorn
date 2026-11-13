using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.World.Ldtk;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 픽셀 키트가 평면에서 만든 시험 방(art/world/rooms/test.two-rooms, `kit.py room build`)을 게임의 맵 읽기로 읽는다.
    /// 칠한 Collision 층(1 벽, 2 바닥)이 게임의 충돌과 같고, 도착 지점에서 문간을 지나 옆방 바닥까지 걸어서 닿는다.
    /// 칸 좌표를 테스트에 적지 않는다 — 벽·바닥 칸은 LDtk의 Collision 층에서, 문간과 두 방은 바닥을 채워 찾는다(<see cref="RoomCells"/>).
    /// 화면 근거가 아니다(화면은 Host PixelKitRoomHostTests).
    /// </summary>
    public sealed class PixelKitRoomTests
    {
        /// <summary>시험 방의 맵 목록(게임에 --maps로 주는 파일). 저장소 뿌리 기준.</summary>
        public const string RoomMaps = "art/world/rooms/test.two-rooms/maps.json";

        private static (WorldMapLoader Loader, MapEntry Entry, LoadedMap Map) Load()
        {
            WorldCatalog catalog = WorldCatalog.Load(Path.Combine(Repo.Root, RoomMaps));
            var loader = new WorldMapLoader(Repo.Root, catalog);
            MapEntry entry = catalog.Maps.Single();
            return (loader, entry, loader.LoadMap(entry.Id));
        }

        [Test]
        public void TheRoomLoads_WithItsTilesetAndNoMapContractProblems()
        {
            var (loader, entry, map) = Load();
            Assert.That(map.Problems, Is.Empty, string.Join("\n", map.Problems));
            foreach (string name in new[] { "Wall", "Ground" })
            {
                LdtkLayer layer = map.Level.Layer(name);
                Assert.That(layer, Is.Not.Null, name);
                Assert.That(layer.TilesetMissing, Is.False, name + " 층의 타일셋 PNG를 찾았다");
                Assert.That(layer.Tiles, Is.Not.Empty, name);
            }
            Assert.That(entry.CollisionRule, Is.EqualTo("interior-auto"));
            var none = InteractionData.Parse("{ \"radiusCells\": 1.75, \"npcPriority\": 30, \"doorPriority\": 110, \"maps\": {} }");
            List<MapCheckProblem> problems = MapChecks.Check(loader, none);
            Assert.That(problems.Select(p => p.ToString()), Is.Empty);
        }

        [Test]
        public void PaintedWallCellsAreBlocked_AndFloorCellsAreOpen()
        {
            var (_, _, map) = Load();
            var cells = RoomCells.From(map.Level);
            CollisionGrid collision = map.World.Collision;
            Assert.That((collision.Columns, collision.Rows), Is.EqualTo((cells.Columns, cells.Rows)), "게임의 격자 = Collision 층의 칸 수");
            Assert.That(cells.Count(RoomCells.Wall), Is.GreaterThan(0));
            Assert.That(cells.Count(RoomCells.Floor), Is.GreaterThan(0));
            var wrong = new List<string>();
            foreach (CellPos c in cells.All())
            {
                int v = cells[c];
                bool blocked = collision.IsBlocked(c);
                if (v == RoomCells.Floor ? blocked : !blocked) wrong.Add(c + " 값 " + v + (blocked ? " 막힘" : " 열림"));
            }
            Assert.That(wrong, Is.Empty, "Collision 1은 막히고 2는 열린다(0은 밖이라 막힌다)");
            // 맵 밖도 막힌다(가장자리 너머로 걸어 나가지 않는다).
            Assert.That(collision.IsBlocked(-1, 0) && collision.IsBlocked(cells.Columns, 0) && collision.IsBlocked(0, cells.Rows), Is.True);
        }

        [Test]
        public void FromTheSpawn_YouWalkThroughTheDoorwayIntoTheOtherRoom()
        {
            var (_, entry, map) = Load();
            var cells = RoomCells.From(map.Level);
            int size = map.World.Grid.CellSize;
            map.World.TryGetSpawn(MapChecks.StartSpawn(entry), out var spawnPoint);
            CellPos spawn = CellPos.Containing(spawnPoint.X, spawnPoint.Y, size);
            Assert.That(cells[spawn], Is.EqualTo(RoomCells.Floor), "도착 지점은 바닥 칸");

            (CellPos doorway, HashSet<CellPos> here, HashSet<CellPos> there) = cells.SplitAtDoorway(spawn);
            TestContext.WriteLine($"문간 {doorway}, 도착 지점 방 {here.Count}칸, 건너편 방 {there.Count}칸");
            Assert.That(there.Count, Is.GreaterThanOrEqualTo(9), "문간 건너편이 방 하나만큼 넓다(막다른 한두 칸이 아니다)");

            HashSet<CellPos> reached = MapChecks.Reachable(map.World, MapChecks.StartSpawn(entry));
            Assert.That(there.Where(c => !reached.Contains(c)), Is.Empty, "건너편 방 바닥 전부에 걸어서 닿는다");
            Assert.That(here.Where(c => !reached.Contains(c)), Is.Empty, "도착 지점 방 바닥 전부에 걸어서 닿는다");
            Assert.That(reached.Where(c => cells[c] != RoomCells.Floor), Is.Empty, "걸어서 닿는 칸은 모두 칠한 바닥이다");

            List<CellPos> path = GridPath.Find(map.World.Collision, spawn, c => there.Contains(c));
            Assert.That(path, Is.Not.Null, "건너편 방으로 가는 길");
            Assert.That(path, Does.Contain(doorway), "길은 문간을 지난다");
        }
    }

    /// <summary>
    /// LDtk Collision 층의 칸 값(0 밖, 1 벽, 2 바닥)과 그것으로 찾는 방 구조. 좌표를 적지 않고 칸 값에서만 찾는다.
    /// 문간 = 벽 줄의 틈(양옆 또는 위아래가 벽)이면서 그 칸을 막으면 바닥이 두 덩어리로 갈리는 칸 중 작은 덩어리가 가장 큰 칸(막다른 복도 끝은 작은 덩어리가 작아 뽑히지 않는다).
    /// </summary>
    public sealed class RoomCells
    {
        public const int Out = 0, Wall = 1, Floor = 2;
        private readonly int[] _values;
        public readonly int Columns, Rows;

        private RoomCells(int[] values, int columns, int rows) { _values = values; Columns = columns; Rows = rows; }

        public static RoomCells From(LdtkLevel level)
        {
            LdtkLayer layer = level.Layer("Collision");
            Assert.That(layer, Is.Not.Null, "Collision 층");
            Assert.That(layer.IntGrid, Has.Length.EqualTo(layer.Columns * layer.Rows));
            return new RoomCells(layer.IntGrid, layer.Columns, layer.Rows);
        }

        public int this[CellPos c] => c.X >= 0 && c.Y >= 0 && c.X < Columns && c.Y < Rows ? _values[c.Y * Columns + c.X] : Out;

        public IEnumerable<CellPos> All()
        {
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                    yield return new CellPos(x, y);
        }

        public int Count(int value) => _values.Count(v => v == value);

        /// <summary>blocked를 뺀 바닥에서 start와 이어진 칸(상하좌우).</summary>
        public HashSet<CellPos> FloodFloor(CellPos start, CellPos? blocked = null)
        {
            var seen = new HashSet<CellPos>();
            if (this[start] != Floor || start.Equals(blocked)) return seen;
            var queue = new Queue<CellPos>();
            seen.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                CellPos at = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = new CellPos(at.X + dx, at.Y + dy);
                    if (this[n] == Floor && !n.Equals(blocked) && seen.Add(n)) queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// <summary>(문간, 도착 지점 쪽 바닥, 건너편 바닥). 바닥을 두 덩어리로 가르는 칸이 없으면 실패한다.</summary>
        public (CellPos Doorway, HashSet<CellPos> Here, HashSet<CellPos> There) SplitAtDoorway(CellPos spawn)
        {
            HashSet<CellPos> floor = FloodFloor(spawn);
            (CellPos Cell, HashSet<CellPos> Here, HashSet<CellPos> There)? best = null;
            foreach (CellPos c in floor)
            {
                bool gap = (this[new CellPos(c.X - 1, c.Y)] == Wall && this[new CellPos(c.X + 1, c.Y)] == Wall) || (this[new CellPos(c.X, c.Y - 1)] == Wall && this[new CellPos(c.X, c.Y + 1)] == Wall);
                if (c.Equals(spawn) || !gap) continue;                 // 벽 줄의 틈만(양옆 또는 위아래가 벽)
                HashSet<CellPos> here = FloodFloor(spawn, c);
                var rest = new HashSet<CellPos>(floor);
                rest.ExceptWith(here);
                rest.Remove(c);
                if (rest.Count == 0) continue;
                HashSet<CellPos> there = FloodFloor(rest.First(), c);
                if (there.Count != rest.Count) continue;                // 셋 이상으로 갈린다
                int smaller = System.Math.Min(here.Count, there.Count);
                if (best == null || smaller > System.Math.Min(best.Value.Here.Count, best.Value.There.Count))
                    best = (c, here, there);
            }
            Assert.That(best, Is.Not.Null, "바닥을 두 방으로 가르는 문간이 없다");
            return (best.Value.Cell, best.Value.Here, best.Value.There);
        }
    }
}
