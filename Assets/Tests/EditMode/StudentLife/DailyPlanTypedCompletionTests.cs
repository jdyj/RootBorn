using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanTypedCompletionTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private LifeCalendarDefinition _calendar;
        private StudentLifeProgress _student;
        private DailyPlanRouteDefinition _route;
        private DailyPlanRouteSetDefinition _set;
        private CareerPracticeDefinition _practice;
        private PartTimeWorkDefinition _work;
        private LocationActivityDefinition _activity;
        private TraitDefinition _trait;

        [SetUp]
        public void SetUp()
        {
            _calendar = Make<LifeCalendarDefinition>(); _calendar.ConfigureForTests(2, 3);
            _student = new StudentLifeProgress("typed-slot", "typed-player", 100, 100); _student.ConfigureCalendar(_calendar);
            _practice = Make<CareerPracticeDefinition>(); _practice.ConfigureForTests("practice.fixture", "Practice", null, null, null);
            _work = Make<PartTimeWorkDefinition>(); _work.ConfigureForTests("work.fixture", "Work", null, 30, 1, 1, 0, null, null, null);
            _activity = Make<LocationActivityDefinition>(); _activity.ConfigureForTests("activity.fixture", "Activity", null, LocationGrowthRoute.SelfStudy, 30, 0, 0, 0, null, null);
            _trait = Make<TraitDefinition>(); _trait.ConfigureForTests("trait.diary-planning", "Planning");
            var outcome = Make<DailyPlanScheduleTraitOutcome>(); outcome.Trait = _trait;
            _route = Make<DailyPlanRouteDefinition>();
            _route.ConfigureForTests("route.fixture", "Plan", null, LocationGrowthRoute.SelfStudy, new[] { _activity }, null,
                new DailyPlanRouteOutcomeBase[] { outcome }, "Plan title", "Plan detail", "Plan next");
            _set = Make<DailyPlanRouteSetDefinition>(); _set.ConfigureForTests("set.fixture", "Plan set", new[] { _route }, 1);
        }
        [TearDown] public void TearDown() { foreach (var obj in _objects) if (obj != null) Object.DestroyImmediate(obj); _objects.Clear(); }

        [Test]
        public void PracticeMatcher_AcceptsOnlyTheConfiguredScriptableObjectReference()
        {
            ConfigureMatches();
            var other = Make<CareerPracticeDefinition>(); other.ConfigureForTests(_practice.Id, "Other", null, null, null);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_route, "MatchesPractice", _practice), Is.True);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_route, "MatchesPractice", other), Is.False);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_route, "MatchesPractice", new object[] { null }), Is.False);
        }

        [Test]
        public void WorkMatcher_AcceptsOnlyTheConfiguredScriptableObjectReference()
        {
            ConfigureMatches();
            var other = Make<PartTimeWorkDefinition>(); other.ConfigureForTests(_work.Id, "Other", null, 0, 0, 0, 0, null, null, null);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_route, "MatchesWork", _work), Is.True);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_route, "MatchesWork", other), Is.False);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_route, "MatchesWork", new object[] { null }), Is.False);
        }

        [Test]
        public void SuccessfulPractice_CreditsPlannedSlotAndRouteGrowth_WithoutChargingTimeAgain()
        {
            ConfigureMatches(); Assign(0); Assert.That(_student.TrySpendTimePoints(1), Is.True);
            Assert.That(Record("TryRecordPractice", _practice, _student, 1, "practice-complete"), Is.True);
            Assert.That(_student.TimePointsSpentToday, Is.EqualTo(1));
            Assert.That(_student.GetTraitValue(_trait), Is.EqualTo(1));
            var slot = DailyPlanScheduleApi.Values(Owned(), "Slots")[0];
            Assert.That(DailyPlanScheduleApi.Read<string>(slot, "MatchedActivityId"), Is.EqualTo(_practice.Id));
            Assert.That(DailyPlanScheduleApi.Read<object>(slot, "MatchedActivityKind").ToString(), Is.EqualTo("Practice"));
        }

        [Test]
        public void SuccessfulPartTimeWork_CreditsTheTypedWorkSlot_WithoutReplayingTheWorkItself()
        {
            ConfigureMatches(); Assign(0);
            Assert.That(Record("TryRecordPartTimeWork", _work, _student, 1, "work-complete"), Is.True);
            Assert.That(_student.Energy, Is.EqualTo(100)); Assert.That(_student.Focus, Is.EqualTo(100));
            Assert.That(_student.TimePointsSpentToday, Is.Zero, "This adapter only records a completed settlement.");
            var slot = DailyPlanScheduleApi.Values(Owned(), "Slots")[0];
            Assert.That(DailyPlanScheduleApi.Read<string>(slot, "MatchedActivityId"), Is.EqualTo(_work.Id));
            Assert.That(DailyPlanScheduleApi.Read<object>(slot, "MatchedActivityKind").ToString(), Is.EqualTo("PartTimeWork"));
        }

        [Test]
        public void TypedAndLocationAdapters_ShareOneDailyReceiptLedger()
        {
            ConfigureMatches(); Assign(0); Assign(1);
            Assert.That(DailyPlanScheduleApi.Record(Owned(), new[] { _set }, _activity, _student, 1, "same-settlement"), Is.True);
            Assert.That(Record("TryRecordPractice", _practice, _student, 1, "same-settlement"), Is.False);
            Assert.That(Record("TryRecordPartTimeWork", _work, _student, 1, "same-settlement"), Is.False);
            Assert.That(_student.GetTraitValue(_trait), Is.EqualTo(1));
            Assert.That(Record("TryRecordPractice", _practice, _student, 1, "another-settlement"), Is.True);
            Assert.That(_student.GetTraitValue(_trait), Is.EqualTo(2));
        }

        [Test]
        public void TypedCompletionJsonRoundTrip_PreservesKindAndReceiptWithoutPayingAgain()
        {
            ConfigureMatches(); Assign(0);
            Assert.That(Record("TryRecordPartTimeWork", _work, _student, 1, "persisted-work"), Is.True);
            _student = StudentLifeProgress.FromSaveData(JsonUtility.FromJson<StudentLifeProgressSaveData>(JsonUtility.ToJson(_student.ToSaveData())), null, null, null);
            _student.ConfigureCalendar(_calendar);
            Assert.That(Record("TryRecordPartTimeWork", _work, _student, 1, "persisted-work"), Is.False);
            Assert.That(_student.GetTraitValue(_trait), Is.EqualTo(1));
            Assert.That(DailyPlanScheduleApi.Read<object>(DailyPlanScheduleApi.Values(Owned(), "Slots")[0], "MatchedActivityKind").ToString(), Is.EqualTo("PartTimeWork"));
        }

        [Test]
        public void WrongDayOrPlayer_TypedCompletionCannotCreditTheSchedule()
        {
            ConfigureMatches(); Assign(0);
            Assert.That(Record("TryRecordPractice", _practice, _student, 2, "wrong-day"), Is.False);
            var foreign = new StudentLifeProgress("other-slot", "typed-player", 100, 100); foreign.ConfigureCalendar(_calendar);
            Assert.That(Record("TryRecordPartTimeWork", _work, foreign, 1, "wrong-player"), Is.False);
            Assert.That(_student.GetTraitValue(_trait), Is.Zero);
            Assert.That(DailyPlanScheduleApi.Read<bool>(DailyPlanScheduleApi.Values(Owned(), "Slots")[0], "Completed"), Is.False);
        }

        private void ConfigureMatches()
        {
            Set(_route, "_matchingPractices", new[] { _practice }); Set(_route, "_matchingWorks", new[] { _work });
        }
        private DailyPlanProgress Owned() => DailyPlanScheduleApi.Read<DailyPlanProgress>(_student, "DailyPlan");
        private void Assign(int slot) => Assert.That(DailyPlanScheduleApi.Call<bool>(Owned(), "TryAssignSlot", slot, _route, _student), Is.True);
        private bool Record(string methodName, ScriptableObject definition, StudentLifeProgress actor, int day, string key)
        {
            var method = typeof(DailyPlanRouteRunner).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Real facility completion needs a typed diary adapter.");
            return (bool)method.Invoke(new DailyPlanRouteRunner(), new object[] { Owned(), new[] { _set }, definition, actor, day, key, null });
        }
        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Typed diary SO references missing: " + name); field.SetValue(target, value);
        }
    }
}
