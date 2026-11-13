using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.World;
using Rootborn.World.Ldtk;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 픽셀 키트가 평면에서 만든 시험 방(art/world/rooms/test.two-rooms)을 실제 창과 게임 루프로 걷는다(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 맵 목록은 --maps로 그 방의 maps.json을 준다. 조작은 방향 키(HoldKey)와 걸어가기(WalkTo)뿐 — 순간이동·내부 명령 없음.
    /// 칸 좌표를 적지 않는다: 벽·바닥은 LDtk의 Collision 층(게임이 읽은 레벨)에서, 문간과 두 방은 바닥을 채워 찾는다.
    /// 그림 픽셀은 단언하지 않는다 — 스크린샷을 직접 열어 본다: output/pixel-kit/room/pixel-kit-room-NN-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class PixelKitRoomHostTests
    {
        private const string RoomMaps = "art/world/rooms/test.two-rooms/maps.json";   // 저장소 뿌리 기준
        private const int Wall = 1, Floor = 2;
        /// <summary>벽 쪽으로 누르고 있는 프레임(초당 4칸이라 60프레임이면 네 칸 갈 시간).</summary>
        private const int PushFrames = 60;

        [Test]
        public void PIXEL_KIT_ROOM_001_WallsStopTheWalker_AndTheDoorwayLeadsIntoTheOtherRoom()
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            string maps = Path.Combine(root, RoomMaps);
            string mapId = WorldCatalog.Load(maps).Maps.Single().Id;
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--world", mapId, "--size", "1920x1080", "--maps", maps,
                "--evidence", Path.Combine(root, "output", "pixel-kit", "room"),
            });
            options.TestMode = true;
            using var player = new PlayerDriver(options);
            player.Frames(3);
            Assert.That(player.World.Map.Id, Is.EqualTo(mapId));
            LoadedMap loaded = player.Game.World.Current;
            Assert.That(loaded.Problems, Is.Empty, string.Join("\n", loaded.Problems));
            Assert.That(loaded.Level.Layer("Wall").TilesetMissing || loaded.Level.Layer("Ground").TilesetMissing, Is.False, "타일셋 PNG를 찾았다");

            LdtkLayer layer = loaded.Level.Layer("Collision");
            int Value(CellPos c) => c.X >= 0 && c.Y >= 0 && c.X < layer.Columns && c.Y < layer.Rows ? layer.IntGrid[c.Y * layer.Columns + c.X] : 0;
            CellPos spawn = player.PlayerCell;
            Assert.That(Value(spawn), Is.EqualTo(Floor), "도착 지점은 바닥 칸");
            (CellPos doorway, HashSet<CellPos> here, HashSet<CellPos> there) = SplitAtDoorway(Value, spawn);
            TestContext.Out.WriteLine($"도착 {spawn}, 문간 {doorway}, 도착 방 {here.Count}칸, 건너편 {there.Count}칸");
            int shot = 1;
            player.Screenshot($"pixel-kit-room-{shot++:D2}-spawn");

            // 벽 정면 앞: 위 두 칸이 벽(정면)인 바닥 칸에서 위로 민다.
            CellPos front = Nearest(here, spawn, c => Value(Up(c, 1)) == Wall && Value(Up(c, 2)) == Wall);
            player.WalkTo(front);
            player.HoldKey(TestKey.Up, PushFrames);
            Assert.That(player.PlayerCell, Is.EqualTo(front), "벽 정면 앞에서 멈췄다(벽 칸으로 들어가지 않는다)");
            Assert.That(Value(player.PlayerCell), Is.EqualTo(Floor));
            player.Screenshot($"pixel-kit-room-{shot++:D2}-against-wall-face");

            // 세로 벽 옆(윗면): 오른쪽 칸이 벽이고 그 아래 두 칸도 벽인(시험 방의 face_height 2보다 바닥이 멀어 윗면으로 그려지는) 바닥 칸에서 오른쪽으로 민다.
            CellPos side = Nearest(here, spawn, c =>
            {
                var e = new CellPos(c.X + 1, c.Y);
                return Value(e) == Wall && Value(Up(e, -1)) == Wall && Value(Up(e, -2)) == Wall;
            });
            player.WalkTo(side);
            player.HoldKey(TestKey.Right, PushFrames);
            Assert.That(player.PlayerCell, Is.EqualTo(side), "세로 벽(윗면) 옆에서 멈췄다");
            player.Screenshot($"pixel-kit-room-{shot++:D2}-beside-wall-top");

            // 문간을 지나 옆방으로 걷는다.
            player.WalkTo(doorway);
            Assert.That(player.PlayerCell, Is.EqualTo(doorway));
            player.Screenshot($"pixel-kit-room-{shot++:D2}-in-doorway");
            CellPos far = Farthest(Value, there, doorway);
            player.WalkTo(far);
            Assert.That(player.PlayerCell, Is.EqualTo(far), "문간 건너편 방의 바닥 칸에 걸어서 닿았다");
            Assert.That(there, Does.Contain(player.PlayerCell));
            player.Screenshot($"pixel-kit-room-{shot++:D2}-other-room");

            // 옆방에서도 벽 정면 앞에서 멈춘다.
            CellPos front2 = Nearest(there, far, c => Value(Up(c, 1)) == Wall && Value(Up(c, 2)) == Wall);
            player.WalkTo(front2);
            player.HoldKey(TestKey.Up, PushFrames);
            Assert.That(player.PlayerCell, Is.EqualTo(front2), "옆방의 벽 정면 앞에서 멈췄다");
            player.Screenshot($"pixel-kit-room-{shot++:D2}-other-room-wall-face");

            Assert.That(player.CollisionViolations, Is.EqualTo(0), "몸이 막힌 칸에 겹친 프레임이 없다");
        }

        private static CellPos Up(CellPos c, int n) => new CellPos(c.X, c.Y - n);

        /// <summary>조건을 만족하는 칸 중 from에서 맨해튼 거리가 가장 가까운 것(같으면 위·왼쪽 먼저). 없으면 실패.</summary>
        private static CellPos Nearest(IEnumerable<CellPos> cells, CellPos from, Func<CellPos, bool> ok)
        {
            var list = cells.Where(ok).OrderBy(c => Math.Abs(c.X - from.X) + Math.Abs(c.Y - from.Y)).ThenBy(c => c.Y).ThenBy(c => c.X).ToList();
            Assert.That(list, Is.Not.Empty, "조건에 맞는 칸이 없다");
            return list[0];
        }

        /// <summary>room 안에서 from에서 걸음 수가 가장 먼 칸(같으면 위·왼쪽 먼저).</summary>
        private static CellPos Farthest(Func<CellPos, int> value, HashSet<CellPos> room, CellPos from)
        {
            var dist = new Dictionary<CellPos, int> { [from] = 0 };
            var queue = new Queue<CellPos>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                CellPos at = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = new CellPos(at.X + dx, at.Y + dy);
                    if ((room.Contains(n)) && !dist.ContainsKey(n)) { dist[n] = dist[at] + 1; queue.Enqueue(n); }
                }
            }
            return room.Where(dist.ContainsKey).OrderByDescending(c => dist[c]).ThenBy(c => c.Y).ThenBy(c => c.X).First();
        }

        private static HashSet<CellPos> Flood(Func<CellPos, int> value, CellPos start, CellPos? blocked)
        {
            var seen = new HashSet<CellPos>();
            if (value(start) != Floor || start.Equals(blocked)) return seen;
            var queue = new Queue<CellPos>();
            seen.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                CellPos at = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = new CellPos(at.X + dx, at.Y + dy);
                    if (value(n) == Floor && !n.Equals(blocked) && seen.Add(n)) queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// <summary>
        /// 문간 = 벽 줄의 틈(양옆 또는 위아래가 벽)이면서 그 칸을 막으면 바닥이 두 덩어리로 갈리는 칸 중 작은 덩어리가 가장 큰 칸(World PixelKitRoomTests의 RoomCells와 같은 규칙).
        /// (문간, 도착 지점 쪽 바닥, 건너편 바닥).
        /// </summary>
        private static (CellPos, HashSet<CellPos>, HashSet<CellPos>) SplitAtDoorway(Func<CellPos, int> value, CellPos spawn)
        {
            HashSet<CellPos> floor = Flood(value, spawn, null);
            (CellPos Cell, HashSet<CellPos> Here, HashSet<CellPos> There)? best = null;
            foreach (CellPos c in floor)
            {
                bool gap = (value(new CellPos(c.X - 1, c.Y)) == Wall && value(new CellPos(c.X + 1, c.Y)) == Wall) || (value(new CellPos(c.X, c.Y - 1)) == Wall && value(new CellPos(c.X, c.Y + 1)) == Wall);
                if (c.Equals(spawn) || !gap) continue;                 // 벽 줄의 틈만(양옆 또는 위아래가 벽)
                HashSet<CellPos> here = Flood(value, spawn, c);
                var rest = new HashSet<CellPos>(floor);
                rest.ExceptWith(here);
                rest.Remove(c);
                if (rest.Count == 0) continue;
                HashSet<CellPos> there = Flood(value, rest.First(), c);
                if (there.Count != rest.Count) continue;
                if (best == null || Math.Min(here.Count, there.Count) > Math.Min(best.Value.Here.Count, best.Value.There.Count))
                    best = (c, here, there);
            }
            Assert.That(best, Is.Not.Null, "바닥을 두 방으로 가르는 문간이 없다");
            Assert.That(best.Value.There.Count, Is.GreaterThanOrEqualTo(9), "건너편이 방 하나만큼 넓다");
            return (best.Value.Cell, best.Value.Here, best.Value.There);
        }
    }
}
