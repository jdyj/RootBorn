using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Rootborn.Ui;

namespace Rootborn.Host.Rendering
{
    /// <summary>
    /// UI 테마 폴더: theme.json 하나와 PNG들(형식 설명은 game/themes/README.md). 테마 종류를 코드에 적지 않는다 — 폴더를 더하면 새 테마다.
    /// 읽을 때 화면이 쓰는 스킨·아이콘(ThemeRequirements)이 다 있는지, 그림 파일이 있는지, 칸이 그림 안에 드는지 본다.
    /// 문제가 있으면 모두 모아 ThemeException으로 알린다(빈 그림으로 그리지 않는다).
    /// </summary>
    public sealed class ThemeFolder
    {
        public const string FileName = "theme.json";
        /// <summary>그림 경로가 이 말로 시작하면 저장소 기준, 아니면 테마 폴더 기준.</summary>
        public const string RepoPrefix = "repo:";
        public const int SupportedFormat = 1;

        private ThemeFolder(string directory, string name, UiSkins skins, Dictionary<string, ThemeIcon> icons, Dictionary<string, string> definitionIcons, ThemeTextColors textColors)
        {
            Directory = directory; Name = name; Skins = skins; Icons = icons; DefinitionIcons = definitionIcons; TextColors = textColors;
        }

        public string Directory { get; }
        public string Name { get; }
        public UiSkins Skins { get; }
        /// <summary>아이콘 키 → 그림.</summary>
        public IReadOnlyDictionary<string, ThemeIcon> Icons { get; }
        /// <summary>정의 id → 그 정의의 아이콘 키("icon:&lt;키&gt;@&lt;정의 id&gt;"를 풀 때 먼저 본다).</summary>
        public IReadOnlyDictionary<string, string> DefinitionIcons { get; }
        public ThemeTextColors TextColors { get; }

