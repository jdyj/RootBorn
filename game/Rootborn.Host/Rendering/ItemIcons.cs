using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Rootborn.Host.Rendering
{
    /// <summary>
    /// 아이템 id → 그림(PNG 경로와 스프라이트 이름). 화면은 아이콘 키 "item:&lt;아이템 id&gt;"만 말하고(가방 칸, 상세),
    /// 어느 PNG인지는 여기서 data/items/ItemDefinition.json의 icon으로 정한다. Core 아이템 정의는 그림을 갖지 않는다.
    /// 그림이 없는 아이템(icon이 null)은 표에 없다.
    /// </summary>
    public sealed class ItemIcons
    {
        public static readonly ItemIcons Empty = new ItemIcons(new Dictionary<string, SpriteRef>(StringComparer.Ordinal));

        private readonly Dictionary<string, SpriteRef> _icons;

        private ItemIcons(Dictionary<string, SpriteRef> icons) { _icons = icons; }

        public static ItemIcons Load(string repoRoot)
        {
            string path = Path.Combine(repoRoot, "data", "items", "ItemDefinition.json");
            if (!File.Exists(path)) return Empty;
            var icons = new Dictionary<string, SpriteRef>(StringComparer.Ordinal);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out JsonElement id) || !item.TryGetProperty("icon", out JsonElement icon) || icon.ValueKind != JsonValueKind.Object) continue;
                if (!icon.TryGetProperty("sprite", out JsonElement sprite) || !icon.TryGetProperty("name", out JsonElement name)) continue;
                icons[id.GetString() ?? string.Empty] = new SpriteRef(sprite.GetString(), name.GetString());
            }
            return new ItemIcons(icons);
        }

        public bool TryGet(string itemId, out SpriteRef icon) => _icons.TryGetValue(itemId ?? string.Empty, out icon);
    }
}
