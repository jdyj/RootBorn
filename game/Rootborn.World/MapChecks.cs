using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
using Rootborn.Core.World;
using Rootborn.Data;
using Rootborn.World.Ldtk;

namespace Rootborn.World
{
    /// <summary>맵 검사에서 걸린 것 하나: 어느 맵, 무엇이.</summary>
    public readonly struct MapCheckProblem
    {
        public readonly string MapId, Problem;

        public MapCheckProblem(string mapId, string problem)
        {
            MapId = mapId ?? string.Empty;
            Problem = problem ?? string.Empty;
        }

        public override string ToString() => MapId + ": " + Problem;
    }

    /// <summary>걸어서 닿을 수 있는 (맵, 도착 지점) 하나.</summary>
    public readonly struct WorldPlace : IEquatable<WorldPlace>
    {
        public readonly string MapId, SpawnId;

        public WorldPlace(string mapId, string spawnId)
        {
            MapId = mapId ?? string.Empty;
            SpawnId = spawnId ?? string.Empty;
        }

        public bool Equals(WorldPlace other) => MapId == other.MapId && SpawnId == other.SpawnId;
        public override bool Equals(object obj) => obj is WorldPlace other && Equals(other);
        public override int GetHashCode() => MapId.GetHashCode() ^ (SpawnId.GetHashCode() * 31);
        public override string ToString() => MapId + "/" + SpawnId;
    }

    /// <summary>
    /// 맵 목록의 모든 맵에 대해 도는 검사(game/world/README.md "새 맵 검사"). 맵 이름을 모른다 — 맵 목록과 상호작용 파일만 본다.
    /// - 문 엔티티의 값이 문 경로 표(maps.json doors)에 있다(없으면 열리지 않는다).
    /// - 목적지가 있는 문: 목적지 맵이 맵 목록에 있고 그 맵에 도착 지점이 있다. 그 맵의 시작 도착 지점(맵 목록의 첫 도착 지점)에서 걸어서 닿는다.
    /// - 도착 지점: 걸을 수 있는 칸 위에 있다.
    /// - 상호작용 물건과 NPC 자리: 시작 도착 지점에서 걸어서 닿는 칸이거나 그 옆 칸(8방향)에 있다.
    /// - 그림: 가리키는 타일셋 PNG가 저장소에 있다(맵을 읽으며 모은 문제).
    /// - 부지: 건물 목록에 있는 건물, 아는 단계 종류, 맵 안·서로 겹치지 않는 부지, 부지 안에 드는 건물, 칸 크기의 배수인 그림, 막는 칸 안의 문 칸.
    ///   가구 방에는 부지를 두지 않는다. 부지의 문도 위의 문 검사를 받는다.
    /// - 짓는 집(build 성질이 있는 맵)만: Build 층이 있고 맵과 같은 크기, 값은 0·1. 도착 지점·상호작용·NPC 자리·문 앞 칸은 바닥이고 Build 0.
    /// "걸어서 닿는다"는 Core GridPath(칸 단위 상하좌우)로 판정한다. 문은 그 상자에 들어가거나 맞닿은 열린 칸까지 가면 닿은 것이다.
    /// </summary>
    public static class MapChecks
    {
        /// <param name="definitions">있으면 짓는 집의 기본 꾸밈이 데이터에 있는 벽·바닥 꾸밈인지 본다.</param>
        /// <param name="art">정의와 함께 있으면 띠 색 줄과 띠 두께가 기본 벽 꾸밈 시트의 좌표표와 맞는지 본다.</param>
        public static List<MapCheckProblem> Check(WorldMapLoader loader, InteractionData interactions, GameDefinitions definitions = null, RoomFinishArt art = null)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            var problems = new List<MapCheckProblem>();
            foreach (MapEntry entry in loader.Catalog.Maps)
            {
                LoadedMap loaded;
                try { loaded = loader.LoadMap(entry.Id); }
                catch (Exception e) { problems.Add(new MapCheckProblem(entry.Id, "맵을 읽지 못했다: " + e.Message)); continue; }
                CheckMap(loader, loaded, interactions?.Map(entry.Id) ?? MapInteractions.Empty, problems);
                if (entry.Build != null) CheckBuildArt(loaded, definitions, art, problems);
            }
            if (interactions != null)
                foreach (string mapId in interactions.MapIds)
                    if (loader.Catalog.Find(mapId) == null) problems.Add(new MapCheckProblem(mapId, "상호작용 파일의 맵이 맵 목록에 없다"));
            return problems;
        }

