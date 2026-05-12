using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentDayQuestInventorySummaryTests
    {
        [Test]
        public void STUDENT_DAY_SUMMARY_001_QuestInventoryAndRewardSnapshotsBuildGenericDeltaEntries()
        {
            var wood = MakeItem("item.wood", "item.wood", 99);
            var rewardItem = MakeItem("item.reward-token", "item.reward-token", 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", rewardItem);
            SetField(reward, "_count", 2);
            var quest = MakeQuest("quest.gather-wood", "quest.gather-wood", reward);
            var log = new QuestLog(new[] { quest });
            var inventory = new Inventory();

            var before = StudentDayQuestInventorySnapshot.Capture(log, new[] { quest }, inventory);
            log.Accept(quest);
            inventory.Add(wood, 3);
            SetQuestRewardClaimed(log, quest);
            inventory.Add(rewardItem, 2);
            var after = StudentDayQuestInventorySnapshot.Capture(log, new[] { quest }, inventory);

            var summary = StudentDayQuestInventorySummaryBuilder.Build(before, after);

            Assert.AreEqual(2, summary.QuestEntries.Length);
            AssertQuestEntry(summary.QuestEntries[0], "quest.gather-wood", "quest.gather-wood", QuestState.NotStarted, QuestState.Active);
            AssertQuestEntry(summary.QuestEntries[1], "quest.gather-wood", "quest.gather-wood", QuestState.Active, QuestState.RewardClaimed);
            Assert.AreEqual(2, summary.InventoryDeltas.Length);
            AssertInventoryDelta(summary.InventoryDeltas, "item.wood", "item.wood", 3, false);
            AssertInventoryDelta(summary.InventoryDeltas, "item.reward-token", "item.reward-token", 2, true);
            Assert.AreEqual(1, summary.RewardEntries.Length);
            Assert.AreEqual("quest.gather-wood", summary.RewardEntries[0].QuestId);
            Assert.AreEqual("item.reward-token", summary.RewardEntries[0].ItemId);
            Assert.AreEqual(2, summary.RewardEntries[0].Count);
        }

        [Test]
        public void STUDENT_DAY_SUMMARY_002_RebuildingSameRewardClaimedSummaryDoesNotCreateDuplicateDelta()
        {
            var rewardItem = MakeItem("item.reward-token", "item.reward-token", 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", rewardItem);
            SetField(reward, "_count", 2);
            var quest = MakeQuest("quest.gather-wood", "quest.gather-wood", reward);
            var log = new QuestLog(new[] { quest });
            var inventory = new Inventory();
            log.Accept(quest);
            SetQuestRewardClaimed(log, quest);
            inventory.Add(rewardItem, 2);

            var snapshot = StudentDayQuestInventorySnapshot.Capture(log, new[] { quest }, inventory);
            var summary = StudentDayQuestInventorySummaryBuilder.Build(snapshot, snapshot);

            Assert.AreEqual(0, summary.QuestEntries.Length);
            Assert.AreEqual(0, summary.InventoryDeltas.Length);
            Assert.AreEqual(0, summary.RewardEntries.Length);
            Assert.IsFalse(log.ClaimReward(quest, new RewardRuntimeContext(log, inventory, null, null)));
            Assert.AreEqual(2, inventory.CountOf(rewardItem));
        }

        private static QuestDefinition MakeQuest(string id, string displayName, ItemQuestReward reward)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_id", id);
            SetField(quest, "_displayNameKey", displayName);
            SetField(quest, "_rewards", new QuestRewardBase[] { reward });
            return quest;
        }

        private static ItemDefinition MakeItem(string id, string displayKey, int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_id", id);
            SetField(item, "_displayKey", displayKey);
            SetField(item, "_maxStack", maxStack);
            return item;
        }

        private static void SetQuestRewardClaimed(QuestLog log, QuestDefinition quest)
        {
            var save = log.ToSaveData();
            save.Quests[0].State = QuestState.RewardClaimed;
            save.Quests[0].StateName = QuestState.RewardClaimed.ToString();
            log.LoadFromSaveData(save);
            Assert.AreEqual(QuestState.RewardClaimed, log.GetState(quest));
        }

        private static void AssertQuestEntry(QuestSummaryEntry entry, string questId, string displayName, QuestState from, QuestState to)
        {
            Assert.AreEqual(questId, entry.QuestId);
            Assert.AreEqual(displayName, entry.DisplayName);
            Assert.AreEqual(from, entry.BeforeState);
            Assert.AreEqual(to, entry.AfterState);
        }

        private static void AssertInventoryDelta(InventoryDeltaEntry[] entries, string itemId, string displayName, int delta, bool fromReward)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].ItemId == itemId)
                {
                    Assert.AreEqual(displayName, entries[i].DisplayName);
                    Assert.AreEqual(delta, entries[i].Delta);
                    Assert.AreEqual(fromReward, entries[i].FromReward);
                    return;
                }
            }

            Assert.Fail("Missing inventory delta for " + itemId);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }
    }
}
