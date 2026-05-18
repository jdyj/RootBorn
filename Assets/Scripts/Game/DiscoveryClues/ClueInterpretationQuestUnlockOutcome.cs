using System;
using System.Collections.Generic;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationOutcome_QuestUnlock", menuName = "Rootborn/Discovery Clues/Interpretations/Outcomes/Quest Unlock")]
    public sealed class ClueInterpretationQuestUnlockOutcome : ClueInterpretationOutcomeBase
    {
        [SerializeField] private QuestDefinition[] _quests = Array.Empty<QuestDefinition>();
        public override string OutcomeId => ClueInterpretationOutcomeIds.Quest;
        public override bool Apply(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation, List<string> encyclopediaEntryIds, List<string> careerHintIds, List<string> followUpQuestIds, List<string> worldStateIds, List<string> relationshipIds, List<string> statusIds)
        {
            bool applied = false;
            for (int i = 0; i < _quests.Length; i++)
            {
                var quest = _quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                if (context.QuestLog != null) { context.QuestLog.AddQuest(quest); context.QuestLog.Accept(quest); }
                ClueInterpretationOutcomeIds.AddUnique(followUpQuestIds, quest.Id);
                applied = true;
            }
            return applied;
        }
        public void ConfigureForTests(QuestDefinition[] quests) => _quests = quests ?? Array.Empty<QuestDefinition>();
    }
}
