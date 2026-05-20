using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanRouteRunnerTests
    {
        [Test]
        public void RouteSet_ReturnsAvailableRoutesFromData()
        {
            var location = CreateLocation("library", "location.library");
            var route = CreateRoute(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy);
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var progress = CreateProgress();
            var runner = new DailyPlanRouteRunner();

            var routes = runner.GetAvailableRoutes(new[] { set }, progress);

            Assert.That(routes, Is.EquivalentTo(new[] { route }));
        }

        [Test]
        public void SelectRoute_PersistsSelectedRouteForSameDay()
        {
            var location = CreateLocation("library", "location.library");
            var route = CreateRoute(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy);
            var student = CreateProgress();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            var selected = runner.TrySelectRoute(daily, route, student);

            Assert.That(selected, Is.True);
            Assert.That(daily.SelectedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(daily.CurrentDay, Is.EqualTo(1));
        }

        [Test]
        public void SelectSameRouteTwice_IsNoOp()
        {
            var route = CreateRoute(
                "daily.town.help",
                "daily.route.help",
                null,
                LocationGrowthRoute.SocialHelp);
            var student = CreateProgress();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            Assert.That(runner.TrySelectRoute(daily, route, student), Is.True);
            Assert.That(runner.TrySelectRoute(daily, route, student), Is.False);
            Assert.That(daily.SelectedRouteId, Is.EqualTo("daily.town.help"));
        }

        [Test]
        public void MatchingActivity_CompletesSelectedRoute()
        {
            var location = CreateLocation("library", "location.library");
            var activity = CreateActivity(
                "activity.library.read",
                "activity.library.read",
                location,
                LocationGrowthRoute.SelfStudy);
            var route = CreateRoute(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy);
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var student = CreateProgress();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            runner.TrySelectRoute(daily, route, student);
            var completed = runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out var result);

            Assert.That(completed, Is.True);
            Assert.That(result.Route, Is.SameAs(route));
            Assert.That(result.Activity, Is.SameAs(activity));
            Assert.That(daily.CompletedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(daily.MatchedActivityId, Is.EqualTo("activity.library.read"));
        }

        [Test]
        public void ExplicitMatchingActivity_CompletesRouteEvenWhenLocationRoutePairDiffers()
        {
            var library = CreateLocation("library", "location.library");
            var workshop = CreateLocation("workshop", "location.workshop");
            var activity = CreateActivity(
                "activity.workshop.shift",
                "activity.workshop.shift",
                workshop,
                LocationGrowthRoute.Work);
            var route = CreateRoute(
                "daily.library.special",
                "daily.route.library.special",
                library,
                LocationGrowthRoute.SelfStudy,
                new[] { activity });
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var student = CreateProgress();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            runner.TrySelectRoute(daily, route, student);
            var completed = runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out var result);

            Assert.That(completed, Is.True);
            Assert.That(result.Route, Is.SameAs(route));
            Assert.That(result.Activity, Is.SameAs(activity));
        }

        [Test]
        public void NonMatchingActivity_DoesNotCompleteSelectedRoute()
        {
            var library = CreateLocation("library", "location.library");
            var workshop = CreateLocation("workshop", "location.workshop");
            var activity = CreateActivity(
                "activity.workshop.shift",
                "activity.workshop.shift",
                workshop,
                LocationGrowthRoute.Work);
            var route = CreateRoute(
                "daily.library.self-study",
                "daily.route.library",
                library,
                LocationGrowthRoute.SelfStudy);
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var student = CreateProgress();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            runner.TrySelectRoute(daily, route, student);
            var completed = runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out var result);

            Assert.That(completed, Is.False);
            Assert.That(result.IsCompleted, Is.False);
            Assert.That(daily.CompletedRouteId, Is.Empty);
        }

        [Test]
        public void CompletionWithSameRequestKey_DoesNotDuplicate()
        {
            var location = CreateLocation("library", "location.library");
            var activity = CreateActivity(
                "activity.library.read",
                "activity.library.read",
                location,
                LocationGrowthRoute.SelfStudy);
            var route = CreateRoute(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy);
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var student = CreateProgress();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            runner.TrySelectRoute(daily, route, student);
            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out _), Is.True);
            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out _), Is.False);

            Assert.That(daily.AppliedCompletionKeys, Is.EquivalentTo(new[] { "request-1" }));
        }

        [Test]
        public void SaveDataRoundTrip_PreservesCompletionWithoutReapply()
        {
            var progress = new DailyPlanProgress("slot-a", "player-a", 2);
            var route = CreateRoute("daily.library.self-study", "daily.route.library", null, LocationGrowthRoute.SelfStudy);
            var activity = CreateActivity("activity.library.read", "activity.library.read", null, LocationGrowthRoute.SelfStudy);

            progress.SelectRoute(route);
            progress.CompleteRoute(route, activity, "request-1");
            var restored = DailyPlanProgress.FromSaveData(progress.ToSaveData(), "slot-a", "player-a", 2);

            Assert.That(restored.SelectedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(restored.CompletedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(restored.MatchedActivityId, Is.EqualTo("activity.library.read"));
            Assert.That(restored.AppliedCompletionKeys, Is.EquivalentTo(new[] { "request-1" }));
            Assert.That(restored.CompleteRoute(route, activity, "request-1"), Is.False);
        }

        [Test]
        public void MissingSavedRoute_DoesNotCrashAndDoesNotCompleteActivity()
        {
            var daily = DailyPlanProgress.FromSaveData(
                new DailyPlanProgressSaveData
                {
                    SaveSlot = "slot-a",
                    PlayerId = "player-a",
                    CurrentDay = 1,
                    SelectedRouteId = "daily.missing"
                },
                "slot-a",
                "player-a",
                1);
            var activity = CreateActivity("activity.library.read", "activity.library.read", null, LocationGrowthRoute.SelfStudy);
            var runner = new DailyPlanRouteRunner();

            var completed = runner.TryRecordActivity(daily, System.Array.Empty<DailyPlanRouteSetDefinition>(), activity, CreateProgress(), "request-1", out var result);

            Assert.That(completed, Is.False);
            Assert.That(result.IsCompleted, Is.False);
            Assert.That(daily.SelectedRouteId, Is.EqualTo("daily.missing"));
        }

        private static StudentLifeProgress CreateProgress()
        {
            return new StudentLifeProgress("slot-a", "player-a", 100, 100);
        }

        private static LocationDefinition CreateLocation(string id, string displayNameKey)
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests(id, displayNameKey, Vector2.zero, System.Array.Empty<DiscoveryDefinition>());
            return location;
        }

        private static LocationActivityDefinition CreateActivity(
            string id,
            string displayNameKey,
            LocationDefinition location,
            LocationGrowthRoute growthRoute)
        {
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests(
                id,
                displayNameKey,
                location,
                growthRoute,
                60,
                0,
                0,
                0,
                System.Array.Empty<LocationActivityRequirementBase>(),
                System.Array.Empty<LocationActivityOutcomeBase>());
            return activity;
        }

        private static DailyPlanRouteDefinition CreateRoute(
            string id,
            string displayNameKey,
            LocationDefinition location,
            LocationGrowthRoute growthRoute,
            LocationActivityDefinition[] matchingActivities = null)
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                id,
                displayNameKey,
                location,
                growthRoute,
                matchingActivities ?? System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective." + id + ".title",
                "objective." + id + ".detail",
                "objective." + id + ".next");
            return route;
        }
    }
}
