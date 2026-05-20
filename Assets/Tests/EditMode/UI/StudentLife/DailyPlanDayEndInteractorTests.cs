using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class DailyPlanDayEndInteractorTests
    {
        [Test]
        public void BuildDailyPlanSummaryForTests_ReturnsCompletedRouteAndMatchedActivity()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var route = registry.DailyPlanRouteSets[0].Routes[0];
            var matchedActivity = route.MatchingActivities[0];
            var player = new GameObject("Player", typeof(DailyPlanProgressComponent));
            var daily = player.GetComponent<DailyPlanProgressComponent>();
            daily.RestoreFromSaveData(
                new DailyPlanProgressSaveData
                {
                    SaveSlot = "slot-a",
                    PlayerId = "player-a",
                    CurrentDay = 1,
                    SelectedRouteId = route.Id,
                    CompletedRouteId = route.Id,
                    MatchedActivityId = matchedActivity.Id,
                    AppliedCompletionKeys = new[] { "request-1" }
                },
                "slot-a",
                "player-a",
                1);

            var summary = StudentDayEndInteractor.BuildDailyPlanSummaryForTests(player, registry);

            Assert.That(summary.RouteDisplayNameKey, Is.EqualTo(route.DisplayNameKey));
            Assert.That(summary.ActivityDisplayNameKey, Is.EqualTo(matchedActivity.DisplayNameKey));
            Assert.That(summary.Completed, Is.True);
            Assert.That(summary.NextHintKey, Is.EqualTo(route.NextHintKey));
            Object.DestroyImmediate(player);
        }
    }
}
