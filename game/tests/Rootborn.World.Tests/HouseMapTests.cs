using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Building;
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

        // ---------- 조합해 그리기(작업 9): 출처표 + 기본 꾸밈 + 조각 시트 = 키트가 구운 Wall·Ground ----------

        private static RoomFinishArt Art() => new RoomFinishArt(Path.Combine(Repo.Root, "data"));

        /// <summary>시트 PNG(정의의 sheet, 저장소 뿌리 기준) → 픽셀. 시트마다 한 번 읽는다.</summary>
        internal static Func<string, PieceImage> Sheets()
        {
            var cache = new Dictionary<string, PieceImage>(StringComparer.Ordinal);
            return sheet =>
            {
                if (!cache.TryGetValue(sheet, out PieceImage image))
                {
                    string path = Path.Combine(Repo.Root, sheet);
                    cache[sheet] = image = File.Exists(path) ? PngPixels.Read(path) : null;
                }
                return image;
            };
        }

        [Test]
        public void BuildSettingsResolve()
        {
            // 짓는 집의 build 값이 정의·조각 시트로 풀린다: 바꿔 그릴 층이 있고, 기본 꾸밈이 데이터에 있는 벽·바닥 꾸밈이고, 띠 색 줄과 띠 두께가 시트의 좌표표와 맞는다.
            var (loader, entry, _) = House();
            InteractionData things = InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));
            List<string> found = MapChecks.Check(loader, things, Repo.Definitions, Art()).Where(p => p.MapId == entry.Id).Select(p => p.ToString()).ToList();
            Assert.That(found, Is.Empty, string.Join("\n", found));
            Assert.That(entry.Build.ReplacesLayers, Is.Not.Empty);
            Assert.That(Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultWall).Slot, Is.EqualTo(RoomFinishSlots.Wall));
            Assert.That(Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultFloor).Slot, Is.EqualTo(RoomFinishSlots.Floor));
        }

        [Test]
        public void BrokenBuildValues_AreCaughtByTheMapCheck()
        {
            // 맵 목록 사본의 build 값을 하나씩 망가뜨리면 맵 검사가 그 문제를 낸다(정의·그림을 넘겼을 때만 보는 것 포함).
            InteractionData things = InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));
            List<string> Check(Action<MapBuildSettings> edit)
            {
                WorldCatalog catalog = WorldCatalog.Parse(File.ReadAllText(Path.Combine(Repo.Root, WorldCatalog.DefaultRelativePath)));
                MapEntry entry = catalog.Maps.Single(m => m.Build != null);
                edit(entry.Build);
                return MapChecks.Check(new WorldMapLoader(Repo.Root, catalog), things, Repo.Definitions, Art())
                    .Where(p => p.MapId == entry.Id).Select(p => p.Problem).ToList();
            }
            Assert.That(Check(b => { }), Is.Empty);
            Assert.That(Check(b => b.DefaultWall = b.DefaultFloor), Has.Some.StartsWith("짓는 집: 기본 벽 꾸밈"));
            Assert.That(Check(b => b.DefaultFloor = "finish.floor.no-such"), Has.Some.StartsWith("짓는 집: 기본 바닥 꾸밈"));
            Assert.That(Check(b => b.BandColors = "wall.top.no-such"), Has.Some.StartsWith("짓는 집: 띠 색 줄"));
            Assert.That(Check(b => b.Band = 8), Has.Some.StartsWith("짓는 집: 띠 두께"));
            Assert.That(Check(b => b.ReplacesLayers = new[] { "Wall", "NoSuchLayer" }), Has.Some.StartsWith("짓는 집: 바꿔 그릴 층 NoSuchLayer"));
        }

        [Test]
        public void LoadedHouseHasTilePlan_AndPictureSkipsReplacedLayers()
        {
            var (_, entry, map) = House();
            Assert.That(map.Tiles, Is.Not.Null, "짓는 집은 읽을 때 출처표를 만든다");
            Assert.That((map.Tiles.Grid.Columns, map.Tiles.Grid.Rows), Is.EqualTo((map.World.Grid.Columns, map.World.Grid.Rows)));
            Assert.That((map.Tiles.FaceHeight, map.Tiles.Band), Is.EqualTo((entry.Build.FaceHeight, entry.Build.Band)));
            int[] collision = Layer(map, "Collision");
            for (int i = 0; i < collision.Length; i++)
                Assert.That(map.Tiles.Grid.At(i % map.Tiles.Grid.Columns, i / map.Tiles.Grid.Columns), Is.EqualTo(collision[i]), "격자 = Collision 값");
            // 현관(벽 칸 위의 문)이 문지방으로 들어갔다.
            DoorArea door = map.World.Doors.Single();
            CellPos doorCell = CollisionInput.CellsUnder(door.Area, map.World.Grid.CellSize).Single(c => collision[map.World.Grid.Index(c.X, c.Y)] == Wall);
            Assert.That(map.Tiles.At(doorCell.X, doorCell.Y).Parts.Select(p => p.Source), Has.Some.EqualTo(Rootborn.Core.Building.TileSources.Sill));

            var replaced = new HashSet<string>(entry.Build.ReplacesLayers.Select(n => map.Level.Layer(n).TilesetPath));
            Assert.That(replaced, Is.Not.Empty);
            Assert.That(map.Picture.Under.Where(s => replaced.Contains(s.Texture)), Is.Empty, "Under에 바꿔 그릴 층의 타일이 없다");
            Assert.That(map.Picture.ReplacedAt, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(map.Picture.Under.Count));
            MapPicture full = MapPicture.From(map.Level);
            Assert.That(full.ReplacedAt, Is.EqualTo(-1), "건너뛰지 않으면 자리가 없다");
            int replacedTiles = entry.Build.ReplacesLayers.Sum(n => map.Level.Layer(n).Tiles.Count);
            Assert.That(full.Under.Count, Is.EqualTo(map.Picture.Under.Count + replacedTiles));

            // 진단(단언 아님): 맵을 읽을 때 한 번 드는 출처표 시간과 조합 시간(엔진 없음, 이 PC).
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int Runs = 10;
            for (int i = 0; i < Runs; i++) WorldMapLoader.Build(entry, map.Level);
            double load = watch.Elapsed.TotalMilliseconds / Runs;
            watch.Restart();
            for (int i = 0; i < Runs; i++) RoomTilePlan.Build(map.Tiles.Grid, map.Tiles.FaceHeight, map.Tiles.Band, null);
            double plan = watch.Elapsed.TotalMilliseconds / Runs;
            RoomFinishDefinition wall = Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultWall), floor = Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultFloor);
            var compositor = new RoomTileCompositor(Sheets(), Art());
            compositor.Compose(map.Tiles, _ => wall, _ => floor, wall.Sheet, entry.Build.BandColors); // 시트 읽기 데우기
            watch.Restart();
            for (int i = 0; i < Runs; i++) compositor.Compose(map.Tiles, _ => wall, _ => floor, wall.Sheet, entry.Build.BandColors);
            TestContext.Out.WriteLine($"맵 만들기(출처표 포함) {load:0.0}ms, 출처표만 {plan:0.0}ms, 조합 {watch.Elapsed.TotalMilliseconds / Runs:0.0}ms — {Runs}번 평균");
        }

        /// <summary>키트가 구운 그림: 바꿔 그릴 층의 타일을 그리는 순서(아래 층 먼저)로 칸마다 겹친다. 알파는 0·255만 받는다.</summary>
        private static Dictionary<CellPos, uint[]> Baked(LoadedMap map, IEnumerable<string> layers, Func<string, PieceImage> png)
        {
            const int size = RoomTilePlan.CellSize;
            var names = new HashSet<string>(layers);
            var cells = new Dictionary<CellPos, uint[]>();
            foreach (LdtkLayer layer in map.Level.Layers) // 아래 층 먼저(그리는 순서)
            {
                if (!names.Contains(layer.Name)) continue;
                Assert.That(layer.GridSize, Is.EqualTo(size), layer.Name);
                PieceImage sheet = png(layer.TilesetPath);
                foreach (LdtkTile t in layer.Tiles)
                {
                    Assert.That((t.Flip, t.Alpha, layer.Opacity), Is.EqualTo((0, 1f, 1f)), layer.Name + " 타일은 뒤집거나 비치지 않는다");
                    var cell = new CellPos((t.Px + layer.OffsetX) / size, (t.Py + layer.OffsetY) / size);
                    if (!cells.TryGetValue(cell, out uint[] px)) cells[cell] = px = new uint[size * size];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            uint p = sheet.Pixels[(t.SrcY + y) * sheet.Width + t.SrcX + x];
                            uint a = p >> 24;
                            Assert.That(a == 0 || a == 255, Is.True, layer.Name + " 반투명 픽셀 " + cell);
                            if (a == 255) px[y * size + x] = p;
                        }
                }
            }
            return cells;
        }

        private static uint[] CellOf(ComposedRoom room, int index)
        {
            const int size = RoomTilePlan.CellSize;
            var (_, sx, sy) = room.Cells[index];
            var px = new uint[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    uint p = room.Pixels[(sy + y) * room.Width + sx + x];
                    px[y * size + x] = (p >> 24) == 0 ? 0u : p; // 투명은 색을 보지 않는다
                }
            return px;
        }

        [Test]
        public void ComposedHouse_IsTheKitTiles_PixelForPixel_InEveryCell()
        {
            // 두 경로가 같은 그림을 내는지 본다(특정 색을 적지 않는다): 게임의 맵 읽기 → 출처표 + 기본 꾸밈 + 조각 시트로 조합한 칸 그림 =
            // 같은 칸에 LDtk의 Ground 타일 위에 Wall 타일을 얹은 그림(키트가 구워 방 폴더 tiles.png에 둔 것).
            var (_, entry, map) = House();
            MapBuildSettings build = entry.Build;
            RoomFinishDefinition wall = Repo.Definitions.Get<RoomFinishDefinition>(build.DefaultWall);
            RoomFinishDefinition floor = Repo.Definitions.Get<RoomFinishDefinition>(build.DefaultFloor);
            // 전제: 기본 꾸밈이 키트가 이 방을 구울 때 쓴 벽 모양·바닥·띠 색과 같다(방 폴더의 자리 표 tiles.slots.json에 남은 값).
            string slotsPath = Path.Combine(Repo.Root, Path.GetDirectoryName(entry.Ldtk), "tiles.slots.json");
            using (JsonDocument slots = JsonDocument.Parse(File.ReadAllText(slotsPath)))
            {
                JsonElement s = slots.RootElement;
                Assert.That((wall.Art, floor.Art, build.BandColors, build.FaceHeight, build.Band),
                    Is.EqualTo((s.GetProperty("wall").GetString(), s.GetProperty("floor").GetString(), s.GetProperty("top").GetString(),
                        s.GetProperty("face_height").GetInt32(), s.GetProperty("band").GetInt32())),
                    "전제: maps.json build의 기본 꾸밈(art)·띠 색·정면 높이·띠 두께 = 키트가 구운 방의 자리 표 " + slotsPath);
            }

            Func<string, PieceImage> png = Sheets();
            var compositor = new RoomTileCompositor(png, Art());
            ComposedRoom room = compositor.Compose(map.Tiles, _ => wall, _ => floor, wall.Sheet, build.BandColors);
            Assert.That(room.Problems, Is.Empty, string.Join("\n", room.Problems));
            Assert.That(room.TooLarge, Is.False);
            Dictionary<CellPos, uint[]> baked = Baked(map, build.ReplacesLayers, path => png(Path.GetRelativePath(Repo.Root, path)));

            var different = new List<string>();
            var composed = new HashSet<CellPos>();
            const int size = RoomTilePlan.CellSize;
            for (int i = 0; i < room.Cells.Count; i++)
            {
                CellPos cell = room.Cells[i].Cell;
                composed.Add(cell);
                uint[] got = CellOf(room, i);
                uint[] want = baked.TryGetValue(cell, out uint[] b) ? b : new uint[size * size];
                for (int p = 0; p < got.Length; p++)
                    if (got[p] != want[p]) { different.Add(cell + " 첫 다른 픽셀 (" + p % size + ", " + p / size + ") 조합 " + got[p].ToString("X8") + " / 구운 것 " + want[p].ToString("X8")); break; }
            }
            foreach (CellPos cell in baked.Keys)
                if (!composed.Contains(cell) && baked[cell].Any(p => p != 0)) different.Add(cell + " 구운 그림이 있는데 조합 칸이 없다");
            TestContext.Out.WriteLine($"조합 칸 {room.Cells.Count}, 서로 다른 칸 그림 {room.UniqueTiles}, 구운 칸 {baked.Count}, 다른 칸 {different.Count}");
            Assert.That(room.Cells.Count, Is.EqualTo(map.Tiles.Cells.Count), "밖이 아닌 칸마다 하나");
            Assert.That(different, Is.Empty, "조합 = 구운 그림이어야 한다:\n" + string.Join("\n", different.Take(20)));
            Assert.That(room.UniqueTiles, Is.LessThan(room.Cells.Count), "같은 조합은 한 칸만 만든다");
        }

        [Test]
        public void TwoFloorFinishes_EachSideOfTheVerticalWallBandShowsItsOwnRoomsFloor()
        {
            // 집의 두 방 사이 세로 벽(띠 칸의 바닥 주인이 왼쪽·오른쪽 둘)에서, 왼쪽 방과 오른쪽 방에 다른 바닥 꾸밈을 주면 띠 왼쪽 비침은 왼쪽 방 바닥 조각,
            // 오른쪽 비침은 오른쪽 방 바닥 조각의 같은 자리 픽셀이다(출처로 확인 — 두 조각이 그 자리에서 다르다는 것도 본다).
            var (_, entry, map) = House();
            RoomFinishDefinition wall = Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultWall);
            RoomFinishDefinition left = Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultFloor);
            RoomFinishDefinition right = Repo.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == RoomFinishSlots.Floor && f.Id != left.Id);
            CellTiles band = map.Tiles.Cells.First(c =>
                c.Parts.Where(p => p.Source == Rootborn.Core.Building.TileSources.Floor).Select(p => p.Owner).Distinct().OrderBy(o => o.X).SequenceEqual(new[] { new CellPos(-1, 0), new CellPos(1, 0) })
                && c.Parts.Any(p => p.Source == Rootborn.Core.Building.TileSources.Band)
                && c.Parts.Where(p => p.Source == Rootborn.Core.Building.TileSources.Floor).All(p => p.Shade == "floor")); // 그림자 없는 띠 칸(바닥 조각 한 장만 본다)
            int splitX = band.Cell.X;
            Func<string, PieceImage> png = Sheets();
            RoomFinishArt art = Art();
            ComposedRoom room = new RoomTileCompositor(png, art).Compose(map.Tiles, _ => wall, c => c.X < splitX ? left : right, wall.Sheet, entry.Build.BandColors);
            Assert.That(room.Problems, Is.Empty, string.Join("\n", room.Problems));
            int index = Enumerable.Range(0, room.Cells.Count).Single(i => room.Cells[i].Cell.Equals(band.Cell));
            uint[] got = CellOf(room, index);

            uint Piece(RoomFinishDefinition f, int x, int y)
            {
                Assert.That(art.TryPiece(f, "floor", out FinishPiece piece), Is.True);
                PieceImage sheet = png(piece.Texture);
                return sheet.Pixels[(piece.Y + y) * sheet.Width + piece.X + x];
            }
            int checkedLeft = 0, checkedRight = 0, differ = 0;
            foreach (TilePart part in band.Parts.Where(p => p.Source == Rootborn.Core.Building.TileSources.Floor))
                foreach (PixelRect r in part.Rects)
                    for (int y = r.Y0; y < r.Y1; y++)
                        for (int x = r.X0; x < r.X1; x++)
                        {
                            RoomFinishDefinition want = part.Owner.X < 0 ? left : right;
                            Assert.That(got[y * RoomTilePlan.CellSize + x], Is.EqualTo(Piece(want, x, y)), band.Cell + " (" + x + ", " + y + ") 주인 " + part.Owner);
                            if (Piece(left, x, y) != Piece(right, x, y)) differ++;
                            if (part.Owner.X < 0) checkedLeft++; else checkedRight++;
                        }
            Assert.That((checkedLeft > 0, checkedRight > 0), Is.EqualTo((true, true)), "띠 양옆 모두 바닥이 비친다");
            Assert.That(differ, Is.GreaterThan(0), "두 바닥 조각이 같으면 이 시험은 아무것도 보지 않는다");
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