        private static void CheckMap(WorldMapLoader loader, LoadedMap loaded, MapInteractions things, List<MapCheckProblem> problems)
        {
            WorldMap map = loaded.World;
            string id = map.Id;
            int size = map.Grid.CellSize;
            foreach (string p in loaded.Problems) problems.Add(new MapCheckProblem(id, "그림·층: " + p));
            foreach (string p in loaded.LotProblems) problems.Add(new MapCheckProblem(id, "부지: " + p));
            // 가구 방(FurnitureRoom)은 처음 읽은 맵을 기억해 두어 단계가 바뀐 건물을 따라가지 못한다. 지금은 지원하지 않는다고 알린다.
            if (loaded.Entry.Lots.Count > 0 && !string.IsNullOrEmpty(things.PlacementSurface))
                problems.Add(new MapCheckProblem(id, "부지: 가구 방(placementSurface " + things.PlacementSurface + ")에는 건물 부지를 둘 수 없다(지원하지 않는다)"));

            foreach (string spawn in map.SpawnIds)
            {
                map.TryGetSpawn(spawn, out var point);
                if (map.Collision.IsBlocked(CellPos.Containing(point.X, point.Y, size)))
                    problems.Add(new MapCheckProblem(id, "도착 지점 " + spawn + "이 막힌 칸이다"));
            }

            string start = StartSpawn(loaded.Entry);
            if (start == null) { problems.Add(new MapCheckProblem(id, "도착 지점이 하나도 없다")); return; }
            HashSet<CellPos> reached = Reachable(map, start);

            foreach (DoorArea door in map.Doors)
            {
                if (!door.Destination.HasValue)
                {
                    CellPos at = CellPos.Containing(door.Area.CenterX, door.Area.CenterY, size);
                    problems.Add(new MapCheckProblem(id, "문 '" + door.Target + "' " + at + "의 값이 문 경로 표(doors)에 없어 열리지 않는다"));
                    continue;
                }
                TravelTarget to = door.Destination.Value;
                MapEntry target = loader.Catalog.Find(to.MapId);
                if (target == null) problems.Add(new MapCheckProblem(id, "문 '" + door.Target + "'의 목적지 맵 " + to.MapId + "이 맵 목록에 없다"));
                else if (!target.Spawns.ContainsKey(to.SpawnId)) problems.Add(new MapCheckProblem(id, "문 '" + door.Target + "'의 목적지 " + to + " 도착 지점이 없다"));
                if (!Touches(reached, door.Area, size))
                    problems.Add(new MapCheckProblem(id, "문 '" + door.Target + "'에 시작 도착 지점 " + start + "에서 걸어서 닿지 않는다"));
            }

            foreach (InteractionThing t in things.Things)
                if (!NextTo(reached, t.X, t.Y, size))
                    problems.Add(new MapCheckProblem(id, "상호작용 " + t.Kind + "/" + t.Id + "이 걸어서 닿는 칸 옆에 없다"));
            foreach (var anchors in things.NpcAnchors)
                for (int i = 0; i < anchors.Value.Count; i++)
                    if (!NextTo(reached, anchors.Value[i].X, anchors.Value[i].Y, size))
                        problems.Add(new MapCheckProblem(id, "NPC 자리 " + anchors.Key + "#" + i + "이 걸어서 닿는 칸 옆에 없다"));

            if (loaded.Entry.Build != null) CheckBuild(loaded, things, reached, problems);
        }

