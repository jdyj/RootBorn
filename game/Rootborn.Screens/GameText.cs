using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Rootborn.Screens
{
    /// <summary>
    /// 현지화 문구. data/localization의 언어별 JSON(키 → 문구, 예: ko.json, en.json)을 읽어 키로 문구를 준다.
    /// 키가 지금 언어에 없으면 기준 언어(BaseLanguage, 보통 ko)의 문구로 대신하고, 기준 언어에도 없으면 키를 그대로 돌려준다.
    /// 어느 쪽이든 찾지 못한 "언어:키"를 MissingKeys에 모은다(Unity 판처럼 코드에 적은 한글로 대신하지 않는다).
    /// 화면 테스트는 MissingKeys가 비었는지 본다.
    /// </summary>
    public sealed class GameText
    {
        /// <summary>기준 언어 코드. 다른 언어에 없는 문구는 이 언어로 대신한다. 이 상수 밖에 "ko"를 적지 않는다.</summary>
        public const string DefaultBaseLanguage = "ko";

        private readonly Dictionary<string, Dictionary<string, string>> _tables;
        private readonly Dictionary<string, string> _base;
        private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _missingOrder = new List<string>();
        private Dictionary<string, string> _current;
        private string _language;

        /// <summary>언어가 바뀌었다. 열린 화면은 글자를 다시 받는다.</summary>
        public event Action LanguageChanged;

        /// <param name="baseLanguage">기준 언어. 그 언어의 표가 있어야 한다.</param>
        public GameText(IDictionary<string, Dictionary<string, string>> tables, string language, string baseLanguage = DefaultBaseLanguage)
        {
            if (tables == null || tables.Count == 0) throw new ArgumentException("언어 표가 없다", nameof(tables));
            _tables = new Dictionary<string, Dictionary<string, string>>(tables, StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(baseLanguage) || !_tables.TryGetValue(baseLanguage, out _base))
                throw new ArgumentException("기준 언어 표가 없다: " + baseLanguage, nameof(baseLanguage));
            BaseLanguage = baseLanguage.ToLowerInvariant();
            SetLanguage(language, raise: false);
        }

        /// <summary>기준 언어 코드(소문자).</summary>
        public string BaseLanguage { get; }

        /// <summary>폴더의 *.json을 언어 표로 읽는다(파일 이름이 언어 코드).</summary>
        public static GameText Load(string localizationDirectory, string language) => LoadMerged(language, localizationDirectory);

        /// <summary>
        /// 여러 폴더를 차례로 읽어 합친다. 같은 키는 뒤 폴더의 문구가 이긴다.
        /// 첫 폴더는 변환기가 다시 쓰는 data/localization이고, 뒤 폴더(game/localization)는 MonoGame 판에서 더하거나 고친 문구다.
        /// 첫 폴더는 있어야 하고, 뒤 폴더는 없으면 건너뛴다.
        /// </summary>
        public static GameText LoadMerged(string language, params string[] localizationDirectories)
            => LoadMergedWithBase(language, DefaultBaseLanguage, localizationDirectories);

        /// <summary>LoadMerged와 같고 기준 언어를 정해 준다.</summary>
        public static GameText LoadMergedWithBase(string language, string baseLanguage, params string[] localizationDirectories)
        {
            if (localizationDirectories == null || localizationDirectories.Length == 0) throw new ArgumentException("현지화 폴더가 없다", nameof(localizationDirectories));
            var tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < localizationDirectories.Length; i++)
            {
                string directory = localizationDirectories[i];
                if (!Directory.Exists(directory))
                {
                    if (i == 0) throw new DirectoryNotFoundException("현지화 폴더가 없다: " + directory);
                    continue;
                }
                foreach (string file in Directory.GetFiles(directory, "*.json"))
                {
                    Dictionary<string, string> table = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))
                        ?? throw new InvalidDataException("현지화 파일이 비었다: " + file);
                    string code = Path.GetFileNameWithoutExtension(file);
                    if (!tables.TryGetValue(code, out Dictionary<string, string> merged)) tables[code] = merged = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, string> pair in table) merged[pair.Key] = pair.Value;
                }
            }
            return new GameText(tables, language, baseLanguage);
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

        /// <summary>지금까지 찾지 못한 "언어:키"(처음 찾은 순서). 지금 언어에 없어 기준 언어로 대신한 것도 들어간다.</summary>
        public IReadOnlyList<string> MissingKeys => _missingOrder;

        public bool Has(string key, string language) =>
            key != null && _tables.TryGetValue(language, out Dictionary<string, string> table) && table.ContainsKey(key);

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_current.TryGetValue(key, out string value)) return value;
            RecordMissing(_language, key);
            if (!ReferenceEquals(_current, _base))
            {
                if (_base.TryGetValue(key, out value)) return value;
                RecordMissing(BaseLanguage, key);
            }
            return key;
        }

        private void RecordMissing(string language, string key)
        {
            string entry = language + ":" + key;
            if (_missing.Add(entry)) _missingOrder.Add(entry);
        }

        public string Format(string key, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, Get(key), args ?? Array.Empty<object>());
    }
}
