using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 화면 등록 표(ScreenCatalog)가 상호작용 종류로 여는 화면마다, 실제 맵 데이터(game/world/interactions.json)에
    /// 그 종류의 지점이 하나 이상 있다. 종류 목록은 손으로 적지 않고 등록 표에서 읽는다. 지점이 걸어서 닿는지는 World 테스트
    /// (WorldEntryPointsTests)가 본다. 화면 근거가 아니다.
    /// </summary>
    public sealed class WorldEntryPointKindsTests
    {
        /// <summary>
        /// 지점이 아니라 다른 길로 생기는 종류: "npc"는 interactions.json의 npcAnchors(NPC 자리)에서 저절로 생긴다.
        /// </summary>
        private static readonly Dictionary<string, string> KindsWithoutThings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["npc"] = "npcAnchors",
        };

        /// <summary>
        /// 등록 표의 상호작용 종류 → 화면 id. ScreenRegistry는 종류 목록을 공개하지 않아(TryGetByInteraction만 있다) 리플렉션으로 읽는다.
        /// 등록 표에 종류 목록 조회가 생기면 그것으로 바꾼다.
        /// </summary>
        private static IReadOnlyDictionary<string, string> RegisteredInteractionKinds()
        {
            ScreenRegistry registry = ScreenCatalog.Create();
            FieldInfo field = typeof(ScreenRegistry).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .SingleOrDefault(f => f.FieldType == typeof(Dictionary<string, string>) && f.Name.IndexOf("interaction", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(field, Is.Not.Null, "ScreenRegistry의 상호작용 종류 표를 찾지 못했다(이름이 바뀌었으면 이 테스트를 고친다)");
            var kinds = (Dictionary<string, string>)field.GetValue(registry);
            foreach (string kind in kinds.Keys)
                Assert.That(registry.TryGetByInteraction(kind, out string id) && id == kinds[kind], Is.True, "읽은 표가 등록 표 조회와 같아야 한다: " + kind);
            return kinds;
        }

        /// <summary>실제 interactions.json에서 맵마다 지점 종류와 NPC 자리가 있는지 모은다.</summary>
        private static (HashSet<string> ThingKinds, bool HasNpcAnchors) RealPoints()
        {
            string path = Path.Combine(TestData.RepoRoot, "game", "world", "interactions.json");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            bool anchors = false;
            foreach (JsonProperty map in doc.RootElement.GetProperty("maps").EnumerateObject())
            {
                if (map.Value.TryGetProperty("things", out JsonElement things))
                    foreach (JsonElement thing in things.EnumerateArray()) kinds.Add(thing.GetProperty("kind").GetString());
                if (map.Value.TryGetProperty("npcAnchors", out JsonElement npc) && npc.EnumerateObject().Any(a => a.Value.GetArrayLength() > 0)) anchors = true;
            }
            return (kinds, anchors);
        }

        [Test]
        public void EveryInteractionKindThatOpensAScreen_HasAPointInTheRealMaps()
        {
            IReadOnlyDictionary<string, string> registered = RegisteredInteractionKinds();
            Assert.That(registered, Is.Not.Empty);
            var (thingKinds, hasAnchors) = RealPoints();

            var missing = new List<string>();
            foreach (var pair in registered)
            {
                bool present = KindsWithoutThings.ContainsKey(pair.Key) ? hasAnchors : thingKinds.Contains(pair.Key);
                if (!present) missing.Add(pair.Key + " (화면 " + pair.Value + ")");
            }
            Assert.That(missing, Is.Empty, "실제 맵에 지점이 없어 열 수 없는 화면:\n" + string.Join("\n", missing));
            TestContext.WriteLine("등록된 종류: " + string.Join(", ", registered.Select(p => p.Key + "→" + p.Value)));
        }
    }
}
