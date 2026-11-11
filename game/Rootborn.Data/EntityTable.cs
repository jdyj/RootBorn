using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Rootborn.Data
{
    /// <summary>
    /// data/ 아래 엔티티 파일을 모두 읽어 (type, id) → JSON 노드 표로 둔다. 객체는 만들지 않는다.
    /// 엔티티 파일 = 맨 위가 배열이고 원소가 type과 id를 가진 JSON. 그 밖의 JSON(등록표, 현지화, 그림 좌표표)은 건너뛴다.
    /// JSON 문법이 깨진 파일은 parse 오류 하나로 모으고 나머지는 계속 읽는다.
    /// </summary>
    public sealed class EntityTable
    {
        public const string RegistryFileName = "registry.json";

        private readonly Dictionary<(string Type, string Id), EntityNode> _byKey = new Dictionary<(string, string), EntityNode>();
        private readonly List<EntityNode> _ordered = new List<EntityNode>();
        private readonly HashSet<string> _types = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _registryKeys = new List<string>();
        private readonly Dictionary<string, IReadOnlyList<string>> _registry = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        private EntityTable()
        {
        }

        /// <summary>(파일 경로, 배열 순서) 순서의 모든 엔티티.</summary>
        public IEnumerable<EntityNode> All => _ordered;

        public IReadOnlyCollection<string> Types => _types;

        /// <summary>registry.json의 키, 파일 순서 그대로.</summary>
        public IReadOnlyList<string> RegistryKeys => _registryKeys;

        public bool TryGet(string type, string id, out EntityNode node)
        {
            if (type == null || id == null)
            {
                node = null;
                return false;
            }

            return _byKey.TryGetValue((type, id), out node);
        }

        /// <summary>registry.json의 그 키 아래 id 목록(순서 있음). 키가 없으면 빈 목록. 문자열 하나인 키는 원소 하나짜리 목록.</summary>
        public IReadOnlyList<string> RegistryIds(string registryKey)
        {
            return registryKey != null && _registry.TryGetValue(registryKey, out var ids) ? ids : Array.Empty<string>();
        }

        public static EntityTable ReadDirectory(string dataDirectory, List<LoadError> errors)
        {
            if (dataDirectory == null) throw new ArgumentNullException(nameof(dataDirectory));
            if (errors == null) throw new ArgumentNullException(nameof(errors));

            var table = new EntityTable();
            var files = Directory.GetFiles(dataDirectory, "*.json", SearchOption.AllDirectories)
                .Select(path => (Full: path, Relative: Path.GetRelativePath(dataDirectory, path).Replace('\\', '/')))
                .OrderBy(file => file.Relative, StringComparer.Ordinal)
                .ToList();

            foreach (var file in files)
            {
                JsonElement root;
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllBytes(file.Full));
                    root = document.RootElement.Clone();
                }
                catch (JsonException e)
                {
                    errors.Add(new LoadError("parse", "", "", file.Full, e.Message));
                    continue;
                }

                if (file.Relative == RegistryFileName)
                {
                    table.ReadRegistry(root);
                    continue;
                }

                table.ReadEntityFile(file.Full, root, errors);
            }

            return table;
        }

        private void ReadEntityFile(string file, JsonElement root, List<LoadError> errors)
        {
            if (root.ValueKind != JsonValueKind.Array || !root.EnumerateArray().Any(IsEntityElement)) return;

            var index = 0;
            foreach (var element in root.EnumerateArray())
            {
                var at = index++;
                if (!IsEntityElement(element))
                {
                    errors.Add(new LoadError("missing-field", "", "", file, $"원소 {at}에 문자열 type과 id가 없다"));
                    continue;
                }

                var type = element.GetProperty("type").GetString();
                var id = element.GetProperty("id").GetString();
                var asset = element.TryGetProperty("asset", out var assetValue) && assetValue.ValueKind == JsonValueKind.String
                    ? assetValue.GetString()
                    : "";
                var node = new EntityNode(type, id, asset, element, file, at);

                if (_byKey.TryGetValue((type, id), out var existing))
                {
                    errors.Add(new LoadError("duplicate", type, id, node.Source,
                        $"같은 (type, id)가 두 곳에 있다: {existing.Source} 와 {node.Source}"));
                    continue;
                }

                _byKey.Add((type, id), node);
                _ordered.Add(node);
                _types.Add(type);
            }
        }

        private static bool IsEntityElement(JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
                && element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String;
        }

        private void ReadRegistry(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return;

            foreach (var property in root.EnumerateObject())
            {
                IReadOnlyList<string> ids;
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    ids = property.Value.EnumerateArray()
                        .Where(value => value.ValueKind == JsonValueKind.String)
                        .Select(value => value.GetString())
                        .ToArray();
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    ids = new[] { property.Value.GetString() };
                }
                else
                {
                    continue;
                }

                _registryKeys.Add(property.Name);
                _registry[property.Name] = ids;
            }
        }
    }
}
