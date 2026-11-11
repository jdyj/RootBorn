using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Rootborn.Screens
{
    /// <summary>
    /// 엔티티의 화면 이름 키(displayNameKey)를 data/의 JSON에서 읽는다. Core 정의가 화면 이름 키를 갖지 않는 종류에만 쓴다 —
    /// 지금은 옷 칸(WearableSlotDefinition)과 옷(WearableDefinition)뿐이다(옷장). 다른 종류는 Core 정의의 DisplayNameKey를 쓴다.
    /// 파일은 종류 이름으로 찾고, 처음 물을 때 그 파일 하나만 읽는다. Core 옷 규칙에 이름 키가 들어오면 이 파일을 지운다.
    /// </summary>
    public sealed class DisplayNames
    {
        public static readonly DisplayNames Empty = new DisplayNames(null);

        private readonly string _dataDirectory;
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, string>> _byType = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private DisplayNames(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
            if (dataDirectory == null || !Directory.Exists(dataDirectory)) return;
            foreach (string file in Directory.GetFiles(dataDirectory, "*.json", SearchOption.AllDirectories))
                _files[Path.GetFileNameWithoutExtension(file)] = file;
        }

        public static DisplayNames Load(string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory)) throw new DirectoryNotFoundException("데이터 폴더가 없다: " + dataDirectory);
            return new DisplayNames(dataDirectory);
        }

        /// <summary>종류(데이터 파일 이름)와 id로 화면 이름 키. 없으면 빈 글자.</summary>
        public string Get(string type, string id)
        {
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id)) return string.Empty;
            if (!_byType.TryGetValue(type, out Dictionary<string, string> table))
            {
                table = Read(type);
                _byType[type] = table;
            }
            return table.TryGetValue(id, out string key) ? key : string.Empty;
        }

        private Dictionary<string, string> Read(string type)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!_files.TryGetValue(type, out string file)) return table;
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return table;
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String) continue;
                if (item.TryGetProperty("displayNameKey", out JsonElement key) && key.ValueKind == JsonValueKind.String)
                    table[id.GetString()] = key.GetString();
            }
            return table;
        }
    }
}
