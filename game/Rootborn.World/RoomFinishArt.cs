using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Rootborn.Core.Building;

namespace Rootborn.World
{
    /// <summary>꾸밈 조각 시트에서 조각 하나의 자리: PNG 경로와 왼쪽 위 기준 픽셀 좌표. 조각은 32x32다(<see cref="RoomFinishArt.PieceSize"/>).</summary>
    public readonly record struct FinishPiece(string Texture, int X, int Y);

    /// <summary>띠 칠 네 색. 색은 MonoGame <c>Color.PackedValue</c>와 같은 순서(가장 낮은 바이트가 R, 그다음 G, B, 가장 높은 바이트가 A = 255).</summary>
    public readonly record struct BandColors(uint Outline, uint Dark, uint Face, uint Light);

    /// <summary>
    /// 꾸밈 조각 시트의 좌표표(data/atlases/&lt;시트 PNG&gt;.atlas.json)를 푼다. 정의(<see cref="RoomFinishDefinition"/>)의 시트 경로와 그림 키(Art)만
    /// 알면 되고, 조각의 자리와 크기는 좌표표가 말한다 — 시트가 바뀌어도 코드는 그대로다. 엔진 참조 없음.
    ///
    /// 조각 이름은 시트 안에서 <c>&lt;Art&gt;/&lt;조각&gt;</c>이다(조각 = 출처표의 글: face:1:lc, face:2:lrc@8-24, floor, floor:shade).
    /// 벽지·바닥과 무관한 조각(문지방 sill:…, 문짝 door)은 <c>common/&lt;조각&gt;</c>, 띠 칠은 좌표표의 <c>bands</c>(띠 색 줄 이름 → o·d·f·l).
    /// 좌표표는 시트마다 처음 찾을 때 한 번 읽는다. 없는 시트·없는 조각은 false를 돌려준다(던지지 않는다 — 그리는 쪽이 문제로 알린다).
    /// 있지만 모양이 틀린 좌표표(위 왼쪽 기준이 아니거나 조각이 32x32가 아님)는 <see cref="InvalidDataException"/>이다.
    /// </summary>
    public sealed class RoomFinishArt
    {
        public const int PieceSize = 32;

        /// <summary>꾸밈과 무관한 조각의 이름 앞(키트 room_export.COMMON과 같은 글).</summary>
        public const string Common = "common";

        private readonly string _atlasRoot;
        private readonly Dictionary<string, Atlas> _tables = new Dictionary<string, Atlas>(StringComparer.Ordinal);

        /// <param name="dataDirectory">저장소의 data 폴더(좌표표는 그 아래 atlases).</param>
        public RoomFinishArt(string dataDirectory)
        {
            _atlasRoot = Path.Combine(dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory)), "atlases");
        }

        /// <summary>꾸밈의 조각 하나. 조각 이름은 출처표의 글이다.</summary>
        public bool TryPiece(RoomFinishDefinition finish, string piece, out FinishPiece sprite)
        {
            sprite = default;
            if (finish == null || string.IsNullOrEmpty(piece)) return false;
            return Cached(finish.Sheet, finish.Art, piece, out sprite);
        }

        /// <summary>꾸밈과 무관한 조각(문지방, 문짝). 시트는 꾸밈의 <see cref="RoomFinishDefinition.Sheet"/>.</summary>
        public bool TryCommon(string sheet, string piece, out FinishPiece sprite)
        {
            sprite = default;
            return !string.IsNullOrEmpty(piece) && Cached(sheet, Common, piece, out sprite);
        }

        // (시트, 그림 키, 조각) → 찾은 결과. 조각 이름(그림 키 + "/" + 조각)은 처음 한 번만 이어 붙인다 — 조합은 칸·부분마다 묻는다.
        private readonly Dictionary<(string Sheet, string Art, string Piece), (bool Found, FinishPiece Sprite)> _found =
            new Dictionary<(string, string, string), (bool, FinishPiece)>();

        private bool Cached(string sheet, string art, string piece, out FinishPiece sprite)
        {
            var key = (sheet ?? string.Empty, art ?? string.Empty, piece);
            if (_found.TryGetValue(key, out var hit))
            {
                sprite = hit.Sprite;
                return hit.Found;
            }
            bool found = Find(sheet, key.Item2 + "/" + piece, out sprite); // 깨진 좌표표는 여기서 던지고 기억하지 않는다(Table과 같다)
            _found[key] = (found, sprite);
            return found;
        }

        /// <summary>띠 색 줄(예: wall.top.cream)의 네 색.</summary>
        public bool TryBand(string sheet, string bandId, out BandColors colors)
        {
            colors = default;
            Atlas atlas = Table(sheet);
            return atlas != null && !string.IsNullOrEmpty(bandId) && atlas.Bands.TryGetValue(bandId, out colors);
        }

        /// <summary>시트가 받는 띠 두께(px). 좌표표가 없으면 0.</summary>
        public int BandOf(string sheet) => Table(sheet)?.Band ?? 0;

        private bool Find(string sheet, string name, out FinishPiece sprite)
        {
            sprite = default;
            Atlas atlas = Table(sheet);
            return atlas != null && atlas.Pieces.TryGetValue(name, out sprite);
        }

        private Atlas Table(string sheet)
        {
            if (string.IsNullOrEmpty(sheet)) return null;
            if (_tables.TryGetValue(sheet, out Atlas atlas)) return atlas;
            string path = Path.Combine(_atlasRoot, sheet + ".atlas.json");
            atlas = File.Exists(path) ? Read(sheet, path) : null;
            _tables[sheet] = atlas;
            return atlas;
        }

        private static Atlas Read(string sheet, string path)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("origin", out JsonElement origin) || origin.GetString() != "top-left")
                throw new InvalidDataException("위 왼쪽 기준이 아닌 좌표표: " + path);
            var atlas = new Atlas { Band = root.TryGetProperty("band", out JsonElement band) ? band.GetInt32() : 0 };
            foreach (JsonProperty s in root.GetProperty("sprites").EnumerateObject())
            {
                JsonElement v = s.Value;
                if (v.GetProperty("w").GetInt32() != PieceSize || v.GetProperty("h").GetInt32() != PieceSize)
                    throw new InvalidDataException($"조각 {s.Name}가 {PieceSize}x{PieceSize}가 아니다: {path}");
                atlas.Pieces[s.Name] = new FinishPiece(sheet, v.GetProperty("x").GetInt32(), v.GetProperty("y").GetInt32());
            }

            if (root.TryGetProperty("bands", out JsonElement bands))
                foreach (JsonProperty b in bands.EnumerateObject())
                    atlas.Bands[b.Name] = new BandColors(Color(b.Value, "o", path), Color(b.Value, "d", path), Color(b.Value, "f", path), Color(b.Value, "l", path));
            return atlas;
        }

        private static uint Color(JsonElement set, string letter, string path)
        {
            string hex = set.GetProperty(letter).GetString();
            if (hex == null || hex.Length != 7 || hex[0] != '#' || !uint.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint rgb))
                throw new InvalidDataException($"띠 색 '{hex}'가 #RRGGBB가 아니다: {path}");
            uint r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            return r | (g << 8) | (b << 16) | 0xFF000000u;
        }

        private sealed class Atlas
        {
            public int Band;
            public readonly Dictionary<string, FinishPiece> Pieces = new Dictionary<string, FinishPiece>(StringComparer.Ordinal);
            public readonly Dictionary<string, BandColors> Bands = new Dictionary<string, BandColors>(StringComparer.Ordinal);
        }
    }
}
