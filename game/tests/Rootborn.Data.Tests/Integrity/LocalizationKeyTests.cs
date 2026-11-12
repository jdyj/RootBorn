using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.Data.Tests.Integrity
{
    /// <summary>
    /// data/ 정의가 가리키는 문구 키가 합쳐진 문구 표(data/localization + game/localization, 뒤가 이긴다 — 게임의 GameText.LoadMerged와 같다)의
    /// 모든 언어에 있는지 본다(자동 테스트). 키 필드는 이름이 Key로 끝나는 문자열과 Keys로 끝나는 문자열 배열이다.
    /// iconKey는 문구가 아니라 테마 아이콘 키라 뺀다. 언어 목록은 두 폴더의 파일 이름에서 읽는다(여기에 적지 않는다).
    /// 지금 빠진 키는 Fixtures/known-missing-localization-keys.json에 이유별로 고정한다(문구를 넣지 않는다).
    /// </summary>
    public sealed class LocalizationKeyTests
    {
        private const string KnownMissingFile = "known-missing-localization-keys.json";
        private static readonly string[] NotTextKeys = { "iconKey" };

        [Test]
        public void EveryTextKeyInData_IsInEveryLanguage_ExceptTheKnownMissingKeys()
        {
            var data = DataScan.Real;
            var languages = MergedLanguages(data.Root);
            Assert.That(languages.Count, Is.GreaterThanOrEqualTo(2), "data/localization과 game/localization의 언어 파일");

            var used = UsedKeys(data);
            var missing = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var pair in used)
            {
                var lacking = languages.Where(l => !l.Value.ContainsKey(pair.Key)).Select(l => l.Key).OrderBy(l => l, StringComparer.Ordinal).ToList();
                if (lacking.Count > 0) missing[pair.Key] = lacking;
            }

            var known = KnownMissing();
            var unexpected = missing.Keys.Where(k => !known.ContainsKey(k))
                .Select(k => $"{k} (없는 언어: {string.Join(", ", missing[k])}; 쓰는 곳: {string.Join(", ", used[k].Take(3))})").ToList();
            var gone = known.Keys.Where(k => !missing.ContainsKey(k)).ToList();

            TestContext.WriteLine($"언어 {string.Join(", ", languages.Keys.OrderBy(k => k, StringComparer.Ordinal))}, 정의가 쓰는 키 {used.Count}개, 빠진 키 {missing.Count}개");
            foreach (var group in known.GroupBy(k => k.Value))
                TestContext.WriteLine($"- {group.Count()}개: {group.Key} — 예: {string.Join(", ", group.Take(5).Select(k => k.Key))}");
            Assert.That(unexpected, Is.Empty, "알려진 목록 밖의 빠진 키(문구를 넣거나, 넣을 수 없으면 " + KnownMissingFile + "에 이유와 함께 적는다):\n" + string.Join("\n", unexpected));
            Assert.That(gone, Is.Empty, "이제 모든 언어에 있는 키(" + KnownMissingFile + "에서 지운다):\n" + string.Join("\n", gone));
        }

        [Test]
        public void TheFirstGuidesGreeting_HasATextInEveryLanguage_WithoutAFormOfAddress()
        {
            // 첫 안내인이 누구인지(호칭)는 스토리 미정이라 임시 문구는 호칭("엄마"·"아빠" 등)을 넣지 않는다(2026-11-13).
            const string key = "dialogue.firstNpc.greeting";
            Assert.That(UsedKeys(DataScan.Real).ContainsKey(key), Is.True);
            foreach (var language in MergedLanguages(DataScan.Real.Root))
            {
                Assert.That(language.Value.TryGetValue(key, out var text), Is.True, language.Key);
                Assert.That(text, Is.Not.Empty, language.Key);
                Assert.That(text, Does.Not.Contain("엄마").And.Not.Contain("아빠").And.Not.Contain("부모"), language.Key);
            }
        }

        [Test]
        public void KeyFields_IncludeKeyArraysAndSkipIconKeys()
        {
            using var dir = new TempDir();
            dir.Write("data/registry.json", "{}");
            dir.Write("data/a/Thing.json", @"[{ ""type"": ""Thing"", ""id"": ""thing.1"", ""displayNameKey"": ""thing.name"", ""iconKey"": ""chef"",
                ""lineKeys"": [ ""line.1"", ""line.2"" ], ""part"": { ""descriptionKey"": ""thing.desc"" }, ""emptyKey"": """" }]");

            var used = UsedKeys(DataScan.Of(dir.Path));

            Assert.That(used.Keys, Is.EquivalentTo(new[] { "thing.name", "line.1", "line.2", "thing.desc" }));
            Assert.That(used["line.2"].Single(), Is.EqualTo("Thing/thing.1 lineKeys"));
        }

        /// <summary>키 → 쓰는 곳("종류/id 필드") 목록.</summary>
        private static Dictionary<string, List<string>> UsedKeys(DataScan data)
        {
            var used = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var (owner, _, path, obj) in data.EntityObjects())
            {
                foreach (var property in obj.EnumerateObject())
                {
                    if (Array.IndexOf(NotTextKeys, property.Name) >= 0) continue;
                    var at = owner + " " + (path.Length == 0 ? property.Name : path + "." + property.Name);
                    if (property.Name.EndsWith("Key", StringComparison.Ordinal) && property.Value.ValueKind == JsonValueKind.String)
                        Use(used, property.Value.GetString(), at);
                    else if (property.Name.EndsWith("Keys", StringComparison.Ordinal) && property.Value.ValueKind == JsonValueKind.Array)
                        foreach (var element in property.Value.EnumerateArray())
                            if (element.ValueKind == JsonValueKind.String) Use(used, element.GetString(), at);
                }
            }

            return used;
        }

        private static void Use(Dictionary<string, List<string>> used, string key, string at)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!used.TryGetValue(key, out var places)) used[key] = places = new List<string>();
            places.Add(at);
        }

        /// <summary>언어 코드 → 합친 표. 같은 키는 뒤 폴더(game/localization)가 이긴다.</summary>
        private static Dictionary<string, Dictionary<string, string>> MergedLanguages(string root)
        {
            var languages = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in new[] { Path.Combine(root, "data", "localization"), Path.Combine(root, "game", "localization") })
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.GetFiles(folder, "*.json"))
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    if (!languages.TryGetValue(code, out var table)) languages[code] = table = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var pair in DataScan.ReadJson(file).EnumerateObject()) table[pair.Name] = pair.Value.GetString();
                }
            }

            return languages;
        }

        /// <summary>알려진 빠진 키 → 이유.</summary>
        private static Dictionary<string, string> KnownMissing()
        {
            var file = DataScan.ReadJson(Path.Combine(AppContext.BaseDirectory, "Fixtures", KnownMissingFile));
            var known = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in file.GetProperty("groups").EnumerateArray())
            {
                var reason = group.GetProperty("reason").GetString();
                foreach (var key in group.GetProperty("keys").EnumerateArray())
                    Assert.That(known.TryAdd(key.GetString(), reason), Is.True, "고정 파일에 두 번 적힌 키: " + key.GetString());
            }

            return known;
        }
    }
}
