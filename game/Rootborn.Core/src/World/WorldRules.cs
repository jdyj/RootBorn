using System;
using System.Collections.Generic;

namespace Rootborn.Core.World
{
    /// <summary>칸 단위 최단 경로(상하좌우). 테스트 드라이버와 NPC 길 찾기가 어디로 걸을지 정할 때 쓴다.</summary>
    public static class GridPath
    {
        private static readonly CellPos[] Steps = { new CellPos(1, 0), new CellPos(-1, 0), new CellPos(0, 1), new CellPos(0, -1) };

        /// <summary>출발 칸에서 goal을 만족하는 가장 가까운 칸까지. 출발 칸은 경로에 넣지 않는다. 길이 없으면 null.</summary>
        public static List<CellPos> Find(CollisionGrid grid, CellPos from, Func<CellPos, bool> goal)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (goal == null) throw new ArgumentNullException(nameof(goal));
            var previous = new Dictionary<CellPos, CellPos> { [from] = from };
            var queue = new Queue<CellPos>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                CellPos current = queue.Dequeue();
                if (goal(current))
                {
                    var path = new List<CellPos>();
                    while (!current.Equals(from)) { path.Add(current); current = previous[current]; }
                    path.Reverse();
                    return path;
                }
                foreach (CellPos step in Steps)
                {
                    var next = new CellPos(current.X + step.X, current.Y + step.Y);
                    if (grid.IsBlocked(next) || previous.ContainsKey(next)) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
            return null;
        }
    }

    /// <summary>
    /// 도착 지점 검사. Unity 판 굽기가 저장을 거부하던 조건을 그대로 옮겼다:
    /// 열린 칸 위, 출구 상자에서 0.9칸 이상, 그 자리에서 모든 출구까지 걸어갈 수 있음.
    /// </summary>
    public static class ArrivalRules
    {
        public const float MinCellsFromExit = 0.9f;

        public static List<string> Check(CollisionGrid grid, IReadOnlyList<DoorArea> exits, (float X, float Y) spawn)
        {
            var problems = new List<string>();
            int size = grid.Grid.CellSize;
            CellPos cell = CellPos.Containing(spawn.X, spawn.Y, size);
            if (grid.IsBlocked(cell)) { problems.Add("도착 지점 " + cell + "이 막힌 칸이다"); return problems; }
            HashSet<CellPos> reachable = MapReach.From(grid, cell);
            foreach (DoorArea exit in exits)
            {
                if (!exit.Destination.HasValue) continue;
                if (exit.Area.DistanceTo(spawn.X, spawn.Y) / size < MinCellsFromExit)
                    problems.Add("도착 지점이 출구 '" + exit.Target + "' 상자에서 " + MinCellsFromExit + "칸 안이다");
                // 걸어서 닿는 칸이 출구 상자에 겹치거나 맞닿으면 닿는다(맵 검사·문 판정과 같은 기준 — 바깥 벽 칸의 문은 칸이 막혀 있다).
                if (!MapReach.Touches(reachable, exit.Area, size)) problems.Add("도착 지점에서 출구 '" + exit.Target + "'까지 걸어갈 수 없다");
            }
            return problems;
        }
    }

    /// <summary>카메라: 따라가되 맵 밖은 비추지 않는다. 배율은 맵이 정한 보이는 높이에 가장 가까운 정수.</summary>
    public static class WorldCamera
    {
        /// <summary>화면 왼쪽(위) 끝이 맵의 어느 픽셀에 오는가. 맵이 화면보다 작으면 가운데에 두어 음수가 된다.</summary>
        public static float Offset(float focus, float mapSize, float viewSize)
        {
            if (mapSize <= viewSize) return (float)Math.Round(-(viewSize - mapSize) / 2f);
            return (float)Math.Round(Math.Min(Math.Max(focus - viewSize / 2f, 0f), mapSize - viewSize));
        }

        /// <summary>Unity 판 AuthoredWorldScene.FitPixelFrame처럼 화면 높이를 보이는 높이로 나눈 값에 가장 가까운 정수(최소 1).</summary>
        public static int Zoom(int screenHeight, float viewCells, int cellSize)
        {
            if (viewCells <= 0f || cellSize <= 0) return 1;
            return Math.Max(1, (int)Math.Round(screenHeight / (viewCells * cellSize), MidpointRounding.AwayFromZero));
        }
    }
}
