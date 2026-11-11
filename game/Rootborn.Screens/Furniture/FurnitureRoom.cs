using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 가구를 놓는 방 하나(Unity AuthoredHousePlacementBinding이 씬에서 하던 일): 맵에서 가구 방 칸을 만들고, 놓인 가구가 막는 칸을
    /// 캐릭터 충돌에 더한다. 맵 크기는 맵(LDtk 레벨)에서 온다 — 코드에 적지 않는다.
    ///
    /// 칸 좌표: 가구 칸(GridPoint)은 Unity 판처럼 y가 위로 커진다(가구 정의의 차지 칸 (0,1)은 기준 칸의 위 칸이고, 그림의 기준점도
    /// 아래 칸에 있다). 맵 칸(CellPos)은 y가 아래로 커진다. 둘 사이 바꾸기는 여기 한 곳(ToPlacement·ToMap)에만 둔다: x는 같고 y는 뒤집는다.
    ///
    /// 방 칸: 맵 안의 칸은 모두 바닥, 막힌 칸은 고정 칸, 도착 지점이 있는 칸은 들어오는 칸과 늘 지나갈 수 있어야 하는 칸이다.
    /// 들어오는 칸은 도착 지점 id 순서(문자열 순)의 첫 칸이다.
    /// </summary>
    public sealed class FurnitureRoom
    {
        private readonly bool[] _blocked;
        private readonly bool[] _baseBlocked;
        private readonly List<GridPoint> _floor;

        public FurnitureRoom(WorldMap map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            MapGrid grid = map.Grid;
            _floor = new List<GridPoint>(grid.Columns * grid.Rows);
            var fixedCells = new List<GridPoint>();
            _baseBlocked = new bool[grid.Count];
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                {
                    GridPoint cell = ToPlacement(new CellPos(x, y));
                    _floor.Add(cell);
                    if (!map.Collision.IsBlocked(x, y)) continue;
                    fixedCells.Add(cell);
                    _baseBlocked[grid.Index(x, y)] = true;
                }

            var ids = new List<string>(map.SpawnIds);
            ids.Sort(StringComparer.Ordinal);
            var spawns = new List<GridPoint>(ids.Count);
            var spawnPoints = new Dictionary<string, (float X, float Y)>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                map.TryGetSpawn(id, out var point);
                spawnPoints[id] = point;
                GridPoint cell = ToPlacement(CellPos.Containing(point.X, point.Y, grid.CellSize));
                if (!spawns.Contains(cell)) spawns.Add(cell);
            }
            if (spawns.Count == 0) throw new InvalidOperationException(map.Id + ": 방에 도착 지점이 없다");
            Grid = new PlacementGrid(_floor, fixedCells, spawns[0], spawns);

            // 캐릭터가 걷는 맵: 문과 도착 지점은 그대로, 막힌 칸 표만 이 방이 가진다(가구가 놓이면 그 칸을 더한다).
            _blocked = (bool[])_baseBlocked.Clone();
            WalkMap = new WorldMap(map.Id, new CollisionGrid(grid, _blocked, map.Collision.OpenOutside), map.Doors, spawnPoints);
        }

        /// <summary>가구가 없는 원래 맵(방 칸을 만드는 쪽).</summary>
        public WorldMap Map { get; }

        /// <summary>GameSession.EnterHouse에 넘길 방 칸(가구 칸 좌표).</summary>
        public PlacementGrid Grid { get; }

        /// <summary>캐릭터가 걷는 맵. 막힌 칸 = 원래 막힌 칸 + 이동을 막는 가구가 차지한 칸(SetFurnitureBlocking).</summary>
        public WorldMap WalkMap { get; }

        /// <summary>바닥 칸(가구 칸 좌표, 맵의 위 줄부터).</summary>
        public IReadOnlyList<GridPoint> FloorCells => _floor;

        public GridPoint ToPlacement(CellPos mapCell) => new GridPoint(mapCell.X, Map.Grid.Rows - 1 - mapCell.Y);

        public CellPos ToMap(GridPoint cell) => new CellPos(cell.X, Map.Grid.Rows - 1 - cell.Y);

        /// <summary>가구 칸의 맵 픽셀 사각형.</summary>
        public AreaRect CellArea(GridPoint cell)
        {
            CellPos c = ToMap(cell);
            int size = Map.Grid.CellSize;
            return new AreaRect(c.X * size, c.Y * size, size, size);
        }

        /// <summary>이동을 막는 가구가 차지한 칸(House.BlockingCells)으로 캐릭터 충돌을 다시 맞춘다. 맵 밖 칸은 무시한다.</summary>
        public void SetFurnitureBlocking(IEnumerable<GridPoint> cells)
        {
            Array.Copy(_baseBlocked, _blocked, _blocked.Length);
            if (cells == null) return;
            MapGrid grid = Map.Grid;
            foreach (GridPoint cell in cells)
            {
                CellPos c = ToMap(cell);
                if (grid.Contains(c.X, c.Y)) _blocked[grid.Index(c.X, c.Y)] = true;
            }
        }
    }
}
