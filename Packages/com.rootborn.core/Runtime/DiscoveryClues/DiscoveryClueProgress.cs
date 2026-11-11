using System;
using System.Collections.Generic;

namespace Rootborn.Core.DiscoveryClues
{
    /// <summary>세이브 파일에 쓰는 모양. 필드 이름은 기존 세이브 파일과 같아야 한다.</summary>
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

    /// <summary>단서 하나의 기록. 본 출처, 추적 여부, 완료와 그때 준 것들. 완료는 한 번뿐이다.</summary>
    public sealed class DiscoveryClueRecord
    {
        private readonly List<string> _sourceIdsSeen = new List<string>();
        private readonly List<string> _rewardClaimedIds = new List<string>();
        private readonly List<string> _encyclopediaEntryIds = new List<string>();
        private readonly List<string> _careerHintIds = new List<string>();
        private readonly List<string> _followUpQuestIds = new List<string>();
        private readonly List<string> _worldStateFlagIds = new List<string>();

        public DiscoveryClueRecord(string clueId) { ClueId = clueId ?? string.Empty; }

        public string ClueId { get; }
        public string[] SourceIdsSeen => _sourceIdsSeen.ToArray();
        public bool Seen { get; private set; }
        public bool Tracked { get; private set; }
        public bool Completed { get; private set; }
        public bool RewardClaimed { get; private set; }
        public int FirstSeenDay { get; private set; }
        public int LastSeenDay { get; private set; }
        public int CompletedDay { get; private set; }
        public int CompletedCount { get; private set; }
        public string[] RewardClaimedIds => _rewardClaimedIds.ToArray();
        public string[] RelatedDiscoveredEncyclopediaEntryIds => _encyclopediaEntryIds.ToArray();
        public string[] RelatedCareerHintGrantHistory => _careerHintIds.ToArray();
        public string[] RelatedFollowUpQuestIds => _followUpQuestIds.ToArray();
        public string[] RelatedWorldStateFlagIds => _worldStateFlagIds.ToArray();

        public bool HasSeenSource(string sourceId) => !string.IsNullOrEmpty(sourceId) && _sourceIdsSeen.Contains(sourceId);

        internal void MarkSourceSeen(string sourceId, int day)
        {
            AddUnique(_sourceIdsSeen, sourceId);
            int safeDay = Math.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            Seen = true;
        }

        internal void SetTracked(bool tracked) => Tracked = tracked;

        internal void MarkCompleted(int day, string[] rewardIds, string[] encyclopediaEntryIds, string[] careerHintIds, string[] questIds, string[] worldStateFlagIds)
        {
            if (Completed) return;
            int safeDay = Math.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            CompletedDay = safeDay;
            Completed = true;
            Seen = true;
            RewardClaimed = true;
            CompletedCount = 1;
            AddRange(_rewardClaimedIds, rewardIds);
            AddRange(_encyclopediaEntryIds, encyclopediaEntryIds);
            AddRange(_careerHintIds, careerHintIds);
            AddRange(_followUpQuestIds, questIds);
            AddRange(_worldStateFlagIds, worldStateFlagIds);
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
            var record = new DiscoveryClueRecord(saveData?.ClueId);
            if (saveData == null) return record;
            AddRange(record._sourceIdsSeen, saveData.SourceIdsSeen);
            record.Seen = saveData.Seen;
            record.Tracked = saveData.Tracked;
            record.Completed = saveData.Completed;
            record.RewardClaimed = saveData.RewardClaimed;
            record.FirstSeenDay = Math.Max(0, saveData.FirstSeenDay);
            record.LastSeenDay = Math.Max(0, saveData.LastSeenDay);
            record.CompletedDay = Math.Max(0, saveData.CompletedDay);
            record.CompletedCount = Math.Max(0, saveData.CompletedCount);
            AddRange(record._rewardClaimedIds, saveData.RewardClaimedIds);
            AddRange(record._encyclopediaEntryIds, saveData.RelatedDiscoveredEncyclopediaEntryIds);
            AddRange(record._careerHintIds, saveData.RelatedCareerHintGrantHistory);
            AddRange(record._followUpQuestIds, saveData.RelatedFollowUpQuestIds);
            AddRange(record._worldStateFlagIds, saveData.RelatedWorldStateFlagIds);
            return record;
        }

