using System;
using System.Collections.Generic;
using Rootborn.Core.World;

namespace Rootborn.Core.Building
{
    /// <summary>방 격자 → 칸마다 픽셀 출처표. 픽셀 키트 room_tiles._plan·_cell_sources의 계산을 그대로 옮긴 것이고, 결과는 키트가 낸
    /// 시험 표(Tools/pixel-kit/tests/fixtures/room-vectors.json)와 칸·부분·픽셀 단위로 같다(Core 테스트 RoomTileVectorTests).
    /// 그림·색·PNG를 모른다 — 조각을 붙이는 일은 World·Host가 한다. 꼴과 뜻은 Tools/pixel-kit/README.md "방 만들기 — 시험 표".
    ///
    /// 좌표: 맵 칸은 <see cref="CellPos"/>(y 아래로 +), 칸 안 픽셀은 칸 왼쪽 위가 (0, 0)이고 y 아래로 +, 사각형은 반열린 구간.</summary>
    public sealed class RoomTilePlan
    {
        public const int CellSize = 32;
        /// <summary>띠 두께 하한(외곽선 둘 + 면). 상한은 칸 크기 미만.</summary>
        public const int MinBand = 4;

        private readonly CellTiles[] _byIndex;

        private RoomTilePlan(RoomGrid grid, int faceHeight, int band, CellTiles[] byIndex, IReadOnlyList<CellTiles> cells)
        {
            Grid = grid;
            FaceHeight = faceHeight;
            Band = band;
            _byIndex = byIndex;
            Cells = cells;
        }

        public RoomGrid Grid { get; }
        public int FaceHeight { get; }
        public int Band { get; }
        /// <summary>밖이 아닌 칸의 출처표, 줄 순서(y 다음 x).</summary>
        public IReadOnlyList<CellTiles> Cells { get; }

        /// <summary>칸 (x, y)의 출처표. 밖이거나 격자 밖이면 null.</summary>
        public CellTiles At(int x, int y) => Grid.Contains(x, y) ? _byIndex[y * Grid.Columns + x] : null;

        /// <summary>격자 → 출처표.</summary>
        /// <param name="faceHeight">벽 정면이 바닥 위로 몇 칸인가(1 이상).</param>
        /// <param name="band">윗면 띠 두께 px(<see cref="MinBand"/> 이상 <see cref="CellSize"/> 미만).</param>
        /// <param name="wallDoors">벽 칸의 문 칸들(LDtk에서는 벽 칸에 놓인 Door). null이나 빈 목록이면 문 없음. 윗면 칸이고 위·오른쪽·왼쪽(이 순서)
        /// 중 바닥이 있으면 그 칸의 띠 대신 문지방(sill), 바닥 바로 위 정면 칸이면 문짝(door). 그 밖의 칸(바닥, 밖, 격자 밖, 바닥이 붙지 않은
        /// 윗면, 위가 막힌 정면)의 문은 무시한다. 같은 칸이 여러 번 나와도 한 번이다.</param>
        public static RoomTilePlan Build(RoomGrid grid, int faceHeight, int band, IReadOnlyCollection<CellPos> wallDoors)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (faceHeight < 1)
                throw new ArgumentOutOfRangeException(nameof(faceHeight), faceHeight, "faceHeight는 1 이상이어야 한다");
            if (band < MinBand || band >= CellSize)
                throw new ArgumentOutOfRangeException(nameof(band), band, $"띠 두께 band는 {MinBand} 이상 {CellSize} 미만이어야 한다");
            var builder = new Builder(grid, faceHeight, band, wallDoors);
            var (byIndex, cells) = builder.Run();
            return new RoomTilePlan(grid, faceHeight, band, byIndex, cells);
        }

        /// <summary>한 번의 계산. 칸 안 픽셀 분류 코드: 띠, 좁은 정면, 문지방, 비침(바닥 쪽), 비어 있음(밖 쪽), 그리고 FaceBase 이상은 늘인 정면 열쇠 번호.</summary>
        private sealed class Builder
        {
            private const int C = CellSize;
            private const int Pixels = C * C;
            private const int Reach = 2;   // 띠 칠이 보는 반경(외곽선 1, 안쪽 빛·그늘 2)
            private const byte Unset = 0, BandPx = 1, NarrowPx = 2, SillPx = 3, FloorPx = 4, VoidPx = 5, FaceBase = 6;
            private const byte NoOrigin = 255;

