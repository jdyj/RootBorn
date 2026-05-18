using System;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyEventLoopTests
    {
        [Test]
        public void DAILYEVENT_EDIT_002_003_ResolverUsesDataRulesAndChoiceOutcomesApplyWithoutEntityIdBranches()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var kind = ScriptableObject.CreateInstance<DailyEventKindDefinition>();
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            var locationRule = ScriptableObject.CreateInstance<DailyEventLocationAvailabilityRule>();
            var traitOutcome = ScriptableObject.CreateInstance<DailyEventTraitDeltaOutcome>();
            var relationshipOutcome = ScriptableObject.CreateInstance<DailyEventRelationshipDeltaOutcome>();
            var choice = ScriptableObject.CreateInstance<DailyEventChoiceDefinition>();
            var dailyEvent = ScriptableObject.CreateInstance<DailyEventDefinition>();
            try
            {
                location.ConfigureForTests("location.library", "location.library.name", Vector2.zero, Array.Empty<DiscoveryDefinition>());
                kind.ConfigureForTests("daily-event-kind.deferrable", "Deferrable", true, true, false, false, false, false, 0, 0);
                trait.ConfigureForTests("trait.curiosity", "trait.curiosity.name");
                relationship.ConfigureForTests("relationship.librarian", "relationship.librarian.name");
                locationRule.ConfigureForTests(location);
                traitOutcome.ConfigureForTests(trait, 2);
                relationshipOutcome.ConfigureForTests(relationship, 1);
                choice.ConfigureForTests(
                    "daily-event-choice.read-to-end",
                    "Read to the end",
                    "Curiosity and librarian relationship increase.",
                    20,
                    1,
                    0,
                    0,
                    false,
                    false,
                    Array.Empty<DailyEventAvailabilityRuleBase>(),
                    new DailyEventOutcomeBase[] { traitOutcome, relationshipOutcome });
                dailyEvent.ConfigureForTests(
                    "daily-event.library-hard-book",
                    "Hard Book",
                    "A difficult book catches your eye.",
                    location,
                    null,
                    kind,
                    new DailyEventAvailabilityRuleBase[] { locationRule },
                    new[] { choice });

                var progress = new StudentLifeProgress("slot", "player", 8, 8);
                var eventProgress = new DailyEventProgress("slot", "player");
                var resolver = new DailyEventResolver(new[] { dailyEvent });
                var context = new DailyEventContext(progress, eventProgress, location, 1, 9 * 60);

                var available = resolver.GetAvailableEvents(context);

                Assert.AreEqual(1, available.Length, "DAILYEVENT-EDIT-002 failed: location rule did not expose the data-driven event.");
                Assert.AreSame(dailyEvent, available[0]);
                Assert.AreEqual(1, resolver.BuildCount, "DAILYEVENT-EDIT-007 failed: resolver should build lookup/cache once.");

                var runner = new DailyEventRunner();
                Assert.IsTrue(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "daily-choice-request-1", out var result), "DAILYEVENT-EDIT-003 failed: choice should apply through outcome strategies.");
                Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
                Assert.AreEqual(2, progress.GetTraitValue(trait));
                Assert.AreEqual(1, progress.GetRelationshipValue(relationship));
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), "daily-event:daily-event.library-hard-book:daily-event-choice.read-to-end:+trait.curiosity=2");
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), "daily-event:daily-event.library-hard-book:daily-event-choice.read-to-end:+relationship.librarian=1");
                Assert.AreEqual(DailyEventStates.Completed, eventProgress.GetRecord(dailyEvent.Id).State);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dailyEvent);
                UnityEngine.Object.DestroyImmediate(choice);
                UnityEngine.Object.DestroyImmediate(relationshipOutcome);
                UnityEngine.Object.DestroyImmediate(traitOutcome);
                UnityEngine.Object.DestroyImmediate(locationRule);
                UnityEngine.Object.DestroyImmediate(relationship);
                UnityEngine.Object.DestroyImmediate(trait);
                UnityEngine.Object.DestroyImmediate(kind);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        [Test]
        public void DAILYEVENT_EDIT_004_DeferrableEventPersistsAndReappearsOnConfiguredDay()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var kind = ScriptableObject.CreateInstance<DailyEventKindDefinition>();
            var locationRule = ScriptableObject.CreateInstance<DailyEventLocationAvailabilityRule>();
            var choice = ScriptableObject.CreateInstance<DailyEventChoiceDefinition>();
            var dailyEvent = ScriptableObject.CreateInstance<DailyEventDefinition>();
            try
            {
                location.ConfigureForTests("location.square", "location.square.name", Vector2.zero, Array.Empty<DiscoveryDefinition>());
                kind.ConfigureForTests("daily-event-kind.deferrable", "Deferrable", true, true, false, false, false, false, 1, 0);
                locationRule.ConfigureForTests(location);
                choice.ConfigureForTests("daily-event-choice.help-now", "Help now", "Relationship up", 0, 0, 0, 0, false, false, Array.Empty<DailyEventAvailabilityRuleBase>(), Array.Empty<DailyEventOutcomeBase>());
                dailyEvent.ConfigureForTests("daily-event.square-friend", "Friend Request", "A friend waves.", location, null, kind, new DailyEventAvailabilityRuleBase[] { locationRule }, new[] { choice });

                var progress = new StudentLifeProgress("slot", "player", 8, 8);
                var eventProgress = new DailyEventProgress("slot", "player");
                var runner = new DailyEventRunner();
                var resolver = new DailyEventResolver(new[] { dailyEvent });

                Assert.IsTrue(runner.TryDefer(dailyEvent, eventProgress, 1, out var deferred), "DAILYEVENT-EDIT-004 failed: deferrable event should enter deferred state.");
                Assert.AreEqual(DailyEventStates.Deferred, deferred.State);
                Assert.AreEqual(2, deferred.DueDay);

                var restored = DailyEventProgress.FromSaveData(eventProgress.ToSaveData());
                Assert.AreEqual(DailyEventStates.Deferred, restored.GetRecord(dailyEvent.Id).State);

                var dayOneAvailable = resolver.GetAvailableEvents(new DailyEventContext(progress, restored, location, 1, 9 * 60));
                Assert.AreEqual(0, dayOneAvailable.Length, "DAILYEVENT-EDIT-004 failed: deferred event reappeared before due day.");

                var dayTwoAvailable = resolver.GetAvailableEvents(new DailyEventContext(progress, restored, location, 2, 9 * 60));
                Assert.AreEqual(1, dayTwoAvailable.Length, "DAILYEVENT-EDIT-004 failed: deferred event did not reappear on due day.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dailyEvent);
                UnityEngine.Object.DestroyImmediate(choice);
                UnityEngine.Object.DestroyImmediate(locationRule);
                UnityEngine.Object.DestroyImmediate(kind);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        [Test]
        public void DAILYEVENT_EDIT_005_TimedEventExpiresWithoutBlockingProgression()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var kind = ScriptableObject.CreateInstance<DailyEventKindDefinition>();
            var locationRule = ScriptableObject.CreateInstance<DailyEventLocationAvailabilityRule>();
            var choice = ScriptableObject.CreateInstance<DailyEventChoiceDefinition>();
            var dailyEvent = ScriptableObject.CreateInstance<DailyEventDefinition>();
            try
            {
                location.ConfigureForTests("location.workshop", "location.workshop.name", Vector2.zero, Array.Empty<DiscoveryDefinition>());
                kind.ConfigureForTests("daily-event-kind.timed", "Timed", true, false, true, false, false, false, 0, 17 * 60);
                locationRule.ConfigureForTests(location);
                choice.ConfigureForTests("daily-event-choice.accept-work", "Accept", "Earn responsibility", 0, 0, 0, 0, false, false, Array.Empty<DailyEventAvailabilityRuleBase>(), Array.Empty<DailyEventOutcomeBase>());
                dailyEvent.ConfigureForTests("daily-event.workshop-request", "Workshop Request", "A timed request waits.", location, null, kind, new DailyEventAvailabilityRuleBase[] { locationRule }, new[] { choice });

                var progress = new StudentLifeProgress("slot", "player", 8, 8);
                var eventProgress = new DailyEventProgress("slot", "player");
                var resolver = new DailyEventResolver(new[] { dailyEvent });

                Assert.AreEqual(1, resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, location, 1, 16 * 60)).Length);
                Assert.AreEqual(0, resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, location, 1, 18 * 60)).Length);
                Assert.AreEqual(DailyEventStates.Expired, eventProgress.GetRecord(dailyEvent.Id).State, "DAILYEVENT-EDIT-005 failed: timed event should record expired state after due time.");
                Assert.AreEqual(StudentDayState.InProgress, progress.DayState, "DAILYEVENT-EDIT-005 failed: expired event must not block the day loop.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dailyEvent);
                UnityEngine.Object.DestroyImmediate(choice);
                UnityEngine.Object.DestroyImmediate(locationRule);
                UnityEngine.Object.DestroyImmediate(kind);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        [Test]
        public void DAILYEVENT_EDIT_006_CompletionRequestIdDedupesChoiceOutcomesAcrossSaveLoad()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var kind = ScriptableObject.CreateInstance<DailyEventKindDefinition>();
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var traitOutcome = ScriptableObject.CreateInstance<DailyEventTraitDeltaOutcome>();
            var choice = ScriptableObject.CreateInstance<DailyEventChoiceDefinition>();
            var dailyEvent = ScriptableObject.CreateInstance<DailyEventDefinition>();
            try
            {
                location.ConfigureForTests("location.home", "location.home.name", Vector2.zero, Array.Empty<DiscoveryDefinition>());
                kind.ConfigureForTests("daily-event-kind.optional", "Optional", true, false, false, false, false, false, 0, 0);
                trait.ConfigureForTests("trait.planning", "trait.planning.name");
                traitOutcome.ConfigureForTests(trait, 3);
                choice.ConfigureForTests("daily-event-choice.plan", "Plan", "Planning up", 0, 0, 0, 0, false, false, Array.Empty<DailyEventAvailabilityRuleBase>(), new DailyEventOutcomeBase[] { traitOutcome });
                dailyEvent.ConfigureForTests("daily-event.home-plan", "Evening Plan", "Plan tomorrow.", location, null, kind, Array.Empty<DailyEventAvailabilityRuleBase>(), new[] { choice });

                var progress = new StudentLifeProgress("slot", "player", 8, 8);
                var eventProgress = new DailyEventProgress("slot", "player");
                var runner = new DailyEventRunner();

                Assert.IsTrue(runner.TryChoose(dailyEvent, choice, progress, eventProgress, "same-request", out _));
                var restoredEvents = DailyEventProgress.FromSaveData(eventProgress.ToSaveData());
                var restoredStudent = StudentLifeProgress.FromSaveData(progress.ToSaveData(), new[] { trait }, null, null);

                Assert.IsFalse(runner.TryChoose(dailyEvent, choice, restoredStudent, restoredEvents, "same-request", out var duplicate));
                Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
                Assert.AreEqual(3, restoredStudent.GetTraitValue(trait), "DAILYEVENT-EDIT-006 failed: duplicate request applied trait delta again.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dailyEvent);
                UnityEngine.Object.DestroyImmediate(choice);
                UnityEngine.Object.DestroyImmediate(traitOutcome);
                UnityEngine.Object.DestroyImmediate(trait);
                UnityEngine.Object.DestroyImmediate(kind);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }
    }
}
