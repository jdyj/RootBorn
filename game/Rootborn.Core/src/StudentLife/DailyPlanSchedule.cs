using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    [Serializable]
    // 세이브 파일에 숫자로 쓰인다(DailyPlanSlotSaveData.MatchedActivityKind). 숫자를 바꾸지 않고 새 값은 끝에 더한다(SavedEnumValuesTests).
    public enum DailyPlanActivityKind { LocationActivity = 0, Practice = 1, PartTimeWork = 2 }

    /// <summary>세이브 파일에 쓰는 일과 칸 하나. 필드 이름은 기존 세이브 파일과 같아야 한다.</summary>
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

    /// <summary>세이브 파일에 쓰는 지난 하루의 일기 한 장.</summary>
    [Serializable]
    public sealed class DailyPlanDaySaveData
    {
        public int DayNumber;
        public DailyPlanSlotSaveData[] Slots = Array.Empty<DailyPlanSlotSaveData>();
        public string[] CompletionKeys = Array.Empty<string>();
    }

    /// <summary>오늘 일과의 칸 하나(시간대, 고른 길, 채웠는지, 채운 영수증).</summary>
    public sealed class DailyPlanSlotRecord
    {
        private string[] _resultLogs = Array.Empty<string>();

        internal DailyPlanSlotRecord(int index, string period) { SlotIndex = index; PeriodKey = period ?? string.Empty; }

        public int SlotIndex { get; }
        public string PeriodKey { get; internal set; }

        /// <summary>이 칸이 묶인 하루의 시간대(TimeSlotDefinition id, 흐르는 시간). 칸의 문구 키에서 얻는다(세이브 모양은 그대로). 모르면 빈 문자열.</summary>
        public string TimeSlotId => DailyPlanProgress.TimeSlotFor(PeriodKey);

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
            NextHintKey = NextHintKey, ResultLogs = (string[])_resultLogs.Clone(),
        };

        /// <summary>길이 없는 칸은 채운 것으로 되살리지 않는다. 칸 번호는 순서대로 다시 매긴다.</summary>
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

    /// <summary>지난 하루의 일기 한 장. 읽기만 한다.</summary>
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
