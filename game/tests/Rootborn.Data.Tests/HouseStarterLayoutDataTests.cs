using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 새 게임의 처음 배치 가구(data/building/HouseStarterLayout.json): 실제 데이터가 읽히고, 잘못된 것은 읽기 오류다(자동 테스트).
    /// 목록과 자리는 시험용(결정 대기 29번)이라 값이 아니라 모양과 연결을 본다. 실제 새 집에 맞는지는 Game HouseStarterTests.StarterFitsTheRealHouse.
    /// </summary>
    public sealed class HouseStarterLayoutDataTests
    {
        private static string Starter(string id, string surface, string furniture = "f.desk", string direction = "0") =>
            @"{ ""type"": ""HouseStarterLayout"", ""id"": """ + id + @""", ""surfaceId"": """ + surface + @""",
                ""placements"": [ { ""furniture"": """ + furniture + @""", ""cell"": { ""x"": 2, ""y"": 3 }, ""direction"": " + direction + @" } ] }";

        private static LoadResult LoadStarters(params string[] starters)
        {
            using var dir = new TempDir();
            dir.Write("data/a/FurnitureDefinition.json", @"[{ ""type"": ""FurnitureDefinition"", ""id"": ""f.desk"", ""stableId"": ""f.desk"", ""displayName"": ""d"",
                ""category"": ""Desk"", ""allowedSurfaceIds"": [""house""], ""tileParts"": [], ""footprintCells"": [{ ""x"": 0, ""y"": 0 }],
                ""blocksMovement"": 1, ""placementPreference"": 0, ""unlockToken"": """" }]");
            dir.Write("data/b/HouseStarterLayout.json", "[" + string.Join(",", starters) + "]");
            return GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
        }

        [Test]
        public void RealData_LoadsTheStarter_WithKnownFurniture()
        {
            var result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            var starters = result.Definitions.All<HouseStarterLayout>();
            Assert.That(starters.Count, Is.EqualTo(1));
            Assert.That(starters[0].SurfaceId, Is.EqualTo("house"));
            Assert.That(starters[0].Placements.Count, Is.GreaterThan(0));
            foreach (var p in starters[0].Placements)
                Assert.That(result.Definitions.All<FurnitureDefinition>().Any(f => f.StableId == p.FurnitureId), Is.True, "모르는 가구 " + p.FurnitureId);
        }

        [Test]
        public void Builds_PlacementsFromTheFile()
        {
            var result = LoadStarters(Starter("s.a", "house", direction: "1"));
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            var p = result.Definitions.Get<HouseStarterLayout>("s.a").Placements.Single();
            Assert.That((p.FurnitureId, p.Anchor.X, p.Anchor.Y, p.Direction), Is.EqualTo(("f.desk", 2, 3, FurniturePlacementDirection.East)));
        }

        [Test]
        public void TwoStartersForOneSurface_IsALoadError()
        {
            var result = LoadStarters(Starter("s.a", "house"), Starter("s.b", "house"));
            Assert.That(result.Errors.Any(e => e.ToString().Contains("s.b")), Is.True, string.Join("\n", result.Errors));
            Assert.That(LoadStarters(Starter("s.a", "house"), Starter("s.b", "other")).Errors, Is.Empty, "다른 면이면 된다");
        }

        [Test]
        public void UnknownFurnitureOrInvalidLine_IsALoadError()
        {
            Assert.That(LoadStarters(Starter("s.a", "house", furniture: "f.gone")).Errors.Any(e => e.ToString().Contains("f.gone")), Is.True, "모르는 가구");
            Assert.That(LoadStarters(Starter("s.a", "")).Errors, Is.Not.Empty, "빈 면");
            Assert.That(LoadStarters(Starter("s.a", "house", furniture: "")).Errors, Is.Not.Empty, "빈 가구 id");
        }
    }
}
