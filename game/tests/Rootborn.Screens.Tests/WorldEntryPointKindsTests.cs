using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>등록 표의 상호작용 종류 → 화면 id(ScreenRegistry.InteractionKinds). 조회와 같은지도 본다.</summary>
        private static IReadOnlyDictionary<string, string> RegisteredInteractionKinds()
        {
            ScreenRegistry registry = ScreenCatalog.Create();
            IReadOnlyDictionary<string, string> kinds = registry.InteractionKinds;
            foreach (var pair in kinds)
                Assert.That(registry.TryGetByInteraction(pair.Key, out string id) && id == pair.Value, Is.True, "종류 목록이 등록 표 조회와 같아야 한다: " + pair.Key);
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
