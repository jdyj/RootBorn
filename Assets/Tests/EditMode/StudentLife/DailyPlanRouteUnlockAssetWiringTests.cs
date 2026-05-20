using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanRouteUnlockAssetWiringTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string RouteId = "daily.route.library.archive-research";
        private const string WorldStateId = "world.library.archive-open";

        [Test]
        public void RouteUnlockWorldStateFlagExistsInRegistry()
        {
            var registry = LoadRegistry();

            Assert.IsNotNull(FindWorldState(registry), "Route unlock world-state flag must be registered.");
        }

        [Test]
        public void ClueInterpretationOutcomeReferencesSameFlagUsedByRouteRequirement()
        {
            var registry = LoadRegistry();
            var flag = FindWorldState(registry);
            var route = FindRoute(registry);

            Assert.IsNotNull(flag);
            Assert.IsNotNull(route, "Unlocked Daily Plan route must be registered in a route set.");
            Assert.IsTrue(route.Requirements.OfType<DailyPlanWorldStateRequirement>().Any(), "Route must be gated by DailyPlanWorldStateRequirement.");
            Assert.IsTrue(InterpretationOutcomesReferenceFlag(registry, flag), "A registered clue interpretation outcome must activate the same world-state flag.");
        }

        [Test]
        public void RegisteredRoute_IsUnavailableBeforeWorldStateActivationAndAvailableAfter()
        {
            var registry = LoadRegistry();
            var route = FindRoute(registry);
            var flag = FindWorldState(registry);
            var student = new StudentLifeProgress("slot-route-unlock", "player-route-unlock", 100, 100);
            var locked = new DailyPlanRouteContext(student, new WorldStateProgress("slot-route-unlock", "player-route-unlock"), null, null);

            Assert.IsNotNull(route);
            Assert.IsFalse(route.IsAvailable(in locked));

            var world = new WorldStateProgress("slot-route-unlock", "player-route-unlock");
            Assert.IsTrue(world.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, "test", 1)));
            var unlocked = new DailyPlanRouteContext(student, world, null, null);

            Assert.IsTrue(route.IsAvailable(in unlocked));
        }

        [Test]
        public void RegisteredRoute_HasReachableTargetLocationMatchingActivityAndObjectiveText()
        {
            var registry = LoadRegistry();
            var route = FindRoute(registry);

            Assert.IsNotNull(route);
            Assert.IsNotNull(route.TargetLocation, "Unlocked route must target an existing location.");
            CollectionAssert.Contains(registry.Locations, route.TargetLocation, "Route target location must be registered.");
            Assert.IsTrue(route.MatchingActivities.Count > 0 || route.TargetLocation != null, "Route must be matchable by activity or target location route.");
            if (route.MatchingActivities.Count > 0)
            {
                Assert.IsTrue(route.MatchingActivities.All(activity => activity != null && registry.LocationActivities.Contains(activity)), "All route matching activities must be registered.");
            }

            Assert.IsFalse(string.IsNullOrEmpty(route.ObjectiveTitleKey));
            Assert.IsFalse(string.IsNullOrEmpty(route.ObjectiveDetailKey));
            Assert.IsFalse(string.IsNullOrEmpty(route.NextHintKey));
        }

        [Test]
        public void RegisteredClueInterpretationCanBeFoundWithoutResourcesFallback()
        {
            var registry = LoadRegistry();
            var flag = FindWorldState(registry);

            Assert.IsNotNull(registry.DiscoveryClues.FirstOrDefault(clue => clue != null && clue.Id == "clue.discovery.library-archive-rumor"));
            Assert.IsTrue(InterpretationOutcomesReferenceFlag(registry, flag));
        }

        private static GameDataRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            return registry;
        }

        private static WorldStateFlagDefinition FindWorldState(GameDataRegistry registry)
        {
            return registry.WorldStateFlags.FirstOrDefault(flag => flag != null && flag.Id == WorldStateId);
        }

        private static DailyPlanRouteDefinition FindRoute(GameDataRegistry registry)
        {
            return registry.DailyPlanRouteSets
                .Where(set => set != null)
                .SelectMany(set => set.Routes)
                .FirstOrDefault(route => route != null && route.Id == RouteId);
        }

        private static bool InterpretationOutcomesReferenceFlag(GameDataRegistry registry, WorldStateFlagDefinition flag)
        {
            if (flag == null) return false;
            return registry.ClueInterpretations
                .Where(interpretation => interpretation != null)
                .SelectMany(interpretation => interpretation.Outcomes)
                .OfType<ClueInterpretationWorldStateOutcome>()
                .Any(outcome => WorldStateOutcomeReferencesFlag(outcome, flag));
        }

        private static bool WorldStateOutcomeReferencesFlag(ClueInterpretationWorldStateOutcome outcome, WorldStateFlagDefinition flag)
        {
            var field = typeof(ClueInterpretationWorldStateOutcome).GetField("_flags", BindingFlags.Instance | BindingFlags.NonPublic);
            var flags = (WorldStateFlagDefinition[])field.GetValue(outcome);
            return flags != null && flags.Contains(flag);
        }
    }
}