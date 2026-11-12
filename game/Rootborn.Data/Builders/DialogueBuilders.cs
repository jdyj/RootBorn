using System.Collections.Generic;
using Rootborn.Core.Dialogue;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.Quests;

namespace Rootborn.Data.Builders
{
    /// <summary>대화, 대화 선택지·조건, NPC와 역할, NPC 일정과 일정 규칙, 일정 줄의 머무는 행동.</summary>
    internal static class DialogueBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("DialogueChoiceDefinition", (n, c) => new DialogueChoice(
                n.Str("labelKey"), n.Enum<DialogueQuestAction>("questAction"), c.One<QuestDefinition>(n, "quest")));

            // 퀘스트 폴더의 옛 대화 에셋에는 튜토리얼 단계와 조건 필드가 없다(뒤에 더해진 필드, 초기값 null·빈 배열).
            b.Add("DialogueDefinition", BuilderHelpers.AddedLater(new[] { "requiredTutorialStage", "conditions" }, (n, c) => new DialogueDefinition(
                n.Id, n.Strings("lineKeys"), c.All<DialogueChoice>(n, "choices").ToArray(), n.RefId("requiredTutorialStage"),
                c.All<DialogueCondition>(n, "conditions").ToArray())));

            // data/에는 아직 없다. 필드 이름은 Unity DialogueCondition.cs.
            b.Add("DialogueCondition", (n, c) => new DialogueCondition(
                n.RefId("requiredTutorialStage"), n.RefId("minimumRelationship"), n.Int("minimumRelationshipValue"),
                n.RefId("maximumStatus"), n.Int("maximumStatusValue")));

            b.Add("NpcRoleDefinition", (n, c) => new NpcRole(n.Id, n.Str("displayName"), n.Int("sortOrder")));

            // 그림(worldTexture, worldSpriteRect, characterAppearance)은 Host 몫이다.
            b.Add("NpcDefinition", (n, c) => new NpcDefinition(
                n.Id, n.Str("displayNameKey"), n.Str("introductionKey"), n.RefId("homeLocation"),
                c.All<NpcRole>(n, "roles").ToArray(), c.One<DialogueDefinition>(n, "defaultDialogue"),
                c.All<INpcDialogueCondition>(n, "defaultDialogueConditions").ToArray(),
                c.All<DialogueDefinition>(n, "stageDialogues").ToArray(),
                c.All<QuestDefinition>(n, "quests").ToArray()));

            b.Add("NpcScheduleDefinition", (n, c) =>
            {
                var entries = new List<NpcScheduleEntry>();
                foreach (var entry in n.Items("entries"))
                {
                    entries.Add(new NpcScheduleEntry(
                        entry.RefId("location"), c.One<DialogueDefinition>(entry, "dialogue"), entry.Str("dialogueKey"),
                        entry.Str("interactionHintKey"), entry.Str("eventNoticeKey"), entry.Int("priority"),
                        c.All<INpcScheduleRule>(entry, "rules").ToArray(),
                        // stay가 없는 줄은 서 있기(NpcScheduleEntry가 null을 서 있기로 받는다). 같은 ref는 같은 객체 하나라 머무름이 이어진다.
                        entry.Has("stay") ? c.One<INpcStayBehavior>(entry, "stay") : null));
                }

                return new NpcSchedule(n.Id, c.One<NpcDefinition>(n, "npc"), n.RefId("fallbackLocation"), entries.ToArray());
            });
            b.Add("TimeSlotScheduleRule", (n, c) => new TimeSlotScheduleRule(n.RefIds("timeSlots")));
            b.Add("NpcWanderStay", (n, c) => new NpcWanderStay(n.Float("radiusCells"), n.Float("pauseMinSeconds"), n.Float("pauseMaxSeconds")));
        }
    }
}
