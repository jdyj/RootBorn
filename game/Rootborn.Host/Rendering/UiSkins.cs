using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Ui;

namespace Rootborn.Host.Rendering
{
    /// <summary>UI 스킨 묶음. 도시형 몰딩은 Unity 판의 승인된 기본 스킨(2026-10-27), Style2는 테마가 없을 때의 대체 스킨.</summary>
    public enum SkinTheme { Urban, Style2 }

    /// <summary>패널 그림 이름 하나가 가리키는 것: PNG(저장소 기준 경로), 9조각 자리, 늘이는지(Sliced) 반복하는지, 원본 1픽셀의 논리 크기, 그 위 글자 색.</summary>
    public sealed class SkinSource
    {
        public SkinSource(string texture, NineSliceSource slices, bool stretch, float logicalPerPixel, UiColor? textColor)
        {
            Texture = texture; Slices = slices; Stretch = stretch; LogicalPerPixel = logicalPerPixel; TextColor = textColor;
        }

        public string Texture { get; }
        public NineSliceSource Slices { get; }
        /// <summary>true면 변과 가운데를 늘인다(Unity Image.Type.Sliced), false면 가운데 칸을 반복한다(Style2 공통 패널 규약).</summary>
        public bool Stretch { get; }
        /// <summary>원본 1픽셀이 논리 좌표로 몇인가. 0이면 화면 배율의 타일 배율(ScreenFit.TileScale)을 쓴다.</summary>
        public float LogicalPerPixel { get; }
        /// <summary>이 그림 위 글자 색(도시형 몰딩 버튼). 없으면 화면이 정한 색.</summary>
        public UiColor? TextColor { get; }

        /// <summary>그릴 때의 정수 배율.</summary>
        public int Scale(ScreenFit fit) => LogicalPerPixel <= 0 ? fit.TileScale : Math.Max(1, (int)MathF.Round(LogicalPerPixel * fit.Scale));
    }

    /// <summary>
    /// 패널 그림 이름(UiSkin) → 어느 PNG의 어디인가. 그림 좌표는 이 파일에만 둔다.
    /// - Style2: Modern_UI_Style_2.png(16x16 판). 값은 PNG 픽셀을 직접 재서 정했다(2026-11-11). 16px 칸 안에서 그림은 일부만 차지하므로
    ///   칸 단위(r, c)로 자르면 안 된다. Unity 판 Style2 대체 경로에서 패널·칸은 공통 패널, 대화창은 r14~r16 c3~c5다.
    /// - 도시형 몰딩: 좌표는 데이터에서 읽는다. data/ui/UrbanUiThemeDefinition.json의 panel/button/slotSprite → data/atlases의 그 PNG 좌표표
    ///   (Unity 스프라이트 사각형과 9조각 테두리, 위 왼쪽 기준). 원본 PNG에서 테두리 바깥 2px는 그림자라 Unity도 그리지 않는다(2026-11-12 실측).
    ///   Unity ModernUiTileImage처럼 칸 그림은 slot, 버튼은 button, 그 밖(패널·대화창)은 panel. 글자 칸은 slot. Canvas 기준 PPU 16에서
    ///   pixelsPerUnitMultiplier 0.5라 원본 1픽셀이 논리 2.
    /// </summary>
    public sealed class UiSkins
    {
        public const string Style2Sheet = "Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png";
        public const string UrbanThemeFile = "data/ui/UrbanUiThemeDefinition.json";

        // 공통 패널(r2~r4, c0~c2): 그림 x11~37, y42~69. 모서리 그림이 들어가는 폭: 왼쪽 5, 오른쪽 5, 위 6, 아래 5.
        public static readonly NineSliceSource Style2CommonPanel = new NineSliceSource(11, 16, 32, 37, 42, 48, 64, 69);

