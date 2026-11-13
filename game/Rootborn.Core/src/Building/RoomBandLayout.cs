using System.Collections.Generic;

namespace Rootborn.Core.Building
{
    /// <summary>윗면 칸마다 띠 사각형 목록과 가운데 사각형, 가로 자리 X(픽셀 키트 room_tiles._band_layout을 옮긴 것).
    ///
    /// X(가로 자리): 양옆이 방 쪽(바닥·정면)이면 가운데, 한쪽만 방이면 그쪽에 붙는다. Y(세로 자리): 아래가 정면이면 아래,
    /// 위만 방이면 위. 한쪽만 방인 칸이 양쪽이 방인 벽에 이어지면 그 벽의 자리를 따르고, 정해지지 않은 축은 그 축을 따라
    /// 이어진 윗면에서 받는다(네 번 전파, 그래도 없으면 가운데). 띠 = 가운데 사각형 + 이웃 윗면 쪽 팔. 두 칸이 나누는 변의
    /// 팔 범위는 "양쪽 방"으로 정해진 쪽(더 강한 쪽)의 것. 반복 순서·동점 처리·전파 횟수는 Python과 같다(시험 표가 본다).</summary>
    internal sealed class RoomBandLayout
    {
        private const int C = RoomTilePlan.CellSize;
        private const int Passes = 4;

        private readonly int _w, _h;
        private readonly int[] _tags;

        /// <summary>윗면 칸의 가로 자리 [Lo, Hi)(칸 인덱스 y*w+x). 윗면이 아닌 칸은 쓰지 않는다.</summary>
        internal readonly (int Lo, int Hi)[] X;
        internal readonly (int Lo, int Hi)[] Y;
        internal readonly PixelRect[] Core;
        internal readonly List<PixelRect>[] Rects;

        internal RoomBandLayout(int w, int h, int[] tags, int band, List<int> caps)
        {
            _w = w;
            _h = h;
            _tags = tags;
            int n = w * h;
            var centre = ((C - band) / 2, (C - band) / 2 + band);
            var lo = (0, band);
            var hi = (C - band, C);

            var xs = new (int Lo, int Hi)?[n];
            var ys = new (int Lo, int Hi)?[n];
            var rx = new int[n];
            var ry = new int[n];
            foreach (int i in caps)
            {
                int x = i % w, y = i / w;
                bool a = RoomSide(x - 1, y), b = RoomSide(x + 1, y);
                if (a && b) { xs[i] = centre; rx[i] = 2; }
                else if (a) { xs[i] = lo; rx[i] = 1; }
                else if (b) { xs[i] = hi; rx[i] = 1; }
                if (Tag(x, y + 1) > 0) { ys[i] = hi; ry[i] = 2; }        // 정면 위 윗면은 정면 쪽(아래)에 붙는다
                else if (RoomSide(x, y - 1)) { ys[i] = lo; ry[i] = 1; }  // 위가 방이고 아래가 밖·윗면이면 위
            }
            foreach (int i in caps)
            {
                int x = i % w, y = i / w;
                if (rx[i] == 1)
                {
                    if (IsCap(x, y - 1) && rx[i - w] == 2) xs[i] = xs[i - w];
                    if (IsCap(x, y + 1) && rx[i + w] == 2) xs[i] = xs[i + w];
                }
                if (ry[i] == 1)
                {
                    if (IsCap(x - 1, y) && ry[i - 1] == 2) ys[i] = ys[i - 1];
                    if (IsCap(x + 1, y) && ry[i + 1] == 2) ys[i] = ys[i + 1];
                }
            }
            for (int pass = 0; pass < Passes; pass++)
                foreach (int i in caps)
                {
                    int x = i % w, y = i / w;
                    if (xs[i] == null)
                        xs[i] = Best(xs, rx, IsCap(x, y - 1) ? i - w : -1, IsCap(x, y + 1) ? i + w : -1);
                    if (ys[i] == null)
                        ys[i] = Best(ys, ry, IsCap(x - 1, y) ? i - 1 : -1, IsCap(x + 1, y) ? i + 1 : -1);
                }

            X = new (int, int)[n];
            Y = new (int, int)[n];
            Core = new PixelRect[n];
            Rects = new List<PixelRect>[n];
            foreach (int i in caps)
            {
                X[i] = xs[i] ?? centre;
                Y[i] = ys[i] ?? centre;
            }
            foreach (int i in caps)
            {
                int x = i % w, y = i / w;
                var cx = X[i];
                var cy = Y[i];
                var core = new PixelRect(cx.Lo, cy.Lo, cx.Hi, cy.Hi);
                var rs = new List<PixelRect>(5) { core };
                if (IsCap(x, y - 1))
                {
                    var ex = rx[i - w] >= rx[i] ? X[i - w] : X[i];
                    rs.Add(new PixelRect(ex.Lo, 0, ex.Hi, cy.Hi));
                }
                if (IsCap(x, y + 1))
                {
                    var ex = rx[i] >= rx[i + w] ? X[i] : X[i + w];
                    rs.Add(new PixelRect(ex.Lo, cy.Lo, ex.Hi, C));
                }
                if (IsCap(x - 1, y))
                {
                    var ey = ry[i - 1] >= ry[i] ? Y[i - 1] : Y[i];
                    rs.Add(new PixelRect(0, ey.Lo, cx.Hi, ey.Hi));
                }
                if (IsCap(x + 1, y))
                {
                    var ey = ry[i] >= ry[i + 1] ? Y[i] : Y[i + 1];
                    rs.Add(new PixelRect(cx.Lo, ey.Lo, C, ey.Hi));
                }
                Core[i] = core;
                Rects[i] = rs;
            }
        }

        /// <summary>Python max([(R[n], X[n]) …]): 세기 다음 (Lo, Hi) 순으로 가장 큰 것, 같으면 앞의 것.</summary>
        private static (int Lo, int Hi)? Best((int Lo, int Hi)?[] v, int[] r, int a, int b)
        {
            (int Lo, int Hi)? best = null;
            int bestR = 0;
            foreach (int n in new[] { a, b })
            {
                if (n < 0 || v[n] == null)
                    continue;
                var cand = v[n].Value;
                if (best == null || r[n] > bestR || r[n] == bestR && (cand.Lo > best.Value.Lo || cand.Lo == best.Value.Lo && cand.Hi > best.Value.Hi))
                {
                    best = cand;
                    bestR = r[n];
                }
            }
            return best;
        }

        private int Tag(int x, int y) => RoomTileKinds.TagAt(_tags, _w, _h, x, y);

        private bool IsCap(int x, int y) => Tag(x, y) == RoomTileKinds.Cap;

        private bool RoomSide(int x, int y)
        {
            int t = Tag(x, y);
            return t == RoomTileKinds.Floor || t > 0;
        }
    }
}
