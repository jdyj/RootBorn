using System;
using System.Collections.Generic;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "DiscoveryClueOutcome_QuestUnlock", menuName = "Rootborn/Discovery Clues/Outcomes/Quest Unlock")]
    public sealed class DiscoveryClueQuestUnlockOutcome : DiscoveryClueOutcomeBase
    {
        [SerializeField] private QuestDefinition[] _quests = Array.Empty<QuestDefinition>();
        public override string OutcomeId => DiscoveryClueOutcomeIds.Quest;

        public override bool Apply(in DiscoveryClueContext context, DiscoveryClueDefinition clue, List<string> discoveredEntries, List<string> careerHints, List<string> followUpQuests, List<string> worldStateFlags)
        {
            bool applied = false;
            for (int i = 0; i < _quests.Length; i++)
            {
                var quest = _quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                if (context.QuestLog != null)
                {
                    context.QuestLog.AddQuest(quest);
                    context.QuestLog.Accept(quest);
                }

                DiscoveryClueOutcomeIds.AddUnique(followUpQuests, quest.Id);
                applied = true;
            }

            return applied;
        }

        public void ConfigureForTests(QuestDefinition[] quests)
        {
            _quests = quests ?? Array.Empty<QuestDefinition>();
        }
    }
}
