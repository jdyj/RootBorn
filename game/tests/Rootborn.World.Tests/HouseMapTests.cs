using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.World.Ldtk;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 게임의 집은 픽셀 키트가 평면(art/kit/rooms/house.json)에서 만든 방이다.
    /// 집은 맵 id가 아니라 맵 목록의 build 성질로 고른다(짓는 집은 하나). 좌표는 테스트에 적지 않는다 — 맵·평면·게임 데이터(maps.json, interactions.json)에서 읽는다.
    /// 화면 근거가 아니다(화면은 Host HouseMapHostTests).
    /// </summary>
    public sealed class HouseMapTests
    {
        /// <summary>키트 방 평면 폴더(저장소 뿌리 기준). 평면 id가 방 폴더 이름이다(art/world/rooms/&lt;id&gt;/&lt;id&gt;.ldtk).</summary>
        private const string PlanFolder = "art/kit/rooms", RoomFolder = "art/world/rooms";
        private const int Wall = 1, Floor = 2;

        private static (WorldMapLoader Loader, MapEntry Entry, LoadedMap Map) House()
        {
            WorldMapLoader loader = Repo.Loader();
            List<MapEntry> built = loader.Catalog.Maps.Where(m => m.Build != null).ToList();
            Assert.That(built.Select(m => m.Id), Is.EqualTo(new[] { "House" }), "짓는 집(build 성질이 있는 맵)은 집 하나");
            return (loader, built[0], loader.LoadMap(built[0].Id));
        }

        /// <summary>그 맵 LDtk를 만든 키트 평면: 방 폴더 이름 = 평면 id.</summary>
        private static string[] PlanOf(MapEntry entry)
        {
            string id = Path.GetFileNameWithoutExtension(entry.Ldtk);
            Assert.That(entry.Ldtk, Is.EqualTo(RoomFolder + "/" + id + "/" + id + ".ldtk"), "집 맵은 키트 방 폴더의 LDtk");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, PlanFolder, id + ".json")));
            return doc.RootElement.GetProperty("plan").EnumerateArray().Select(r => r.GetString()).ToArray();
        }

        private static int[] Layer(LoadedMap map, string name)
        {
            LdtkLayer layer = map.Level.Layer(name);
            Assert.That(layer, Is.Not.Null, name + " 층");
            Assert.That(layer.IntGrid, Has.Length.EqualTo(map.World.Grid.Count), name + " 층 크기 = 맵 칸 수");
            return layer.IntGrid;
        }

        [Test]
        public void HouseIsTheKitRoom_CollisionEqualsThePlan()
        {
            var (_, entry, map) = House();
            Assert.That(map.Problems, Is.Empty, string.Join("\n", map.Problems));
            Assert.That(entry.CollisionRule, Is.EqualTo("interior-auto"));
            Assert.That(entry.Build.FrameLayer, Is.Not.Empty);
            Assert.That(entry.Build.FrameId, Is.Not.Empty);
            string[] plan = PlanOf(entry);
            MapGrid grid = map.World.Grid;
            Assert.That((grid.Columns, grid.Rows), Is.EqualTo((plan[0].Length, plan.Length)), "맵 크기 = 평면 크기");
            int[] collision = Layer(map, "Collision");
            var wrong = new List<string>();
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                {
                    int want = plan[y][x] == '.' ? Floor : Wall;   // 평면: '.' 바닥, '#'·벽 문 글자는 벽
                    int got = collision[grid.Index(x, y)];
                    bool blocked = map.World.Collision.IsBlocked(x, y);
                    if (got != want || blocked != (want == Wall)) wrong.Add(new CellPos(x, y) + " 평면 '" + plan[y][x] + "' Collision " + got + (blocked ? " 막힘" : " 열림"));
                }
            Assert.That(wrong, Is.Empty, "Collision 층과 게임의 충돌이 평면과 같다");
        }

        [Test]
        public void TheOnlyDoorLeadsToTown_AndTouchesTheFloorInside()
        {
            var (loader, entry, map) = House();
            DoorArea door = map.World.Doors.Single();
            Assert.That(door.Destination, Is.EqualTo(new TravelTarget("Town", "town-home-entry")));
            int size = map.World.Grid.CellSize;
            Assert.That(door.Area.CenterX, Is.EqualTo(464f).Within(0.6f), "문 가운데 x는 옛 집의 출구(Unity HouseExit (0.5+14)*32)와 같다");
            HashSet<CellPos> reached = MapChecks.Reachable(map.World, MapChecks.StartSpawn(entry));
            Assert.That(MapChecks.Touches(reached, door.Area, size), Is.True, "도착 지점에서 걸어서 닿는 칸이 문 상자에 맞닿는다");
            int[] collision = Layer(map, "Collision");
            foreach (CellPos c in CollisionInput.CellsUnder(door.Area, size))
                Assert.That(collision[map.World.Grid.Index(c.X, c.Y)], Is.EqualTo(Wall), "문은 바깥 벽 칸 " + c);
            // 마을의 집 문이 이 집의 도착 지점으로 온다.
            WorldMap town = loader.LoadMap(door.Destination.Value.MapId).World;
            Assert.That(town.Doors.Where(d => d.Destination.HasValue && d.Destination.Value.MapId == entry.Id).Select(d => d.Destination.Value.SpawnId),
                Has.Some.EqualTo(MapChecks.StartSpawn(entry)), "마을에서 집으로 들어오는 문");
        }

        [Test]
        public void WalkingDownFromTheEntry_GoesOutThroughTheDoor_AndTheTownDoorLeadsBackIn()
        {
            // Core 걷기(WorldSession, 실제 Walker와 문 판정)로 방향만 준다. 도착 지점에서 문까지 곧은 길이 아니면 경로를 따라 간다.
            var (loader, entry, map) = House();
            var session = new WorldSession(loader, loader.Catalog.Walker);
            Assert.That(session.Enter(entry.Id, MapChecks.StartSpawn(entry)), Is.True);
            DoorArea door = map.World.Doors.Single();
            int size = map.World.Grid.CellSize;
            var doorCells = new HashSet<CellPos>(CollisionInput.CellsUnder(door.Area, size));
            List<CellPos> path = GridPath.Find(map.World.Collision, CellPos.Containing(session.Walker.X, session.Walker.Y, size),
                c => doorCells.Any(d => System.Math.Abs(d.X - c.X) + System.Math.Abs(d.Y - c.Y) == 1));
            Assert.That(path, Is.Not.Null, "문 앞 바닥까지 가는 길");
            Step(session, path, size);
            Assert.That(session.Map.Id, Is.EqualTo(entry.Id), "문 앞에 섰을 뿐 아직 나가지 않았다");
            CellPos inside = CellPos.Containing(session.Walker.X, session.Walker.Y, size);
            CellPos toward = doorCells.First(d => System.Math.Abs(d.X - inside.X) + System.Math.Abs(d.Y - inside.Y) == 1);
            for (int i = 0; i < 240 && session.Map.Id == entry.Id; i++) session.Update(toward.X - inside.X, toward.Y - inside.Y, 1f / 60f);
            Assert.That(session.Map.Id, Is.EqualTo("Town"), "문 쪽으로 누르면 마을로 나간다");
            Assert.That(session.Map.TryGetSpawn("town-home-entry", out var townSpawn), Is.True);
            Assert.That((session.Walker.X, session.Walker.Y), Is.EqualTo((townSpawn.X, townSpawn.Y)));

            // 마을의 집 문으로 다시 들어온다.
            DoorArea houseDoor = session.Map.Doors.First(d => d.Destination.HasValue && d.Destination.Value.MapId == entry.Id);
            var townDoorCells = new HashSet<CellPos>(CollisionInput.CellsUnder(houseDoor.Area, size));
            List<CellPos> back = GridPath.Find(session.Map.Collision, CellPos.Containing(session.Walker.X, session.Walker.Y, size),
                c => townDoorCells.Contains(c) || townDoorCells.Any(d => System.Math.Abs(d.X - c.X) + System.Math.Abs(d.Y - c.Y) == 1));
            Assert.That(back, Is.Not.Null, "마을 도착 지점에서 집 문까지 가는 길");
            Step(session, back, size);
            CellPos here = CellPos.Containing(session.Walker.X, session.Walker.Y, size);
            CellPos into = townDoorCells.Contains(here) ? here : townDoorCells.First(d => System.Math.Abs(d.X - here.X) + System.Math.Abs(d.Y - here.Y) == 1);
            int dx = System.Math.Sign(into.X - here.X), dy = System.Math.Sign(into.Y - here.Y);
            if (dx == 0 && dy == 0) dy = houseDoor.Area.CenterY < session.Walker.Y ? -1 : 1;
            for (int i = 0; i < 240 && session.Map.Id == "Town"; i++) session.Update(dx, dy, 1f / 60f);
            Assert.That(session.Map.Id, Is.EqualTo(entry.Id), "마을의 집 문으로 들어오면 집");
            Assert.That(session.Map.TryGetSpawn(MapChecks.StartSpawn(entry), out var spawn), Is.True);
            Assert.That((session.Walker.X, session.Walker.Y), Is.EqualTo((spawn.X, spawn.Y)), "집 도착 지점에 선다");
        }

        /// <summary>칸 경로를 방향만 주어 걷는다(칸 가운데에 닿으면 다음 칸). 맵이 바뀌면 멈춘다.</summary>
        private static void Step(WorldSession session, List<CellPos> path, int size)
        {
            string map = session.Map.Id;
            foreach (CellPos next in path)
            {
                var center = CellPos.Center(next, size);
                for (int i = 0; i < 240 && session.Map.Id == map; i++)
                {
                    float ddx = center.X - session.Walker.X, ddy = center.Y - session.Walker.Y;
                    if (System.Math.Abs(ddx) < 0.5f && System.Math.Abs(ddy) < 0.5f) break;
                    int dx = System.Math.Abs(ddx) >= 0.5f ? System.Math.Sign(ddx) : 0, dy = dx == 0 ? System.Math.Sign(ddy) : 0;
                    float step = size * 4f / 60f;
                    float dist = dx != 0 ? System.Math.Abs(ddx) : System.Math.Abs(ddy);
                    session.Update(dx, dy, dist < step ? dist / (size * 4f) : 1f / 60f);
                }
                if (session.Map.Id != map) return;
            }
        }

        [Test]
        public void TodaysPointsAreFloor_AndFixedInBuild()
        {
            var (_, entry, map) = House();
            InteractionData data = InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));
            MapInteractions things = data.Map(entry.Id);
            int size = map.World.Grid.CellSize;
            int[] build = Layer(map, entry.Build.FrameLayer);
            var points = new List<(string What, float X, float Y)>();
            foreach (string spawn in entry.Spawns.Keys) points.Add(("도착 지점 " + spawn, entry.Spawns[spawn].X, entry.Spawns[spawn].Y));
            foreach (InteractionThing t in things.Things) points.Add(("상호작용 " + t.Kind + "/" + t.Id, t.X, t.Y));
            foreach (var anchors in things.NpcAnchors)
                for (int i = 0; i < anchors.Value.Count; i++) points.Add(("NPC 자리 " + anchors.Key + "#" + i, anchors.Value[i].X, anchors.Value[i].Y));
            Assert.That(points.Count(p => p.What.StartsWith("상호작용")), Is.GreaterThanOrEqualTo(2), "집의 고정 지점(일과 판, 하루 끝)을 읽었다");
            Assert.That(points.Count(p => p.What.StartsWith("NPC")), Is.GreaterThanOrEqualTo(1));
            var wrong = new List<string>();
            foreach (var p in points)
            {
                CellPos c = CellPos.Containing(p.X, p.Y, size);
                if (map.World.Collision.IsBlocked(c)) wrong.Add(p.What + " " + c + " 막힘");
                else if (build[map.World.Grid.Index(c.X, c.Y)] != 0) wrong.Add(p.What + " " + c + " Build " + build[map.World.Grid.Index(c.X, c.Y)]);
            }
            Assert.That(wrong, Is.Empty, "고정 지점은 바닥이고 Build 0(허물 수 없는 칸)");
        }

        [Test]
        public void EveryFloorIsReachableFromTheEntry()
        {
            var (_, entry, map) = House();
            int[] collision = Layer(map, "Collision");
            HashSet<CellPos> reached = MapChecks.Reachable(map.World, MapChecks.StartSpawn(entry));
            MapGrid grid = map.World.Grid;
            var floor = new List<CellPos>();
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (collision[grid.Index(x, y)] == Floor) floor.Add(new CellPos(x, y));
            Assert.That(floor, Is.Not.Empty);
            Assert.That(floor.Where(c => !reached.Contains(c)), Is.Empty, "칠한 바닥 전부에 도착 지점에서 걸어서 닿는다");
            Assert.That(reached.Where(c => grid.Contains(c.X, c.Y) && collision[grid.Index(c.X, c.Y)] != Floor), Is.Empty, "걸어서 닿는 칸은 모두 칠한 바닥");
        }
    }
}
