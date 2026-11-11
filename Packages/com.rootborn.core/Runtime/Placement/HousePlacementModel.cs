using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.Placement
{
    /// <summary>
    /// 가구를 놓을 방의 칸. 화면(타일맵)이 만들어 넘긴다. 방 크기는 여기서만 정해진다(코드에 적지 않는다).
    /// 바닥이 아닌 칸에는 놓을 수 없고, 고정 칸(벽·충돌·장식·고정 소품)은 놓을 수도 지나갈 수도 없다.
    /// </summary>
    public interface IPlacementGrid
    {
        bool HasFloor(GridPoint cell);
        bool IsFixed(GridPoint cell);

        /// <summary>플레이어가 방에 들어오는 칸. 가구로 막을 수 없다.</summary>
        GridPoint Spawn { get; }

        /// <summary>문 앞처럼 늘 지나갈 수 있어야 하는 칸.</summary>
        IReadOnlyList<GridPoint> Approaches { get; }
    }

    /// <summary>칸 목록을 복사해 두는 격자. 만든 뒤 원본 목록이 바뀌어도 판정은 그대로다.</summary>
    public sealed class PlacementGrid : IPlacementGrid
    {
        private readonly HashSet<GridPoint> _floor;
        private readonly HashSet<GridPoint> _fixed;
        private readonly GridPoint[] _approaches;

        public PlacementGrid(IEnumerable<GridPoint> floor, IEnumerable<GridPoint> fixedCells, GridPoint spawn, IEnumerable<GridPoint> approaches)
        {
            _floor = floor == null ? new HashSet<GridPoint>() : new HashSet<GridPoint>(floor);
            _fixed = fixedCells == null ? new HashSet<GridPoint>() : new HashSet<GridPoint>(fixedCells);
            Spawn = spawn;
            _approaches = approaches == null ? Array.Empty<GridPoint>() : new List<GridPoint>(approaches).ToArray();
        }

        public GridPoint Spawn { get; }
        public IReadOnlyList<GridPoint> Approaches => _approaches;
        public bool HasFloor(GridPoint cell) => _floor.Contains(cell);
        public bool IsFixed(GridPoint cell) => _fixed.Contains(cell);
    }

    /// <summary>
    /// 방 칸 위의 판정(원본 AuthoredHousePlacementModel). 가구가 차지할 수 있는 칸인지,
    /// 이동을 막는 가구를 더해도 출발 칸과 문 앞까지 걸어갈 수 있는지 본다.
    /// </summary>
    public sealed class HousePlacementModel
    {
        private static readonly GridPoint[] Steps = { new GridPoint(0, 1), new GridPoint(1, 0), new GridPoint(0, -1), new GridPoint(-1, 0) };
        private readonly IPlacementGrid _grid;

        public HousePlacementModel(IPlacementGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public GridPoint Spawn => _grid.Spawn;

        /// <summary>모든 칸이 바닥이고 고정 칸·출발 칸·문 앞이 아니어야 한다. 빈 목록은 안 된다.</summary>
        public bool CanOccupy(IReadOnlyList<GridPoint> cells)
        {
            if (cells == null || cells.Count == 0) return false;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!_grid.HasFloor(cell) || _grid.IsFixed(cell) || cell.Equals(_grid.Spawn) || IsApproach(cell)) return false;
            }

            return true;
        }

        /// <summary>더 막는 칸을 넣어도, 원래 갈 수 있던 출발 칸과 문 앞에 여전히 갈 수 있으면 true.</summary>
        public bool PreservesAccess(IEnumerable<GridPoint> additionalBlocking)
        {
            var extra = additionalBlocking == null ? new HashSet<GridPoint>() : new HashSet<GridPoint>(additionalBlocking);
            var before = Reachable(new HashSet<GridPoint>());
            var after = Reachable(extra);
            if (before.Contains(_grid.Spawn) && !after.Contains(_grid.Spawn)) return false;
            var approaches = _grid.Approaches;
            for (int i = 0; i < approaches.Count; i++)
                if (before.Contains(approaches[i]) && !after.Contains(approaches[i])) return false;
            return true;
        }

        private bool IsApproach(GridPoint cell)
        {
            var approaches = _grid.Approaches;
            for (int i = 0; i < approaches.Count; i++) if (approaches[i].Equals(cell)) return true;
            return false;
        }

        private bool Walkable(GridPoint cell, HashSet<GridPoint> extra) => _grid.HasFloor(cell) && !_grid.IsFixed(cell) && !extra.Contains(cell);

        private HashSet<GridPoint> Reachable(HashSet<GridPoint> extra)
        {
            var visited = new HashSet<GridPoint>();
            var start = _grid.Spawn;
            if (!Walkable(start, extra)) return visited;
            var queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            visited.Add(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                for (int i = 0; i < Steps.Length; i++)
                {
                    var next = new GridPoint(cell.X + Steps[i].X, cell.Y + Steps[i].Y);
                    if (Walkable(next, extra) && visited.Add(next)) queue.Enqueue(next);
                }
            }

            return visited;
        }
    }
}
