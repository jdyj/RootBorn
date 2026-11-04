using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Objectives;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class DailyPlanScheduleJournalTests
    {
        [Test]
        public void TwoPendingSlotsWithSameRoute_AppearAsTwoDistinctJournalGoals()
        {
            WithSchedule(false, (registry, player, route) =>
            {
                var items = new DailyPlanObjectiveSource().BuildItems(new ObjectiveJournalContext(registry, player, null));
                Assert.That(items.Length, Is.EqualTo(2));
                Assert.That(items.Select(x => x.StableKey).Distinct().Count(), Is.EqualTo(2));
                Assert.That(items.All(x => x.Title == route.ObjectiveTitleKey), Is.True);
            });
        }

        [Test]
        public void CompletedFirstSlot_DoesNotHideSecondPendingSlotWithTheSameRoute()
        {
            WithSchedule(true, (registry, player, route) =>
            {
                var items = new DailyPlanObjectiveSource().BuildItems(new ObjectiveJournalContext(registry, player, null));
                Assert.That(items.Length, Is.EqualTo(1));
                Assert.That(items[0].Title, Is.EqualTo(route.ObjectiveTitleKey));
                Assert.That(items[0].StableKey, Is.Not.EqualTo("daily-plan:choose"));
            });
        }

        private static void WithSchedule(bool firstCompleted, Action<GameDataRegistry, GameObject, DailyPlanRouteDefinition> inspect)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry, Is.Not.Null);
            var route = registry.DailyPlanRouteSets[0].Routes[0];
            var player = new GameObject("ScheduledJournalPlayer");
            try
            {
                var component = player.AddComponent<DailyPlanProgressComponent>();
                string completed = firstCompleted ? "true" : "false";
                string json = "{\"Version\":2,\"SaveSlot\":\"journal-slot\",\"PlayerId\":\"journal-player\",\"CurrentDay\":1,"
                    + "\"SelectedRouteId\":\"" + route.Id + "\",\"CompletedRouteId\":\"" + (firstCompleted ? route.Id : "") + "\","
                    + "\"Slots\":[{\"SlotIndex\":0,\"PeriodKey\":\"daily-plan.period.after-school\",\"RouteId\":\"" + route.Id + "\",\"Completed\":" + completed + "},"
                    + "{\"SlotIndex\":1,\"PeriodKey\":\"daily-plan.period.evening\",\"RouteId\":\"" + route.Id + "\",\"Completed\":false}]}";
                component.RestoreFromSaveData(JsonUtility.FromJson<DailyPlanProgressSaveData>(json), "journal-slot", "journal-player", 1);
                inspect(registry, player, route);
            }
            finally { Object.DestroyImmediate(player); }
        }
    }
}
