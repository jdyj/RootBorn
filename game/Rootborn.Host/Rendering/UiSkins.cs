using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Ui;

namespace Rootborn.Host.Rendering
{
    /// <summary>
    /// 패널 그림 이름 하나가 가리키는 것: PNG(전체 경로), 9조각 자리, 늘이는지(Sliced) 반복하는지, 원본 1픽셀의 논리 크기, 그 위 글자 색, 안쪽 여백.
    /// 값은 테마 폴더의 theme.json에서 온다(ThemeFolder).
    /// </summary>
    public sealed class SkinSource
    {
        public SkinSource(string texture, NineSliceSource slices, bool stretch, float logicalPerPixel, UiColor? textColor, SkinPadding padding = default)
        {
            Texture = texture; Slices = slices; Stretch = stretch; LogicalPerPixel = logicalPerPixel; TextColor = textColor; Padding = padding;
        }

        public string Texture { get; }
        public NineSliceSource Slices { get; }
        /// <summary>true면 변과 가운데를 늘인다(Unity Image.Type.Sliced), false면 가운데 칸을 반복한다(Style2 공통 패널 규약).</summary>
        public bool Stretch { get; }
        /// <summary>원본 1픽셀이 논리 좌표로 몇인가. 0이면 화면 배율의 타일 배율(ScreenFit.TileScale)을 쓴다.</summary>
        public float LogicalPerPixel { get; }
        /// <summary>이 그림 위 글자 색(도시형 몰딩 버튼). 없으면 화면이 정한 색.</summary>
        public UiColor? TextColor { get; }
        /// <summary>글자를 놓아도 되는 안쪽 여백(논리 좌표).</summary>
        public SkinPadding Padding { get; }

        /// <summary>그릴 때의 정수 배율.</summary>
        public int Scale(ScreenFit fit) => LogicalPerPixel <= 0 ? fit.TileScale : Math.Max(1, (int)MathF.Round(LogicalPerPixel * fit.Scale));
    }

    /// <summary>패널 그림 이름(UiSkin) → 어느 PNG의 어디인가. 테마 폴더(ThemeFolder)가 채운다. 그림 좌표를 코드에 적지 않는다.</summary>
    public sealed class UiSkins
    {
        private readonly Dictionary<string, SkinSource> _skins;

        public UiSkins(IDictionary<string, SkinSource> skins) { _skins = new Dictionary<string, SkinSource>(skins, StringComparer.Ordinal); }

        public SkinSource Get(string skin) =>
            skin != null && _skins.TryGetValue(skin, out SkinSource source) ? source : throw new InvalidOperationException("모르는 패널 그림 이름: " + skin);

        public bool TryGet(string skin, out SkinSource source) { source = null; return skin != null && _skins.TryGetValue(skin, out source); }
    }

    /// <summary>데이터의 그림 참조: PNG 경로(저장소 기준)와 그 안의 스프라이트 이름.</summary>
    public readonly record struct SpriteRef(string Path, string Name)
    {
        public override string ToString() => Path + "#" + Name;
    }

    /// <summary>스프라이트 하나의 자리(위 왼쪽 기준 픽셀)와 9조각 테두리.</summary>
    public readonly record struct AtlasSprite(int X, int Y, int W, int H, int BorderLeft, int BorderTop, int BorderRight, int BorderBottom)
    {
        public bool HasBorder => BorderLeft > 0 || BorderTop > 0 || BorderRight > 0 || BorderBottom > 0;
        public PixelRect Rect => new PixelRect(X, Y, W, H);
    }

    /// <summary>
    /// 그림 좌표표: data/atlases/&lt;PNG 경로&gt;.atlas.json(데이터 변환기가 Unity 스프라이트 정보로 만든 표, 위 왼쪽 기준).
    /// 처음 묻는 PNG의 표만 읽어 둔다.
    /// </summary>
    public sealed class SpriteAtlases
    {
        private readonly string _repoRoot;
        private readonly Dictionary<string, Dictionary<string, AtlasSprite>> _tables = new Dictionary<string, Dictionary<string, AtlasSprite>>(StringComparer.OrdinalIgnoreCase);

        public SpriteAtlases(string repoRoot) { _repoRoot = repoRoot; }

        public string FullPath(string texture) => System.IO.Path.Combine(_repoRoot, texture);

        public bool TryFind(string texture, string name, out AtlasSprite sprite)
        {
            sprite = default;
            Dictionary<string, AtlasSprite> table = Table(texture);
            return table != null && table.TryGetValue(name ?? string.Empty, out sprite);
        }

        public AtlasSprite Find(string texture, string name) =>
            TryFind(texture, name, out AtlasSprite sprite) ? sprite : throw new InvalidDataException("그림 좌표표에 없는 그림: " + texture + "#" + name);

