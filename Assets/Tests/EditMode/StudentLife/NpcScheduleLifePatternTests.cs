using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class NpcScheduleLifePatternTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void NPCSCHEDULE_EDIT_001_003_RegistryLoadsSchedulesAndTimeBasedLocations()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.GreaterOrEqual(registry.TimeSlots.Length, 3, "NPCSCHEDULE-EDIT-001 failed: time slots must be registered.");
            Assert.GreaterOrEqual(registry.NpcSchedules.Length, 3, "NPCSCHEDULE-EDIT-001 failed: at least three NPC schedules must be registered.");

            int multiEntryCount = 0;
            int locationChangingCount = 0;
            var resolver = new NpcScheduleResolver(new GameDataLookupCache(registry));

            for (int i = 0; i < registry.NpcSchedules.Length; i++)
            {
                var schedule = registry.NpcSchedules[i];
                Assert.IsNotNull(schedule, "NPCSCHEDULE-EDIT-001 failed: registry has a null NPC schedule.");
                Assert.IsNotNull(schedule.Npc, schedule.name + " must reference an NPC.");
                Assert.IsNotNull(schedule.FallbackLocation, schedule.name + " must define a fallback location.");
                Assert.Greater(schedule.Entries.Count, 0, schedule.Npc.Id + " must expose schedule entries.");
                StringAssert.StartsWith("Assets/Data/NPCs/Schedules/", AssetDatabase.GetAssetPath(schedule));

                if (schedule.Entries.Count >= 2) multiEntryCount++;
                var morning = resolver.Resolve(schedule, new NpcScheduleContext(1, 0, "time.morning", null));
                var evening = resolver.Resolve(schedule, new NpcScheduleContext(1, 0, "time.evening", null));
                Assert.IsNotNull(morning.Location, schedule.Npc.Id + " morning location must resolve.");
                Assert.IsNotNull(evening.Location, schedule.Npc.Id + " evening location must resolve.");
                if (morning.Location != evening.Location || morning.Dialogue != evening.Dialogue || morning.InteractionHintKey != evening.InteractionHintKey) locationChangingCount++;
            }

            Assert.GreaterOrEqual(multiEntryCount, 3, "NPCSCHEDULE-EDIT-002 failed: at least three NPCs must have two or more schedule entries.");
            Assert.GreaterOrEqual(locationChangingCount, 2, "NPCSCHEDULE-EDIT-003 failed: at least two NPCs must change location or interaction by time slot.");
        }

        [Test]
        public void NPCSCHEDULE_EDIT_004_005_ResolverUsesRuleArraysWithoutEntityIdBranching()
        {
            var locationA = CreateLocation("location.a", Vector2.zero);
            var locationB = CreateLocation("location.b", Vector2.one);
            var morning = CreateTimeSlot("time.morning", 0);
            var evening = CreateTimeSlot("time.evening", 2);
            var npc = CreateNpc("npc.schedule-test", locationA);
            var morningRule = ScriptableObject.CreateInstance<TimeSlotScheduleRule>();
            var eveningRule = ScriptableObject.CreateInstance<TimeSlotScheduleRule>();
            var schedule = ScriptableObject.CreateInstance<NpcScheduleDefinition>();
            try
            {
                morningRule.ConfigureForTests(new[] { morning });
                eveningRule.ConfigureForTests(new[] { evening });
                schedule.ConfigureForTests(
                    npc,
                    locationA,
                    new[]
                    {
                        new NpcScheduleEntry(locationA, "dialogue.morning", "hint.morning", "event.morning", 10, new NpcScheduleRuleBase[] { morningRule }),
                        new NpcScheduleEntry(locationB, "dialogue.evening", "hint.evening", "event.evening", 20, new NpcScheduleRuleBase[] { eveningRule })
                    });
                var resolver = new NpcScheduleResolver(null);

                var resolvedMorning = resolver.Resolve(schedule, new NpcScheduleContext(1, 0, "time.morning", null));
                var resolvedEvening = resolver.Resolve(schedule, new NpcScheduleContext(1, 0, "time.evening", null));

                Assert.AreSame(locationA, resolvedMorning.Location, "NPCSCHEDULE-EDIT-005 failed: morning entry did not resolve from rules.");
                Assert.AreSame(locationB, resolvedEvening.Location, "NPCSCHEDULE-EDIT-005 failed: evening entry did not resolve from rules.");
                Assert.AreEqual("dialogue.evening", resolvedEvening.DialogueKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(schedule);
                UnityEngine.Object.DestroyImmediate(eveningRule);
                UnityEngine.Object.DestroyImmediate(morningRule);
                UnityEngine.Object.DestroyImmediate(npc);
                UnityEngine.Object.DestroyImmediate(evening);
                UnityEngine.Object.DestroyImmediate(morning);
                UnityEngine.Object.DestroyImmediate(locationB);
                UnityEngine.Object.DestroyImmediate(locationA);
            }

            string[] files =
            {
                "Assets/Scripts/Game/Dialogue/NpcScheduleDefinition.cs",
                "Assets/Scripts/Game/Dialogue/NpcScheduleResolver.cs",
                "Assets/Scripts/UI/StudentLife/LocationNpcRuntimeInstaller.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("npcId ==", source, files[i] + " must not branch by npc id.");
                StringAssert.DoesNotContain("locationId ==", source, files[i] + " must not branch by location id.");
                StringAssert.DoesNotContain("timeSlotId ==", source, files[i] + " must not branch by time slot id.");
                StringAssert.DoesNotContain("switch (npcId", source, files[i] + " must not switch by npc id.");
                StringAssert.DoesNotContain("switch (locationId", source, files[i] + " must not switch by location id.");
                StringAssert.DoesNotContain("switch (timeSlotId", source, files[i] + " must not switch by time slot id.");
            }
        }

        [Test]
        public void NPCSCHEDULE_EDIT_006_007_MeetingsPersistPerSaveSlotPlayerAndDedupeScheduleNotices()
        {
            var location = CreateLocation("location.library", Vector2.zero);
            var npc = CreateNpc("npc.librarian", location);
            try
            {
                var progress = new StudentLifeProgress("slot-a", "player-a", 8, 8);
                var scheduleProgress = new NpcScheduleProgress(progress);

                Assert.IsTrue(scheduleProgress.TryRecordMeeting(npc, location, "time.morning", "event.library-books", out var first));
                Assert.IsFalse(scheduleProgress.TryRecordMeeting(npc, location, "time.morning", "event.library-books", out var duplicate));

                var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData(), null, null, null);
                var restoredProgress = new NpcScheduleProgress(restored);

                Assert.AreEqual(NpcScheduleProgressResultKind.FirstMeeting, first.Kind);
                Assert.AreEqual(NpcScheduleProgressResultKind.DuplicateMeeting, duplicate.Kind);
                Assert.IsTrue(restoredProgress.HasMet(npc, location, "time.morning"), "NPCSCHEDULE-EDIT-006 failed: meeting record did not survive save/load.");
                Assert.AreEqual("location.library", restoredProgress.GetLastMeetingLocationId(npc), "NPCSCHEDULE-EDIT-006 failed: last meeting location was not restored.");
                Assert.AreEqual(1, restoredProgress.MeetingRecordIds.Length, "NPCSCHEDULE-EDIT-007 failed: duplicate meeting created extra state.");
                CollectionAssert.Contains(restored.GetActivityLogIds(), "npc.librarian@location.library@time.morning", "NPCSCHEDULE-EDIT-006 failed: meeting must be visible through persisted progress logs.");
                CollectionAssert.Contains(restored.GetTodayResultLogIds(), "event.library-books", "NPCSCHEDULE-EDIT-007 failed: first notice must be tracked once.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(npc);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        [Test]
        public void NPCSCHEDULE_EDIT_008_LookupCacheResolvesTimeSlotsAndSchedulesWithoutRebuilds()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var cache = new GameDataLookupCache(registry);

            Assert.IsTrue(cache.TryGetTimeSlot("time.morning", out var morning), "NPCSCHEDULE-EDIT-008 failed: morning time slot missing from cache.");
            Assert.IsTrue(cache.TryGetNpcSchedule("npc.librarian", out var schedule), "NPCSCHEDULE-EDIT-008 failed: librarian schedule missing from cache.");
            Assert.AreSame(morning, cache.GetTimeSlotOrNull("time.morning"));
            Assert.AreSame(schedule, cache.GetNpcScheduleOrNull("npc.librarian"));
            Assert.AreEqual(1, cache.BuildCount, "NPCSCHEDULE-EDIT-008 failed: cache rebuilt during schedule lookup.");
        }

        private static LocationDefinition CreateLocation(string id, Vector2 position)
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests(id, id + ".name", id + ".desc", null, id, position, Array.Empty<NpcRoleDefinition>(), Array.Empty<NpcDefinition>(), Array.Empty<DiscoveryDefinition>(), Array.Empty<LocationVisitRuleBase>());
            return location;
        }

        private static TimeSlotDefinition CreateTimeSlot(string id, int order)
        {
            var slot = ScriptableObject.CreateInstance<TimeSlotDefinition>();
            slot.ConfigureForTests(id, id + ".name", order);
            return slot;
        }

        private static NpcDefinition CreateNpc(string id, LocationDefinition location)
        {
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            npc.ConfigureForTests(id, id + ".name", id + ".intro", location, Array.Empty<NpcRoleDefinition>(), null, Array.Empty<NpcDialogueConditionBase>());
            return npc;
        }
    }
}