        // 둥근 밝은 상자(r14~r16, c3~c5 대화 패널): 그림 x60~84, y235~260.
        // 왼쪽 위 모서리의 밝은 점이 x63, y239까지 내려와 왼쪽 4, 위 5로 잡는다. 오른쪽 3, 아래 3.
        public static readonly NineSliceSource Style2RoundedLight = new NineSliceSource(60, 64, 81, 84, 235, 240, 257, 260);

        // 둥근 어두운 상자(r14~r16, c0~c2): 그림 x12~36, y235~260. 밝은 상자와 모양이 같고 안쪽 색만 다르다.
        public static readonly NineSliceSource Style2RoundedDark = new NineSliceSource(12, 16, 33, 36, 235, 240, 257, 260);

        private const float UrbanLogicalPerPixel = 2f;

        private readonly Dictionary<string, SkinSource> _skins = new Dictionary<string, SkinSource>(StringComparer.Ordinal);

        private UiSkins(SkinTheme theme) { Theme = theme; }

        public SkinTheme Theme { get; }

        public SkinSource Get(string skin) =>
            skin != null && _skins.TryGetValue(skin, out SkinSource source) ? source : throw new InvalidOperationException("모르는 패널 그림 이름: " + skin);

        public static UiSkins Style2()
        {
            var skins = new UiSkins(SkinTheme.Style2);
            skins._skins[UiSkin.Panel] = new SkinSource(Style2Sheet, Style2CommonPanel, false, 0, null);
            skins._skins[UiSkin.Button] = new SkinSource(Style2Sheet, Style2RoundedLight, false, 0, null);
            skins._skins[UiSkin.Field] = new SkinSource(Style2Sheet, Style2RoundedLight, false, 0, null);
            skins._skins[UiSkin.Dialogue] = new SkinSource(Style2Sheet, Style2RoundedLight, false, 0, null);
            skins._skins[UiSkin.Slot] = new SkinSource(Style2Sheet, Style2RoundedDark, false, 0, null);
            return skins;
        }

        public static UiSkins Urban(string repoRoot, SpriteAtlases atlases)
        {
            UrbanTheme theme = UrbanTheme.Load(repoRoot);
            SkinSource Sliced(SpriteRef sprite, UiColor? text)
            {
                AtlasSprite s = atlases.Find(sprite.Path, sprite.Name);
                if (!s.HasBorder) throw new InvalidDataException("도시형 몰딩 그림에 9조각 테두리가 없다: " + sprite);
                var slices = new NineSliceSource(s.X, s.X + s.BorderLeft, s.X + s.W - s.BorderRight, s.X + s.W,
                    s.Y, s.Y + s.BorderTop, s.Y + s.H - s.BorderBottom, s.Y + s.H);
                return new SkinSource(sprite.Path, slices, true, UrbanLogicalPerPixel, text);
            }

            var skins = new UiSkins(SkinTheme.Urban);
            SkinSource panel = Sliced(theme.Panel, null);
            skins._skins[UiSkin.Panel] = panel;
            skins._skins[UiSkin.Dialogue] = panel;
            skins._skins[UiSkin.Button] = Sliced(theme.Button, theme.ButtonTextColor);
            SkinSource slot = Sliced(theme.Slot, null);
            skins._skins[UiSkin.Slot] = slot;
            // Unity 판 글자 칸(이름, 가방 검색)은 테마 그림 없이 흰 바탕 Image였다. 패널 그림(테두리 논리 36)은 글자를 가리므로
            // 테두리가 얇은 칸 그림으로 둔다.
            skins._skins[UiSkin.Field] = slot;
            return skins;
        }

        public static UiSkins For(SkinTheme theme, string repoRoot, SpriteAtlases atlases) =>
            theme == SkinTheme.Urban ? Urban(repoRoot, atlases) : Style2();
    }

    /// <summary>데이터의 그림 참조: PNG 경로(저장소 기준)와 그 안의 스프라이트 이름.</summary>
    public readonly record struct SpriteRef(string Path, string Name)
    {
        public override string ToString() => Path + "#" + Name;
    }

