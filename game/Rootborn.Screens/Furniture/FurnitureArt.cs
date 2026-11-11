using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.Placement;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 가구 그림 한 칸의 크기와 기준점(좌표표 data/atlases에서 읽은 값). 좌표는 PNG 왼쪽 위 기준 픽셀, 기준점(Pivot)도 왼쪽 위 기준 0~1.
    /// PixelsPerUnit은 그림 몇 픽셀이 방 한 칸인지(Unity 판 스프라이트 PPU, 타일 칸 = 1 유닛).
    /// </summary>
    public readonly record struct FurnitureSprite(string Texture, int X, int Y, int W, int H, float PivotX, float PivotY, float PixelsPerUnit)
    {
        /// <summary>칸 단위 폭과 높이.</summary>
        public float WidthCells => W / PixelsPerUnit;
        public float HeightCells => H / PixelsPerUnit;
    }

    /// <summary>
    /// 가구 정의의 그림 참조(FurnitureTilePart의 PNG 경로와 이름)를 좌표표에서 찾는다. 그림 크기·기준점·PPU를 코드에 적지 않는다 —
    /// 그림을 바꾸면 PNG와 그 좌표표만 바뀐다. 표는 PNG마다 처음 찾을 때 한 번 읽는다. 엔진 참조 없음(화면과 Host가 함께 쓴다).
    /// </summary>
    public sealed class FurnitureArt
    {
        private readonly string _atlasRoot;
        private readonly Dictionary<string, Dictionary<string, FurnitureSprite>> _tables = new Dictionary<string, Dictionary<string, FurnitureSprite>>(StringComparer.OrdinalIgnoreCase);

        /// <param name="dataDirectory">저장소의 data 폴더(좌표표는 그 아래 atlases).</param>
        public FurnitureArt(string dataDirectory)
        {
            _atlasRoot = Path.Combine(dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory)), "atlases");
        }

        /// <summary>화면 그림 id("PNG 경로#이름", Host ImageCatalog가 푼다). 그림 참조가 없으면 null.</summary>
        public static string ImageId(FurnitureTilePart part) =>
            string.IsNullOrEmpty(part.Sprite) ? null : string.IsNullOrEmpty(part.Name) ? part.Sprite : part.Sprite + "#" + part.Name;

        /// <summary>가구의 대표 그림(첫 그림 칸). 없으면 false(Unity GetPrimarySprite).</summary>
        public bool TryPrimary(FurnitureDefinition furniture, out FurnitureTilePart part, out FurnitureSprite sprite)
        {
            part = default;
            sprite = default;
            if (furniture == null) return false;
            foreach (FurnitureTilePart candidate in furniture.TileParts)
                if (TryGet(candidate, out sprite)) { part = candidate; return true; }
            return false;
        }

        public bool TryGet(FurnitureTilePart part, out FurnitureSprite sprite)
        {
            sprite = default;
            if (string.IsNullOrEmpty(part.Sprite)) return false;
            Dictionary<string, FurnitureSprite> table = Table(part.Sprite);
            return table != null && table.TryGetValue(part.Name ?? string.Empty, out sprite);
        }

        private Dictionary<string, FurnitureSprite> Table(string texture)
        {
            if (_tables.TryGetValue(texture, out Dictionary<string, FurnitureSprite> table)) return table;
            table = null;
            string path = Path.Combine(_atlasRoot, texture + ".atlas.json");
            if (File.Exists(path))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("origin", out JsonElement origin) && origin.GetString() != "top-left")
                    throw new InvalidDataException("위 왼쪽 기준이 아닌 좌표표: " + path);
                float ppu = root.TryGetProperty("pixelsPerUnit", out JsonElement p) ? p.GetSingle() : 0f;
                if (ppu <= 0f) throw new InvalidDataException("좌표표에 pixelsPerUnit이 없다: " + path);
                table = new Dictionary<string, FurnitureSprite>(StringComparer.Ordinal);
                foreach (JsonProperty s in root.GetProperty("sprites").EnumerateObject())
                {
                    JsonElement v = s.Value;
                    float px = 0.5f, py = 0.5f;
                    if (v.TryGetProperty("pivot", out JsonElement pivot)) { px = pivot.GetProperty("x").GetSingle(); py = pivot.GetProperty("y").GetSingle(); }
                    table[s.Name] = new FurnitureSprite(texture, v.GetProperty("x").GetInt32(), v.GetProperty("y").GetInt32(),
                        v.GetProperty("w").GetInt32(), v.GetProperty("h").GetInt32(), px, py, ppu);
                }
            }
            _tables[texture] = table;
            return table;
        }
    }
}
