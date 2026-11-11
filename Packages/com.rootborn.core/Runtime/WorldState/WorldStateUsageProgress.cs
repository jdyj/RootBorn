using System;
using System.Collections.Generic;

namespace Rootborn.Core.WorldState
{
    /// <summary>세이브 파일에 쓰는 모양. 필드 이름은 기존 세이브 파일과 같아야 한다.</summary>
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

    /// <summary>사용 하나의 기록. 쓴 횟수, 처음·마지막 날, 쿨다운, 그동안 준 것들.</summary>
    public sealed class WorldStateUsageRecord
    {
        private readonly List<string> _rewardClaimedIds = new List<string>();
        private readonly List<string> _unlockedActivityIds = new List<string>();
        private readonly List<string> _unlockedFollowUpQuestIds = new List<string>();
        private readonly List<string> _discoveredEncyclopediaEntryIds = new List<string>();
        private readonly List<string> _careerHintGrantHistory = new List<string>();

        public WorldStateUsageRecord(string usageId, string sourceWorldStateFlagId)
        {
            UsageId = usageId ?? string.Empty;
            SourceWorldStateFlagId = sourceWorldStateFlagId ?? string.Empty;
        }

        public string UsageId { get; }
        public string SourceWorldStateFlagId { get; private set; }
        public int UsedCount { get; private set; }
        public int FirstUsedDay { get; private set; }
        public int LastUsedDay { get; private set; }
        public int CooldownUntilDay { get; private set; }
        public string[] RewardClaimedIds => _rewardClaimedIds.ToArray();
        public string[] UnlockedActivityIds => _unlockedActivityIds.ToArray();
        public string[] UnlockedFollowUpQuestIds => _unlockedFollowUpQuestIds.ToArray();
        public string[] DiscoveredEncyclopediaEntryIds => _discoveredEncyclopediaEntryIds.ToArray();
        public string[] CareerHintGrantHistory => _careerHintGrantHistory.ToArray();

        public bool HasClaimedReward(string rewardId) => !string.IsNullOrEmpty(rewardId) && _rewardClaimedIds.Contains(rewardId);

        internal void MarkUsed(WorldStateUsage usage, int day, WorldStateUsageRepeatPolicy policy, string[] rewardIds, string[] activityIds, string[] questIds, string[] encyclopediaEntryIds, string[] careerHintIds)
        {
            if (usage != null && !string.IsNullOrEmpty(usage.SourceFlagId)) SourceWorldStateFlagId = usage.SourceFlagId;
            int safeDay = Math.Max(1, day);
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
            var record = new WorldStateUsageRecord(saveData?.UsageId, saveData?.SourceWorldStateFlagId);
            if (saveData == null) return record;
            record.UsedCount = Math.Max(0, saveData.UsedCount);
            record.FirstUsedDay = Math.Max(0, saveData.FirstUsedDay);
            record.LastUsedDay = Math.Max(0, saveData.LastUsedDay);
            record.CooldownUntilDay = Math.Max(0, saveData.CooldownUntilDay);
            AddRange(record._rewardClaimedIds, saveData.RewardClaimedIds);
            AddRange(record._unlockedActivityIds, saveData.UnlockedActivityIds);
            AddRange(record._unlockedFollowUpQuestIds, saveData.UnlockedFollowUpQuestIds);
            AddRange(record._discoveredEncyclopediaEntryIds, saveData.DiscoveredEncyclopediaEntryIds);
            AddRange(record._careerHintGrantHistory, saveData.CareerHintGrantHistory);
            return record;
        }

        // 원본은 HashSet이라 순서가 정해지지 않았다. 여기는 넣은 순서를 지킨다.
        private static void AddRange(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i]) && !target.Contains(values[i])) target.Add(values[i]);
        }
    }

    public readonly struct WorldStateUsageTodaySummaryEntry
    {
        public readonly string UsageId;
        public readonly string SourceWorldStateFlagId;
        public readonly int UsedDay;

        public WorldStateUsageTodaySummaryEntry(string usageId, string sourceWorldStateFlagId, int usedDay)
        {
            UsageId = usageId ?? string.Empty;
            SourceWorldStateFlagId = sourceWorldStateFlagId ?? string.Empty;
            UsedDay = Math.Max(0, usedDay);
        }
    }

    /// <summary>플레이어 한 명의 월드 상태 사용 기록과 오늘 쓴 목록.</summary>
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

        /// <summary>
        /// 기록이 없으면 빈 기록을 돌려준다. 원본은 이때 빈 기록을 저장해 두었다(조회만 해도 세이브에 남았다). 여기는 쓴 기록만 남긴다.
        /// </summary>
        public WorldStateUsageRecord GetRecord(string usageId)
        {
            string key = usageId ?? string.Empty;
            return _records.TryGetValue(key, out var record) ? record : new WorldStateUsageRecord(key, string.Empty);
        }

        public bool HasClaimedReward(string usageId, string rewardId)
        {
            return !string.IsNullOrEmpty(usageId) && _records.TryGetValue(usageId, out var record) && record.HasClaimedReward(rewardId);
        }

        public void MarkUsed(WorldStateUsage usage, int day, WorldStateUsageRepeatPolicy policy, string[] rewardIds, string[] activityIds, string[] questIds, string[] encyclopediaEntryIds, string[] careerHintIds)
        {
            if (usage == null || string.IsNullOrEmpty(usage.Id)) return;
            if (!_records.TryGetValue(usage.Id, out var record))
            {
                record = new WorldStateUsageRecord(usage.Id, string.Empty);
                _records.Add(usage.Id, record);
            }

            record.MarkUsed(usage, day, policy, rewardIds, activityIds, questIds, encyclopediaEntryIds, careerHintIds);
            if (!_todayUsageIds.Contains(usage.Id)) _todayUsageIds.Add(usage.Id);
        }

        public WorldStateUsageProgressSaveData ToSaveData()
        {
            var records = new List<WorldStateUsageRecordSaveData>(_records.Count);
            foreach (var pair in _records) if (!string.IsNullOrEmpty(pair.Key)) records.Add(pair.Value.ToSaveData());
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
            var progress = new WorldStateUsageProgress(saveData?.SaveSlot, saveData?.PlayerId);
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    if (saveData.Records[i] == null) continue;
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