    /// <summary>data/ui/UrbanUiThemeDefinition.json: 패널·버튼·칸 그림, 버튼 글자 색, 아이콘 키 → 그림.</summary>
    public sealed class UrbanTheme
    {
        public SpriteRef Panel, Button, Slot;
        public UiColor ButtonTextColor;
        public readonly Dictionary<string, SpriteRef> Icons = new Dictionary<string, SpriteRef>(StringComparer.Ordinal);
        /// <summary>정의 id → 그 정의의 아이콘 키(테마의 presentations, Unity UrbanUiThemeDefinition.ResolveIcon).</summary>
        public readonly Dictionary<string, string> Presentations = new Dictionary<string, string>(StringComparer.Ordinal);

        public static UrbanTheme Load(string repoRoot)
        {
            string path = System.IO.Path.Combine(repoRoot, UiSkins.UrbanThemeFile);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement[0] : doc.RootElement;
            var theme = new UrbanTheme
            {
                Panel = Ref(root.GetProperty("panelSprite")),
                Button = Ref(root.GetProperty("buttonSprite")),
                Slot = Ref(root.GetProperty("slotSprite")),
            };
            JsonElement c = root.GetProperty("buttonTextColor");
            byte B(string k) => (byte)MathF.Round(Math.Clamp(c.GetProperty(k).GetSingle(), 0f, 1f) * 255f);
            theme.ButtonTextColor = new UiColor(B("r"), B("g"), B("b"), B("a"));
            if (root.TryGetProperty("icons", out JsonElement icons))
                foreach (JsonElement icon in icons.EnumerateArray())
                    theme.Icons[icon.GetProperty("key").GetString()] = Ref(icon.GetProperty("sprite"));
            if (root.TryGetProperty("presentations", out JsonElement presentations))
                foreach (JsonElement p in presentations.EnumerateArray())
                    if (p.TryGetProperty("definition", out JsonElement d) && d.TryGetProperty("ref", out JsonElement id) && p.TryGetProperty("iconKey", out JsonElement key))
                        theme.Presentations[id.GetString() ?? string.Empty] = key.GetString() ?? string.Empty;
            return theme;
        }

        private static SpriteRef Ref(JsonElement e) => new SpriteRef(e.GetProperty("sprite").GetString(), e.GetProperty("name").GetString());
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
    /// "icon:&lt;키&gt;"(도시형 몰딩 테마의 아이콘 키, 예: icon:journal), "icon:&lt;키&gt;@&lt;정의 id&gt;"(테마가 정의마다 정한 아이콘, 없으면 그 키), "&lt;PNG 경로&gt;#&lt;스프라이트 이름&gt;"(좌표표), "&lt;PNG 경로&gt;"(그림 전체).
    /// 그리기 명령에 원본 칸(Source)이 있으면 그 PNG 안의 그 칸을 쓴다.
    /// </summary>
    public sealed class ImageCatalog
    {
        private readonly SpriteAtlases _atlases;
        private readonly UrbanTheme _theme;
        private readonly Dictionary<string, (string texture, PixelRect source)> _cache = new Dictionary<string, (string, PixelRect)>(StringComparer.Ordinal);

        public ImageCatalog(SpriteAtlases atlases, UrbanTheme theme)
        {
            _atlases = atlases;
            _theme = theme;
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
                    if (_theme != null && _theme.Presentations.TryGetValue(definition, out string own) && _theme.Icons.ContainsKey(own)) key = own;
                }
                if (_theme == null || !_theme.Icons.TryGetValue(key, out SpriteRef icon)) return (null, default);
                return _atlases.TryFind(icon.Path, icon.Name, out AtlasSprite s) ? (icon.Path, s.Rect) : (null, default);
            }
            int hash = imageId.IndexOf('#');
            if (hash < 0) return (imageId, default);
            string path = imageId.Substring(0, hash), name = imageId.Substring(hash + 1);
            return _atlases.TryFind(path, name, out AtlasSprite sprite) ? (path, sprite.Rect) : (null, default);
        }
    }
}
