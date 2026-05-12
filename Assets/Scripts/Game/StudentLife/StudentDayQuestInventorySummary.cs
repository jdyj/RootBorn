using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;

namespace Rootborn.Game.StudentLife
{
    public readonly struct QuestSummaryEntry
    {
        public readonly string QuestId;
        public readonly string DisplayName;
        public readonly QuestState BeforeState;
        public readonly QuestState AfterState;

        public QuestSummaryEntry(string questId, string displayName, QuestState beforeState, QuestState afterState)
        {
            QuestId = string.IsNullOrEmpty(questId) ? string.Empty : questId;
            DisplayName = string.IsNullOrEmpty(displayName) ? QuestId : displayName;
            BeforeState = beforeState;
            AfterState = afterState;
        }
    }

    public readonly struct InventoryDeltaEntry
    {
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly int BeforeCount;
        public readonly int AfterCount;
        public readonly int Delta;
        public readonly bool FromReward;

        public InventoryDeltaEntry(string itemId, string displayName, int beforeCount, int afterCount, bool fromReward)
        {
            ItemId = string.IsNullOrEmpty(itemId) ? string.Empty : itemId;
            DisplayName = string.IsNullOrEmpty(displayName) ? ItemId : displayName;
            BeforeCount = Math.Max(0, beforeCount);
            AfterCount = Math.Max(0, afterCount);
            Delta = AfterCount - BeforeCount;
            FromReward = fromReward;
        }
    }

    public readonly struct RewardSummaryEntry
    {
        public readonly string QuestId;
        public readonly string QuestDisplayName;
        public readonly string ItemId;
        public readonly string ItemDisplayName;
        public readonly int Count;

        public RewardSummaryEntry(string questId, string questDisplayName, string itemId, string itemDisplayName, int count)
        {
            QuestId = string.IsNullOrEmpty(questId) ? string.Empty : questId;
            QuestDisplayName = string.IsNullOrEmpty(questDisplayName) ? QuestId : questDisplayName;
            ItemId = string.IsNullOrEmpty(itemId) ? string.Empty : itemId;
            ItemDisplayName = string.IsNullOrEmpty(itemDisplayName) ? ItemId : itemDisplayName;
            Count = Math.Max(0, count);
        }
    }

    public readonly struct StudentDayQuestInventorySummary
    {
        public readonly QuestSummaryEntry[] QuestEntries;
        public readonly InventoryDeltaEntry[] InventoryDeltas;
        public readonly RewardSummaryEntry[] RewardEntries;

        public StudentDayQuestInventorySummary(QuestSummaryEntry[] questEntries, InventoryDeltaEntry[] inventoryDeltas, RewardSummaryEntry[] rewardEntries)
        {
            QuestEntries = questEntries ?? Array.Empty<QuestSummaryEntry>();
            InventoryDeltas = inventoryDeltas ?? Array.Empty<InventoryDeltaEntry>();
            RewardEntries = rewardEntries ?? Array.Empty<RewardSummaryEntry>();
        }
    }

    public sealed class StudentDayQuestInventorySnapshot
    {
        private readonly QuestSnapshotEntry[] _quests;
        private readonly InventorySnapshotEntry[] _items;

        private StudentDayQuestInventorySnapshot(QuestSnapshotEntry[] quests, InventorySnapshotEntry[] items)
        {
            _quests = quests ?? Array.Empty<QuestSnapshotEntry>();
            _items = items ?? Array.Empty<InventorySnapshotEntry>();
        }

        public QuestSnapshotEntry[] Quests => _quests;
        public InventorySnapshotEntry[] Items => _items;

        public static StudentDayQuestInventorySnapshot Capture(QuestLog questLog, IReadOnlyList<QuestDefinition> quests, Inventory inventory)
        {
            return new StudentDayQuestInventorySnapshot(CaptureQuests(questLog, quests), CaptureInventory(inventory));
        }

