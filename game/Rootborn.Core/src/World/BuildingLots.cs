using System;
using System.Collections.Generic;

namespace Rootborn.Core.World
{
    /// <summary>칸 단위 사각형 [X, Y, W, H]. 왼쪽 위 칸이 (X, Y)이고 Right·Bottom은 그 바로 바깥 칸이다.</summary>
    public readonly struct CellRect : IEquatable<CellRect>
    {
        public readonly int X, Y, W, H;

        public CellRect(int x, int y, int w, int h)
        {
            X = x; Y = y; W = w; H = h;
        }

        public int Right => X + W;
        public int Bottom => Y + H;
        public bool Contains(CellPos c) => c.X >= X && c.Y >= Y && c.X < Right && c.Y < Bottom;
        public bool Contains(CellRect r) => r.X >= X && r.Y >= Y && r.Right <= Right && r.Bottom <= Bottom;
        public bool Overlaps(CellRect r) => X < r.Right && r.X < Right && Y < r.Bottom && r.Y < Bottom;

        public bool Equals(CellRect o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object obj) => obj is CellRect o && Equals(o);
        public override int GetHashCode() => X ^ (Y * 31) ^ (W * 131) ^ (H * 1031);
        public override string ToString() => "[" + X + "," + Y + " " + W + "x" + H + "]";
    }

    /// <summary>
    /// 건물 한 단계가 차지하는 칸(그림을 모른다): 크기, 막는 칸, 문 칸. 좌표는 건물 왼쪽 위 칸 기준.
    /// 문 칸은 막는 칸 안에 있어야 한다(문은 벽에 난다). 놓을 때 문 칸만 빼고 막는다.
    /// </summary>
    public sealed class BuildingFootprint
    {
        public readonly int Columns, Rows;
        public readonly CellRect Solid;
        public readonly CellPos Door;

        public BuildingFootprint(int columns, int rows, CellRect solid, CellPos door)
        {
            if (columns <= 0 || rows <= 0) throw new ArgumentException("건물 크기는 양수여야 한다: " + columns + "x" + rows);
            var whole = new CellRect(0, 0, columns, rows);
            if (solid.W <= 0 || solid.H <= 0 || !whole.Contains(solid)) throw new ArgumentException("막는 칸 " + solid + "이 건물 " + columns + "x" + rows + " 밖이다");
            if (!solid.Contains(door)) throw new ArgumentException("문 칸 " + door + "이 막는 칸 " + solid + " 밖이다");
            Columns = columns;
            Rows = rows;
            Solid = solid;
            Door = door;
        }
    }

    /// <summary>맵의 부지 하나: 이름, 예약한 칸, 건물 문이 올 칸(기준점). 어느 단계의 건물도 예약한 칸 안에 들어가야 한다.</summary>
    public sealed class BuildingLot
    {
        public readonly string Name;
        public readonly CellRect Cells;
        public readonly CellPos DoorCell;

        public BuildingLot(string name, CellRect cells, CellPos doorCell)
        {
            Name = name ?? string.Empty;
            Cells = cells;
            DoorCell = doorCell;
        }
    }

    /// <summary>놓은 결과: 건물 왼쪽 위 칸, 막는 칸(문 칸 제외), 문 칸. 놓을 수 없으면 Ok가 false이고 Problem에 까닭이 있다.</summary>
    public sealed class BuildingPlacementResult
    {
        private static readonly CellPos[] None = Array.Empty<CellPos>();

        public readonly bool Ok;
        public readonly string Problem;
        public readonly CellPos TopLeft, Door;
        public readonly IReadOnlyList<CellPos> Blocked;

        private BuildingPlacementResult(bool ok, string problem, CellPos topLeft, CellPos door, IReadOnlyList<CellPos> blocked)
        {
            Ok = ok; Problem = problem; TopLeft = topLeft; Door = door; Blocked = blocked ?? None;
        }

        internal static BuildingPlacementResult Placed(CellPos topLeft, CellPos door, IReadOnlyList<CellPos> blocked) => new BuildingPlacementResult(true, null, topLeft, door, blocked);
        internal static BuildingPlacementResult Refused(string problem) => new BuildingPlacementResult(false, problem, default, default, None);

        /// <summary>문 칸 바로 아래 칸(문 앞).</summary>
        public CellPos DoorFront => new CellPos(Door.X, Door.Y + 1);

        /// <summary>문 상자: 문 칸 한 칸 그대로(픽셀).</summary>
        public AreaRect DoorArea(int cellSize) => new AreaRect(Door.X * cellSize, Door.Y * cellSize, cellSize, cellSize);
    }

    /// <summary>부지에 건물을 놓는 판정. 기준점은 문이다: 건물이 커져도 문 앞 길과 도착 지점은 그대로다.</summary>
    public static class BuildingPlacement
    {
        public static BuildingPlacementResult Place(BuildingLot lot, BuildingFootprint footprint)
        {
            if (lot == null) throw new ArgumentNullException(nameof(lot));
            if (footprint == null) throw new ArgumentNullException(nameof(footprint));
            var topLeft = new CellPos(lot.DoorCell.X - footprint.Door.X, lot.DoorCell.Y - footprint.Door.Y);
            var whole = new CellRect(topLeft.X, topLeft.Y, footprint.Columns, footprint.Rows);
            if (!lot.Cells.Contains(whole))
                return BuildingPlacementResult.Refused("부지 " + lot.Name + " " + lot.Cells + " 밖으로 건물 " + whole + "이 나간다");
            CellRect solid = footprint.Solid;
            var blocked = new List<CellPos>(solid.W * solid.H - 1);
            for (int y = solid.Y; y < solid.Bottom; y++)
                for (int x = solid.X; x < solid.Right; x++)
                {
                    var cell = new CellPos(topLeft.X + x, topLeft.Y + y);
                    if (!cell.Equals(lot.DoorCell)) blocked.Add(cell);
                }
            return BuildingPlacementResult.Placed(topLeft, lot.DoorCell, blocked);
        }

        /// <summary>단계 값을 건물의 단계 수 안으로: 1보다 작으면 1, 많으면 가장 높은 단계(세이브가 데이터보다 앞서도 멈추지 않게).</summary>
        public static int ClampLevel(int level, int levelCount) => Math.Max(1, Math.Min(level, Math.Max(1, levelCount)));

        /// <summary>부지끼리의 문제: 맵 밖 부지, 예약한 칸 밖의 문 칸, 서로 겹치는 부지. 맵 검사가 모아 보여 준다.</summary>
        public static List<string> CheckLots(IReadOnlyList<BuildingLot> lots, MapGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            var problems = new List<string>();
            if (lots == null) return problems;
            var map = new CellRect(0, 0, grid.Columns, grid.Rows);
            for (int i = 0; i < lots.Count; i++)
            {
                BuildingLot lot = lots[i];
                if (!map.Contains(lot.Cells)) problems.Add("부지 " + lot.Name + " " + lot.Cells + "이 맵 밖으로 나간다(맵 " + grid.Columns + "x" + grid.Rows + ")");
                if (!lot.Cells.Contains(lot.DoorCell)) problems.Add("부지 " + lot.Name + "의 문 칸 " + lot.DoorCell + "이 예약한 칸 " + lot.Cells + " 밖이다");
                for (int j = 0; j < i; j++)
                    if (lots[j].Cells.Overlaps(lot.Cells)) problems.Add("부지 " + lots[j].Name + "와 " + lot.Name + "가 겹친다");
            }
            return problems;
        }
    }
}
