using System;
using System.Collections.Generic;

namespace Rootborn.Game.StudentLife
{
    [Serializable]
    public enum DailyPlanActivityKind { LocationActivity, Practice, PartTimeWork }

    [Serializable]
    public sealed class DailyPlanSlotSaveData
    {
        public int SlotIndex;
        public string PeriodKey;
        public string RouteId;
        public bool Completed;
        public string MatchedActivityId;
        public DailyPlanActivityKind MatchedActivityKind;
        public string CompletionRequestKey;
        public string NextHintKey;
        public string[] ResultLogs = Array.Empty<string>();
    }

    [Serializable]
    public sealed class DailyPlanDaySaveData
    {
        public int DayNumber;
        public DailyPlanSlotSaveData[] Slots = Array.Empty<DailyPlanSlotSaveData>();
        public string[] CompletionKeys = Array.Empty<string>();
    }

    public sealed class DailyPlanSlotRecord
    {
        private string[] _resultLogs = Array.Empty<string>();
        internal DailyPlanSlotRecord(int index, string period) { SlotIndex = index; PeriodKey = period ?? string.Empty; }
        public int SlotIndex { get; }
        public string PeriodKey { get; internal set; }
        public string RouteId { get; internal set; } = string.Empty;
        public bool Completed { get; internal set; }
        public string MatchedActivityId { get; internal set; } = string.Empty;
        public DailyPlanActivityKind MatchedActivityKind { get; internal set; }
        public string CompletionRequestKey { get; internal set; } = string.Empty;
        public string NextHintKey { get; internal set; } = string.Empty;
        public IReadOnlyList<string> ResultLogs => Array.AsReadOnly(_resultLogs);
        internal void SetResultLogs(string[] logs) => _resultLogs = logs == null ? Array.Empty<string>() : (string[])logs.Clone();
        internal DailyPlanSlotSaveData ToSaveData() => new DailyPlanSlotSaveData
        {
            SlotIndex = SlotIndex, PeriodKey = PeriodKey, RouteId = RouteId, Completed = Completed,
            MatchedActivityId = MatchedActivityId, MatchedActivityKind = MatchedActivityKind, CompletionRequestKey = CompletionRequestKey,
            NextHintKey = NextHintKey, ResultLogs = (string[])_resultLogs.Clone()
        };
        internal static DailyPlanSlotRecord FromSaveData(DailyPlanSlotSaveData data, int index)
        {
            var record = new DailyPlanSlotRecord(index, data?.PeriodKey);
            if (data == null) return record;
            record.RouteId = data.RouteId ?? string.Empty;
            record.Completed = data.Completed && !string.IsNullOrEmpty(record.RouteId);
            record.MatchedActivityId = record.Completed ? data.MatchedActivityId ?? string.Empty : string.Empty;
            record.MatchedActivityKind = data.MatchedActivityKind;
            record.CompletionRequestKey = record.Completed ? data.CompletionRequestKey ?? string.Empty : string.Empty;
            record.NextHintKey = data.NextHintKey ?? string.Empty;
            record.SetResultLogs(data.ResultLogs);
            return record;
        }
    }

    public sealed class DailyPlanDayRecord
    {
        private readonly DailyPlanSlotRecord[] _slots;
        private readonly string[] _completionKeys;
        private readonly IReadOnlyList<DailyPlanSlotRecord> _view;
        internal DailyPlanDayRecord(int day, DailyPlanSlotSaveData[] slots, string[] keys)
        {
            DayNumber = day;
            _slots = new DailyPlanSlotRecord[slots?.Length ?? 0];
            for (int i = 0; i < _slots.Length; i++) _slots[i] = DailyPlanSlotRecord.FromSaveData(slots[i], i);
            _view = Array.AsReadOnly(_slots);
            _completionKeys = keys == null ? Array.Empty<string>() : (string[])keys.Clone();
        }
        public int DayNumber { get; }
        public IReadOnlyList<DailyPlanSlotRecord> Slots => _view;
        internal DailyPlanDaySaveData ToSaveData()
        {
            var slots = new DailyPlanSlotSaveData[_slots.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = _slots[i].ToSaveData();
            return new DailyPlanDaySaveData { DayNumber = DayNumber, Slots = slots, CompletionKeys = (string[])_completionKeys.Clone() };
        }
    }
}
