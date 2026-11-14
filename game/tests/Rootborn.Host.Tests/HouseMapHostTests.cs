using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Start;
using Rootborn.World;
using Rootborn.World.Ldtk;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 새 집(픽셀 키트가 만든 방, game/world/maps.json의 House)을 새 게임으로 실제 창과 게임 루프에서 걷는다(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 슬롯 선택 → 캐릭터 만들기 → 집. 방 넷을 걷고, 벽 정면·세로 벽 앞에서 멈추고, 일과 판과 하루 끝에서 E, 현관 안쪽에서 아래로 눌러 마을로 나가고,
    /// 마을의 house 문으로 다시 들어온다. 조작은 PlayerDriver의 동사(버튼 누르기, 글자, 방향 키, 걸어가기, 문 지나기)뿐 — 순간이동·내부 명령 없음.
    /// 칸 좌표를 적지 않는다: 벽·바닥은 게임이 읽은 레벨의 Collision 층에서, 방은 바닥을 채워 찾고, 지점은 interactions.json에서 읽는다.
    /// 그림 픽셀은 단언하지 않는다 — 스크린샷을 직접 연다: output/house-building/stage1/house-map-NN-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HouseMapHostTests
    {
        private const int Wall = 1, Floor = 2;
        /// <summary>벽 쪽으로 누르고 있는 프레임(초당 4칸이라 60프레임이면 네 칸 갈 시간).</summary>
        private const int PushFrames = 60;
        /// <summary>한 축으로 벽 사이 바닥이 이 칸 수 이하이면 문간(방과 방을 잇는 좁은 틈)으로 본다.</summary>
        private const int DoorwayRun = 3;

        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-house-map-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        [Test]
        public void HOUSE_MAP_001_NewGameWalksEveryRoomOfTheNewHouse_AndOutTheDoor()
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--size", "1920x1080", "--evidence", Path.Combine(root, "output", "house-building", "stage1"),
            });
            options.TestMode = true;
            using var player = new PlayerDriver(options);
            player.Frames(3);
            int shot = 1;
            string Shot(string name) => player.Screenshot($"house-map-{shot++:D2}-{name}");

            // 슬롯 선택 → 새 이야기 → 이름 → 시작하기.
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            player.PressButton(SaveSlotScreen.NewGameId(0));
            player.WaitForScreen(ScreenCatalog.CharacterCreator);
            player.PressButton(CharacterCreatorScreen.NameFieldId);
            player.TypeText("하늘");
            player.PressKey(TestKey.Escape);
            player.PressButton(CharacterCreatorScreen.ConfirmId);
            player.Frames(3);
            Assert.That(player.Game.World, Is.Not.Null, "시작하기로 월드에 들어간다");
            MapEntry entry = player.Game.World.Current.Entry;
            Assert.That(entry.Build, Is.Not.Null, "새 게임은 짓는 집(build 성질이 있는 맵)에서 시작한다");
            string houseId = player.World.Map.Id;
            LoadedMap loaded = player.Game.World.Current;
            Assert.That(loaded.Problems, Is.Empty, string.Join("\n", loaded.Problems));
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "그림을 다 찾았다");

            LdtkLayer layer = loaded.Level.Layer("Collision");
            int Value(CellPos c) => c.X >= 0 && c.Y >= 0 && c.X < layer.Columns && c.Y < layer.Rows ? layer.IntGrid[c.Y * layer.Columns + c.X] : 0;
            CellPos spawn = player.PlayerCell;
            Assert.That(Value(spawn), Is.EqualTo(Floor), "도착 지점은 바닥 칸");
            Shot("spawn");

            // 집 전체: 가구 배치(P)를 열면 카메라가 집 전체를 비춘다.
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            player.MovePointer(-50, -50);
            player.Frames(3);
            Shot("whole-house");
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.FurniturePlacement);
            player.Frames(2);

            // 첫 안내인(집 안을 걷는 NPC)에게 다가간다.
            if (player.Game.World.Npcs.Count > 0)
            {
                string npc = player.Game.World.Npcs[0].NpcId;
                player.WalkToNpc(npc);
                Shot("npc");
            }

            // 방 넷: 문간(한 축으로 벽 사이가 좁은 바닥)을 뺀 바닥을 채워 나눈다.
            List<HashSet<CellPos>> rooms = Rooms(Value, spawn);
            TestContext.Out.WriteLine("방: " + string.Join(", ", rooms.Select(r => r.Count + "칸")));
            Assert.That(rooms.Count, Is.EqualTo(4), "새 집은 방 넷");
            for (int i = 0; i < rooms.Count; i++)
            {
                CellPos middle = Middle(rooms[i]);
                player.WalkTo(middle);
                Assert.That(player.PlayerCell, Is.EqualTo(middle), "방 " + (i + 1) + "의 가운데에 걸어서 닿았다");
                Shot("room-" + (i + 1));
            }

            // 벽 정면 앞: 위 두 칸이 벽인 바닥 칸의 한 칸 아래에서 위로 민다(도착 지점에서 가장 가까운 곳).
            CellPos front = Nearest(rooms.SelectMany(r => r), spawn, c => Value(Up(c, 1)) == Floor && Value(Up(c, 2)) == Wall && Value(Up(c, 3)) == Wall);
            AssertWallStops(player, Value, front, Up(front, 1), "벽 정면");
            Shot("against-wall-face");
            // 세로 벽 옆: 오른쪽 두 번째 칸이 벽이고 그 위아래도 벽인 바닥 칸에서 오른쪽으로 민다.
            CellPos side = Nearest(rooms.SelectMany(r => r), spawn, c =>
            {
                var e = new CellPos(c.X + 2, c.Y);
                return Value(new CellPos(c.X + 1, c.Y)) == Floor && Value(e) == Wall && Value(Up(e, -1)) == Wall && Value(Up(e, 1)) == Wall;
            });
            AssertWallStops(player, Value, side, new CellPos(side.X + 1, side.Y), "세로 벽");
            Shot("beside-wall");

            // 일과 판까지 걸어가 E → 일과 화면 → 닫기.
            InteractionData things = InteractionData.Load(Path.Combine(root, InteractionData.DefaultRelativePath));
            InteractionThing board = things.Map(houseId).Things.First(t => t.Kind == DailyPlanScreen.InteractionKind);
            player.WalkNear(board.X, board.Y, 24f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo(DailyPlanScreen.InteractionKind));
            Shot("plan-board");
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.DailyPlan);
            player.PressButton(DailyPlanScreen.CloseTopId);
            player.WaitForScreenClosed(ScreenCatalog.DailyPlan);

            // 하루 끝 자리까지 걸어가 E → 하루 결과 → 다음 날.
            InteractionThing end = things.Map(houseId).Things.First(t => t.Kind == DayResultScreen.InteractionKind);
            player.WalkNear(end.X, end.Y, 24f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo(DayResultScreen.InteractionKind));
            Shot("day-end");
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.DayResult);
            player.PressButton(DayResultScreen.NextDayId);
            player.WaitForScreenClosed(ScreenCatalog.DayResult);
            if (player.Screens.IsOpen(ScreenCatalog.DailyEvent))
            {
                player.PressButton(DailyEventScreen.CloseId);
                player.WaitForScreenClosed(ScreenCatalog.DailyEvent);
            }

            // 현관: 문 상자(바깥 벽 칸)에 위로 맞닿은 바닥 칸(현관 안쪽)의 한 칸 위까지 걸어가, 아래로 눌러 현관 안쪽으로 내려서면
            // 몸이 문 상자에 닿아 문이 열리고 마을로 나간다.
            DoorArea door = player.World.Map.Doors.Single(d => d.Destination.HasValue);
            int size = player.World.Map.Grid.CellSize;
            var doorCells = new HashSet<CellPos>(CollisionInput.CellsUnder(door.Area, size));
            Assert.That(doorCells.All(c => Value(c) == Wall), Is.True, "현관은 바깥 벽 칸");
            CellPos inside = doorCells.Select(c => Up(c, 1)).First(c => Value(c) == Floor);
            Assert.That(Value(Up(inside, 1)), Is.EqualTo(Floor), "현관 안쪽 위도 바닥");
            player.WalkTo(Up(inside, 1));
            Assert.That((player.World.Map.Id, player.World.OpeningDoor), Is.EqualTo((houseId, (DoorArea)null)), "현관 앞에 섰을 뿐 문은 열리지 않았다");
            Shot("front-door");
            player.KeyDown(TestKey.Down);
            player.FramesUntil(() => player.World.OpeningDoor != null || player.World.Map.Id != houseId, "현관 안쪽으로 내려서 문이 열리기 시작", 120);
            Assert.That(player.World.Map.Id, Is.EqualTo(houseId));
            Assert.That(player.World.OpeningDoor, Is.SameAs(door), "몸이 바깥 벽 칸의 문에 닿아 문이 열린다");
            Assert.That(player.World.Map.Collision.AnyBlocked(player.World.Walker.Box), Is.False, "벽 칸(문 칸)에 들어가지 않고 닿기만 한다");
            Shot("door-opening");
            player.FramesUntil(() => player.World.Map.Id != houseId, "현관 아래로 눌러 나가기", 300);
            player.KeyUp(TestKey.Down);
            player.Frames(2);
            Assert.That(player.World.Map.Id, Is.EqualTo(door.Destination.Value.MapId));
            Assert.That(player.World.Map.TryGetSpawn(door.Destination.Value.SpawnId, out var townSpawn), Is.True);
            Assert.That((player.World.Walker.X, player.World.Walker.Y), Is.EqualTo((townSpawn.X, townSpawn.Y)), "마을의 집 앞에 선다");
            Shot("town");

            // 마을의 house 문으로 다시 들어온다.
            Assert.That(player.WalkThroughDoor("house"), Is.EqualTo(houseId));
            Assert.That(player.World.Map.TryGetSpawn(MapChecks.StartSpawn(entry), out var houseSpawn), Is.True);
            Assert.That((player.World.Walker.X, player.World.Walker.Y), Is.EqualTo((houseSpawn.X, houseSpawn.Y)), "집 도착 지점에 선다");
            Shot("back-home");

            Assert.That(player.CollisionViolations, Is.EqualTo(0), "몸이 막힌 칸에 겹친 프레임이 없다");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void HOUSE_MAP_002_NewGameHouse_HasTheStarterFurniture()
        {
            // 새 게임의 처음 배치 가구(data/building/HouseStarterLayout.json, 시험용 — 결정 대기 29번)가 새 집에 놓여 보인다.
            // 스크린샷을 직접 연다: output/house-building/stage1b/house-starter-NN-*.png.
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--size", "1920x1080", "--evidence", Path.Combine(root, "output", "house-building", "stage1b"),
            });
            options.TestMode = true;
            using var player = new PlayerDriver(options);
            player.Frames(3);

            player.PressButton(SaveSlotScreen.NewGameId(0));
            player.WaitForScreen(ScreenCatalog.CharacterCreator);
            player.PressButton(CharacterCreatorScreen.NameFieldId);
            player.TypeText("하늘");
            player.PressKey(TestKey.Escape);
            player.PressButton(CharacterCreatorScreen.ConfirmId);
            player.Frames(3);
            Assert.That(player.Game.World.Current.Entry.Build, Is.Not.Null, "새 게임은 짓는 집에서 시작한다");

            var starter = player.Game.Session.Definitions.All<Rootborn.Core.Placement.HouseStarterLayout>().SelectMany(s => s.Placements).ToList();
            var placed = player.Game.Session.House.Placed;
            Assert.That(placed.Select(p => (p.Definition.StableId, p.Anchor, p.Direction)),
                Is.EqualTo(starter.Select(p => (p.FurnitureId, p.Anchor, p.Direction))), "처음 배치가 데이터 자리 그대로 놓였다");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "가구 그림을 다 찾았다");

            // 처음 가구가 있는 방으로 걸어 들어가 찍는다(카메라가 플레이어를 따라간다): 첫 줄 가구의 맵 칸에서 오른쪽 두 칸, 아래 한 칸.
            // 가구 칸 y는 위로 커진다 — 맵 칸 = (x, 행 수 - 1 - y)(Screens FurnitureRoom).
            int rows = player.World.Map.Grid.Rows;
            var first = starter[0].Anchor;
            player.WalkTo(new CellPos(first.X + 2, rows - 1 - first.Y + 1));
            player.Frames(2);
            player.Screenshot("house-starter-01");

            // 집 전체(가구 배치 P의 카메라)로 한 번 더.
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            player.MovePointer(-50, -50);
            player.Frames(3);
            player.Screenshot("house-starter-02-whole-house");
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.FurniturePlacement);
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
        }

        private static CellPos Up(CellPos c, int n) => new CellPos(c.X, c.Y - n);

        private static readonly (int Dx, int Dy)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>한 축으로 벽 사이 바닥 줄의 길이(그 칸 포함).</summary>
        private static int Run(Func<CellPos, int> value, CellPos c, int dx, int dy)
        {
            int n = 1;
            for (var p = new CellPos(c.X + dx, c.Y + dy); value(p) == Floor; p = new CellPos(p.X + dx, p.Y + dy)) n++;
            for (var p = new CellPos(c.X - dx, c.Y - dy); value(p) == Floor; p = new CellPos(p.X - dx, p.Y - dy)) n++;
            return n;
        }

        /// <summary>도착 지점에서 닿는 바닥 중 문간(가로나 세로로 벽 사이가 DoorwayRun칸 이하)을 뺀 덩어리들. 9칸 이상만 방으로 센다.</summary>
        private static List<HashSet<CellPos>> Rooms(Func<CellPos, int> value, CellPos spawn)
        {
            var floor = new HashSet<CellPos>();
            var queue = new Queue<CellPos>();
            floor.Add(spawn);
            queue.Enqueue(spawn);
            while (queue.Count > 0)
            {
                CellPos at = queue.Dequeue();
                foreach (var (dx, dy) in Steps)
                {
                    var n = new CellPos(at.X + dx, at.Y + dy);
                    if (value(n) == Floor && floor.Add(n)) queue.Enqueue(n);
                }
            }
            var inner = new HashSet<CellPos>(floor.Where(c => Run(value, c, 1, 0) > DoorwayRun && Run(value, c, 0, 1) > DoorwayRun));
            var rooms = new List<HashSet<CellPos>>();
            var seen = new HashSet<CellPos>();
            foreach (CellPos start in inner.OrderBy(c => c.Y).ThenBy(c => c.X))
            {
                if (!seen.Add(start)) continue;
                var room = new HashSet<CellPos> { start };
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    CellPos at = queue.Dequeue();
                    foreach (var (dx, dy) in Steps)
                    {
                        var n = new CellPos(at.X + dx, at.Y + dy);
                        if (inner.Contains(n) && seen.Add(n)) { room.Add(n); queue.Enqueue(n); }
                    }
                }
                if (room.Count >= 9) rooms.Add(room);
            }
            return rooms;
        }

        /// <summary>방 칸들의 무게 중심에 가장 가까운 칸(같으면 위·왼쪽).</summary>
        private static CellPos Middle(HashSet<CellPos> room)
        {
            double mx = room.Average(c => c.X), my = room.Average(c => c.Y);
            return room.OrderBy(c => (c.X - mx) * (c.X - mx) + (c.Y - my) * (c.Y - my)).ThenBy(c => c.Y).ThenBy(c => c.X).First();
        }

        /// <summary>조건을 만족하는 칸 중 from에서 맨해튼 거리가 가장 가까운 것(같으면 위·왼쪽 먼저). 없으면 실패.</summary>
        private static CellPos Nearest(IEnumerable<CellPos> cells, CellPos from, Func<CellPos, bool> ok)
        {
            var list = cells.Where(ok).OrderBy(c => Math.Abs(c.X - from.X) + Math.Abs(c.Y - from.Y)).ThenBy(c => c.Y).ThenBy(c => c.X).ToList();
            Assert.That(list, Is.Not.Empty, "조건에 맞는 칸이 없다");
            return list[0];
        }

        private static readonly (int Dx, int Dy, TestKey Key)[] Directions = { (0, -1, TestKey.Up), (0, 1, TestKey.Down), (-1, 0, TestKey.Left), (1, 0, TestKey.Right) };

        /// <summary>
        /// start(바닥)에서 이웃 stop(바닥) 쪽으로, 그 너머가 벽인 방향으로 민다(PixelKitRoomHostTests와 같은 판정).
        /// 대조군: 같은 프레임 수만큼 다른 열린 쪽으로 누르면 칸이 바뀐다. 벽 쪽으로 누르면 stop까지 한 칸 다가가 몸의 벽 쪽 변이 벽 칸 경계에 붙어 멈춘다.
        /// </summary>
        private static void AssertWallStops(PlayerDriver player, Func<CellPos, int> value, CellPos start, CellPos stop, string what)
        {
            int dx = stop.X - start.X, dy = stop.Y - start.Y;
            var wall = new CellPos(stop.X + dx, stop.Y + dy);
            Assert.That((value(start), value(stop), value(wall)), Is.EqualTo((Floor, Floor, Wall)), what + ": 바닥 두 칸 다음이 벽");
            TestKey toWall = Directions.Single(d => d.Dx == dx && d.Dy == dy).Key;
            var open = Directions.Where(d => (d.Dx, d.Dy) != (dx, dy) && value(new CellPos(start.X + d.Dx, start.Y + d.Dy)) == Floor).ToList();
            Assert.That(open, Is.Not.Empty, what + ": 대조군으로 누를 열린 쪽이 없다");

            player.WalkTo(start);
            player.HoldKey(open[0].Key, PushFrames);
            Assert.That(player.PlayerCell, Is.Not.EqualTo(start), what + " 대조군: 열린 쪽으로 누르면 칸이 바뀐다");

            player.WalkTo(start);
            int size = player.World.Map.Grid.CellSize;
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.HoldKey(toWall, PushFrames);
            Walker w = player.World.Walker;
            AreaRect box = w.Box;
            Assert.That(player.PlayerCell, Is.EqualTo(stop), what + ": 벽 앞 바닥 칸에서 멈췄다");
            float moved = dx != 0 ? (w.X - x0) * dx : (w.Y - y0) * dy;
            Assert.That(moved, Is.GreaterThan(size / 2f), what + ": 몸이 벽 쪽으로 움직였다");
            float lead = dx != 0 ? w.X + dx * box.W / 2f : w.Y + dy * box.H / 2f;
            float edge = dx != 0 ? (stop.X + 0.5f + dx * 0.5f) * size : (stop.Y + 0.5f + dy * 0.5f) * size;
            Assert.That((lead - edge) * (dx + dy), Is.LessThanOrEqualTo(0.01f), what + ": 몸의 벽 쪽 변이 벽 칸 경계를 넘지 않는다");
            Assert.That(Math.Abs(lead - edge), Is.LessThan(size * 4f / 60f), what + ": 벽 칸 경계에 붙어 멈췄다");
        }
    }
}