        private Dictionary<string, AtlasSprite> Table(string texture)
        {
            if (_tables.TryGetValue(texture, out Dictionary<string, AtlasSprite> table)) return table;
            string path = System.IO.Path.Combine(_repoRoot, "data", "atlases", texture + ".atlas.json");
            table = null;
            if (File.Exists(path))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("origin", out JsonElement origin) && origin.GetString() != "top-left")
                    throw new InvalidDataException("위 왼쪽 기준이 아닌 좌표표: " + path);
                table = new Dictionary<string, AtlasSprite>(StringComparer.Ordinal);
                foreach (JsonProperty p in root.GetProperty("sprites").EnumerateObject())
                {
                    JsonElement s = p.Value;
                    int bl = 0, bt = 0, br = 0, bb = 0;
                    if (s.TryGetProperty("border", out JsonElement b))
                    {
                        bl = b.GetProperty("left").GetInt32(); bt = b.GetProperty("top").GetInt32();
                        br = b.GetProperty("right").GetInt32(); bb = b.GetProperty("bottom").GetInt32();
                    }
                    table[p.Name] = new AtlasSprite(s.GetProperty("x").GetInt32(), s.GetProperty("y").GetInt32(), s.GetProperty("w").GetInt32(), s.GetProperty("h").GetInt32(), bl, bt, br, bb);
                }
            }
            _tables[texture] = table;
            return table;
        }
    }

    /// <summary>
    /// 그림 id → PNG와 그 안의 칸. 그림 id 꼴:
    /// "icon:&lt;키&gt;"(테마의 아이콘 키, 예: icon:journal), "icon:&lt;키&gt;@&lt;정의 id&gt;"(테마가 그 정의에 정한 아이콘, 없으면 그 키),
    /// "item:&lt;아이템 id&gt;"(data/items의 icon), "&lt;PNG 경로&gt;#&lt;스프라이트 이름&gt;"(좌표표), "&lt;PNG 경로&gt;"(그림 전체).
    /// 그리기 명령에 원본 칸(Source)이 있으면 그 PNG 안의 그 칸을 쓴다.
    /// </summary>
    public sealed class ImageCatalog
    {
        private readonly SpriteAtlases _atlases;
        private readonly ThemeFolder _theme;
        private readonly ItemIcons _items;
        private readonly Dictionary<string, (string texture, PixelRect source)> _cache = new Dictionary<string, (string, PixelRect)>(StringComparer.Ordinal);

        public ImageCatalog(SpriteAtlases atlases, ThemeFolder theme, ItemIcons items = null)
        {
            _atlases = atlases;
            _theme = theme;
            _items = items ?? ItemIcons.Empty;
        }

        /// <summary>PNG 경로와 칸. 칸 W가 0이면 그림 전체.</summary>
        public bool TryResolve(string imageId, PixelRect source, out string texture, out PixelRect rect)
        {
            texture = null; rect = default;
            if (string.IsNullOrEmpty(imageId)) return false;
            if (!_cache.TryGetValue(imageId, out (string texture, PixelRect source) hit))
            {
                hit = Resolve(imageId);
                _cache[imageId] = hit;
            }
            if (hit.texture == null) return false;
            texture = hit.texture;
            rect = source.W > 0 ? source : hit.source;
            return true;
        }

        private (string, PixelRect) Resolve(string imageId)
        {
            if (imageId.StartsWith("icon:", StringComparison.Ordinal))
            {
                // "icon:<키>@<정의 id>": 테마가 그 정의에 정한 아이콘이 있으면 그것, 없으면 <키>.
                string key = imageId.Substring(5);
                int at = key.IndexOf('@');
                if (at >= 0)
                {
                    string definition = key.Substring(at + 1);
                    key = key.Substring(0, at);
                    if (_theme != null && _theme.DefinitionIcons.TryGetValue(definition, out string own) && _theme.Icons.ContainsKey(own)) key = own;
                }
                if (_theme == null || !_theme.Icons.TryGetValue(key, out ThemeIcon icon)) return (null, default);
                return (icon.Texture, icon.Rect);
            }
            if (imageId.StartsWith("item:", StringComparison.Ordinal))
            {
                // 아이템 그림: 좌표표에 있으면 그 칸, 없으면 PNG 전체(낱장 그림).
                if (!_items.TryGet(imageId.Substring(5), out SpriteRef item)) return (null, default);
                if (_atlases.TryFind(item.Path, item.Name, out AtlasSprite itemSprite)) return (item.Path, itemSprite.Rect);
                return File.Exists(_atlases.FullPath(item.Path)) ? (item.Path, default) : (null, default);
            }
            int hash = imageId.IndexOf('#');
            if (hash < 0) return (imageId, default);
            string path = imageId.Substring(0, hash), name = imageId.Substring(hash + 1);
            return _atlases.TryFind(path, name, out AtlasSprite sprite) ? (path, sprite.Rect) : (null, default);
        }
    }
}
