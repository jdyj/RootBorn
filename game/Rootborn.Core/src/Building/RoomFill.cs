using System;
using System.Collections.Generic;
using Rootborn.Core.World;

namespace Rootborn.Core.Building
{
    /// <summary>
    /// 방 격자의 바닥을 방과 문간으로 나눈다(방 채우기 붓이 덮는 칸). 규칙:
    /// 1. 좁은 칸: 그 칸을 지나는 가로 또는 세로 바닥 줄(벽·밖에서 끊기는 연속 바닥 칸 수)이 <see cref="NarrowRun"/> 이하인 바닥 칸.
    /// 2. 방 바탕: 좁지 않은 바닥 칸의 4방향 덩어리.
    /// 3. 좁은 칸의 4방향 덩어리마다: 맞닿은 방 바탕이 하나면 그 방에 든다, 둘 이상이면 문간, 없으면 혼자 한 방.
    /// 4. 채우기: 시작 칸이 방이면 그 방 + 그 방에 맞닿은 문간 칸, 시작 칸이 문간이면 그 문간 덩어리만.
    /// 맵 id·크기를 모른다. 목록은 <see cref="Build"/> 때 방·문간마다 한 번 만들고 같은 인스턴스를 돌려준다.
    /// </summary>
    public sealed class RoomFill
    {
        public const int NarrowRun = 2;

        private static readonly IReadOnlyList<CellPos> NoCells = Array.Empty<CellPos>();

        private readonly int _w, _h;
        private readonly IReadOnlyList<CellPos>[] _listOf;   // 칸 → 채우기 목록(바닥이 아니면 null)
        private readonly bool[] _doorway;

        private RoomFill(int w, int h, IReadOnlyList<CellPos>[] listOf, bool[] doorway, int roomCount)
        {
            _w = w;
            _h = h;
            _listOf = listOf;
            _doorway = doorway;
            RoomCount = roomCount;
        }

        /// <summary>방 수(문간은 세지 않는다).</summary>
        public int RoomCount { get; }

        public bool IsDoorway(CellPos cell) => Inside(cell) && _doorway[cell.Y * _w + cell.X];

        /// <summary>그 칸에서 채우는 칸들(y 다음 x 순). 바닥이 아니면 빈 목록. 같은 방(또는 같은 문간)이면 같은 인스턴스.</summary>
        public IReadOnlyList<CellPos> RoomOf(CellPos floorCell) =>
            Inside(floorCell) ? _listOf[floorCell.Y * _w + floorCell.X] ?? NoCells : NoCells;

        private bool Inside(CellPos c) => c.X >= 0 && c.Y >= 0 && c.X < _w && c.Y < _h;

