using System;
using System.Collections.Generic;
using System.Text;

namespace Rootborn.Core.Building
{
    /// <summary>칸 안 픽셀 사각형. 반열린 구간 [X0, X1) × [Y0, Y1), 칸 왼쪽 위가 (0, 0)이고 y는 아래로 +.</summary>
    public readonly struct PixelRect : IEquatable<PixelRect>
    {
        public readonly int X0, Y0, X1, Y1;

        public PixelRect(int x0, int y0, int x1, int y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public int Area => (X1 - X0) * (Y1 - Y0);

        public bool Equals(PixelRect o) => X0 == o.X0 && Y0 == o.Y0 && X1 == o.X1 && Y1 == o.Y1;
        public override bool Equals(object obj) => obj is PixelRect o && Equals(o);
        public override int GetHashCode() => ((X0 * 31 + Y0) * 31 + X1) * 31 + Y1;
        public override string ToString() => X0 + "," + Y0 + "," + X1 + "," + Y1;
    }

    /// <summary>칸 안 픽셀 집합의 정규형(픽셀 키트 room_tiles._encode(픽셀, 0, 32)와 같은 글과 사각형).
    /// 줄마다 가로로 이어진 구간(왼쪽부터)으로 나누고, 위아래로 이어진 줄들의 구간 목록이 똑같으면 한 띠로 묶는다.
    /// 띠는 위에서 아래로, 띠 안 구간은 왼쪽에서 오른쪽으로, "x0,y0,x1,y1"을 "+"로 잇는다.</summary>
    public static class PixelRects
    {
        private const int C = RoomTilePlan.CellSize;

        public static string Encode(IEnumerable<(int X, int Y)> pixels) => Text(Rects(pixels));

        public static IReadOnlyList<PixelRect> Rects(IEnumerable<(int X, int Y)> pixels)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            var rows = new uint[C];
            foreach (var (x, y) in pixels)
            {
                if (x < 0 || y < 0 || x >= C || y >= C)
                    throw new ArgumentOutOfRangeException(nameof(pixels), $"픽셀 ({x}, {y})이 칸 [0, {C}) 밖이다");
                rows[y] |= 1u << x;
            }
            return FromRows(rows);
        }

        /// <summary>줄마다 비트 묶음(비트 x = 픽셀 x)에서 사각형. 구간 목록이 같은 줄 = 비트 묶음이 같은 줄.</summary>
        internal static List<PixelRect> FromRows(uint[] rows)
        {
            var rects = new List<PixelRect>();
            uint prev = 0;
            int top = 0;
            for (int y = 0; y <= C; y++)
            {
                uint cur = y < C ? rows[y] : 0;
                if (cur == prev)
                    continue;
                AddRuns(rects, prev, top, y);
                prev = cur;
                top = y;
            }
            return rects;
        }

        internal static string Text(IReadOnlyList<PixelRect> rects)
        {
            var sb = new StringBuilder(rects.Count * 12);
            for (int i = 0; i < rects.Count; i++)
            {
                if (i > 0)
                    sb.Append('+');
                var r = rects[i];
                sb.Append(r.X0).Append(',').Append(r.Y0).Append(',').Append(r.X1).Append(',').Append(r.Y1);
            }
            return sb.ToString();
        }

        private static void AddRuns(List<PixelRect> rects, uint mask, int y0, int y1)
        {
            int x = 0;
            while (x < C)
            {
                if ((mask & (1u << x)) == 0)
                {
                    x++;
                    continue;
                }
                int start = x;
                while (x < C && (mask & (1u << x)) != 0)
                    x++;
                rects.Add(new PixelRect(start, y0, x, y1));
            }
        }
    }
}
