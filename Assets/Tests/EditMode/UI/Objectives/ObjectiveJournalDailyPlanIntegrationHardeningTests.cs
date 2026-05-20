using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.Objectives;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalDailyPlanIntegrationHardeningTests
    {
        private const string LibraryArchiveRouteId = "daily.route.library.archive-research";
        private const string WorkshopRepairLedgerRouteId = "daily.route.workshop.repair-ledger";
        private const string LibraryArchiveWorldStateId = "world.library.archive-open";
        private const string WorkshopRepairLedgerWorldStateId = "world.workshop.repair-ledger-open";

        [Test]
        public void SelectedUnlockedRoute_AppearsInGoalsAndIsRemovedFromHints()
        {
            using (var fixture = new DailyPlanRouteFixture(true, true))
            {
                var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[]
                {
                    new DailyPlanObjectiveSource(),
                    new DailyPlanRouteHintObjectiveSource()
                });

                var result = aggregator.Build(fixture.Context);

                Assert.IsTrue(result.ContainsKey("Goals"));
                Assert.AreEqual("daily-plan:" + fixture.Route.Id, result["Goals"][0].StableKey);
                Assert.AreEqual(fixture.Route.ObjectiveTitleKey, result["Goals"][0].Title);
                Assert.IsTrue(result.ContainsKey("Hints"));
                Assert.AreEqual(0, result["Hints"].Length, "Selected Daily Plan routes must move out of Hints so Objective Journal does not show duplicate guidance.");
            }
        }

        [Test]
        public void UnlockedUnselectedRoute_AppearsOnlyInHints()
        {
            using (var fixture = new DailyPlanRouteFixture(true, false))
            {
                var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[]
                {
                    new DailyPlanObjectiveSource(),
                    new DailyPlanRouteHintObjectiveSource()
                });

                var result = aggregator.Build(fixture.Context);

                Assert.AreEqual("daily-plan:choose", result["Goals"][0].StableKey);
                Assert.AreEqual(1, result["Hints"].Length);
                Assert.AreEqual("daily-plan-route-hint:" + fixture.Route.Id, result["Hints"][0].StableKey);
            }
        }

        [Test]
        public void RealDiscoveryRouteUnlocks_UseSingleHintAndGoalSourceContract()
        {
            var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[]
            {
                new DailyPlanObjectiveSource(),
                new DailyPlanRouteHintObjectiveSource()
            });

            using (var fixture = new RealDiscoveryRouteFixture(string.Empty))
            {
                var result = aggregator.Build(fixture.Context);

                Assert.AreEqual("daily-plan:choose", result["Goals"][0].StableKey, "Without a selected Daily Plan route, the Journal must keep the choose-route goal.");
                Assert.IsTrue(result.TryGetValue("Hints", out var hints), "Unlocked real Discovery routes must be emitted through Hints.");
                int libraryIndex = IndexOfStableKey(hints, "daily-plan-route-hint:" + LibraryArchiveRouteId);
                int workshopIndex = IndexOfStableKey(hints, "daily-plan-route-hint:" + WorkshopRepairLedgerRouteId);
                Assert.GreaterOrEqual(libraryIndex, 0, "First Discovery route unlock data must participate in route hint source output.");
                Assert.GreaterOrEqual(workshopIndex, 0, "Second Discovery route unlock data must participate in route hint source output.");
                Assert.Less(libraryIndex, workshopIndex, "Real route hint ordering must remain deterministic for the first and second Discovery route unlocks.");
            }

            using (var fixture = new RealDiscoveryRouteFixture(WorkshopRepairLedgerRouteId))
            {
                var result = aggregator.Build(fixture.Context);

                Assert.IsTrue(result.TryGetValue("Goals", out var goals), "Selected real route must be emitted through Goals.");
                Assert.GreaterOrEqual(IndexOfStableKey(goals, "daily-plan:" + WorkshopRepairLedgerRouteId), 0, "Selected second route must move into Goals.");
                Assert.IsTrue(result.TryGetValue("Hints", out var hints), "Other unlocked real routes must remain available through Hints.");
                Assert.GreaterOrEqual(IndexOfStableKey(hints, "daily-plan-route-hint:" + LibraryArchiveRouteId), 0, "Unselected first route must remain hinted when the second route is selected.");
                Assert.AreEqual(-1, IndexOfStableKey(hints, "daily-plan-route-hint:" + WorkshopRepairLedgerRouteId), "Selected second route must not be duplicated as a Hint.");
            }
        }

        [Test]
        public void AggregatorRepeatedRefreshClearsStaleHintRowsAndKeepsSingleTrackedHud()
        {
            var source = new MutableSource(
                "hint-source",
                new ObjectiveJournalCategory("Hints", "Hints"),
                new[] { new ObjectiveJournalItem("hint.route", "Hints", "Route Hint", "Detail", "Available", "Route unlocked", "Select a route", false, 0) });
            var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[] { source });
            var root = new GameObject("ObjectiveJournalIntegrationFixture", typeof(RectTransform));

            try
            {
                var panelGo = new GameObject("ObjectiveJournalPanel", typeof(RectTransform));
                panelGo.transform.SetParent(root.transform, false);
                var panel = panelGo.AddComponent<ObjectiveJournalPanel>();

                var hudGo = new GameObject("TrackedObjectiveHud", typeof(RectTransform));
                hudGo.transform.SetParent(root.transform, false);
                var hud = hudGo.AddComponent<TrackedObjectiveHud>();
                panel.BindTrackedHud(hud);

                panel.Show();
                aggregator.Refresh(panel, new ObjectiveJournalContext(null, null, null), "hint.route");
                aggregator.Refresh(panel, new ObjectiveJournalContext(null, null, null), "hint.route");

                Assert.AreEqual("Hints", panel.SelectedCategoryIdForTests);
                Assert.AreEqual(1, panel.VisibleRowCountForTests, "Repeated provider refresh must update rows in-place instead of duplicating the tracked objective row.");
                Assert.AreEqual(1, root.GetComponentsInChildren<TrackedObjectiveHud>(true).Length, "Tracking restore must reuse the bound HUD instead of creating additional permanent HUD instances.");
                Assert.IsTrue(hud.IsVisible);

                source.Items = Array.Empty<ObjectiveJournalItem>();
                aggregator.Refresh(panel, new ObjectiveJournalContext(null, null, null), "hint.route");
                panel.SelectCategory("Hints");

                Assert.AreEqual(0, panel.VisibleRowCountForTests, "When a selected route leaves Hints, stale hint rows must be cleared on refresh.");
                Assert.AreEqual(string.Empty, panel.SelectedStableKeyForTests);
                Assert.AreEqual(1, root.GetComponentsInChildren<TrackedObjectiveHud>(true).Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DailyPlanSources_DegradeWithNullContextWithoutThrowing()
        {
            var context = new ObjectiveJournalContext(null, null, null);

            Assert.DoesNotThrow(() => new DailyPlanObjectiveSource().BuildItems(context));
            Assert.DoesNotThrow(() => new DailyPlanRouteHintObjectiveSource().BuildItems(context));
            Assert.AreEqual("daily-plan:choose", new DailyPlanObjectiveSource().BuildItems(context)[0].StableKey);
            Assert.IsEmpty(new DailyPlanRouteHintObjectiveSource().BuildItems(context));
        }

        private static int IndexOfStableKey(ObjectiveJournalItem[] items, string stableKey)
        {
            if (items == null) return -1;
            for (int i = 0; i < items.Length; i++)
            {
                if (string.Equals(items[i].StableKey, stableKey, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        private sealed class MutableSource : IObjectiveJournalSource
        {
            public MutableSource(string sourceId, ObjectiveJournalCategory category, ObjectiveJournalItem[] items)
            {
                SourceId = sourceId;
                Category = category;
                Items = items ?? Array.Empty<ObjectiveJournalItem>();
            }

            public string SourceId { get; }
            public ObjectiveJournalCategory Category { get; }
            public ObjectiveJournalItem[] Items { get; set; }

            public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
            {
                return Items;
            }
        }

        private sealed class RealDiscoveryRouteFixture : IDisposable
        {
            private const string SaveSlot = "slot-real-discovery-route-contract";
            private const string PlayerId = "player-real-discovery-route-contract";

            private readonly string _rootDirectory;
            private readonly GameObject _player;

            public RealDiscoveryRouteFixture(string selectedRouteId)
            {
                _rootDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-real-route-contract-" + Guid.NewGuid().ToString("N"));
                SaveService.SetRootDirectoryForTests(_rootDirectory);
                ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = SaveSlot, DisplayName = "Real Route Contract" });
                Registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
                Assert.IsNotNull(Registry, "GameDataRegistry asset must exist for real route source contract coverage.");
                AssertRouteExists(Registry, LibraryArchiveRouteId);
                AssertRouteExists(Registry, WorkshopRepairLedgerRouteId);

                _player = new GameObject("Player");
                var student = _player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests(SaveSlot, PlayerId, 100, 100, 0, 480);
                var daily = _player.AddComponent<DailyPlanProgressComponent>();
                daily.RestoreFromSaveData(
                    new DailyPlanProgressSaveData
                    {
                        SaveSlot = SaveSlot,
                        PlayerId = PlayerId,
                        CurrentDay = 1,
                        SelectedRouteId = selectedRouteId ?? string.Empty
                    },
                    SaveSlot,
                    PlayerId,
                    1);

                var world = new WorldStateProgress(SaveSlot, PlayerId);
                ActivateFlag(world, Registry, LibraryArchiveWorldStateId);
                ActivateFlag(world, Registry, WorkshopRepairLedgerWorldStateId);
                WorldStateProgressPersistence.Save(world);

                Context = new ObjectiveJournalContext(Registry, _player, ActiveSaveContext.Metadata);
            }

            public GameDataRegistry Registry { get; }
            public ObjectiveJournalContext Context { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_player);
                ActiveSaveContext.Clear();
                SaveService.SetRootDirectoryForTests(null);
                if (System.IO.Directory.Exists(_rootDirectory)) System.IO.Directory.Delete(_rootDirectory, true);
            }

            private static void ActivateFlag(WorldStateProgress world, GameDataRegistry registry, string flagId)
            {
                var flag = FindFlag(registry, flagId);
                Assert.IsTrue(world.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, "objective-journal-contract", 1)), "Real route unlock world-state flag should activate: " + flagId);
            }

            private static WorldStateFlagDefinition FindFlag(GameDataRegistry registry, string flagId)
            {
                var flags = registry.WorldStateFlags;
                for (int i = 0; i < flags.Length; i++)
                {
                    if (flags[i] != null && string.Equals(flags[i].Id, flagId, StringComparison.Ordinal)) return flags[i];
                }

                Assert.Fail("Missing real route unlock world-state flag in registry: " + flagId);
                return null;
            }

            private static void AssertRouteExists(GameDataRegistry registry, string routeId)
            {
                var route = new DailyPlanRouteRunner().FindRoute(registry.DailyPlanRouteSets, routeId);
                Assert.IsNotNull(route, "Missing real Daily Plan route in registry: " + routeId);
            }
        }

        private sealed class DailyPlanRouteFixture : IDisposable
        {
            private readonly string _rootDirectory;
            private readonly WorldStateFlagDefinition _flag;
            private readonly DailyPlanWorldStateRequirement _requirement;
            private readonly DailyPlanRouteSetDefinition _routeSet;
            private readonly GameDataRegistry _registry;
            private readonly GameObject _player;

            public DailyPlanRouteFixture(bool unlocked, bool selected)
            {
                _rootDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-objective-journal-hardening-" + Guid.NewGuid().ToString("N"));
                SaveService.SetRootDirectoryForTests(_rootDirectory);
                ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-objective-hardening", DisplayName = "Objective Hardening" });

                _flag = CreateFlag("world.objective-hardening.route-unlocked");
                _requirement = ScriptableObject.CreateInstance<DailyPlanWorldStateRequirement>();
                _requirement.ConfigureForTests(_flag, true);
                Route = CreateRoute("daily.route.objective-hardening", _requirement);
                _routeSet = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
                _routeSet.ConfigureForTests("daily.routes.objective-hardening", "daily.routes.objective-hardening", new[] { Route }, 1);
                _registry = ScriptableObject.CreateInstance<GameDataRegistry>();
                SetRegistryRoutes(_registry, new[] { _routeSet });

                _player = new GameObject("Player");
                var student = _player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-objective-hardening", "player-objective-hardening", 100, 100, 0, 480);
                var daily = _player.AddComponent<DailyPlanProgressComponent>();
                daily.RestoreFromSaveData(
                    new DailyPlanProgressSaveData
                    {
                        SaveSlot = "slot-objective-hardening",
                        PlayerId = "player-objective-hardening",
                        CurrentDay = 1,
                        SelectedRouteId = selected ? Route.Id : string.Empty
                    },
                    "slot-objective-hardening",
                    "player-objective-hardening",
                    1);

                if (unlocked)
                {
                    var world = new WorldStateProgress("slot-objective-hardening", "player-objective-hardening");
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