        /// <summary>
        /// --ui-theme 값 → 테마 폴더. 경로 구분자가 있거나 절대 경로면 그 폴더, 아니면 저장소의 game/themes/&lt;이름&gt;.
        /// </summary>
        public static string Locate(string nameOrPath, string repoRoot)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath)) throw new InvalidOperationException("UI 테마 이름이 비었다");
            bool isPath = Path.IsPathRooted(nameOrPath) || nameOrPath.IndexOf('/') >= 0 || nameOrPath.IndexOf('\\') >= 0;
            return isPath ? Path.GetFullPath(nameOrPath) : Path.Combine(repoRoot, "game", "themes", nameOrPath);
        }

        /// <summary>테마 폴더를 읽고 검사한다(필요한 스킨·아이콘은 ThemeRequirements).</summary>
        public static ThemeFolder Load(string directory, string repoRoot)
        {
            string file = Path.Combine(directory, FileName);
            if (!File.Exists(file)) throw new ThemeException(directory, new[] { "theme.json이 없다: " + file });

            var problems = new List<string>();
            var skins = new Dictionary<string, SkinSource>(StringComparer.Ordinal);
            var icons = new Dictionary<string, ThemeIcon>(StringComparer.Ordinal);
            var definitionIcons = new Dictionary<string, string>(StringComparer.Ordinal);
            var sizes = new Dictionary<string, (int w, int h)?>(StringComparer.OrdinalIgnoreCase);
            ThemeTextColors textColors = ThemeTextColors.Default;
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));

            JsonDocument doc;
            try { doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
            catch (JsonException e) { throw new ThemeException(directory, new[] { "theme.json을 읽을 수 없다: " + e.Message }); }

            using (doc)
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("format", out JsonElement format) && format.GetInt32() != SupportedFormat)
                    problems.Add("모르는 형식 번호 " + format.GetInt32() + "(읽을 수 있는 것: " + SupportedFormat + ")");
                if (root.TryGetProperty("name", out JsonElement n) && n.ValueKind == JsonValueKind.String) name = n.GetString();

                if (root.TryGetProperty("skins", out JsonElement skinTable))
                    foreach (JsonProperty p in skinTable.EnumerateObject())
                    {
                        SkinSource skin = ReadSkin(p.Name, p.Value, directory, repoRoot, sizes, problems);
                        if (skin != null) skins[p.Name] = skin;
                    }
                if (root.TryGetProperty("icons", out JsonElement iconTable))
                    foreach (JsonProperty p in iconTable.EnumerateObject())
                    {
                        string where = "아이콘 " + p.Name;
                        string texture = ReadImage(p.Value, where, directory, repoRoot, sizes, problems, out (int w, int h)? size);
                        if (texture == null) continue;
                        PixelRect rect = default;
                        if (p.Value.TryGetProperty("rect", out JsonElement r))
                        {
                            rect = ReadRect(r, where, problems);
                            CheckInside(rect, size, where, problems);
                        }
                        icons[p.Name] = new ThemeIcon(texture, rect);
                    }
                if (root.TryGetProperty("definitionIcons", out JsonElement defs))
                    foreach (JsonProperty p in defs.EnumerateObject())
                        definitionIcons[p.Name] = p.Value.GetString() ?? string.Empty;
                if (root.TryGetProperty("textColors", out JsonElement colors))
                    textColors = ThemeTextColors.Read(colors, problems);
            }

            foreach (string skin in ThemeRequirements.Skins)
                if (!skins.ContainsKey(skin)) problems.Add("빠진 스킨: " + skin);
            foreach (string icon in ThemeRequirements.Icons)
                if (!icons.ContainsKey(icon)) problems.Add("빠진 아이콘: " + icon);
            foreach (KeyValuePair<string, string> d in definitionIcons)
                if (!icons.ContainsKey(d.Value)) problems.Add("정의별 아이콘 " + d.Key + "이 없는 아이콘 키를 가리킨다: " + d.Value);

            if (problems.Count > 0) throw new ThemeException(directory, problems);
            return new ThemeFolder(directory, name, new UiSkins(skins), icons, definitionIcons, textColors);
        }

        private static SkinSource ReadSkin(string skinName, JsonElement e, string directory, string repoRoot, Dictionary<string, (int, int)?> sizes, List<string> problems)
        {
            string where = "스킨 " + skinName;
            int before = problems.Count;
            string texture = ReadImage(e, where, directory, repoRoot, sizes, problems, out (int w, int h)? size);
            PixelRect rect;
            if (e.TryGetProperty("rect", out JsonElement r)) rect = ReadRect(r, where, problems);
            else if (size.HasValue) rect = new PixelRect(0, 0, size.Value.w, size.Value.h);
            else rect = default;
            CheckInside(rect, size, where, problems);

            int[] border = e.TryGetProperty("border", out JsonElement b) ? ReadInts(b, 4, where + " border", problems) : null;
            if (border == null) { problems.Add(where + ": border(왼쪽, 위, 오른쪽, 아래 두께)가 없다"); border = new int[4]; }
            if (border[0] < 0 || border[1] < 0 || border[2] < 0 || border[3] < 0 || border[0] + border[2] >= rect.W || border[1] + border[3] >= rect.H)
                problems.Add(where + ": border가 그림 칸보다 두껍다(가운데가 남아야 한다)");

            string fill = e.TryGetProperty("fill", out JsonElement f) ? f.GetString() : "stretch";
            if (fill != "stretch" && fill != "tile") problems.Add(where + ": fill은 stretch 또는 tile: " + fill);
            float pixelSize = e.TryGetProperty("pixelSize", out JsonElement px) ? px.GetSingle() : 0f;
            if (pixelSize < 0f) problems.Add(where + ": pixelSize는 0 이상");

            UiColor? textColor = null;
            if (e.TryGetProperty("textColor", out JsonElement tc)) textColor = ReadColor(tc, where + " textColor", problems);

            float unit = pixelSize > 0f ? pixelSize : ScreenFit.ReferenceTileScale;
            var padding = new SkinPadding(border[0] * unit, border[1] * unit, border[2] * unit, border[3] * unit);
            if (e.TryGetProperty("padding", out JsonElement pad))
            {
                int[] p = ReadInts(pad, 4, where + " padding", problems);
                if (p != null) padding = new SkinPadding(p[0], p[1], p[2], p[3]);
            }

            if (problems.Count > before || texture == null) return null;
            var slices = new NineSliceSource(rect.X, rect.X + border[0], rect.X + rect.W - border[2], rect.X + rect.W,
                rect.Y, rect.Y + border[1], rect.Y + rect.H - border[3], rect.Y + rect.H);
            return new SkinSource(texture, slices, fill == "stretch", pixelSize, textColor, padding);
        }

        private static string ReadImage(JsonElement e, string where, string directory, string repoRoot, Dictionary<string, (int, int)?> sizes, List<string> problems, out (int w, int h)? size)
        {
            size = null;
            if (!e.TryGetProperty("image", out JsonElement image) || image.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(image.GetString()))
            {
                problems.Add(where + ": image(그림 파일)가 없다");
                return null;
            }
            string value = image.GetString();
            string path = value.StartsWith(RepoPrefix, StringComparison.Ordinal)
                ? Path.GetFullPath(Path.Combine(repoRoot, value.Substring(RepoPrefix.Length)))
                : Path.GetFullPath(Path.Combine(directory, value));
            if (!sizes.TryGetValue(path, out size))
            {
                size = File.Exists(path) ? PngSize.Read(path) : null;
                sizes[path] = size;
            }
            if (!File.Exists(path)) { problems.Add(where + ": 그림 파일이 없다: " + value); return null; }
            if (!size.HasValue) { problems.Add(where + ": PNG가 아니다: " + value); return null; }
            return path;
        }

        private static PixelRect ReadRect(JsonElement e, string where, List<string> problems)
        {
            int[] v = ReadInts(e, 4, where + " rect", problems);
            return v == null ? default : new PixelRect(v[0], v[1], v[2], v[3]);
        }

        private static void CheckInside(PixelRect rect, (int w, int h)? size, string where, List<string> problems)
        {
            if (!size.HasValue) return;
            if (rect.W <= 0 || rect.H <= 0 || rect.X < 0 || rect.Y < 0 || rect.X + rect.W > size.Value.w || rect.Y + rect.H > size.Value.h)
                problems.Add(where + ": rect " + rect.X + "," + rect.Y + "," + rect.W + "," + rect.H + "가 그림(" + size.Value.w + "x" + size.Value.h + ") 밖이다");
        }

        private static int[] ReadInts(JsonElement e, int count, string where, List<string> problems)
        {
            if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != count) { problems.Add(where + ": 수 " + count + "개 배열이어야 한다"); return null; }
            var values = new int[count];
            int i = 0;
            foreach (JsonElement v in e.EnumerateArray())
            {
                if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out values[i])) { problems.Add(where + ": 정수가 아니다"); return null; }
                i++;
            }
            return values;
        }

        /// <summary>"#RRGGBB" 또는 "#RRGGBBAA".</summary>
        internal static UiColor ReadColor(JsonElement e, string where, List<string> problems)
        {
            string s = e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            if (s != null && s.Length is 7 or 9 && s[0] == '#' && uint.TryParse(s.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            {
                if (s.Length == 7) v = (v << 8) | 0xFF;
                return new UiColor((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
            }
            problems.Add(where + ": 색은 \"#RRGGBB\" 또는 \"#RRGGBBAA\": " + (s ?? e.ToString()));
            return default;
        }

        public static string ToHex(UiColor c) =>
            c.A == 255 ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B)
                       : string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", c.R, c.G, c.B, c.A);
    }

    /// <summary>아이콘 하나: PNG(전체 경로)와 그 안의 칸(W가 0이면 그림 전체).</summary>
    public readonly record struct ThemeIcon(string Texture, PixelRect Rect);

    /// <summary>스킨 안쪽 여백(논리 좌표). 적지 않으면 테두리 두께 × 원본 1픽셀의 논리 크기.</summary>
    public readonly record struct SkinPadding(float Left, float Top, float Right, float Bottom);

    /// <summary>
    /// 글자 색 묶음. 화면은 Rootborn.Ui.UiTheme의 이름 붙은 색(Ink, InkMuted, InkOnDark)으로 글자를 그리고, 테마가 그 색을 바꿀 수 있다.
    /// 스킨의 textColor(버튼 글자 등)가 먼저다.
    /// </summary>
    public sealed class ThemeTextColors
    {
        public static readonly ThemeTextColors Default = new ThemeTextColors(UiTheme.Ink, UiTheme.InkMuted, UiTheme.InkOnDark);

        public ThemeTextColors(UiColor ink, UiColor inkMuted, UiColor inkOnDark) { Ink = ink; InkMuted = inkMuted; InkOnDark = inkOnDark; }

        public UiColor Ink { get; }
        public UiColor InkMuted { get; }
        public UiColor InkOnDark { get; }

        /// <summary>화면이 고른 글자 색 → 테마의 글자 색. 이름 붙은 색이 아니면 그대로.</summary>
        public UiColor Map(UiColor color) =>
            color == UiTheme.Ink ? Ink : color == UiTheme.InkMuted ? InkMuted : color == UiTheme.InkOnDark ? InkOnDark : color;

        internal static ThemeTextColors Read(JsonElement e, List<string> problems)
        {
            UiColor Get(string key, UiColor fallback) => e.TryGetProperty(key, out JsonElement c) ? ThemeFolder.ReadColor(c, "textColors." + key, problems) : fallback;
            return new ThemeTextColors(Get("ink", UiTheme.Ink), Get("inkMuted", UiTheme.InkMuted), Get("inkOnDark", UiTheme.InkOnDark));
        }
    }

    /// <summary>테마 폴더의 문제들. 시작할 때 이 목록을 그대로 보인다.</summary>
    public sealed class ThemeException : InvalidOperationException
    {
        public ThemeException(string directory, IReadOnlyList<string> problems)
            : base("UI 테마를 쓸 수 없다(" + directory + "):" + Environment.NewLine + " - " + string.Join(Environment.NewLine + " - ", problems))
        {
            Directory = directory; Problems = problems;
        }

        public string Directory { get; }
        public IReadOnlyList<string> Problems { get; }
    }

    /// <summary>PNG 머리(IHDR)에서 가로·세로만 읽는다.</summary>
    public static class PngSize
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static (int w, int h)? Read(string path)
        {
            var head = new byte[24];
            using (FileStream s = File.OpenRead(path))
                if (s.Read(head, 0, head.Length) != head.Length) return null;
            for (int i = 0; i < Signature.Length; i++) if (head[i] != Signature[i]) return null;
            int Int(int at) => (head[at] << 24) | (head[at + 1] << 16) | (head[at + 2] << 8) | head[at + 3];
            return (Int(16), Int(20));
        }
    }
}
