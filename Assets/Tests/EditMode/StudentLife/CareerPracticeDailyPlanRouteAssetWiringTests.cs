using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
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
        private const string CareerRouteE2EPath = "Assets/Tests/PlayMode/EndToEnd/CareerDailyPlanRouteE2EScenarioTests.cs";
        private const string CareerJobRouteE2EPath = "Assets/Tests/PlayMode/EndToEnd/CareerJobRouteE2EScenarioTests.cs";
        private const string RequirementTypeName = "Rootborn.Game.StudentLife.DailyPlanCareerHintRequirement, Rootborn.Game";
        private const string WorkRequirementTypeName = "Rootborn.Game.StudentLife.DailyPlanPartTimeWorkRequirement, Rootborn.Game";
        private const string CareerId = "career.interior";
        private const string RouteId = "daily.route.career.interior-practice";
        private const string WorkshopRouteId = "daily.workshop.shift";
        private const string CornerStoreWorkId = "work.shift.corner-store";

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
        public void DailyPlanPartTimeWorkRequirement_ReturnsFalseBeforeWorkAndTrueAfterWorkLog()
        {
            var work = CreateWork(CornerStoreWorkId);
            var requirement = CreateWorkRequirement(work, true);
            var route = CreateRoute(WorkshopRouteId, requirement);
            var progress = new StudentLifeProgress("slot-work-route", "player-work-route", 100, 100);
            var inventory = new Inventory();
            var runner = new PartTimeWorkRunner();
            try
            {
                Assert.IsFalse(requirement.IsSatisfied(new DailyPlanRouteContext(progress), route), "Workshop route must be locked before the configured work is completed.");

                Assert.IsTrue(runner.TryPerform(work, progress, inventory, "work-route-request", out var result));
                Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);

                Assert.IsTrue(requirement.IsSatisfied(new DailyPlanRouteContext(progress), route), "Workshop route must unlock from the data-defined part-time work completion log.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(work.Workplace);
                UnityEngine.Object.DestroyImmediate(work);
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
        public void RegisteredWorkshopShiftRoute_IsGatedByCornerStorePartTimeWork()
        {
            var registry = LoadRegistry();
            var work = FindWork(registry, CornerStoreWorkId);
            var route = FindRoute(registry, WorkshopRouteId);

            Assert.IsNotNull(work, "Corner Store part-time work must be registered.");
            Assert.IsNotNull(route, "Workshop/job route must be registered in a route set.");
            Assert.IsTrue(route.Requirements.Any(requirement => RequirementReferencesWork(requirement, work)), "Workshop/job route must have a DailyPlanPartTimeWorkRequirement referencing the data-defined part-time work.");

            var locked = new DailyPlanRouteContext(new StudentLifeProgress("slot-work-route", "player-work-route", 100, 100));
            Assert.IsFalse(route.IsAvailable(in locked), "Workshop/job route must be unavailable before the configured part-time work is completed.");

            var progress = new StudentLifeProgress("slot-work-route", "player-work-route", 100, 100);
            var inventory = new Inventory();
            Assert.IsTrue(new PartTimeWorkRunner().TryPerform(work, progress, inventory, "registered-work-route-request", out var result));
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            var unlocked = new DailyPlanRouteContext(progress);
            Assert.IsTrue(route.IsAvailable(in unlocked), "Workshop/job route must be available after the configured part-time work is completed.");
        }

        [Test]
        public void RegisteredCareerPracticeRoute_HasReachableTargetAndObjectiveText()
        {
            var registry = LoadRegistry();
            var route = FindRoute(registry, RouteId);

            AssertRouteIsReadyForDailyPlan(registry, route, "Career-gated route");
        }

        [Test]
        public void RegisteredWorkshopShiftRoute_HasReachableTargetAndObjectiveText()
        {
            var registry = LoadRegistry();
            var route = FindRoute(registry, WorkshopRouteId);

            AssertRouteIsReadyForDailyPlan(registry, route, "Workshop/job route");
        }

        [Test]
        public void CareerDailyPlanRouteE2E_ContainsExactOnceSavedStateAssertions()
        {
            string source = File.ReadAllText(CareerRouteE2EPath);

            StringAssert.Contains(
                "AssertSaveContainsExactlyOnce",
                source,
                "Career Daily Plan E2E must assert StudentLife and DailyPlan save files contain career/route state exactly once, not merely that they contain it.");
            StringAssert.Contains(
                "CountOccurrences",
                source,
                "Career Daily Plan E2E exact-once assertions must use an explicit occurrence counter so duplicate serialized route or hint records fail visibly.");
            StringAssert.Contains(
                "AssertObjectiveRowsContainExactlyOnce",
                source,
                "Career Daily Plan E2E must assert Objective Journal rows do not duplicate the selected career route before or after reload.");
        }

        [Test]
        public void CareerJobRouteE2E_ContainsExactOnceSavedStateAssertions()
        {
            string source = File.ReadAllText(CareerJobRouteE2EPath);

            StringAssert.Contains(
                "AssertSaveContainsExactlyOnce",
                source,
                "Career/Job route E2E must assert StudentLife and DailyPlan save files contain work/route state exactly once, not merely that they contain it.");
            StringAssert.Contains(
                "CountOccurrences",
                source,
                "Career/Job route E2E exact-once assertions must use an explicit occurrence counter so duplicate serialized work or route records fail visibly.");
            StringAssert.DoesNotContain(
                "AssertSaveContains(",
                source,
                "Career/Job route E2E must not rely on contains-only save assertions.");
            StringAssert.Contains(
                "Daily Plan save must include selected workshop route exactly once",
                source,
                "Career/Job route E2E must exact-once check selected workshop route save state before reload.");
            StringAssert.Contains(
                "reload duplicated or removed the selected workshop route save state",
                source,
                "Career/Job route E2E must exact-once check selected workshop route save state after reload.");
            StringAssert.Contains(
                "StudentLife save must include part-time work progress exactly once",
                source,
                "Career/Job route E2E must exact-once check part-time work save state before reload.");
            StringAssert.Contains(
                "reload duplicated or removed the part-time work save state",
                source,
                "Career/Job route E2E must exact-once check part-time work save state after reload.");
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
                Assert.AreEqual(1, unselected["Hints"].Count(item => item.StableKey == "daily-plan-route-hint:" + route.Id), "Unlocked unselected career route hint must be emitted exactly once.");

                daily.RestoreFromSaveData(new DailyPlanProgressSaveData { SaveSlot = "slot-career-route-objective", PlayerId = "player-career-route-objective", CurrentDay = 1, SelectedRouteId = route.Id }, "slot-career-route-objective", "player-career-route-objective", 1);
                var selected = aggregator.Build(context);

                Assert.AreEqual(1, selected["Goals"].Count(item => item.StableKey == "daily-plan:" + route.Id), "Selected career route goal must be emitted exactly once.");
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

        private static PartTimeWorkDefinition FindWork(GameDataRegistry registry, string id)
        {
            return registry.PartTimeWorks.FirstOrDefault(work => work != null && work.Id == id);
        }

        private static DailyPlanRouteDefinition FindRoute(GameDataRegistry registry, string id)
        {
            return registry.DailyPlanRouteSets
                .Where(set => set != null)
                .SelectMany(set => set.Routes)
                .FirstOrDefault(route => route != null && route.Id == id);
        }

        private static void AssertRouteIsReadyForDailyPlan(GameDataRegistry registry, DailyPlanRouteDefinition route, string label)
        {
            Assert.IsNotNull(route, label + " must be registered in a route set.");
            Assert.IsNotNull(route.TargetLocation, label + " must target an existing location.");
            CollectionAssert.Contains(registry.Locations, route.TargetLocation, label + " target location must be registered.");
            Assert.IsTrue(route.MatchingActivities.Count > 0 || route.TargetLocation != null, label + " must be matchable by activity or target location route.");
            if (route.MatchingActivities.Count > 0)
            {
                Assert.IsTrue(route.MatchingActivities.All(activity => activity != null && registry.LocationActivities.Contains(activity)), label + " matching activities must be registered.");
            }

            Assert.IsFalse(string.IsNullOrEmpty(route.ObjectiveTitleKey), label + " must expose Objective Journal title text key.");
            Assert.IsFalse(string.IsNullOrEmpty(route.ObjectiveDetailKey), label + " must expose Objective Journal detail text key.");
            Assert.IsFalse(string.IsNullOrEmpty(route.NextHintKey), label + " must expose Objective Journal next hint text key.");
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

        private static DailyPlanRouteRequirementBase CreateWorkRequirement(PartTimeWorkDefinition work, bool expectedCompleted)
        {
            var type = Type.GetType(WorkRequirementTypeName);
            Assert.IsNotNull(type, WorkRequirementTypeName + " must exist.");
            var requirement = ScriptableObject.CreateInstance(type) as DailyPlanRouteRequirementBase;
            Assert.IsNotNull(requirement, WorkRequirementTypeName + " must derive from DailyPlanRouteRequirementBase.");
            var method = type.GetMethod("ConfigureForTests", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(PartTimeWorkDefinition), typeof(bool) }, null);
            Assert.IsNotNull(method, WorkRequirementTypeName + " must expose ConfigureForTests(PartTimeWorkDefinition, bool) for test wiring.");
            method.Invoke(requirement, new object[] { work, expectedCompleted });
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

        private static bool RequirementReferencesWork(DailyPlanRouteRequirementBase requirement, PartTimeWorkDefinition work)
        {
            if (requirement == null || work == null) return false;
            var type = Type.GetType(WorkRequirementTypeName);
            if (type == null || !type.IsInstanceOfType(requirement)) return false;
            var field = type.GetField("_work", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && ReferenceEquals(field.GetValue(requirement), work);
        }

        private static CareerDefinition CreateCareer(string id)
        {
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests(id, id + ".name", Array.Empty<CareerUnlockRequirementBase>());
            return career;
        }

        private static PartTimeWorkDefinition CreateWork(string id)
        {
            var workplace = ScriptableObject.CreateInstance<WorkplaceDefinition>();
            workplace.ConfigureForTests("workplace.corner-store", "workplace.corner-store", "location.town-store", "npc.first-guide", Array.Empty<PartTimeWorkDefinition>());
            var work = ScriptableObject.CreateInstance<PartTimeWorkDefinition>();
            work.ConfigureForTests(id, id, workplace, 45, 0, 0, 0, Array.Empty<InventoryGrant>(), Array.Empty<WorkRequirementBase>(), Array.Empty<WorkOutcomeBase>());
            return work;
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
