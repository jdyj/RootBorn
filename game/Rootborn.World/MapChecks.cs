using System;
using System.Collections.Generic;
using Rootborn.Core.World;

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
    /// "걸어서 닿는다"는 Core GridPath(칸 단위 상하좌우)로 판정한다. 문은 그 상자에 들어가거나 맞닿은 열린 칸까지 가면 닿은 것이다.
    /// </summary>
    public static class MapChecks
    {
        public static List<MapCheckProblem> Check(WorldMapLoader loader, InteractionData interactions)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            var problems = new List<MapCheckProblem>();
            foreach (MapEntry entry in loader.Catalog.Maps)
            {
                LoadedMap loaded;
                try { loaded = loader.LoadMap(entry.Id); }
                catch (Exception e) { problems.Add(new MapCheckProblem(entry.Id, "맵을 읽지 못했다: " + e.Message)); continue; }
                CheckMap(loader, loaded, interactions?.Map(entry.Id) ?? MapInteractions.Empty, problems);
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
        }

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
