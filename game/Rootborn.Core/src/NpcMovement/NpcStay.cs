using System;
using System.Collections.Generic;
using Rootborn.Core.World;

namespace Rootborn.Core.NpcMovement
{
    /// <summary>
    /// 실행·플랫폼과 무관한 문자열 해시. FNV-1a 32비트를 UTF-16 코드 단위마다 돌린다.
    /// NPC 씨앗처럼 저장하지 않고 매번 같은 값이어야 하는 곳에 쓴다(string.GetHashCode는 실행마다 다르다).
    /// </summary>
    public static class StableHash
    {
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;

        public static uint Of(string text)
        {
            uint hash = OffsetBasis;
            if (text == null) return hash;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= Prime;
            }
            return hash;
        }
    }

    /// <summary>xorshift32 난수. 같은 씨앗이면 어디서나 같은 수열이다. 씨앗 0은 1로 바꾼다(0은 영원히 0이다).</summary>
    public struct NpcRandom
    {
        private uint _state;

        public NpcRandom(uint seed) { _state = seed == 0u ? 1u : seed; }

        private uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>0 이상 exclusiveMax 미만.</summary>
        public int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        /// <summary>min 이상 max 이하(위 24비트로 고른다). min == max여도 난수 하나를 쓴다.</summary>
        public float Range(float min, float max) => min + (max - min) * ((NextUInt() >> 8) * (1f / 16777216f));
    }

    /// <summary>머무름 하나를 계산할 때 주어지는 것. Grid는 자리(Anchor)가 있는 맵의 충돌 격자다.</summary>
    public readonly struct NpcStayContext
    {
        public readonly NpcSpot Anchor;
        public readonly CollisionGrid Grid;
        public readonly uint Seed;
        public readonly double CellsPerMinute, SecondsPerMinute;

        public NpcStayContext(NpcSpot anchor, CollisionGrid grid, uint seed, double cellsPerMinute, double secondsPerMinute)
        {
            Anchor = anchor;
            Grid = grid;
            Seed = seed;
            CellsPerMinute = cellsPerMinute;
            SecondsPerMinute = secondsPerMinute;
        }
    }

    public interface INpcStayBehavior
    {
        /// <summary>머무름이 시작된 뒤 minutesIntoStay(>= 0)가 지났을 때의 자리. state는 이 머무름 하나에 딸린 것으로, 처음에는 null을 넘긴다.</summary>
        NpcPose PoseAt(in NpcStayContext context, double minutesIntoStay, ref object state);
    }

    /// <summary>자리 그대로 아래를 보고 서 있는다.</summary>
    public sealed class NpcStandStay : INpcStayBehavior
    {
        public static NpcStandStay Instance { get; } = new NpcStandStay();

        private NpcStandStay() { }

        public NpcPose PoseAt(in NpcStayContext context, double minutesIntoStay, ref object state) =>
            NpcPose.At(context.Anchor.MapId, context.Anchor.X, context.Anchor.Y, Facing.Down, false);
    }

    /// <summary>
    /// 자리 둘레를 어슬렁거린다. 걸음 하나 = [쉼: Range(PauseMin, PauseMax)초] → [후보 칸에서 Next(count)로 고른 칸까지 최단 길(GridPath.Find와 같은 길)로 걷기].
    /// 후보는 자리 칸에서 반지름 네모 안으로만 걸어서 닿는 열린 칸((Y, X) 오름차순). 후보가 자리 칸뿐이거나 자리 칸이 막혔으면 서 있기와 같다.
    /// 자리는 (자리, 씨앗, 시각)만으로 정해지고, state는 앞으로 가는 동안 처음부터 다시 따라가지 않게 하는 기억일 뿐이다.
    /// </summary>
    public sealed class NpcWanderStay : INpcStayBehavior
    {
        public NpcWanderStay(float radiusCells, float pauseMinSeconds, float pauseMaxSeconds)
        {
            RadiusCells = NonNegative(radiusCells);
            PauseMinSeconds = NonNegative(pauseMinSeconds);
            PauseMaxSeconds = Math.Max(PauseMinSeconds, NonNegative(pauseMaxSeconds));
        }

        public float RadiusCells { get; }
        public float PauseMinSeconds { get; }
        public float PauseMaxSeconds { get; }

        private static float NonNegative(float value) => value > 0f ? value : 0f;

        private sealed class Walk
        {
            /// <summary>반지름 네모만 열린 작은 격자와 그 왼쪽 위 칸의 맵 좌표. 길은 이 격자에서 찾는다.</summary>
            public CollisionGrid Area;
            public CellPos Origin;
            /// <summary>갈 수 있는 칸(맵 좌표, (Y, X) 오름차순).</summary>
            public CellPos[] Candidates;
            public NpcRandom Random;
            /// <summary>지금 걸음의 시작 분과 그 때 선 칸·방향.</summary>
            public double StepStart;
            public CellPos Cell;
            public Facing Facing;
            public double PauseMinutes, WalkMinutes;
            /// <summary>지금 걸음의 길. Cell 다음 칸부터(비면 제자리). 걸음마다 같은 목록을 비우고 다시 채운다.</summary>
            public List<CellPos> Path;
            /// <summary>길 찾기에 다시 쓰는 칸(작은 격자 칸 번호마다 앞 칸 번호, 찾는 차례). 걸음마다 새로 만들지 않는다.</summary>
            public int[] Previous, Queue;
            public bool Standing;
        }

        public NpcPose PoseAt(in NpcStayContext context, double minutesIntoStay, ref object state)
        {
            double t = minutesIntoStay > 0.0 ? minutesIntoStay : 0.0; // 음수와 NaN은 0
            var walk = state as Walk;
            if (walk == null || t < walk.StepStart)
            {
                walk = Start(in context);
                state = walk;
            }
            if (walk.Standing) return NpcStandStay.Instance.PoseAt(in context, t, ref state);

            while (t >= walk.StepStart + walk.PauseMinutes + walk.WalkMinutes) Advance(walk, in context);

            int size = context.Grid.Grid.CellSize;
            double walkStart = walk.StepStart + walk.PauseMinutes;
            if (t < walkStart || walk.Path.Count == 0)
            {
                var c = CellPos.Center(walk.Cell, size);
                return NpcPose.At(context.Anchor.MapId, c.X, c.Y, walk.Facing, false);
            }

            float travelled = (float)((t - walkStart) * context.CellsPerMinute);
            int last = walk.Path.Count; // 칸 0은 walk.Cell, 칸 i(1..last)는 Path[i-1]
            int k = Math.Min((int)Math.Floor(travelled), last);
            float frac = k < last ? travelled - k : 0f;
            CellPos from = k == 0 ? walk.Cell : walk.Path[k - 1];
            var a = CellPos.Center(from, size);
            if (frac > 0f)
            {
                CellPos to = walk.Path[k];
                var b = CellPos.Center(to, size);
                return NpcPose.At(context.Anchor.MapId, a.X + (b.X - a.X) * frac, a.Y + (b.Y - a.Y) * frac, Direction(from, to), true);
            }
            Facing facing = k == 0 ? walk.Facing : Direction(k == 1 ? walk.Cell : walk.Path[k - 2], from);
            return NpcPose.At(context.Anchor.MapId, a.X, a.Y, facing, true);
        }

        private Walk Start(in NpcStayContext context)
        {
            var walk = new Walk { Facing = Facing.Down };
            CollisionGrid grid = context.Grid;
            bool timeOk = context.CellsPerMinute > 0.0 && !double.IsInfinity(context.CellsPerMinute)
                && context.SecondsPerMinute > 0.0 && !double.IsInfinity(context.SecondsPerMinute);
            if (grid == null || !timeOk) { walk.Standing = true; return walk; }

            CellPos anchor = CellPos.Containing(context.Anchor.X, context.Anchor.Y, grid.Grid.CellSize);
            if (grid.IsBlocked(anchor)) { walk.Standing = true; return walk; }
            walk.Area = Area(grid, anchor, out walk.Origin);
            walk.Candidates = Candidates(walk.Area, new CellPos(anchor.X - walk.Origin.X, anchor.Y - walk.Origin.Y));
            if (walk.Candidates.Length <= 1) { walk.Standing = true; return walk; }
            for (int i = 0; i < walk.Candidates.Length; i++)
                walk.Candidates[i] = new CellPos(walk.Candidates[i].X + walk.Origin.X, walk.Candidates[i].Y + walk.Origin.Y);

            int cells = walk.Area.Grid.Count;
            walk.Path = new List<CellPos>(cells);
            walk.Previous = new int[cells];
            walk.Queue = new int[cells];
            walk.Random = new NpcRandom(context.Seed);
            walk.StepStart = 0.0;
            walk.Cell = anchor;
            Plan(walk, context.SecondsPerMinute, context.CellsPerMinute);
            return walk;
        }

        /// <summary>지금 걸음을 끝내고 다음 걸음을 뽑는다.</summary>
        private void Advance(Walk walk, in NpcStayContext context)
        {
            List<CellPos> path = walk.Path;
            if (path.Count > 0)
            {
                walk.Facing = Direction(path.Count > 1 ? path[path.Count - 2] : walk.Cell, path[path.Count - 1]);
                walk.Cell = path[path.Count - 1];
            }
            walk.StepStart += walk.PauseMinutes + walk.WalkMinutes;
            Plan(walk, context.SecondsPerMinute, context.CellsPerMinute);
        }

        /// <summary>walk.Cell에서 시작하는 걸음 하나: 쉼을 뽑고, 갈 칸을 뽑고, 길을 찾는다.</summary>
        private void Plan(Walk walk, double secondsPerMinute, double cellsPerMinute)
        {
            float pauseSeconds = walk.Random.Range(PauseMinSeconds, PauseMaxSeconds);
            CellPos target = walk.Candidates[walk.Random.Next(walk.Candidates.Length)];
            walk.PauseMinutes = pauseSeconds / secondsPerMinute;
            CellPos o = walk.Origin;
            var from = new CellPos(walk.Cell.X - o.X, walk.Cell.Y - o.Y);
            var goal = new CellPos(target.X - o.X, target.Y - o.Y);
            List<CellPos> path = walk.Path;
            path.Clear();
            if (!goal.Equals(from)) FindPath(walk, from, goal);
            for (int i = 0; i < path.Count; i++) path[i] = new CellPos(path[i].X + o.X, path[i].Y + o.Y);
            walk.WalkMinutes = path.Count / cellsPerMinute;
        }

        /// <summary>GridPath.Find와 같은 순서(앞에서부터 넓게, 오른쪽·왼쪽·아래·위)라 같은 길이 나온다.</summary>
        private static readonly CellPos[] Steps = { new CellPos(1, 0), new CellPos(-1, 0), new CellPos(0, 1), new CellPos(0, -1) };

        /// <summary>
        /// 작은 격자에서 from부터 goal까지의 최단 길을 walk.Path에 채운다(from은 넣지 않는다, 길이 없으면 비운 채). GridPath.Find와 같은 길이지만
        /// 걸음마다 새 객체를 만들지 않는다 — 걷는 NPC가 모두 프레임마다 자리를 계산하므로 걸음이 바뀌는 프레임에 쓰레기를 남기지 않게.
        /// 작은 격자 밖은 막혔다.
        /// </summary>
        private static void FindPath(Walk walk, CellPos from, CellPos goal)
        {
            CollisionGrid area = walk.Area;
            MapGrid grid = area.Grid;
            int[] previous = walk.Previous, queue = walk.Queue;
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            int start = grid.Index(from.X, from.Y), end = grid.Index(goal.X, goal.Y);
            previous[start] = start;
            int head = 0, tail = 0;
            queue[tail++] = start;
            while (head < tail)
            {
                int current = queue[head++];
                if (current == end)
                {
                    List<CellPos> path = walk.Path;
                    for (int c = current; c != start; c = previous[c]) path.Add(new CellPos(c % grid.Columns, c / grid.Columns));
                    path.Reverse();
                    return;
                }
                int cx = current % grid.Columns, cy = current / grid.Columns;
                for (int s = 0; s < Steps.Length; s++)
                {
                    int nx = cx + Steps[s].X, ny = cy + Steps[s].Y;
                    if (!grid.Contains(nx, ny) || area.IsBlocked(nx, ny)) continue;
                    int next = grid.Index(nx, ny);
                    if (previous[next] >= 0) continue;
                    previous[next] = current;
                    queue[tail++] = next;
                }
            }
        }

        /// <summary>
        /// 반지름 네모(맵 안으로 자른 것)만 열려 있는 작은 격자. 네모 밖은 막혔다 — 그래서 이 격자 위의 GridPath.Find 길은
        /// 네모 밖으로 돌아가지 않는다(맵 전체에서 찾으면 더 짧은 길이 네모 밖을 지날 수 있다).
        /// </summary>
        private CollisionGrid Area(CollisionGrid grid, CellPos anchor, out CellPos origin)
        {
            int r = (int)Math.Min(Math.Floor(RadiusCells), Math.Max(grid.Columns, grid.Rows));
            int x0 = Math.Max(0, anchor.X - r), x1 = Math.Min(grid.Columns - 1, anchor.X + r);
            int y0 = Math.Max(0, anchor.Y - r), y1 = Math.Min(grid.Rows - 1, anchor.Y + r);
            int cols = x1 - x0 + 1, rows = y1 - y0 + 1;
            var blocked = new bool[cols * rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    blocked[y * cols + x] = grid.IsBlocked(x0 + x, y0 + y);
            origin = new CellPos(x0, y0);
            return new CollisionGrid(new MapGrid(cols, rows, grid.Grid.CellSize), blocked);
        }

        /// <summary>작은 격자에서 자리 칸으로부터 걸어서 닿는 열린 칸(작은 격자 좌표). (Y, X) 오름차순.</summary>
        private static CellPos[] Candidates(CollisionGrid area, CellPos start)
        {
            var reached = new List<CellPos>();
            GridPath.Find(area, start, c => { reached.Add(c); return false; });
            reached.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
            return reached.ToArray();
        }

        private static Facing Direction(CellPos from, CellPos to)
        {
            if (to.X > from.X) return Facing.Right;
            if (to.X < from.X) return Facing.Left;
            return to.Y < from.Y ? Facing.Up : Facing.Down;
        }
    }
}
