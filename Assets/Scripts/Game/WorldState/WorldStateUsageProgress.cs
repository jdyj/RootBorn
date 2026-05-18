using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [Serializable]
    public sealed class WorldStateUsageProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public WorldStateUsageRecordSaveData[] Records = Array.Empty<WorldStateUsageRecordSaveData>();
        public string[] TodayUsageIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class WorldStateUsageRecordSaveData
    {
        public string UsageId;
        public string SourceWorldStateFlagId;
        public int UsedCount;
        public int FirstUsedDay;
        public int LastUsedDay;
        public int CooldownUntilDay;
        public string[] RewardClaimedIds = Array.Empty<string>();
        public string[] UnlockedActivityIds = Array.Empty<string>();
        public string[] UnlockedFollowUpQuestIds = Array.Empty<string>();
        public string[] DiscoveredEncyclopediaEntryIds = Array.Empty<string>();
        public string[] CareerHintGrantHistory = Array.Empty<string>();
    }

    public sealed class WorldStateUsageRecord
    {
        private readonly HashSet<string> _rewardClaimedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unlockedActivityIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unlockedFollowUpQuestIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _discoveredEncyclopediaEntryIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _careerHintGrantHistory = new HashSet<string>(StringComparer.Ordinal);

        public WorldStateUsageRecord(string usageId, string sourceWorldStateFlagId)
        {
            UsageId = string.IsNullOrEmpty(usageId) ? string.Empty : usageId;
            SourceWorldStateFlagId = string.IsNullOrEmpty(sourceWorldStateFlagId) ? string.Empty : sourceWorldStateFlagId;
        }

        public string UsageId { get; }
        public string SourceWorldStateFlagId { get; private set; }
        public int UsedCount { get; private set; }
        public int FirstUsedDay { get; private set; }
        public int LastUsedDay { get; private set; }
        public int CooldownUntilDay { get; private set; }
        public string[] RewardClaimedIds => ToArray(_rewardClaimedIds);
        public string[] UnlockedActivityIds => ToArray(_unlockedActivityIds);
        public string[] UnlockedFollowUpQuestIds => ToArray(_unlockedFollowUpQuestIds);
        public string[] DiscoveredEncyclopediaEntryIds => ToArray(_discoveredEncyclopediaEntryIds);
        public string[] CareerHintGrantHistory => ToArray(_careerHintGrantHistory);

        public bool HasClaimedReward(string rewardId) => !string.IsNullOrEmpty(rewardId) && _rewardClaimedIds.Contains(rewardId);

        public void MarkUsed(WorldStateUsageDefinition usage, int day, WorldStateUsageRepeatPolicyDefinition policy, string[] rewardIds, string[] activityIds, string[] questIds, string[] encyclopediaEntryIds, string[] careerHintIds)
        {
            if (usage != null && !string.IsNullOrEmpty(usage.SourceFlagId)) SourceWorldStateFlagId = usage.SourceFlagId;
            int safeDay = Mathf.Max(1, day);
            if (FirstUsedDay <= 0) FirstUsedDay = safeDay;
            LastUsedDay = safeDay;
            UsedCount++;
            CooldownUntilDay = policy != null ? policy.NextCooldownUntilDay(safeDay) : 0;
            AddRange(_rewardClaimedIds, rewardIds);
            AddRange(_unlockedActivityIds, activityIds);
            AddRange(_unlockedFollowUpQuestIds, questIds);
            AddRange(_discoveredEncyclopediaEntryIds, encyclopediaEntryIds);
            AddRange(_careerHintGrantHistory, careerHintIds);
        }

        public WorldStateUsageRecordSaveData ToSaveData()
        {
            return new WorldStateUsageRecordSaveData
            {
                UsageId = UsageId,
                SourceWorldStateFlagId = SourceWorldStateFlagId,
                UsedCount = UsedCount,
                FirstUsedDay = FirstUsedDay,
                LastUsedDay = LastUsedDay,
                CooldownUntilDay = CooldownUntilDay,
                RewardClaimedIds = RewardClaimedIds,
                UnlockedActivityIds = UnlockedActivityIds,
                UnlockedFollowUpQuestIds = UnlockedFollowUpQuestIds,
                DiscoveredEncyclopediaEntryIds = DiscoveredEncyclopediaEntryIds,
                CareerHintGrantHistory = CareerHintGrantHistory,
            };
        }

        public static WorldStateUsageRecord FromSaveData(WorldStateUsageRecordSaveData saveData)
        {
            var record = new WorldStateUsageRecord(saveData != null ? saveData.UsageId : string.Empty, saveData != null ? saveData.SourceWorldStateFlagId : string.Empty);
            if (saveData == null) return record;
            record.UsedCount = Mathf.Max(0, saveData.UsedCount);
            record.FirstUsedDay = Mathf.Max(0, saveData.FirstUsedDay);
            record.LastUsedDay = Mathf.Max(0, saveData.LastUsedDay);
            record.CooldownUntilDay = Mathf.Max(0, saveData.CooldownUntilDay);
            AddRange(record._rewardClaimedIds, saveData.RewardClaimedIds);
            AddRange(record._unlockedActivityIds, saveData.UnlockedActivityIds);
            AddRange(record._unlockedFollowUpQuestIds, saveData.UnlockedFollowUpQuestIds);
            AddRange(record._discoveredEncyclopediaEntryIds, saveData.DiscoveredEncyclopediaEntryIds);
            AddRange(record._careerHintGrantHistory, saveData.CareerHintGrantHistory);
            return record;
        }

        private static void AddRange(HashSet<string> target, string[] values)
        {
            if (target == null || values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }
    }

    public readonly struct WorldStateUsageTodaySummaryEntry
    {
        public readonly string UsageId;
        public readonly string SourceWorldStateFlagId;
        public readonly int UsedDay;

        public WorldStateUsageTodaySummaryEntry(string usageId, string sourceWorldStateFlagId, int usedDay)
        {
            UsageId = string.IsNullOrEmpty(usageId) ? string.Empty : usageId;
            SourceWorldStateFlagId = string.IsNullOrEmpty(sourceWorldStateFlagId) ? string.Empty : sourceWorldStateFlagId;
            UsedDay = Mathf.Max(0, usedDay);
        }
    }

    public sealed class WorldStateUsageProgress
    {
        private readonly Dictionary<string, WorldStateUsageRecord> _records = new Dictionary<string, WorldStateUsageRecord>(StringComparer.Ordinal);
        private readonly List<string> _todayUsageIds = new List<string>();

        public WorldStateUsageProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int RecordCount => _records.Count;

        public WorldStateUsageTodaySummaryEntry[] TodayUsageSummary
        {
            get
            {
                var result = new List<WorldStateUsageTodaySummaryEntry>(_todayUsageIds.Count);
                for (int i = 0; i < _todayUsageIds.Count; i++)
                {
                    if (_records.TryGetValue(_todayUsageIds[i], out var record)) result.Add(new WorldStateUsageTodaySummaryEntry(record.UsageId, record.SourceWorldStateFlagId, record.LastUsedDay));
                }

                return result.ToArray();
            }
        }

        public WorldStateUsageRecord GetRecord(string usageId)
        {
            string key = string.IsNullOrEmpty(usageId) ? string.Empty : usageId;
            if (!_records.TryGetValue(key, out var record))
            {
                record = new WorldStateUsageRecord(key, string.Empty);
                _records[key] = record;
            }

            return record;
        }

        public bool HasClaimedReward(string usageId, string rewardId)
        {
            return !string.IsNullOrEmpty(usageId) && _records.TryGetValue(usageId, out var record) && record.HasClaimedReward(rewardId);
        }

        public void MarkUsed(WorldStateUsageDefinition usage, int day, WorldStateUsageRepeatPolicyDefinition policy, string[] rewardIds, string[] activityIds, string[] questIds, string[] encyclopediaEntryIds, string[] careerHintIds)
        {
            if (usage == null || string.IsNullOrEmpty(usage.Id)) return;
            var record = GetRecord(usage.Id);
            record.MarkUsed(usage, day, policy, rewardIds, activityIds, questIds, encyclopediaEntryIds, careerHintIds);
            if (!_todayUsageIds.Contains(usage.Id)) _todayUsageIds.Add(usage.Id);
        }

        public WorldStateUsageProgressSaveData ToSaveData()
        {
            var records = new List<WorldStateUsageRecordSaveData>(_records.Count);
            foreach (var pair in _records)
            {
                if (!string.IsNullOrEmpty(pair.Key)) records.Add(pair.Value.ToSaveData());
            }

            return new WorldStateUsageProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                Records = records.ToArray(),
                TodayUsageIds = _todayUsageIds.ToArray(),
            };
        }

        public static WorldStateUsageProgress FromSaveData(WorldStateUsageProgressSaveData saveData)
        {
            var progress = new WorldStateUsageProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player");
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    var record = WorldStateUsageRecord.FromSaveData(saveData.Records[i]);
                    if (!string.IsNullOrEmpty(record.UsageId)) progress._records[record.UsageId] = record;
                }
            }

            if (saveData.TodayUsageIds != null)
            {
                for (int i = 0; i < saveData.TodayUsageIds.Length; i++)
                {
                    string usageId = saveData.TodayUsageIds[i];
                    if (!string.IsNullOrEmpty(usageId) && progress._records.ContainsKey(usageId) && !progress._todayUsageIds.Contains(usageId)) progress._todayUsageIds.Add(usageId);
                }
            }

            return progress;
        }
    }
}
