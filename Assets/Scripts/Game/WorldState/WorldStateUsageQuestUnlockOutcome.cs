using System;
using System.Collections.Generic;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [CreateAssetMenu(fileName = "WorldStateUsageOutcome_QuestUnlock", menuName = "Rootborn/World State/Usage/Outcomes/Quest Unlock")]
    public sealed class WorldStateUsageQuestUnlockOutcome : WorldStateUsageOutcomeBase
    {
        [SerializeField] private QuestDefinition[] _quests = Array.Empty<QuestDefinition>();
        public override string OutcomeId => WorldStateUsageOutcomeIds.Quest;
        public IReadOnlyList<QuestDefinition> Quests => _quests;

        public override bool Apply(in WorldStateUsageContext context, WorldStateUsageDefinition usage, List<string> unlockedActivities, List<string> unlockedQuests, List<string> discoveredEntries, List<string> careerHints)
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

                WorldStateUsageOutcomeIds.AddUnique(unlockedQuests, quest.Id);
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
