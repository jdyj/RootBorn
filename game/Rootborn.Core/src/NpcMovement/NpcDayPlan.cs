using System;
using System.Collections.Generic;
using Rootborn.Core.Dialogue;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Core.World;

namespace Rootborn.Core.NpcMovement
{
    /// <summary>장소 id의 NPC 자리. 맵 데이터가 준다.</summary>
    public interface INpcAnchors
    {
        /// <summary>그 장소의 slot번째 자리(0부터). 어느 맵에도 없으면 false.</summary>
        bool TryGet(string locationId, int slot, out NpcSpot spot);
    }

    /// <summary>NPC가 걷는 빠르기(실제 초당 칸).</summary>
    public sealed class NpcWalkSettings
    {
        public NpcWalkSettings(float speedCellsPerSecond)
        {
            if (!(speedCellsPerSecond > 0f) || float.IsInfinity(speedCellsPerSecond))
                throw new ArgumentOutOfRangeException(nameof(speedCellsPerSecond), speedCellsPerSecond, "걷는 빠르기는 0보다 커야 한다");
            SpeedCellsPerSecond = speedCellsPerSecond;
        }

        public float SpeedCellsPerSecond { get; }
    }

    /// <summary>하루 계획의 한 구간. 시작 분부터 다음 구간의 시작까지 자리를 준다.</summary>
    internal abstract class NpcPlanSegment
    {
        protected NpcPlanSegment(double start) { Start = start; }

        public double Start { get; }

        public abstract NpcPose PoseAt(double dayMinutes);
    }

    /// <summary>한 자리에서의 머무름. 머무는 행동의 기억(state)은 이 구간 하나에 딸린다.</summary>
    internal sealed class NpcStaySegment : NpcPlanSegment
    {
        private readonly NpcStayContext _context;
        private object _state;

        public NpcStaySegment(double start, INpcStayBehavior behavior, in NpcStayContext context) : base(start)
        {
            Behavior = behavior;
            _context = context;
        }

        public INpcStayBehavior Behavior { get; }
        public NpcSpot Anchor => _context.Anchor;

        public override NpcPose PoseAt(double dayMinutes) => Behavior.PoseAt(in _context, dayMinutes - Start, ref _state);
    }

    /// <summary>시작 분에 출발해 길을 따라 걷는 구간. 길 끝에 닿으면 끝에 선다(다음 구간이 이어받는다).</summary>
    internal sealed class NpcMoveSegment : NpcPlanSegment
    {
        private readonly NpcRoute _route;
        private readonly double _cellsPerMinute;

        public NpcMoveSegment(double start, NpcRoute route, double cellsPerMinute) : base(start)
        {
            _route = route;
            _cellsPerMinute = cellsPerMinute;
        }

        public double Minutes => _route.TotalCells / _cellsPerMinute;

        public override NpcPose PoseAt(double dayMinutes) => _route.PoseAt((float)((dayMinutes - Start) * _cellsPerMinute));
    }

    /// <summary>
    /// NPC 하나의 하루: 시작 분으로 정렬한 구간(머무름, 이동, 없음)의 줄. 시각을 주면 그 시각이 든 구간의 자리를 준다.
    /// 하루 밖의 시각(과 NaN)은 하루의 처음·끝으로 자른다. 시각이 뒤로 가도 된다.
    /// </summary>
    public sealed class NpcDayPlan
    {
        private readonly double _dayStart, _dayEnd;
        private readonly double[] _starts;
        /// <summary>null은 어느 맵에도 없는 구간.</summary>
        private readonly NpcPlanSegment[] _segments;

        internal NpcDayPlan(string npcId, double dayStart, double dayEnd, double[] starts, NpcPlanSegment[] segments)
        {
            NpcId = npcId ?? string.Empty;
            _dayStart = dayStart;
            _dayEnd = dayEnd;
            _starts = starts;
            _segments = segments;
        }

        public string NpcId { get; }

        /// <summary>구간 수(없음 구간 포함). 테스트·측정용.</summary>
        public int SegmentCount => _segments.Length;

        public NpcPose PoseAt(double dayMinutes)
        {
            if (_segments.Length == 0) return NpcPose.Absent;
            double t = dayMinutes;
            if (!(t >= _dayStart)) t = _dayStart; // NaN도 처음으로
            else if (t > _dayEnd) t = _dayEnd;

            int lo = 0, hi = _starts.Length - 1, found = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (_starts[mid] <= t) { found = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            NpcPlanSegment segment = _segments[found];
            return segment == null ? NpcPose.Absent : segment.PoseAt(t);
        }
    }

