using System;
using System.Collections.Generic;
using Rootborn.Core.World;

namespace Rootborn.Core.NpcMovement
{
    /// <summary>맵 하나의 픽셀 자리. NPC가 서거나 갈 곳.</summary>
    public readonly struct NpcSpot
    {
        public readonly string MapId;
        public readonly float X, Y;

        public NpcSpot(string mapId, float x, float y)
        {
            MapId = mapId ?? string.Empty;
            X = x;
            Y = y;
        }
    }

    /// <summary>한 순간 NPC가 어디에 어떻게 있는지. Present가 false면 어느 맵에도 없다.</summary>
    public readonly struct NpcPose
    {
        public readonly bool Present;
        public readonly string MapId;
        public readonly float X, Y;
        public readonly Facing Facing;
        public readonly bool Moving;

        private NpcPose(bool present, string mapId, float x, float y, Facing facing, bool moving)
        {
            Present = present;
            MapId = mapId;
            X = x;
            Y = y;
            Facing = facing;
            Moving = moving;
        }

        public static NpcPose Absent => default;

        public static NpcPose At(string mapId, float x, float y, Facing facing, bool moving) =>
            new NpcPose(true, mapId ?? string.Empty, x, y, facing, moving);
    }

    /// <summary>길 중 한 맵 안의 부분. Cells[0]은 그 맵에서의 출발 칸이고 이웃한 칸(상하좌우)이 차례로 이어진다.</summary>
    public sealed class NpcRouteLeg
    {
        public string MapId { get; }
        public int CellSize { get; }
        public IReadOnlyList<CellPos> Cells { get; }

        internal NpcRouteLeg(string mapId, int cellSize, IReadOnlyList<CellPos> cells)
        {
            MapId = mapId;
            CellSize = cellSize;
            Cells = cells;
        }
    }

    /// <summary>
    /// 찾은 길. 다리마다 한 맵이고 문을 지나는 데는 0칸이 든다. 지난 칸 수로 그 순간의 자리를 준다.
    /// 다리의 끝(문 앞)에 정확히 선 순간은 다음 다리의 출발 칸으로 친다 — 그래서 언제나 정확히 한 맵에 있다.
    /// </summary>
    public sealed class NpcRoute
    {
        private readonly float[] _legStart;
        private readonly Facing[] _facingBeforeLeg;

        public IReadOnlyList<NpcRouteLeg> Legs { get; }

        /// <summary>각 다리의 (Cells.Count - 1)의 합.</summary>
        public float TotalCells { get; }

        internal NpcRoute(IReadOnlyList<NpcRouteLeg> legs)
        {
            if (legs == null || legs.Count == 0) throw new ArgumentException("길에는 다리가 하나 이상 있어야 한다", nameof(legs));
            Legs = legs;
            _legStart = new float[legs.Count];
            _facingBeforeLeg = new Facing[legs.Count];
            float total = 0f;
            Facing facing = Facing.Down;
            for (int i = 0; i < legs.Count; i++)
            {
                _legStart[i] = total;
                _facingBeforeLeg[i] = facing;
                IReadOnlyList<CellPos> cells = legs[i].Cells;
                total += cells.Count - 1;
                if (cells.Count > 1) facing = Direction(cells[cells.Count - 2], cells[cells.Count - 1]);
            }
            TotalCells = total;
        }

        /// <summary>
        /// cellsTravelled칸 지난 때의 자리. 0..TotalCells로 자르고, 칸 가운데 사이를 곧게 잇는다.
        /// 방향은 마지막으로 움직인 방향(한 번도 안 움직였으면 아래). 끝에서는 움직이지 않는다.
        /// </summary>
        public NpcPose PoseAt(float cellsTravelled)
        {
            int leg;
            float local;
            if (!(cellsTravelled > 0f)) { leg = 0; local = 0f; }
            else if (cellsTravelled >= TotalCells) { leg = Legs.Count - 1; local = Legs[leg].Cells.Count - 1; }
            else
            {
                leg = 0;
                while (leg < Legs.Count - 1 && _legStart[leg] + (Legs[leg].Cells.Count - 1) <= cellsTravelled) leg++;
                local = cellsTravelled - _legStart[leg];
            }

            NpcRouteLeg part = Legs[leg];
            IReadOnlyList<CellPos> cells = part.Cells;
            int last = cells.Count - 1;
            int k = Math.Min((int)Math.Floor(local), last);
            float frac = k < last ? local - k : 0f;
            bool moving = cellsTravelled < TotalCells;

            var a = CellPos.Center(cells[k], part.CellSize);
            if (frac > 0f)
            {
                var b = CellPos.Center(cells[k + 1], part.CellSize);
                return NpcPose.At(part.MapId, a.X + (b.X - a.X) * frac, a.Y + (b.Y - a.Y) * frac, Direction(cells[k], cells[k + 1]), moving);
            }
            Facing facing = k > 0 ? Direction(cells[k - 1], cells[k]) : _facingBeforeLeg[leg];
            return NpcPose.At(part.MapId, a.X, a.Y, facing, moving);
        }

        private static Facing Direction(CellPos from, CellPos to)
        {
            if (to.X > from.X) return Facing.Right;
            if (to.X < from.X) return Facing.Left;
            return to.Y < from.Y ? Facing.Up : Facing.Down;
        }
    }

