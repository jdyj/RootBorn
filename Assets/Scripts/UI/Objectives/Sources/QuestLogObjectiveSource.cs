using System;
using System.Collections.Generic;
using Rootborn.Game.Quests;
using Rootborn.UI.Quests;
using UnityEngine;

namespace Rootborn.UI.Objectives
{
    public sealed class QuestLogObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Quests", "Quests");

        public string SourceId => "quest-log";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var questPanel = UnityEngine.Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            if (questPanel == null || questPanel.QuestLog == null || questPanel.Quests == null)
            {
                return Array.Empty<ObjectiveJournalItem>();
            }

            var items = new List<ObjectiveJournalItem>();
            for (int i = 0; i < questPanel.Quests.Length; i++)
            {
                var quest = questPanel.Quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id))
                {
                    continue;
                }

                var state = questPanel.QuestLog.GetState(quest);
                items.Add(new ObjectiveJournalItem(
                    "quest." + quest.Id,
                    Category.CategoryId,
                    ObjectiveJournalSourceText.SafeText(quest.DisplayNameKey, quest.Id),
                    ObjectiveJournalSourceText.SafeText(quest.DescriptionKey, string.Empty),
                    BuildQuestStateText(state),
                    BuildObjectiveText(questPanel.QuestLog, quest),
                    BuildQuestHintText(state),
                    false,
                    BuildQuestSortPriority(state)));
            }

            return items.ToArray();
        }

        private static string BuildQuestStateText(QuestState state)
        {
            return state == QuestState.NotStarted ? "Available" : state.ToString();
        }

        private static string BuildQuestHintText(QuestState state)
        {
            if (state == QuestState.NotStarted)
            {
                return "Accept this quest from its provider.";
            }

            if (state == QuestState.RewardClaimed)
            {
                return "Reward claimed. Review this completed quest history.";
            }

            return state == QuestState.Completed ? "Claim the quest reward when inventory can accept it." : "Review the quest detail and complete the next objective.";
        }

        private static int BuildQuestSortPriority(QuestState state)
        {
            if (state == QuestState.Completed)
            {
                return 10;
            }

            if (state == QuestState.Active)
            {
                return 20;
            }

            if (state == QuestState.NotStarted)
            {
                return 40;
            }

            return 60;
        }

        private static string BuildObjectiveText(QuestLog questLog, QuestDefinition quest)
        {
            if (quest.Objectives == null || quest.Objectives.Length == 0)
            {
                return questLog.GetState(quest).ToString();
            }

            string text = string.Empty;
            for (int i = 0; i < quest.Objectives.Length; i++)
            {
                var objective = quest.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                string label = ObjectiveJournalSourceText.SafeText(objective.DisplayKey, objective.GetType().Name);
                string line = label + " " + questLog.GetObjectiveCount(quest, i) + " / " + objective.RequiredCount;
                text += string.IsNullOrEmpty(text) ? line : " | " + line;
            }

            return string.IsNullOrEmpty(text) ? questLog.GetState(quest).ToString() : text;
        }
    }
}