        public static RoomFill Build(RoomGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            int w = grid.Columns, h = grid.Rows, n = w * h;
            var floor = new bool[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    floor[y * w + x] = grid.At(x, y) == RoomCells.Floor;

            var narrow = Narrow(floor, w, h);

            // 2. 방 바탕 덩어리
            var baseId = new int[n];
            for (int i = 0; i < n; i++) baseId[i] = -1;
            int bases = Label(floor, w, h, i => !narrow[i], baseId);

            // 3. 좁은 덩어리마다 맞닿은 방 바탕
            var narrowId = new int[n];
            for (int i = 0; i < n; i++) narrowId[i] = -1;
            int narrowGroups = Label(floor, w, h, i => narrow[i], narrowId);
            var touching = new List<int>[narrowGroups];
            for (int g = 0; g < narrowGroups; g++) touching[g] = new List<int>(2);
            for (int i = 0; i < n; i++)
            {
                if (narrowId[i] < 0) continue;
                int x = i % w, y = i / w;
                AddTouch(touching[narrowId[i]], baseId, w, h, x + 1, y);
                AddTouch(touching[narrowId[i]], baseId, w, h, x - 1, y);
                AddTouch(touching[narrowId[i]], baseId, w, h, x, y + 1);
                AddTouch(touching[narrowId[i]], baseId, w, h, x, y - 1);
            }

            // 방 번호: 방 바탕 0..bases-1, 혼자인 좁은 덩어리는 그 뒤. 문간은 문간 번호.
            int rooms = bases;
            var groupRoom = new int[narrowGroups];       // 방에 들면 방 번호, 문간이면 -1
            var groupDoorway = new int[narrowGroups];    // 문간 번호(아니면 -1)
            int doorways = 0;
            for (int g = 0; g < narrowGroups; g++)
            {
                groupDoorway[g] = -1;
                if (touching[g].Count == 1) groupRoom[g] = touching[g][0];
                else if (touching[g].Count == 0) groupRoom[g] = rooms++;
                else
                {
                    groupRoom[g] = -1;
                    groupDoorway[g] = doorways++;
                }
            }

            var roomLists = new List<CellPos>[rooms];
            for (int r = 0; r < rooms; r++) roomLists[r] = new List<CellPos>();
            var doorwayLists = new List<CellPos>[doorways];
            for (int d = 0; d < doorways; d++) doorwayLists[d] = new List<CellPos>();

            var listOf = new IReadOnlyList<CellPos>[n];
            var isDoorway = new bool[n];
            // 칸 순서(y 다음 x)로 훑어 넣으므로 목록은 정렬되어 있다
            for (int i = 0; i < n; i++)
            {
                if (!floor[i]) continue;
                var cell = new CellPos(i % w, i / w);
                if (baseId[i] >= 0)
                {
                    roomLists[baseId[i]].Add(cell);
                    continue;
                }
                int g = narrowId[i];
                if (groupDoorway[g] < 0)
                {
                    roomLists[groupRoom[g]].Add(cell);
                    continue;
                }
                isDoorway[i] = true;
                doorwayLists[groupDoorway[g]].Add(cell);
                foreach (int b in touching[g])
                    roomLists[b].Add(cell);
            }

            for (int i = 0; i < n; i++)
            {
                if (!floor[i]) continue;
                if (baseId[i] >= 0) listOf[i] = roomLists[baseId[i]];
                else
                {
                    int g = narrowId[i];
                    listOf[i] = groupDoorway[g] >= 0 ? doorwayLists[groupDoorway[g]] : (IReadOnlyList<CellPos>)roomLists[groupRoom[g]];
                }
            }
            return new RoomFill(w, h, listOf, isDoorway, rooms);
        }

        private static bool[] Narrow(bool[] floor, int w, int h)
        {
            int n = w * h;
            var across = new int[n];
            var down = new int[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w;)
                {
                    if (!floor[y * w + x]) { x++; continue; }
                    int end = x;
                    while (end < w && floor[y * w + end]) end++;
                    for (int k = x; k < end; k++) across[y * w + k] = end - x;
                    x = end;
                }
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h;)
                {
                    if (!floor[y * w + x]) { y++; continue; }
                    int end = y;
                    while (end < h && floor[end * w + x]) end++;
                    for (int k = y; k < end; k++) down[k * w + x] = end - y;
                    y = end;
                }
            var narrow = new bool[n];
            for (int i = 0; i < n; i++)
                narrow[i] = floor[i] && (across[i] <= NarrowRun || down[i] <= NarrowRun);
            return narrow;
        }

        /// <summary>조건에 맞는 바닥 칸의 4방향 덩어리에 번호를 붙인다(칸 순서로 처음 만난 덩어리가 0). 덩어리 수를 돌려준다.</summary>
        private static int Label(bool[] floor, int w, int h, Func<int, bool> include, int[] id)
        {
            int groups = 0;
            var stack = new Stack<int>();
            for (int start = 0; start < floor.Length; start++)
            {
                if (!floor[start] || id[start] >= 0 || !include(start)) continue;
                id[start] = groups;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int i = stack.Pop(), x = i % w, y = i / w;
                    Visit(x + 1, y);
                    Visit(x - 1, y);
                    Visit(x, y + 1);
                    Visit(x, y - 1);
                }
                groups++;
            }
            return groups;

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int j = y * w + x;
                if (!floor[j] || id[j] >= 0 || !include(j)) return;
                id[j] = groups;
                stack.Push(j);
            }
        }

        private static void AddTouch(List<int> list, int[] baseId, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int b = baseId[y * w + x];
            if (b >= 0 && !list.Contains(b)) list.Add(b);
        }
    }
}
