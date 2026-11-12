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

        /// <summary>열리는 문의 진행도(0~1, DoorWatch.Progress). 열리는 문이 없으면 0.</summary>
        public float OpeningProgress => _doors?.Progress ?? 0f;

        /// <summary>
        /// 서 있는 맵을 다시 읽는다(부지의 건물 단계가 바뀌었을 때). 자리와 보는 쪽은 그대로 두되, 몸이 막힌 칸에 걸리면 가장 가까운 열린 칸 가운데로 옮긴다.
        /// 문 지켜보기는 새 문으로 다시 시작한다(이미 겹친 문은 나갔다 들어와야 열린다). 이동이 아니라 도착을 기록하지 않는다.
        /// 다시 읽지 못하면 지금 맵을 그대로 두고 false.
        /// </summary>
        public bool Reload()
        {
            if (Map == null) throw new InvalidOperationException("Enter로 맵에 먼저 들어간다");
            WorldMap map;
            try { map = _source.Load(Map.Id); }
            catch (Exception e) { LastProblem = "맵 " + Map.Id + "을 다시 읽지 못했다: " + e.Message; return false; }
            if (map == null) { LastProblem = "맵이 없다: " + Map.Id; return false; }
            Map = map;
            if (map.Collision.AnyBlocked(Walker.Box))
            {
                CellPos from = CellPos.Containing(Walker.X, Walker.Y, map.Grid.CellSize);
                if (NearestOpen(map.Collision, from, out CellPos open))
                {
                    Facing facing = Walker.Facing;
                    Walker.PlaceAt(CellPos.Center(open, map.Grid.CellSize));
                    Walker.Face(facing);
                }
            }
            _doors = new DoorWatch(map.Doors, DoorWatch.UnityOpeningSeconds, DoorWatch.UnityContactSkinCells * map.Grid.CellSize);
            _doors.Reset(Walker.Box);
            LastProblem = null;
            return true;
        }

        /// <summary>막힌 칸도 지나며(맵 안에서) 상하좌우로 넓혀 찾은 가장 가까운 열린 칸.</summary>
        private static bool NearestOpen(CollisionGrid grid, CellPos from, out CellPos open)
        {
            from = new CellPos(Math.Max(0, Math.Min(grid.Columns - 1, from.X)), Math.Max(0, Math.Min(grid.Rows - 1, from.Y)));
            var seen = new HashSet<CellPos> { from };
            var queue = new Queue<CellPos>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                CellPos c = queue.Dequeue();
                if (grid.Grid.Contains(c.X, c.Y) && !grid.IsBlocked(c)) { open = c; return true; }
                for (int i = 0; i < 4; i++)
                {
                    var next = new CellPos(c.X + (i == 0 ? 1 : i == 1 ? -1 : 0), c.Y + (i == 2 ? 1 : i == 3 ? -1 : 0));
                    if (grid.Grid.Contains(next.X, next.Y) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            open = from;
            return false;
        }

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
            StepAsideIfCaught();
            Walker.Step(dx, dy, dt, Map.Collision);
            DoorArea door = _doors.Update(Walker.Box, dt);
            if (door == null || !door.Destination.HasValue) return false;
            TravelTarget target = door.Destination.Value;
            if (!Enter(target.MapId, target.SpawnId, door.Target)) return false;
            Travels++;
            return true;
        }

        /// <summary>
        /// 몸 밑에 막는 칸이 생겼으면(같이 쓰는 집에서 남이 가구를 놓았다, 자기 자리에 놓았다) 가장 가까운 빈 칸의 가운데로 비켜난다.
        /// 몸 상자가 막힌 칸과 겹치면 어느 쪽으로 걸어도 막혀 갇힌다(Walker는 막힌 칸에 들어가는 걸음을 받지 않는다). Unity 판은 물리 엔진이 겹친 몸을 밀어냈다.
        /// 몸이 선 칸에서 한 칸씩 넓혀 가며(같은 거리면 위·왼쪽 먼저 — 결정적) 몸이 막힌 칸에 걸치지 않는 칸 가운데를 찾는다. 겹치지 않으면 아무것도 하지 않는다(할당 없음).
        /// </summary>
        private void StepAsideIfCaught()
        {
            CollisionGrid grid = Map.Collision;
            if (!grid.AnyBlocked(Walker.Box)) return;
            int size = grid.Grid.CellSize;
            CellPos here = CellPos.Containing(Walker.X, Walker.Y, size);
            float half = Walker.Box.W / 2f;
            int reach = Math.Max(grid.Columns, grid.Rows);
            for (int ring = 1; ring <= reach; ring++)
            {
                bool found = false;
                float bestDistance = float.MaxValue, bestX = 0f, bestY = 0f;
                for (int y = here.Y - ring; y <= here.Y + ring; y++)
                    for (int x = here.X - ring; x <= here.X + ring; x++)
                    {
                        if (Math.Max(Math.Abs(x - here.X), Math.Abs(y - here.Y)) != ring || !grid.Grid.Contains(x, y)) continue;
                        var center = CellPos.Center(new CellPos(x, y), size);
                        if (grid.AnyBlocked(new AreaRect(center.X - half, center.Y - half, half * 2f, half * 2f))) continue;
                        float d = (center.X - Walker.X) * (center.X - Walker.X) + (center.Y - Walker.Y) * (center.Y - Walker.Y);
                        if (d < bestDistance - 1e-3f) { found = true; bestDistance = d; bestX = center.X; bestY = center.Y; }
                    }
                if (!found) continue;
                Facing facing = Walker.Facing;
                Walker.PlaceAt(bestX, bestY);
                Walker.Face(facing);
                return;
            }
        }
    }
}
