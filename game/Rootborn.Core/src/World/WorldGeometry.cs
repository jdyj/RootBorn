using System;
using System.Collections.Generic;

namespace Rootborn.Core.World
{
    /// <summary>칸 좌표. 맵 왼쪽 위가 (0,0)이고 아래로 갈수록 Y가 커진다(LDtk와 같다).</summary>
    public readonly struct CellPos : IEquatable<CellPos>
    {
        public readonly int X, Y;

        public CellPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>칸 가운데의 픽셀 좌표.</summary>
        public static (float X, float Y) Center(CellPos cell, int cellSize) => ((cell.X + 0.5f) * cellSize, (cell.Y + 0.5f) * cellSize);

        /// <summary>픽셀 좌표가 들어 있는 칸.</summary>
        public static CellPos Containing(float x, float y, int cellSize) => new CellPos((int)Math.Floor(x / cellSize), (int)Math.Floor(y / cellSize));

        public bool Equals(CellPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is CellPos other && Equals(other);
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663);
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    /// <summary>픽셀 단위 사각형. 경계에 닿기만 한 것은 겹친 것이 아니다.</summary>
    public readonly struct AreaRect : IEquatable<AreaRect>
    {
        public readonly float X, Y, W, H;

        public AreaRect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float Right => X + W;
        public float Bottom => Y + H;
        public float Top => Y;
        public float Left => X;
        public float CenterX => X + W / 2f;
        public float CenterY => Y + H / 2f;

        public static AreaRect Centered(float centerX, float centerY, float w, float h) => new AreaRect(centerX - w / 2f, centerY - h / 2f, w, h);

        public bool Overlaps(AreaRect other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

        /// <summary>점에서 사각형까지 거리. 안에 있으면 0.</summary>
        public float DistanceTo(float x, float y)
        {
            float dx = Math.Max(Math.Max(X - x, 0f), x - Right);
            float dy = Math.Max(Math.Max(Y - y, 0f), y - Bottom);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(AreaRect o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object obj) => obj is AreaRect o && Equals(o);
        public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() * 3) ^ (W.GetHashCode() * 7) ^ (H.GetHashCode() * 13);
        public override string ToString() => "[" + X + "," + Y + " " + W + "x" + H + "]";
    }

    /// <summary>맵 칸 수와 칸 크기(픽셀). 크기는 언제나 읽어 온 맵에서 온다.</summary>
    public sealed class MapGrid
    {
        public readonly int Columns, Rows, CellSize;

        public MapGrid(int columns, int rows, int cellSize)
        {
            if (columns <= 0 || rows <= 0 || cellSize <= 0) throw new ArgumentException("맵 크기와 칸 크기는 양수여야 한다");
            Columns = columns;
            Rows = rows;
            CellSize = cellSize;
        }

        public int Count => Columns * Rows;
        public float PixelWidth => Columns * CellSize;
        public float PixelHeight => Rows * CellSize;
        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows;
        public int Index(int x, int y) => y * Columns + x;
    }

    /// <summary>타일 층 하나가 어느 칸에 타일을 가졌는지. 이름은 맵 원본(LDtk)의 층 이름이다.</summary>
    public sealed class MapLayer
    {
        public readonly string Name;
        private readonly bool[] _occupied;

        public MapLayer(string name, bool[] occupied)
        {
            Name = name ?? string.Empty;
            _occupied = occupied ?? throw new ArgumentNullException(nameof(occupied));
        }

        public int Count => _occupied.Length;
        public bool Has(int index) => _occupied[index];
    }

    /// <summary>막힌 칸 표. 맵 밖은 막혔다 — 출구 상자가 맵 밖까지 덮은 칸만 예외로 열려 있다(Unity 판 굽기의 테두리 규칙).</summary>
    public sealed class CollisionGrid
    {
        public readonly MapGrid Grid;
        private readonly bool[] _blocked;
        private readonly HashSet<CellPos> _openOutside;

        public CollisionGrid(MapGrid grid, bool[] blocked, IEnumerable<CellPos> openOutside = null)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (blocked == null || blocked.Length != grid.Count) throw new ArgumentException("막힌 칸 표의 크기가 맵과 다르다");
            _blocked = blocked;
            _openOutside = new HashSet<CellPos>();
            if (openOutside != null)
                foreach (CellPos c in openOutside) if (!grid.Contains(c.X, c.Y)) _openOutside.Add(c);
        }

        public int Columns => Grid.Columns;
        public int Rows => Grid.Rows;
        public IEnumerable<CellPos> OpenOutside => _openOutside;

        public bool IsBlocked(int x, int y) => Grid.Contains(x, y) ? _blocked[Grid.Index(x, y)] : !_openOutside.Contains(new CellPos(x, y));
        public bool IsBlocked(CellPos cell) => IsBlocked(cell.X, cell.Y);

        /// <summary>경계에서 이만큼(픽셀) 안쪽까지는 옆 칸에 걸친 것으로 치지 않는다. 걸음 계산의 부동소수 오차(144.00006 같은)를 흡수한다.</summary>
        public const float EdgeTolerance = 0.001f;

        /// <summary>사각형이 겹치는 칸 중 하나라도 막혔는가.</summary>
        public bool AnyBlocked(AreaRect area)
        {
            int size = Grid.CellSize;
            int x0 = (int)Math.Floor((area.X + EdgeTolerance) / size), x1 = (int)Math.Ceiling((area.Right - EdgeTolerance) / size) - 1;
            int y0 = (int)Math.Floor((area.Y + EdgeTolerance) / size), y1 = (int)Math.Ceiling((area.Bottom - EdgeTolerance) / size) - 1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (IsBlocked(x, y)) return true;
            return false;
        }

        public IEnumerable<CellPos> BlockedCells()
        {
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                    if (_blocked[Grid.Index(x, y)]) yield return new CellPos(x, y);
        }
    }
}
