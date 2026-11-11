using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.StudentLife
{
    // 세이브 모양: 원본과 필드 이름·형이 같다(옛 세이브를 읽는다).
    [Serializable]
    public sealed class ExplorationProgressSaveData : IVersionedSave
    {
        /// <summary>이 프로그램이 아는 가장 높은 형식 버전. 0은 Unity 판이 쓴 파일(Version 필드 없음)이다.</summary>
        public const int KnownVersion = 1;

        public int Version;
        public string SaveSlot;
        public string PlayerId;
        public ExplorationProgressRecordSaveData[] Records = Array.Empty<ExplorationProgressRecordSaveData>();
        public string[] TodayExplorationSummary = Array.Empty<string>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    [Serializable]
    public sealed class ExplorationProgressRecordSaveData
    {
        public string InteractionId;
        public string[] SelectedChoiceIds = Array.Empty<string>();
        public bool Completed;
        public string[] RewardClaimedChoiceIds = Array.Empty<string>();
        public int RepeatCount;
        public int FirstCompletedDay;
        public int FirstCompletedTimeMinutes;
        public int LastCompletedDay;
        public int LastCompletedTimeMinutes;
        public string[] ItemGrantHistory = Array.Empty<string>();
        public string[] ItemSpendHistory = Array.Empty<string>();
        public string[] DiscoveredClueIds = Array.Empty<string>();
        public string[] DiscoveredEncyclopediaEntryIds = Array.Empty<string>();
        public string[] CareerHintGrantHistory = Array.Empty<string>();
        public string[] FollowUpQuestIds = Array.Empty<string>();
        public string[] FatigueStatusOutcomeHistory = Array.Empty<string>();
    }

    /// <summary>탐색 지점 하나의 기록: 고른 선택지, 보상 받은 선택지, 반복 횟수, 처음·마지막 완료 시각, 결과 내역.</summary>
    public sealed class ExplorationProgressRecord
    {
        private readonly HashSet<string> _selectedChoiceIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _rewardClaimedChoiceIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _itemGrantHistory = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _itemSpendHistory = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _discoveredClueIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _discoveredEncyclopediaEntryIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _careerHintGrantHistory = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _followUpQuestIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _fatigueStatusOutcomeHistory = new HashSet<string>(StringComparer.Ordinal);

        public ExplorationProgressRecord(string interactionId) { InteractionId = interactionId ?? string.Empty; }

        public string InteractionId { get; }
        public string[] SelectedChoiceIds => ToArray(_selectedChoiceIds);
        public bool Completed { get; private set; }
        public int RepeatCount { get; private set; }
        public int FirstCompletedDay { get; private set; }
        public int FirstCompletedTimeMinutes { get; private set; }
        public int LastCompletedDay { get; private set; }
        public int LastCompletedTimeMinutes { get; private set; }
        public string[] ItemGrantHistory => ToArray(_itemGrantHistory);
        public string[] ItemSpendHistory => ToArray(_itemSpendHistory);
        public string[] DiscoveredClueIds => ToArray(_discoveredClueIds);
        public string[] DiscoveredEncyclopediaEntryIds => ToArray(_discoveredEncyclopediaEntryIds);
        public string[] CareerHintGrantHistory => ToArray(_careerHintGrantHistory);
        public string[] FollowUpQuestIds => ToArray(_followUpQuestIds);
        public string[] FatigueStatusOutcomeHistory => ToArray(_fatigueStatusOutcomeHistory);

        public bool IsRewardClaimed(string choiceId) => !string.IsNullOrEmpty(choiceId) && _rewardClaimedChoiceIds.Contains(choiceId);
        public bool HasSelectedChoice(string choiceId) => !string.IsNullOrEmpty(choiceId) && _selectedChoiceIds.Contains(choiceId);

        public void MarkCompleted(string choiceId, int day, int timeMinutes, ExplorationOutcomeCollector collector)
        {
            if (!string.IsNullOrEmpty(choiceId))
            {
                _selectedChoiceIds.Add(choiceId);
                _rewardClaimedChoiceIds.Add(choiceId);
            }
            int safeDay = Math.Max(1, day);
            int safeTime = Math.Max(0, timeMinutes);
            if (!Completed)
            {
                FirstCompletedDay = safeDay;
                FirstCompletedTimeMinutes = safeTime;
            }
            Completed = true;
            RepeatCount = Math.Max(0, RepeatCount) + 1;
            LastCompletedDay = safeDay;
            LastCompletedTimeMinutes = safeTime;
            if (collector == null) return;
            AddRange(_itemGrantHistory, collector.ItemGrantIds);
            AddRange(_itemSpendHistory, collector.ItemSpendIds);
            AddRange(_discoveredClueIds, collector.ClueIds);
            AddRange(_discoveredEncyclopediaEntryIds, collector.EncyclopediaEntryIds);
            AddRange(_careerHintGrantHistory, collector.CareerHintIds);
            AddRange(_followUpQuestIds, collector.FollowUpQuestIds);
            AddRange(_fatigueStatusOutcomeHistory, collector.FatigueStatusIds);
        }

        public ExplorationProgressRecordSaveData ToSaveData()
        {
            return new ExplorationProgressRecordSaveData
            {
                InteractionId = InteractionId,
                SelectedChoiceIds = SelectedChoiceIds,
                Completed = Completed,
                RewardClaimedChoiceIds = ToArray(_rewardClaimedChoiceIds),
                RepeatCount = RepeatCount,
                FirstCompletedDay = FirstCompletedDay,
                FirstCompletedTimeMinutes = FirstCompletedTimeMinutes,
                LastCompletedDay = LastCompletedDay,
                LastCompletedTimeMinutes = LastCompletedTimeMinutes,
                ItemGrantHistory = ItemGrantHistory,
                ItemSpendHistory = ItemSpendHistory,
                DiscoveredClueIds = DiscoveredClueIds,
                DiscoveredEncyclopediaEntryIds = DiscoveredEncyclopediaEntryIds,
                CareerHintGrantHistory = CareerHintGrantHistory,
                FollowUpQuestIds = FollowUpQuestIds,
                FatigueStatusOutcomeHistory = FatigueStatusOutcomeHistory,
            };
        }

        public static ExplorationProgressRecord FromSaveData(ExplorationProgressRecordSaveData saveData)
        {
            var record = new ExplorationProgressRecord(saveData != null ? saveData.InteractionId : string.Empty);
            if (saveData == null) return record;
            AddRange(record._selectedChoiceIds, saveData.SelectedChoiceIds);
            AddRange(record._rewardClaimedChoiceIds, saveData.RewardClaimedChoiceIds);
            AddRange(record._itemGrantHistory, saveData.ItemGrantHistory);
            AddRange(record._itemSpendHistory, saveData.ItemSpendHistory);
            AddRange(record._discoveredClueIds, saveData.DiscoveredClueIds);
            AddRange(record._discoveredEncyclopediaEntryIds, saveData.DiscoveredEncyclopediaEntryIds);
            AddRange(record._careerHintGrantHistory, saveData.CareerHintGrantHistory);
            AddRange(record._followUpQuestIds, saveData.FollowUpQuestIds);
            AddRange(record._fatigueStatusOutcomeHistory, saveData.FatigueStatusOutcomeHistory);
            record.Completed = saveData.Completed;
            record.RepeatCount = Math.Max(0, saveData.RepeatCount);
            record.FirstCompletedDay = Math.Max(0, saveData.FirstCompletedDay);
            record.FirstCompletedTimeMinutes = Math.Max(0, saveData.FirstCompletedTimeMinutes);
            record.LastCompletedDay = Math.Max(0, saveData.LastCompletedDay);
            record.LastCompletedTimeMinutes = Math.Max(0, saveData.LastCompletedTimeMinutes);
            return record;
        }

        private static void AddRange(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }
    }

    /// <summary>탐색 진행 상태. 지점별 기록과 오늘 한 탐색("지점:선택지") 목록.</summary>
    public sealed class ExplorationProgress
    {
        private readonly Dictionary<string, ExplorationProgressRecord> _records = new Dictionary<string, ExplorationProgressRecord>(StringComparer.Ordinal);
        private readonly List<string> _todayExplorationSummary = new List<string>();

        public ExplorationProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public string[] TodayExplorationSummary => _todayExplorationSummary.ToArray();

        /// <summary>기록을 만들지 않고 찾는다. 조건 판정과 요약은 이것만 쓴다.</summary>
        public bool TryGetRecord(string interactionId, out ExplorationProgressRecord record)
        {
            record = null;
            return !string.IsNullOrEmpty(interactionId) && _records.TryGetValue(interactionId, out record);
        }

        /// <summary>없으면 만든다. 빈 id 기록은 저장하지 않는다.</summary>
        public ExplorationProgressRecord GetRecord(string interactionId)
        {
            string key = interactionId ?? string.Empty;
            if (!_records.TryGetValue(key, out var record))
            {
                record = new ExplorationProgressRecord(key);
                _records[key] = record;
            }
            return record;
        }

        public void MarkCompleted(string interactionId, string choiceId, int day, int timeMinutes, ExplorationOutcomeCollector collector)
        {
            GetRecord(interactionId).MarkCompleted(choiceId, day, timeMinutes, collector);
            string summary = interactionId + ":" + choiceId;
            if (!string.IsNullOrEmpty(interactionId) && !_todayExplorationSummary.Contains(summary)) _todayExplorationSummary.Add(summary);
        }

        public ExplorationProgressSaveData ToSaveData()
        {
            var records = new List<ExplorationProgressRecordSaveData>(_records.Count);
            foreach (var pair in _records) if (!string.IsNullOrEmpty(pair.Key)) records.Add(pair.Value.ToSaveData());
            return new ExplorationProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, Records = records.ToArray(), TodayExplorationSummary = _todayExplorationSummary.ToArray() };
        }

        public static ExplorationProgress FromSaveData(ExplorationProgressSaveData saveData)
        {
            var progress = new ExplorationProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player");
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    var record = ExplorationProgressRecord.FromSaveData(saveData.Records[i]);
                    if (record.InteractionId.Length > 0) progress._records[record.InteractionId] = record;
                }
            }
            if (saveData.TodayExplorationSummary != null)
            {
                for (int i = 0; i < saveData.TodayExplorationSummary.Length; i++)
                {
                    string value = saveData.TodayExplorationSummary[i];
                    if (!string.IsNullOrEmpty(value) && !progress._todayExplorationSummary.Contains(value)) progress._todayExplorationSummary.Add(value);
                }
            }
            return progress;
        }
    }

    /// <summary>
    /// 탐색 진행의 읽기·쓰기 순서(원본은 ExplorationInteractionPointInteractor 안에 있었다). 파일 이름은 SaveFileNames, 형식(JSON)은 저장소가 맡는다.
    /// 기본 플레이어는 공용 파일 하나를 쓴다. 다른 플레이어는 새로 쓸 때 정리한 id 이름을 쓰고,
    /// 읽을 때 그 파일이 없으면 Unity 판의 정리하지 않은 이름을 찾는다.
    /// </summary>
    public static class ExplorationProgressPersistence
    {
        public static ExplorationProgress LoadOrCreate(ISaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? SaveFileNames.LegacyPlayerId : playerId;
            return store.TryReadWithLegacyName(slot, SaveFileNames.Exploration(player), SaveFileNames.LegacyExploration(player), out ExplorationProgressSaveData data)
                ? ExplorationProgress.FromSaveData(data)
                : new ExplorationProgress(slot, player);
        }

        public static void Save(ISaveStore store, ExplorationProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, SaveFileNames.Exploration(progress.PlayerId), progress.ToSaveData());
        }
    }
}
