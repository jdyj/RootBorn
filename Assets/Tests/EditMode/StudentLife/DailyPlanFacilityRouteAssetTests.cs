using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanFacilityRouteAssetTests
    {
        private const string Root = "Assets/Data/StudentLife/DailyPlans/FacilityPracticeRoutes/";
        private static readonly string[] Careers = { "Chef", "EmergencyCare", "Interior", "Soldier" };
        private static readonly string[] Locations = { "Restaurant", "MedicalClinic", "DesignStudio", "TrainingGround" };
        private static readonly string[] Works = { "CornerStoreShift", "WorkshopHelper" };
        private static readonly string[] WorkRoutes = { "CornerStore", "Workshop" };
        private static GameDataRegistry Registry => Load<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
        private static DailyPlanRouteSetDefinition DefaultSet => Load<DailyPlanRouteSetDefinition>("Assets/Data/StudentLife/DailyPlans/DefaultDailyPlanRouteSet.asset");

        [Test]
        public void FourFacilityPlans_BindActualRegisteredMinigamePractices_AndRemainSchoolFree()
        {
            for (int i = 0; i < Careers.Length; i++)
            {
                var route = Load<DailyPlanRouteDefinition>(Root + "DailyPlan_Practice_" + Careers[i] + ".asset");
                var practice = Load<CareerPracticeDefinition>("Assets/Data/StudentLife/CareerPractices/PracticeS/Practice_" + Careers[i] + ".asset");
                var location = Load<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_" + Locations[i] + ".asset");
                Assert.That(route.TargetLocation, Is.SameAs(location));
                Assert.That(Values<CareerPracticeDefinition>(route, "MatchingPractices"), Does.Contain(practice));
                Assert.That(practice.Minigame, Is.Not.Null);
                CollectionAssert.Contains(Registry.CareerPractices, practice); CollectionAssert.Contains(Registry.Locations, location);
                CollectionAssert.Contains(DefaultSet.Routes, route);
                Assert.That(route.Requirements, Is.Empty, "A core facility plan must be selectable without school-only gates.");
                Assert.That(route.IsAvailable(new StudentLifeProgress("slot", "player", 100, 100)), Is.True);
                Assert.That(route.MatchingActivities, Is.Empty, "An introduction cannot substitute for the planned minigame.");
            }
        }

        [Test]
        public void TwoFirstJobPlans_BindActualPayingWorkAndItsRegisteredLocation()
        {
            for (int i = 0; i < Works.Length; i++)
            {
                var route = Load<DailyPlanRouteDefinition>(Root + "DailyPlan_Work_" + WorkRoutes[i] + ".asset");
                var work = Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_" + Works[i] + ".asset");
                Assert.That(Values<PartTimeWorkDefinition>(route, "MatchingWorks"), Does.Contain(work));
                Assert.That(work.Minigame, Is.Not.Null); Assert.That(work.Rewards.Any(x => x.Count > 0), Is.True);
                Assert.That(route.TargetLocation.Id, Is.EqualTo(work.Workplace.LocationId));
                CollectionAssert.Contains(Registry.PartTimeWorks, work); CollectionAssert.Contains(DefaultSet.Routes, route);
                Assert.That(route.IsAvailable(new StudentLifeProgress("slot", "player", 100, 100)), Is.True);
            }
        }

        [Test]
        public void DefaultRouteSet_PreservesAllExistingAlternatives_AndAddsEachNewPlanExactlyOnce()
        {
            string[] legacy = { "LibrarySelfStudyRoute", "TownSquareHelpRoute", "WorkshopShiftRoute", "LibraryArchiveResearchRoute",
                "WorkshopRepairLedgerRoute", "QuestChainLibraryRewardRoute", "CareerInteriorPracticeRoute" };
            foreach (string name in legacy)
            {
                var route = Load<DailyPlanRouteDefinition>("Assets/Data/StudentLife/DailyPlans/" + name + ".asset");
                Assert.That(DefaultSet.Routes.Count(x => x == route), Is.EqualTo(1), name);
            }
            foreach (string path in NewPaths())
            {
                var route = Load<DailyPlanRouteDefinition>(path);
                Assert.That(DefaultSet.Routes.Count(x => x == route), Is.EqualTo(1), path);
            }
            Assert.That(DefaultSet.Routes.Where(x => x != null).Select(x => x.Id).Distinct().Count(), Is.EqualTo(DefaultSet.Routes.Count));
            Assert.That(Registry.DailyPlanRouteSets.Count(x => x == DefaultSet), Is.EqualTo(1));
        }

        [Test]
        public void TypedOnlyPlan_DoesNotCreditAnUnlistedActivityJustBecauseLocationAndGrowthRouteMatch()
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            var practice = ScriptableObject.CreateInstance<CareerPracticeDefinition>();
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var introduction = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            try
            {
                route.ConfigureForTests("route.typed-only", "Plan", location, LocationGrowthRoute.Exploration, null, null, null, "Title", "Detail", "Next");
                var field = typeof(DailyPlanRouteDefinition).GetField("_matchingPractices", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null); field.SetValue(route, new[] { practice });
                introduction.ConfigureForTests("activity.introduction", "Introduction", location, LocationGrowthRoute.Exploration, 0, 0, 0, 0, null, null);
                Assert.That(route.Matches(introduction), Is.False, "The actual minigame is required; generic location fallback must not complete a typed-only plan.");
            }
            finally { Object.DestroyImmediate(route); Object.DestroyImmediate(practice); Object.DestroyImmediate(location); Object.DestroyImmediate(introduction); }
        }

        [Test]
        public void SixAuthoredFacilityPlans_HaveUniqueStableIdsAndLocalDataAddresses()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings; Assert.That(settings, Is.Not.Null);
            var ids = new System.Collections.Generic.HashSet<string>(); var addresses = new System.Collections.Generic.HashSet<string>();
            foreach (string path in NewPaths())
            {
                var route = Load<DailyPlanRouteDefinition>(path); Assert.That(ids.Add(route.Id), Is.True);
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)); Assert.That(entry, Is.Not.Null, path);
                Assert.That(entry.parentGroup.Name, Is.EqualTo("Data")); StringAssert.StartsWith("data/", entry.address);
                Assert.That(addresses.Add(entry.address), Is.True); Assert.That(route.ObjectiveDetailKey, Is.Not.Empty); Assert.That(route.NextHintKey, Is.Not.Empty);
            }
        }

        private static string[] NewPaths() => Careers.Select(x => Root + "DailyPlan_Practice_" + x + ".asset")
            .Concat(WorkRoutes.Select(x => Root + "DailyPlan_Work_" + x + ".asset")).ToArray();
        private static T[] Values<T>(object target, string name) => ((IEnumerable)DailyPlanScheduleApi.Read<object>(target, name)).Cast<T>().ToArray();
        private static T Load<T>(string path) where T : Object { var value = AssetDatabase.LoadAssetAtPath<T>(path); Assert.That(value, Is.Not.Null, path); return value; }
    }
}
