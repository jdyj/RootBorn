using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rootborn.Data
{
    /// <summary>덮어쓰기로 바뀐 필드 하나: 어느 엔티티의 어느 최상위 필드가 전(JSON 글자)에서 후로. 전이 null이면 원본에 그 필드가 없었다.</summary>
    public sealed class OverlayChange
    {
        public OverlayChange(string type, string id, string field, string before, string after)
        {
            Type = type;
            Id = id;
            Field = field;
            Before = before;
            After = after;
        }

        public string Type { get; }
        public string Id { get; }
        public string Field { get; }
        public string Before { get; }
        public string After { get; }

        public override string ToString() => $"{Type}/{Id}.{Field}: {Before ?? "(없음)"} -> {After}";
    }

    /// <summary>
    /// 데이터 덮어쓰기: data/와 같은 모양의 작은 폴더(엔티티 배열 JSON)로 data/를 고치지 않고 수치를 바꿔 읽는다. 시뮬레이션 테스트용이고 게임 실행은 쓰지 않는다.
    /// 단위는 엔티티(type + id)다. 덮어쓰기 엔티티에 적힌 최상위 필드(type·id 제외)만 같은 엔티티의 원본을 대신하고, 안 적은 필드는 원본 그대로다.
    /// 배열·객체 필드는 안쪽을 합치지 않고 통째로 바꾼다. 원본에 없는 (type, id)는 오류다 — 새 엔티티 추가는 지원하지 않고 오타가 조용히 무시되지 않게 한다.
    /// registry.json과 economy·time 같은 규칙 설정 파일은 덮지 않는다(registry.json이 덮어쓰기 폴더에 있으면 오류).
    /// </summary>
    internal static class DataOverlay
    {
        public const string UnknownEntityKind = "overlay-unknown";
        public const string UnsupportedKind = "overlay-unsupported";
        public const string FolderKind = "overlay-folder";

        /// <summary>table(원본) 위에 overlayDirectory를 덮고 바뀐 목록을 돌려준다. 오류는 errors에 모은다.</summary>
        public static List<OverlayChange> Apply(EntityTable table, string overlayDirectory, List<LoadError> errors)
        {
            var changes = new List<OverlayChange>();
            if (!Directory.Exists(overlayDirectory))
            {
                errors.Add(new LoadError(FolderKind, "", "", overlayDirectory, "덮어쓰기 폴더가 없다"));
                return changes;
            }

            if (File.Exists(Path.Combine(overlayDirectory, EntityTable.RegistryFileName)))
                errors.Add(new LoadError(UnsupportedKind, "", "", Path.Combine(overlayDirectory, EntityTable.RegistryFileName), "registry.json은 덮어쓸 수 없다"));

            var overlay = EntityTable.ReadDirectory(overlayDirectory, errors);
            foreach (var node in overlay.All)
            {
                if (!table.TryGet(node.Type, node.Id, out var original))
                {
                    errors.Add(new LoadError(UnknownEntityKind, node.Type, node.Id, node.File, "원본 data/에 없는 (type, id)다. 덮어쓰기는 있는 엔티티만 바꾼다"));
                    continue;
                }

                var merged = Merge(original, node, changes);
                if (merged != null) table.Replace(original, merged);
            }

            return changes;
        }

        private static EntityNode Merge(EntityNode original, EntityNode overlay, List<OverlayChange> changes)
        {
            var result = JsonNode.Parse(original.Json.GetRawText()).AsObject();
            bool any = false;
            foreach (var property in overlay.Json.EnumerateObject())
            {
                if (property.Name == "type" || property.Name == "id") continue;

                var after = JsonNode.Parse(property.Value.GetRawText());
                string afterText = Compact(after);
                string beforeText = original.Json.TryGetProperty(property.Name, out var before) ? Compact(JsonNode.Parse(before.GetRawText())) : null;
                if (beforeText == afterText) continue;

                result[property.Name] = after;
                changes.Add(new OverlayChange(original.Type, original.Id, property.Name, beforeText, afterText));
                any = true;
            }

            if (!any) return null;
            using var document = JsonDocument.Parse(result.ToJsonString());
            var json = document.RootElement.Clone();
            var asset = json.TryGetProperty("asset", out var assetValue) && assetValue.ValueKind == JsonValueKind.String ? assetValue.GetString() : "";
            return new EntityNode(original.Type, original.Id, asset, json, original.File, original.Index);
        }

        private static string Compact(JsonNode node) => node == null ? "null" : node.ToJsonString();
    }
}
