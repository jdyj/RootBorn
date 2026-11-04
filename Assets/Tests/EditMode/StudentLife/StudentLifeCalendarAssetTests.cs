using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeCalendarAssetTests
    {
        [Test] public void RuntimeRegistry_HasPersistentCalendarAndRegisteredPolicies()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry.StudentLifeCalendar, Is.Not.Null);
            Assert.That(registry.StudentLifeCalendar.GetBudget(1), Is.EqualTo(2));
            Assert.That(registry.StudentLifeCalendar.GetBudget(6), Is.EqualTo(3));
            Assert.That(registry.ActivityTimePolicies.Length, Is.GreaterThanOrEqualTo(3));
            CheckAddress(registry.StudentLifeCalendar);
            foreach (var policy in registry.ActivityTimePolicies) CheckAddress(policy);
        }

        [Test] public void RegisteredActivities_UseExplicitTimePolicies()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            foreach (var a in registry.LifeActivities) if (a != null) Assert.That(a.TimePolicy, Is.Not.Null, a.name);
            foreach (var a in registry.LocationActivities) if (a != null) Assert.That(a.TimePolicy, Is.Not.Null, a.name);
            foreach (var a in registry.OutsideSchoolActivities) if (a != null) Assert.That(a.TimePolicy, Is.Not.Null, a.name);
            foreach (var a in registry.PartTimeWorks) if (a != null) Assert.That(a.TimePolicy, Is.Not.Null, a.name);
            foreach (var a in registry.TownHelpActions) if (a != null) Assert.That(a.TimePolicy, Is.Not.Null, a.name);
            foreach (var a in registry.CareerPractices) if (a != null && a.Activity != null) Assert.That(a.Activity.TimePolicy.TimePointCost, Is.EqualTo(1), a.name);
        }

        [Test] public void FormalClass_IsFreeOnceOnWeekdays_OutsideSchoolRemainsAvailable()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var lesson = AssetDatabase.LoadAssetAtPath<LocationActivityDefinition>("Assets/Data/StudentLife/LocationIdentities/Activities/LocationActivity_School_Class.asset");
            Assert.That(lesson.TimePolicy, Is.Not.Null);
            Assert.That(lesson.TimePolicy.TimePointCost, Is.Zero);
            Assert.That(lesson.TimePolicy.OncePerDay, Is.True);
            Assert.That(lesson.TimePolicy.IsAllowedDay(1), Is.True);
            Assert.That(lesson.TimePolicy.IsAllowedDay(6), Is.False);
            var p = new StudentLifeProgress("calendar-asset-test", "player", 100, 100);
            p.ConfigureCalendar(registry.StudentLifeCalendar);
            var outside = AssetDatabase.LoadAssetAtPath<OutsideSchoolActivityDefinition>("Assets/Data/StudentLife/OutsideSchool/OutsideActivity_LibrarySelfStudy.asset");
            Assert.That(outside.TimePolicy, Is.Not.Null);
            Assert.That(p.CanUseTimePolicy(outside.TimePolicy), Is.True, "School must not be prerequisite");
        }

        private static void CheckAddress(Object asset)
        {
            Assert.That(asset, Is.Not.Null);
            string path = AssetDatabase.GetAssetPath(asset);
            Assert.That(path, Does.StartWith("Assets/Data/StudentLife/Calendar/"));
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null, path);
            Assert.That(entry.labels, Does.Contain("LifeCalendar"));
        }
    }
}
