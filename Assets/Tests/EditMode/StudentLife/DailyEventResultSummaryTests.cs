using System;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyEventResultSummaryTests
    {
        [Test]
        public void DAILYEVENT_EDIT_008_DeclineDoesNotBlockProgressAndResultSummaryCapturesChoiceLogs()
        {
            var kind = ScriptableObject.CreateInstance<DailyEventKindDefinition>();
            var choice = ScriptableObject.CreateInstance<DailyEventChoiceDefinition>();
            var dailyEvent = ScriptableObject.CreateInstance<DailyEventDefinition>();
            try
            {
                kind.ConfigureForTests("daily-event-kind.optional", "Optional", true, false, false, false, false, false, 0, 0);
                choice.ConfigureForTests("daily-event-choice.no", "No", "Declined", 0, 0, 0, 0, false, true, Array.Empty<DailyEventAvailabilityRuleBase>(), Array.Empty<DailyEventOutcomeBase>());
                dailyEvent.ConfigureForTests("daily-event.decline-test", "Decline Test", "Decline event", null, null, kind, Array.Empty<DailyEventAvailabilityRuleBase>(), new[] { choice });

                var progress = new StudentLifeProgress("slot", "player", 8, 8);
                var eventProgress = new DailyEventProgress("slot", "player");
                var runner = new DailyEventRunner();

                Assert.IsTrue(runner.TryDecline(dailyEvent, eventProgress, progress.CurrentDay, out var declined));
                Assert.AreEqual(DailyEventStates.Declined, declined.State);
                Assert.AreEqual(StudentDayState.InProgress, progress.DayState, "DAILYEVENT-EDIT-008 failed: declined event blocked normal day progression.");

                declined.MarkCompleted(choice.Id, "request", 1, new[] { "daily-event:daily-event.decline-test:daily-event-choice.no:+trait.test=1" });
                var summary = DailyEventResultSummary.FromRecord(declined);

                Assert.AreEqual("daily-event.decline-test", summary.EventId);
                Assert.AreEqual("daily-event-choice.no", summary.SelectedChoiceId);
                Assert.AreEqual(DailyEventStates.Completed, summary.State);
                CollectionAssert.Contains(summary.ResultSummaryLogIds, "daily-event:daily-event.decline-test:daily-event-choice.no:+trait.test=1");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dailyEvent);
                UnityEngine.Object.DestroyImmediate(choice);
                UnityEngine.Object.DestroyImmediate(kind);
            }
        }
    }
}
