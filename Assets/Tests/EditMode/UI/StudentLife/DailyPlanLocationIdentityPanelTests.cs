using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class DailyPlanLocationIdentityPanelTests
    {
        [Test]
        public void ClickingMatchingLocationActivity_CompletesSelectedDailyPlan()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var panelGo = new GameObject("LocationIdentityPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = panelGo.AddComponent<LocationIdentityPanel>();
            var player = new GameObject("Player", typeof(StudentLifeProgressComponent), typeof(DailyPlanProgressComponent));
            var student = player.GetComponent<StudentLifeProgressComponent>();
            student.ConfigureForTests("slot-a", "player-a", 100, 100, 0, 8 * 60);
            var daily = player.GetComponent<DailyPlanProgressComponent>();
            daily.Load("slot-a", "player-a", 1);
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
            var identity = ScriptableObject.CreateInstance<LocationIdentityDefinition>();
            identity.ConfigureForTests(
                "identity.library",
                "identity.library",
                location,
                "identity.library.desc",
                new[] { activity },
                System.Array.Empty<LocationHintEntry>(),
                8 * 60,
                19 * 60);
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.library.title",
                "objective.daily.library.detail",
                "objective.daily.library.next");
            var routeSet = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            routeSet.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            daily.TrySelect(route, student.Progress);

            panel.BindDailyPlanRouteSetsForTests(new[] { routeSet });
            panel.Show(identity, player);
            panel.ClickActivityForTests(0);

            Assert.That(daily.Progress.CompletedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(daily.Progress.MatchedActivityId, Is.EqualTo("activity.library.read"));
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(player);
        }
    }
}
