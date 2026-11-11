using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Rootborn.Data.Tests.Integrity
{
    /// <summary>
    /// 온전성 검사가 같이 쓰는 읽기 도우미. data/ 엔티티를 한 번 읽어 두고, JSON 안의 객체를 경로와 함께 훑는다.
    /// 게임 코드처럼 읽지 않고 파일을 있는 그대로 본다(규칙이 아니라 데이터의 빈 곳을 찾는 검사다).
    /// </summary>
    internal sealed class DataScan
    {
        private static readonly Lazy<DataScan> _real = new Lazy<DataScan>(() => new DataScan(RepoPaths.Root));

        /// <summary>주석(//)과 끝 쉼표를 허용한다. game/world/*.json이 주석을 쓴다.</summary>
        public static readonly JsonDocumentOptions Lenient = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>시험용 저장소(임시 폴더)를 읽는다. 그 아래 data/registry.json이 있어야 한다.</summary>
        public static DataScan Of(string root) => new DataScan(root);

        private DataScan(string root)
        {
            Root = root;
            DataDirectory = Path.Combine(root, "data");
            var errors = new List<LoadError>();
            Table = EntityTable.ReadDirectory(DataDirectory, errors);
            if (errors.Count > 0) throw new InvalidDataException("data/를 읽지 못했다:\n" + string.Join("\n", errors));
            Entities = Table.All.ToList();
            Registry = ReadJson(Path.Combine(DataDirectory, EntityTable.RegistryFileName));
            Ids = new HashSet<string>(Entities.Select(e => e.Id), StringComparer.Ordinal);
        }

        /// <summary>저장소의 실제 data/(한 번만 읽는다).</summary>
        public static DataScan Real => _real.Value;

        public string Root { get; }
        public string DataDirectory { get; }
        public EntityTable Table { get; }
        public IReadOnlyList<EntityNode> Entities { get; }

        /// <summary>registry.json 전체(엔티티 표에는 id 목록만 들어가므로 그림 참조를 보려고 따로 읽는다).</summary>
        public JsonElement Registry { get; }

        /// <summary>종류와 무관하게 data/에 있는 모든 엔티티 id.</summary>
        public HashSet<string> Ids { get; }

        public bool HasEntity(string type, string id) => Table.TryGet(type, id, out _);

        /// <summary>저장소 뿌리 기준 경로(슬래시)를 절대 경로로.</summary>
        public string Full(string repoRelative) => Path.GetFullPath(Path.Combine(Root, repoRelative));

        /// <summary>절대 경로가 저장소 안인가.</summary>
        public bool InsideRepo(string fullPath)
        {
            var root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(fullPath).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>저장소 뿌리 기준 경로(슬래시)로.</summary>
        public string Relative(string fullPath) => Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

        public static JsonElement ReadJson(string path)
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path), Lenient);
            return document.RootElement.Clone();
        }

        /// <summary>엔티티마다 (주인 "type/id", 그 안의 모든 객체와 경로). 경로는 "tileParts[0].tile"처럼 쓴다. 맨 위 객체의 경로는 "".</summary>
        public IEnumerable<(string Owner, EntityNode Node, string Path, JsonElement Object)> EntityObjects()
        {
            foreach (var node in Entities)
                foreach (var (path, obj) in Objects(node.Json, ""))
                    yield return (node.Type + "/" + node.Id, node, path, obj);
        }

        /// <summary>값 안의 모든 객체를 경로와 함께(자기 자신 포함).</summary>
        public static IEnumerable<(string Path, JsonElement Object)> Objects(JsonElement value, string path)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    yield return (path, value);
                    foreach (var property in value.EnumerateObject())
                        foreach (var inner in Objects(property.Value, path.Length == 0 ? property.Name : path + "." + property.Name))
                            yield return inner;
                    break;
                case JsonValueKind.Array:
                    var i = 0;
                    foreach (var element in value.EnumerateArray())
                    {
                        foreach (var inner in Objects(element, path + "[" + i + "]")) yield return inner;
                        i++;
                    }

                    break;
            }
        }

        /// <summary>객체가 정확히 이 이름들의 문자열 필드만 갖는가.</summary>
        public static bool HasOnlyStrings(JsonElement obj, params string[] fields)
        {
            var count = 0;
            foreach (var property in obj.EnumerateObject())
            {
                if (Array.IndexOf(fields, property.Name) < 0 || property.Value.ValueKind != JsonValueKind.String) return false;
                count++;
            }

            return count == fields.Length;
        }

        public static string Str(JsonElement obj, string field)
        {
            return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
