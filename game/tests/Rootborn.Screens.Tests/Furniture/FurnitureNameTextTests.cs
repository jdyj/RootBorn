using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Data;
using Rootborn.Data.Builders;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// 모든 가구의 화면 이름(자동 테스트). 가구 배치 화면은 이름 키(displayNameKey)가 있으면 그 문구를, 없으면 id로 만든 규칙 키
    /// (DataTextKeys.Name(stableId))의 문구를 보인다. 둘 다 없으면 그림 시트 이름(영어 낱말과 번호)이 그대로 보이므로,
    /// 문구가 모든 언어에 있고, 시트 이름 모양이 아니고, 한 언어 안에서 겹치지 않는지 본다. 언어 목록은 폴더에서 읽는다.
    /// 임시 이름이다(docs/superpowers/specs/2026-11-13-placeholder-text-open-decisions.md).
    /// </summary>
    public sealed class FurnitureNameTextTests
    {
        private static readonly Regex SheetNameShape = new Regex(@"_\d+x\d+|\d+x\d+|Singles|Shadowless|_", RegexOptions.IgnoreCase);

        private static IReadOnlyList<FurnitureDefinition> Furniture()
        {
            LoadResult loaded = GameDataLoader.Load(TestData.DataDirectory, RootbornBuilders.Register);
            loaded.ThrowIfFailed();
            return loaded.Definitions.All<FurnitureDefinition>().ToList();
        }

        [Test]
        public void EveryFurniture_HasANameInEveryLanguage_NotASheetName_AndUniqueWithinALanguage()
        {
            var furniture = Furniture();
            Assert.That(furniture.Count, Is.GreaterThan(1000), "실제 가구 정의");
            string[] languages = TestData.Text().Languages.OrderBy(l => l, StringComparer.Ordinal).ToArray();
            Assert.That(languages.Length, Is.GreaterThanOrEqualTo(2));

            foreach (string language in languages)
            {
                var text = TestData.Text(language);
                var problems = new List<string>();
                var seen = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var f in furniture)
                {
                    string key = !string.IsNullOrEmpty(f.DisplayNameKey) && text.Has(f.DisplayNameKey, language) ? f.DisplayNameKey : DataTextKeys.Name(f.StableId);
                    if (!text.Has(key, language)) { problems.Add($"문구 없음: {f.StableId} ({key})"); continue; }
                    string name = text.Get(key);
                    if (string.IsNullOrWhiteSpace(name)) problems.Add($"빈 문구: {f.StableId}");
                    else if (SheetNameShape.IsMatch(name)) problems.Add($"시트 이름 모양: {f.StableId} = {name}");
                    else if (seen.TryGetValue(name, out string other)) problems.Add($"이름이 겹침: {f.StableId} / {other} = {name}");
                    else seen[name] = f.StableId;
                }

                Assert.That(problems, Is.Empty, language + "\n" + string.Join("\n", problems.Take(20)));
            }
        }
    }
}