            private readonly RoomGrid _grid;
            private readonly int _w, _h, _band;
            private readonly int[] _tags;
            private readonly string[] _classify;
            private readonly string[] _keys;          // classify 값, 좁은 정면 옆 정면은 끝 표시를 뗀 것
            private readonly HashSet<CellPos> _doors;

            private readonly List<string> _faceKeys = new List<string>();
            private readonly List<int> _faceLevels = new List<int>();
            private readonly Dictionary<string, byte> _faceCodes = new Dictionary<string, byte>(StringComparer.Ordinal);

            private RoomBandLayout _layout;
            private byte[][] _cls;
            private byte[][] _org;                    // 출처 이웃 (dx+1) + (dy+1)*3, 없으면 NoOrigin
            private (int Lo, int Hi)?[] _narrow;
            private string[] _sills;
            private int[] _towards;                   // 문지방 칸 → 바닥 쪽 방향 코드, 없으면 -1
            private bool[] _doorFaces;
            private string[] _ground;
            private bool[] _bandMap;
            private int _pw, _ph;

            internal Builder(RoomGrid grid, int faceHeight, int band, IReadOnlyCollection<CellPos> wallDoors)
            {
                _grid = grid;
                _w = grid.Columns;
                _h = grid.Rows;
                _band = band;
                _tags = RoomTileKinds.Tags(grid, faceHeight);
                _classify = RoomTileKinds.Classify(_tags, _w, _h);
                _keys = (string[])_classify.Clone();
                _doors = wallDoors == null ? new HashSet<CellPos>() : new HashSet<CellPos>(wallDoors);
            }

            private int Tag(int x, int y) => RoomTileKinds.TagAt(_tags, _w, _h, x, y);

            private static int Dir(int dx, int dy) => dx + 1 + (dy + 1) * 3;

            private byte FaceCode(string key)
            {
                if (_faceCodes.TryGetValue(key, out byte code))
                    return code;
                if (_faceKeys.Count >= 255 - FaceBase)
                    throw new InvalidOperationException("늘인 정면 열쇠가 너무 많다");
                code = (byte)(FaceBase + _faceKeys.Count);
                _faceKeys.Add(key);
                _faceLevels.Add(int.Parse(key.Split(':')[1]));
                _faceCodes.Add(key, code);
                return code;
            }

            /// <summary>이웃 칸에서 오는 픽셀 종류: 바닥이면 비침, 정면이면 끝 표시를 뗀 정면, 그 밖은 비어 있음.</summary>
            private byte Side(int nx, int ny)
            {
                int t = Tag(nx, ny);
                if (t == RoomTileKinds.Floor)
                    return FloorPx;
                if (t > 0)
                    return FaceCode(RoomTileKinds.StripLeftRight(_keys[ny * _w + nx]));
                return VoidPx;
            }

            internal (CellTiles[], IReadOnlyList<CellTiles>) Run()
            {
                int n = _w * _h;
                var caps = new List<int>();
                for (int i = 0; i < n; i++)
                    if (_tags[i] == RoomTileKinds.Cap)
                        caps.Add(i);
                _layout = new RoomBandLayout(_w, _h, _tags, _band, caps);

                _doorFaces = new bool[n];
                foreach (var d in _doors)
                    if (_grid.Contains(d.X, d.Y) && _tags[d.Y * _w + d.X] == 1)
                        _doorFaces[d.Y * _w + d.X] = true;

                FindNarrowFaces(n);
                _cls = new byte[n][];
                _org = new byte[n][];
                foreach (int i in caps)
                    ClassifyCap(i);
                _sills = new string[n];
                _towards = new int[n];
                for (int i = 0; i < n; i++)
                    _towards[i] = -1;
                foreach (var d in _doors)
                    PlaceSill(d);
                for (int i = 0; i < n; i++)
                    if (_narrow[i] != null)
                        ClassifyNarrow(i);
                BuildBandMap();
                BuildGround(n);

                var byIndex = new CellTiles[n];
                var cells = new List<CellTiles>(n);
                for (int i = 0; i < n; i++)
                {
                    if (_classify[i] == null)
                        continue;
                    var tiles = new CellTiles(new CellPos(i % _w, i / _w), _classify[i], Sources(i));
                    byIndex[i] = tiles;
                    cells.Add(tiles);
                }
                return (byIndex, cells);
            }

