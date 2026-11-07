using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;

namespace Rootborn.UI.Objectives
{
    public sealed class QuestLogObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Quests", "Quests");

        public string SourceId => "quest-log";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var session = ResolveSession(context);
            if (session == null)
            {
                return Array.Empty<ObjectiveJournalItem>();
            }

            var quests = session.Quests;
            var items = new List<ObjectiveJournalItem>(quests.Count);
            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id))
                {
                    continue;
                }

                var state = session.QuestLog.GetState(quest);
                items.Add(new ObjectiveJournalItem(
                    "quest." + quest.Id,
                    Category.CategoryId,
                    ObjectiveJournalSourceText.SafeText(quest.DisplayNameKey, quest.Id),
                    ObjectiveJournalSourceText.SafeText(quest.DescriptionKey, string.Empty),
                    BuildQuestStateText(state),
                    BuildObjectiveText(session.QuestLog, quest),
                    BuildQuestHintText(state),
                    false,
                    BuildQuestSortPriority(state)));
            }

            return items.ToArray();
        }

        // The same per-player session NPC dialogue changes, in whichever world scene the player is.
        private static PlayerQuestSession ResolveSession(ObjectiveJournalContext context)
        {
            var registry = context.Registry;
            if (registry == null && !GameDataRegistryProvider.TryGet(out registry))
            {
                return null;
            }

            if (registry == null)
            {
                return null;
            }

            string slot = context.ActiveSave != null ? context.ActiveSave.SlotId : ActiveSaveContext.SlotId;
            return PlayerQuestRuntime.Get(slot, PlayerQuestRuntime.PlayerIdOf(context.Player), registry.Quests);
        }

        private static string BuildQuestStateText(QuestState state)
        {
            return ObjectiveJournalSourceText.State(state == QuestState.NotStarted ? "Available" : state.ToString());
        }

        private static string BuildQuestHintText(QuestState state)
        {
            if (state == QuestState.NotStarted)
            {
                return ObjectiveJournalSourceText.Localized("ui.journal.quest.accept-hint", "Accept this quest from its provider.");
            }

            if (state == QuestState.RewardClaimed)
            {
                return ObjectiveJournalSourceText.Localized("ui.journal.quest.claimed-hint", "Reward claimed. Review this completed quest history.");
            }

            return state == QuestState.Completed ? ObjectiveJournalSourceText.Localized("ui.journal.quest.claim-hint", "Claim the quest reward when inventory can accept it.") : ObjectiveJournalSourceText.Localized("ui.journal.quest.next-hint", "Review the quest detail and complete the next objective.");
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
                return ObjectiveJournalSourceText.State(questLog.GetState(quest).ToString());
            }

            string text = string.Empty;
            for (int i = 0; i < quest.Objectives.Length; i++)
            {
                var objective = quest.Objectives[i];
                if (objective == null)
                {
                    continue;
                }

                string label = ObjectiveJournalSourceText.ObjectiveLabel(objective.DisplayKey);
                string line = label + " " + questLog.GetObjectiveCount(quest, i) + " / " + objective.RequiredCount;
                text += string.IsNullOrEmpty(text) ? line : " | " + line;
            }

            return string.IsNullOrEmpty(text) ? ObjectiveJournalSourceText.State(questLog.GetState(quest).ToString()) : text;
        }
    }
}