        /// <summary>
        /// 짓는 집(맵 항목의 build)만: Build 층이 있고 맵과 같은 크기이며 값은 0·1이다. 도착 지점·상호작용·NPC 자리가 든 칸과 문 앞
        /// (문 상자 칸과 그 상하좌우 중 걸어서 닿는 바닥 칸)은 바닥이고 Build 0(고정 — 지어서 막을 수 없다)이다.
        /// </summary>
        private static void CheckBuild(LoadedMap loaded, MapInteractions things, HashSet<CellPos> reached, List<MapCheckProblem> problems)
        {
            WorldMap map = loaded.World;
            string id = map.Id, layerName = loaded.Entry.Build.FrameLayer;
            MapGrid grid = map.Grid;
            int size = grid.CellSize;
            LdtkLayer layer = loaded.Level?.Layer(layerName);
            if (layer == null || layer.Type != "IntGrid") { problems.Add(new MapCheckProblem(id, "짓는 집: Build 층 " + layerName + "이 없다")); return; }
            if (layer.IntGrid == null || layer.GridSize != size || layer.IntGrid.Length != grid.Count)
            {
                problems.Add(new MapCheckProblem(id, "짓는 집: Build 층 크기가 맵과 다르다"));
                return;
            }
            int shift = loaded.Entry.EntityRowShift; // 칠한 Collision 층과 같은 줄 밀기
            int Value(CellPos c)
            {
                int y = c.Y - shift;
                return grid.Contains(c.X, c.Y) && y >= 0 && y < grid.Rows ? layer.IntGrid[grid.Index(c.X, y)] : 0;
            }
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                {
                    int v = Value(new CellPos(x, y));
                    if (v != 0 && v != 1) problems.Add(new MapCheckProblem(id, "짓는 집: Build 값은 0·1이어야 한다 " + new CellPos(x, y)));
                }

            void Fixed(string what, CellPos c)
            {
                if (map.Collision.IsBlocked(c)) problems.Add(new MapCheckProblem(id, "짓는 집: " + what + " " + c + "이 바닥이 아니다"));
                else if (Value(c) == 1) problems.Add(new MapCheckProblem(id, "짓는 집: " + what + " " + c + "이 Build 1이다"));
            }
            foreach (string spawn in map.SpawnIds)
            {
                map.TryGetSpawn(spawn, out var point);
                Fixed("도착 지점 " + spawn, CellPos.Containing(point.X, point.Y, size));
            }
            foreach (InteractionThing t in things.Things) Fixed("상호작용 " + t.Kind + "/" + t.Id, CellPos.Containing(t.X, t.Y, size));
            foreach (var anchors in things.NpcAnchors)
                for (int i = 0; i < anchors.Value.Count; i++)
                    Fixed("NPC 자리 " + anchors.Key + "#" + i, CellPos.Containing(anchors.Value[i].X, anchors.Value[i].Y, size));
            foreach (DoorArea door in map.Doors)
            {
                var front = new HashSet<CellPos>();
                foreach (CellPos c in CollisionInput.CellsUnder(door.Area, size))
                    foreach (var (dx, dy) in Around)
                    {
                        var n = new CellPos(c.X + dx, c.Y + dy);
                        if (grid.Contains(n.X, n.Y) && reached.Contains(n)) front.Add(n);
                    }
                foreach (CellPos c in front) Fixed("문 앞 '" + door.Target + "'", c);
            }
        }

        /// <summary>
        /// 짓는 집의 조합 그리기: 바꿔 그릴 층이 레벨의 타일 층으로 있고(앞뒤를 가리는 ysort 층은 안 된다 — 조합 칸은 캐릭터 아래에 깐다),
        /// 출처표가 만들어졌다. 정의가 있으면 기본 벽·바닥 꾸밈이 그 자리의 꾸밈으로 있고, 그림(좌표표)도 있으면 띠 색 줄이 기본 벽 꾸밈 시트에 있고
        /// 시트의 띠 두께가 build.band와 같다(조각이 그 두께로 그려졌다).
        /// </summary>
        private static void CheckBuildArt(LoadedMap loaded, GameDefinitions definitions, RoomFinishArt art, List<MapCheckProblem> problems)
        {
            string id = loaded.Entry.Id;
            MapBuildSettings build = loaded.Entry.Build;
            foreach (string name in build.ReplacesLayers)
            {
                LdtkLayer layer = loaded.Level?.Layer(name);
                if (layer == null || layer.Type != "Tiles") problems.Add(new MapCheckProblem(id, "짓는 집: 바꿔 그릴 층 " + name + "이 레벨의 타일 층으로 없다"));
                else if (layer.Tags.Contains(MapPicture.DepthSortTag)) problems.Add(new MapCheckProblem(id, "짓는 집: 바꿔 그릴 층 " + name + "이 앞뒤를 가리는 층(" + MapPicture.DepthSortTag + ")이다"));
            }
            CheckBetweenReplaced(loaded, problems);
            if (loaded.Tiles == null) problems.Add(new MapCheckProblem(id, "짓는 집: 칸마다 그릴 출처표를 만들지 못했다(LDtk 층을 그린다)"));
            if (definitions == null) return;
            RoomFinishDefinition wall = Finish(definitions, build.DefaultWall, RoomFinishSlots.Wall, "기본 벽 꾸밈", id, problems);
            Finish(definitions, build.DefaultFloor, RoomFinishSlots.Floor, "기본 바닥 꾸밈", id, problems);
            if (art == null || wall == null) return;
            try
            {
                if (!art.TryBand(wall.Sheet, build.BandColors, out _))
                    problems.Add(new MapCheckProblem(id, "짓는 집: 띠 색 줄 " + build.BandColors + "이 시트 " + wall.Sheet + "의 좌표표에 없다"));
                int band = art.BandOf(wall.Sheet);
                if (band != build.Band)
                    problems.Add(new MapCheckProblem(id, "짓는 집: 띠 두께 " + build.Band + "px가 시트 " + wall.Sheet + "의 좌표표 band " + band + "px와 다르다"));
                if (loaded.Tiles != null) CheckFinishPieces(id, loaded.Tiles, definitions, art, problems);
            }
            catch (Exception e) when (RoomTileCompositor.IsAtlasReadError(e))
            {
                problems.Add(new MapCheckProblem(id, "짓는 집: 시트 " + wall.Sheet + "의 좌표표를 읽지 못했다: " + e.Message));
            }
        }

