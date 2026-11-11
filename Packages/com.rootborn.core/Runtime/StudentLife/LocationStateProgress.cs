using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.StudentLife
{
    // 세이브 모양: 원본과 필드 이름·형이 같다(옛 세이브를 읽는다).
    [Serializable]
    public sealed class LocationStateProgressSaveData : IVersionedSave
    {
        /// <summary>이 프로그램이 아는 가장 높은 형식 버전. 0은 Unity 판이 쓴 파일(Version 필드 없음)이다.</summary>
        public const int KnownVersion = 1;

        public int Version;
        public string SaveSlot; public string PlayerId; public LocationStateRecordSaveData[] Records = Array.Empty<LocationStateRecordSaveData>(); public LocationStateLastVisitSaveData[] LastVisitedStates = Array.Empty<LocationStateLastVisitSaveData>(); public LocationStateTodaySummarySaveData[] TodaySummary = Array.Empty<LocationStateTodaySummarySaveData>(); public string[] HiddenHintVisibleStateIds = Array.Empty<string>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }
    [Serializable] public sealed class LocationStateRecordSaveData { public string StateId; public bool Discovered; public int FirstDiscoveredDay; public string FirstDiscoveredTimeSlotId; public string[] RewardClaimedIds = Array.Empty<string>(); }
    [Serializable] public sealed class LocationStateLastVisitSaveData { public string LocationId; public string StateId; }
    [Serializable] public sealed class LocationStateTodaySummarySaveData { public string LocationId; public string StateId; public int Day; public string TimeSlotId; public string DisplayName; public bool Hidden; }

    /// <summary>장소 상태의 발견, 상태별 보상 수령, 장소별 마지막 상태, 오늘 본 상태, 숨은 힌트 표시.</summary>
    public sealed class LocationStateProgress
    {
        private sealed class Record
        {
            public string StateId;
            public bool Discovered;
            public int FirstDiscoveredDay;
            public string FirstDiscoveredTimeSlotId;
            public readonly HashSet<string> RewardClaimedIds = new HashSet<string>(StringComparer.Ordinal);
        }

        private readonly Dictionary<string, Record> _records = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _lastVisitedStateByLocation = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<LocationStateTodaySummarySaveData> _todaySummary = new List<LocationStateTodaySummarySaveData>();
        private readonly HashSet<string> _hiddenHintVisibleStateIds = new HashSet<string>(StringComparer.Ordinal);

        public LocationStateProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public LocationStateTodaySummarySaveData[] TodaySummary => _todaySummary.ToArray();

        public bool HasDiscovered(string stateId) => !string.IsNullOrEmpty(stateId) && _records.TryGetValue(stateId, out var record) && record.Discovered;
        public bool HasRewardClaimed(string stateId, string rewardId) => !string.IsNullOrEmpty(stateId) && !string.IsNullOrEmpty(rewardId) && _records.TryGetValue(stateId, out var record) && record.RewardClaimedIds.Contains(rewardId);
        public string GetLastVisitedStateId(string locationId) => !string.IsNullOrEmpty(locationId) && _lastVisitedStateByLocation.TryGetValue(locationId, out var stateId) ? stateId : string.Empty;
        public bool IsHiddenHintVisible(string stateId) => !string.IsNullOrEmpty(stateId) && _hiddenHintVisibleStateIds.Contains(stateId);

        public void MarkDiscovered(string locationId, string stateId, int day, string timeSlotId, string displayName, bool hidden)
        {
            if (string.IsNullOrEmpty(stateId)) return;
            var record = GetOrCreate(stateId);
            if (!record.Discovered)
            {
                record.Discovered = true;
                record.FirstDiscoveredDay = Math.Max(1, day);
                record.FirstDiscoveredTimeSlotId = timeSlotId ?? string.Empty;
            }
            if (!string.IsNullOrEmpty(locationId)) _lastVisitedStateByLocation[locationId] = stateId;
            AddToday(locationId, stateId, day, timeSlotId, displayName, hidden);
        }

        public bool MarkRewardClaimed(string stateId, string rewardId)
        {
            if (string.IsNullOrEmpty(stateId) || string.IsNullOrEmpty(rewardId)) return false;
            return GetOrCreate(stateId).RewardClaimedIds.Add(rewardId);
        }

        public void MarkHiddenHintVisible(string stateId)
        {
            if (!string.IsNullOrEmpty(stateId)) _hiddenHintVisibleStateIds.Add(stateId);
        }

        public LocationStateProgressSaveData ToSaveData()
        {
            var records = new List<LocationStateRecordSaveData>(_records.Count);
            foreach (var pair in _records)
            {
                var record = pair.Value;
                records.Add(new LocationStateRecordSaveData { StateId = record.StateId, Discovered = record.Discovered, FirstDiscoveredDay = record.FirstDiscoveredDay, FirstDiscoveredTimeSlotId = record.FirstDiscoveredTimeSlotId, RewardClaimedIds = ToArray(record.RewardClaimedIds) });
            }
            var visits = new List<LocationStateLastVisitSaveData>(_lastVisitedStateByLocation.Count);
            foreach (var pair in _lastVisitedStateByLocation) visits.Add(new LocationStateLastVisitSaveData { LocationId = pair.Key, StateId = pair.Value });
            return new LocationStateProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, Records = records.ToArray(), LastVisitedStates = visits.ToArray(), TodaySummary = _todaySummary.ToArray(), HiddenHintVisibleStateIds = ToArray(_hiddenHintVisibleStateIds) };
        }

        public static LocationStateProgress FromSaveData(LocationStateProgressSaveData saveData, string fallbackSaveSlot, string fallbackPlayerId)
        {
            var progress = new LocationStateProgress(saveData != null && !string.IsNullOrEmpty(saveData.SaveSlot) ? saveData.SaveSlot : fallbackSaveSlot,
                saveData != null && !string.IsNullOrEmpty(saveData.PlayerId) ? saveData.PlayerId : fallbackPlayerId);
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    var saved = saveData.Records[i];
                    if (saved == null || string.IsNullOrEmpty(saved.StateId)) continue;
                    var record = progress.GetOrCreate(saved.StateId);
                    record.Discovered = saved.Discovered;
                    record.FirstDiscoveredDay = Math.Max(0, saved.FirstDiscoveredDay);
                    record.FirstDiscoveredTimeSlotId = saved.FirstDiscoveredTimeSlotId ?? string.Empty;
                    AddRange(record.RewardClaimedIds, saved.RewardClaimedIds);
                }
            }
            if (saveData.LastVisitedStates != null)
            {
                for (int i = 0; i < saveData.LastVisitedStates.Length; i++)
                {
                    var saved = saveData.LastVisitedStates[i];
                    if (saved != null && !string.IsNullOrEmpty(saved.LocationId) && !string.IsNullOrEmpty(saved.StateId)) progress._lastVisitedStateByLocation[saved.LocationId] = saved.StateId;
                }
            }
            if (saveData.TodaySummary != null)
            {
                for (int i = 0; i < saveData.TodaySummary.Length; i++) if (saveData.TodaySummary[i] != null) progress._todaySummary.Add(saveData.TodaySummary[i]);
            }
            AddRange(progress._hiddenHintVisibleStateIds, saveData.HiddenHintVisibleStateIds);
            return progress;
        }

        private Record GetOrCreate(string stateId)
        {
            if (!_records.TryGetValue(stateId, out var record))
            {
                record = new Record { StateId = stateId };
                _records.Add(stateId, record);
            }
            return record;
        }

        private void AddToday(string locationId, string stateId, int day, string timeSlotId, string displayName, bool hidden)
        {
            for (int i = 0; i < _todaySummary.Count; i++) if (_todaySummary[i].StateId == stateId) return;
            _todaySummary.Add(new LocationStateTodaySummarySaveData { LocationId = locationId ?? string.Empty, StateId = stateId, Day = Math.Max(1, day), TimeSlotId = timeSlotId ?? string.Empty, DisplayName = displayName ?? string.Empty, Hidden = hidden });
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        private static void AddRange(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }
    }
}
