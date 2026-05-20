using NUnit.Framework;
using Rootborn.Game.WorldState;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanRouteContextTests
    {
        [Test]
        public void DailyPlanWorldStateRequirement_ReturnsFalseWithoutWorldStateProgress()
        {
            var flag = CreateFlag("world.route.library-unlocked");
            var requirement = ScriptableObject.CreateInstance<DailyPlanWorldStateRequirement>();
            var route = CreateRoute("daily.route.locked", requirement);
            try
            {
                requirement.ConfigureForTests(flag, true);
                var context = new DailyPlanRouteContext(CreateProgress(), null, null, null);

                Assert.IsFalse(requirement.IsSatisfied(in context, route));
            }
            finally
            {
                Object.DestroyImmediate(flag);
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void DailyPlanWorldStateRequirement_ReturnsTrueWhenFlagActive()
        {
            var flag = CreateFlag("world.route.library-unlocked");
            var requirement = ScriptableObject.CreateInstance<DailyPlanWorldStateRequirement>();
            var route = CreateRoute("daily.route.unlocked", requirement);
            try
            {
                requirement.ConfigureForTests(flag, true);
                var world = new WorldStateProgress("slot-a", "player-a");
                Assert.IsTrue(world.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, "test", 1)));
                var context = new DailyPlanRouteContext(CreateProgress(), world, null, null);

                Assert.IsTrue(requirement.IsSatisfied(in context, route));
            }
            finally
            {
                Object.DestroyImmediate(flag);
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void DailyPlanRouteRunner_GetAvailableRoutes_UsesContextRequirements()
        {
            var flag = CreateFlag("world.route.library-unlocked");
            var requirement = ScriptableObject.CreateInstance<DailyPlanWorldStateRequirement>();
            var route = CreateRoute("daily.route.library-archive", requirement);
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            try
            {
                requirement.ConfigureForTests(flag, true);
                set.ConfigureForTests("daily.routes", "daily.routes", new[] { route }, 1);
                var progress = CreateProgress();
                var runner = new DailyPlanRouteRunner();
                var lockedContext = new DailyPlanRouteContext(progress, null, null, null);

                Assert.IsEmpty(runner.GetAvailableRoutes(new[] { set }, in lockedContext));

                var world = new WorldStateProgress("slot-a", "player-a");
                world.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, "test", 1));
                var unlockedContext = new DailyPlanRouteContext(progress, world, null, null);

                CollectionAssert.AreEqual(new[] { route }, runner.GetAvailableRoutes(new[] { set }, in unlockedContext));
            }
            finally
            {
                Object.DestroyImmediate(flag);
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(route);
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void DailyPlanRouteRunner_TrySelectRoute_RejectsLockedWorldStateRoute()
        {
            var flag = CreateFlag("world.route.library-unlocked");
            var requirement = ScriptableObject.CreateInstance<DailyPlanWorldStateRequirement>();
            var route = CreateRoute("daily.route.library-archive", requirement);
            try
            {
                requirement.ConfigureForTests(flag, true);
                var daily = new DailyPlanProgress("slot-a", "player-a", 1);
                var context = new DailyPlanRouteContext(CreateProgress(), null, null, null);

                Assert.IsFalse(new DailyPlanRouteRunner().TrySelectRoute(daily, route, in context));
                Assert.IsEmpty(daily.SelectedRouteId);
            }
            finally
            {
                Object.DestroyImmediate(flag);
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void DailyPlanRouteRunner_TrySelectRoute_AcceptsUnlockedWorldStateRoute()
        {
            var flag = CreateFlag("world.route.library-unlocked");
            var requirement = ScriptableObject.CreateInstance<DailyPlanWorldStateRequirement>();
            var route = CreateRoute("daily.route.library-archive", requirement);
            try
            {
                requirement.ConfigureForTests(flag, true);
                var world = new WorldStateProgress("slot-a", "player-a");
                world.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, "test", 1));
                var daily = new DailyPlanProgress("slot-a", "player-a", 1);
                var context = new DailyPlanRouteContext(CreateProgress(), world, null, null);

                Assert.IsTrue(new DailyPlanRouteRunner().TrySelectRoute(daily, route, in context));
                Assert.AreEqual(route.Id, daily.SelectedRouteId);
            }
            finally
            {
                Object.DestroyImmediate(flag);
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void DailyPlanRouteRequirement_StudentProgressOnlyCompatibilityStillWorks()
        {
            var requirement = ScriptableObject.CreateInstance<EnergyThresholdRouteRequirement>();
            var route = CreateRoute("daily.route.energy", requirement);
            try
            {
                requirement.ConfigureForTests(10);
                var context = new DailyPlanRouteContext(CreateProgress(), null, null, null);

                Assert.IsTrue(route.IsAvailable(in context));
                Assert.IsTrue(route.IsAvailable(CreateProgress()));
            }
            finally
            {
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(route);
            }
        }

        private static DailyPlanRouteDefinition CreateRoute(string id, DailyPlanRouteRequirementBase requirement)
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                id,
                id + ".name",
                null,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                new[] { requirement },
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                id + ".title",
                id + ".detail",
                id + ".next");
            return route;
        }

        private static StudentLifeProgress CreateProgress()
        {
            return new StudentLifeProgress("slot-a", "player-a", 100, 100);
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
                System.Array.Empty<WorldStateSummarySurface>(),
                System.Array.Empty<WorldStateBadgeKind>(),
                0,
                1);
            return flag;
        }

        private sealed class EnergyThresholdRouteRequirement : DailyPlanRouteRequirementBase
        {
            private int _minimumEnergy;

            public void ConfigureForTests(int minimumEnergy)
            {
                _minimumEnergy = minimumEnergy;
            }

            public override bool IsSatisfied(StudentLifeProgress progress, DailyPlanRouteDefinition route)
            {
                return progress != null && progress.Energy >= _minimumEnergy;
            }
        }
    }
}