    /// <summary>
    /// 마을 NPC 모두의 하루 계획. 자리를 정하는 입력은 (데이터, 날, 그날의 분)뿐이다 — 개인 진행, 맵에 들어온 때, 프레임 간격은 보지 않는다.
    /// 계획은 Prepare한 날에 한 번 만들고, 가구가 바뀐 맵(ForgetMap) 뒤에는 다음 Prepare/PoseOf에서 다시 만든다.
    /// PoseOf는 프레임마다 불려도 새 객체를 만들지 않는다(어슬렁 걸음이 넘어갈 때 머무는 행동이 만드는 것은 예외).
    /// </summary>
    public sealed class NpcWorld
    {
        private readonly string[] _npcIds;
        private readonly NpcScheduleLookupCache _schedules;
        private readonly INpcAnchors _anchors;
        private readonly IWorldMapSource _maps;
        private readonly TimeRules _rules;
        private readonly NpcRouteFinder _routes;
        private readonly NpcScheduleResolver _resolver = new NpcScheduleResolver();
        private readonly Dictionary<string, CollisionGrid> _grids = new Dictionary<string, CollisionGrid>(StringComparer.Ordinal);
        private readonly double _cellsPerMinute, _secondsPerMinute;

        private bool _prepared;
        private int _day;
        private NpcDayPlan[] _plans;

        public NpcWorld(IReadOnlyList<string> npcIdsInOrder, NpcScheduleLookupCache schedules, INpcAnchors anchors,
                        IWorldMapSource maps, NpcWalkSettings walk, TimeRules rules)
        {
            if (npcIdsInOrder == null) throw new ArgumentNullException(nameof(npcIdsInOrder));
            if (walk == null) throw new ArgumentNullException(nameof(walk));
            _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
            _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _npcIds = new string[npcIdsInOrder.Count];
            for (int i = 0; i < _npcIds.Length; i++) _npcIds[i] = npcIdsInOrder[i] ?? string.Empty;
            _routes = new NpcRouteFinder(maps);
            _secondsPerMinute = (double)rules.RealSecondsPerGameDay / TimeRules.MinutesPerDay;
            _cellsPerMinute = walk.SpeedCellsPerSecond * _secondsPerMinute;
        }

        public int Count => _npcIds.Length;

        public string NpcId(int index) => _npcIds[index];

        /// <summary>그 날의 계획을 준비한다. 같은 날이고 버린 맵이 없으면 다시 만들지 않는다.</summary>
        public void Prepare(int day)
        {
            if (_prepared && _day == day && _plans != null) return;
            _prepared = true;
            _day = day;
            _plans = Build(day);
        }

        /// <summary>Prepare한 날의 그 시각 자리. Prepare 전에는 어느 맵에도 없다.</summary>
        public NpcPose PoseOf(int index, double dayMinutes)
        {
            NpcDayPlan plan = PlanOf(index);
            return plan == null ? NpcPose.Absent : plan.PoseAt(dayMinutes);
        }

        /// <summary>가구가 바뀐 맵: 그 맵의 길 기억과 격자, 모든 계획을 버린다(머무름의 기억도 새로 시작한다).</summary>
        public void ForgetMap(string mapId)
        {
            mapId = mapId ?? string.Empty;
            _routes.Forget(mapId);
            _grids.Remove(mapId);
            _plans = null;
        }

        /// <summary>Prepare한 날의 계획(버렸으면 다시 만든다). Prepare 전에는 null. 테스트·측정용.</summary>
        internal NpcDayPlan PlanOf(int index)
        {
            if (index < 0 || index >= _npcIds.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (_plans == null)
            {
                if (!_prepared) return null;
                _plans = Build(_day);
            }
            return _plans[index];
        }

        private NpcDayPlan[] Build(int day)
        {
            var slots = new List<(int Minutes, string SlotId)>(4);
            LocationStateTimeSlots.SlotStarts(_rules.DayStartMinutes, _rules.DayEndMinutes, slots);
            int n = _npcIds.Length;

            // 시간대마다 NPC 모두의 자리: npcIdsInOrder 순서로, 같은 장소에 앞서 배정된 NPC 수가 자리 번호이고, 순서는 레지스트리의 NPC 순서다.
            var present = new bool[slots.Count][];
            var spots = new NpcSpot[slots.Count][];
            var stays = new INpcStayBehavior[slots.Count][];
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int k = 0; k < slots.Count; k++)
            {
                present[k] = new bool[n];
                spots[k] = new NpcSpot[n];
                stays[k] = new INpcStayBehavior[n];
                used.Clear();
                var context = new NpcScheduleContext(day, 0, slots[k].SlotId, null);
                for (int i = 0; i < n; i++)
                {
                    if (!_schedules.TryGetByNpcId(_npcIds[i], out NpcSchedule schedule)) continue;
                    NpcScheduleResult result = _resolver.Resolve(schedule, in context);
                    string location = result.LocationId;
                    if (location.Length == 0) continue;
                    used.TryGetValue(location, out int slot);
                    used[location] = slot + 1;
                    if (!_anchors.TryGet(location, slot, out NpcSpot spot)) continue;
                    present[k][i] = true;
                    spots[k][i] = spot;
                    stays[k][i] = result.Entry != null ? result.Entry.Stay : NpcStandStay.Instance;
                }
            }

            var plans = new NpcDayPlan[n];
            var builder = new PlanBuilder(this, day);
            for (int i = 0; i < n; i++)
            {
                builder.Begin(_npcIds[i]);
                for (int k = 0; k < slots.Count; k++)
                    builder.Slot(slots[k].Minutes, present[k][i], spots[k][i], stays[k][i]);
                plans[i] = builder.Finish(_rules.DayStartMinutes, _rules.DayEndMinutes);
            }
            return plans;
        }

