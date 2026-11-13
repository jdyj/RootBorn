using System;
using System.Collections.Generic;

namespace Rootborn.Core.Building
{
    /// <summary>방 격자 칸의 값. 0 밖, 1 벽, 2 바닥(픽셀 키트 room_tiles의 OUT/WALL/FLOOR와 같다).</summary>
    public static class RoomCells
    {
        public const int Out = 0, Wall = 1, Floor = 2;
    }

    /// <summary>칸마다 밖/벽/바닥인 방 격자. 줄 순서(y가 아래로 +, CellPos와 같다), 격자 밖은 <see cref="RoomCells.Out"/>.
    /// 맵 id·크기를 모른다 — 집 맵이 바뀌어도 격자만 바뀐다.</summary>
    public sealed class RoomGrid
    {
        private readonly int[] _cells;

        public RoomGrid(int columns, int rows, IReadOnlyList<int> cells)
        {
            if (columns < 0)
                throw new ArgumentOutOfRangeException(nameof(columns), columns, "격자 너비는 0 이상이어야 한다");
            if (rows < 0)
                throw new ArgumentOutOfRangeException(nameof(rows), rows, "격자 높이는 0 이상이어야 한다");
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));
            if ((long)columns * rows != cells.Count)
                throw new ArgumentException($"격자 {columns}x{rows}에는 칸 값이 {(long)columns * rows}개여야 한다(지금 {cells.Count}개)", nameof(cells));
            _cells = new int[cells.Count];
            for (int i = 0; i < _cells.Length; i++)
            {
                int v = cells[i];
                if (v != RoomCells.Out && v != RoomCells.Wall && v != RoomCells.Floor)
                    throw new ArgumentException($"격자 ({i % columns}, {i / columns})의 값 {v}은 0(밖)·1(벽)·2(바닥)이 아니다", nameof(cells));
                _cells[i] = v;
            }
            Columns = columns;
            Rows = rows;
        }

        public int Columns { get; }
        public int Rows { get; }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows;

        /// <summary>칸 (x, y)의 값. 격자 밖은 <see cref="RoomCells.Out"/>.</summary>
        public int At(int x, int y) => Contains(x, y) ? _cells[y * Columns + x] : RoomCells.Out;
    }
}
