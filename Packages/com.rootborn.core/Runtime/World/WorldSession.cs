using System;
using System.Collections.Generic;

namespace Rootborn.Core.World
{
    /// <summary>걸어 다닐 수 있는 맵 하나: 막힌 칸, 문, 도착 지점(픽셀 좌표). 그림은 모른다.</summary>
    public sealed class WorldMap
    {
        public readonly string Id;
        public readonly CollisionGrid Collision;
        public readonly IReadOnlyList<DoorArea> Doors;
        private readonly Dictionary<string, (float X, float Y)> _spawns;

        public WorldMap(string id, CollisionGrid collision, IReadOnlyList<DoorArea> doors, IDictionary<string, (float X, float Y)> spawns)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Collision = collision ?? throw new ArgumentNullException(nameof(collision));
            Doors = doors ?? Array.Empty<DoorArea>();
            _spawns = new Dictionary<string, (float X, float Y)>(spawns ?? new Dictionary<string, (float X, float Y)>(), StringComparer.Ordinal);
        }

        public MapGrid Grid => Collision.Grid;
        public IEnumerable<string> SpawnIds => _spawns.Keys;
        public bool TryGetSpawn(string spawnId, out (float X, float Y) point) => _spawns.TryGetValue(spawnId ?? string.Empty, out point);
    }

    /// <summary>맵 id로 맵을 만들어 주는 곳(파일 읽기는 바깥에서 한다).</summary>
    public interface IWorldMapSource
    {
        WorldMap Load(string mapId);
    }

    /// <summary>
    /// 지금 있는 맵과 걷는 몸. 프레임마다 입력 방향과 dt를 받아 걷고, 문이 열리면 목적지 맵의 도착 지점으로 옮긴다.
    /// 새 맵을 불러올 수 없거나 도착 지점이 없으면 옮기지 않고 그 자리에 둔다.
    /// </summary>
    public sealed class WorldSession
    {
        private readonly IWorldMapSource _source;
        private readonly WalkerSettings _settings;
        private DoorWatch _doors;

        public WorldMap Map { get; private set; }
        public Walker Walker { get; private set; }
        public int Travels { get; private set; }
        public string LastProblem { get; private set; }

        /// <summary>도착을 기록하는 곳(세이브 슬롯). 없으면 기록하지 않는다. 기록이 거절되면 그 맵으로 옮기지 않는다.</summary>
        public IWorldArrivals Arrivals { get; set; }

        public WorldSession(IWorldMapSource source, WalkerSettings settings)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public DoorArea OpeningDoor => _doors?.Opening;

        /// <summary>
        /// 맵에 들어간다. 도착 지점 id가 없거나 맵에 없거나 도착 기록이 거절되면 들어가지 않는다(지금 상태는 그대로).
        /// displayName은 도착 기록에 남는 문 이름(Unity LastPortalDisplayName).
        /// </summary>
        public bool Enter(string mapId, string spawnId, string displayName = null)
        {
            WorldMap map;
            try { map = _source.Load(mapId); }
            catch (Exception e) { LastProblem = "맵 " + mapId + "을 불러오지 못했다: " + e.Message; return false; }
            if (map == null) { LastProblem = "맵이 없다: " + mapId; return false; }
            if (!map.TryGetSpawn(spawnId, out var point)) { LastProblem = "맵 " + mapId + "에 도착 지점 " + spawnId + "이 없다"; return false; }
            if (Arrivals != null && !Arrivals.Record(map.Id, spawnId, displayName)) { LastProblem = Arrivals.LastProblem ?? "도착을 기록하지 못했다"; return false; }
            Facing facing = Walker?.Facing ?? Facing.Down;
            Map = map;
            Walker = new Walker(_settings, map.Grid.CellSize);
            Walker.PlaceAt(point);
            Walker.Face(facing);
            _doors = new DoorWatch(map.Doors, DoorWatch.UnityOpeningSeconds, DoorWatch.UnityContactSkinCells * map.Grid.CellSize);
            _doors.Reset(Walker.Box);
            LastProblem = null;
            return true;
        }

        /// <summary>한 프레임. 문을 지나 맵이 바뀌었으면 true.</summary>
        public bool Update(int dx, int dy, float dt)
        {
            if (Map == null) throw new InvalidOperationException("Enter로 맵에 먼저 들어간다");
            Walker.Step(dx, dy, dt, Map.Collision);
            DoorArea door = _doors.Update(Walker.Box, dt);
            if (door == null || !door.Destination.HasValue) return false;
            TravelTarget target = door.Destination.Value;
            if (!Enter(target.MapId, target.SpawnId, door.Target)) return false;
            Travels++;
            return true;
        }
    }
}
