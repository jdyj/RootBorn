using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.Dialogue;
using Rootborn.Core.StudentLife;
using Rootborn.Core.World;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.World;

namespace Rootborn.Simulation
{
    /// <summary>
    /// 플레이어가 머릿속에 그리는 지도: 맵과 문으로 이어진 길, 맵마다 있는 상호작용 지점, NPC가 언제 어느 장소에 서는지(데이터의 일정).
    /// 맵 id·지점 id는 데이터에서 읽은 값 그대로이고 코드는 특정 값을 모른다. 정의와 맵은 프로세스에서 한 번만 읽는다.
    /// </summary>
    public sealed class PlayAtlas
    {
        private static readonly Dictionary<string, PlayAtlas> Cache = new Dictionary<string, PlayAtlas>(StringComparer.OrdinalIgnoreCase);

        public readonly string RepoRoot;
        public readonly GameDefinitions Definitions;
        public readonly WorldMapLoader Maps;
        public readonly InteractionData Interactions;
        private readonly Dictionary<string, List<(string Door, string To)>> _doors = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);
        private readonly NpcScheduleLookupCache _schedules;
        private readonly NpcScheduleAccess _access;

        private PlayAtlas(string repoRoot)
        {
            RepoRoot = repoRoot;
            LoadResult loaded = GameDataLoader.Load(Path.Combine(repoRoot, "data"), RootbornBuilders.Register);
            loaded.ThrowIfFailed();
            Definitions = loaded.Definitions;
            Maps = new WorldMapLoader(repoRoot, WorldCatalog.Load(Path.Combine(repoRoot, WorldCatalog.DefaultRelativePath)));
            Interactions = InteractionData.Load(Path.Combine(repoRoot, InteractionData.DefaultRelativePath));
            foreach (MapEntry entry in Maps.Catalog.Maps)
            {
                var list = new List<(string, string)>();
                foreach (DoorArea door in Maps.LoadMap(entry.Id).World.Doors)
                    if (door.Destination.HasValue) list.Add((door.Target, door.Destination.Value.MapId));
                _doors[entry.Id] = list;
            }
            _schedules = new NpcScheduleLookupCache(Definitions.All<NpcSchedule>());
            _access = new NpcScheduleAccess(_schedules);
        }

        public static PlayAtlas Load(string repoRoot)
        {
            string key = Path.GetFullPath(repoRoot);
            lock (Cache)
            {
                if (!Cache.TryGetValue(key, out PlayAtlas atlas)) Cache[key] = atlas = new PlayAtlas(key);
                return atlas;
            }
        }

        public IEnumerable<string> MapIds => _doors.Keys;

        /// <summary>
        /// from에서 goal을 만족하는 맵까지 가는 길의 첫 문(문 목적지 값). 이미 그 맵이면 빈 글자, 길이 없으면 null.
        /// </summary>
        public string FirstDoorToward(string from, Func<string, bool> goal)
        {
            if (string.IsNullOrEmpty(from) || goal == null) return null;
            if (goal(from)) return string.Empty;
            var firstDoor = new Dictionary<string, string>(StringComparer.Ordinal) { [from] = null };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string map = queue.Dequeue();
                if (!_doors.TryGetValue(map, out var doors)) continue;
                foreach (var (door, to) in doors)
                {
                    if (string.IsNullOrEmpty(to) || firstDoor.ContainsKey(to)) continue;
                    string first = firstDoor[map] ?? door;
                    firstDoor[to] = first;
                    if (goal(to)) return first;
                    queue.Enqueue(to);
                }
            }
            return null;
        }

        /// <summary>그 맵에 그 종류(·id)의 상호작용 지점이 있다. id가 null이면 종류만 본다.</summary>
        public bool HasThing(string mapId, string kind, string id)
        {
            foreach (InteractionThing t in Interactions.Map(mapId).Things)
                if ((kind == null || t.Kind == kind) && (id == null || t.Id == id)) return true;
            return false;
        }

        /// <summary>그 맵에 그 id의 지점이 있거나, 그 NPC가 그 날·시각에 그 맵에 선다.</summary>
        public bool Holds(string mapId, string subjectId, int day, float hour)
        {
            if (HasThing(mapId, null, subjectId)) return true;
            string location = NpcLocation(subjectId, day, hour);
            return location != null && Interactions.Map(mapId).NpcAnchors.ContainsKey(location);
        }

        /// <summary>NPC가 그 날·시각에 서는 장소 id(일정이 없으면 null).</summary>
        public string NpcLocation(string npcId, int day, float hour)
        {
            if (string.IsNullOrEmpty(npcId) || !_schedules.TryGetByNpcId(npcId, out NpcSchedule schedule)) return null;
            string slot = LocationStateTimeSlots.FromMinutes((int)(hour * 60f));
            string location = _access.ResolveLocationId(schedule.Id, day, slot, null);
            return string.IsNullOrEmpty(location) ? null : location;
        }

        /// <summary>모든 맵의 장소 지점 id(location-visit·location-activity처럼 장소를 여는 지점). 종류 목록은 부르는 쪽이 준다.</summary>
        public IEnumerable<string> ThingIds(ICollection<string> kinds)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string map in _doors.Keys)
                foreach (InteractionThing t in Interactions.Map(map).Things)
                    if (kinds.Contains(t.Kind) && seen.Add(t.Id)) yield return t.Id;
        }

        /// <summary>데이터의 모든 상호작용 종류(지도 지점 + NPC).</summary>
        public HashSet<string> AllInteractionKinds()
        {
            var kinds = new HashSet<string>(StringComparer.Ordinal) { "npc" };
            foreach (string map in _doors.Keys)
                foreach (InteractionThing t in Interactions.Map(map).Things) kinds.Add(t.Kind);
            return kinds;
        }

        /// <summary>저장소 뿌리(data/registry.json이 있는 곳)를 실행 파일 위치에서 위로 올라가며 찾는다.</summary>
        public static string FindRepoRoot()
        {
            string fromEnv = Environment.GetEnvironmentVariable("ROOTBORN_REPO_ROOT");
            if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "data", "registry.json"))) return dir.FullName;
            throw new DirectoryNotFoundException("data/registry.json을 찾지 못했다");
        }
    }
}
