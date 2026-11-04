using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanScheduleTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private LifeCalendarDefinition _calendar;
        private StudentLifeProgress _student;
        private DailyPlanProgress _daily;
        private LocationActivityDefinition _activity;
        private DailyPlanRouteDefinition _route;
        private DailyPlanRouteDefinition _other;
        private DailyPlanRouteSetDefinition _set;
        private TraitDefinition _trait;

        [SetUp]
        public void SetUp()
        {
            _calendar = Make<LifeCalendarDefinition>(); _calendar.ConfigureForTests(2, 3);
            _student = new StudentLifeProgress("schedule-slot", "schedule-player", 100, 100);
            _student.ConfigureCalendar(_calendar);
            _daily = new DailyPlanProgress(_student.SaveSlot, _student.PlayerId, 1);
            _activity = Make<LocationActivityDefinition>();
            _activity.ConfigureForTests("activity.study", "Study", null, LocationGrowthRoute.SelfStudy, 30, 0, 0, 0,
                Array.Empty<LocationActivityRequirementBase>(), Array.Empty<LocationActivityOutcomeBase>());
            _trait = Make<TraitDefinition>(); _trait.ConfigureForTests("trait.focused", "Focused");
            var outcome = Make<DailyPlanScheduleTraitOutcome>(); outcome.Trait = _trait;
            _route = Route("route.study", _activity, outcome);
            _other = Route("route.alternative", _activity, outcome);
            _set = Make<DailyPlanRouteSetDefinition>(); _set.ConfigureForTests("set", "Plans", new[] { _route, _other }, 1);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects) if (obj != null) Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void WeekdayCalendar_CreatesAfterSchoolAndEveningSlots_WithoutSpendingTime()
        {
            Sync();
            Assert.That(DailyPlanScheduleApi.Read<int>(_daily, "SlotCount"), Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "daily-plan.period.after-school", "daily-plan.period.evening" },
                Slots().Select(x => DailyPlanScheduleApi.Read<string>(x, "PeriodKey")).ToArray());
            Assert.That(_student.TimePointsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void WeekendCalendar_CreatesMorningAfternoonAndEveningSlots()
        {
            AdvanceStudentTo(6); Sync();
            Assert.That(_daily.CurrentDay, Is.EqualTo(6));
            Assert.That(DailyPlanScheduleApi.Read<int>(_daily, "SlotCount"), Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "daily-plan.period.morning", "daily-plan.period.afternoon", "daily-plan.period.evening" },
                Slots().Select(x => DailyPlanScheduleApi.Read<string>(x, "PeriodKey")).ToArray());
        }

        [Test]
        public void AssigningTwoRoutes_PreservesBothSlots_AndRejectsOutOfRange()
        {
            Sync();
            Assert.That(Assign(0, _route), Is.True);
            Assert.That(Assign(1, _other), Is.True);
            Assert.That(Assign(2, _route), Is.False);
            CollectionAssert.AreEqual(new[] { _route.Id, _other.Id }, Slots().Select(x => DailyPlanScheduleApi.Read<string>(x, "RouteId")).ToArray());
            Assert.That(_student.TimePointsSpentToday, Is.Zero, "Planning does not consume an activity.");
        }

        [Test]
        public void EditingPendingSlot_DoesNotEraseCompletedSlotOrReceipt()
        {
            Sync(); Assign(0, _route); Assign(1, _route);
            Assert.That(Record(1, "visit-a"), Is.True);
            Assert.That(Assign(1, _other), Is.True);
            Assert.That(DailyPlanScheduleApi.Read<bool>(Slots()[0], "Completed"), Is.True);
            Assert.That(DailyPlanScheduleApi.Read<string>(Slots()[0], "CompletionRequestKey"), Is.EqualTo("visit-a"));
            Assert.That(Record(1, "visit-a"), Is.False);
            Assert.That(_student.GetTraitValue(_trait), Is.EqualTo(1));
        }

        [Test]
        public void CompletedSlot_CannotBeReassignedToResetItsReward()
        {
            Sync(); Assign(0, _route); Record(1, "visit-a");
            Assert.That(Assign(0, _other), Is.False);
            Assert.That(Assign(0, _route), Is.False);
            Assert.That(DailyPlanScheduleApi.Read<string>(Slots()[0], "RouteId"), Is.EqualTo(_route.Id));
        }

        [Test]
        public void RepeatedPractice_RequiresSeparateEvents_AndCannotExceedScheduledSlots()
        {
            Sync(); Assign(0, _route); Assign(1, _route);
            Assert.That(Record(1, "practice-1"), Is.True);
            Assert.That(CompletedCount(), Is.EqualTo(1), "A single event cannot fill two matching slots.");
            Assert.That(Record(1, "practice-1"), Is.False);
            Assert.That(Record(1, "practice-2"), Is.True);
            Assert.That(Record(1, "practice-3"), Is.False);
            Assert.That(CompletedCount(), Is.EqualTo(2));
            Assert.That(_student.GetTraitValue(_trait), Is.EqualTo(2));
        }

        [Test]
        public void PlanCredit_DoesNotChargeTimeAgainForAnAlreadySuccessfulActivity()
        {
            Sync(); Assign(0, _route);
            Assert.That(_student.TrySpendTimePoints(1), Is.True);
            Assert.That(Record(1, "already-paid"), Is.True);
            Assert.That(_student.TimePointsSpentToday, Is.EqualTo(1));
        }

        [Test]
        public void NonMatchingActivity_DoesNotConsumeSlotOrReceipt()
        {
            Sync(); Assign(0, _route);
            var different = Make<LocationActivityDefinition>();
            different.ConfigureForTests("activity.rest", "Rest", null, LocationGrowthRoute.Rest, 0, 0, 0, 0,
                Array.Empty<LocationActivityRequirementBase>(), Array.Empty<LocationActivityOutcomeBase>());
            Assert.That(DailyPlanScheduleApi.Record(_daily, new[] { _set }, different, _student, 1, "event-x"), Is.False);
            Assert.That(Record(1, "event-x"), Is.True);
        }

        [Test]
        public void ResultReadyDay_RejectsPlanningAndLateCompletion()
        {
            Sync(); Assign(0, _route);
            Assert.That(_student.TryEndDay(null, out _), Is.True);
            Assert.That(Assign(1, _other), Is.False);
            Assert.That(Record(1, "late-event"), Is.False);
            Assert.That(_student.GetTraitValue(_trait), Is.Zero);
        }

        [Test]
        public void Rollover_ArchivesCompletedAndIncompleteChoicesExactlyOnce()
        {
            Sync(); Assign(0, _route); Assign(1, _other); Record(1, "day-one");
            AdvanceStudentTo(2); Sync(); Sync();
            var diary = DailyPlanScheduleApi.Values(_daily, "Diary");
            Assert.That(diary.Length, Is.EqualTo(1));
            Assert.That(DailyPlanScheduleApi.Read<int>(diary[0], "DayNumber"), Is.EqualTo(1));
            var oldSlots = DailyPlanScheduleApi.Values(diary[0], "Slots");
            Assert.That(oldSlots.Length, Is.EqualTo(2));
            Assert.That(DailyPlanScheduleApi.Read<bool>(oldSlots[0], "Completed"), Is.True);
            Assert.That(DailyPlanScheduleApi.Read<bool>(oldSlots[1], "Completed"), Is.False);
            Assert.That(DailyPlanScheduleApi.Read<string>(oldSlots[1], "RouteId"), Is.EqualTo(_other.Id));
            Assert.That(Slots().All(x => string.IsNullOrEmpty(DailyPlanScheduleApi.Read<string>(x, "RouteId"))), Is.True);
        }

        [Test]
        public void PreviousDayEvent_IsRejected_ButFreshDayScopedRequestCanComplete()
        {
            Sync(); Assign(0, _route); Record(1, "local-request-1");
            AdvanceStudentTo(2); Sync(); Assign(0, _route);
            Assert.That(Record(1, "delayed-day-one"), Is.False);
            Assert.That(Record(2, "local-request-1"), Is.True, "Receipt keys are scoped by actual activity day.");
            Assert.That(_student.GetTraitValue(_trait), Is.EqualTo(2));
        }

        [Test]
        public void ForeignIdentityOrBackwardDay_CannotMutateSchedule()
        {
            Sync(); Assign(0, _route);
            var foreign = new StudentLifeProgress("another-slot", "schedule-player", 100, 100); foreign.ConfigureCalendar(_calendar);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_daily, "SynchronizeDay", foreign), Is.False);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_daily, "TryAssignSlot", 1, _other, foreign), Is.False);
            Assert.That(DailyPlanScheduleApi.Record(_daily, new[] { _set }, _activity, foreign, 1, "wrong-player"), Is.False);
            AdvanceStudentTo(2); Sync();
            var old = new StudentLifeProgress(_student.SaveSlot, _student.PlayerId, 100, 100); old.ConfigureCalendar(_calendar);
            Assert.That(DailyPlanScheduleApi.Call<bool>(_daily, "SynchronizeDay", old), Is.False);
            Assert.That(_daily.CurrentDay, Is.EqualTo(2));
        }

        [Test]
        public void LegacySingleRouteMigration_PreservesCompletedReceiptInFirstSlot()
        {
            var legacy = new DailyPlanProgressSaveData { SaveSlot = _student.SaveSlot, PlayerId = _student.PlayerId, CurrentDay = 1,
                SelectedRouteId = _route.Id, CompletedRouteId = _route.Id, MatchedActivityId = _activity.Id, AppliedCompletionKeys = new[] { "legacy-event" } };
            _daily = DailyPlanProgress.FromSaveData(legacy, _student.SaveSlot, _student.PlayerId, 1); Sync();
            Assert.That(Slots().Length, Is.EqualTo(2));
            Assert.That(DailyPlanScheduleApi.Read<bool>(Slots()[0], "Completed"), Is.True);
            Assert.That(DailyPlanScheduleApi.Read<string>(Slots()[0], "MatchedActivityId"), Is.EqualTo(_activity.Id));
            Assign(1, _route);
            Assert.That(Record(1, "legacy-event"), Is.False);
        }

        [Test]
        public void ScheduleAndDiaryJsonRoundTrip_PreserveReceiptsAndDoNotReapplyOutcomes()
        {
            Sync(); Assign(0, _route); Record(1, "saved-event");
            AdvanceStudentTo(2); Sync(); Assign(0, _other); Record(2, "current-event");
            string json = JsonUtility.ToJson(_daily.ToSaveData());
            _daily = DailyPlanProgress.FromSaveData(JsonUtility.FromJson<DailyPlanProgressSaveData>(json), _student.SaveSlot, _student.PlayerId, 2);
            Sync();
            Assert.That(DailyPlanScheduleApi.Values(_daily, "Diary").Length, Is.EqualTo(1));
            Assert.That(CompletedCount(), Is.EqualTo(1));
            Assert.That(Record(2, "current-event"), Is.False);
            Assert.That(_student.GetTraitValue(_trait), Is.EqualTo(2));
        }

        [Test]
        public void ExistingSelectRouteAdapter_DoesNotEraseAnEarlierCompletionReceipt()
        {
            _daily.SelectRoute(_route);
            Assert.That(_daily.CompleteRoute(_route, _activity, "legacy-receipt"), Is.True);
            _daily.SelectRoute(_other);
            CollectionAssert.Contains(_daily.AppliedCompletionKeys, "legacy-receipt");
            Assert.That(_daily.CompleteRoute(_other, _activity, "legacy-receipt"), Is.False);
        }

        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private DailyPlanRouteDefinition Route(string id, LocationActivityDefinition activity, DailyPlanRouteOutcomeBase outcome)
        {
            var value = Make<DailyPlanRouteDefinition>();
            value.ConfigureForTests(id, id, null, LocationGrowthRoute.SelfStudy, new[] { activity },
                Array.Empty<DailyPlanRouteRequirementBase>(), new[] { outcome }, id + ".title", id + ".detail", id + ".next");
            return value;
        }
        private void Sync() => DailyPlanScheduleApi.Call<bool>(_daily, "SynchronizeDay", _student);
        private bool Assign(int slot, DailyPlanRouteDefinition route) => DailyPlanScheduleApi.Call<bool>(_daily, "TryAssignSlot", slot, route, _student);
        private bool Record(int day, string key) => DailyPlanScheduleApi.Record(_daily, new[] { _set }, _activity, _student, day, key);
        private object[] Slots() => DailyPlanScheduleApi.Values(_daily, "Slots");
        private int CompletedCount() => Slots().Count(x => DailyPlanScheduleApi.Read<bool>(x, "Completed"));
        private void AdvanceStudentTo(int day)
        {
            while (_student.CurrentDay < day) { Assert.That(_student.TryEndDay(null, out _), Is.True); Assert.That(_student.TryStartNextDay(), Is.True); }
        }
    }

    public sealed class DailyPlanScheduleTraitOutcome : DailyPlanRouteOutcomeBase
    {
        public TraitDefinition Trait;
        public override string Apply(StudentLifeProgress progress, DailyPlanRouteDefinition route)
        { progress.AddTrait(Trait, 1); return "+trait.focused:1"; }
    }

    internal static class DailyPlanScheduleApi
    {
        public static T Read<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null);
            var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, target.GetType().Name + "." + name + " is required by the schedule contract.");
            return (T)property.GetValue(target);
        }
        public static object[] Values(object target, string name) => Read<IEnumerable>(target, name).Cast<object>().ToArray();
        public static T Call<T>(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, target.GetType().Name + "." + name + " is required by the schedule contract.");
            return (T)method.Invoke(target, args);
        }
        public static bool Record(DailyPlanProgress daily, DailyPlanRouteSetDefinition[] sets, LocationActivityDefinition activity,
            StudentLifeProgress student, int day, string key)
        {
            var method = typeof(DailyPlanRouteRunner).GetMethod("TryRecordScheduledActivity", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Daily Plan needs day-scoped activity credit.");
            object[] args = { daily, sets, activity, student, day, key, null };
            return (bool)method.Invoke(new DailyPlanRouteRunner(), args);
        }
    }
}
