using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    internal sealed class FakeLocationActivity : IDailyPlanLocationActivity
    {
        public FakeLocationActivity(string id, string locationId, string growthRoute) { Id = id; LocationId = locationId; GrowthRoute = growthRoute; }
        public string Id { get; }
        public string LocationId { get; }
        public string GrowthRoute { get; }
    }

    internal sealed class FakePartTimeWork : IMinigamePartTimeWork
    {
        public FakePartTimeWork(string id, Minigame minigame = null, ActivityTimePolicy timePolicy = null, int timeCostMinutes = 30, int energyCost = 1, int focusCost = 1, int stressDelta = 0)
        {
            Id = id; Minigame = minigame; TimePolicy = timePolicy; TimeCostMinutes = timeCostMinutes; EnergyCost = energyCost; FocusCost = focusCost; StressDelta = stressDelta;
        }
        public string Id { get; }
        public Minigame Minigame { get; }
        public ActivityTimePolicy TimePolicy { get; }
        public int TimeCostMinutes { get; }
        public int EnergyCost { get; }
        public int FocusCost { get; }
        public int StressDelta { get; }
        public bool RequirementsMet = true;
        public bool HasSatisfiedRequirements(StudentLifeProgress progress) => RequirementsMet;
    }

    internal sealed class TraitOutcome : IDailyPlanRouteOutcome
    {
        private readonly string _traitId;
        public TraitOutcome(string traitId) { _traitId = traitId; }
        public string Apply(StudentLifeProgress progress, DailyPlanRoute route) { progress.AddTrait(_traitId, 1); return "+" + _traitId + ":1"; }
    }

    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/DailyPlanScheduleTests.cs
    public sealed class DailyPlanScheduleTests
    {
        private const string Trait = "trait.focused";
        private LifeCalendar _calendar;
        private StudentLifeProgress _student;
        private DailyPlanProgress _daily;
        private FakeLocationActivity _activity;
        private DailyPlanRoute _route, _other;
        private DailyPlanRouteSet _set;

        [SetUp]
        public void SetUp()
        {
            _calendar = new LifeCalendar(2, 3);
            _student = new StudentLifeProgress("schedule-slot", "schedule-player", 100, 100);
            _student.ConfigureCalendar(_calendar);
            _daily = new DailyPlanProgress(_student.SaveSlot, _student.PlayerId, 1);
            _activity = new FakeLocationActivity("activity.study", null, "SelfStudy");
            _route = Route("route.study");
            _other = Route("route.alternative");
            _set = new DailyPlanRouteSet("set", "Plans", new[] { _route, _other }, 2); // 같은 길을 두 칸에 넣는 시나리오가 있어 하루 2번까지 허용한다
        }

        [Test]
        public void WeekdayCalendar_CreatesAfterSchoolAndEveningSlots_WithoutSpendingTime()
        {
            Sync();
            Assert.That(_daily.SlotCount, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "daily-plan.period.after-school", "daily-plan.period.evening" }, _daily.Slots.Select(x => x.PeriodKey).ToArray());
            Assert.That(_student.TimePointsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void WeekendCalendar_CreatesMorningAfternoonAndEveningSlots()
        {
            AdvanceStudentTo(6); Sync();
            Assert.That(_daily.CurrentDay, Is.EqualTo(6));
            Assert.That(_daily.SlotCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "daily-plan.period.morning", "daily-plan.period.afternoon", "daily-plan.period.evening" }, _daily.Slots.Select(x => x.PeriodKey).ToArray());
        }

        [Test]
        public void AssigningTwoRoutes_PreservesBothSlots_AndRejectsOutOfRange()
        {
            Sync();
            Assert.That(Assign(0, _route), Is.True);
            Assert.That(Assign(1, _other), Is.True);
            Assert.That(Assign(2, _route), Is.False);
            CollectionAssert.AreEqual(new[] { _route.Id, _other.Id }, _daily.Slots.Select(x => x.RouteId).ToArray());
            Assert.That(_student.TimePointsSpentToday, Is.Zero, "Planning does not consume an activity.");
        }

        [Test]
        public void EditingPendingSlot_DoesNotEraseCompletedSlotOrReceipt()
        {
            Sync(); Assign(0, _route); Assign(1, _route);
            Assert.That(Record(1, "visit-a"), Is.True);
            Assert.That(Assign(1, _other), Is.True);
            Assert.That(_daily.Slots[0].Completed, Is.True);
            Assert.That(_daily.Slots[0].CompletionRequestKey, Is.EqualTo("visit-a"));
            Assert.That(Record(1, "visit-a"), Is.False);
            Assert.That(_student.GetTraitValue(Trait), Is.EqualTo(1));
        }

        [Test]
        public void CompletedSlot_CannotBeReassignedToResetItsReward()
        {
            Sync(); Assign(0, _route); Record(1, "visit-a");
            Assert.That(Assign(0, _other), Is.False);
            Assert.That(Assign(0, _route), Is.False);
            Assert.That(_daily.Slots[0].RouteId, Is.EqualTo(_route.Id));
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
            Assert.That(_student.GetTraitValue(Trait), Is.EqualTo(2));
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
            var different = new FakeLocationActivity("activity.rest", null, "Rest");
            Assert.That(new DailyPlanRouteRunner().TryRecordScheduledActivity(_daily, new[] { _set }, different, _student, 1, "event-x", out _), Is.False);
            Assert.That(Record(1, "event-x"), Is.True);
        }

        [Test]
        public void ResultReadyDay_RejectsPlanningAndLateCompletion()
        {
            Sync(); Assign(0, _route);
            Assert.That(_student.TryEndDay(null, out _), Is.True);
            Assert.That(Assign(1, _other), Is.False);
            Assert.That(Record(1, "late-event"), Is.False);
            Assert.That(_student.GetTraitValue(Trait), Is.Zero);
        }

        [Test]
        public void Rollover_ArchivesCompletedAndIncompleteChoicesExactlyOnce()
        {
            Sync(); Assign(0, _route); Assign(1, _other); Record(1, "day-one");
            AdvanceStudentTo(2); Sync(); Sync();
            Assert.That(_daily.Diary.Count, Is.EqualTo(1));
            Assert.That(_daily.Diary[0].DayNumber, Is.EqualTo(1));
            var oldSlots = _daily.Diary[0].Slots;
            Assert.That(oldSlots.Count, Is.EqualTo(2));
            Assert.That(oldSlots[0].Completed, Is.True);
            Assert.That(oldSlots[1].Completed, Is.False);
            Assert.That(oldSlots[1].RouteId, Is.EqualTo(_other.Id));
            Assert.That(_daily.Slots.All(x => string.IsNullOrEmpty(x.RouteId)), Is.True);
        }

        [Test]
        public void PreviousDayEvent_IsRejected_ButFreshDayScopedRequestCanComplete()
        {
            Sync(); Assign(0, _route); Record(1, "local-request-1");
            AdvanceStudentTo(2); Sync(); Assign(0, _route);
            Assert.That(Record(1, "delayed-day-one"), Is.False);
            Assert.That(Record(2, "local-request-1"), Is.True, "Receipt keys are scoped by actual activity day.");
            Assert.That(_student.GetTraitValue(Trait), Is.EqualTo(2));
        }

        [Test]
        public void ForeignIdentityOrBackwardDay_CannotMutateSchedule()
        {
            Sync(); Assign(0, _route);
            var foreign = new StudentLifeProgress("another-slot", "schedule-player", 100, 100); foreign.ConfigureCalendar(_calendar);
            Assert.That(_daily.SynchronizeDay(foreign), Is.False);
            Assert.That(_daily.TryAssignSlot(1, _other, foreign), Is.False);
            Assert.That(new DailyPlanRouteRunner().TryRecordScheduledActivity(_daily, new[] { _set }, _activity, foreign, 1, "wrong-player", out _), Is.False);
            AdvanceStudentTo(2); Sync();
            var old = new StudentLifeProgress(_student.SaveSlot, _student.PlayerId, 100, 100); old.ConfigureCalendar(_calendar);
            Assert.That(_daily.SynchronizeDay(old), Is.False);
            Assert.That(_daily.CurrentDay, Is.EqualTo(2));
        }

        [Test]
        public void LegacySingleRouteMigration_PreservesCompletedReceiptInFirstSlot()
        {
            var legacy = new DailyPlanProgressSaveData
            {
                SaveSlot = _student.SaveSlot, PlayerId = _student.PlayerId, CurrentDay = 1,
                SelectedRouteId = _route.Id, CompletedRouteId = _route.Id, MatchedActivityId = _activity.Id, AppliedCompletionKeys = new[] { "legacy-event" },
            };
            _daily = DailyPlanProgress.FromSaveData(legacy, _student.SaveSlot, _student.PlayerId, 1); Sync();
            Assert.That(_daily.Slots.Count, Is.EqualTo(2));
            Assert.That(_daily.Slots[0].Completed, Is.True);
            Assert.That(_daily.Slots[0].MatchedActivityId, Is.EqualTo(_activity.Id));
            Assign(1, _route);
            Assert.That(Record(1, "legacy-event"), Is.False);
        }

        [Test]
        public void ScheduleAndDiaryRoundTrip_PreserveReceiptsAndDoNotReapplyOutcomes()
        {
            Sync(); Assign(0, _route); Record(1, "saved-event");
            AdvanceStudentTo(2); Sync(); Assign(0, _other); Record(2, "current-event");
            _daily = DailyPlanProgress.FromSaveData(_daily.ToSaveData(), _student.SaveSlot, _student.PlayerId, 2);
            Sync();
            Assert.That(_daily.Diary.Count, Is.EqualTo(1));
            Assert.That(CompletedCount(), Is.EqualTo(1));
            Assert.That(Record(2, "current-event"), Is.False);
            Assert.That(_student.GetTraitValue(Trait), Is.EqualTo(2));
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

        // 원래 테스트가 안 다루던 경계

        [Test]
        public void CalendarShrink_KeepsPlannedSlots_ButDropsEmptyOnes()
        {
            Sync(); Assign(1, _route);
            _student.ConfigureCalendar(new LifeCalendar(1, 1));
            Assert.That(_daily.SynchronizeDay(_student), Is.False);
            Assert.That(_daily.SlotCount, Is.EqualTo(2), "A planned evening slot is not silently discarded.");
            Assert.That(_daily.Slots[1].RouteId, Is.EqualTo(_route.Id));

            var fresh = new DailyPlanProgress(_student.SaveSlot, _student.PlayerId, 1);
            Assert.That(fresh.SynchronizeDay(_student), Is.True);
            Assert.That(fresh.SlotCount, Is.EqualTo(1));
            Assert.That(fresh.Slots[0].PeriodKey, Is.EqualTo("daily-plan.period.evening"));
        }

        [Test]
        public void EmptyRequestKeyOrRouteId_IsRejectedWithoutChange()
        {
            Sync(); Assign(0, _route);
            Assert.That(Record(1, ""), Is.False);
            Assert.That(Record(1, null), Is.False);
            Assert.That(_daily.TryAssignSlot(1, new DailyPlanRoute("", null, null, null, null, null, null, null, null, null, null, null), _student), Is.False);
            Assert.That(_daily.TryAssignSlot(1, null, _student), Is.False);
            Assert.That(CompletedCount(), Is.Zero);
            Assert.That(_student.GetTraitValue(Trait), Is.Zero);
        }

        [Test]
        public void SaveData_SortsReceipts_AndDiaryIgnoresFutureOrDuplicateDays()
        {
            var data = new DailyPlanProgressSaveData
            {
                Version = 2, SaveSlot = _student.SaveSlot, PlayerId = _student.PlayerId, CurrentDay = 3,
                AppliedCompletionKeys = new[] { "b", "a", "" },
                Slots = new[] { new DailyPlanSlotSaveData { RouteId = _route.Id, Completed = true, CompletionRequestKey = "c", MatchedActivityId = "x" } },
                Diary = new[]
                {
                    new DailyPlanDaySaveData { DayNumber = 2 }, new DailyPlanDaySaveData { DayNumber = 1 },
                    new DailyPlanDaySaveData { DayNumber = 2 }, new DailyPlanDaySaveData { DayNumber = 3 }, null, new DailyPlanDaySaveData { DayNumber = 0 },
                },
            };
            var loaded = DailyPlanProgress.FromSaveData(data, _student.SaveSlot, _student.PlayerId, 3);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, loaded.ToSaveData().AppliedCompletionKeys);
            CollectionAssert.AreEqual(new[] { 1, 2 }, loaded.Diary.Select(d => d.DayNumber).ToArray());
            Assert.That(loaded.ToSaveData().Version, Is.EqualTo(2));
            Assert.That(loaded.CompletedRouteId, Is.EqualTo(_route.Id));

            Assert.That(DailyPlanProgress.FromSaveData(data, _student.SaveSlot, _student.PlayerId, 2).Slots[0].RouteId, Is.Empty, "A save from a later day than the student is ignored.");
        }

        [Test]
        public void SlotWithoutRoute_CannotBeRestoredAsCompleted()
        {
            var data = new DailyPlanProgressSaveData
            {
                Version = 2, SaveSlot = "s", PlayerId = "p", CurrentDay = 1,
                Slots = new[] { new DailyPlanSlotSaveData { Completed = true, MatchedActivityId = "x", CompletionRequestKey = "k", ResultLogs = null } },
            };
            var slot = DailyPlanProgress.FromSaveData(data, "s", "p", 1).Slots[0];
            Assert.That(slot.Completed, Is.False);
            Assert.That(slot.MatchedActivityId, Is.Empty);
            Assert.That(slot.CompletionRequestKey, Is.Empty);
            Assert.That(slot.ResultLogs, Is.Empty);
        }

        private DailyPlanRoute Route(string id)
            => new DailyPlanRoute(id, id, null, "SelfStudy", new IDailyPlanLocationActivity[] { _activity }, null, null, null, new IDailyPlanRouteOutcome[] { new TraitOutcome(Trait) }, id + ".title", id + ".detail", id + ".next");

        private void Sync() => _daily.SynchronizeDay(_student);
        private bool Assign(int slot, DailyPlanRoute route) => _daily.TryAssignSlot(slot, route, _student);
        private bool Record(int day, string key) => new DailyPlanRouteRunner().TryRecordScheduledActivity(_daily, new[] { _set }, _activity, _student, day, key, out _);
        private int CompletedCount() => _daily.Slots.Count(x => x.Completed);

        private void AdvanceStudentTo(int day)
        {
            while (_student.CurrentDay < day)
            {
                Assert.That(_student.TryEndDay(null, out _), Is.True);
                Assert.That(_student.TryStartNextDay(), Is.True);
            }
        }
    }
}
