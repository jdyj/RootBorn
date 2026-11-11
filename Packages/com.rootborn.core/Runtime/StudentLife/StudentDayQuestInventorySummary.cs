using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;

namespace Rootborn.Core.StudentLife
{
    public readonly struct QuestSummaryEntry
    {
        public readonly string QuestId;
        public readonly string DisplayName;
        public readonly QuestStage BeforeState;
        public readonly QuestStage AfterState;

        public QuestSummaryEntry(string questId, string displayName, QuestStage beforeState, QuestStage afterState)
        {
            QuestId = questId ?? string.Empty;
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
            ItemId = itemId ?? string.Empty;
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
            QuestId = questId ?? string.Empty;
            QuestDisplayName = string.IsNullOrEmpty(questDisplayName) ? QuestId : questDisplayName;
            ItemId = itemId ?? string.Empty;
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

    public readonly struct QuestSnapshotEntry
    {
        public readonly QuestDefinition Definition;
        public readonly string QuestId;
        public readonly string DisplayName;
        public readonly QuestStage State;

        public QuestSnapshotEntry(QuestDefinition definition, string questId, string displayName, QuestStage state)
        {
            Definition = definition;
            QuestId = questId ?? string.Empty;
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
            ItemId = itemId ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? ItemId : displayName;
            Count = Math.Max(0, count);
        }
    }

    /// <summary>하루 시작·끝의 퀘스트 단계와 물건 수(물건별 합). 요약은 두 장면을 비교해 만든다.</summary>
    public sealed class StudentDayQuestInventorySnapshot
    {
        private StudentDayQuestInventorySnapshot(QuestSnapshotEntry[] quests, InventorySnapshotEntry[] items)
        {
            Quests = quests ?? Array.Empty<QuestSnapshotEntry>();
            Items = items ?? Array.Empty<InventorySnapshotEntry>();
        }

        public QuestSnapshotEntry[] Quests { get; }
        public InventorySnapshotEntry[] Items { get; }

        public static StudentDayQuestInventorySnapshot Capture(QuestLog questLog, IReadOnlyList<QuestDefinition> quests, ItemInventory inventory)
            => new StudentDayQuestInventorySnapshot(CaptureQuests(questLog, quests), CaptureInventory(inventory));

        private static QuestSnapshotEntry[] CaptureQuests(QuestLog questLog, IReadOnlyList<QuestDefinition> quests)
        {
            if (quests == null || quests.Count == 0) return Array.Empty<QuestSnapshotEntry>();
            var entries = new List<QuestSnapshotEntry>(quests.Count);
            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || quest.Id.Length == 0) continue;
                entries.Add(new QuestSnapshotEntry(quest, quest.Id, string.IsNullOrEmpty(quest.DisplayNameKey) ? quest.Id : quest.DisplayNameKey,
                    questLog != null ? questLog.GetState(quest) : QuestStage.NotStarted));
            }
            return entries.ToArray();
        }

        private static InventorySnapshotEntry[] CaptureInventory(ItemInventory inventory)
        {
            if (inventory == null) return Array.Empty<InventorySnapshotEntry>();
            var order = new List<string>();
            var byItem = new Dictionary<string, InventorySnapshotEntry>(StringComparer.Ordinal);
            var slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.Item == null || slot.Count <= 0 || string.IsNullOrEmpty(slot.Item.Id)) continue;
                string itemId = slot.Item.Id;
                if (!byItem.TryGetValue(itemId, out var existing)) order.Add(itemId);
                byItem[itemId] = new InventorySnapshotEntry(slot.Item, itemId, string.IsNullOrEmpty(slot.Item.DisplayKey) ? itemId : slot.Item.DisplayKey, existing.Count + slot.Count);
            }
            var entries = new InventorySnapshotEntry[order.Count];
            for (int i = 0; i < order.Count; i++) entries[i] = byItem[order[i]];
            return entries;
        }
    }

    /// <summary>
    /// 하루의 퀘스트 변화, 물건 변화, 받은 퀘스트 보상 요약. 원본과 같은 규칙으로, 시작하지 않은 퀘스트가 끝나 있으면
    /// "시작"과 "완료(또는 보상 받음)" 두 줄을 만든다. 같은 장면끼리 비교하면 아무것도 만들지 않는다.
    /// 보상 줄은 물건 하나로 끝나는 보상(TryGetInventoryGrant)만 센다.
    /// </summary>
    public static class StudentDayQuestInventorySummaryBuilder
    {
        public static StudentDayQuestInventorySummary Build(StudentDayQuestInventorySnapshot before, StudentDayQuestInventorySnapshot after)
        {
            before = before ?? StudentDayQuestInventorySnapshot.Capture(null, null, null);
            after = after ?? StudentDayQuestInventorySnapshot.Capture(null, null, null);
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
                QuestStage beforeState = FindQuestState(before, current.QuestId);
                if (beforeState == current.State) continue;
                if (beforeState == QuestStage.NotStarted && current.State != QuestStage.NotStarted)
                    results.Add(new QuestSummaryEntry(current.QuestId, current.DisplayName, QuestStage.NotStarted, QuestStage.Active));
                if (current.State == QuestStage.RewardClaimed)
                    results.Add(new QuestSummaryEntry(current.QuestId, current.DisplayName, QuestStage.Active, QuestStage.RewardClaimed));
                else if (current.State == QuestStage.Completed)
                    results.Add(new QuestSummaryEntry(current.QuestId, current.DisplayName, QuestStage.Active, QuestStage.Completed));
                else if (beforeState != QuestStage.NotStarted || current.State == QuestStage.Active)
                    results.Add(new QuestSummaryEntry(current.QuestId, current.DisplayName, beforeState, current.State));
            }
            return results.ToArray();
        }

        private static RewardSummaryEntry[] BuildRewardEntries(StudentDayQuestInventorySnapshot before, StudentDayQuestInventorySnapshot after)
        {
            var entries = new List<RewardSummaryEntry>();
            for (int i = 0; i < after.Quests.Length; i++)
            {
                var quest = after.Quests[i];
                if (quest.State != QuestStage.RewardClaimed || FindQuestState(before, quest.QuestId) == QuestStage.RewardClaimed || quest.Definition == null) continue;
                var rewards = quest.Definition.Rewards;
                for (int j = 0; j < rewards.Count; j++)
                {
                    var reward = rewards[j];
                    if (reward == null || !reward.TryGetInventoryGrant(out var grant) || grant.Item == null) continue;
                    entries.Add(new RewardSummaryEntry(quest.QuestId, quest.DisplayName, grant.Item.Id, string.IsNullOrEmpty(grant.Item.DisplayKey) ? grant.Item.Id : grant.Item.DisplayKey, grant.Count));
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
                if (beforeCount != item.Count) results.Add(new InventoryDeltaEntry(item.ItemId, item.DisplayName, beforeCount, item.Count, IsRewardItem(rewards, item.ItemId)));
            }
            for (int i = 0; i < before.Items.Length; i++)
            {
                var item = before.Items[i];
                if (FindItemCount(after, item.ItemId) == 0) results.Add(new InventoryDeltaEntry(item.ItemId, item.DisplayName, item.Count, 0, false));
            }
            return results.ToArray();
        }

        private static QuestStage FindQuestState(StudentDayQuestInventorySnapshot snapshot, string questId)
        {
            if (snapshot == null || string.IsNullOrEmpty(questId)) return QuestStage.NotStarted;
            for (int i = 0; i < snapshot.Quests.Length; i++) if (snapshot.Quests[i].QuestId == questId) return snapshot.Quests[i].State;
            return QuestStage.NotStarted;
        }

        private static int FindItemCount(StudentDayQuestInventorySnapshot snapshot, string itemId)
        {
            if (snapshot == null || string.IsNullOrEmpty(itemId)) return 0;
            for (int i = 0; i < snapshot.Items.Length; i++) if (snapshot.Items[i].ItemId == itemId) return snapshot.Items[i].Count;
            return 0;
        }

        private static bool IsRewardItem(RewardSummaryEntry[] rewards, string itemId)
        {
            if (rewards == null || string.IsNullOrEmpty(itemId)) return false;
            for (int i = 0; i < rewards.Length; i++) if (rewards[i].ItemId == itemId) return true;
            return false;
        }
    }
}
