using System;
using System.Collections.Generic;

namespace Rootborn.Ui
{
    /// <summary>
    /// 9조각 패널이 원본 그림에서 차지하는 자리. 세로선 4개(X)와 가로선 4개(Y)로 3x3 칸을 나눈다.
    /// 테두리 두께는 변마다 다를 수 있다. 값은 PNG를 직접 재서 정한다(16px 칸 단위로 추정하지 않는다).
    /// </summary>
    public sealed class NineSliceSource
    {
        private readonly int[] _x, _y;

        public NineSliceSource(int x0, int x1, int x2, int x3, int y0, int y1, int y2, int y3)
        {
            _x = new[] { x0, x1, x2, x3 };
            _y = new[] { y0, y1, y2, y3 };
        }

        public int ColX(int col) => _x[col];
        public int RowY(int row) => _y[row];
        public int ColW(int col) => _x[col + 1] - _x[col];
        public int RowH(int row) => _y[row + 1] - _y[row];
    }

    /// <summary>화면의 Dest에 원본의 Source를 그린다.</summary>
    public readonly record struct SlicePiece(PixelRect Dest, PixelRect Source);

    /// <summary>
    /// 모서리는 한 번, 변과 가운데는 타일을 반복해 채운다(Style2 공통 패널 규약: 가운데 칸을 반복).
    /// 타일은 정수 배율로만 키운다. 끝에 남는 폭은 원본도 그만큼 잘라 그린다.
    /// </summary>
    public static class NineSlice
    {
        public static void Build(PixelRect dest, NineSliceSource source, int scale, List<SlicePiece> into)
        {
            int left = Math.Min(source.ColW(0) * scale, dest.W / 2), right = Math.Min(source.ColW(2) * scale, dest.W - left);
            int top = Math.Min(source.RowH(0) * scale, dest.H / 2), bottom = Math.Min(source.RowH(2) * scale, dest.H - top);
            for (int row = 0; row < 3; row++)
            {
                int y = row == 0 ? dest.Y : row == 1 ? dest.Y + top : dest.Y + dest.H - bottom;
                int spanH = row == 0 ? top : row == 1 ? dest.H - top - bottom : bottom;
                int stepH = row == 1 ? source.RowH(1) * scale : spanH;
                for (int col = 0; col < 3; col++)
                {
                    int x = col == 0 ? dest.X : col == 1 ? dest.X + left : dest.X + dest.W - right;
                    int spanW = col == 0 ? left : col == 1 ? dest.W - left - right : right;
                    int stepW = col == 1 ? source.ColW(1) * scale : spanW;
                    for (int oy = 0; oy < spanH; oy += stepH)
                    {
                        int h = Math.Min(stepH, spanH - oy);
                        int sourceH = Math.Min(source.RowH(row), (h + scale - 1) / scale);
                        for (int ox = 0; ox < spanW; ox += stepW)
                        {
                            int w = Math.Min(stepW, spanW - ox);
                            int sourceW = Math.Min(source.ColW(col), (w + scale - 1) / scale);
                            into.Add(new SlicePiece(new PixelRect(x + ox, y + oy, w, h),
                                new PixelRect(source.ColX(col), source.RowY(row), sourceW, sourceH)));
                        }
                    }
                }
            }
        }
    }
}
