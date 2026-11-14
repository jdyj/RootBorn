using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rootborn.Core.Building;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>
    /// 그림 한 장의 픽셀(엔진 없음). 픽셀은 가장 낮은 바이트가 R, 그다음 G, B, 가장 높은 바이트가 A다(MonoGame <c>Color.PackedValue</c>와 같은 순서).
    /// 줄 순서: pixels[y * width + x], 왼쪽 위가 (0, 0).
    /// </summary>
    public sealed class PieceImage
    {
        public PieceImage(int width, int height, uint[] pixels)
        {
            if (width < 0 || height < 0) throw new ArgumentOutOfRangeException(nameof(width), "크기는 0 이상");
            Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != width * height) throw new ArgumentException($"픽셀 수 {pixels.Length}가 {width}x{height}와 다르다", nameof(pixels));
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
        public uint[] Pixels { get; }
    }

    /// <summary>
    /// 조합한 방: 서로 다른 칸 그림을 한 장에 모은 묶음(너비 <see cref="AtlasWidth"/>px, 한 줄 32칸)과 칸마다 그 칸 그림의 자리.
    /// 밖이 아닌 칸이 <see cref="MaxCells"/>칸(묶음 16MB)을 넘으면 만들지 않는다(<see cref="TooLarge"/>, 픽셀·칸 없음).
    /// </summary>
    public sealed class ComposedRoom
    {
        public const int AtlasWidth = 1024;
        public const int MaxCells = 4096;

        internal ComposedRoom(int width, int height, uint[] pixels, IReadOnlyList<(CellPos Cell, int SrcX, int SrcY)> cells, int uniqueTiles,
            IReadOnlyList<string> problems, bool tooLarge)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
            Cells = cells;
            UniqueTiles = uniqueTiles;
            Problems = problems;
            TooLarge = tooLarge;
        }

        public int Width { get; }
        public int Height { get; }
        /// <summary>묶음의 픽셀(<see cref="PieceImage"/>와 같은 꼴). TooLarge면 비어 있다.</summary>
        public uint[] Pixels { get; }
        /// <summary>출처표의 칸 순서(밖이 아닌 칸, y 다음 x)대로 칸과 그 칸 그림의 묶음 안 왼쪽 위.</summary>
        public IReadOnlyList<(CellPos Cell, int SrcX, int SrcY)> Cells { get; }
        public int UniqueTiles { get; }
        public IReadOnlyList<string> Problems { get; }
        public bool TooLarge { get; }
    }

    /// <summary>
    /// 칸마다 그림을 조합한다(엔진 없음): Core 출처표(<see cref="RoomTilePlan"/>) + 칸마다의 꾸밈 + 조각 시트 → 32x32 칸 그림. 참조 조합기
    /// (Tools/pixel-kit/tests/test_room_vectors.py compose_cell)와 같은 일을 한다:
    /// - band: 띠 칠 네 색(좌표표 bands의 띠 색 줄) 중 그 부분의 칠(o 외곽선, d 안쪽 그늘, f 면, l 안쪽 빛)로 채운다.
    /// - face·narrow: 주인 칸(이 칸 + Owner)의 벽 꾸밈의 그 조각(Piece)에서 같은 자리 픽셀.
    /// - floor: 주인 칸의 바닥 꾸밈의 floor 조각. 그림자 범위(그 칸 자신의 바닥 열쇠 Shade: floor:shade = 칸 전체, floor:shade:a-b = 열 [a, b)) × 위 두 줄만 floor:shade 조각.
    /// - sill·door: 꾸밈과 무관한 공용 조각(좌표표의 common/…, 띠 색과 같은 시트).
    /// 부분들은 겹치지 않으므로 순서와 무관하고, 어느 부분에도 없는 픽셀은 투명이다. 같은 (부분 목록, 쓰인 꾸밈 id) 칸은 한 번만 만든다.
    /// 조각·시트·띠 색·꾸밈이 없거나 좌표표가 깨졌으면 그 픽셀은 투명으로 두고 문제 한 줄을 남긴다(던지지 않는다 — 그리는 쪽이 LDtk 층으로 그린다).
    /// 조각 이름(face:…, sill:…)은 열어 보지 않는 열쇠다 — 바닥 열쇠(Shade)만 여기 한 곳에서 푼다.
    /// 맵을 읽을 때·꾸밈이 바뀔 때만 부른다(매 프레임 부르지 않는다).
    /// </summary>
    public sealed class RoomTileCompositor
    {
        /// <summary>한 번의 조합이 남기는 문제 줄의 상한(같은 문제는 한 번).</summary>
        public const int MaxProblems = 40;

        private const int Size = RoomTilePlan.CellSize;
        internal const string FloorPiece = "floor", ShadePiece = "floor:shade", ShadeKeyPrefix = "floor:shade:";
        private const string BandShades = "odfl";

        private readonly Func<string, PieceImage> _sheet;
        private readonly RoomFinishArt _art;
        private readonly Dictionary<string, PieceImage> _sheets = new Dictionary<string, PieceImage>(StringComparer.Ordinal);

        /// <param name="sheet">시트 경로(정의의 sheet 문자열, 좌표표의 Texture) → 픽셀. 없으면 null.</param>
        public RoomTileCompositor(Func<string, PieceImage> sheet, RoomFinishArt art)
        {
            _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            _art = art ?? throw new ArgumentNullException(nameof(art));
        }

        /// <summary>출처 종류 이름 → 칠하는 법과 주인의 꾸밈 자리(주인 없는 종류는 null). 종류를 더하는 일 = 여기 한 줄 + Core TileSources 한 줄.</summary>
        private static readonly Dictionary<string, (string Slot, Action<Job, TilePart, CellPos, uint[]> Paint)> Painters =
            new Dictionary<string, (string, Action<Job, TilePart, CellPos, uint[]>)>(StringComparer.Ordinal)
            {
                [TileSources.Band] = (null, (job, part, owner, tile) => job.PaintBand(part, tile)),
                [TileSources.Face] = (RoomFinishSlots.Wall, (job, part, owner, tile) => job.PaintWall(part, owner, tile)),
                [TileSources.Narrow] = (RoomFinishSlots.Wall, (job, part, owner, tile) => job.PaintWall(part, owner, tile)),
                [TileSources.Floor] = (RoomFinishSlots.Floor, (job, part, owner, tile) => job.PaintFloor(part, owner, tile)),
                [TileSources.Sill] = (null, (job, part, owner, tile) => job.PaintCommon(part, tile)),
                [TileSources.Door] = (null, (job, part, owner, tile) => job.PaintCommon(part, tile)),
            };

        /// <param name="wallAt">칸 → 그 칸의 벽 꾸밈(주인 칸으로 부른다).</param>
        /// <param name="floorAt">칸 → 그 칸의 바닥 꾸밈.</param>
        /// <param name="sheetForBand">띠 색과 공용 조각(문지방·문짝)을 찾을 시트(벽 꾸밈의 Sheet).</param>
        /// <param name="bandColorsId">띠 색 줄 이름(맵 build.bandColors).</param>
        public ComposedRoom Compose(RoomTilePlan plan, Func<CellPos, RoomFinishDefinition> wallAt, Func<CellPos, RoomFinishDefinition> floorAt,
            string sheetForBand, string bandColorsId)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (wallAt == null) throw new ArgumentNullException(nameof(wallAt));
            if (floorAt == null) throw new ArgumentNullException(nameof(floorAt));
            var job = new Job(this, wallAt, floorAt, sheetForBand, bandColorsId);
            IReadOnlyList<CellTiles> cells = plan.Cells;
            if (cells.Count > ComposedRoom.MaxCells)
            {
                job.Problem($"칸 {cells.Count}개가 조합 상한 {ComposedRoom.MaxCells}칸을 넘는다");
                return new ComposedRoom(0, 0, Array.Empty<uint>(), Array.Empty<(CellPos, int, int)>(), 0, job.Problems, true);
            }

            const int perRow = ComposedRoom.AtlasWidth / Size;
            var unique = new Dictionary<string, int>(cells.Count, StringComparer.Ordinal);
            var tiles = new List<uint[]>();
            var placed = new (CellPos Cell, int SrcX, int SrcY)[cells.Count];
            var key = new StringBuilder(256);
            for (int i = 0; i < cells.Count; i++)
            {
                CellTiles cell = cells[i];
                key.Clear();
                foreach (TilePart part in cell.Parts)
                    key.Append(part.Source).Append('|').Append(part.Shade).Append('|').Append(part.Piece).Append('|')
                        .Append(job.FinishId(part, cell.Cell)).Append('|').Append(part.Pixels).Append(';');
                string k = key.ToString();
                if (!unique.TryGetValue(k, out int index))
                {
                    index = tiles.Count;
                    var tile = new uint[Size * Size];
                    foreach (TilePart part in cell.Parts) job.Paint(part, cell.Cell, tile);
                    tiles.Add(tile);
                    unique[k] = index;
                }
                placed[i] = (cell.Cell, index % perRow * Size, index / perRow * Size);
            }

            int rows = (tiles.Count + perRow - 1) / perRow;
            int width = ComposedRoom.AtlasWidth, height = rows * Size;
            var pixels = new uint[width * height];
            for (int t = 0; t < tiles.Count; t++)
            {
                int ox = t % perRow * Size, oy = t / perRow * Size;
                for (int y = 0; y < Size; y++) Array.Copy(tiles[t], y * Size, pixels, (oy + y) * width + ox, Size);
            }
            return new ComposedRoom(width, height, pixels, placed, tiles.Count, job.Problems, false);
        }

        /// <summary>깨진 좌표표를 읽을 때 RoomFinishArt가 던지는 예외들(조합과 맵 검사가 같은 범위를 문제 한 줄로 바꾼다).</summary>
        public static bool IsAtlasReadError(Exception e) =>
            e is InvalidDataException || e is System.Text.Json.JsonException || e is KeyNotFoundException
            || e is InvalidOperationException || e is FormatException || e is IOException;

        private PieceImage SheetImage(string path)
        {
            if (!_sheets.TryGetValue(path, out PieceImage image)) _sheets[path] = image = _sheet(path);
            return image;
        }

        /// <summary>한 번의 조합: 꾸밈 찾기, 띠 색, 문제 모으기.</summary>
        private sealed class Job
        {
            private readonly RoomTileCompositor _owner;
            private readonly Func<CellPos, RoomFinishDefinition> _wallAt, _floorAt;
            private readonly string _sheet, _bandId;
            private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
            private BandColors _band;
            private bool _bandRead, _bandOk;
            public readonly List<string> Problems = new List<string>();

            public Job(RoomTileCompositor owner, Func<CellPos, RoomFinishDefinition> wallAt, Func<CellPos, RoomFinishDefinition> floorAt, string sheet, string bandId)
            {
                _owner = owner;
                _wallAt = wallAt;
                _floorAt = floorAt;
                _sheet = sheet ?? string.Empty;
                _bandId = bandId ?? string.Empty;
            }

            public void Problem(string text)
            {
                if (Problems.Count < MaxProblems && _seen.Add(text)) Problems.Add(text);
            }

            private static CellPos Owner(TilePart part, CellPos cell) => new CellPos(cell.X + part.Owner.X, cell.Y + part.Owner.Y);

            /// <summary>
            /// 이 부분이 쓰는 꾸밈 id(꾸밈이 필요 없는 종류·모르는 종류는 빈 글). 같은 칸 그림인지 가리는 열쇠에 든다.
            /// 꾸밈이 필요한 종류인데 주인이 없으면(Core는 내지 않는다 — 종류 표가 어긋난 경우) 문제로 알리고 따로 묶는다(칠하지 않는다).
            /// </summary>
            public string FinishId(TilePart part, CellPos cell)
            {
                if (!Painters.TryGetValue(part.Source, out var p) || p.Slot == null) return string.Empty;
                if (!part.HasOwner) { NoOwner(part, p.Slot, cell); return "!"; }
                RoomFinishDefinition f = p.Slot == RoomFinishSlots.Floor ? _floorAt(Owner(part, cell)) : _wallAt(Owner(part, cell));
                return f?.Id ?? "?";
            }

            public void Paint(TilePart part, CellPos cell, uint[] tile)
            {
                if (!Painters.TryGetValue(part.Source, out var p)) { Problem("모르는 출처 종류 " + part.Source + " — 그 픽셀은 투명"); return; }
                if (p.Slot != null && !part.HasOwner) { NoOwner(part, p.Slot, cell); return; }
                p.Paint(this, part, Owner(part, cell), tile);
            }

            private void NoOwner(TilePart part, string slot, CellPos cell) =>
                Problem($"칸 {cell}의 {part.Source} 부분은 {slot} 꾸밈이 필요한데 주인 칸이 없다 — 그 픽셀은 투명");

            public void PaintBand(TilePart part, uint[] tile)
            {
                if (!_bandRead)
                {
                    _bandRead = true;
                    _bandOk = Guard(() => _owner._art.TryBand(_sheet, _bandId, out _band));
                    if (!_bandOk) Problem($"띠 색 줄 {_bandId}이 시트 {_sheet}의 좌표표에 없다 — 띠는 투명");
                }
                int shade = part.Shade != null && part.Shade.Length == 1 ? BandShades.IndexOf(part.Shade[0]) : -1;
                if (!_bandOk) return;
                if (shade < 0) { Problem("모르는 띠 칠 " + part.Shade); return; }
                uint color = shade == 0 ? _band.Outline : shade == 1 ? _band.Dark : shade == 2 ? _band.Face : _band.Light;
                foreach (PixelRect r in part.Rects)
                    for (int y = r.Y0; y < r.Y1; y++)
                        for (int x = r.X0; x < r.X1; x++) tile[y * Size + x] = color;
            }

            public void PaintWall(TilePart part, CellPos owner, uint[] tile)
            {
                RoomFinishDefinition finish = Finish(_wallAt(owner), RoomFinishSlots.Wall, owner);
                if (finish == null) return;
                FinishPiece piece = default;
                bool found = Guard(() => _owner._art.TryPiece(finish, part.Piece, out piece));
                if (!found) { Problem($"조각 {finish.Art}/{part.Piece}가 시트 {finish.Sheet}의 좌표표에 없다(꾸밈 {finish.Id}) — 그 픽셀은 투명"); return; }
                Copy(piece, part.Rects, 0, Size, 0, Size, tile);
            }

            public void PaintFloor(TilePart part, CellPos owner, uint[] tile)
            {
                if (!ShadeRange(part.Shade, out int lo, out int hi)) { Problem("모르는 바닥 열쇠 " + part.Shade + " — 그 픽셀은 투명"); return; }
                RoomFinishDefinition finish = Finish(_floorAt(owner), RoomFinishSlots.Floor, owner);
                if (finish == null) return;
                FinishPiece floor = default, shade = default;
                if (Guard(() => _owner._art.TryPiece(finish, FloorPiece, out floor))) Copy(floor, part.Rects, 0, Size, 0, Size, tile);
                else Problem($"조각 {finish.Art}/{FloorPiece}가 시트 {finish.Sheet}의 좌표표에 없다(꾸밈 {finish.Id}) — 그 픽셀은 투명");
                if (lo >= hi) return;
                if (Guard(() => _owner._art.TryPiece(finish, ShadePiece, out shade))) Copy(shade, part.Rects, lo, hi, 0, 2, tile, clear: false);
                else
                {
                    Problem($"조각 {finish.Art}/{ShadePiece}가 시트 {finish.Sheet}의 좌표표에 없다(꾸밈 {finish.Id}) — 그림자 픽셀은 투명");
                    Clear(part.Rects, lo, hi, 0, 2, tile);
                }
            }

            public void PaintCommon(TilePart part, uint[] tile)
            {
                FinishPiece piece = default;
                if (!Guard(() => _owner._art.TryCommon(_sheet, part.Piece, out piece)))
                {
                    Problem($"공용 조각 {RoomFinishArt.Common}/{part.Piece}가 시트 {_sheet}의 좌표표에 없다 — 그 픽셀은 투명");
                    return;
                }
                Copy(piece, part.Rects, 0, Size, 0, Size, tile);
            }

            private RoomFinishDefinition Finish(RoomFinishDefinition finish, string slot, CellPos owner)
            {
                if (finish == null) { Problem($"칸 {owner}의 {slot} 꾸밈이 없다 — 그 픽셀은 투명"); return null; }
                if (finish.Slot != slot) { Problem($"꾸밈 {finish.Id}은 {finish.Slot} 자리라 {slot}에 쓸 수 없다 — 그 픽셀은 투명"); return null; }
                return finish;
            }

            /// <summary>좌표표 읽기(깨진 좌표표는 RoomFinishArt가 던진다)를 문제 한 줄로 바꾼다.</summary>
            private bool Guard(Func<bool> read)
            {
                try { return read(); }
                catch (Exception e) when (IsAtlasReadError(e))
                {
                    Problem("조각 시트 좌표표를 읽지 못했다: " + e.Message);
                    return false;
                }
            }

            /// <summary>조각에서 부분의 사각형 ∩ 열 [x0, x1) × 줄 [y0, y1)의 픽셀을 같은 자리로 옮긴다.</summary>
            private void Copy(FinishPiece piece, IReadOnlyList<PixelRect> rects, int x0, int x1, int y0, int y1, uint[] tile, bool clear = true)
            {
                PieceImage sheet = _owner.SheetImage(piece.Texture);
                if (sheet == null || piece.X < 0 || piece.Y < 0 || piece.X + Size > sheet.Width || piece.Y + Size > sheet.Height)
                {
                    Problem(sheet == null ? $"시트 그림 {piece.Texture}을 읽지 못했다 — 그 픽셀은 투명" : $"시트 {piece.Texture}의 조각 자리 ({piece.X}, {piece.Y})가 그림 밖이다");
                    if (clear) Clear(rects, x0, x1, y0, y1, tile);
                    return;
                }
                uint[] src = sheet.Pixels;
                foreach (PixelRect r in rects)
                {
                    int ya = Math.Max(r.Y0, y0), yb = Math.Min(r.Y1, y1), xa = Math.Max(r.X0, x0), xb = Math.Min(r.X1, x1);
                    for (int y = ya; y < yb; y++)
                        for (int x = xa; x < xb; x++) tile[y * Size + x] = src[(piece.Y + y) * sheet.Width + piece.X + x];
                }
            }

            private static void Clear(IReadOnlyList<PixelRect> rects, int x0, int x1, int y0, int y1, uint[] tile)
            {
                foreach (PixelRect r in rects)
                    for (int y = Math.Max(r.Y0, y0); y < Math.Min(r.Y1, y1); y++)
                        for (int x = Math.Max(r.X0, x0); x < Math.Min(r.X1, x1); x++) tile[y * Size + x] = 0;
            }

            /// <summary>바닥 열쇠 → 그림자 열 범위: floor 없음(lo = hi), floor:shade 칸 전체, floor:shade:a-b 열 [a, b). 틀린 열쇠는 false.</summary>
            private static bool ShadeRange(string key, out int lo, out int hi)
            {
                lo = hi = 0;
                if (key == FloorPiece) return true;
                if (key == ShadePiece) { hi = Size; return true; }
                if (key == null || !key.StartsWith(ShadeKeyPrefix, StringComparison.Ordinal)) return false;
                string[] range = key.Substring(ShadeKeyPrefix.Length).Split('-');
                return range.Length == 2
                    && int.TryParse(range[0], NumberStyles.None, CultureInfo.InvariantCulture, out lo)
                    && int.TryParse(range[1], NumberStyles.None, CultureInfo.InvariantCulture, out hi)
                    && 0 <= lo && lo < hi && hi <= Size;
            }
        }
    }

    /// <summary>
    /// 짓는 집의 벽·바닥을 그리는 길 하나(엔진 없음): 기본 꾸밈(build.defaultWall·defaultFloor)으로 조합한 칸 그림을 칸마다 한 장씩 그리거나,
    /// 조합이 안 되면(꾸밈·조각·시트가 없음, 좌표표가 깨짐, 칸 수 상한) 문제를 알리고 LDtk의 바꿔 그릴 층을 그대로 그린다(<see cref="FallbackUnder"/>).
    /// Host는 Room의 픽셀을 텍스처 <see cref="TextureKey"/>로 올리고, Picture.Under[0..ReplacedAt) → Draws → 나머지 순으로 그린다.
    /// 맵에 처음 들어설 때·꾸밈이 바뀔 때만 만든다.
    /// </summary>
    public sealed class ComposedMapTiles
    {
        /// <summary>조합 텍스처 이름 앞. 그림 캐시가 파일보다 먼저 찾는 실행 중 텍스처의 열쇠다(파일 경로와 겹치지 않는다).</summary>
        public const string TexturePrefix = "composed:";

        private ComposedMapTiles() { }

        public string MapId { get; private set; }
        public string TextureKey { get; private set; }
        /// <summary>조합한 그림으로 그린다. false면 FallbackUnder(LDtk 층을 건너뛰지 않은 Under)로 그린다.</summary>
        public bool Ok { get; private set; }
        /// <summary>조합 결과(꾸밈을 못 찾았으면 null).</summary>
        public ComposedRoom Room { get; private set; }
        public IReadOnlyList<string> Problems { get; private set; }
        /// <summary>Ok일 때 칸마다 한 장(텍스처 TextureKey, 맵 픽셀 자리). 아니면 비어 있다.</summary>
        public IReadOnlyList<SpriteDraw> Draws { get; private set; }
        /// <summary>
        /// Ok일 때 Picture.Under에서 Draws를 끼울 자리(0..Under.Count, Under.Count면 맨 뒤): 바꿔 그릴 층의 자리(<see cref="MapPicture.ReplacedAt"/>).
        /// 바꿔 그릴 층이 레벨에 없으면 0(맨 아래).
        /// </summary>
        public int InsertAt { get; private set; }
        /// <summary>Ok가 아닐 때 그릴 Under 전체(MapPicture.From(level) — 드문 길이라 그때만 만든다). Ok면 null.</summary>
        public IReadOnlyList<SpriteDraw> FallbackUnder { get; private set; }

        /// <param name="finish">꾸밈 id → 정의(없으면 null).</param>
        public static ComposedMapTiles Compose(LoadedMap map, RoomTileCompositor compositor, Func<string, RoomFinishDefinition> finish)
            => Compose(map, compositor, finish, null);

        /// <summary>
        /// 칸마다의 꾸밈으로 조합한다. lookup이 null이면 모든 칸이 기본 꾸밈(build.defaultWall·defaultFloor)이다.
        /// 칸의 id가 비었거나, 정의에 없거나(지운 꾸밈), 다른 자리의 꾸밈이면 그 칸은 기본 꾸밈으로 그린다 — 문제 줄을 남기지 않는다.
        /// 띠 색은 lookup.BandColors(비면 build.bandColors). 띠 색·공용 조각의 시트는 기본 벽 꾸밈의 시트다.
        /// </summary>
        /// <param name="lookup">주인 바닥 칸 → 보이는 꾸밈 id(없으면 null).</param>
        public static ComposedMapTiles Compose(LoadedMap map, RoomTileCompositor compositor, Func<string, RoomFinishDefinition> finish, IHouseFinishLookup lookup)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (compositor == null) throw new ArgumentNullException(nameof(compositor));
            if (finish == null) throw new ArgumentNullException(nameof(finish));
            MapBuildSettings build = map.Entry?.Build ?? throw new ArgumentException("짓는 집(build)이 아닌 맵: " + map.Entry?.Id, nameof(map));
            if (map.Tiles == null) throw new ArgumentException("출처표가 없는 맵(LDtk 층을 그린다): " + map.Entry.Id, nameof(map));
            var result = new ComposedMapTiles { MapId = map.Entry.Id, TextureKey = TexturePrefix + map.Entry.Id };
            var problems = new List<string>();
            RoomFinishDefinition wall = finish(build.DefaultWall), floor = finish(build.DefaultFloor);
            if (wall == null) problems.Add("기본 벽 꾸밈 " + build.DefaultWall + "이 정의에 없다");
            if (floor == null) problems.Add("기본 바닥 꾸밈 " + build.DefaultFloor + "이 정의에 없다");
            if (wall != null && floor != null)
            {
                if (lookup == null) result.Room = compositor.Compose(map.Tiles, _ => wall, _ => floor, wall.Sheet, build.BandColors);
                else
                {
                    var found = new Dictionary<string, RoomFinishDefinition>(StringComparer.Ordinal); // 한 번의 조합 안에서 id마다 한 번만 찾는다
                    RoomFinishDefinition Pick(string id, string slot, RoomFinishDefinition fallback)
                    {
                        if (string.IsNullOrEmpty(id)) return fallback;
                        if (!found.TryGetValue(id, out RoomFinishDefinition d)) found[id] = d = finish(id);
                        return d != null && string.Equals(d.Slot, slot, StringComparison.Ordinal) ? d : fallback;
                    }
                    string band = lookup.BandColors;
                    result.Room = compositor.Compose(map.Tiles,
                        c => Pick(lookup.WallAt(c), RoomFinishSlots.Wall, wall),
                        c => Pick(lookup.FloorAt(c), RoomFinishSlots.Floor, floor),
                        wall.Sheet, string.IsNullOrEmpty(band) ? build.BandColors : band);
                }
                problems.AddRange(result.Room.Problems);
            }
            result.Problems = problems;
            result.Ok = problems.Count == 0 && result.Room != null && !result.Room.TooLarge;
            if (result.Ok)
            {
                const int size = RoomTilePlan.CellSize;
                var draws = new SpriteDraw[result.Room.Cells.Count];
                for (int i = 0; i < draws.Length; i++)
                {
                    var (cell, sx, sy) = result.Room.Cells[i];
                    float x = cell.X * size, y = cell.Y * size;
                    draws[i] = new SpriteDraw(result.TextureKey, sx, sy, size, size, x, y, size, size, 0, 1f, y + size / 2f);
                }
                result.Draws = draws;
                // 바꿔 그릴 층이 레벨에 하나도 없으면(맵 검사가 문제로 알린다) 맨 아래에 그린다 — 조합은 됐는데 그릴 자리가 없어 빈 화면이 되지 않게.
                int at = map.Picture?.ReplacedAt ?? -1, count = map.Picture?.Under.Count ?? 0;
                result.InsertAt = at < 0 ? 0 : Math.Min(at, count);
            }
            else
            {
                result.Draws = Array.Empty<SpriteDraw>();
                result.FallbackUnder = MapPicture.From(map.Level).Under;
            }
            return result;
        }
    }
}
