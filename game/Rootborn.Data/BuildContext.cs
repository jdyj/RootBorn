using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Rootborn.Data
{
    /// <summary>
    /// 만들기 함수가 다른 엔티티를 요청하는 문맥. 한 번 만든 (type, id)는 같은 객체를 돌려준다.
    /// { "ref", "type" } 은 표에서 찾아 만들고, ref 없이 type만 있는 객체는 그 자리에서 만든다(id 없음, 기억하지 않음).
    /// 문제는 예외가 아니라 오류로 모으고 null을 돌려준다.
    /// </summary>
    public sealed class BuildContext
    {
        private readonly EntityTable _table;
        private readonly DefinitionBuilders _builders;
        private readonly List<LoadError> _errors;
        private readonly Dictionary<(string Type, string Id), object> _built = new Dictionary<(string, string), object>();
        private readonly HashSet<(string Type, string Id)> _failed = new HashSet<(string, string)>();
        private readonly HashSet<(string Type, string Id)> _building = new HashSet<(string, string)>();

        internal BuildContext(EntityTable table, DefinitionBuilders builders, List<LoadError> errors)
        {
            _table = table;
            _builders = builders;
            _errors = errors;
        }

        /// <summary>필드 하나의 참조(또는 자리 전략)를 만든다. null이면 null.</summary>
        public T One<T>(NodeReader node, string field) where T : class
        {
            if (!node.TryField(field, out var value)) return null;
            return Resolve<T>(node, field, value);
        }

        /// <summary>참조 배열을 만든다. null 원소와 만들지 못한 원소는 빠진다(만들지 못한 것은 오류로 남는다).</summary>
        public List<T> All<T>(NodeReader node, string field) where T : class
        {
            var result = new List<T>();
            if (!node.TryField(field, out var value)) return result;
            if (value.ValueKind != JsonValueKind.Array)
            {
                node.WrongType(field, "참조 배열", value, 0);
                return result;
            }

            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Null) continue;
                var item = Resolve<T>(node, field, element);
                if (item != null) result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// 참조 배열을 자리를 지켜 만든다. null 원소와 만들지 못한 원소는 그 자리에 null이 들어간다.
        /// 다른 배열(개수 등)과 순서로 짝을 맞출 때 쓴다.
        /// </summary>
        public List<T> Slots<T>(NodeReader node, string field) where T : class
        {
            var result = new List<T>();
            if (!node.TryField(field, out var value)) return result;
            if (value.ValueKind != JsonValueKind.Array)
            {
                node.WrongType(field, "참조 배열", value, 0);
                return result;
            }

            foreach (var element in value.EnumerateArray())
                result.Add(element.ValueKind == JsonValueKind.Null ? null : Resolve<T>(node, field, element));

            return result;
        }

        /// <summary>표의 (type, id) 엔티티를 만든다. 없으면 missing-ref 오류와 null.</summary>
        public T ById<T>(string jsonType, string id) where T : class
        {
            if (!_table.TryGet(jsonType, id, out var target))
            {
                _errors.Add(new LoadError("missing-ref", jsonType, id, "", $"표에 {jsonType}/{id}가 없다"));
                return null;
            }

            return As<T>(Build(target, target.Id, target.Source), jsonType, id, target.Source, target.Type, target.Id);
        }

        /// <summary>
        /// 참조가 가리키는 엔티티의 JSON을 만들지 않고 읽는다(아직 Core에 없는 종류의 값 하나가 필요할 때).
        /// null 참조면 false, 표에 없으면 missing-ref 오류와 false.
        /// </summary>
        public bool TryPeek(NodeReader node, string field, out NodeReader target)
        {
            target = default;
            if (!node.TryField(field, out var value)) return false;
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("ref", out var reference) || !value.TryGetProperty("type", out var typeValue))
            {
                node.WrongType(field, "참조 { ref, type }", value, 0);
                return false;
            }

            return TryPeekReference(new NodeReader(node.Type, node.Id, node.Source, value, _errors), out target, field);
        }

        /// <summary>참조 객체({ ref, type }) 하나를 읽는 reader로 그 엔티티를 들여다본다. 만들지 않는다.</summary>
        public bool TryPeekReference(NodeReader reference, out NodeReader target, string field = "(참조)")
        {
            target = default;
            var json = reference.Json;
            if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("ref", out var refValue) || !json.TryGetProperty("type", out var typeValue)
                || typeValue.ValueKind != JsonValueKind.String)
            {
                reference.WrongType(field, "참조 { ref, type }", json, 0);
                return false;
            }

            if (refValue.ValueKind == JsonValueKind.Null) return false;
            if (refValue.ValueKind != JsonValueKind.String)
            {
                reference.WrongType(field, "문자열 ref", refValue, 0);
                return false;
            }

            var type = typeValue.GetString();
            var id = refValue.GetString();
            if (!_table.TryGet(type, id, out var entity))
            {
                reference.Report("missing-ref", $"필드 '{field}'가 가리키는 {type}/{id}가 표에 없다");
                return false;
            }

            target = new NodeReader(entity.Type, entity.Id, entity.Source, entity.Json, _errors);
            return true;
        }

        internal object Build(EntityNode node)
        {
            return Build(node, node.Id, node.Source);
        }

        internal bool TryGetBuilt(EntityNode node, out object built)
        {
            return _built.TryGetValue((node.Type, node.Id), out built);
        }

        private T Resolve<T>(NodeReader owner, string field, JsonElement value) where T : class
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("type", out var typeValue) || typeValue.ValueKind != JsonValueKind.String)
                return owner.WrongType<T>(field, "참조 { ref, type } 또는 { type, ... }", value, null);

            var type = typeValue.GetString();
            if (value.TryGetProperty("ref", out var reference))
            {
                if (reference.ValueKind == JsonValueKind.Null) return null;
                if (reference.ValueKind != JsonValueKind.String)
                    return owner.WrongType<T>(field, "문자열 ref", reference, null);

                var id = reference.GetString();
                if (!_table.TryGet(type, id, out var target))
                {
                    owner.Report("missing-ref", $"필드 '{field}'가 가리키는 {type}/{id}가 표에 없다");
                    return null;
                }

                var built = Build(target, target.Id, target.Source);
                return As<T>(built, owner.Type, owner.Id, owner.Source, type, id, field);
            }

            // ref 없이 type만 있으면 그 자리에서 만든다. id 없음, 기억하지 않음.
            var inline = new NodeReader(type, owner.Id, owner.Source, value, _errors);
            var made = Invoke(type, inline);
            return As<T>(made, owner.Type, owner.Id, owner.Source, type, "(자리 전략)", field);
        }

        private object Build(EntityNode node, string id, string source)
        {
            var key = (node.Type, node.Id);
            if (_built.TryGetValue(key, out var existing)) return existing;
            if (_failed.Contains(key)) return null;
            if (_building.Contains(key))
            {
                _errors.Add(new LoadError("cycle", node.Type, id, source, $"{node.Type}/{id}를 만드는 중에 다시 요청했다(끝나지 않는 참조)"));
                return null;
            }

            _building.Add(key);
            try
            {
                var reader = new NodeReader(node.Type, id, source, node.Json, _errors);
                var made = Invoke(node.Type, reader);
                if (made == null) _failed.Add(key);
                else _built[key] = made;
                return made;
            }
            finally
            {
                _building.Remove(key);
            }
        }

        private object Invoke(string type, NodeReader reader)
        {
            if (!_builders.TryGetBuilder(type, out var build))
            {
                reader.Report("unknown-type", _builders.DescribeUnbuilt(type));
                return null;
            }

            try
            {
                var made = build(reader, this);
                if (made == null) reader.Report("constructor", $"{type} 만들기 함수가 null을 돌려줬다");
                return made;
            }
            catch (Exception e)
            {
                reader.Report("constructor", $"{type}: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        private T As<T>(object built, string ownerType, string ownerId, string ownerSource, string targetType, string targetId, string field = null)
            where T : class
        {
            if (built == null) return null;
            if (built is T typed) return typed;

            var where = field == null ? "" : $"필드 '{field}'의 ";
            _errors.Add(new LoadError("wrong-ref-type", ownerType, ownerId, ownerSource,
                $"{where}{targetType}/{targetId}는 {built.GetType().Name}인데 {typeof(T).Name}가 필요하다"));
            return null;
        }
    }
}