    /// <summary>
    /// NPC 길 찾기. 같은 맵은 칸 단위 최단 경로, 다른 맵은 맵을 마디로 "닿는 문 → 목적지 도착 지점"을 이음으로 하는
    /// 너비 우선 탐색으로 문을 가장 적게 지나는 길을 고른다. 목적지 칸이 막혀 있으면 그 둘레 8칸 중 닿는 칸으로 간다.
    /// 읽은 맵과 찾은 길(없음도)은 기억하고, 가구가 바뀐 맵은 Forget으로 버린다.
    /// </summary>
    public sealed class NpcRouteFinder
    {
        private readonly IWorldMapSource _maps;
        private readonly Dictionary<string, WorldMap> _loaded = new Dictionary<string, WorldMap>(StringComparer.Ordinal);
        private readonly Dictionary<(string, CellPos, string, CellPos), NpcRoute> _routes = new Dictionary<(string, CellPos, string, CellPos), NpcRoute>();

        public NpcRouteFinder(IWorldMapSource maps)
        {
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
        }

        /// <summary>from에서 to까지의 길. 길이 없거나 맵을 읽지 못하면 null. 같은 (from 칸, to 칸)은 기억한 것을 돌려준다.</summary>
        public NpcRoute Find(NpcSpot from, NpcSpot to)
        {
            WorldMap fromMap = Map(from.MapId), toMap = Map(to.MapId);
            if (fromMap == null || toMap == null) return null;
            CellPos fromCell = CellPos.Containing(from.X, from.Y, fromMap.Grid.CellSize);
            CellPos toCell = CellPos.Containing(to.X, to.Y, toMap.Grid.CellSize);
            var key = (from.MapId, fromCell, to.MapId, toCell);
            if (_routes.TryGetValue(key, out NpcRoute known)) return known;
            NpcRoute route = Search(from.MapId, fromMap, fromCell, to.MapId, toMap, toCell);
            _routes[key] = route;
            return route;
        }

        /// <summary>그 맵을 지나는 기억한 길과 읽은 맵을 버린다(가구가 바뀜). 못 찾았던 길도 다시 찾게 버린다.</summary>
        public void Forget(string mapId)
        {
            mapId = mapId ?? string.Empty;
            _loaded.Remove(mapId);
            var drop = new List<(string, CellPos, string, CellPos)>();
            foreach (var pair in _routes)
            {
                if (pair.Value == null) { drop.Add(pair.Key); continue; }
                foreach (NpcRouteLeg leg in pair.Value.Legs)
                    if (leg.MapId == mapId) { drop.Add(pair.Key); break; }
            }
            foreach (var key in drop) _routes.Remove(key);
        }

        private sealed class Node
        {
            public string MapId;
            public WorldMap Map;
            public CellPos Start;
            public Node Parent;
            /// <summary>부모 맵에서 이 맵으로 넘어온 문까지의 칸(부모의 출발 칸 포함).</summary>
            public List<CellPos> LegToHere;
        }

        private NpcRoute Search(string fromMapId, WorldMap fromMap, CellPos fromCell, string toMapId, WorldMap toMap, CellPos toCell)
        {
            bool toBlocked = toMap.Collision.IsBlocked(toCell);
            Func<CellPos, bool> atGoal = c => toBlocked
                ? !c.Equals(toCell) && Math.Abs(c.X - toCell.X) <= 1 && Math.Abs(c.Y - toCell.Y) <= 1
                : c.Equals(toCell);

            var seen = new HashSet<(string, CellPos)> { (fromMapId, fromCell) };
            var queue = new Queue<Node>();
            queue.Enqueue(new Node { MapId = fromMapId, Map = fromMap, Start = fromCell });
            while (queue.Count > 0)
            {
                Node node = queue.Dequeue();
                CollisionGrid grid = node.Map.Collision;
                int size = node.Map.Grid.CellSize;
                if (node.MapId == toMapId)
                {
                    List<CellPos> path = GridPath.Find(grid, node.Start, atGoal);
                    if (path != null) return Build(node, path);
                }
                foreach (DoorArea door in node.Map.Doors)
                {
                    if (!door.Destination.HasValue) continue;
                    TravelTarget target = door.Destination.Value;
                    WorldMap next = Map(target.MapId);
                    if (next == null || !next.TryGetSpawn(target.SpawnId, out var spawn)) continue;
                    CellPos nextStart = CellPos.Containing(spawn.X, spawn.Y, next.Grid.CellSize);
                    if (seen.Contains((target.MapId, nextStart))) continue;
                    AreaRect area = door.Area;
                    List<CellPos> toDoor = GridPath.Find(grid, node.Start, c => MapReach.Touches(c, area, size));
                    if (toDoor == null) continue;
                    seen.Add((target.MapId, nextStart));
                    toDoor.Insert(0, node.Start);
                    queue.Enqueue(new Node { MapId = target.MapId, Map = next, Start = nextStart, Parent = node, LegToHere = toDoor });
                }
            }
            return null;
        }

        private static NpcRoute Build(Node last, List<CellPos> finalPath)
        {
            var legs = new List<NpcRouteLeg>();
            finalPath.Insert(0, last.Start);
            legs.Add(new NpcRouteLeg(last.MapId, last.Map.Grid.CellSize, finalPath));
            for (Node n = last; n.Parent != null; n = n.Parent)
                legs.Add(new NpcRouteLeg(n.Parent.MapId, n.Parent.Map.Grid.CellSize, n.LegToHere));
            legs.Reverse();
            return new NpcRoute(legs);
        }

        private WorldMap Map(string mapId)
        {
            mapId = mapId ?? string.Empty;
            if (_loaded.TryGetValue(mapId, out WorldMap map)) return map;
            try { map = _maps.Load(mapId); }
            catch (Exception) { map = null; } // 읽지 못한 맵은 지나지 않는다(MapChecks.Walk와 같다)
            _loaded[mapId] = map;
            return map;
        }
    }
}
