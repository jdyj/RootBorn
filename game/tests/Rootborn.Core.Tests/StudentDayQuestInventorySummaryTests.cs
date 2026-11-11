using System;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/StudentDayQuestInventorySummaryTests.cs
    public sealed class StudentDayQuestInventorySummaryTests
    {
        private static QuestDefinition Quest(string id, QuestReward reward) => new QuestDefinition(id, id, id, Array.Empty<QuestObjective>(), reward != null ? new[] { reward } : null);

        private static void SetStage(QuestLog log, string questId, QuestStage stage)
        {
            log.LoadFromSaveData(new QuestLogSaveData { Quests = new[] { new QuestProgressSaveData { QuestId = questId, State = stage, StateName = stage.ToString() } } });
        }

        private static void AssertQuestEntry(QuestSummaryEntry entry, string questId, QuestStage from, QuestStage to)
        {
            Assert.AreEqual(questId, entry.QuestId);
            Assert.AreEqual(questId, entry.DisplayName);
            Assert.AreEqual(from, entry.BeforeState);
            Assert.AreEqual(to, entry.AfterState);
        }

        private static void AssertInventoryDelta(InventoryDeltaEntry[] entries, string itemId, int delta, bool fromReward)
        {
            var entry = Array.Find(entries, e => e.ItemId == itemId);
            Assert.AreEqual(itemId, entry.ItemId, "Missing inventory delta for " + itemId);
            Assert.AreEqual(itemId, entry.DisplayName);
            Assert.AreEqual(delta, entry.Delta);
            Assert.AreEqual(fromReward, entry.FromReward);
        }

        [Test]
        public void STUDENT_DAY_SUMMARY_001_QuestInventoryAndRewardSnapshotsBuildGenericDeltaEntries()
        {
            var wood = new ItemDefinition("item.wood", 99, displayKey: "item.wood");
            var rewardItem = new ItemDefinition("item.reward-token", 99, displayKey: "item.reward-token");
            var quest = Quest("quest.gather-wood", new ItemQuestReward(rewardItem, 2));
            var log = new QuestLog(new[] { quest });
            var inventory = new ItemInventory();
            var before = StudentDayQuestInventorySnapshot.Capture(log, new[] { quest }, inventory);
            log.Accept(quest);
            inventory.Add(wood, 3);
            SetStage(log, quest.Id, QuestStage.RewardClaimed);
            inventory.Add(rewardItem, 2);
            var after = StudentDayQuestInventorySnapshot.Capture(log, new[] { quest }, inventory);

            var summary = StudentDayQuestInventorySummaryBuilder.Build(before, after);

            Assert.AreEqual(2, summary.QuestEntries.Length);
            AssertQuestEntry(summary.QuestEntries[0], "quest.gather-wood", QuestStage.NotStarted, QuestStage.Active);
            AssertQuestEntry(summary.QuestEntries[1], "quest.gather-wood", QuestStage.Active, QuestStage.RewardClaimed);
            Assert.AreEqual(2, summary.InventoryDeltas.Length);
            AssertInventoryDelta(summary.InventoryDeltas, "item.wood", 3, false);
            AssertInventoryDelta(summary.InventoryDeltas, "item.reward-token", 2, true);
            Assert.AreEqual(1, summary.RewardEntries.Length);
            Assert.AreEqual("quest.gather-wood", summary.RewardEntries[0].QuestId);
            Assert.AreEqual("item.reward-token", summary.RewardEntries[0].ItemId);
            Assert.AreEqual(2, summary.RewardEntries[0].Count);
        }

        [Test]
        public void STUDENT_DAY_SUMMARY_002_RebuildingSameRewardClaimedSummaryDoesNotCreateDuplicateDelta()
        {
            var rewardItem = new ItemDefinition("item.reward-token", 99);
            var quest = Quest("quest.gather-wood", new ItemQuestReward(rewardItem, 2));
            var log = new QuestLog(new[] { quest });
            var inventory = new ItemInventory();
            log.Accept(quest);
            SetStage(log, quest.Id, QuestStage.RewardClaimed);
            inventory.Add(rewardItem, 2);
            var snapshot = StudentDayQuestInventorySnapshot.Capture(log, new[] { quest }, inventory);

            var summary = StudentDayQuestInventorySummaryBuilder.Build(snapshot, snapshot);

            Assert.AreEqual(0, summary.QuestEntries.Length);
            Assert.AreEqual(0, summary.InventoryDeltas.Length);
            Assert.AreEqual(0, summary.RewardEntries.Length);
            Assert.IsFalse(log.ClaimReward(quest, new RewardRuntimeContext(log, inventory, null, null)));
            Assert.AreEqual(2, inventory.CountOf(rewardItem));
        }

        // 더한 경계

        [Test]
        public void RemovedItemsActiveToCompletedAndNullSnapshots()
        {
            var stone = new ItemDefinition("item.stone", 1);
            var quest = Quest("quest.q", null);
            var log = new QuestLog(new[] { quest });
            log.Accept(quest);
            var inventory = new ItemInventory();
            inventory.Add(stone, 1);
            inventory.Add(stone, 1);
            var before = StudentDayQuestInventorySnapshot.Capture(log, new[] { quest, null }, inventory);
            Assert.AreEqual(2, before.Items[0].Count, "같은 물건은 칸이 달라도 합친다.");
            SetStage(log, quest.Id, QuestStage.Completed);
            inventory.Clear();
            var after = StudentDayQuestInventorySnapshot.Capture(log, new[] { quest }, inventory);

            var summary = StudentDayQuestInventorySummaryBuilder.Build(before, after);
            Assert.AreEqual(1, summary.QuestEntries.Length);
            AssertQuestEntry(summary.QuestEntries[0], "quest.q", QuestStage.Active, QuestStage.Completed);
            AssertInventoryDelta(summary.InventoryDeltas, "item.stone", -2, false);
            Assert.IsEmpty(summary.RewardEntries);

            var empty = StudentDayQuestInventorySummaryBuilder.Build(null, null);
            Assert.IsEmpty(empty.QuestEntries);
            Assert.IsEmpty(empty.InventoryDeltas);
        }
    }
}
