using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/NpcScheduleLifePatternTests.cs (EDIT_004~007. 001~003·008은 등록부·에셋 검사, 004의 소스 문자열 검사는 옮기지 않음)
    public sealed class NpcScheduleTests
    {
        private static NpcDefinition Npc(string id, string homeLocationId, DialogueDefinition defaultDialogue = null)
            => new NpcDefinition(id, id + ".name", id + ".intro", homeLocationId, null, defaultDialogue, null, null, null);

        [Test]
        public void NPCSCHEDULE_EDIT_004_005_ResolverUsesRuleArraysWithoutEntityIdBranching()
        {
            var npc = Npc("npc.schedule-test", "location.a");
            var schedule = new NpcSchedule("Schedule_Test", npc, "location.a", new[]
            {
                new NpcScheduleEntry("location.a", null, "dialogue.morning", "hint.morning", "event.morning", 10, new INpcScheduleRule[] { new TimeSlotScheduleRule(new[] { "time.morning" }) }),
                new NpcScheduleEntry("location.b", null, "dialogue.evening", "hint.evening", "event.evening", 20, new INpcScheduleRule[] { new TimeSlotScheduleRule(new[] { "time.evening" }) }),
            });
            var resolver = new NpcScheduleResolver();

            var resolvedMorning = resolver.Resolve(schedule, new NpcScheduleContext(1, 0, "time.morning", null));
            var resolvedEvening = resolver.Resolve(schedule, new NpcScheduleContext(1, 0, "time.evening", null));

            Assert.AreEqual("location.a", resolvedMorning.LocationId);
            Assert.AreEqual("location.b", resolvedEvening.LocationId);
            Assert.AreEqual("dialogue.evening", resolvedEvening.DialogueKey);
        }

        [Test]
        public void NPCSCHEDULE_EDIT_006_007_MeetingsPersistPerSaveSlotPlayerAndDedupeScheduleNotices()
        {
            var npc = Npc("npc.librarian", "location.library");
            var progress = new StudentLifeProgress("slot-a", "player-a", 8, 8);
            var scheduleProgress = new NpcScheduleProgress(progress);

            Assert.IsTrue(scheduleProgress.TryRecordMeeting(npc, "location.library", "time.morning", "event.library-books", out var first));
            Assert.IsFalse(scheduleProgress.TryRecordMeeting(npc, "location.library", "time.morning", "event.library-books", out var duplicate));

            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            var restoredProgress = new NpcScheduleProgress(restored);

            Assert.AreEqual(NpcScheduleProgressResultKind.FirstMeeting, first.Kind);
            Assert.AreEqual(NpcScheduleProgressResultKind.DuplicateMeeting, duplicate.Kind);
            Assert.IsTrue(restoredProgress.HasMet(npc, "location.library", "time.morning"));
            Assert.AreEqual("location.library", restoredProgress.GetLastMeetingLocationId(npc));
            Assert.AreEqual(1, restoredProgress.MeetingRecordIds.Length);
            CollectionAssert.Contains(restored.GetActivityLogIds(), "npc.librarian@location.library@time.morning");
            CollectionAssert.Contains(restored.GetTodayResultLogIds(), "event.library-books");
        }

        [Test]
        public void NPCSCHEDULE_CORE_HighestPriorityWinsAndFallbacksApply()
        {
            var defaultDialogue = new DialogueDefinition("dialogue.default", null, null, null, null);
            var entryDialogue = new DialogueDefinition("dialogue.entry", null, null, null, null);
            var npc = Npc("npc.a", "location.home", defaultDialogue);
            var always = new NpcScheduleEntry(null, entryDialogue, "key.always", null, null, 5, null);
            var tie = new NpcScheduleEntry("location.tie", null, "key.tie", null, null, 5, new INpcScheduleRule[] { null });
            var schedule = new NpcSchedule("Schedule_A", npc, null, new[] { null, always, tie });
            var resolver = new NpcScheduleResolver();

            var result = resolver.Resolve(schedule, new NpcScheduleContext(0, 9, null, null));
            Assert.AreSame(always, result.Entry, "On equal priority the earlier entry stays.");
            Assert.AreEqual("location.home", result.LocationId, "An entry without a location uses the schedule fallback, then the NPC home.");
            Assert.AreSame(entryDialogue, result.Dialogue);
            Assert.AreSame(npc, result.Npc);

            var empty = new NpcSchedule("Schedule_B", npc, "location.fallback", null);
            var none = resolver.Resolve(empty, new NpcScheduleContext(1, 0, "time.morning", null));
            Assert.IsNull(none.Entry);
            Assert.AreEqual("location.fallback", none.LocationId);
            Assert.AreSame(defaultDialogue, none.Dialogue, "Without an entry the NPC default dialogue is used.");
            Assert.AreEqual(string.Empty, none.DialogueKey);
            Assert.IsNull(resolver.Resolve(null, default).Npc);

            var context = new NpcScheduleContext(0, 9, null, null);
            Assert.AreEqual(1, context.Day);
            Assert.AreEqual(6, context.WeekdayIndex);
            Assert.AreEqual(string.Empty, context.TimeSlotId);
            Assert.IsTrue(new TimeSlotScheduleRule(null).IsSatisfied(context), "No time slots means any time.");
            Assert.IsFalse(new TimeSlotScheduleRule(new[] { "time.night", null }).IsSatisfied(context));
        }

        [Test]
        public void NPCSCHEDULE_CORE_MeetingRejectsMissingPartsAndReadsLastLocation()
        {
            var npc = Npc("npc.a", null);
            var progress = new StudentLifeProgress("slot-a", "player-a", 8, 8);
            var meetings = new NpcScheduleProgress(progress);

            Assert.IsFalse(meetings.TryRecordMeeting(npc, null, "time.morning", null, out var noPlace));
            Assert.AreEqual(NpcScheduleProgressResultKind.InvalidRequest, noPlace.Kind);
            Assert.IsFalse(meetings.TryRecordMeeting(null, "location.a", "time.morning", null, out _));
            Assert.IsFalse(meetings.TryRecordMeeting(npc, "location.a", string.Empty, null, out _));
            Assert.IsFalse(new NpcScheduleProgress(null).TryRecordMeeting(npc, "location.a", "time.morning", null, out var noProgress));
            Assert.AreEqual("npc.a@location.a@time.morning", noProgress.RecordId);
            Assert.AreEqual(0, progress.GetActivityLogIds().Length);

            Assert.IsTrue(meetings.TryRecordMeeting(npc, "location.a", "time.morning", null, out _));
            Assert.IsTrue(meetings.TryRecordMeeting(npc, "location.b", "time.evening", string.Empty, out _));
            progress.RecordActivityCompleted("activity.not-a-meeting");
            Assert.AreEqual("location.b", meetings.GetLastMeetingLocationId(npc));
            Assert.AreEqual(2, meetings.MeetingRecordIds.Length);
            Assert.AreEqual(string.Empty, meetings.GetLastMeetingLocationId(null));
            Assert.AreEqual(0, progress.GetTodayResultLogIds().Length, "No notice, no result log.");
        }

        [Test]
        public void NPCSCHEDULE_CORE_LocationStateAccessResolvesByScheduleIdWithWeekdayZero()
        {
            var npc = Npc("npc.a", "location.home");
            var schedule = new NpcSchedule("Schedule_A", npc, null, new[]
            {
                new NpcScheduleEntry("location.park", null, null, null, null, 1, new INpcScheduleRule[] { new TimeSlotScheduleRule(new[] { LocationStateTimeSlots.Evening }) }),
            });
            var later = new NpcSchedule("Schedule_Later", npc, "location.later", null);
            var schedules = new NpcScheduleLookupCache(new[] { schedule, null, later, new NpcSchedule(string.Empty, null, null, null) });
            ILocationStateNpcSchedules access = new NpcScheduleAccess(schedules);

            Assert.AreEqual("location.park", access.ResolveLocationId("Schedule_A", 3, LocationStateTimeSlots.Evening, null));
            Assert.AreEqual("location.home", access.ResolveLocationId("Schedule_A", 3, LocationStateTimeSlots.Morning, null));
            Assert.AreEqual(string.Empty, access.ResolveLocationId("Schedule_Missing", 3, LocationStateTimeSlots.Evening, null));
            Assert.AreEqual(string.Empty, access.ResolveLocationId(null, 3, LocationStateTimeSlots.Evening, null));
            Assert.IsTrue(schedules.TryGetByNpcId("npc.a", out var byNpc));
            Assert.AreSame(later, byNpc, "Like Unity, the later schedule for the same NPC wins the NPC lookup.");
            Assert.IsTrue(schedules.TryGetById("Schedule_A", out var byId));
            Assert.AreSame(schedule, byId);
            Assert.IsFalse(schedules.TryGetByNpcId(null, out _));
            Assert.AreEqual(1, schedules.BuildCount);
        }
    }
}
