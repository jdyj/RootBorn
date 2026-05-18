using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [Serializable]
    public sealed class DiscoveryClueProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public DiscoveryClueRecordSaveData[] Records = Array.Empty<DiscoveryClueRecordSaveData>();
        public string[] TodayClueIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class DiscoveryClueRecordSaveData
    {
        public string ClueId;
        public string[] SourceIdsSeen = Array.Empty<string>();
        public bool Seen;
        public bool Tracked;
        public bool Completed;
        public bool RewardClaimed;
        public int FirstSeenDay;
        public int LastSeenDay;
        public int CompletedDay;
        public int CompletedCount;
        public string[] RewardClaimedIds = Array.Empty<string>();
        public string[] RelatedDiscoveredEncyclopediaEntryIds = Array.Empty<string>();
        public string[] RelatedCareerHintGrantHistory = Array.Empty<string>();
        public string[] RelatedFollowUpQuestIds = Array.Empty<string>();
        public string[] RelatedWorldStateFlagIds = Array.Empty<string>();
    }

    public sealed class DiscoveryClueRecord
    {
        private readonly HashSet<string> _sourceIdsSeen = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _rewardClaimedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _relatedDiscoveredEncyclopediaEntryIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _relatedCareerHintGrantHistory = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _relatedFollowUpQuestIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _relatedWorldStateFlagIds = new HashSet<string>(StringComparer.Ordinal);

        public DiscoveryClueRecord(string clueId)
        {
            ClueId = string.IsNullOrEmpty(clueId) ? string.Empty : clueId;
        }

        public string ClueId { get; }
        public string[] SourceIdsSeen => ToArray(_sourceIdsSeen);
        public bool Seen { get; private set; }
        public bool Tracked { get; private set; }
        public bool Completed { get; private set; }
        public bool RewardClaimed { get; private set; }
        public int FirstSeenDay { get; private set; }
        public int LastSeenDay { get; private set; }
        public int CompletedDay { get; private set; }
        public int CompletedCount { get; private set; }
        public string[] RewardClaimedIds => ToArray(_rewardClaimedIds);
        public string[] RelatedDiscoveredEncyclopediaEntryIds => ToArray(_relatedDiscoveredEncyclopediaEntryIds);
        public string[] RelatedCareerHintGrantHistory => ToArray(_relatedCareerHintGrantHistory);
        public string[] RelatedFollowUpQuestIds => ToArray(_relatedFollowUpQuestIds);
        public string[] RelatedWorldStateFlagIds => ToArray(_relatedWorldStateFlagIds);

        public bool HasSeenSource(string sourceId) => !string.IsNullOrEmpty(sourceId) && _sourceIdsSeen.Contains(sourceId);

        public void MarkSourceSeen(DiscoveryClueSourceDefinition source, int day)
        {
            if (source != null && !string.IsNullOrEmpty(source.Id)) _sourceIdsSeen.Add(source.Id);
            int safeDay = Mathf.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            Seen = true;
        }

        public void SetTracked(bool tracked) => Tracked = tracked;

        public void MarkCompleted(int day, string[] rewardIds, string[] encyclopediaEntryIds, string[] careerHintIds, string[] questIds, string[] worldStateFlagIds)
        {
            if (Completed) return;
            int safeDay = Mathf.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            CompletedDay = safeDay;
            Completed = true;
            Seen = true;
            RewardClaimed = true;
            CompletedCount = 1;
            AddRange(_rewardClaimedIds, rewardIds);
            AddRange(_relatedDiscoveredEncyclopediaEntryIds, encyclopediaEntryIds);
            AddRange(_relatedCareerHintGrantHistory, careerHintIds);
            AddRange(_relatedFollowUpQuestIds, questIds);
            AddRange(_relatedWorldStateFlagIds, worldStateFlagIds);
        }

        public DiscoveryClueRecordSaveData ToSaveData()
        {
            return new DiscoveryClueRecordSaveData
            {
                ClueId = ClueId,
                SourceIdsSeen = SourceIdsSeen,
                Seen = Seen,
                Tracked = Tracked,
                Completed = Completed,
                RewardClaimed = RewardClaimed,
                FirstSeenDay = FirstSeenDay,
                LastSeenDay = LastSeenDay,
                CompletedDay = CompletedDay,
                CompletedCount = CompletedCount,
                RewardClaimedIds = RewardClaimedIds,
                RelatedDiscoveredEncyclopediaEntryIds = RelatedDiscoveredEncyclopediaEntryIds,
                RelatedCareerHintGrantHistory = RelatedCareerHintGrantHistory,
                RelatedFollowUpQuestIds = RelatedFollowUpQuestIds,
                RelatedWorldStateFlagIds = RelatedWorldStateFlagIds,
            };
        }

        public static DiscoveryClueRecord FromSaveData(DiscoveryClueRecordSaveData saveData)
        {
            var record = new DiscoveryClueRecord(saveData != null ? saveData.ClueId : string.Empty);
            if (saveData == null) return record;
            AddRange(record._sourceIdsSeen, saveData.SourceIdsSeen);
            record.Seen = saveData.Seen;
            record.Tracked = saveData.Tracked;
            record.Completed = saveData.Completed;
            record.RewardClaimed = saveData.RewardClaimed;
            record.FirstSeenDay = Mathf.Max(0, saveData.FirstSeenDay);
            record.LastSeenDay = Mathf.Max(0, saveData.LastSeenDay);
            record.CompletedDay = Mathf.Max(0, saveData.CompletedDay);
            record.CompletedCount = Mathf.Max(0, saveData.CompletedCount);
            AddRange(record._rewardClaimedIds, saveData.RewardClaimedIds);
            AddRange(record._relatedDiscoveredEncyclopediaEntryIds, saveData.RelatedDiscoveredEncyclopediaEntryIds);
            AddRange(record._relatedCareerHintGrantHistory, saveData.RelatedCareerHintGrantHistory);
            AddRange(record._relatedFollowUpQuestIds, saveData.RelatedFollowUpQuestIds);
            AddRange(record._relatedWorldStateFlagIds, saveData.RelatedWorldStateFlagIds);
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

    public readonly struct DiscoveryClueTodaySummaryEntry
    {
        public readonly string ClueId;
        public readonly int LastSeenDay;
        public readonly bool Completed;
        public readonly bool RewardClaimed;

        public DiscoveryClueTodaySummaryEntry(string clueId, int lastSeenDay, bool completed, bool rewardClaimed)
        {
            ClueId = string.IsNullOrEmpty(clueId) ? string.Empty : clueId;
            LastSeenDay = Mathf.Max(0, lastSeenDay);
            Completed = completed;
            RewardClaimed = rewardClaimed;
        }
    }

    public sealed class DiscoveryClueProgress
    {
        private readonly Dictionary<string, DiscoveryClueRecord> _records = new Dictionary<string, DiscoveryClueRecord>(StringComparer.Ordinal);
        private readonly List<string> _todayClueIds = new List<string>();

        public DiscoveryClueProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int RecordCount => _records.Count;

        public DiscoveryClueTodaySummaryEntry[] TodayClueSummary
        {
            get
            {
                var result = new List<DiscoveryClueTodaySummaryEntry>(_todayClueIds.Count);
                for (int i = 0; i < _todayClueIds.Count; i++)
                {
                    if (_records.TryGetValue(_todayClueIds[i], out var record)) result.Add(new DiscoveryClueTodaySummaryEntry(record.ClueId, record.LastSeenDay, record.Completed, record.RewardClaimed));
                }

                return result.ToArray();
            }
        }

        public DiscoveryClueRecord GetRecord(string clueId)
        {
            string key = string.IsNullOrEmpty(clueId) ? string.Empty : clueId;
            if (!_records.TryGetValue(key, out var record))
            {
                record = new DiscoveryClueRecord(key);
                _records[key] = record;
            }

            return record;
        }

        public void MarkSourceSeen(DiscoveryClueDefinition clue, DiscoveryClueSourceDefinition source, int day)
        {
            if (clue == null || string.IsNullOrEmpty(clue.Id)) return;
            var record = GetRecord(clue.Id);
            record.MarkSourceSeen(source, day);
            AddToday(clue.Id);
        }

        public void MarkTracked(DiscoveryClueDefinition clue, bool tracked)
        {
            if (clue == null || string.IsNullOrEmpty(clue.Id)) return;
            GetRecord(clue.Id).SetTracked(tracked);
        }

        public void MarkCompleted(DiscoveryClueDefinition clue, int day, string[] rewardIds, string[] encyclopediaEntryIds, string[] careerHintIds, string[] questIds, string[] worldStateFlagIds)
        {
            if (clue == null || string.IsNullOrEmpty(clue.Id)) return;
            var record = GetRecord(clue.Id);
            record.MarkCompleted(day, rewardIds, encyclopediaEntryIds, careerHintIds, questIds, worldStateFlagIds);
            AddToday(clue.Id);
        }

        public DiscoveryClueProgressSaveData ToSaveData()
        {
            var records = new List<DiscoveryClueRecordSaveData>(_records.Count);
            foreach (var pair in _records)
            {
                if (!string.IsNullOrEmpty(pair.Key)) records.Add(pair.Value.ToSaveData());
            }

            return new DiscoveryClueProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                Records = records.ToArray(),
                TodayClueIds = _todayClueIds.ToArray(),
            };
        }

        public static DiscoveryClueProgress FromSaveData(DiscoveryClueProgressSaveData saveData)
        {
            var progress = new DiscoveryClueProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player");
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    var record = DiscoveryClueRecord.FromSaveData(saveData.Records[i]);
                    if (!string.IsNullOrEmpty(record.ClueId)) progress._records[record.ClueId] = record;
                }
            }

            if (saveData.TodayClueIds != null)
            {
                for (int i = 0; i < saveData.TodayClueIds.Length; i++)
                {
                    string clueId = saveData.TodayClueIds[i];
                    if (!string.IsNullOrEmpty(clueId) && progress._records.ContainsKey(clueId) && !progress._todayClueIds.Contains(clueId)) progress._todayClueIds.Add(clueId);
                }
            }

            return progress;
        }

        private void AddToday(string clueId)
        {
            if (!string.IsNullOrEmpty(clueId) && !_todayClueIds.Contains(clueId)) _todayClueIds.Add(clueId);
        }
    }
}
