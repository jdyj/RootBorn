using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/LocationStateSystemTests.cs, LocationStateConditionStrategyTests.cs
    // 세계 상태·NPC 일정은 아직 옮기지 않은 영역이라 테스트용 조회 객체로 바꿨다.
    public sealed class LocationStateTests
    {
        private static LocationState State(string id, string locationId, int priority, ILocationStateCondition[] conditions = null, ILocationStateEffect[] effects = null)
            => new LocationState(id, id + ".name", id + ".description", locationId, priority, conditions, effects);

        private static LocationStateContext Context(string locationId, string timeSlotId, StudentLifeProgress progress, ILocationStateWorldFlags world = null,
            QuestLog quests = null, ILocationStateNpcSchedules schedules = null, LocationStateProgress stateProgress = null)
            => new LocationStateContext(locationId, timeSlotId, progress, world, quests, schedules, stateProgress);

        private sealed class Flags : ILocationStateWorldFlags
        {
            public readonly HashSet<string> Active = new HashSet<string>();
            public bool IsActive(string flagId) => Active.Contains(flagId);
        }

        private static QuestLog QuestLogWith(string questId, QuestStage stage)
        {
            var log = new QuestLog(new[] { new QuestDefinition(questId, questId, questId, Array.Empty<QuestObjective>()) });
            log.LoadFromSaveData(new QuestLogSaveData { Quests = new[] { new QuestProgressSaveData { QuestId = questId, State = stage, StateName = stage.ToString() } } });
            return log;
        }

        private sealed class Schedules : ILocationStateNpcSchedules
        {
            public string LastTimeSlotId;
            public string ResolveLocationId(string scheduleId, int day, string timeSlotId, StudentLifeProgress progress)
            {
                LastTimeSlotId = timeSlotId;
                return scheduleId == "schedule.librarian" && timeSlotId == "time.afternoon" ? "location.library" : string.Empty;
            }
        }

        [Test]
        public void LOCATION_STATE_EDIT_001_003_RegistryDataPathLoadsAndTimeConditionSelectsWithoutEntityBranches()
        {
            var morningState = State("location.state.library-morning", "location.library", 10, new ILocationStateCondition[] { new LocationStateTimeSlotCondition(new[] { "time.morning" }) });
            var eveningState = State("location.state.library-evening", "location.library", 20, new ILocationStateCondition[] { new LocationStateTimeSlotCondition(new[] { "time.evening" }) });
            var cache = new LocationStateLookupCache(new[] { morningState, eveningState });
            var resolver = new LocationStateResolver(cache, LocationStateConflictMode.HighestPriority);
            var progress = new StudentLifeProgress("slot", "player", 10, 10, 0, 8 * 60);

            Assert.That(resolver.Resolve(Context("location.library", "time.morning", progress)).PrimaryState, Is.SameAs(morningState));
            Assert.That(resolver.Resolve(Context("location.library", "time.evening", progress)).PrimaryState, Is.SameAs(eveningState));
            Assert.That(cache.BuildCount, Is.EqualTo(1));
        }

        [Test]
        public void LOCATION_STATE_EDIT_004_006_StrategyConditionsAndEffectsExposeActivitiesNpcsCluesObjectsPortalsAndShops()
        {
            var effect = new LocationStateAvailabilityEffect(new[] { "location.activity.library-archive" }, new[] { "npc.librarian" }, new[] { "clue.old-ledger" },
                new[] { "object.archive-desk" }, new[] { "portal.backroom" }, new[] { "shop.library-special" });
            var state = State("location.state.library-archive", "location.library", 50,
                new ILocationStateCondition[] { new LocationStateWorldStateCondition("world.library.archive-open", true), new LocationStateRelationshipCondition("relationship.librarian", 2) },
                new ILocationStateEffect[] { effect });
            var student = new StudentLifeProgress("slot", "player", 10, 10, 0, 18 * 60);
            student.AddRelationship("relationship.librarian", 2);
            var world = new Flags();
            world.Active.Add("world.library.archive-open");

            var summary = new LocationStateResolver(new LocationStateLookupCache(new[] { state }), LocationStateConflictMode.HighestPriority)
                .Resolve(Context("location.library", "time.evening", student, world));

            Assert.That(summary.PrimaryState, Is.SameAs(state));
            Assert.That(summary.AvailableActivityIds, Does.Contain("location.activity.library-archive"));
            Assert.That(summary.PresentNpcIds, Does.Contain("npc.librarian"));
            Assert.That(summary.ClueIds, Does.Contain("clue.old-ledger"));
            Assert.That(summary.InteractableObjectIds, Does.Contain("object.archive-desk"));
            Assert.That(summary.PortalIds, Does.Contain("portal.backroom"));
            Assert.That(summary.ShopItemIds, Does.Contain("shop.library-special"));
        }

        [Test]
        public void LOCATION_STATE_EDIT_004_StatusQuestAndNpcScheduleConditionsAreDataStrategies()
        {
            var progress = new StudentLifeProgress("slot", "player", 10, 10, 0, 13 * 60);
            progress.AddStatus("status.focused", 2);
            var quests = QuestLogWith("quest.archive", QuestStage.Completed);
            Assert.That(quests.GetState("quest.archive"), Is.EqualTo(QuestStage.Completed));
            var context = Context("location.library", "time.afternoon", progress, null, quests, new Schedules());

            Assert.That(new LocationStateStatusCondition("status.focused", 2).IsSatisfied(in context), Is.True);
            Assert.That(new LocationStateQuestStateCondition("quest.archive", QuestStage.Completed).IsSatisfied(in context), Is.True);
            Assert.That(new LocationStateNpcScheduleCondition("schedule.librarian", "location.library").IsSatisfied(in context), Is.True);
        }

        [Test]
        public void LOCATION_STATE_EDIT_005_007_OneLocationSupportsMultipleTimeStatesAndConflictPolicies()
        {
            var low = State("location.state.workshop-cleanup", "location.workshop", 10);
            var high = State("location.state.workshop-shipping", "location.workshop", 30);
            var cache = new LocationStateLookupCache(new[] { low, high });

            var highestSummary = new LocationStateResolver(cache, LocationStateConflictMode.HighestPriority).Resolve(Context("location.workshop", "time.evening", new StudentLifeProgress("slot", "player", 10, 10, 0, 18 * 60)));
            var mergedSummary = new LocationStateResolver(cache, LocationStateConflictMode.MergeAll).Resolve(Context("location.workshop", "time.evening", new StudentLifeProgress("slot", "player", 10, 10, 0, 18 * 60)));

            Assert.That(highestSummary.PrimaryState, Is.SameAs(high));
            Assert.That(highestSummary.StateIds.Length, Is.EqualTo(1));
            Assert.That(mergedSummary.StateIds, Is.EquivalentTo(new[] { low.Id, high.Id }));
        }

        [Test]
        public void LOCATION_STATE_EDIT_008_ProgressPersistsDiscoveryRewardsLastVisitTodaySummaryAndHiddenHints()
        {
            var progress = new LocationStateProgress("slot-a", "player-a");
            progress.MarkDiscovered("location.library", "location.state.library-evening", 2, "time.evening", "Quiet Evening", false);
            progress.MarkRewardClaimed("location.state.library-evening", "reward.note");
            progress.MarkHiddenHintVisible("location.state.library-archive");

            var restored = LocationStateProgress.FromSaveData(progress.ToSaveData(), "fallback", "fallback-player");

            Assert.That(restored.HasDiscovered("location.state.library-evening"), Is.True);
            Assert.That(restored.HasRewardClaimed("location.state.library-evening", "reward.note"), Is.True);
            Assert.That(restored.GetLastVisitedStateId("location.library"), Is.EqualTo("location.state.library-evening"));
            Assert.That(restored.IsHiddenHintVisible("location.state.library-archive"), Is.True);
            Assert.That(restored.TodaySummary.Length, Is.EqualTo(1));
            Assert.That(restored.SaveSlot, Is.EqualTo("slot-a"));
            Assert.That(restored.PlayerId, Is.EqualTo("player-a"));
        }

        // 더한 경계

        [Test]
        public void ChangedBehavior_HiddenStateMakesTheSummaryAndTodayRecordHidden()
        {
            var hidden = new LocationState("state.secret", "state.secret.name", "state.secret.description", "location.cafe", 1, null, null, isHidden: true);
            var stateProgress = new LocationStateProgress("slot", "player");
            var progress = new StudentLifeProgress("slot", "player", 10, 10, 0, 18 * 60);
            var resolver = new LocationStateResolver(new LocationStateLookupCache(new[] { hidden }), LocationStateConflictMode.FallbackFirst);

            var summary = resolver.Resolve(Context("location.cafe", "time.evening", progress, stateProgress: stateProgress));

            Assert.That(summary.IsHidden, Is.True);
            Assert.That(stateProgress.TodaySummary[0].Hidden, Is.True);
        }

        [Test]
        public void ResolveMarksPrimaryStateDiscoveredOnceAndFallbackModeTakesFirstMatch()
        {
            var first = State("state.first", "location.cafe", 1);
            var second = State("state.second", "location.cafe", 9);
            var stateProgress = new LocationStateProgress("slot", "player");
            var progress = new StudentLifeProgress("slot", "player", 10, 10, 0, 18 * 60);
            var resolver = new LocationStateResolver(new LocationStateLookupCache(new[] { first, second }), LocationStateConflictMode.FallbackFirst);

            var summary = resolver.Resolve(Context("location.cafe", "time.evening", progress, stateProgress: stateProgress));
            resolver.Resolve(Context("location.cafe", "time.night", progress, stateProgress: stateProgress));

            Assert.That(summary.PrimaryState, Is.SameAs(first));
            Assert.That(summary.DisplayName, Is.EqualTo("state.first.name"));
            Assert.That(summary.Description, Is.EqualTo("state.first.description"));
            Assert.That(summary.IsHidden, Is.False);
            Assert.That(stateProgress.HasDiscovered("state.first"), Is.True);
            Assert.That(stateProgress.HasDiscovered("state.second"), Is.False);
            Assert.That(stateProgress.TodaySummary.Length, Is.EqualTo(1));
            Assert.That(stateProgress.TodaySummary[0].TimeSlotId, Is.EqualTo("time.evening"));
            Assert.That(stateProgress.ToSaveData().Records[0].FirstDiscoveredTimeSlotId, Is.EqualTo("time.evening"));
        }

        [Test]
        public void NoMatchOrUnknownLocationGivesEmptySummaryWithoutDiscovery()
        {
            var stateProgress = new LocationStateProgress("slot", "player");
            var state = State("state.a", "location.a", 1, new ILocationStateCondition[] { new LocationStateActivityLogCondition("activity.read", 2) });
            var progress = new StudentLifeProgress("slot", "player", 10, 10);
            progress.RecordActivityCompleted("activity.read");
            var resolver = new LocationStateResolver(new LocationStateLookupCache(new[] { state, null }), LocationStateConflictMode.HighestPriority);

            var notYet = resolver.Resolve(Context("location.a", null, progress, stateProgress: stateProgress));
            var unknown = resolver.Resolve(Context("", null, progress));
            Assert.That(notYet.PrimaryState, Is.Null);
            Assert.That(notYet.LocationId, Is.EqualTo("location.a"));
            Assert.That(unknown.StateIds, Is.Empty);
            Assert.That(stateProgress.TodaySummary, Is.Empty);

            progress.RecordActivityCompleted("activity.read");
            Assert.That(resolver.Resolve(Context("location.a", null, progress)).PrimaryState, Is.SameAs(state));
        }

        [Test]
        public void TimeSlotFallsBackToProgressClockWhenNotGiven()
        {
            var condition = new LocationStateTimeSlotCondition(new[] { "time.evening" });
            var schedules = new Schedules();

            Assert.That(condition.IsSatisfied(Context("l", null, new StudentLifeProgress("s", "p", 1, 1, 0, 18 * 60))), Is.True);
            Assert.That(condition.IsSatisfied(Context("l", null, new StudentLifeProgress("s", "p", 1, 1, 0, 8 * 60))), Is.False);
            Assert.That(new LocationStateTimeSlotCondition(null).IsSatisfied(Context("l", null, null)), Is.True);
            new LocationStateNpcScheduleCondition("schedule.librarian", null).IsSatisfied(Context("location.library", null, new StudentLifeProgress("s", "p", 1, 1, 0, 13 * 60), schedules: schedules));
            Assert.That(schedules.LastTimeSlotId, Is.EqualTo("time.afternoon"));

            Assert.That(LocationStateTimeSlots.FromMinutes(0), Is.EqualTo("time.morning"));
            Assert.That(LocationStateTimeSlots.FromMinutes(8 * 60), Is.EqualTo("time.morning"));
            Assert.That(LocationStateTimeSlots.FromMinutes(7 * 60), Is.EqualTo("time.morning"));
            Assert.That(LocationStateTimeSlots.FromMinutes(21 * 60), Is.EqualTo("time.night"));
        }

        [Test]
        public void WorldStateConditionMatchesExpectedInactiveAndMissingLookups()
        {
            var inactive = new LocationStateWorldStateCondition("flag", false);
            var active = new LocationStateWorldStateCondition("flag", true);
            var context = Context("l", "t", null);

            Assert.That(inactive.IsSatisfied(in context), Is.True);
            Assert.That(active.IsSatisfied(in context), Is.False);
            Assert.That(new LocationStateQuestStateCondition("q", QuestStage.NotStarted).IsSatisfied(in context), Is.False);
            Assert.That(new LocationStateNpcScheduleCondition("schedule.librarian", "location.library").IsSatisfied(in context), Is.False);
        }

        [Test]
        public void RewardClaimIsOnceAndEmptyIdsAreIgnored()
        {
            var progress = new LocationStateProgress(null, null);
            Assert.That(progress.SaveSlot, Is.EqualTo("default"));
            Assert.That(progress.MarkRewardClaimed("s", "r"), Is.True);
            Assert.That(progress.MarkRewardClaimed("s", "r"), Is.False);
            Assert.That(progress.MarkRewardClaimed("", "r"), Is.False);
            progress.MarkDiscovered("l", "", 1, "t", "n", false);
            Assert.That(progress.TodaySummary, Is.Empty);

            var restored = LocationStateProgress.FromSaveData(null, "fb", "fp");
            Assert.That(restored.SaveSlot, Is.EqualTo("fb"));
            Assert.That(restored.PlayerId, Is.EqualTo("fp"));
            Assert.That(LocationStateProgressPersistence.FileNameFor("player"), Is.EqualTo("location-state-progress.json"));
            Assert.That(LocationStateProgressPersistence.FileNameFor("p 2"), Is.EqualTo("location-state-progress-p_2.json"));
        }
    }
}
