using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Rootborn.Host.Rendering;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 도시형 몰딩 테마를 예전 자리(data/ui/UrbanUiThemeDefinition.json + data/atlases 좌표표)에서 game/themes의 theme.json 꼴로 바꾸는 도우미.
    /// 예전 Host가 읽던 규칙을 그대로 옮겼다:
    /// - 패널·대화창 = panelSprite, 버튼 = buttonSprite(+ buttonTextColor), 칸·글자 칸 = slotSprite. 모두 늘이기, 원본 1픽셀 = 논리 2.
    /// - 아이콘 키 → 좌표표의 칸. presentations(정의 id → 아이콘 키) → definitionIcons.
    /// 아이콘은 Style2 테마에서도 같다(예전 Host는 스킨과 상관없이 도시형 몰딩 아이콘을 썼다).
    /// ThemeConversionTests가 결과를 game/themes의 파일과 대조하고, WriteThemeFiles(Explicit)로 다시 쓴다.
    /// </summary>
    public static class UrbanThemeConversion
    {
        public const string UrbanThemeFile = "data/ui/UrbanUiThemeDefinition.json";
        private const int UrbanPixelSize = 2;

        /// <summary>예전 데이터에서 읽은 아이콘·정의별 아이콘(두 테마가 같이 쓴다).</summary>
        public static (JsonObject icons, JsonObject definitionIcons) Icons(string repoRoot)
        {
            JsonElement root = ThemeRoot(repoRoot, out JsonDocument doc);
            using (doc)
            {
                var atlases = new SpriteAtlases(repoRoot);
                var icons = new JsonObject();
                foreach (JsonElement icon in root.GetProperty("icons").EnumerateArray())
                {
                    SpriteRef sprite = Ref(icon.GetProperty("sprite"));
                    AtlasSprite s = atlases.Find(sprite.Path, sprite.Name);
                    icons[icon.GetProperty("key").GetString()] = new JsonObject { ["image"] = ThemeFolder.RepoPrefix + sprite.Path, ["rect"] = Ints(s.X, s.Y, s.W, s.H) };
                }
                var definitionIcons = new JsonObject();
                foreach (JsonElement p in root.GetProperty("presentations").EnumerateArray())
                    if (p.TryGetProperty("definition", out JsonElement d) && d.TryGetProperty("ref", out JsonElement id) && p.TryGetProperty("iconKey", out JsonElement key))
                        definitionIcons[id.GetString()] = key.GetString();
                return (icons, definitionIcons);
            }
        }

        public static JsonObject Urban(string repoRoot)
        {
            JsonElement root = ThemeRoot(repoRoot, out JsonDocument doc);
            JsonObject panel, button, slot;
            using (doc)
            {
                var atlases = new SpriteAtlases(repoRoot);
                JsonObject Sliced(JsonElement spriteElement)
                {
                    SpriteRef sprite = Ref(spriteElement);
                    AtlasSprite s = atlases.Find(sprite.Path, sprite.Name);
                    if (!s.HasBorder) throw new InvalidDataException("도시형 몰딩 그림에 9조각 테두리가 없다: " + sprite);
                    return new JsonObject
                    {
                        ["image"] = ThemeFolder.RepoPrefix + sprite.Path,
                        ["rect"] = Ints(s.X, s.Y, s.W, s.H),
                        ["border"] = Ints(s.BorderLeft, s.BorderTop, s.BorderRight, s.BorderBottom),
                        ["fill"] = "stretch",
                        ["pixelSize"] = UrbanPixelSize,
                    };
                }
                panel = Sliced(root.GetProperty("panelSprite"));
                button = Sliced(root.GetProperty("buttonSprite"));
                JsonElement c = root.GetProperty("buttonTextColor");
                byte B(string k) => (byte)MathF.Round(Math.Clamp(c.GetProperty(k).GetSingle(), 0f, 1f) * 255f);
                button["textColor"] = ThemeFolder.ToHex(new UiColor(B("r"), B("g"), B("b"), B("a")));
                slot = Sliced(root.GetProperty("slotSprite"));
            }
            (JsonObject icons, JsonObject definitionIcons) = Icons(repoRoot);
            return new JsonObject
            {
                ["format"] = ThemeFolder.SupportedFormat,
                ["name"] = "urban",
                ["about"] = "도시형 몰딩(Unity 판의 승인된 기본 스킨, 2026-10-27). data/ui/UrbanUiThemeDefinition.json과 data/atlases 좌표표에서 옮겼다. 글자 칸은 테두리가 얇은 칸 그림, 대화창은 패널 그림.",
                ["skins"] = new JsonObject
                {
                    [UiSkin.Panel] = panel,
                    [UiSkin.Dialogue] = panel.DeepClone(),
                    [UiSkin.Button] = button,
                    [UiSkin.Slot] = slot,
                    [UiSkin.Field] = slot.DeepClone(),
                },
                ["icons"] = icons,
                ["definitionIcons"] = definitionIcons,
                ["textColors"] = DefaultTextColors(),
            };
        }

        public static JsonObject DefaultTextColors() => new JsonObject
        {
            ["ink"] = ThemeFolder.ToHex(UiTheme.Ink),
            ["inkMuted"] = ThemeFolder.ToHex(UiTheme.InkMuted),
            ["inkOnDark"] = ThemeFolder.ToHex(UiTheme.InkOnDark),
        };

        /// <summary>들여 쓴 JSON. 수만 든 배열(rect, border)은 한 줄로.</summary>
        public static string ToText(JsonNode node)
        {
            string text = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() }).Replace("\r\n", "\n");
            text = Regex.Replace(text, @"\[\s*(-?\d+(?:\s*,\s*-?\d+)*)\s*\]", m => "[ " + Regex.Replace(m.Groups[1].Value, @"\s*,\s*", ", ") + " ]");
            return text + "\n";
        }

        private static JsonElement ThemeRoot(string repoRoot, out JsonDocument doc)
        {
            doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, UrbanThemeFile)));
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement[0] : doc.RootElement;
        }

        private static SpriteRef Ref(JsonElement e) => new SpriteRef(e.GetProperty("sprite").GetString(), e.GetProperty("name").GetString());

        private static JsonArray Ints(params int[] values)
        {
            var array = new JsonArray();
            foreach (int v in values) array.Add(v);
            return array;
        }
    }
}
