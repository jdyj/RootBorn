using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeTimePointTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown] public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [TestCase(1, 2)] [TestCase(5, 2)] [TestCase(6, 3)] [TestCase(7, 3)] [TestCase(8, 2)]
        public void Calendar_UsesWeekdayAndWeekendBudgets(int day, int expected)
        {
            var calendar = Calendar();
            Assert.That(Call(calendar, "GetBudget", day), Is.EqualTo(expected));
        }

        [Test] public void Weekday_ThirdPaidActionIsRejectedWithoutChangingAnyProgress()
        {
            var p = Progress();
            var action = Activity(Policy("paid", 1, 127, false));
            var runner = new LifeActivityRunner();
            Assert.That(runner.TryPerform(action, p, "a", out _), Is.True);
            Assert.That(runner.TryPerform(action, p, "b", out _), Is.True);
            string before = JsonUtility.ToJson(p.ToSaveData());
            Assert.That(runner.TryPerform(action, p, "c", out _), Is.False);
            Assert.That(JsonUtility.ToJson(p.ToSaveData()), Is.EqualTo(before));
            Assert.That(Get(p, "TimePointsRemaining"), Is.EqualTo(0));
        }

        [Test] public void DuplicateRequest_DoesNotSpendAnotherTimePoint()
        {
            var p = Progress();
            var action = Activity(Policy("paid", 1, 127, false));
            var runner = new LifeActivityRunner();
            Assert.That(runner.TryPerform(action, p, "a", out _), Is.True);
            Assert.That(runner.TryPerform(action, p, "a", out var result), Is.False);
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(Get(p, "TimePointsRemaining"), Is.EqualTo(1));
        }

        [Test] public void SchoolPolicy_IsFreeOncePerWeekday_AndDoesNotGateOutsideGrowth()
        {
            var p = Progress();
            var school = Activity(Policy("morning", 0, 31, true));
            var outside = Activity(Policy("paid", 1, 127, false));
            var runner = new LifeActivityRunner();
            Assert.That(runner.TryPerform(outside, p, "outside-first", out _), Is.True);
            Assert.That(runner.TryPerform(school, p, "school", out _), Is.True);
            Assert.That(Get(p, "TimePointsRemaining"), Is.EqualTo(1));
            Assert.That(runner.TryPerform(school, p, "school-again", out _), Is.False);
            var restored = Restore(p);
            Assert.That(runner.TryPerform(school, restored, "school-after-load", out _), Is.False);
        }

        [Test] public void Weekend_HasThreeActions_AndDisallowsWeekdaySchool()
        {
            var p = Progress();
            for (int i = 0; i < 5; i++) NextDay(p);
            Assert.That(p.CurrentDay, Is.EqualTo(6));
            var school = Activity(Policy("morning", 0, 31, true));
            var paid = Activity(Policy("paid", 1, 127, false));
            var runner = new LifeActivityRunner();
            Assert.That(runner.TryPerform(school, p, "school", out _), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(runner.TryPerform(paid, p, "work-" + i, out _), Is.True);
            Assert.That(runner.TryPerform(paid, p, "fourth", out _), Is.False);
        }

        [Test] public void SaveRoundTrip_AndCalendarRebind_DoNotRefillBudget_NextDayDoes()
        {
            var p = Progress();
            Assert.That(Call(p, "TrySpendTimePoints", 1), Is.EqualTo(true));
            var restored = Restore(p);
            Call(restored, "ConfigureCalendar", Calendar());
            Assert.That(Get(restored, "TimePointsRemaining"), Is.EqualTo(1));
            NextDay(restored);
            Assert.That(Get(restored, "TimePointsRemaining"), Is.EqualTo(2));
            Assert.That(Call(restored, "TrySpendTimePoints", -1), Is.EqualTo(false));
            Assert.That(Get(restored, "TimePointsRemaining"), Is.EqualTo(2));
        }

        [Test] public void SettledDay_RejectsPaidAndFreeActivitiesUntilNextDay()
        {
            var p = Progress();
            p.TryEndDay(null, out _);
            string before = JsonUtility.ToJson(p.ToSaveData());
            var runner = new LifeActivityRunner();
            Assert.That(runner.TryPerform(Activity(Policy("paid", 1, 127, false)), p, "paid", out _), Is.False);
            Assert.That(runner.TryPerform(Activity(Policy("free", 0, 127, false)), p, "free", out _), Is.False);
            Assert.That(JsonUtility.ToJson(p.ToSaveData()), Is.EqualTo(before));
        }

        [Test] public void LegacySaveWithoutTimePointFields_StartsWithAvailableBudget()
        {
            var dto = JsonUtility.FromJson<StudentLifeProgressSaveData>("{\"SaveSlot\":\"old\",\"PlayerId\":\"p\",\"CurrentDay\":6,\"Energy\":20,\"Focus\":20}");
            var p = StudentLifeProgress.FromSaveData(dto, null, null, null);
            Call(p, "ConfigureCalendar", Calendar());
            Assert.That(Get(p, "TimePointsRemaining"), Is.EqualTo(3));
        }

        [Test] public void CostlyActionWithoutCalendar_FailsClosed()
        {
            var p = new StudentLifeProgress("test", "p", 100, 100);
            Assert.That(Call(p, "TrySpendTimePoints", 1), Is.EqualTo(false));
        }

        [TestCase("LocationActivityDefinition", "LocationActivityRunner")]
        [TestCase("OutsideSchoolActivityDefinition", "OutsideSchoolActivityRunner")]
        [TestCase("TownHelpActionDefinition", "TownHelpActionRunner")]
        [TestCase("PartTimeWorkDefinition", "PartTimeWorkRunner")]
        public void EveryPaidActivityEntryPoint_UsesTheSharedBudget(string definitionName, string runnerName)
        {
            var p = Progress();
            var policy = Policy("shared", 1, 127, false);
            var activity = Create(definitionName);
            var so = new SerializedObject(activity);
            var field = so.FindProperty("_timePolicy");
            Assert.That(field, Is.Not.Null);
            field.objectReferenceValue = policy;
            so.ApplyModifiedPropertiesWithoutUndo();
            var type = typeof(StudentLifeProgress).Assembly.GetType("Rootborn.Game.StudentLife." + runnerName);
            var runner = Activator.CreateInstance(type);
            var method = type.GetMethod("TryPerform");
            var inventory = new Inventory();
            for (int i = 0; i < 3; i++)
            {
                object[] args = runnerName == "PartTimeWorkRunner"
                    ? new object[] { activity, p, inventory, "step-" + i, null }
                    : new object[] { activity, p, "step-" + i, null };
                string before = JsonUtility.ToJson(p.ToSaveData());
                Assert.That((bool)method.Invoke(runner, args), Is.EqualTo(i < 2));
                if (i == 2) Assert.That(JsonUtility.ToJson(p.ToSaveData()), Is.EqualTo(before));
            }
            Assert.That(Get(p, "TimePointsSpentToday"), Is.EqualTo(2));
        }

        private StudentLifeProgress Progress()
        {
            var p = new StudentLifeProgress("tp-test", "p", 100, 100);
            Call(p, "ConfigureCalendar", Calendar());
            return p;
        }
        private StudentLifeProgress Restore(StudentLifeProgress p)
        {
            var result = StudentLifeProgress.FromSaveData(JsonUtility.FromJson<StudentLifeProgressSaveData>(JsonUtility.ToJson(p.ToSaveData())), null, null, null);
            Call(result, "ConfigureCalendar", Calendar());
            return result;
        }
        private static void NextDay(StudentLifeProgress p)
        {
            Assert.That(p.TryEndDay(null, out _), Is.True);
            Assert.That(p.TryStartNextDay(), Is.True);
        }
        private ScriptableObject Calendar()
        {
            var calendar = Create("LifeCalendarDefinition");
            Call(calendar, "ConfigureForTests", 2, 3);
            return calendar;
        }
        private ScriptableObject Policy(string id, int cost, int mask, bool once)
        {
            var rule = Create("ActivityTimePolicyDefinition");
            Call(rule, "ConfigureForTests", id, cost, mask, once);
            return rule;
        }
        private LifeActivityDefinition Activity(ScriptableObject policy)
        {
            var action = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            _created.Add(action);
            action.ConfigureForTests(Guid.NewGuid().ToString("N"), "test", LifeActivityCategory.SelfStudy, 30, 1, 1, 0, null, null);
            var so = new SerializedObject(action);
            var property = so.FindProperty("_timePolicy");
            Assert.That(property, Is.Not.Null, "Activity must carry a data-authored time policy");
            property.objectReferenceValue = policy;
            so.ApplyModifiedPropertiesWithoutUndo();
            return action;
        }
        private ScriptableObject Create(string name)
        {
            var type = typeof(StudentLifeProgress).Assembly.GetType("Rootborn.Game.StudentLife." + name);
            Assert.That(type, Is.Not.Null, name + " is missing");
            var result = ScriptableObject.CreateInstance(type);
            _created.Add(result);
            return result;
        }
        private static object Get(object target, string name)
        {
            var property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name + " is missing");
            return property.GetValue(target);
        }
        private static object Call(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, name + " is missing");
            return method.Invoke(target, args);
        }
    }
}