        /// <summary>
        /// 이 맵의 출처표가 쓰는 조각을 고를 수 있는 모든 꾸밈이 갖고 있다: 벽 자리 꾸밈마다 정면·좁은 정면 조각 이름 전부, 바닥 자리 꾸밈마다
        /// floor(와 그림자를 쓰면 floor:shade). 플레이어가 어느 꾸밈을 고르든 조합이 빈 픽셀로 무너지지 않게(예: 정면 높이 3인 맵은 face:3 조각을
        /// 부르는데 시트에는 face:1·face:2뿐이다). 맵마다 (꾸밈, 조각) 하나에 문제 하나.
        /// </summary>
        private static void CheckFinishPieces(string id, RoomTilePlan plan, GameDefinitions definitions, RoomFinishArt art, List<MapCheckProblem> problems)
        {
            var wallPieces = new SortedSet<string>(StringComparer.Ordinal);
            var floorPieces = new SortedSet<string>(StringComparer.Ordinal);
            foreach (CellTiles cell in plan.Cells)
                foreach (TilePart part in cell.Parts)
                {
                    if (part.Source == TileSources.Face || part.Source == TileSources.Narrow) wallPieces.Add(part.Piece);
                    else if (part.Source == TileSources.Floor)
                        floorPieces.Add(part.Shade.StartsWith(RoomTileCompositor.ShadePiece, StringComparison.Ordinal) ? RoomTileCompositor.ShadePiece : RoomTileCompositor.FloorPiece);
                }
            if (floorPieces.Count > 0) floorPieces.Add(RoomTileCompositor.FloorPiece); // 그림자 범위 밖 픽셀은 floor 조각에서 온다
            foreach (RoomFinishDefinition finish in definitions.All<RoomFinishDefinition>())
            {
                SortedSet<string> wanted = finish.Slot == RoomFinishSlots.Wall ? wallPieces : finish.Slot == RoomFinishSlots.Floor ? floorPieces : null;
                if (wanted == null) continue;
                foreach (string piece in wanted)
                    if (!art.TryPiece(finish, piece, out _))
                        problems.Add(new MapCheckProblem(id, "짓는 집: 꾸밈 " + finish.Id + "에 출처표가 쓰는 조각 " + piece + "이 없다"));
            }
        }

        /// <summary>
        /// 바꿔 그릴 층들 사이(그리기 순서)에 그려지는 다른 층이 없다: 조합한 벽·바닥은 가장 아래 바꿔 그릴 층 자리에 한 번에 그려지므로,
        /// 사이에 낀 층(보이는 타일, art 태그 층의 그림 엔티티)은 원래 벽 아래였어도 벽 위로 올라간다. 앞뒤를 가리는(ysort) 층은 원래 캐릭터와 함께 그려져 상관없다.
        /// </summary>
        private static void CheckBetweenReplaced(LoadedMap loaded, List<MapCheckProblem> problems)
        {
            List<LdtkLayer> layers = loaded.Level?.Layers;
            if (layers == null) return;
            IReadOnlyList<string> replaced = loaded.Entry.Build.ReplacesLayers;
            int first = -1, last = -1;
            for (int i = 0; i < layers.Count; i++)
                if (Named(replaced, layers[i].Name)) { if (first < 0) first = i; last = i; }
            for (int i = first + 1; i < last; i++)
            {
                LdtkLayer layer = layers[i];
                if (Named(replaced, layer.Name) || !layer.Visible || layer.Tags.Contains(MapPicture.DepthSortTag)) continue;
                bool drawn = layer.Tiles.Count > 0;
                if (!drawn && layer.Tags.Contains(MapPicture.ArtTag))
                    foreach (LdtkEntity e in layer.Entities) if (e.HasTile) { drawn = true; break; }
                if (drawn)
                    problems.Add(new MapCheckProblem(loaded.Entry.Id, "짓는 집: 바꿔 그릴 층 사이에 그려지는 층 " + layer.Name
                        + "이 있다(조합한 벽·바닥은 한 번에 그려져 이 층이 그 위로 올라간다 — 바꿔 그릴 층 위나 아래로 옮긴다)"));
            }
        }

