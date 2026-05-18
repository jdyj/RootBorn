using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [Serializable]
    public sealed class DailyEventProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public Record[] Records = Array.Empty<Record>();

        [Serializable]
        public sealed class Record
        {
            public string EventId;
            public string State;
            public int FirstSeenDay;
            public int LastSeenDay;
            public int DueDay;
            public int DueWorldTime;
            public string SelectedChoiceId;
            public string CompletionRequestId;
            public int RepeatCount;
            public int CooldownUntilDay;
            public string[] ResultSummaryLogIds = Array.Empty<string>();
        }
    }

    public sealed class DailyEventRecord
    {
        private readonly List<string> _resultSummaryLogIds = new List<string>();

        public DailyEventRecord(string eventId)
        {
            EventId = string.IsNullOrEmpty(eventId) ? string.Empty : eventId;
            State = DailyEventStates.Unseen;
            SelectedChoiceId = string.Empty;
            CompletionRequestId = string.Empty;
        }

        public string EventId { get; }
        public string State { get; private set; }
        public int FirstSeenDay { get; private set; }
        public int LastSeenDay { get; private set; }
        public int DueDay { get; private set; }
        public int DueWorldTime { get; private set; }
        public string SelectedChoiceId { get; private set; }
        public string CompletionRequestId { get; private set; }
        public int RepeatCount { get; private set; }
        public int CooldownUntilDay { get; private set; }
        public string[] ResultSummaryLogIds => _resultSummaryLogIds.ToArray();

        public void MarkAvailable(int day)
        {
            int safeDay = Mathf.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            State = DailyEventStates.Available;
        }

        public void MarkDeferred(int currentDay, int dueDay)
        {
            int safeDay = Mathf.Max(1, currentDay);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            DueDay = Mathf.Max(safeDay, dueDay);
            State = DailyEventStates.Deferred;
        }

        public void MarkCompleted(string choiceId, string requestId, int day, string[] logs)
        {
            int safeDay = Mathf.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            SelectedChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
            CompletionRequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            State = DailyEventStates.Completed;
            RepeatCount++;
            _resultSummaryLogIds.Clear();
            AddLogs(logs);
        }

        public void MarkDeclined(int day)
        {
            int safeDay = Mathf.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            State = DailyEventStates.Declined;
        }

        public void MarkExpired(int day, int dueWorldTime)
        {
            int safeDay = Mathf.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            DueDay = safeDay;
            DueWorldTime = Mathf.Max(0, dueWorldTime);
            State = DailyEventStates.Expired;
        }

        public void MarkCooldown(int untilDay)
        {
            CooldownUntilDay = Mathf.Max(0, untilDay);
            State = DailyEventStates.Cooldown;
        }

        public bool HasCompletionRequest(string requestId)
        {
            return !string.IsNullOrEmpty(requestId) && CompletionRequestId == requestId;
        }

        public DailyEventProgressSaveData.Record ToSaveData()
        {
            return new DailyEventProgressSaveData.Record
            {
                EventId = EventId,
                State = State,
                FirstSeenDay = FirstSeenDay,
                LastSeenDay = LastSeenDay,
                DueDay = DueDay,
                DueWorldTime = DueWorldTime,
                SelectedChoiceId = SelectedChoiceId,
                CompletionRequestId = CompletionRequestId,
                RepeatCount = RepeatCount,
                CooldownUntilDay = CooldownUntilDay,
                ResultSummaryLogIds = _resultSummaryLogIds.ToArray(),
            };
        }

        public static DailyEventRecord FromSaveData(DailyEventProgressSaveData.Record saveData)
        {
            var record = new DailyEventRecord(saveData != null ? saveData.EventId : string.Empty);
            if (saveData == null) return record;
            record.State = string.IsNullOrEmpty(saveData.State) ? DailyEventStates.Unseen : saveData.State;
            record.FirstSeenDay = Mathf.Max(0, saveData.FirstSeenDay);
            record.LastSeenDay = Mathf.Max(0, saveData.LastSeenDay);
            record.DueDay = Mathf.Max(0, saveData.DueDay);
            record.DueWorldTime = Mathf.Max(0, saveData.DueWorldTime);
            record.SelectedChoiceId = string.IsNullOrEmpty(saveData.SelectedChoiceId) ? string.Empty : saveData.SelectedChoiceId;
            record.CompletionRequestId = string.IsNullOrEmpty(saveData.CompletionRequestId) ? string.Empty : saveData.CompletionRequestId;
            record.RepeatCount = Mathf.Max(0, saveData.RepeatCount);
            record.CooldownUntilDay = Mathf.Max(0, saveData.CooldownUntilDay);
            record.AddLogs(saveData.ResultSummaryLogIds);
            return record;
        }

        private void AddLogs(string[] logs)
        {
            if (logs == null) return;
            for (int i = 0; i < logs.Length; i++) if (!string.IsNullOrEmpty(logs[i]) && !_resultSummaryLogIds.Contains(logs[i])) _resultSummaryLogIds.Add(logs[i]);
        }
    }

    public sealed class DailyEventProgress
    {
        private readonly Dictionary<string, DailyEventRecord> _records = new Dictionary<string, DailyEventRecord>(StringComparer.Ordinal);

        public DailyEventProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }

        public DailyEventRecord GetRecord(string eventId)
        {
            string key = string.IsNullOrEmpty(eventId) ? string.Empty : eventId;
            if (!_records.TryGetValue(key, out var record))
            {
                record = new DailyEventRecord(key);
                _records[key] = record;
            }

            return record;
        }

        public bool HasCompletionRequest(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) return false;
            foreach (var pair in _records)
            {
                if (pair.Value != null && pair.Value.HasCompletionRequest(requestId)) return true;
            }

            return false;
        }

        public DailyEventProgressSaveData ToSaveData()
        {
            var records = new DailyEventProgressSaveData.Record[_records.Count];
            int index = 0;
            foreach (var pair in _records) records[index++] = pair.Value.ToSaveData();
            return new DailyEventProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, Records = records };
        }

        public static DailyEventProgress FromSaveData(DailyEventProgressSaveData saveData)
        {
            var progress = new DailyEventProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player");
            if (saveData == null || saveData.Records == null) return progress;
            for (int i = 0; i < saveData.Records.Length; i++)
            {
                var record = DailyEventRecord.FromSaveData(saveData.Records[i]);
                if (!string.IsNullOrEmpty(record.EventId)) progress._records[record.EventId] = record;
            }

            return progress;
        }
    }
}
