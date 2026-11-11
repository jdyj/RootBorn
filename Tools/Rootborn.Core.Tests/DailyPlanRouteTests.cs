using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/DailyPlanRouteRunnerTests.cs, DailyPlanRouteContextTests.cs, DailyPlanTypedCompletionTests.cs,
    //          DailyPlanSchedulePersistenceTests.cs(WrongEmbeddedIdentity, StudentJsonRoundTrip, StudentNextDay), DailyPlanScheduleIntegrationTests.cs(LegacyStudentWithoutDiary, LegacyMigration의 규칙 부분)
    public sealed class DailyPlanRouteTests
    {
        // --- DailyPlanRouteRunnerTests ---

        [Test]
        public void RouteSet_ReturnsAvailableRoutesFromData()
        {
            var route = Route("daily.library.self-study", "location.library", "SelfStudy");
            var set = new DailyPlanRouteSet("daily.default", "daily.default", new[] { route }, 1);
            Assert.That(new DailyPlanRouteRunner().GetAvailableRoutes(new[] { set }, Student()), Is.EquivalentTo(new[] { route }));
        }

        [Test]
        public void SelectRoute_PersistsSelectedRouteForSameDay()
        {
            var route = Route("daily.library.self-study", "location.library", "SelfStudy");
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            Assert.That(new DailyPlanRouteRunner().TrySelectRoute(daily, route, Student()), Is.True);
            Assert.That(daily.SelectedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(daily.CurrentDay, Is.EqualTo(1));
        }

        [Test]
        public void SelectSameRouteTwice_IsNoOp()
        {
            var route = Route("daily.town.help", null, "SocialHelp");
            var student = Student();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();
            Assert.That(runner.TrySelectRoute(daily, route, student), Is.True);
            Assert.That(runner.TrySelectRoute(daily, route, student), Is.False);
            Assert.That(daily.SelectedRouteId, Is.EqualTo("daily.town.help"));
        }

        [Test]
        public void MatchingActivity_CompletesSelectedRoute()
        {
            var activity = new FakeLocationActivity("activity.library.read", "location.library", "SelfStudy");
            var route = Route("daily.library.self-study", "location.library", "SelfStudy");
            var set = new DailyPlanRouteSet("daily.default", null, new[] { route }, 1);
            var student = Student();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();
            runner.TrySelectRoute(daily, route, student);

            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out var result), Is.True);
            Assert.That(result.Route, Is.SameAs(route));
            Assert.That(result.Activity, Is.SameAs(activity));
            Assert.That(daily.CompletedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(daily.MatchedActivityId, Is.EqualTo("activity.library.read"));
        }

        [Test]
        public void ExplicitMatchingActivity_CompletesRouteEvenWhenLocationRoutePairDiffers()
        {
            var activity = new FakeLocationActivity("activity.workshop.shift", "location.workshop", "Work");
            var route = Route("daily.library.special", "location.library", "SelfStudy", new IDailyPlanLocationActivity[] { activity });
            var set = new DailyPlanRouteSet("daily.default", null, new[] { route }, 1);
            var student = Student();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();
            runner.TrySelectRoute(daily, route, student);

            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out var result), Is.True);
            Assert.That(result.Route, Is.SameAs(route));
            Assert.That(result.Activity, Is.SameAs(activity));
        }

        [Test]
        public void NonMatchingActivity_DoesNotCompleteSelectedRoute()
        {
            var activity = new FakeLocationActivity("activity.workshop.shift", "location.workshop", "Work");
            var route = Route("daily.library.self-study", "location.library", "SelfStudy");
            var set = new DailyPlanRouteSet("daily.default", null, new[] { route }, 1);
            var student = Student();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();
            runner.TrySelectRoute(daily, route, student);

            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out var result), Is.False);
            Assert.That(result.IsCompleted, Is.False);
            Assert.That(daily.CompletedRouteId, Is.Empty);
        }

        [Test]
        public void CompletionWithSameRequestKey_DoesNotDuplicate()
        {
            var (daily, set, activity, student, runner) = SelectedLibrary();
            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out _), Is.True);
            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out _), Is.False);
            Assert.That(daily.AppliedCompletionKeys, Is.EquivalentTo(new[] { "request-1" }));
        }

        [Test]
        public void CompletionWithDifferentRequestAfterRouteCompleted_DoesNotDuplicate()
        {
            var (daily, set, activity, student, runner) = SelectedLibrary();
            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out _), Is.True);
            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-2", out _), Is.False);
            Assert.That(daily.AppliedCompletionKeys, Is.EquivalentTo(new[] { "request-1" }));
        }

        [Test]
        public void SaveDataRoundTrip_PreservesCompletionWithoutReapply()
        {
            var progress = new DailyPlanProgress("slot-a", "player-a", 2);
            var route = Route("daily.library.self-study", null, "SelfStudy");
            var activity = new FakeLocationActivity("activity.library.read", null, "SelfStudy");
            progress.SelectRoute(route);
            progress.CompleteRoute(route, activity, "request-1");
            var restored = DailyPlanProgress.FromSaveData(progress.ToSaveData(), "slot-a", "player-a", 2);
            Assert.That(restored.SelectedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(restored.CompletedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(restored.MatchedActivityId, Is.EqualTo("activity.library.read"));
            Assert.That(restored.AppliedCompletionKeys, Is.EquivalentTo(new[] { "request-1" }));
            Assert.That(restored.CompleteRoute(route, activity, "request-1"), Is.False);
        }

        [Test]
        public void MissingSavedRoute_DoesNotCrashAndDoesNotCompleteActivity()
        {
            var daily = DailyPlanProgress.FromSaveData(new DailyPlanProgressSaveData { SaveSlot = "slot-a", PlayerId = "player-a", CurrentDay = 1, SelectedRouteId = "daily.missing" }, "slot-a", "player-a", 1);
            var activity = new FakeLocationActivity("activity.library.read", null, "SelfStudy");
            Assert.That(new DailyPlanRouteRunner().TryRecordActivity(daily, Array.Empty<DailyPlanRouteSet>(), activity, Student(), "request-1", out var result), Is.False);
            Assert.That(result.IsCompleted, Is.False);
            Assert.That(daily.SelectedRouteId, Is.EqualTo("daily.missing"));
        }

        // --- DailyPlanRouteContextTests ---

        [Test]
        public void DailyPlanWorldStateRequirement_ReturnsFalseWithoutWorldStateProgress()
        {
            var requirement = new DailyPlanWorldStateRequirement("world.route.library-unlocked", true);
            var route = RouteWith(requirement);
            Assert.IsFalse(requirement.IsSatisfied(new DailyPlanRouteContext(Student(), null), route));
        }

        [Test]
        public void DailyPlanWorldStateRequirement_ReturnsTrueWhenFlagActive()
        {
            var requirement = new DailyPlanWorldStateRequirement("world.route.library-unlocked", true);
            var route = RouteWith(requirement);
            Assert.IsTrue(requirement.IsSatisfied(new DailyPlanRouteContext(Student(), new FakeWorld("world.route.library-unlocked")), route));
        }

        [Test]
        public void DailyPlanRouteRunner_GetAvailableRoutes_UsesContextRequirements()
        {
            var route = RouteWith(new DailyPlanWorldStateRequirement("world.route.library-unlocked", true));
            var set = new DailyPlanRouteSet("daily.routes", null, new[] { route }, 1);
            var progress = Student();
            var runner = new DailyPlanRouteRunner();
            Assert.IsEmpty(runner.GetAvailableRoutes(new[] { set }, new DailyPlanRouteContext(progress, null)));
            CollectionAssert.AreEqual(new[] { route }, runner.GetAvailableRoutes(new[] { set }, new DailyPlanRouteContext(progress, new FakeWorld("world.route.library-unlocked"))));
        }

        [Test]
        public void DailyPlanRouteRunner_TrySelectRoute_RejectsLockedWorldStateRoute()
        {
            var route = RouteWith(new DailyPlanWorldStateRequirement("world.route.library-unlocked", true));
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            Assert.IsFalse(new DailyPlanRouteRunner().TrySelectRoute(daily, route, new DailyPlanRouteContext(Student(), null)));
            Assert.IsEmpty(daily.SelectedRouteId);
        }

        [Test]
        public void DailyPlanRouteRunner_TrySelectRoute_AcceptsUnlockedWorldStateRoute()
        {
            var route = RouteWith(new DailyPlanWorldStateRequirement("world.route.library-unlocked", true));
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            Assert.IsTrue(new DailyPlanRouteRunner().TrySelectRoute(daily, route, new DailyPlanRouteContext(Student(), new FakeWorld("world.route.library-unlocked"))));
            Assert.AreEqual(route.Id, daily.SelectedRouteId);
        }

        [Test]
        public void DailyPlanRouteRequirement_StudentProgressOnlyCompatibilityStillWorks()
        {
            var route = RouteWith(new EnergyThreshold(10));
            Assert.IsTrue(route.IsAvailable(new DailyPlanRouteContext(Student(), null)));
            Assert.IsTrue(route.IsAvailable(Student()));
        }

        // --- DailyPlanTypedCompletionTests ---

        [Test]
        public void PracticeMatcher_AcceptsOnlyTheConfiguredReference()
        {
            var t = new TypedFixture();
            var other = new CareerPractice(t.Practice.Id, "Other", null, null, null);
            Assert.That(t.Route.MatchesPractice(t.Practice), Is.True);
            Assert.That(t.Route.MatchesPractice(other), Is.False);
            Assert.That(t.Route.MatchesPractice(null), Is.False);
        }

        [Test]
        public void WorkMatcher_AcceptsOnlyTheConfiguredReference()
        {
            var t = new TypedFixture();
            var other = new FakePartTimeWork(t.Work.Id);
            Assert.That(t.Route.MatchesWork(t.Work), Is.True);
            Assert.That(t.Route.MatchesWork(other), Is.False);
            Assert.That(t.Route.MatchesWork(null), Is.False);
        }

        [Test]
        public void SuccessfulPractice_CreditsPlannedSlotAndRouteGrowth_WithoutChargingTimeAgain()
        {
            var t = new TypedFixture(); t.Assign(0);
            Assert.That(t.Student.TrySpendTimePoints(1), Is.True);
            Assert.That(t.RecordPractice(t.Student, 1, "practice-complete"), Is.True);
            Assert.That(t.Student.TimePointsSpentToday, Is.EqualTo(1));
            Assert.That(t.Student.GetTraitValue(TypedFixture.Trait), Is.EqualTo(1));
            var slot = t.Student.DailyPlan.Slots[0];
            Assert.That(slot.MatchedActivityId, Is.EqualTo(t.Practice.Id));
            Assert.That(slot.MatchedActivityKind, Is.EqualTo(DailyPlanActivityKind.Practice));
        }

        [Test]
        public void SuccessfulPartTimeWork_CreditsTheTypedWorkSlot_WithoutReplayingTheWorkItself()
        {
            var t = new TypedFixture(); t.Assign(0);
            Assert.That(t.RecordWork(t.Student, 1, "work-complete"), Is.True);
            Assert.That(t.Student.Energy, Is.EqualTo(100));
            Assert.That(t.Student.Focus, Is.EqualTo(100));
            Assert.That(t.Student.TimePointsSpentToday, Is.Zero, "This adapter only records a completed settlement.");
            var slot = t.Student.DailyPlan.Slots[0];
            Assert.That(slot.MatchedActivityId, Is.EqualTo(t.Work.Id));
            Assert.That(slot.MatchedActivityKind, Is.EqualTo(DailyPlanActivityKind.PartTimeWork));
        }

        [Test]
        public void TypedAndLocationAdapters_ShareOneDailyReceiptLedger()
        {
            var t = new TypedFixture(); t.Assign(0); t.Assign(1);
            Assert.That(new DailyPlanRouteRunner().TryRecordScheduledActivity(t.Student.DailyPlan, t.Sets, t.Activity, t.Student, 1, "same-settlement", out _), Is.True);
            Assert.That(t.RecordPractice(t.Student, 1, "same-settlement"), Is.False);
            Assert.That(t.RecordWork(t.Student, 1, "same-settlement"), Is.False);
            Assert.That(t.Student.GetTraitValue(TypedFixture.Trait), Is.EqualTo(1));
            Assert.That(t.RecordPractice(t.Student, 1, "another-settlement"), Is.True);
            Assert.That(t.Student.GetTraitValue(TypedFixture.Trait), Is.EqualTo(2));
        }

        [Test]
        public void TypedCompletionSaveRoundTrip_PreservesKindAndReceiptWithoutPayingAgain()
        {
            var t = new TypedFixture(); t.Assign(0);
            Assert.That(t.RecordWork(t.Student, 1, "persisted-work"), Is.True);
            var restored = StudentLifeProgress.FromSaveData(t.Student.ToSaveData());
            restored.ConfigureCalendar(t.Calendar);
            Assert.That(t.RecordWork(restored, 1, "persisted-work"), Is.False);
            Assert.That(restored.GetTraitValue(TypedFixture.Trait), Is.EqualTo(1));
            Assert.That(restored.DailyPlan.Slots[0].MatchedActivityKind, Is.EqualTo(DailyPlanActivityKind.PartTimeWork));
        }

        [Test]
        public void WrongDayOrPlayer_TypedCompletionCannotCreditTheSchedule()
        {
            var t = new TypedFixture(); t.Assign(0);
            Assert.That(t.RecordPractice(t.Student, 2, "wrong-day"), Is.False);
            var foreign = new StudentLifeProgress("other-slot", "typed-player", 100, 100); foreign.ConfigureCalendar(t.Calendar);
            Assert.That(new DailyPlanRouteRunner().TryRecordPartTimeWork(t.Student.DailyPlan, t.Sets, t.Work, foreign, 1, "wrong-player", out _), Is.False);
            Assert.That(t.Student.GetTraitValue(TypedFixture.Trait), Is.Zero);
            Assert.That(t.Student.DailyPlan.Slots[0].Completed, Is.False);
        }

        [Test]
        public void TypedRoute_DoesNotMatchLocationActivityByPlaceAlone()
        {
            var practice = new CareerPractice("practice.x", null, null, null, null);
            var route = new DailyPlanRoute("route.typed", null, "location.x", "Work", null, new[] { practice }, null, null, null, null, null, null);
            Assert.That(route.Matches(new FakeLocationActivity("activity.x", "location.x", "Work")), Is.False);
            Assert.That(Route("route.plain", "location.x", "Work").Matches(new FakeLocationActivity("activity.x", "location.x", "Work")), Is.True);
            Assert.That(Route("route.plain", "location.x", "Work").Matches(new FakeLocationActivity("activity.x", "location.x", "Rest")), Is.False);
            Assert.That(Route("route.plain", "location.x", "Work").Matches(null), Is.False);
        }

        // --- 학생 진행 상태 안의 일과 (DailyPlanSchedulePersistenceTests / DailyPlanScheduleIntegrationTests) ---

        [Test]
        public void WrongEmbeddedIdentity_CannotImportAnotherPlayersCompletion()
        {
            var saved = new DailyPlanProgressSaveData { SaveSlot = "foreign", PlayerId = "foreign-player", CurrentDay = 1, SelectedRouteId = "route.fixture", CompletedRouteId = "route.fixture", AppliedCompletionKeys = new[] { "foreign-reward" } };
            var loaded = DailyPlanProgress.FromSaveData(saved, "slot", "player", 1);
            Assert.That(loaded.SaveSlot, Is.EqualTo("slot"));
            Assert.That(loaded.PlayerId, Is.EqualTo("player"));
            Assert.That(loaded.SelectedRouteId, Is.Empty);
            Assert.That(loaded.AppliedCompletionKeys, Is.Empty);
        }

        [Test]
        public void StudentRoundTrip_IncludesPlanAndCompletionReceiptWithItsTraitReward()
        {
            var t = new TypedFixture(); t.Assign(0);
            Assert.That(new DailyPlanRouteRunner().TryRecordScheduledActivity(t.Student.DailyPlan, t.Sets, t.Activity, t.Student, 1, "saved-completion", out _), Is.True);
            var dto = t.Student.ToSaveData();
            Assert.That(dto.DailyPlan, Is.Not.Null);
            var restored = StudentLifeProgress.FromSaveData(dto); restored.ConfigureCalendar(t.Calendar);
            Assert.That(restored.GetTraitValue(TypedFixture.Trait), Is.EqualTo(1));
            Assert.That(new DailyPlanRouteRunner().TryRecordScheduledActivity(restored.DailyPlan, t.Sets, t.Activity, restored, 1, "saved-completion", out _), Is.False);
            Assert.That(restored.GetTraitValue(TypedFixture.Trait), Is.EqualTo(1));
        }

        [Test]
        public void StudentNextDay_ArchivesOwnedScheduleBeforeSnapshotCapture()
        {
            var t = new TypedFixture(); t.Assign(0);
            Assert.That(t.Student.TryEndDay(null, out _), Is.True);
            Assert.That(t.Student.TryStartNextDay(), Is.True);
            Assert.That(t.Student.DailyPlan.CurrentDay, Is.EqualTo(2));
            Assert.That(t.Student.DailyPlan.Diary.Count, Is.EqualTo(1));
            Assert.That(StudentLifeProgress.FromSaveData(t.Student.ToSaveData()).DailyPlan.CurrentDay, Is.EqualTo(2));
        }

        [Test]
        public void LegacyStudentWithoutDiary_StartsOnItsCurrentDay_WithoutInventingOldPages()
        {
            // 엔진 세이브 도구는 빠진 안쪽 객체를 빈 껍데기로 채운다. 그 경우를 흉내 낸다.
            var legacy = new StudentLifeProgressSaveData { SaveSlot = "slot", PlayerId = "player", CurrentDay = 6, Energy = 10, Focus = 10, DailyPlan = new DailyPlanProgressSaveData() };
            var student = StudentLifeProgress.FromSaveData(legacy);
            Assert.That(student.DailyPlan.CurrentDay, Is.EqualTo(6));
            Assert.That(student.DailyPlan.Diary, Is.Empty);
        }

        [Test]
        public void LegacyImport_TakesMatchingDiaryOnlyOnce_AndNeverForeignOrFutureOnes()
        {
            var route = new DailyPlanRoute("legacy.route", "Legacy", null, "SelfStudy", null, null, null, null, null, "Title", "Detail", "Next");
            var legacy = new DailyPlanProgress("slot", "player", 1); legacy.SelectRoute(route);
            var student = new StudentLifeProgress("slot", "player", 100, 100);

            var foreign = legacy.ToSaveData(); foreign.PlayerId = "another";
            Assert.That(student.TryImportLegacyDailyPlan(foreign), Is.False);
            var future = legacy.ToSaveData(); future.CurrentDay = 3;
            Assert.That(student.TryImportLegacyDailyPlan(future), Is.False);
            Assert.That(student.TryImportLegacyDailyPlan(null), Is.False);

            Assert.That(student.TryImportLegacyDailyPlan(legacy.ToSaveData()), Is.True);
            Assert.That(student.DailyPlan.SelectedRouteId, Is.EqualTo(route.Id));
            Assert.That(student.TryImportLegacyDailyPlan(legacy.ToSaveData()), Is.False);
        }

        [Test]
        public void LegacyImport_IsRefusedAfterASavedPlanOrLivePlanning()
        {
            var route = new DailyPlanRoute("legacy.route", null, null, "SelfStudy", null, null, null, null, null, null, null, null);
            var legacy = new DailyPlanProgress("slot", "player", 1); legacy.SelectRoute(route);

            var saved = StudentLifeProgress.FromSaveData(new StudentLifeProgress("slot", "player", 1, 1).ToSaveData());
            Assert.That(saved.TryImportLegacyDailyPlan(legacy.ToSaveData()), Is.False, "A student save that already carries its plan wins.");

            var live = new StudentLifeProgress("slot", "player", 1, 1);
            live.DailyPlan.SelectRoute(route);
            Assert.That(live.TryImportLegacyDailyPlan(legacy.ToSaveData()), Is.False);
        }

        [Test]
        public void ForeignNestedDailyPlan_IsReplacedByAnEmptyPlanForThisStudent()
        {
            var t = new TypedFixture(); t.Assign(0);
            var data = t.Student.ToSaveData();
            data.DailyPlan.PlayerId = "another-player";
            var restored = StudentLifeProgress.FromSaveData(data);
            Assert.That(restored.DailyPlan.PlayerId, Is.EqualTo("typed-player"));
            Assert.That(restored.DailyPlan.SelectedRouteId, Is.Empty);
        }

        [Test]
        public void CareerHintRequirement_ComparesUnlockWithExpectation()
        {
            var student = Student();
            var needsHint = new DailyPlanCareerHintRequirement("career.chef", true);
            var needsNoHint = new DailyPlanCareerHintRequirement("career.chef", false);
            Assert.IsFalse(needsHint.IsSatisfied(new DailyPlanRouteContext(student), null));
            Assert.IsTrue(needsNoHint.IsSatisfied(new DailyPlanRouteContext(student), null));
            student.UnlockCareerHint("career.chef");
            Assert.IsTrue(needsHint.IsSatisfied(new DailyPlanRouteContext(student), null));
            Assert.IsFalse(needsNoHint.IsSatisfied(new DailyPlanRouteContext(student), null));
            Assert.IsTrue(new DailyPlanCareerHintRequirement("", false).IsSatisfied(new DailyPlanRouteContext(student), null));
        }

        [Test]
        public void PartTimeWorkRequirement_LooksAtTodayActivityThenTodayAndYesterdayLogs()
        {
            var reader = new PrefixLogReader();
            var requirement = new DailyPlanPartTimeWorkRequirement("work.cafe", true, reader);
            var notDone = new DailyPlanPartTimeWorkRequirement("work.cafe", false, reader);
            var student = Student();
            Assert.IsFalse(requirement.IsSatisfied(new DailyPlanRouteContext(student), null));
            Assert.IsTrue(notDone.IsSatisfied(new DailyPlanRouteContext(student), null));

            student.RecordActivityCompleted("activity.other", new[] { "work:work.cafe" });
            Assert.IsTrue(requirement.IsSatisfied(new DailyPlanRouteContext(student), null), "today result log");
            student.TryEndDay(null, out _);
            student.TryStartNextDay();
            Assert.IsTrue(requirement.IsSatisfied(new DailyPlanRouteContext(student), null), "yesterday result log");

            var direct = Student();
            direct.RecordActivityCompleted("work.cafe");
            Assert.IsTrue(new DailyPlanPartTimeWorkRequirement("work.cafe", true, null).IsSatisfied(new DailyPlanRouteContext(direct), null), "today activity, no reader needed");
            Assert.IsTrue(new DailyPlanPartTimeWorkRequirement(null, false, reader).IsSatisfied(new DailyPlanRouteContext(direct), null), "an empty work id never counts as done");
        }

        private sealed class PrefixLogReader : IPartTimeWorkLogReader
        {
            public string[] ReadWorkIds(string[] logs)
            {
                var ids = new List<string>();
                foreach (var log in logs ?? Array.Empty<string>()) if (log.StartsWith("work:", StringComparison.Ordinal)) ids.Add(log.Substring(5));
                return ids.ToArray();
            }
        }

        private static StudentLifeProgress Student() => new StudentLifeProgress("slot-a", "player-a", 100, 100);

        private static DailyPlanRoute Route(string id, string locationId, string growthRoute, IDailyPlanLocationActivity[] matching = null)
            => new DailyPlanRoute(id, id, locationId, growthRoute, matching, null, null, null, null, "objective." + id + ".title", "objective." + id + ".detail", "objective." + id + ".next");

        private static DailyPlanRoute RouteWith(IDailyPlanRouteRequirement requirement)
            => new DailyPlanRoute("daily.route", "daily.route.name", null, "SelfStudy", null, null, null, new[] { requirement }, null, "t", "d", "n");

        private static (DailyPlanProgress, DailyPlanRouteSet, FakeLocationActivity, StudentLifeProgress, DailyPlanRouteRunner) SelectedLibrary()
        {
            var activity = new FakeLocationActivity("activity.library.read", "location.library", "SelfStudy");
            var route = Route("daily.library.self-study", "location.library", "SelfStudy");
            var set = new DailyPlanRouteSet("daily.default", null, new[] { route }, 1);
            var student = Student();
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();
            runner.TrySelectRoute(daily, route, student);
            return (daily, set, activity, student, runner);
        }

        private sealed class FakeWorld : IDailyPlanWorldState
        {
            private readonly HashSet<string> _active;
            public FakeWorld(params string[] active) { _active = new HashSet<string>(active); }
            public bool IsActive(string flagId) => _active.Contains(flagId);
        }

        private sealed class EnergyThreshold : IDailyPlanRouteRequirement
        {
            private readonly int _minimum;
            public EnergyThreshold(int minimum) { _minimum = minimum; }
            public bool IsSatisfied(DailyPlanRouteContext context, DailyPlanRoute route) => context.StudentProgress != null && context.StudentProgress.Energy >= _minimum;
        }

        private sealed class TypedFixture
        {
            public const string Trait = "trait.diary-planning";
            public readonly LifeCalendar Calendar = new LifeCalendar(2, 3);
            public readonly StudentLifeProgress Student;
            public readonly CareerPractice Practice = new CareerPractice("practice.fixture", "Practice", null, null, null);
            public readonly FakePartTimeWork Work = new FakePartTimeWork("work.fixture");
            public readonly FakeLocationActivity Activity = new FakeLocationActivity("activity.fixture", null, "SelfStudy");
            public readonly DailyPlanRoute Route;
            public readonly DailyPlanRouteSet[] Sets;

            public TypedFixture()
            {
                Student = new StudentLifeProgress("typed-slot", "typed-player", 100, 100);
                Student.ConfigureCalendar(Calendar);
                Route = new DailyPlanRoute("route.fixture", "Plan", null, "SelfStudy", new IDailyPlanLocationActivity[] { Activity }, new[] { Practice }, new IMinigamePartTimeWork[] { Work },
                    null, new IDailyPlanRouteOutcome[] { new TraitOutcome(Trait) }, "Plan title", "Plan detail", "Plan next");
                Sets = new[] { new DailyPlanRouteSet("set.fixture", "Plan set", new[] { Route }, 2) }; // 같은 길을 두 칸에 넣는 시나리오라 하루 2번까지 허용한다
            }

            public void Assign(int slot) => Assert.That(Student.DailyPlan.TryAssignSlot(slot, Route, Student), Is.True);
            public bool RecordPractice(StudentLifeProgress actor, int day, string key) => new DailyPlanRouteRunner().TryRecordPractice(actor.DailyPlan, Sets, Practice, actor, day, key, out _);
            public bool RecordWork(StudentLifeProgress actor, int day, string key) => new DailyPlanRouteRunner().TryRecordPartTimeWork(actor.DailyPlan, Sets, Work, actor, day, key, out _);
        }
    }
}
