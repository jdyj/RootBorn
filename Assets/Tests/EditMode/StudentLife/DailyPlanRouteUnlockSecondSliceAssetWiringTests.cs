using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.Objectives;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanRouteUnlockSecondSliceAssetWiringTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string FirstRouteId = "daily.route.library.archive-research";
        private const string FirstWorldStateId = "world.library.archive-open";
        private const string SecondRouteId = "daily.route.workshop.repair-ledger";
        private const string SecondWorldStateId = "world.workshop.repair-ledger-open";
        private const string SecondInterpretationId = "interpret.workshop-repair-ledger";

        [Test]
        public void SecondRouteUnlockWorldStateFlagExistsInRegistry()
        {
            var registry = LoadRegistry();

            Assert.IsNotNull(FindWorldState(registry, SecondWorldStateId), "Second route unlock world-state flag must be registered.");
        }

        [Test]
        public void SecondClueInterpretationOutcomeReferencesSameFlagUsedBySecondRouteRequirement()
        {
            var registry = LoadRegistry();
            var flag = FindWorldState(registry, SecondWorldStateId);
            var route = FindRoute(registry, SecondRouteId);
            var interpretation = FindInterpretation(registry, SecondInterpretationId);

            Assert.IsNotNull(flag);
            Assert.IsNotNull(route, "Second unlocked Daily Plan route must be registered in a route set.");
            Assert.IsNotNull(interpretation, "Second route unlock must be driven by a registered clue interpretation.");
            Assert.IsTrue(route.Requirements.OfType<DailyPlanWorldStateRequirement>().Any(requirement => RequirementReferencesFlag(requirement, flag)), "Second route must be gated by the second world-state flag.");
            Assert.IsTrue(interpretation.Outcomes.OfType<ClueInterpretationWorldStateOutcome>().Any(outcome => WorldStateOutcomeReferencesFlag(outcome, flag)), "Second clue interpretation outcome must activate the same world-state flag used by the route requirement.");
        }

        [Test]
        public void FirstAndSecondUnlockRoutesUseSeparateRouteIdsAndWorldStateFlags()
        {
            var registry = LoadRegistry();
            var firstRoute = FindRoute(registry, FirstRouteId);
            var secondRoute = FindRoute(registry, SecondRouteId);
            var firstFlag = FindWorldState(registry, FirstWorldStateId);
            var secondFlag = FindWorldState(registry, SecondWorldStateId);

            Assert.IsNotNull(firstRoute);
            Assert.IsNotNull(secondRoute);
            Assert.IsNotNull(firstFlag);
            Assert.IsNotNull(secondFlag);
            Assert.AreNotEqual(firstRoute.Id, secondRoute.Id);
            Assert.AreNotEqual(firstFlag.Id, secondFlag.Id);
            Assert.IsFalse(firstRoute.Requirements.OfType<DailyPlanWorldStateRequirement>().Any(requirement => RequirementReferencesFlag(requirement, secondFlag)), "First unlock route must not reuse the second world-state flag.");
            Assert.IsFalse(secondRoute.Requirements.OfType<DailyPlanWorldStateRequirement>().Any(requirement => RequirementReferencesFlag(requirement, firstFlag)), "Second unlock route must not reuse the first world-state flag.");
        }

        [Test]
        public void SecondRoute_IsUnavailableBeforeWorldStateActivationAndAvailableAfter()
        {
            var registry = LoadRegistry();
            var route = FindRoute(registry, SecondRouteId);
            var flag = FindWorldState(registry, SecondWorldStateId);
            var student = new StudentLifeProgress("slot-second-route", "player-second-route", 100, 100);
            var locked = new DailyPlanRouteContext(student, new WorldStateProgress("slot-second-route", "player-second-route"), null, null);

            Assert.IsNotNull(route);
            Assert.IsNotNull(flag);
            Assert.IsFalse(route.IsAvailable(in locked));

            var world = new WorldStateProgress("slot-second-route", "player-second-route");
            Assert.IsTrue(world.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, "test", 1)));
            var unlocked = new DailyPlanRouteContext(student, world, null, null);

            Assert.IsTrue(route.IsAvailable(in unlocked));
        }

        [Test]
        public void SecondRoute_HasReachableTargetLocationMatchingActivityAndObjectiveText()
        {
            var registry = LoadRegistry();
            var route = FindRoute(registry, SecondRouteId);

            Assert.IsNotNull(route);
            Assert.IsNotNull(route.TargetLocation, "Second route must target an existing location.");
            CollectionAssert.Contains(registry.Locations, route.TargetLocation, "Second route target location must be registered.");
            Assert.IsTrue(route.MatchingActivities.Count > 0 || route.TargetLocation != null, "Second route must be matchable by activity or target location route.");
            if (route.MatchingActivities.Count > 0)
            {
                Assert.IsTrue(route.MatchingActivities.All(activity => activity != null && registry.LocationActivities.Contains(activity)), "All second route matching activities must be registered.");
            }

            Assert.IsFalse(string.IsNullOrEmpty(route.ObjectiveTitleKey));
            Assert.IsFalse(string.IsNullOrEmpty(route.ObjectiveDetailKey));
            Assert.IsFalse(string.IsNullOrEmpty(route.NextHintKey));
        }

        [Test]
        public void ObjectiveJournal_ExposesUnlockedUnselectedSecondRouteAsHintThenMovesSelectedRouteToGoals()
        {
            string rootDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-second-route-objective-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(rootDirectory);
            var registry = LoadRegistry();
            var route = FindRoute(registry, SecondRouteId);
            var flag = FindWorldState(registry, SecondWorldStateId);
            var player = new GameObject("Player");

            try
            {
                Assert.IsNotNull(route);
                Assert.IsNotNull(flag);
                var student = player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-second-route-objective", "player-second-route-objective", 100, 100, 0, 480);
                player.AddComponent<PlayerIdentity>().Configure("player-second-route-objective");
                var daily = player.AddComponent<DailyPlanProgressComponent>();
                daily.RestoreFromSaveData(new DailyPlanProgressSaveData { SaveSlot = "slot-second-route-objective", PlayerId = "player-second-route-objective", CurrentDay = 1 }, "slot-second-route-objective", "player-second-route-objective", 1);
                var world = new WorldStateProgress("slot-second-route-objective", "player-second-route-objective");
                Assert.IsTrue(world.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, "test", 1)));
                WorldStateProgressPersistence.Save(world);
                var context = new ObjectiveJournalContext(registry, player, new SaveSlotMetadata { SlotId = "slot-second-route-objective", DisplayName = "Second Route" });
                var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[] { new DailyPlanObjectiveSource(), new DailyPlanRouteHintObjectiveSource() });

                var unselected = aggregator.Build(context);
                Assert.AreEqual("daily-plan:choose", unselected["Goals"][0].StableKey);
                Assert.IsTrue(unselected["Hints"].Any(item => item.StableKey == "daily-plan-route-hint:" + route.Id));

                daily.RestoreFromSaveData(new DailyPlanProgressSaveData { SaveSlot = "slot-second-route-objective", PlayerId = "player-second-route-objective", CurrentDay = 1, SelectedRouteId = route.Id }, "slot-second-route-objective", "player-second-route-objective", 1);
                var selected = aggregator.Build(context);

                Assert.IsTrue(selected["Goals"].Any(item => item.StableKey == "daily-plan:" + route.Id));
                Assert.IsFalse(selected["Hints"].Any(item => item.StableKey == "daily-plan-route-hint:" + route.Id));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                SaveService.SetRootDirectoryForTests(null);
                if (System.IO.Directory.Exists(rootDirectory)) System.IO.Directory.Delete(rootDirectory, true);
            }
        }

        private static GameDataRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            return registry;
        }

        private static WorldStateFlagDefinition FindWorldState(GameDataRegistry registry, string id)
        {
            return registry.WorldStateFlags.FirstOrDefault(flag => flag != null && flag.Id == id);
        }

        private static DailyPlanRouteDefinition FindRoute(GameDataRegistry registry, string id)
        {
            return registry.DailyPlanRouteSets
                .Where(set => set != null)
                .SelectMany(set => set.Routes)
                .FirstOrDefault(route => route != null && route.Id == id);
        }

        private static ClueInterpretationDefinition FindInterpretation(GameDataRegistry registry, string id)
        {
            return registry.ClueInterpretations.FirstOrDefault(interpretation => interpretation != null && interpretation.Id == id);
        }

        private static bool RequirementReferencesFlag(DailyPlanWorldStateRequirement requirement, WorldStateFlagDefinition flag)
        {
            if (requirement == null || flag == null) return false;
            var field = typeof(DailyPlanWorldStateRequirement).GetField("_flag", BindingFlags.Instance | BindingFlags.NonPublic);
            return ReferenceEquals(field.GetValue(requirement), flag);
        }

        private static bool WorldStateOutcomeReferencesFlag(ClueInterpretationWorldStateOutcome outcome, WorldStateFlagDefinition flag)
        {
            if (outcome == null || flag == null) return false;
            var field = typeof(ClueInterpretationWorldStateOutcome).GetField("_flags", BindingFlags.Instance | BindingFlags.NonPublic);
            var flags = (WorldStateFlagDefinition[])field.GetValue(outcome);
            return flags != null && flags.Contains(flag);
        }
    }
}
