using System;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/DailyEventLoopTests.cs, DailyEventResultSummaryTests.cs (Registry 검사 DailyEventRegistryTests는 에셋 검사라 옮기지 않음)
    public sealed class DailyEventTests
    {
        private static DailyEventKind Kind(string id, bool deferrable = false, bool timed = false, bool repeatable = false, int deferDays = 0, int dueTimeMinutes = 0)
            => new DailyEventKind(id, true, deferrable, timed, false, repeatable, false, deferDays, dueTimeMinutes);

        private static DailyEventChoice Choice(string id, int timeCostMinutes = 0, int energyCost = 0, int focusCost = 0, bool isDecline = false, IDailyEventOutcome[] outcomes = null, ActivityTimePolicy timePolicy = null)
            => new DailyEventChoice(id, id, timePolicy, timeCostMinutes, energyCost, focusCost, 0, false, isDecline, null, outcomes);

        private static DailyEvent Event(string id, string locationId, DailyEventKind kind, IDailyEventAvailabilityRule[] rules, params DailyEventChoice[] choices)
            => new DailyEvent(id, id, locationId, string.Empty, kind, rules, choices);

        [Test]
        public void DAILYEVENT_EDIT_002_003_ResolverUsesDataRulesAndChoiceOutcomesApplyWithoutEntityIdBranches()
        {
            var choice = Choice("daily-event-choice.read-to-end", 20, 1, 0, outcomes: new IDailyEventOutcome[]
            {
                new DailyEventTraitDeltaOutcome("trait.curiosity", 2), new DailyEventRelationshipDeltaOutcome("relationship.librarian", 1),
            });
            var dailyEvent = Event("daily-event.library-hard-book", "location.library", Kind("daily-event-kind.deferrable", deferrable: true),
                new IDailyEventAvailabilityRule[] { new DailyEventLocationAvailabilityRule("location.library") }, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var resolver = new DailyEventResolver(new[] { dailyEvent });

            var available = resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, "location.library", 1, 9 * 60));

            Assert.That(available.Length, Is.EqualTo(1));
            Assert.That(available[0], Is.SameAs(dailyEvent));
            Assert.That(resolver.BuildCount, Is.EqualTo(1));

            Assert.That(new DailyEventRunner().TryChoose(dailyEvent, choice, progress, eventProgress, "daily-choice-request-1", out var result), Is.True);
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(progress.GetTraitValue("trait.curiosity"), Is.EqualTo(2));
            Assert.That(progress.GetRelationshipValue("relationship.librarian"), Is.EqualTo(1));
            Assert.That(progress.GetTodayResultLogIds(), Does.Contain("daily-event:daily-event.library-hard-book:daily-event-choice.read-to-end:+trait.curiosity=2"));
            Assert.That(progress.GetTodayResultLogIds(), Does.Contain("daily-event:daily-event.library-hard-book:daily-event-choice.read-to-end:+relationship.librarian=1"));
            Assert.That(eventProgress.GetRecord(dailyEvent.Id).State, Is.EqualTo(DailyEventStates.Completed));
            Assert.That(progress.TimeMinutes, Is.EqualTo(20));
            Assert.That(progress.Energy, Is.EqualTo(7));
        }

        [Test]
        public void DAILYEVENT_EDIT_004_DeferrableEventPersistsAndReappearsOnConfiguredDay()
        {
            var dailyEvent = Event("daily-event.square-friend", "location.square", Kind("daily-event-kind.deferrable", deferrable: true, deferDays: 1),
                new IDailyEventAvailabilityRule[] { new DailyEventLocationAvailabilityRule(null) }, Choice("daily-event-choice.help-now"));
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var resolver = new DailyEventResolver(new[] { dailyEvent });

            Assert.That(new DailyEventRunner().TryDefer(dailyEvent, eventProgress, 1, out var deferred), Is.True);
            Assert.That(deferred.State, Is.EqualTo(DailyEventStates.Deferred));
            Assert.That(deferred.DueDay, Is.EqualTo(2));

            var restored = DailyEventProgress.FromSaveData(eventProgress.ToSaveData());
            Assert.That(restored.GetRecord(dailyEvent.Id).State, Is.EqualTo(DailyEventStates.Deferred));
            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, restored, "location.square", 1, 9 * 60)), Is.Empty);
            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, restored, "location.square", 2, 9 * 60)).Length, Is.EqualTo(1));
        }

        [Test]
        public void DAILYEVENT_EDIT_005_TimedEventExpiresWithoutBlockingProgression()
        {
            var dailyEvent = Event("daily-event.workshop-request", "location.workshop", Kind("daily-event-kind.timed", timed: true, dueTimeMinutes: 17 * 60),
                new IDailyEventAvailabilityRule[] { new DailyEventLocationAvailabilityRule("location.workshop") }, Choice("daily-event-choice.accept-work"));
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var resolver = new DailyEventResolver(new[] { dailyEvent });

            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, "location.workshop", 1, 16 * 60)).Length, Is.EqualTo(1));
            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, "location.workshop", 1, 18 * 60)), Is.Empty);
            Assert.That(eventProgress.GetRecord(dailyEvent.Id).State, Is.EqualTo(DailyEventStates.Expired));
            Assert.That(progress.DayState, Is.EqualTo(StudentDayState.InProgress));
        }

        [Test]
        public void DAILYEVENT_EDIT_006_CompletionRequestIdDedupesChoiceOutcomesAcrossSaveLoad()
        {
            var choice = Choice("daily-event-choice.plan", outcomes: new IDailyEventOutcome[] { new DailyEventTraitDeltaOutcome("trait.planning", 3) });
            var dailyEvent = Event("daily-event.home-plan", "location.home", Kind("daily-event-kind.optional"), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var runner = new DailyEventRunner();

            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "same-request", out _), Is.True);
            var restoredEvents = DailyEventProgress.FromSaveData(eventProgress.ToSaveData());
            var restoredStudent = StudentLifeProgress.FromSaveData(progress.ToSaveData());

            Assert.That(runner.TryChoose(dailyEvent, choice, restoredStudent, restoredEvents, "same-request", out var duplicate), Is.False);
            Assert.That(duplicate.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(restoredStudent.GetTraitValue("trait.planning"), Is.EqualTo(3));
        }

        [Test]
        public void DAILYEVENT_EDIT_008_DeclineDoesNotBlockProgressAndResultSummaryCapturesChoiceLogs()
        {
            var choice = Choice("daily-event-choice.no", isDecline: true);
            var dailyEvent = Event("daily-event.decline-test", null, Kind("daily-event-kind.optional"), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");

            Assert.That(new DailyEventRunner().TryDecline(dailyEvent, eventProgress, progress.CurrentDay, out var declined), Is.True);
            Assert.That(declined.State, Is.EqualTo(DailyEventStates.Declined));
            Assert.That(progress.DayState, Is.EqualTo(StudentDayState.InProgress));

            declined.MarkCompleted(choice.Id, "request", 1, new[] { "daily-event:daily-event.decline-test:daily-event-choice.no:+trait.test=1" });
            var summary = DailyEventResultSummary.FromRecord(declined);

            Assert.That(summary.EventId, Is.EqualTo("daily-event.decline-test"));
            Assert.That(summary.SelectedChoiceId, Is.EqualTo("daily-event-choice.no"));
            Assert.That(summary.State, Is.EqualTo(DailyEventStates.Completed));
            Assert.That(summary.ResultSummaryLogIds, Does.Contain("daily-event:daily-event.decline-test:daily-event-choice.no:+trait.test=1"));
        }

        // 더한 경계

        [Test]
        public void InsufficientEnergyRejectsChoiceWithoutChangingAnything()
        {
            var choice = Choice("choice.tiring", energyCost: 9, outcomes: new IDailyEventOutcome[] { new DailyEventTraitDeltaOutcome("trait.grit", 1) });
            var dailyEvent = Event("event.hard", null, Kind("kind"), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");

            Assert.That(new DailyEventRunner().TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out var result), Is.False);

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.InsufficientResources));
            Assert.That(progress.Energy, Is.EqualTo(8));
            Assert.That(progress.GetTraitValue("trait.grit"), Is.EqualTo(0));
            Assert.That(progress.HasAppliedRequest("r1"), Is.False);
            Assert.That(eventProgress.GetRecord("event.hard").State, Is.EqualTo(DailyEventStates.Unseen));
        }

        [Test]
        public void ChoiceAvailabilityRuleIsCheckedAndRejectionChangesNothing()
        {
            // Unity 판과 달라진 동작: 실행기가 선택지의 가용 규칙을 검사한다(원본은 보지 않았다).
            var choice = new DailyEventChoice("choice.friend", "choice.friend", null, 10, 1, 0, 0, false, false,
                new IDailyEventAvailabilityRule[] { new DailyEventRelationshipThresholdRule("relationship.friend", 3) },
                new IDailyEventOutcome[] { new DailyEventTraitDeltaOutcome("trait.kind", 1) });
            var dailyEvent = Event("event.friend", "location.park", Kind("kind"), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var runner = new DailyEventRunner();

            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out var rejected), Is.False);
            Assert.That(rejected.Kind, Is.EqualTo(LifeActivityResultKind.RequirementFailed));
            Assert.That(progress.Energy, Is.EqualTo(8));
            Assert.That(progress.GetTraitValue("trait.kind"), Is.EqualTo(0));
            Assert.That(progress.HasAppliedRequest("r1"), Is.False);
            Assert.That(eventProgress.GetRecord("event.friend").State, Is.EqualTo(DailyEventStates.Unseen));

            progress.AddRelationship("relationship.friend", 3);
            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out var applied), Is.True);
            Assert.That(applied.Kind, Is.EqualTo(LifeActivityResultKind.Applied));

            var located = new DailyEventChoice("choice.here", "choice.here", null, 0, 0, 0, 0, false, false,
                new IDailyEventAvailabilityRule[] { new DailyEventLocationAvailabilityRule(null) }, null);
            var locatedEvent = Event("event.here", "location.park", Kind("kind"), null, located);
            Assert.That(runner.TryChoose(locatedEvent, located, progress, eventProgress, "r2", "location.cafe", out _), Is.False);
            Assert.That(runner.TryChoose(locatedEvent, located, progress, eventProgress, "r2", out _), Is.True);
        }

        [Test]
        public void EmptyRequestIdIsInvalidAndDoesNotApply()
        {
            var choice = Choice("choice", outcomes: new IDailyEventOutcome[] { new DailyEventTraitDeltaOutcome("trait.grit", 1) });
            var dailyEvent = Event("event", null, Kind("kind"), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);

            Assert.That(new DailyEventRunner().TryChoose(dailyEvent, choice, progress, new DailyEventProgress("slot", "player"), "", out var result), Is.False);
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.InvalidRequest));
            Assert.That(progress.GetTraitValue("trait.grit"), Is.EqualTo(0));
        }

        [Test]
        public void SameRequestInSameSessionIsRejectedAsDuplicate()
        {
            var choice = Choice("choice", outcomes: new IDailyEventOutcome[] { new DailyEventSkillDeltaOutcome("skill.talk", 2) });
            var dailyEvent = Event("event", null, Kind("kind", repeatable: true), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var runner = new DailyEventRunner();

            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out _), Is.True);
            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out var duplicate), Is.False);
            Assert.That(duplicate.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(progress.GetSkillValue("skill.talk"), Is.EqualTo(2));
            Assert.That(eventProgress.GetRecord("event").RepeatCount, Is.EqualTo(1));
        }

        [Test]
        public void CareerHintOutcomeLogsOnlyFirstUnlockAndStatusDeltaLogsNegativeWithoutPlus()
        {
            var choice = Choice("choice", outcomes: new IDailyEventOutcome[] { new DailyEventCareerHintOutcome("career.nurse"), new DailyEventStatusDeltaOutcome("status.tired", -2) });
            var dailyEvent = Event("event", null, Kind("kind"), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            progress.AddStatus("status.tired", 5);

            string[] first = choice.ApplyOutcomes(progress, dailyEvent);
            string[] second = choice.ApplyOutcomes(progress, dailyEvent);

            Assert.That(first, Is.EqualTo(new[] { "daily-event:event:choice:unlock=career.nurse", "daily-event:event:choice:status.tired=-2" }));
            Assert.That(second, Is.EqualTo(new[] { "daily-event:event:choice:status.tired=-2" }));
            Assert.That(progress.IsCareerHintUnlocked("career.nurse"), Is.True);
            Assert.That(progress.GetStatusValue("status.tired"), Is.EqualTo(1));
        }

        [Test]
        public void RelationshipThresholdRuleAndLocationMismatchHideEvent()
        {
            var dailyEvent = Event("event", "location.cafe", Kind("kind"), new IDailyEventAvailabilityRule[]
            {
                new DailyEventLocationAvailabilityRule(null), new DailyEventRelationshipThresholdRule("relationship.barista", 3),
            }, Choice("choice"));
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var resolver = new DailyEventResolver(new[] { dailyEvent });

            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, "location.cafe", 1, 0)), Is.Empty);
            progress.AddRelationship("relationship.barista", 3);
            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, "location.park", 1, 0)), Is.Empty);
            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, null, 1, 0)), Is.Empty);
            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, "location.cafe", 1, 0)).Length, Is.EqualTo(1));
            Assert.That(eventProgress.GetRecord("event").State, Is.EqualTo(DailyEventStates.Available));
            Assert.That(eventProgress.GetRecord("event").FirstSeenDay, Is.EqualTo(1));
        }

        [Test]
        public void CompletedNonRepeatableEventIsHiddenButRepeatableReappears()
        {
            var once = Event("event.once", null, Kind("kind"), null, Choice("c1"));
            var again = Event("event.again", null, Kind("kind.repeat", repeatable: true), null, Choice("c2"));
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var runner = new DailyEventRunner();
            runner.TryChoose(once, once.Choices[0], progress, eventProgress, "r1", out _);
            runner.TryChoose(again, again.Choices[0], progress, eventProgress, "r2", out _);

            var available = new DailyEventResolver(new[] { once, again }).GetAvailableEvents(new DailyEventContext(progress, eventProgress, null, 1, 0));

            Assert.That(available, Is.EqualTo(new[] { again }));
        }

        [Test]
        public void ChangedBehavior_AnsweredEventCannotBeChosenAgainWithANewRequestId()
        {
            // 원본은 요청 id 중복만 봐서, 이미 고른 이벤트를 새 요청 id로 다시 고르면 결과가 또 적용됐다.
            var choice = Choice("c", energyCost: 1, outcomes: new IDailyEventOutcome[] { new DailyEventTraitDeltaOutcome("trait.grit", 1) });
            var dailyEvent = Event("event.once", null, Kind("kind"), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var runner = new DailyEventRunner();
            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out _), Is.True);
            var before = progress.ToSaveData();

            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r2", out var again), Is.False);

            Assert.That(again.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(progress.GetTraitValue("trait.grit"), Is.EqualTo(1));
            Assert.That(progress.Energy, Is.EqualTo(7));
            Assert.That(progress.HasAppliedRequest("r2"), Is.False);
            Assert.That(eventProgress.GetRecord("event.once").CompletionRequestId, Is.EqualTo("r1"));
            Assert.That(eventProgress.GetRecord("event.once").RepeatCount, Is.EqualTo(1));
            Assert.That(progress.ToSaveData().AppliedRequestIds, Is.EqualTo(before.AppliedRequestIds));
        }

        [Test]
        public void ChangedBehavior_DeclinedEventCannotBeChosenLaterTheSameDay()
        {
            var choice = Choice("c", outcomes: new IDailyEventOutcome[] { new DailyEventTraitDeltaOutcome("trait.grit", 1) });
            var dailyEvent = Event("event.once", null, Kind("kind"), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var runner = new DailyEventRunner();
            Assert.That(runner.TryDecline(dailyEvent, eventProgress, progress.CurrentDay, out _), Is.True);

            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out var result), Is.False);

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(progress.GetTraitValue("trait.grit"), Is.EqualTo(0));
            Assert.That(eventProgress.GetRecord("event.once").State, Is.EqualTo(DailyEventStates.Declined));
        }

        [Test]
        public void ChangedBehavior_DeferredEventCannotBeChosenBeforeItComesBack()
        {
            var choice = Choice("c");
            var dailyEvent = Event("event.later", null, Kind("kind", deferrable: true, deferDays: 2), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var runner = new DailyEventRunner();
            Assert.That(runner.TryDefer(dailyEvent, eventProgress, progress.CurrentDay, out _), Is.True);

            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out var result), Is.False);
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(eventProgress.GetRecord("event.later").State, Is.EqualTo(DailyEventStates.Deferred));
        }

        [Test]
        public void RepeatableEventCanStillBeChosenAgainWithANewRequestId()
        {
            var choice = Choice("c", outcomes: new IDailyEventOutcome[] { new DailyEventTraitDeltaOutcome("trait.grit", 1) });
            var dailyEvent = Event("event.again", null, Kind("kind.repeat", repeatable: true), null, choice);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var eventProgress = new DailyEventProgress("slot", "player");
            var runner = new DailyEventRunner();

            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r1", out _), Is.True);
            Assert.That(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "r2", out _), Is.True, "다시 나오는 이벤트는 목록 규칙과 같이 다시 고를 수 있다");
            Assert.That(progress.GetTraitValue("trait.grit"), Is.EqualTo(2));
        }

        [Test]
        public void NonDeferrableEventCannotBeDeferredAndCooldownBlocksUntilDay()
        {
            var dailyEvent = Event("event", null, Kind("kind"), null, Choice("c"));
            var eventProgress = new DailyEventProgress("slot", "player");

            Assert.That(new DailyEventRunner().TryDefer(dailyEvent, eventProgress, 1, out var record), Is.False);
            Assert.That(record.State, Is.EqualTo(DailyEventStates.Unseen));

            record.MarkCooldown(3);
            var resolver = new DailyEventResolver(new[] { dailyEvent });
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, null, 2, 0)), Is.Empty);
            Assert.That(resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, null, 3, 0)).Length, Is.EqualTo(1));
        }

        [Test]
        public void SaveDataRoundTripKeepsEveryRecordField()
        {
            var eventProgress = new DailyEventProgress("slot-a", "player-1");
            eventProgress.GetRecord("event.a").MarkCompleted("choice.a", "req-a", 3, new[] { "log-1", "log-1", "log-2" });
            eventProgress.GetRecord("event.b").MarkExpired(2, 600);

            var restored = DailyEventProgress.FromSaveData(eventProgress.ToSaveData());
            var a = restored.GetRecord("event.a");
            var b = restored.GetRecord("event.b");

            Assert.That(restored.SaveSlot, Is.EqualTo("slot-a"));
            Assert.That(restored.PlayerId, Is.EqualTo("player-1"));
            Assert.That(a.State, Is.EqualTo(DailyEventStates.Completed));
            Assert.That(a.SelectedChoiceId, Is.EqualTo("choice.a"));
            Assert.That(a.CompletionRequestId, Is.EqualTo("req-a"));
            Assert.That(a.RepeatCount, Is.EqualTo(1));
            Assert.That(a.FirstSeenDay, Is.EqualTo(3));
            Assert.That(a.ResultSummaryLogIds, Is.EqualTo(new[] { "log-1", "log-2" }));
            Assert.That(b.State, Is.EqualTo(DailyEventStates.Expired));
            Assert.That(b.DueDay, Is.EqualTo(2));
            Assert.That(b.DueWorldTime, Is.EqualTo(600));
            Assert.That(restored.HasCompletionRequest("req-a"), Is.True);
            Assert.That(DailyEventProgress.FromSaveData(null).SaveSlot, Is.EqualTo("default"));
        }

        [Test]
        public void DailyEventAndChoice_DisplayNameKeyFallsBackToId()
        {
            // Unity StudentLifeDefinitionBase.DisplayNameKey: 비면 id.
            var choice = new DailyEventChoice("choice.a", "summary.a", null, 0, 0, 0, 0, false, false, null, null, "choice.a.name");
            var unnamedChoice = new DailyEventChoice("choice.b", "summary.b", null, 0, 0, 0, 0, false, false, null, null);
            var dailyEvent = new DailyEvent("event.a", "event.a.description", "location.a", null, null, null, new[] { choice }, "event.a.name");
            var unnamedEvent = new DailyEvent("event.b", null, null, null, null, null, null);

            Assert.That(choice.DisplayNameKey, Is.EqualTo("choice.a.name"));
            Assert.That(unnamedChoice.DisplayNameKey, Is.EqualTo("choice.b"));
            Assert.That(dailyEvent.DisplayNameKey, Is.EqualTo("event.a.name"));
            Assert.That(unnamedEvent.DisplayNameKey, Is.EqualTo("event.b"));
        }
    }
}
