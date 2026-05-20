using System.IO;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanProgressComponentTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "rootborn-daily-plan-component-tests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
            SaveService.SetRootDirectoryForTests(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            SaveService.SetRootDirectoryForTests(null);
            if (!string.IsNullOrEmpty(_tempRoot) && Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }

        [Test]
        public void Load_CreatesProgressForSaveSlotPlayerAndDay()
        {
            var go = new GameObject("DailyPlanProgress");
            var component = go.AddComponent<DailyPlanProgressComponent>();

            component.Load("slot-a", "player-a", 3);

            Assert.That(component.Progress, Is.Not.Null);
            Assert.That(component.Progress.SaveSlot, Is.EqualTo("slot-a"));
            Assert.That(component.Progress.PlayerId, Is.EqualTo("player-a"));
            Assert.That(component.Progress.CurrentDay, Is.EqualTo(3));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TrySelect_SavesProgressAndRaisesChanged()
        {
            var go = new GameObject("DailyPlanProgress");
            var component = go.AddComponent<DailyPlanProgressComponent>();
            var route = CreateRoute("daily.library.self-study", "daily.route.library");
            var student = new StudentLifeProgress("slot-a", "player-a", 100, 100);
            var changedCount = 0;
            component.Changed += () => changedCount++;

            component.Load("slot-a", "player-a", 1);
            var selected = component.TrySelect(route, student);
            var restored = DailyPlanProgressPersistence.LoadOrCreate("slot-a", "player-a", 1);

            Assert.That(selected, Is.True);
            Assert.That(changedCount, Is.EqualTo(2));
            Assert.That(restored.SelectedRouteId, Is.EqualTo("daily.library.self-study"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryRecordActivity_SavesCompletionAndRaisesChangedOnce()
        {
            var go = new GameObject("DailyPlanProgress");
            var component = go.AddComponent<DailyPlanProgressComponent>();
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("library", "location.library", Vector2.zero, System.Array.Empty<DiscoveryDefinition>());
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests(
                "activity.library.read",
                "activity.library.read",
                location,
                LocationGrowthRoute.SelfStudy,
                60,
                0,
                0,
                0,
                System.Array.Empty<LocationActivityRequirementBase>(),
                System.Array.Empty<LocationActivityOutcomeBase>());
            var route = CreateRoute("daily.library.self-study", "daily.route.library", location, LocationGrowthRoute.SelfStudy);
            var routeSet = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            routeSet.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var student = new StudentLifeProgress("slot-a", "player-a", 100, 100);
            var changedCount = 0;
            component.Changed += () => changedCount++;

            component.Load("slot-a", "player-a", 1);
            component.TrySelect(route, student);
            changedCount = 0;
            var completed = component.TryRecordActivity(new[] { routeSet }, activity, student, "request-1", out var result);
            var restored = DailyPlanProgressPersistence.LoadOrCreate("slot-a", "player-a", 1);

            Assert.That(completed, Is.True);
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(restored.CompletedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(restored.MatchedActivityId, Is.EqualTo("activity.library.read"));
            Object.DestroyImmediate(go);
        }

        private static DailyPlanRouteDefinition CreateRoute(
            string id,
            string displayNameKey,
            LocationDefinition location = null,
            LocationGrowthRoute growthRoute = LocationGrowthRoute.SelfStudy)
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                id,
                displayNameKey,
                location,
                growthRoute,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective." + id + ".title",
                "objective." + id + ".detail",
                "objective." + id + ".next");
            return route;
        }
    }
}
