using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Objectives;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class CareerPracticeDailyPlanRouteAssetWiringTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string RequirementTypeName = "Rootborn.Game.StudentLife.DailyPlanCareerHintRequirement, Rootborn.Game";
        private const string CareerId = "career.interior";
        private const string RouteId = "daily.route.career.interior-practice";

        [Test]
        public void DailyPlanCareerHintRequirement_ReturnsFalseBeforeCareerHintUnlock()
        {
            var career = CreateCareer(CareerId);
            var requirement = CreateRequirement(career, true);
            var route = CreateRoute(RouteId, requirement);
            try
            {
                var context = new DailyPlanRouteContext(new StudentLifeProgress("slot-career-route", "player-career-route", 100, 100));

                Assert.IsFalse(requirement.IsSatisfied(in context, route));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(career);
                UnityEngine.Object.DestroyImmediate(requirement);
                UnityEngine.Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void DailyPlanCareerHintRequirement_ReturnsTrueAfterCareerHintUnlock()
        {
            var career = CreateCareer(CareerId);
            var requirement = CreateRequirement(career, true);
            var route = CreateRoute(RouteId, requirement);
            try
            {
                var progress = new StudentLifeProgress("slot-career-route", "player-career-route", 100, 100);
                progress.UnlockCareerHint(career);
                var context = new DailyPlanRouteContext(progress);

                Assert.IsTrue(requirement.IsSatisfied(in context, route));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(career);
                UnityEngine.Object.DestroyImmediate(requirement);
                UnityEngine.Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void RegisteredCareerPracticeRoute_IsGatedByInteriorCareerHint()
        {
            var registry = LoadRegistry();
            var career = FindCareer(registry, CareerId);
            var route = FindRoute(registry, RouteId);

            Assert.IsNotNull(career, "Interior career must be registered.");
            Assert.IsNotNull(route, "Career-gated Daily Plan route must be registered in a route set.");
            Assert.IsTrue(route.Requirements.Any(requirement => RequirementReferencesCareer(requirement, career)), "Career-gated route must have a DailyPlanCareerHintRequirement referencing the interior career.");

            var locked = new DailyPlanRouteContext(new StudentLifeProgress("slot-career-route", "player-career-route", 100, 100));
            Assert.IsFalse(route.IsAvailable(in locked), "Career-gated route must be unavailable before the career hint is unlocked.");

            var progress = new StudentLifeProgress("slot-career-route", "player-career-route", 100, 100);
            progress.UnlockCareerHint(career);
            var unlocked = new DailyPlanRouteContext(progress);
            Assert.IsTrue(route.IsAvailable(in unlocked), "Career-gated route must be available after the career hint is unlocked.");
        }

        [Test]
        public void RegisteredCareerPracticeRoute_HasReachableTargetAndObjectiveText()
        {
            var registry = LoadRegistry();
            var route = FindRoute(registry, RouteId);

            Assert.IsNotNull(route);
            Assert.IsNotNull(route.TargetLocation, "Career-gated route must target an existing location.");
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
        public void ObjectiveJournal_ExposesUnlockedUnselectedCareerRouteAsHintThenMovesSelectedRouteToGoals()
        {
            string rootDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-career-route-objective-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(rootDirectory);
            var registry = LoadRegistry();
            var career = FindCareer(registry, CareerId);
            var route = FindRoute(registry, RouteId);
            var player = new GameObject("Player");

            try
            {
                Assert.IsNotNull(career);
                Assert.IsNotNull(route);
                var student = player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-career-route-objective", "player-career-route-objective", 100, 100, 0, 480);
                student.EnsureProgress().UnlockCareerHint(career);
                player.AddComponent<PlayerIdentity>().Configure("player-career-route-objective");
                var daily = player.AddComponent<DailyPlanProgressComponent>();
                daily.RestoreFromSaveData(new DailyPlanProgressSaveData { SaveSlot = "slot-career-route-objective", PlayerId = "player-career-route-objective", CurrentDay = 1 }, "slot-career-route-objective", "player-career-route-objective", 1);
                var context = new ObjectiveJournalContext(registry, player, new SaveSlotMetadata { SlotId = "slot-career-route-objective", DisplayName = "Career Route" });
                var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[] { new DailyPlanObjectiveSource(), new DailyPlanRouteHintObjectiveSource() });

                var unselected = aggregator.Build(context);
                Assert.AreEqual("daily-plan:choose", unselected["Goals"][0].StableKey);
                Assert.IsTrue(unselected["Hints"].Any(item => item.StableKey == "daily-plan-route-hint:" + route.Id));

                daily.RestoreFromSaveData(new DailyPlanProgressSaveData { SaveSlot = "slot-career-route-objective", PlayerId = "player-career-route-objective", CurrentDay = 1, SelectedRouteId = route.Id }, "slot-career-route-objective", "player-career-route-objective", 1);
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

        private static CareerDefinition FindCareer(GameDataRegistry registry, string id)
        {
            return registry.Careers.FirstOrDefault(career => career != null && career.Id == id);
        }

        private static DailyPlanRouteDefinition FindRoute(GameDataRegistry registry, string id)
        {
            return registry.DailyPlanRouteSets
                .Where(set => set != null)
                .SelectMany(set => set.Routes)
                .FirstOrDefault(route => route != null && route.Id == id);
        }

        private static DailyPlanRouteRequirementBase CreateRequirement(CareerDefinition career, bool expectedUnlocked)
        {
            var type = Type.GetType(RequirementTypeName);
            Assert.IsNotNull(type, RequirementTypeName + " must exist.");
            var requirement = ScriptableObject.CreateInstance(type) as DailyPlanRouteRequirementBase;
            Assert.IsNotNull(requirement, RequirementTypeName + " must derive from DailyPlanRouteRequirementBase.");
            var method = type.GetMethod("ConfigureForTests", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(CareerDefinition), typeof(bool) }, null);
            Assert.IsNotNull(method, RequirementTypeName + " must expose ConfigureForTests(CareerDefinition, bool) for test wiring.");
            method.Invoke(requirement, new object[] { career, expectedUnlocked });
            return requirement;
        }

        private static bool RequirementReferencesCareer(DailyPlanRouteRequirementBase requirement, CareerDefinition career)
        {
            if (requirement == null || career == null) return false;
            var type = Type.GetType(RequirementTypeName);
            if (type == null || !type.IsInstanceOfType(requirement)) return false;
            var field = type.GetField("_career", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && ReferenceEquals(field.GetValue(requirement), career);
        }

        private static CareerDefinition CreateCareer(string id)
        {
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests(id, id + ".name", Array.Empty<CareerUnlockRequirementBase>());
            return career;
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
    }
}
