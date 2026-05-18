using System;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class LocationStateConditionStrategyTests
    {
        [Test]
        public void LOCATION_STATE_EDIT_004_StatusQuestAndNpcScheduleConditionsAreDataStrategies()
        {
            var location = MakeLocation("location.library");
            var timeSlot = MakeTimeSlot("time.afternoon", 2);
            var status = ScriptableObject.CreateInstance<StatusDefinition>();
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            var schedule = ScriptableObject.CreateInstance<NpcScheduleDefinition>();
            var scheduleRule = ScriptableObject.CreateInstance<TimeSlotScheduleRule>();
            var statusCondition = ScriptableObject.CreateInstance<LocationStateStatusCondition>();
            var questCondition = ScriptableObject.CreateInstance<LocationStateQuestStateCondition>();
            var scheduleCondition = ScriptableObject.CreateInstance<LocationStateNpcScheduleCondition>();
            try
            {
                status.ConfigureForTests("status.focused", "Focused", true);
                quest.ConfigureForRuntime("quest.archive", "Archive Quest", "Archive quest", Array.Empty<QuestObjectiveBase>(), Array.Empty<QuestRewardBase>(), Array.Empty<QuestCompletionEffectBase>());
                npc.ConfigureForTests("npc.librarian", "Librarian", "Librarian", location, Array.Empty<NpcRoleDefinition>(), null, Array.Empty<NpcDialogueConditionBase>());
                scheduleRule.ConfigureForTests(new[] { timeSlot });
                schedule.ConfigureForTests(npc, location, new[] { new NpcScheduleEntry(location, "dialogue.library", "hint", "notice", 10, new NpcScheduleRuleBase[] { scheduleRule }) });
                statusCondition.ConfigureForTests(status, 2);
                questCondition.ConfigureForTests(quest, QuestState.Completed);
                scheduleCondition.ConfigureForTests(schedule, location);

                var progress = new StudentLifeProgress("slot", "player", 10, 10, 0, 13 * 60);
                progress.AddStatusForTests(status, 2);
                var questLog = new QuestLog(new[] { quest });
                questLog.LoadFromSaveData(new QuestLogSaveData { Quests = new[] { new QuestProgressSaveData { QuestId = quest.Id, State = QuestState.Completed, StateName = QuestState.Completed.ToString() } } });
                var cache = new GameDataLookupCache(null);
                var resolver = new NpcScheduleResolver(cache);
                var context = new LocationStateContext(location, timeSlot, progress, null, questLog, resolver, null);

                Assert.IsTrue(statusCondition.IsSatisfied(in context), "LOCATION-STATE-EDIT-004 failed: status condition strategy did not evaluate from progress.");
                Assert.IsTrue(questCondition.IsSatisfied(in context), "LOCATION-STATE-EDIT-004 failed: quest condition strategy did not evaluate from QuestLog.");
                Assert.IsTrue(scheduleCondition.IsSatisfied(in context), "LOCATION-STATE-EDIT-004 failed: NPC schedule condition strategy did not evaluate from schedule data.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scheduleCondition);
                UnityEngine.Object.DestroyImmediate(questCondition);
                UnityEngine.Object.DestroyImmediate(statusCondition);
                UnityEngine.Object.DestroyImmediate(scheduleRule);
                UnityEngine.Object.DestroyImmediate(schedule);
                UnityEngine.Object.DestroyImmediate(npc);
                UnityEngine.Object.DestroyImmediate(quest);
                UnityEngine.Object.DestroyImmediate(status);
                UnityEngine.Object.DestroyImmediate(timeSlot);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        private static LocationDefinition MakeLocation(string id)
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests(id, id + ".name", Vector2.zero, Array.Empty<DiscoveryDefinition>());
            return location;
        }

        private static TimeSlotDefinition MakeTimeSlot(string id, int order)
        {
            var timeSlot = ScriptableObject.CreateInstance<TimeSlotDefinition>();
            timeSlot.ConfigureForTests(id, id + ".name", order);
            return timeSlot;
        }
    }
}
