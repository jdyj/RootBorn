using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Rootborn.Game.Localization
{
    [CreateAssetMenu(fileName = "GameTextCatalog", menuName = "Rootborn/Localization/Text Catalog")]
    public sealed class GameTextCatalog : ScriptableObject
    {
        [Serializable] private struct Entry { public string key; public string ko; public string en; }
        [Serializable] private sealed class Document { public Entry[] entries; }
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        private Dictionary<string, Entry> _index;

        public void ConfigureJson(string json)
        {
            var document = JsonUtility.FromJson<Document>(json);
            if (document?.entries == null) throw new ArgumentException("Text catalog needs entries.");
            var index = BuildIndex(document.entries);
            _entries = document.entries;
            _index = index;
        }

        public string Get(string key, string language)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_index == null) _index = BuildIndex(_entries);
            if (!_index.TryGetValue(key, out var entry)) return key;
            if (language == "ko" && !string.IsNullOrEmpty(entry.ko)) return entry.ko;
            return string.IsNullOrEmpty(entry.en) ? key : entry.en;
        }

        public string Format(string key, string language, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key, language), args ?? Array.Empty<object>());
        }

        private void OnEnable() => _index = null;
        private void OnValidate() => _index = null;

        private static Dictionary<string, Entry> BuildIndex(Entry[] entries)
        {
            var index = new Dictionary<string, Entry>(entries.Length, StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.key) || index.ContainsKey(entry.key)) throw new ArgumentException("Empty or duplicate localization key.");
                index.Add(entry.key, entry);
            }
            return index;
        }
    }
}