        private static bool Named(IReadOnlyList<string> names, string name)
        {
            foreach (string n in names) if (string.Equals(n, name, StringComparison.Ordinal)) return true;
            return false;
        }

        private static RoomFinishDefinition Finish(GameDefinitions definitions, string finishId, string slot, string what, string mapId, List<MapCheckProblem> problems)
        {
            if (!definitions.TryGet(finishId ?? string.Empty, out RoomFinishDefinition finish))
            {
                problems.Add(new MapCheckProblem(mapId, "짓는 집: " + what + " " + finishId + "이 데이터에 없다"));
                return null;
            }
            if (finish.Slot != slot)
            {
                problems.Add(new MapCheckProblem(mapId, "짓는 집: " + what + " " + finishId + "은 " + finish.Slot + " 자리 꾸밈이다(" + slot + "여야 한다)"));
                return null;
            }
            return finish;
        }

        private static readonly (int Dx, int Dy)[] Around = { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>맵의 시작 도착 지점: 맵 목록에 처음 적힌 도착 지점(--world로 띄울 때 서는 곳). 없으면 null.</summary>
        public static string StartSpawn(MapEntry entry)
        {
            foreach (string id in entry.Spawns.Keys) return id;
            return null;
        }

        /// <summary>도착 지점에서 걸어서 닿는 칸 전부(그 칸 포함). 도착 지점이 막혔으면 빈 집합.</summary>
        public static HashSet<CellPos> Reachable(WorldMap map, string spawnId)
        {
            if (!map.TryGetSpawn(spawnId, out var point)) return new HashSet<CellPos>();
            return MapReach.From(map.Collision, CellPos.Containing(point.X, point.Y, map.Grid.CellSize));
        }

        /// <summary>닿은 칸 중 하나가 상자에 겹치거나 맞닿는가(Unity 판 물리의 접촉 여유처럼 1픽셀 넓혀 본다).</summary>
        public static bool Touches(HashSet<CellPos> reached, AreaRect area, int cellSize) => MapReach.Touches(reached, area, cellSize);

        /// <summary>점이 든 칸이나 그 둘레 8칸 중 하나에 걸어서 닿는가.</summary>
        public static bool NextTo(HashSet<CellPos> reached, float x, float y, int cellSize)
        {
            CellPos at = CellPos.Containing(x, y, cellSize);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (reached.Contains(new CellPos(at.X + dx, at.Y + dy))) return true;
            return false;
        }

        /// <summary>
        /// 한 자리에서 걸어서 갈 수 있는 모든 (맵, 도착 지점). 걸어서 닿는 문의 목적지로 넘어가며 넓힌다.
        /// 맵을 읽지 못하거나 도착 지점이 없는 목적지는 넘어가지 않는다.
        /// </summary>
        public static List<WorldPlace> Walk(WorldMapLoader loader, WorldPlace from)
        {
            var seen = new HashSet<WorldPlace> { from };
            var order = new List<WorldPlace> { from };
            var queue = new Queue<WorldPlace>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                WorldPlace place = queue.Dequeue();
                WorldMap map;
                try { map = loader.LoadMap(place.MapId).World; }
                catch (InvalidOperationException) { continue; }
                HashSet<CellPos> reached = Reachable(map, place.SpawnId);
                foreach (DoorArea door in map.Doors)
                {
                    if (!door.Destination.HasValue || !Touches(reached, door.Area, map.Grid.CellSize)) continue;
                    TravelTarget to = door.Destination.Value;
                    MapEntry target = loader.Catalog.Find(to.MapId);
                    if (target == null || !target.Spawns.ContainsKey(to.SpawnId)) continue;
                    var next = new WorldPlace(target.Id, to.SpawnId);
                    if (seen.Add(next)) { order.Add(next); queue.Enqueue(next); }
                }
            }
            return order;
        }
    }
}
