using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Rootborn.Screens
{
    /// <summary>
    /// 현지화 문구. data/localization의 언어별 JSON(키 → 문구, 예: ko.json, en.json)을 읽어 키로 문구를 준다.
    /// 키가 지금 언어에 없으면 키를 그대로 돌려주고 MissingKeys에 모은다(Unity 판처럼 코드에 적은 한글로 대신하지 않는다).
    /// 화면 테스트는 MissingKeys가 비었는지 본다.
    /// </summary>
    public sealed class GameText
    {
        private readonly Dictionary<string, Dictionary<string, string>> _tables;
        private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _missingOrder = new List<string>();
        private Dictionary<string, string> _current;
        private string _language;

        /// <summary>언어가 바뀌었다. 열린 화면은 글자를 다시 받는다.</summary>
        public event Action LanguageChanged;

        public GameText(IDictionary<string, Dictionary<string, string>> tables, string language)
        {
            if (tables == null || tables.Count == 0) throw new ArgumentException("언어 표가 없다", nameof(tables));
            _tables = new Dictionary<string, Dictionary<string, string>>(tables, StringComparer.OrdinalIgnoreCase);
            SetLanguage(language, raise: false);
        }

        /// <summary>폴더의 *.json을 언어 표로 읽는다(파일 이름이 언어 코드).</summary>
        public static GameText Load(string localizationDirectory, string language)
        {
            if (!Directory.Exists(localizationDirectory)) throw new DirectoryNotFoundException("현지화 폴더가 없다: " + localizationDirectory);
            var tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(localizationDirectory, "*.json"))
            {
                Dictionary<string, string> table = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("현지화 파일이 비었다: " + file);
                tables[Path.GetFileNameWithoutExtension(file)] = table;
            }
            return new GameText(tables, language);
        }

        public IReadOnlyCollection<string> Languages => _tables.Keys;

        public string Language
        {
            get => _language;
            set => SetLanguage(value, raise: true);
        }

        private void SetLanguage(string language, bool raise)
        {
            if (string.IsNullOrEmpty(language) || !_tables.TryGetValue(language, out Dictionary<string, string> table))
                throw new ArgumentException("모르는 언어: " + language);
            bool changed = !string.Equals(_language, language, StringComparison.OrdinalIgnoreCase);
            _language = language.ToLowerInvariant();
            _current = table;
            if (changed && raise) LanguageChanged?.Invoke();
        }

        /// <summary>지금까지 지금 언어에서 찾지 못한 키(처음 찾은 순서).</summary>
        public IReadOnlyList<string> MissingKeys => _missingOrder;

        public bool Has(string key, string language) =>
            key != null && _tables.TryGetValue(language, out Dictionary<string, string> table) && table.ContainsKey(key);

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_current.TryGetValue(key, out string value)) return value;
            if (_missing.Add(_language + ":" + key)) _missingOrder.Add(_language + ":" + key);
            return key;
        }

        public string Format(string key, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, Get(key), args ?? Array.Empty<object>());
    }
}
