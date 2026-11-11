using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 실제 두 폴더(data/localization 위에 game/localization)를 합친 뒤 모든 언어가 같은 키를 가진다.
    /// 언어 목록은 폴더의 파일에서 읽는다 — 언어 파일을 더하면 그 언어도 본다. 자동 테스트다.
    /// </summary>
    public sealed class LocalizationCoverageTests
    {
        /// <summary>어느 언어에 없어도 되는 키(문구가 아직 정해지지 않은 것). 지금은 없다. 넣을 때는 이유를 적는다.</summary>
        private static readonly (string Language, string Key, string Reason)[] AllowedGaps = System.Array.Empty<(string, string, string)>();

        private static Dictionary<string, Dictionary<string, string>> MergedTables()
        {
            var tables = new Dictionary<string, Dictionary<string, string>>();
            foreach (string directory in new[] { TestData.LocalizationDirectory, TestData.ExtraLocalizationDirectory })
            {
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.GetFiles(directory, "*.json"))
                {
                    string code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (!tables.TryGetValue(code, out var merged)) tables[code] = merged = new Dictionary<string, string>();
                    foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))) merged[pair.Key] = pair.Value;
                }
            }
            return tables;
        }

        [Test]
        public void EveryLanguage_HasEveryKeyThatAnyLanguageHas()
        {
            var tables = MergedTables();
            Assert.That(tables.Keys, Does.Contain(GameText.DefaultBaseLanguage), "기준 언어 파일이 있어야 한다");
            var allKeys = new HashSet<string>(tables.Values.SelectMany(t => t.Keys));
            var gaps = new List<string>();
            foreach (var pair in tables)
                foreach (string key in allKeys)
                    if (!pair.Value.ContainsKey(key) && !AllowedGaps.Any(g => g.Language == pair.Key && g.Key == key))
                        gaps.Add(pair.Key + ":" + key);
            Assert.That(gaps, Is.Empty, "언어끼리 어긋난 키");
        }

        [Test]
        public void NoLanguage_HasAnEmptyText()
        {
            var empty = MergedTables().SelectMany(t => t.Value.Where(p => string.IsNullOrEmpty(p.Value)).Select(p => t.Key + ":" + p.Key)).ToArray();
            Assert.That(empty, Is.Empty, "빈 문구");
        }

        [Test]
        public void GameText_SeesTheSameLanguagesAsTheFolders()
        {
            Assert.That(TestData.Text().Languages.Select(l => l.ToLowerInvariant()).OrderBy(l => l), Is.EqualTo(MergedTables().Keys.OrderBy(l => l)));
        }
    }
}
