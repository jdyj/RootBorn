using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Rootborn.Data
{
    /// <summary>
    /// 만들기 함수가 엔티티 JSON 필드를 읽는 도우미.
    /// 없는 필드는 missing-field 오류를 남기고 기본값을 준다. null 값은 오류 없이 기본값(참조는 빈 문자열)이다.
    /// 형이 다른 값은 wrong-type 오류와 기본값이다.
    /// </summary>
    public readonly struct NodeReader
    {
        private readonly JsonElement _json;
        private readonly List<LoadError> _errors;
        private readonly HashSet<string> _optional;

        internal NodeReader(string type, string id, string source, JsonElement json, List<LoadError> errors)
            : this(type, id, source, json, errors, null)
        {
        }

        private NodeReader(string type, string id, string source, JsonElement json, List<LoadError> errors, HashSet<string> optional)
        {
            Type = type ?? "";
            Id = id ?? "";
            Source = source ?? "";
            _json = json;
            _errors = errors;
            _optional = optional;
        }

        /// <summary>
        /// 이 필드들은 파일에 없어도 오류가 아니다. Unity가 에셋을 저장한 뒤에 클래스에 더해진 필드라 옛 에셋에 없고,
        /// Unity는 그 필드의 초기값을 썼다. 초기값이 형의 기본값(false, 0, "", 빈 배열, null 참조)인 필드에만 쓴다.
        /// </summary>
        public NodeReader Optional(params string[] fields)
        {
            var optional = _optional == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(_optional, StringComparer.Ordinal);
            foreach (var field in fields) optional.Add(field);
            return new NodeReader(Type, Id, Source, _json, _errors, optional);
        }

        /// <summary>엔티티 id. 자리에서 만든 전략이나 엔티티 안의 객체는 감싼 엔티티의 id.</summary>
        public string Id { get; }

        public string Type { get; }

        /// <summary>오류에 붙는 위치(감싼 엔티티의 asset 또는 파일 경로).</summary>
        public string Source { get; }

        public JsonElement Json => _json;

        /// <summary>Unity 에셋 이름(asset 경로의 파일 이름, 확장자 없음). asset이 없으면 id.</summary>
        public string AssetName
        {
            get
            {
                if (Source.EndsWith(".asset", StringComparison.Ordinal))
                {
                    var slash = Source.LastIndexOf('/');
                    return Source.Substring(slash + 1, Source.Length - slash - 1 - ".asset".Length);
                }

                return Id;
            }
        }

        public bool Has(string field)
        {
            return _json.ValueKind == JsonValueKind.Object && _json.TryGetProperty(field, out _);
        }

        public string Str(string field)
        {
            if (!TryField(field, out var value)) return "";
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            return WrongType(field, "문자열", value, "");
        }

        public int Int(string field)
        {
            if (!TryField(field, out var value)) return 0;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            return WrongType(field, "정수", value, 0);
        }

        public float Float(string field)
        {
            if (!TryField(field, out var value)) return 0f;
            if (value.ValueKind == JsonValueKind.Number) return value.GetSingle();
            return WrongType(field, "실수", value, 0f);
        }

        public bool Bool(string field)
        {
            if (!TryField(field, out var value)) return false;
            switch (value.ValueKind)
            {
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Number when value.TryGetInt32(out var number) && (number == 0 || number == 1): return number == 1;
                default: return WrongType(field, "참거짓", value, false);
            }
        }

        /// <summary>{ "ref": id, "type": ... } 의 id. null이나 빈 참조는 빈 문자열.</summary>
        public string RefId(string field)
        {
            if (!TryField(field, out var value)) return "";
            return RefIdOf(field, value);
        }

        /// <summary>참조 배열의 id들. null 원소는 건너뛴다. 필드가 null이면 빈 배열.</summary>
        public string[] RefIds(string field)
        {
            if (!TryField(field, out var value)) return Array.Empty<string>();
            if (value.ValueKind != JsonValueKind.Array) return WrongType(field, "참조 배열", value, Array.Empty<string>());

            var ids = new List<string>(value.GetArrayLength());
            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Null) continue;
                var id = RefIdOf(field, element);
                if (id.Length > 0) ids.Add(id);
            }

            return ids.ToArray();
        }

        /// <summary>숫자로 내보낸 열거형을 TEnum으로. 정의되지 않은 숫자는 enum-range 오류와 기본값.</summary>
        public TEnum Enum<TEnum>(string field) where TEnum : struct, Enum
        {
            if (!TryField(field, out var value)) return default;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
                return WrongType(field, "열거형 숫자", value, default(TEnum));

            var underlying = System.Enum.GetUnderlyingType(typeof(TEnum));
            var boxed = System.Enum.ToObject(typeof(TEnum), Convert.ChangeType(number, underlying));
            if (!System.Enum.IsDefined(typeof(TEnum), boxed))
            {
                Report("enum-range", $"필드 '{field}'의 값 {number}는 {typeof(TEnum).Name}에 없다");
                return default;
            }

            return (TEnum)boxed;
        }

        /// <summary>
        /// Unity가 int 배열을 묶어 쓴 16진 문자열(원소마다 8글자, 리틀엔디언)이나 숫자 배열을 읽는다.
        /// </summary>
        public int[] IntArray(string field)
        {
            if (!TryField(field, out var value)) return Array.Empty<int>();
            switch (value.ValueKind)
            {
                case JsonValueKind.Array:
                {
                    var numbers = new List<int>(value.GetArrayLength());
                    foreach (var element in value.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)) numbers.Add(number);
                        else WrongType(field, "정수 원소", element, 0);
                    }

                    return numbers.ToArray();
                }
                case JsonValueKind.String:
                    return HexInts(field, value.GetString());
                default:
                    return WrongType(field, "정수 배열", value, Array.Empty<int>());
            }
        }

        public float[] FloatArray(string field)
        {
            if (!TryField(field, out var value)) return Array.Empty<float>();
            if (value.ValueKind != JsonValueKind.Array) return WrongType(field, "실수 배열", value, Array.Empty<float>());

            var numbers = new List<float>(value.GetArrayLength());
            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Number) numbers.Add(element.GetSingle());
                else WrongType(field, "실수 원소", element, 0);
            }

            return numbers.ToArray();
        }

        public string[] Strings(string field)
        {
            if (!TryField(field, out var value)) return Array.Empty<string>();
            if (value.ValueKind != JsonValueKind.Array) return WrongType(field, "문자열 배열", value, Array.Empty<string>());

            var strings = new List<string>(value.GetArrayLength());
            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String) strings.Add(element.GetString());
                else if (element.ValueKind != JsonValueKind.Null) WrongType(field, "문자열 원소", element, 0);
            }

            return strings.ToArray();
        }

        /// <summary>배열 안의 객체(엔티티 안에 바로 적힌 구조체)를 하나씩 읽는다. null 필드는 빈 목록.</summary>
        public IEnumerable<NodeReader> Items(string field)
        {
            if (!TryField(field, out var value)) return Array.Empty<NodeReader>();
            if (value.ValueKind != JsonValueKind.Array) return WrongType(field, "배열", value, Array.Empty<NodeReader>());

            var items = new List<NodeReader>(value.GetArrayLength());
            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object) items.Add(new NodeReader(Type, Id, Source, element, _errors));
                else if (element.ValueKind != JsonValueKind.Null) WrongType(field, "객체 원소", element, 0);
            }

            return items;
        }

        /// <summary>엔티티 안에 바로 적힌 객체 하나. 없거나 null이면 false.</summary>
        public bool TryChild(string field, out NodeReader child)
        {
            child = default;
            if (!TryField(field, out var value)) return false;
            if (value.ValueKind != JsonValueKind.Object)
            {
                WrongType(field, "객체", value, 0);
                return false;
            }

            child = new NodeReader(Type, Id, Source, value, _errors);
            return true;
        }

        /// <summary>필드 값을 꺼낸다. 없으면 missing-field 오류, null이면 오류 없이 false.</summary>
        internal bool TryField(string field, out JsonElement value)
        {
            if (_json.ValueKind != JsonValueKind.Object || !_json.TryGetProperty(field, out value))
            {
                value = default;
                if (_optional == null || !_optional.Contains(field)) Report("missing-field", $"필드 '{field}'가 없다");
                return false;
            }

            return value.ValueKind != JsonValueKind.Null;
        }

        internal void Report(string kind, string message)
        {
            _errors?.Add(new LoadError(kind, Type, Id, Source, message));
        }

        internal T WrongType<T>(string field, string expected, JsonElement value, T fallback)
        {
            Report("wrong-type", $"필드 '{field}'는 {expected}여야 하는데 {value.ValueKind}다");
            return fallback;
        }

        private int[] HexInts(string field, string hex)
        {
            if (hex.Length == 0) return Array.Empty<int>();
            if (hex.Length % 8 != 0)
            {
                Report("wrong-type", $"필드 '{field}'의 16진 int 배열 길이 {hex.Length}가 8의 배수가 아니다");
                return Array.Empty<int>();
            }

            var numbers = new int[hex.Length / 8];
            for (var i = 0; i < numbers.Length; i++)
            {
                uint number = 0;
                for (var b = 0; b < 4; b++)
                {
                    var at = i * 8 + b * 2;
                    if (!IsHex(hex[at]) || !IsHex(hex[at + 1]))
                    {
                        Report("wrong-type", $"필드 '{field}'의 값 '{hex}'는 16진 int 배열이 아니다");
                        return Array.Empty<int>();
                    }

                    number |= (uint)Convert.ToByte(hex.Substring(at, 2), 16) << (8 * b);
                }

                numbers[i] = unchecked((int)number);
            }

            return numbers;
        }

        private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private string RefIdOf(string field, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("ref", out var reference))
            {
                if (reference.ValueKind == JsonValueKind.String) return reference.GetString();
                if (reference.ValueKind == JsonValueKind.Null) return "";
            }

            return WrongType(field, "참조 { ref, type }", value, "");
        }
    }
}
