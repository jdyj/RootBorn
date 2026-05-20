using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanRegistryTests
    {
        [Test]
        public void GameDataRegistry_ExposesDailyPlanRouteSets()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.DailyPlanRouteSets, Is.Not.Null);
            Assert.That(registry.DailyPlanRouteSets.Length, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void DefaultDailyPlanRouteSet_HasThreeUsableRoutes()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.DailyPlanRouteSets, Is.Not.Null);
            Assert.That(registry.DailyPlanRouteSets.Length, Is.GreaterThanOrEqualTo(1));

            var set = registry.DailyPlanRouteSets[0];
            Assert.That(set, Is.Not.Null);
            Assert.That(set.Id, Is.Not.Empty);
            Assert.That(set.Routes.Count, Is.GreaterThanOrEqualTo(3));

            foreach (var route in set.Routes)
            {
                Assert.That(route, Is.Not.Null);
                Assert.That(route.Id, Is.Not.Empty);
                Assert.That(route.DisplayNameKey, Is.Not.Empty);
                Assert.That(route.ObjectiveTitleKey, Is.Not.Empty);
                Assert.That(route.ObjectiveDetailKey, Is.Not.Empty);
                Assert.That(route.NextHintKey, Is.Not.Empty);
                Assert.That(route.TargetLocation != null || route.MatchingActivities.Count > 0, Is.True);
            }
        }
    }
}
