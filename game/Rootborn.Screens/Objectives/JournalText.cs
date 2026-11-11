using System;
using System.Collections.Generic;
using System.Text;

namespace Rootborn.Screens.Objectives
{
    /// <summary>JournalText를 글자로 바꾸는 쪽(화면). 키 → 문구, 자리 표시 키 → 문구, 데이터 값(키이거나 글) → 문구.</summary>
    public interface IJournalTextResolver
    {
        string T(string key);
        string F(string key, object[] args);
        string Shown(string keyOrText);
    }

    /// <summary>
    /// 수첩 항목의 글자 한 칸. 모으는 쪽은 사람이 읽는 문구를 만들지 않고 이것(키와 값)만 넘긴다. 화면이 지금 언어로 바꾼다.
    /// 값이 든 문구는 자리 표시 키 하나(Key(key, 값...))로, 목록은 List(구분 기호도 키)로 둔다. 문장을 이어 붙이지 않는다.
    /// </summary>
    public sealed class JournalText
    {
        private enum Kind { Empty, Key, KeyOr, Data, Raw, List }

        public const string ListSeparatorKey = "ui.journal.list-separator";
        public static readonly JournalText Empty = new JournalText(Kind.Empty, null, null, null);

        private readonly Kind _kind;
        private readonly string _value;
        private readonly object[] _args;
        private readonly JournalText[] _items;
        private string _separatorKey = ListSeparatorKey;

        private JournalText(Kind kind, string value, object[] args, JournalText[] items)
        {
            _kind = kind; _value = value; _args = args; _items = items;
        }

        /// <summary>문구 키. 값(숫자, 글자, 또 다른 JournalText)이 있으면 그 키는 자리 표시 문구다.</summary>
        public static JournalText Key(string key, params object[] args) =>
            string.IsNullOrEmpty(key) ? Empty : new JournalText(Kind.Key, key, args != null && args.Length > 0 ? args : null, null);

        /// <summary>데이터가 넘긴 값: 키면 문구, 아니면 그대로(Unity ObjectiveJournalSourceText.Shown).</summary>
        public static JournalText Data(string keyOrText) => string.IsNullOrEmpty(keyOrText) ? Empty : new JournalText(Kind.Data, keyOrText, null, null);

        /// <summary>문구 키가 있으면 그 문구, 없으면 fallback 그대로(Unity ObjectiveJournalSourceText.Localized). 빠진 키로 세지 않는다.</summary>
        public static JournalText KeyOr(string key, string fallback) => string.IsNullOrEmpty(key) ? Raw(fallback) : new JournalText(Kind.KeyOr, key, new object[] { fallback ?? string.Empty }, null);

        /// <summary>번역하지 않는 값(숫자 등).</summary>
        public static JournalText Raw(string text) => string.IsNullOrEmpty(text) ? Empty : new JournalText(Kind.Raw, text, null, null);

        /// <summary>목록: 빈 것과 같은 글자를 빼고 구분 기호 키(" | ")로 잇는다.</summary>
        public static JournalText List(IEnumerable<JournalText> items) => List(items, ListSeparatorKey);

        /// <summary>목록: 구분 기호 키를 따로 준다(예: 쉼표).</summary>
        public static JournalText List(IEnumerable<JournalText> items, string separatorKey)
        {
            var list = new List<JournalText>();
            if (items != null) foreach (JournalText item in items) if (item != null && !item.IsEmpty) list.Add(item);
            return list.Count == 0 ? Empty : list.Count == 1 ? list[0] : new JournalText(Kind.List, null, null, list.ToArray()) { _separatorKey = separatorKey ?? ListSeparatorKey };
        }

        public bool IsEmpty => _kind == Kind.Empty;

        /// <summary>시험과 비교용: 종류와 키·값(언어와 무관).</summary>
        public override string ToString()
        {
            switch (_kind)
            {
                case Kind.Empty: return string.Empty;
                case Kind.List: return "[" + string.Join(",", Array.ConvertAll(_items, i => i.ToString())) + "]" + _separatorKey;
                case Kind.Key when _args != null: return _value + "(" + string.Join(",", Array.ConvertAll(_args, a => a?.ToString() ?? string.Empty)) + ")";
                default: return _value;
            }
        }

        public override bool Equals(object obj) => obj is JournalText other && other.ToString() == ToString();
        public override int GetHashCode() => ToString().GetHashCode();

        public string Resolve(IJournalTextResolver r)
        {
            switch (_kind)
            {
                case Kind.Key:
                    if (_args == null) return r.T(_value);
                    var args = new object[_args.Length];
                    for (int i = 0; i < args.Length; i++) args[i] = _args[i] is JournalText text ? text.Resolve(r) : _args[i];
                    return r.F(_value, args);
                case Kind.KeyOr:
                    string shown = r.Shown(_value);
                    return shown == _value ? (string)_args[0] : shown;
                case Kind.Data: return r.Shown(_value);
                case Kind.Raw: return _value;
                case Kind.List:
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    var builder = new StringBuilder(64);
                    string separator = r.T(_separatorKey);
                    foreach (JournalText item in _items)
                    {
                        string value = item.Resolve(r);
                        if (string.IsNullOrEmpty(value) || !seen.Add(value)) continue;
                        if (builder.Length > 0) builder.Append(separator);
                        builder.Append(value);
                    }
                    return builder.ToString();
                default: return string.Empty;
            }
        }
    }
}
