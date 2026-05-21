using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.Objectives;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class DailyPlanRouteHintObjectiveSourceTests
    {
        [Test]
        public void DailyPlanRouteHintObjectiveSource_ExposesExpectedSourceIdAndCategory()
        {
            var source = new DailyPlanRouteHintObjectiveSource();

            Assert.AreEqual("daily-plan-route-hints", source.SourceId);
            Assert.AreEqual("Hints", source.Category.CategoryId);
        }

        [Test]
        public void DailyPlanRouteHintObjectiveSource_DoesNotEmitHintForLockedRoute()
        {
            using (var fixture = new RouteHintFixture(false, false))
            {
                var source = new DailyPlanRouteHintObjectiveSource();

                var items = source.BuildItems(fixture.Context);

                Assert.IsEmpty(items);
            }
        }

        [Test]
        public void DailyPlanRouteHintObjectiveSource_EmitsHintForUnlockedUnselectedRoute()
        {
            using (var fixture = new RouteHintFixture(true, false))
            {
                var source = new DailyPlanRouteHintObjectiveSource();

                var items = source.BuildItems(fixture.Context);

                Assert.AreEqual(1, items.Length);
                Assert.AreEqual("daily-plan-route-hint:" + fixture.Route.Id, items[0].StableKey);
                Assert.AreEqual("Hints", items[0].CategoryId);
                Assert.AreEqual(fixture.Route.ObjectiveTitleKey, items[0].Title);
                Assert.AreEqual(fixture.Route.ObjectiveDetailKey, items[0].Subtitle);
                Assert.AreEqual(fixture.Route.NextHintKey, items[0].NextHintText);
            }
        }

        [Test]
        public void DailyPlanRouteHintObjectiveSource_DoesNotEmitSelectedRouteAsHint()
        {
            using (var fixture = new RouteHintFixture(true, true))
            {
                var source = new DailyPlanRouteHintObjectiveSource();

                var items = source.BuildItems(fixture.Context);

                Assert.IsEmpty(items);
            }
        }

        [Test]
        public void ObjectiveJournalSourceCatalog_DefaultSourcesContainsDailyPlanRouteHintsInDeterministicOrder()
        {
            var sources = ObjectiveJournalSourceCatalog.CreateDefaultSources();
            var ids = new string[sources.Length];
            for (int i = 0; i < sources.Length; i++) ids[i] = sources[i].SourceId;

            CollectionAssert.AreEqual(
                new[] { "student-day-guide", "daily-plan", "daily-plan-route-hints", "resource-recipes", "status-activity", "quest-log", "quest-chain-log", "campaign-progress", "milestones", "career-interests" },
                ids);
        }

        [Test]
        public void ObjectiveJournalProvider_ComposesDailyPlanRouteHintIntoHintsCategory()
        {
            using (var fixture = new RouteHintFixture(true, false))
            {
                var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[] { new DailyPlanRouteHintObjectiveSource() });

                var result = aggregator.Build(fixture.Context);

                Assert.IsTrue(result.ContainsKey("Hints"));
                Assert.AreEqual("daily-plan-route-hint:" + fixture.Route.Id, result["Hints"][0].StableKey);
            }
        }

        private sealed class RouteHintFixture : IDisposable
        {
            private readonly string _rootDirectory;
            private readonly WorldStateFlagDefinition _flag;
            private readonly DailyPlanWorldStateRequirement _requirement;
            private readonly DailyPlanRouteSetDefinition _routeSet;
            private readonly GameDataRegistry _registry;
            private readonly GameObject _player;

            public RouteHintFixture(bool unlocked, bool selected)
            {
                _rootDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-route-hint-" + Guid.NewGuid().ToString("N"));
                SaveService.SetRootDirectoryForTests(_rootDirectory);
                ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-route-hint", DisplayName = "Route Hint" });

                _flag = CreateFlag("world.route.hint-unlocked");
                _requirement = ScriptableObject.CreateInstance<DailyPlanWorldStateRequirement>();
                _requirement.ConfigureForTests(_flag, true);
                Route = CreateRoute("daily.route.hint", _requirement);
                _routeSet = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
                _routeSet.ConfigureForTests("daily.routes.hints", "daily.routes.hints", new[] { Route }, 1);
                _registry = ScriptableObject.CreateInstance<GameDataRegistry>();
                SetRegistryRoutes(_registry, new[] { _routeSet });

                _player = new GameObject("Player");
                var student = _player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-route-hint", "player-route-hint", 100, 100, 0, 480);
                var daily = _player.AddComponent<DailyPlanProgressComponent>();
                daily.RestoreFromSaveData(
                    new DailyPlanProgressSaveData
                    {
                        SaveSlot = "slot-route-hint",
                        PlayerId = "player-route-hint",
                        CurrentDay = 1,
                        SelectedRouteId = selected ? Route.Id : string.Empty
                    },
                    "slot-route-hint",
                    "player-route-hint",
                    1);

                if (unlocked)
                {
                    var world = new WorldStateProgress("slot-route-hint", "player-route-hint");
                    Assert.IsTrue(world.TryActivate(_flag, new WorldStateActivationSource(string.Empty, string.Empty, "test", 1)));
                    WorldStateProgressPersistence.Save(world);
                }

                Context = new ObjectiveJournalContext(_registry, _player, ActiveSaveContext.Metadata);
            }

            public DailyPlanRouteDefinition Route { get; }
            public ObjectiveJournalContext Context { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_player);
                UnityEngine.Object.DestroyImmediate(_registry);
                UnityEngine.Object.DestroyImmediate(_routeSet);
                UnityEngine.Object.DestroyImmediate(Route);
                UnityEngine.Object.DestroyImmediate(_requirement);
                UnityEngine.Object.DestroyImmediate(_flag);
                ActiveSaveContext.Clear();
                SaveService.SetRootDirectoryForTests(null);
                if (System.IO.Directory.Exists(_rootDirectory)) System.IO.Directory.Delete(_rootDirectory, true);
            }
        }

        private static void SetRegistryRoutes(GameDataRegistry registry, DailyPlanRouteSetDefinition[] routeSets)
        {
            var field = typeof(GameDataRegistry).GetField("_dailyPlanRouteSets", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(registry, routeSets);
        }

        private static DailyPlanRouteDefinition CreateRoute(string id, DailyPlanRouteRequirementBase requirement)
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                id,
                id + ".name",
                null,
                LocationGrowthRoute.SelfStudy,
                Array.Empty<LocationActivityDefinition>(),
                new[] { requirement },
                Array.Empty<DailyPlanRouteOutcomeBase>(),
                id + ".title",
                id + ".detail",
                id + ".next");
            return route;
        }

        private static WorldStateFlagDefinition CreateFlag(string id)
        {
            var flag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            flag.ConfigureForTests(
                id,
                id + ".name",
                id + ".desc",
                null,
                null,
                null,
                WorldStateScopeKind.Shared,
                WorldStateChangeKind.LocationUnlocked,
                id + ".next",
                Array.Empty<WorldStateSummarySurface>(),
                Array.Empty<WorldStateBadgeKind>(),
                0,
                1);
            return flag;
        }
    }
}