            /// <summary>좁은 정면: 양쪽 끝 표시가 있는 정면 칸 위로 정면을 따라 올라가 윗면을 만나면, 그 윗면의 띠 가로 자리 폭으로 좁힌다.
            /// 문을 박은 정면은 좁히지 않는다. 좁은 정면 옆 정면은 그쪽 끝 표시를 뗀다(외곽선은 좁은 정면의 옆변이 친다).</summary>
            private void FindNarrowFaces(int n)
            {
                _narrow = new (int, int)?[n];
                var order = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    if (_doorFaces[i] || _tags[i] <= 0 || !RoomTileKinds.HasBothEnds(_keys[i]))
                        continue;
                    int x = i % _w, yy = i / _w - 1;
                    while (Tag(x, yy) > 0)
                        yy--;
                    if (Tag(x, yy) == RoomTileKinds.Cap)
                    {
                        _narrow[i] = _layout.X[yy * _w + x];
                        order.Add(i);
                    }
                }
                foreach (int i in order)
                {
                    int x = i % _w, y = i / _w;
                    StripFlag(x - 1, y, 'r');
                    StripFlag(x + 1, y, 'l');
                }
            }

            private void StripFlag(int nx, int ny, char flag)
            {
                if (nx < 0 || nx >= _w)
                    return;
                int ni = ny * _w + nx;
                if (_tags[ni] <= 0 || _narrow[ni] != null)
                    return;
                var parts = _keys[ni].Split(':');
                if (parts.Length != 3 || parts[2].IndexOf(flag) < 0)
                    return;
                string rest = parts[2].Replace(flag.ToString(), "");
                _keys[ni] = rest.Length > 0 ? "face:" + parts[1] + ":" + rest : "face:" + parts[1];
            }