        // 원본은 HashSet이라 순서가 정해지지 않았다. 여기는 넣은 순서를 지킨다.
        private static void AddRange(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) AddUnique(target, values[i]);
        }

        private static void AddUnique(List<string> target, string value)
        {
            if (!string.IsNullOrEmpty(value) && !target.Contains(value)) target.Add(value);
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
            ClueId = clueId ?? string.Empty;
            LastSeenDay = Math.Max(0, lastSeenDay);
            Completed = completed;
            RewardClaimed = rewardClaimed;
        }
    }

    /// <summary>플레이어 한 명의 발견 단서 기록과 오늘 바뀐 단서 목록.</summary>
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

        /// <summary>기록이 없으면 빈 기록을 돌려준다. 원본은 이때 빈 기록을 저장해 두었다(조회만 해도 세이브에 남았다).</summary>
        public DiscoveryClueRecord GetRecord(string clueId)
        {
            string key = clueId ?? string.Empty;
            return _records.TryGetValue(key, out var record) ? record : new DiscoveryClueRecord(key);
        }

        public void MarkSourceSeen(DiscoveryClue clue, DiscoveryClueSource source, int day)
        {
            if (clue == null) return;
            MarkSourceSeen(clue.Id, source != null ? source.Id : string.Empty, day);
        }

        /// <summary>id로 출처를 봤다고 남긴다. 출처 id가 비어도 단서는 본 것으로 남는다(원본과 같다).</summary>
        public void MarkSourceSeen(string clueId, string sourceId, int day)
        {
            if (string.IsNullOrEmpty(clueId)) return;
            GetOrCreate(clueId).MarkSourceSeen(sourceId, day);
            AddToday(clueId);
        }

        public void MarkTracked(DiscoveryClue clue, bool tracked)
        {
            if (clue == null || string.IsNullOrEmpty(clue.Id)) return;
            GetOrCreate(clue.Id).SetTracked(tracked);
        }

        public void MarkCompleted(DiscoveryClue clue, int day, string[] rewardIds, string[] encyclopediaEntryIds, string[] careerHintIds, string[] questIds, string[] worldStateFlagIds)
        {
            if (clue == null || string.IsNullOrEmpty(clue.Id)) return;
            GetOrCreate(clue.Id).MarkCompleted(day, rewardIds, encyclopediaEntryIds, careerHintIds, questIds, worldStateFlagIds);
            AddToday(clue.Id);
        }

        public DiscoveryClueProgressSaveData ToSaveData()
        {
            var records = new List<DiscoveryClueRecordSaveData>(_records.Count);
            foreach (var pair in _records) if (!string.IsNullOrEmpty(pair.Key)) records.Add(pair.Value.ToSaveData());
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
            var progress = new DiscoveryClueProgress(saveData?.SaveSlot, saveData?.PlayerId);
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    if (saveData.Records[i] == null) continue;
                    var record = DiscoveryClueRecord.FromSaveData(saveData.Records[i]);
                    if (!string.IsNullOrEmpty(record.ClueId)) progress._records[record.ClueId] = record;
                }
            }

            if (saveData.TodayClueIds != null)
            {
                for (int i = 0; i < saveData.TodayClueIds.Length; i++)
                {
                    string clueId = saveData.TodayClueIds[i];
                    if (!string.IsNullOrEmpty(clueId) && progress._records.ContainsKey(clueId)) progress.AddToday(clueId);
                }
            }

            return progress;
        }

        private DiscoveryClueRecord GetOrCreate(string clueId)
        {
            if (!_records.TryGetValue(clueId, out var record))
            {
                record = new DiscoveryClueRecord(clueId);
                _records.Add(clueId, record);
            }

            return record;
        }

        private void AddToday(string clueId)
        {
            if (!string.IsNullOrEmpty(clueId) && !_todayClueIds.Contains(clueId)) _todayClueIds.Add(clueId);
        }
    }
}