        private CollisionGrid GridOf(string mapId)
        {
            if (_grids.TryGetValue(mapId, out CollisionGrid grid)) return grid;
            WorldMap map;
            try { map = _maps.Load(mapId); }
            catch (InvalidOperationException) { return null; } // 읽지 못한 맵에서는 서 있는다. 기억하지 않아 다음 계획에서 다시 읽는다.
            if (map == null) return null;
            _grids[mapId] = map.Collision;
            return map.Collision;
        }

        /// <summary>NPC 하나의 구간 줄을 시간대 경계마다 이어 붙인다(설계 5절).</summary>
        private sealed class PlanBuilder
        {
            private readonly NpcWorld _world;
            private readonly int _day;
            private readonly List<double> _starts = new List<double>(16);
            private readonly List<NpcPlanSegment> _segments = new List<NpcPlanSegment>(16);
            private string _npcId;
            private uint _npcSeed;
            /// <summary>가장 나중의 머무름(이미 시작했거나 이동 끝에 시작할 것). 없음 구간 뒤에는 null.</summary>
            private NpcStaySegment _target;

            public PlanBuilder(NpcWorld world, int day)
            {
                _world = world;
                _day = day;
            }

            public void Begin(string npcId)
            {
                _npcId = npcId;
                _npcSeed = StableHash.Of(npcId) ^ unchecked((uint)((long)_day * 2654435761L));
                _starts.Clear();
                _segments.Clear();
                _target = null;
            }

            public void Slot(double boundary, bool present, NpcSpot spot, INpcStayBehavior stay)
            {
                if (!present)
                {
                    // 자리가 없어지면 경계에 사라진다.
                    if (_segments.Count == 0 || _segments[_segments.Count - 1] != null)
                    {
                        TrimFrom(boundary);
                        Add(boundary, null);
                    }
                    _target = null;
                    return;
                }

                if (_target == null)
                {
                    // 하루의 처음이나 없다가 생긴 자리: 걸어오지 않고 그 자리에 나타난다.
                    TrimFrom(boundary);
                    _target = AddStay(boundary, spot, stay);
                    return;
                }

                if (SameSpot(_target.Anchor, spot))
                {
                    // 자리가 같으면 같은 행동은 머무름이 이어지고, 다른 행동은 경계에서(아직 걷는 중이면 도착할 때) 새로 시작한다.
                    if (ReferenceEquals(_target.Behavior, stay)) return;
                    double start = Math.Max(boundary, _target.Start);
                    TrimFrom(start);
                    _target = AddStay(start, spot, stay);
                    return;
                }

                // 자리가 다르면 경계 시각의 자리에서 출발한다(머무는 중이면 그때의 자리, 걷는 중이면 그 길 위).
                TrimFrom(boundary);
                NpcPose from = _segments[_segments.Count - 1].PoseAt(boundary);
                var fromSpot = new NpcSpot(from.MapId, from.X, from.Y);
                NpcRoute route = _world._routes.Find(fromSpot, spot);
                if (route == null)
                {
                    // 길이 없으면 출발 자리에서 서 있는다. 다음 경계에 다시 시도한다.
                    _target = AddStay(boundary, fromSpot, NpcStandStay.Instance);
                    return;
                }
                var move = new NpcMoveSegment(boundary, route, _world._cellsPerMinute);
                Add(boundary, move);
                _target = AddStay(boundary + move.Minutes, spot, stay);
            }

            public NpcDayPlan Finish(double dayStart, double dayEnd)
            {
                var plan = new NpcDayPlan(_npcId, dayStart, dayEnd, _starts.ToArray(), _segments.ToArray());
                _target = null;
                return plan;
            }

            /// <summary>그 분이나 그 뒤에 시작하는 구간(아직 시작하지 않은 머무름)을 버린다.</summary>
            private void TrimFrom(double minutes)
            {
                while (_starts.Count > 0 && _starts[_starts.Count - 1] >= minutes)
                {
                    _starts.RemoveAt(_starts.Count - 1);
                    _segments.RemoveAt(_segments.Count - 1);
                }
            }

            private NpcStaySegment AddStay(double start, NpcSpot anchor, INpcStayBehavior behavior)
            {
                uint seed = _npcSeed ^ (uint)_segments.Count;
                var context = new NpcStayContext(anchor, _world.GridOf(anchor.MapId), seed, _world._cellsPerMinute, _world._secondsPerMinute);
                var segment = new NpcStaySegment(start, behavior ?? NpcStandStay.Instance, in context);
                Add(start, segment);
                return segment;
            }

            private void Add(double start, NpcPlanSegment segment)
            {
                _starts.Add(start);
                _segments.Add(segment);
            }

            private static bool SameSpot(NpcSpot a, NpcSpot b) =>
                string.Equals(a.MapId, b.MapId, StringComparison.Ordinal) && a.X == b.X && a.Y == b.Y;
        }
    }
}