            /// <summary>윗면 칸의 픽셀: 띠 사각형은 띠, 나머지는 그 픽셀이 든 쪽의 두 변 중 윗면이 아닌 이웃(띠 옆 픽셀은 옆 이웃 먼저),
            /// 둘 다 윗면이면 대각 이웃에서 온다. 둘레가 모두 윗면이면 띠로 메운다. 바닥이 비치는 칸의 밖 쪽 자리도 띠로 메운다.</summary>
            private void ClassifyCap(int i)
            {
                int x = i % _w, y = i / _w;
                var around = new byte[9];      // 0 = 그쪽이 윗면
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && Tag(x + dx, y + dy) != RoomTileKinds.Cap)
                            around[Dir(dx, dy)] = Side(x + dx, y + dy);
                var cls = new byte[Pixels];
                var org = new byte[Pixels];
                for (int p = 0; p < Pixels; p++)
                    org[p] = NoOrigin;
                foreach (var r in _layout.Rects[i])
                    for (int py = r.Y0; py < r.Y1; py++)
                        for (int px = r.X0; px < r.X1; px++)
                            cls[py * C + px] = BandPx;
                var core = _layout.Core[i];
                bool anyFloor = false, anyVoid = false;
                for (int py = 0; py < C; py++)
                    for (int px = 0; px < C; px++)
                    {
                        int p = py * C + px;
                        if (cls[p] == Unset)
                        {
                            int hx = px < core.X0 ? -1 : px >= core.X1 ? 1 : px < C / 2 ? -1 : 1;
                            int hy = py < core.Y0 ? -1 : py >= core.Y1 ? 1 : py < C / 2 ? -1 : 1;
                            int first, second;
                            if (px < core.X0 || px >= core.X1)
                            {
                                first = Dir(hx, 0);
                                second = Dir(0, hy);
                            }
                            else
                            {
                                first = Dir(0, hy);
                                second = Dir(hx, 0);
                            }
                            int diagonal = Dir(hx, hy);
                            byte res = BandPx, src = NoOrigin;
                            if (around[first] != 0) { res = around[first]; src = (byte)first; }
                            else if (around[second] != 0) { res = around[second]; src = (byte)second; }
                            else if (around[diagonal] != 0) { res = around[diagonal]; src = (byte)diagonal; }
                            cls[p] = res;
                            org[p] = src;
                        }
                        anyFloor |= cls[p] == FloorPx;
                        anyVoid |= cls[p] == VoidPx;
                    }
                if (anyFloor && anyVoid)
                    for (int p = 0; p < Pixels; p++)
                        if (cls[p] == VoidPx)
                        {
                            cls[p] = BandPx;
                            org[p] = NoOrigin;
                        }
                _cls[i] = cls;
                _org[i] = org;
            }

            /// <summary>윗면 칸의 문: 띠가 지나던 범위를 칸 끝에서 끝까지 문지방으로, 남은 띠 자리는 비침(바닥이 비치는 칸이면) 또는 비어 있음.
            /// 이웃 칸의 띠는 띠 지도에서 이 칸이 빠지므로 끝이 외곽선으로 닫힌다.</summary>
            private void PlaceSill(CellPos d)
            {
                if (!_grid.Contains(d.X, d.Y))
                    return;
                int i = d.Y * _w + d.X;
                if (_cls[i] == null || _narrow[i] != null)
                    return;
                char side;
                int dx, dy;
                if (Tag(d.X, d.Y - 1) == RoomTileKinds.Floor) { side = 'n'; dx = 0; dy = -1; }
                else if (Tag(d.X + 1, d.Y) == RoomTileKinds.Floor) { side = 'e'; dx = 1; dy = 0; }
                else if (Tag(d.X - 1, d.Y) == RoomTileKinds.Floor) { side = 'w'; dx = -1; dy = 0; }
                else return;
                var core = _layout.Core[i];
                int a = side == 'n' ? core.Y0 : core.X0, b = side == 'n' ? core.Y1 : core.X1;
                var cls = _cls[i];
                byte rest = Array.IndexOf(cls, FloorPx) >= 0 ? FloorPx : VoidPx;
                for (int p = 0; p < Pixels; p++)
                {
                    int along = side == 'n' ? p / C : p % C;
                    if (a <= along && along < b)
                        cls[p] = SillPx;
                    else if (cls[p] == BandPx)
                        cls[p] = rest;
                }
                _sills[i] = "sill:" + side + ":" + a + "-" + b;
                _towards[i] = Dir(dx, dy);
            }

            /// <summary>좁은 정면 칸: 열 [x0, x1)은 좁은 정면, 옆은 그쪽 이웃에서(바닥이면 비침, 정면이면 늘인 정면).</summary>
            private void ClassifyNarrow(int i)
            {
                int x = i % _w, y = i / _w;
                var (x0, x1) = _narrow[i].Value;
                byte left = Side(x - 1, y), right = Side(x + 1, y);
                var cls = new byte[Pixels];
                var org = new byte[Pixels];
                for (int p = 0; p < Pixels; p++)
                {
                    int px = p % C;
                    if (x0 <= px && px < x1)
                    {
                        cls[p] = NarrowPx;
                        org[p] = NoOrigin;
                    }
                    else if (px < x0)
                    {
                        cls[p] = left;
                        org[p] = (byte)Dir(-1, 0);
                    }
                    else
                    {
                        cls[p] = right;
                        org[p] = (byte)Dir(1, 0);
                    }
                }
                _cls[i] = cls;
                _org[i] = org;
            }

            private void BuildBandMap()
            {
                _pw = _w * C;
                _ph = _h * C;
                _bandMap = new bool[_pw * _ph];
                for (int i = 0; i < _cls.Length; i++)
                {
                    var cls = _cls[i];
                    if (cls == null)
                        continue;
                    int ox = i % _w * C, oy = i / _w * C;
                    for (int p = 0; p < Pixels; p++)
                        if (cls[p] == BandPx)
                            _bandMap[(oy + p / C) * _pw + ox + p % C] = true;
                }
            }

            /// <summary>정면 칸의 맨 아랫줄에서 정면 그림인 열(비트 묶음) — 그 밑 바닥의 그늘은 이 열에만.</summary>
            private uint FaceColumns(int x, int y)
            {
                int i = y * _w + x;
                if (_narrow[i] == null)
                    return uint.MaxValue;
                var (a, b) = _narrow[i].Value;
                uint mask = 0;
                for (int px = a; px < b; px++)
                    mask |= 1u << px;
                var cls = _cls[i];
                for (int px = 0; px < C; px++)
                    if (cls[(C - 1) * C + px] >= FaceBase)
                        mask |= 1u << px;
                return mask;
            }

            /// <summary>바로 위 칸(x, y − 1)의 맨 아랫줄에서 정면 그림인 열 — 그 밑 바닥 맨 위 두 줄의 그림자는 이 열에만. 정면 칸이면
            /// <see cref="FaceColumns"/>, 띠 칸이면 그 칸 안으로 늘인 정면의 열(가로 벽 정면이 세로 벽 띠 칸으로 들어온 T자 자리), 문지방 칸·그 밖은 없음.</summary>
            private uint FaceAbove(int x, int y)
            {
                if (Tag(x, y - 1) > 0)
                    return FaceColumns(x, y - 1);
                if (y - 1 < 0 || x < 0 || x >= _w || y - 1 >= _h)
                    return 0;
                int i = (y - 1) * _w + x;
                var cls = _cls[i];
                if (cls == null || _sills[i] != null)
                    return 0;
                uint mask = 0;
                for (int px = 0; px < C; px++)
                    if (cls[(C - 1) * C + px] >= FaceBase)
                        mask |= 1u << px;
                return mask;
            }

            private static string ShadeKey(uint cols)
            {
                if (cols == 0)
                    return RoomTileKinds.FloorKey;
                int lo = 0, hi = C;
                while ((cols & (1u << lo)) == 0) lo++;
                while ((cols & (1u << (hi - 1))) == 0) hi--;   // 사이에 빠진 열은 이 칸의 벽 그림에 덮인 자리다
                return lo == 0 && hi == C ? RoomTileKinds.FloorShadeKey : RoomTileKinds.FloorShadeKey + ":" + lo + "-" + hi;
            }

            private void BuildGround(int n)
            {
                _ground = new string[n];
                for (int i = 0; i < n; i++)
                {
                    int x = i % _w, y = i / _w;
                    if (_tags[i] == RoomTileKinds.Floor)
                    {
                        _ground[i] = ShadeKey(FaceAbove(x, y));
                        continue;
                    }
                    var cls = _cls[i];
                    if (cls == null || Array.IndexOf(cls, FloorPx) < 0)
                        continue;
                    // 벽 칸 밑 바닥: 위 칸 정면(늘인 정면 포함)의 그림자는 이 칸에서 바닥이 비치는 맨 위 두 줄의 열에만 의미가 있다
                    uint shown = 0;
                    for (int p = 0; p < 2 * C; p++)
                        if (cls[p] == FloorPx)
                            shown |= 1u << (p % C);
                    _ground[i] = ShadeKey(shown & FaceAbove(x, y));
                }
            }

            private bool BandAt(int gx, int gy) => gx >= 0 && gy >= 0 && gx < _pw && gy < _ph && _bandMap[gy * _pw + gx];

            /// <summary>띠 칠: 8방향에 띠 아님 → o, 아니면 5x5의 dy 0..2(자기 제외)가 모두 띠이고 dy −2..−1도 모두 띠 → f, 위만 빠짐 → l, 그 밖 d.</summary>
            private string Shade(int gx, int gy)
            {
                if (gx >= Reach && gy >= Reach && gx < _pw - Reach && gy < _ph - Reach)
                    return ShadeInside(gy * _pw + gx);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && !BandAt(gx + dx, gy + dy))
                            return "o";
                for (int dy = 0; dy <= Reach; dy++)
                    for (int dx = -Reach; dx <= Reach; dx++)
                        if ((dx != 0 || dy != 0) && !BandAt(gx + dx, gy + dy))
                            return "d";
                for (int dy = -Reach; dy < 0; dy++)
                    for (int dx = -Reach; dx <= Reach; dx++)
                        if (!BandAt(gx + dx, gy + dy))
                            return "l";
                return "f";
            }

            /// <summary>Shade와 같은 판정, 반경 안이 모두 맵 안인 픽셀(경계 검사 없이).</summary>
            private string ShadeInside(int g)
            {
                var m = _bandMap;
                int w = _pw;
                if (!m[g - w - 1] || !m[g - w] || !m[g - w + 1] || !m[g - 1] || !m[g + 1] || !m[g + w - 1] || !m[g + w] || !m[g + w + 1])
                    return "o";
                for (int dy = 0; dy <= Reach; dy++)
                    for (int dx = -Reach; dx <= Reach; dx++)
                        if ((dx != 0 || dy != 0) && !m[g + dy * w + dx])
                            return "d";
                for (int dy = -Reach; dy < 0; dy++)
                    for (int dx = -Reach; dx <= Reach; dx++)
                        if (!m[g + dy * w + dx])
                            return "l";
                return "f";
            }

            private sealed class Group
            {
                public TileSourceKind Kind;
                public string Value;
                public bool HasOwner;
                public int Dx, Dy;
                public readonly uint[] Rows = new uint[C];
            }

            private IReadOnlyList<TilePart> Sources(int i)
            {
                int x = i % _w, y = i / _w;
                string key = _keys[i];
                if (_tags[i] == RoomTileKinds.Floor)
                {
                    var full = new uint[C];
                    for (int r = 0; r < C; r++)
                        full[r] = uint.MaxValue;
                    return new[] { Part(TileSources.Floor, _ground[i], true, 0, 0, full) };
                }
                if (_cls[i] == null)
                    return FullFace(i, key);

                var cls = _cls[i];
                var org = _org[i];
                string ground = _ground[i];
                bool isSill = _sills[i] != null;
                int fallback = _towards[i];
                if (fallback < 0 && _narrow[i] != null)
                    for (int p = 0; p < Pixels; p++)
                        if (cls[p] == FloorPx)
                        {
                            fallback = org[p];
                            break;
                        }
                var groups = new List<Group>(8);
                int ox = x * C, oy = y * C;
                string narrowPiece = null;
                if (_narrow[i] != null)
                {
                    var (a, b) = _narrow[i].Value;
                    narrowPiece = key + "@" + a + "-" + b;
                }
                for (int p = 0; p < Pixels; p++)
                {
                    byte c = cls[p];
                    int px = p % C, py = p / C;
                    if (c == BandPx && !isSill)
                        Add(groups, TileSources.Band, Shade(ox + px, oy + py), false, 0, 0, px, py);
                    else if (c == SillPx)
                        Add(groups, TileSources.Sill, _sills[i], false, 0, 0, px, py);
                    else if (c == NarrowPx)
                        Add(groups, TileSources.Narrow, narrowPiece, true, 0, _tags[i], px, py);
                    else if (c >= FaceBase && !isSill)
                    {
                        int o = org[p];
                        Add(groups, TileSources.Face, _faceKeys[c - FaceBase], true, o % 3 - 1, o / 3 - 1 + _faceLevels[c - FaceBase], px, py);
                    }
                    else if (ground != null)                                   // 비침
                    {
                        int o = c == FloorPx && org[p] != NoOrigin ? org[p] : fallback;
                        if (o < 0)
                            throw new InvalidOperationException($"칸 ({x}, {y}) 픽셀 ({px}, {py}): 비침의 주인 바닥을 정할 수 없다");
                        Add(groups, TileSources.Floor, ground, true, o % 3 - 1, o / 3 - 1, px, py);
                    }
                    // 그 밖은 비어 있고 Ground도 없다 — 투명, 적지 않는다
                }
                var parts = new List<TilePart>(groups.Count);
                foreach (var g in groups)
                    parts.Add(new TilePart(g.Kind, g.Value, g.HasOwner, new CellPos(g.Dx, g.Dy), PixelRects.FromRows(g.Rows)));
                parts.Sort(Compare);
                return parts;
            }

            /// <summary>꽉 찬 정면(문을 박은 정면이면 문짝 자리만 문짝).</summary>
            private IReadOnlyList<TilePart> FullFace(int i, string key)
            {
                var face = new uint[C];
                var door = new uint[C];
                for (int r = 0; r < C; r++)
                    face[r] = uint.MaxValue;
                if (_doorFaces[i])
                {
                    uint cols = 0;
                    for (int px = DoorRect.X0; px < DoorRect.X1; px++)
                        cols |= 1u << px;
                    for (int r = DoorRect.Y0; r < DoorRect.Y1; r++)
                    {
                        door[r] = cols;
                        face[r] &= ~cols;
                    }
                }
                var parts = new List<TilePart>(2) { Part(TileSources.Face, key, true, 0, _tags[i], face) };
                if (_doorFaces[i])
                    parts.Add(Part(TileSources.Door, DoorPiece, false, 0, 0, door));
                parts.Sort(Compare);
                return parts;
            }

            private static TilePart Part(string src, string value, bool hasOwner, int dx, int dy, uint[] rows) =>
                new TilePart(TileSources.Find(src), value, hasOwner, new CellPos(dx, dy), PixelRects.FromRows(rows));

            private static void Add(List<Group> groups, string src, string value, bool hasOwner, int dx, int dy, int px, int py)
            {
                Group g = null;
                // 이웃 픽셀은 대개 같은 부분이라 마지막에 쓴 것부터 본다(결과는 같고 빠르다).
                for (int k = groups.Count - 1; k >= 0; k--)
                {
                    var e = groups[k];
                    if (e.Dx == dx && e.Dy == dy && e.HasOwner == hasOwner && string.Equals(e.Kind.Name, src, StringComparison.Ordinal)
                        && (ReferenceEquals(e.Value, value) || string.Equals(e.Value, value, StringComparison.Ordinal)))
                    {
                        g = e;
                        if (k != groups.Count - 1)
                        {
                            groups.RemoveAt(k);
                            groups.Add(e);
                        }
                        break;
                    }
                }
                if (g == null)
                {
                    g = new Group { Kind = TileSources.Find(src), Value = value, HasOwner = hasOwner, Dx = dx, Dy = dy };
                    groups.Add(g);
                }
                g.Rows[py] |= 1u << px;
            }

            /// <summary>부분 순서(Python part_order): 출처 이름, 값, 주인(없는 것 먼저, dx 다음 dy), 픽셀 글 — 글은 ordinal.</summary>
            private static int Compare(TilePart a, TilePart b)
            {
                int c = string.CompareOrdinal(a.Source, b.Source);
                if (c != 0) return c;
                c = string.CompareOrdinal(a.Shade ?? a.Piece ?? "", b.Shade ?? b.Piece ?? "");
                if (c != 0) return c;
                c = a.HasOwner.CompareTo(b.HasOwner);
                if (c != 0) return c;
                c = a.Owner.X.CompareTo(b.Owner.X);
                if (c != 0) return c;
                c = a.Owner.Y.CompareTo(b.Owner.Y);
                if (c != 0) return c;
                return string.CompareOrdinal(a.Pixels, b.Pixels);
            }
        }

        /// <summary>정면 문짝이 칠하는 자리(Python DOOR_RECT). 밖은 정면 그대로(맨 아랫줄 외곽선 포함).</summary>
        public static readonly PixelRect DoorRect = new PixelRect(6, 4, 26, CellSize - 1);
        /// <summary>정면 문짝 조각 이름.</summary>
        public const string DoorPiece = "door";
    }
}
