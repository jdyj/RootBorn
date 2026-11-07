using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
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
                    BuildQuestSortPriority(state),
                    state == QuestState.Completed ? CreateClaimAction(session, quest, context.Player) : null));
            }

            return items.ToArray();
        }

        // Any completed quest can be claimed from its journal detail, whoever offered it (e.g. dream-linked quests).
        private static ObjectiveJournalAction CreateClaimAction(PlayerQuestSession session, QuestDefinition quest, GameObject player)
        {
            if (player == null || session.LoadFailed)
            {
                return null;
            }

            return new ObjectiveJournalAction(
                ObjectiveJournalSourceText.Localized("ui.journal.quest.claim", "Claim reward"),
                () => ClaimFromJournal(session, quest, player));
        }

        /// <summary>
        /// The journal's claim: the session checks inventory room for every reward first and pays all or nothing, marks
        /// the quest claimed and saves it, so repeating (also after reload) pays nothing. The owner then saves the
        /// inventory and world it changed, as Mom's claim choice does.
        /// </summary>
        public static ObjectiveJournalActionResult ClaimFromJournal(PlayerQuestSession session, QuestDefinition quest, GameObject player)
        {
            var failed = new ObjectiveJournalActionResult(false, ObjectiveJournalSourceText.Localized(
                "ui.journal.quest.claim-failed", "The reward could not be claimed. Make room in your bag and try again."));
            if (session == null || quest == null || player == null || session.LoadFailed)
            {
                return failed;
            }

            var owner = player.GetComponent<AdultCareerRuntimeComponent>();
            if (owner != null && !owner.CanMutate)
            {
                return failed;
            }

            var inventory = owner != null ? owner.PlayerInventory : player.GetComponent<PlayerInventory>();
            var studentComponent = owner != null ? owner.StudentComponent : player.GetComponent<StudentLifeProgressComponent>();
            if (inventory == null || inventory.Inventory == null)
            {
                return failed;
            }

            var context = session.RewardContext(
                inventory.Inventory,
                studentComponent != null ? studentComponent.Progress : null,
                owner != null ? owner.World : null,
                owner != null ? owner.FurnitureUnlocks : null);
            if (!session.TryClaimReward(quest, in context))
            {
                return failed;
            }

            if (owner != null)
            {
                owner.SaveCurrent();
            }

            return new ObjectiveJournalActionResult(true, ObjectiveJournalSourceText.Localized("ui.journal.quest.claimed", "Reward received."));
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
