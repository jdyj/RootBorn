using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using Rootborn.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 정의 로더가 만든 장면 목록·낮밤 조명이 Rootborn.World가 JSON을 직접 읽은 것과 같다(자동 테스트).
    /// Rootborn.World가 로더를 쓰도록 바꿀 때 이 테스트가 같은 값임을 보장한다.
    /// </summary>
    public sealed class LoaderMatchesWorldReadersTests
    {
        [Test]
        public void TheTravelCatalog_IsTheSame()
        {
            var fromWorld = WorldTravelData.Load(TestWorld.RepoRoot);
            var fromLoader = TestWorld.Definitions.All<WorldTravelCatalog>().Single();

            Assert.That((fromLoader.NewGameScene, fromLoader.NewGameSpawnId), Is.EqualTo((fromWorld.NewGameScene, fromWorld.NewGameSpawnId)));
            Assert.That(fromLoader.Destinations.Select(d => d.SceneName + ":" + string.Join(",", d.SpawnIds)),
                Is.EqualTo(fromWorld.Destinations.Select(d => d.SceneName + ":" + string.Join(",", d.SpawnIds))));
        }

        [Test]
        public void TheDayNightLighting_HasTheSameKeys()
        {
            var fromWorld = DayNightLighting.Load(TestWorld.RepoRoot);
            var fromLoader = TestWorld.Definitions.All<DayNightLightingProfile>().Single();

            Assert.That(fromLoader.LerpSpeed, Is.EqualTo(fromWorld.LerpSpeed));
            foreach (var key in fromLoader.ColorKeys)
            {
                var color = fromWorld.EvaluateColor(key.DayFraction * 24f);
                Assert.That((color.R, color.G, color.B), Is.EqualTo((key.R, key.G, key.B)).Within(1e-5f), "색 키 " + key.DayFraction);
            }

            foreach (var key in fromLoader.IntensityKeys)
                Assert.That(fromWorld.EvaluateIntensity(key.Hour), Is.EqualTo(System.Math.Max(0f, key.Value)).Within(1e-5f), "세기 키 " + key.Hour);
        }
    }
}