        private static QuestSnapshotEntry[] CaptureQuests(QuestLog questLog, IReadOnlyList<QuestDefinition> quests)
        {
            if (quests == null || quests.Count == 0)
            {
                return Array.Empty<QuestSnapshotEntry>();
            }

            var entries = new List<QuestSnapshotEntry>(quests.Count);
            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id))
                {
                    continue;
                }

                entries.Add(new QuestSnapshotEntry(
                    quest,
                    quest.Id,
                    string.IsNullOrEmpty(quest.DisplayNameKey) ? quest.Id : quest.DisplayNameKey,
                    questLog != null ? questLog.GetState(quest) : QuestState.NotStarted));
            }

            return entries.ToArray();
        }

        private static InventorySnapshotEntry[] CaptureInventory(Inventory inventory)
        {
            if (inventory == null)
            {
                return Array.Empty<InventorySnapshotEntry>();
            }

            var byItem = new Dictionary<string, InventorySnapshotEntry>();
            var slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.Item == null || slot.Count <= 0 || string.IsNullOrEmpty(slot.Item.Id))
                {
                    continue;
                }

                string itemId = slot.Item.Id;
                byItem.TryGetValue(itemId, out var existing);
                byItem[itemId] = new InventorySnapshotEntry(
                    slot.Item,
                    itemId,
                    string.IsNullOrEmpty(slot.Item.DisplayKey) ? itemId : slot.Item.DisplayKey,
                    existing.Count + slot.Count);
            }

            var entries = new InventorySnapshotEntry[byItem.Count];
            byItem.Values.CopyTo(entries, 0);
            return entries;
        }
    }

    public static class StudentDayQuestInventorySummaryBuilder
    {
        public static StudentDayQuestInventorySummary Build(StudentDayQuestInventorySnapshot before, StudentDayQuestInventorySnapshot after)
        {
            before ??= StudentDayQuestInventorySnapshot.Capture(null, null, null);
            after ??= StudentDayQuestInventorySnapshot.Capture(null, null, null);

            var questEntries = BuildQuestEntries(before, after);
            var rewardEntries = BuildRewardEntries(before, after);
            var inventoryDeltas = BuildInventoryDeltas(before, after, rewardEntries);
            return new StudentDayQuestInventorySummary(questEntries, inventoryDeltas, rewardEntries);
        }

        private static QuestSummaryEntry[] BuildQuestEntries(StudentDayQuestInventorySnapshot before, StudentDayQuestInventorySnapshot after)
        {
            var results = new List<QuestSummaryEntry>();
            for (int i = 0; i < after.Quests.Length; i++)
            {
                var current = after.Quests[i];
                QuestState beforeState = FindQuestState(before, current.QuestId);
                if (beforeState == current.State)
                {
                    continue;
                }

                if (beforeState == QuestState.NotStarted && current.State != QuestState.NotStarted)
                {
                    results.Add(new QuestSummaryEntry(current.QuestId, current.DisplayName, QuestState.NotStarted, QuestState.Active));
                }

                if (current.State == QuestState.RewardClaimed)
                {
                    results.Add(new QuestSummaryEntry(current.QuestId, current.DisplayName, QuestState.Active, QuestState.RewardClaimed));
                }
                else if (current.State == QuestState.Completed)
                {
                    results.Add(new QuestSummaryEntry(current.QuestId, current.DisplayName, QuestState.Active, QuestState.Completed));
                }
                else if (beforeState != QuestState.NotStarted || current.State == QuestState.Active)
                {
                    results.Add(new QuestSummaryEntry(current.QuestId, current.DisplayName, beforeState, current.State));
                }
            }

            return results.ToArray();
        }

        private static RewardSummaryEntry[] BuildRewardEntries(StudentDayQuestInventorySnapshot before, StudentDayQuestInventorySnapshot after)
        {
            var entries = new List<RewardSummaryEntry>();
            for (int i = 0; i < after.Quests.Length; i++)
            {
                var quest = after.Quests[i];
                if (quest.State != QuestState.RewardClaimed || FindQuestState(before, quest.QuestId) == QuestState.RewardClaimed || quest.Definition == null || quest.Definition.Rewards == null)
                {
                    continue;
                }

                for (int j = 0; j < quest.Definition.Rewards.Length; j++)
                {
                    if (quest.Definition.Rewards[j] is ItemQuestReward itemReward && itemReward.Item != null)
                    {
                        entries.Add(new RewardSummaryEntry(
                            quest.QuestId,
                            quest.DisplayName,
                            itemReward.Item.Id,
                            string.IsNullOrEmpty(itemReward.Item.DisplayKey) ? itemReward.Item.Id : itemReward.Item.DisplayKey,
                            itemReward.Count));
                    }
                }
            }

            return entries.ToArray();
        }

        private static InventoryDeltaEntry[] BuildInventoryDeltas(StudentDayQuestInventorySnapshot before, StudentDayQuestInventorySnapshot after, RewardSummaryEntry[] rewards)
        {
            var results = new List<InventoryDeltaEntry>();
            for (int i = 0; i < after.Items.Length; i++)
            {
                var item = after.Items[i];
                int beforeCount = FindItemCount(before, item.ItemId);
                if (beforeCount == item.Count)
                {
                    continue;
                }

                results.Add(new InventoryDeltaEntry(item.ItemId, item.DisplayName, beforeCount, item.Count, IsRewardItem(rewards, item.ItemId)));
            }

            for (int i = 0; i < before.Items.Length; i++)
            {
                var item = before.Items[i];
                if (FindItemCount(after, item.ItemId) != 0)
                {
                    continue;
                }

                results.Add(new InventoryDeltaEntry(item.ItemId, item.DisplayName, item.Count, 0, false));
            }

            return results.ToArray();
        }

        private static QuestState FindQuestState(StudentDayQuestInventorySnapshot snapshot, string questId)
        {
            if (snapshot == null || string.IsNullOrEmpty(questId))
            {
                return QuestState.NotStarted;
            }

            for (int i = 0; i < snapshot.Quests.Length; i++)
            {
                if (snapshot.Quests[i].QuestId == questId)
                {
                    return snapshot.Quests[i].State;
                }
            }

            return QuestState.NotStarted;
        }

        private static int FindItemCount(StudentDayQuestInventorySnapshot snapshot, string itemId)
        {
            if (snapshot == null || string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            for (int i = 0; i < snapshot.Items.Length; i++)
            {
                if (snapshot.Items[i].ItemId == itemId)
                {
                    return snapshot.Items[i].Count;
                }
            }

            return 0;
        }

        private static bool IsRewardItem(RewardSummaryEntry[] rewards, string itemId)
        {
            if (rewards == null || string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (int i = 0; i < rewards.Length; i++)
            {
                if (rewards[i].ItemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public readonly struct QuestSnapshotEntry
    {
        public readonly QuestDefinition Definition;
        public readonly string QuestId;
        public readonly string DisplayName;
        public readonly QuestState State;

        public QuestSnapshotEntry(QuestDefinition definition, string questId, string displayName, QuestState state)
        {
            Definition = definition;
            QuestId = string.IsNullOrEmpty(questId) ? string.Empty : questId;
            DisplayName = string.IsNullOrEmpty(displayName) ? QuestId : displayName;
            State = state;
        }
    }

    public readonly struct InventorySnapshotEntry
    {
        public readonly ItemDefinition Definition;
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly int Count;

        public InventorySnapshotEntry(ItemDefinition definition, string itemId, string displayName, int count)
        {
            Definition = definition;
            ItemId = string.IsNullOrEmpty(itemId) ? string.Empty : itemId;
            DisplayName = string.IsNullOrEmpty(displayName) ? ItemId : displayName;
            Count = Math.Max(0, count);
        }
    }
}
