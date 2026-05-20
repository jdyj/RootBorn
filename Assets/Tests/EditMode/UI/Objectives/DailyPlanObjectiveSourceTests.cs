using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Objectives;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class DailyPlanObjectiveSourceTests
    {
        [Test]
        public void Source_BeforeSelection_ShowsChooseRouteGoal()
        {
            var player = new GameObject("Player");
            try
            {
                var component = player.AddComponent<DailyPlanProgressComponent>();
                component.RestoreFromSaveData(new DailyPlanProgressSaveData(), "slot-a", "player-a", 1);
                var registry = LoadRegistry();
                var source = new DailyPlanObjectiveSource();

                var items = source.BuildItems(new ObjectiveJournalContext(registry, player, null));

                Assert.That(items.Length, Is.EqualTo(1));
                Assert.That(items[0].CategoryId, Is.EqualTo("Goals"));
                Assert.That(items[0].StableKey, Is.EqualTo("daily-plan:choose"));
                Assert.That(items[0].Title, Is.EqualTo("objective.daily.choose.title"));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Source_SelectedRoute_ShowsSelectedGoalWithStableKey()
        {
            var player = new GameObject("Player");
            try
            {
                var registry = LoadRegistry();
                var route = registry.DailyPlanRouteSets[0].Routes[0];
                var component = player.AddComponent<DailyPlanProgressComponent>();
                component.RestoreFromSaveData(
                    new DailyPlanProgressSaveData
                    {
                        SaveSlot = "slot-a",
                        PlayerId = "player-a",
                        CurrentDay = 1,
                        SelectedRouteId = route.Id
                    },
                    "slot-a",
                    "player-a",
                    1);
                var source = new DailyPlanObjectiveSource();

                var items = source.BuildItems(new ObjectiveJournalContext(registry, player, null));

                Assert.That(items.Length, Is.EqualTo(1));
                Assert.That(items[0].StableKey, Is.EqualTo("daily-plan:" + route.Id));
                Assert.That(items[0].Title, Is.EqualTo(route.ObjectiveTitleKey));
                Assert.That(items[0].Subtitle, Is.EqualTo(route.DisplayNameKey));
                Assert.That(items[0].State, Is.EqualTo("Selected"));
                Assert.That(items[0].NextHintText, Is.EqualTo(route.ObjectiveDetailKey));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Source_CompletedRoute_ShowsNextHint()
        {
            var player = new GameObject("Player");
            try
            {
                var registry = LoadRegistry();
                var route = registry.DailyPlanRouteSets[0].Routes[0];
                var component = player.AddComponent<DailyPlanProgressComponent>();
                component.RestoreFromSaveData(
                    new DailyPlanProgressSaveData
                    {
                        SaveSlot = "slot-a",
                        PlayerId = "player-a",
                        CurrentDay = 1,
                        SelectedRouteId = route.Id,
                        CompletedRouteId = route.Id,
                        MatchedActivityId = "location.activity.library-hard-book"
                    },
                    "slot-a",
                    "player-a",
                    1);
                var source = new DailyPlanObjectiveSource();

                var items = source.BuildItems(new ObjectiveJournalContext(registry, player, null));

                Assert.That(items.Length, Is.EqualTo(1));
                Assert.That(items[0].State, Is.EqualTo("Completed"));
                Assert.That(items[0].NextHintText, Is.EqualTo(route.NextHintKey));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SourceCatalog_RegistersDailyPlanSource()
        {
            var sources = ObjectiveJournalSourceCatalog.CreateDefaultSources();

            Assert.That(System.Array.Exists(sources, source => source.SourceId == "daily-plan"), Is.True);
        }

        private static GameDataRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.DailyPlanRouteSets.Length, Is.GreaterThanOrEqualTo(1));
            return registry;
        }
    }
}
